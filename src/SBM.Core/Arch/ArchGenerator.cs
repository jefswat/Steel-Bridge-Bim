using System.Diagnostics;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
using SBM.Core.Units;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Arch;

/// <summary>
/// Steel tied arch generator (pure function framing -> <see cref="ArchModel"/>): local structure frame, ribs, tie girders, hangers with sockets,
/// floorbeams, upper bracing, knuckles (simplified), deck slab on a corrugated form deck, curbs, bearings on the pier seats. Plan values are
/// built as given; every difference between plan values is reported (<see cref="ArchModel.Checks"/> / warnings), never adjusted.
/// </summary>
public static partial class ArchGenerator
{
    public static ArchModel Generate(BridgeFraming framing, UnitSystem units)
    {
        var g = new Gen(framing, units);
        var sw = Stopwatch.StartNew();
        g.Run();
        g.Model.Steel.GenerationTime = sw.Elapsed;
        return g.Model;
    }

    /// <summary>Frame of the local structure coordinate system (see <see cref="ArchFrameDefinition"/>); null + reason when it cannot be built.</summary>
    public static Frame3? BuildFrame(ArchFrameDefinition fr, Alignment al, Vec2? tiltAxis, out string error)
    {
        error = "";
        var p1 = al.PointAt(fr.Wp1Station, fr.Wp1Offset);
        var p2 = al.PointAt(fr.Wp2Station, fr.Wp2Offset);
        double dx = fr.Wp2LocalX - fr.Wp1LocalX;
        if ((p2 - p1).Length < 1e-6 || Math.Abs(dx) < 1e-6) { error = "the two arch work points coincide"; return null; }
        if (tiltAxis is not { } a) { error = $"tilt axis support '{fr.TiltAxisSupport}' is not a support of this bridge"; return null; }
        var dPlan = (p2 - p1).Normalized();
        var origin = p1 + (p2 - p1) * ((0 - fr.Wp1LocalX) / dx);
        var n = a.Normalized().Left;
        if (Vec2.Dot(n, dPlan) < 0) n = -n;
        double t = Numerics.DegToRad(fr.TiltDegrees);
        var z = new Vec3(n.X * Math.Sin(t), n.Y * Math.Sin(t), Math.Cos(t));
        var x = dPlan.WithZ(-Math.Tan(t) * Vec2.Dot(dPlan, n)).Normalized();
        var y = Vec3.Cross(z, x).Normalized();
        x = Vec3.Cross(y, z).Normalized();
        return new Frame3(origin.WithZ(fr.OriginElevation), x, y, z);
    }

    private sealed partial class Gen
    {
        private readonly BridgeFraming _f;
        private readonly BridgeDefinition _b;
        private readonly ArchDefinition _d;
        private readonly Alignment _al;
        private readonly UnitSystem _u;
        public ArchModel Model = null!;
        private Frame3 _fr;
        private readonly Dictionary<string, Vec3> _pts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TieGeo> _ties = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<ArchRibResult> _ribs = new();
        private readonly HashSet<string> _warned = new();

        public Gen(BridgeFraming f, UnitSystem u)
        {
            _f = f; _b = f.Definition; _d = f.Definition.Arch!; _al = f.Alignment; _u = u;
        }

        private string Src => string.IsNullOrWhiteSpace(_d.Source) ? "" : $" [{_d.Source}]";
        private void Warn(string m) { if (_warned.Add(m)) Model.Steel.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, m)); }
        private void Info(string m) => Model.Steel.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info, m));
        private void Error(string m) => Model.Steel.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, m));
        private string L(double m) => _u.FormatLength(m, LengthKind.Short);
        private static double ToIn(double m) => m / 0.0254;
        private static double ToFt(double m) => m / 0.3048;
        private const double Tol = 0.0254 / 16; // 1/16"

        private Vec3 W(Vec3 local) => _fr.ToWorld(local);
        private Vec3 Wd(Vec3 dir) => _fr.X * dir.X + _fr.Y * dir.Y + _fr.Z * dir.Z;

        public void Run()
        {
            var steel = new SteelModel { Bolt = new BoltGeometry(In(0.875)), BoltGrade = "A325", StudDiameter = In(0.875), StudHeadDiameter = In(1.375), StudHeadHeight = In(0.375) };
            var cm = new Concrete.ConcreteModel();
            var axis = _f.Supports.FirstOrDefault(s => string.Equals(s.Name, _d.Frame.TiltAxisSupport, StringComparison.OrdinalIgnoreCase))?.Direction;
            var frame = BuildFrame(_d.Frame, _al, axis, out string err);
            Model = new ArchModel { Frame = frame ?? Frame3.World, Steel = steel, Concrete = cm };
            steel.Arch = Model;
            if (frame == null) { Error($"Arch: local frame not built - {err}."); return; }
            _fr = frame.Value;
            foreach (var s in _d.Unresolved) Warn(s);
            FrameChecks();
            foreach (var p in _d.Points) _pts[p.Name] = p.Vec;
            foreach (var t in _d.Ties) BuildTie(t);
            foreach (var r in _d.Ribs) BuildRib(r);
            BuildHangers();
            foreach (var fb in _d.Floorbeams) BuildFloorbeam(fb);
            BuildBracing();
            foreach (var k in _d.Knuckles) { if (k.Detail is { } kd) BuildKnuckleV2(k, kd); else BuildKnuckle(k); }
            if (_d.Deck != null) BuildDeck(_d.Deck);
            for (int i = 0; i < _d.Bearings.Count; i++) BuildBearing(_d.Bearings[i], i);
        }

        private Vec3? P(string name)
        {
            if (_pts.TryGetValue(name, out var p)) return p;
            foreach (var t in _ties.Values)
                if (t.Def.PanelPoints.FirstOrDefault(q => string.Equals(q.Name, name, StringComparison.OrdinalIgnoreCase)) is { } pp)
                    return t.At(pp.Distance).p.WithZ(0);
            Warn($"Arch: point '{name}' is not defined - the members using it are not built.");
            return null;
        }

        private void AddPart(string mark, PartCategory cat, string asm, string desc, Mesh mesh, string material, double length,
            double lbPerFt = 0, Assumption? assumption = null)
        {
            if (mesh.IsEmpty) return;
            Model.Steel.Parts.Add(new SteelPart
            {
                Mark = mark, Category = cat, Assembly = asm, Description = desc, Mesh = mesh, Material = material, Length = length,
                NominalWeightLbPerFt = lbPerFt, Assumption = assumption,
                Role = cat == PartCategory.ArchBracing ? MemberRole.Lateral : MemberRole.None,
            });
            if (assumption != null) Warn(assumption.Reason);
        }

        private void AddAssembly(string mark, AssemblyKind kind, string desc)
        {
            if (Model.Steel.Assemblies.Any(a => a.Mark == mark)) return;
            Model.Steel.Assemblies.Add(new SteelAssembly { Mark = mark, Kind = kind, Description = desc });
        }

        private void Check(string item, double plan, double model, double tol, Func<double, string> fmt, string note = "")
        {
            double diff = model - plan;
            bool ok = Math.Abs(diff) <= tol;
            Model.Checks.Add(new ArchCheck(item, fmt(plan), fmt(model), diff, ok, note));
            if (!ok) Warn($"Arch CHECK {item}: plan {fmt(plan)}, model {fmt(model)} (difference {L(diff)}){(note == "" ? "" : " - " + note)}.");
        }

        // ------------------------------------------------------------------ frame

        private void FrameChecks()
        {
            var fr = _d.Frame;
            var p1 = _al.PointAt(fr.Wp1Station, fr.Wp1Offset);
            var p2 = _al.PointAt(fr.Wp2Station, fr.Wp2Offset);
            double planWp = (p2 - p1).Length;
            double planLocal = (fr.Wp2LocalX - fr.Wp1LocalX) * _fr.X.XY.Length;
            Check("chord CL1-CL2 in plan (local chord 201'-0 projected vs work points from the layout)", planLocal, planWp, Tol, v => $"{ToFt(v):F3}'",
                "the local geometry is kept; the work points only orient and place it (U3)");
            if (!double.IsNaN(fr.CheckWp1Elevation))
                Check("WP1 elevation (local plane vs printed)", fr.CheckWp1Elevation, W(new Vec3(fr.Wp1LocalX, 0, 0)).Z, Tol, v => $"{ToFt(v):F3}'");
            if (!double.IsNaN(fr.CheckWp2Elevation))
                Check("WP2 elevation (local plane vs printed)", fr.CheckWp2Elevation, W(new Vec3(fr.Wp2LocalX, 0, 0)).Z, Tol, v => $"{ToFt(v):F3}'");
        }

        // ------------------------------------------------------------------ ties

        private sealed class TieGeo
        {
            public required ArchTieDefinition Def;
            public Vec2 Start;
            public double Angle;
            public double Length => Def.Elements.Sum(e => e.Length);

            public (Vec2 p, double a) At(double s)
            {
                var p = Start;
                double a = Angle;
                foreach (var e in Def.Elements)
                {
                    double l = Math.Min(e.Length, Math.Max(0, s));
                    double k = e.Kind == ArchTieElementKind.Line ? 0 : (e.Kind == ArchTieElementKind.ArcLeft ? 1 : -1) / e.Radius;
                    p = Step(p, a, k, l);
                    a += k * l;
                    s -= e.Length;
                    if (s <= 0) break;
                }
                if (s > 0) p += Vec2.FromAngle(a) * s;
                return (p, a);
            }

            private static Vec2 Step(Vec2 p, double a, double k, double l) =>
                Math.Abs(k) < 1e-12 ? p + Vec2.FromAngle(a) * l
                    : p + new Vec2(Math.Sin(a + k * l) - Math.Sin(a), -(Math.Cos(a + k * l) - Math.Cos(a))) / k;

            public double Nearest(Vec2 q)
            {
                double best = 0, bd = double.MaxValue, len = Length;
                for (int i = 0; i <= 400; i++)
                {
                    double s = len * i / 400, dd = (At(s).p - q).Length;
                    if (dd < bd) { bd = dd; best = s; }
                }
                double lo = Math.Max(0, best - len / 400), hi = Math.Min(len, best + len / 400);
                for (int it = 0; it < 60; it++)
                {
                    double m1 = lo + (hi - lo) / 3, m2 = hi - (hi - lo) / 3;
                    if ((At(m1).p - q).Length < (At(m2).p - q).Length) hi = m2; else lo = m1;
                }
                return (lo + hi) / 2;
            }
        }

        private void BuildTie(ArchTieDefinition t)
        {
            if (P(t.Start) is not { } a || P(t.End) is not { } b) return;
            var geo = new TieGeo { Def = t, Start = a.XY };
            double chord = (b.XY - a.XY).Angle;
            double Miss(double ang) { geo.Angle = ang; return (geo.At(geo.Length).p - b.XY).Length; }
            double lo = chord - 0.6, hi = chord + 0.6;
            for (int i = 0; i < 200; i++)
            {
                double m1 = lo + (hi - lo) / 3, m2 = hi - (hi - lo) / 3;
                if (Miss(m1) < Miss(m2)) hi = m2; else lo = m1;
            }
            double miss = Miss((lo + hi) / 2);
            _ties[t.Name] = geo;
            Check($"{t.Name}: plan chain closes on {t.End}", 0, miss, Tol, L, "start direction solved for the closest closure");
            var panels = t.PanelPoints.Select(pp => (pp.Name, geo.At(pp.Distance).p.WithZ(0))).ToList();
            Model.Ties.Add(new ArchTieResult(t.Name, geo.Angle, geo.Length, miss, panels));

            string asm = t.Name.ToUpperInvariant().Replace(' ', '-');
            AddAssembly(asm, AssemblyKind.ArchTie, $"{t.Name}: built-up T {L(t.WebThickness)} x {L(t.WebHeight)} web + {L(t.FlangeThickness)} x {L(t.FlangeWidth)} flange, curved in plan");
            var stations = new List<double>();
            double acc = 0;
            foreach (var e in t.Elements)
            {
                int n = Math.Max(1, (int)Math.Ceiling(e.Length / 0.5));
                for (int i = 0; i < n; i++) stations.Add(acc + e.Length * i / n);
                acc += e.Length;
            }
            stations.Add(acc);
            Mesh Plate(double n0, double n1, double z0, double z1, double sa, double sb)
            {
                var sec = new[] { new Vec2(n0, z0), new Vec2(n1, z0), new Vec2(n1, z1), new Vec2(n0, z1) };
                var st = stations.Where(x => x > sa + 1e-6 && x < sb - 1e-6).Prepend(sa).Append(sb).ToList();
                var rings = st.Select(s =>
                {
                    var (p, ang) = geo.At(s);
                    var nl = Vec2.FromAngle(ang).Left;
                    return sec.Select(q => W((p + nl * q.X).WithZ(q.Y))).ToArray();
                }).ToList();
                var m = new Mesh();
                MeshBuilder.AddLoft(m, sec, rings);
                return m;
            }
            // Pieces between the field splices (gap at each), splice plates + bolts at each splice.
            var cuts = t.SpliceAt.Select(x => x.Distance).Where(x => x > 0 && x < geo.Length).OrderBy(x => x).ToList();
            // v2 knuckles: the tie field pieces start / end at the knuckle joints (end flange inner edge + half the joint gap).
            double trimA = KnuckleJoint(t, geo, true) is { } ja ? ja + (_d.Knuckles.FirstOrDefault(kk => kk.Detail != null)?.Detail?.JointGap ?? 0) / 2 : 0;
            double trimB = KnuckleJoint(t, geo, false) is { } jb ? geo.Length - jb - (_d.Knuckles.FirstOrDefault(kk => kk.Detail != null)?.Detail?.JointGap ?? 0) / 2 : geo.Length;
            var bounds = new List<double> { trimA };
            bounds.AddRange(cuts);
            bounds.Add(trimB);
            for (int k = 0; k + 1 < bounds.Count; k++)
            {
                double sa = bounds[k] + (k > 0 ? t.SpliceGap / 2 : 0), sb = bounds[k + 1] - (k + 2 < bounds.Count ? t.SpliceGap / 2 : 0);
                string pm = cuts.Count == 0 ? asm : $"{asm}-{k + 1}";
                string seg = cuts.Count == 0 ? "" : $", segment {k + 1}";
                AddPart($"{pm}-F", PartCategory.TiePlate, asm, $"PL {L(t.FlangeThickness)} x {L(t.FlangeWidth)} tie flange{seg}",
                    Plate(-t.FlangeWidth / 2, t.FlangeWidth / 2, 0, t.FlangeThickness, sa, sb), t.Material, sb - sa);
                AddPart($"{pm}-W", PartCategory.TiePlate, asm, $"PL {L(t.WebThickness)} x {L(t.WebHeight)} tie web{seg}",
                    Plate(-t.WebThickness / 2, t.WebThickness / 2, t.FlangeThickness, t.FlangeThickness + t.WebHeight, sa, sb), t.Material, sb - sa);
            }
            if (t.SpliceDetail is { } sd)
            {
                var asu = t.SpliceUnresolved.Length > 0 ? Assumption.Unresolved(t.SpliceUnresolved, "splices at the framing plan positions (mid panel)") : null;
                if (asu != null) Warn(t.SpliceUnresolved);
                if (!double.IsNaN(sd.CheckFlangePlateLength)) Check($"{t.Name}: splice flange plate length (edges + bolt spacing vs printed)", sd.CheckFlangePlateLength, sd.FlangePlateLength, Tol, L);
                if (!double.IsNaN(sd.CheckWebPlateLength)) Check($"{t.Name}: splice web plate length (edges + bolt spacing vs printed)", sd.CheckWebPlateLength, sd.WebPlateLength, Tol, L);
                foreach (var sp in t.SpliceAt.Where(x => x.Distance > 0 && x.Distance < geo.Length))
                {
                    double s0 = sp.Distance;
                    Vec3 Fm(double u, double w, double z) { var (p, a) = geo.At(s0 + u); return W((p + Vec2.FromAngle(a).Left * w).WithZ(z)); }
                    var g = new SpliceGeo(Fm, new() { (0.0, -1, t.FlangeThickness) }, (u, v, tt) => Fm(u, tt, v), t.WebThickness, t.FlangeThickness + t.WebHeight, t.FlangeThickness);
                    string sm = $"{asm}-{sp.Name}".ToUpperInvariant().Replace(' ', '-').Replace("(", "").Replace(")", "").Replace("'", "P");
                    AddAssembly(sm, AssemblyKind.ArchSplice, $"{t.Name} field splice {sp.Name}: {sd.Name}");
                    BuildSplice(sd, g, sm, asu);
                }
            }
        }

        // ------------------------------------------------------------------ field splices (rib / tie)

        /// <summary>Splice geometry of a member at a joint: flange map (u along the member from the joint, w across the flange, t normal to it),
        /// flanges (outside face t, outward sign, thickness), web map (u, v along the web depth, t across the web), web thickness and its two ends (v).</summary>
        private sealed record SpliceGeo(Func<double, double, double, Vec3> FlangeMap, List<(double Face, int Out, double Tf)> Flanges,
            Func<double, double, double, Vec3> WebMap, double Tw, double WebPlusEnd, double WebMinusEnd);

        private static List<Vec2> ClippedRect(double u0, double u1, double w0, double w1, double c)
        {
            if (c <= 0) return new() { new(u0, w0), new(u1, w0), new(u1, w1), new(u0, w1) };
            return new() { new(u0 + c, w0), new(u1 - c, w0), new(u1, w0 + c), new(u1, w1 - c), new(u1 - c, w1), new(u0 + c, w1), new(u0, w1 - c), new(u0, w0 + c) };
        }

        private static Frame3 BoltFrame(Func<double, double, double, Vec3> map, double u, double w, double tHead, double dir)
        {
            var o = map(u, w, tHead);
            var z = (map(u, w, tHead + dir * 1e-3) - o).Normalized();
            var x = (map(u + 1e-3, w, tHead) - o).Normalized();
            var y = Vec3.Cross(z, x).Normalized();
            x = Vec3.Cross(y, z).Normalized();
            return new Frame3(o, x, y, z);
        }

        /// <summary>Splice plates (with holes) and bolts of one joint (see <see cref="ArchSpliceDefinition"/>); returns the bolt count.</summary>
        private int BuildSplice(ArchSpliceDefinition d, SpliceGeo g, string asm, Assumption? asu)
        {
            var bolt = Model.Steel.Bolt;
            double hd = bolt.HoleDiameter;
            int nb = 0, np = 0;
            void Bolt(Func<double, double, double, Vec3> map, double u, double w, double tHead, double dir, double grip)
            {
                Model.Steel.Bolts.Add(new BoltInstance { Frame = BoltFrame(map, u, w, tHead, dir), Grip = grip, Length = bolt.LengthForGrip(grip), Assembly = asm });
                nb++;
            }
            void Plate(string what, List<Vec2> outline, List<Hole> holes, Func<double, double, double, Vec3> map, double t0, double t1, double len)
            {
                var m = new Mesh();
                double lo = Math.Min(t0, t1), hi = Math.Max(t0, t1);
                PlateBuilder.AddPrism(m, outline, holes, map, _ => lo, _ => hi);
                np++;
                Model.Steel.Parts.Add(new SteelPart { Mark = $"{asm}-P{np}", Category = PartCategory.SplicePlate, Assembly = asm, Description = what, Mesh = m,
                    Material = d.Material, Length = len, Assumption = asu });
            }
            // Columns (u from the joint) used by a row, both sides.
            IEnumerable<double> Us(ArchBoltRow r, double first, double pitch, int count)
            {
                for (int k = r.FirstColumn; k < count; k += Math.Max(1, r.ColumnStep))
                {
                    double u = first + k * pitch;
                    yield return -u; yield return u;
                }
            }
            // Flanges: outside plate (centred), inside plates each side of the web (outer edge flush with the outside plate edge).
            double lf = d.FlangePlateLength / 2, wo = d.FlangeOuterWidth / 2;
            if (d.FlangeBoltColumns > 0 && d.FlangeOuterWidth > 0)
                foreach (var (face, outw, tf) in g.Flanges)
                {
                    var holesOuter = new List<Hole>();
                    var holesIn = new[] { new List<Hole>(), new List<Hole>() };
                    double grip = d.FlangeOuterThickness + tf + d.FlangeInnerThickness;
                    foreach (var r in d.FlangeRows)
                        foreach (int side in new[] { 1, -1 })
                        {
                            double w = side * (wo - r.Gage);
                            foreach (var u in Us(r, d.FlangeBoltFirst, d.FlangeBoltPitch, d.FlangeBoltColumns))
                            {
                                holesOuter.Add(new Hole(new Vec2(u, w), hd));
                                holesIn[side > 0 ? 0 : 1].Add(new Hole(new Vec2(u, w), hd));
                                Bolt(g.FlangeMap, u, w, face + outw * d.FlangeOuterThickness, -outw, grip);
                            }
                        }
                    Plate($"PL {L(d.FlangeOuterThickness)} x {L(d.FlangeOuterWidth)} x {L(2 * lf)} outside flange splice plate", ClippedRect(-lf, lf, -wo, wo, d.FlangeClip), holesOuter,
                        g.FlangeMap, face, face + outw * d.FlangeOuterThickness, 2 * lf);
                    if (d.FlangeInnerWidth > 0)
                        for (int side = 0; side < 2; side++)
                        {
                            double w0 = side == 0 ? wo - d.FlangeInnerWidth : -wo, w1 = side == 0 ? wo : -wo + d.FlangeInnerWidth;
                            double ti = face - outw * tf;
                            Plate($"PL {L(d.FlangeInnerThickness)} x {L(d.FlangeInnerWidth)} x {L(2 * lf)} inside flange splice plate", ClippedRect(-lf, lf, w0, w1, 0), holesIn[side],
                                g.FlangeMap, ti, ti - outw * d.FlangeInnerThickness, 2 * lf);
                        }
                }
            // Web plates (both faces).
            if (d.WebBoltColumns > 0 && d.WebPlateWidth > 0)
            {
                double lw = d.WebPlateLength / 2;
                double vTop = double.IsNaN(d.WebPlateEdgeFromWebEnd) ? (g.WebPlusEnd + g.WebMinusEnd) / 2 + d.WebPlateWidth / 2 : g.WebPlusEnd - d.WebPlateEdgeFromWebEnd;
                var holes = new List<Hole>();
                double grip = 2 * d.WebPlateThickness + g.Tw;
                foreach (var r in d.WebRows)
                    foreach (var u in Us(r, d.WebBoltFirst, d.WebBoltPitch, d.WebBoltColumns))
                    {
                        holes.Add(new Hole(new Vec2(u, vTop - r.Gage), hd));
                        Bolt(g.WebMap, u, vTop - r.Gage, g.Tw / 2 + d.WebPlateThickness, -1, grip);
                    }
                foreach (int side in new[] { 1, -1 })
                    Plate($"PL {L(d.WebPlateThickness)} x {L(d.WebPlateWidth)} x {L(2 * lw)} web splice plate{(d.WebPlateNote.Length > 0 ? " (" + d.WebPlateNote + ")" : "")}",
                        ClippedRect(-lw, lw, vTop - d.WebPlateWidth, vTop, d.WebClip), holes, g.WebMap, side * g.Tw / 2, side * (g.Tw / 2 + d.WebPlateThickness), 2 * lw);
            }
            Model.SpliceBolts[asm] = nb;
            return nb;
        }

        // ------------------------------------------------------------------ ribs

        private void BuildRib(ArchRibDefinition r)
        {
            if (P(r.ChordStart) is not { } a || P(r.ChordEnd) is not { } b) return;
            var w = SteelCatalog.FindWideFlange(r.Section);
            if (w == null) { Warn($"Arch: {r.Name} section '{r.Section}' is not in the AISC catalog - rib not built."); return; }
            var c = (b - a).Normalized();
            var u0 = new Vec3(0, 0, 1);
            u0 = (u0 - c * Vec3.Dot(c, u0)).Normalized();
            var wv = Vec3.Cross(c, u0);
            if (wv.Y < 0) wv = -wv;
            double inc = Numerics.DegToRad(Math.Abs(r.InclinationDegrees)) * Math.Sign(r.InclinationDegrees);
            var up = (u0 * Math.Cos(inc) + wv * Math.Sin(inc)).Normalized();
            double h = (b - a).Length / 2;
            if (r.Radius <= h) { Warn($"Arch: {r.Name} radius {L(r.Radius)} is smaller than half the chord - rib not built."); return; }
            double sag = Math.Sqrt(r.Radius * r.Radius - h * h);
            var mid = (a + b) * 0.5;
            var center = mid - up * sag;
            double Theta(Vec3 p) { var d = p - center; return Math.Atan2(Vec3.Dot(d, up), Vec3.Dot(d, c)); }
            double t0 = Theta(a), t1 = Theta(b);
            var res = new ArchRibResult(r.Name, center, c, up, r.Radius, r.Radius - sag, r.Radius * Math.Abs(t0 - t1), t0, t1);
            _ribs.Add(res);
            Model.Ribs.Add(res);
            if (!double.IsNaN(r.Rise)) Check($"{r.Name}: rise (from chord {L(2 * h)} and R {L(r.Radius)})", r.Rise, res.Rise, Tol, L);

            var binormal = Vec3.Cross(c, up).Normalized();
            var sec = SteelCatalog.WideFlangePolygon(w, -w.WebThickness / 2);
            // Splice angles (points projected on the circle) and their distance from it.
            var cuts = new List<double>();
            foreach (var sn in r.Splices)
            {
                if (P(sn) is not { } sp) continue;
                double th = Theta(sp);
                var onCircle = res.PointAt(th);
                double off = (sp - onCircle).Length;
                if (off > Tol) Warn($"Arch CHECK {r.Name}: splice {sn} is {L(off)} off the rib circle.");
                cuts.Add(th);
            }
            cuts.Sort((x, y) => y.CompareTo(x)); // from t0 (start, larger angle) down to t1
            double gap = r.SpliceGap / r.Radius / 2;
            double tStart = t0, tEnd = t1;
            if (!double.IsNaN(r.StubBottomZ))
            {
                // Rib end where the WP reaches the given local Z (above the chord): sin(th) = (z - Cz) / (R up.Z) with c.Z = 0.
                double s = (r.StubBottomZ - center.Z) / (r.Radius * up.Z);
                if (Math.Abs(s) <= 1) { double asn = Math.Asin(s); tStart = Math.PI - asn; tEnd = asn; }
            }
            var bounds = new List<double> { tStart };
            bounds.AddRange(cuts);
            bounds.Add(tEnd);
            string asm = r.Name.ToUpperInvariant().Replace(' ', '-');
            AddAssembly(asm, AssemblyKind.ArchRib, $"{r.Name}: {w.Name} bent about the weak axis, R {L(r.Radius)}, inclined {Math.Abs(r.InclinationDegrees):0.###} deg");
            for (int k = 0; k + 1 < bounds.Count; k++)
            {
                double ta = bounds[k] - (k > 0 ? gap : 0), tb = bounds[k + 1] + (k + 2 < bounds.Count ? gap : 0);
                int n = Math.Max(2, (int)Math.Ceiling(Math.Abs(ta - tb) / Numerics.DegToRad(1.0)));
                var rings = new List<Vec3[]>();
                for (int i = 0; i <= n; i++)
                {
                    double th = ta + (tb - ta) * i / n;
                    var p = res.PointAt(th);
                    var rad = (p - center).Normalized();
                    rings.Add(sec.Select(q => W(p + binormal * q.X + rad * q.Y)).ToArray());
                }
                var m = new Mesh();
                MeshBuilder.AddLoft(m, sec, rings);
                double len = r.Radius * Math.Abs(ta - tb);
                bool hasStubs = !double.IsNaN(r.StubBottomZ) && cuts.Count >= 2;
                bool stub = hasStubs && (k == 0 || k == bounds.Count - 2);
                if (stub && _d.Knuckles.Any(kk => kk.Detail != null && kk.Corners.Any(cc => string.Equals(cc.Rib, r.Name, StringComparison.OrdinalIgnoreCase)))) continue;   // v2 knuckle builds the stub
                if (stub)
                {
                    string km = $"{asm}-STUB{(k == 0 ? 1 : 2)}";
                    var asu = r.StubUnresolved.Length > 0 ? Assumption.Unresolved(r.StubUnresolved, $"{w.Name} envelope from the knuckle to the end splice") : null;
                    AddPart(km, PartCategory.KnucklePlate, asm, $"Knuckle arch stub ({w.Name} envelope)", m, "ASTM A709 Gr. 50", len, 0, asu);
                }
                else AddPart($"{asm}-{k + (hasStubs ? 0 : 1)}", PartCategory.ArchRib, asm, $"{w.Name} rib segment", m, r.Material, len, w.WeightPerFootLb);
            }
            if (r.SpliceDetail is { } sd)
                foreach (var sn in r.Splices)
                {
                    if (P(sn) is not { } sp) continue;
                    double thc = Theta(sp);
                    Vec3 Dir(double th) => c * Math.Cos(th) + up * Math.Sin(th);
                    // Splice plates are flat in the plane tangent to the rib at the joint (19 1/2" / 27 1/2" long on R 126'-0: <= 1/16" off the curve).
                    // u along the tangent from the joint (toward the chord end), w / t radial, binormal.
                    var radC = Dir(thc); var tanC = c * Math.Sin(thc) - up * Math.Cos(thc);
                    Vec3 Fm(double u, double wr, double tb) => W(center + radC * (r.Radius + wr) + tanC * u + binormal * tb);
                    Vec3 Wm(double u, double vb, double tr) => W(center + radC * (r.Radius + tr) + tanC * u + binormal * vb);
                    double h2 = w.Depth / 2;
                    var g = new SpliceGeo(Fm, new() { (h2, 1, w.FlangeThickness), (-h2, -1, w.FlangeThickness) }, Wm, w.WebThickness, h2 - w.FlangeThickness, -h2 + w.FlangeThickness);
                    string sm = $"{asm}-{sn}".ToUpperInvariant().Replace("'", "P");
                    AddAssembly(sm, AssemblyKind.ArchSplice, $"{r.Name} field splice {sn}: {sd.Name}");
                    BuildSplice(sd, g, sm, null);
                }
        }

        // ------------------------------------------------------------------ hangers

        private void BuildHangers()
        {
            var hs = _d.HangerSystem;
            foreach (var h in _d.Hangers)
            {
                var tc = h.TieAnchor.Vec;
                var hc = h.RibAnchor.Vec;
                double len = (hc - tc).Length;
                var wt = W(tc);
                var wr = W(hc);
                // Plan length: to the rib working-line point when given (p.147 "cable length TC-HC"), else pin to pin.
                var wp = h.RibWorkPoint?.Vec;
                double lenPlan = wp is { } q ? (q - tc).Length : len;
                Model.Hangers.Add(new ArchHangerResult(h.Name, wt, wr, len, h.PlanLength) { RibWorkPointWorld = wp is { } q2 ? W(q2) : null, PlanBasisLength = lenPlan });
                if (!double.IsNaN(h.PlanLength))
                    Check(wp == null ? $"hanger {h.Name}: pin-to-pin length" : $"hanger {h.Name}: length tie pin to the rib working-line point (p.147 column)", h.PlanLength, lenPlan, Tol, L,
                        wp == null ? "" : $"pin to pin {L(len)}");
                if (wp is { } w2)
                {
                    // The rib pin lies on the cable line (TC -> working-line point).
                    var ax = (w2 - tc).Normalized();
                    double off = (hc - tc - ax * Vec3.Dot(hc - tc, ax)).Length;
                    if (off > Tol) Warn($"Arch CHECK hanger {h.Name}: rib pin {L(off)} off the line tie pin - rib working-line point.");
                }
                var dir = (wr - wt).Normalized();
                string asm = $"HANGER-{h.Name}";
                AddAssembly(asm, AssemblyKind.ArchHanger, $"Hanger {h.Name}: {L(hs.StrandDiameter)} structural strand, {h.Rib}");
                double wTie = hs.TieSocket?.W ?? 0, wRib = hs.RibSocket?.W ?? 0;
                var s0 = wt + dir * wTie;
                var s1 = wr - dir * wRib;
                if ((s1 - s0).Length > 0.01 && Vec3.Dot(s1 - s0, dir) > 0)
                {
                    var m = new Mesh();
                    RolledBuilder.AddTube(m, new[] { s0, s1 }, hs.StrandDiameter / 2, 16);
                    AddPart($"{asm}-S", PartCategory.Hanger, asm, $"{L(hs.StrandDiameter)} strand{(hs.StrandDescription == "" ? "" : ", " + hs.StrandDescription)}",
                        m, hs.StrandMaterial, (s1 - s0).Length, hs.StrandWeightLbPerFt);
                }
                // Fin plate planes: the cable and the member tangent.
                Vec3 tieTan = _fr.X;
                if (_ties.TryGetValue(h.Tie, out var tg)) { var (_, ang) = tg.At(tg.Nearest(tc.XY)); tieTan = Wd(Vec2.FromAngle(ang).WithZ(0)); }
                Vec3 ribTan = _fr.X;
                if (_ribs.FirstOrDefault(r => string.Equals(r.Name, h.Rib, StringComparison.OrdinalIgnoreCase)) is { } rr)
                {
                    var d = hc - rr.Center;
                    double th = Math.Atan2(Vec3.Dot(d, rr.Up), Vec3.Dot(d, rr.ChordDir));
                    ribTan = Wd(rr.ChordDir * -Math.Sin(th) + rr.Up * Math.Cos(th));
                }
                if (hs.TieSocket is { } ts) Socket(ts, asm, "TS", wt, dir, tieTan);
                if (hs.RibSocket is { } rs) Socket(rs, asm, "RS", wr, -dir, ribTan);
                if (_d.Fins is { } fs)
                {
                    if (h.RibFin is { } rf && _ribs.FirstOrDefault(r => string.Equals(r.Name, h.Rib, StringComparison.OrdinalIgnoreCase)) is { } rres)
                        RibFinPlate(h, rf, fs, rres);
                    if (h.TieFin is { } tf && _ties.TryGetValue(h.Tie, out var tgeo)) TieFinPlate(h, tf, fs, tgeo);
                }
            }
            BuildConnectors();
        }

        // ------------------------------------------------------------------ cable fin plates (rib p.148, tie p.149)

        private string RibAsm(string ribName) => ribName.ToUpperInvariant().Replace(' ', '-');

        /// <summary>Rib fin plate: plane = rib binormal x the cable projected on the arch plane; between the flange inner faces; top P4 below the rib
        /// CL (web plane), straight sides P3, then tangent to R around the pin (HC); 1.378" pin hole. Plan P1 / P2 / B are checked against the model.</summary>
        private void RibFinPlate(ArchHangerDefinition h, ArchRibFin rf, ArchFinSettings fs, ArchRibResult rib)
        {
            var w = SteelCatalog.FindWideFlange(_d.Ribs.FirstOrDefault(r => r.Name == rib.Name)?.Section ?? "W14x132");
            if (w == null) return;
            var hc = h.RibAnchor.Vec; var tc = h.TieAnchor.Vec;
            var bn = Vec3.Cross(rib.ChordDir, rib.Up).Normalized();
            var dc = (tc - hc).Normalized();
            var e = -(dc - bn * Vec3.Dot(dc, bn)).Normalized();          // in the arch plane, from the pin toward the rib
            var nf = Vec3.Cross(bn, e).Normalized();
            // Distance pin -> rib web (WP circle) along e.
            var d0 = hc - rib.Center;
            double bq = Vec3.Dot(d0, e), cq = Vec3.Dot(d0, d0) - Vec3.Dot(d0, bn) * Vec3.Dot(d0, bn) - rib.Radius * rib.Radius;
            // |d0 - bn(d0.bn) + e y|^2 = R^2 (in-plane distance; e is in the arch plane)
            double disc = bq * bq - cq;
            if (disc < 0) { Warn($"Arch: fin plate {h.Name} - the cable does not reach the rib web along the fin; not built."); return; }
            double yWeb = -bq + Math.Sqrt(disc);
            if (yWeb < 0) yWeb = -bq - Math.Sqrt(disc);
            double p1 = Vec3.Dot(d0, bn);
            var hitP = hc + e * yWeb;
            var rad = (hitP - rib.Center - bn * Vec3.Dot(hitP - rib.Center, bn)).Normalized();
            var tan = Vec3.Cross(bn, rad).Normalized();
            if (Vec3.Dot(tan, rib.ChordDir) < 0) tan = -tan;                 // rib tangent toward the chord end (p.148 angle B)
            double bModel = Math.Acos(Math.Clamp(Vec3.Dot(tan, -e), -1, 1)) * 180 / Math.PI;
            double perp = yWeb * Math.Abs(Vec3.Dot(e, rad));
            string nm = $"fin plate {h.Name} at rib";
            if (!double.IsNaN(rf.AngleB)) Check($"{nm}: angle B (rib tangent to the cable)", rf.AngleB, bModel, 0.05, v => $"{v:F3} deg", "p.148 table vs the p.147 HC / TC points and the rib circle");
            if (!double.IsNaN(rf.P1)) Check($"{nm}: |P1| (pin off the arch plane)", Math.Abs(rf.P1), Math.Abs(p1), Tol, L);
            if (!double.IsNaN(rf.P2)) Check($"{nm}: P2 (pin to the rib working line along the fin)", rf.P2, yWeb, Tol, L, $"pin {L(perp)} normal to the rib working line");
            double c = w.Depth / 2 - w.FlangeThickness;
            double yTop = yWeb - rf.P4, ySb = yTop - rf.P3, r = fs.RibPinRadius, clip = fs.RibTopClip;
            // Outline in (x across from the pin = binormal, y toward the rib from the pin).
            double xl = -c - p1, xr = c - p1;
            (double a, double b) Tang(double qx, double qy, bool right)
            {
                double phi = Math.Atan2(qy, qx), dd = Math.Sqrt(qx * qx + qy * qy), al = dd > r ? Math.Acos(r / dd) : 0;
                return right ? (phi - al, 0) : (phi + al, 0);
            }
            var outl = new List<Vec2> { new(xl, yTop - clip), new(xl + clip, yTop), new(xr - clip, yTop), new(xr, yTop - clip), new(xr, ySb) };
            double ta = Tang(xr, ySb, true).a, tb = Tang(xl, ySb, false).a;
            while (tb > ta) tb -= 2 * Math.PI;
            int ns = 16;
            for (int i = 0; i <= ns; i++) { double th = ta + (tb - ta) * i / ns; outl.Add(new Vec2(r * Math.Cos(th), r * Math.Sin(th))); }
            outl.Add(new Vec2(xl, ySb));
            if (MeshBuilder.SignedArea(outl) < 0) outl.Reverse();
            var m = new Mesh();
            PlateBuilder.AddPrism(m, outl, new[] { new Hole(Vec2.Zero, fs.PinHole) }, (u, v, t) => W(hc + bn * u + e * v + nf * t), _ => -fs.RibThickness / 2, _ => fs.RibThickness / 2);
            var asu = fs.RibUnresolved.Length > 0 ? Assumption.Unresolved(fs.RibUnresolved, $"PL {L(fs.RibThickness)}, top clips {L(clip)}") : null;
            AddPart($"{RibAsm(h.Rib)}-FIN-{h.Name}", PartCategory.FinPlate, RibAsm(h.Rib),
                $"PL {L(fs.RibThickness)} cable fin plate at rib, {h.Name} (P2 {L(yWeb)}, P3 {L(rf.P3)}, P4 {L(rf.P4)}{(double.IsNaN(rf.WeldLength) ? "" : $", weld D {L(rf.WeldLength)}")})",
                m, fs.Material, yWeb - rf.P4 + r, 0, asu);
        }

        /// <summary>Tie fin plate: lower part on the outside face of the tie web, centred on the floorbeam (panel point), bolted through the web; bend
        /// line along the tie above the web; upper part turned about it to contain the pin (TC). Plan angle A / orientation / pin height are checked.</summary>
        private void TieFinPlate(ArchHangerDefinition h, ArchTieFin tf, ArchFinSettings fs, TieGeo tg)
        {
            var pp = tg.Def.PanelPoints.FirstOrDefault(p => string.Equals(p.Name, tf.Panel, StringComparison.OrdinalIgnoreCase));
            if (pp == null) { Warn($"Arch: fin plate {h.Name} - panel point '{tf.Panel}' is not on {tg.Def.Name}; not built."); return; }
            var tc = h.TieAnchor.Vec;
            var (p2, ang) = tg.At(pp.Distance);
            var t2 = Vec2.FromAngle(ang); var nl = t2.Left;
            // Outside face (p.146 / p.149: the fin is on the outside of the tie web, the floorbeam inside): away from the other tie.
            var other = _ties.Values.FirstOrDefault(o => o != tg);
            Vec2 inward = other != null ? other.At(other.Nearest(p2)).p - p2 : -p2;
            int side = Vec2.Dot(inward, nl) > 0 ? -1 : 1;
            double latTc = Vec2.Dot(tc.XY - p2, nl * side);                      // + = outside
            var def = tg.Def;
            double off = def.WebThickness / 2 + fs.TieThickness / 2;
            double zb = def.FlangeThickness + def.WebHeight + fs.TieBendAboveWeb;
            var t3 = t2.WithZ(0); var n3 = (nl * side).WithZ(0);
            var bend0 = (p2 + nl * side * off).WithZ(zb);                        // bend line point at the panel point
            var q = tc - bend0;
            double sPin = Vec3.Dot(q, t3);
            var qn = q - t3 * sPin;
            double vPin = qn.Length;
            var v3 = qn.Normalized();
            double aModel = Math.Acos(Math.Clamp(Vec3.Dot(v3, -n3), -1, 1)) * 180 / Math.PI;   // from the horizontal toward the tie CL
            int sg = sPin >= 0 ? 1 : -1;
            var cable = (h.RibAnchor.Vec - tc).Normalized();
            var cp = (cable - t3 * Vec3.Dot(cable, t3)).Normalized();
            double aCable = Math.Acos(Math.Clamp(Vec3.Dot(cp, -n3), -1, 1)) * 180 / Math.PI;
            string nm = $"fin plate {h.Name} at tie";
            if (!double.IsNaN(tf.AngleA)) Check($"{nm}: angle A (cable lean across the tie)", tf.AngleA, aCable, 0.05, v => $"{v:F3} deg",
                $"p.149 table vs the cable TC -> rib pin; the plate bend -> TC is {aModel:F3} deg (built: the plate bends at the plan line and reaches the pin)");
            Check($"{nm}: pin above the bend line in the plate plane", fs.TiePinAboveBend, vPin, Tol, L, "p.149 4\" vs TC Z (p.147) - tie WP - 11 7/8 - 1/2 (U10)");
            Check($"{nm}: pin off the floorbeam CL along the tie", fs.TieWidth / 2 - fs.TiePinFromEdge, Math.Abs(sPin), Tol, L, "p.149 6 1/8 / 1 7/8 on the 8\" plate centred on the floorbeam (p.146)");
            string orientModel = sg > 0 ? "RIGHT" : "LEFT";
            bool towardCable = (Vec3.Dot(cable, t3) >= 0) == (sg > 0);
            if (tf.Orient.Length > 0)
            {
                bool ok = string.Equals(orientModel, tf.Orient, StringComparison.OrdinalIgnoreCase) && towardCable;
                Model.Checks.Add(new ArchCheck($"{nm}: orientation", tf.Orient, $"{orientModel} (pin on the up-chain side = RIGHT){(towardCable ? "" : ", pin AWAY from the cable lean")}", 0, ok, "p.149 table vs TC relative to the panel point"));
                if (!ok) Warn($"Arch CHECK {nm}: orientation plan {tf.Orient}, model {orientModel}.");
            }
            double latPlan = off + (fs.TiePinAboveBend * Math.Cos(tf.AngleA * Math.PI / 180) * -1);
            if (!double.IsNaN(tf.AngleA)) Check($"{nm}: pin (TC) off the tie CL, + outside", latPlan, latTc, Tol, L, $"p.149: 7/8\" + 4\" x cos(A) toward the tie CL; TC Z {L(tc.Z)}");
            // Lower part (plane parallel to the web), (u along the tie from the panel point, z).
            double hw = fs.TieWidth / 2;
            var lower = new List<Vec2> { new(-hw, zb - fs.TieDepthBelowBend), new(hw, zb - fs.TieDepthBelowBend), new(hw, zb), new(-hw, zb) };
            var holes = new List<Hole>();
            double hx = hw - fs.TieHoleEdge;
            foreach (var rz in fs.TieHoleRows) foreach (var sx in new[] { -hx, hx }) holes.Add(new Hole(new Vec2(sx, zb - rz), Model.Steel.Bolt.HoleDiameter));
            Vec3 LowerMap(double u, double z, double t) => W((p2 + t2 * u + nl * side * (off + t)).WithZ(z));
            var m = new Mesh();
            PlateBuilder.AddPrism(m, lower, holes, LowerMap, _ => -fs.TieThickness / 2, _ => fs.TieThickness / 2);
            // Upper part (u along the tie, v up the plate from the bend line), CCW for sg = +1, mirrored otherwise.
            double r = fs.TiePinRadius, sp = Math.Abs(sPin);
            var up = new List<Vec2> { new(-hw, 0), new(hw, 0) };
            double dxr = hw - sp;
            double ta = Math.Abs(dxr) < r ? Math.Acos(dxr / r) : 0;
            up.Add(new Vec2(hw, vPin + r * Math.Sin(ta)));
            var F = new Vec2(-hw, fs.TieFarEdgeAboveBend) - new Vec2(sp, vPin);
            double phi = Math.Atan2(F.Y, F.X); if (phi < 0) phi += 2 * Math.PI;
            double tb = phi - (F.Length > r ? Math.Acos(r / F.Length) : 0);
            for (int i = 1; i <= 14; i++) { double th = ta + (tb - ta) * i / 14; up.Add(new Vec2(sp + r * Math.Cos(th), vPin + r * Math.Sin(th))); }
            up.Add(new Vec2(-hw, fs.TieFarEdgeAboveBend));
            if (sg < 0) { up = up.Select(qq => new Vec2(-qq.X, qq.Y)).Reverse().ToList(); }
            var nUp = Vec3.Cross(t3, v3).Normalized();
            Vec3 UpperMap(double u, double v, double t) => W(bend0 + t3 * u + v3 * v + nUp * t);
            var mu = new Mesh();
            PlateBuilder.AddPrism(mu, up, new[] { new Hole(new Vec2(sPin, vPin), fs.PinHole) }, UpperMap, _ => -fs.TieThickness / 2, _ => fs.TieThickness / 2);
            string asm = def.Name.ToUpperInvariant().Replace(' ', '-');
            // Bent plate as two solids (legs below / above the bend line) so each IFC body is closed.
            AddPart($"{asm}-FIN-{h.Name}", PartCategory.FinPlate, asm, $"PL {L(fs.TieThickness)} bent cable fin plate at tie, {h.Name} at {tf.Panel}, lower leg (bolted)", m, fs.Material, fs.TieDepthBelowBend);
            AddPart($"{asm}-FIN-{h.Name}-U", PartCategory.FinPlate, asm, $"PL {L(fs.TieThickness)} bent cable fin plate at tie, {h.Name} at {tf.Panel}, upper leg (A {aModel:F2} deg, pin)", mu, fs.Material, vPin + r);
            // Bolts through the fin and the tie web (head on the fin).
            double grip = fs.TieThickness + def.WebThickness;
            foreach (var hl in holes)
                Model.Steel.Bolts.Add(new BoltInstance { Frame = BoltFrame(LowerMap, hl.Center.X, hl.Center.Y, fs.TieThickness / 2, -1), Grip = grip, Length = Model.Steel.Bolt.LengthForGrip(grip), Assembly = asm });
        }

        // ------------------------------------------------------------------ cable connectors (p.150)

        private void BuildConnectors()
        {
            if (_d.CableConnectorDetail is not { } cd || _d.CableConnectors.Count == 0) return;
            var byName = _d.Hangers.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var cc in _d.CableConnectors)
            {
                if (!byName.TryGetValue(cc.CableA, out var ha) || !byName.TryGetValue(cc.CableB, out var hb)) { Warn($"Arch: cable connector {cc.Name} - hanger not found."); continue; }
                Vec3 a0 = ha.TieAnchor.Vec, a1 = ha.RibAnchor.Vec, b0 = hb.TieAnchor.Vec, b1 = hb.RibAnchor.Vec;
                var u = a1 - a0; var v = b1 - b0; var w0 = a0 - b0;
                double A = Vec3.Dot(u, u), B = Vec3.Dot(u, v), C = Vec3.Dot(v, v), D = Vec3.Dot(u, w0), E = Vec3.Dot(v, w0), den = A * C - B * B;
                if (Math.Abs(den) < 1e-12) continue;
                double s = (B * E - C * D) / den, t = (A * E - B * D) / den;
                if (s <= 0 || s >= 1 || t <= 0 || t >= 1) { Warn($"Arch: cable connector {cc.Name} - {cc.CableA} and {cc.CableB} do not cross between their anchors; not built."); continue; }
                var pa = a0 + u * s; var pb = b0 + v * t;
                double sep = (pa - pb).Length;
                var ua = u.Normalized(); var ub = v.Normalized();
                var nrm = Vec3.Cross(ua, ub).Normalized();
                if (Vec3.Dot(pa - pb, nrm) < 0) nrm = -nrm;                    // from cable B toward cable A
                var mid = (pa + pb) * 0.5;
                double need = cd.PendantThickness + 2 * (ha == hb ? 0 : _d.HangerSystem.StrandDiameter / 2);
                Check($"cable connector {cc.Name} ({cc.CableA} x {cc.CableB}): clear distance between the strand axes", need, sep, Tol, L,
                    "p.150 pendant plate between two 1\" strands needs this; the p.147 TC / HC lines are closer - the connector clamps the cables (deflection not modeled)");
                string asm = $"CONNECTOR-{cc.Name}";
                AddAssembly(asm, AssemblyKind.ArchHanger, $"Cable connector {cc.Name}: {cc.CableA} x {cc.CableB} (p.150, UHMW-PE)");
                var m = new Mesh();
                // Pendant plate: hexagon in the plane normal to nrm, long axis along the bisector of the two cables.
                var ax = (ua + (Vec3.Dot(ua, ub) >= 0 ? ub : -ub)).Normalized();
                var ay = Vec3.Cross(nrm, ax).Normalized();
                double hl = cd.PendantLength / 2, hs = cd.PendantSide / 2, apex = hs + hl * Math.Tan(cd.PendantEdgeAngleDeg * Math.PI / 180);
                var hex = new List<Vec2> { new(-hl, -hs), new(0, -apex), new(hl, -hs), new(hl, hs), new(0, apex), new(-hl, hs) };
                var fr = new Frame3(W(mid), Wd(ax), Wd(ay), Wd(nrm));
                PlateBuilder.AddPlanar(m, hex, Array.Empty<Hole>(), fr, -cd.PendantThickness / 2, cd.PendantThickness / 2);
                // Sleeve brackets: half ring (outer R, wall t) around each strand axis, open toward the pendant, with a tab each side on the pendant face.
                foreach (var (pc, ud, sgn) in new[] { (pa, ua, 1.0), (pb, ub, -1.0) })
                {
                    double ro = cd.BracketOuterRadius, ri = ro - cd.BracketThickness;
                    var ring = new List<Vec2>();
                    for (int i = 0; i <= 12; i++) { double th = Math.PI * i / 12; ring.Add(new Vec2(ro * Math.Cos(th), ro * Math.Sin(th))); }
                    for (int i = 12; i >= 0; i--) { double th = Math.PI * i / 12; ring.Add(new Vec2(ri * Math.Cos(th), ri * Math.Sin(th))); }
                    if (MeshBuilder.SignedArea(ring) < 0) ring.Reverse();
                    var across = Vec3.Cross(ud, nrm).Normalized();
                    var f2 = new Frame3(W(pc), Wd(across), Wd(nrm * sgn), Wd(ud));
                    MeshBuilder.AddExtrusion(m, ring, f2, -cd.BracketLength / 2, cd.BracketLength / 2);
                    var face = mid + nrm * (sgn * cd.PendantThickness / 2);
                    var f3 = new Frame3(W(face + ud * Vec3.Dot(pc - face, ud) + across * Vec3.Dot(pc - face, across)), Wd(across), Wd(nrm * sgn), Wd(ud));
                    foreach (double sx in new[] { -1.0, 1.0 })
                        MeshBuilder.AddBox(m, f3, sx > 0 ? ro : -ro - cd.BracketTab, sx > 0 ? ro + cd.BracketTab : -ro, 0, cd.BracketThickness, -cd.BracketLength / 2, cd.BracketLength / 2);
                }
                Model.Steel.Parts.Add(new SteelPart { Mark = $"{asm}-1", Category = PartCategory.CableConnector, Assembly = asm, Description = $"Cable connector (pendant plate + 2 sleeve brackets) {cc.CableA} x {cc.CableB}",
                    Mesh = m, Material = cd.Material, Length = cd.PendantLength });
            }
        }

        private void Socket(ArchSocketDefinition s, string asm, string tag, Vec3 pin, Vec3 toCable, Vec3 memberTangent)
        {
            var nrm = Vec3.Cross(toCable, memberTangent);
            if (nrm.Length < 1e-6) nrm = Vec3.Cross(toCable, _fr.Z);
            nrm = nrm.Normalized();
            var a = Vec3.Cross(nrm, toCable).Normalized();
            var m = new Mesh();
            // Jaw body: A wide (fin plane) x B across the pin, from behind the pin (A / 2) to L along the cable.
            var rect = new[] { new Vec2(-s.A / 2, -s.B / 2), new Vec2(s.A / 2, -s.B / 2), new Vec2(s.A / 2, s.B / 2), new Vec2(-s.A / 2, s.B / 2) };
            RolledBuilder.AddExtrusion(m, rect, pin, a, nrm, toCable, -s.A / 2, s.L);
            if (s.W > s.L) RolledBuilder.AddTube(m, new[] { pin + toCable * s.L, pin + toCable * s.W }, s.ShankDiameter / 2, 16);
            RolledBuilder.AddTube(m, new[] { pin - nrm * (s.G / 2), pin + nrm * (s.G / 2) }, s.PinDiameter / 2, 16);
            AddPart($"{asm}-{tag}", PartCategory.HangerSocket, asm, s.Name, m, s.Material, s.W);
        }

        // ------------------------------------------------------------------ floorbeams

        private void BuildFloorbeam(ArchFloorbeamDefinition fb)
        {
            if (!_ties.TryGetValue(fb.TieA, out var ta) || !_ties.TryGetValue(fb.TieB, out var tb))
            { Warn($"Arch: floorbeam {fb.Name} - tie '{fb.TieA}' or '{fb.TieB}' not built."); return; }
            if (P(fb.PanelA) is not { } pa || P(fb.PanelB) is not { } pb) return;
            var e2 = (pb.XY - pa.XY);
            double len = e2.Length;
            var e = e2.Normalized();
            var nrm = e.Left;
            double flangeA = ta.Def.FlangeWidth / 2, flangeB = tb.Def.FlangeWidth / 2;
            double s0 = flangeA + fb.EndGap, s1 = len - flangeB - fb.EndGap;
            double sc = (s0 + s1) / 2;
            if (fb.CrownAtAlignment)
            {
                double Off(double s) => _al.Inverse(W((pa.XY + e * s).WithZ(fb.DepthAtCrown)).XY).Offset;
                double lo = s0, hi = s1, flo = Off(lo), fhi = Off(hi);
                if (Math.Sign(flo) != Math.Sign(fhi))
                {
                    for (int i = 0; i < 60; i++) { double m = (lo + hi) / 2, fm = Off(m); if (Math.Sign(fm) == Math.Sign(flo)) { lo = m; flo = fm; } else hi = m; }
                    sc = (lo + hi) / 2;
                }
                else Warn($"Arch: floorbeam {fb.Name} does not cross the alignment - crown at mid length.");
            }
            Model.Floorbeams.Add(new ArchFloorbeamResult(fb.Name, W(pa), W(pb), len, sc));
            string asm = fb.Name;
            AddAssembly(asm, AssemblyKind.ArchFloorbeam, $"Floorbeam {fb.Name}: built-up T {L(fb.WebThickness)} web, {L(fb.FlangeThickness)} x {L(fb.FlangeWidth)} flange");
            var org = W(pa);
            var ex = Wd(e.WithZ(0));
            var en = Wd(nrm.WithZ(0));
            var flange = new Mesh();
            RolledBuilder.AddExtrusion(flange, new[] { new Vec2(-fb.FlangeWidth / 2, 0), new Vec2(fb.FlangeWidth / 2, 0), new Vec2(fb.FlangeWidth / 2, fb.FlangeThickness), new Vec2(-fb.FlangeWidth / 2, fb.FlangeThickness) },
                org, en, _fr.Z, ex, s0, s1);
            AddPart($"{asm}-F", PartCategory.FloorbeamPlate, asm, $"PL {L(fb.FlangeThickness)} x {L(fb.FlangeWidth)} floorbeam flange", flange, fb.Material, s1 - s0);
            var web = new Mesh();
            var poly = new[]
            {
                new Vec2(s0, fb.FlangeThickness), new Vec2(s1, fb.FlangeThickness), new Vec2(s1, fb.DepthAtEnds), new Vec2(sc, fb.DepthAtCrown), new Vec2(s0, fb.DepthAtEnds),
            };
            // PT slots (obround) and web holes; slot pairs: outer opening at PtDimB from tie A / PtDimA from tie B, the pair PtPairSpacing apart.
            var slotCs = new List<double>();
            if (fb.PtSlotLength > 0)
            {
                if (!double.IsNaN(fb.PtDimB)) { slotCs.Add(fb.PtDimB); slotCs.Add(fb.PtDimB + fb.PtPairSpacing); }
                if (!double.IsNaN(fb.PtDimA)) { slotCs.Add(len - fb.PtDimA); slotCs.Add(len - fb.PtDimA - fb.PtPairSpacing); }
            }
            var openings = slotCs.Select(x => PlateBuilder.RoundedRectangle(new Vec2(x, fb.PtSlotZ), fb.PtSlotLength, fb.PtSlotHeight, fb.PtSlotHeight / 2 - 1e-6, 8)).ToList();
            var webHoles = fb.WebHoles.Where(hh => hh.X > s0 && hh.X < s1).Select(hh => new Hole(new Vec2(hh.X, hh.Z), fb.WebHoleDiameter)).ToList();
            var wf = new Frame3(org, ex, _fr.Z, en);
            if (openings.Count > 0) PlateBuilder.AddPlanarWithOpenings(web, poly, webHoles, openings, wf, -fb.WebThickness / 2, fb.WebThickness / 2);
            else PlateBuilder.AddPlanar(web, poly, webHoles, wf, -fb.WebThickness / 2, fb.WebThickness / 2);
            string slotNote = slotCs.Count > 0 ? $", {slotCs.Count} PT slots {L(fb.PtSlotHeight)} x {L(fb.PtSlotLength)}" : "";
            AddPart($"{asm}-W", PartCategory.FloorbeamPlate, asm, $"PL {L(fb.WebThickness)} floorbeam web {L(fb.DepthAtEnds)} - {L(fb.DepthAtCrown)} deep{slotNote}", web, fb.Material, s1 - s0);
            if (_d.FloorbeamConnection is { } fc)
            {
                FloorbeamEnd(fb, fc, ta.Def, asm, wf, len, false);
                FloorbeamEnd(fb, fc, tb.Def, asm, wf, len, true);
            }
            // Horizontal studs on both web faces at StudZ, 32 per face over the clear web (connection plates and PT slots excluded).
            if (fb.StudsPerFace > 0 && !double.IsNaN(fb.StudLength))
            {
                double plateEnd = _d.FloorbeamConnection is { } fc2 ? flangeA + fb.EndGap / 2 + fc2.PlateLength / 2 : s0;
                double plateEndB = _d.FloorbeamConnection is { } fc3 ? len - (flangeB + fb.EndGap / 2 + fc3.PlateLength / 2) : s1;
                double clear = fb.StudMinSpacing / 2;
                var zones = new List<(double a, double b)> { (plateEnd + clear, plateEndB - clear) };
                foreach (var x in slotCs.OrderBy(x => x))
                {
                    double ea = x - fb.PtSlotLength / 2 - clear, eb = x + fb.PtSlotLength / 2 + clear;
                    zones = zones.SelectMany(z => z.b <= ea || z.a >= eb ? new[] { z } : new[] { (a: z.a, b: ea), (a: eb, b: z.b) }).Where(z => z.b - z.a > -1e-9).ToList();
                }
                double total = zones.Sum(z => z.b - z.a);
                // Distribute: n_i studs in zone i (n_i - 1 spaces), counts by length (largest remainder), at least 1 per zone that fits.
                int n = fb.StudsPerFace;
                var counts = zones.Select(_ => 1).ToList();
                while (counts.Sum() > n) { int i = counts.IndexOf(counts.Max()); counts[i]--; }
                while (counts.Sum() < n) { int i = Enumerable.Range(0, zones.Count).OrderByDescending(k => counts[k] == 1 ? 1e9 + (zones[k].b - zones[k].a) : (zones[k].b - zones[k].a) / (counts[k] - 1)).First(); counts[i]++; }
                var xs = new List<double>();
                for (int k = 0; k < zones.Count; k++)
                {
                    var (za, zb2) = zones[k];
                    if (counts[k] == 1) xs.Add((za + zb2) / 2);
                    else for (int i = 0; i < counts[k]; i++) xs.Add(za + (zb2 - za) * i / (counts[k] - 1));
                }
                xs.Sort();
                var gaps = xs.Zip(xs.Skip(1), (a, b) => b - a).ToList();
                double minG = gaps.Count > 0 ? gaps.Min() : 0, maxIn = 0;
                for (int k = 0; k < zones.Count; k++) if (counts[k] > 1) maxIn = Math.Max(maxIn, (zones[k].b - zones[k].a) / (counts[k] - 1));
                if (minG < fb.StudMinSpacing - 1e-6 || maxIn > fb.StudMaxSpacing + 1e-6)
                    Warn($"Arch CHECK {fb.Name}: stud spacing {L(minG)} min / {L(maxIn)} max outside the plan {L(fb.StudMinSpacing)} - {L(fb.StudMaxSpacing)} (p.145 note 3: adjust at openings).");
                var studAsu = fb.StudUnresolved.Length > 0 ? fb.StudUnresolved : "";
                foreach (double x in xs)
                    foreach (int sd2 in new[] { 1, -1 })
                    {
                        var o = wf.ToWorld(x, fb.StudZ, sd2 * fb.WebThickness / 2);
                        var zdir = wf.Z * sd2;
                        var xdir = wf.X;
                        Model.Steel.Studs.Add(new StudInstance { Frame = new Frame3(o, xdir, Vec3.Cross(zdir, xdir).Normalized(), zdir), Length = fb.StudLength, Assembly = asm });
                    }
                if (studAsu.Length > 0) Warn(studAsu);
            }
        }

        /// <summary>Floorbeam end at one tie (p.146): stiffener in the floorbeam web plane (tie web to the tie flange edge), web connection plates both
        /// faces (stiffener to floorbeam web), flange connection plates top (and bottom) each side of the web, 7/8" bolts.</summary>
        private void FloorbeamEnd(ArchFloorbeamDefinition fb, ArchFloorbeamConnection fc, ArchTieDefinition tie, string asm, Frame3 wf, double len, bool atB)
        {
            // s measured from the tie CL toward the span; map to the floorbeam frame (s from tie A).
            double S(double s) => atB ? len - s : s;
            double half = tie.FlangeWidth / 2, joint = half + fb.EndGap / 2;
            double zf = tie.FlangeThickness, zw = tie.FlangeThickness + tie.WebHeight, sw = tie.WebThickness / 2;
            string end = atB ? "B" : "A";
            var bolt = Model.Steel.Bolt;
            double hd = bolt.HoleDiameter;
            Vec2 P(double s, double z) => new(S(s), z);
            List<Vec2> Ccw(List<Vec2> l) { if (MeshBuilder.SignedArea(l) < 0) l.Reverse(); return l; }
            // Stiffener.
            var st = Ccw(new List<Vec2> { P(sw, zf + fc.StiffenerClip), P(sw + fc.StiffenerClip, zf), P(half, zf), P(half, zf + fc.StiffenerOuterHeight), P(sw + fc.StiffenerTopFlat, zw), P(sw, zw) });
            var ms = new Mesh();
            PlateBuilder.AddPlanar(ms, st, Array.Empty<Hole>(), wf, -fc.StiffenerThickness / 2, fc.StiffenerThickness / 2);
            // Web connection plates (both faces), holes.
            double pl = fc.PlateLength / 2, ztop = fb.DepthAtEnds, zbot = ztop - fc.WebPlateHeight;
            var holes = new List<Hole>();
            foreach (var r in fc.WebRows)
                for (int k = r.FirstColumn; k < fc.BoltColumns; k += Math.Max(1, r.ColumnStep))
                    foreach (int sg in new[] { -1, 1 }) holes.Add(new Hole(P(joint + sg * (fc.BoltFirst + k * fc.BoltPitch), ztop - r.Gage), hd));
            var wp = Ccw(new List<Vec2> { P(joint - pl, zbot), P(joint + pl, zbot), P(joint + pl, ztop), P(joint - pl, ztop) });
            double tw = Math.Max(fb.WebThickness, fc.StiffenerThickness);
            var mw = new Mesh();
            foreach (int sd in new[] { 1, -1 })
                PlateBuilder.AddPlanar(mw, wp, holes, wf, sd > 0 ? tw / 2 : -tw / 2 - fc.PlateThickness, sd > 0 ? tw / 2 + fc.PlateThickness : -tw / 2);
            double gw = 2 * fc.PlateThickness + tw;
            foreach (var h in holes)
                Model.Steel.Bolts.Add(new BoltInstance { Frame = BoltFrame((u, v, t) => wf.ToWorld(u, v, t), h.Center.X, h.Center.Y, tw / 2 + fc.PlateThickness, -1), Grip = gw, Length = bolt.LengthForGrip(gw), Assembly = asm });
            // Flange connection plates: each side of the web, outer edge flush with the floorbeam flange edge; top and bottom.
            var ff = new Frame3(wf.Origin, wf.X, wf.Z, wf.Y);   // (s, lateral, up)
            double fe = fb.FlangeWidth / 2;
            var mf = new Mesh();
            var mfb = new Mesh();
            int nfb = 0;
            foreach (int sd in new[] { 1, -1 })
            {
                var fh = new List<Hole>();
                foreach (var r in fc.FlangeRows)
                    for (int k = r.FirstColumn; k < fc.BoltColumns; k += Math.Max(1, r.ColumnStep))
                        foreach (int sg in new[] { -1, 1 }) fh.Add(new Hole(new Vec2(S(joint + sg * (fc.BoltFirst + k * fc.BoltPitch)), sd * (fe - r.Gage)), hd));
                double w0 = sd > 0 ? fe - fc.FlangePlateWidth : -fe, w1 = sd > 0 ? fe : -fe + fc.FlangePlateWidth;
                var fp = Ccw(new List<Vec2> { new(S(joint - pl), w0), new(S(joint + pl), w0), new(S(joint + pl), w1), new(S(joint - pl), w1) });
                const double up = 1;
                PlateBuilder.AddPlanar(mf, fp, fh, ff, up > 0 ? zf : -zf - fc.PlateThickness, up > 0 ? zf + fc.PlateThickness : -zf);
                if (fc.BottomFlangePlates) { PlateBuilder.AddPlanar(mfb, fp, fh, ff, up > 0 ? -fc.PlateThickness : 0, up > 0 ? 0 : fc.PlateThickness); nfb++; }
                double gf = fc.PlateThickness + zf + (fc.BottomFlangePlates ? fc.PlateThickness : 0);
                foreach (var h in fh)
                    Model.Steel.Bolts.Add(new BoltInstance { Frame = BoltFrame((u, v, t) => ff.ToWorld(u, v, t), h.Center.X, h.Center.Y, up * (zf + fc.PlateThickness), -up), Grip = gf, Length = bolt.LengthForGrip(gf), Assembly = asm });
            }
            AddPart($"{asm}-ST{end}", PartCategory.ArchStiffener, asm, $"PL {L(fc.StiffenerThickness)} connection stiffener at {tie.Name} (p.146)", ms, fc.Material, half - sw);
            AddPart($"{asm}-WP{end}", PartCategory.ArchConnectionPlate, asm, $"2 - PL {L(fc.PlateThickness)} x {L(fc.WebPlateHeight)} x {L(fc.PlateLength)} web connection plates (p.146)", mw, fc.Material, fc.PlateLength);
            AddPart($"{asm}-FP{end}", PartCategory.ArchConnectionPlate, asm, $"2 - PL {L(fc.PlateThickness)} x {L(fc.FlangePlateWidth)} x {L(fc.PlateLength)} top flange connection plates (p.146)", mf, fc.Material, fc.PlateLength);
            if (nfb > 0)
                AddPart($"{asm}-FB{end}", PartCategory.ArchConnectionPlate, asm, $"2 - PL {L(fc.PlateThickness)} x {L(fc.FlangePlateWidth)} x {L(fc.PlateLength)} bottom flange connection plates (p.146)", mfb, fc.Material, fc.PlateLength,
                    0, fc.BottomUnresolved.Length > 0 ? Assumption.Unresolved(fc.BottomUnresolved, "bottom plates = the top plates mirrored") : null);
        }

        // ------------------------------------------------------------------ upper lateral bracing

        private void BuildBracing()
        {
            if (_d.Bracing.Count == 0) return;
            var w = SteelCatalog.FindWideFlange(_d.BracingSection);
            if (w == null) { Warn($"Arch: bracing section '{_d.BracingSection}' is not in the AISC catalog - bracing not built."); return; }
            var sec = SteelCatalog.WideFlangePolygon(w, -w.WebThickness / 2);
            AddAssembly("UPPER-BRACING", AssemblyKind.ArchBracing, $"Upper lateral bracing {w.Name}");
            var asu = _d.BracingOrientationUnresolved.Length > 0 ? Assumption.Unresolved(_d.BracingOrientationUnresolved, "web in the plane of the member and the local Z axis, work point to work point") : null;
            // Member axes (world) first: crossing trims need the continuous members.
            var axes = new Dictionary<string, (Vec3 a, Vec3 b)>(StringComparer.OrdinalIgnoreCase);
            foreach (var br in _d.Bracing)
                if (P(br.From) is { } pa && P(br.To) is { } pb) axes[br.Name] = (W(pa), W(pb));
            double Trim(Vec3 ax, string other, double gap)
            {
                if (!axes.TryGetValue(other, out var o)) return 0;
                var od = (o.b - o.a).Normalized();
                double s = Vec3.Cross(ax, od).Length;
                return s < 1e-6 ? 0 : (w.FlangeWidth / 2 + gap) / s;
            }
            foreach (var br in _d.Bracing)
            {
                if (P(br.From) is not { } a || P(br.To) is not { } b) continue;
                var wa = W(a); var wb = W(b);
                if (br.Via.Length > 0 && P(br.Via) is { } via)
                {
                    var wv = W(via); var dd = (wb - wa).Normalized();
                    double off = (wv - wa - dd * Vec3.Dot(wv - wa, dd)).Length;
                    Check($"bracing {br.Name}: {br.Via} on the straight member {br.From}-{br.To}", 0, off, In(0.25), L, "p.128 table (continuous member through the crossing, p.141 / 142)");
                }
                var ax0 = (wb - wa).Normalized();
                if (br.TrimStartAt.Length > 0) wa += ax0 * Trim(ax0, br.TrimStartAt, br.TrimGap);
                if (br.TrimEndAt.Length > 0) wb -= ax0 * Trim(ax0, br.TrimEndAt, br.TrimGap);
                var ax = (wb - wa).Normalized();
                var m = (_fr.Z - ax * Vec3.Dot(ax, _fr.Z)).Normalized();
                var v = Vec3.Cross(ax, m).Normalized();
                var mesh = new Mesh();
                RolledBuilder.AddExtrusion(mesh, sec, wa, m, v, ax, 0, (wb - wa).Length);
                AddPart(br.Name, PartCategory.ArchBracing, "UPPER-BRACING", $"{w.Name} {br.From}-{br.To}", mesh, _d.BracingMaterial, (wb - wa).Length, w.WeightPerFootLb, asu);
            }
            foreach (var cr in _d.BracingCrossings) BraceCrossing(cr, w, axes);
        }

        /// <summary>Crossing splice plates (top / bottom) along the spliced member over the continuous one, holes and bolts (p.141 / 142).</summary>
        private void BraceCrossing(ArchBraceCrossing cr, WideFlangeSize w, Dictionary<string, (Vec3 a, Vec3 b)> axes)
        {
            if (!axes.TryGetValue(cr.Continuous, out var co) || !axes.TryGetValue(cr.PieceA, out var pa) || !axes.TryGetValue(cr.PieceB, out var pb) || P(cr.Point) is not { } cp) return;
            var c0 = W(cp);
            var sd = ((pa.b - pa.a).Normalized() + (pb.b - pb.a).Normalized()).Normalized();   // spliced member direction (mean of the two pieces)
            var m = (_fr.Z - sd * Vec3.Dot(sd, _fr.Z)).Normalized();
            var v = Vec3.Cross(sd, m).Normalized();
            var cd = (co.b - co.a).Normalized();
            var c2 = new Vec2(Vec3.Dot(cd, sd), Vec3.Dot(cd, v)).Normalized();
            double angModel = Math.Acos(Math.Min(1, Math.Abs(c2.X))) * 180 / Math.PI;   // acute angle between the members in the plate plane
            var mc = (_fr.Z - cd * Vec3.Dot(cd, _fr.Z)).Normalized(); var vc = Vec3.Cross(cd, mc).Normalized();
            var s2 = new Vec2(Vec3.Dot(sd, cd), Vec3.Dot(sd, vc)).Normalized();
            double angCont = Math.Acos(Math.Min(1, Math.Abs(s2.X))) * 180 / Math.PI;
            double ang3d = Math.Acos(Math.Min(1, Math.Abs(Vec3.Dot(sd, cd)))) * 180 / Math.PI;
            string asm = $"UPPER-BRACING";
            var hd = Model.Steel.Bolt.HoleDiameter;
            var asu = cr.Unresolved.Length > 0 ? Assumption.Unresolved(cr.Unresolved, $"PL {L(cr.Thickness)}") : null;
            int nb = 0;
            foreach (var (top, l1, l2, l3, angPlan) in new[] { (true, cr.TopL1, cr.TopL2, cr.TopL3, cr.TopAngleDeg), (false, cr.BottomL1, cr.BottomL2, cr.BottomL3, cr.BottomAngleDeg) })
            {
                double lt = l1 + l2 + l3, hw = cr.Width / 2;
                if (!double.IsNaN(angPlan)) Check($"bracing crossing {cr.Name} {(top ? "top" : "bottom")}: angle between the members (in space)", angPlan, ang3d, 0.75,
                    x => $"{x:F3} deg", $"p.141 / 142 vs the p.128 points: {angModel:F3} deg in the spliced member flange plane, {angCont:F3} deg in the continuous member flange plane (U6: U points off the rib plane)");
                Check($"bracing crossing {cr.Name} {(top ? "top" : "bottom")}: crossing zone L2 x sin(angle)", w.FlangeWidth + 2 * In(0.5), l2 * Math.Sin(angPlan * Math.PI / 180), In(0.0625), L, "plan L2 = flange width + 1/2\" gap each side across the continuous member");
                double z0 = top ? w.Depth / 2 : -w.Depth / 2 - cr.Thickness, z1 = top ? w.Depth / 2 + cr.Thickness : -w.Depth / 2;
                var holes = new List<Hole>();
                foreach (double y in new[] { -cr.Gage / 2, cr.Gage / 2 })
                {
                    foreach (double x in new[] { lt / 2 - cr.Edge, lt / 2 - cr.Edge - cr.Pitch }) { holes.Add(new Hole(new Vec2(x, y), hd)); holes.Add(new Hole(new Vec2(-x, y), hd)); }
                    // On the continuous flange: EdgeToContinuousFlange (perpendicular) inside its flange tips, along this row.
                    foreach (double dp in new[] { -(w.FlangeWidth / 2 - cr.EdgeToContinuousFlange), w.FlangeWidth / 2 - cr.EdgeToContinuousFlange })
                    {
                        // perpendicular distance from the continuous axis (through the origin along c2): p.X * (-c2.Y) + p.Y * c2.X = dp
                        if (Math.Abs(c2.Y) < 1e-6) continue;
                        double x = (dp - y * c2.X) / -c2.Y;
                        holes.Add(new Hole(new Vec2(x, y), hd));
                    }
                }
                var mesh = new Mesh();
                var fr = new Frame3(c0, sd, v, m);
                PlateBuilder.AddPlanar(mesh, Ccw(ClippedRect(-lt / 2, lt / 2, -hw, hw, 0)), holes, fr, z0, z1);
                Model.Steel.Parts.Add(new SteelPart { Mark = $"BR-{cr.Name}-{(top ? "T" : "B")}", Category = PartCategory.ArchConnectionPlate, Assembly = asm,
                    Description = $"PL {L(cr.Thickness)} x {L(cr.Width)} x {L(lt)} {(top ? "top" : "bottom")} splice plate at {cr.Name} (p.141 / 142)", Mesh = mesh, Material = _d.BracingMaterial, Length = lt, Assumption = asu });
                double grip = cr.Thickness + w.FlangeThickness;
                foreach (var h in holes)
                {
                    var o = fr.ToWorld(h.Center.X, h.Center.Y, top ? z1 : z0);
                    var zd = top ? -m : m;
                    Model.Steel.Bolts.Add(new BoltInstance { Frame = new Frame3(o, sd, Vec3.Cross(zd, sd).Normalized(), zd), Grip = grip, Length = Model.Steel.Bolt.LengthForGrip(grip), Assembly = asm });
                    nb++;
                }
            }
            Model.SpliceBolts[$"UPPER-BRACING-{cr.Name}"] = nb;
        }

        // ------------------------------------------------------------------ knuckles

        private void BuildKnuckle(ArchKnuckleDefinition k)
        {
            if (P(k.EndFrom) is not { } a || P(k.EndTo) is not { } b) return;
            var t = (b - a).Normalized();
            var tp = t.XY.Normalized();
            // Normal toward the span (toward the local origin).
            var n = tp.Left;
            if (Vec2.Dot(n, -a.XY) < 0) n = -n;
            // Centre: where the end line crosses the chord (local Y = 0).
            double sy = Math.Abs(b.Y - a.Y) > 1e-9 ? -a.Y / (b.Y - a.Y) : 0.5;
            var ctr = Vec2.Lerp(a.XY, b.XY, sy);
            string asm = $"KNUCKLE-{k.Name}".ToUpperInvariant().Replace(' ', '-');
            AddAssembly(asm, AssemblyKind.ArchKnuckle, $"Knuckle {k.Name}: end floorbeam (end flange, end web), deck plate");
            var org = W(ctr.WithZ(0));
            var et = Wd(tp.WithZ(0));
            var en = Wd(n.WithZ(0));
            double half = k.EndLength / 2;
            // End flange: from the end web outside face (outside the end line by EndWebOffset) EndFlangeWidth toward the span.
            double o0 = -k.EndWebOffset, o1 = o0 + k.EndFlangeWidth;
            var fl = new Mesh();
            RolledBuilder.AddExtrusion(fl, new[] { new Vec2(o0, k.EndFlangeBottomZ), new Vec2(o1, k.EndFlangeBottomZ), new Vec2(o1, k.EndFlangeBottomZ + k.EndFlangeThickness), new Vec2(o0, k.EndFlangeBottomZ + k.EndFlangeThickness) },
                org, en, _fr.Z, et, -half, half);
            AddPart($"{asm}-EF", PartCategory.KnucklePlate, asm, $"PL {L(k.EndFlangeThickness)} x {L(k.EndFlangeWidth)} end flange", fl, k.Material, k.EndLength);
            // End web: crowned (height at the chord, straight to its ends).
            double hEnd = !double.IsNaN(k.EndWebHeightAtEnds) ? k.EndWebHeightAtEnds : k.EndWebScaledHeightAtEnds;
            if (!double.IsNaN(hEnd))
            {
                double zt = k.EndFlangeBottomZ + k.EndFlangeThickness;
                var web = new Mesh();
                RolledBuilder.AddExtrusion(web, new[] { new Vec2(-half, zt), new Vec2(half, zt), new Vec2(half, zt + hEnd), new Vec2(0, zt + k.EndWebHeightAtChord), new Vec2(-half, zt + hEnd) },
                    org, et, _fr.Z, en, o0, o0 + k.EndWebThickness);
                var asu = double.IsNaN(k.EndWebHeightAtEnds) && k.EndWebUnresolved.Length > 0 ? Assumption.Unresolved(k.EndWebUnresolved, $"end height {L(hEnd)} scaled") : null;
                AddPart($"{asm}-EW", PartCategory.KnucklePlate, asm, $"PL {L(k.EndWebThickness)} end web", web, k.Material, k.EndLength, 0, asu);
            }
            else Warn(k.EndWebUnresolved.Length > 0 ? k.EndWebUnresolved : $"Arch: knuckle {k.Name} end web height at the ends not given - end web not built.");
            // Deck plate: outline points, the end-line points moved to the inside face of the end web.
            var outline = new List<Vec2>();
            foreach (var pn in k.DeckPlate)
            {
                if (P(pn) is not { } q) return;
                var p2 = q.XY;
                bool onEnd = string.Equals(pn, k.EndFrom, StringComparison.OrdinalIgnoreCase) || string.Equals(pn, k.EndTo, StringComparison.OrdinalIgnoreCase);
                outline.Add(onEnd ? p2 + n * (o0 + k.EndWebThickness) : p2);
            }
            if (outline.Count >= 3)
            {
                if (MeshBuilder.SignedArea(outline) < 0) outline.Reverse();
                var dp = new Mesh();
                MeshBuilder.AddExtrusion(dp, outline, _fr, k.DeckPlateTopZ - k.DeckPlateThickness, k.DeckPlateTopZ);
                var asu = k.DeckPlateUnresolved.Length > 0 ? Assumption.Unresolved(k.DeckPlateUnresolved, $"top at local Z {L(k.DeckPlateTopZ)}") : null;
                AddPart($"{asm}-DP", PartCategory.KnucklePlate, asm, $"PL {L(k.DeckPlateThickness)} deck plate", dp, k.Material, 0, 0, asu);
            }
            foreach (var pt in k.PtAnchors) Model.Tendons.Add(($"{k.Name} PT anchor {pt.Name}", new List<Vec3> { W(pt.Vec) }, "PT anchorage (data)"));
        }

        // ------------------------------------------------------------------ deck

        /// <summary>Local Z of the finished grade above a local plan point (iterated through the tilted frame).</summary>
        private double GradeZ(double x, double y)
        {
            double z = 0;
            for (int i = 0; i < 4; i++)
            {
                var wp = W(new Vec3(x, y, z));
                var so = _al.Inverse(wp.XY);
                double g = _b.Deck.FinishedElevation(_al, so.Station, so.Offset);
                z += (g - wp.Z) / _fr.Z.Z;
            }
            return z;
        }

        private void BuildDeck(ArchDeckDefinition dk)
        {
            if (!_ties.TryGetValue(dk.RightTie, out var ts) || !_ties.TryGetValue(dk.LeftTie, out var tn))
            { Warn("Arch: deck ties not built - deck not built."); return; }
            // Knots: (south distance, north distance) at the end lines and the floorbeams.
            var knots = new List<(double s, double n, bool form)>();
            var fbs = _d.Floorbeams.Select(fb =>
            {
                var ps = ts.Def.PanelPoints.FirstOrDefault(p => p.Name == fb.PanelA);
                var pn = tn.Def.PanelPoints.FirstOrDefault(p => p.Name == fb.PanelB);
                return (ps, pn);
            }).Where(x => x.ps != null && x.pn != null).OrderBy(x => x.ps!.Distance).ToList();
            if (fbs.Count < 2) { Warn("Arch: deck needs the floorbeams on both deck ties - deck not built."); return; }
            var ka = _d.Knuckles.FirstOrDefault(k => dk.EndKnuckles.Count > 0 && k.Name == dk.EndKnuckles[0]);
            var kb = _d.Knuckles.FirstOrDefault(k => dk.EndKnuckles.Count > 1 && k.Name == dk.EndKnuckles[1]);
            double ext0 = ka?.EndWebOffset ?? 0, ext1 = kb?.EndWebOffset ?? 0;
            knots.Add((0, tn.Length, false));
            foreach (var (ps, pn) in fbs) knots.Add((ps!.Distance, pn!.Distance, true));
            knots.Add((ts.Length, 0, false));
            double webS = ts.Def.WebThickness / 2, webN = tn.Def.WebThickness / 2;
            const int topN = 24;
            // Form profile across, as fractions of the first floorbeam's clear width.
            double w0 = (tn.At(knots[1].n).p - ts.At(knots[1].s).p).Length - webS - webN;
            var prof = new List<(double u, bool top)>();
            double flute = dk.FormPitch - dk.FormTopFlat;
            double inset = (flute - dk.FormFluteBottom) / 2;
            if (dk.FlutesOnBarGrid && dk.Rebar is { } rbg)
            {
                // A corrugation under every longitudinal bottom bar ("#5 bar in each corrugation"): flute centres on the bar grid about the structure CL.
                int kmax = (int)Math.Ceiling(w0 / 2 / rbg.LongSpacing) + 1;
                for (int k = -kmax; k <= kmax; k++)
                {
                    double c = w0 / 2 + k * rbg.LongSpacing;
                    double o0 = c - flute / 2, o1 = c + flute / 2;
                    if (o0 <= 1e-6 || o1 >= w0 - 1e-6) continue;
                    prof.Add((o0, true)); prof.Add((c - dk.FormFluteBottom / 2, false)); prof.Add((c + dk.FormFluteBottom / 2, false)); prof.Add((o1, true));
                }
            }
            else
                for (double u0 = 0; u0 + dk.FormPitch <= w0 + 1e-9; u0 += dk.FormPitch)
                {
                    prof.Add((u0 + dk.FormTopFlat, true));
                    prof.Add((u0 + dk.FormTopFlat + inset, false));
                    prof.Add((u0 + dk.FormTopFlat + inset + dk.FormFluteBottom, false));
                    prof.Add((u0 + dk.FormPitch, true));
                }
            prof.RemoveAll(p => p.u >= w0 - 1e-6);
            double zTop = dk.FormBottomZ + dk.FormDepth;

            Vec3[] Ring(double ds, double dn, double extra, bool form, bool sheet)
            {
                var a = ts.At(ds).p; var b = tn.At(dn).p;
                var e = (b - a).Normalized();
                var nOut = e.Right; // toward the outside of the ring? only used for the end extension
                if (extra != 0)
                {
                    // Move the end ring outward (away from the span) by 'extra', normal to the ring line.
                    var mid = (a + b) * 0.5;
                    var toward = -mid.Normalized();
                    if (Vec2.Dot(nOut, toward) > 0) nOut = -nOut;
                    a += nOut * extra; b += nOut * extra;
                }
                var sa = a + e * webS; var sb = b - e * webN;
                double w = (sb - sa).Length;
                double f = w / w0;
                var pts = new List<Vec3>();
                if (sheet)
                {
                    // Form deck sheet: profile bottom then back along the top of the sheet.
                    var line = new List<Vec2> { new(0, zTop) };
                    foreach (var (u, top) in prof) line.Add(new Vec2(u * f, top ? zTop : dk.FormBottomZ));
                    line.Add(new Vec2(w, zTop));
                    foreach (var q in OffsetBelow(line, dk.FormThickness)) pts.Add(W((sa + e * q.X).WithZ(q.Y)));
                    for (int i = line.Count - 1; i >= 0; i--) pts.Add(W((sa + e * line[i].X).WithZ(line[i].Y)));
                    return pts.ToArray();
                }
                // Bottom from south to north, then top from north to south.
                if (form)
                {
                    pts.Add(W(sa.WithZ(zTop)));
                    foreach (var (u, top) in prof) pts.Add(W((sa + e * (u * f)).WithZ(top ? zTop : dk.FormBottomZ)));
                    pts.Add(W(sb.WithZ(zTop)));
                }
                else
                {
                    double zb = (ka ?? kb)?.DeckPlateTopZ ?? zTop;
                    for (int i = 0; i <= prof.Count + 1; i++) pts.Add(W((sa + e * (w * i / (prof.Count + 1))).WithZ(zb)));
                }
                for (int i = topN; i >= 0; i--)
                {
                    var q = sa + e * (w * i / topN);
                    pts.Add(W(q.WithZ(GradeZ(q.X, q.Y))));
                }
                return pts.ToArray();
            }

            var slab = new Mesh();
            var ends = new Mesh();   // knuckle zones: separate closed solids (they touch the main slab at the first / last floorbeam)
            var sheet = new Mesh();
            double area = 0;
            void Zone(int k0, int k1, bool form, double ext0z, double ext1z)
            {
                var target = form ? slab : ends;
                var rings = new List<Vec3[]>();
                var srings = new List<Vec3[]>();
                for (int k = k0; k < k1; k++)
                {
                    int sub = 4;
                    for (int i = 0; i < sub; i++)
                    {
                        double t = (double)i / sub;
                        double ds = knots[k].s + (knots[k + 1].s - knots[k].s) * t, dn = knots[k].n + (knots[k + 1].n - knots[k].n) * t;
                        double ex = k == k0 && i == 0 ? ext0z : 0;
                        rings.Add(Ring(ds, dn, ex, form, false));
                        if (form) srings.Add(Ring(ds, dn, 0, true, true));
                    }
                }
                rings.Add(Ring(knots[k1].s, knots[k1].n, ext1z, form, false));
                if (form) srings.Add(Ring(knots[k1].s, knots[k1].n, 0, true, true));
                // Cap triangulation needs a 2D section in ring order: the first ring's points in its own plane.
                var r0 = rings[0];
                var ex2 = (r0[^1] - r0[0]).Normalized();
                var ez = _fr.Z;
                var sec = r0.Select(p => new Vec2(Vec3.Dot(p - r0[0], ex2), Vec3.Dot(p - r0[0], ez))).ToArray();
                MeshBuilder.AddLoft(target, sec, rings);
                for (int i = 0; i + 1 < rings.Count; i++)
                {
                    // Top surface strip between two rings (top polyline = the last topN + 1 points).
                    var r1 = rings[i]; var r2 = rings[i + 1];
                    var n1 = r1[r1.Length - topN - 1]; var s1 = r1[^1]; var n2 = r2[r2.Length - topN - 1]; var s2 = r2[^1];
                    area += 0.5 * (Vec3.Cross(n1 - s1, s2 - s1).Length + Vec3.Cross(s2 - n2, n1 - n2).Length);
                }
                if (form)
                {
                    var s0 = srings[0];
                    var sx = (s0[s0.Length / 2 - 1] - s0[0]).Normalized();
                    var ssec = s0.Select(p => new Vec2(Vec3.Dot(p - s0[0], sx), Vec3.Dot(p - s0[0], ez))).ToArray();
                    MeshBuilder.AddLoft(sheet, ssec, srings);
                }
            }
            Zone(0, 1, false, ext0, 0);
            Zone(1, knots.Count - 2, true, 0, 0);
            Zone(knots.Count - 2, knots.Count - 1, false, 0, ext1);
            Model.Slab = slab;
            Model.SlabArea = area;
            Model.Concrete.Deck = slab;
            Model.SlabEnds = ends;
            AddAssembly("FORM-DECK", AssemblyKind.ArchDeckForm, "Corrugated steel deck form spanning the floorbeams");
            var fasu = dk.FormUnresolved.Length > 0 ? Assumption.Unresolved(dk.FormUnresolved, $"pitch {L(dk.FormPitch)}, top flat {L(dk.FormTopFlat)}, flute bottom {L(dk.FormFluteBottom)}") : null;
            AddPart("FORM-DECK", PartCategory.FormDeck, "FORM-DECK", dk.FormDescription.Length > 0 ? dk.FormDescription : $"{L(dk.FormDepth)} x {L(dk.FormPitch)} steel deck form", sheet,
                "ASTM A653 Gr. 33, G165 galvanized", 0, 0, fasu);
            BuildCurbs(dk, ka, kb);
            _deck = (ts, tn, knots.Select(k => (k.s, k.n)).ToList(), ka, kb);
            // Second body of the deck IfcSlab (ConcreteModel.Haunches = the extra deck items): knuckle-zone slab and curbs.
            var extra = new Mesh();
            foreach (var mm in new[] { Model.SlabEnds, Model.Curbs }) if (mm != null) Append(extra, mm);
            Model.Concrete.Haunches = extra;
            foreach (var t in dk.Tendons) Model.Tendons.Add((t.Name, t.Path.Select(p => W(p.Vec)).ToList(), t.Description));
            if (dk.Rebar is { } rb) BuildDeckRebar(dk, rb);
            if (dk.PtSystem is { } pt) BuildPt(dk, pt);
            if (dk.Rebar is { } rb2) CheckBarList(rb2);
        }

        private void BuildCurbs(ArchDeckDefinition dk, ArchKnuckleDefinition? ka, ArchKnuckleDefinition? kb)
        {
            if (dk.Curbs.Count == 0 || ka == null || kb == null) return;
            if (P(ka.EndFrom) is not { } a0 || P(ka.EndTo) is not { } a1 || P(kb.EndFrom) is not { } b0 || P(kb.EndTo) is not { } b1) return;
            // End lines in plan (outside faces of the end webs), world.
            (Vec2 p, Vec2 d) EndLine(Vec3 p, Vec3 q, double off)
            {
                var t = (q - p).XY.Normalized();
                var n = t.Left;
                if (Vec2.Dot(n, -p.XY) < 0) n = -n;
                var lp = p.XY - n * off;
                return (W(lp.WithZ(0)).XY, (W((lp + t).WithZ(0)) - W(lp.WithZ(0))).XY.Normalized());
            }
            var e0 = EndLine(a0, a1, ka.EndWebOffset);
            var e1 = EndLine(b0, b1, kb.EndWebOffset);
            var curbs = new Mesh();
            foreach (var c in dk.Curbs)
            {
                double outer = c.InnerOffset + Math.Sign(c.InnerOffset) * c.Width;
                double sta0 = CrossStation(c.InnerOffset, e0), sta1 = CrossStation(c.InnerOffset, e1);
                if (double.IsNaN(sta0) || double.IsNaN(sta1)) { Warn($"Arch: curb {c.Name} does not cross the deck end lines - not built."); continue; }
                if (sta1 < sta0) (sta0, sta1) = (sta1, sta0);
                int n = Math.Max(2, (int)Math.Ceiling((sta1 - sta0) / 0.5));
                var rings = new List<Vec3[]>();
                Vec2[]? sec = null;
                for (int i = 0; i <= n; i++)
                {
                    double s = sta0 + (sta1 - sta0) * i / n;
                    double gi = _b.Deck.FinishedElevation(_al, s, c.InnerOffset), go = _b.Deck.FinishedElevation(_al, s, outer);
                    double top = gi + c.Height;
                    var pi = _al.PointAt(s, c.InnerOffset); var po = _al.PointAt(s, outer);
                    rings.Add(new[] { pi.WithZ(gi), po.WithZ(go), po.WithZ(top), pi.WithZ(top) });
                    sec ??= new[] { new Vec2(c.InnerOffset, gi), new Vec2(outer, go), new Vec2(outer, top), new Vec2(c.InnerOffset, top) };
                }
                MeshBuilder.AddLoft(curbs, sec!, rings);
            }
            Model.Curbs = curbs;

        }

        /// <summary>Polyline (u, z) offset by <paramref name="t"/> to its lower side, normal to each segment (mitered vertices).</summary>
        private static List<Vec2> OffsetBelow(IReadOnlyList<Vec2> line, double t)
        {
            Vec2 N(int i) { var d = (line[i + 1] - line[i]).Normalized(); return new Vec2(d.Y, -d.X); }
            var res = new List<Vec2> { line[0] + N(0) * t };
            for (int i = 1; i + 1 < line.Count; i++)
            {
                var n1 = N(i - 1); var n2 = N(i);
                var m = (n1 + n2);
                double c = Vec2.Dot(m.Normalized(), n1);
                res.Add(m.Length < 1e-9 || c < 0.1 ? line[i] + n2 * t : line[i] + m.Normalized() * (t / c));
            }
            res.Add(line[^1] + N(line.Count - 2) * t);
            return res;
        }

        private static void Append(Mesh dst, Mesh src)
        {
            int b = dst.Positions.Count;
            dst.Positions.AddRange(src.Positions);
            dst.Indices.AddRange(src.Indices.Select(i => i + b));
        }

        /// <summary>Station where the alignment offset line crosses a plan line (NaN if not within the alignment).</summary>
        private double CrossStation(double offset, (Vec2 p, Vec2 d) line)
        {
            double F(double sta) => Vec2.Cross(line.d, _al.PointAt(sta, offset) - line.p);
            var so = _al.Inverse(line.p);
            double lo = so.Station - Ft(30), hi = so.Station + Ft(30);
            double flo = F(lo), fhi = F(hi);
            if (Math.Sign(flo) == Math.Sign(fhi)) return double.NaN;
            for (int i = 0; i < 80; i++) { double m = (lo + hi) / 2, fm = F(m); if (Math.Sign(fm) == Math.Sign(flo)) { lo = m; flo = fm; } else hi = m; }
            return (lo + hi) / 2;
        }

        // ------------------------------------------------------------------ bearings

        private void BuildBearing(ArchBearingDefinition br, int index)
        {
            if (P(br.Point) is not { } lp) return;
            var sl = _f.Supports.FirstOrDefault(s => string.Equals(s.Name, br.Support, StringComparison.OrdinalIgnoreCase));
            if (sl == null) { Warn($"Arch: bearing {br.Name} - support '{br.Support}' is not a support of this bridge."); return; }
            if (double.IsNaN(br.SeatElevation)) { Warn($"Arch: bearing {br.Name} - no seat (pedestal top) elevation; bearing not built."); return; }
            double zu = double.IsNaN(br.SteelUndersideZ) ? 0 : br.SteelUndersideZ;
            var top = W(lp.XY.WithZ(zu));
            var c2 = top.XY;
            // Long direction: the plan pedestal / bearing axis (angle from the support line +S toward +N) or normal to the support line; pointing up-structure (+X).
            var sDir = sl.Direction; var nDir = sDir.Left;
            var along = double.IsNaN(br.LongAxisAngleDeg) ? nDir
                : sDir * Math.Cos(br.LongAxisAngleDeg * Math.PI / 180) + nDir * Math.Sin(br.LongAxisAngleDeg * Math.PI / 180);
            if (Vec2.Dot(along, _fr.X.XY) < 0) along = -along;
            if (!double.IsNaN(br.LongAxisAngleDeg))
            {
                double toChord = Math.Acos(Math.Clamp(Math.Abs(Vec2.Dot(along.Normalized(), _fr.X.XY.Normalized())), 0, 1)) * 180 / Math.PI;
                Info($"Arch bearing {br.Name}: plates and anchor bolts along {br.LongAxisAngleDeg:0.###} deg to the {sl.Name} line ({br.LongAxisSource}); {toChord:F2} deg off the arch chord in plan.");
            }
            var across = along.Left;
            var f0 = new Frame3(c2.WithZ(br.SeatElevation), along.WithZ(0), across.WithZ(0), new Vec3(0, 0, 1));
            string asm = $"BRG-{br.Name}";
            AddAssembly(asm, AssemblyKind.Bearing, $"Arch bearing {br.Name} ({br.Fixity}) at {br.Support}");
            double z = 0;
            Mesh Box(double l, double w, double z0, double z1) { var m = new Mesh(); MeshBuilder.AddBox(m, f0, -l / 2, l / 2, -w / 2, w / 2, z0, z1); return m; }
            AddPart($"{asm}-PAD", PartCategory.BearingPad, asm, $"Bearing pad {L(br.PadThickness)}", Box(br.MasonryLength, br.MasonryWidth, z, z + br.PadThickness), "Preformed bearing pad", 0);
            z += br.PadThickness;
            AddPart($"{asm}-MP", PartCategory.MasonryPlate, asm, $"PL {L(br.MasonryThickness)} x {L(br.MasonryWidth)} x {L(br.MasonryLength)} masonry plate", Box(br.MasonryLength, br.MasonryWidth, z, z + br.MasonryThickness), "ASTM A709 Gr. 50", br.MasonryLength);
            double zMasonryTop = z + br.MasonryThickness;
            z = zMasonryTop;
            AddPart($"{asm}-LS", PartCategory.SolePlate, asm, $"PL {L(br.LowerSoleThickness)} lower sole plate", Box(br.SoleLength, br.SoleWidth, z, z + br.LowerSoleThickness), "ASTM A709 Gr. 50", br.SoleLength);
            z += br.LowerSoleThickness;
            AddPart($"{asm}-EL", PartCategory.BearingPad, asm, $"Elastomer {L(br.ElastomerLength)} x {L(br.ElastomerWidth)} x {L(br.ElastomerHeight)} (60 duro, steel laminated)", Box(br.ElastomerLength, br.ElastomerWidth, z, z + br.ElastomerHeight), "Elastomer 60 durometer + A1011 Gr. 40 plates", 0);
            z += br.ElastomerHeight;
            // Beveled upper sole: A at the -X (west) end, C at the +X end, B at the CL.
            var us = new Mesh();
            double hl = br.SoleLength / 2;
            var prof = new[] { new Vec2(-hl, z), new Vec2(hl, z), new Vec2(hl, z + br.UpperSoleC), new Vec2(-hl, z + br.UpperSoleA) };
            RolledBuilder.AddExtrusion(us, prof, f0.Origin, f0.X, f0.Z, f0.Y, -br.SoleWidth / 2, br.SoleWidth / 2);
            AddPart($"{asm}-US", PartCategory.SolePlate, asm, $"PL beveled upper sole {L(br.UpperSoleA)} / {L(br.UpperSoleB)} / {L(br.UpperSoleC)}", us, "ASTM A709 Gr. 50", br.SoleLength);
            double zTop = z + br.UpperSoleB;
            var pin = new Mesh();
            RolledBuilder.AddTube(pin, new[] { f0.ToWorld(0, 0, zMasonryTop), f0.ToWorld(0, 0, zTop - br.UpperSoleB / 2) }, br.PinDiameter / 2, 16);
            AddPart($"{asm}-PIN", PartCategory.ShearPin, asm, $"{L(br.PinDiameter)} anchor pin (A829 / AISI 4140)", pin, "ASTM A829 (AISI 4140)", zTop - br.UpperSoleB / 2 - zMasonryTop);
            foreach (var (sa, sc) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
                Model.Steel.Bolts.Add(new BoltInstance
                {
                    Frame = new Frame3(f0.ToWorld(sa * br.AnchorAlong, sc * br.AnchorAcross, zMasonryTop), f0.X, f0.Y, f0.Z),
                    Grip = br.MasonryThickness + br.PadThickness, Length = br.AnchorEmbedment, Assembly = asm, Kind = BoltKind.Anchor,
                    Diameter = br.AnchorDiameter, Embedment = br.AnchorEmbedment,
                });
            double stackTop = br.SeatElevation + zTop;
            Model.Bearings.Add(new ArchBearingResult(br.Name, br.Support, top, br.SeatElevation, stackTop, top.Z));
            Model.BearingNames.Add(br.Name);
            Check($"bearing {br.Name}: underside of steel from the WP chain (plan) vs top of the {L(zTop)} bearing stack on the plan pedestal (model)", top.Z, stackTop, Tol,
                v => $"{ToFt(v):F3}'", "pedestal tops are the plan values (p.102); the WP chain is p.119 / p.124 - B1-B3, reported only");
            var so = _al.Inverse(c2);
            Model.Steel.BearingSeats.Add(new BearingSeat(sl.Name, Model.BearingNames.Count - 1, so.Station, br.SeatElevation, br.UpperSoleA,
                "Elastomeric (arch)", br.Fixity, c2, along, br.MasonryLength, br.MasonryWidth));
        }
    }
}
