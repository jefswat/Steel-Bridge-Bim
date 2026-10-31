using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;

namespace SBM.Core.CableStayed;

/// <summary>Steel edge girder deck (<see cref="CsEdgeGirderDeck"/>): girder field sections, floorbeams, field splices, concrete slab, rocket launcher
/// stay anchorages, and the deck section / properties the analysis uses.</summary>
public sealed partial class CableStayedGenerator
{
    private CsEdgeGirderDeck? Eg => _d.Deck.EdgeGirders;
    private double _egFrom = double.NaN, _egTo = double.NaN;
    private List<double> _deckStations = new();
    /// <summary>Per deck anchorage: its station and the station of its far end (away from the pylon) - the field splice goes past the far end.</summary>
    private readonly List<(double sta, double far)> _anchorEnds = new();

    /// <summary>Envelope of the edge girder deck (x right, y up from the grade at the CL): slab with the two girders below it (a pi shape).</summary>
    private List<Vec2> EdgeGirderOutline()
    {
        var e = Eg!;
        double ts = e.SlabThickness, d = e.Depth, g = e.GirderOffset, bw = Math.Max(e.TopFlangeWidth, e.BottomFlangeWidth) / 2;
        return new List<Vec2>
        {
            new(-e.SlabLeft, 0), new(e.SlabRight, 0), new(e.SlabRight, -ts), new(g + bw, -ts), new(g + bw, -d), new(g - bw, -d), new(g - bw, -ts),
            new(-g + bw, -ts), new(-g + bw, -d), new(-g - bw, -d), new(-g - bw, -ts), new(-e.SlabLeft, -ts),
        };
    }

    /// <summary>Composite section (slab transformed by Es / Ec) and the weight per length: girders, floorbeams smeared over their spacing, slab.</summary>
    private void EdgeGirderProps()
    {
        var e = Eg!; var ad = _d.Analysis;
        double n = ad.SteelModulus / ad.ConcreteModulus;
        double tos = -e.TopOfSteelBelowGrade;
        var rects = new List<(double x0, double x1, double y0, double y1, double f)>   // f = transformed width factor
        {
            (-e.SlabLeft, e.SlabRight, -e.SlabThickness, 0, 1 / n),
        };
        foreach (double sg in new[] { -1.0, 1.0 })
        {
            double g = sg * e.GirderOffset;
            rects.Add((g - e.TopFlangeWidth / 2, g + e.TopFlangeWidth / 2, tos - e.TopFlangeThickness, tos, 1));
            rects.Add((g - e.WebThickness / 2, g + e.WebThickness / 2, tos - e.TopFlangeThickness - e.WebDepth, tos - e.TopFlangeThickness, 1));
            rects.Add((g - e.BottomFlangeWidth / 2, g + e.BottomFlangeWidth / 2, tos - e.TopFlangeThickness - e.WebDepth - e.BottomFlangeThickness,
                tos - e.TopFlangeThickness - e.WebDepth, 1));
        }
        double A = 0, Sx = 0, Sy = 0;
        foreach (var r in rects) { double a = (r.x1 - r.x0) * (r.y1 - r.y0) * r.f; A += a; Sx += a * (r.x0 + r.x1) / 2; Sy += a * (r.y0 + r.y1) / 2; }
        double cx = Sx / A, cy = Sy / A, Iy = 0, Iz = 0, J = 0;
        foreach (var r in rects)
        {
            double b = r.x1 - r.x0, h = r.y1 - r.y0, a = b * h * r.f;
            Iy += r.f * b * h * h * h / 12 + a * Math.Pow((r.y0 + r.y1) / 2 - cy, 2);
            Iz += r.f * h * b * b * b / 12 + a * Math.Pow((r.x0 + r.x1) / 2 - cx, 2);
            double lo = Math.Min(b, h), hi = Math.Max(b, h);
            J += r.f * hi * lo * lo * lo / 3;
        }
        _m.DeckProps = new CsSectionProps { A = A, Iy = Iy, Iz = Iz, J = J, Centroid = new Vec2(cx, cy), Depth = e.Depth, Width = e.SlabLeft + e.SlabRight };
        double girders = 2 * (e.TopFlangeWidth * e.TopFlangeThickness + e.WebDepth * e.WebThickness + e.BottomFlangeWidth * e.BottomFlangeThickness);
        double fbLen = 2 * e.GirderOffset - e.WebThickness;
        girders += e.StringerOffsets.Count * (2 * e.StringerFlangeWidth * e.StringerFlangeThickness + e.StringerWebDepth * e.StringerWebThickness);   // + longitudinal supports
        double fb = e.FloorbeamSpacing > 0 ? (e.FloorbeamTopFlangeWidth * e.FloorbeamTopFlangeThickness + e.FloorbeamWebDepth * e.FloorbeamWebThickness
                    + e.FloorbeamBottomFlangeWidth * e.FloorbeamBottomFlangeThickness) * fbLen / e.FloorbeamSpacing : 0;
        _m.DeckWeightPerLength = ((girders + fb) * ad.SteelDensity + (e.SlabLeft + e.SlabRight) * e.SlabThickness * ad.ConcreteDensity) * CableStayedAnalysis.G;
        Info($"Cable-stayed edge girder deck: steel area {(girders + fb) / (0.0254 * 0.0254):0} in² (two girders + floorbeams smeared), "
            + $"weight {_m.DeckWeightPerLength / 14593.9:0.000} kip/ft (slab {(e.SlabLeft + e.SlabRight) * e.SlabThickness * ad.ConcreteDensity * CableStayedAnalysis.G / 14593.9:0.000}); "
            + $"composite section n = {n:0.0}: I {Iy / Math.Pow(0.3048, 4):0} ft⁴.");
    }

    /// <summary>Frame at a station and offset: X along the alignment WITH the grade, Y right (horizontal), Z normal to the grade (up).</summary>
    private Frame3 DeckFrame(double sta, double offset, double z)
    {
        var t = Vec2.FromAngle(_al.AngleAt(sta));
        double slope = Grade(sta + 0.5) - Grade(sta - 0.5);
        var x = new Vec3(t.X, t.Y, slope).Normalized();
        var y = t.Right.WithZ(0);
        var zz = Vec3.Cross(y, x).Normalized();
        return new Frame3(_al.PointAt(sta, offset).WithZ(Grade(sta) + z), x, y, zz);
    }

    /// <summary>Pylon support station that owns a deck station (nearest pylon).</summary>
    private double NearestPylon(double sta) =>
        _pylonSupports.Where(p => p != null).Select(p => p!.Station).OrderBy(p => Math.Abs(p - sta)).DefaultIfEmpty(double.NaN).First();

    /// <summary>Floorbeam stations: every FloorbeamSpacing from the nearest pylon, plus the unit ends.</summary>
    private List<double> FloorbeamStations(double a, double b)
    {
        var e = Eg!;
        var fbs = new List<double>();
        if (e.FloorbeamSpacing > 0)
        {
            var pys = _pylonSupports.Where(p => p != null).Select(p => p!.Station).OrderBy(x => x).ToList();
            for (int i = 0; i < pys.Count; i++)
            {
                double lo = i == 0 ? a : (pys[i - 1] + pys[i]) / 2, hi = i == pys.Count - 1 ? b : (pys[i] + pys[i + 1]) / 2;
                for (int k = (int)Math.Ceiling((lo - pys[i]) / e.FloorbeamSpacing - 1e-6); pys[i] + k * e.FloorbeamSpacing <= hi + 1e-6; k++)
                {
                    double x = pys[i] + k * e.FloorbeamSpacing;
                    if (x >= a - 1e-6 && x <= b + 1e-6 && !fbs.Any(q => Math.Abs(q - x) < 0.01)) fbs.Add(x);
                }
            }
            foreach (double x in new[] { a, b }) if (!fbs.Any(q => Math.Abs(q - x) < e.FloorbeamSpacing / 3)) fbs.Add(x);
            fbs.Sort();
        }
        return fbs;
    }

    private void BuildEdgeGirderDeck(double s0, double s1, List<double> stations, Mesh slab)
    {
        var e = Eg!;
        double a = Math.Max(s0, _egFrom), b = Math.Min(s1, _egTo);
        if (b <= a) { Warn("Cable-stayed edge girders: the edge girder range is outside the deck."); return; }
        double tos = -e.TopOfSteelBelowGrade, ttf = e.TopFlangeThickness, D = e.WebDepth, tbf = e.BottomFlangeThickness;
        var st = stations.Where(x => x > a + 1e-6 && x < b - 1e-6).Prepend(a).Append(b).ToList();

        // floorbeam stations first (splices keep clear of them)
        var fbs = FloorbeamStations(a, b);
        double fbHalf = Math.Max(e.FloorbeamTopFlangeWidth, e.FloorbeamBottomFlangeWidth) / 2;
        // splice stations: the splice plates start SpliceBeyondAnchor past the far end of every deck stay anchorage (the anchor point when there is no
        // anchorage), away from its pylon; a splice that would meet a floorbeam moves past it by the same clearance; both girders split at the farther
        // of the two anchorages of a station
        var splices = new SortedSet<double>();
        int moved = 0;
        if (e.SpliceBeyondAnchor > 0)
            foreach (var gr in _d.Stays.Where(g => g.GroundAnchors.Count == 0))
                for (int i = 0; i < gr.Count; i++)
                {
                    double x = AnchorStation(gr, i), p = NearestPylon(x);
                    if (double.IsNaN(p)) continue;
                    double dir = Math.Sign(x - p), half = e.SpliceLength / 2, clr = e.SpliceBeyondAnchor;
                    double far = _anchorEnds.Where(q => Math.Abs(q.sta - x) < 0.01).Select(q => q.far).DefaultIfEmpty(x).MaxBy(f => dir * f);
                    double sp = far + dir * (clr + half);
                    for (int guard = 0; guard < 10; guard++)
                    {
                        var hit = fbs.Where(f => Math.Abs(f - sp) < half + fbHalf + clr - 1e-6).OrderBy(f => dir * f).Cast<double?>().FirstOrDefault();
                        if (hit == null) break;
                        sp = hit.Value + dir * (fbHalf + clr + half);
                        if (guard == 0) moved++;
                    }
                    sp = Math.Round(sp, 3);
                    if (sp > a + 0.5 && sp < b - 0.5 && !splices.Any(q => Math.Abs(q - sp) < 0.05)) splices.Add(sp);
                }
        // girders: one field section per side between splices
        var cuts = splices.Prepend(a).Append(b).ToList();
        int pieces = 0;
        foreach (var (side, sg) in new[] { ("L", -1.0), ("R", 1.0) })
        {
            double g = sg * e.GirderOffset;
            for (int k = 0; k + 1 < cuts.Count; k++)
            {
                string asm = $"EG{side}-{k + 1}";
                _m.Steel.Assemblies.Add(new SteelAssembly { Mark = asm, Kind = AssemblyKind.CsEdgeGirder,
                    Description = $"Edge girder {(side == "L" ? "left" : "right")} field section {k + 1}, sta {_u.FormatStation(cuts[k])} to {_u.FormatStation(cuts[k + 1])}" });
                var pst = st.Where(x => x > cuts[k] + 1e-6 && x < cuts[k + 1] - 1e-6).Prepend(cuts[k]).Append(cuts[k + 1]).ToList();
                foreach (var (mk, cat, x0, x1, y0, y1, what) in new[]
                {
                    ("TF", PartCategory.FlangePlate, g - e.TopFlangeWidth / 2, g + e.TopFlangeWidth / 2, tos - ttf, tos, $"top flange {L(e.TopFlangeWidth)} x {L(ttf)}"),
                    ("W", PartCategory.WebPlate, g - e.WebThickness / 2, g + e.WebThickness / 2, tos - ttf - D, tos - ttf, $"web {L(D)} x {L(e.WebThickness)}"),
                    ("BF", PartCategory.FlangePlate, g - e.BottomFlangeWidth / 2, g + e.BottomFlangeWidth / 2, tos - ttf - D - tbf, tos - ttf - D, $"bottom flange {L(e.BottomFlangeWidth)} x {L(tbf)}"),
                })
                {
                    var r = new List<Vec2> { new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1) };
                    var mesh = new Mesh();
                    Sweep(mesh, r, u => Ring(r, u), pst, "edge girder");
                    _m.Steel.Parts.Add(new SteelPart { Mark = $"{asm}-{mk}", Category = cat, Assembly = asm, Mesh = mesh, Length = pst[^1] - pst[0],
                        Description = $"Edge girder {what}", Assumption = _flag });
                }
                pieces++;
            }
            // field splices
            int si = 0;
            foreach (double sp in splices)
            {
                si++;
                string asm = $"FS{side}-{si}";
                _m.Steel.Assemblies.Add(new SteelAssembly { Mark = asm, Kind = AssemblyKind.CsFieldSplice, Description = $"Edge girder field splice {(side == "L" ? "left" : "right")} at sta {_u.FormatStation(sp)}" });
                var f = DeckFrame(sp, g, 0);
                double hl = e.SpliceLength / 2, tw = e.SpliceWebPlateThickness, tf = e.SpliceFlangePlateThickness, tw2 = e.WebThickness / 2;
                double gap = 0.0125;   // inside flange plates stop 1/2" clear of the web
                var plates = new List<(string mk, double y0, double y1, double z0, double z1)>
                {
                    ("WN", -tw2 - tw, -tw2, tos - ttf - D + 0.05, tos - ttf - 0.05), ("WF", tw2, tw2 + tw, tos - ttf - D + 0.05, tos - ttf - 0.05),
                    ("TO", -e.TopFlangeWidth / 2, e.TopFlangeWidth / 2, tos, tos + tf),
                    ("TI1", -e.TopFlangeWidth / 2, -tw2 - gap, tos - ttf - tf, tos - ttf), ("TI2", tw2 + gap, e.TopFlangeWidth / 2, tos - ttf - tf, tos - ttf),
                    ("BO", -e.BottomFlangeWidth / 2, e.BottomFlangeWidth / 2, tos - ttf - D - tbf - tf, tos - ttf - D - tbf),
                    ("BI1", -e.BottomFlangeWidth / 2, -tw2 - gap, tos - ttf - D, tos - ttf - D + tf), ("BI2", tw2 + gap, e.BottomFlangeWidth / 2, tos - ttf - D, tos - ttf - D + tf),
                };
                foreach (var pl in plates)
                {
                    var mesh = new Mesh();
                    MeshBuilder.AddBox(mesh, f, -hl, hl, pl.y0, pl.y1, pl.z0, pl.z1);
                    _m.Steel.Parts.Add(new SteelPart { Mark = $"{asm}-{pl.mk}", Category = PartCategory.SplicePlate, Assembly = asm, Mesh = mesh, Length = e.SpliceLength,
                        Description = $"Field splice plate {L(pl.y1 - pl.y0)} x {L(pl.z1 - pl.z0)} x {L(e.SpliceLength)} (bolts not modeled)", Assumption = _flag });
                }
            }
        }

        double yIn = e.GirderOffset - e.WebThickness / 2;
        var anchorSta = _d.Stays.Where(g => g.GroundAnchors.Count == 0).SelectMany(g => Enumerable.Range(0, g.Count).Select(i => AnchorStation(g, i))).ToList();
        for (int i = 0; i < fbs.Count; i++)
        {
            string asm = $"FB-{i + 1}";
            _m.Steel.Assemblies.Add(new SteelAssembly { Mark = asm, Kind = AssemblyKind.CsFloorbeam,
                Description = $"Floorbeam {i + 1} at sta {_u.FormatStation(fbs[i])}{(anchorSta.Any(x => Math.Abs(x - fbs[i]) < 0.01) ? " (stay anchorage)" : "")}" });
            var f = DeckFrame(fbs[i], 0, 0);
            bool atAnchor = anchorSta.Any(x => Math.Abs(x - fbs[i]) < 0.01);
            double ttfb = e.FloorbeamTopFlangeThickness, Dfb = atAnchor && e.FloorbeamAtAnchorWebDepth > 0 ? e.FloorbeamAtAnchorWebDepth : e.FloorbeamWebDepth;
            // end floorbeams at the unit ends sit inside the unit
            double hb = Math.Max(e.FloorbeamTopFlangeWidth, e.FloorbeamBottomFlangeWidth) / 2, sh = fbs[i] - hb < a ? hb : fbs[i] + hb > b ? -hb : 0;
            var plates = new List<(string mk, double hw, double y, double z0, double z1)>
            {
                ("TF", e.FloorbeamTopFlangeWidth / 2, e.GirderOffset - e.TopFlangeWidth / 2, tos - ttfb, tos),
                ("W", e.FloorbeamWebThickness / 2, yIn, tos - ttfb - Dfb, tos - ttfb),
                ("BF", e.FloorbeamBottomFlangeWidth / 2, yIn, tos - ttfb - Dfb - e.FloorbeamBottomFlangeThickness, tos - ttfb - Dfb),
            };
            foreach (var pl in plates)
            {
                var mesh = new Mesh();
                MeshBuilder.AddBox(mesh, f, sh - pl.hw, sh + pl.hw, -pl.y, pl.y, pl.z0, pl.z1);
                _m.Steel.Parts.Add(new SteelPart { Mark = $"{asm}-{pl.mk}", Category = PartCategory.CsFloorbeamPlate, Assembly = asm, Mesh = mesh, Length = 2 * pl.y,
                    Description = $"Floorbeam {(pl.mk == "W" ? $"web {L(Dfb)} x {L(e.FloorbeamWebThickness)}" : pl.mk == "TF" ? $"top flange {L(2 * pl.hw)} x {L(ttfb)}" : $"bottom flange {L(2 * pl.hw)} x {L(e.FloorbeamBottomFlangeThickness)}")} (connections not modeled)",
                    Assumption = _flag });
            }
        }
        // longitudinal supports (stringers): one piece per bay, framed between the floorbeam webs, top flush with the top of steel
        int sn = 0;
        foreach (double off in e.StringerOffsets)
        {
            sn++;
            for (int i = 0; i + 1 < fbs.Count; i++)
            {
                double x0 = fbs[i] + e.FloorbeamWebThickness / 2, x1 = fbs[i + 1] - e.FloorbeamWebThickness / 2, xm = (x0 + x1) / 2, hl = (x1 - x0) / 2;
                string asm = $"LS{sn}-{i + 1}";
                _m.Steel.Assemblies.Add(new SteelAssembly { Mark = asm, Kind = AssemblyKind.CsStringer,
                    Description = $"Longitudinal support {sn} ({L(Math.Abs(off))} {(off < 0 ? "left" : off > 0 ? "right" : "on the CL")}), bay sta {_u.FormatStation(fbs[i])} - {_u.FormatStation(fbs[i + 1])}" });
                var f = DeckFrame(xm, off, 0);
                double tf = e.StringerFlangeThickness, bw = e.StringerFlangeWidth / 2;
                foreach (var (mk, hw, z0, z1) in new[]
                {
                    ("TF", bw, tos - tf, tos), ("W", e.StringerWebThickness / 2, tos - tf - e.StringerWebDepth, tos - tf),
                    ("BF", bw, tos - 2 * tf - e.StringerWebDepth, tos - tf - e.StringerWebDepth),
                })
                {
                    var mesh = new Mesh();
                    MeshBuilder.AddBox(mesh, f, -hl, hl, -hw, hw, z0, z1);
                    _m.Steel.Parts.Add(new SteelPart { Mark = $"{asm}-{mk}", Category = PartCategory.CsStringerPlate, Assembly = asm, Mesh = mesh, Length = 2 * hl,
                        Description = $"Longitudinal support {(mk == "W" ? $"web {L(e.StringerWebDepth)} x {L(e.StringerWebThickness)}" : $"{(mk == "TF" ? "top" : "bottom")} flange {L(2 * bw)} x {L(tf)}")} (end connections not modeled)",
                        Assumption = _flag });
                }
            }
        }

        // deck: cast-in-place slab, or precast panels + closure pours over every member
        if (e.Precast == null)
        {
            var sec = new List<Vec2> { new(-e.SlabLeft, -e.SlabThickness), new(e.SlabRight, -e.SlabThickness), new(e.SlabRight, 0), new(-e.SlabLeft, 0) };
            Sweep(slab, sec, u => Ring(sec, u), st, "slab");
        }
        else BuildPrecastDeck(st, fbs, a, b);

        Info($"Cable-stayed edge girders sta {_u.FormatStation(a)} - {_u.FormatStation(b)}: {pieces} girder field sections, {splices.Count} splice lines "
            + $"(plates {L(e.SpliceBeyondAnchor)} past the far end of each stay anchorage{(moved > 0 ? $"; {moved} moved past the next floorbeam" : "")}), {fbs.Count} floorbeams at {L(e.FloorbeamSpacing)}.");
        foreach (double sp in splices)
            foreach (double fb in fbs.Where(f => Math.Abs(f - sp) < e.SpliceLength / 2 + Math.Max(e.FloorbeamTopFlangeWidth, e.FloorbeamBottomFlangeWidth) / 2))
                Warn($"Cable-stayed edge girders: field splice at sta {_u.FormatStation(sp)} overlaps the floorbeam at {_u.FormatStation(fb)}.");
    }

    /// <summary>Precast panels between the members, closure pours over the edge girders (to the slab edges) and the longitudinal supports (running through)
    /// and over every floorbeam (between the longitudinal closures).</summary>
    private void BuildPrecastDeck(List<double> st, List<double> fbs, double a, double b)
    {
        var e = Eg!; var pc = e.Precast!;
        double Cw(double flange) => Math.Max(pc.MinClosureWidth, flange - 2 * pc.PanelSeat);
        double ts = e.SlabThickness;
        // longitudinal closures (offset ranges): edge girders out to the slab edges, longitudinal supports centred
        var longi = new List<(string name, double y0, double y1)>
        {
            ("Left edge girder", -e.SlabLeft, -e.GirderOffset + Cw(e.TopFlangeWidth) / 2),
            ("Right edge girder", e.GirderOffset - Cw(e.TopFlangeWidth) / 2, e.SlabRight),
        };
        int k = 0;
        foreach (double off in e.StringerOffsets.OrderBy(x => x))
            longi.Add(($"Longitudinal support {++k}", off - Cw(e.StringerFlangeWidth) / 2, off + Cw(e.StringerFlangeWidth) / 2));
        longi = longi.OrderBy(x => x.y0).ToList();
        // panel / floorbeam closure bands across = the gaps between the longitudinal closures
        var bands = new List<(double y0, double y1)>();
        for (int i = 0; i + 1 < longi.Count; i++) if (longi[i + 1].y0 > longi[i].y1 + 1e-6) bands.Add((longi[i].y1, longi[i + 1].y0));
        double cfb = Cw(e.FloorbeamTopFlangeWidth);
        var parts = _m.Concrete.DeckParts;
        List<double> Sub(double x0, double x1) => st.Where(x => x > x0 + 1e-6 && x < x1 - 1e-6).Prepend(x0).Append(x1).ToList();
        void Piece(string name, string kind, double s0, double s1, double y0, double y1)
        {
            if (s1 - s0 < 0.01 || y1 - y0 < 0.01) return;
            var sec = new List<Vec2> { new(y0, -ts), new(y1, -ts), new(y1, 0), new(y0, 0) };
            var mesh = new Mesh();
            Sweep(mesh, sec, u => Ring(sec, u), Sub(s0, s1), kind);
            parts.Add(new DeckPart(name, kind, mesh, _flag));
        }
        foreach (var l in longi) Piece($"Closure pour over the {l.name.ToLowerInvariant()}", "closure pour", a, b, l.y0, l.y1);
        // floorbeam closures: centred on each floorbeam, kept inside the unit at its ends
        var fbBand = fbs.Select(f => (s0: Math.Max(a, f - cfb / 2), s1: Math.Min(b, f + cfb / 2))).ToList();
        for (int i = 0; i < fbs.Count; i++)
            for (int j = 0; j < bands.Count; j++)
                Piece($"Closure pour over floorbeam {i + 1}{(bands.Count > 1 ? $", bay {j + 1} across" : "")}", "closure pour", fbBand[i].s0, fbBand[i].s1, bands[j].y0, bands[j].y1);
        int np = 0;
        for (int i = 0; i + 1 < fbs.Count; i++)
            for (int j = 0; j < bands.Count; j++)
            {
                np++;
                Piece($"Precast panel {i + 1}-{j + 1} (floorbeams {i + 1} - {i + 2})", "precast panel", fbBand[i].s1, fbBand[i + 1].s0, bands[j].y0, bands[j].y1);
            }
        var p0 = parts.FirstOrDefault(p => p.IsPanel);
        Info($"Cable-stayed precast deck: {parts.Count(p => p.IsPanel)} panels {L(bands.Select(x => x.y1 - x.y0).DefaultIfEmpty(0).Max())} across x "
            + $"{L(e.FloorbeamSpacing - cfb)} along (typ.), {parts.Count(p => !p.IsPanel)} closure pours ({L(cfb)} over the floorbeams, {L(Cw(e.TopFlangeWidth))} + overhang over the edge girders"
            + (e.StringerOffsets.Count > 0 ? $", {L(Cw(e.StringerFlangeWidth))} over {e.StringerOffsets.Count} longitudinal support(s)" : "") + ").");
    }

    /// <summary>Rocket launcher anchorage of one stay (<see cref="CsStayAnchorage"/>): the stay's deck point is the working point on the pipe axis;
    /// returns the bearing plate point (the stay's lower end), or the deck point itself when no anchorage applies.</summary>
    private Vec3 RocketLauncher(string stayName, Vec3 deckPoint, Vec3 upper, double sta, double deckOffset)
    {
        var e = Eg; var an = e?.Anchorage;
        if (e == null || an == null || double.IsNaN(sta) || sta < _egFrom - 1e-6 || sta > _egTo + 1e-6) return deckPoint;
        double side = Math.Sign(deckOffset);
        if (Math.Abs(Math.Abs(deckOffset) - (e.GirderOffset + an.AxisOutboard)) > 0.3) return deckPoint;
        var a = (upper - deckPoint).Normalized();
        if (a.Z < 0.05) { Warn($"Cable-stayed anchorage of {stayName}: the stay is too flat for an anchor pipe."); return deckPoint; }
        double tos = Grade(sta) - e.TopOfSteelBelowGrade;
        double tfUnder = tos - e.TopFlangeThickness, bfTop = tfUnder - e.WebDepth, bfBot = bfTop - e.BottomFlangeThickness;
        Vec3 AtZ(double z) => deckPoint + a * ((z - deckPoint.Z) / a.Z);   // point on the axis at a height
        double T(double z) => (z - deckPoint.Z) / a.Z;                     // axis parameter (along a from the deck point) at a height
        var low = AtZ(bfBot - an.PipeBelowBottomFlange);
        var top = AtZ(tos + an.PipeAboveTopOfSteel);
        string asm = $"{stayName}-RL";
        _m.Steel.Assemblies.Add(new SteelAssembly { Mark = asm, Kind = AssemblyKind.CsAnchorage, Description = $"Rocket launcher anchorage of stay {stayName} at sta {_u.FormatStation(sta)}" });
        var parts = new List<SteelPart>();
        void Add(string mk, PartCategory cat, Mesh mesh, double len, string desc, double density = 0) =>
            parts.Add(new SteelPart { Mark = $"{asm}-{mk}", Category = cat, Assembly = asm, Mesh = mesh, Length = len, Description = desc, Assumption = _flag, Density = density });

        var e1 = Vec3.Cross(Math.Abs(a.Z) > 0.9 ? new Vec3(1, 0, 0) : new Vec3(0, 0, 1), a).Normalized();
        var e2 = Vec3.Cross(a, e1);
        double r = an.PipeDiameter / 2, tbp = an.BearingPlateThickness, plen = (top - low).Length;
        // bearing plate under the pipe's lower end, pipe from it to the upper end (hollow: density scaled to the wall area)
        var bp = new Mesh();
        MeshBuilder.AddBox(bp, new Frame3(low, e1, e2, a), -an.BearingPlateSize / 2, an.BearingPlateSize / 2, -an.BearingPlateSize / 2, an.BearingPlateSize / 2, -tbp, 0);
        Add("BP", PartCategory.StayAnchorPlate, bp, an.BearingPlateSize, $"Anchor bearing plate {L(an.BearingPlateSize)} square x {L(tbp)}");
        var pipe = new Mesh();
        MeshBuilder.AddPrism(pipe, new Frame3(low, e1, e2, a), r, 24, 0, plen);
        double full = 0.5 * 24 * r * r * Math.Sin(2 * Math.PI / 24), wall = Math.PI * (r * r - Math.Pow(Math.Max(0, r - an.PipeWall), 2));
        Add("P", PartCategory.StayAnchorPipe, pipe, plen, $"Anchor pipe {L(an.PipeDiameter)} OD x {L(an.PipeWall)} wall, {L(plen)} long, from {L(an.PipeAboveTopOfSteel)} above the top of steel to {L(an.PipeBelowBottomFlange)} below the bottom flange",
            _d.Analysis.SteelDensity * wall / full);

        // frames: nW = horizontal web normal toward the pipe, tH = horizontal along the girder, web face at the girder web outer face
        var nW = (_al.RightNormalAt(sta) * side).WithZ(0).Normalized();
        var tH = Vec3.Cross(new Vec3(0, 0, 1), nW).Normalized();
        double webFace = e.WebThickness / 2;                 // web outer face from the web centerline
        double reach = an.AxisOutboard - webFace;            // web outer face -> pipe axis
        var webPt = deckPoint - nW * an.AxisOutboard;        // web centerline at the deck point's height
        // stem mounting plate: flat on the web outer face, along the stay's projection on the web, between the girder flanges (top / bottom edges level)
        {
            var pa = a - nW * Vec3.Dot(a, nW); pa = pa.Normalized();
            double du = Vec3.Dot(pa, tH), dz = pa.Z;
            var f = new Frame3(new Vec3((webPt + nW * webFace).X, (webPt + nW * webFace).Y, 0), tH, new Vec3(0, 0, 1), nW);
            double u0 = Vec3.Dot(deckPoint - webPt, tH);       // 0: the deck point is on the web normal through webPt
            double U(double z) => u0 + du / dz * (z - deckPoint.Z);
            double hw = an.MountingPlateWidth / 2 / Math.Max(0.05, Math.Abs(dz));   // horizontal half width at constant height
            var poly = new List<Vec2> { new(U(bfTop) - hw, bfTop), new(U(bfTop) + hw, bfTop), new(U(tfUnder) + hw, tfUnder), new(U(tfUnder) - hw, tfUnder) };
            if (MeshBuilder.SignedArea(poly) < 0) poly.Reverse();
            var m = new Mesh();
            MeshBuilder.AddExtrusion(m, poly, f, 0, an.MountingPlateThickness);
            Add("SM", PartCategory.StayAnchorPlate, m, (tfUnder - bfTop) / Math.Abs(dz), $"Stem mounting plate {L(an.MountingPlateWidth)} x {L(an.MountingPlateThickness)}, bolted to the girder web (bolts not modeled)");
        }
        // stem plate: in the plane of the pipe axis and the web normal, from the mounting plate to the pipe axis, between the anchor flanges
        {
            double t0 = T(bfTop + an.FlangeThickness), t1 = T(tfUnder - an.FlangeThickness);
            var n2 = Vec3.Cross(a, nW).Normalized();
            var f = new Frame3(deckPoint, a, nW, n2);
            double w0 = -(reach - an.MountingPlateThickness);
            // the plate's ends are level (anchor flanges): at w the axis parameter of a height shifts by 0 (nW is horizontal) -> rectangle in (t, w)
            var poly = new List<Vec2> { new(t0, w0), new(t1, w0), new(t1, 0), new(t0, 0) };
            if (MeshBuilder.SignedArea(poly) < 0) poly.Reverse();
            var m = new Mesh();
            MeshBuilder.AddExtrusion(m, poly, f, -an.StemThickness / 2, an.StemThickness / 2);
            Add("ST", PartCategory.StayAnchorPlate, m, t1 - t0, $"Stem plate {L(reach - an.MountingPlateThickness)} x {L(an.StemThickness)} x {L(t1 - t0)} (mounting plate to pipe)");
        }
        // anchor top / bottom flanges: level plates under the girder top flange / on the girder bottom flange, from the web face out past the pipe
        foreach (var (mk, z0, z1, what) in new[] { ("TF", tfUnder - an.FlangeThickness, tfUnder, "top"), ("BF", bfTop, bfTop + an.FlangeThickness, "bottom") })
        {
            var c = AtZ((z0 + z1) / 2);
            var f = new Frame3(new Vec3((webPt + nW * webFace).X, (webPt + nW * webFace).Y, 0) + tH * Vec3.Dot(c - webPt, tH), tH, nW, new Vec3(0, 0, 1));
            var m = new Mesh();
            MeshBuilder.AddBox(m, f, -an.FlangeLength / 2, an.FlangeLength / 2, 0, reach + r + 0.025, z0, z1);
            Add(mk, PartCategory.StayAnchorPlate, m, an.FlangeLength, $"Anchor {what} flange {L(an.FlangeLength)} x {L(reach + r + 0.025)} x {L(an.FlangeThickness)}, bolted to the girder {what} flange (bolts not modeled)");
        }
        _m.Steel.Parts.AddRange(parts);
        // far end along the girder (away from the pylon) over every anchorage part: the field splice goes past it
        double p0 = NearestPylon(sta), dirS = Math.Sign(sta - p0);
        double far = parts.SelectMany(p => p.Mesh.Positions).Select(v => _al.Inverse(v.XY).Station).Select(x => dirS * x).Max() * dirS;
        _anchorEnds.Add((sta, far));
        return low - a * tbp;
    }
}
