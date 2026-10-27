using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
using SBM.Core.Substructure;
using SBM.Core.Units;

namespace SBM.Core.CableStayed;

/// <summary>
/// Cable-stayed footbridge geometry from <see cref="CableStayedDefinition"/> (pure function framing -> <see cref="CableStayedModel"/>): deck swept along the
/// alignment / profile, pylon trunk + curved legs + cross beams + struts, stays, piers / abutment seats / footings, then the analysis wireframe and the
/// first-cut forces (<see cref="CableStayedAnalysis"/>). Every value is an input; inputs that are missing are reported, nothing is assumed.
/// </summary>
public sealed class CableStayedGenerator
{
    private readonly BridgeFraming _f;
    private readonly CableStayedDefinition _d;
    private readonly UnitSystem _u;
    private readonly CableStayedModel _m = new();
    private readonly Alignments.Alignment _al;
    private Assumption? _flag;
    private SupportLineResult? _pylonSupport;

    private CableStayedGenerator(BridgeFraming f, UnitSystem u)
    {
        _f = f; _u = u; _d = f.Definition.CableStayed!; _al = f.Alignment;
    }

    public static CableStayedModel Generate(BridgeFraming f, UnitSystem units)
    {
        var g = new CableStayedGenerator(f, units);
        g.Run();
        return g._m;
    }

    private void Warn(string m) => _m.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, m));
    private void Info(string m) => _m.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info, m));
    private string L(double m) => _u.FormatLength(m);
    private string Area(double m2) => _u.IsUS ? $"{m2 / (0.0254 * 0.0254):0.00} in²" : $"{m2 * 1e6:0} mm²";

    private void Run()
    {
        _m.Steel.CableStayed = _m;
        _m.Concrete.CableStayed = _m;
        foreach (var e in _d.Validate()) Warn(e);
        foreach (var x in _d.Unresolved) Warn(x.StartsWith("UNRESOLVED", StringComparison.Ordinal) ? x : "UNRESOLVED (cable-stayed): " + x);
        if (_d.Unresolved.Count > 0)
            _flag = Assumption.UnresolvedDimension(string.Join(" | ", _d.Unresolved.Take(3)) + (_d.Unresolved.Count > 3 ? " ..." : ""),
                (_d.Concept.Length > 0 ? _d.Concept : "concept values") + (_d.Source.Length > 0 ? $" ({_d.Source})" : ""));
        if (_f.Supports.Count < 2) { Warn("Cable-stayed: at least two support lines are required (deck ends)."); return; }
        foreach (var s in _f.Supports)
            _m.Supports.Add((s.Name, s.Station, s.Name.Equals(_d.Pylon.Support, StringComparison.OrdinalIgnoreCase)));
        _pylonSupport = _f.Supports.FirstOrDefault(s => s.Name.Equals(_d.Pylon.Support, StringComparison.OrdinalIgnoreCase));
        if (_pylonSupport == null) Warn($"Cable-stayed: the pylon support line '{_d.Pylon.Support}' does not exist (supports: {string.Join(", ", _f.Supports.Select(s => s.Name))}).");

        BuildDeck();
        if (_pylonSupport != null && !double.IsNaN(_d.Pylon.BaseElevation))
        {
            BuildPylon();
            BuildStays();
        }
        BuildPiers();
        CableStayedAnalysis.Build(_f, _d, _m);
        Clearances();
        _m.Steel.Diagnostics.AddRange(_m.Diagnostics);
    }

    // ------------------------------------------------------------------ deck

    private double Grade(double sta) => _al.Profile.ElevationAt(sta);

    private void BuildDeck()
    {
        var dd = _d.Deck;
        var outline = dd.Outline();
        if (outline.Count < 3) return;
        if (MeshBuilder.SignedArea(outline) < 0) outline.Reverse();
        _m.DeckSection.AddRange(outline);
        _m.DeckProps = CableStayedAnalysis.PolygonProps(outline, dd.WallThickness);
        double s0 = double.IsNaN(dd.BeginStation) ? _f.Supports[0].Station - dd.EndOverhang : dd.BeginStation;
        double s1 = double.IsNaN(dd.EndStation) ? _f.Supports[^1].Station + dd.EndOverhang : dd.EndStation;
        if (s1 <= s0) { Warn("Cable-stayed deck: the end station is not after the begin station."); return; }
        _m.DeckBegin = s0; _m.DeckEnd = s1;
        var stations = DeckStations(s0, s1);

        // one piece per span (between support stations inside the deck)
        var cuts = new List<double> { s0 };
        cuts.AddRange(_f.Supports.Select(s => s.Station).Where(x => x > s0 + 0.01 && x < s1 - 0.01).OrderBy(x => x));
        cuts.Add(s1);
        bool steel = dd.Material == CsMaterial.Steel;
        string asm = "CSD";
        if (steel) _m.Steel.Assemblies.Add(new SteelAssembly { Mark = asm, Kind = AssemblyKind.CsDeck, Description = "Cable-stayed deck" });
        var concrete = new Mesh();
        for (int k = 0; k + 1 < cuts.Count; k++)
        {
            var st = stations.Where(x => x > cuts[k] + 1e-6 && x < cuts[k + 1] - 1e-6).Prepend(cuts[k]).Append(cuts[k + 1]).ToList();
            var mesh = steel ? new Mesh() : concrete;
            Sweep(mesh, outline, u => Ring(outline, u), st, "deck");
            if (steel)
                _m.Steel.Parts.Add(new SteelPart
                {
                    Mark = $"CSD-{k + 1}", Category = PartCategory.CsDeck, Assembly = asm, Mesh = mesh, Length = st[^1] - st[0],
                    Description = $"Deck {L(dd.TopWidth > 0 ? dd.TopWidth : outline.Max(p => p.X) - outline.Min(p => p.X))} wide, sta {_u.FormatStation(st[0])} to {_u.FormatStation(st[^1])}"
                        + (dd.WallThickness > 0 ? $" (envelope of a box; equivalent wall {L(dd.WallThickness)})" : ""),
                    Assumption = _flag,
                    Density = dd.WallThickness > 0 ? CableStayedAnalysis.EquivalentDensity(_m.DeckProps.A, Math.Abs(MeshBuilder.SignedArea(outline)), _d.Analysis.SteelDensity) : 0,
                });
        }
        if (!steel) _m.Concrete.Deck = concrete;

        // barrier panels on both edges
        if (dd.BarrierHeight > 0)
        {
            double xr = outline.Max(p => p.X), xl = outline.Min(p => p.X), top = outline.Max(p => p.Y);
            _m.Steel.Assemblies.Add(new SteelAssembly { Mark = "CSB", Kind = AssemblyKind.Railing, Description = $"Deck barrier panels ({dd.BarrierMaterial})",
                PredefinedType = "GUARDRAIL" });
            foreach (var (side, x) in new[] { ("L", xl + dd.BarrierInset), ("R", xr - dd.BarrierInset - dd.BarrierThickness) })
            {
                var sec = new List<Vec2> { new(x, top), new(x + dd.BarrierThickness, top), new(x + dd.BarrierThickness, top + dd.BarrierHeight), new(x, top + dd.BarrierHeight) };
                var mesh = new Mesh();
                Sweep(mesh, sec, u => Ring(sec, u), stations, "barrier");
                _m.Steel.Parts.Add(new SteelPart
                {
                    Mark = $"CSB-{side}", Category = PartCategory.RailEnvelope, Assembly = "CSB", Mesh = mesh, Length = s1 - s0, Material = dd.BarrierMaterial,
                    Density = dd.BarrierDensity, Description = $"{dd.BarrierMaterial} barrier panel {L(dd.BarrierHeight)} high, {(side == "L" ? "left" : "right")} edge", Assumption = _flag,
                });
            }
        }
    }

    /// <summary>Deck ring stations: equal steps no longer than the ring spacing, plus supports and stay anchors.</summary>
    private List<double> DeckStations(double s0, double s1)
    {
        double step = Math.Max(0.1, _d.Deck.RingSpacing);
        int n = Math.Max(1, (int)Math.Ceiling((s1 - s0) / step));
        var st = Enumerable.Range(0, n + 1).Select(i => s0 + (s1 - s0) * i / n).ToList();
        st.AddRange(_f.Supports.Select(s => s.Station).Where(x => x > s0 && x < s1));
        foreach (var g in _d.Stays.Where(g => g.GroundAnchors.Count == 0))
            for (int i = 0; i < g.Count; i++) { double x = AnchorStation(g, i); if (x > s0 && x < s1) st.Add(x); }
        st.Sort();
        var r = new List<double>();
        foreach (var x in st) if (r.Count == 0 || x - r[^1] > 0.05) r.Add(x);
        if (r[^1] < s1 - 1e-9) r[^1] = s1;
        return r;
    }

    private Vec3[] Ring(List<Vec2> sec, double sta)
    {
        double z0 = Grade(sta);
        return sec.Select(p => _al.PointAt(sta, p.X).WithZ(z0 + p.Y)).ToArray();
    }

    private static double AnchorStation(CsStayGroup g, int i) => g.Count <= 1 ? g.FirstStation : g.FirstStation + (g.LastStation - g.FirstStation) * i / (g.Count - 1);

    // ------------------------------------------------------------------ pylon

    private Frame3 _pf;

    private Vec3 P(Vec3 local) => _pf.ToWorld(local);

    private void BuildPylon()
    {
        var py = _d.Pylon;
        var sl = _pylonSupport!;
        double a = _al.AngleAt(sl.Station) + py.RotationDeg * Math.PI / 180;
        var x = Vec2.FromAngle(a);
        var y = x.Right;
        var o = sl.AlignmentPoint + x * py.OriginX + y * py.OriginY;
        _pf = new Frame3(o.WithZ(py.BaseElevation), x.WithZ(0), y.WithZ(0), new Vec3(0, 0, 1));
        _m.PylonFrame = _pf;
        _m.Steel.Assemblies.Add(new SteelAssembly { Mark = "PY", Kind = AssemblyKind.Pylon, Description = $"Pylon at {sl.Name}" });

        // trunk: base to the leg junction
        if (py.TrunkHeight > 0)
        {
            var a0 = P(Vec3.Zero); var a1 = P(new Vec3(py.TrunkTopX, py.TrunkTopY, py.TrunkHeight));
            var mesh = Member(new[] { a0, a1 }, py.TrunkSection, _pf.Y);
            if (py.TrunkSection.Material == CsMaterial.Steel)
                _m.Steel.Parts.Add(new SteelPart { Mark = "PY-T", Category = PartCategory.Pylon, Assembly = "PY", Mesh = mesh, Length = (a1 - a0).Length,
                    Description = $"Pylon trunk {Sec(py.TrunkSection)}", Assumption = _flag, Density = Dens(py.TrunkSection) });
            else _pylonConcrete.Add(new SubSolid(SubElementKind.Column, $"{sl.Name} pylon trunk", $"Pylon trunk {Sec(py.TrunkSection)}", mesh) { Assumption = _flag });
        }
        // legs
        for (int i = 0; i < py.Legs.Count; i++)
        {
            var leg = py.Legs[i];
            string name = leg.Name.Length > 0 ? leg.Name : $"Leg {i + 1}";
            var curve = LegCurve(leg, name);
            _m.Legs.Add(curve);
            int n = Math.Max(2, leg.Segments);
            var mesh = new Mesh();
            var rect = new List<Vec2> { new(-0.5, -0.5), new(0.5, -0.5), new(0.5, 0.5), new(-0.5, 0.5) };
            Sweep(mesh, rect, t => LegRing(leg, t), Enumerable.Range(0, n + 1).Select(k => (double)k / n).ToList(), "pylon leg");
            var part = new SteelPart { Mark = $"PY-L{i + 1}", Category = PartCategory.Pylon, Assembly = "PY", Mesh = mesh, Length = curve.Length,
                Description = $"Pylon leg {name}, {Sec(leg.Section)}, {L(curve.Length)} long, tip {L(leg.Tip.Z)} above the base", Assumption = _flag, Density = Dens(leg.Section) };
            if (leg.Section.Material == CsMaterial.Steel) _m.Steel.Parts.Add(part);
            else _pylonConcrete.Add(new SubSolid(SubElementKind.Column, $"{sl.Name} pylon leg {name}", part.Description, mesh) { Assumption = _flag });
        }
        // cross beams
        int cb = 1;
        foreach (var c in py.CrossBeams)
        {
            if (c.LegA >= _m.Legs.Count || c.LegB >= _m.Legs.Count) { Warn($"Pylon cross beam '{c.Name}': leg {c.LegA} / {c.LegB} does not exist."); continue; }
            var pa = AtHeight(_m.Legs[c.LegA], c.Height); var pb = AtHeight(_m.Legs[c.LegB], c.Height);
            var mesh = Member(new[] { pa, pb }, c.Section, _pf.X);
            _m.Steel.Parts.Add(new SteelPart { Mark = $"PY-X{cb++}", Category = PartCategory.PylonCrossBeam, Assembly = "PY", Mesh = mesh, Length = (pb - pa).Length,
                Description = $"Pylon cross beam {(c.Name.Length > 0 ? c.Name + " " : "")}at {L(c.Height)} above the base, {Sec(c.Section)}", Assumption = _flag, Density = Dens(c.Section) });
        }
        // struts
        int si = 1;
        foreach (var s in _d.Struts)
        {
            var p0 = P(s.From); var p1 = P(s.To);
            var mesh = Member(new[] { p0, p1 }, s.Section, _pf.Y);
            _m.Steel.Parts.Add(new SteelPart { Mark = $"PY-S{si}", Category = PartCategory.PylonStrut, Assembly = "PY", Mesh = mesh, Length = (p1 - p0).Length,
                Description = $"Strut {(s.Name.Length > 0 ? s.Name + " " : "")}{Sec(s.Section)}", Assumption = _flag, Density = Dens(s.Section) });
            if (s.FootingLength > 0 && s.FootingWidth > 0 && s.FootingThickness > 0)
            {
                var low = s.From.Z <= s.To.Z ? s.From : s.To;
                var fm = new Mesh();
                MeshBuilder.AddBox(fm, _pf with { Origin = P(low) }, -s.FootingLength / 2, s.FootingLength / 2, -s.FootingWidth / 2, s.FootingWidth / 2, -s.FootingThickness, 0);
                _pylonConcrete.Add(new SubSolid(SubElementKind.Footing, $"{sl.Name} strut {si} footing", $"Strut footing {L(s.FootingLength)} x {L(s.FootingWidth)} x {L(s.FootingThickness)}", fm) { Assumption = _flag });
            }
            si++;
        }
        if (py.FootingLength > 0 && py.FootingWidth > 0 && py.FootingThickness > 0)
        {
            var fm = new Mesh();
            MeshBuilder.AddBox(fm, _pf, -py.FootingLength / 2, py.FootingLength / 2, -py.FootingWidth / 2, py.FootingWidth / 2, -py.FootingThickness, 0);
            _pylonConcrete.Add(new SubSolid(SubElementKind.Footing, $"{sl.Name} pylon footing", $"Pylon footing {L(py.FootingLength)} x {L(py.FootingWidth)} x {L(py.FootingThickness)}", fm) { Assumption = _flag });
        }
    }

    private readonly List<SubSolid> _pylonConcrete = new();

    /// <summary>A section swept through exact rings: the corner curves are fitted as NURBS (one knot vector, 0.1 mm) and the mesh is tessellated from
    /// them at <paramref name="us"/> (Rhino / IFC get the exact surfaces); when the fit does not converge the mesh alone is built from the exact rings.</summary>
    private void Sweep(Mesh mesh, List<Vec2> section, Func<double, Vec3[]> exact, List<double> us, string kind)
    {
        int tri = mesh.TriangleCount;
        var sw = us.Count >= 2 ? NurbsSweep.Create(section.Count, exact, us[0], us[^1], Array.Empty<double>(), 1e-4, out _, Math.Max(1e-3, (us[^1] - us[0]) / 8)) : null;
        if (sw == null) { MeshBuilder.AddLoft(mesh, section, us.Select(exact).ToList()); return; }
        MeshBuilder.AddLoft(mesh, section, us.Select(sw.Ring).ToList());
        mesh.AddNurbs(sw.ToSolid(kind), tri);
    }

    /// <summary>Leg section corners at the curve PARAMETER t (cubic in the pylon frame): width along the horizontal normal of the tangent (frame Y where
    /// the leg is plumb), depth along tangent x width; size tapered by t.</summary>
    private Vec3[] LegRing(CsPylonLeg leg, double t)
    {
        var (p, d) = Bezier(leg, t);
        var up = new Vec3(0, 0, 1);
        var w = Vec3.Cross(up, d);
        w = w.Length < 1e-6 ? new Vec3(0, 1, 0) : w.Normalized();
        var dd = Vec3.Cross(d, w).Normalized();
        double hw = leg.Section.WidthAt(t) / 2, hd = leg.Section.DepthAt(t) / 2;
        return new[] { P(p - w * hw - dd * hd), P(p + w * hw - dd * hd), P(p + w * hw + dd * hd), P(p - w * hw + dd * hd) };
    }

    /// <summary>Point and unit tangent of the leg's cubic (pylon frame) at parameter t.</summary>
    private static (Vec3 p, Vec3 d) Bezier(CsPylonLeg leg, double t)
    {
        var (p0, p1, p2, p3) = Controls(leg);
        double u = 1 - t;
        var p = p0 * (u * u * u) + p1 * (3 * u * u * t) + p2 * (3 * u * t * t) + p3 * (t * t * t);
        var d = (p1 - p0) * (3 * u * u) + (p2 - p1) * (6 * u * t) + (p3 - p2) * (3 * t * t);
        return (p, d.Length < 1e-12 ? (p3 - p0).Normalized() : d.Normalized());
    }

    private static (Vec3, Vec3, Vec3, Vec3) Controls(CsPylonLeg leg)
    {
        Vec3 p0 = leg.Start, p3 = leg.Tip;
        double chord = (p3 - p0).Length;
        Vec3 Dir(double lean, double splay) => new Vec3(Math.Tan(lean * Math.PI / 180), Math.Tan(splay * Math.PI / 180), 1).Normalized();
        if (leg.Straight || chord < 1e-9) return (p0, p0 + (p3 - p0) * (1.0 / 3), p0 + (p3 - p0) * (2.0 / 3), p3);
        return (p0, p0 + Dir(leg.StartLeanDeg, leg.StartSplayDeg) * (leg.StartHandle * chord), p3 - Dir(leg.TipLeanDeg, leg.TipSplayDeg) * (leg.TipHandle * chord), p3);
    }

    private double Dens(CsSection s) => s.Wall > 0 && s.Material == CsMaterial.Steel
        ? CableStayedAnalysis.EquivalentDensity(CableStayedAnalysis.RectProps(s, 0).A, s.Width * s.Depth, _d.Analysis.SteelDensity)
        : s.Material == CsMaterial.Concrete ? _d.Analysis.ConcreteDensity : 0;

    private string Sec(CsSection s) =>
        $"{L(s.Width)} x {L(s.Depth)}" + (double.IsNaN(s.EndWidth) && double.IsNaN(s.EndDepth) ? "" : $" to {L(s.WidthAt(1))} x {L(s.DepthAt(1))}")
        + (s.Wall > 0 ? $" box, wall {L(s.Wall)} (envelope modeled)" : s.Material == CsMaterial.Concrete ? " concrete" : " solid");

    /// <summary>The leg as a cubic curve in the pylon frame, sampled densely in world coordinates (arc-length table for the stay anchors).</summary>
    private CsLegCurve LegCurve(CsPylonLeg leg, string name)
    {
        const int n = 600;
        var pts = new Vec3[n + 1];
        var s = new double[n + 1];
        for (int i = 0; i <= n; i++)
        {
            pts[i] = P(Bezier(leg, (double)i / n).p);
            if (i > 0) s[i] = s[i - 1] + (pts[i] - pts[i - 1]).Length;
        }
        return new CsLegCurve { Name = name, Definition = leg, Points = pts, S = s };
    }

    private static Vec3 AtHeight(CsLegCurve c, double hLocalAboveBase)
    {
        double z = c.Points[0].Z - (c.Definition.Start.Z) + hLocalAboveBase;   // world z of the height (frame base + h)
        Vec3 best = c.Points[0]; double bd = double.MaxValue;
        foreach (var p in c.Points) { double d = Math.Abs(p.Z - z); if (d < bd) { bd = d; best = p; } }
        return best;
    }

    /// <summary>Closed solid of a rectangular section swept through the points (taper by length fraction); width horizontal and normal to the member
    /// (parallel transported along it), <paramref name="fallbackWidthAxis"/> where the member is vertical.</summary>
    private static Mesh Member(IReadOnlyList<Vec3> pts, CsSection sec, Vec3 fallbackWidthAxis)
    {
        var up = new Vec3(0, 0, 1);
        double total = 0; var cum = new double[pts.Count];
        for (int i = 1; i < pts.Count; i++) { total += (pts[i] - pts[i - 1]).Length; cum[i] = total; }
        Vec3 Tan(int i) => (pts[Math.Min(pts.Count - 1, i + 1)] - pts[Math.Max(0, i - 1)]).Normalized();
        var t0 = Tan(0);
        var w = Vec3.Cross(up, t0);
        w = w.Length < 1e-6 ? fallbackWidthAxis : w.Normalized();
        var rings = new List<Vec3[]>();
        var section = new List<Vec2> { new(-0.5, -0.5), new(0.5, -0.5), new(0.5, 0.5), new(-0.5, 0.5) };
        for (int i = 0; i < pts.Count; i++)
        {
            var t = Tan(i);
            w = (w - t * Vec3.Dot(w, t));
            w = w.Length < 1e-9 ? fallbackWidthAxis : w.Normalized();
            var d = Vec3.Cross(t, w).Normalized();
            double u = total > 0 ? cum[i] / total : 0;
            double hw = sec.WidthAt(u) / 2, hd = sec.DepthAt(u) / 2;
            rings.Add(new[] { pts[i] - w * hw - d * hd, pts[i] + w * hw - d * hd, pts[i] + w * hw + d * hd, pts[i] - w * hw + d * hd });
        }
        var m = new Mesh();
        MeshBuilder.AddLoft(m, section, rings);
        return m;
    }

    // ------------------------------------------------------------------ stays

    private void BuildStays()
    {
        int gi = 0;
        foreach (var g in _d.Stays)
        {
            gi++;
            if (g.Leg < 0 || g.Leg >= _m.Legs.Count) continue;
            var leg = _m.Legs[g.Leg];
            string asm = $"ST{gi}";
            string gname = g.Name.Length > 0 ? g.Name : $"group {gi}";
            _m.Steel.Assemblies.Add(new SteelAssembly { Mark = asm, Kind = AssemblyKind.StayGroup, Description = $"Stays {gname} ({leg.Name})" });
            var lower = new List<(Vec3 p, double sta)>();
            if (g.GroundAnchors.Count > 0) lower.AddRange(g.GroundAnchors.Select(a => (P(a), double.NaN)));
            else
                for (int i = 0; i < g.Count; i++)
                {
                    double sta = AnchorStation(g, i);
                    lower.Add((_al.PointAt(sta, g.DeckOffset).WithZ(Grade(sta) + g.DeckAnchorHeight), sta));
                }
            // the farthest anchor (plan) goes to the highest pylon point
            var basePt = _pf.Origin;
            var order = lower.OrderByDescending(a => (a.p.XY - basePt.XY).Length).ToList();
            int n = order.Count;
            double area = g.Area > 0 ? g.Area : 0.6 * Math.PI * g.Diameter * g.Diameter / 4;
            for (int k = 0; k < n; k++)
            {
                double fr = n == 1 ? g.PylonTo : g.PylonTo - (g.PylonTo - g.PylonFrom) * k / (n - 1);
                var up = leg.At(fr);
                var lo = order[k].p;
                var stay = new CsStay { Name = $"{asm}-{k + 1}", Group = gname, Leg = g.Leg, Lower = lo, Upper = up, Station = order[k].sta, PylonFraction = fr, Diameter = g.Diameter, Area = area };
                _m.Stays.Add(stay);
                var mesh = new Mesh();
                var ax = (up - lo).Normalized();
                var e1 = Vec3.Cross(Math.Abs(ax.Z) > 0.9 ? new Vec3(1, 0, 0) : new Vec3(0, 0, 1), ax).Normalized();
                var e2 = Vec3.Cross(ax, e1);
                const int sides = 16;
                var circle = Enumerable.Range(0, sides).Select(j => Vec2.FromAngle(2 * Math.PI * j / sides) * (g.Diameter / 2)).ToList();
                Vec3[] Ring(Vec3 c) => circle.Select(q => c + e1 * q.X + e2 * q.Y).ToArray();
                MeshBuilder.AddLoft(mesh, circle, new[] { Ring(lo), Ring(up) });
                _m.Steel.Parts.Add(new SteelPart
                {
                    Mark = stay.Name, Category = PartCategory.StayCable, Assembly = asm, Mesh = mesh, Length = stay.Length, Material = g.Type,
                    Description = $"{g.Type} {L(g.Diameter)} (area {Area(area)}), {L(stay.Length)} at {stay.AngleDeg:0.0} deg"
                        + (double.IsNaN(stay.Station) ? ", ground anchor" : $", deck anchor sta {_u.FormatStation(stay.Station)}"),
                    Assumption = _flag, Density = _d.Analysis.SteelDensity * area / (Math.PI * g.Diameter * g.Diameter / 4),
                });
            }
        }
        if (_m.Stays.Count > 0)
            Info($"Cable-stayed: {_m.Stays.Count} stays, {_m.Stays.Min(s => s.AngleDeg):0.0} - {_m.Stays.Max(s => s.AngleDeg):0.0} deg to the horizontal, "
                + $"{L(_m.Stays.Min(s => s.Length))} - {L(_m.Stays.Max(s => s.Length))} long.");
        foreach (var s in _m.Stays.Where(s => s.AngleDeg < 20))
            Warn($"Cable-stayed: stay {s.Name} is at {s.AngleDeg:0.0} deg to the horizontal (flat stays carry little vertical load; usual minimum about 20-25 deg).");
    }

    // ------------------------------------------------------------------ piers

    private void BuildPiers()
    {
        var sub = new SubstructureModel();
        var pd = _d.Piers;
        double soffitBelowGrade = _m.DeckSection.Count > 0 ? _m.DeckSection.Min(p => p.Y) : 0;
        double bottomW = _m.DeckSection.Count > 0 ? _m.DeckSection.Where(p => Math.Abs(p.Y - soffitBelowGrade) < 1e-6).Select(p => p.X).DefaultIfEmpty(0).Max()
                                                    - _m.DeckSection.Where(p => Math.Abs(p.Y - soffitBelowGrade) < 1e-6).Select(p => p.X).DefaultIfEmpty(0).Min() : 0;
        for (int i = 0; i < _f.Supports.Count; i++)
        {
            var sl = _f.Supports[i];
            var dir = sl.Direction;
            var tan = Vec2.FromAngle(_al.AngleAt(sl.Station));
            var nn = new Vec2(-dir.Y, dir.X);
            if (Vec2.Dot(nn, tan) < 0) nn = -nn;
            var unit = new SubstructureUnit
            {
                SupportIndex = i, Name = sl.Name, Type = sl.Type, Definition = SubstructureDefinition.Default(sl.Type), Origin = sl.AlignmentPoint, D = dir, N = nn, Station = sl.Station,
            };
            bool isPylon = ReferenceEquals(sl, _pylonSupport);
            if (isPylon) foreach (var so in _pylonConcrete) unit.Solids.Add(so);
            else
            {
                double soffit = Grade(sl.Station) + soffitBelowGrade;
                var fr = new Frame3(sl.AlignmentPoint.WithZ(0), tan.WithZ(0), tan.Right.WithZ(0), new Vec3(0, 0, 1));
                bool end = i == 0 || i == _f.Supports.Count - 1;
                if (end && pd.AbutmentLength > 0 && pd.AbutmentDepth > 0)
                {
                    var m = new Mesh();
                    double hw = bottomW / 2 + pd.AbutmentExtraWidth;
                    MeshBuilder.AddBox(m, fr, -pd.AbutmentLength / 2, pd.AbutmentLength / 2, -hw, hw, soffit - pd.AbutmentDepth, soffit);
                    unit.Solids.Add(new SubSolid(SubElementKind.Cap, $"{sl.Name} seat", $"Abutment seat {L(pd.AbutmentLength)} x {L(2 * hw)} x {L(pd.AbutmentDepth)}", m) { Assumption = _flag });
                }
                else if (!end && pd.ColumnWidth > 0)
                {
                    double zb = pd.BaseFor(sl.Name);
                    if (double.IsNaN(zb)) Warn($"UNRESOLVED (cable-stayed): pier column at {sl.Name} - no base elevation (CsPierDefinition.BaseElevation / BaseElevations); not built.");
                    else if (zb >= soffit) Warn($"Cable-stayed: pier {sl.Name} base {L(zb)} is not below the deck soffit {L(soffit)}; not built.");
                    else
                    {
                        var m = new Mesh();
                        if (pd.ColumnDepth <= 0) MeshBuilder.AddPrism(m, fr, pd.ColumnWidth / 2, 32, zb, soffit);
                        else MeshBuilder.AddBox(m, fr, -pd.ColumnDepth / 2, pd.ColumnDepth / 2, -pd.ColumnWidth / 2, pd.ColumnWidth / 2, zb, soffit);
                        unit.Solids.Add(new SubSolid(SubElementKind.Column, $"{sl.Name} column",
                            $"{(pd.ColumnDepth <= 0 ? $"Round column {L(pd.ColumnWidth)}" : $"Column {L(pd.ColumnWidth)} x {L(pd.ColumnDepth)}")}, {L(soffit - zb)} high", m) { Assumption = _flag });
                        if (pd.FootingLength > 0 && pd.FootingWidth > 0 && pd.FootingThickness > 0)
                        {
                            var fm = new Mesh();
                            MeshBuilder.AddBox(fm, fr, -pd.FootingLength / 2, pd.FootingLength / 2, -pd.FootingWidth / 2, pd.FootingWidth / 2, zb - pd.FootingThickness, zb);
                            unit.Solids.Add(new SubSolid(SubElementKind.Footing, $"{sl.Name} footing", $"Footing {L(pd.FootingLength)} x {L(pd.FootingWidth)} x {L(pd.FootingThickness)}", fm) { Assumption = _flag });
                        }
                    }
                }
            }
            if (unit.Solids.Count > 0) sub.Units.Add(unit);
        }
        _m.Substructure = sub.Units.Count > 0 ? sub : null;
    }

    // ------------------------------------------------------------------ checks

    private void Clearances()
    {
        if (_m.DeckSection.Count == 0) return;
        double soffit = _m.DeckSection.Min(p => p.Y);
        foreach (var c in _d.Analysis.Clearances)
        {
            double best = double.MaxValue, at = c.FromStation;
            int n = Math.Max(2, (int)Math.Ceiling(Math.Abs(c.ToStation - c.FromStation) / 0.25));
            for (int i = 0; i <= n; i++)
            {
                double s = c.FromStation + (c.ToStation - c.FromStation) * i / n;
                double clr = Grade(s) + soffit - c.SurfaceElevation;
                if (clr < best) { best = clr; at = s; }
            }
            bool ok = best >= c.Required - 1e-6;
            _m.Clearances.Add(new CsClearanceResult(c.Name, c.Required, best, at, ok));
            string msg = $"Cable-stayed clearance {c.Name}: {L(best)} at sta {_u.FormatStation(at)} (required {L(c.Required)}{(c.Source.Length > 0 ? ", " + c.Source : "")})";
            if (ok) Info(msg); else Warn(msg + " - NOT MET");
        }
    }
}
