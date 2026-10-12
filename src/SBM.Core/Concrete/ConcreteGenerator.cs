using System.Collections.Concurrent;
using System.Diagnostics;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Units;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Concrete;

/// <summary>One expansion joint: the blockout concrete (cast after the device is set) and a generic device envelope.</summary>
public sealed class ExpansionJointResult
{
    public required string Support { get; init; }
    public required bool AtBegin { get; init; }
    public required ExpansionJointDefinition Definition { get; init; }
    /// <summary>Blockout concrete on this unit's side of the joint.</summary>
    public required Mesh Blockout { get; init; }
    /// <summary>Generic device envelope (null when <see cref="ExpansionJointDefinition.Body"/> is off).</summary>
    public Mesh? Body { get; init; }
    /// <summary>Length of the joint across the deck between barriers (along the joint line).</summary>
    public double Length { get; init; }
    /// <summary>Thickened deck end under the blockout (null = not modeled).</summary>
    public DeckEndBlockResult? EndBlock { get; init; }
    public double BlockoutVolume => Math.Abs(Blockout.Volume());
    public double BodyVolume => Body == null ? 0 : Math.Abs(Body.Volume());
    public string DeviceName => Definition.Designation is { Length: > 0 } dn ? $"{dn} (placeholder), {Support}"
        : Definition.Type == ExpansionJointType.StripSeal
        ? "Generic strip seal expansion joint (manufacturer-designed placeholder)"
        : Definition.Type == ExpansionJointType.Sealed ? "Generic sealed expansion joint (placeholder)"
        : "Generic modular expansion joint (manufacturer-designed placeholder)";
    /// <summary>Device geometry the plans do not fix (from <see cref="ExpansionJointDefinition.Unresolved"/>); null = none.</summary>
    public Assumption? Assumption => Definition.Unresolved is { Length: > 0 } why ? Assumption.Unresolved(why, Definition.Basis ?? "generic envelope") : null;
    public string BlockoutName => $"Joint blockout concrete, {Support}";
}

/// <summary>Where a point lies relative to a deck end block: X = distance from the deck-end face (+ toward the deck, perpendicular to the joint), Drop = below finished top of slab.</summary>
public readonly record struct EndBlockHit(double X, double Drop, bool OnDeck, bool Inside, double AboveSoffit);

/// <summary>Steel found inside a deck end block: embedded as the plans show, or an unintended clash. Heights are above the block soffit (m).</summary>
public sealed record DeckEndClash(string Support, string Member, bool Intended, int Count, double MaxAboveSoffit, string Note);

/// <summary>The thickened deck end ("end dam diaphragm") at an expansion joint: solid + the analytic outline used for clash checks.</summary>
public sealed class DeckEndBlockResult
{
    public required string Support { get; init; }
    public required bool AtBegin { get; init; }
    public required DeckEndBlockDefinition Definition { get; init; }
    public required Mesh Mesh { get; init; }
    /// <summary>Soffit polyline (X from the deck-end face, Drop below top of slab) from the face to the slab soffit.</summary>
    public required List<EndBlockPoint> Soffit { get; init; }
    public required double SlabThickness { get; init; }
    public required double BlockoutWidth { get; init; }
    /// <summary>Drop of the bottom of the concrete above the block at X &lt; BlockoutWidth (the blockout's bottom).</summary>
    public required double BlockoutBottom { get; init; }
    /// <summary>Distance of a plan point from the deck-end face (+ toward the deck, perpendicular to the joint).</summary>
    public required Func<Vec2, double> PlanX { get; init; }
    /// <summary>Drop of a point below the finished top of slab, and whether it lies between the deck edges.</summary>
    public required Func<Vec3, (double drop, bool onDeck)> DeckAt { get; init; }
    public double Volume => Math.Abs(Mesh.Volume());
    public string Name => $"Deck end block (end dam diaphragm), {Support}";

    public double SoffitDropAt(double x)
    {
        var s = Soffit;
        if (x < s[0].X || x > s[^1].X) return double.NaN;
        for (int i = 1; i < s.Count; i++)
            if (x <= s[i].X + 1e-12)
            {
                double w = s[i].X - s[i - 1].X;
                return w < 1e-12 ? s[i].Drop : s[i - 1].Drop + (s[i].Drop - s[i - 1].Drop) * (x - s[i - 1].X) / w;
            }
        return s[^1].Drop;
    }

    /// <summary>Classifies a world point: inside the block solid (between the slab/blockout concrete above and the soffit), and how far it is above the soffit.</summary>
    public EndBlockHit Probe(Vec3 p)
    {
        double x = PlanX(p.XY);
        if (x < -1e-6 || x > Soffit[^1].X + 1e-6) return new EndBlockHit(x, 0, false, false, 0);
        var (drop, onDeck) = DeckAt(p);
        double soff = SoffitDropAt(x);
        if (!onDeck || double.IsNaN(soff)) return new EndBlockHit(x, drop, onDeck, false, 0);
        double top = x < BlockoutWidth ? BlockoutBottom : SlabThickness;
        return new EndBlockHit(x, drop, onDeck, drop >= top - 1e-6 && drop <= soff + 1e-6, soff - drop);
    }
}

public sealed class ConcreteModel
{
    public List<ExpansionJointResult> Joints { get; } = new();
    public Mesh? Deck { get; set; }
    public Mesh? Haunches { get; set; }
    /// <summary>Steel embedded in or clashing with the deck end blocks (filled by <c>DeckEndBlockCheck</c> once the steel exists).</summary>
    public List<DeckEndClash> EndBlockClashes { get; } = new();
    public List<(string Name, Mesh Mesh)> Barriers { get; } = new();
    /// <summary>Closed solids a barrier mesh is made of (custom barriers with zones: one per section length) - IFC items; absent = the mesh is one solid.</summary>
    public Dictionary<string, List<Mesh>> BarrierPieces { get; } = new();
    /// <summary>Deck slab widenings under barrier zones (plan "deck bump-outs"), part of the deck slab.</summary>
    public List<(string Name, Mesh Mesh)> DeckBumps { get; } = new();
    public double DeckBumpVolume => DeckBumps.Sum(b => Math.Abs(b.Mesh.Volume()));
    /// <summary>Faces carrying a surface tag (custom barrier sections), for the surface treatments.</summary>
    public List<TaggedSurface> TaggedSurfaces { get; } = new();
    public List<BarMark> Marks { get; } = new();
    public List<BarPlacement> Bars { get; } = new();
    /// <summary>Cover violations of explicit plan bars (<see cref="Bridge.PlanBarSet"/>), bars kept as placed.</summary>
    public List<PlanBarCoverViolation> CoverViolations { get; } = new();
    public List<string> Notes { get; } = new();
    public List<Diagnostic> Diagnostics { get; } = new();
    /// <summary>Exact-geometry option: what was built as NURBS (null when the option is off).</summary>
    public NurbsReport? Nurbs { get; set; }
    /// <summary>Precast segmental box (bridges with <c>BridgeDefinition.Segmental</c>): segments, tendons, bearings and bents. Hung on the
    /// concrete model so every consumer that receives the concrete model (viewport, IFC, database) finds it; null otherwise.</summary>
    public SBM.Core.Segmental.SegmentalModel? Segmental { get; set; }
    public double DeckVolume => Math.Abs(Deck?.Volume() ?? 0);
    public double HaunchVolume => Math.Abs(Haunches?.Volume() ?? 0);
    public double BarrierVolume => Barriers.Sum(b => Math.Abs(b.Mesh.Volume()));
    public double RebarMass => Marks.Sum(m => m.TotalMass);
    public TimeSpan GenerationTime { get; set; }
}

/// <summary>
/// Cast-in-place deck, haunches and barriers as solids, plus deck and barrier reinforcement with bar marks.
/// Deck top follows finished grade (profile + cross slope); haunches fill from the top flange to the deck bottom;
/// barriers (TxDOT SSTR by default) sit on the deck at each edge between the deck end lines.
/// </summary>
public sealed partial class ConcreteGenerator
{
    private static readonly Vec3 Up = new(0, 0, 1);
    private readonly BridgeFraming _f;
    private readonly BridgeDefinition _def;
    private readonly Alignment _a;
    private readonly UnitSystem _u;
    private readonly ConcreteModel _m = new();
    private readonly ConcurrentQueue<Diagnostic> _diag = new();
    private readonly ConcurrentDictionary<(long, long, long), (double b, double e)> _ends = new();

    private ConcreteGenerator(BridgeFraming f, UnitSystem u)
    {
        _f = f;
        _def = f.Definition;
        _a = f.Alignment;
        _u = u;
    }

    public static ConcreteModel Generate(BridgeFraming framing, UnitSystem units, Project.GeometrySettings? geometry = null)
    {
        var g = new ConcreteGenerator(framing, units);
        if (geometry is { ExactCurvedGeometry: true })
        {
            g._geo = geometry;
            g._fitTol = geometry.FitToleranceFor(units);
            g._m.Nurbs = g._nurbs = new NurbsReport { Tolerance = g._fitTol };
        }
        var sw = Stopwatch.StartNew();
        if (!framing.HasErrors && framing.Deck != null) g.Run();
        g._m.Diagnostics.AddRange(g._diag);
        g._m.GenerationTime = sw.Elapsed;
        return g._m;
    }

    private void Warn(string s) => _diag.Enqueue(new Diagnostic(DiagnosticSeverity.Warning, s));

    private DeckDefinition Deck => _def.Deck;
    private DeckResult DR => _f.Deck!;

    private void Run()
    {
        var tasks = new List<Action> { BuildDeckSolid, BuildHaunches, BuildBarriers, BuildJoints };
        Parallel.ForEach(tasks, t => t());
        var bars = new ConcurrentBag<(string series, RebarSize size, BarShape shape, string loc, bool epoxy, BarPlacement placement)>();
        var rebarTasks = new List<Action>();
        if (Deck.Reinforcement.Enabled)
        {
            if (Deck.Reinforcement.Plan != null) rebarTasks.Add(BuildPlanDeckRebar);
            else if (Deck.Reinforcement.PlanBarSets.Count == 0) rebarTasks.Add(() => BuildDeckRebar(bars));
            if (Deck.Reinforcement.PlanBarSets.Count > 0) rebarTasks.Add(BuildPlanBarSets);
        }
        if (Deck.Barrier.Type == BarrierType.TxDotSstr && Deck.Barrier.Reinforcement) rebarTasks.Add(() => BuildSstrRebar(bars));
        Parallel.ForEach(rebarTasks, t => t());
        AssignMarks(bars);
    }

    /// <summary>Explicit plan bar sets of the deck (<see cref="PlanBarSet"/>, project coordinates): marks + bars, clear cover against the deck concrete
    /// (slab, haunches, bump-outs, barriers / curbs, joint blockouts) -> warnings and <see cref="ConcreteModel.CoverViolations"/>.</summary>
    private void BuildPlanBarSets()
    {
        var hosts = new List<Mesh>();
        // the slab itself is tested exactly (its mesh is a chorded loft: on a tight curve the chord sagitta at the edges exceeds the 1/8" tolerance)
        (double, Vec3) Slab(Vec3 p)
        {
            var iv = _a.Inverse(p.XY);
            double d = iv.Distance, o = iv.Offset, top = Top(d, o), bot = top - Deck.Thickness;
            double best = o - LeftAtD(d); var n = new Vec3(0, 0, 0);
            void Take(double v, Vec3 nn) { if (v < best) { best = v; n = nn; } }
            Take(RightAtD(d) - o, new Vec3(0, 0, 0));
            Take(DR.FromBegin(p.XY), new Vec3(0, 0, 0));
            Take(DR.FromEnd(p.XY), new Vec3(0, 0, 0));
            Take(top - p.Z, new Vec3(0, 0, 1));
            Take(p.Z - bot, new Vec3(0, 0, -1));
            return (best, n);
        }
        if (_m.Haunches != null) hosts.Add(_m.Haunches);
        hosts.AddRange(_m.DeckBumps.Select(b => b.Mesh));
        hosts.AddRange(_m.Barriers.Select(b => b.Mesh));
        hosts.AddRange(_m.Joints.Select(j => j.Blockout));
        hosts.AddRange(_m.Joints.Where(j => j.EndBlock != null).Select(j => j.EndBlock!.Mesh));
        var res = PlanBarSetBuilder.Build(Deck.Reinforcement.PlanBarSets, p => p, "", "Deck", hosts, Deck.Reinforcement.EpoxyCoated, new Func<Vec3, (double, Vec3)>[] { Slab });
        foreach (var w in res.Warnings) Warn(w);
        lock (_m)
        {
            _m.Marks.AddRange(res.Marks);
            _m.Bars.AddRange(res.Bars);
            _m.Notes.AddRange(res.Notes);
            _m.CoverViolations.AddRange(res.Violations);
        }
    }

    // ================================================================== surfaces & end lines

    private double Top(double d, double o) => Deck.FinishedElevation(_a, _a.DistanceToStation(d), o);

    /// <summary>Alignment distances where the offset curve crosses the begin/end deck lines (optionally inset, toward the deck).</summary>
    private (double b, double e) Ends(double o, double inset = 0) => Ends(o, inset, inset);

    private (double b, double e) Ends(double o, double insetB, double insetE)
    {
        var key = ((long)Math.Round(o * 1e4), (long)Math.Round(insetB * 1e4), (long)Math.Round(insetE * 1e4));
        return _ends.GetOrAdd(key, _ =>
        {
            var nb = DR.BeginLineDirection.Left;           // toward the deck (up-station)
            var ne = -DR.EndLineDirection.Left;
            double db = PolyCross(true, insetB, _ => o) ?? Cross(DR.BeginLinePoint + nb * insetB, DR.BeginLineDirection, o);
            double de = PolyCross(false, insetE, _ => o) ?? Cross(DR.EndLinePoint + ne * insetE, DR.EndLineDirection, o);
            return (db, de);
        });
    }

    /// <summary>Deck edges at an alignment distance (variable width).</summary>
    private double LeftAtD(double d) => Deck.LeftAt(_a.DistanceToStation(d));
    private double RightAtD(double d) => Deck.RightAt(_a.DistanceToStation(d));

    /// <summary>Alignment distances where a variable offset curve crosses the begin/end deck lines (optionally inset).</summary>
    private (double b, double e) EndsVar(Func<double, double> offAtD, double inset = 0) => EndsVar(offAtD, inset, inset);

    private (double b, double e) EndsVar(Func<double, double> offAtD, double insetB, double insetE)
    {
        var nb = DR.BeginLineDirection.Left;
        var ne = -DR.EndLineDirection.Left;
        double One(Vec2 p, Vec2 dir)
        {
            var inv = _a.Inverse(p);
            double o = offAtD(inv.Distance);
            var q = p + dir * (o - inv.Offset);
            double guess = _a.Inverse(q).Distance;
            double w = Math.Abs(o - inv.Offset) + 15;
            return _a.IntersectOffsetCurveWithLine(offAtD, p, dir, guess - w, guess + w, guess, out double d) ? d : guess;
        }
        return (PolyCross(true, insetB, offAtD) ?? One(DR.BeginLinePoint + nb * insetB, DR.BeginLineDirection),
                PolyCross(false, insetE, offAtD) ?? One(DR.EndLinePoint + ne * insetE, DR.EndLineDirection));
    }

    private readonly ConcurrentDictionary<(bool, long), DeckEndPolyline> _insetPolys = new();

    /// <summary>Kinked deck-end line (begin / end) moved by <paramref name="inset"/> toward the deck; null = the end is straight.</summary>
    private DeckEndPolyline? EndPoly(bool begin, double inset)
    {
        var p = begin ? DR.BeginPolyline : DR.EndPolyline;
        if (p == null) return null;
        return _insetPolys.GetOrAdd((begin, (long)Math.Round(inset * 1e6)), _ => p.Inset(inset));
    }

    /// <summary>Crossing of an offset curve with a kinked deck end (null when that end is straight).</summary>
    private double? PolyCross(bool begin, double inset, Func<double, double> offAtD)
    {
        if (EndPoly(begin, inset) is not { } p) return null;
        if (!p.CrossOffsetCurve(_a, offAtD, out double d)) Warn($"Kinked deck end ({(begin ? "begin" : "end")}): an offset line does not cross it near the deck; nearest estimate used.");
        return d;
    }

    /// <summary>Interior break points of the kinked deck ends between the deck edges (solid columns run through them).</summary>
    private IEnumerable<Vec2> KinkVertices()
    {
        foreach (var p in new[] { DR.BeginPolyline, DR.EndPolyline })
            if (p != null) foreach (var v in p.VerticesBetween(_a, LeftAtD, RightAtD)) yield return v;
    }

    private double Cross(Vec2 p, Vec2 dir, double o)
    {
        var inv = _a.Inverse(p);
        var q = p + dir * (o - inv.Offset);
        double guess = _a.Inverse(q).Distance;
        double w = Math.Abs(o - inv.Offset) + 15;
        return _a.IntersectOffsetCurveWithLine(_ => o, p, dir, guess - w, guess + w, guess, out double d) ? d : guess;
    }

    private List<double> DeckOffsets(double step)
    {
        double l = Deck.LeftEdgeOffset, r = Deck.RightEdgeOffset;
        var list = new List<double> { l, r };
        int n = Math.Max(1, (int)Math.Ceiling((r - l) / step));
        for (int i = 1; i < n; i++) list.Add(l + (r - l) * i / n);
        foreach (double piv in PivotColumns()) if (piv > l && piv < r) list.Add(piv);
        foreach (var g in StripLines(l, r)) list.Add(g);
        foreach (var v in KinkVertices()) list.Add(_a.Inverse(v).Offset);
        return list.Distinct().OrderBy(x => x).ToList();
    }

    /// <summary>Deck grid columns at the crown (cross-slope pivot): the constant pivot offset, or with a varying pivot
    /// (<see cref="CrossSlopeTable.PivotOffsets"/>) every table offset; a crown that moves between columns inside the deck is
    /// reported (the slab top is chorded across it there).</summary>
    private IEnumerable<double> PivotColumns()
    {
        var cs = _a.CrossSlope;
        if (!cs.VariablePivot) return new[] { cs.PivotOffset };
        var offs = cs.PivotOffsets!.Select(p => p.Offset).Distinct().ToList();
        if (offs.Count > 1 && offs.Any(o => o > Deck.MinLeft && o < Deck.MaxRight))
            lock (_m.Notes) if (!_m.Notes.Any(n => n.StartsWith("Varying cross-slope pivot")))
                _m.Notes.Add("Varying cross-slope pivot inside the deck: the slab top has grid columns at the table's pivot offsets only; between them the crown line is chorded across the cells.");
        return offs;
    }

    /// <summary>Gutter lines (edge strip start) strictly inside [l, r] of a constant-width deck, when the edge strip option is on.</summary>
    private IEnumerable<double> StripLines(double l, double r)
    {
        if (Deck.EdgeStrip is not { LowSideLevel: true, Width: > 0 } es) yield break;
        if (Deck.LeftEdgeOffset + es.Width > l + 1e-6 && Deck.LeftEdgeOffset + es.Width < r - 1e-6) yield return Deck.LeftEdgeOffset + es.Width;
        if (Deck.RightEdgeOffset - es.Width > l + 1e-6 && Deck.RightEdgeOffset - es.Width < r - 1e-6) yield return Deck.RightEdgeOffset - es.Width;
    }

    /// <summary>
    /// Variable width: across-the-deck columns as offset functions of the alignment distance, between the edges inset by li / ri. Columns sit at
    /// fixed fractions of the width; with the edge strip option on, the gutter lines (edge -/+ strip width) are columns too, so the level strip
    /// has its own cells.
    /// </summary>
    private List<Func<double, double>> VarColumns(double li, double ri)
    {
        var cols = VarColumnsBase(li, ri);
        // kinked deck ends: one more column through each break point, at its fraction between the neighbouring columns
        foreach (var v in KinkVertices())
        {
            var so = _a.Inverse(v);
            double dv = so.Distance, ov = so.Offset;
            for (int j = 0; j + 1 < cols.Count; j++)
            {
                double a = cols[j](dv), b = cols[j + 1](dv);
                if (ov <= a + 1e-6 || ov >= b - 1e-6) continue;
                var ca = cols[j]; var cb = cols[j + 1];
                double f = (ov - a) / (b - a);
                cols.Insert(j + 1, d => ca(d) + (cb(d) - ca(d)) * f);
                break;
            }
        }
        return cols;
    }

    private List<Func<double, double>> VarColumnsBase(double li, double ri)
    {
        var list = new List<Func<double, double>>();
        double wMax = Deck.MaxRight - Deck.MinLeft;
        if (Deck.EdgeStrip is { LowSideLevel: true, Width: > 0 } es)
        {
            double w = es.Width;
            bool gl = li < w - 1e-6, gr = ri < w - 1e-6;
            int ni = Math.Max(1, (int)Math.Ceiling((wMax - 2 * w) / 1.0));
            Func<double, double> lo = d => gl ? LeftAtD(d) + w : LeftAtD(d) + li;
            Func<double, double> hi = d => gr ? RightAtD(d) - w : RightAtD(d) - ri;
            list.Add(d => LeftAtD(d) + li);
            if (gl) list.Add(lo);
            for (int k = 1; k < ni; k++) { double t = (double)k / ni; list.Add(d => lo(d) + (hi(d) - lo(d)) * t); }
            if (gr) list.Add(hi);
            list.Add(d => RightAtD(d) - ri);
            return list;
        }
        int nk = Math.Max(2, (int)Math.Ceiling(wMax / 1.0)) + 1;
        for (int k = 0; k < nk; k++)
        {
            double t = (double)k / (nk - 1);
            list.Add(d => { double l = LeftAtD(d) + li, r = RightAtD(d) - ri; return l + (r - l) * t; });
        }
        return list;
    }

    private int AlongSamples(double length) => Math.Clamp((int)Math.Ceiling(length / 1.5), 2, 2000);

    /// <summary>Closed solid from top/bottom grids [i along][k across]; orientation fixed by volume sign.</summary>
    private static Mesh GridSolid(Vec3[,] top, Vec3[,] bot)
    {
        var m = new Mesh();
        int ni = top.GetLength(0), nk = top.GetLength(1);
        int[,] T = new int[ni, nk], B = new int[ni, nk];
        for (int i = 0; i < ni; i++)
            for (int k = 0; k < nk; k++) { T[i, k] = m.Add(top[i, k]); B[i, k] = m.Add(bot[i, k]); }
        void Q(int a, int b, int c, int d) { m.Tri(a, b, c); m.Tri(a, c, d); }
        for (int i = 0; i + 1 < ni; i++)
            for (int k = 0; k + 1 < nk; k++)
            {
                Q(T[i, k], T[i + 1, k], T[i + 1, k + 1], T[i, k + 1]);
                Q(B[i, k], B[i, k + 1], B[i + 1, k + 1], B[i + 1, k]);
            }
        for (int i = 0; i + 1 < ni; i++)
        {
            Q(T[i, 0], B[i, 0], B[i + 1, 0], T[i + 1, 0]);
            Q(T[i, nk - 1], T[i + 1, nk - 1], B[i + 1, nk - 1], B[i, nk - 1]);
        }
        for (int k = 0; k + 1 < nk; k++)
        {
            Q(T[0, k], T[0, k + 1], B[0, k + 1], B[0, k]);
            Q(T[ni - 1, k], B[ni - 1, k], B[ni - 1, k + 1], T[ni - 1, k + 1]);
        }
        if (m.Volume() < 0)
            for (int i = 0; i < m.Indices.Count; i += 3) (m.Indices[i + 1], m.Indices[i + 2]) = (m.Indices[i + 2], m.Indices[i + 1]);
        return m;
    }

    // ================================================================== deck, haunches, barriers

    private void BuildDeckSolid()
    {
        if (Deck.VariableWidth) { BuildVariableDeckSolid(); return; }
        var offs = DeckOffsets(1.0);
        var (b0, e0) = Ends(0, InsetB, InsetE);
        int ni = AlongSamples(e0 - b0) + 1;
        if (_geo != null && NurbsDeck(offs.Select(o => (Func<double, double>)(_ => o)).ToList(), ref ni, out var nt, out var nb) is { } slab)
        {
            _m.Deck = GridSolid(nt, nb);
            _m.Deck.AddNurbs(slab.ToSolid("deck slab"), 0);
            AddJointShelves();
            return;
        }
        var top = new Vec3[ni, offs.Count];
        var bot = new Vec3[ni, offs.Count];
        for (int k = 0; k < offs.Count; k++)
        {
            var (db, de) = Ends(offs[k], InsetB, InsetE);
            for (int i = 0; i < ni; i++)
            {
                double d = db + (de - db) * i / (ni - 1);
                var p = _a.PointAtDistance(d, offs[k]);
                double z = Top(d, offs[k]);
                top[i, k] = p.WithZ(z);
                bot[i, k] = p.WithZ(z - Deck.Thickness);
            }
        }
        _m.Deck = GridSolid(top, bot);
        AddJointShelves();
    }

    /// <summary>Variable width: columns at fixed fractions of the width between the edges at each station.</summary>
    private void BuildVariableDeckSolid()
    {
        var cols = VarColumns(0, 0);
        int nk = cols.Count;
        var (b0, e0) = Ends(0, InsetB, InsetE);
        int ni = AlongSamples(e0 - b0) + 1;
        if (_geo != null && NurbsDeck(cols, ref ni, out var nt, out var nb) is { } slab)
        {
            _m.Deck = GridSolid(nt, nb);
            _m.Deck.AddNurbs(slab.ToSolid("deck slab"), 0);
            AddJointShelves();
            return;
        }
        var top = new Vec3[ni, nk];
        var bot = new Vec3[ni, nk];
        for (int k = 0; k < nk; k++)
        {
            var Off = cols[k];
            var (db, de) = EndsVar(Off, InsetB, InsetE);
            for (int i = 0; i < ni; i++)
            {
                double d = db + (de - db) * i / (ni - 1);
                double o = Off(d);
                var p = _a.PointAtDistance(d, o);
                double z = Top(d, o);
                top[i, k] = p.WithZ(z);
                bot[i, k] = p.WithZ(z - Deck.Thickness);
            }
        }
        _m.Deck = GridSolid(top, bot);
        AddJointShelves();
    }

    private void BuildHaunches()
    {
        if (_def.Girders.IsTub) { BuildTubHaunches(); return; }   // OPTIONAL tub girders: one haunch over each top flange (ConcreteTubHaunch.cs)
        var mesh = new Mesh();
        foreach (var g in _f.Girders)
        {
            var path = g.Path;
            var ss = path.S.ToList();
            if (ss.Count < 2) continue;
            if (_geo != null && NurbsHaunch(mesh, g, s =>
                {
                    if (_def.Girders.IsPrestressed) return (Prestressed.TxGirderCatalog.Get(_def.Girders.Prestressed).TopWidth / 2, 0.0);
                    var sg = _def.Girders.Section.SegmentAt(GirderPlate.TopFlange, path.At(s).station);
                    return (sg.Width / 2, sg.Thickness);
                })) continue;
            var rings = new List<Vec3[]>(ss.Count);
            Vec2[]? sec = null;
            foreach (double s in HaunchSamples(ss))
            {
                var (p, t, sta) = path.At(s);
                double hw, tf;
                if (_def.Girders.IsPrestressed) (hw, tf) = (Prestressed.TxGirderCatalog.Get(_def.Girders.Prestressed).TopWidth / 2, 0.0);
                else
                {
                    var seg = _def.Girders.Section.SegmentAt(GirderPlate.TopFlange, sta);
                    (hw, tf) = (seg.Width / 2, seg.Thickness);
                }
                var right = new Vec3(t.Right.X, t.Right.Y, 0);
                var pl = (p + right * -hw).XY;
                var pr = (p + right * hw).XY;
                double zl = DeckBottomAt(pl), zr = DeckBottomAt(pr);
                double ztf = p.Z + tf;
                sec ??= new[] { new Vec2(-hw, 0), new Vec2(hw, 0), new Vec2(hw, 1), new Vec2(-hw, 1) };
                rings.Add(new[] { pl.WithZ(ztf), pr.WithZ(ztf), pr.WithZ(Math.Max(zr, ztf + 1e-4)), pl.WithZ(Math.Max(zl, ztf + 1e-4)) });
            }
            MeshBuilder.AddLoft(mesh, sec!, rings);
        }
        _m.Haunches = mesh;
    }

    /// <summary>Haunch ring stations: the girder path samples plus points at most 1.5 m apart in between, so the haunch top follows the
    /// deck soffit (profile / vertical curve, cross slope) along straight chords whose path has samples only at the span ends
    /// (prestressed beams, chorded girders). Path jogs (samples &lt; 0.1 mm apart, per-span layouts) are kept as they are.</summary>
    public static List<double> HaunchSamples(IReadOnlyList<double> ss)
    {
        var r = new List<double>(ss.Count * 2);
        for (int i = 0; i < ss.Count; i++)
        {
            r.Add(ss[i]);
            if (i + 1 == ss.Count) break;
            double ds = ss[i + 1] - ss[i];
            if (ds < 1e-4) continue;
            int n = (int)Math.Ceiling(ds / 1.5 - 1e-9);
            for (int k = 1; k < n; k++) r.Add(ss[i] + ds * k / n);
        }
        return r;
    }

    private double DeckBottomAt(Vec2 p)
    {
        var so = _a.Inverse(p);
        return Deck.FinishedElevation(_a, so.Station, so.Offset) - Deck.Thickness;
    }

    private void BuildBarriers()
    {
        if (Deck.Barrier.Type == BarrierType.Custom)
        {
            foreach (var (side, on) in new[] { (-1, Deck.Barrier.Left), (1, Deck.Barrier.Right) })
                if (on)
                {
                    try { BuildCustomBarrier(side, side < 0 ? LeftAtD : RightAtD); }
                    catch (ArgumentException ex) { Warn($"Custom barrier not built - {ex.Message}"); }
                }
            return;
        }
        var prof = BarrierProfiles.For(Deck.Barrier);
        if (prof == null) return;
        foreach (var (side, on) in new[] { (-1, Deck.Barrier.Left), (1, Deck.Barrier.Right) })
        {
            if (!on) continue;
            Func<double, double> edgeAt = side < 0 ? LeftAtD : RightAtD;
            double OffAt(double d, double r) => edgeAt(d) - side * r;
            var (b0, e0) = Deck.VariableWidth ? EndsVar(edgeAt) : Ends(edgeAt(0));
            int ni = AlongSamples(e0 - b0) + 1;
            var ends = prof.Select(q => Deck.VariableWidth ? EndsVar(d => OffAt(d, q.X)) : Ends(OffAt(0, q.X))).ToArray();
            if (_geo != null && NurbsBarrier(prof, (u, k) =>
                {
                    double d = ends[k].b + (ends[k].e - ends[k].b) * u;
                    double o = OffAt(d, prof[k].X);
                    var p = _a.PointAtDistance(d, o);
                    double zBase = prof[k].Y < 1e-9 ? Top(d, o) : Top(d, edgeAt(d));
                    return p.WithZ(zBase + prof[k].Y);
                }, ni) is { } nm)
            {
                _m.Barriers.Add((side < 0 ? "Left barrier" : "Right barrier", nm));
                continue;
            }
            var rings = new List<Vec3[]>(ni);
            for (int i = 0; i < ni; i++)
            {
                var ring = new Vec3[prof.Length];
                for (int k = 0; k < prof.Length; k++)
                {
                    double d = ends[k].b + (ends[k].e - ends[k].b) * i / (ni - 1);
                    double o = OffAt(d, prof[k].X);
                    var p = _a.PointAtDistance(d, o);
                    double zBase = prof[k].Y < 1e-9 ? Top(d, o) : Top(d, edgeAt(d));
                    ring[k] = p.WithZ(zBase + prof[k].Y);
                }
                rings.Add(ring);
            }
            var mesh = new Mesh();
            MeshBuilder.AddLoft(mesh, prof, rings);
            _m.Barriers.Add((side < 0 ? "Left barrier" : "Right barrier", mesh));
        }
    }

    // ================================================================== expansion joints

    private ExpansionJointDefinition? JointAt(int support) =>
        _def.Supports[support].ExpansionJoint is { Active: true } j ? j : null;
    private ExpansionJointDefinition? JointB => JointAt(0);
    private ExpansionJointDefinition? JointE => JointAt(_def.Supports.Count - 1);
    private double InsetB => JointB?.BlockoutWidth ?? 0;
    private double InsetE => JointE?.BlockoutWidth ?? 0;

    /// <summary>Alignment distance where an offset curve crosses a deck end line moved by <paramref name="inset"/> (+ toward the deck).</summary>
    private double EndD(Func<double, double> offAtD, bool begin, double inset)
    {
        if (PolyCross(begin, inset, offAtD) is double pd) return pd;
        var p = begin ? DR.BeginLinePoint + DR.BeginLineDirection.Left * inset : DR.EndLinePoint - DR.EndLineDirection.Left * inset;
        var dir = begin ? DR.BeginLineDirection : DR.EndLineDirection;
        var inv = _a.Inverse(p);
        double o = offAtD(inv.Distance);
        var q = p + dir * (o - inv.Offset);
        double guess = _a.Inverse(q).Distance;
        double w = Math.Abs(o - inv.Offset) + 15;
        return _a.IntersectOffsetCurveWithLine(offAtD, p, dir, guess - w, guess + w, guess, out double d) ? d : guess;
    }

    /// <summary>Across-the-deck columns as offset functions; the full width uses exactly the deck solid's columns (matching seams).</summary>
    private List<Func<double, double>> JointColumns(double li, double ri)
    {
        var list = new List<Func<double, double>>();
        if (Deck.VariableWidth) return VarColumns(li, ri);
        var offs = DeckOffsets(1.0);
        if (li == 0 && ri == 0) return offs.Select(o => (Func<double, double>)(_ => o)).ToList();
        double l0 = Deck.LeftEdgeOffset + li, r0 = Deck.RightEdgeOffset - ri;
        int n = Math.Max(1, (int)Math.Ceiling((r0 - l0) / 1.0));
        var os = Enumerable.Range(0, n + 1).Select(i => l0 + (r0 - l0) * i / n).ToList();
        foreach (double piv in PivotColumns()) if (piv > l0 && piv < r0) os.Add(piv);
        os.AddRange(StripLines(l0, r0));
        return os.Distinct().OrderBy(x => x).Select(o => (Func<double, double>)(_ => o)).ToList();
    }

    /// <summary>Slab of the deck-end zone between two insets from the deck end face (+ toward the deck); top/bottom are drops below finished grade.</summary>
    private Mesh EndSlab(bool begin, double inset0, double inset1, double topDrop, double botDrop, double li = 0, double ri = 0)
    {
        var cols = JointColumns(li, ri);
        int nk = cols.Count;
        var top = new Vec3[2, nk];
        var bot = new Vec3[2, nk];
        for (int k = 0; k < nk; k++)
            for (int i = 0; i < 2; i++)
            {
                double d = EndD(cols[k], begin, i == 0 ? inset0 : inset1);
                double o = cols[k](d);
                var p = _a.PointAtDistance(d, o);
                double z = Top(d, o);
                top[i, k] = p.WithZ(z - topDrop);
                bot[i, k] = p.WithZ(z - botDrop);
            }
        return GridSolid(top, bot);
    }

    /// <summary>Where the blockout is shallower than the slab the deck keeps a shelf under it (the primary deck is cut back by the full blockout width).</summary>
    private void AddJointShelves()
    {
        foreach (bool begin in new[] { true, false })
        {
            var j = begin ? JointB : JointE;
            if (j == null || j.BlockoutWidth <= 1e-9 || EffectiveBlockoutDepth(j) >= Deck.Thickness - 1e-6) continue;
            _m.Deck!.Append(EndSlab(begin, 0, j.BlockoutWidth, EffectiveBlockoutDepth(j), Deck.Thickness));
        }
    }

    /// <summary>Blockout depth actually built: the plan depth when the end block provides the concrete below the slab, else limited to the slab.</summary>
    private double EffectiveBlockoutDepth(ExpansionJointDefinition j) =>
        j.EndBlock != null && j.EndBlock.SoffitFor(Deck.Thickness).Count >= 2 ? j.BlockoutDepth : Math.Min(j.BlockoutDepth, Deck.Thickness);

    private DeckEndBlockResult? BuildEndBlock(bool begin, SupportDefinition sup, ExpansionJointDefinition j)
    {
        var eb = j.EndBlock!;
        double slab = Deck.Thickness, bo = j.BlockoutDepth, d = j.BlockoutWidth;
        var soffit = eb.SoffitFor(slab);
        string where = $"Deck end block at {sup.Name}";
        if (soffit.Count < 2) { Warn($"{where}: depth {_u.FormatLength(eb.Depth, LengthKind.Short)} is not greater than the slab ({_u.FormatLength(slab, LengthKind.Short)}); no end block built."); return null; }
        if (Math.Abs(soffit[0].X) > 1e-6) { Warn($"{where}: the soffit profile must start at the deck-end face (X = 0); no end block built."); return null; }
        double bt = Math.Max(bo, slab);
        if (soffit[0].Drop <= bt + 1e-6) { Warn($"{where}: the soffit at the deck end ({_u.FormatLength(soffit[0].Drop, LengthKind.Short)} below top of slab) is not below the blockout ({_u.FormatLength(bt, LengthKind.Short)}); no end block built."); return null; }
        if (soffit[^1].X <= d + 1e-6 || soffit[^1].Drop > slab + 1e-6)
        { Warn($"{where}: the soffit must reach the slab soffit beyond the blockout edge ({_u.FormatLength(d, LengthKind.Short)}); no end block built."); return null; }
        // closed outline in (x, drop): face top, soffit, back along the slab soffit to the blockout edge, up to the blockout bottom
        var outline = soffit.Select(q => new Vec2(q.X, q.Drop)).ToList();
        outline.Insert(0, new Vec2(0, bt));
        if (bt > slab + 1e-6) { outline.Add(new Vec2(d, slab)); outline.Add(new Vec2(d, bt)); }
        var section = outline.Select(q => new Vec2(q.X, -q.Y)).ToList();
        var cols = JointColumns(0, 0);
        var rings = new List<Vec3[]>(cols.Count);
        foreach (var c in cols)
        {
            var ring = new Vec3[outline.Count];
            for (int i = 0; i < outline.Count; i++)
            {
                double dd = EndD(c, begin, outline[i].X);
                double o = c(dd);
                ring[i] = _a.PointAtDistance(dd, o).WithZ(Top(dd, o) - outline[i].Y);
            }
            rings.Add(ring);
        }
        var mesh = new Mesh();
        MeshBuilder.AddLoft(mesh, section, rings);
        var origin = begin ? DR.BeginLinePoint : DR.EndLinePoint;
        var normal = begin ? DR.BeginLineDirection.Left : -DR.EndLineDirection.Left;
        (double, bool) DeckAt(Vec3 p)
        {
            var so = _a.Inverse(p.XY);
            bool on = so.Offset >= Deck.LeftAt(so.Station) - 1e-6 && so.Offset <= Deck.RightAt(so.Station) + 1e-6;
            return (Deck.FinishedElevation(_a, so.Station, so.Offset) - p.Z, on);
        }
        return new DeckEndBlockResult
        {
            Support = sup.Name, AtBegin = begin, Definition = eb, Mesh = mesh, Soffit = soffit, SlabThickness = slab, BlockoutWidth = d,
            BlockoutBottom = bt, PlanX = (begin ? DR.BeginPolyline : DR.EndPolyline) is { } kp ? kp.SignedDistance : xy => Vec2.Dot(xy - origin, normal), DeckAt = DeckAt,
        };
    }

    private void BuildJoints()
    {
        var res = new List<ExpansionJointResult>();
        double barrierBase = BarrierProfiles.For(Deck.Barrier) is { Length: > 0 } pr ? pr.Max(q => q.X) : 0;
        foreach (bool begin in new[] { true, false })
        {
            int idx = begin ? 0 : _def.Supports.Count - 1;
            var sup = _def.Supports[idx];
            var j = sup.ExpansionJoint;
            if (j is not { Active: true }) continue;
            if (j.DeckEndOffset is double plan && Math.Abs(plan - sup.DeckEndOffset) > 0.001)
                Warn($"Expansion joint at {sup.Name}: plans give CL bearing to deck end {_u.FormatLength(plan, LengthKind.Short)}, the support has {_u.FormatLength(sup.DeckEndOffset, LengthKind.Short)} (not changed).");
            DeckEndBlockResult? block = j.EndBlock != null ? BuildEndBlock(begin, sup, j) : null;
            if (j.BlockoutDepth > Deck.Thickness + 1e-6 && block == null)
                Warn($"Expansion joint at {sup.Name}: blockout depth {_u.FormatLength(j.BlockoutDepth, LengthKind.Short)} exceeds the modeled slab ({_u.FormatLength(Deck.Thickness, LengthKind.Short)}); the plans' deeper end block is not modeled, so the blockout concrete is limited to the slab thickness.");
            if (j.SetDown >= Math.Min(j.BlockoutDepth, Deck.Thickness))
                Warn($"Expansion joint at {sup.Name}: set-down is not smaller than the blockout depth.");
            double depth = block != null ? j.BlockoutDepth : Math.Min(j.BlockoutDepth, Deck.Thickness);
            // No blockout width = the plans give no blockout (the deck is not cut back; no blockout concrete).
            var blockout = j.BlockoutWidth > 1e-9 ? EndSlab(begin, 0, j.BlockoutWidth, 0, depth) : new Mesh();
            Mesh? body = null;
            double length = 0;
            double li = Deck.Barrier.Left ? (j.EdgeInset ?? barrierBase) : 0, ri = Deck.Barrier.Right ? (j.EdgeInset ?? barrierBase) : 0;
            if (j.Body && j.GapAt50F > 1e-6)
            {
                // Device envelope: fills the opening between the deck-end faces from just below top of slab (set-down) to the blockout depth;
                // centred on the deck-end line when the plans dimension the slab to the CL joint without an opening (BodyCentered, placeholder).
                body = j.BodyCentered ? EndSlab(begin, j.GapAt50F / 2, -j.GapAt50F / 2, Math.Min(j.SetDown, depth * 0.5), depth, li, ri)
                    : EndSlab(begin, 0, -j.GapAt50F, Math.Min(j.SetDown, depth * 0.5), depth, li, ri);
            }
            if (j.Body || j.PlanLength != null)
            {
                var pts = JointColumns(li, ri).Select(c => { double d = EndD(c, begin, 0); var p = _a.PointAtDistance(d, c(d)); return new Vec2(p.X, p.Y); }).ToList();
                for (int k = 1; k < pts.Count; k++) length += (pts[k] - pts[k - 1]).Length;
            }
            if (j.Unresolved is { Length: > 0 } jw) Warn($"Expansion joint at {sup.Name}: {jw}");
            if (j.PlanLength is double jpl)
                lock (_m.Diagnostics) _m.Diagnostics.Add(new(Math.Abs(length - jpl) <= 0.003048 ? DiagnosticSeverity.Info : DiagnosticSeverity.Warning,
                    $"Expansion joint at {sup.Name}: length along the joint {_u.FormatLength(length)} vs plan {_u.FormatLength(jpl)}."));
            res.Add(new ExpansionJointResult { Support = sup.Name, AtBegin = begin, Definition = j, Blockout = blockout, Body = body, Length = length, EndBlock = block });
        }
        if (res.Count == 0) return;
        lock (_m.Joints) _m.Joints.AddRange(res);
        lock (_m.Notes) _m.Notes.Add("Expansion joints: blockout concrete is cut out of the deck and cast separately; the device is a generic envelope (the manufacturer designs and supplies it). Deck reinforcement is not cut back at the blockout."
            + (res.Any(r => r.EndBlock != null) ? " Deck end blocks (end dam diaphragms) are separate solids below the blockout; their reinforcing is not modeled. Haunch solids are not cut back where they run through an end block (the overlap is counted twice in haunch quantities)." : ""));
    }


    // ================================================================== deck reinforcement

    private void BuildDeckRebar(ConcurrentBag<(string, RebarSize, BarShape, string, bool, BarPlacement)> bars)
    {
        var r = Deck.Reinforcement;
        var tT = RebarCatalog.Size(r.TopTransverseSize);
        var bT = RebarCatalog.Size(r.BottomTransverseSize);
        var tL = RebarCatalog.Size(r.TopLongitudinalSize);
        var bL = RebarCatalog.Size(r.BottomLongitudinalSize);
        double t = Deck.Thickness;
        // Transverse bars are the outer layer of each mat.
        double zTopT = -r.TopCover - tT.Diameter / 2, zTopL = -r.TopCover - tT.Diameter - tL.Diameter / 2;
        double zBotT = -t + r.BottomCover + bT.Diameter / 2, zBotL = -t + r.BottomCover + bT.Diameter + bL.Diameter / 2;
        if (zTopL - tL.Diameter / 2 < zBotL + bL.Diameter / 2)
            Warn("Deck reinforcement: the top and bottom mats overlap; check slab thickness and cover.");
        bool epoxy = r.EpoxyCoated;
        double c = r.SideCover;
        // Variable width: longitudinal bars run within the narrowest deck (noted); transverse bars use the local width.
        double l = (Deck.VariableWidth ? Deck.LeftEdgeTable is { Count: > 0 } lt ? Math.Max(Deck.LeftEdgeOffset, lt.Max(p => p.Offset)) : Deck.LeftEdgeOffset : Deck.LeftEdgeOffset) + c;
        double rr = (Deck.VariableWidth ? Deck.RightEdgeTable is { Count: > 0 } rt ? Math.Min(Deck.RightEdgeOffset, rt.Min(p => p.Offset)) : Deck.RightEdgeOffset : Deck.RightEdgeOffset) - c;
        if (Deck.VariableWidth) lock (_m.Notes) _m.Notes.Add("Variable-width deck: longitudinal deck bars are placed within the narrowest width; add bars in the widening by hand.");

        double Lap(RebarSize s, double cover, double spacing) =>
            r.LapOverride > 0 ? r.LapOverride : RebarCatalog.ClassBLap(s, r.ConcreteStrengthKsi, r.SteelYieldKsi, epoxy, cover, spacing);
        lock (_m.Notes)
            foreach (var (s, cv, sp, name) in new[] { (tL, r.TopCover + tT.Diameter, r.TopLongitudinalSpacing, "top longitudinal"),
                         (bL, r.BottomCover + bT.Diameter, r.BottomLongitudinalSpacing, "bottom longitudinal") })
                _m.Notes.Add($"Lap splice, {name} {s.Name}: {_u.FormatLength(Lap(s, cv, sp))}" + (r.LapOverride > 0 ? " (override)" : " (AASHTO Class B)"));

        // ---- transverse bars
        double maxSkew = _def.Supports.Max(s => Math.Abs(s.SkewDegrees));
        bool parallel = r.TransverseOrientation switch
        {
            TransverseBarOrientation.ParallelToSkew => true,
            TransverseBarOrientation.NormalToAlignment => false,
            _ => maxSkew <= r.SkewLimitDegrees,
        };
        lock (_m.Notes) _m.Notes.Add($"Transverse deck bars {(parallel ? "parallel to skew" : "normal to the alignment")} (max skew {maxSkew:0.#}°).");
        var (db0, de0) = Ends(0, c);
        double dMin = db0, dMax = de0;
        if (!parallel)
        {
            var (bl, el) = Ends(l, c);
            var (br, er) = Ends(rr, c);
            dMin = Math.Min(bl, br);
            dMax = Math.Max(el, er);
        }
        foreach (var (size, spacing, zOff, series, loc) in new[]
                 {
                     (tT, r.TopTransverseSpacing, zTopT, "T", "Deck top transverse"),
                     (bT, r.BottomTransverseSpacing, zBotT, "B", "Deck bottom transverse"),
                 })
        {
            int n = (int)Math.Floor((dMax - dMin) / spacing + 1e-9) + 1;
            double first = dMin + ((dMax - dMin) - (n - 1) * spacing) / 2;
            Parallel.For(0, n, i =>
            {
                double d = first + i * spacing;
                var dir = TransverseDirection(d, parallel);
                var a = _a.PointAtDistance(d);
                double lb = Deck.VariableWidth ? LeftAtD(d) + c : l, rb = Deck.VariableWidth ? RightAtD(d) - c : rr;
                if (!LineAcross(a, dir, lb, rb, d, out var p0, out var p1)) return;
                if (!ClipToEnds(ref p0, ref p1, c)) return;
                var pts = SampleBar(p0, p1, zOff);
                foreach (var piece in SplitBar(pts, r.MaxBarLength, Lap(size, r.TopCover, spacing), 0))
                    bars.Add((series, size, BarShape.Straight(Length(piece)), loc, epoxy, new BarPlacement { Mark = series, Points = piece }));
            });
        }

        // ---- longitudinal bars (each line follows its offset curve between the inset deck ends)
        var tops = new List<double>();
        foreach (var (size, spacing, zOff, series, loc, cover, isTop) in new[]
                 {
                     (tL, r.TopLongitudinalSpacing, zTopL, "L", "Deck top longitudinal", r.TopCover + tT.Diameter, true),
                     (bL, r.BottomLongitudinalSpacing, zBotL, "M", "Deck bottom longitudinal", r.BottomCover + bT.Diameter, false),
                 })
        {
            var offs = EvenOffsets(l + size.Diameter, rr - size.Diameter, spacing);
            if (isTop) tops.AddRange(offs);
            double lap = Lap(size, cover, spacing);
            Parallel.For(0, offs.Count, j =>
            {
                double o = offs[j];
                var (b, e) = Ends(o, c);
                var line = _a.SamplePolyline(b, e, o, 0.01, 1.5).Select(p => p.WithZ(0)).ToList();
                var pts = line.Select(p => { var so = _a.Inverse(p.XY); return p.XY.WithZ(Deck.FinishedElevation(_a, so.Station, so.Offset) + zOff); }).ToArray();
                // Stagger laps on alternate bars by half a bar length.
                foreach (var piece in SplitBar(pts, r.MaxBarLength, lap, j % 2 == 1 ? r.MaxBarLength / 2 : r.MaxBarLength))
                    bars.Add((series, size, BarShape.Straight(Length(piece)), loc, epoxy, new BarPlacement { Mark = series, Points = piece }));
            });
        }

        // ---- negative moment (≥ 1% total longitudinal; 2/3 in the top layer) over interior supports
        if (r.NegativeMomentSteel && _def.Supports.Count > 2)
        {
            var nb = RebarCatalog.Size(r.NegativeMomentSize);
            double width = Deck.RightEdgeOffset - Deck.LeftEdgeOffset;
            double aReq = r.RequiredSteelRatio * width * t;
            int nTop = tops.Count;
            int nBot = EvenOffsets(l + bL.Diameter, rr - bL.Diameter, r.BottomLongitudinalSpacing).Count;
            double aTop = nTop * tL.Area, aBot = nBot * bL.Area;
            int addTop = (int)Math.Max(0, Math.Ceiling((2.0 / 3 * aReq - aTop) / nb.Area - 1e-9));
            int addBot = (int)Math.Max(0, Math.Ceiling((aReq - aTop - addTop * nb.Area - aBot) / nb.Area - 1e-9));
            if (nb.Diameter > In(0.75)) Warn("AASHTO 6.10.1.7 limits negative moment deck bars to #6 and smaller.");
            double sq = 0.00064516;
            for (int j = 1; j < _def.Supports.Count - 1; j++)
            {
                double dS = _a.StationToDistance(_def.Supports[j].Station);
                double back = dS - _a.StationToDistance(_def.Supports[j - 1].Station);
                double ahead = _a.StationToDistance(_def.Supports[j + 1].Station) - dS;
                double d0 = dS - r.NegativeRegionFraction * back, d1 = dS + r.NegativeRegionFraction * ahead;
                string loc = $"Deck negative moment, {_def.Supports[j].Name}";
                lock (_m.Notes)
                    _m.Notes.Add($"{_def.Supports[j].Name}: 1% of {_u.FormatLength(width)} × {_u.FormatLength(t, LengthKind.Short)} deck = {aReq / sq:F2} in²; " +
                                 $"top {aTop / sq:F2} + {addTop} {nb.Name} = {(aTop + addTop * nb.Area) / sq:F2} in² (need {2.0 / 3 * aReq / sq:F2}), " +
                                 $"bottom {aBot / sq:F2} + {addBot} {nb.Name}; region {_u.FormatStation(_a.DistanceToStation(d0))} to {_u.FormatStation(_a.DistanceToStation(d1))}.");
                foreach (var (count, refOffs, zOff, cover) in new[]
                         {
                             (addTop, tops, zTopL, r.TopCover + tT.Diameter),
                             (addBot, EvenOffsets(l + bL.Diameter, rr - bL.Diameter, r.BottomLongitudinalSpacing), zBotL, r.BottomCover + bT.Diameter),
                         })
                {
                    if (count == 0) continue;
                    var offsN = InterleavedOffsets(refOffs, count).ToList();
                    double lap = Lap(nb, cover, r.TopLongitudinalSpacing);
                    // One bar length for the whole region (sized for its longest line), bars centered on the support.
                    double longest = offsN.Max(o => Length(_a.SamplePolyline(d0, d1, o, 0.01, 1.5).Select(p => p.WithZ(0)).ToArray()));
                    int pieces = longest <= r.MaxBarLength ? 1 : (int)Math.Ceiling((longest - lap) / (r.MaxBarLength - lap) - 1e-9);
                    double barLen = Math.Ceiling((longest + (pieces - 1) * lap) / pieces / In(1)) * In(1);
                    double cov = pieces * barLen - (pieces - 1) * lap;
                    foreach (double o in offsN)
                    {
                        double margin = cov; // generous; trimmed below
                        var line = _a.SamplePolyline(dS - margin, dS + margin, o, 0.01, 1.5);
                        var pts = line.Select(p => p.WithZ(Top(_a.Inverse(p).Distance, o) + zOff)).ToArray();
                        double sMid = Length(_a.SamplePolyline(dS - margin, dS, o, 0.01, 1.5).Select(p => p.WithZ(0)).ToArray());
                        for (int q = 0; q < pieces; q++)
                        {
                            double a = sMid - cov / 2 + q * (barLen - lap);
                            var piece = SubPolyline(pts, a, a + barLen);
                            bars.Add(("N", nb, BarShape.Straight(barLen), loc, epoxy, new BarPlacement { Mark = "N", Points = piece }));
                        }
                    }
                }
            }
        }
    }

    private Vec2 TransverseDirection(double d, bool parallel)
    {
        var right = Vec2.FromAngle(_a.EvaluateAtDistance(d).angle).Right;
        if (!parallel) return right;
        // Interpolate the skew between the end lines by station.
        var (db, de) = Ends(0);
        double tt = Math.Clamp((d - db) / Math.Max(de - db, 1e-6), 0, 1);
        double sb = Math.Atan2(Vec2.Cross(right, DR.BeginLineDirection), Vec2.Dot(right, DR.BeginLineDirection));
        double se = Math.Atan2(Vec2.Cross(right, DR.EndLineDirection), Vec2.Dot(right, DR.EndLineDirection));
        return right.Rotate(sb + (se - sb) * tt);
    }

    private bool LineAcross(Vec2 a, Vec2 dir, double l, double r, double guess, out Vec2 p0, out Vec2 p1)
    {
        p0 = p1 = default;
        double w = Math.Abs(l) + Math.Abs(r) + 20;
        if (!_a.IntersectOffsetCurveWithLine(_ => l, a, dir, guess - w, guess + w, guess, out double dl)) return false;
        if (!_a.IntersectOffsetCurveWithLine(_ => r, a, dir, guess - w, guess + w, guess, out double dr)) return false;
        p0 = _a.PointAtDistance(dl, l);
        p1 = _a.PointAtDistance(dr, r);
        return true;
    }

    /// <summary>Clips a plan segment to the deck between the end lines inset by the cover.</summary>
    private bool ClipToEnds(ref Vec2 p0, ref Vec2 p1, double cover) => ClipToEnds(ref p0, ref p1, cover, cover);

    private bool ClipToEnds(ref Vec2 p0, ref Vec2 p1, double coverBegin, double coverEnd)
    {
        var nb = DR.BeginLineDirection.Left;
        var ne = -DR.EndLineDirection.Left;
        var qb = DR.BeginLinePoint + nb * coverBegin;
        var qe = DR.EndLinePoint + ne * coverEnd;
        foreach (var (q, n, begin) in new[] { (qb, nb, true), (qe, ne, false) })
        {
            if (EndPoly(begin, begin ? coverBegin : coverEnd) is { } poly) { if (!poly.Clip(ref p0, ref p1)) return false; continue; }
            double s0 = Vec2.Dot(p0 - q, n), s1 = Vec2.Dot(p1 - q, n);
            if (s0 < 0 && s1 < 0) return false;
            if (s0 < 0) p0 = p0 + (p1 - p0) * (s0 / (s0 - s1));
            else if (s1 < 0) p1 = p0 + (p1 - p0) * (s0 / (s0 - s1));
        }
        return Vec2.Distance(p0, p1) > Ft(1);
    }

    /// <summary>Bar points between two plan points following the deck surface (extra points at the crown).</summary>
    private Vec3[] SampleBar(Vec2 p0, Vec2 p1, double zOff)
    {
        var s0 = _a.Inverse(p0);
        var s1 = _a.Inverse(p1);
        var ts = new List<double> { 0, 1 };
        double piv = _a.CrossSlope.PivotOffsetAt((s0.Station + s1.Station) / 2);
        if ((s0.Offset - piv) * (s1.Offset - piv) < 0) ts.Add((piv - s0.Offset) / (s1.Offset - s0.Offset));
        int n = (int)Math.Ceiling(Vec2.Distance(p0, p1) / 3.0);
        for (int i = 1; i < n; i++) ts.Add((double)i / n);
        return ts.Distinct().OrderBy(x => x).Select(tt =>
        {
            var p = Vec2.Lerp(p0, p1, tt);
            var so = _a.Inverse(p);
            return p.WithZ(Deck.FinishedElevation(_a, so.Station, so.Offset) + zOff);
        }).ToArray();
    }

    private static List<double> EvenOffsets(double a, double b, double spacing)
    {
        int n = Math.Max(2, (int)Math.Ceiling((b - a) / spacing - 1e-9) + 1);
        return Enumerable.Range(0, n).Select(i => a + (b - a) * i / (n - 1)).ToList();
    }

    /// <summary>Offsets midway between existing bars, spread evenly across the gaps.</summary>
    private static IEnumerable<double> InterleavedOffsets(List<double> existing, int count)
    {
        var gaps = existing.Zip(existing.Skip(1), (x, y) => (x + y) / 2).ToList();
        if (gaps.Count == 0) yield break;
        for (int i = 0; i < count; i++)
        {
            int g = (int)Math.Floor((i + 0.5) * gaps.Count / count);
            if (count > gaps.Count) // more bars than gaps: second set at quarter points
            {
                int set = i / gaps.Count, gi = i % gaps.Count;
                double x = existing[gi], y = existing[gi + 1];
                yield return x + (y - x) * (set % 2 == 0 ? 0.5 : 0.25);
                continue;
            }
            yield return gaps[Math.Clamp(g, 0, gaps.Count - 1)];
        }
    }

    /// <summary>Portion of a polyline between arc lengths a and b.</summary>
    private static Vec3[] SubPolyline(Vec3[] pts, double a, double b)
    {
        var cum = new double[pts.Length];
        for (int i = 1; i < pts.Length; i++) cum[i] = cum[i - 1] + (pts[i] - pts[i - 1]).Length;
        Vec3 At(double s)
        {
            if (s <= 0) return pts[0];
            int i = Array.FindIndex(cum, c => c >= s);
            if (i < 0) return pts[^1];
            if (i == 0) return pts[0];
            return Vec3.Lerp(pts[i - 1], pts[i], (s - cum[i - 1]) / Math.Max(cum[i] - cum[i - 1], 1e-12));
        }
        var list = new List<Vec3> { At(a) };
        for (int i = 0; i < pts.Length; i++) if (cum[i] > a && cum[i] < b) list.Add(pts[i]);
        list.Add(At(b));
        return list.ToArray();
    }

    private static double Length(Vec3[] pts)
    {
        double l = 0;
        for (int i = 1; i < pts.Length; i++) l += (pts[i] - pts[i - 1]).Length;
        return l;
    }

    /// <summary>
    /// Splits a bar polyline into lap-spliced pieces no longer than <paramref name="maxLen"/>. With
    /// <paramref name="firstPiece"/> = 0 the pieces are equal; otherwise stock-length pieces follow a first piece of that
    /// length (used to stagger laps on alternate bars) with a closure piece at the end.
    /// </summary>
    private static IEnumerable<Vec3[]> SplitBar(Vec3[] pts, double maxLen, double lap, double firstPiece)
    {
        double total = Length(pts);
        if (total <= maxLen + 1e-6) { yield return pts; yield break; }
        if (firstPiece <= 0)
        {
            int n = (int)Math.Ceiling((total - lap) / (maxLen - lap) - 1e-9);
            double piece = Math.Ceiling((total + (n - 1) * lap) / n / In(1)) * In(1);
            firstPiece = piece;
            maxLen = piece;
        }
        var cum = new double[pts.Length];
        for (int i = 1; i < pts.Length; i++) cum[i] = cum[i - 1] + (pts[i] - pts[i - 1]).Length;
        Vec3 At(double s)
        {
            int i = Array.FindIndex(cum, c => c >= s);
            if (i <= 0) return pts[0];
            double f = (s - cum[i - 1]) / Math.Max(cum[i] - cum[i - 1], 1e-12);
            return Vec3.Lerp(pts[i - 1], pts[i], f);
        }
        Vec3[] Sub(double a, double b)
        {
            var list = new List<Vec3> { At(a) };
            for (int i = 0; i < pts.Length; i++) if (cum[i] > a && cum[i] < b) list.Add(pts[i]);
            list.Add(At(b));
            return list.ToArray();
        }
        double start = 0;
        double len = firstPiece > lap ? firstPiece : maxLen;
        while (start + len < total - 1e-6)
        {
            yield return Sub(start, start + len);
            start = start + len - lap;
            len = maxLen;
        }
        // Avoid a uselessly short last piece: rebalance the last two.
        yield return Sub(start, total);
    }

    // ================================================================== TxDOT SSTR rail reinforcement

    /// <summary>Bars S, U (#4 at 6" max) and 8 Bars R (#4) per TxDOT RL-SSTR "on bridge slab".</summary>
    private void BuildSstrRebar(ConcurrentBag<(string, RebarSize, BarShape, string, bool, BarPlacement)> bars)
    {
        var b4 = RebarCatalog.Size("#4");
        double db = b4.Diameter;
        bool epoxy = Deck.Barrier.EpoxyCoated;
        var (sShape, uShape, rBack, rFront) = SstrShapes(db);
        double lapR = epoxy ? In(29) : In(19); // standard: #4 laps 2'-5" epoxy / 1'-7" uncoated
        lock (_m.Notes) _m.Notes.Add($"TxDOT SSTR rail: Bars S and U #4 at {_u.FormatLength(Deck.Barrier.StirrupSpacing, LengthKind.Short)} max, 8 Bars R #4, laps {_u.FormatLength(lapR)} (RL-SSTR).");

        foreach (var (side, on) in new[] { (-1, Deck.Barrier.Left), (1, Deck.Barrier.Right) })
        {
            if (!on) continue;
            Func<double, double> edgeAt = side < 0 ? LeftAtD : RightAtD;
            double edge = edgeAt(0);
            string loc = side < 0 ? "Left rail" : "Right rail";
            var (b, e) = Deck.VariableWidth ? EndsVar(edgeAt, In(2)) : Ends(edge, In(2));
            // Stirrups at equal spaces ≤ max along the rail back face.
            var line = _a.SamplePolyline(b, e, edge, 0.01, 0.5);
            double len = 0;
            for (int i = 1; i < line.Count; i++) len += Vec2.Distance(line[i - 1], line[i]);
            int n = (int)Math.Ceiling(len / Deck.Barrier.StirrupSpacing - 1e-9) + 1;
            for (int i = 0; i < n; i++)
            {
                double d = b + (e - b) * i / (n - 1);
                var (p, ang) = _a.EvaluateAtDistance(d);
                var right = Vec2.FromAngle(ang).Right;
                double ed = edgeAt(d);
                var o = p + right * ed;
                var inward = right * -side;
                var x = new Vec3(inward.X, inward.Y, 0);
                var frame = new Frame3(o.WithZ(Top(d, ed)), x, Up, Vec3.Cross(x, Up));
                foreach (var (series, shape) in new[] { ("S", sShape), ("U", uShape) })
                {
                    var pts = shape.Centerline.Select(q => frame.ToWorld(q.X, q.Y, 0)).ToArray();
                    bars.Add((series, b4, shape, loc, epoxy, new BarPlacement { Mark = series, Points = pts, Frame = frame, Group = "Rail" }));
                }
            }
            // Bars R: continuous longitudinals inside Bars S, lapped.
            foreach (var q in rBack.Concat(rFront))
            {
                double o = edge - side * q.X;
                Vec3[] pts;
                if (Deck.VariableWidth)
                {
                    var (rb, re) = EndsVar(d => edgeAt(d) - side * q.X, In(2));
                    int nn = Math.Max(2, (int)Math.Ceiling((re - rb) / 1.5));
                    pts = Enumerable.Range(0, nn + 1).Select(k =>
                    {
                        double d = rb + (re - rb) * k / nn;
                        return _a.PointAtDistance(d, edgeAt(d) - side * q.X).WithZ(Top(d, edgeAt(d)) + q.Y);
                    }).ToArray();
                }
                else
                {
                    var (rb, re) = Ends(o, In(2));
                    var pl = _a.SamplePolyline(rb, re, o, 0.01, 1.5);
                    pts = pl.Select(p2 => p2.WithZ(Top(_a.Inverse(p2).Distance, edge) + q.Y)).ToArray();
                }
                foreach (var piece in SplitBar(pts, Deck.Reinforcement.MaxBarLength, lapR, 0))
                    bars.Add(("R", b4, BarShape.Straight(Length(piece)), loc, epoxy, new BarPlacement { Mark = "R", Points = piece, Group = "Rail" }));
            }
        }
    }

    /// <summary>
    /// SSTR bar shapes in rail coordinates (x inward from the back face, y up from the top of slab), centerlines.
    /// Bars S: back leg 1 1/2" clear of the back face, legs joined over a 2 1/2" pin, top 1 1/4" below the rail top,
    /// sloped leg parallel to the traffic face, bottom of back leg on the slab, sloped leg 1/2" up.
    /// Bars U: hairpin over a 3 3/4" pin, legs 4 3/4" out-to-out, 1'-4 1/2" tall, 8" foot into the slab under the top mat.
    /// </summary>
    private (BarShape s, BarShape u, Vec2[] rBack, Vec2[] rFront) SstrShapes(double db)
    {
        double k = In(0.75), cov = In(1.5);
        // ---- Bars S
        double xb = k + cov + db / 2;
        double rS = In(2.5) / 2 + db / 2;
        double yTop = In(34.75) - db / 2;
        var c = new Vec2(xb + rS, yTop - rS);
        var down = new Vec2(In(7), -In(36)).Normalized();     // sloped leg direction going down (parallel to traffic face)
        var n = new Vec2(-down.Y, down.X);                     // normal pointing away from the back leg (+x)
        if (n.X < 0) n = -n;
        double cst = Vec2.Dot(n, c) + rS;                      // sloped leg line: n·p = cst
        double yApex = (cst - n.X * xb) / n.Y;
        var apex = new Vec2(xb, yApex);
        double yBot = In(0.5) + db / 2;
        var bottom = new Vec2((cst - n.Y * yBot) / n.X, yBot);
        var sPts = BarShape.Fillet(new Vec2(xb, db / 2), apex, bottom, rS);
        var s = new BarShape { Code = "SSTR-S", Description = "Rail stirrup (Bars S)", Centerline = sPts, PinDiameter = In(2.5) };
        double outX0 = xb - db / 2, outX1 = bottom.X + db / 2;
        s.Dimensions.Add(new BarDimension("A", In(34.75), new Vec2(outX0, 0), new Vec2(outX0, In(34.75)), new Vec2(-1, 0)));
        s.Dimensions.Add(new BarDimension("B", In(34.75) - In(0.5), new Vec2(outX1, In(0.5)), new Vec2(outX1, In(34.75)), new Vec2(1, 0)));
        s.Dimensions.Add(new BarDimension("C", outX1 - outX0, new Vec2(outX0, 0), new Vec2(outX1, 0), new Vec2(0, -1)));

        // ---- Bars U
        var rd = Deck.Reinforcement;
        double yFoot = -(rd.TopCover + RebarCatalog.Size(rd.TopTransverseSize).Diameter + db / 2);
        double rU = In(3.75) / 2 + db / 2;
        double x1 = xb + db + In(0.5) + db / 2, x2 = x1 + 2 * rU;
        double yTopU = yFoot - db / 2 + In(16.5) - db / 2;
        var uPts = new List<Vec2> { new(x1, yFoot) };
        var cu = new Vec2((x1 + x2) / 2, yTopU - rU);
        for (int i = 0; i <= 12; i++) uPts.Add(cu + Vec2.FromAngle(Math.PI - Math.PI * i / 12) * rU);
        var footCorner = new Vec2(x2, yFoot);
        var footEnd = new Vec2(x2 - db / 2 + In(8) - db / 2, yFoot);
        var tail = BarShape.Fillet(new Vec2(x2, cu.Y), footCorner, footEnd, 3 * db + db / 2);
        uPts.AddRange(tail.Skip(1));
        var u = new BarShape { Code = "SSTR-U", Description = "Rail anchor (Bars U)", Centerline = uPts.ToArray(), PinDiameter = In(3.75) };
        u.Dimensions.Add(new BarDimension("A", In(16.5), new Vec2(x1 - db / 2, yFoot - db / 2), new Vec2(x1 - db / 2, yTopU + db / 2), new Vec2(-1, 0)));
        u.Dimensions.Add(new BarDimension("B", 2 * rU + db, new Vec2(x1 - db / 2, yTopU + db / 2), new Vec2(x2 + db / 2, yTopU + db / 2), new Vec2(0, 1)));
        u.Dimensions.Add(new BarDimension("C", In(8), new Vec2(x2 - db / 2, yFoot - db / 2), new Vec2(x2 - db / 2 + In(8), yFoot - db / 2), new Vec2(0, -1)));

        // ---- Bars R: 4 inside the back leg (top 2 3/4" below rail top), 4 inside the sloped leg (top 4 1/4" below).
        double rx = xb + db;
        var back = Enumerable.Range(0, 4).Select(i => new Vec2(rx, In(36) - In(2.75) - i * (In(36) - In(2.75) - In(3)) / 3)).ToArray();
        var front = Enumerable.Range(0, 4).Select(i =>
        {
            double y = In(36) - In(4.25) - i * (In(36) - In(4.25) - In(3.5)) / 3;
            double xs = (cst - n.Y * y) / n.X;                 // sloped leg centerline at y
            return new Vec2(xs - db, y);
        }).ToArray();
        return (s, u, back, front);
    }

    // ================================================================== marks

    /// <summary>
    /// Bar marks: bars of one size, shape and length (to the nearest inch) share a mark when there are at least
    /// <c>MinRepeat</c> of them; the remaining odd lengths of straight bars (closures at skewed or curved ends) are
    /// grouped into "varies" marks spanning at most 3 ft, listed with their individual lengths.
    /// </summary>
    private void AssignMarks(ConcurrentBag<(string series, RebarSize size, BarShape shape, string loc, bool epoxy, BarPlacement placement)> bars)
    {
        const int MinRepeat = 4;
        double tolIn = Math.Round(Deck.Reinforcement.MarkLengthTolerance / In(1));
        // Parallel producers: fixed geometric order first, so ties in the length sorts (and the bar numbering) are reproducible.
        foreach (var series in InStableOrder(bars, b => b.placement.Points).GroupBy(b => b.series).OrderBy(g => SeriesOrder(g.Key)))
        {
            // Snap straight bar lengths within the grouping tolerance to the shortest length of their cluster.
            var snapped = new Dictionary<BarPlacement, double>();
            foreach (var bySize in series.Where(b => b.shape.Code == "STR").GroupBy(b => b.size.Name))
            {
                double? clusterTop = null, clusterMin = null;
                var cluster = new List<BarPlacement>();
                void Flush() { foreach (var p in cluster) snapped[p] = clusterMin!.Value; cluster.Clear(); }
                foreach (var b in bySize.OrderByDescending(b => b.shape.DevelopedLength))
                {
                    double len = Math.Round(b.shape.DevelopedLength / In(1));
                    if (clusterTop != null && clusterTop - len > tolIn) { Flush(); clusterTop = null; }
                    clusterTop ??= len;
                    clusterMin = len;
                    cluster.Add(b.placement);
                }
                if (cluster.Count > 0) Flush();
            }
            double Len(BarShape s, BarPlacement p) => s.Code == "STR" && snapped.TryGetValue(p, out double v) ? v : Math.Round(s.DevelopedLength / In(1));
            var groups = series.GroupBy(b => (b.size.Name, Len: Len(b.shape, b.placement), b.shape.Code))
                .OrderByDescending(g => g.Key.Len).ToList();
            var fixedGroups = groups.Where(g => g.Count() >= MinRepeat || g.Key.Code != "STR").ToList();
            var odd = groups.Except(fixedGroups).SelectMany(g => g).OrderByDescending(b => b.shape.DevelopedLength).ToList();
            var varyGroups = new List<List<(string series, RebarSize size, BarShape shape, string loc, bool epoxy, BarPlacement placement)>>();
            foreach (var b in odd)
            {
                var cur = varyGroups.LastOrDefault();
                if (cur == null || cur[0].size.Name != b.size.Name || cur[0].shape.DevelopedLength - b.shape.DevelopedLength > Ft(3)) varyGroups.Add(cur = new());
                cur.Add(b);
            }
            int total = fixedGroups.Count + varyGroups.Count, k = 1;
            string Name() => total == 1 ? series.Key : $"{series.Key}{k++}";
            string Loc(IEnumerable<string> locs) => string.Join(", ", locs.Distinct().OrderBy(x => x));

            foreach (var g in fixedGroups)
            {
                string mark = Name();
                var first = g.First();
                var shape = first.shape.Code == "STR" ? BarShape.Straight(g.Key.Len * In(1)) : first.shape;
                _m.Marks.Add(new BarMark { Mark = mark, Size = first.size, Shape = shape, Location = Loc(g.Select(x => x.loc)), Epoxy = first.epoxy, Count = g.Count() });
                foreach (var b in g)
                    _m.Bars.Add(new BarPlacement { Mark = mark, Points = b.placement.Points, Frame = b.placement.Frame, Group = b.placement.Group });
            }
            foreach (var g in varyGroups)
            {
                string mark = Name();
                var first = g[0];
                var lengths = g.Select(b => Len(b.shape, b.placement) * In(1)).ToList();
                _m.Marks.Add(new BarMark
                {
                    Mark = mark, Size = first.size, Shape = BarShape.Straight(lengths.Max()), Location = Loc(g.Select(x => x.loc)), Epoxy = first.epoxy,
                    Count = g.Count, VaryingLengths = lengths.Count > 1 ? lengths : null,
                });
                foreach (var b in g)
                    _m.Bars.Add(new BarPlacement { Mark = mark, Points = b.placement.Points, Frame = b.placement.Frame, Group = b.placement.Group });
            }
        }
    }

    /// <summary>Items from a concurrent collection in a run-independent order: by first, then last centerline point.</summary>
    private static IOrderedEnumerable<T> InStableOrder<T>(IEnumerable<T> items, Func<T, Vec3[]> pts) =>
        items.OrderBy(b => pts(b)[0].X).ThenBy(b => pts(b)[0].Y).ThenBy(b => pts(b)[0].Z)
            .ThenBy(b => pts(b)[^1].X).ThenBy(b => pts(b)[^1].Y).ThenBy(b => pts(b)[^1].Z).ThenBy(b => pts(b).Length);

    private static int SeriesOrder(string s) => s switch { "T" => 0, "B" => 1, "L" => 2, "M" => 3, "N" => 4, "S" => 5, "U" => 6, "R" => 7, _ => 9 };
}
