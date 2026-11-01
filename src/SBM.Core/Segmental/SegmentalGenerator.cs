using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Substructure;
using SBM.Core.Units;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Segmental;

/// <summary>
/// Precast segmental box girder generator (pure function framing -> <see cref="SegmentalModel"/>). Geometry rules (design note
/// docs/plan-data/bonner-b2500/segmental-unit5/SEGMENTAL-DESIGN.md):
/// - every joint lies on the alignment at its station (x along -L- from the span's start bent); its top CL point (WP) is on the profile grade;
///   the joint section is normal to the 3D alignment tangent (grade) and rotated by the cross slope (box cast square to the top surface);
/// - a segment is the straight 3D chord between its two joint WPs (plan: "segment chording"), sections interpolated along the chord;
/// - depth: printed table at the joints, reconstructed parabola between them (flagged), constant beyond the vertex, bent halves: soffit step
///   to the CL depth over the transition length and a solid pier diaphragm; closure joints / end segments constant;
/// - tendons: straight runs joint to joint in their duct (row A / B below the top, row C above the soffit), so they follow the box.
/// </summary>
public sealed partial class SegmentalGenerator
{
    private readonly BridgeFraming _f;
    private readonly SegmentalDefinition _d;
    private readonly Alignment _a;
    private readonly SegmentalModel _m;
    private readonly HashSet<string> _warned = new();
    private readonly Dictionary<string, double> _bentSta = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _bentIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(SegmentResult r, SegmentDefinition d, SegmentalSpan s)> _segs = new();
    private bool _planarWarned;

    private SegmentalGenerator(BridgeFraming f)
    {
        _f = f;
        _d = f.Definition.Segmental!;
        _a = f.Alignment;
        _m = new SegmentalModel { Definition = _d };
    }

    public static SegmentalModel Generate(BridgeFraming f, UnitSystem units)
    {
        var g = new SegmentalGenerator(f);
        g.Run();
        return g._m;
    }

    private void Warn(string msg) { if (_warned.Add(msg)) _m.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, msg)); }
    private void Info(string msg) => _m.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info, msg));
    private string Pages => string.IsNullOrWhiteSpace(_d.Source) ? "plans" : _d.Source;

    private void Run()
    {
        foreach (var s in _f.Supports) { _bentSta[s.Name] = s.Station; _bentIndex[s.Name] = s.Index; }
        foreach (var u in _d.Unresolved) Warn(u);
        foreach (var r in _d.Reported) Info(r);
        if (double.IsNaN(_d.ParabolaVertexX))
            Info("Segmental depth: no reconstruction curve given - the depth is interpolated linearly between the printed table sections.");
        else
            Warn(UnresolvedFlag.Text(Pages, "variable depth curve between the printed sections",
                "the plans print only the depth table; the curve is a reconstruction (parabola, vertex at x = " +
                $"{_d.ParabolaVertexX / Ft(1):0.##} ft), not a plan value", "Used: printed depths at the joints, the parabola between them."));
        BuildSegments();
        BuildTendons();
        BuildTransverse();
        BuildBearings();
        BuildPiers();
        BuildDetails();
        BuildMisc();
        if (_d.SectionBars is { Count: > 0 }) BuildSectionBars();
        Report();
    }

    // ------------------------------------------------------------------ depth

    /// <summary>Box depth and bottom slab at distance x from the cantilever bent CL in the cantilever zone (x >= first table section).</summary>
    public (double h, double t) Cantilever(double x)
    {
        var tab = _d.DepthTable;
        if (x >= tab[^1].X) return (Par(x, tab[^1]), tab[^1].BottomSlab);
        if (x <= tab[0].X) return (tab[0].Depth, tab[0].BottomSlab);
        int i = 1;
        while (tab[i].X < x) i++;
        var a = tab[i - 1]; var b = tab[i];
        double u = (x - a.X) / (b.X - a.X);
        double t = a.BottomSlab + (b.BottomSlab - a.BottomSlab) * u;
        if (double.IsNaN(_d.ParabolaVertexX)) return (a.Depth + (b.Depth - a.Depth) * u, t);
        double ra = a.Depth - ParOnly(a.X), rb = b.Depth - ParOnly(b.X);
        return (ParOnly(x) + ra + (rb - ra) * u, t);
    }

    private double ParOnly(double x) => x >= _d.ParabolaVertexX ? _d.ParabolaVertexDepth
        : _d.ParabolaVertexDepth + _d.ParabolaCoefficient * (_d.ParabolaVertexX - x) * (_d.ParabolaVertexX - x);

    private double Par(double x, SegmentalDepthSection last) => double.IsNaN(_d.ParabolaVertexX) || x >= _d.ParabolaVertexX ? last.Depth : ParOnly(x);

    // ------------------------------------------------------------------ frames

    private double CrossSlope(double sta)
    {
        double w = _d.Box.TopWidth / 2;
        double dr = _a.CrossSlope.DeltaElevation(sta, w), dl = _a.CrossSlope.DeltaElevation(sta, -w);
        if (!_planarWarned && Math.Abs(dr + dl) > 0.003)
        {
            _planarWarned = true;
            Warn($"Segmental box: the cross slope at {sta / Ft(1):0.00} ft is not one plane across the box (left {dl / w:P2} vs right {dr / w:P2}); the box top uses the mean slope.");
        }
        return (dr - dl) / (2 * w);
    }

    /// <summary>Joint frame at a station: WP on the alignment and profile grade, section normal to the graded tangent, rotated by the cross slope.</summary>
    public SectionFrame Frame(double sta)
    {
        double dist = _a.StationToDistance(sta);
        var (p, ang) = _a.EvaluateAtDistance(dist);
        var tan = Vec2.FromAngle(ang);
        var right = tan.Right;
        double g = _a.Profile.IsEmpty ? 0 : _a.Profile.GradeAt(sta);
        double cs = CrossSlope(sta);
        var T = new Vec3(tan.X, tan.Y, g).Normalized();
        var L = new Vec3(right.X, right.Y, cs).Normalized();
        var lat = (L - T * Vec3.Dot(L, T)).Normalized();
        var up = Vec3.Cross(lat, T).Normalized();
        return new SectionFrame(p.WithZ(_a.ElevationAt(sta, 0)), lat, up, T);
    }

    /// <summary>Section frame at any station inside the unit: origin on the chord of the segment containing it, orientation from the alignment.</summary>
    public SectionFrame FrameOnChord(double sta)
    {
        var f = Frame(sta);
        foreach (var (r, _, _) in _segs)
            if (sta >= r.Station0 - 1e-9 && sta <= r.Station1 + 1e-9)
            {
                double u = (sta - r.Station0) / (r.Station1 - r.Station0);
                return f with { Origin = Vec3.Lerp(r.Top0, r.Top1, u) };
            }
        return f;
    }

    /// <summary>Depth / bottom slab / hollow at a station (any segment type).</summary>
    public (double h, double t, bool hollow) SectionAt(double sta)
    {
        foreach (var (r, d, _) in _segs)
            if (sta >= r.Station0 - 1e-9 && sta <= r.Station1 + 1e-9) return SectionIn(d, r, sta);
        return (_d.ConstantDepth, _d.ConstantBottomSlab, true);
    }

    private (double h, double t, bool hollow) SectionIn(SegmentDefinition d, SegmentResult r, double sta)
    {
        var b = SectionBase(d, r, sta);
        if (InteriorAt(r, sta, b.h) is { } ia) return (b.h, double.IsNaN(ia.t) ? b.t : ia.t, ia.hollow);
        return b;
    }

    /// <summary>Depth / bottom slab / hollow by the standard rule of the segment type (no <see cref="SegmentalDefinition.Interiors"/>).</summary>
    private (double h, double t, bool hollow) SectionBase(SegmentDefinition d, SegmentResult r, double sta)
    {
        switch (d.Type)
        {
            case SegmentType.Closure: return (_d.ConstantDepth, _d.ConstantBottomSlab, true);
            case SegmentType.End: return (_d.ConstantDepth, _d.ConstantBottomSlab, false);
            case SegmentType.Typical:
            {
                var (h, t) = Cantilever(Math.Abs(sta - _bentSta[d.Bent]));
                return (h, t, true);
            }
            default:
            {
                double half = d.X1 - d.X0;
                double xb = Math.Abs(sta - _bentSta[d.Bent]);
                var (hj, tj) = Cantilever(half);
                double hcl = _d.Bent(d.Bent)?.DepthAtCl ?? double.NaN;
                if (double.IsNaN(hcl)) hcl = hj;
                double tr = _d.BentSoffitTransition;
                double u = double.IsNaN(tr) || tr <= 0 ? 0 : Math.Clamp((half - xb) / tr, 0, 1);
                double h = hj + (hcl - hj) * u;
                bool hollow = double.IsNaN(_d.BentDiaphragm) || xb > _d.BentDiaphragm + 1e-9;
                return (h, tj + (h - hj), hollow);
            }
        }
    }

    // ------------------------------------------------------------------ segments

    private void BuildSegments()
    {
        foreach (var sp in _d.Spans)
        {
            var s0 = _f.Supports[sp.Span];
            foreach (var d in sp.Segments)
            {
                double sta0 = s0.Station + d.X0, sta1 = s0.Station + d.X1;
                if (d.Bent != "" && !_bentSta.ContainsKey(d.Bent))
                {
                    Warn($"Segmental {sp.Name} {d.Name}: cantilever bent '{d.Bent}' is not a support of this bridge; segment skipped.");
                    continue;
                }
                var f0 = Frame(sta0);
                var f1 = Frame(sta1);
                var tmp = new SegmentResult
                {
                    Name = d.Name, Type = d.Type, Span = sp.Name, Bent = d.Bent, Index = d.Index, Station0 = sta0, Station1 = sta1,
                    Depth0 = 0, Depth1 = 0, Top0 = f0.Origin, Top1 = f1.Origin, Mesh = new Mesh(),
                };
                var (h0, _, _) = SectionIn(d, tmp, sta0);
                var (h1, _, _) = SectionIn(d, tmp, sta1);
                var r = new SegmentResult
                {
                    Name = d.Name, Type = d.Type, Span = sp.Name, Bent = d.Bent, Index = d.Index, Station0 = sta0, Station1 = sta1,
                    Depth0 = h0, Depth1 = h1, Top0 = f0.Origin, Top1 = f1.Origin, Mesh = new Mesh(),
                    Description = d.Type switch
                    {
                        SegmentType.BentHalf => $"Bent segment half at {d.Bent} ({(sta0 < _bentSta[d.Bent] ? "down" : "up")}station)",
                        SegmentType.Closure => $"Cast-in-place closure joint, {sp.Name}",
                        SegmentType.End => $"End segment, {sp.Name}",
                        _ => $"Typical segment, cantilever {d.Bent}",
                    },
                };
                _segs.Add((r, d, sp));
            }
        }
        SetupInteriors();
        foreach (var (r, d, _) in _segs)
        {
            if (BuildInterior(r, d)) { _m.Segments.Add(r); continue; }
            var stas = RingStations(r, d);
            // interval states (hollow / solid diaphragm); a ring carries the void when it bounds a hollow interval -> ONE closed shell
            var hollowIv = Enumerable.Range(0, stas.Count - 1).Select(i => SectionIn(d, r, (stas[i] + stas[i + 1]) / 2).hollow).ToArray();
            var rings = new List<(SectionFrame, List<Vec2>, List<Vec2>?)>();
            for (int k = 0; k < stas.Count; k++)
            {
                double sta = stas[k];
                var (h, t, _) = SectionIn(d, r, sta);
                double u = (sta - r.Station0) / (r.Station1 - r.Station0);
                var fr = Frame(sta) with { Origin = Vec3.Lerp(r.Top0, r.Top1, u) };
                if (k == 0) fr = Frame(r.Station0);
                if (k == stas.Count - 1) fr = Frame(r.Station1);
                bool hasVoid = (k > 0 && hollowIv[k - 1]) || (k < stas.Count - 1 && hollowIv[k]);
                rings.Add((fr, SegmentalSection.Outer(_d.Box, h), hasVoid ? SegmentalSection.Void(_d.Box, h, t) : null));
            }
            r.Mesh.Append(SegmentalLoft.Build(rings, SegmentalSection.OuterCl, SegmentalSection.VoidBottomCl(_d.Box)));
            if (d.Type == SegmentType.End)
                r.Assumption = Assumption.Unresolved($"end segment interior (end diaphragm, vermin screen opening, expansion joint blockout, 2'-2\" top slab step) is not modeled - not all of it is dimensioned (p.271)",
                    "solid outer envelope of the 9'-0\" box over the segment length");
            else if (d.Type == SegmentType.BentHalf)
                r.Assumption = Assumption.Unresolved($"pier diaphragm openings (diaphragm opening, utility opening, 3'-0\" access opening) and the length of the 3\" soffit step are not modeled / not dimensioned (p.277)",
                    "solid diaphragm over its plan length; soffit step over the plan's 1'-6\" bulkhead zone");
            _m.Segments.Add(r);
        }
        if (_m.Segments.Any(s => s.Type == SegmentType.End && !_interior.ContainsKey(s.Name)))
            Warn(UnresolvedFlag.Text("p.271", "end segment interior", "the end diaphragm, vermin screen opening and expansion joint blockout are drawn but not fully dimensioned", "End segments are modeled as the solid outer envelope (flagged on the element)."));
        if (_m.Segments.Any(s => s.Type == SegmentType.BentHalf && !_interior.ContainsKey(s.Name)))
            Warn(UnresolvedFlag.Text("p.277", "pier diaphragm openings and the 3\" soffit step length", "the openings are drawn with partial dimensions and the step is drawn without its own length", "Diaphragm modeled solid; step over the 1'-6\" bulkhead zone (flagged on the element)."));
    }

    private List<double> RingStations(SegmentResult r, SegmentDefinition d)
    {
        var l = new List<double> { r.Station0, r.Station1 };
        double len = r.Station1 - r.Station0;
        bool csVaries = Math.Abs(CrossSlope(r.Station0) - CrossSlope(r.Station1)) > 1e-6;
        int n = 1;
        if (d.Type == SegmentType.Typical && Math.Abs(r.Depth0 - r.Depth1) > 1e-6) n = 4;
        if (csVaries) n = Math.Max(n, 4);
        for (int i = 1; i < n; i++) l.Add(r.Station0 + len * i / n);
        if (d.Type == SegmentType.BentHalf)
        {
            double bs = _bentSta[d.Bent];
            double sgn = r.Station0 < bs - 1e-9 ? -1 : 1;     // side of the bent
            double half = d.X1 - d.X0;
            foreach (double xb in new[] { half - _d.BentSoffitTransition, _d.BentDiaphragm })
                if (!double.IsNaN(xb) && xb > 1e-6 && xb < half - 1e-6) l.Add(bs + sgn * xb);
        }
        // a cantilever segment crossing the depth curve's vertex: ring there (depth kink)
        if (d.Type == SegmentType.Typical && !double.IsNaN(_d.ParabolaVertexX))
        {
            double bs = _bentSta[d.Bent];
            foreach (double s in new[] { bs - _d.ParabolaVertexX, bs + _d.ParabolaVertexX })
                if (s > r.Station0 + 1e-6 && s < r.Station1 - 1e-6) l.Add(s);
        }
        return l.Distinct().OrderBy(x => x).ToList();
    }

    // ------------------------------------------------------------------ tendons

    private SegmentResult? Seg(string bent, int index, bool down)
    {
        if (!_bentSta.TryGetValue(bent, out double bs)) return null;
        return _m.Segments.FirstOrDefault(s => s.Bent.Equals(bent, StringComparison.OrdinalIgnoreCase) && s.Index == index && s.Type == SegmentType.Typical
            && (down ? s.Station1 <= bs + 1e-6 : s.Station0 >= bs - 1e-6));
    }

    /// <summary>Duct point at a station: lateral x, depth below the top or height above the soffit.</summary>
    private Vec3 Duct(double sta, double x, double depth, bool aboveSoffit)
    {
        var f = FrameOnChord(sta);
        var (h, _, _) = SectionAt(sta);
        double y = aboveSoffit ? h - depth : depth;
        return f.At(new Vec2(x, y));
    }

    private List<Vec3> Path(double s0, double s1, double x, double depth, bool above)
    {
        var stas = new List<double> { s0, s1 };
        foreach (var r in _m.Segments)
        {
            if (r.Station0 > s0 + 1e-6 && r.Station0 < s1 - 1e-6) stas.Add(r.Station0);
            if (r.Station1 > s0 + 1e-6 && r.Station1 < s1 - 1e-6) stas.Add(r.Station1);
        }
        return Simplify(stas.Distinct().OrderBy(s => s).Select(s => Duct(s, x, depth, above)).ToList(), PathTolerance);
    }

    /// <summary>Duct paths: joint points closer than this to the straight run through their neighbours are dropped (Douglas-Peucker).</summary>
    public const double PathTolerance = 0.002;

    public static List<Vec3> Simplify(List<Vec3> pts, double tol)
    {
        if (pts.Count < 3) return pts;
        var keep = new bool[pts.Count];
        keep[0] = keep[^1] = true;
        void Dp(int a, int b)
        {
            if (b - a < 2) return;
            var ab = pts[b] - pts[a];
            double len = ab.Length;
            int best = -1; double dmax = 0;
            for (int i = a + 1; i < b; i++)
            {
                double dd = len < 1e-12 ? (pts[i] - pts[a]).Length : Vec3.Cross(pts[i] - pts[a], ab).Length / len;
                if (dd > dmax) { dmax = dd; best = i; }
            }
            if (dmax > tol) { keep[best] = true; Dp(a, best); Dp(best, b); }
        }
        Dp(0, pts.Count - 1);
        return pts.Where((_, i) => keep[i]).ToList();
    }

    private void BuildTendons()
    {
        var missingDuct = new List<string>();
        foreach (var t in _d.Tendons)
        {
            double s0, s1;
            string anchors;
            switch (t.Layout)
            {
                case TendonLayout.Cantilever:
                {
                    var dn = Seg(t.Location, t.AnchorDown, true);
                    var up = Seg(t.Location, t.AnchorUp, false);
                    if (dn == null || up == null)
                    {
                        Warn($"Segmental tendon {t.Location} {t.Name}: anchor segment {(dn == null ? $"D{t.AnchorDown}" : $"U{t.AnchorUp}")} not found; tendon not built.");
                        continue;
                    }
                    s0 = dn.Station0; s1 = up.Station1;
                    anchors = $"{dn.Name} / {up.Name} (tip-side faces)";
                    break;
                }
                case TendonLayout.CentredOnClosure:
                {
                    var cj = _m.Segments.FirstOrDefault(s => s.Type == SegmentType.Closure && s.Span.Equals(t.Location, StringComparison.OrdinalIgnoreCase));
                    if (cj == null) { Warn($"Segmental tendon {t.Location} {t.Name}: no closure joint in that span; tendon not built."); continue; }
                    double c = (cj.Station0 + cj.Station1) / 2;
                    s0 = c - t.PlanLength / 2; s1 = c + t.PlanLength / 2;
                    anchors = $"centred on {cj.Name}";
                    break;
                }
                default:
                {
                    var es = _m.Segments.FirstOrDefault(s => s.Type == SegmentType.End && s.Span.Equals(t.Location, StringComparison.OrdinalIgnoreCase));
                    if (es == null) { Warn($"Segmental tendon {t.Location} {t.Name}: no end segment in that span; tendon not built."); continue; }
                    bool atStart = _m.Segments.Where(s => s.Span == es.Span).Min(s => s.Station0) >= es.Station0 - 1e-6;
                    if (atStart) { s0 = es.Station0; s1 = es.Station0 + t.PlanLength; }
                    else { s1 = es.Station1; s0 = es.Station1 - t.PlanLength; }
                    anchors = $"from the anchor face of {es.Name}";
                    break;
                }
            }
            if (double.IsNaN(t.DuctOffset) || double.IsNaN(t.DuctDepth)) { missingDuct.Add($"{t.Location} {t.Name}"); continue; }
            for (int k = 0; k < t.Count; k++)
            {
                double x = t.Count == 1 ? 0 : (k == 0 ? -t.DuctOffset : t.DuctOffset);
                string side = t.Count == 1 ? "" : (k == 0 ? " L" : " R");
                var tr = new TendonResult
                {
                    Name = $"{t.Location} {t.Name}{side}", Group = t.Group, Location = t.Location, Strands = t.Strands,
                    Points = Path(s0, s1, x, t.DuctDepth, t.AboveSoffit), PlanLength = t.PlanLength, PlanWeightLb = t.PlanWeightEachLb,
                    JackingForceKip = t.JackingForceKip, StressingEnd = t.StressingEnd, Anchors = anchors,
                    Owner = t.Layout == TendonLayout.Cantilever ? t.Location : "",
                };
                if (t.Unresolved != "") tr.Assumption = Assumption.Unresolved(t.Unresolved, "straight runs joint to joint in the assumed duct");
                _m.Tendons.Add(tr);
            }
        }
        if (missingDuct.Count > 0)
            Warn(UnresolvedFlag.Text(Pages, $"duct position of {missingDuct.Count} tendon designations ({string.Join(", ", missingDuct.Take(6))}{(missingDuct.Count > 6 ? ", ..." : "")})",
                "the duct is not given", "Those tendons are data only (no solid)."));
        foreach (var g in _d.Tendons.Where(t => t.Unresolved != "").Select(t => t.Unresolved).Distinct()) Warn(g);
    }

    private void BuildTransverse()
    {
        foreach (var td in _d.Transverse)
        {
            if (double.IsNaN(td.Depth)) { Warn(UnresolvedFlag.Text(Pages, $"transverse tendon {td.Name} depth", "not given", "Data only.")); continue; }
            foreach (var r in _m.Segments.Where(s => s.Type == td.SegmentType))
            {
                if (td.SegmentLength > 0 && Math.Abs(r.AlignmentLength - td.SegmentLength) > In(1)) continue;
                for (int i = 0; i < td.PerSegment; i++)
                {
                    double sta = r.Station0 + (r.Station1 - r.Station0) * (i + 0.5) / td.PerSegment;
                    var f = FrameOnChord(sta);
                    var pts = new List<Vec3> { f.At(new Vec2(-td.PlanLength / 2, td.Depth)), f.At(new Vec2(td.PlanLength / 2, td.Depth)) };
                    var tr = new TendonResult
                    {
                        Name = $"{r.Name} {td.Name}{i + 1}", Group = TendonGroup.Transverse, Location = r.Name, Strands = td.Strands, Points = pts,
                        PlanLength = td.PlanLength, PlanWeightLb = td.PerSegment > 0 ? td.PlanWeightPerSegmentLb / td.PerSegment : 0,
                        JackingForceKip = td.JackingForceKip, StressingEnd = "alternate (single end)", Owner = r.Name, Anchors = "deck edges",
                    };
                    if (td.Unresolved != "") tr.Assumption = Assumption.Unresolved(td.Unresolved, "equally spaced in the segment, straight at the given depth");
                    _m.Tendons.Add(tr);
                }
            }
            if (td.Unresolved != "") Warn(td.Unresolved);
        }
    }

    // ------------------------------------------------------------------ bearings

    private (Vec2 origin, Vec2 d, Vec2 n) BentFrame(string bent)
    {
        double sta = _bentSta[bent];
        var (p, ang) = _a.EvaluateAtDistance(_a.StationToDistance(sta));
        var tan = Vec2.FromAngle(ang);
        return (p, tan.Right, tan);
    }

    /// <summary>Box soffit elevation on the vertical line through a plan point near station <paramref name="sta"/>.</summary>
    private double SoffitZ(double sta, Vec2 q)
    {
        var f = FrameOnChord(sta);
        var (h, _, _) = SectionAt(sta);
        var s = f.Origin - f.Up * h;
        return s.Z - (f.Up.X * (q.X - s.X) + f.Up.Y * (q.Y - s.Y)) / f.Up.Z;
    }

    private void BuildBearings()
    {
        foreach (var b in _d.Bents)
        {
            if (!_bentSta.ContainsKey(b.Name)) { Warn($"Segmental bent '{b.Name}' is not a support of this bridge."); continue; }
            var (o, dd, nn) = BentFrame(b.Name);
            foreach (var br in b.Bearings)
            {
                var q = o + dd * br.Offset + nn * br.Along;
                double sta = _bentSta[b.Name] + br.Along;
                double soffit = SoffitZ(sta, q);
                double height = br.Height;
                double top = !double.IsNaN(br.PedestalTop) && !double.IsNaN(height) ? br.PedestalTop + height : soffit;
                Mesh? mesh = null;
                if (!double.IsNaN(br.SizeAcross) && !double.IsNaN(br.SizeAlong) && !double.IsNaN(height))
                {
                    mesh = new Mesh();
                    var fr = new Frame3(q.WithZ(0), dd.WithZ(0), nn.WithZ(0), new Vec3(0, 0, 1));
                    MeshBuilder.AddBox(mesh, fr, -br.SizeAcross / 2, br.SizeAcross / 2, -br.SizeAlong / 2, br.SizeAlong / 2, top - height, top);
                }
                var res = new SegmentalBearingResult
                {
                    Name = $"{b.Name} {br.Name}", Bent = b.Name, Type = br.Type, Top = q.WithZ(top), Height = height, SoffitZ = soffit,
                    PedestalTop = br.PedestalTop, Mesh = mesh,
                    Description = $"Disc bearing Type {br.Type}, assumed height {(double.IsNaN(height) ? "not given" : $"{height / In(1):0.###} in")}" +
                                  (mesh != null ? $", body = masonry plate max {br.SizeAcross / In(1):0.##} x {br.SizeAlong / In(1):0.##} in" : ", no body (plan size not given)"),
                };
                if (br.Unresolved != "") { res.Assumption = Assumption.Unresolved(br.Unresolved, mesh != null ? "generic block: plan max plate size x assumed height" : "record only, no solid"); Warn(br.Unresolved); }
                _m.Bearings.Add(res);
                if (!double.IsNaN(br.PedestalTop) && !double.IsNaN(height))
                    _m.Checks.Add(new SegmentalCheck($"{res.Name}: box soffit - (pedestal top + assumed bearing height)", soffit, top, soffit - top,
                        "plinth / bearing set-up between the bearing and the soffit (not dimensioned)"));
            }
        }
    }

    // ------------------------------------------------------------------ piers

    private void BuildPiers()
    {
        var sub = new SubstructureModel();
        foreach (var b in _d.Bents)
        {
            if (b.Pier is not { } p || !_bentSta.ContainsKey(b.Name)) continue;
            var (o, dd, nn) = BentFrame(b.Name);
            var sd = SubstructureDefinition.Default(SupportType.Pier);
            sd.Foundation = FoundationType.PileCap;
            sd.Reinforcement.ConcreteStrengthKsi = 8.0;
            var unit = new SubstructureUnit
            {
                SupportIndex = _bentIndex[b.Name], Name = b.Name, Type = SupportType.Pier, Definition = sd, Origin = o, D = dd, N = nn, Station = _bentSta[b.Name],
            };
            var fr = new Frame3(o.WithZ(0), dd.WithZ(0), nn.WithZ(0), new Vec3(0, 0, 1));
            foreach (var u in p.Unresolved) Warn(u);
            string flag = p.Unresolved.Count > 0 ? " (see UNRESOLVED warnings)" : "";
            if (p.FootingOutline.Count >= 3 && !double.IsNaN(p.FootingTop) && !double.IsNaN(p.FootingBottom))
            {
                var m = new Mesh();
                var ol = p.FootingOutline.Select(v => v.ToVec2()).ToList();
                var zone = p.FootingLevelZone?.Select(v => v.ToVec2()).ToList();
                if (MeshBuilder.SignedArea(ol) < 0) { ol.Reverse(); zone?.Reverse(); }
                if (zone != null && p.FootingTopEdge is double edge && zone.Count == ol.Count)
                {
                    // sloped top: outline walls up to the edge elevation, planes up to the level zone at the footing top
                    Vec3[] Ring(List<Vec2> poly, double z) => poly.Select(v => fr.ToWorld(v.X, v.Y, z)).ToArray();
                    MeshBuilder.AddLoft(m, ol, new[] { Ring(ol, p.FootingBottom), Ring(ol, edge), Ring(zone, p.FootingTop) }, false);
                }
                else
                {
                    if (zone != null) Warn($"Segmental pier {b.Name}: the footing level zone needs the footing outline's vertex count and FootingTopEdge - flat top built.");
                    MeshBuilder.AddExtrusion(m, ol, fr, p.FootingBottom, p.FootingTop);
                }
                unit.Solids.Add(new SubSolid(SubElementKind.Footing, $"{b.Name} footing", $"CIP footing on {p.PileCount} 36\" sq PS piles" + (_d.Misc?.Foundation(b.Name)?.Piles.Count > 0 ? "" : " (piles not modeled)"), m));
                var minS = ol.Min(v => v.X); var maxS = ol.Max(v => v.X); var minN = ol.Min(v => v.Y); var maxN = ol.Max(v => v.Y);
                unit.Foundations.Add(new FoundationInfo { Type = FoundationType.PileCap, S = (minS + maxS) / 2, N = (minN + maxN) / 2, TopZ = p.FootingTop, BottomZ = p.FootingBottom, SizeS = maxS - minS, SizeN = maxN - minN });
            }
            double capBottom = p.CapTop - p.CapHeight;
            if (p.ColumnSegments is { Count: > 0 } && !double.IsNaN(p.ColumnS) && !double.IsNaN(p.ColumnN) && !double.IsNaN(p.FootingTop) && !double.IsNaN(p.ColumnBaseTop))
                BuildColumnStack(b.Name, p, unit, fr, capBottom);
            else if (!double.IsNaN(p.ColumnS) && !double.IsNaN(p.ColumnN) && !double.IsNaN(p.FootingTop) && !double.IsNaN(capBottom))
            {
                var m = new Mesh();
                double cs = ColumnOffsetS(b.Name);
                MeshBuilder.AddBox(m, fr, cs - p.ColumnS / 2, cs + p.ColumnS / 2, -p.ColumnN / 2, p.ColumnN / 2, p.FootingTop, capBottom);
                unit.Solids.Add(new SubSolid(SubElementKind.Column, $"{b.Name} column", $"CIP column base to EL {p.ColumnBaseTop / Ft(1):0.00} + precast hollow column segments, envelope {p.ColumnS / Ft(1):0.##} x {p.ColumnN / Ft(1):0.##} ft{flag}", m));
                unit.Columns.Add(new ColumnInfo(0, cs, 0, capBottom, p.FootingTop, ColumnShape.Rectangular, p.ColumnS, p.ColumnN));
            }
            if (p.CapOutline.Count >= 3 && !double.IsNaN(p.CapTop) && !double.IsNaN(p.CapHeight))
            {
                var m = new Mesh();
                var ol = p.CapOutline.Select(v => v.ToVec2()).ToList();
                if (MeshBuilder.SignedArea(ol) < 0) ol.Reverse();
                var holes = (p.CapOpenings ?? new()).Where(h => h.Count >= 3).Select(h => h.Select(v => v.ToVec2()).ToList()).ToList();
                var c = new Vec2(ol.Average(v => v.X), ol.Average(v => v.Y));
                var inset = ol.Select(v => new Vec2(v.X - Math.Sign(v.X - c.X) * p.CapTaperInset, v.Y - Math.Sign(v.Y - c.Y) * p.CapTaperInset)).ToList();
                bool taper = p.CapTaperHeight > 0 && p.CapTaperInset > 0;
                if (holes.Count > 0)
                    HollowPrism(m, fr, taper ? new[] { (inset, capBottom), (ol, capBottom + p.CapTaperHeight), (ol, p.CapTop) } : new[] { (ol, capBottom), (ol, p.CapTop) }, holes);
                else if (taper)
                {
                    Vec3[] Ring(List<Vec2> poly, double z) => poly.Select(v => fr.ToWorld(v.X, v.Y, z)).ToArray();
                    MeshBuilder.AddLoft(m, ol, new[] { Ring(inset, capBottom), Ring(ol, capBottom + p.CapTaperHeight), Ring(ol, p.CapTop) });
                }
                else MeshBuilder.AddExtrusion(m, ol, fr, capBottom, p.CapTop);
                unit.Solids.Add(new SubSolid(SubElementKind.Cap, $"{b.Name} cap", $"Precast bent cap, top EL {p.CapTop / Ft(1):0.00}" + (holes.Count > 0 ? $", {holes.Count} full-depth access opening(s)" : "")
                    + (p.Source.Length > 0 ? $" ({p.Source})" : ""), m));
                double s0 = ol.Min(v => v.X), s1 = ol.Max(v => v.X);
                unit.CapS0 = s0; unit.CapS1 = s1; unit.CapN0 = ol.Min(v => v.Y); unit.CapN1 = ol.Max(v => v.Y);
                unit.CapTop.Add(new Vec2(s0, p.CapTop)); unit.CapTop.Add(new Vec2(s1, p.CapTop));
                unit.CapBottom.Add(new Vec2(s0, capBottom)); unit.CapBottom.Add(new Vec2(s1, capBottom));
                unit.CapTopDescription = $"Plan top of bent EL {p.CapTop / Ft(1):0.00}";
            }
            // CIP bearing pedestals: cap top to the plan pedestal top.
            foreach (var br in b.Bearings.Where(x => !double.IsNaN(x.PedestalTop) && !double.IsNaN(x.PedestalAcross) && !double.IsNaN(x.PedestalAlong) && !double.IsNaN(p.CapTop)))
            {
                var m = new Mesh();
                double ps = br.Offset + (br.PedestalShift ?? 0);
                var q = fr with { Origin = (o + dd * ps + nn * br.Along).WithZ(0) };
                MeshBuilder.AddBox(m, q, -br.PedestalAcross / 2, br.PedestalAcross / 2, -br.PedestalAlong / 2, br.PedestalAlong / 2, p.CapTop, br.PedestalTop);
                unit.Solids.Add(new SubSolid(SubElementKind.Pedestal, $"{b.Name} {br.Name} pedestal", $"CIP bearing pedestal, top EL {br.PedestalTop / Ft(1):0.00}", m));
                unit.Pedestals.Add(new PedestalInfo(-1, ps, br.Along, br.PedestalAcross, br.PedestalAlong, p.CapTop, br.PedestalTop) { Label = br.Name, SeatS = br.PedestalShift is null ? double.NaN : br.Offset });
            }
            if (p.PtBars is { Count: > 0 }) BuildPierPtBars(b.Name, p, unit, fr);
            if (p.BarList is { Count: > 0 }) PierBarList(b.Name, p, sub);
            if (unit.Solids.Count > 0) sub.Units.Add(unit);
        }
        _m.Substructure = sub.Units.Count > 0 ? sub : null;
    }

    /// <summary>Closed prism through polygon levels (same vertex count, CCW, bottom to top) with vertical holes through its full height.</summary>
    private static void HollowPrism(Mesh m, Frame3 fr, IReadOnlyList<(List<Vec2> poly, double z)> levels, List<List<Vec2>> holes)
    {
        Vec3 W(Vec2 v, double z) => fr.ToWorld(v.X, v.Y, z);
        double z0 = levels[0].z, z1 = levels[^1].z;
        // the cap triangulation bridges each hole along +x to the outer outline: put that bridge point into every level's outline first (same edge in
        // every level), so the walls and the caps share it (no T-junction); holes must not shadow each other along +x
        var lv = levels.Select(l => l.poly.ToList()).ToList();
        foreach (var h in holes)
        {
            // the same vertex PlateBuilder.TriangulateWithHoles bridges from: the hole made clockwise, its first vertex of largest x
            var hc = h.ToList(); if (MeshBuilder.SignedArea(hc) > 0) hc.Reverse();
            int mi = 0; for (int i = 1; i < hc.Count; i++) if (hc[i].X > hc[mi].X) mi = i;
            var mpt = hc[mi];
            int edge = -1; double best = double.MaxValue;
            var p0 = lv[0];
            for (int i = 0; i < p0.Count; i++)
            {
                var a = p0[i]; var c = p0[(i + 1) % p0.Count];
                if ((a.Y > mpt.Y) == (c.Y > mpt.Y)) continue;
                double x = a.X + (mpt.Y - a.Y) * (c.X - a.X) / (c.Y - a.Y);
                if (x >= mpt.X && x < best) { best = x; edge = i; }
            }
            if (edge < 0) continue;
            foreach (var poly in lv)
            {
                var a = poly[edge]; var c = poly[(edge + 1) % poly.Count];
                if (Math.Abs(a.Y - mpt.Y) < 1e-9 || Math.Abs(c.Y - mpt.Y) < 1e-9) continue;
                double x = a.X + (mpt.Y - a.Y) * (c.X - a.X) / (c.Y - a.Y);
                poly.Insert(edge + 1, new Vec2(x, mpt.Y));
            }
        }
        levels = levels.Select((l, i) => (lv[i], l.z)).ToList();
        for (int k = 0; k + 1 < levels.Count; k++)
        {
            var (a, za) = levels[k]; var (b, zb) = levels[k + 1];
            for (int i = 0; i < a.Count; i++)
            {
                int j = (i + 1) % a.Count;
                int i0 = m.Add(W(a[i], za)), i1 = m.Add(W(a[j], za)), i2 = m.Add(W(b[j], zb)), i3 = m.Add(W(b[i], zb));
                m.Tri(i0, i1, i2); m.Tri(i0, i2, i3);
            }
        }
        foreach (var h0 in holes)
        {
            var h = h0.ToList();
            if (MeshBuilder.SignedArea(h) < 0) h.Reverse();                     // CCW; hole walls face inward (into the hole)
            for (int i = 0; i < h.Count; i++)
            {
                int j = (i + 1) % h.Count;
                int i0 = m.Add(W(h[i], z0)), i1 = m.Add(W(h[j], z0)), i2 = m.Add(W(h[j], z1)), i3 = m.Add(W(h[i], z1));
                m.Tri(i0, i2, i1); m.Tri(i0, i3, i2);
            }
        }
        foreach (var (poly, z, up) in new[] { (levels[0].poly, z0, false), (levels[^1].poly, z1, true) })
        {
            var (verts, tris0) = PlateBuilder.TriangulateWithHoles(poly, holes.Select(h => h.ToArray()).ToList());
            // ear clipping across collinear outline vertices (e.g. a T with both stem corners on one line) leaves a triangle edge through a vertex:
            // split such triangles at that vertex so the cap conforms to the walls
            var tris = new List<(int, int, int)>(tris0);
            for (int pass = 0; pass < 64; pass++)
            {
                bool split = false;
                for (int t = 0; t < tris.Count && !split; t++)
                {
                    var (a, b2, c2) = tris[t];
                    foreach (var (u, w, o) in new[] { (a, b2, c2), (b2, c2, a), (c2, a, b2) })
                    {
                        var e = verts[w] - verts[u]; double l2 = Vec2.Dot(e, e);
                        if (l2 < 1e-18) continue;
                        int hit = -1;
                        for (int q = 0; q < verts.Count && hit < 0; q++)
                        {
                            if (q == u || q == w || q == o || (verts[q] - verts[u]).Length < 1e-9 || (verts[q] - verts[w]).Length < 1e-9) continue;
                            double tt = Vec2.Dot(verts[q] - verts[u], e) / l2;
                            if (tt <= 1e-9 || tt >= 1 - 1e-9) continue;
                            if ((verts[u] + e * tt - verts[q]).Length < 1e-7) hit = q;
                        }
                        if (hit < 0) continue;
                        tris[t] = (u, hit, o); tris.Add((hit, w, o)); split = true; break;
                    }
                }
                if (!split) break;
            }
            int baseIdx = m.Positions.Count;
            foreach (var v in verts) m.Add(W(v, z));
            foreach (var (a, b2, c2) in tris) { if (up) m.Tri(baseIdx + a, baseIdx + b2, baseIdx + c2); else m.Tri(baseIdx + a, baseIdx + c2, baseIdx + b2); }
        }
        if (m.Volume() < 0) for (int i = 0; i < m.Indices.Count; i += 3) (m.Indices[i + 1], m.Indices[i + 2]) = (m.Indices[i + 2], m.Indices[i + 1]);
    }

    /// <summary>CIP column base (solid, footing top to <see cref="SegmentalPier.ColumnBaseTop"/>), grout bed, hollow precast segments; the stack top vs the cap
    /// soffit is a check row (never adjusted).</summary>
    private void BuildColumnStack(string bent, SegmentalPier p, SubstructureUnit unit, Frame3 fr, double capBottom)
    {
        double cs = ColumnOffsetS(bent);
        var outer = new List<Vec2> { new(cs - p.ColumnS / 2, -p.ColumnN / 2), new(cs + p.ColumnS / 2, -p.ColumnN / 2), new(cs + p.ColumnS / 2, p.ColumnN / 2), new(cs - p.ColumnS / 2, p.ColumnN / 2) };
        var core = p.ColumnCore?.Select(v => new Vec2(cs + v.S, v.N)).ToList();
        string src = p.Source.Length > 0 ? $" ({p.Source})" : "";
        var mb = new Mesh();
        MeshBuilder.AddExtrusion(mb, outer, fr, p.FootingTop, p.ColumnBaseTop);
        unit.Solids.Add(new SubSolid(SubElementKind.Column, $"{bent} column base", $"CIP column base, solid {p.ColumnS / Ft(1):0.##} x {p.ColumnN / Ft(1):0.##} ft, footing top to EL {p.ColumnBaseTop / Ft(1):0.00}{src}", mb));
        double z = p.ColumnBaseTop;
        if (p.GroutBed is double g && g > 0)
        {
            var mg = new Mesh();
            MeshBuilder.AddExtrusion(mg, outer, fr, z, z + g);
            unit.Solids.Add(new SubSolid(SubElementKind.Column, $"{bent} column grout bed", $"grout bed {g / In(1):0.##} in under the first precast column segment{src}", mg));
            z += g;
        }
        int k = 0;
        foreach (double hseg in p.ColumnSegments!)
        {
            k++;
            var ms = new Mesh();
            if (core is { Count: >= 3 }) HollowPrism(ms, fr, new[] { (outer, z), (outer, z + hseg) }, new() { core });
            else MeshBuilder.AddExtrusion(ms, outer, fr, z, z + hseg);
            unit.Solids.Add(new SubSolid(SubElementKind.Column, $"{bent} column segment {k}",
                $"precast {(core != null ? "hollow " : "")}column segment {k}, {hseg / Ft(1):0.###} ft, EL {z / Ft(1):0.00} - {(z + hseg) / Ft(1):0.00}{src}", ms));
            z += hseg;
        }
        unit.Columns.Add(new ColumnInfo(0, cs, 0, z, p.FootingTop, ColumnShape.Rectangular, p.ColumnS, p.ColumnN));
        if (!double.IsNaN(capBottom))
            _m.Checks.Add(new SegmentalCheck($"{bent}: top of the precast column stack vs the cap soffit (ft)", z / Ft(1), capBottom / Ft(1), (z - capBottom) / Ft(1),
                "segments stacked on the grout bed over the CIP base; the cap is match cast on the top segment"));
    }

    /// <summary>Schedule-only marks of a pier bar bill (unit = the bent); per-row and total weight check rows.</summary>
    private void PierBarList(string bent, SegmentalPier p, SubstructureModel sub)
    {
        if (p.BarListUnresolved.Length > 0) Warn(p.BarListUnresolved);
        double model = 0, plan = 0; int bad = 0;
        foreach (var row in p.BarList!)
        {
            RebarSize size;
            try { size = RebarCatalog.Size(row.Size); } catch { Warn($"Segmental pier {bent}: bar {row.Mark} size '{row.Size}' unknown - not scheduled."); continue; }
            if (double.IsNaN(row.Length) || row.Count <= 0) continue;
            var mk = new BarMark
            {
                Mark = row.Mark, Size = size, Shape = BarShape.Straight(row.Length), Unit = bent, Count = row.Count, Coating = row.Coating,
                Location = $"{row.Element}{(row.Shape.Length > 0 ? $", {row.Shape}" : "")} (schedule only, not placed - UNRESOLVED){(row.Source.Length > 0 ? $" ({row.Source})" : "")}",
            };
            sub.Marks.Add(mk);
            double lb = mk.TotalMass / 0.45359237;
            model += lb;
            if (!double.IsNaN(row.PlanWeightLb)) { plan += row.PlanWeightLb; if (Math.Abs(lb - row.PlanWeightLb) > Math.Max(2, 0.01 * row.PlanWeightLb)) bad++; }
        }
        if (plan > 0)
            _m.Checks.Add(new SegmentalCheck($"{bent}: pier bar bill weight, count x length x unit weight vs the printed weights (lb)", model, plan, model - plan,
                $"{p.BarList!.Count} rows, {bad} rows differ by more than 1 %; schedule only (no 3D bars)"));
    }

    /// <summary>Vertical pier PT bars: bar, dead-end plate (optional) and stressing anchor plate as sub-solids (PtBar / PtAnchor).</summary>
    private void BuildPierPtBars(string bent, SegmentalPier p, SubstructureUnit unit, Frame3 fr)
    {
        int n = 0;
        foreach (var pb in p.PtBars!)
        {
            if (pb.Unresolved.Length > 0) Warn(pb.Unresolved);
            if (double.IsNaN(pb.Bottom) || double.IsNaN(pb.Top) || double.IsNaN(pb.Diameter)) { Warn($"Segmental pier {bent}: PT bar '{pb.Name}' needs Bottom / Top / Diameter - not built."); continue; }
            var flag = pb.Unresolved.Length > 0 ? Assumption.UnresolvedDimension(pb.Unresolved, "PT bar / plates as listed") : null;
            double pt = double.IsNaN(pb.PlateThickness) ? 0 : pb.PlateThickness;
            Vec3 P(double z) => fr.ToWorld(pb.S, pb.N, z);
            var mbar = SBM.Core.Misc.MiscMesh.Cylinder(P(pb.Bottom), P(pb.Top + pb.Protrusion), pb.Diameter / 2, 16);
            string src = pb.Source.Length > 0 ? $" ({pb.Source})" : "";
            unit.Solids.Add(new SubSolid(SubElementKind.PtBar, $"{bent} {pb.Name}", $"{pb.Diameter / In(1):0.###}\" PT bar{(double.IsNaN(pb.DuctDiameter) ? "" : $" in a {pb.DuctDiameter / In(1):0.###}\" duct")}, " +
                $"EL {pb.Bottom / Ft(1):0.00} - {pb.Top / Ft(1):0.00}{src}", mbar, false) { Material = pb.Material.Length > 0 ? pb.Material : "PT bar", Density = 7850, Assumption = flag });
            void Plate(string what, double w, double t, double zTop)
            {
                if (double.IsNaN(w) || double.IsNaN(t)) return;
                var mp = new Mesh();
                MeshBuilder.AddBox(mp, fr, pb.S - w / 2, pb.S + w / 2, pb.N - w / 2, pb.N + w / 2, zTop - t, zTop);
                unit.Solids.Add(new SubSolid(SubElementKind.PtAnchor, $"{bent} {pb.Name} {what} anchor plate", $"{what} anchor plate {w / In(1):0.##}\" x {w / In(1):0.##}\" x {t / In(1):0.##}\" of {pb.Name}{src}", mp, false)
                    { Material = "Steel anchor plate (PT supplier)", Density = 7850, Assumption = flag });
            }
            Plate("stressing", pb.PlateWidth, pt, pb.Top);
            Plate("dead-end", pb.DeadPlateWidth, double.IsNaN(pb.DeadPlateThickness) ? 0 : pb.DeadPlateThickness, pb.Bottom + (double.IsNaN(pb.DeadPlateThickness) ? 0 : pb.DeadPlateThickness));
            n++;
        }
        if (n > 0) Info($"Segmental pier {bent}: {n} PT bars.");
    }

    // ------------------------------------------------------------------ report

    private void Report()
    {
        foreach (var s in _f.Supports)
        {
            // (end bents 18 / 29: the WP lies in the joint gap beyond the end segment; the box there = the constant end section on -L-)
            var f = FrameOnChord(s.Station);
            var (h, _, _) = SectionAt(s.Station);
            var soffit = f.Origin - f.Up * h;
            var (p, ang) = _a.EvaluateAtDistance(_a.StationToDistance(s.Station));
            var tan = Vec2.FromAngle(ang);
            double zAtWp = SoffitZ(s.Station, f.Origin.XY);
            var dxy = soffit.XY - f.Origin.XY;
            _m.BentRows.Add(new SegmentalBentRow(s.Name, s.Station, f.Origin, h, soffit, zAtWp, Vec2.Dot(dxy, tan), Vec2.Dot(dxy, tan.Right), CrossSlope(s.Station),
                _a.Profile.IsEmpty ? 0 : _a.Profile.GradeAt(s.Station)));
            _ = p;
        }
        // chord offset: mid-ordinate between each segment chord and the alignment at mid-segment (plan)
        double worst = 0; string worstName = "";
        double chordDiff = 0; string chordName = "";
        foreach (var r in _m.Segments)
        {
            double mid = (r.Station0 + r.Station1) / 2;
            var p = _a.PointAt(mid);
            var c = Vec2.Lerp(r.Top0.XY, r.Top1.XY, 0.5);
            double off = Vec2.Distance(p, c);
            if (off > worst) { worst = off; worstName = r.Name; }
            if (r.Type == SegmentType.Typical)
            {
                double dl = r.ChordLength - r.AlignmentLength;
                if (Math.Abs(dl) > Math.Abs(chordDiff)) { chordDiff = dl; chordName = r.Name; }
            }
        }
        _m.Checks.Add(new SegmentalCheck("max plan offset between a segment chord and -L- (mid-segment)", worst, 0, worst, $"segment {worstName}"));
        _m.Checks.Add(new SegmentalCheck("max (3D chord length - length along -L-) of a typical segment", chordDiff, 0, chordDiff,
            $"segment {chordName}: joints are placed at their -L- stations, so a graded chord is longer than its station length"));
        Info($"Segmental chords: max plan offset of a chord from -L- = {worst / In(1):0.###} in ({worstName}); max 3D chord minus station length = {chordDiff / In(1):0.###} in ({chordName}).");
        foreach (var t in _d.DepthTable)
        {
            var (h, tt) = Cantilever(t.X);
            _m.Checks.Add(new SegmentalCheck($"depth at section {t.Number} (x = {t.X / Ft(1):0.##} ft)", h, t.Depth, h - t.Depth, ""));
            if (!double.IsNaN(t.HalfSoffitPrinted))
                _m.Checks.Add(new SegmentalCheck($"W/2 at section {t.Number} (4:1 web rule)", _d.Box.HalfSoffit(t.Depth), t.HalfSoffitPrinted, _d.Box.HalfSoffit(t.Depth) - t.HalfSoffitPrinted, ""));
            _ = tt;
        }
    }
}
