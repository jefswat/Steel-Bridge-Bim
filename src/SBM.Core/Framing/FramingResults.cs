using SBM.Core.Bridge;
using SBM.Core.Geometry;

namespace SBM.Core.Framing;

public sealed class SupportLineResult
{
    public required int Index { get; init; }
    public required string Name { get; init; }
    public required SupportType Type { get; init; }
    public required double Station { get; init; }
    public required double SkewDegrees { get; init; }
    /// <summary>Point on the alignment at the support station.</summary>
    public required Vec2 AlignmentPoint { get; init; }
    /// <summary>Unit direction of the support line, pointing left → right.</summary>
    public required Vec2 Direction { get; init; }
    /// <summary>Bearing points (top of steel at web CL) per girder; null if the girder misses this support.</summary>
    public required BearingResult?[] Bearings { get; init; }
}

public sealed class BearingResult
{
    public required int Girder { get; init; }
    public required int Support { get; init; }
    public required double Station { get; init; }
    public required double Offset { get; init; }
    /// <summary>Top of steel at web centerline.</summary>
    public required Vec3 TopOfSteel { get; set; }
    /// <summary>Bottom of bottom flange at web centerline (girder assumed plumb).</summary>
    public required double BottomOfSteelElevation { get; set; }
    public required double DeckElevation { get; init; }
    /// <summary>Distance along the girder from its start end (set for bearings on auxiliary supports; NaN = derive it from
    /// the span).</summary>
    public double PathDistance { get; init; } = double.NaN;
}

/// <summary>
/// Girder reference line sampled densely along its length: top of web at the web centerline (3D), plan tangent and
/// alignment station, indexed by distance s from the girder's start end. Linear interpolation between samples.
/// </summary>
public sealed class GirderPath
{
    public List<double> S { get; } = new();
    public List<Vec3> WebTop { get; } = new();
    public List<Vec2> Tangent { get; } = new();
    public List<double> Station { get; } = new();
    public int Count => S.Count;
    public double Length => S.Count > 0 ? S[^1] : 0;

    public void Add(double s, Vec3 webTop, Vec2 tangent, double station)
    {
        S.Add(s);
        WebTop.Add(webTop);
        Tangent.Add(tangent);
        Station.Add(station);
    }

    private int Segment(double s)
    {
        int lo = 0, hi = S.Count - 1;
        if (s <= S[0]) return 0;
        if (s >= S[^1]) return Math.Max(0, S.Count - 2);
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (S[mid] <= s) lo = mid; else hi = mid;
        }
        return lo;
    }

    /// <summary>Top of web point, unit plan tangent and station at distance s (extrapolates linearly beyond the ends).</summary>
    public (Vec3 point, Vec2 tangent, double station) At(double s)
    {
        int i = Segment(s);
        double ds = S[i + 1] - S[i];
        double t = ds > 0 ? (s - S[i]) / ds : 0;
        var p = Vec3.Lerp(WebTop[i], WebTop[i + 1], t);
        var tan = Vec2.Lerp(Tangent[i], Tangent[i + 1], Math.Clamp(t, 0, 1)).Normalized();
        return (p, tan, Station[i] + (Station[i + 1] - Station[i]) * t);
    }

    /// <summary>Longitudinal grade (dz/ds) of the top of web at s.</summary>
    public double GradeAt(double s)
    {
        int i = Segment(s);
        double ds = S[i + 1] - S[i];
        return ds > 0 ? (WebTop[i + 1].Z - WebTop[i].Z) / ds : 0;
    }

    /// <summary>Sample distances strictly inside (s0, s1).</summary>
    public IEnumerable<double> SamplesBetween(double s0, double s1) => S.Where(s => s > s0 + 1e-6 && s < s1 - 1e-6);
}

public sealed class GirderResult
{
    public required int Index { get; init; }
    public string Name => $"G{Index + 1}";
    public required List<GirderSpanCurve> Spans { get; init; }
    public required GirderPath Path { get; init; }
    /// <summary>Distance from the girder start end to the first CL bearing.</summary>
    public required double StartExtension { get; init; }

    /// <summary>Distance along the girder from its start end to a span-local position.</summary>
    public double DistanceAt(int span, double u)
    {
        double s = StartExtension;
        for (int j = 0; j < span; j++) s += Spans[j].Length;
        return s + Spans[span].SAt(u);
    }
    /// <summary>Web centerline at top of steel, including end extensions.</summary>
    public required List<Vec3> WebLine { get; init; }
    public required List<Vec3> TopFlangeLeft { get; init; }
    public required List<Vec3> TopFlangeRight { get; init; }
    public required Vec3 StartEnd { get; init; }
    public required Vec3 EndEnd { get; init; }
    /// <summary>Total plan length along the web centerline, end to end.</summary>
    public required double Length { get; init; }

    /// <summary>Resolved plate runs along the girder (path distance from the start end) - the single source for plate sizes.</summary>
    public List<(GirderPlate Plate, double S0, double S1, double Width, double Thickness)> PlateRuns { get; } = new();

    /// <summary>Plate size at a path distance (null when the girder has no runs, e.g. prestressed beams).</summary>
    public (double Width, double Thickness)? PlateAt(GirderPlate plate, double s)
    {
        (double, double)? last = null;
        foreach (var r in PlateRuns)
        {
            if (r.Plate != plate) continue;
            last = (r.Width, r.Thickness);
            if (s <= r.S1 + 1e-9) return last;
        }
        return last;
    }
}

public enum ResolvedCrossFrameType { XFrame, KFrame, InvertedKFrame, Diaphragm, Strut, PlateDiaphragm }

public sealed class CrossFrameResult
{
    public string Mark { get; set; } = "";
    public required int Span { get; init; }
    private int _right = -1;
    /// <summary>Right girder of the bay (Bay + 1, or the next continuing girder past discontinuous ones).</summary>
    public int RightGirder { get => _right >= 0 ? _right : Bay + 1; init => _right = value; }
    /// <summary>Bay index: between girder Bay and Bay + 1.</summary>
    public required int Bay { get; init; }
    public required ResolvedCrossFrameType Type { get; init; }
    /// <summary>As-designed style name (CrossFrameDefinition.Styles), "" = the definition's default members.</summary>
    public string Style { get; init; } = "";
    public required bool AtSupport { get; init; }
    public required Vec3 Left { get; init; }
    public required Vec3 Right { get; init; }
    /// <summary>Distance along the left/right girder from the start of that girder's span (CL bearing).</summary>
    public required double LeftGirderDistance { get; init; }
    public required double RightGirderDistance { get; init; }
    /// <summary>Alignment station of the line anchor.</summary>
    public required double Station { get; init; }
    /// <summary>Support index for support cross frames, otherwise −1.</summary>
    public int SupportIndex { get; init; } = -1;
    /// <summary>Distance from each girder's start end (global, for locating on the girder path).</summary>
    public double LeftGlobalDistance { get; set; }
    public double RightGlobalDistance { get; set; }
    public double PlanLength => (Right.XY - Left.XY).Length;
    /// <summary>Unit plan direction of the frame line, left girder → right girder.</summary>
    public Vec2 Direction => (Right.XY - Left.XY).Normalized();
}

public enum StiffenerKind { Bearing, ConnectionPlate, Intermediate }

public sealed class StiffenerResult
{
    public required int Girder { get; init; }
    public required StiffenerKind Kind { get; init; }
    /// <summary>Point on the web centerline at top of steel.</summary>
    public required Vec3 Location { get; init; }
    /// <summary>Unit tangent of the girder (plan) at the stiffener.</summary>
    public required Vec2 GirderTangent { get; init; }
    /// <summary>
    /// Unit plan direction in which the plate extends from the web, pointing to the girder's right side. Equal to the
    /// girder normal for square stiffeners; skewed for connection plates in a skewed cross-frame plane.
    /// </summary>
    public required Vec2 Direction { get; init; }
    /// <summary>Angle between the plate and the girder normal (0 = square), radians.</summary>
    public double SkewAngle => Math.Acos(Math.Clamp(Vec2.Dot(Direction, GirderTangent.Right), -1, 1));
    /// <summary>Which web faces carry a plate.</summary>
    public required StiffenerSide Side { get; init; }
    public required PlateSize Plate { get; init; }
    /// <summary>Distance along the girder from its start end.</summary>
    public required double GirderDistance { get; init; }
}

public sealed class FieldSpliceResult
{
    public required int Girder { get; init; }
    public required int Span { get; init; }
    public required int SpliceIndex { get; init; }
    /// <summary>Splice number as on the plans (FieldSpliceDefinition.Number, else index + 1).</summary>
    public int Number { get; init; }
    public required Vec3 Location { get; init; }
    public required Vec2 GirderTangent { get; init; }
    public required double GirderDistance { get; init; }
}

public sealed class DeckResult
{
    /// <summary>Closed outline at the top of deck: left edge (ahead), end line, right edge (back), begin line.</summary>
    public required List<Vec3> Outline { get; init; }
    public required List<List<Vec3>> BarrierLines { get; init; }
    public required double Area { get; init; }
    /// <summary>End-of-deck lines (plan point and unit direction, left → right) at the begin and end of the bridge.</summary>
    public Vec2 BeginLinePoint { get; init; }
    public Vec2 BeginLineDirection { get; init; }
    public Vec2 EndLinePoint { get; init; }
    public Vec2 EndLineDirection { get; init; }
}

public enum DiagnosticSeverity { Info, Warning, Error }

public sealed record Diagnostic(DiagnosticSeverity Severity, string Message);

/// <summary>Everything derived from a <see cref="BridgeDefinition"/> + alignment. Immutable once generated.</summary>
/// <summary>A cross girder carrying the end of a discontinuous girder.</summary>
public sealed class CrossGirderResult
{
    public required int Girder { get; init; }
    public required int LeftGirder { get; init; }
    public required int RightGirder { get; init; }
    /// <summary>Top of web at the CL of the discontinuous girder end.</summary>
    public required Vec3 Center { get; init; }
    /// <summary>Unit vector along the cross girder (left to right).</summary>
    public required Vec2 Direction { get; init; }
    /// <summary>Top of web points at the left and right girder web centerlines.</summary>
    public required Vec3 Left { get; init; }
    public required Vec3 Right { get; init; }
    public required double LeftGirderDistance { get; init; }
    public required double RightGirderDistance { get; init; }
    public string Mark => $"XG-G{Girder + 1}";
}

public sealed class BridgeFraming
{
    /// <summary>Discontinuous girders: girder index → (path distance of the girder end, cross girder line point, direction,
    /// left and right supporting girders).</summary>
    public List<CrossGirderResult> CrossGirders { get; } = new();

    public required BridgeDefinition Definition { get; init; }

    /// <summary>Plate at a bearing (the girder's resolved plate runs; station-based plate changes as a fallback).</summary>
    public PlateSegment PlateAtBearing(BearingResult br, GirderPlate plate)
    {
        var g = Girders[br.Girder];
        int n = g.Spans.Count;
        double s = !double.IsNaN(br.PathDistance) ? br.PathDistance : g.DistanceAt(Math.Min(br.Support, n - 1), br.Support >= n ? 1 : 0);
        return g.PlateAt(plate, s) is { } r ? new PlateSegment { Plate = plate, Width = r.Width, Thickness = r.Thickness }
            : Definition.Girders.Section.SegmentAt(plate, br.Station);
    }
    public required Alignments.Alignment Alignment { get; init; }
    public List<SupportLineResult> Supports { get; } = new();
    /// <summary>Auxiliary support lines (girders beginning there); their Index continues after <see cref="Supports"/>.</summary>
    public List<SupportLineResult> AuxSupports { get; } = new();
    /// <summary>Support line by framing index (supports, then auxiliary supports).</summary>
    public SupportLineResult SupportLine(int index) => index < Supports.Count ? Supports[index] : AuxSupports[index - Supports.Count];
    public List<GirderResult> Girders { get; } = new();
    public List<CrossFrameResult> CrossFrames { get; } = new();
    public List<StiffenerResult> Stiffeners { get; } = new();
    public List<FieldSpliceResult> Splices { get; } = new();
    public DeckResult? Deck { get; set; }
    public List<Diagnostic> Diagnostics { get; } = new();
    public TimeSpan GenerationTime { get; set; }

    public bool HasErrors => Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

    public IEnumerable<BearingResult> Bearings => Supports.Concat(AuxSupports).SelectMany(s => s.Bearings).OfType<BearingResult>();
}
