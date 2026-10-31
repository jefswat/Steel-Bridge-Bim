namespace SBM.Core.Bridge;

/// <summary>
/// OPTIONAL STANDARD ("horizontal") cross frame (<see cref="CrossFrameDefinition.Standard"/> / <see cref="CrossFrameStyle.Standard"/>; null = off, omitted
/// from the .sbm). Design-build practice: the engineer sets the top work point on the DEFINED end and ONE frame depth; the frame is straight (level chords
/// in the frame plane) and the slop between the frame and each girder goes into the HOLE POSITIONS on the connection plates (no slotted holes). One standard
/// per frame type; frames are grouped into FAMILIES of the same type / style and the same work point spacing (same bay condition, no tolerance across bays).
/// The program reports the deepest common depth, the governing frame and every misfit; it never picks the depth and never fills a missing value with a
/// code rule (missing inputs are UNRESOLVED flags). Design note: docs/plan-data/standard-cross-frames-design.md.
/// </summary>
public sealed class StandardCrossFrameDefinition
{
    /// <summary>Name in the report ("" = "standard").</summary>
    public string Name { get; set; } = "";
    /// <summary>Which frames are standard: Intermediate (default), Support or All.</summary>
    public StandardFrameScope Scope { get; set; } = StandardFrameScope.Intermediate;
    /// <summary>Limit to these frame styles (<see cref="CrossFrameStyle.Name"/>; "" = frames without a style); empty = every frame in <see cref="Scope"/>.</summary>
    public List<string> Styles { get; set; } = new();
    /// <summary>Support frames: limit to these support names (support standard or not "depends" - per support); empty = every support line in scope.</summary>
    public List<string> Supports { get; set; } = new();

    /// <summary>End that carries the defined top work point: Taller (default = the end whose reference is higher along the frame up axis), Left or Right
    /// (the bay's lower / higher numbered girder). The other end's holes move.</summary>
    public StandardDefinedEnd DefinedEnd { get; set; } = StandardDefinedEnd.Taller;
    /// <summary>What <see cref="TopWorkPoint"/> is measured from: TopOfWeb (underside of the top flange, default) or TopOfSteel.</summary>
    public StandardFrameReference Reference { get; set; } = StandardFrameReference.TopOfWeb;
    /// <summary>Top work point below the reference at the defined end, along the frame up axis. NaN = not given: UNRESOLVED, the standard is not applied
    /// (frames built as without it) and the report uses 0.</summary>
    public double TopWorkPoint { get; set; } = double.NaN;
    /// <summary>Top work point to bottom work point along the frame up axis. NaN = not given: UNRESOLVED, the standard is not applied; the report still
    /// gives the deepest common depth.</summary>
    public double Depth { get; set; } = double.NaN;
    /// <summary>Required clear distance from the TOP work point to the underside of the top flange / from the BOTTOM work point to the top of the bottom
    /// flange (user answer 2026-10-27: clearances are measured to the work point). NaN = not given: the report uses 0 and raises UNRESOLVED.</summary>
    public double TopClearance { get; set; } = double.NaN;
    public double BottomClearance { get; set; } = double.NaN;

    /// <summary>Work point spacings are rounded to this before frames are grouped into families (shop dimension precision, e.g. 1/16"). NaN = not given:
    /// exact grouping (0.1 mm) - frames whose spacings differ by numerical amounts then form separate families and the report says so (user answer
    /// 2026-10-27: same bay condition, no tolerance across bays; this is only the precision the spacing is detailed to).</summary>
    public double SpacingRounding { get; set; } = double.NaN;

    /// <summary>Plane the standard frame is built in (null = <see cref="CrossFrameDefinition.FramePlane"/>, default Midway; with the legacy
    /// <see cref="CrossFrameDefinition.PlatesAtEachTopOfWeb"/> null = Averaged): Warped (each end's gussets on its own plate, members rolled between them),
    /// Averaged (up axis = average of the two connection plates, plane through the left top of web - the twist is shared by both ends), DefinedEnd (the frame
    /// lies on the defined end's connection plate; all twist at the far end) or Midway (averaged up axis, plane midway between the two plates).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public StandardFramePlane? Plane { get; set; }

    /// <summary>Twist / far-gusset gap limits for CHECK warnings (NaN = report only, as INFO; nothing is straightened).</summary>
    public double TwistLimitDegrees { get; set; } = double.NaN;
    public double TwistGapLimit { get; set; } = double.NaN;

    /// <summary>Plan / firm standard reference for the report.</summary>
    public string Source { get; set; } = "";
    /// <summary>Open items raised as UNRESOLVED warnings.</summary>
    public List<string> Unresolved { get; set; } = new();

    public string DisplayName => Name != "" ? Name : "standard";

    public StandardCrossFrameDefinition Clone()
    {
        var c = (StandardCrossFrameDefinition)MemberwiseClone();
        c.Styles = Styles.ToList();
        c.Supports = Supports.ToList();
        c.Unresolved = Unresolved.ToList();
        return c;
    }
}

public enum StandardFrameScope { Intermediate, Support, All }
public enum StandardDefinedEnd { Taller, Left, Right }
public enum StandardFrameReference { TopOfWeb, TopOfSteel }
/// <summary>Averaged = up axis averaged, plane through the left top of web (the existing detailer's plane); DefinedEnd = on the defined end's plate;
/// Midway = up axis averaged and the plane midway between the two plates (splits the plates' offset as well as the twist); Warped = each end in its own
/// connection plate's plane, members straight between the work points and rolled linearly from one plate's orientation to the other's.</summary>
public enum StandardFramePlane { Averaged, DefinedEnd, Midway, Warped }

/// <summary>Orientation of a cross frame plane and its connection plates (OPTIONAL <see cref="CrossFrameDefinition.IntermediatePlane"/> /
/// <see cref="CrossFrameDefinition.SupportPlane"/>; null = intermediate normal to the flanges, support plumb).</summary>
public enum CrossFramePlaneOrientation
{
    /// <summary>Normal to the flanges (perpendicular to the girder's 3D tangent; intermediate frames without an input).</summary>
    NormalToFlanges,
    /// <summary>Normal to the bottom flange: the same as <see cref="NormalToFlanges"/> while the flanges are parallel (kept as its own value so the
    /// .sbm says what the plans say).</summary>
    NormalToBottomFlange,
    Plumb,
}
