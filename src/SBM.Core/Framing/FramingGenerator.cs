using System.Collections.Concurrent;
using System.Diagnostics;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Geometry;

namespace SBM.Core.Framing;

/// <summary>
/// Turns a parametric <see cref="BridgeDefinition"/> into explicit framing geometry along an <see cref="Alignment"/>.
/// Pure function of its inputs; independent pieces (girders, bays) are generated in parallel.
/// </summary>
public sealed class FramingGenerator
{
    private readonly BridgeDefinition _def;
    private readonly Alignment _align;
    private readonly BridgeFraming _result;
    private readonly ConcurrentQueue<Diagnostic> _diag = new();

    private double[] _dSup = Array.Empty<double>();
    private double[][] _offsets = Array.Empty<double[]>(); // [support][girder]
    private Vec2[] _supDir = Array.Empty<Vec2>();
    private GirderSpanCurve[][] _curves = Array.Empty<GirderSpanCurve[]>(); // [girder][span]
    private double[] _startExt = Array.Empty<double>(); // per girder: extension before first bearing
    private GirderPlanCurve?[] _plan = Array.Empty<GirderPlanCurve?>(); // per girder: as-designed plan geometry (null = offset curve)

    private FramingGenerator(BridgeDefinition def, Alignment align)
    {
        _def = def;
        _align = align;
        _result = new BridgeFraming { Definition = def, Alignment = align };
    }

    public static BridgeFraming Generate(BridgeDefinition def, Alignment align)
    {
        var g = new FramingGenerator(def, align);
        var sw = Stopwatch.StartNew();
        try
        {
            g.Run();
        }
        catch (Exception ex) when (ex is AlignmentException or InvalidOperationException or ArgumentException)
        {
            g.Error(ex.Message);
        }
        foreach (var d in g._diag) g._result.Diagnostics.Add(d);
        g._result.GenerationTime = sw.Elapsed;
        return g._result;
    }

    private void Warn(string m) => _diag.Enqueue(new Diagnostic(DiagnosticSeverity.Warning, m));
    private void Error(string m) => _diag.Enqueue(new Diagnostic(DiagnosticSeverity.Error, m));

    private int NG => _def.Girders.Count;
    private int NS => _def.Supports.Count;
    private int NSpan => NS - 1;


    private void Run()
    {
        foreach (var e in _def.Validate()) Error(e);
        if (_align.Elements.Count == 0) Error("The alignment has no horizontal geometry.");
        if (!_diag.IsEmpty && _diag.Any(d => d.Severity == DiagnosticSeverity.Error)) return;

        SetupSupports();
        ComputeBearings();
        if (_diag.Any(d => d.Severity == DiagnosticSeverity.Error)) return;
        BuildGirders();
        if (_diag.Any(d => d.Severity == DiagnosticSeverity.Error)) return;
        ApplyTerminations();
        ApplyBeginnings();
        if (!PS)
        {
            // Prestressed concrete girders have no cross frames, stiffeners or field splices.
            LocateSplices();
            BuildCrossFrames();
            BuildStiffeners();
            BuildSplices();
            BuildPlateRuns();
        }
        BuildDeck();
    }

    // ------------------------------------------------------------------ supports & offsets

    private void SetupSupports()
    {
        _dSup = _def.Supports.Select(s => _align.StationToDistance(s.Station)).ToArray();
        for (int j = 0; j < NS; j++)
        {
            if (_dSup[j] < 0 || _dSup[j] > _align.Length)
                Warn($"{_def.Supports[j].Name} is beyond the end of alignment '{_align.Name}'; the end tangent is extended.");
        }

        var uniform = _def.Girders.UniformOffsets();
        _offsets = new double[NS][];
        for (int j = 0; j < NS; j++)
        {
            _offsets[j] = _def.Girders.OffsetsAtSupports is { } tbl ? tbl[j].ToArray() : uniform.ToArray();
        }

        _supDir = new Vec2[NS];
        for (int j = 0; j < NS; j++)
        {
            var (_, ang) = _align.EvaluateAtDistance(_dSup[j]);
            _supDir[j] = Vec2.FromAngle(ang).Right.Rotate(Numerics.DegToRad(_def.Supports[j].SkewDegrees));
        }

        // As-designed plan geometry replaces the offset curve of those girders; the offsets at the supports then
        // follow from it (they are only the starting guesses for the bearing intersections and the stiffener side).
        _plan = new GirderPlanCurve?[NG];
        foreach (var pg in _def.Girders.PlanGeometry)
        {
            if (pg.Girder < 0 || pg.Girder >= NG || pg.Elements.Count == 0) continue;
            var curve = new GirderPlanCurve(_align, pg);
            _plan[pg.Girder] = curve;
            for (int j = 0; j < NS; j++) _offsets[j][pg.Girder] = curve.OffsetAt(_dSup[j]);
        }
    }

    /// <summary>Girder offset at an alignment distance (linear between supports, constant beyond).</summary>
    private double OffsetAt(int girder, double d)
    {
        if (_plan[girder] is { } pc) return pc.OffsetAt(d);
        if (d <= _dSup[0]) return _offsets[0][girder];
        if (d >= _dSup[^1]) return _offsets[^1][girder];
        int j = 0;
        while (j < NS - 2 && d > _dSup[j + 1]) j++;
        double t = (d - _dSup[j]) / (_dSup[j + 1] - _dSup[j]);
        return _offsets[j][girder] + (_offsets[j + 1][girder] - _offsets[j][girder]) * t;
    }

    private double DeckElevation(double d, double offset) => _align.ElevationAt(_align.DistanceToStation(d), offset);

    private double DeckElevationAtPoint(Vec2 p)
    {
        var so = _align.Inverse(p);
        return _align.ElevationAt(so.Station, so.Offset);
    }

    private bool IntersectSupportWithOffsetCurve(int support, Func<double, double> offsetAt, double offsetGuess, Vec2 linePoint, out double d)
    {
        double skew = Numerics.DegToRad(_def.Supports[support].SkewDegrees);
        double guess = _dSup[support] + offsetGuess * Math.Tan(skew);
        double w = Math.Abs(offsetGuess) * (Math.Abs(Math.Tan(skew)) + 0.5) + 10;
        return _align.IntersectOffsetCurveWithLine(offsetAt, linePoint, _supDir[support], guess - w, guess + w, guess, out d);
    }

    private GirderSection Sec => _def.Girders.Section;

    /// <summary>Deck surface to top of web: slab + haunch (haunch is measured from top of web to bottom of deck).</summary>
    private double WebTopDrop => _def.Deck.Thickness + _def.Deck.Haunch;

    /// <summary>Prestressed girders: "top of web" = top of beam (no flange plates), the "web" = full depth, and each
    /// span's beam is straight (chorded in plan, straight in profile between its support-line points).</summary>
    private bool PS => _def.Girders.IsPrestressed;
    private Prestressed.TxGirderShape PsShape => Prestressed.TxGirderCatalog.Get(_def.Girders.Prestressed);

    private double TopFlangeT(double station) => PS ? 0 : Sec.SegmentAt(GirderPlate.TopFlange, station).Thickness;
    private double TopFlangeW(double station) => PS ? PsShape.TopWidth : Sec.SegmentAt(GirderPlate.TopFlange, station).Width;
    private double BottomFlangeT(double station) => PS ? 0 : Sec.SegmentAt(GirderPlate.BottomFlange, station).Thickness;

    private void ComputeBearings()
    {
        double dWeb = PS ? PsShape.Depth : Sec.WebDepth;
        for (int j = 0; j < NS; j++)
        {
            var sup = _def.Supports[j];
            var a = _align.PointAtDistance(_dSup[j]);
            var bearings = new BearingResult?[NG];
            int jj = j;
            Parallel.For(0, NG, i =>
            {
                if (!IntersectSupportWithOffsetCurve(jj, x => OffsetAt(i, x), _offsets[jj][i], a, out double d))
                {
                    Error($"G{i + 1} does not intersect the {sup.Name} support line.");
                    return;
                }
                double off = OffsetAt(i, d);
                double sta = _align.DistanceToStation(d);
                var p = _align.PointAtDistance(d, off);
                double deck = DeckElevation(d, off);
                double webTop = deck - WebTopDrop;
                bearings[i] = new BearingResult
                {
                    Girder = i, Support = jj, Station = sta, Offset = off,
                    TopOfSteel = p.WithZ(webTop + TopFlangeT(sta)),
                    BottomOfSteelElevation = webTop - dWeb - BottomFlangeT(sta), DeckElevation = deck,
                };
            });
            _result.Supports.Add(new SupportLineResult
            {
                Index = j, Name = sup.Name, Type = sup.Type, Station = sup.Station, SkewDegrees = sup.SkewDegrees,
                AlignmentPoint = a, Direction = _supDir[j], Bearings = bearings,
            });
        }
    }

    // ------------------------------------------------------------------ girders

    /// <summary>Top-of-web elevation and alignment station at a girder point.</summary>
    private (double webTop, double station) WebTopAt(GirderSpanCurve c, double u)
    {
        if (PS)
        {
            // Straight beam between the support-line points (haunch is set at the supports; no camber).
            double z0 = _result.Supports[c.Span].Bearings[c.Girder]!.TopOfSteel.Z;
            double z1 = _result.Supports[c.Span + 1].Bearings[c.Girder]!.TopOfSteel.Z;
            double t = c.Length > 0 ? c.SAt(u) / c.Length : u;
            return (z0 + (z1 - z0) * t, _align.Inverse(c.PointAt(u)).Station);
        }
        if (!c.Chorded)
        {
            double d = c.DistanceAt(u);
            return (DeckElevation(d, c.OffsetAtU(u)) - WebTopDrop, _align.DistanceToStation(d));
        }
        var so = _align.Inverse(c.PointAt(u));
        return (_align.ElevationAt(so.Station, so.Offset) - WebTopDrop, so.Station);
    }

    private (double webTop, double station) WebTopAtPoint(Vec2 p)
    {
        var so = _align.Inverse(p);
        return (_align.ElevationAt(so.Station, so.Offset) - WebTopDrop, so.Station);
    }

    /// <summary>Top of steel (top of top flange) at web centerline.</summary>
    private Vec3 Point3(GirderSpanCurve c, double u)
    {
        var (wt, sta) = WebTopAt(c, u);
        return c.PointAt(u).WithZ(wt + TopFlangeT(sta));
    }

    private void BuildGirders()
    {
        bool chorded = _def.Girders.PlanLayout == GirderPlanLayout.Chorded || PS;
        _curves = new GirderSpanCurve[NG][];
        _startExt = new double[NG];
        var girders = new GirderResult[NG];

        Parallel.For(0, NG, i =>
        {
            var spans = new GirderSpanCurve[NSpan];
            for (int j = 0; j < NSpan; j++)
            {
                double d0 = _alignDistanceOfBearing(i, j), d1 = _alignDistanceOfBearing(i, j + 1);
                if (d1 <= d0 + 0.01)
                {
                    Error($"G{i + 1}: bearings at {_def.Supports[j].Name} and {_def.Supports[j + 1].Name} cross or coincide (check skews and span lengths).");
                    return;
                }
                int ii = i;
                spans[j] = new GirderSpanCurve(_align, d => OffsetAt(ii, d), i, j, d0, d1, chorded);
            }
            _curves[i] = spans;

            var first = _def.Supports[0];
            var last = _def.Supports[^1];
            double endExt = PS ? _def.Girders.Prestressed.BearingToEnd : 0;
            // Girders run past the CL bearing at both ends of the bridge (abutments and the expansion piers of a unit).
            double ext0 = PS ? endExt : first.GirderEndExtension;
            double ext1 = PS ? endExt : last.GirderEndExtension;
            _startExt[i] = ext0;

            var path = new GirderPath();
            var web = new List<Vec3>();
            var left = new List<Vec3>();
            var right = new List<Vec3>();

            void AddSample(double s, Vec2 p, Vec2 t, double webTop, double sta)
            {
                if (path.Count > 0 && s <= path.S[^1] + 1e-6) return;
                path.Add(s, p.WithZ(webTop), t, sta);
                double tf = TopFlangeT(sta);
                var top = p.WithZ(webTop + tf);
                web.Add(top);
                var r = t.Right * (TopFlangeW(sta) / 2);
                left.Add(new Vec3(top.X - r.X, top.Y - r.Y, top.Z));
                right.Add(new Vec3(top.X + r.X, top.Y + r.Y, top.Z));
            }

            var c0 = spans[0];
            var t0 = c0.TangentAt(0);
            var startPt = c0.Start - t0 * ext0;
            var (wt0, st0) = WebTopAtPoint(startPt);
            if (PS) wt0 = WebTopAt(c0, 0).webTop - (WebTopAt(c0, 1).webTop - WebTopAt(c0, 0).webTop) / c0.Length * ext0;
            AddSample(0, startPt, t0, wt0, st0);

            double sAcc = ext0;
            for (int j = 0; j < NSpan; j++)
            {
                var c = spans[j];
                var pts = c.Sample(8, 0.01, 1.5);
                int n = pts.Count - 1;
                for (int k = 0; k <= n; k++)
                {
                    double u = (double)k / n;
                    var (wt, sta) = WebTopAt(c, u);
                    AddSample(sAcc + c.SAt(u), pts[k], c.TangentAt(u), wt, sta);
                }
                sAcc += c.Length;
            }

            var cn = spans[^1];
            var tn = cn.TangentAt(1);
            var endPt = cn.End + tn * ext1;
            var (wt1, st1) = WebTopAtPoint(endPt);
            if (PS) wt1 = WebTopAt(cn, 1).webTop + (WebTopAt(cn, 1).webTop - WebTopAt(cn, 0).webTop) / cn.Length * ext1;
            AddSample(sAcc + ext1, endPt, tn, wt1, st1);

            girders[i] = new GirderResult
            {
                Index = i, Spans = spans.ToList(), WebLine = web, TopFlangeLeft = left, TopFlangeRight = right,
                StartEnd = web[0], EndEnd = web[^1], Length = sAcc + ext1, Path = path, StartExtension = ext0,
            };
        });

        if (girders.Any(g => g == null)) return;
        _result.Girders.AddRange(girders);
    }

    private double _alignDistanceOfBearing(int girder, int support)
    {
        var b = _result.Supports[support].Bearings[girder]!;
        return _align.StationToDistance(b.Station);
    }

    /// <summary>Distance from the girder start end to the CL bearing at the start of a span.</summary>
    private double SpanStart(int girder, int span)
    {
        double s = _startExt[girder];
        for (int j = 0; j < span; j++) s += _curves[girder][j].Length;
        return s;
    }

    /// <summary>Distance along girder i from its start end (including the end extension).</summary>
    private double GirderDistance(int girder, int span, double u)
    {
        double s = _startExt[girder];
        for (int j = 0; j < span; j++) s += _curves[girder][j].Length;
        return s + _curves[girder][span].SAt(u);
    }

    // ------------------------------------------------------------------ discontinuous girders

    private double[] _endS = Array.Empty<double>();   // per girder: path distance of the girder end
    private double[] _endD = Array.Empty<double>();   // per girder: alignment distance of the girder end (CL cross girder)

    private double EndS(int g) => _endS.Length > 0 ? _endS[g] : double.MaxValue;
    private bool AliveAtSpanStart(int g, int span) => EndS(g) > SpanStart(g, span) + 1.0;
    /// <summary>True when girder g ends before the start of span <paramref name="span"/> (span = NSpan: before the bridge end).</summary>
    private bool? EndsBefore(int g, int span) => _endS.Length == 0 ? false
        : _endS[g] < (span >= NSpan ? _result.Girders[g].Length : SpanStart(g, span)) - 1e-6;

    /// <summary>A frame of bay (L, R) is valid where both girders exist and all girders between them have ended.</summary>
    private bool BayValidAt(int span, int left, int right, FrameHit h)
    {
        double clear = _def.Girders.CrossGirder.FlangeWidth + Units.UnitSystem.In(6);
        if (BeginS(left) > 0 && SpanStart(left, span) + h.SL < BeginS(left) + _def.CrossFrames.MinDistanceFromBearing) return false;
        if (BeginS(right) > 0 && SpanStart(right, span) + h.SR < BeginS(right) + _def.CrossFrames.MinDistanceFromBearing) return false;
        if (SpanStart(left, span) + h.SL > EndS(left) - clear) return false;
        if (SpanStart(right, span) + h.SR > EndS(right) - clear) return false;
        for (int m = left + 1; m < right; m++)
            if (_endD.Length == 0 || _endD[m] > h.D - clear) return false;
        return true;
    }

    /// <summary>Cuts discontinuous girders at their cross girders: truncated paths, no bearings beyond, cross girder lines.</summary>
    private void ApplyTerminations()
    {
        _endS = _result.Girders.Select(g => g.Length).ToArray();
        _endD = _result.Girders.Select(_ => double.MaxValue).ToArray();
        var terms = _def.Girders.Terminations;
        if (terms.Count == 0) return;
        var xg = _def.Girders.CrossGirder;
        foreach (var t in terms)
        {
            if (t.Girder < 0 || t.Girder >= NG || t.Support < 0 || t.Support >= NSpan) { Warn($"Girder termination G{t.Girder + 1}: support index out of range."); continue; }
            double sCl = SpanStart(t.Girder, t.Support) + t.Distance;
            var g = _result.Girders[t.Girder];
            if (sCl >= g.Length - 0.1) { Warn($"G{t.Girder + 1} termination is beyond the girder end; ignored."); continue; }
            var (pCl, tan, _) = g.Path.At(sCl);
            _endD[t.Girder] = _align.Inverse(pCl.XY).Distance;
            _endS[t.Girder] = sCl - xg.WebThickness / 2 - xg.EndGap;
        }
        // Truncate girders (path, lines, length) and drop bearings past the end.
        for (int i = 0; i < NG; i++)
        {
            var g = _result.Girders[i];
            if (_endS[i] >= g.Length - 1e-9) continue;
            double e = _endS[i];
            var path = new GirderPath();
            var web = new List<Vec3>(); var left = new List<Vec3>(); var right = new List<Vec3>();
            void Add(double s)
            {
                var (p, t, sta) = g.Path.At(s);
                path.Add(s, p, t, sta);
                double tf = TopFlangeT(sta);
                var top = p.XY.WithZ(p.Z + tf);
                web.Add(top);
                var r = t.Right * (TopFlangeW(sta) / 2);
                left.Add(new Vec3(top.X - r.X, top.Y - r.Y, top.Z));
                right.Add(new Vec3(top.X + r.X, top.Y + r.Y, top.Z));
            }
            foreach (double s in g.Path.S) if (s < e - 1e-6) Add(s);
            Add(e);
            _result.Girders[i] = new GirderResult
            {
                Index = i, Spans = g.Spans, WebLine = web, TopFlangeLeft = left, TopFlangeRight = right,
                StartEnd = web[0], EndEnd = web[^1], Length = e, Path = path, StartExtension = g.StartExtension,
            };
            for (int j = 0; j < NS; j++)
            {
                int span = Math.Min(j, NSpan - 1);
                double sb = j == NS - 1 ? SpanStart(i, NSpan - 1) + _curves[i][NSpan - 1].Length : SpanStart(i, j);
                if (sb > e) _result.Supports[j].Bearings[i] = null;
            }
        }
        // Cross girder lines: square to the discontinuous girder at its end, between the nearest girders that continue.
        foreach (var t in terms)
        {
            int i = t.Girder;
            if (i < 0 || i >= NG || double.IsInfinity(_endD[i]) || _endD[i] == double.MaxValue) continue;
            double sCl = _endS[i] + xg.WebThickness / 2 + xg.EndGap;
            var g0 = _result.Girders[i];
            // Path beyond the cut is gone: rebuild the end point from the span curve.
            var (span, u) = LocateOnGirder(i, sCl);
            var c = _curves[i][span];
            var center = Point3(c, u);
            var tan0 = c.TangentAt(u);
            // Cross girder square to the discontinuous girder unless the plans dimension its angle (clockwise from the tangent).
            var dir = t.CrossGirderAngleDegrees is { } ang ? tan0.Rotate(-Numerics.DegToRad(ang)).Normalized() : tan0.Right;
            int l = Enumerable.Range(0, i).Reverse().FirstOrDefault(k => EndS(k) > GirderDistanceAtLine(k, center.XY, dir) + 0.5, -1);
            int r = Enumerable.Range(i + 1, NG - i - 1).FirstOrDefault(k => EndS(k) > GirderDistanceAtLine(k, center.XY, dir) + 0.5, -1);
            if (l < 0 || r < 0) { Warn($"G{i + 1}: no continuing girder on both sides for its cross girder."); continue; }
            double sL = GirderDistanceAtLine(l, center.XY, dir), sR = GirderDistanceAtLine(r, center.XY, dir);
            var (pl, _, _) = _result.Girders[l].Path.At(sL);
            var (pr, _, _) = _result.Girders[r].Path.At(sR);
            _result.CrossGirders.Add(new CrossGirderResult
            {
                Girder = i, LeftGirder = l, RightGirder = r, Center = center.XY.WithZ(center.Z - TopFlangeT(_align.DistanceToStation(_endD[i]))),
                Direction = (pr.XY - pl.XY).Normalized(), Left = pl, Right = pr, LeftGirderDistance = sL, RightGirderDistance = sR,
            });
        }
    }

    // ------------------------------------------------------------------ girders beginning on auxiliary supports

    private double[] _beginS = Array.Empty<double>();   // per girder: path distance of the start bearing (0 = not begun)
    private double BeginS(int g) => _beginS.Length > 0 ? _beginS[g] : 0;
    private Vec2[] _auxDir = Array.Empty<Vec2>();

    /// <summary>
    /// Girders that begin on an auxiliary support line: the girder is cut at (bearing − end extension), its path re-based
    /// so it starts at 0 again (span geometry is unchanged; SpanStart becomes smaller), its bearings before the cut are
    /// dropped and its start bearing is put on the auxiliary support line.
    /// </summary>
    private void ApplyBeginnings()
    {
        var aux = _def.AuxiliarySupports;
        _beginS = new double[NG];
        _auxDir = new Vec2[aux.Count];
        for (int k = 0; k < aux.Count; k++)
        {
            double d = _align.StationToDistance(aux[k].Station);
            var (pt, ang) = _align.EvaluateAtDistance(d);
            _auxDir[k] = Vec2.FromAngle(ang).Right.Rotate(Numerics.DegToRad(aux[k].SkewDegrees));
            _result.AuxSupports.Add(new SupportLineResult
            {
                Index = NS + k, Name = aux[k].Name, Type = aux[k].Type, Station = aux[k].Station, SkewDegrees = aux[k].SkewDegrees,
                AlignmentPoint = _align.PointAtDistance(d), Direction = _auxDir[k], Bearings = new BearingResult?[NG],
            });
        }
        double dWeb = PS ? PsShape.Depth : Sec.WebDepth;
        foreach (var b in _def.Girders.Beginnings)
        {
            int i = b.Girder, k = aux.FindIndex(a => a.Name == b.Support);
            if (i < 0 || i >= NG) { Warn($"Girder beginning: girder index {i + 1} out of range."); continue; }
            if (k < 0) { Warn($"G{i + 1} begins on '{b.Support}', which is not an auxiliary support of this bridge."); continue; }
            var line = _result.AuxSupports[k];
            double sb = GirderDistanceAtLine(i, line.AlignmentPoint, _auxDir[k]);
            if (double.IsNaN(sb)) { Warn($"G{i + 1} does not cross the {line.Name} support line."); continue; }
            double ext = aux[k].GirderEndExtension;
            double cut = sb - ext;
            if (cut <= 0) { Warn($"G{i + 1}: {line.Name} is at or before the girder start; beginning ignored."); continue; }
            var g = _result.Girders[i];
            var path = new GirderPath();
            var web = new List<Vec3>(); var left = new List<Vec3>(); var right = new List<Vec3>();
            void Add(double s)
            {
                var (p, t, sta) = g.Path.At(s);
                path.Add(s - cut, p, t, sta);
                double tf = TopFlangeT(sta);
                var top = p.XY.WithZ(p.Z + tf);
                web.Add(top);
                var r = t.Right * (TopFlangeW(sta) / 2);
                left.Add(new Vec3(top.X - r.X, top.Y - r.Y, top.Z));
                right.Add(new Vec3(top.X + r.X, top.Y + r.Y, top.Z));
            }
            Add(cut);
            foreach (double s in g.Path.S) if (s > cut + 1e-6) Add(s);
            _result.Girders[i] = new GirderResult
            {
                Index = i, Spans = g.Spans, WebLine = web, TopFlangeLeft = left, TopFlangeRight = right,
                StartEnd = web[0], EndEnd = web[^1], Length = g.Length - cut, Path = path, StartExtension = g.StartExtension - cut,
            };
            _startExt[i] -= cut;
            if (_endS.Length > 0) _endS[i] -= cut;
            _beginS[i] = ext;
            // Bearings before the start bearing are gone.
            for (int j = 0; j < NS; j++)
                if (_result.Supports[j].Bearings[i] != null && (j == NS - 1 ? SpanStart(i, NSpan - 1) + _curves[i][NSpan - 1].Length : SpanStart(i, j)) < ext)
                    _result.Supports[j].Bearings[i] = null;
            var (pb, _, stab) = path.At(ext);
            var so = _align.Inverse(pb.XY);
            double webTop = pb.Z;
            line.Bearings[i] = new BearingResult
            {
                Girder = i, Support = NS + k, Station = so.Station, Offset = so.Offset,
                TopOfSteel = pb.XY.WithZ(webTop + TopFlangeT(stab)), BottomOfSteelElevation = webTop - dWeb - BottomFlangeT(stab),
                DeckElevation = webTop + WebTopDrop, PathDistance = ext,
            };
        }
    }

    private (int span, double u) LocateOnGirder(int g, double s)
    {
        for (int j = 0; j < NSpan; j++)
        {
            double s0 = SpanStart(g, j), len = _curves[g][j].Length;
            if (s <= s0 + len || j == NSpan - 1) return (j, _curves[g][j].UAt(Math.Clamp(s - s0, 0, len)));
        }
        return (0, 0);
    }

    /// <summary>Path distance where a line through p along dir crosses girder g (NaN when it misses).</summary>
    private double GirderDistanceAtLine(int g, Vec2 p, Vec2 dir)
    {
        for (int j = 0; j < NSpan; j++)
        {
            var c = _curves[g][j];
            var d = _align.Inverse(p).Distance;
            if (c.Intersect(p, dir, out double u, Math.Clamp((d - c.D0) / (c.D1 - c.D0), 0, 1), 0) && u >= 0 && u <= 1)
                return SpanStart(g, j) + c.SAt(u);
        }
        return double.NaN;
    }

    // ------------------------------------------------------------------ cross frames

    private double SpanMaxSkew(int span) =>
        Math.Max(Math.Abs(_def.Supports[span].SkewDegrees), Math.Abs(_def.Supports[span + 1].SkewDegrees));

    private Vec2 FrameDirection(int span, double d, bool parallelToSkew)
    {
        var (_, ang) = _align.EvaluateAtDistance(d);
        var right = Vec2.FromAngle(ang).Right;
        if (!parallelToSkew) return right;
        double t = Math.Clamp((d - _dSup[span]) / (_dSup[span + 1] - _dSup[span]), 0, 1);
        double skew = _def.Supports[span].SkewDegrees + (_def.Supports[span + 1].SkewDegrees - _def.Supports[span].SkewDegrees) * t;
        return right.Rotate(Numerics.DegToRad(skew));
    }

    private ResolvedCrossFrameType ResolveType(CrossFrameType t, double width)
    {
        return t switch
        {
            CrossFrameType.XFrame => ResolvedCrossFrameType.XFrame,
            CrossFrameType.KFrame => ResolvedCrossFrameType.KFrame,
            CrossFrameType.InvertedKFrame => ResolvedCrossFrameType.InvertedKFrame,
            CrossFrameType.Diaphragm => ResolvedCrossFrameType.Diaphragm,
            _ => width / _def.Girders.Section.TotalDepth <= _def.CrossFrames.KFrameAspectRatio
                ? ResolvedCrossFrameType.XFrame : ResolvedCrossFrameType.KFrame,
        };
    }

    private readonly record struct FrameHit(double D, double UL, double UR, double SL, double SR);

    private static ResolvedCrossFrameType KindType(CrossFrameStyleKind k) => k switch
    {
        CrossFrameStyleKind.XFrame => ResolvedCrossFrameType.XFrame,
        CrossFrameStyleKind.KFrame => ResolvedCrossFrameType.KFrame,
        CrossFrameStyleKind.Strut => ResolvedCrossFrameType.Strut,
        CrossFrameStyleKind.PlateDiaphragm => ResolvedCrossFrameType.PlateDiaphragm,
        _ => ResolvedCrossFrameType.InvertedKFrame,
    };

    private readonly ConcurrentDictionary<string, bool> _missingStyles = new();
    /// <summary>Type for a style name ("" = legacy rule). Unknown names are reported once and use the legacy rule.</summary>
    private ResolvedCrossFrameType StyleType(string style, CrossFrameType legacy, double width)
    {
        if (style == "") return ResolveType(legacy, width);
        if (_def.CrossFrames.FindStyle(style) is { } st) return KindType(st.Kind);
        if (_missingStyles.TryAdd(style, true)) Warn($"Cross frame style '{style}' is not defined; those frames use the default members.");
        return ResolveType(legacy, width);
    }

    private string SupportStyleOf(int support)
    {
        var cf = _def.CrossFrames;
        return cf.SupportStyles.TryGetValue(_def.SupportAt(support).Name, out var st) ? st : cf.SupportStyle;
    }

    /// <summary>Intersects the frame line anchored at alignment distance d with both girders of a bay.</summary>
    private FrameHit? HitBay(int span, int bay, double d, bool parallel, int right = -1)
    {
        var cl = _curves[bay][span];
        var cr = _curves[right >= 0 ? right : bay + 1][span];
        var a = _align.PointAtDistance(d);
        var dir = FrameDirection(span, d, parallel);
        double guessL = (d - cl.D0) / (cl.D1 - cl.D0), guessR = (d - cr.D0) / (cr.D1 - cr.D0);
        if (!cl.Intersect(a, dir, out double ul, guessL, 0.5) || !cr.Intersect(a, dir, out double ur, guessR, 0.5)) return null;
        return new FrameHit(d, ul, ur, cl.SAt(ul), cr.SAt(ur));
    }

    /// <summary>Clearance margin of a frame from the bearings (negative = too close / outside the span).</summary>
    private double Clearance(int span, int bay, FrameHit? h, int right = -1)
    {
        if (h is not { } x) return double.NegativeInfinity;
        double c = _def.CrossFrames.MinDistanceFromBearing;
        double ll = _curves[bay][span].Length, lr = _curves[right >= 0 ? right : bay + 1][span].Length;
        return Math.Min(Math.Min(x.SL - c, ll - c - x.SL), Math.Min(x.SR - c, lr - c - x.SR));
    }

    private double FiniteClearance(int span, int bay, FrameHit? h, int right = -1)
    {
        double c = Clearance(span, bay, h, right);
        return double.IsNegativeInfinity(c) ? -1e9 : c;
    }

    private void BuildCrossFrames()
    {
        var cf = _def.CrossFrames;
        var frames = new ConcurrentBag<CrossFrameResult>();

        // Support cross frames along each support line.
        for (int j = 0; j < NS; j++)
        {
            int span = Math.Min(j, NSpan - 1);
            var active = Enumerable.Range(0, NG).Where(g => _result.Supports[j].Bearings[g] != null).ToList();
            for (int q = 0; q + 1 < active.Count; q++)
            {
                int b = active[q], bR = active[q + 1];
                var l = _result.Supports[j].Bearings[b]!;
                var r = _result.Supports[j].Bearings[bR]!;
                double width = (r.TopOfSteel.XY - l.TopOfSteel.XY).Length;
                bool atEnd = j == NS - 1;
                string sst = SupportStyleOf(j);
                if (sst == "none") continue;
                frames.Add(new CrossFrameResult
                {
                    Span = span, Bay = b, RightGirder = bR, AtSupport = true, Type = StyleType(sst, cf.SupportType, width), Style = cf.FindStyle(sst) != null ? sst : "",
                    Left = l.TopOfSteel, Right = r.TopOfSteel, Station = _def.Supports[j].Station, SupportIndex = j,
                    LeftGirderDistance = atEnd ? _curves[b][span].Length : 0,
                    RightGirderDistance = atEnd ? _curves[bR][span].Length : 0,
                });
            }
        }

        // Support cross frames along auxiliary support lines (between consecutive girders beginning there).
        foreach (var line in _result.AuxSupports)
        {
            var active = Enumerable.Range(0, NG).Where(g => line.Bearings[g] != null).ToList();
            for (int q = 0; q + 1 < active.Count; q++)
            {
                int b = active[q], bR = active[q + 1];
                var l = line.Bearings[b]!;
                var r = line.Bearings[bR]!;
                double width = (r.TopOfSteel.XY - l.TopOfSteel.XY).Length;
                string sst = SupportStyleOf(line.Index);
                if (sst == "none") continue;
                frames.Add(new CrossFrameResult
                {
                    Span = 0, Bay = b, RightGirder = bR, AtSupport = true, Type = StyleType(sst, cf.SupportType, width), Style = cf.FindStyle(sst) != null ? sst : "",
                    Left = l.TopOfSteel, Right = r.TopOfSteel, Station = line.Station, SupportIndex = line.Index,
                    LeftGirderDistance = l.PathDistance - SpanStart(b, 0), RightGirderDistance = r.PathDistance - SpanStart(bR, 0),
                });
            }
        }

        // Intermediate cross frames.
        var jobs = new List<(int span, int bay, int right)>();
        for (int j = 0; j < NSpan; j++)
            for (int b = 0; b < NG - 1; b++)
            {
                if (!AliveAtSpanStart(b, j)) continue;
                // Adjacent bay, then bays bridging girders that end within (or before) this span.
                for (int r = b + 1; r < NG; r++)
                {
                    if (AliveAtSpanStart(r, j)) jobs.Add((j, b, r));
                    if (EndsBefore(r, j + 1) == false) break;
                }
            }

        var lineSpans = BuildFrameLines(frames);
        Parallel.ForEach(jobs, job =>
        {
            var (j, b, bR) = job;
            if (lineSpans.Contains(j)) return;          // the framing plan gives this span's frames
            double maxSkew = SpanMaxSkew(j);
            bool parallel = cf.Orientation switch
            {
                CrossFrameOrientation.ParallelToSkew => true,
                CrossFrameOrientation.NormalToGirders => false,
                _ => maxSkew <= cf.SkewLimitDegrees,
            };
            bool staggered = cf.Arrangement switch
            {
                CrossFrameArrangement.Staggered => true,
                CrossFrameArrangement.Contiguous => false,
                _ => maxSkew > cf.SkewLimitDegrees,
            };
            if (parallel) staggered = false; // lines parallel to the supports are naturally contiguous

            double a, z;
            int n;
            if (!staggered)
            {
                a = _dSup[j];
                z = _dSup[j + 1];
                double lmax = 0;
                for (int i = 0; i < NG; i++) lmax = Math.Max(lmax, _curves[i][j].Length);
                n = Math.Max(1, (int)Math.Ceiling(lmax / cf.MaxSpacing - 1e-9));
            }
            else
            {
                // Bay centerline crossings with the support lines.
                Func<double, double> mid = d => (OffsetAt(b, d) + OffsetAt(bR, d)) / 2;
                double offMid0 = (_offsets[j][b] + _offsets[j][bR]) / 2;
                double offMid1 = (_offsets[j + 1][b] + _offsets[j + 1][bR]) / 2;
                if (!IntersectSupportWithOffsetCurve(j, mid, offMid0, _align.PointAtDistance(_dSup[j]), out a) ||
                    !IntersectSupportWithOffsetCurve(j + 1, mid, offMid1, _align.PointAtDistance(_dSup[j + 1]), out z))
                {
                    Warn($"Span {j + 1}, bay {b + 1}: could not locate bay centerline at supports.");
                    return;
                }
                double lbay = (_curves[b][j].Length + _curves[bR][j].Length) / 2;
                n = Math.Max(1, (int)Math.Ceiling(lbay / cf.MaxSpacing - 1e-9));
            }

            double dm = (a + z) / 2;
            var hitMid = HitBay(j, b, dm, parallel, bR);
            if (Clearance(j, b, hitMid, bR) < 0)
            {
                if (n > 1) Warn($"Span {j + 1}, bay {b + 1}: bay is too short for intermediate cross frames at the required bearing clearance.");
                return;
            }

            var placed = new List<FrameHit>();
            for (int k = 1; k < n; k++)
            {
                double d = a + (z - a) * k / n;
                var h = HitBay(j, b, d, parallel, bR);
                if (Clearance(j, b, h, bR) < 0)
                {
                    // Slide toward mid-bay until the frame clears both bearings.
                    double target = dm;
                    if (Numerics.Brent(x => FiniteClearance(j, b, HitBay(j, b, x, parallel, bR), bR), d, target, out double ds, 1e-6))
                    {
                        ds += Math.Sign(target - d) * 1e-4;
                        h = HitBay(j, b, ds, parallel, bR);
                    }
                    if (Clearance(j, b, h, bR) < -1e-3) continue;
                }
                // Field splice conflict: always reported; moved only when the user opted in (and the move is reported).
                if (h is { } h0 && SpliceIntrusion(j, b, bR, h0.SL, h0.SR) > 0 && !_def.CrossFrames.MoveClearOfSplices)
                    Warn($"Span {j + 1}, bay {b + 1} (G{b + 1}-G{bR + 1}): intermediate cross frame is within the minimum distance of {SpliceName(j, b, bR, h0.SL, h0.SR)} — check the cross frame and splice locations against the design.");
                else if (h is { } h0m && SpliceIntrusion(j, b, bR, h0m.SL, h0m.SR) > 0)
                {
                    FrameHit? moved = null;
                    double limit = (z - a) / n / 2;
                    for (double step = 0.05; step <= limit && moved == null; step += 0.05)
                        foreach (double sgn in new[] { 1.0, -1.0 })
                        {
                            var hm = HitBay(j, b, d + sgn * step, parallel, bR);
                            if (hm is { } m && Clearance(j, b, hm, bR) >= 0 && SpliceIntrusion(j, b, bR, m.SL, m.SR) <= 0) { moved = hm; break; }
                        }
                    if (moved == null) Warn($"Span {j + 1}, bay {b + 1}: an intermediate cross frame could not be kept clear of a field splice (minimum distance from splice).");
                    else
                    {
                        Warn($"Span {j + 1}, bay {b + 1} (G{b + 1}-G{bR + 1}): intermediate cross frame MOVED along the girders to clear {SpliceName(j, b, bR, h0m.SL, h0m.SR)}.");
                        h = moved;
                    }
                }
                if (h is { } hit && !placed.Any(p => Math.Abs(p.SL - hit.SL) < 0.3 || Math.Abs(p.SR - hit.SR) < 0.3))
                    placed.Add(hit);
            }
            // Discontinuous girders: keep frames where both girders exist and every girder between them has ended.
            placed.RemoveAll(h => !BayValidAt(j, b, bR, h));

            var cl = _curves[b][j];
            var cr = _curves[bR][j];
            foreach (var h in placed)
            {
                var pl = Point3(cl, h.UL);
                var pr = Point3(cr, h.UR);
                frames.Add(new CrossFrameResult
                {
                    Span = j, Bay = b, RightGirder = bR, AtSupport = false,
                    Type = StyleType(cf.IntermediateStyle, cf.IntermediateType, (pr.XY - pl.XY).Length),
                    Style = cf.FindStyle(cf.IntermediateStyle) != null ? cf.IntermediateStyle : "",
                    Left = pl, Right = pr, Station = _align.DistanceToStation(h.D),
                    LeftGirderDistance = h.SL, RightGirderDistance = h.SR,
                });
            }

            // Unbraced length check along both girders.
            foreach (var (curve, list, gname) in new[]
                     {
                         (cl, placed.Select(p => p.SL), $"G{b + 1}"),
                         (cr, placed.Select(p => p.SR), $"G{bR + 1}"),
                     })
            {
                if (_endS.Length > 0 && (_endS[b] < SpanStart(b, j) + _curves[b][j].Length || _endS[bR] < SpanStart(bR, j) + _curves[bR][j].Length
                    || bR != b + 1)) continue; // partial bays near discontinuous girders
                var s = list.Append(0).Append(curve.Length).OrderBy(x => x).ToList();
                double worst = s.Zip(s.Skip(1), (x, y) => y - x).DefaultIfEmpty(0).Max();
                if (worst > cf.MaxSpacing * 1.001)
                    Warn($"Span {j + 1}, bay {b + 1}: cross-frame spacing along {gname} reaches {worst:F2} m, exceeding the maximum.");
            }
        });

        // Optional limit: no cross frames in bays wider than MaxBayWidth (e.g. a widening whose interior girders are not modeled).
        var kept = cf.MaxBayWidth > 0 ? frames.Where(f => (f.Right.XY - f.Left.XY).Length <= cf.MaxBayWidth).ToList() : frames.ToList();
        if (kept.Count < frames.Count)
            Warn($"{frames.Count - kept.Count} cross frames omitted in bays wider than {cf.MaxBayWidth / 0.3048:0.#} ft (cross-frame maximum bay width).");
        var ordered = kept.OrderBy(f => f.Span).ThenBy(f => f.Station).ThenBy(f => f.Bay).ToList();
        int mark = 1;
        foreach (var f in ordered)
        {
            f.Mark = $"CF{mark++}";
            f.LeftGlobalDistance = SpanStart(f.Bay, f.Span) + f.LeftGirderDistance;
            f.RightGlobalDistance = SpanStart(f.RightGirder, f.Span) + f.RightGirderDistance;
        }
        _result.CrossFrames.AddRange(ordered);
    }

    /// <summary>
    /// Frames on the framing plan's lines: each line passes through the point at the given distance along its reference
    /// girder (from that girder's first bearing), radial to the alignment or normal to the girder; it gets a frame in every
    /// bay whose two girders it crosses within the same span (a girder crossed exactly at a bearing counts for both spans).
    /// Frames closer to a bearing than the minimum distance are kept (they are on the plans) but reported.
    /// Returns the spans that received line frames.
    /// </summary>
    private HashSet<int> BuildFrameLines(ConcurrentBag<CrossFrameResult> frames)
    {
        var cf = _def.CrossFrames;
        var spans = new HashSet<int>();
        foreach (var line in cf.Lines)
        {
            int g = line.Girder;
            if (g < 0 || g >= NG) { Warn($"Cross frame line: girder {g + 1} does not exist."); continue; }
            double s0 = BeginS(g) > 0 ? BeginS(g) : SpanStart(g, 0);
            double total = SpanStart(g, NSpan - 1) + _curves[g][NSpan - 1].Length - s0;
            string where = $"Frame line {line.Distance / 0.3048:F2} ft along G{g + 1}";
            if (line.Distance < 0 || line.Distance > total + 1e-6) { Warn($"{where}: beyond the girder ({total / 0.3048:F2} ft)."); continue; }
            var (rj, ru) = LocateOnGirder(g, s0 + line.Distance);
            var rc = _curves[g][rj];
            var p = rc.PointAt(ru);
            double d = _align.Inverse(p).Distance;
            var dir = line.NormalToGirder ? rc.TangentAt(ru).Normalized().Right : _align.RightNormalAt(_align.DistanceToStation(d));
            // Crossings of every girder in every span (a crossing at a bearing appears in both adjacent spans).
            var hits = new List<(int g, List<(int span, double u, double s)> at)>();
            for (int i = 0; i < NG; i++)
            {
                var at = new List<(int, double, double)>();
                for (int k = 0; k < NSpan; k++)
                {
                    var c = _curves[i][k];
                    double guess = Math.Clamp((d - c.D0) / (c.D1 - c.D0), 0, 1);
                    if (!c.Intersect(p, dir, out double u, guess, 0) || u < -1e-6 || u > 1 + 1e-6) continue;
                    u = Math.Clamp(u, 0, 1);
                    double sg = SpanStart(i, k) + c.SAt(u);
                    if (sg < BeginS(i) - 1e-6 || sg > EndS(i) + 1e-6) continue;   // girder not there (begins / ends)
                    // Within 6" of a bearing: the line is at the bearing (plans' "about" spacings through skewed piers).
                    double si = c.SAt(u);
                    if (si < 0.1524) { u = 0; si = 0; } else if (c.Length - si < 0.1524) { u = 1; si = c.Length; }
                    at.Add((k, u, si));
                    if (u == 0 && k > 0) at.Add((k - 1, 1, _curves[i][k - 1].Length));
                    if (u == 1 && k < NSpan - 1) at.Add((k + 1, 0, 0));
                }
                at = at.GroupBy(x => x.Item1).Select(x => x.First()).ToList();
                if (at.Count > 0) hits.Add((i, at));
            }
            double minD = cf.MinDistanceFromBearing;
            for (int q = 0; q + 1 < hits.Count; q++)
            {
                int b = hits[q].g, bR = hits[q + 1].g;
                if (line.Bays != null && !line.Bays.Contains(b)) continue;
                var common = hits[q].at.Select(x => x.span).Intersect(hits[q + 1].at.Select(x => x.span)).ToList();
                if (common.Count == 0)                                // the bay straddles a support line
                {
                    if (line.Bays != null) Warn($"{where}: bay G{b + 1}-G{bR + 1} straddles a support line; no frame.");
                    continue;
                }
                // Prefer the span where neither crossing sits on a bearing.
                int OnBearing(int k, int idx) => hits[idx].at.First(x => x.span == k).u is <= 1e-9 or >= 1 - 1e-9 ? 1 : 0;
                int j = common.OrderBy(k => OnBearing(k, q) + OnBearing(k, q + 1)).First();
                var (_, ul, sl) = hits[q].at.First(x => x.span == j);
                var (_, uR, sr) = hits[q + 1].at.First(x => x.span == j);
                string st = line.BayStyles.TryGetValue(b, out var bs) ? bs : line.Style != "" ? line.Style : cf.IntermediateStyle;
                if (st == "none") continue;
                var h = new FrameHit(d, ul, uR, sl, sr);
                if (!BayValidAt(j, b, bR, h)) continue;
                double ll = _curves[b][j].Length, lr = _curves[bR][j].Length;
                // Frames AT a bearing (pier zones of skewed supports) are deliberate; report only those near one.
                double near = new[] { sl, ll - sl, sr, lr - sr }.Where(x => x > 0.01).DefaultIfEmpty(double.MaxValue).Min();
                if (near < minD)
                    Warn($"{where}, span {j + 1}, bay G{b + 1}-G{bR + 1}: frame is closer than {minD / 0.3048:F1} ft to a bearing (kept as on the plans).");
                if (SpliceIntrusion(j, b, bR, sl, sr) > 0)
                    Warn($"{where}, span {j + 1}, bay G{b + 1}-G{bR + 1}: frame is within the splice plates of {SpliceName(j, b, bR, sl, sr)} (kept as on the plans).");
                var pl = Point3(_curves[b][j], ul);
                var pr = Point3(_curves[bR][j], uR);
                spans.Add(j);
                frames.Add(new CrossFrameResult
                {
                    Span = j, Bay = b, RightGirder = bR, AtSupport = false,
                    Type = StyleType(st, cf.IntermediateType, (pr.XY - pl.XY).Length), Style = cf.FindStyle(st) != null ? st : "",
                    Left = pl, Right = pr, Station = _align.DistanceToStation(d), LeftGirderDistance = sl, RightGirderDistance = sr,
                });
            }
        }
        return spans;
    }

    // ------------------------------------------------------------------ stiffeners

    private void BuildStiffeners()
    {
        var sd = _def.Stiffeners;
        var all = new List<StiffenerResult>[NG];
        double center = _offsets.SelectMany(o => o).Average();

        Parallel.For(0, NG, i =>
        {
            var list = new List<StiffenerResult>();

            // Bearing stiffeners (pairs), in the plane of the support line so the support cross frames bolt to them.
            foreach (var line in _result.AuxSupports)
                if (line.Bearings[i] is { } ab)
                {
                    var (span0, u0) = LocateOnGirder(i, ab.PathDistance);
                    var c0 = _curves[i][span0];
                    var t0 = c0.TangentAt(u0);
                    list.Add(new StiffenerResult
                    {
                        Girder = i, Kind = StiffenerKind.Bearing, Location = Point3(c0, u0), GirderTangent = t0,
                        Direction = TowardRight(_auxDir[line.Index - NS], t0),
                        Side = StiffenerSide.Both, Plate = sd.BearingStiffener, GirderDistance = ab.PathDistance,
                    });
                }
            for (int j = 0; j < NS; j++)
            {
                if (BeginS(i) > 0 && _result.Supports[j].Bearings[i] == null) continue;   // before a begun girder's start
                int span = Math.Min(j, NSpan - 1);
                double u = j == NS - 1 ? 1 : 0;
                var c = _curves[i][span];
                var t = c.TangentAt(u);
                list.Add(new StiffenerResult
                {
                    Girder = i, Kind = StiffenerKind.Bearing, Location = Point3(c, u), GirderTangent = t,
                    Direction = TowardRight(_supDir[j], t),
                    Side = StiffenerSide.Both, Plate = sd.BearingStiffener, GirderDistance = GirderDistance(i, span, u),
                });
            }

            // Connection stiffeners at intermediate cross frames, in the cross-frame plane (skewed with the frame).
            var conn = new List<(int span, double s, StiffenerSide side, Vec2 dir)>();
            foreach (var f in _result.CrossFrames.Where(f => !f.AtSupport))
            {
                if (f.Bay == i) conn.Add((f.Span, f.LeftGirderDistance, StiffenerSide.Right, f.Direction));
                else if (f.RightGirder == i) conn.Add((f.Span, f.RightGirderDistance, StiffenerSide.Left, f.Direction));
            }
            conn.Sort((x, y) => x.span != y.span ? x.span.CompareTo(y.span) : x.s.CompareTo(y.s));
            for (int k = 0; k < conn.Count; k++)
            {
                var (span, s, side, dir) = conn[k];
                // Frames from both bays in one line share a stiffener through the web (a pair).
                if (k + 1 < conn.Count && conn[k + 1].span == span && Math.Abs(conn[k + 1].s - s) < 0.02 && conn[k + 1].side != side
                    && Vec2.Dot(conn[k + 1].dir, dir) > 0.9998)
                {
                    side = StiffenerSide.Both;
                    k++;
                }
                var c = _curves[i][span];
                double u = c.UAt(s);
                var t = c.TangentAt(u);
                list.Add(new StiffenerResult
                {
                    Girder = i, Kind = StiffenerKind.ConnectionPlate, Location = Point3(c, u), GirderTangent = t,
                    Direction = TowardRight(dir, t),
                    Side = side, Plate = sd.ConnectionPlate, GirderDistance = GirderDistance(i, span, u),
                });
            }

            // Intermediate transverse stiffeners in panels between bearings and connection plates.
            if (sd.IntermediateMaxSpacing > 0)
            {
                var side = sd.IntermediateSide;
                if (side == StiffenerSide.Interior)
                {
                    double off = _offsets[0][i];
                    side = off < center ? StiffenerSide.Right : StiffenerSide.Left;
                }
                for (int j = 0; j < NSpan; j++)
                {
                    var c = _curves[i][j];
                    var stops = conn.Where(x => x.span == j).Select(x => x.s).Append(0).Append(c.Length).Distinct().OrderBy(x => x).ToList();
                    for (int k = 0; k + 1 < stops.Count; k++)
                    {
                        double len = stops[k + 1] - stops[k];
                        int n = (int)Math.Ceiling(len / sd.IntermediateMaxSpacing - 1e-9);
                        for (int m = 1; m < n; m++)
                        {
                            double u = c.UAt(stops[k] + len * m / n);
                            var t = c.TangentAt(u);
                            list.Add(new StiffenerResult
                            {
                                Girder = i, Kind = StiffenerKind.Intermediate, Location = Point3(c, u), GirderTangent = t, Direction = t.Right,
                                Side = side, Plate = sd.IntermediateStiffener, GirderDistance = GirderDistance(i, j, u),
                            });
                        }
                    }
                }
            }
            all[i] = list;
        });

        foreach (var l in all) _result.Stiffeners.AddRange(l.Where(s => s.GirderDistance <= EndS(s.Girder) + 1e-6).OrderBy(s => s.GirderDistance));
    }

    private static Vec2 TowardRight(Vec2 dir, Vec2 tangent) => Vec2.Dot(dir, tangent.Right) >= 0 ? dir : -dir;

    // ------------------------------------------------------------------ field splices

    /// <summary>Field splice positions per girder and span as curve distances (s along <c>_curves[i][j]</c>), so cross
    /// frames can keep clear of the splice plates.</summary>
    private List<(double S, int Index)>[][] _spliceS = Array.Empty<List<(double, int)>[]>();

    /// <summary>Path distance of girder g's CL bearing at support j (framing index; auxiliary lines included), NaN if none.</summary>
    private double BearingPathDistance(int g, int j)
    {
        if (j >= NS)
        {
            var line = _result.AuxSupports.FirstOrDefault(a => a.Index == j);
            return line?.Bearings.ElementAtOrDefault(g) is { } b ? b.PathDistance : double.NaN;
        }
        return j == NSpan ? SpanStart(g, NSpan - 1) + _curves[g][NSpan - 1].Length : SpanStart(g, j);
    }

    /// <summary>Where splice k crosses each girder: (girder, span, u) - per-girder locations, or the radial line at the station.</summary>
    private List<(int g, int span, double u)> SpliceHits(int k)
    {
        var fs = _def.FieldSplices[k];
        var hits = new List<(int, int, double)>();
        if (fs.Girders.Count > 0)
        {
            foreach (var loc in fs.Girders)
            {
                if (loc.Girder < 0 || loc.Girder >= NG) { Warn($"Field splice {SpliceNo(k)}: girder {loc.Girder + 1} does not exist."); continue; }
                double s0 = BearingPathDistance(loc.Girder, loc.Support);
                if (double.IsNaN(s0)) { Warn($"Field splice {SpliceNo(k)}: G{loc.Girder + 1} has no bearing at support {loc.Support + 1}."); continue; }
                double sg = s0 + loc.Offset;
                if (sg <= BeginS(loc.Girder) + 0.3 || sg >= EndS(loc.Girder) - 0.3) { Warn($"Field splice {SpliceNo(k)} is outside G{loc.Girder + 1}."); continue; }
                var (span, u) = LocateOnGirder(loc.Girder, sg);
                hits.Add((loc.Girder, span, u));
            }
            return hits;
        }
        double d = _align.StationToDistance(fs.Station);
        var a = _align.PointAtDistance(d);
        var dir = FrameDirection(0, d, false);
        for (int i = 0; i < NG; i++)
            for (int j = 0; j < NSpan; j++)
            {
                var c = _curves[i][j];
                if (!c.Intersect(a, dir, out double u, (d - c.D0) / (c.D1 - c.D0), 0) || u <= 0 || u >= 1) continue;
                hits.Add((i, j, u));
                break;
            }
        return hits;
    }

    /// <summary>
    /// Required distance from splice k's CL to a cross frame: with a scheduled detail, its longest splice plate half-length
    /// + 6" (connection plate half-width and clearance) — a physical clash; otherwise the definition's minimum distance.
    /// </summary>
    private double SpliceClear(int k)
    {
        var d = _def.FieldSplices[k].Detail;
        if (d == null || (d.Top == null && d.Bottom == null && d.Web == null)) return _def.CrossFrames.MinDistanceFromSplice;
        double first = d.FirstBoltFromJoint > 0 ? d.FirstBoltFromJoint : _def.FieldSpliceGap / 2 + _def.Bolts.EdgeDistance;
        double end = d.PlateEndDistance > 0 ? d.PlateEndDistance : _def.Bolts.EdgeDistance;
        double h = 0;
        foreach (var f in new[] { d.Top, d.Bottom }) if (f != null) h = Math.Max(h, first + f.Spaces * f.Spacing + end);
        if (d.Web != null) h = Math.Max(h, first + d.Web.ColumnSpaces * d.Web.ColumnSpacing + end);
        return h + 0.1524;
    }

    /// <summary>Path distance of girder g where the radial line at a station crosses it (NaN when it misses).</summary>
    private double PathDistanceAtStation(int g, double station)
    {
        double d = _align.StationToDistance(station);
        var p = _align.PointAtDistance(d);
        var dir = Vec2.FromAngle(_align.EvaluateAtDistance(d).angle).Right;
        for (int j = 0; j < NSpan; j++)
        {
            var c = _curves[g][j];
            if (c.Intersect(p, dir, out double u, (d - c.D0) / (c.D1 - c.D0), 0.0) && u >= 0 && u <= 1) return GirderDistance(g, j, u);
        }
        return double.NaN;
    }

    /// <summary>
    /// Resolves each girder's plate runs (as-designed layouts, else the station-based plate changes) into path distances,
    /// then puts the bearings' top / bottom of steel on the plate actually at each bearing.
    /// </summary>
    private void BuildPlateRuns()
    {
        for (int i = 0; i < NG; i++)
        {
            var g = _result.Girders[i];
            double len = g.Length;
            foreach (var plate in new[] { GirderPlate.TopFlange, GirderPlate.Web, GirderPlate.BottomFlange })
            {
                var layout = Sec.Layouts.FirstOrDefault(l => l.Girders.Contains(i) && l.Runs.Any(r => r.Plate == plate));
                double a = 0;
                if (layout != null)
                {
                    var runs = layout.Runs.Where(r => r.Plate == plate).ToList();
                    for (int k = 0; k < runs.Count; k++)
                    {
                        var r = runs[k];
                        double b = len;
                        if (k < runs.Count - 1)
                        {
                            b = r.EndSplice > 0
                                ? _result.Splices.FirstOrDefault(sp => sp.Girder == i && sp.Number == r.EndSplice)?.GirderDistance ?? double.NaN
                                : r.EndSupport >= 0 ? BearingPathDistance(i, r.EndSupport) + r.EndOffset : double.NaN;
                            if (double.IsNaN(b) && r.EndSplice > 0 && runs.Skip(k + 1).Take(runs.Count - k - 2)
                                    .All(x => x.EndSplice == 0 || !_result.Splices.Any(sp => sp.Girder == i && sp.Number == x.EndSplice)))
                                b = len;      // the girder ends (cross girder) before this and every later splice: the run goes to its end
                            if (double.IsNaN(b))
                            {
                                Warn($"G{i + 1}: {plate} run {Sh(r.Width)} x {Sh(r.Thickness)} ends at {(r.EndSplice > 0 ? $"field splice {r.EndSplice}" : $"support {r.EndSupport + 1}")}, which the girder does not have.");
                                continue;
                            }
                            if (b < a - 1e-4) Warn($"G{i + 1}: {plate} runs out of order near {b / 0.3048:F2} ft.");
                            b = Math.Clamp(b, a, len);
                        }
                        if (b > a + 1e-6) g.PlateRuns.Add((plate, a, b, plate == GirderPlate.Web ? Sec.WebDepth : r.Width, r.Thickness));
                        a = Math.Max(a, b);
                    }
                }
                else
                {
                    var segs = Sec.Segments(plate);
                    for (int k = 0; k < segs.Count; k++)
                    {
                        double b = len;
                        if (k < segs.Count - 1)
                        {
                            b = PathDistanceAtStation(i, segs[k].EndStation);
                            if (double.IsNaN(b)) b = segs[k].EndStation < g.Path.Station[0] ? 0 : len;
                            b = Math.Clamp(b, a, len);
                        }
                        if (b > a + 1e-6) g.PlateRuns.Add((plate, a, b, segs[k].Width, segs[k].Thickness));
                        a = b;
                    }
                }
                if (!g.PlateRuns.Any(r => r.Plate == plate))
                {
                    var s0 = Sec.Segments(plate)[^1];
                    g.PlateRuns.Add((plate, 0, len, s0.Width, s0.Thickness));
                }
            }
            // Bearings: top / bottom of steel on the plate at the bearing.
            foreach (var line in _result.Supports.Concat(_result.AuxSupports))
            {
                if (line.Bearings.ElementAtOrDefault(i) is not { } br) continue;
                double s = line.Index >= NS ? br.PathDistance : BearingPathDistance(i, line.Index);
                if (double.IsNaN(s)) continue;
                double tfOld = TopFlangeT(br.Station), bfOld = BottomFlangeT(br.Station);
                double tf = g.PlateAt(GirderPlate.TopFlange, s)?.Thickness ?? tfOld, bf = g.PlateAt(GirderPlate.BottomFlange, s)?.Thickness ?? bfOld;
                br.TopOfSteel = br.TopOfSteel.XY.WithZ(br.TopOfSteel.Z - tfOld + tf);
                br.BottomOfSteelElevation += bfOld - bf;
            }
        }
    }

    private string Sh(double m) => $"{m / 0.0254:0.###}\"";

    private int SpliceNo(int k) => _def.FieldSplices[k].Number > 0 ? _def.FieldSplices[k].Number : k + 1;

    private void LocateSplices()
    {
        _spliceS = Enumerable.Range(0, NG).Select(_ => Enumerable.Range(0, NSpan).Select(_ => new List<(double, int)>()).ToArray()).ToArray();
        for (int k = 0; k < _def.FieldSplices.Count; k++)
            foreach (var (i, j, u) in SpliceHits(k))
                _spliceS[i][j].Add((_curves[i][j].SAt(u), k));
    }

    /// <summary>Distance by which a frame at curve distances (sl, sr) intrudes on a splice zone (≤ 0 = clear).</summary>
    private double SpliceIntrusion(int span, int left, int right, double sl, double sr)
    {
        if (_def.CrossFrames.MinDistanceFromSplice <= 0 || _spliceS.Length == 0) return 0;
        double worst = 0;
        foreach (var (s, k) in _spliceS[left][span]) worst = Math.Max(worst, SpliceClear(k) - Math.Abs(s - sl));
        foreach (var (s, k) in _spliceS[right][span]) worst = Math.Max(worst, SpliceClear(k) - Math.Abs(s - sr));
        return worst;
    }

    /// <summary>"field splice FS6 on G2" for the splice nearest a frame at curve distances (sl, sr).</summary>
    private string SpliceName(int span, int left, int right, double sl, double sr)
    {
        var all = _spliceS[left][span].Select(x => (d: Math.Abs(x.S - sl), x.Index, g: left))
            .Concat(_spliceS[right][span].Select(x => (d: Math.Abs(x.S - sr), x.Index, g: right)));
        var n = all.OrderBy(x => x.d).FirstOrDefault();
        return $"field splice FS{SpliceNo(n.Index)} on G{n.g + 1}";
    }

    private void BuildSplices()
    {
        for (int k = 0; k < _def.FieldSplices.Count; k++)
        {
            var hits = SpliceHits(k);
            foreach (var (i, j, u) in hits)
            {
                var c = _curves[i][j];
                double gd = GirderDistance(i, j, u);
                if (gd > EndS(i) - 0.5) continue; // beyond a discontinuous girder's end
                _result.Splices.Add(new FieldSpliceResult
                {
                    Girder = i, Span = j, SpliceIndex = k, Number = SpliceNo(k), Location = Point3(c, u), GirderTangent = c.TangentAt(u), GirderDistance = gd,
                });
                var near = _result.Stiffeners.Where(s => s.Girder == i && Math.Abs(s.GirderDistance - gd) < 0.45).ToList();
                if (near.Count > 0)
                    Warn($"Field splice {SpliceNo(k)} on G{i + 1} is within 1'-6\" of a {near[0].Kind} stiffener.");
            }
            if (_def.FieldSplices[k].Girders.Count == 0)
                for (int i = 0; i < NG; i++)
                    if (!hits.Any(h => h.g == i) && EndS(i) >= _result.Girders[i].Length - 1e-6)
                        Warn($"Field splice {SpliceNo(k)} does not fall within the girder length of G{i + 1}.");
        }
    }

    // ------------------------------------------------------------------ deck & barriers

    private (Vec2 p, Vec2 dir) DeckEndLine(int support, bool begin)
    {
        var sup = _def.Supports[support];
        var dir = _supDir[support];
        var ahead = dir.Left; // perpendicular to the support line, pointing up-station
        var a = _align.PointAtDistance(_dSup[support]);
        double off = sup.DeckEndOffset;
        return (begin ? a - ahead * off : a + ahead * off, dir);
    }

    private bool EdgeAtEndLine(double offset, (Vec2 p, Vec2 dir) line, int support, out double d)
    {
        double skew = Numerics.DegToRad(_def.Supports[support].SkewDegrees);
        double guess = _align.Inverse(line.p).Distance + offset * Math.Tan(skew);
        double w = Math.Abs(offset) * (Math.Abs(Math.Tan(skew)) + 0.5) + 10;
        return _align.IntersectOffsetCurveWithLine(_ => offset, line.p, line.dir, guess - w, guess + w, guess, out d);
    }

    private List<Vec3>? OffsetLineBetweenEnds(double offset, double dz = 0)
    {
        var b = DeckEndLine(0, true);
        var e = DeckEndLine(NS - 1, false);
        if (!EdgeAtEndLine(offset, b, 0, out double d0) || !EdgeAtEndLine(offset, e, NS - 1, out double d1)) return null;
        return _align.SamplePolyline(d0, d1, offset)
            .Select(p => p.WithZ(DeckElevationAtPoint(p) + dz)).ToList();
    }

    /// <summary>A deck line whose offset varies with station (variable-width deck edges), between the deck end lines.</summary>
    private List<Vec3>? VariableLineBetweenEnds(Func<double, double> offsetAtStation, double dz = 0)
    {
        double F(double d) => offsetAtStation(_align.DistanceToStation(d));
        bool Hit((Vec2 p, Vec2 dir) line, int support, out double d)
        {
            double o = offsetAtStation(_align.Inverse(line.p).Station);
            double skew = Numerics.DegToRad(_def.Supports[support].SkewDegrees);
            double guess = _align.Inverse(line.p).Distance + o * Math.Tan(skew);
            double w = Math.Abs(o) * (Math.Abs(Math.Tan(skew)) + 0.5) + 10;
            return _align.IntersectOffsetCurveWithLine(F, line.p, line.dir, guess - w, guess + w, guess, out d);
        }
        if (!Hit(DeckEndLine(0, true), 0, out double d0) || !Hit(DeckEndLine(NS - 1, false), NS - 1, out double d1)) return null;
        int n = Math.Max(2, (int)Math.Ceiling((d1 - d0) / 1.5));
        var list = new List<Vec3>(n + 1);
        for (int i = 0; i <= n; i++)
        {
            double d = d0 + (d1 - d0) * i / n;
            var p = _align.PointAtDistance(d, F(d));
            list.Add(p.WithZ(DeckElevationAtPoint(p) + dz));
        }
        return list;
    }

    private void BuildDeck()
    {
        var deck = _def.Deck;
        bool vw = deck.VariableWidth;
        var left = vw ? VariableLineBetweenEnds(deck.LeftAt) : OffsetLineBetweenEnds(deck.LeftEdgeOffset);
        var right = vw ? VariableLineBetweenEnds(deck.RightAt) : OffsetLineBetweenEnds(deck.RightEdgeOffset);
        if (left == null || right == null)
        {
            Warn("Could not close the deck outline at the end supports.");
            return;
        }
        var outline = new List<Vec3>(left);
        outline.AddRange(Enumerable.Reverse(right));
        outline.Add(left[0]);

        double area = 0;
        for (int k = 0; k + 1 < outline.Count; k++)
            area += outline[k].X * outline[k + 1].Y - outline[k + 1].X * outline[k].Y;
        area = Math.Abs(area) / 2;

        var barriers = new List<List<Vec3>>();
        var bar = deck.Barrier;
        if (bar.Type != BarrierType.None)
        {
            var (toe, top, height) = Concrete.BarrierProfiles.PlanLines(bar);
            void Add(Func<double, double> off, double dz)
            {
                var l = vw ? VariableLineBetweenEnds(off, dz) : OffsetLineBetweenEnds(off(0), dz);
                if (l != null) barriers.Add(l);
            }
            if (bar.Left)
            {
                Add(s => deck.LeftAt(s) + toe, 0);
                Add(s => deck.LeftAt(s) + top, height);
            }
            if (bar.Right)
            {
                Add(s => deck.RightAt(s) - toe, 0);
                Add(s => deck.RightAt(s) - top, height);
            }
        }

        foreach (var g in _result.Girders)
        {
            double o0 = _offsets[0][g.Index];
            double hw = TopFlangeW(0) / 2;
            double s0 = _def.Supports[0].Station;
            if (o0 - hw < deck.LeftAt(s0) || o0 + hw > deck.RightAt(s0))
                Warn($"{g.Name} top flange extends beyond the deck edge.");
        }

        var bl = DeckEndLine(0, true);
        var el = DeckEndLine(NS - 1, false);
        _result.Deck = new DeckResult { Outline = outline, BarrierLines = barriers, Area = area,
            BeginLinePoint = bl.p, BeginLineDirection = bl.dir, EndLinePoint = el.p, EndLineDirection = el.dir };
    }
}
