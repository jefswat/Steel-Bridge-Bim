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

public sealed class ConcreteModel
{
    public Mesh? Deck { get; set; }
    public Mesh? Haunches { get; set; }
    public List<(string Name, Mesh Mesh)> Barriers { get; } = new();
    public List<BarMark> Marks { get; } = new();
    public List<BarPlacement> Bars { get; } = new();
    public List<string> Notes { get; } = new();
    public List<Diagnostic> Diagnostics { get; } = new();
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
public sealed class ConcreteGenerator
{
    private static readonly Vec3 Up = new(0, 0, 1);
    private readonly BridgeFraming _f;
    private readonly BridgeDefinition _def;
    private readonly Alignment _a;
    private readonly UnitSystem _u;
    private readonly ConcreteModel _m = new();
    private readonly ConcurrentQueue<Diagnostic> _diag = new();
    private readonly ConcurrentDictionary<long, (double b, double e)> _ends = new();

    private ConcreteGenerator(BridgeFraming f, UnitSystem u)
    {
        _f = f;
        _def = f.Definition;
        _a = f.Alignment;
        _u = u;
    }

    public static ConcreteModel Generate(BridgeFraming framing, UnitSystem units)
    {
        var g = new ConcreteGenerator(framing, units);
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
        var tasks = new List<Action> { BuildDeckSolid, BuildHaunches, BuildBarriers };
        Parallel.ForEach(tasks, t => t());
        var bars = new ConcurrentBag<(string series, RebarSize size, BarShape shape, string loc, bool epoxy, BarPlacement placement)>();
        var rebarTasks = new List<Action>();
        if (Deck.Reinforcement.Enabled) rebarTasks.Add(() => BuildDeckRebar(bars));
        if (Deck.Barrier.Type == BarrierType.TxDotSstr && Deck.Barrier.Reinforcement) rebarTasks.Add(() => BuildSstrRebar(bars));
        Parallel.ForEach(rebarTasks, t => t());
        AssignMarks(bars);
    }

    // ================================================================== surfaces & end lines

    private double Top(double d, double o) => _a.ElevationAt(_a.DistanceToStation(d), o);

    /// <summary>Alignment distances where the offset curve crosses the begin/end deck lines (optionally inset).</summary>
    private (double b, double e) Ends(double o, double inset = 0)
    {
        long key = (long)Math.Round(o * 1e4) * 1000 + (long)Math.Round(inset * 1e4);
        return _ends.GetOrAdd(key, _ =>
        {
            var nb = DR.BeginLineDirection.Left;           // toward the deck (up-station)
            var ne = -DR.EndLineDirection.Left;
            double db = Cross(DR.BeginLinePoint + nb * inset, DR.BeginLineDirection, o);
            double de = Cross(DR.EndLinePoint + ne * inset, DR.EndLineDirection, o);
            return (db, de);
        });
    }

    /// <summary>Deck edges at an alignment distance (variable width).</summary>
    private double LeftAtD(double d) => Deck.LeftAt(_a.DistanceToStation(d));
    private double RightAtD(double d) => Deck.RightAt(_a.DistanceToStation(d));

    /// <summary>Alignment distances where a variable offset curve crosses the begin/end deck lines (optionally inset).</summary>
    private (double b, double e) EndsVar(Func<double, double> offAtD, double inset = 0)
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
        return (One(DR.BeginLinePoint + nb * inset, DR.BeginLineDirection), One(DR.EndLinePoint + ne * inset, DR.EndLineDirection));
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
        double piv = _a.CrossSlope.PivotOffset;
        if (piv > l && piv < r) list.Add(piv);
        return list.Distinct().OrderBy(x => x).ToList();
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
        var (b0, e0) = Ends(0);
        int ni = AlongSamples(e0 - b0) + 1;
        var top = new Vec3[ni, offs.Count];
        var bot = new Vec3[ni, offs.Count];
        for (int k = 0; k < offs.Count; k++)
        {
            var (db, de) = Ends(offs[k]);
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
    }

    /// <summary>Variable width: columns at fixed fractions of the width between the edges at each station.</summary>
    private void BuildVariableDeckSolid()
    {
        double wMax = Deck.MaxRight - Deck.MinLeft;
        int nk = Math.Max(2, (int)Math.Ceiling(wMax / 1.0)) + 1;
        var fr = Enumerable.Range(0, nk).Select(k => (double)k / (nk - 1)).ToArray();
        var (b0, e0) = Ends(0);
        int ni = AlongSamples(e0 - b0) + 1;
        var top = new Vec3[ni, nk];
        var bot = new Vec3[ni, nk];
        for (int k = 0; k < nk; k++)
        {
            double t = fr[k];
            double Off(double d) => LeftAtD(d) + (RightAtD(d) - LeftAtD(d)) * t;
            var (db, de) = EndsVar(Off);
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
    }

    private void BuildHaunches()
    {
        var mesh = new Mesh();
        foreach (var g in _f.Girders)
        {
            var path = g.Path;
            var ss = path.S.ToList();
            if (ss.Count < 2) continue;
            var rings = new List<Vec3[]>(ss.Count);
            Vec2[]? sec = null;
            foreach (double s in ss)
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

    private double DeckBottomAt(Vec2 p)
    {
        var so = _a.Inverse(p);
        return _a.ElevationAt(so.Station, so.Offset) - Deck.Thickness;
    }

    private void BuildBarriers()
    {
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
                var pts = line.Select(p => { var so = _a.Inverse(p.XY); return p.XY.WithZ(_a.ElevationAt(so.Station, so.Offset) + zOff); }).ToArray();
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
    private bool ClipToEnds(ref Vec2 p0, ref Vec2 p1, double cover)
    {
        var nb = DR.BeginLineDirection.Left;
        var ne = -DR.EndLineDirection.Left;
        var qb = DR.BeginLinePoint + nb * cover;
        var qe = DR.EndLinePoint + ne * cover;
        foreach (var (q, n) in new[] { (qb, nb), (qe, ne) })
        {
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
        double piv = _a.CrossSlope.PivotOffset;
        if ((s0.Offset - piv) * (s1.Offset - piv) < 0) ts.Add((piv - s0.Offset) / (s1.Offset - s0.Offset));
        int n = (int)Math.Ceiling(Vec2.Distance(p0, p1) / 3.0);
        for (int i = 1; i < n; i++) ts.Add((double)i / n);
        return ts.Distinct().OrderBy(x => x).Select(tt =>
        {
            var p = Vec2.Lerp(p0, p1, tt);
            var so = _a.Inverse(p);
            return p.WithZ(_a.ElevationAt(so.Station, so.Offset) + zOff);
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
        foreach (var series in bars.GroupBy(b => b.series).OrderBy(g => SeriesOrder(g.Key)))
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

    private static int SeriesOrder(string s) => s switch { "T" => 0, "B" => 1, "L" => 2, "M" => 3, "N" => 4, "S" => 5, "U" => 6, "R" => 7, _ => 9 };
}
