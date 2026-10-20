using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Bridge;

/// <summary>
/// OPTIONAL integral steel pier cap on a support line (<c>SupportDefinition.IntegralSteelCap</c>; null = none, the default, omitted from the .sbm).
/// A TWIN I-girder cap transverse to the bridge, sitting on its own bearings at the cap ends (off the girder lines), with the longitudinal girders
/// FRAMING INTO it (Iowa DOT Design 1617 Nebraska St. Ramp B, pier 2, p.29-31):
/// <list type="bullet">
/// <item>Cap: <see cref="Members"/> I-sections side by side (web CL at +-<see cref="WebOffset"/> from the CL cap = the support line), built in the
/// FINAL (loaded) shape: straight between the bearings, bottom of the bottom flange on the top of bearing elevations, webs plumb, flanges square
/// to the webs and sloped along the cap. Camber (<see cref="Camber"/>) is recorded with its tolerances and compared, never applied.</item>
/// <item>Girders: cut at the cap - web and bottom flange end at a bolted <see cref="CapGirderConnection.EndPlateThickness"/> end plate against the
/// outer face of the cap web; the top flange ends <see cref="CapGirderConnection.TopFlangeEndBeyondCapFlange"/> outside the cap flange edge and is
/// spliced over the cap (outer + 2 inner plates, filler at the flange level, TAPERED fill on the cap flange); the web is coped under the cap flange.
/// No girder bearings, bearing stiffeners or support cross frames on this line.</item>
/// <item>Inside the cap per girder: connector plates on the inner web faces (same outline as the end plate, bolted through end plate + web +
/// connector plate), girder connection diaphragm plate in the girder web plane, mill-to-bear plate at the bottom flange level.</item>
/// <item>Bearing / jacking stiffeners, bolted cover plates, handholes with bolted cover and backing plates, studs, wire mesh end screens.</item>
/// </list>
/// Positions ALONG the cap are horizontal distances S on the support line (right +) from the alignment point. Lengths in meters. Missing plan
/// data are <see cref="Unresolved"/> texts (raised as warnings); a missing bearing elevation leaves the cap unbuilt.
/// </summary>
public sealed class IntegralSteelCapDefinition
{
    /// <summary>Assembly mark prefix (e.g. "CAP2").</summary>
    public string Mark { get; set; } = "CAP";
    /// <summary>Plan reference printed in notes / flags (e.g. "p.29-31").</summary>
    public string Source { get; set; } = "";

    /// <summary>CL bearings along the support line (S from the alignment point, right +), left to right; at least two.</summary>
    public List<double> BearingOffsets { get; set; } = new();
    /// <summary>Top of bearing = underside of the cap bottom flange at each CL bearing (plan bearing table). Required.</summary>
    public List<double> BearingTopElevations { get; set; } = new();
    /// <summary>Plan names of the bearings (e.g. "Column No. 1"); empty = "B1", "B2" ...</summary>
    public List<string> BearingNames { get; set; } = new();
    /// <summary>Cap end beyond the outer CL bearings (plans 1'-6).</summary>
    public double EndOverhang { get; set; } = Ft(1.5);
    /// <summary>Plan slope of the cap (check only, e.g. 0.0339); NaN = none.</summary>
    public double PlanSlope { get; set; } = double.NaN;

    /// <summary>Number of I-members side by side (2 = twin).</summary>
    public int Members { get; set; } = 2;
    /// <summary>Web CL of each member from the CL cap (twin: +-).</summary>
    public double WebOffset { get; set; } = In(10 + 3.0 / 16);
    public double WebDepth { get; set; } = In(90);
    public double WebThickness { get; set; } = In(0.875);
    public PlateSize TopFlange { get; set; } = new(In(20), In(1.75));
    public PlateSize BottomFlange { get; set; } = new(In(20), In(1.75));
    /// <summary>Plan gap between the inner flange edge and the CL cap (check only; plans 3/16).</summary>
    public double PlanFlangeGap { get; set; } = double.NaN;
    public string Material { get; set; } = "ASTM A709 Gr. HPS 70W";
    /// <summary>Material of the parts the plans do not list as HPS (handhole covers, backing plates, fills, screens' angles).</summary>
    public string SecondaryMaterial { get; set; } = "ASTM A709 Gr. 50W";

    /// <summary>Camber ordinates from the chord between the bearings (recorded and checked, the model stays in the final shape).</summary>
    public List<CapCamberPoint> Camber { get; set; } = new();

    /// <summary>Bearing stiffener plate (each face of each web at every CL bearing); null = none.</summary>
    public PlateSize? BearingStiffener { get; set; }
    /// <summary>Jacking stiffeners (same plate) this far INBOARD of each outer CL bearing; 0 = none.</summary>
    public double JackingStiffenerOffset { get; set; }
    /// <summary>45 degree corner clip of the stiffeners at the flanges.</summary>
    public double StiffenerClip { get; set; } = In(1);
    /// <summary>Unwelded length of the stiffener-to-web weld under the top flange (plans "4 3/8 NO WELD").</summary>
    public double StiffenerTopNoWeld { get; set; }
    public double StiffenerWeld { get; set; } = In(5.0 / 16);
    /// <summary>OPTIONAL (false = off, omitted from the file): the stiffeners are milled to bear on the bottom flange and welded there with the
    /// <see cref="StiffenerWeld"/> fillet on both plate faces (Iowa DOT 1617 p.31 Sections B-B / C-C "mill to bear, 5/16 + 5/16").</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool StiffenerBottomWeld { get; set; }

    public List<CapCoverPlate> CoverPlates { get; set; } = new();

    /// <summary>Girder framing connections; null = girders are not connected (only cut).</summary>
    public CapGirderConnection? Connection { get; set; }

    /// <summary>Handholes in the cap webs at each girder; empty = none.</summary>
    public List<CapHandhole> Handholes { get; set; } = new();
    public CapHandholeDetail? HandholeDetail { get; set; }

    /// <summary>Stud lines on the cap top (S from the FIRST CL bearing, along the cap) and stud rows across (N from the CL cap).</summary>
    public List<double> StudLines { get; set; } = new();
    public List<double> StudRows { get; set; } = new();
    /// <summary>Stud rows across on top of a top cover plate (empty = <see cref="StudRows"/>).</summary>
    public List<double> CoverPlateStudRows { get; set; } = new();
    public double StudLength { get; set; } = In(9);

    /// <summary>Wire mesh screens closing both cap ends between the webs; null = none.</summary>
    public CapEndScreen? EndScreens { get; set; }

    /// <summary>Plan items not modeled / not dimensioned (raised as warnings, nothing invented).</summary>
    public List<string> Unresolved { get; set; } = new();

    /// <summary>OPTIONAL concrete haunch between the cap top (flanges / top cover plates) and the deck soffit (<see cref="CapHaunchDefinition"/>,
    /// built by the concrete generator, part of the deck placement); null = none (omitted from the .sbm).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public CapHaunchDefinition? Haunch { get; set; }

    public IntegralSteelCapDefinition Clone()
    {
        var c = (IntegralSteelCapDefinition)MemberwiseClone();
        c.BearingOffsets = BearingOffsets.ToList();
        c.BearingTopElevations = BearingTopElevations.ToList();
        c.BearingNames = BearingNames.ToList();
        c.TopFlange = TopFlange.Clone();
        c.BottomFlange = BottomFlange.Clone();
        c.Camber = Camber.Select(x => x.Clone()).ToList();
        c.BearingStiffener = BearingStiffener?.Clone();
        c.CoverPlates = CoverPlates.Select(x => x.Clone()).ToList();
        c.Connection = Connection?.Clone();
        c.Handholes = Handholes.Select(x => x.Clone()).ToList();
        c.HandholeDetail = HandholeDetail?.Clone();
        c.StudLines = StudLines.ToList();
        c.StudRows = StudRows.ToList();
        c.CoverPlateStudRows = CoverPlateStudRows.ToList();
        c.EndScreens = EndScreens?.Clone();
        c.Unresolved = Unresolved.ToList();
        c.Haunch = Haunch?.Clone();
        return c;
    }
}

/// <summary>
/// OPTIONAL concrete haunch over an integral steel pier cap (Iowa Design 1617 p.25 Section A-A): cast with the deck, it fills the space between the
/// top of the cap steel (top flanges, or the top cover plates where they are) and the deck soffit. Generic rules (the concrete generator,
/// ConcreteCapHaunch.cs): across the cap it is <see cref="Width"/> wide centred on the CL cap with plumb sides; along the cap it runs between the deck
/// edges (to the outer face of a deck widening under a barrier zone at this support when the widening covers the haunch); over the girder top flanges the
/// GIRDER haunches own the concrete, so the cap haunch stops at their sides (no volume counted twice); its top is the deck soffit (profile, cross slope,
/// crown), its bottom the cap steel. Reinforcing: U stirrups midway between the cap stud lines (<see cref="Stirrups"/>) and longitudinal bars per bay
/// (<see cref="Bars"/>), placed as plan bar sets (marks, counts and listed lengths = the plan's; clear cover checked, never moved).
/// </summary>
public sealed class CapHaunchDefinition
{
    /// <summary>Plan reference (e.g. "p.25 Section A-A").</summary>
    public string Source { get; set; } = "";
    /// <summary>Width across the cap (N, centred on the CL cap); 0 = out-to-out of the top flanges (sides flush with the flange edges).</summary>
    public double Width { get; set; }
    public List<CapHaunchStirrups> Stirrups { get; set; } = new();
    public List<CapHaunchBars> Bars { get; set; } = new();
    /// <summary>Plan items about the haunch the plans do not settle (raised as warnings).</summary>
    public List<string> Unresolved { get; set; } = new();

    public CapHaunchDefinition Clone()
    {
        var c = (CapHaunchDefinition)MemberwiseClone();
        c.Stirrups = Stirrups.Select(x => x.Clone()).ToList();
        c.Bars = Bars.Select(x => x.Clone()).ToList();
        c.Unresolved = Unresolved.ToList();
        return c;
    }
}

/// <summary>
/// U stirrups of a cap haunch, one in the plane normal to the cap axis MIDWAY BETWEEN consecutive cap stud lines (plans "place between shear studs")
/// wherever that point lies in the cap haunch (not under a girder haunch, between the deck edges). Shape (plan bend diagram, out-to-out): bottom
/// <see cref="BottomWidth"/> centred on the CL cap, legs <see cref="LegHeight"/> up (bottom face to the top of the hook), hooks <see cref="HookLength"/>
/// outward at the top (from the inner face of the leg); bottom <see cref="ClearAboveSteel"/> clear above the cap steel under it.
/// </summary>
public sealed class CapHaunchStirrups
{
    public string Mark { get; set; } = "";
    public string Size { get; set; } = "#5";
    public string Location { get; set; } = "";
    /// <summary>Coating ("" = the deck's).</summary>
    public string Coating { get; set; } = "";
    public double BottomWidth { get; set; }
    public double LegHeight { get; set; }
    public double HookLength { get; set; }
    /// <summary>Printed bend pin diameter D (plan bend diagram; 0 = the standard minimum).</summary>
    public double Pin { get; set; }
    public double ClearAboveSteel { get; set; } = In(2);
    /// <summary>Plan clear cover to the concrete faces (checked).</summary>
    public double Cover { get; set; } = In(2);
    public double ListedLength { get; set; }
    public int? PlanCount { get; set; }
    public double? PlanWeightLb { get; set; }
    public string Source { get; set; } = "";
    public CapHaunchStirrups Clone() => (CapHaunchStirrups)MemberwiseClone();
}

/// <summary>Which stretch of a cap haunch a longitudinal bar set fills.</summary>
public enum CapHaunchBay
{
    /// <summary>Between two girder haunches.</summary>
    Interior,
    /// <summary>Between an exterior girder haunch and the deck edge.</summary>
    Exterior,
}

/// <summary>
/// Straight bars along the cap in a cap haunch: in every <see cref="Bay"/> one bar per entry of <see cref="Positions"/> (X = N of the bar centre from
/// the CL cap, Z = bar centre above the cap steel at the bay's middle, square to the flanges), parallel to the cap axis, <see cref="ListedLength"/>
/// long, centred in the bay's clear length (the end clearances are reported).
/// </summary>
public sealed class CapHaunchBars
{
    public string Mark { get; set; } = "";
    public string Size { get; set; } = "#5";
    public string Location { get; set; } = "";
    public string Coating { get; set; } = "";
    public CapHaunchBay Bay { get; set; }
    public List<CapPoint> Positions { get; set; } = new();
    public double ListedLength { get; set; }
    public double Cover { get; set; } = In(2);
    public int? PlanCount { get; set; }
    public double? PlanWeightLb { get; set; }
    public string Source { get; set; } = "";
    public CapHaunchBars Clone() { var c = (CapHaunchBars)MemberwiseClone(); c.Positions = Positions.Select(x => x.Clone()).ToList(); return c; }
}

/// <summary>Camber ordinate of the cap at a distance from the first CL bearing (horizontal, along the cap), with its fabrication tolerance.</summary>
public sealed class CapCamberPoint
{
    public double Distance { get; set; }
    public double Camber { get; set; }
    public double TolMinus { get; set; }
    public double TolPlus { get; set; }
    public CapCamberPoint() { }
    public CapCamberPoint(double distance, double camber, double tolMinus, double tolPlus) { Distance = distance; Camber = camber; TolMinus = tolMinus; TolPlus = tolPlus; }
    public CapCamberPoint Clone() => (CapCamberPoint)MemberwiseClone();
}

/// <summary>
/// Bolted cover plate over both twin flanges near a cap end. Extent along the cap: from <see cref="From"/> to <see cref="To"/> = distances from
/// the CL bearing <see cref="Bearing"/> TOWARD THE CAP INTERIOR (negative = toward the cap end). <see cref="FromSoleEdge"/>: the start is the
/// modeled bearing's sole plate edge + <see cref="From"/> (plans: "starts 1 1/2 inches from the edge of the bearing sole plate").
/// Bolt columns: distances from the CL bearing (same sense); bolt rows across: N from the CL cap.
/// </summary>
public sealed class CapCoverPlate
{
    public bool Top { get; set; } = true;
    /// <summary>Index of the CL bearing (0 = first / left).</summary>
    public int Bearing { get; set; }
    public double From { get; set; }
    public bool FromSoleEdge { get; set; }
    public double To { get; set; }
    public double Width { get; set; } = In(40.375);
    public double Thickness { get; set; } = In(0.75);
    public List<double> BoltColumns { get; set; } = new();
    public List<double> BoltRows { get; set; } = new();
    /// <summary>Plan reference / reason when a dimension is not legible (raised as UNRESOLVED; the part carries the flag).</summary>
    public string? Unresolved { get; set; }
    public CapCoverPlate Clone()
    {
        var c = (CapCoverPlate)MemberwiseClone();
        c.BoltColumns = BoltColumns.ToList();
        c.BoltRows = BoltRows.ToList();
        return c;
    }
}

/// <summary>Girder framing into the cap (end plate moment connection + top flange splice over the cap).</summary>
public sealed class CapGirderConnection
{
    public double EndPlateThickness { get; set; } = In(0.5);
    /// <summary>End plate outline in its plane: x along the cap (right +) from the CL girder, z from the top of the CAP web (underside of the cap top
    /// flange) at the CL girder, down negative (plans dimension Section E-E from the cap).</summary>
    public List<CapPoint> EndPlateOutline { get; set; } = new();
    /// <summary>End plate bolts (same coordinates); they pass through end plate + cap web + connector plate.</summary>
    public List<CapPoint> EndPlateBolts { get; set; } = new();
    public double ConnectorPlateThickness { get; set; } = In(0.5);
    /// <summary>Girder connection diaphragm plate inside the cap, in the girder web plane (0 = none).</summary>
    public double DiaphragmPlateThickness { get; set; } = In(0.75);
    /// <summary>Mill-to-bear plate inside the cap at the bottom flange level (width along the cap, thickness); null = none.</summary>
    public PlateSize? BottomPlate { get; set; }
    /// <summary>OPTIONAL (0 = no weld solids, omitted from the file): fillet size DRAWN for the diaphragm plate to connector plate welds (plan weld symbols
    /// without a size, p.30 Girder Splice elevation). The size is not a plan value: the weld parts are flagged UNRESOLVED (dimension only).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public double DiaphragmWeld { get; set; }
    /// <summary>OPTIONAL (0 = none, omitted from the file): fillet size, both faces, where the diaphragm plate pieces are milled to bear on the bottom plate
    /// (plan "5/16 + 5/16, mill to bear", p.30).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public double BottomPlateWeld { get; set; }
    /// <summary>Girder top flange end beyond the cap flange edge (plans 1").</summary>
    public double TopFlangeEndBeyondCapFlange { get; set; } = In(1);
    /// <summary>Corner radius of the girder web cope under the cap flange (drawn as a chamfer of the same legs).</summary>
    public double CopeRadius { get; set; } = In(2);
    /// <summary>Clear distance from the cope edge to the cap flange underside.</summary>
    public double CopeClearance { get; set; } = In(0.5);
    public PlateSize OuterSplicePlate { get; set; } = new(In(24), In(1.375));
    public PlateSize InnerSplicePlate { get; set; } = new(In(11), In(1.375));
    /// <summary>Gap between the girder web CL and the inner splice plates' inner edges.</summary>
    public double InnerPlateFromWeb { get; set; } = In(1);
    /// <summary>Top splice bolt columns: distances along the girder from the CL cap (both sides symmetric; positive values).</summary>
    public List<double> SpliceBoltColumns { get; set; } = new();
    /// <summary>Top splice bolt rows: offsets from the CL girder (right +).</summary>
    public List<double> SpliceBoltRows { get; set; } = new();
    /// <summary>Plate end beyond the outermost bolt column.</summary>
    public double SplicePlateEndDistance { get; set; } = In(1.5);
    /// <summary>Studs on the outer splice plate: distances from the CL cap (both sides) and rows across (from the CL girder).</summary>
    public List<double> SpliceStudColumns { get; set; } = new();
    public List<double> SpliceStudRows { get; set; } = new();
    public double SpliceStudLength { get; set; } = In(4);
    /// <summary>Plan check: top of cap top flange to the top of the girder top flange at the CL cap (plans "4 1/4 (AT CL CAP)"); NaN = none.</summary>
    public double PlanCapFlangeToGirderFlangeTop { get; set; } = double.NaN;
    public CapGirderConnection Clone()
    {
        var c = (CapGirderConnection)MemberwiseClone();
        c.EndPlateOutline = EndPlateOutline.Select(p => p.Clone()).ToList();
        c.EndPlateBolts = EndPlateBolts.Select(p => p.Clone()).ToList();
        c.BottomPlate = BottomPlate?.Clone();
        c.OuterSplicePlate = OuterSplicePlate.Clone();
        c.InnerSplicePlate = InnerSplicePlate.Clone();
        c.SpliceBoltColumns = SpliceBoltColumns.ToList();
        c.SpliceBoltRows = SpliceBoltRows.ToList();
        c.SpliceStudColumns = SpliceStudColumns.ToList();
        c.SpliceStudRows = SpliceStudRows.ToList();
        return c;
    }
}

/// <summary>A point in a plate's own 2D coordinates.</summary>
public sealed class CapPoint
{
    public double X { get; set; }
    public double Z { get; set; }
    public CapPoint() { }
    public CapPoint(double x, double z) { X = x; Z = z; }
    public CapPoint Clone() => (CapPoint)MemberwiseClone();
}

public enum CapWebSide { Back, Ahead }

/// <summary>A handhole in a cap web at every girder: web (back = down-station member), offset along the cap from the CL girder (right +),
/// centre below the top of the cap web.</summary>
public sealed class CapHandhole
{
    public CapWebSide Web { get; set; }
    public double Offset { get; set; }
    public double Depth { get; set; }
    public CapHandhole() { }
    public CapHandhole(CapWebSide web, double offset, double depth) { Web = web; Offset = offset; Depth = depth; }
    public CapHandhole Clone() => (CapHandhole)MemberwiseClone();
}

/// <summary>Handhole detail: elliptical web opening, octagonal bolted cover plate outside, two backing plates inside (resin bonded), bolts.
/// Coordinates (x along the cap, z up) from the handhole centre.</summary>
public sealed class CapHandholeDetail
{
    public double OpeningWidth { get; set; } = In(6);
    public double OpeningHeight { get; set; } = In(8);
    public double CoverThickness { get; set; } = In(0.5);
    /// <summary>Cover plate outline (closed polygon) around the centre.</summary>
    public List<CapPoint> CoverOutline { get; set; } = new();
    /// <summary>Backing plates (each a closed polygon, the opening is cut out where they overlap it).</summary>
    public List<List<CapPoint>> BackingOutlines { get; set; } = new();
    public double BackingThickness { get; set; } = In(0.5);
    public List<CapPoint> Bolts { get; set; } = new();
    public double BoltDiameter { get; set; } = In(0.75);
    public CapHandholeDetail Clone()
    {
        var c = (CapHandholeDetail)MemberwiseClone();
        c.CoverOutline = CoverOutline.Select(p => p.Clone()).ToList();
        c.BackingOutlines = BackingOutlines.Select(l => l.Select(p => p.Clone()).ToList()).ToList();
        c.Bolts = Bolts.Select(p => p.Clone()).ToList();
        return c;
    }
}

/// <summary>Galvanized wire mesh screen across each cap end between the webs, on angles bolted to the web inner faces.</summary>
public sealed class CapEndScreen
{
    /// <summary>Screen plane inboard of the cap end.</summary>
    public double Inset { get; set; } = In(1);
    public string Angle { get; set; } = "L2x2x1/4";
    /// <summary>Mesh: opening and wire diameter (the panel is one solid of two wire layers' thickness with the wire mass).</summary>
    public double MeshOpening { get; set; } = In(1);
    public double WireDiameter { get; set; } = In(0.25);
    public int BoltsPerAngle { get; set; } = 5;
    public double BoltDiameter { get; set; } = In(0.375);
    /// <summary>Top / bottom bolt from the flange faces (plans 4).</summary>
    public double BoltEndDistance { get; set; } = In(4);
    public string BoltMaterial { get; set; } = "Stainless steel ASTM A193";
    public string MeshMaterial { get; set; } = "Galvanized steel wire mesh";
    public CapEndScreen Clone() => (CapEndScreen)MemberwiseClone();
}
