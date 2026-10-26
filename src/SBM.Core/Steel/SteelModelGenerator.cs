using System.Collections.Concurrent;
using System.Diagnostics;
using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Units;

namespace SBM.Core.Steel;

/// <summary>
/// Generates explicit 3D steel from framing. Runs in three phases:
/// 1. Layouts (sequential, cheap): field sections, plate profiles with shop/field tapers, field splice bolt layouts.
/// 2. Connections (parallel): cross frames, lateral bracing, field splices — these register the bolt holes they need
///    in girder plates and stiffeners.
/// 3. Members (parallel): girder plates (with holes), stiffeners (with holes), bearings, shear studs.
/// Conventions: top of web follows the deck (slab + haunch below finished grade); webs plumb; flanges level
/// transversely; intermediate stiffeners and cross frames are normal to the flanges, support stiffeners/frames plumb.
/// </summary>
public sealed partial class SteelModelGenerator
{
    private static readonly Vec3 Up = new(0, 0, 1);

    private readonly BridgeFraming _f;
    private readonly BridgeDefinition _def;
    private readonly UnitSystem _u;
    private readonly SteelModel _model;
    private readonly ConcurrentBag<SteelPart> _parts = new();
    private readonly ConcurrentBag<SteelAssembly> _assemblies = new();
    private readonly ConcurrentBag<BoltInstance> _bolts = new();
    private readonly ConcurrentBag<StudInstance> _studs = new();
    private readonly ConcurrentBag<BearingSeat> _seats = new();
    private readonly ConcurrentBag<CrossFrameDetail2D> _cfDetails = new();
    private readonly ConcurrentQueue<Diagnostic> _diag = new();

    /// <summary>Per girder: field section bounds (s0, s1) and their assembly marks.</summary>
    private readonly Dictionary<int, List<(double s0, double s1, string mark)>> _sections = new();
    private readonly Dictionary<int, PlateProfile> _top = new(), _bot = new(), _web = new();
    private readonly Dictionary<int, GirderHoles> _holes = new();
    /// <summary>Per girder: dapped ends (OPTIONAL SupportDefinition.GirderEndDap); empty = square ends.</summary>
    private readonly Dictionary<int, List<GirderEndDap>> _daps = new();

    /// <summary>Lowering of the top of web at path distance s of girder g by a dapped end (0 = none).</summary>
    private double DapDrop(int g, double s)
    {
        if (!_daps.TryGetValue(g, out var l) || l.Count == 0) return 0;
        double d = 0;
        foreach (var x in l) d = Math.Max(d, x.Drop(s));
        return d;
    }
    private double DapAngle(int g, double s)
    {
        if (!_daps.TryGetValue(g, out var l)) return 0;
        foreach (var x in l) if (x.Drop(s) > 0) return x.Angle(s);
        return 0;
    }
    private readonly Dictionary<int, ConcurrentBag<(double s0, double s1)>> _studExclusions = new();
    private readonly ConcurrentDictionary<(int girder, long key), ConcurrentBag<(Vec3 c, double d)>> _stiffHoles = new();

    private sealed class GirderHoles
    {
        public readonly ConcurrentBag<Hole> Top = new(), Bottom = new(), Web = new();
        /// <summary>The holes of one plate in a run-independent order (the bags fill from parallel connection tasks).</summary>
        public static List<Hole> Sorted(IEnumerable<Hole> bag) =>
            bag.OrderBy(h => h.Center.X).ThenBy(h => h.Center.Y).ThenBy(h => h.Diameter).ToList();
    }

    private SteelModelGenerator(BridgeFraming f, UnitSystem u)
    {
        _f = f;
        _def = f.Definition;
        _u = u;
        _model = new SteelModel
        {
            Bolt = new BoltGeometry(_def.Bolts.Diameter), BoltGrade = _def.Bolts.Grade,
            StudDiameter = _def.Studs.Diameter, StudHeadDiameter = _def.Studs.HeadDiameter, StudHeadHeight = _def.Studs.HeadHeight,
        };
    }

    public static SteelModel Generate(BridgeFraming framing, UnitSystem units, Project.GeometrySettings? geometry = null)
    {
        var g = new SteelModelGenerator(framing, units);
        if (geometry is { ExactCurvedGeometry: true })
        {
            g._geo = geometry;
            g._fitTol = geometry.FitToleranceFor(units);
            g._model.Nurbs = new NurbsReport { Tolerance = g._fitTol };
        }
        var sw = Stopwatch.StartNew();
        if (!framing.HasErrors && framing.Girders.Count > 0)
        {
            if (framing.Definition.Girders.IsTub) g.RunTub();   // OPTIONAL tub girders (TubGirderGenerator.cs); the I-girder path below is unchanged
            else g.Run();
        }
        g._model.Assemblies.AddRange(g._assemblies.OrderBy(a => a.Kind).ThenBy(a => a.Mark, MarkComparer.Instance));
        g._model.CrossFrameDetails.AddRange(g._cfDetails.OrderBy(c => c.Mark, MarkComparer.Instance));
        g._model.Parts.AddRange(g._parts.OrderBy(p => p.Assembly, MarkComparer.Instance).ThenBy(p => p.Mark, MarkComparer.Instance));
        g._model.Bolts.AddRange(g._bolts.OrderBy(b => b.Assembly, MarkComparer.Instance));
        g._model.Studs.AddRange(g._studs.OrderBy(b => b.Assembly, MarkComparer.Instance));
        g._model.BearingSeats.AddRange(g._seats.OrderBy(s => s.Station).ThenBy(s => s.Girder));
        g._model.IntegralCaps.AddRange(g._capReports.OrderBy(r => r.Mark, StringComparer.Ordinal));
        foreach (var r in g._model.IntegralCaps)
            r.SteelMassKg = g._model.Parts.Where(p => p.Category != PartCategory.Weld && (p.Assembly == r.Mark || p.Assembly.StartsWith(r.Mark + "-C", StringComparison.Ordinal))).Sum(p => p.Mass);
        g.ApplyFabricationRules();
        if (g._model.Parts.FirstOrDefault(p => p.Category == PartCategory.Weld && p.Assumption != null && p.Description.Contains(", held back ")) is { Assumption: { } wa })
            g.Warn($"{wa.Reason} {wa.Basis}.");
        g._model.Diagnostics.AddRange(g._diag.Distinct());
        g._model.GenerationTime = sw.Elapsed;
        return g._model;
    }

    private void Warn(string m) => _diag.Enqueue(new Diagnostic(DiagnosticSeverity.Warning, m));

    private GirderSection Sec => _def.Girders.Section;
    private double HoleDia => _model.Bolt.HoleDiameter;

    private void Run()
    {
        // Phase 1: layouts.
        SetupIntegralCaps();   // OPTIONAL integral steel caps: the girders are cut at the caps (before the field sections are formed)
        foreach (var g in _f.Girders)
        {
            _sections[g.Index] = FieldSections(g);
            _top[g.Index] = BuildProfile(g, GirderPlate.TopFlange);
            _bot[g.Index] = BuildProfile(g, GirderPlate.BottomFlange);
            _web[g.Index] = BuildProfile(g, GirderPlate.Web);
            _holes[g.Index] = new GirderHoles();
            _studExclusions[g.Index] = new ConcurrentBag<(double, double)>();
            _daps[g.Index] = GirderEndDap.For(_def, _f, g, Warn);
            if (_def.Girders.CrossGirder.DiscontinuousCopeLength is double cl && _f.CrossGirders.Any(x => x.Girder == g.Index))
                _studExclusions[g.Index].Add((g.Length - cl, g.Length + 1));   // no top flange over the cope
        }
        foreach (var grp in _daps.Values.SelectMany(l => l).GroupBy(d => (d.SupportName, d.HasCope)))
        {
            var d0 = grp.First();
            _diag.Enqueue(new Diagnostic(DiagnosticSeverity.Info, $"Girder end dap at {grp.Key.SupportName}: {grp.Count()} girder end(s), top flange dropped {In(d0.Def.FlangeDrop)} over {In(d0.Def.DropRun)} (R {In(d0.Def.BendRadius)}), lowered to {In(d0.Def.LowFlatFromBearing)} past the CL bearing"
                + (grp.Key.HasCope ? $", web end cope {In(d0.Def.CopeInset)} ({In(d0.Def.CopeEdge)} tabs, {In(d0.Def.CopeRun)} transition, R {In(d0.Def.CopeRadius)})" : "") + (d0.Def.Source != "" ? $" ({d0.Def.Source})" : "") + "."));
        }

        // Phase 2: connections (register holes).
        var conn = new List<Action>();
        foreach (var c in _f.CrossFrames) { var cc = c; conn.Add(() => BuildCrossFrame(cc)); }
        foreach (var sp in _f.Splices) { var s = sp; conn.Add(() => BuildFieldSplice(s)); }
        conn.Add(BuildLateralBracing);
        conn.Add(BuildInspectionAccess);
        conn.Add(BuildFlangeDeflectors);
        foreach (var x in _f.CrossGirders) { var xx = x; conn.Add(() => BuildCrossGirder(xx)); }   // registers bolt holes in girder webs
        foreach (var cp in _caps) { var cc = cp; conn.Add(() => BuildIntegralCap(cc)); }   // registers top flange holes and stud exclusions
        Parallel.ForEach(conn, a => a());
        BuildInspectionWalkways();   // after the connections: the walkways bear on the generated cross frames / cross girders

        // Phase 3: members.
        var members = new List<Action>();
        foreach (var g in _f.Girders) { var gg = g; members.Add(() => BuildGirder(gg)); members.Add(() => BuildStuds(gg)); }
        // Stiffener numbers (G2-2-CS1, CS2 ...) are assigned up front in girder-distance order, not by whichever parallel
        // task gets there first: a mark (and the IFC GlobalId derived from it) must name the same plate on every run.
        var stiffNo = new int[_f.Stiffeners.Count];
        var counters = new Dictionary<string, int>();
        foreach (int i in Enumerable.Range(0, _f.Stiffeners.Count)
                     .OrderBy(i => _f.Stiffeners[i].Girder).ThenBy(i => _f.Stiffeners[i].GirderDistance).ThenBy(i => i))
        {
            var st = _f.Stiffeners[i];
            if (st.Side is not (StiffenerSide.Both or StiffenerSide.Right or StiffenerSide.Left)) continue;   // no plate (BuildStiffener returns)
            string key = SectionMark(st.Girder, st.GirderDistance) + StiffenerCode(st.Kind);
            stiffNo[i] = counters[key] = counters.GetValueOrDefault(key) + 1;
        }
        for (int i = 0; i < _f.Stiffeners.Count; i++) { var ss = _f.Stiffeners[i]; int no = stiffNo[i]; members.Add(() => BuildStiffener(ss, no)); }
        foreach (var b in _f.Bearings)
        {
            if (_def.SupportAt(b.Support).IntegralSteelCap != null && _caps.Any(c => c.Support == b.Support)) continue;   // girders frame into the cap: no girder bearings
            var bb = b; members.Add(() => BuildBearing(bb));
        }
        Parallel.ForEach(members, a => a());
    }

    // ================================================================== formatting helpers

    private string In(double m) => _u.FormatLength(m, LengthKind.Short);
    private string Pl(double w, double t) => _u.IsUS ? $"PL {In(w)} x {In(t)}" : $"PL {w * 1000:0}x{t * 1000:0}";
    private static double Inch(double inches) => UnitSystem.In(inches);

    // ================================================================== weld termination (WeldTermination, BridgeDefinition.Welds)

    /// <summary>Hold-back of a fillet weld of the given size from free edges, clips and the end of contact (plan value or the AWS default).</summary>
    private double HoldBack(double size) => WeldTermination.HoldBack(_def.Welds, size);
    /// <summary>UNRESOLVED flag of the weld termination (null when the plans give it).</summary>
    private Assumption? WeldFlag(double size) => WeldTermination.Flag(_def.Welds, size, In);
    /// <summary>", held back 1/4" (p.120)" for weld descriptions.</summary>
    private string HoldNote(double size) => ", " + WeldTermination.Note(_def.Welds, size, In);

    // ================================================================== girder geometry helpers

    /// <summary>Distance along girder g where the radial line at a station crosses it (NaN if it doesn't).</summary>
    private double GirderDistanceAtStation(GirderResult g, double station)
    {
        var a = _f.Alignment;
        double d = a.StationToDistance(station);
        var p = a.PointAtDistance(d);
        var dir = Vec2.FromAngle(a.EvaluateAtDistance(d).angle).Right;
        for (int j = 0; j < g.Spans.Count; j++)
        {
            var c = g.Spans[j];
            if (c.Intersect(p, dir, out double u, (d - c.D0) / (c.D1 - c.D0), 0.0) && u >= 0 && u <= 1)
                return g.DistanceAt(j, u);
        }
        return double.NaN;
    }

    private List<double> SpliceJoints(int girder) =>
        _f.Splices.Where(s => s.Girder == girder).Select(s => s.GirderDistance).OrderBy(x => x).ToList();

    private List<(double s0, double s1, string mark)> FieldSections(GirderResult g)
    {
        double gap = _def.FieldSpliceGap;
        var list = new List<(double, double, string)>();
        double a = 0;
        int k = 1;
        // Field splice joints and (OPTIONAL) integral steel cap cuts: the girder stops at the end plates on both sides of the cap.
        var joints = SpliceJoints(g.Index).Select(c => (c, lo: gap / 2, hi: gap / 2))
            .Concat(CapCutsOf(g.Index).Select(c => (c: c.SC, lo: c.Half, hi: c.Half))).OrderBy(x => x.c).ToList();
        foreach (var (c, lo, hi) in joints)
        {
            list.Add((a, c - lo, $"{g.Name}-{k++}"));
            a = c + hi;
        }
        list.Add((a, g.Length, $"{g.Name}-{k}"));
        return list;
    }

    private string SectionMark(int girder, double s)
    {
        var secs = _sections[girder];
        foreach (var (s0, s1, mark) in secs)
            if (s <= s1 + 1e-6) return mark;
        return secs[^1].mark;
    }

    /// <summary>Plate size along a girder: runs between plate changes, plus linear transitions (tapers).</summary>
    private sealed class PlateProfile
    {
        public List<(double s0, double s1, PlateSegment seg)> Runs { get; } = new();
        public List<(double s0, double s1, double v0, double v1)> WidthT { get; } = new();
        public List<(double s0, double s1, double v0, double v1)> ThickT { get; } = new();

        private PlateSegment At(double s)
        {
            foreach (var r in Runs) if (s <= r.s1 + 1e-9) return r.seg;
            return Runs[^1].seg;
        }

        public double Width(double s)
        {
            foreach (var t in WidthT) if (s >= t.s0 - 1e-9 && s <= t.s1 + 1e-9) return t.v0 + (t.v1 - t.v0) * (s - t.s0) / (t.s1 - t.s0);
            return At(s).Width;
        }

        public double Thickness(double s)
        {
            foreach (var t in ThickT) if (s >= t.s0 - 1e-9 && s <= t.s1 + 1e-9) return t.v0 + (t.v1 - t.v0) * (s - t.s0) / (t.s1 - t.s0);
            return At(s).Thickness;
        }

        public double BaseThickness(double s) => At(s).Thickness;
        public double BaseWidth(double s) => At(s).Width;

        public IEnumerable<double> Breaks =>
            Runs.Select(r => r.s0).Concat(Runs.Select(r => r.s1))
                .Concat(WidthT.SelectMany(t => new[] { t.s0, t.s1 })).Concat(ThickT.SelectMany(t => new[] { t.s0, t.s1 }));

        /// <summary>Distinct plate pieces (run boundaries) inside [a, b].</summary>
        public IEnumerable<(double a, double b, PlateSegment seg)> PiecesWithin(double a, double b)
        {
            foreach (var r in Runs)
            {
                double p0 = Math.Max(a, r.s0), p1 = Math.Min(b, r.s1);
                if (p1 > p0 + 1e-4) yield return (p0, p1, r.seg);
            }
        }
    }

    private PlateProfile BuildProfile(GirderResult g, GirderPlate plate)
    {
        var p = new PlateProfile();
        var segs = Sec.Segments(plate);
        if (g.PlateRuns.Any(r => r.Plate == plate))
        {
            // Resolved by the framing (as-designed layouts or station-based plate changes).
            foreach (var r in g.PlateRuns.Where(r => r.Plate == plate))
                p.Runs.Add((r.S0, r.S1, new PlateSegment { Plate = plate, Width = r.Width, Thickness = r.Thickness }));
        }
        else
        {
            double a = 0;
            for (int i = 0; i < segs.Count; i++)
            {
                double b = g.Length;
                if (i < segs.Count - 1)
                {
                    b = GirderDistanceAtStation(g, segs[i].EndStation);
                    if (double.IsNaN(b)) b = segs[i].EndStation < g.Path.Station[0] ? 0 : g.Length;
                    b = Math.Clamp(b, a, g.Length);
                }
                if (b > a + 1e-6) p.Runs.Add((a, b, segs[i]));
                a = b;
            }
        }
        if (p.Runs.Count == 0) p.Runs.Add((0, g.Length, segs[^1]));
        if (plate == GirderPlate.Web) return p;

        var joints = SpliceJoints(g.Index);
        double gap = _def.FieldSpliceGap;
        bool AtJoint(double s) => joints.Any(j => Math.Abs(j - s) < 0.05);

        // Shop splices: 1:2.5 width taper and 1:2.5 thickness transition on the larger plate (AASHTO 6.13.6.2 / AWS D1.5).
        for (int i = 0; i + 1 < p.Runs.Count; i++)
        {
            double b = p.Runs[i].s1;
            if (AtJoint(b)) continue;
            var s0 = p.Runs[i].seg;
            var s1 = p.Runs[i + 1].seg;
            if (Math.Abs(s0.Width - s1.Width) > 1e-6)
            {
                double lt = 2.5 * Math.Abs(s0.Width - s1.Width) / 2;
                if (s0.Width > s1.Width) p.WidthT.Add((b - lt, b, s0.Width, s1.Width));
                else p.WidthT.Add((b, b + lt, s0.Width, s1.Width));
            }
            if (Math.Abs(s0.Thickness - s1.Thickness) > 1e-6)
            {
                double lt = 2.5 * Math.Abs(s0.Thickness - s1.Thickness);
                if (s0.Thickness > s1.Thickness) p.ThickT.Add((b - lt, b, s0.Thickness, s1.Thickness));
                else p.ThickT.Add((b, b + lt, s0.Thickness, s1.Thickness));
            }
        }

        // Field splices: flanges of different width are tapered (1:2.5) down to the narrower width at the joint so the
        // splice plates match both sides. Thickness differences are taken with fillers.
        foreach (double j in joints)
        {
            double wl = p.BaseWidth(j - gap / 2 - 1e-4), wr = p.BaseWidth(j + gap / 2 + 1e-4);
            if (Math.Abs(wl - wr) < 1e-6) continue;
            double lt = 2.5 * Math.Abs(wl - wr) / 2;
            if (wl > wr) p.WidthT.Add((j - gap / 2 - lt, j - gap / 2, wl, wr));
            else p.WidthT.Add((j + gap / 2, j + gap / 2 + lt, wl, wr));
        }
        return p;
    }

    /// <summary>Girder-coordinate map: (s, lateral r, vertical z relative to top of web) → world.</summary>
    private static Vec3 GirderPoint(GirderPath path, double s, double r, double z)
    {
        var (p, t, _) = path.At(s);
        return p + new Vec3(t.Right.X, t.Right.Y, 0) * r + Up * z;
    }

    /// <summary>Unit vector normal to the flanges (perpendicular to the girder's 3D tangent), orthogonal to <paramref name="across"/>.</summary>
    private static Vec3 FlangeNormal(GirderPath path, double s, Vec3 across)
    {
        var (_, t, _) = path.At(s);
        double g = path.GradeAt(s);
        var t3 = new Vec3(t.X, t.Y, g).Normalized();
        var w = (Up - t3 * Vec3.Dot(Up, t3)).Normalized();
        return (w - across * Vec3.Dot(w, across)).Normalized();
    }

    // ================================================================== girders

    /// <summary>Discontinuous girder end cope at its cross girder (OPTIONAL CrossGirderSection.DiscontinuousCopeLength): s where the flanges stop,
    /// tab top / bottom (girder z: 0 = top of web, down negative) and the cope radius; null = none.</summary>
    private (double sc, double zt, double zb, double r)? XgCope(GirderResult g)
    {
        var xg = _def.Girders.CrossGirder;
        if (xg.DiscontinuousCopeLength is not double len || !_f.CrossGirders.Any(x => x.Girder == g.Index)) return null;
        if (xg.TopBelowDiscontinuousWebTop is not double off || xg.DiscontinuousTabInset is not double ins)
        {
            Warn($"{g.Name}: discontinuous girder cope needs CrossGirderSection.TopBelowDiscontinuousWebTop and DiscontinuousTabInset; not modeled.");
            return null;
        }
        double zt = -off - ins, zb = -off - 2 * xg.FlangeThickness - xg.WebDepth + ins;
        return (g.Length - len, zt, zb, xg.DiscontinuousCopeRadius ?? 0);
    }

    private void BuildGirder(GirderResult g)
    {
        var path = g.Path;
        double d = Sec.WebDepth;
        var top = _top[g.Index];
        var bot = _bot[g.Index];
        var web = _web[g.Index];
        var holes = _holes[g.Index];
        var topHoles = GirderHoles.Sorted(holes.Top);
        var botHoles = GirderHoles.Sorted(holes.Bottom);
        var webHoles = GirderHoles.Sorted(holes.Web);
        var breaks = path.S.Concat(top.Breaks).Concat(bot.Breaks).ToList();
        var daps = _daps[g.Index];
        var dapS = daps.SelectMany(x => x.Samples()).ToList();
        var copeS = daps.SelectMany(x => x.CopeSamples()).ToList();
        int gi = g.Index;
        Func<double, double> drop = s => DapDrop(gi, s);
        var topBreaks = daps.Count == 0 ? breaks : breaks.Concat(dapS).ToList();
        var webBreaks = daps.Count == 0 ? (IEnumerable<double>)path.S : path.S.Concat(dapS).Concat(copeS).ToList();
        var topPlateBreaks = daps.Count == 0 ? top.Breaks : top.Breaks.Concat(dapS).ToList();

        Func<double, double, double, Vec3> map = (s, r, z) => GirderPoint(path, s, r, z);
        Func<double, double, double, Vec3> webMap = (s, z, r) => GirderPoint(path, s, r, z);

        foreach (var (s0, s1, mark) in _sections[g.Index])
        {
            _assemblies.Add(new SteelAssembly { Mark = mark, Kind = AssemblyKind.GirderFieldSection, Girder = g.Index,
                Description = $"{g.Name} field section, {_u.FormatLength(s1 - s0)}" });

            // OPTIONAL integral steel cap at this section's end / start: the top flange stops short of the cap flange, the web is coped under it.
            var capE = CapCutsOf(g.Index).FirstOrDefault(c => Math.Abs(c.SC - c.Half - s1) < 1e-6);
            var capS = CapCutsOf(g.Index).FirstOrDefault(c => Math.Abs(c.SC + c.Half - s0) < 1e-6);
            double tf0 = capS != null ? Math.Max(s0, capS.SC + capS.TopEnd) : s0, tf1 = capE != null ? Math.Min(s1, capE.SC - capE.TopEnd) : s1;
            // OPTIONAL discontinuous girder end at a cross girder: flanges stop at the cope, a web tab carries the end angles (sheet 121 A-A / E-E)
            var xgCope = XgCope(g);
            double bf1 = s1;
            if (xgCope is { } xc0 && s1 > xc0.sc)
            {
                tf1 = Math.Min(tf1, Math.Max(s0, xc0.sc)); bf1 = Math.Max(s0, xc0.sc);
            }
            var copes = new List<(double a, double b, double depth, CapCut cut)>();
            if (capE != null && tf1 < s1 - 1e-6) copes.Add((tf1, s1, CopeDepth(path, capE), capE));
            if (capS != null && tf0 > s0 + 1e-6) copes.Add((s0, tf0, CopeDepth(path, capS), capS));
            int n = 1;
            foreach (var (a, b, seg) in top.PiecesWithin(tf0, tf1))
            {
                var mesh = new Mesh();
                var hs = topHoles.Where(h => h.Center.X > a && h.Center.X < b).ToList();
                if (daps.Count == 0)
                    LongPlate(mesh, path, a, b, breaks, top.Breaks, s => -top.Width(s) / 2, s => top.Width(s) / 2, _ => 0, top.Thickness, hs, map, false, "girder flange");
                else   // bent flange at a dapped end: underside on the dap profile, thickness kept normal to the plate
                    LongPlate(mesh, path, a, b, topBreaks, topPlateBreaks, s => -top.Width(s) / 2, s => top.Width(s) / 2, s => -drop(s),
                        s => -drop(s) + top.Thickness(s) / Math.Cos(DapAngle(gi, s)), hs, map, false, "girder flange");
                _parts.Add(new SteelPart { Mark = $"{mark}-TF{n++}", Category = PartCategory.FlangePlate, Assembly = mark, Mesh = mesh,
                    Length = b - a, Description = Pl(seg.Width, seg.Thickness) });
            }
            n = 1;
            foreach (var (a, bFull, seg) in web.PiecesWithin(s0, s1))
            {
                var mesh = new Mesh();
                double tw = seg.Thickness;
                // discontinuous girder end at a cross girder: the web is full depth to the cope, then a coped tab (built after the main piece)
                double b = xgCope is { } xq && bFull > xq.sc + 1e-6 ? Math.Max(a, xq.sc) : bFull;
                var hsAll = webHoles.Where(h => h.Center.X > a && h.Center.X < bFull).ToList();
                var hs = hsAll.Where(h => h.Center.X < b).ToList();
                if (b - a < 1e-6) { }   // the whole piece is the tab
                else if (daps.Count == 0 && copes.Count == 0)
                    LongPlate(mesh, path, a, b, path.S, Array.Empty<double>(), _ => -d, _ => 0, _ => -tw / 2, _ => tw / 2, hs, webMap, true, "girder web");
                else if (daps.Count == 0)
                {
                    // coped web at an integral cap: full height up to the flange end, then the top lowered under the cap flange
                    var cutsW = copes.SelectMany(c => new[] { c.a, c.b }).Where(x => x > a + 1e-6 && x < b - 1e-6).Append(a).Append(b).Distinct().OrderBy(x => x).ToList();
                    for (int ci = 0; ci + 1 < cutsW.Count; ci++)
                    {
                        double pa = cutsW[ci], pb = cutsW[ci + 1], mid = (pa + pb) / 2;
                        double dep = copes.Where(c => mid > c.a && mid < c.b).Select(c => c.depth).DefaultIfEmpty(0).Max();
                        var cope = copes.Where(c => mid > c.a && mid < c.b).Select(c => ((CapCut, bool)?)(c.cut, true)).FirstOrDefault();
                        // the cap flange underside is a plane (cap slope), the girder top of web follows the grade: the cope face is cut to the plane, not parallel to the web top
                        Func<double, double> copeTop = cope is { } cc ? (ss => -(path.At(ss).Item1.Z - (cc.Item1.Cap.SurfaceZ(cc.Item1.Cap.S(path.At(ss).Item1.XY), cc.Item1.Cap.ZTow) - (cc.Item1.Cap.D.Connection?.CopeClearance ?? 0)))) : (_ => -dep);
                        LongPlate(mesh, path, pa, pb, path.S, Array.Empty<double>(), _ => -d, copeTop, _ => -tw / 2, _ => tw / 2,
                            hs.Where(h => h.Center.X > pa && h.Center.X < pb).ToList(), webMap, true, "girder web");
                    }
                }
                else
                {
                    // dapped end: web top follows the lowered flange; a cope zone keeps only the two end tabs
                    var cuts = new List<(double a, double b, bool cope, GirderEndDap? dap)>();
                    double m0 = a, m1 = b;
                    foreach (var dp in daps.Where(x => x.HasCope))
                    {
                        var (c0, c1) = dp.CopeZone;
                        double p0 = Math.Max(a, c0), p1 = Math.Min(b, c1);
                        if (p1 > p0 + 1e-6) cuts.Add((p0, p1, true, dp));
                        if (dp.AtStart) m0 = Math.Max(m0, c1); else m1 = Math.Min(m1, c0);
                    }
                    if (m1 > m0 + 1e-6)
                        LongPlate(mesh, path, m0, m1, webBreaks, dapS, _ => -d, s => -drop(s), _ => -tw / 2, _ => tw / 2,
                            hs.Where(h => h.Center.X > m0 && h.Center.X < m1).ToList(), webMap, true, "girder web");
                    foreach (var (p0, p1, _, dp) in cuts)
                    {
                        var none = Array.Empty<Hole>();
                        LongPlate(mesh, path, p0, p1, webBreaks, Array.Empty<double>(), s => -drop(s) - dp!.CopeDepth(s), s => -drop(s), _ => -tw / 2, _ => tw / 2, none, webMap, true, "girder web");
                        LongPlate(mesh, path, p0, p1, webBreaks, Array.Empty<double>(), _ => -d, s => -d + dp!.CopeDepth(s), _ => -tw / 2, _ => tw / 2, none, webMap, true, "girder web");
                    }
                }
                if (xgCope is { } xc && bFull > b + 1e-6)
                {
                    double R = xc.r, c0 = xc.sc;
                    double Arc(double ss) { double q = Math.Clamp(ss - c0 - R, -R, 0); return R - Math.Sqrt(Math.Max(0, R * R - q * q)); }
                    var arcS = Enumerable.Range(0, 9).Select(i => c0 + R * i / 8.0).Where(x => x > b && x < bFull).ToList();
                    LongPlate(mesh, path, b, bFull, path.S.Concat(arcS).ToList(), arcS, ss => xc.zb - Arc(ss), ss => xc.zt + Arc(ss), _ => -tw / 2, _ => tw / 2,
                        hsAll.Where(h => h.Center.X > b).ToList(), webMap, true, "girder web");
                }
                _parts.Add(new SteelPart { Mark = $"{mark}-W{n++}", Category = PartCategory.WebPlate, Assembly = mark, Mesh = mesh,
                    Length = bFull - a, Description = Pl(d, tw) });
            }
            n = 1;
            foreach (var (a, b, seg) in bot.PiecesWithin(s0, bf1))
            {
                var mesh = new Mesh();
                var hs = botHoles.Where(h => h.Center.X > a && h.Center.X < b).ToList();
                LongPlate(mesh, path, a, b, breaks, bot.Breaks, s => -bot.Width(s) / 2, s => bot.Width(s) / 2, s => -d - bot.Thickness(s), _ => -d, hs, map, false, "girder flange");
                _parts.Add(new SteelPart { Mark = $"{mark}-BF{n++}", Category = PartCategory.FlangePlate, Assembly = mark, Mesh = mesh,
                    Length = b - a, Description = Pl(seg.Width, seg.Thickness) });
            }

            // Continuous flange-to-web fillet welds, both sides, top and bottom.
            double aw = _def.Stiffeners.FlangeToWebWeld;
            var welds = new Mesh();
            double fwHb = HoldBack(aw), fwLen = 0;                   // held back from the plate ends of the field section
            foreach (var (rs, zs) in new[] { (-1, 0), (1, 0), (-1, 1), (1, 1) })
            {
                double dz = zs == 0 ? -aw : aw;
                bool topWeld = zs == 0;
                double wa = (topWeld ? tf0 : s0) + fwHb, wb = (topWeld ? tf1 : bf1) - fwHb;
                if (wb <= wa + WeldTermination.MinBead) continue;
                fwLen += wb - wa;
                SweepAlongGirder(welds, path, wa, wb, topWeld ? dapS.Where(x => x > wa && x < wb).ToArray() : Array.Empty<double>(), s =>
                {
                    double tw = web.Thickness(s) / 2;
                    double z = topWeld ? -drop(s) : -d;
                    return new[] { new Vec2(rs * tw, z), new Vec2(rs * (tw + aw), z), new Vec2(rs * tw, z + dz) };
                });
            }
            _parts.Add(new SteelPart
            {
                Mark = $"{mark}-FW", Category = PartCategory.Weld, Assembly = mark, Mesh = welds, Length = fwLen, Material = "E70XX",
                Description = $"Fillet weld {In(aw)}, flange to web, both sides{HoldNote(aw)}", Assumption = WeldFlag(aw),
            });
        }
    }

    /// <summary>Depth of the girder web cope under an integral cap's top flange (girder top of web down to the cap top of web less the clearance).</summary>
    private static double CopeDepth(GirderPath path, CapCut c)
    {
        var p = path.At(c.SC).Item1;
        double capTow = c.Cap.SurfaceZ(c.Cap.S(p.XY), c.Cap.ZTow);
        return Math.Max(0, p.Z - (capTow - (c.Cap.D.Connection?.CopeClearance ?? 0)));
    }

    /// <summary>OPTIONAL plan fabrication notes (<see cref="BridgeDefinition.Fabrication"/>) merged onto the matching parts.</summary>
    private void ApplyFabricationRules()
    {
        if (_def.Fabrication is not { Count: > 0 } rules) return;
        var asm = _model.Assemblies.GroupBy(a => a.Mark).ToDictionary(gr => gr.Key, gr => gr.First());
        int hit = 0;
        foreach (var p in _model.Parts)
        {
            asm.TryGetValue(p.Assembly, out var a);
            FabricationAttributes? acc = null;
            foreach (var r in rules)
            {
                if (!r.Matches(p.Category.ToString(), a?.Kind.ToString(), p.Mark, a?.Description ?? "", p.Description, a?.Girder ?? -1)) continue;
                if (r.Material is { Length: > 0 } mat) p.Material = mat;
                var set = r.Set.Clone();
                if (r.Note.Length > 0) set.Notes.Insert(0, r.Source.Length > 0 ? $"{r.Note} {r.Source}" : r.Note);
                acc = acc == null ? set : acc.Merge(set);
            }
            if (acc == null) continue;
            p.Fabrication = p.Fabrication == null ? acc : p.Fabrication.Merge(acc);
            hit++;
        }
        _diag.Enqueue(new Diagnostic(DiagnosticSeverity.Info, $"Fabrication notes: {rules.Count} rules applied to {hit} of {_model.Parts.Count} parts."));
        foreach (var r in rules.Where(r => !_model.Parts.Any(p => p.Fabrication?.Notes.Any(n => n.StartsWith(r.Note + " ", StringComparison.Ordinal) || n == r.Note) == true)))
            if (r.Note.Length > 0) _diag.Enqueue(new Diagnostic(DiagnosticSeverity.Info, $"Fabrication note {r.Note} ({r.Source}) matches no generated part."));
    }

    /// <summary>Sweeps a section (r = lateral, z = relative to top of web) along the girder path from s0 to s1.</summary>
    private static void SweepAlongGirder(Mesh mesh, GirderPath path, double s0, double s1, IEnumerable<double> extraBreaks,
        Func<double, Vec2[]> sectionAt)
    {
        var ss = path.SamplesBetween(s0, s1).Concat(extraBreaks.Where(b => b > s0 + 1e-6 && b < s1 - 1e-6))
            .Append(s0).Append(s1).Distinct().OrderBy(x => x).ToList();
        var rings = new List<Vec3[]>(ss.Count);
        Vec2[]? sec0 = null;
        foreach (double s in ss)
        {
            var sec = sectionAt(s);
            sec0 ??= sec;
            rings.Add(sec.Select(q => GirderPoint(path, s, q.X, q.Y)).ToArray());
        }
        MeshBuilder.AddLoft(mesh, sec0!, rings);
    }

    // ================================================================== stiffeners

    private static string StiffenerCode(StiffenerKind k) => k switch { StiffenerKind.Bearing => "BS", StiffenerKind.ConnectionPlate => "CS", _ => "IS" };

    private static long SKey(double s) => (long)Math.Round(s * 100); // 1 cm buckets

    /// <summary>Records a bolt hole through the stiffener at girder distance s.</summary>
    private void AddStiffenerHole(int girder, double s, Vec3 center, double dia) =>
        _stiffHoles.GetOrAdd((girder, SKey(s)), _ => new ConcurrentBag<(Vec3, double)>()).Add((center, dia));

    private IEnumerable<(Vec3 c, double d)> StiffenerHoles(int girder, double s)
    {
        long k = SKey(s);
        for (long dk = -1; dk <= 1; dk++)
            if (_stiffHoles.TryGetValue((girder, k + dk), out var bag))
                foreach (var h in bag.OrderBy(h => h.c.X).ThenBy(h => h.c.Y).ThenBy(h => h.c.Z).ThenBy(h => h.d))   // run-independent order
                    yield return h;
    }

    private void BuildStiffener(StiffenerResult s, int n)
    {
        var g = _f.Girders[s.Girder];
        var (p, t, sta) = g.Path.At(s.GirderDistance);
        double d = Sec.WebDepth;
        double tw = _web[s.Girder].Thickness(s.GirderDistance);
        var sd = _def.Stiffeners;
        double c = sd.CornerClip;
        bool weldBottom = s.Kind != StiffenerKind.Intermediate;
        double h = weldBottom ? d : d - sd.IntermediateBottomGap;
        double dapDrop = DapDrop(s.Girder, s.GirderDistance);
        if (dapDrop > 0) { p -= Up * dapDrop; h -= dapDrop; }   // dapped girder end: the stiffener stops under the lowered flange
        double w = s.Plate.Width, tp = s.Plate.Thickness;

        var u = new Vec3(s.Direction.X, s.Direction.Y, 0);
        // Bearing stiffeners are plumb; connection plates and intermediate stiffeners are normal to the flanges.
        var y = s.Kind == StiffenerKind.Bearing ? Up : FlangeNormal(g.Path, s.GirderDistance, u);
        var frame = new Frame3(p, u, y, Vec3.Cross(u, y));
        double sin = Math.Abs(Vec2.Cross(t, s.Direction));
        double xf = tw / 2 / Math.Max(sin, 0.2);

        string mark = SectionMark(s.Girder, s.GirderDistance);
        var (cat, code, label) = s.Kind switch
        {
            StiffenerKind.Bearing => (PartCategory.BearingStiffener, "BS", "Bearing stiffener"),
            StiffenerKind.ConnectionPlate => (PartCategory.ConnectionStiffener, "CS", "Connection stiffener"),
            _ => (PartCategory.IntermediateStiffener, "IS", "Intermediate stiffener"),
        };
        double topW = _top[s.Girder].Width(s.GirderDistance), botW = _bot[s.Girder].Width(s.GirderDistance);
        // ClipToTopFlange: the plate may be wider than the top flange (clipped below), so only the bottom flange limits it
        double flangeW = sd.ClipToTopFlange ? botW : Math.Min(topW, botW);
        // plate-frame x of the top flange edge (the plate runs along u, the flange edge is flangeW/2 from the web centre line across the girder)
        double xe = topW / 2 / Math.Max(sin, 0.2);
        bool clipTop = sd.ClipToTopFlange && xf + c < xe - 1e-4 && xf + w > xe + 1e-4;
        if ((xf + w) * sin > flangeW / 2 + 1e-4)
            Warn($"{g.Name}: {label.ToLowerInvariant()} at {_u.FormatStation(sta)} projects beyond the flange edge (skewed {Numerics.RadToDeg(s.SkewAngle):F1}°).");

        // Bolt holes registered by cross frames, converted to plate coordinates.
        var holes = StiffenerHoles(s.Girder, s.GirderDistance)
            .Select(hh => (x: Vec3.Dot(hh.c - p, frame.X), yy: Vec3.Dot(hh.c - p, frame.Y), hh.d)).ToList();

        var mesh = new Mesh();
        var welds = new Mesh();
        double weldLen = 0;
        foreach (int side in new[] { 1, -1 })
        {
            if (side == 1 && s.Side is not (StiffenerSide.Both or StiffenerSide.Right)) continue;
            if (side == -1 && s.Side is not (StiffenerSide.Both or StiffenerSide.Left)) continue;
            var poly = new List<Vec2> { new(xf, -c), new(xf + c, 0) };
            if (clipTop) { poly.Add(new Vec2(xe, 0)); poly.Add(new Vec2(xf + w, -(xf + w - xe))); }   // 45 deg back to the top flange edge (p.32 Sect. A-A)
            else poly.Add(new Vec2(xf + w, 0));
            poly.Add(new Vec2(xf + w, -h));
            if (weldBottom) { poly.Add(new Vec2(xf + c, -h)); poly.Add(new Vec2(xf, -h + c)); }
            else poly.Add(new Vec2(xf, -h));
            var pts = poly.Select(q => new Vec2(q.X * side, q.Y)).ToList();
            var hs = holes.Where(hh => hh.x * side > xf && hh.x * side < xf + w && hh.yy < 0 && hh.yy > -h)
                .Select(hh => new Hole(new Vec2(hh.x, hh.yy), hh.d)).ToList();
            PlateBuilder.AddPlanar(mesh, pts, hs, frame, -tp / 2, tp / 2);

            double a = sd.WeldSize;
            var along = u * side;
            var tang3 = new Vec3(t.X, t.Y, 0);
            // Weld termination: the stiffener-to-web weld stops short of the corner clips (hold-back past the clip, or the plans' distance from the toe of
            // the flange-to-web weld, Iowa X = 5T min 2 1/4") or of the free plate end (intermediate stiffener gap); the flange welds stop the hold-back
            // short of the clip and of the stiffener's outer edge.
            double hb = HoldBack(a);
            double webEnd = c + hb;
            if (_def.Welds is { StiffenerWebEndFactor: > 0 } or { StiffenerWebEndMin: > 0 })
                webEnd = Math.Max(webEnd, sd.FlangeToWebWeld + Math.Max(_def.Welds!.StiffenerWebEndFactor * tw, _def.Welds.StiffenerWebEndMin));
            foreach (int face in new[] { 1, -1 })
            {
                var nrm = frame.Z * face;
                var webLeg = Vec3.Dot(tang3, nrm) >= 0 ? tang3 : -tang3;
                var off = nrm * (tp / 2);
                weldLen += welds.AddHeld(frame.ToWorld(side * xf, 0, 0) + off, frame.ToWorld(side * xf, -h, 0) + off, webLeg, along, a, webEnd, weldBottom ? webEnd : hb);
                // clipped outer top corner (Nebraska p.32): the flange weld stops at the clip, held back from it
                weldLen += welds.AddHeld(frame.ToWorld(side * (xf + c), 0, 0) + off, frame.ToWorld(side * (clipTop ? xe : xf + w), 0, 0) + off, -frame.Y, nrm, a, hb, hb);
                if (weldBottom)
                    weldLen += welds.AddHeld(frame.ToWorld(side * (xf + c), -h, 0) + off, frame.ToWorld(side * (xf + w), -h, 0) + off, frame.Y, nrm, a, hb, hb);
            }
        }
        if (mesh.IsEmpty) return;
        string desc = Pl(w, tp) + (clipTop ? $" (outer top corner clipped to the {In(topW)} top flange)" : "") + (s.SkewAngle > Numerics.DegToRad(0.5) ? $", skewed {Numerics.RadToDeg(s.SkewAngle):F1}°" : "")
                      + (s.Side == StiffenerSide.Both ? " (pair)" : "");
        _parts.Add(new SteelPart { Mark = $"{mark}-{code}{n}", Category = cat, Assembly = mark, Description = desc, Mesh = mesh, Length = h });
        _parts.Add(new SteelPart
        {
            Mark = $"{mark}-{code}{n}-W", Category = PartCategory.Weld, Assembly = mark, Mesh = welds, Material = "E70XX", Length = weldLen,
            Description = $"Fillet weld {In(sd.WeldSize)}, {label.ToLowerInvariant()}{HoldNote(sd.WeldSize)}", Assumption = WeldFlag(sd.WeldSize),
        });
    }

    // ================================================================== shear studs

    private void BuildStuds(GirderResult g)
    {
        var sd = _def.Studs;
        if (!sd.Enabled || sd.Pitch <= 0 || sd.RowsAcross < 1) return;
        var path = g.Path;
        var top = _top[g.Index];
        var align = _f.Alignment;
        double clear = sd.Clearance;
        var excl = _studExclusions[g.Index].ToList();
        var boltHoles = GirderHoles.Sorted(_holes[g.Index].Top);
        double slab = _def.Deck.Thickness, haunch = _def.Deck.Haunch;
        double headR = _model.Bolt.HeadAcrossFlats / Math.Sqrt(3);
        bool coverWarned = false;

        foreach (double s in StudRows(g, sd, clear))
        {
            if (excl.Any(e => s > e.s0 - clear && s < e.s1 + clear))
            {
                if (sd.Zones.Count > 0) Warn($"{g.Name}: stud row at {_u.FormatStation(path.At(s).Item3)} (plan zone) omitted - inside a splice / cap exclusion.");
                continue;
            }
            var (p, t, sta) = path.At(s);
            // Deck cross slope at the girder (finished grade is parallel to the deck bottom).
            var so = align.Inverse(p.XY);
            double e0 = align.ElevationAt(so.Station, so.Offset - 0.05), e1 = align.ElevationAt(so.Station, so.Offset + 0.05);
            double slope = (e1 - e0) / 0.1;
            double tf = top.Thickness(s);
            double halfW = top.Width(s) / 2;
            string mark = SectionMark(g.Index, s);
            var right = new Vec3(t.Right.X, t.Right.Y, 0);
            double tsp = sd.EdgeDistance > 0 && sd.RowsAcross > 1 ? (top.Width(s) - 2 * sd.EdgeDistance) / (sd.RowsAcross - 1) : sd.TransverseSpacing;
            for (int k = 0; k < sd.RowsAcross; k++)
            {
                double r = (k - (sd.RowsAcross - 1) / 2.0) * tsp;
                if (Math.Abs(r) + sd.HeadDiameter / 2 > halfW) continue;
                if (boltHoles.Any(h => Math.Abs(h.Center.X - s) < headR + clear && Math.Abs(h.Center.Y - r) < headR + clear)) continue;
                double gapToDeck = haunch - tf + slope * r;
                double need = gapToDeck + sd.Penetration;
                double len = Math.Max(4 * sd.Diameter, Math.Ceiling(need / sd.LengthIncrement - 1e-9) * sd.LengthIncrement);
                if (!coverWarned && len > gapToDeck + slab - sd.MinCover)
                {
                    coverWarned = true;
                    Warn($"{g.Name}: {In(len)} studs at {_u.FormatStation(sta)} leave less than {In(sd.MinCover)} cover; check haunch and slab.");
                }
                var origin = p + right * r + Up * (tf - DapDrop(g.Index, s));   // dapped end: studs stand on the lowered flange (length as computed for the normal haunch)
                _studs.Add(new StudInstance { Frame = new Frame3(origin, new Vec3(t.X, t.Y, 0), Vec3.Cross(Up, new Vec3(t.X, t.Y, 0)), Up), Length = len, Assembly = mark });
            }
        }
    }

    /// <summary>Stud row positions along a girder: the uniform pitch, or the plan zones (equal spaces, a shared end row once).</summary>
    private IEnumerable<double> StudRows(GirderResult g, StudDefinition sd, double clear)
    {
        if (sd.Zones.Count == 0)
        {
            for (double s = clear + sd.Pitch / 2; s < g.Length - clear; s += sd.Pitch) yield return s;
            yield break;
        }
        double Loc(StudLocation l)
        {
            switch (l.Kind)
            {
                case StudLocationKind.GirderStart: return l.Offset;
                case StudLocationKind.GirderEnd: return g.Length + l.Offset;
                case StudLocationKind.Splice:
                    var sp = _f.Splices.FirstOrDefault(x => x.Girder == g.Index && x.Number == l.Number);
                    return sp == null ? double.NaN : sp.GirderDistance + l.Offset;
                default:
                    if (l.Number < 0 || l.Number > g.Spans.Count) return double.NaN;
                    return (l.Number == 0 ? g.StartExtension : g.DistanceAt(l.Number - 1, 1.0)) + l.Offset;   // DistanceAt takes the span fraction u
            }
        }
        var rows = new List<double>();
        foreach (var z in sd.Zones)
        {
            if (z.Girders.Count > 0 && !z.Girders.Contains(g.Index)) continue;
            double a = Loc(z.From), b = Loc(z.To);
            if (double.IsNaN(a) || double.IsNaN(b) || z.Spaces < 1) { Warn($"{g.Name}: stud zone {z.From}..{z.To}: location not found."); continue; }
            for (int i = 0; i <= z.Spaces; i++)
            {
                double s = a + (b - a) * i / z.Spaces;
                if (rows.Any(r => Math.Abs(r - s) < 0.005)) continue;   // a row shared by two zones
                rows.Add(s);
            }
        }
        int outside = rows.Count(s => s < 0 || s > g.Length);
        if (outside > 0) Warn($"{g.Name}: {outside} stud row(s) of the plan zones fall outside the girder (0 .. {_u.FormatLength(g.Length, LengthKind.Long)}) - not placed; check the zone locations.");
        foreach (double s in rows.Where(s => s >= 0 && s <= g.Length).OrderBy(x => x)) yield return s;
    }

    /// <summary>Orders marks naturally (G1-2 before G1-10).</summary>
    public sealed class MarkComparer : IComparer<string>
    {
        public static readonly MarkComparer Instance = new();
        public int Compare(string? x, string? y)
        {
            if (x == null || y == null) return string.Compare(x, y, StringComparison.Ordinal);
            int i = 0, j = 0;
            while (i < x.Length && j < y.Length)
            {
                if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
                {
                    int i0 = i, j0 = j;
                    while (i < x.Length && char.IsDigit(x[i])) i++;
                    while (j < y.Length && char.IsDigit(y[j])) j++;
                    int c = long.Parse(x[i0..i]).CompareTo(long.Parse(y[j0..j]));
                    if (c != 0) return c;
                }
                else
                {
                    int c = x[i].CompareTo(y[j]);
                    if (c != 0) return c;
                    i++; j++;
                }
            }
            return x.Length.CompareTo(y.Length);
        }
    }
}

internal static class MeshWeldExtensions
{
    public static void AddWeld(this Mesh m, Vec3 p0, Vec3 p1, Vec3 leg1, Vec3 leg2, double size) =>
        MeshBuilder.AddFilletWeld(m, p0, p1, leg1.Normalized(), leg2.Normalized(), size);
}
