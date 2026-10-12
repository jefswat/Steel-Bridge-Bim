using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Steel;

/// <summary>
/// Steel TUB girders (OPTIONAL, <see cref="GirderLayoutDefinition.Tub"/>): girder plates of the rotated tub section, field splices,
/// panel-point stiffeners, internal K frames + top struts, top lateral bracing, external intermediate diaphragms, support
/// diaphragms, bearings (pad + beveled plate), access hatches and studs. Runs instead of the I-girder phases (Run) and leaves
/// them untouched. Conventions (plans, Bridge O general notes): the section is rotated about the top working point with the deck;
/// support diaphragm webs plumb; stiffeners, frames and diaphragms plumb (fabricator's option "normal to grade or plumb").
/// Exact NURBS geometry is not applied to tubs (meshes / exact extrusions only).
/// </summary>
public sealed partial class SteelModelGenerator
{
    private TubGirderDefinition TubDef => _def.Girders.Tub!;
    private readonly Dictionary<int, (List<double> s, List<double> slope)> _tubSlope = new();
    private readonly Dictionary<(int g, string plate), List<Hole>> _tubHoles = new();
    private readonly Dictionary<(int g, int pp, int side), List<Hole>> _tubStiffHoles = new();
    private TubGirderReport _tr = null!;

    private void Info(string m) => _diag.Enqueue(new Diagnostic(DiagnosticSeverity.Info, m));

    // ================================================================== geometry helpers

    private double TubSlopeAt(int gi, double s)
    {
        var (ss, sl) = _tubSlope[gi];
        if (ss.Count == 0) return 0;
        if (s <= ss[0]) return sl[0];
        if (s >= ss[^1]) return sl[^1];
        int i = ss.BinarySearch(s);
        if (i >= 0) return sl[i];
        i = ~i;
        double u = (s - ss[i - 1]) / Math.Max(1e-12, ss[i] - ss[i - 1]);
        return sl[i - 1] + (sl[i] - sl[i - 1]) * u;
    }

    private TubFrame TF(int gi, double s)
    {
        var (p, t, _) = _f.Girders[gi].Path.At(s);
        return TubFrame.Create(p, t, TubSlopeAt(gi, s));
    }

    private double BrgS(int gi, int support)
    {
        var g = _f.Girders[gi];
        int n = g.Spans.Count;
        return support < n ? g.DistanceAt(support, 0) : g.DistanceAt(n - 1, 1);
    }

    private double TubLoc(int gi, TubLocation l)
    {
        if (l.Support >= 0) return BrgS(gi, l.Support) + l.Offset;
        var sp = _f.Splices.FirstOrDefault(x => x.Girder == gi && x.Number == l.Splice);
        return sp == null ? double.NaN : sp.GirderDistance + l.Offset;
    }

    private (double W, double T) TubPlate(int gi, GirderPlate p, double s) => _f.Girders[gi].PlateAt(p, s) ?? (0, 0);

    private List<Hole> TubHoles(int gi, string plate)
    {
        if (!_tubHoles.TryGetValue((gi, plate), out var l)) _tubHoles[(gi, plate)] = l = new List<Hole>();
        return l;
    }

    private List<Hole> TubStiffHoles(int gi, int pp, int side)
    {
        if (!_tubStiffHoles.TryGetValue((gi, pp, side), out var l)) _tubStiffHoles[(gi, pp, side)] = l = new List<Hole>();
        return l;
    }

    private static string TopKey(int side) => side < 0 ? "TFL" : "TFR";
    private static string WebKey(int side) => side < 0 ? "WL" : "WR";

    private Func<double, double, double, Vec3> TubTopMap(int gi, int side) => (s, w, y) => TF(gi, s).At(side * TubDef.TopWebSpacing / 2 + w, y);
    private Func<double, double, double, Vec3> TubBottomMap(int gi) => (s, w, y) => TF(gi, s).At(w, y);
    private Func<double, double, double, Vec3> TubWebMap(int gi, int side) => (s, w, n) => TF(gi, s).At(TubSection.WebPoint(TubDef, side, w, n));

    /// <summary>3D direction of a section-plane vector.</summary>
    private static Vec3 Dir(TubFrame f, Vec2 v) => f.Lateral * v.X + f.Up * v.Y;

    private void AddBolt(Vec3 headFace, Vec3 toNut, double grip, string asm)
    {
        var z = toNut.Normalized();
        var hint = Math.Abs(z.Z) < 0.9 ? new Vec3(0, 0, 1) : new Vec3(1, 0, 0);
        var x = Vec3.Cross(hint, z).Normalized();
        var y = Vec3.Cross(z, x);
        _bolts.Add(new BoltInstance { Frame = new Frame3(headFace, x, y, z), Grip = grip, Length = _model.Bolt.LengthForGrip(grip), Assembly = asm });
    }

    private string Ft2(double m) => $"{m / 0.3048:F3} ft";

    // ================================================================== driver

    private void RunTub()
    {
        var t = TubDef;
        _tr = new TubGirderReport();
        _model.Tub = _tr;
        double D = Sec.WebDepth;
        _tr.WebDepth = D;
        _tr.WebSlopeLength = TubSection.WebSlopeLength(t, D);
        Info($"Tub girders: {_f.Girders.Count} tubs, web depth {In(D)} vertical, web slope {1 / t.WebSlope:0.##}:1, web length on the slope {In(_tr.WebSlopeLength)}"
             + (double.IsNaN(t.PlanWebSlopeLength) ? "" : $" (plan {In(t.PlanWebSlopeLength)}, difference {In(_tr.WebSlopeLength - t.PlanWebSlopeLength)})")
             + $", top web working points {In(t.TopWebSpacing)} apart, rotated with the deck: {t.RotateWithDeck}.");

        foreach (var g in _f.Girders)
        {
            int gi = g.Index;
            _sections[gi] = FieldSections(g);
            _studExclusions[gi] = new System.Collections.Concurrent.ConcurrentBag<(double, double)>();
            var ss = g.Path.S.ToList();
            var sl = ss.Select(s => t.RotateWithDeck ? TubSection.DeckSlope(_f, g.Path.At(s).point.XY) : 0.0).ToList();
            _tubSlope[gi] = (ss, sl);
            _tr.GirderLengths.Add(g.Length);
            _tr.BearingS.Add(Enumerable.Range(0, g.Spans.Count + 1).Select(j => BrgS(gi, j)).ToList());
            _tr.SpliceS.Add(_f.Splices.Where(x => x.Girder == gi).ToDictionary(x => x.Number, x => x.GirderDistance));
            foreach (var r in g.PlateRuns) _tr.PlateRuns.Add(new TubGirderReport.PlateRun(gi, r.Plate, r.S0, r.S1, r.Width, r.Thickness));
        }
        foreach (var z in t.TensionZones)
        {
            double a = TubLoc(z.Girder, z.From), b = TubLoc(z.Girder, z.To);
            if (double.IsNaN(a) || double.IsNaN(b)) { Warn($"Tension zone G{z.Girder + 1} {z.From} .. {z.To}: location not found."); continue; }
            _tr.TensionZones.Add(new TubGirderReport.ZoneRow(z.Girder, a, b, z.Flanges));
        }

        // Panel points (bracing layout).
        var panels = new List<List<TubGirderReport.PanelPoint>>();
        foreach (var g in _f.Girders) panels.Add(TubPanelPoints(g));
        _tr.PanelPoints.AddRange(panels.SelectMany(p => p));

        // Connections first (they register bolt holes in the girder plates and stiffeners).
        foreach (var sp in _f.Splices) TubSplice(sp);
        if (t.Lateral.Enabled) foreach (var g in _f.Girders) TubLaterals(g, panels[g.Index]);
        foreach (var g in _f.Girders) foreach (var pp in panels[g.Index].Select((p, i) => (p, i))) TubPanelBracing(g, pp.p, pp.i);
        TubExternalDiaphragms(panels);
        foreach (var sd in t.SupportDiaphragms) TubSupportDiaphragm(sd);

        // Members.
        foreach (var g in _f.Girders)
        {
            TubGirderPlates(g);
            for (int i = 0; i < panels[g.Index].Count; i++) TubStiffeners(g, panels[g.Index][i], i);
            TubStuds(g);
        }
        foreach (var b in t.Bearings) TubBearings(b);

        foreach (var p in t.DeckPours.OrderBy(p => p.FromStation))
            Info($"Deck pour section {p.Section}: {_u.FormatStation(p.FromStation)} to {_u.FormatStation(p.ToStation)} ({Ft2(p.ToStation - p.FromStation)} along the alignment){(p.Note != "" ? ", " + p.Note : "")} (data only; the slab solid is not split).");
        foreach (var u in t.Unresolved.Concat(t.Internal.Unresolved)) Warn("UNRESOLVED " + u);
        Info($"Tub bracing: {_tr.InternalCrossFrames} internal cross frames, {_tr.TopStruts} top struts (incl. those of the cross frames), {_tr.LateralMembers} top lateral members, "
             + $"{_tr.ExternalDiaphragms} external intermediate diaphragms, {_tr.SupportDiaphragms} support diaphragm lines, {_tr.Stiffeners} transverse stiffeners, "
             + $"{_tr.Studs} studs, {_tr.Hatches} access hatches, {_tr.SpliceBolts} field splice bolts.");
    }

    // ================================================================== panel points

    private List<TubGirderReport.PanelPoint> TubPanelPoints(GirderResult g)
    {
        var t = TubDef;
        int gi = g.Index;
        var list = new List<TubGirderReport.PanelPoint>();
        for (int j = 0; j < g.Spans.Count; j++)
        {
            var segs = t.Panels.Where(p => p.Girder == gi && p.Span == j).ToList();
            if (segs.Count == 0) { if (t.Panels.Count > 0) Warn($"{g.Name} span {j + 1}: no bracing panels given."); continue; }
            double a = BrgS(gi, j), b = BrgS(gi, j + 1), cur = a;
            int k = 0;
            foreach (var seg in segs)
            {
                double len = double.IsNaN(seg.Length) ? b - cur : seg.Length;
                double sp = len / Math.Max(1, seg.Spaces);
                for (int i = 1; i <= seg.Spaces; i++)
                {
                    double s = cur + i * sp;
                    if (Math.Abs(s - b) < 0.002) break;
                    k++;
                    var kind = k % Math.Max(1, t.Internal.CrossFrameEvery) == 0 ? TubPanelKind.CrossFrame : TubPanelKind.TopStrut;
                    list.Add(new TubGirderReport.PanelPoint(gi, j, k, s, kind, g.Path.At(s).station));
                }
                cur += len;
            }
            if (Math.Abs(cur - b) > Inch(1.0 / 16))
                Warn($"{g.Name} span {j + 1}: bracing panels end {In(cur - b)} from the CL bearing (panels {Ft2(cur - a)}, girder CL brg to CL brg {Ft2(b - a)}).");
        }
        return list;
    }

    // ================================================================== girder plates

    private void TubGirderPlates(GirderResult g)
    {
        var t = TubDef;
        int gi = g.Index;
        double D = Sec.WebDepth, L = TubSection.WebSlopeLength(t, D);
        var path = g.Path;
        var hatches = t.Hatches.Where(h => h.Girder == gi).Select(h => (h, s: TubLoc(gi, h.At))).Where(x => !double.IsNaN(x.s)).ToList();
        string Zone(GirderPlate p, double a, double b)
        {
            bool top = p == GirderPlate.TopFlange;
            var z = _tr.TensionZones.Where(r => r.Girder == gi && r.S1 > a + 1e-3 && r.S0 < b - 1e-3
                && (r.Flanges == TubFlangeZone.Both || (top ? r.Flanges == TubFlangeZone.Top : r.Flanges == TubFlangeZone.Bottom))).ToList();
            return z.Count == 0 ? "" : ", tension flange (FCM) over " + string.Join(", ", z.Select(r => $"{Ft2(Math.Max(a, r.S0) - a)}..{Ft2(Math.Min(b, r.S1) - a)}"));
        }
        IEnumerable<(double a, double b, double w, double th)> Pieces(GirderPlate p, double s0, double s1) =>
            g.PlateRuns.Where(r => r.Plate == p).Select(r => (a: Math.Max(s0, r.S0), b: Math.Min(s1, r.S1), w: r.Width, th: r.Thickness)).Where(x => x.b > x.a + 1e-4);

        foreach (var (s0, s1, mark) in _sections[gi])
        {
            _assemblies.Add(new SteelAssembly { Mark = mark, Kind = AssemblyKind.GirderFieldSection, Girder = gi,
                Description = $"{g.Name} tub field section, {_u.FormatLength(s1 - s0)}" });
            foreach (int side in new[] { -1, 1 })
            {
                int n = 1;
                var holes = TubHoles(gi, TopKey(side));
                foreach (var (a, b, w, th) in Pieces(GirderPlate.TopFlange, s0, s1))
                {
                    var mesh = new Mesh();
                    var hs = GirderHoles.Sorted(holes.Where(h => h.Center.X > a && h.Center.X < b));
                    PlateBuilder.AddLongPlate(mesh, a, b, path.S, _ => -w / 2, _ => w / 2, _ => 0, _ => th, hs, TubTopMap(gi, side));
                    _parts.Add(new SteelPart { Mark = $"{mark}-TF{(side < 0 ? "L" : "R")}{n++}", Category = PartCategory.FlangePlate, Assembly = mark, Mesh = mesh,
                        Length = b - a, Description = $"Top flange {(side < 0 ? "left" : "right")} {Pl(w, th)} x {Ft2(b - a)}" + Zone(GirderPlate.TopFlange, a, b) });
                }
                n = 1;
                holes = TubHoles(gi, WebKey(side));
                foreach (var (a, b, _, tw) in Pieces(GirderPlate.Web, s0, s1))
                {
                    var mesh = new Mesh();
                    var hs = GirderHoles.Sorted(holes.Where(h => h.Center.X > a && h.Center.X < b));
                    PlateBuilder.AddLongPlate(mesh, a, b, path.S, _ => 0, _ => L, _ => -tw / 2, _ => tw / 2, hs, TubWebMap(gi, side));
                    _parts.Add(new SteelPart { Mark = $"{mark}-W{(side < 0 ? "L" : "R")}{n++}", Category = PartCategory.WebPlate, Assembly = mark, Mesh = mesh,
                        Length = b - a, Description = $"Web {(side < 0 ? "left" : "right")} {Pl(L, tw)} (on the slope) x {Ft2(b - a)}" });
                }
            }
            int nb = 1;
            var bh = TubHoles(gi, "BF");
            foreach (var (a, b, w, th) in Pieces(GirderPlate.BottomFlange, s0, s1))
            {
                var mesh = new Mesh();
                var map = TubBottomMap(gi);
                // split around access hatches (slotted opening + door)
                var cuts = new List<(double a, double b, (TubAccessHatchDefinition h, double s)? hatch)>();
                double cur = a;
                foreach (var hx in hatches.Where(x => x.s > a + x.h.DoorLength && x.s < b - x.h.DoorLength).OrderBy(x => x.s))
                {
                    double h0 = hx.s - hx.h.DoorLength / 2 - Inch(1), h1 = hx.s + hx.h.DoorLength / 2 + Inch(1);
                    if (h0 > cur) cuts.Add((cur, h0, null));
                    cuts.Add((h0, h1, hx));
                    cur = h1;
                }
                if (b > cur) cuts.Add((cur, b, null));
                foreach (var (ca, cb, hatch) in cuts)
                {
                    var hs = GirderHoles.Sorted(bh.Where(h => h.Center.X > ca && h.Center.X < cb));
                    if (hatch is not { } hx)
                    {
                        PlateBuilder.AddLongPlate(mesh, ca, cb, path.S, _ => -w / 2, _ => w / 2, _ => -D - th, _ => -D, hs, map);
                        continue;
                    }
                    var outline = new[] { new Vec2(ca, -w / 2), new Vec2(cb, -w / 2), new Vec2(cb, w / 2), new Vec2(ca, w / 2) };
                    var opening = PlateBuilder.RoundedRectangle(new Vec2(hx.s, 0), hx.h.HoleLength, hx.h.HoleWidth, hx.h.HoleWidth / 2);
                    PlateBuilder.AddMappedWithOpenings(mesh, outline, hs, new[] { opening }, map, -D - th, -D);
                    var door = new Mesh();
                    var dOut = PlateBuilder.RoundedRectangle(new Vec2(hx.s, 0), hx.h.DoorLength, hx.h.DoorWidth, Inch(9));
                    PlateBuilder.AddMappedWithOpenings(door, dOut, Array.Empty<Hole>(), Array.Empty<Vec2[]>(), map, -D - th - hx.h.DoorThickness, -D - th);
                    _parts.Add(new SteelPart { Mark = $"{mark}-HATCH{_tr.Hatches + 1}", Category = PartCategory.AccessDoor, Assembly = mark, Mesh = door, Length = hx.h.DoorLength,
                        Description = $"Access door PL {In(hx.h.DoorThickness)} x {In(hx.h.DoorWidth)} x {In(hx.h.DoorLength)} (bottom flange opening {In(hx.h.HoleWidth)} x {In(hx.h.HoleLength)}) at {Ft2(hx.s)}" });
                    _tr.Hatches++;
                }
                _parts.Add(new SteelPart { Mark = $"{mark}-BF{nb++}", Category = PartCategory.FlangePlate, Assembly = mark, Mesh = mesh,
                    Length = b - a, Description = $"Bottom flange {Pl(w, th)} x {Ft2(b - a)}" + Zone(GirderPlate.BottomFlange, a, b) });
            }
        }
    }

    // ================================================================== field splices

    private void TubSplice(FieldSpliceResult sp)
    {
        var t = TubDef;
        var d = t.Splice;
        var g = _f.Girders[sp.Girder];
        int gi = g.Index;
        double s = sp.GirderDistance, gap = _def.FieldSpliceGap, D = Sec.WebDepth, T = t.TopWebSpacing / 2;
        var ov = d.Splices.FirstOrDefault(o => o.Number == sp.Number);
        string asm = $"FS{sp.Number}-{g.Name}";
        _assemblies.Add(new SteelAssembly { Mark = asm, Kind = AssemblyKind.FieldSplice, Girder = gi, Description = $"Field splice {sp.Number}, {g.Name} (tub)" });
        double sl = s - gap / 2 - 1e-3, sr = s + gap / 2 + 1e-3;
        double hd = HoleDia;
        int bolts = 0;
        var path = g.Path.S;

        // ---- top flanges
        var (tw0, tfL) = TubPlate(gi, GirderPlate.TopFlange, sl);
        var (tw1, tfR) = TubPlate(gi, GirderPlate.TopFlange, sr);
        double W = Math.Min(tw0, tw1), tHi = Math.Max(tfL, tfR), tLo = Math.Min(tfL, tfR);
        bool filler = ov is { FillerThickness: > 0 };
        if (Math.Abs(tfL - tfR) > 1e-4 && (!filler || Math.Abs(ov!.FillerThickness - (tHi - tLo)) > 1e-4))
            Warn($"{asm}: top flanges {In(tfL)} / {In(tfR)} differ by {In(tHi - tLo)}; filler {(filler ? In(ov!.FillerThickness) : "none")} (plan).");
        if (filler && Math.Abs(tfL - tfR) < 1e-4) Warn($"{asm}: filler {In(ov!.FillerThickness)} given but the top flanges are both {In(tfL)}.");
        int thinSide = tfL < tfR ? -1 : 1;
        double Lp = d.TopPlateLength;
        int pn = 1;
        foreach (int side in new[] { -1, 1 })
        {
            var map = TubTopMap(gi, side);
            var holes = new List<Hole>();
            foreach (double line in d.TopLines)
                for (int k = 0; k < d.TopRowsPerSide; k++)
                    foreach (int dir in new[] { -1, 1 })
                        holes.Add(new Hole(new Vec2(s + dir * (d.FirstFromJoint + k * d.TopPitch), side * line), hd));
            TubHoles(gi, TopKey(side)).AddRange(holes);
            var outer = new Mesh();
            PlateBuilder.AddLongPlate(outer, s - Lp / 2, s + Lp / 2, path, _ => -W / 2, _ => W / 2, _ => tHi, _ => tHi + d.TopOuterThickness, holes, map);
            _parts.Add(new SteelPart { Mark = $"{asm}-SP{pn++}", Category = PartCategory.SplicePlate, Assembly = asm, Mesh = outer, Length = Lp,
                Description = $"Top flange splice PL {In(d.TopOuterThickness)} x {In(W)} x {In(Lp)} (outside)" });
            foreach (int k in new[] { -1, 1 })
            {
                double wa = k < 0 ? -W / 2 : W / 2 - d.TopInnerWidth, wb = k < 0 ? -W / 2 + d.TopInnerWidth : W / 2;
                var inner = new Mesh();
                PlateBuilder.AddLongPlate(inner, s - Lp / 2, s + Lp / 2, path, _ => wa, _ => wb, _ => -d.TopInnerThickness, _ => 0,
                    holes.Where(h => h.Center.Y > wa && h.Center.Y < wb).ToList(), map);
                _parts.Add(new SteelPart { Mark = $"{asm}-SP{pn++}", Category = PartCategory.SplicePlate, Assembly = asm, Mesh = inner, Length = Lp,
                    Description = $"Top flange splice PL {In(d.TopInnerThickness)} x {In(d.TopInnerWidth)} x {In(Lp)} (inside)" });
            }
            if (filler)
            {
                double fa = thinSide < 0 ? s - gap / 2 - ov!.FillerLength : s + gap / 2, fb = fa + ov!.FillerLength;
                var fm = new Mesh();
                PlateBuilder.AddLongPlate(fm, fa, fb, path, _ => -ov.FillerWidth / 2, _ => ov.FillerWidth / 2, _ => tLo, _ => tLo + ov.FillerThickness,
                    holes.Where(h => h.Center.X > fa && h.Center.X < fb).ToList(), map);
                _parts.Add(new SteelPart { Mark = $"{asm}-FP{(side < 0 ? 1 : 2)}", Category = PartCategory.FillerPlate, Assembly = asm, Mesh = fm, Length = ov.FillerLength,
                    Description = $"Filler PL {In(ov.FillerThickness)} x {In(ov.FillerWidth)} x {In(ov.FillerLength)}" });
            }
            foreach (var h in holes)
            {
                var f = TF(gi, h.Center.X);
                AddBolt(map(h.Center.X, h.Center.Y, tHi + d.TopOuterThickness), -f.Up, d.TopOuterThickness + tHi + d.TopInnerThickness, asm);
                bolts++;
            }
        }

        // ---- webs
        double twL = TubPlate(gi, GirderPlate.Web, sl).T, twR = TubPlate(gi, GirderPlate.Web, sr).T, tw = Math.Max(twL, twR);
        if (Math.Abs(twL - twR) > 1e-4) Warn($"{asm}: webs {In(twL)} / {In(twR)} differ (no web filler modeled).");
        double L = TubSection.WebSlopeLength(t, D), Lc = L / 2, H = d.WebPlateHeight, tp = d.WebPlateThickness;
        if (H > L) Warn($"{asm}: web splice plates {In(H)} are longer than the web {In(L)} on the slope.");
        foreach (int side in new[] { -1, 1 })
        {
            var map = TubWebMap(gi, side);
            var holes = new List<Hole>();
            for (int c = 0; c < d.WebColumnsPerSide; c++)
                foreach (int dir in new[] { -1, 1 })
                    for (int r = 0; r < d.WebRows; r++)
                        holes.Add(new Hole(new Vec2(s + dir * (d.FirstFromJoint + c * d.WebColumnPitch), Lc + (r - (d.WebRows - 1) / 2.0) * d.WebRowPitch), hd));
            TubHoles(gi, WebKey(side)).AddRange(holes);
            foreach (int face in new[] { 1, -1 })
            {
                var m = new Mesh();
                double n0 = face > 0 ? tw / 2 : -tw / 2 - tp, n1 = n0 + tp;
                PlateBuilder.AddLongPlate(m, s - d.WebPlateWidth / 2, s + d.WebPlateWidth / 2, path, _ => Lc - H / 2, _ => Lc + H / 2, _ => n0, _ => n1, holes, map);
                _parts.Add(new SteelPart { Mark = $"{asm}-SP{pn++}", Category = PartCategory.SplicePlate, Assembly = asm, Mesh = m, Length = H,
                    Description = $"Web splice PL {In(tp)} x {In(d.WebPlateWidth)} x {In(H)} ({(side < 0 ? "left" : "right")} web, {(face > 0 ? "outside" : "inside")})" });
            }
            foreach (var h in holes)
            {
                var f = TF(gi, h.Center.X);
                AddBolt(map(h.Center.X, h.Center.Y, tw / 2 + tp), -Dir(f, TubSection.WebOut(t, side)), 2 * tp + tw, asm);   // heads on the fascia (outside) faces
                bolts++;
            }
        }

        // ---- bottom flange
        var (bw0, bfL) = TubPlate(gi, GirderPlate.BottomFlange, sl);
        var (bw1, bfR) = TubPlate(gi, GirderPlate.BottomFlange, sr);
        if (Math.Abs(bfL - bfR) > 1e-4) Warn($"{asm}: bottom flanges {In(bfL)} / {In(bfR)} differ (no filler modeled).");
        double bf = Math.Max(bfL, bfR);
        if (ov == null) Warn($"UNRESOLVED {asm}: no bottom splice plate thickness given for splice {sp.Number}; {In(new TubSpliceOverride().BottomPlateThickness)} (class default) NOT applied - plates omitted.");
        else
        {
            double tb = ov.BottomPlateThickness, BW = d.BottomPlateWidth, BL = d.BottomPlateLength;
            var map = TubBottomMap(gi);
            var holes = new List<Hole>();
            for (int c = 0; c < d.BottomColumns; c++)
                for (int r = 0; r < d.BottomRowsPerSide; r++)
                    foreach (int dir in new[] { -1, 1 })
                        holes.Add(new Hole(new Vec2(s + dir * (d.FirstFromJoint + r * d.BottomRowPitch), (c - (d.BottomColumns - 1) / 2.0) * d.BottomColumnPitch), hd));
            TubHoles(gi, "BF").AddRange(holes);
            foreach (var (y0, y1, label) in new[] { (-D, -D + tb, "inside"), (-D - bf - tb, -D - bf, "outside") })
            {
                var m = new Mesh();
                PlateBuilder.AddLongPlate(m, s - BL / 2, s + BL / 2, path, _ => -BW / 2, _ => BW / 2, _ => y0, _ => y1, holes, map);
                _parts.Add(new SteelPart { Mark = $"{asm}-SP{pn++}", Category = PartCategory.SplicePlate, Assembly = asm, Mesh = m, Length = BL,
                    Description = $"Bottom flange splice PL {In(tb)} x {In(BW)} x {In(BL)} ({label})" });
            }
            foreach (var h in holes)
            {
                var f = TF(gi, h.Center.X);
                AddBolt(map(h.Center.X, h.Center.Y, -D - bf - tb), f.Up, 2 * tb + bf, asm);   // heads on the fascia (bottom) face
                bolts++;
            }
        }
        _studExclusions[gi].Add((s - Lp / 2, s + Lp / 2));
        _tr.SpliceBolts += bolts;
    }

    // ================================================================== stiffeners, internal frames, top struts

    /// <summary>Stiffener outline (section plane) on web side ±1 at a panel point: top at the top-of-web line, outstand normal to the web.</summary>
    private Vec2[] TubStiffenerOutline(int side, double tw)
    {
        var t = TubDef;
        double D = Sec.WebDepth, yb = -D + t.Internal.StiffenerBottomGap;
        double k = Math.Sqrt(1 + t.WebSlope * t.WebSlope), w = t.Internal.Stiffener.Width;
        double Face(double y) => TubSection.WebFaceX(t, side, y, tw, false);
        double Edge(double y) => Face(y) - side * w * k;
        return new[] { new Vec2(Face(0), 0), new Vec2(Edge(0), 0), new Vec2(Edge(yb), yb), new Vec2(Face(yb), yb) };
    }

    private void TubStiffeners(GirderResult g, TubGirderReport.PanelPoint pp, int index)
    {
        var t = TubDef;
        int gi = g.Index;
        var f = TF(gi, pp.S);
        double tw = TubPlate(gi, GirderPlate.Web, pp.S).T, ts = t.Internal.Stiffener.Thickness;
        string mark = SectionMark(gi, pp.S);
        foreach (int side in new[] { -1, 1 })
        {
            var mesh = new Mesh();
            var holes = _tubStiffHoles.TryGetValue((gi, index, side), out var l) ? l.OrderBy(h => h.Center.X).ThenBy(h => h.Center.Y).ToList() : new List<Hole>();
            PlateBuilder.AddPlanar(mesh, TubStiffenerOutline(side, tw), holes, f.Plane, -ts / 2, ts / 2);
            _parts.Add(new SteelPart { Mark = $"{mark}-TS{pp.Span + 1}.{pp.Point}{(side < 0 ? "L" : "R")}", Category = PartCategory.ConnectionStiffener, Assembly = mark, Mesh = mesh,
                Length = Sec.WebDepth, Description = $"Transverse stiffener {Pl(t.Internal.Stiffener.Width, ts)}, span {pp.Span + 1} panel point {pp.Point}" });
            _tr.Stiffeners++;
        }
    }

    private void TubPanelBracing(GirderResult g, TubGirderReport.PanelPoint pp, int index)
    {
        var t = TubDef;
        var ib = t.Internal;
        int gi = g.Index;
        double D = Sec.WebDepth, T = t.TopWebSpacing / 2;
        var f = TF(gi, pp.S);
        var plane = f.Plane;
        double tw = TubPlate(gi, GirderPlate.Web, pp.S).T, ts = ib.Stiffener.Thickness, tg = ib.GussetThickness;
        string asm = $"{g.Name}-{(pp.Kind == TubPanelKind.CrossFrame ? "K" : "T")}{pp.Span + 1}.{pp.Point}";
        _assemblies.Add(new SteelAssembly { Mark = asm, Kind = AssemblyKind.CrossFrame, Girder = gi,
            Description = pp.Kind == TubPanelKind.CrossFrame ? $"{g.Name} internal cross frame, span {pp.Span + 1} point {pp.Point}" : $"{g.Name} top strut, span {pp.Span + 1} point {pp.Point}" });

        // ---- top strut (WT, stem against the back of the stiffeners; flange coped between the stiffeners)
        var tee = SteelCatalog.FindTee(ib.TopStrut);
        if (tee == null) { Warn($"Top strut {ib.TopStrut} is not in the AISC table."); return; }
        double yTop = -ib.StrutBelowWebTop, d = tee.Depth, tf = tee.FlangeThickness, tst = tee.StemThickness;
        double zs0 = -ts / 2 - tst, zs1 = -ts / 2, zc = (zs0 + zs1) / 2;
        double xStem = Math.Abs(TubSection.WebFaceX(t, 1, yTop - d, tw, false)) - Inch(0.5);
        double xFl = Math.Abs(TubStiffenerOutline(1, tw)[1].X) - Inch(0.5);
        var strut = new Mesh();
        var stemHoles = new List<Hole>();
        double r1 = yTop - Inch(1.75), r2 = yTop - d + Inch(1.75);
        double xi = Math.Abs(TubSection.WebFaceX(t, 1, (r1 + r2) / 2, tw, false));
        foreach (int side in new[] { -1, 1 })
            foreach (double x in new[] { xi - Inch(2), xi - Inch(5) })
                foreach (double y in new[] { r1, r2 })
                {
                    stemHoles.Add(new Hole(new Vec2(side * x, y), HoleDia));
                    TubStiffHoles(gi, index, side).Add(new Hole(new Vec2(side * x, y), HoleDia));
                    AddBolt(plane.ToWorld(side * x, y, zs0), plane.Z, tst + ts, asm);
                }
        var gussetHoles = new List<Hole>();
        if (pp.Kind == TubPanelKind.CrossFrame)
            foreach (double x in new[] { -Inch(3), Inch(3) })
            {
                stemHoles.Add(new Hole(new Vec2(x, r2), HoleDia));
                gussetHoles.Add(new Hole(new Vec2(x, r2), HoleDia));
                AddBolt(plane.ToWorld(x, r2, zs0), plane.Z, tst + tg, asm);
            }
        PlateBuilder.AddPlanar(strut, new[] { new Vec2(-xStem, yTop - d), new Vec2(xStem, yTop - d), new Vec2(xStem, yTop - tf), new Vec2(-xStem, yTop - tf) },
            stemHoles, plane, zs0, zs1);
        MeshBuilder.AddBox(strut, plane, -xFl, xFl, yTop - tf, yTop, zc - tee.FlangeWidth / 2, zc + tee.FlangeWidth / 2);
        _parts.Add(new SteelPart { Mark = $"{asm}-TS", Category = PartCategory.RolledTee, Role = MemberRole.TopChord, Assembly = asm, Mesh = strut,
            Length = 2 * xStem, NominalWeightLbPerFt = tee.WeightPerFootLb, Description = $"Top strut {tee.Name} (flange coped between the stiffeners)" });
        _tr.TopStruts++;
        if (pp.Kind != TubPanelKind.CrossFrame) return;
        _tr.InternalCrossFrames++;

        // ---- centre gusset (outline from the detail's proportions, not dimensioned) and bottom tabs
        var gus = new Mesh();
        var gOut = new[] { new Vec2(-Inch(11), -Inch(18)), new Vec2(Inch(11), -Inch(18)), new Vec2(Inch(11), -Inch(4)), new Vec2(-Inch(11), -Inch(4)) };
        var ang = SteelCatalog.FindAngle(ib.Diagonal);
        if (ang == null) { Warn($"Diagonal {ib.Diagonal} is not in the AISC table."); return; }
        double gA = SteelCatalog.AngleGage(ang.LegA).G;
        var wpT = new Vec2(0, 0);
        foreach (int side in new[] { -1, 1 })
        {
            // tab plate on the bottom flange beside the stiffener (Detail B1)
            var so = TubStiffenerOutline(side, tw);
            double xe = so[2].X;
            var tab = new Mesh();
            MeshBuilder.AddBox(tab, plane, Math.Min(xe, xe - side * ib.TabWidth), Math.Max(xe, xe - side * ib.TabWidth), -D, -D + ib.TabHeight, -ib.TabThickness / 2, ib.TabThickness / 2);
            _parts.Add(new SteelPart { Mark = $"{asm}-TAB{(side < 0 ? 1 : 2)}", Category = PartCategory.Gusset, Assembly = asm, Mesh = tab, Length = ib.TabHeight,
                Description = $"Tab PL {In(ib.TabThickness)} x {In(ib.TabWidth)} x {In(ib.TabHeight)}" });

            // diagonal: bottom work point = web CL x top of bottom flange, top = working point; bolted to the stiffener (bottom) and gusset (top)
            var wpB = new Vec2(TubSection.WebX(t, side, -D), -D);
            var u = (wpT - wpB).Normalized();
            double len = (wpT - wpB).Length;
            double a0 = Inch(2.5), a1 = len - Inch(6.5);
            var v = new Vec2(-u.Y, u.X);
            if (Vec2.Dot(v, new Vec2(0, -1)) > 0) v = -v;   // leg toward the top
            var heel = wpB + u * a0 - v * gA;
            var origin = plane.ToWorld(heel.X, heel.Y, ts / 2);
            var axis = plane.X * u.X + plane.Y * u.Y;
            var dirA = plane.X * v.X + plane.Y * v.Y;
            var holesA = new List<Hole>();
            var bottomB = Enumerable.Range(0, ib.DiagonalBolts).Select(i => Inch(4) + i * ib.BoltPitch).ToList();
            var topB = Enumerable.Range(0, ib.DiagonalBolts).Select(i => len - Inch(8) - i * ib.BoltPitch).ToList();
            foreach (double a in bottomB.Concat(topB))
            {
                holesA.Add(new Hole(new Vec2(a - a0, gA), HoleDia));
                var q = wpB + u * a;
                AddBolt(plane.ToWorld(q.X, q.Y, ts / 2 + ang.Thickness), -plane.Z, ang.Thickness + ts, asm);
                if (bottomB.Contains(a)) TubStiffHoles(gi, index, side).Add(new Hole(q, HoleDia));
                else gussetHoles.Add(new Hole(q, HoleDia));
            }
            var dm = new Mesh();
            RolledBuilder.AddAngle(dm, origin, axis, dirA, plane.Z, a1 - a0, ang.LegA, ang.LegB, ang.Thickness, ang.RootRadius, holesA, Array.Empty<Hole>());
            _parts.Add(new SteelPart { Mark = $"{asm}-D{(side < 0 ? 1 : 2)}", Category = PartCategory.Angle, Role = MemberRole.Diagonal, Assembly = asm, Mesh = dm,
                Length = a1 - a0, NominalWeightLbPerFt = ang.WeightPerFootLb, Description = $"Diagonal {ang.Name}" });
        }
        PlateBuilder.AddPlanar(gus, gOut, gussetHoles, plane, -tg / 2, tg / 2);
        _parts.Add(new SteelPart { Mark = $"{asm}-G", Category = PartCategory.Gusset, Assembly = asm, Mesh = gus, Length = Inch(22),
            Description = $"Gusset PL {In(tg)} (outline not dimensioned on the plans)" });
    }

    // ================================================================== top lateral bracing

    private void TubLaterals(GirderResult g, List<TubGirderReport.PanelPoint> pps)
    {
        var t = TubDef;
        var lb = t.Lateral;
        int gi = g.Index;
        double T = t.TopWebSpacing / 2;
        var tee = SteelCatalog.FindTee(lb.Member);
        if (tee == null) { Warn($"Lateral member {lb.Member} is not in the AISC table."); return; }
        for (int j = 0; j < g.Spans.Count; j++)
        {
            var pts = pps.Where(p => p.Span == j).OrderBy(p => p.S).ToList();
            if (pts.Count == 0) continue;
            string asm = $"{g.Name}-LAT{j + 1}";
            _assemblies.Add(new SteelAssembly { Mark = asm, Kind = AssemblyKind.LateralBracing, Girder = gi, Description = $"{g.Name} top lateral bracing, span {j + 1}" });
            // nodes: support j, panel points, support j + 1; node s at the flange line of the given side
            double NodeS(int node, int side)
            {
                if (node == 0 || node == pts.Count + 1)
                {
                    int sup = node == 0 ? j : j + 1;
                    double sb = BrgS(gi, sup);
                    var fb = TF(gi, sb);
                    var dir = _f.SupportLine(sup).Direction;
                    double dr = Vec2.Dot(dir, fb.PlanRight);
                    double k = Math.Abs(dr) < 1e-9 ? 0 : Vec2.Dot(dir, fb.PlanAhead) / dr;
                    return sb + side * T * fb.Cos * k;
                }
                return pts[node - 1].S;
            }
            double Off(int node)
            {
                if (node == 0 && lb.WorkPointFromSupport.TryGetValue(j, out double o0)) return o0;
                if (node == pts.Count + 1 && lb.WorkPointFromSupport.TryGetValue(j + 1, out double o1)) return o1;
                return lb.WorkPointFromPanelPoint;
            }
            int n = 1;
            for (int i = 0; i <= pts.Count; i++)
            {
                int sA = i % 2 == 0 ? -1 : 1, sB = -sA;
                double a = NodeS(i, sA) + Off(i), b = NodeS(i + 1, sB) - Off(i + 1);
                var PA = TF(gi, a).At(sA * T, 0);
                var PB = TF(gi, b).At(sB * T, 0);
                double trim = Inch(4) / (2 * T);
                var P0 = Vec3.Lerp(PA, PB, trim);
                var P1 = Vec3.Lerp(PA, PB, 1 - trim);
                var e = (P1 - P0).Normalized();
                var up0 = TF(gi, (a + b) / 2).Up;
                var nrm = Vec3.Cross(up0, e).Normalized();
                var up = Vec3.Cross(e, nrm);
                var fr = new Frame3(P0, nrm, up, e);
                double len = (P1 - P0).Length;
                var mesh = new Mesh();
                MeshBuilder.AddBox(mesh, fr, -tee.FlangeWidth / 2, tee.FlangeWidth / 2, -tee.FlangeThickness, 0, 0, len);
                MeshBuilder.AddBox(mesh, fr, -tee.StemThickness / 2, tee.StemThickness / 2, -tee.Depth, -tee.FlangeThickness, 0, len);
                _parts.Add(new SteelPart { Mark = $"{asm}-L{n++}", Category = PartCategory.RolledTee, Role = MemberRole.Lateral, Assembly = asm, Mesh = mesh,
                    Length = len, NominalWeightLbPerFt = tee.WeightPerFootLb, Description = $"Top lateral {tee.Name}" });
                _tr.LateralMembers++;
                // bolts through the girder top flange and the tee flange, 2 lines x n at each end
                foreach (var (end, side, sgn) in new[] { (P0, sA, 1.0), (P1, sB, -1.0) })
                    for (int k = 0; k < lb.BoltsPerLine; k++)
                        foreach (int ln in new[] { -1, 1 })
                        {
                            var pos = end + e * (sgn * (Inch(1.5) + k * lb.BoltPitch)) + nrm * (ln * lb.BoltGage / 2);
                            double sg = sgn > 0 ? a : b;
                            for (int it = 0; it < 3; it++) sg += Vec3.Dot(pos - TF(gi, sg).Origin, TF(gi, sg).Ahead);
                            var fs = TF(gi, sg);
                            double x = Vec3.Dot(pos - fs.Origin, fs.Lateral), w = x - side * T;
                            var (fw, ft) = TubPlate(gi, GirderPlate.TopFlange, sg);
                            if (Math.Abs(w) > fw / 2 - Inch(1.25)) continue;   // off the flange
                            TubHoles(gi, TopKey(side)).Add(new Hole(new Vec2(sg, w), HoleDia));
                            AddBolt(fs.At(x, ft), -fs.Up, ft + tee.FlangeThickness, asm);
                        }
            }
        }
    }

    // ================================================================== external diaphragms

    /// <summary>Plate girder diaphragm between web side +1 of girder gl (at sL) and web side -1 of gr (at sR), in a vertical plane
    /// (dir = null: the line between the two section planes; else the support line direction). Returns the built part count.</summary>
    private void TubDiaphragmBetween(string asm, int gl, double sL, int gr, double sR, Vec2? dir, double yTopWeb, double webDepth,
        double webT, PlateSize flange, bool flangesOutside, string connection, double connLength, int girderBolts, int diaBolts, double pitch,
        double clear, bool studs, string what)
    {
        var t = TubDef;
        var fl = TF(gl, sL);
        var fr = TF(gr, sR);
        double twL = TubPlate(gl, GirderPlate.Web, sL).T, twR = TubPlate(gr, GirderPlate.Web, sR).T;
        double yBot = yTopWeb - webDepth;
        Vec3 Pt(TubFrame f, double x, double y) => dir is { } d ? f.AtInPlane(x, y, d) : f.At(x, y);
        double XL(double y) => TubSection.WebFaceX(t, 1, y, twL, true) + clear;
        double XR(double y) => TubSection.WebFaceX(t, -1, y, twR, true) - clear;
        var aT = Pt(fl, XL(yTopWeb), yTopWeb); var aB = Pt(fl, XL(yBot), yBot);
        var bT = Pt(fr, XR(yTopWeb), yTopWeb); var bB = Pt(fr, XR(yBot), yBot);
        var X = (bT - aT).Normalized();
        var upA = (fl.Up + fr.Up).Normalized();
        var N = Vec3.Cross(X, upA).Normalized();
        var V = Vec3.Cross(N, X);
        var O = aT;
        Vec2 P2(Vec3 p) => new(Vec3.Dot(p - O, X), Vec3.Dot(p - O, V));
        var frame = new Frame3(O, X, V, N);
        var webPoly = new[] { P2(aB), P2(bB), P2(bT), P2(aT) };
        _assemblies.Add(new SteelAssembly { Mark = asm, Kind = AssemblyKind.CrossFrame, Girder = -1, Description = what });

        // connection angles / bent plates at both ends (one each side of the diaphragm web)
        bool bent = connection.StartsWith("BENT ", StringComparison.OrdinalIgnoreCase);
        var ang = SteelCatalog.FindAngle(bent ? connection[5..] : connection);
        var webHoles = new List<Hole>();
        if (ang == null) Warn($"{asm}: connection {connection} is not in the AISC table.");
        else
        {
            double g = SteelCatalog.AngleGage(ang.LegA).G;
            foreach (var (gi, s, side, f, tw) in new[] { (gl, sL, 1, fl, twL), (gr, sR, -1, fr, twR) })
            {
                double Lw = TubSection.WebSlopeLength(t, Sec.WebDepth);
                double yc = (yTopWeb + yBot) / 2;
                double wc = -yc * Math.Sqrt(1 + t.WebSlope * t.WebSlope);   // slope distance of the mid depth
                double w0 = wc - connLength / 2;
                var down = Dir(f, TubSection.WebDown(t, side));
                var outN = Dir(f, TubSection.WebOut(t, side));
                var along = N;   // diaphragm web normal
                foreach (int k in new[] { -1, 1 })
                {
                    var surf0 = Pt(f, TubSection.WebPoint(t, side, w0, tw / 2).X, TubSection.WebPoint(t, side, w0, tw / 2).Y);
                    var heel = surf0 + along * (k * webT / 2);
                    var dA = along * k;
                    var m = new Mesh();
                    var hA = Enumerable.Range(0, girderBolts).Select(i => new Hole(new Vec2((connLength - (girderBolts - 1) * pitch) / 2 + i * pitch, g), HoleDia)).ToList();
                    var hB = Enumerable.Range(0, diaBolts).Select(i => new Hole(new Vec2((connLength - (diaBolts - 1) * pitch) / 2 + i * pitch, g), HoleDia)).ToList();
                    RolledBuilder.AddAngle(m, heel, down, dA, outN, connLength, ang.LegA, ang.LegB, ang.Thickness,
                        bent ? 0 : ang.RootRadius, hA, hB);
                    _parts.Add(new SteelPart { Mark = $"{asm}-C{(side > 0 ? "L" : "R")}{(k < 0 ? 1 : 2)}", Category = PartCategory.Angle, Role = MemberRole.Diaphragm, Assembly = asm,
                        Mesh = m, Length = connLength, NominalWeightLbPerFt = bent ? 0 : ang.WeightPerFootLb,
                        Description = bent ? $"Bent PL {In(ang.Thickness)} x {In(ang.LegA + ang.LegB)} (legs {In(ang.LegA)})" : $"Connection {ang.Name}" });
                    foreach (var h in hA)
                    {
                        var p = heel + down * h.Center.X + dA * h.Center.Y;
                        AddBolt(p + outN * ang.Thickness, -outN, ang.Thickness + tw, asm);
                        // hole in the tub web: (s, slope distance)
                        double sh = s;
                        for (int it = 0; it < 3; it++) sh += Vec3.Dot(p - TF(gi, sh).At(TubSection.WebPoint(t, side, w0 + h.Center.X, tw / 2)), TF(gi, sh).Ahead);
                        TubHoles(gi, WebKey(side)).Add(new Hole(new Vec2(sh, w0 + h.Center.X), HoleDia));
                    }
                    if (k < 0)
                        foreach (var h in hB)
                        {
                            var p = heel + down * h.Center.X + outN * h.Center.Y;
                            AddBolt(p - along * ang.Thickness, along, 2 * ang.Thickness + webT, asm);
                            webHoles.Add(new Hole(P2(p), HoleDia));
                        }
                }
            }
        }
        var web = new Mesh();
        PlateBuilder.AddPlanar(web, webPoly, webHoles, frame, -webT / 2, webT / 2);
        _parts.Add(new SteelPart { Mark = $"{asm}-W", Category = PartCategory.WebPlate, Assembly = asm, Mesh = web, Length = (bT - aT).Length,
            Description = $"Diaphragm web PL {In(webT)} x {In(webDepth)}" });
        // flanges: top above the web top edge, bottom below the bottom edge (or inside when flangesOutside = false)
        foreach (var (p0, p1, top) in new[] { (P2(aT), P2(bT), true), (P2(aB), P2(bB), false) })
        {
            var m = new Mesh();
            double z0 = top ? (flangesOutside ? 0 : -flange.Thickness) : (flangesOutside ? -flange.Thickness : 0);
            Vec3 Map(double u, double nn, double tt) { var q = Vec2.Lerp(p0, p1, (u - p0.X) / (p1.X - p0.X)); return frame.ToWorld(u, q.Y + tt, 0) + N * nn; }
            PlateBuilder.AddPrism(m, new[] { new Vec2(p0.X, -flange.Width / 2), new Vec2(p1.X, -flange.Width / 2), new Vec2(p1.X, flange.Width / 2), new Vec2(p0.X, flange.Width / 2) },
                Array.Empty<Hole>(), (u, nn, tt) => Map(u, nn, tt), _ => z0, _ => z0 + flange.Thickness);
            _parts.Add(new SteelPart { Mark = $"{asm}-{(top ? "TF" : "BF")}", Category = PartCategory.FlangePlate, Assembly = asm, Mesh = m, Length = p1.X - p0.X,
                Description = $"Diaphragm {(top ? "top" : "bottom")} flange {Pl(flange.Width, flange.Thickness)}" });
            if (top && studs)
            {
                double L = p1.X - p0.X;
                int spaces = Math.Max(1, (int)Math.Ceiling((L - Inch(12)) / Inch(12) - 1e-9));
                for (int i = 0; i <= spaces; i++)
                {
                    double u = p0.X + Inch(6) + i * (L - Inch(12)) / spaces;
                    foreach (double nn in new[] { -Inch(1.75), Inch(1.75) })
                    {
                        var o = Map(u, nn, z0 + flange.Thickness);
                        _studs.Add(new StudInstance { Frame = new Frame3(o, X, Vec3.Cross(V, X), V), Length = t.StudLength, Assembly = asm });
                        _tr.Studs++;
                    }
                }
            }
        }
    }

    private void TubExternalDiaphragms(List<List<TubGirderReport.PanelPoint>> panels)
    {
        var t = TubDef;
        var ex = t.External;
        int n = 1;
        foreach (var r in ex.Points)
        {
            int gref = ex.ReferenceGirder;
            var pr = panels[gref].FirstOrDefault(p => p.Span == r.Span && p.Point == r.Point);
            if (pr == null) { Warn($"External diaphragm at span {r.Span + 1} point {r.Point}: G{gref + 1} has no such panel point."); continue; }
            for (int gl = 0; gl + 1 < _f.Girders.Count; gl++)
            {
                int gr = gl + 1;
                // the other girders' matching points = nearest panel point by station
                var pl = gl == gref ? pr : panels[gl].Where(p => p.Span == r.Span).OrderBy(p => Math.Abs(p.Station - pr.Station)).FirstOrDefault();
                var pg = gr == gref ? pr : panels[gr].Where(p => p.Span == r.Span).OrderBy(p => Math.Abs(p.Station - pr.Station)).FirstOrDefault();
                if (pl == null || pg == null) continue;
                if (Math.Abs(pl.Station - pg.Station) > Inch(1))
                    Info($"External diaphragm XD{n}: G{gl + 1} point {pl.Point} and G{gr + 1} point {pg.Point} are {Ft2(Math.Abs(pl.Station - pg.Station))} apart in station (frame not radial).");
                string asm = $"XD{n}";
                TubDiaphragmBetween(asm, gl, pl.S, gr, pg.S, null, -ex.WebTopBelowWebTop, ex.WebDepth, ex.WebThickness, ex.Flange, true, ex.Angle, ex.AngleLength,
                    ex.GirderLegBolts, ex.DiaphragmLegBolts, ex.BoltPitch, ex.EndClearance, false,
                    $"External intermediate diaphragm XD{n}, span {r.Span + 1}, G{gl + 1} point {pl.Point} - G{gr + 1} point {pg.Point}");
                _tr.ExternalDiaphragms++;
                n++;
            }
        }
    }

    // ================================================================== support diaphragms

    private void TubSupportDiaphragm(TubSupportDiaphragmDefinition sd)
    {
        var t = TubDef;
        int j = sd.Support;
        if (j < 0 || j >= _f.Supports.Count) { Warn($"Support diaphragm: support {j + 1} does not exist."); return; }
        var line = _f.SupportLine(j);
        var dir = line.Direction;
        double D = Sec.WebDepth, T = t.TopWebSpacing / 2;
        foreach (var u in sd.Unresolved) Warn($"UNRESOLVED {line.Name} diaphragm: {u}");
        foreach (var g in _f.Girders)
        {
            int gi = g.Index;
            double sb = BrgS(gi, j);
            var f = TF(gi, sb);
            double tw = TubPlate(gi, GirderPlate.Web, sb).T;
            var nrm3 = new Vec3(-dir.Y, dir.X, 0);    // horizontal normal of the support plane
            Vec3 Map(double x, double y, double n) => f.AtInPlane(x, y, dir) + nrm3 * n;
            string asm = $"{g.Name}-SD{j + 1}";
            _assemblies.Add(new SteelAssembly { Mark = asm, Kind = AssemblyKind.CrossFrame, Girder = gi, Description = $"{g.Name} internal support diaphragm at {line.Name}" });
            double XI(int side, double y) => TubSection.WebFaceX(t, side, y, tw, false);
            var outline = new[] { new Vec2(XI(-1, -D), -D), new Vec2(XI(1, -D), -D), new Vec2(XI(1, 0), 0), new Vec2(XI(-1, 0), 0) };
            var openings = new List<Vec2[]>();
            if (sd.AccessOpeningWidth > 0)
                openings.Add(PlateBuilder.RoundedRectangle(new Vec2(0, -D + sd.AccessOpeningBottom + sd.AccessOpeningHeight / 2), sd.AccessOpeningWidth, sd.AccessOpeningHeight, sd.AccessOpeningRadius));
            var web = new Mesh();
            PlateBuilder.AddMappedWithOpenings(web, outline, Array.Empty<Hole>(), openings, Map, -sd.InteriorWebThickness / 2, sd.InteriorWebThickness / 2);
            _parts.Add(new SteelPart { Mark = $"{asm}-W", Category = PartCategory.WebPlate, Assembly = asm, Mesh = web, Length = 2 * T,
                Description = $"Interior support diaphragm web PL {In(sd.InteriorWebThickness)}" + (openings.Count > 0 ? $", access opening {In(sd.AccessOpeningWidth)} x {In(sd.AccessOpeningHeight)} R {In(sd.AccessOpeningRadius)}" : "") });
            if (sd.InteriorTopFlange.Width > 0)
            {
                var (fw, _) = TubPlate(gi, GirderPlate.TopFlange, sb);
                double xa = -(T - fw / 2), xb = T - fw / 2;
                var m = new Mesh();
                PlateBuilder.AddPrism(m, new[] { new Vec2(xa, -sd.InteriorTopFlange.Width / 2), new Vec2(xb, -sd.InteriorTopFlange.Width / 2), new Vec2(xb, sd.InteriorTopFlange.Width / 2), new Vec2(xa, sd.InteriorTopFlange.Width / 2) },
                    Array.Empty<Hole>(), (x, n, y) => Map(x, y, n), _ => 0, _ => sd.InteriorTopFlange.Thickness);
                _parts.Add(new SteelPart { Mark = $"{asm}-TF", Category = PartCategory.FlangePlate, Assembly = asm, Mesh = m, Length = xb - xa,
                    Description = $"Interior support diaphragm top flange {Pl(sd.InteriorTopFlange.Width, sd.InteriorTopFlange.Thickness)}" });
            }
            // bearing stiffeners, near and far side, normal to the bottom flange
            int no = 1;
            foreach (double xc in new[] { -sd.BearingStiffenerSpacing / 2, sd.BearingStiffenerSpacing / 2 })
                foreach (int face in new[] { -1, 1 })
                {
                    var m = new Mesh();
                    double n0 = face > 0 ? sd.InteriorWebThickness / 2 : -sd.InteriorWebThickness / 2 - sd.BearingStiffener.Width, n1 = n0 + sd.BearingStiffener.Width;
                    PlateBuilder.AddPrism(m, new[] { new Vec2(n0, -D), new Vec2(n1, -D), new Vec2(n1, 0), new Vec2(n0, 0) }, Array.Empty<Hole>(),
                        (n, y, x) => Map(xc + x, y, n), _ => -sd.BearingStiffener.Thickness / 2, _ => sd.BearingStiffener.Thickness / 2);
                    _parts.Add(new SteelPart { Mark = $"{asm}-BS{no++}", Category = PartCategory.BearingStiffener, Assembly = asm, Mesh = m, Length = D,
                        Description = $"Bearing stiffener {Pl(sd.BearingStiffener.Width, sd.BearingStiffener.Thickness)}" });
                }
            _tr.Stiffeners += 4;
        }
        for (int gl = 0; gl + 1 < _f.Girders.Count; gl++)
        {
            string asm = $"XSD{j + 1}-{gl + 1}";
            var fl = sd.ExteriorFlange;
            TubDiaphragmBetween(asm, gl, BrgS(gl, j), gl + 1, BrgS(gl + 1, j), dir, -fl.Thickness, D - 2 * fl.Thickness, sd.ExteriorWebThickness, fl, true,
                (sd.Connection == TubDiaphragmConnection.BentPlates ? "BENT " : "") + sd.ConnectionShape, sd.ConnectionLength, sd.GirderLegBolts, sd.DiaphragmLegBolts,
                sd.BoltPitch, Inch(0.5), true, $"Exterior support diaphragm at {line.Name}, G{gl + 1} - G{gl + 2}");
        }
        _tr.SupportDiaphragms++;
    }

    // ================================================================== bearings

    private void TubBearings(TubBearingDefinition bd)
    {
        var t = TubDef;
        int j = bd.Support;
        if (j < 0 || j >= _f.Supports.Count) { Warn($"Tub bearing: support {j + 1} does not exist."); return; }
        var line = _f.SupportLine(j);
        var dir = line.Direction;
        var ahead = dir.Left;   // normal to the support line, up-station
        double D = Sec.WebDepth;
        foreach (var g in _f.Girders)
        {
            int gi = g.Index;
            double sb = BrgS(gi, j);
            var f = TF(gi, sb);
            double bf = TubPlate(gi, GirderPlate.BottomFlange, sb).T;
            double grade = g.Path.GradeAt(sb);
            var B = f.At(0, -D - bf);                       // bottom of the bottom flange on the rotated tub CL
            // underside plane of the bottom flange: z(p) = B.z + grade * along-girder + slope * lateral (rotated flange)
            double slope = f.Sin / f.Cos;
            double Zf(Vec2 p) => B.Z + grade * Vec2.Dot(p - B.XY, f.PlanAhead) + slope * Vec2.Dot(p - B.XY, f.PlanRight);
            double zPlateBot = B.Z - bd.PlateThickness;      // plate CL thickness J (bottom level)
            string fix = _def.SupportAt(j).FixityOf(gi).ToString();
            string asm = $"B{j + 1}-{g.Name}";
            _assemblies.Add(new SteelAssembly { Mark = asm, Kind = AssemblyKind.Bearing, Girder = gi, Description = $"{g.Name} bearing {bd.Mark} at {line.Name} ({fix})" });
            // beveled plate: length L normal to the support line, width K along it
            var pm = new Mesh();
            Vec3 PMap(double a, double c, double tt) { var q = B.XY + ahead * a + dir * c; return q.WithZ(zPlateBot + tt * (Zf(q) - zPlateBot)); }
            PlateBuilder.AddPrism(pm, new[] { new Vec2(-bd.PlateLength / 2, -bd.PlateWidth / 2), new Vec2(bd.PlateLength / 2, -bd.PlateWidth / 2),
                new Vec2(bd.PlateLength / 2, bd.PlateWidth / 2), new Vec2(-bd.PlateLength / 2, bd.PlateWidth / 2) }, Array.Empty<Hole>(), PMap, _ => 0, _ => 1);
            // bevels of the plate top along the support line (transverse) and normal to it (longitudinal)
            double M = Zf(B.XY + dir) - Zf(B.XY), N = Zf(B.XY + ahead) - Zf(B.XY);
            _parts.Add(new SteelPart { Mark = $"{asm}-BP", Category = PartCategory.BearingPlate, Assembly = asm, Mesh = pm, Length = bd.PlateLength,
                Description = $"Beveled bearing PL {In(bd.PlateThickness)} at CL x {In(bd.PlateWidth)} x {In(bd.PlateLength)}, bevels {M * 100:F2} % transverse / {N * 100:F2} % longitudinal" });
            // elastomeric pad
            var pad = new Mesh();
            var pf = new Frame3(B.XY.WithZ(zPlateBot), ahead.WithZ(0), dir.WithZ(0), new Vec3(0, 0, 1));
            MeshBuilder.AddBox(pad, pf, -bd.PadLength / 2, bd.PadLength / 2, -bd.PadWidth / 2, bd.PadWidth / 2, -bd.PadHeight, 0);
            _parts.Add(new SteelPart { Mark = $"{asm}-PAD", Category = PartCategory.BearingPad, Assembly = asm, Mesh = pad, Length = bd.PadLength, Material = "Elastomer (laminated)",
                Description = $"Elastomeric pad {In(bd.PadLength)} x {In(bd.PadWidth)} x {In(bd.PadHeight)} ({bd.InteriorLayers} x {In(bd.InteriorLayer)} interior, {bd.Laminates} x 1/8\" laminates, {In(bd.ExteriorLayer)} exterior)" });
            double seat = zPlateBot - bd.PadHeight;
            // anchor bolts through the bottom flange holes
            foreach (double ra in bd.AnchorRows)
                foreach (int sgn in new[] { -1, 1 })
                {
                    var q = B.XY + ahead * ra + dir * (sgn * bd.AnchorAcross);
                    double ztop = Zf(q) + bf / f.Cos;
                    _bolts.Add(new BoltInstance { Frame = new Frame3(q.WithZ(ztop), ahead.WithZ(0), dir.WithZ(0), new Vec3(0, 0, 1)), Grip = ztop - seat, Length = ztop - seat + bd.AnchorEmbedment,
                        Assembly = asm, Kind = BoltKind.Anchor, Diameter = bd.AnchorDiameter, Embedment = bd.AnchorEmbedment });
                }
            var (pt, _, sta) = g.Path.At(sb);
            var so = _f.Alignment.Inverse(B.XY);
            double slab = _def.Deck.FinishedElevation(_f.Alignment, so.Station, so.Offset);
            double hOff = (B.XY - pt.XY).Length;
            _seats.Add(new BearingSeat(line.Name, gi, sta, seat, bd.PlateThickness, "Elastomeric", fix, B.XY, ahead, bd.PadLength, bd.PadWidth));
            _tr.Bearings.Add(new TubGirderReport.BearingRow(line.Name, gi, bd.Mark, seat, slab, slab - seat, bd.PlanH, M, N, bd.PlanTransverseSlope, bd.PlanLongitudinalSlope, hOff, bd.PlanHorizontalOffset));
        }
    }

    // ================================================================== studs

    private void TubStuds(GirderResult g)
    {
        var t = TubDef;
        int gi = g.Index;
        double T = t.TopWebSpacing / 2;
        foreach (var z in t.StudZones.Where(z => z.Girder == gi))
        {
            double a = TubLoc(gi, z.From), b = TubLoc(gi, z.To);
            if (double.IsNaN(a) || double.IsNaN(b) || z.Spaces < 1) { Warn($"Stud zone {g.Name} {z.From}..{z.To}: location not found."); continue; }
            double pitch = (b - a) / z.Spaces;
            if (z.MaxPitch > 0 && pitch > z.MaxPitch + 1e-4)
                Warn($"{g.Name} studs {z.From}..{z.To}: {z.Spaces} spaces give {In(pitch)} > plan max {In(z.MaxPitch)}.");
            for (int i = 0; i <= z.Spaces; i++)
            {
                double s = a + i * pitch;
                var f = TF(gi, s);
                double tf = TubPlate(gi, GirderPlate.TopFlange, s).T;
                string mark = SectionMark(gi, s);
                foreach (int side in new[] { -1, 1 })
                    foreach (double o in t.StudOffsets)
                    {
                        _studs.Add(new StudInstance { Frame = new Frame3(f.At(side * T + o, tf), f.Ahead, Vec3.Cross(f.Up, f.Ahead), f.Up), Length = t.StudLength, Assembly = mark });
                        _tr.Studs++;
                    }
            }
        }
    }
}
