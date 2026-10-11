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
    private readonly Dictionary<int, ConcurrentBag<(double s0, double s1)>> _studExclusions = new();
    private readonly ConcurrentDictionary<(int girder, long key), ConcurrentBag<(Vec3 c, double d)>> _stiffHoles = new();

    private sealed class GirderHoles
    {
        public readonly ConcurrentBag<Hole> Top = new(), Bottom = new(), Web = new();
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

    public static SteelModel Generate(BridgeFraming framing, UnitSystem units)
    {
        var g = new SteelModelGenerator(framing, units);
        var sw = Stopwatch.StartNew();
        if (!framing.HasErrors && framing.Girders.Count > 0) g.Run();
        g._model.Assemblies.AddRange(g._assemblies.OrderBy(a => a.Kind).ThenBy(a => a.Mark, MarkComparer.Instance));
        g._model.CrossFrameDetails.AddRange(g._cfDetails.OrderBy(c => c.Mark, MarkComparer.Instance));
        g._model.Parts.AddRange(g._parts.OrderBy(p => p.Assembly, MarkComparer.Instance).ThenBy(p => p.Mark, MarkComparer.Instance));
        g._model.Bolts.AddRange(g._bolts.OrderBy(b => b.Assembly, MarkComparer.Instance));
        g._model.Studs.AddRange(g._studs.OrderBy(b => b.Assembly, MarkComparer.Instance));
        g._model.BearingSeats.AddRange(g._seats.OrderBy(s => s.Station).ThenBy(s => s.Girder));
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
        foreach (var g in _f.Girders)
        {
            _sections[g.Index] = FieldSections(g);
            _top[g.Index] = BuildProfile(g, GirderPlate.TopFlange);
            _bot[g.Index] = BuildProfile(g, GirderPlate.BottomFlange);
            _web[g.Index] = BuildProfile(g, GirderPlate.Web);
            _holes[g.Index] = new GirderHoles();
            _studExclusions[g.Index] = new ConcurrentBag<(double, double)>();
        }

        // Phase 2: connections (register holes).
        var conn = new List<Action>();
        foreach (var c in _f.CrossFrames) { var cc = c; conn.Add(() => BuildCrossFrame(cc)); }
        foreach (var sp in _f.Splices) { var s = sp; conn.Add(() => BuildFieldSplice(s)); }
        conn.Add(BuildLateralBracing);
        conn.Add(BuildInspectionAccess);
        foreach (var x in _f.CrossGirders) { var xx = x; conn.Add(() => BuildCrossGirder(xx)); }   // registers bolt holes in girder webs
        Parallel.ForEach(conn, a => a());

        // Phase 3: members.
        var members = new List<Action>();
        foreach (var g in _f.Girders) { var gg = g; members.Add(() => BuildGirder(gg)); members.Add(() => BuildStuds(gg)); }
        foreach (var s in _f.Stiffeners) { var ss = s; members.Add(() => BuildStiffener(ss)); }
        foreach (var b in _f.Bearings) { var bb = b; members.Add(() => BuildBearing(bb)); }
        Parallel.ForEach(members, a => a());
    }

    // ================================================================== formatting helpers

    private string In(double m) => _u.FormatLength(m, LengthKind.Short);
    private string Pl(double w, double t) => _u.IsUS ? $"PL {In(w)} x {In(t)}" : $"PL {w * 1000:0}x{t * 1000:0}";
    private static double Inch(double inches) => UnitSystem.In(inches);

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
        foreach (double c in SpliceJoints(g.Index))
        {
            list.Add((a, c - gap / 2, $"{g.Name}-{k++}"));
            a = c + gap / 2;
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

    private void BuildGirder(GirderResult g)
    {
        var path = g.Path;
        double d = Sec.WebDepth;
        var top = _top[g.Index];
        var bot = _bot[g.Index];
        var web = _web[g.Index];
        var holes = _holes[g.Index];
        var topHoles = holes.Top.ToList();
        var botHoles = holes.Bottom.ToList();
        var webHoles = holes.Web.ToList();
        var breaks = path.S.Concat(top.Breaks).Concat(bot.Breaks).ToList();

        Func<double, double, double, Vec3> map = (s, r, z) => GirderPoint(path, s, r, z);
        Func<double, double, double, Vec3> webMap = (s, z, r) => GirderPoint(path, s, r, z);

        foreach (var (s0, s1, mark) in _sections[g.Index])
        {
            _assemblies.Add(new SteelAssembly { Mark = mark, Kind = AssemblyKind.GirderFieldSection, Girder = g.Index,
                Description = $"{g.Name} field section, {_u.FormatLength(s1 - s0)}" });

            int n = 1;
            foreach (var (a, b, seg) in top.PiecesWithin(s0, s1))
            {
                var mesh = new Mesh();
                var hs = topHoles.Where(h => h.Center.X > a && h.Center.X < b).ToList();
                PlateBuilder.AddLongPlate(mesh, a, b, breaks, s => -top.Width(s) / 2, s => top.Width(s) / 2, _ => 0, top.Thickness, hs, map);
                _parts.Add(new SteelPart { Mark = $"{mark}-TF{n++}", Category = PartCategory.FlangePlate, Assembly = mark, Mesh = mesh,
                    Length = b - a, Description = Pl(seg.Width, seg.Thickness) });
            }
            n = 1;
            foreach (var (a, b, seg) in web.PiecesWithin(s0, s1))
            {
                var mesh = new Mesh();
                var hs = webHoles.Where(h => h.Center.X > a && h.Center.X < b).ToList();
                double tw = seg.Thickness;
                PlateBuilder.AddLongPlate(mesh, a, b, path.S, _ => -d, _ => 0, _ => -tw / 2, _ => tw / 2, hs, webMap);
                _parts.Add(new SteelPart { Mark = $"{mark}-W{n++}", Category = PartCategory.WebPlate, Assembly = mark, Mesh = mesh,
                    Length = b - a, Description = Pl(d, tw) });
            }
            n = 1;
            foreach (var (a, b, seg) in bot.PiecesWithin(s0, s1))
            {
                var mesh = new Mesh();
                var hs = botHoles.Where(h => h.Center.X > a && h.Center.X < b).ToList();
                PlateBuilder.AddLongPlate(mesh, a, b, breaks, s => -bot.Width(s) / 2, s => bot.Width(s) / 2, s => -d - bot.Thickness(s), _ => -d, hs, map);
                _parts.Add(new SteelPart { Mark = $"{mark}-BF{n++}", Category = PartCategory.FlangePlate, Assembly = mark, Mesh = mesh,
                    Length = b - a, Description = Pl(seg.Width, seg.Thickness) });
            }

            // Continuous flange-to-web fillet welds, both sides, top and bottom.
            double aw = _def.Stiffeners.FlangeToWebWeld;
            var welds = new Mesh();
            foreach (var (rs, zs) in new[] { (-1, 0), (1, 0), (-1, 1), (1, 1) })
            {
                double z = zs == 0 ? 0 : -d;
                double dz = zs == 0 ? -aw : aw;
                SweepAlongGirder(welds, path, s0, s1, Array.Empty<double>(), s =>
                {
                    double tw = web.Thickness(s) / 2;
                    return new[] { new Vec2(rs * tw, z), new Vec2(rs * (tw + aw), z), new Vec2(rs * tw, z + dz) };
                });
            }
            _parts.Add(new SteelPart
            {
                Mark = $"{mark}-FW", Category = PartCategory.Weld, Assembly = mark, Mesh = welds, Length = 4 * (s1 - s0), Material = "E70XX",
                Description = $"Fillet weld {In(aw)}, flange to web, both sides",
            });
        }
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

    private readonly ConcurrentDictionary<string, int> _counters = new();
    private int Next(string key) => _counters.AddOrUpdate(key, 1, (_, v) => v + 1);

    private static long SKey(double s) => (long)Math.Round(s * 100); // 1 cm buckets

    /// <summary>Records a bolt hole through the stiffener at girder distance s.</summary>
    private void AddStiffenerHole(int girder, double s, Vec3 center, double dia) =>
        _stiffHoles.GetOrAdd((girder, SKey(s)), _ => new ConcurrentBag<(Vec3, double)>()).Add((center, dia));

    private IEnumerable<(Vec3 c, double d)> StiffenerHoles(int girder, double s)
    {
        long k = SKey(s);
        for (long dk = -1; dk <= 1; dk++)
            if (_stiffHoles.TryGetValue((girder, k + dk), out var bag))
                foreach (var h in bag) yield return h;
    }

    private void BuildStiffener(StiffenerResult s)
    {
        var g = _f.Girders[s.Girder];
        var (p, t, sta) = g.Path.At(s.GirderDistance);
        double d = Sec.WebDepth;
        double tw = _web[s.Girder].Thickness(s.GirderDistance);
        var sd = _def.Stiffeners;
        double c = sd.CornerClip;
        bool weldBottom = s.Kind != StiffenerKind.Intermediate;
        double h = weldBottom ? d : d - sd.IntermediateBottomGap;
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
        double flangeW = Math.Min(_top[s.Girder].Width(s.GirderDistance), _bot[s.Girder].Width(s.GirderDistance));
        if ((xf + w) * sin > flangeW / 2 + 1e-4)
            Warn($"{g.Name}: {label.ToLowerInvariant()} at {_u.FormatStation(sta)} projects beyond the flange edge (skewed {Numerics.RadToDeg(s.SkewAngle):F1}°).");

        // Bolt holes registered by cross frames, converted to plate coordinates.
        var holes = StiffenerHoles(s.Girder, s.GirderDistance)
            .Select(hh => (x: Vec3.Dot(hh.c - p, frame.X), yy: Vec3.Dot(hh.c - p, frame.Y), hh.d)).ToList();

        var mesh = new Mesh();
        var welds = new Mesh();
        foreach (int side in new[] { 1, -1 })
        {
            if (side == 1 && s.Side is not (StiffenerSide.Both or StiffenerSide.Right)) continue;
            if (side == -1 && s.Side is not (StiffenerSide.Both or StiffenerSide.Left)) continue;
            var poly = new List<Vec2> { new(xf, -c), new(xf + c, 0), new(xf + w, 0), new(xf + w, -h) };
            if (weldBottom) { poly.Add(new Vec2(xf + c, -h)); poly.Add(new Vec2(xf, -h + c)); }
            else poly.Add(new Vec2(xf, -h));
            var pts = poly.Select(q => new Vec2(q.X * side, q.Y)).ToList();
            var hs = holes.Where(hh => hh.x * side > xf && hh.x * side < xf + w && hh.yy < 0 && hh.yy > -h)
                .Select(hh => new Hole(new Vec2(hh.x, hh.yy), hh.d)).ToList();
            PlateBuilder.AddPlanar(mesh, pts, hs, frame, -tp / 2, tp / 2);

            double a = sd.WeldSize;
            var along = u * side;
            var tang3 = new Vec3(t.X, t.Y, 0);
            foreach (int face in new[] { 1, -1 })
            {
                var nrm = frame.Z * face;
                var webLeg = Vec3.Dot(tang3, nrm) >= 0 ? tang3 : -tang3;
                var off = nrm * (tp / 2);
                welds.AddWeld(frame.ToWorld(side * xf, -c, 0) + off, frame.ToWorld(side * xf, weldBottom ? -h + c : -h, 0) + off, webLeg, along, a);
                welds.AddWeld(frame.ToWorld(side * (xf + c), 0, 0) + off, frame.ToWorld(side * (xf + w), 0, 0) + off, -frame.Y, nrm, a);
                if (weldBottom)
                    welds.AddWeld(frame.ToWorld(side * (xf + c), -h, 0) + off, frame.ToWorld(side * (xf + w), -h, 0) + off, frame.Y, nrm, a);
            }
        }
        if (mesh.IsEmpty) return;
        string desc = Pl(w, tp) + (s.SkewAngle > Numerics.DegToRad(0.5) ? $", skewed {Numerics.RadToDeg(s.SkewAngle):F1}°" : "")
                      + (s.Side == StiffenerSide.Both ? " (pair)" : "");
        int n = Next(mark + code);
        _parts.Add(new SteelPart { Mark = $"{mark}-{code}{n}", Category = cat, Assembly = mark, Description = desc, Mesh = mesh, Length = h });
        _parts.Add(new SteelPart
        {
            Mark = $"{mark}-{code}{n}-W", Category = PartCategory.Weld, Assembly = mark, Mesh = welds, Material = "E70XX", Length = h,
            Description = $"Fillet weld {In(sd.WeldSize)}, {label.ToLowerInvariant()}",
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
        var boltHoles = _holes[g.Index].Top.ToList();
        double slab = _def.Deck.Thickness, haunch = _def.Deck.Haunch;
        double headR = _model.Bolt.HeadAcrossFlats / Math.Sqrt(3);
        bool coverWarned = false;

        for (double s = clear + sd.Pitch / 2; s < g.Length - clear; s += sd.Pitch)
        {
            if (excl.Any(e => s > e.s0 - clear && s < e.s1 + clear)) continue;
            var (p, t, sta) = path.At(s);
            // Deck cross slope at the girder (finished grade is parallel to the deck bottom).
            var so = align.Inverse(p.XY);
            double e0 = align.ElevationAt(so.Station, so.Offset - 0.05), e1 = align.ElevationAt(so.Station, so.Offset + 0.05);
            double slope = (e1 - e0) / 0.1;
            double tf = top.Thickness(s);
            double halfW = top.Width(s) / 2;
            string mark = SectionMark(g.Index, s);
            var right = new Vec3(t.Right.X, t.Right.Y, 0);
            for (int k = 0; k < sd.RowsAcross; k++)
            {
                double r = (k - (sd.RowsAcross - 1) / 2.0) * sd.TransverseSpacing;
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
                var origin = p + right * r + Up * tf;
                _studs.Add(new StudInstance { Frame = new Frame3(origin, new Vec3(t.X, t.Y, 0), Vec3.Cross(Up, new Vec3(t.X, t.Y, 0)), Up), Length = len, Assembly = mark });
            }
        }
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
