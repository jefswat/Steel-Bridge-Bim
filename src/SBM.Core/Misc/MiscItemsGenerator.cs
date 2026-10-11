using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
using SBM.Core.Substructure;
using SBM.Core.Units;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Misc;

/// <summary>
/// Optional misc items (Ramp G deck drainage, sign supports, crash cushion end posts, lighting) built from <see cref="BridgeDefinition.Misc"/> and
/// the trench-type <see cref="DeckDrainDefinition.Trench"/> drains, AFTER steel, concrete and substructure (they need girders for clearance
/// checks and caps for pier drains). Standing rule: nothing is assumed - what the plans do not give is raised as a warning, never invented.
/// </summary>
public sealed partial class MiscItemsGenerator
{
    private static readonly Vec3 Up = new(0, 0, 1);
    private readonly BridgeFraming _f;
    private readonly SteelModel? _steel;
    private readonly SubstructureModel _sub;
    private readonly UnitSystem _u;
    private readonly Func<Vec2, double?>? _ground;
    private readonly MiscModel _m = new();
    private readonly Alignment _a;
    private readonly BridgeDefinition _def;
    private readonly DeckDefinition _deck;
    private List<(SteelPart part, Box3 box)> _steelIndex = new();

    private MiscItemsGenerator(BridgeFraming f, SteelModel? steel, SubstructureModel sub, UnitSystem u, Func<Vec2, double?>? ground)
    {
        _f = f; _steel = steel; _sub = sub; _u = u; _ground = ground;
        _a = f.Alignment; _def = f.Definition; _deck = _def.Deck;
    }

    /// <summary>Null when the bridge defines no misc items.</summary>
    public static MiscModel? Generate(BridgeFraming f, SteelModel? steel, SubstructureModel sub, UnitSystem u, Func<Vec2, double?>? ground)
    {
        var d = f.Definition;
        bool any = d.Misc != null || d.Drains.Any(x => x.Trench != null);
        if (!any || f.HasErrors) return null;
        var g = new MiscItemsGenerator(f, steel, sub, u, ground);
        if (steel != null) g._steelIndex = MiscMesh.Index(steel.Parts);
        try
        {
            g.BuildDrainage();
            g.BuildPierDrains();
            g.BuildSigns();
            g.BuildEndPosts();
            g.BuildLighting();
        }
        catch (Exception ex)
        {
            g.Warn($"Misc items could not be generated ({ex.Message}) {string.Join(" | ", (ex.StackTrace ?? "").Split('\n').Take(4).Select(l => l.Trim()))}.");
        }
        return g._m;
    }

    // ---------------------------------------------------------------- helpers
    private void Warn(string s) => _m.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, s));
    private void Info(string s) => _m.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info, s));
    private string Sh(double len) => _u.FormatLength(len, LengthKind.Short);
    private string L(double len) => _u.FormatLength(len);

    private void Asm(MiscGroup g, string name, string desc, string cat) => _m.Assemblies.Add(new MiscAssembly(g, name, desc, cat));

    private void Add(MiscGroup g, MiscKind k, string asm, string name, string desc, Mesh mesh, string material, bool placeholder = false, string basis = "",
        string location = "", Mesh? opening = null)
    {
        if (mesh.IsEmpty) return;
        _m.Elements.Add(new MiscElement(g, k, asm, name, desc, mesh, material, placeholder, basis, location, opening));
    }

    private double Ztop(double sta, double off) => _deck.FinishedElevation(_a, sta, off);
    private Vec2 Pt(double sta, double off) => _a.PointAt(sta, off);

    /// <summary>Support framing index by name (supports, then auxiliary supports) or -1.</summary>
    private int SupportIndex(string name)
    {
        for (int i = 0; i < _def.Supports.Count + _def.AuxiliarySupports.Count; i++)
            if (_def.SupportAt(i).Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    /// <summary>Girder reference line (top of web CL, plan tangent, path distance) at an alignment station.</summary>
    private (Vec3 web, Vec2 tan, double s) GirderAt(GirderResult g, double station)
    {
        var p = g.Path;
        int lo = 0, hi = p.Count - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (p.Station[mid] <= station) lo = mid; else hi = mid;
        }
        double ds = p.S[hi] - p.S[lo];
        double t = p.Station[hi] != p.Station[lo] ? (station - p.Station[lo]) / (p.Station[hi] - p.Station[lo]) : 0;
        double s = p.S[lo] + ds * t;
        var (pt, tan, _) = p.At(s);
        return (pt, tan, s);
    }

    /// <summary>Lowest vertex of flange plates within <paramref name="r"/> of a plan point (bottom of steel under it); NaN when none.</summary>
    private double BottomOfSteelNear(Vec2 p, double r)
    {
        double best = double.NaN;
        foreach (var (part, box) in _steelIndex)
        {
            if (part.Category != PartCategory.FlangePlate) continue;
            if (box.Max.X < p.X - r || box.Min.X > p.X + r || box.Max.Y < p.Y - r || box.Min.Y > p.Y + r) continue;
            foreach (var v in part.Mesh.Positions)
                if ((v.XY - p).Length <= r && (double.IsNaN(best) || v.Z < best)) best = v.Z;
        }
        return best;
    }

    private void Record(string drain, string element, Vec3 p0, Vec3 p1, double radius, double station)
    {
        if (_steelIndex.Count == 0) return;
        var (c, against, at) = MiscMesh.Clearance(_steelIndex, p0, p1, radius);
        if (double.IsNaN(c)) { Info($"{drain}: {element} - no steel within 0.75 m."); return; }
        _m.Clearances.Add(new DrainClearance(drain, element, c, against, at.Z, station));
        string msg = $"{drain}: {element} clearance to steel {_u.FormatLength(c, LengthKind.Short)} to {against}";
        if (c <= 0) Warn("CONFLICT " + msg + " (overlap; the drain was not moved).");
        else if (c < In(1)) Warn("TIGHT " + msg + " (under 1\").");
        else Info(msg + ".");
    }

    // ---------------------------------------------------------------- drainage
    private sealed class PipePath
    {
        public required string Name;
        public required List<Vec3> Pts;
        public required double Od;
        public required double Wall;
        public required double[] Stations;
        public double Length => Enumerable.Range(1, Pts.Count - 1).Sum(i => (Pts[i].XY - Pts[i - 1].XY).Length);
        /// <summary>CL elevation at a station (interpolated along the path).</summary>
        public double ZAt(double sta)
        {
            for (int i = 1; i < Pts.Count; i++)
                if (sta <= Stations[i] || i == Pts.Count - 1)
                {
                    double t = Stations[i] != Stations[i - 1] ? (sta - Stations[i - 1]) / (Stations[i] - Stations[i - 1]) : 0;
                    return Pts[i - 1].Z + (Pts[i].Z - Pts[i - 1].Z) * t;
                }
            return Pts[^1].Z;
        }
    }

    private readonly Dictionary<string, PipePath> _collectors = new(StringComparer.OrdinalIgnoreCase);

    private void BuildDrainage()
    {
        foreach (var c in _def.Misc?.Collectors ?? new()) BuildCollector(c);
        int k = 0;
        foreach (var dr in _def.Drains)
        {
            k++;
            if (dr.Trench != null) BuildTrenchDrain(dr, k);
        }
    }

    private void BuildCollector(DrainCollectorDefinition c)
    {
        string name = c.Name;
        if (c.ReferenceGirder < 0 || c.ReferenceGirder >= _f.Girders.Count) { Warn($"{name}: reference girder {c.ReferenceGirder + 1} does not exist."); return; }
        if (c.Points.Count < 2) { Warn($"{name}: needs at least two vertical-from-deck points."); return; }
        var g = _f.Girders[c.ReferenceGirder];
        int j = SupportIndex(c.EndSupport);
        if (j < 0) { Warn($"{name}: end support '{c.EndSupport}' not found."); return; }
        var sl = _f.SupportLine(j);
        double endSta = sl.Bearings.ElementAtOrDefault(c.ReferenceGirder)?.Station ?? sl.Station;
        double s0 = c.StartStation;
        if (endSta <= s0) { Warn($"{name}: end support is not ahead of the start station."); return; }
        var (od, wall) = MiscMesh.PipeSch40(c.NominalDiameter);

        // stations: every ~3 m plus the knots and couplers
        var stas = new SortedSet<double> { s0, endSta };
        for (double s = s0; s < endSta; s += 3) stas.Add(s);
        foreach (var p in c.Points) if (p.Station > s0 && p.Station < endSta) stas.Add(p.Station);
        foreach (var cs in c.CouplerStations) if (cs > s0 && cs < endSta) stas.Add(cs);
        var list = stas.ToList();
        Vec2 Plan(double sta) { var (w, t, _) = GirderAt(g, sta); return w.XY + t.Right * c.OffsetFromGirder; }

        // elevation knots: deck top at the pipe plan position minus the plan's distance to the CL pipe
        var knots = new List<(double sta, double z, double dist)>();
        double acc = 0;
        var cum = new Dictionary<double, double>();
        for (int i = 0; i < list.Count; i++)
        {
            if (i > 0) acc += (Plan(list[i]) - Plan(list[i - 1])).Length;
            cum[list[i]] = acc;
        }
        foreach (var p in c.Points.OrderBy(p => p.Station))
        {
            var pp = Plan(p.Station);
            double off = _a.Inverse(pp).Offset;
            knots.Add((p.Station, Ztop(p.Station, off) - p.DeckTopToCl, 0));
        }
        // implied slope between knots vs the printed slope
        for (int i = 1; i < knots.Count; i++)
        {
            double run = (Plan(knots[i].sta) - Plan(knots[i - 1].sta)).Length;
            double slope = (knots[i - 1].z - knots[i].z) / run;
            if (Math.Abs(slope - c.Slope) > 0.0025)
                Warn($"{name}: the plan's vertical dimensions imply {slope * 100:F2}% between STA {_u.FormatStation(knots[i - 1].sta)} and {_u.FormatStation(knots[i].sta)} (printed {c.Slope * 100:F2}%); the dimensions are modeled.");
            else Info($"{name}: slope between knots {slope * 100:F2}% (printed {c.Slope * 100:F2}%).");
        }
        double ZAtSta(double sta)
        {
            double d = cum.TryGetValue(sta, out var cd) ? cd : 0;
            double d0 = cum[knots[0].sta];
            if (sta <= knots[0].sta) return knots[0].z + (d0 - d) * c.Slope;
            for (int i = 1; i < knots.Count; i++)
                if (sta <= knots[i].sta)
                {
                    double a0 = cum[knots[i - 1].sta], a1 = cum[knots[i].sta];
                    double t = a1 > a0 ? (d - a0) / (a1 - a0) : 0;
                    return knots[i - 1].z + (knots[i].z - knots[i - 1].z) * t;
                }
            var last = knots[^1];
            return last.z - (d - cum[last.sta]) * c.Slope;
        }
        var pts = list.Select(s => Plan(s).WithZ(ZAtSta(s))).ToList();
        var path = new PipePath { Name = name, Pts = pts, Od = od, Wall = wall, Stations = list.ToArray() };
        _collectors[name] = path;

        string asm = name;
        Asm(MiscGroup.Drainage, asm, $"{Sh(c.NominalDiameter)} deck drain collection pipe, {L(path.Length)}, {c.Slope * 100:F2}%", "drain collector");
        var mesh = new Mesh();
        for (int i = 1; i < pts.Count; i++) mesh.Append(MiscMesh.Tube(pts[i - 1], pts[i], od / 2, od / 2 - wall));
        Add(MiscGroup.Drainage, MiscKind.DrainPipe, asm, name + " pipe",
            $"{Sh(c.NominalDiameter)} dia ASTM A53 Gr B Sch 40 pipe (OD {od / 0.0254:F3}\", wall {wall / 0.0254:F3}\"), {L(path.Length)} on the curve parallel to girder {g.Name}, {Sh(c.OffsetFromGirder)} right of its CL; plan chords (fabricator may use linear segments between couplers)",
            mesh, "ASTM A53 Gr B, galvanized");
        // flexible couplers (about 6" wide)
        foreach (var cs in c.CouplerStations.Where(x => x > s0 && x < endSta))
        {
            var pa = Plan(cs - In(3) / 1.0 * 0).WithZ(ZAtSta(cs));
            var (w, t, _) = GirderAt(g, cs);
            var dir = new Vec3(t.X, t.Y, 0);
            Add(MiscGroup.Drainage, MiscKind.PipeFitting, asm, $"{name} flexible coupler STA {_u.FormatStation(cs)}", "Flexible coupler (Dresser Style 38 / Aqualok Superfit / E.J. Prescott), 4 deg deflection, about 6\" wide",
                MiscMesh.Tube(pa - dir * In(3), pa + dir * In(3), od / 2 + In(0.5), od / 2 - wall), "Steel / elastomer", true, "Plans: type and 6\" width only; manufacturer-supplied coupler");
        }
        // hangers 5'-0 max: 3/4" rods from the pipe top to the deck soffit (ceil(L / spacing))
        int nh = (int)Math.Ceiling(path.Length / c.HangerSpacing);
        var hm = new Mesh();
        for (int h = 0; h <= nh; h++)
        {
            double sta = s0 + (endSta - s0) * h / nh;
            var p = Plan(sta);
            double zp = ZAtSta(sta) + od / 2;
            double off = _a.Inverse(p).Offset;
            double zs = Ztop(sta, off) - _deck.Thickness;
            if (zs > zp) hm.Append(MiscMesh.Cylinder(p.WithZ(zp), p.WithZ(zs), In(0.375), 8));
        }
        Add(MiscGroup.Drainage, MiscKind.PipeHanger, asm, name + " hangers", $"{nh + 1} hanger rods 3/4\" dia, spacing {Sh(c.HangerSpacing)} max, pipe top to deck soffit (turnbuckle, eye nut and clamp not modeled)", hm, "Galvanized steel");
        // support angles at Type 1 cross frames are not located on the plan (frame stations come from the framing): not modeled.
        Info($"{name}: L3x3x3/8 support angles on 5/8\" U-bolts at the Type 1 cross frames are not modeled (plan gives no frame stations for the span).");

        for (int i = 1; i < pts.Count; i += 1)
            if (i % 1 == 0 && (i == 1 || i == pts.Count - 1 || i % 5 == 0))
                Record(name, $"pipe STA {_u.FormatStation(list[i - 1])}-{_u.FormatStation(list[i])}", pts[i - 1], pts[i], od / 2, list[i - 1]);
        // full-length minimum
        double bestC = double.MaxValue; string bestA = ""; double bestZ = 0, bestS = 0;
        for (int i = 1; i < pts.Count; i++)
        {
            var (cl, ag, at) = MiscMesh.Clearance(_steelIndex, pts[i - 1], pts[i], od / 2);
            if (!double.IsNaN(cl) && cl < bestC) { bestC = cl; bestA = ag; bestZ = at.Z; bestS = list[i - 1]; }
        }
        if (bestC < double.MaxValue)
        {
            _m.Clearances.Add(new DrainClearance(name, "pipe minimum over its length", bestC, bestA, bestZ, bestS));
            string msg = $"{name}: minimum clearance of the pipe to steel {_u.FormatLength(bestC, LengthKind.Short)} to {bestA}";
            if (bestC <= 0) Warn("CONFLICT " + msg + " (overlap; not moved)."); else Info(msg + ".");
        }
        Warn($"{name}: upstream cleanout location (\"just before drain 22\") and the 45 deg tee / cleanout at each drain are not dimensioned; the pipe starts at STA {_u.FormatStation(s0)}.");
    }

    private void BuildTrenchDrain(DeckDrainDefinition dr, int k)
    {
        var tr = dr.Trench!;
        double sta = dr.Station;
        string label = tr.Label != "" ? tr.Label : k.ToString();
        string asm = $"Drain {label}";
        string who = $"{asm} STA {_u.FormatStation(sta)} {(dr.Left ? "LT" : "RT")}";
        double gfe = tr.GutterFromEdge ?? _deck.EdgeStrip?.Width ?? double.NaN;
        if (double.IsNaN(gfe)) { Warn($"{who}: gutter line distance from the deck edge is not defined (no edge strip): drain not placed."); return; }
        double edge = dr.Left ? _deck.LeftAt(sta) : _deck.RightAt(sta);
        double inb = dr.Left ? 1 : -1;               // inboard direction in offset units
        double gut = edge + inb * gfe;
        double off0 = gut + inb * tr.InletStartFromGutter, off1 = off0 + inb * tr.InletAcrossDeck, offT = gut + inb * tr.TubeFromGutter;
        var p0 = Pt(sta, off0); var p1 = Pt(sta, off1); var pT = Pt(sta, offT);
        var dir = (p1 - p0).Normalized();
        double len = (p1 - p0).Length;
        double z0 = Ztop(sta, off0), z1 = Ztop(sta, off1), zT = Ztop(sta, offT);
        double w = tr.InletAlongRoad + 2 * tr.SidePlate;
        Asm(MiscGroup.Drainage, asm, $"Deck drain {label}: {Sh(tr.InletAcrossDeck)} x {Sh(tr.InletAlongRoad)} trench from {Sh(tr.InletStartFromGutter)} past the gutter line, {Sh(tr.TubeDiameter)} tube {Sh(tr.TubeFromGutter)} from the gutter line", "deck drain");

        // cross slope check against the plan's drain table
        double modelSlope = Math.Abs(z1 - z0) / len;
        if (tr.TableCrossSlope is { } ts && Math.Abs(modelSlope - ts) > 0.0015)
            Warn($"{who}: model cross slope across the trench {modelSlope * 100:F2}% differs from the drain table {ts * 100:F2}%.");

        // trench body (below the grate) and grate panels, top following the deck surface
        double g = tr.GrateThickness, depth = tr.TrenchDepth;
        var body = MiscMesh.VerticalSection(p0, dir, new[] { new Vec2(0, z0 - g), new Vec2(len, z1 - g), new Vec2(len, z1 - depth), new Vec2(0, z0 - depth) }, w / 2);
        var grate = new Mesh();
        for (int i = 0; i < 2; i++)
        {
            double a0 = len * i / 2 + In(0.25), a1 = len * (i + 1) / 2 - In(0.25);
            double za0 = z0 + (z1 - z0) * a0 / len, za1 = z0 + (z1 - z0) * a1 / len;
            grate.Append(MiscMesh.VerticalSection(p0, dir, new[] { new Vec2(a0, za0 - g), new Vec2(a1, za1 - g), new Vec2(a1, za1), new Vec2(a0, za0) }, tr.InletAlongRoad / 2));
        }
        double zSlabBot = zT - _deck.Thickness;
        var open = MiscMesh.VerticalSection(p0, dir, new[] { new Vec2(0, z0 + In(0.5)), new Vec2(len, z1 + In(0.5)), new Vec2(len, z1 - depth), new Vec2(0, z0 - depth) }, w / 2);
        Add(MiscGroup.Drainage, MiscKind.DrainTrench, asm, asm + " trench", $"Trench drain body, 3/8\" plate, {Sh(tr.InletAcrossDeck)} x {Sh(w)} overall, floor {Sh(depth)} below the grate top (plans show 3 1/2\" and 4 1/4\" in different views; the larger is modeled uniformly)",
            body, "ASTM A36 3/8 plate, galvanized", false, "", "", open);
        Add(MiscGroup.Drainage, MiscKind.DrainGrate, asm, asm + " grate", $"Two grate panels 2'-0 x {Sh(tr.InletAlongRoad)} x {Sh(g)} (ferrous casting ASTM A48 Class 35B; opening pattern not modeled)", grate, "ASTM A48 Class 35B");

        // tube
        double zFloor = zT - depth;
        double rOut = tr.TubeDiameter / 2, rIn = rOut - tr.TubeWall;
        bool piped = tr.Collector != "";
        string tubeDesc;
        double zEnd;
        PipePath? coll = null;
        if (piped)
        {
            if (!_collectors.TryGetValue(tr.Collector, out coll)) { Warn($"{who}: collector '{tr.Collector}' not found; tube modeled to a free-fall end is skipped."); return; }
            double zFlange = zSlabBot - In(9);
            var tube = MiscMesh.Tube(pT.WithZ(zFloor), pT.WithZ(zFlange), rOut, rIn);
            Add(MiscGroup.Drainage, MiscKind.DrainTube, asm, asm + " tube", $"{Sh(tr.TubeDiameter)} dia x {Sh(tr.TubeWall)} structural tube, vertical, to the pipe flange 9\" below the deck underside", tube, "ASTM A500 structural tube, galvanized",
                false, "", "", MiscMesh.Cylinder(pT.WithZ(zFloor), pT.WithZ(zSlabBot), rOut));
            Record(who, "tube", pT.WithZ(zFloor), pT.WithZ(zFlange), rOut, sta);
            var (pod, pwall) = MiscMesh.PipeSch40(tr.TubeDiameter);
            // station of the tube on the collector: nearest path station by plan distance
            double zCl = coll.Pts.Select((p, i) => (d: (p.XY - pT).Length, z: p.Z)).OrderBy(x => x.d).First().z;
            double dMin = coll.Pts.Min(p => (p.XY - pT).Length);
            if (dMin > Ft(1)) Warn($"{who}: the tube is {Sh(dMin)} (plan) from the collection pipe CL (nearest pipe node); the drop is modeled straight down at the tube position.");
            zEnd = zCl;
            var drop = MiscMesh.Tube(pT.WithZ(zFlange), pT.WithZ(zCl), pod / 2, pod / 2 - pwall);
            Add(MiscGroup.Drainage, MiscKind.DrainPipe, asm, asm + " drop pipe", $"{Sh(coll.Od)}-class 8\" Sch 40 drop pipe from the flange into the {coll.Name} (45 deg tee geometry not printed: modeled as a straight vertical drop)", drop, "ASTM A53 Gr B, galvanized");
            Record(who, "drop pipe", pT.WithZ(zFlange), pT.WithZ(zCl + coll.Od / 2), pod / 2, sta);
            if (tr.TubeFromGirderCl is { } gc && tr.ReferenceGirder < _f.Girders.Count)
            {
                var (wb, tn, _) = GirderAt(_f.Girders[tr.ReferenceGirder], sta);
                double dist = Vec2.Dot(pT - wb.XY, tn.Right);
                if (Math.Abs(Math.Abs(dist) - gc) > In(1)) Warn($"{who}: tube is {Sh(Math.Abs(dist))} from CL girder {_f.Girders[tr.ReferenceGirder].Name}, the plan says {Sh(gc)}: the gutter-line dimension and the girder dimension disagree.");
                else Info($"{who}: tube {Sh(Math.Abs(dist))} from CL girder {_f.Girders[tr.ReferenceGirder].Name} (plan {Sh(gc)}).");
            }
            tubeDesc = "piped";
        }
        else
        {
            double bot = BottomOfSteelNear(pT, 1.5);
            if (double.IsNaN(bot)) { bot = zSlabBot - _deck.Haunch - _def.Girders.Section.TotalDepth; Warn($"{who}: no girder flange near the tube; bottom of girders taken from the section depth."); }
            zEnd = bot - dr.DropBelowGirders;
            var tube = MiscMesh.Tube(pT.WithZ(zFloor), pT.WithZ(zEnd), rOut, rIn);
            Add(MiscGroup.Drainage, MiscKind.DrainTube, asm, asm + " tube", $"{Sh(tr.TubeDiameter)} dia x {Sh(tr.TubeWall)} structural tube, vertical, free fall, {L(zFloor - zEnd)} long, ends {Sh(dr.DropBelowGirders)} below the bottom of the girders",
                tube, "ASTM A500 structural tube, galvanized", false, "", "", MiscMesh.Cylinder(pT.WithZ(zFloor), pT.WithZ(zSlabBot), rOut));
            Record(who, "tube", pT.WithZ(zFloor), pT.WithZ(zEnd), rOut, sta);
            tubeDesc = "free fall";
            if (dr.SplashBasin)
            {
                double? gz = _ground?.Invoke(pT);
                if (gz is not { } gzv) Warn($"{who}: no terrain under the drain; splash basin omitted.");
                else
                {
                    var basin = new Mesh();
                    MeshBuilder.AddBox(basin, MiscMesh.Plan(pT, dir), -tr.BasinSize / 2, tr.BasinSize / 2, -tr.BasinSize / 2, tr.BasinSize / 2, gzv - tr.BasinDepth, gzv);
                    Add(MiscGroup.Drainage, MiscKind.SplashBasin, asm, asm + " splash basin", $"Splash basin {Sh(tr.BasinSize)} x {Sh(tr.BasinSize)}, erosion stone {Sh(tr.BasinDepth)} deep on engineering fabric, centred under the drain", basin, "Erosion stone on engineering fabric");
                    if (zEnd < gzv) Warn($"{who}: the tube end is below the ground ({L(gzv)}).");
                }
            }
        }
        Info($"{who}: {tubeDesc}; tube centre {Sh(tr.TubeFromGutter)} from the gutter line (offset {L(offT)}), gutter line {L(gut)} (deck edge {L(edge)}).");
    }
}
