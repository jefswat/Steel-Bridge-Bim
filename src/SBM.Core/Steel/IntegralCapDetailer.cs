using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Steel;

/// <summary>What the integral steel cap detailer built and measured (one per cap; <see cref="SteelModel.IntegralCaps"/>).</summary>
public sealed class IntegralCapReport
{
    public required string Mark { get; init; }
    public required string Support { get; init; }
    /// <summary>Slope of the cap (top of bearing chord), rise / run along the support line (right +).</summary>
    public double Slope { get; init; }
    public double PlanSlope { get; init; } = double.NaN;
    /// <summary>Overall cap length (horizontal, end to end) and between the outer CL bearings.</summary>
    public double Length { get; init; }
    public double BearingSpan { get; init; }
    public List<(string Name, double S, double TopOfBearing, double Seat)> Bearings { get; } = new();
    public List<IntegralCapGirderReport> Girders { get; } = new();
    public List<string> Checks { get; } = new();
    public double SteelMassKg { get; set; }
}

public sealed class IntegralCapGirderReport
{
    public required int Girder { get; init; }
    public required string Name { get; init; }
    /// <summary>CL girder on the cap: S along the support line; distance of the CL cap along the girder path.</summary>
    public double S { get; init; }
    public double PathDistance { get; init; }
    /// <summary>Distance from the first CL bearing (horizontal, along the cap).</summary>
    public double FromFirstBearing { get; init; }
    public double GirderTopOfWeb { get; init; }
    public double CapTopOfFlange { get; init; }
    /// <summary>Top of cap top flange to top of girder top flange at the CL cap (plan check "4 1/4 AT CL CAP").</summary>
    public double CapFlangeToGirderFlangeTop { get; init; }
    public double FillMin { get; init; } = double.NaN;
    public double FillMax { get; init; } = double.NaN;
    public double FillAtCl { get; init; } = double.NaN;
    /// <summary>Cap camber interpolated at the girder and its tolerance window; girder dead load deflection at the cap point (plan camber table).</summary>
    public double CapCamber { get; init; } = double.NaN;
    public double CapCamberTolMinus { get; init; }
    public double CapCamberTolPlus { get; init; }
    public double GirderDeflection { get; init; } = double.NaN;
}

/// <summary>
/// OPTIONAL integral steel pier cap (<see cref="IntegralSteelCapDefinition"/>, Iowa Design 1617 pier 2): the twin I-girder cap, its bearings off the
/// girder lines, stiffeners, cover plates, handholes, studs, end screens and the girder framing connections. Conventions (the model is the FINAL shape):
/// cap bottom = chord of the top of bearing elevations, webs plumb, flanges square to the webs; positions along the cap are horizontal S on the
/// support line. The girders are cut in phase 1 (<see cref="FieldSections"/>) and end on their end plates (<see cref="BuildGirder"/>).
/// </summary>
public sealed partial class SteelModelGenerator
{
    /// <summary>Geometry of one cap (<see cref="IntegralCapFrame"/>, shared with the concrete cap haunch) plus the girders framing into it.</summary>
    private sealed class CapGeom : IntegralCapFrame
    {
        public List<(int G, double SC, double Sg)> Girders { get; } = new();
    }

    private sealed record CapCut(int Girder, double SC, double Half, double TopEnd, CapGeom Cap);
    private readonly List<CapGeom> _caps = new();
    private readonly Dictionary<int, List<CapCut>> _capCuts = new();

    private List<CapCut> CapCutsOf(int g) => _capCuts.TryGetValue(g, out var l) ? l : new List<CapCut>();

    /// <summary>Phase 1: cap geometry and the girder cuts (before the field sections are formed).</summary>
    private void SetupIntegralCaps()
    {
        for (int j = 0; j < _def.Supports.Count; j++)
        {
            var cd = _def.Supports[j].IntegralSteelCap;
            if (cd == null) continue;
            var sl = _f.SupportLine(j);
            string src = cd.Source.Length > 0 ? cd.Source : "plan";
            if (cd.BearingOffsets.Count < 2 || cd.BearingTopElevations.Count != cd.BearingOffsets.Count || cd.BearingTopElevations.Any(double.IsNaN))
            {
                Warn(UnresolvedFlag.Text(src, $"{sl.Name} integral steel cap", "it needs two or more CL bearing positions with their top of bearing elevations",
                    "The cap is not built and the girders are not cut there."));
                continue;
            }
            var (o, d, n) = IntegralCapFrame.Axes(_f, j);
            var sb = cd.BearingOffsets.ToList();
            var tob = cd.BearingTopElevations.ToList();
            double m = (tob[^1] - tob[0]) / (sb[^1] - sb[0]);
            var cap = new CapGeom { Support = j, SupName = sl.Name, D = cd, O = o, Dir = d, Nrm = n, M = m, Sb = sb, Tob = tob };
            for (int k = 1; k + 1 < sb.Count; k++)
            {
                double zc = tob[0] + m * (sb[k] - sb[0]);
                if (Math.Abs(zc - tob[k]) > Inch(1.0 / 32))
                    Warn($"{sl.Name} cap: top of bearing {cd.BearingNames.ElementAtOrDefault(k) ?? $"B{k + 1}"} is {In(tob[k] - zc)} off the chord of the outer bearings (reported, the cap follows the chord).");
            }
            var conn = cd.Connection;
            double tw = cd.WebThickness;
            foreach (var g in _f.Girders)
            {
                var br = sl.Bearings[g.Index];
                if (br == null) continue;
                int nSpan = g.Spans.Count;
                double sC = !double.IsNaN(br.PathDistance) ? br.PathDistance : g.DistanceAt(Math.Min(j, nSpan - 1), j == nSpan ? 1 : 0);
                var (p, tg, _) = g.Path.At(sC);
                double ang = Math.Acos(Math.Clamp(Math.Abs(Vec2.Dot(tg.Normalized(), n)), 0, 1)) * 180 / Math.PI;
                if (ang > 0.5) Warn($"{sl.Name} cap: {g.Name} meets the cap {ang:0.##} deg off square; the end plates are built in the cap web planes (girder ends cut square).");
                double half = (cap.MemberCount == 2 ? cd.WebOffset : 0) + tw / 2 + (conn?.EndPlateThickness ?? 0);
                double topEnd = conn != null ? (cap.MemberCount == 2 ? cd.WebOffset : 0) + cd.TopFlange.Width / 2 + conn.TopFlangeEndBeyondCapFlange : half;
                if (!_capCuts.TryGetValue(g.Index, out var l)) _capCuts[g.Index] = l = new List<CapCut>();
                l.Add(new CapCut(g.Index, sC, half, topEnd, cap));
                cap.Girders.Add((g.Index, sC, cap.S(p.XY)));
            }
            _caps.Add(cap);
        }
    }

    private string GirderLabel(int g) => _def.Girders.PlanNames is { } pn && g < pn.Count ? pn[g] : _f.Girders[g].Name;

    private void AddCapBolt(Vec3 head, Vec3 into, Vec3 xAxis, double grip, string asm, double dia, string? material = null) =>
        _bolts.Add(new BoltInstance
        {
            Frame = Frame3.FromXZ(head, Math.Abs(Vec3.Dot(xAxis.Normalized(), into.Normalized())) > 0.99 ? Vec3.Cross(into, new Vec3(0, 0, 1)) : xAxis, into),
            Grip = grip, Length = new BoltGeometry(dia).LengthForGrip(grip), Assembly = asm, Diameter = dia, Material = material,
        });

    private static Vec2[] Ellipse(double cx, double cz, double w, double h, int n = 24) =>
        Enumerable.Range(0, n).Select(i => new Vec2(cx + w / 2 * Math.Cos(2 * Math.PI * i / n), cz + h / 2 * Math.Sin(2 * Math.PI * i / n))).ToArray();

    private static bool PointInPolygon(IReadOnlyList<Vec2> poly, Vec2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y) && p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                inside = !inside;
        return inside;
    }

    /// <summary>Phase 2 (connections): the whole cap with its connections; registers the girder top flange holes and stud exclusions.</summary>
    private void BuildIntegralCap(CapGeom cap)
    {
        var cd = cap.D;
        var conn = cd.Connection;
        string src = cd.Source.Length > 0 ? cd.Source : "plan";
        var fs = cap.Fs;
        double K = cap.K, tbf = cd.BottomFlange.Thickness, ttf = cd.TopFlange.Thickness, tw = cd.WebThickness;
        double x0 = cap.Xc(cap.SE0), x1 = cap.Xc(cap.SE1);
        string asm = cap.Mark;
        var report = new IntegralCapReport
        {
            Mark = asm, Support = cap.SupName, Slope = cap.M, PlanSlope = cd.PlanSlope, Length = cap.SE1 - cap.SE0, BearingSpan = cap.Sb[^1] - cap.Sb[0],
        };
        _assemblies.Add(new SteelAssembly { Mark = asm, Kind = AssemblyKind.PierCap,
            Description = $"Integral steel pier cap, {cap.SupName}, {cap.MemberCount} x I {Pl(cd.TopFlange.Width, cd.TopFlange.Thickness)} / {Pl(cd.WebDepth, tw)} / {Pl(cd.BottomFlange.Width, cd.BottomFlange.Thickness)}, {_u.FormatLength(cap.SE1 - cap.SE0)}" });
        foreach (var u in cd.Unresolved) Warn($"{cap.SupName} cap: {u}");
        if (!double.IsNaN(cd.PlanSlope))
            report.Checks.Add($"Slope: chord of the top of bearing elevations {cap.M * 100:0.000} % vs plan {cd.PlanSlope * 100:0.00} % (difference {(cap.M - cd.PlanSlope) * 100:+0.000;-0.000} %).");
        if (cap.MemberCount == 2 && !double.IsNaN(cd.PlanFlangeGap))
        {
            double gap = cd.WebOffset - cd.TopFlange.Width / 2;
            report.Checks.Add($"Flange gap at the CL cap: {In(gap)} each side (plan {In(cd.PlanFlangeGap)}).");
        }

        int nm = cap.MemberCount;
        var tfHoles = Enumerable.Range(0, nm).Select(_ => new List<Hole>()).ToArray();
        var bfHoles = Enumerable.Range(0, nm).Select(_ => new List<Hole>()).ToArray();
        var webHoles = Enumerable.Range(0, nm).Select(_ => new List<Hole>()).ToArray();
        var webOpen = Enumerable.Range(0, nm).Select(_ => new List<Vec2[]>()).ToArray();
        int MemberAt(double nLocal)
        {
            for (int mm = 0; mm < nm; mm++) if (Math.Abs(nLocal - cap.WebN(mm)) <= cd.TopFlange.Width / 2 + 1e-6) return mm;
            return -1;
        }
        void FlangeHole(bool top, double x, double nLocal, double dia)
        {
            int mm = MemberAt(nLocal);
            if (mm < 0) return;
            (top ? tfHoles : bfHoles)[mm].Add(new Hole(new Vec2(x, nLocal), dia));
        }
        void Part(string a, string mark, PartCategory cat, Mesh mesh, double len, string desc, string? material = null, Assumption? why = null, double weight = 0, double density = 0)
        {
            if (mesh.IsEmpty) return;
            _parts.Add(new SteelPart { Mark = mark, Category = cat, Assembly = a, Mesh = mesh, Length = len, Description = desc, Material = material ?? cd.Material,
                Assumption = why, NominalWeightLbPerFt = weight, Density = density });
        }
        var bg = _model.Bolt;
        double hole = bg.HoleDiameter;

        // ---------------------------------------------------------------- bearings (off the girder lines, at the cap ends)
        var bd = _def.BearingsAt(cap.Support);
        double soleHalf = bd.SolePlateLength / 2;
        for (int k = 0; k < cap.Sb.Count; k++)
        {
            string name = cd.BearingNames.ElementAtOrDefault(k) ?? $"B{k + 1}";
            double seat = BuildCapBearing(cap, k, name, bd);
            report.Bearings.Add((name, cap.Sb[k], cap.Tob[k], seat));
        }

        // ---------------------------------------------------------------- girder connections (end plates, inside plates, top splice over the cap)
        foreach (var (gi, sC, sg) in cap.Girders)
        {
            var g = _f.Girders[gi];
            var path = g.Path;
            string gl = GirderLabel(gi);
            string casm = $"{asm}-C{gl}";
            _assemblies.Add(new SteelAssembly { Mark = casm, Kind = AssemblyKind.PierCapConnection, Girder = gi,
                Description = $"Girder {gl} connection to the integral steel cap, {cap.SupName}" });
            Vec3 GW(double u, double r, double z) => GirderPoint(path, sC + u, r, z);
            double towC = path.At(sC).Item1.Z;
            double capTowAtG = cap.SurfaceZ(sg, cap.ZTow), capTopAtG = cap.SurfaceZ(sg, cap.ZTop);
            double tfB = _top[gi].Thickness(sC - (conn != null ? CapCutsOf(gi).First(c => c.Cap == cap).TopEnd : 0) - 0.02);
            double tfA = _top[gi].Thickness(sC + (conn != null ? CapCutsOf(gi).First(c => c.Cap == cap).TopEnd : 0) + 0.02);
            double tfG = Math.Max(tfB, tfA);
            double fillMin = double.NaN, fillMax = double.NaN, fillCl = double.NaN;
            var d3 = new Vec3(cap.Dir.X, cap.Dir.Y, 0);
            var n3 = cap.Y;
            double wo = nm == 2 ? cd.WebOffset : 0;

            if (conn != null)
            {
                var cut = CapCutsOf(gi).First(c => c.Cap == cap);
                double tEP = conn.EndPlateThickness, tCP = conn.ConnectorPlateThickness;
                var outline = conn.EndPlateOutline.Select(q => new Vec2(q.X, q.Z)).ToList();
                if (outline.Count < 3) Warn(UnresolvedFlag.Text(src, $"{cap.SupName} girder end plate outline", "not given", "End and connector plates not built."));
                var bolts = conn.EndPlateBolts.Select(q => new Vec2(q.X, q.Z)).ToList();
                double epTop = outline.Count > 0 ? outline.Max(q => q.Y) : 0, epBot = outline.Count > 0 ? outline.Min(q => q.Y) : 0;
                if (outline.Count >= 3 && capTowAtG + epTop > capTowAtG + 1e-6) Warn($"{cap.SupName} cap: {gl} end plate reaches above the cap top flange underside.");
                if (outline.Count >= 3 && capTowAtG + epBot < cap.SurfaceZ(sg, tbf) - 1e-6) Warn($"{cap.SupName} cap: {gl} end plate reaches below the cap bottom flange top.");
                foreach (int side in new[] { -1, 1 })
                {
                    if (outline.Count < 3) break;
                    int mm = nm == 2 ? (side < 0 ? 0 : 1) : 0;
                    double nFace = side * (wo + tw / 2);
                    // End plate: on the girder (welded to its end), outer face of the cap web; z from the cap top of web at the CL girder.
                    var fe = new Frame3(cap.Plan(sg, nFace).WithZ(capTowAtG), d3, new Vec3(0, 0, 1), n3 * side);
                    var hs = bolts.Select(q => new Hole(q, hole)).ToList();
                    var em = new Mesh();
                    PlateBuilder.AddPlanar(em, outline, hs, fe, 0, tEP);
                    double sEnd = sC + side * (cut.Half + 1e-3);
                    string secMark = SectionMark(gi, side < 0 ? sC - cut.Half - 1e-3 : sC + cut.Half + 1e-3);
                    Part(secMark, $"{secMark}-EP", PartCategory.EndPlate, em, epTop - epBot, $"End {Pl(outline.Max(q => q.X) - outline.Min(q => q.X), tEP)} x {In(epTop - epBot)} (girder to cap, bolted)");
                    // Connector plate on the inner face of the cap web.
                    var fc = new Frame3(cap.Plan(sg, side * (wo - tw / 2)).WithZ(capTowAtG), d3, new Vec3(0, 0, 1), n3 * -side);
                    var cm = new Mesh();
                    PlateBuilder.AddPlanar(cm, outline, hs, fc, 0, tCP);
                    Part(casm, $"{casm}-CP{(side < 0 ? 1 : 2)}", PartCategory.ConnectorPlate, cm, epTop - epBot, $"Connector {Pl(outline.Max(q => q.X) - outline.Min(q => q.X), tCP)} x {In(epTop - epBot)} (inside the cap)");
                    // Bolts: end plate + cap web + connector plate; holes in the cap web.
                    foreach (var q in bolts)
                    {
                        var head = fe.ToWorld(q.X, q.Y, tEP);
                        AddCapBolt(head, n3 * -side, d3, tEP + tw + tCP, casm, bg.Diameter);
                        var (lx, _, lz) = cap.Local(fe.ToWorld(q.X, q.Y, 0));
                        webHoles[mm].Add(new Hole(new Vec2(lx, lz), hole));
                    }
                }
                // Girder connection diaphragm plate (girder web plane) and the mill-to-bear plate at the bottom flange level, between the connector plates.
                double nIn = wo - tw / 2 - tCP;
                double zBpTop = double.NaN, zBpBot = double.NaN;   // relative to the cap top of web at the girder
                if (conn.BottomPlate is { } bp && nm == 2)
                {
                    double dg = Sec.WebDepth;
                    var bm = new Mesh();
                    Func<double, double, double, Vec3> map = (nn, x, tt) =>
                    {
                        var p = cap.Plan(sg + x, nn);
                        double z = path.At(sC + nn).Item1.Z - dg - tt;   // top on the girder bottom flange top, following the girder grade ("match slope")
                        return p.WithZ(z);
                    };
                    PlateBuilder.AddPrism(bm, new[] { new Vec2(-nIn, -bp.Width / 2), new Vec2(nIn, -bp.Width / 2), new Vec2(nIn, bp.Width / 2), new Vec2(-nIn, bp.Width / 2) },
                        Array.Empty<Hole>(), map, _ => 0, _ => bp.Thickness);
                    Part(casm, $"{casm}-BP", PartCategory.ContinuityPlate, bm, 2 * nIn, $"{Pl(bp.Width, bp.Thickness)} x {In(2 * nIn)} (girder bottom flange level, mill to bear)");
                    zBpTop = towC - dg - capTowAtG; zBpBot = zBpTop - bp.Thickness;
                }
                if (conn.DiaphragmPlateThickness > 0 && outline.Count >= 3 && nm == 2)
                {
                    double td = conn.DiaphragmPlateThickness;
                    var fd = new Frame3(cap.Plan(sg, 0).WithZ(capTowAtG), n3, new Vec3(0, 0, 1), d3);
                    var pieces = new List<(double z0, double z1)>();
                    if (double.IsNaN(zBpTop)) pieces.Add((epBot, epTop));
                    else { if (epTop > zBpTop + 1e-4) pieces.Add((zBpTop, epTop)); if (zBpBot > epBot + 1e-4) pieces.Add((epBot, zBpBot)); }
                    int pn = 1;
                    foreach (var (z0, z1) in pieces)
                    {
                        var dm = new Mesh();
                        MeshBuilder.AddBox(dm, fd, -nIn, nIn, z0, z1, -td / 2, td / 2);
                        Part(casm, $"{casm}-DP{pn++}", PartCategory.DiaphragmPlate, dm, z1 - z0, $"Girder connection diaphragm {Pl(2 * nIn, td)} x {In(z1 - z0)} (girder web plane, inside the cap)");
                    }
                }

                // ---- top flange splice over the cap
                var cols = conn.SpliceBoltColumns.Where(c => c > 0).OrderBy(c => c).ToList();
                if (cols.Count == 0) Warn(UnresolvedFlag.Text(src, $"{cap.SupName} top flange splice bolt columns", "not given", "Splice plates not built."));
                else
                {
                    double uEnd = cols[^1] + conn.SplicePlateEndDistance;
                    double wO = conn.OuterSplicePlate.Width, tO = conn.OuterSplicePlate.Thickness;
                    double wI = conn.InnerSplicePlate.Width, tI = conn.InnerSplicePlate.Thickness, rI = conn.InnerPlateFromWeb;
                    if (Math.Abs(tfB - tfA) > 1e-4) Warn($"{cap.SupName} cap: {gl} top flanges differ ({In(tfB)} back, {In(tfA)} ahead); the outer splice plate sits on the thicker - no filler modeled for the difference.");
                    Func<double, double, double, Vec3> gmap = (s, r, z) => GirderPoint(path, s, r, z);
                    var allU = cols.Select(c => -c).Concat(cols).OrderBy(u => u).ToList();
                    var rows = conn.SpliceBoltRows;
                    bool OverCap(double u) => Math.Abs(u) < cut.TopEnd;
                    var spHoles = allU.SelectMany(u => rows.Select(r => new Hole(new Vec2(sC + u, r), hole))).ToList();
                    var om = new Mesh();
                    LongPlate(om, path, sC - uEnd, sC + uEnd, path.S, Array.Empty<double>(), _ => -wO / 2, _ => wO / 2, _ => tfG, _ => tfG + tO, spHoles, gmap, false, "splice plate");
                    Part(casm, $"{casm}-SP1", PartCategory.SplicePlate, om, 2 * uEnd, $"Outside top flange splice {Pl(wO, tO)} x {In(2 * uEnd)} (over the cap)");
                    int ip = 2;
                    foreach (int sd in new[] { -1, 1 })
                    {
                        double r0 = sd < 0 ? -rI - wI : rI, r1 = sd < 0 ? -rI : rI + wI;
                        var im = new Mesh();
                        LongPlate(im, path, sC - uEnd, sC + uEnd, path.S, Array.Empty<double>(), _ => r0, _ => r1, _ => -tI, _ => 0,
                            spHoles.Where(h => h.Center.Y > r0 && h.Center.Y < r1).ToList(), gmap, false, "splice plate");
                        Part(casm, $"{casm}-SP{ip++}", PartCategory.SplicePlate, im, 2 * uEnd, $"Inside top flange splice {Pl(wI, tI)} x {In(2 * uEnd)} (over the cap)");
                    }
                    // Filler at the flange level between the girder top flange ends.
                    var fm = new Mesh();
                    LongPlate(fm, path, sC - cut.TopEnd, sC + cut.TopEnd, path.S, Array.Empty<double>(), _ => -wO / 2, _ => wO / 2, _ => 0, _ => tfG,
                        spHoles.Where(h => OverCap(h.Center.X - sC)).ToList(), gmap, false, "filler plate");
                    Part(casm, $"{casm}-FL", PartCategory.FillerPlate, fm, 2 * cut.TopEnd, $"Filler {Pl(wO, tfG)} x {In(2 * cut.TopEnd)} (girder flange level over the cap)", cd.SecondaryMaterial);
                    // TAPERED fills under the inside plates on the cap top flange: top = inside plate underside (girder grade, level across the girder),
                    // bottom = cap top flange (cap slope along the cap) - tapered in two directions.
                    double uF = wo + cd.TopFlange.Width / 2;
                    int fn = 1;
                    foreach (int sd in new[] { -1, 1 })
                    {
                        double r0 = sd < 0 ? -rI - wI : rI, r1 = sd < 0 ? -rI : rI + wI;
                        double Zt(double u, double r) => GW(u, r, 0).Z - tI;
                        double Zb(double u, double r) => cap.SurfaceZ(cap.S(GW(u, r, 0).XY), cap.ZTop);
                        foreach (var (u, r) in new[] { (-uF, r0), (uF, r0), (uF, r1), (-uF, r1), (0.0, (r0 + r1) / 2) })
                        {
                            double th = Zt(u, r) - Zb(u, r);
                            fillMin = double.IsNaN(fillMin) ? th : Math.Min(fillMin, th);
                            fillMax = double.IsNaN(fillMax) ? th : Math.Max(fillMax, th);
                        }
                        Func<double, double, double, Vec3> tmap = (u, r, tt) =>
                        {
                            var p = GW(u, r, 0);
                            double zb = Zb(u, r), zt = Zt(u, r);
                            return p.XY.WithZ(zb + tt * (zt - zb));
                        };
                        var fh = spHoles.Where(h => OverCap(h.Center.X - sC) && h.Center.Y > r0 && h.Center.Y < r1 && Math.Abs(h.Center.X - sC) < uF)
                            .Select(h => new Hole(new Vec2(h.Center.X - sC, h.Center.Y), h.Diameter)).ToList();
                        var tm = new Mesh();
                        PlateBuilder.AddPrism(tm, new[] { new Vec2(-uF, r0), new Vec2(uF, r0), new Vec2(uF, r1), new Vec2(-uF, r1) }, fh, tmap, _ => 0, _ => 1);
                        double ta = Zt(-uF, (r0 + r1) / 2) - Zb(-uF, (r0 + r1) / 2), tb = Zt(uF, (r0 + r1) / 2) - Zb(uF, (r0 + r1) / 2);
                        Part(casm, $"{casm}-TF{fn++}", PartCategory.FillerPlate, tm, 2 * uF,
                            $"Tapered fill {In(wI)} x {In(2 * uF)}, {In(Math.Min(ta, tb))} to {In(Math.Max(ta, tb))} (two-direction taper, cap flange to inside splice plate)", cd.SecondaryMaterial);
                    }
                    fillCl = GW(0, rI + wI / 2, 0).Z - tI - cap.SurfaceZ(cap.S(GW(0, rI + wI / 2, 0).XY), cap.ZTop);
                    if (fillMin < 0) Warn($"CONFLICT (plan vs model) {cap.SupName} cap: {gl} inside splice plates are {In(-fillMin)} BELOW the cap top flange (tapered fill negative) - check the girder / cap elevations.");
                    // Bolts: over the girder flange (outer + flange + inner) and over the cap (outer + filler + inner + tapered fill + cap flange).
                    foreach (double u in allU)
                        foreach (double r in rows)
                        {
                            var head = GW(u, r, tfG + tO);
                            var tng = path.At(sC + u).Item2;
                            var tx = new Vec3(tng.X, tng.Y, 0);
                            double grip = tO + tfG + tI;
                            if (OverCap(u))
                            {
                                bool onFill = Math.Abs(u) < uF;
                                var bottomPt = GW(u, r, 0);
                                double zCapTop = cap.SurfaceZ(cap.S(bottomPt.XY), cap.ZTop);
                                grip = tO + tfG + (bottomPt.Z - zCapTop) + (onFill ? ttf * K : 0);
                                if (onFill)
                                {
                                    var (lx, ly, _) = cap.Local(bottomPt.XY.WithZ(zCapTop));
                                    FlangeHole(true, lx, ly, hole);
                                }
                            }
                            else _holes[gi].Top.Add(new Hole(new Vec2(sC + u, r), hole));
                            AddCapBolt(head, new Vec3(0, 0, -1), tx, grip, casm, bg.Diameter);
                        }
                    // Studs on the outside splice plate.
                    foreach (double c in conn.SpliceStudColumns.Where(c => c > 0))
                        foreach (int sd in new[] { -1, 1 })
                            foreach (double r in conn.SpliceStudRows)
                            {
                                double u = sd * c;
                                var p = GW(u, r, tfG + tO);
                                var tng = path.At(sC + u).Item2;
                                var tx = new Vec3(tng.X, tng.Y, 0);
                                _studs.Add(new StudInstance { Frame = new Frame3(p, tx, Vec3.Cross(new Vec3(0, 0, 1), tx), new Vec3(0, 0, 1)), Length = conn.SpliceStudLength, Assembly = casm });
                            }
                    _studExclusions[gi].Add((sC - uEnd, sC + uEnd));
                }
            }
            else _studExclusions[gi].Add((sC - CapCutsOf(gi).First(c => c.Cap == cap).Half, sC + CapCutsOf(gi).First(c => c.Cap == cap).Half));

            // Cap camber at the girder vs the girder dead load deflection at the cap point (plan camber table) - task: camber with a deflecting support.
            double fromB = sg - cap.Sb[0];
            var (cc, tm0, tp0) = CapCamberAt(cd, fromB, cap.Sb[^1] - cap.Sb[0]);
            double defl = double.NaN;
            var gc = _def.Girders.Camber.FirstOrDefault(x => x.Girder == gi);
            var key = gc?.Keys.FirstOrDefault(k => k.Support == cap.Support);
            if (gc != null && key != null && gc.Points.FirstOrDefault(p => p.Number == key.Number) is { } cpt)
            {
                defl = cpt.SteelDeflection + cpt.SlabDeflection;
                if (!double.IsNaN(cc))
                {
                    double diff = defl - cc;
                    string msg = $"{cap.SupName} cap: girder {gl} dead load deflection at the cap (plan point {key.Number}: steel {In(cpt.SteelDeflection)} + slab/barrier {In(cpt.SlabDeflection)} = {In(defl)}) "
                        + $"vs the cap camber there {In(cc)} (-{In(tm0)} / +{In(tp0)}): difference {In(diff)}.";
                    if (diff < -tm0 - 1e-6 || diff > tp0 + 1e-6)
                        Warn($"CHECK (camber, deflecting support, {src}): {msg} The girder camber includes this support deflection (chord between the field splices), but the cap camber does not match it within the cap tolerance - confirm with the designer.");
                    else _diag.Enqueue(new Diagnostic(DiagnosticSeverity.Info, msg + " Within the cap camber tolerance; the girder camber includes the support deflection."));
                }
            }
            report.Girders.Add(new IntegralCapGirderReport
            {
                Girder = gi, Name = gl, S = sg, PathDistance = sC, FromFirstBearing = fromB, GirderTopOfWeb = towC, CapTopOfFlange = capTopAtG,
                CapFlangeToGirderFlangeTop = towC + tfG - capTopAtG, FillMin = fillMin, FillMax = fillMax, FillAtCl = fillCl,
                CapCamber = cc, CapCamberTolMinus = tm0, CapCamberTolPlus = tp0, GirderDeflection = defl,
            });
            if (conn != null && !double.IsNaN(conn.PlanCapFlangeToGirderFlangeTop))
                report.Checks.Add($"{gl}: top of cap top flange to top of girder top flange at the CL cap {In(towC + tfG - capTopAtG)} (plan {In(conn.PlanCapFlangeToGirderFlangeTop)}, difference {In(towC + tfG - capTopAtG - conn.PlanCapFlangeToGirderFlangeTop)}).");
        }

        // ---------------------------------------------------------------- handholes (opening in the web, bolted cover outside, backing plates inside)
        if (cd.Handholes.Count > 0)
        {
            var hd = cd.HandholeDetail;
            if (hd == null || hd.CoverOutline.Count < 3) Warn(UnresolvedFlag.Text(src, $"{cap.SupName} handhole detail", "not given", "Handholes not built."));
            else
            {
                var cov = hd.CoverOutline.Select(p => new Vec2(p.X, p.Z)).ToList();
                var hb = new BoltGeometry(hd.BoltDiameter);
                int hn = 0;
                foreach (var (gi, _, sg) in cap.Girders)
                {
                    string gl = GirderLabel(gi);
                    foreach (var h in cd.Handholes)
                    {
                        hn++;
                        int side = h.Web == CapWebSide.Back ? -1 : 1;
                        int mm = nm == 2 ? (side < 0 ? 0 : 1) : 0;
                        double s = sg + h.Offset, xh = cap.Xc(s), zh = cap.ZTow - h.Depth;
                        webOpen[mm].Add(Ellipse(xh, zh, hd.OpeningWidth, hd.OpeningHeight));
                        double wo = nm == 2 ? cd.WebOffset : 0;
                        var fo = new Frame3(fs.ToWorld(xh, side * (wo + tw / 2), zh), cap.X, cap.Z, cap.Y * side);
                        var fi = new Frame3(fs.ToWorld(xh, side * (wo - tw / 2), zh), cap.X, cap.Z, cap.Y * -side);
                        var bolts = hd.Bolts.Select(p => new Vec2(p.X, p.Z)).ToList();
                        var cm = new Mesh();
                        PlateBuilder.AddPlanar(cm, cov, bolts.Select(q => new Hole(q, hb.HoleDiameter)).ToList(), fo, 0, hd.CoverThickness);
                        string hm = $"{asm}-HH{hn}";
                        Part(asm, $"{hm}-COV", PartCategory.HandholeCover, cm, cov.Max(q => q.Y) - cov.Min(q => q.Y),
                            $"Handhole cover {Pl(cov.Max(q => q.X) - cov.Min(q => q.X), hd.CoverThickness)} x {In(cov.Max(q => q.Y) - cov.Min(q => q.Y))}, {(side < 0 ? "back" : "ahead")} web at {gl} {In(h.Offset)}", cd.SecondaryMaterial);
                        int bn = 1;
                        foreach (var bo in hd.BackingOutlines)
                        {
                            var bpoly = bo.Select(p => new Vec2(p.X, p.Z)).ToList();
                            if (bpoly.Count < 3) continue;
                            var bm = new Mesh();
                            PlateBuilder.AddPlanar(bm, bpoly, bolts.Where(q => PointInPolygon(bpoly, q)).Select(q => new Hole(q, hb.HoleDiameter)).ToList(), fi, 0, hd.BackingThickness);
                            Part(asm, $"{hm}-BK{bn++}", PartCategory.HandholeBacking, bm, bpoly.Max(q => q.Y) - bpoly.Min(q => q.Y),
                                $"Handhole backing {Pl(bpoly.Max(q => q.X) - bpoly.Min(q => q.X), hd.BackingThickness)} x {In(bpoly.Max(q => q.Y) - bpoly.Min(q => q.Y))} (resin bonded)", cd.SecondaryMaterial);
                        }
                        foreach (var q in bolts)
                        {
                            AddCapBolt(fo.ToWorld(q.X, q.Y, hd.CoverThickness), cap.Y * -side, cap.X, hd.CoverThickness + tw + hd.BackingThickness, asm, hd.BoltDiameter);
                            webHoles[mm].Add(new Hole(new Vec2(xh + q.X, zh + q.Y), hb.HoleDiameter));
                        }
                        // clash with the end plate of the girder on the same web face
                        if (conn is { EndPlateOutline.Count: >= 3 })
                        {
                            var ep = conn.EndPlateOutline.Select(q => new Vec2(q.X, q.Z + cap.ZTow)).ToList();   // (x from CL girder, local z)
                            var cw = cov.Select(q => new Vec2(h.Offset + q.X, zh + q.Y)).ToList();
                            if (cw.Any(q => PointInPolygon(ep, q)) || ep.Any(q => PointInPolygon(cw, q)))
                                Warn($"CONFLICT (plan vs model) {cap.SupName} cap: handhole cover at {gl} {In(h.Offset)} ({(side < 0 ? "back" : "ahead")} web, {In(h.Depth)} below the top of web) overlaps the girder end plate.");
                        }
                    }
                }
            }
        }

        // ---------------------------------------------------------------- cover plates
        int cpn = 0;
        var topCovers = new List<(double s0, double s1)>();
        foreach (var cp in cd.CoverPlates)
        {
            cpn++;
            int k = Math.Clamp(cp.Bearing, 0, cap.Sb.Count - 1);
            int sgn = k == cap.Sb.Count - 1 && cap.Sb.Count > 1 ? -1 : 1;
            double sb = cap.Sb[k];
            var (s0, s1) = cap.CoverRange(cp, soleHalf);
            if (cp.Top) topCovers.Add((s0, s1));
            double xa = cap.Xc(s0), xb = cap.Xc(s1);
            var holes = cp.BoltColumns.SelectMany(c => cp.BoltRows.Select(r => new Hole(new Vec2(cap.Xc(sb + sgn * c), r), hole))).ToList();
            var m = new Mesh();
            PlateBuilder.AddLongPlate(m, xa, xb, Array.Empty<double>(), _ => -cp.Width / 2, _ => cp.Width / 2,
                _ => cp.Top ? cap.ZTop : -cp.Thickness, _ => cp.Top ? cap.ZTop + cp.Thickness : 0, holes, (x, y, z) => fs.ToWorld(x, y, z));
            Assumption? why = null;
            if (cp.Unresolved is { Length: > 0 } un) { Warn($"{cap.SupName} cap: {un}"); why = Assumption.Unresolved(un, cp.FromSoleEdge ? $"start {In(cp.From)} from the modeled sole plate edge ({In(bd.SolePlateLength)} sole plate)" : "as dimensioned"); }
            Part(asm, $"{asm}-CV{cpn}", PartCategory.CoverPlate, m, s1 - s0,
                $"{(cp.Top ? "Top" : "Bottom")} cover {Pl(cp.Width, cp.Thickness)} x {In((s1 - s0) * K)} at {cd.BearingNames.ElementAtOrDefault(k) ?? $"B{k + 1}"}", null, why);
            double tf = cp.Top ? ttf : tbf;
            foreach (var h in holes)
            {
                FlangeHole(cp.Top, h.Center.X, h.Center.Y, hole);
                var head = fs.ToWorld(h.Center.X, h.Center.Y, cp.Top ? cap.ZTop + cp.Thickness : -cp.Thickness);
                AddCapBolt(head, cp.Top ? -cap.Z : cap.Z, cap.X, cp.Thickness + tf, asm, bg.Diameter);
            }
        }

        // ---------------------------------------------------------------- stiffeners (bearing at every CL bearing, jacking inboard of the outer ones): plumb
        if (cd.BearingStiffener is { } bs)
        {
            var at = cap.Sb.Select(s => (s, "bearing")).ToList();
            if (cd.JackingStiffenerOffset > 0) { at.Add((cap.Sb[0] + cd.JackingStiffenerOffset, "jacking")); at.Add((cap.Sb[^1] - cd.JackingStiffenerOffset, "jacking")); }
            double clip = cd.StiffenerClip, ws = bs.Width, ts = bs.Thickness;
            double clear = nm == 2 ? 2 * cd.WebOffset - tw : double.PositiveInfinity;
            if (nm == 2 && 2 * ws > clear - Inch(0.5)) Warn($"{cap.SupName} cap: two {In(ws)} stiffeners do not fit between the webs ({In(clear)} clear).");
            int sn = 0;
            foreach (var (s, kind) in at.OrderBy(a => a.s))
                for (int mm = 0; mm < nm; mm++)
                    foreach (int dir in new[] { -1, 1 })
                    {
                        sn++;
                        double nF = cap.WebN(mm) + dir * tw / 2;
                        double zb = cap.SurfaceZ(s, tbf), zt = cap.SurfaceZ(s, cap.ZTow), hgt = zt - zb;
                        var f = new Frame3(cap.Plan(s, nF).WithZ(zb), cap.Y * dir, new Vec3(0, 0, 1), new Vec3(cap.Dir.X, cap.Dir.Y, 0));
                        var poly = new[] { new Vec2(0, clip), new Vec2(clip, 0), new Vec2(ws, 0), new Vec2(ws, hgt), new Vec2(clip, hgt), new Vec2(0, hgt - clip) };
                        var mesh = new Mesh();
                        PlateBuilder.AddPlanar(mesh, poly, Array.Empty<Hole>(), f, -ts / 2, ts / 2);
                        Part(asm, $"{asm}-{(kind == "bearing" ? "BS" : "JS")}{sn}", PartCategory.BearingStiffener, mesh, hgt,
                            $"{(kind == "bearing" ? "Bearing" : "Jacking")} stiffener {Pl(ws, ts)} x {In(hgt)} (plumb, {In(clip)} clips)");
                        var welds = new Mesh();
                        double top = hgt - Math.Max(clip, cd.StiffenerTopNoWeld);
                        foreach (int face in new[] { -1, 1 })
                        {
                            var off = f.Z * (face * ts / 2);
                            welds.AddWeld(f.ToWorld(0, clip, 0) + off, f.ToWorld(0, top, 0) + off, f.Z * face, f.X, cd.StiffenerWeld);
                        }
                        Part(asm, $"{asm}-{(kind == "bearing" ? "BS" : "JS")}{sn}-W", PartCategory.Weld, welds, top - clip,
                            $"Fillet weld {In(cd.StiffenerWeld)}, {kind} stiffener to web" + (cd.StiffenerTopNoWeld > 0 ? $" ({In(cd.StiffenerTopNoWeld)} no weld at the top)" : ""), "E70XX / E80XX");
                    }
        }

        // ---------------------------------------------------------------- end screens
        if (cd.EndScreens is { } es)
        {
            if (nm != 2) Warn($"{cap.SupName} cap: end screens need the twin cap; not built.");
            else
            {
                var ang = SteelCatalog.FindAngle(es.Angle);
                if (ang == null) Warn($"{cap.SupName} cap: angle {es.Angle} not in the catalog; end screens not built.");
                else
                {
                    var sb2 = new BoltGeometry(es.BoltDiameter);
                    int en = 0;
                    foreach (int end in new[] { 0, 1 })
                    {
                        en++;
                        int si = end == 0 ? 1 : -1;
                        double xs = end == 0 ? cap.Xc(cap.SE0) + es.Inset : cap.Xc(cap.SE1) - es.Inset;
                        double z0 = tbf, z1 = cap.ZTow, ht = z1 - z0;
                        for (int mm = 0; mm < 2; mm++)
                        {
                            int sg = mm == 0 ? -1 : 1;
                            double nw = sg * (cd.WebOffset - tw / 2);
                            double la = ang.LegA, lb = ang.LegB, t = ang.Thickness;
                            var poly = new List<Vec2>
                            {
                                new(xs, nw), new(xs + si * la, nw), new(xs + si * la, nw - sg * t), new(xs + si * t, nw - sg * t), new(xs + si * t, nw - sg * lb), new(xs, nw - sg * lb),
                            };
                            if (MeshBuilder.SignedArea(poly) < 0) poly.Reverse();
                            var am = new Mesh();
                            MeshBuilder.AddExtrusion(am, poly, fs, z0, z1);
                            Part(asm, $"{asm}-ES{en}-L{mm + 1}", PartCategory.Angle, am, ht, $"{ang.Name} x {In(ht)} (end screen frame)", cd.SecondaryMaterial, null, ang.WeightPerFootLb);
                            int nb = Math.Max(2, es.BoltsPerAngle);
                            double gage = ang.GageA.G > 0 && ang.GageA.G < la ? ang.GageA.G : la / 2;
                            for (int b = 0; b < nb; b++)
                            {
                                double zz = z0 + es.BoltEndDistance + (ht - 2 * es.BoltEndDistance) * b / (nb - 1);
                                double xx = xs + si * gage;
                                var head = fs.ToWorld(xx, nw - sg * t, zz);
                                AddCapBolt(head, cap.Y * sg, cap.Z, t + tw, asm, es.BoltDiameter, es.BoltMaterial);
                                webHoles[mm].Add(new Hole(new Vec2(xx, zz), sb2.HoleDiameter));
                            }
                        }
                        // Mesh panel on the outstanding legs (two wire layers' thickness; density = the wire steel of a square mesh).
                        double nIn = cd.WebOffset - tw / 2 - ang.Thickness, tp = 2 * es.WireDiameter;
                        double xa = xs + si * ang.Thickness, xb = xa + si * tp;
                        var pm = new Mesh();
                        MeshBuilder.AddBox(pm, fs, Math.Min(xa, xb), Math.Max(xa, xb), -nIn, nIn, z0, z1);
                        double frac = 2 * (Math.PI * es.WireDiameter * es.WireDiameter / 4) / es.MeshOpening / tp;
                        Part(asm, $"{asm}-ES{en}-MESH", PartCategory.MeshScreen, pm, ht,
                            $"Wire mesh screen {In(es.MeshOpening)} x {In(es.MeshOpening)}, {In(es.WireDiameter)} wire, {In(2 * nIn)} x {In(ht)} (galvanized + painted)", es.MeshMaterial, null, 0, SteelCatalog.Density * frac);
                    }
                }
            }
        }

        // ---------------------------------------------------------------- studs on the cap top (on a top cover plate where there is one)
        if (cd.StudLines.Count > 0 && cd.StudRows.Count > 0)
            foreach (double l in cd.StudLines)
            {
                double s = cap.Sb[0] + l;
                bool onCover = topCovers.Any(c => s >= c.s0 - 1e-6 && s <= c.s1 + 1e-6);
                var rowsS = onCover && cd.CoverPlateStudRows.Count > 0 ? cd.CoverPlateStudRows : cd.StudRows;
                double ztop = cap.ZTop + (onCover ? cd.CoverPlates.Where(c => c.Top).Select(c => c.Thickness).DefaultIfEmpty(0).Max() : 0);
                foreach (double r in rowsS)
                    _studs.Add(new StudInstance { Frame = new Frame3(fs.ToWorld(cap.Xc(s), r, ztop), cap.X, cap.Y, cap.Z), Length = cd.StudLength, Assembly = asm });
            }

        // ---------------------------------------------------------------- the cap members (flanges with holes, webs with openings and holes)
        for (int mm = 0; mm < nm; mm++)
        {
            double wn = cap.WebN(mm);
            string mtag = nm == 2 ? (mm == 0 ? "1" : "2") : "";
            var bfm = new Mesh();
            PlateBuilder.AddLongPlate(bfm, x0, x1, Array.Empty<double>(), _ => wn - cd.BottomFlange.Width / 2, _ => wn + cd.BottomFlange.Width / 2, _ => 0, _ => tbf,
                bfHoles[mm], (x, y, z) => fs.ToWorld(x, y, z));
            Part(asm, $"{asm}-BF{mtag}", PartCategory.FlangePlate, bfm, x1 - x0, $"{Pl(cd.BottomFlange.Width, tbf)} x {In(x1 - x0)} (cap bottom flange)");
            var tfm = new Mesh();
            PlateBuilder.AddLongPlate(tfm, x0, x1, Array.Empty<double>(), _ => wn - cd.TopFlange.Width / 2, _ => wn + cd.TopFlange.Width / 2, _ => cap.ZTow, _ => cap.ZTop,
                tfHoles[mm], (x, y, z) => fs.ToWorld(x, y, z));
            Part(asm, $"{asm}-TF{mtag}", PartCategory.FlangePlate, tfm, x1 - x0, $"{Pl(cd.TopFlange.Width, ttf)} x {In(x1 - x0)} (cap top flange)");
            // web: pieces split midway between girders (one triangulation per piece)
            var fw = new Frame3(fs.Origin, cap.X, cap.Z, cap.Y);
            var cuts = cap.Girders.Select(gg => cap.Xc(gg.Sg)).OrderBy(x => x).ToList();
            var bounds = new List<double> { x0 };
            for (int i = 0; i + 1 < cuts.Count; i++) bounds.Add((cuts[i] + cuts[i + 1]) / 2);
            bounds.Add(x1);
            var wm = new Mesh();
            for (int i = 0; i + 1 < bounds.Count; i++)
            {
                double a = bounds[i], b = bounds[i + 1];
                var ops = webOpen[mm].Where(o => o.Average(q => q.X) > a && o.Average(q => q.X) <= b).ToList();
                var hs = webHoles[mm].Where(h => h.Center.X > a && h.Center.X <= b).ToList();
                var outl = new[] { new Vec2(a, tbf), new Vec2(b, tbf), new Vec2(b, cap.ZTow), new Vec2(a, cap.ZTow) };
                if (ops.Count > 0) PlateBuilder.AddPlanarWithOpenings(wm, outl, hs, ops, fw, wn - tw / 2, wn + tw / 2);
                else PlateBuilder.AddPlanar(wm, outl, hs, fw, wn - tw / 2, wn + tw / 2);
            }
            Part(asm, $"{asm}-W{mtag}", PartCategory.WebPlate, wm, x1 - x0, $"{Pl(cd.WebDepth, tw)} x {In(x1 - x0)} (cap web, {webOpen[mm].Count} handholes)");
        }
        Warn(UnresolvedFlag.Text(src, $"{cap.SupName} cap web-to-flange welds", "continuous submerged arc welds without a size (U3)", "Not modeled as weld solids."));
        if (conn != null) Warn(UnresolvedFlag.Text(src, $"{cap.SupName} girder end plate / connector plate welds", "weld symbols without size or type (U2)", "Not modeled as weld solids."));

        // camber record
        if (cd.Camber.Count > 0)
            report.Checks.Add("Cap camber (from the chord between the bearings, recorded, the model is the final shape): " +
                string.Join(", ", cd.Camber.OrderBy(c => c.Distance).Select(c => $"{_u.FormatLength(c.Distance)}: {In(c.Camber)} (-{In(c.TolMinus)}, +{In(c.TolPlus)})")) + ".");
        foreach (var c in report.Checks) _diag.Enqueue(new Diagnostic(DiagnosticSeverity.Info, $"{cap.SupName} cap: {c}"));
        lock (_capReports) _capReports.Add(report);
    }

    private readonly List<IntegralCapReport> _capReports = new();

    /// <summary>Cap camber (and tolerance window) at a distance from the first bearing: linear between the plan ordinates, 0 at the bearings.</summary>
    private static (double camber, double tolMinus, double tolPlus) CapCamberAt(IntegralSteelCapDefinition cd, double dist, double span)
    {
        if (cd.Camber.Count == 0) return (double.NaN, 0, 0);
        var pts = cd.Camber.OrderBy(c => c.Distance).ToList();
        if (pts[0].Distance > 1e-6) pts.Insert(0, new CapCamberPoint(0, 0, 0, 0));
        if (pts[^1].Distance < span - 1e-6) pts.Add(new CapCamberPoint(span, 0, 0, 0));
        if (dist <= pts[0].Distance) return (pts[0].Camber, pts[0].TolMinus, pts[0].TolPlus);
        for (int i = 0; i + 1 < pts.Count; i++)
            if (dist <= pts[i + 1].Distance)
            {
                double t = (dist - pts[i].Distance) / Math.Max(1e-9, pts[i + 1].Distance - pts[i].Distance);
                double L(double a, double b) => a + (b - a) * t;
                return (L(pts[i].Camber, pts[i + 1].Camber), L(pts[i].TolMinus, pts[i + 1].TolMinus), L(pts[i].TolPlus, pts[i + 1].TolPlus));
            }
        return (pts[^1].Camber, pts[^1].TolMinus, pts[^1].TolPlus);
    }

    /// <summary>Bearing under the cap at CL bearing k: tapered sole plate (top on the cap slope, bottom level), the bridge / support bearing body and
    /// masonry plate (Generic or Disc), anchor bolts; seat with plan location and axis along the cap. Returns the seat elevation.</summary>
    private double BuildCapBearing(CapGeom cap, int k, string name, BearingDefinition bd)
    {
        var cd = cap.D;
        var fix = _def.SupportAt(cap.Support).ResolvedFixity;
        double sb = cap.Sb[k];
        var p = cap.Plan(sb, 0);
        double zbf = cap.Tob[k];
        var tx = new Vec3(cap.Dir.X, cap.Dir.Y, 0);
        var frame = new Frame3(new Vec3(p.X, p.Y, zbf), tx, Up, Vec3.Cross(tx, Up));
        string mark = $"B-{cap.SupName.Replace(" ", "")}-{name.Replace(" ", "").Replace(".", "")}";
        string fixName = fix switch { BearingFixity.Fixed => "fixed", BearingFixity.GuidedExpansion => "guided expansion", _ => "expansion" };
        _assemblies.Add(new SteelAssembly { Mark = mark, Kind = AssemblyKind.Bearing, Description = $"{bd.Type} bearing ({fixName}), {cap.SupName}, {name} (under the integral steel cap)" });
        double grade = cap.M;
        double capW = cap.MemberCount == 2 ? 2 * cd.WebOffset + cd.BottomFlange.Width : cd.BottomFlange.Width;
        double ls = bd.SolePlateLength, ws = Math.Max(bd.Type == BearingType.Disc ? bd.UpperPlateWidth : bd.GenericBodyWidth, capW + 2 * bd.SolePlateProjection);
        double zb = -Math.Abs(grade) * ls / 2 - bd.SolePlateMinThickness;
        double tmax = bd.SolePlateMinThickness + Math.Abs(grade) * ls;
        var sole = new Mesh();
        MeshBuilder.AddExtrusion(sole, new[] { new Vec2(-ls / 2, zb), new Vec2(ls / 2, zb), new Vec2(ls / 2, grade * ls / 2), new Vec2(-ls / 2, -grade * ls / 2) }, frame, -ws / 2, ws / 2);
        _parts.Add(new SteelPart { Mark = $"{mark}-SP", Category = PartCategory.SolePlate, Assembly = mark, Mesh = sole, Length = ls,
            Description = $"Sole {Pl(ws, tmax)} x {In(ls)} (tapered {grade * 100:0.00} % along the cap, min {In(bd.SolePlateMinThickness)})" });
        double seat;
        if (bd.Type == BearingType.Disc) seat = zbf + BuildDisc(bd, mark, frame, zb, fix);
        else
        {
            double hb = Math.Max(0, bd.GenericBodyHeight(grade));
            if (hb < Inch(1)) Warn($"{mark}: sole plate + masonry plate + pad leave {In(hb)} for the bearing within the assumed height {In(bd.GenericHeight)}.");
            var body = new Mesh();
            MeshBuilder.AddBox(body, frame, -bd.GenericBodyLength / 2, bd.GenericBodyLength / 2, zb - hb, zb, -bd.GenericBodyWidth / 2, bd.GenericBodyWidth / 2);
            _parts.Add(new SteelPart { Mark = $"{mark}-BRG", Category = PartCategory.BearingDisc, Assembly = mark, Mesh = body, Length = bd.GenericBodyLength,
                Material = "By bearing manufacturer", Description = $"Bearing ({fixName}, by manufacturer), assumed height {In(bd.GenericHeight)}" });
            seat = zbf + BuildMasonry(bd, mark, frame, zb - hb, fix, 0);
        }
        var (fpL, fpW) = bd.Footprint(fix);
        var so = _f.Alignment.Inverse(p);
        _seats.Add(new BearingSeat(cap.SupName, 2000 + k, so.Station, seat, tmax, bd.Type.ToString(), fixName, p, cap.Dir, fpL, fpW, name));
        return seat;
    }
}
