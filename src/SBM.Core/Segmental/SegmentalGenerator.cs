using SBM.Core.Alignments;
using SBM.Core.Bridge;
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
public sealed class SegmentalGenerator
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
        foreach (var (r, d, _) in _segs)
        {
            var stas = RingStations(r, d);
            var pieces = new List<Mesh>();
            int i0 = 0;
            while (i0 < stas.Count - 1)
            {
                // one piece = consecutive rings with the same hollow / solid state (state of the interval)
                bool hollow = SectionIn(d, r, (stas[i0] + stas[i0 + 1]) / 2).hollow;
                int i1 = i0 + 1;
                while (i1 < stas.Count - 1 && SectionIn(d, r, (stas[i1] + stas[i1 + 1]) / 2).hollow == hollow) i1++;
                var rings = new List<(SectionFrame, List<Vec2>, List<Vec2>?)>();
                for (int k = i0; k <= i1; k++)
                {
                    double sta = stas[k];
                    var (h, t, _) = SectionIn(d, r, sta);
                    double u = (sta - r.Station0) / (r.Station1 - r.Station0);
                    var fr = Frame(sta) with { Origin = Vec3.Lerp(r.Top0, r.Top1, u) };
                    if (k == 0) fr = Frame(r.Station0);
                    if (k == stas.Count - 1) fr = Frame(r.Station1);
                    rings.Add((fr, SegmentalSection.Outer(_d.Box, h), hollow ? SegmentalSection.Void(_d.Box, h, t) : null));
                }
                pieces.Add(SegmentalLoft.Build(rings, SegmentalSection.OuterCl, SegmentalSection.VoidBottomCl(_d.Box)));
                i0 = i1;
            }
            foreach (var p in pieces) r.Mesh.Append(p);
            if (pieces.Count > 1) r.Pieces.AddRange(pieces);
            if (d.Type == SegmentType.End)
                r.Assumption = Assumption.Unresolved($"end segment interior (end diaphragm, vermin screen opening, expansion joint blockout, 2'-2\" top slab step) is not modeled - not all of it is dimensioned (p.271)",
                    "solid outer envelope of the 9'-0\" box over the segment length");
            else if (d.Type == SegmentType.BentHalf)
                r.Assumption = Assumption.Unresolved($"pier diaphragm openings (diaphragm opening, utility opening, 3'-0\" access opening) and the length of the 3\" soffit step are not modeled / not dimensioned (p.277)",
                    "solid diaphragm over its plan length; soffit step over the plan's 1'-6\" bulkhead zone");
            _m.Segments.Add(r);
        }
        if (_m.Segments.Any(s => s.Type == SegmentType.End))
            Warn(UnresolvedFlag.Text("p.271", "end segment interior", "the end diaphragm, vermin screen opening and expansion joint blockout are drawn but not fully dimensioned", "End segments are modeled as the solid outer envelope (flagged on the element)."));
        if (_m.Segments.Any(s => s.Type == SegmentType.BentHalf))
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
        return stas.Distinct().OrderBy(s => s).Select(s => Duct(s, x, depth, above)).ToList();
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
                if (MeshBuilder.SignedArea(ol) < 0) ol.Reverse();
                MeshBuilder.AddExtrusion(m, ol, fr, p.FootingBottom, p.FootingTop);
                unit.Solids.Add(new SubSolid(SubElementKind.Footing, $"{b.Name} footing", $"CIP footing on {p.PileCount} 36\" sq PS piles (piles not modeled)", m));
                var minS = ol.Min(v => v.X); var maxS = ol.Max(v => v.X); var minN = ol.Min(v => v.Y); var maxN = ol.Max(v => v.Y);
                unit.Foundations.Add(new FoundationInfo { Type = FoundationType.PileCap, S = (minS + maxS) / 2, N = (minN + maxN) / 2, TopZ = p.FootingTop, BottomZ = p.FootingBottom, SizeS = maxS - minS, SizeN = maxN - minN });
            }
            double capBottom = p.CapTop - p.CapHeight;
            if (!double.IsNaN(p.ColumnS) && !double.IsNaN(p.ColumnN) && !double.IsNaN(p.FootingTop) && !double.IsNaN(capBottom))
            {
                var m = new Mesh();
                MeshBuilder.AddBox(m, fr, -p.ColumnS / 2, p.ColumnS / 2, -p.ColumnN / 2, p.ColumnN / 2, p.FootingTop, capBottom);
                unit.Solids.Add(new SubSolid(SubElementKind.Column, $"{b.Name} column", $"CIP column base to EL {p.ColumnBaseTop / Ft(1):0.00} + precast hollow column segments, envelope {p.ColumnS / Ft(1):0.##} x {p.ColumnN / Ft(1):0.##} ft{flag}", m));
                unit.Columns.Add(new ColumnInfo(0, 0, 0, capBottom, p.FootingTop, ColumnShape.Rectangular, p.ColumnS, p.ColumnN));
            }
            if (p.CapOutline.Count >= 3 && !double.IsNaN(p.CapTop) && !double.IsNaN(p.CapHeight))
            {
                var m = new Mesh();
                var ol = p.CapOutline.Select(v => v.ToVec2()).ToList();
                if (MeshBuilder.SignedArea(ol) < 0) ol.Reverse();
                if (p.CapTaperHeight > 0 && p.CapTaperInset > 0)
                {
                    var c = new Vec2(ol.Average(v => v.X), ol.Average(v => v.Y));
                    var inset = ol.Select(v => new Vec2(v.X - Math.Sign(v.X - c.X) * p.CapTaperInset, v.Y - Math.Sign(v.Y - c.Y) * p.CapTaperInset)).ToList();
                    Vec3[] Ring(List<Vec2> poly, double z) => poly.Select(v => fr.ToWorld(v.X, v.Y, z)).ToArray();
                    MeshBuilder.AddLoft(m, ol, new[] { Ring(inset, capBottom), Ring(ol, capBottom + p.CapTaperHeight), Ring(ol, p.CapTop) });
                }
                else MeshBuilder.AddExtrusion(m, ol, fr, capBottom, p.CapTop);
                unit.Solids.Add(new SubSolid(SubElementKind.Cap, $"{b.Name} cap", $"Precast bent cap, top EL {p.CapTop / Ft(1):0.00}", m));
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
                var q = fr with { Origin = (o + dd * br.Offset + nn * br.Along).WithZ(0) };
                MeshBuilder.AddBox(m, q, -br.PedestalAcross / 2, br.PedestalAcross / 2, -br.PedestalAlong / 2, br.PedestalAlong / 2, p.CapTop, br.PedestalTop);
                unit.Solids.Add(new SubSolid(SubElementKind.Pedestal, $"{b.Name} {br.Name} pedestal", $"CIP bearing pedestal, top EL {br.PedestalTop / Ft(1):0.00}", m));
                unit.Pedestals.Add(new PedestalInfo(-1, br.Offset, br.Along, br.PedestalAcross, br.PedestalAlong, p.CapTop, br.PedestalTop) { Label = br.Name });
            }
            if (unit.Solids.Count > 0) sub.Units.Add(unit);
        }
        _m.Substructure = sub.Units.Count > 0 ? sub : null;
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
