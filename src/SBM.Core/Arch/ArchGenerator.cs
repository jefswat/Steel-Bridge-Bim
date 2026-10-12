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
public static class ArchGenerator
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

    private sealed class Gen
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
            var steel = new SteelModel { Bolt = new BoltGeometry(In(0.875)), BoltGrade = "A325" };
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
            foreach (var k in _d.Knuckles) BuildKnuckle(k);
            if (_d.Deck != null) BuildDeck(_d.Deck);
            for (int i = 0; i < _d.Bearings.Count; i++) BuildBearing(_d.Bearings[i], i);
            Info($"Arch: {Model.Steel.Parts.Count} steel parts, {Model.Steel.SteelMass * 2.20462:N0} lb steel (model), {Model.Hangers.Count} hangers, {Model.Floorbeams.Count} floorbeams.");
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
            Check("chord CL1-CL2 in plan (work points from the layout vs local chord)", planLocal, planWp, Tol, L,
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
            Mesh Plate(double n0, double n1, double z0, double z1)
            {
                var sec = new[] { new Vec2(n0, z0), new Vec2(n1, z0), new Vec2(n1, z1), new Vec2(n0, z1) };
                var rings = stations.Select(s =>
                {
                    var (p, ang) = geo.At(s);
                    var nl = Vec2.FromAngle(ang).Left;
                    return sec.Select(q => W((p + nl * q.X).WithZ(q.Y))).ToArray();
                }).ToList();
                var m = new Mesh();
                MeshBuilder.AddLoft(m, sec, rings);
                return m;
            }
            AddPart($"{asm}-F", PartCategory.TiePlate, asm, $"PL {L(t.FlangeThickness)} x {L(t.FlangeWidth)} tie flange",
                Plate(-t.FlangeWidth / 2, t.FlangeWidth / 2, 0, t.FlangeThickness), t.Material, geo.Length);
            AddPart($"{asm}-W", PartCategory.TiePlate, asm, $"PL {L(t.WebThickness)} x {L(t.WebHeight)} tie web",
                Plate(-t.WebThickness / 2, t.WebThickness / 2, t.FlangeThickness, t.FlangeThickness + t.WebHeight), t.Material, geo.Length);
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
                bool stub = cuts.Count > 0 && (k == 0 || k == bounds.Count - 2);
                if (stub)
                {
                    string km = $"{asm}-STUB{(k == 0 ? 1 : 2)}";
                    var asu = r.StubUnresolved.Length > 0 ? Assumption.Unresolved(r.StubUnresolved, $"{w.Name} envelope from the knuckle to the end splice") : null;
                    AddPart(km, PartCategory.KnucklePlate, asm, $"Knuckle arch stub ({w.Name} envelope)", m, "ASTM A709 Gr. 50", len, 0, asu);
                }
                else AddPart($"{asm}-{k + (cuts.Count > 0 ? 0 : 1)}", PartCategory.ArchRib, asm, $"{w.Name} rib segment", m, r.Material, len, w.WeightPerFootLb);
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
                Model.Hangers.Add(new ArchHangerResult(h.Name, wt, wr, len, h.PlanLength));
                if (!double.IsNaN(h.PlanLength)) Check($"hanger {h.Name}: pin-to-pin length", h.PlanLength, len, Tol, L);
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
            RolledBuilder.AddExtrusion(web, poly, org, ex, _fr.Z, en, -fb.WebThickness / 2, fb.WebThickness / 2);
            AddPart($"{asm}-W", PartCategory.FloorbeamPlate, asm, $"PL {L(fb.WebThickness)} floorbeam web {L(fb.DepthAtEnds)} - {L(fb.DepthAtCrown)} deep", web, fb.Material, s1 - s0);
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
            foreach (var br in _d.Bracing)
            {
                if (P(br.From) is not { } a || P(br.To) is not { } b) continue;
                var wa = W(a); var wb = W(b);
                var ax = (wb - wa).Normalized();
                var m = (_fr.Z - ax * Vec3.Dot(ax, _fr.Z)).Normalized();
                var v = Vec3.Cross(ax, m).Normalized();
                var mesh = new Mesh();
                RolledBuilder.AddExtrusion(mesh, sec, wa, m, v, ax, 0, (wb - wa).Length);
                AddPart(br.Name, PartCategory.ArchBracing, "UPPER-BRACING", $"{w.Name} {br.From}-{br.To}", mesh, _d.BracingMaterial, (wb - wa).Length, w.WeightPerFootLb, asu);
            }
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
                    foreach (var q in line) pts.Add(W((sa + e * q.X).WithZ(q.Y - dk.FormThickness)));
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
            var sheet = new Mesh();
            double area = 0;
            void Zone(int k0, int k1, bool form, double ext0z, double ext1z)
            {
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
                MeshBuilder.AddLoft(slab, sec, rings);
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
            AddAssembly("FORM-DECK", AssemblyKind.ArchDeckForm, "Corrugated steel deck form spanning the floorbeams");
            var fasu = dk.FormUnresolved.Length > 0 ? Assumption.Unresolved(dk.FormUnresolved, $"pitch {L(dk.FormPitch)}, top flat {L(dk.FormTopFlat)}, flute bottom {L(dk.FormFluteBottom)}") : null;
            AddPart("FORM-DECK", PartCategory.FormDeck, "FORM-DECK", dk.FormDescription.Length > 0 ? dk.FormDescription : $"{L(dk.FormDepth)} x {L(dk.FormPitch)} steel deck form", sheet,
                "ASTM A653 Gr. 33, G165 galvanized", 0, 0, fasu);
            BuildCurbs(dk, ka, kb);
            foreach (var t in dk.Tendons) Model.Tendons.Add((t.Name, t.Path.Select(p => W(p.Vec)).ToList(), t.Description));
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
            Model.Concrete.Haunches = curbs;
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
            // Long direction normal to the support line, pointing up-structure (+X).
            var along = sl.Direction.Left;
            if (Vec2.Dot(along, _fr.X.XY) < 0) along = -along;
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
            Check($"bearing {br.Name}: top of stack (plan pedestal + {L(zTop)} stack) vs underside of steel (WP chain)", top.Z, stackTop, Tol,
                v => $"{ToFt(v):F3}'", "pedestal tops are the plan values (p.102); the WP chain is p.119 / p.124 - B1-B3, reported only");
            var so = _al.Inverse(c2);
            Model.Steel.BearingSeats.Add(new BearingSeat(sl.Name, Model.BearingNames.Count - 1, so.Station, br.SeatElevation, br.UpperSoleA,
                "Elastomeric (arch)", br.Fixity, c2, along, br.MasonryLength, br.MasonryWidth));
        }
    }
}
