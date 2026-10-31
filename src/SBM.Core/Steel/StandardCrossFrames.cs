using SBM.Core.Bridge;
using SBM.Core.Geometry;

namespace SBM.Core.Steel;

/// <summary>A cross frame plane: x along <see cref="U"/> (plan direction, horizontal) from the origin, z along the up axis <see cref="Wt"/> read like an
/// elevation (origin = its own Z), v along the normal N = Wt x U. Same mapping as the detailer's frame plane.</summary>
public readonly record struct FramePlaneGeom(Vec3 Origin, Vec3 U, Vec3 Wt)
{
    /// <summary>OPTIONAL z read at the origin (null = Origin.Z): a connection plate's plane in a warped frame keeps the frame's 2D coordinates.</summary>
    public double? Z0 { get; init; }
    public Vec3 N => Vec3.Cross(Wt, U).Normalized();
    public Vec3 W(Vec2 xz, double v) => Origin + U * xz.X + N * v + Wt * (xz.Y - (Z0 ?? Origin.Z));
    public double LocalZ(Vec3 p) => (Z0 ?? Origin.Z) + Vec3.Dot(p - Origin, Wt);
    public double LocalV(Vec3 p) => Vec3.Dot(p - Origin, N);
}

/// <summary>One end of a cross frame: the connection (or bearing) plate it bolts to. <see cref="Origin"/> = the plate's mid-plane point on the web
/// centre line at the top of web (lowered at a dapped end), <see cref="Up"/> = the plate's up axis (orthogonal to the frame's plan direction),
/// <see cref="FlangeNormal"/> = true flange normal (level across the girder, sloped along it), <see cref="BottomFlangePoint"/> = a point of the bottom
/// flange's top face on the web centre line.</summary>
public sealed record PlateEndGeometry(Vec3 Origin, Vec3 Up, Vec3 FlangeNormal, Vec3 BottomFlangePoint, double TopFlangeThickness);

/// <summary>
/// Pure measurements of STANDARD cross frames (<see cref="StandardCrossFrameDefinition"/>) and of the twist between the two connection plates of any
/// frame: where a bolt axis meets a plate, plate coordinates, flange faces seen from the plate, twist angle, far-gusset gaps. Used by the steel generator
/// (connections phase) and by tests; nothing here builds solids.
/// </summary>
public static class StandardCrossFrames
{
    /// <summary>Standard of a frame: its style's own, else the definition's when the style is listed (or the list is empty); then the scope / supports.</summary>
    public static StandardCrossFrameDefinition? For(CrossFrameDefinition def, CrossFrameStyle? st, string style, bool atSupport, string supportName)
    {
        var s = st?.Standard;
        if (s == null && def.Standard is { } d && (d.Styles.Count == 0 || d.Styles.Contains(style))) s = d;
        if (s == null) return null;
        bool inScope = s.Scope == StandardFrameScope.All || (s.Scope == StandardFrameScope.Support) == atSupport;
        if (!inScope) return null;
        if (atSupport && s.Supports.Count > 0 && !s.Supports.Contains(supportName)) return null;
        return s;
    }

    /// <summary>Unit vector normal to the flanges at path distance s (perpendicular to the girder's 3D tangent), orthogonal to <paramref name="across"/>
    /// (the steel generator's rule for connection plates and intermediate frames).</summary>
    public static Vec3 FlangeNormal(Framing.GirderPath path, double s, Vec3 across)
    {
        var (_, t, _) = path.At(s);
        double g = path.GradeAt(s);
        var t3 = new Vec3(t.X, t.Y, g).Normalized();
        var up = new Vec3(0, 0, 1);
        var w = (up - t3 * Vec3.Dot(up, t3)).Normalized();
        return (w - across * Vec3.Dot(w, across)).Normalized();
    }

    /// <summary>Plate mid-plane normal oriented toward +N of the frame (the gusset side).</summary>
    public static Vec3 PlateNormal(PlateEndGeometry e, Vec3 u, Vec3 frameN)
    {
        var z = Vec3.Cross(u, e.Up).Normalized();
        return Vec3.Dot(z, frameN) < 0 ? z * -1 : z;
    }

    /// <summary>Parameter v along the frame normal where the bolt axis through frame point b meets the plate plane offset by <paramref name="face"/>
    /// from the mid-plane (0 = mid-plane, +tp/2 = the face the gusset bears on).</summary>
    public static double AxisHit(FramePlaneGeom pl, Vec2 b, PlateEndGeometry e, double face)
    {
        var n = PlateNormal(e, pl.U, pl.N);
        var q = pl.W(b, 0);
        double dn = Vec3.Dot(pl.N, n);
        if (Math.Abs(dn) < 1e-9) return 0;
        return (face - Vec3.Dot(q - e.Origin, n)) / dn;
    }

    /// <summary>World point where the bolt axis through frame point b meets the plate's mid-plane (= the hole centre on the plate).</summary>
    public static Vec3 HoleCentre(FramePlaneGeom pl, Vec2 b, PlateEndGeometry e) => pl.W(b, AxisHit(pl, b, e, 0));

    /// <summary>Gap along the bolt axis between the gusset's back face (frame v = tp/2) and the plate face (+ = gap to be drawn up, - = interference).</summary>
    public static double Gap(FramePlaneGeom pl, Vec2 b, PlateEndGeometry e, double tp) => tp / 2 - AxisHit(pl, b, e, tp / 2);

    /// <summary>Plate coordinates of a world point: (x along U from the web centre line, y along the plate up axis from the top of web).</summary>
    public static Vec2 PlateCoords(Vec3 p, PlateEndGeometry e, Vec3 u) => new(Vec3.Dot(p - e.Origin, u), Vec3.Dot(p - e.Origin, e.Up));

    /// <summary>Plate y of the underside of the top flange on the plate line at plate x.</summary>
    public static double TopFlangeY(PlateEndGeometry e, Vec3 u, double x)
    {
        double dy = Vec3.Dot(e.Up, e.FlangeNormal);
        return Math.Abs(dy) < 1e-9 ? 0 : -x * Vec3.Dot(u, e.FlangeNormal) / dy;
    }

    /// <summary>Plate y of the top of the bottom flange on the plate line at plate x.</summary>
    public static double BottomFlangeY(PlateEndGeometry e, Vec3 u, double x)
    {
        double dy = Vec3.Dot(e.Up, e.FlangeNormal);
        return Math.Abs(dy) < 1e-9 ? double.NaN : -(Vec3.Dot(e.Origin - e.BottomFlangePoint, e.FlangeNormal) + x * Vec3.Dot(u, e.FlangeNormal)) / dy;
    }

    /// <summary>Signed angle (degrees) from the left plate's up axis to the right plate's about the frame's plan direction.</summary>
    public static double TwistDegrees(Vec3 yL, Vec3 yR, Vec3 u) =>
        Numerics.RadToDeg(Math.Atan2(Vec3.Dot(Vec3.Cross(yL, yR), u), Vec3.Dot(yL, yR)));

    /// <summary>Averaged plane (up axis = mean of the two plate up axes, through the left top of web) and the plane fitted to one end (its plate's
    /// mid-plane; up axis = that plate's). Both keep x measured from the left web point.</summary>
    public static FramePlaneGeom Averaged(Vec3 leftTopOfWeb, Vec3 u, PlateEndGeometry l, PlateEndGeometry r)
    {
        var w = (l.Up + r.Up).Normalized();
        return new FramePlaneGeom(leftTopOfWeb, u, (w - u * Vec3.Dot(w, u)).Normalized());
    }

    public static FramePlaneGeom Midway(Vec3 leftTopOfWeb, Vec3 u, PlateEndGeometry l, PlateEndGeometry r)
    {
        var a = Averaged(leftTopOfWeb, u, l, r);
        var n = a.N;
        double off = (Vec3.Dot(l.Origin - leftTopOfWeb, n) + Vec3.Dot(r.Origin - leftTopOfWeb, n)) / 2;
        return a with { Origin = leftTopOfWeb + n * off };
    }

    public static FramePlaneGeom Fitted(Vec3 leftTopOfWeb, Vec3 u, PlateEndGeometry def)
    {
        var w = (def.Up - u * Vec3.Dot(def.Up, u)).Normalized();
        var n = Vec3.Cross(w, u).Normalized();
        return new FramePlaneGeom(leftTopOfWeb + n * Vec3.Dot(def.Origin - leftTopOfWeb, n), u, w);
    }

    /// <summary>Bolt b of a frame built in <paramref name="from"/> carried into plane <paramref name="to"/> keeping its x and its height above the defined
    /// end's top of web (the frame is rigid; only its plane changes).</summary>
    public static Vec2 Carry(Vec2 b, FramePlaneGeom from, FramePlaneGeom to, Vec3 definedTopOfWeb) =>
        new(b.X, to.LocalZ(definedTopOfWeb) + (b.Y - from.LocalZ(definedTopOfWeb)));

    /// <summary>Min / max far-gusset gap over the bolts of one end.</summary>
    public static (double min, double max) GapRange(FramePlaneGeom pl, IEnumerable<Vec2> bolts, PlateEndGeometry e, double tp)
    {
        double lo = double.PositiveInfinity, hi = double.NegativeInfinity;
        foreach (var b in bolts) { double g = Gap(pl, b, e, tp); lo = Math.Min(lo, g); hi = Math.Max(hi, g); }
        return double.IsInfinity(lo) ? (double.NaN, double.NaN) : (lo, hi);
    }

    /// <summary>Spacing family key: same type / style / standard and the same work point spacing (0.1 mm; user answer 2026-10-27: no tolerance across bays).</summary>
    public static long SpacingKey(double spacing, double rounding = double.NaN) =>
        double.IsNaN(rounding) || rounding <= 0 ? (long)Math.Round(spacing * 1e4) : (long)Math.Round(spacing / rounding);
}

/// <summary>One cross frame of the fit / twist report (<see cref="CrossFrameFitReport"/>). Lengths in meters, angles in degrees. Ends: L = the bay's lower
/// numbered girder, R = the other. Clearances / shifts are along the connection plate's up axis; gaps along the bolt axis.</summary>
public sealed class CrossFrameFitRow
{
    public required string Mark { get; init; }
    public string Family { get; set; } = "";
    /// <summary>Standard name ("" = not standard: twist report only).</summary>
    public string Standard { get; init; } = "";
    public string Style { get; init; } = "";
    public string Type { get; init; } = "";
    public bool AtSupport { get; init; }
    public int Span { get; init; }
    public int LeftGirder { get; init; }
    public int RightGirder { get; init; }
    public double Station { get; init; }
    public double LeftS { get; init; }
    public double RightS { get; init; }
    public double LeftStation { get; init; }
    public double RightStation { get; init; }
    public string LeftPlate { get; set; } = "";
    public string RightPlate { get; set; } = "";
    /// <summary>Work point spacing (bolt column to bolt column) in the frame.</summary>
    public double Spacing { get; init; }
    /// <summary>"L" / "R": end carrying the defined top work point.</summary>
    public string DefinedEnd { get; init; } = "L";
    /// <summary>Right reference minus left reference along the frame up axis (top of web difference across the bay).</summary>
    public double ReferenceDifference { get; init; }
    /// <summary>True when the standard geometry was built (TopWorkPoint and Depth given).</summary>
    public bool Applied { get; init; }
    /// <summary>"averaged" / "defined end" / "midway" / "warped": plane the frame was built in.</summary>
    public string BuiltPlane { get; init; } = "averaged";
    public double TopWorkPoint { get; init; } = double.NaN;
    public double Depth { get; init; } = double.NaN;
    /// <summary>Clear distance top WP -> top flange underside / bottom WP -> bottom flange top, per end (NaN when not a standard frame).</summary>
    public double ClearTopL { get; init; } = double.NaN;
    public double ClearTopR { get; init; } = double.NaN;
    public double ClearBottomL { get; init; } = double.NaN;
    public double ClearBottomR { get; init; } = double.NaN;
    /// <summary>Deepest frame this location takes with the entered top WP and bottom clearance (clearance NaN = 0), and the end that limits it.</summary>
    public double MaxDepth { get; init; } = double.NaN;
    public string MaxDepthEnd { get; init; } = "";
    /// <summary>MaxDepth - Depth (negative = does not fit; still built at the entered depth).</summary>
    public double Headroom { get; init; } = double.NaN;
    /// <summary>Top clearance shortfall (required - found, + = the top WP is too close to the top flange at that end; NaN when no standard).</summary>
    public double TopShortfall { get; init; } = double.NaN;
    /// <summary>Hole group shift vs the standard pattern (+ = up), top and bottom groups, per end.</summary>
    public double ShiftTopL { get; init; } = double.NaN;
    public double ShiftTopR { get; init; } = double.NaN;
    public double ShiftBottomL { get; init; } = double.NaN;
    public double ShiftBottomR { get; init; } = double.NaN;
    public double TwistDegrees { get; init; }
    /// <summary>Plate mid-plane offset from the built frame plane at each end's top of web.</summary>
    public double OffsetL { get; init; }
    public double OffsetR { get; init; }
    /// <summary>Far-gusset gaps (min, max over the end's bolts) in the built plane and in both plane options.</summary>
    public (double Min, double Max) GapBuiltL { get; init; }
    public (double Min, double Max) GapBuiltR { get; init; }
    public (double Min, double Max) GapAveragedL { get; init; }
    public (double Min, double Max) GapAveragedR { get; init; }
    public (double Min, double Max) GapMidwayL { get; init; }
    public (double Min, double Max) GapMidwayR { get; init; }
    public (double Min, double Max) GapFittedL { get; init; }
    public (double Min, double Max) GapFittedR { get; init; }
    public int BoltsL { get; init; }
    public int BoltsR { get; init; }
    public bool TwistFlag { get; set; }
    /// <summary>Warped frames: largest member roll between its two ends (degrees, signed), that member's code and the largest leg-tip deviation from an
    /// untwisted member (m) over the frame's members. 0 / "" for one-plane frames.</summary>
    public double RollDegrees { get; set; }
    public string RollMember { get; set; } = "";
    public double LegTipDeviation { get; set; }
    /// <summary>Move of the left / right connection plate along its girder from the plan line's top-of-web crossing onto the frame plane (m; 0 = the
    /// reference end, or legacy placement).</summary>
    public double LeftShift { get; init; }
    public double RightShift { get; init; }
    /// <summary>"L" / "R": end whose top of web defines the frame's location ("" = legacy placement at each top of web).</summary>
    public string ReferenceEnd { get; init; } = "";
    /// <summary>Family grouping key input (standard's SpacingRounding).</summary>
    public double SpacingRounding { get; init; } = double.NaN;

    static double AbsMax((double Min, double Max) g) => double.IsNaN(g.Min) ? 0 : Math.Max(Math.Abs(g.Min), Math.Abs(g.Max));
    public double MaxGapBuilt => Math.Max(AbsMax(GapBuiltL), AbsMax(GapBuiltR));
    public double MaxGapAveraged => Math.Max(AbsMax(GapAveragedL), AbsMax(GapAveragedR));
    public double MaxGapFitted => Math.Max(AbsMax(GapFittedL), AbsMax(GapFittedR));
    public double MaxGapMidway => Math.Max(AbsMax(GapMidwayL), AbsMax(GapMidwayR));
    static double Spread((double Min, double Max) g) => double.IsNaN(g.Min) ? 0 : g.Max - g.Min;
    /// <summary>Largest gap spread over the bolts of one end (the twist part of the gap; the rest is the plate's offset from the frame plane).</summary>
    public double SpreadAveraged => Math.Max(Spread(GapAveragedL), Spread(GapAveragedR));
    public double SpreadFitted => Math.Max(Spread(GapFittedL), Spread(GapFittedR));
    public string Bay => $"G{LeftGirder + 1}-G{RightGirder + 1}";
}

/// <summary>A standard frame family: frames of one standard, style and type with the same work point spacing (one shop frame).</summary>
public sealed record CrossFrameFamily(string Id, string Standard, string Style, string Type, bool AtSupport, double Spacing, double TopWorkPoint, double Depth,
    IReadOnlyList<string> Marks)
{
    public string Prototype => Marks[0];
}

/// <summary>Depth solver result of one standard (report only - it never chooses).</summary>
public sealed class StandardFrameSummary
{
    public required string Name { get; init; }
    public double TopWorkPoint { get; init; }
    public double Depth { get; init; }
    public double TopClearance { get; init; }
    public double BottomClearance { get; init; }
    public bool Applied { get; init; }
    public int Frames { get; init; }
    /// <summary>Deepest common depth for the entered top WP (bottom clearance NaN = 0) and where it governs.</summary>
    public double MaxCommonDepth { get; init; } = double.NaN;
    public string GoverningMark { get; init; } = "";
    public string GoverningEnd { get; init; } = "";
    public int GoverningGirder { get; init; } = -1;
    public int GoverningSpan { get; init; } = -1;
    public double GoverningStation { get; init; } = double.NaN;
    public double GoverningReferenceDifference { get; init; } = double.NaN;
    /// <summary>Smallest top WP clearing the top clearance at every frame, and the deepest common depth at it (information, not applied).</summary>
    public double MinTopWorkPoint { get; init; } = double.NaN;
    public double MaxDepthAtMinTopWorkPoint { get; init; } = double.NaN;
    public List<string> Misfits { get; } = new();
    public List<string> TopMisfits { get; } = new();
}

/// <summary>Cross frame fit / twist report of one bridge (<see cref="SteelModel.CrossFrameFit"/>; null when neither a standard nor ReportTwist is set).</summary>
public sealed class CrossFrameFitReport
{
    public List<CrossFrameFitRow> Rows { get; } = new();
    public List<CrossFrameFamily> Families { get; } = new();
    public List<StandardFrameSummary> Standards { get; } = new();
    public CrossFrameFitRow? Row(string mark) => Rows.FirstOrDefault(r => r.Mark == mark);
}
