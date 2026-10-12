using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Bridge;

// OPTIONAL steel TUB (box) girder superstructure (GirderLayoutDefinition.Tub; null = steel I-girders, the default).
// Every girder of the bridge is then a tub: two inclined webs, one bottom flange, two top flanges. The girder path (framing)
// is the TOP WORKING POINT = top of web on the tub CL; plate sizes come from GirderSection.Layouts (top flange width = ONE
// top flange; web "width" ignored), field splices from BridgeDefinition.FieldSplices, all as for I-girders.
// Lengths in meters. Everything not listed by the plans is left out and reported (UNRESOLVED / GAP), never invented.

/// <summary>A point on one girder: the CL bearing of a support (framing index) or a field splice (plan number), plus an
/// offset along the girder (path distance, + ahead / up-station).</summary>
public sealed class TubLocation
{
    public int Support { get; set; } = -1;
    public int Splice { get; set; }
    public double Offset { get; set; }
    public TubLocation() { }
    public static TubLocation AtSupport(int support, double offset = 0) => new() { Support = support, Offset = offset };
    public static TubLocation AtSplice(int splice, double offset = 0) => new() { Splice = splice, Offset = offset };
    public TubLocation Clone() => (TubLocation)MemberwiseClone();
    public override string ToString() => (Support >= 0 ? $"brg {Support + 1}" : $"FS{Splice}") + (Offset != 0 ? $" {Offset / 0.3048:+0.000;-0.000} ft" : "");
}

/// <summary>Bracing panels of one girder in one span (plans' "n EQ. SPA."), in order from the span's begin bearing:
/// <see cref="Spaces"/> equal spaces over <see cref="Length"/> along the girder (NaN = the rest of the span).</summary>
public sealed class TubPanelSegment
{
    public int Girder { get; set; }
    public int Span { get; set; }
    public double Length { get; set; } = double.NaN;
    public int Spaces { get; set; }
    public TubPanelSegment Clone() => (TubPanelSegment)MemberwiseClone();
}

/// <summary>Internal K (inverted V) cross frames + top struts at the panel points (plans: intermediate bracing detail).</summary>
public sealed class TubInternalBracingDefinition
{
    /// <summary>Panel points k (1 = first interior point of the span) with k % <see cref="CrossFrameEvery"/> == 0 get a cross frame,
    /// the others a top strut only (plans: solid lines = internal cross frames, dashed = top struts / stiffeners).</summary>
    public int CrossFrameEvery { get; set; } = 2;
    public string Diagonal { get; set; } = "L5x5x5/8";
    public string TopStrut { get; set; } = "WT6x29";
    /// <summary>Top of the strut flange below the top-of-web line (Detail C: 1").</summary>
    public double StrutBelowWebTop { get; set; } = In(1);
    public double GussetThickness { get; set; } = In(0.75);
    /// <summary>Transverse (connection) stiffener on each web at every panel point: outstand x thickness.</summary>
    public PlateSize Stiffener { get; set; } = new(In(10), In(0.75));
    /// <summary>Gap between the stiffener bottom and the bottom flange (Detail B1).</summary>
    public double StiffenerBottomGap { get; set; } = In(1.5);
    /// <summary>Bottom tab plate on the bottom flange at each diagonal: width (along the frame) x height x thickness (Detail B1).</summary>
    public double TabWidth { get; set; } = In(10);
    public double TabHeight { get; set; } = In(12);
    public double TabThickness { get; set; } = In(0.75);
    public int DiagonalBolts { get; set; } = 3;
    public double BoltPitch { get; set; } = In(3);
    /// <summary>Plan items of the frames that are not dimensioned (gusset outline ...): reported, nothing invented beyond the drawn proportions.</summary>
    public List<string> Unresolved { get; set; } = new();
    public TubInternalBracingDefinition Clone()
    {
        var c = (TubInternalBracingDefinition)MemberwiseClone();
        c.Stiffener = Stiffener.Clone(); c.Unresolved = Unresolved.ToList();
        return c;
    }
}

/// <summary>Top lateral bracing (Warren) inside each tub just below the top flanges (plans: lateral bracing detail).</summary>
public sealed class TubLateralBracingDefinition
{
    public bool Enabled { get; set; } = true;
    public string Member { get; set; } = "WT9x38";
    /// <summary>Work point on the top flange CL this far from the stiffener at a panel point (8").</summary>
    public double WorkPointFromPanelPoint { get; set; } = In(8);
    /// <summary>Same from a support diaphragm CL per support (framing index); missing = <see cref="WorkPointFromPanelPoint"/>.</summary>
    public Dictionary<int, double> WorkPointFromSupport { get; set; } = new();
    /// <summary>Bolt lines (each side of the member axis, at the gage) x bolts per line at each end.</summary>
    public int BoltsPerLine { get; set; } = 3;
    public double BoltPitch { get; set; } = In(3);
    public double BoltGage { get; set; } = In(4.0625);
    public TubLateralBracingDefinition Clone() { var c = (TubLateralBracingDefinition)MemberwiseClone(); c.WorkPointFromSupport = new(WorkPointFromSupport); return c; }
}

/// <summary>External intermediate diaphragm between two adjacent tubs (plate girder bolted to angles on the webs).</summary>
public sealed class TubExternalDiaphragmDefinition
{
    /// <summary>Locations: (span, panel point k of the reference girder <see cref="ReferenceGirder"/>); the line is radial to the alignment through
    /// that point, the other girder's matching panel point is the nearest one (the distance is reported).</summary>
    public List<TubPanelRef> Points { get; set; } = new();
    public int ReferenceGirder { get; set; }
    public double WebThickness { get; set; } = In(0.625);
    /// <summary>Web depth between the flanges and the web's top below the top-of-web line.</summary>
    public double WebDepth { get; set; } = In(38);
    public double WebTopBelowWebTop { get; set; } = In(6);
    public PlateSize Flange { get; set; } = new(In(12), In(0.75));
    public string Angle { get; set; } = "L8x8x3/4";
    /// <summary>Angle length along the tub web and bolts per angle on the tub web / on the diaphragm web.</summary>
    public double AngleLength { get; set; } = In(32);
    public int GirderLegBolts { get; set; } = 8;
    public int DiaphragmLegBolts { get; set; } = 9;
    public double BoltPitch { get; set; } = In(3.5);
    /// <summary>Clear distance between the diaphragm web end and the tub web.</summary>
    public double EndClearance { get; set; } = In(0.5);
    public TubExternalDiaphragmDefinition Clone()
    {
        var c = (TubExternalDiaphragmDefinition)MemberwiseClone();
        c.Points = Points.Select(p => p.Clone()).ToList(); c.Flange = Flange.Clone();
        return c;
    }
}

/// <summary>A panel point: span (0-based) and point k (1 = first interior panel point from the span's begin bearing).</summary>
public sealed class TubPanelRef
{
    public int Span { get; set; }
    public int Point { get; set; }
    public TubPanelRef() { }
    public TubPanelRef(int span, int point) { Span = span; Point = point; }
    public TubPanelRef Clone() => (TubPanelRef)MemberwiseClone();
}

public enum TubDiaphragmConnection { Angles, BentPlates }

/// <summary>Support (pier / end) diaphragm on one support line: an interior diaphragm in every tub and an exterior diaphragm between the tubs.</summary>
public sealed class TubSupportDiaphragmDefinition
{
    /// <summary>Support framing index.</summary>
    public int Support { get; set; }
    public double InteriorWebThickness { get; set; } = In(0.75);
    /// <summary>Interior top flange plate between the tub top flanges (width along the girder x thickness); width 0 = none.</summary>
    public PlateSize InteriorTopFlange { get; set; } = new(In(12), In(1));
    public double ExteriorWebThickness { get; set; } = In(0.75);
    public PlateSize ExteriorFlange { get; set; } = new(In(12), In(1));
    /// <summary>Bearing stiffeners, near and far side of the interior diaphragm web (outstand x thickness), pair centres this far apart across the tub.</summary>
    public PlateSize BearingStiffener { get; set; } = new(In(8), In(1));
    public double BearingStiffenerSpacing { get; set; } = In(18);
    /// <summary>Access opening in the interior diaphragm web (width x height, corner radius, bottom above the bottom flange); width 0 = none.</summary>
    public double AccessOpeningWidth { get; set; }
    public double AccessOpeningHeight { get; set; }
    public double AccessOpeningRadius { get; set; }
    public double AccessOpeningBottom { get; set; }
    public TubDiaphragmConnection Connection { get; set; } = TubDiaphragmConnection.Angles;
    /// <summary>Connection angle (Angles) or bent plate leg x leg x thickness written as an angle name (BentPlates: "L8x8x1/2" = bent PL 1/2 x 16).</summary>
    public string ConnectionShape { get; set; } = "L8x8x1/2";
    public double ConnectionLength { get; set; } = In(42.5);
    public int GirderLegBolts { get; set; } = 11;
    public int DiaphragmLegBolts { get; set; } = 12;
    public double BoltPitch { get; set; } = In(3.5);
    public List<string> Unresolved { get; set; } = new();
    public TubSupportDiaphragmDefinition Clone()
    {
        var c = (TubSupportDiaphragmDefinition)MemberwiseClone();
        c.InteriorTopFlange = InteriorTopFlange.Clone(); c.ExteriorFlange = ExteriorFlange.Clone(); c.BearingStiffener = BearingStiffener.Clone();
        c.Unresolved = Unresolved.ToList();
        return c;
    }
}

/// <summary>Bolted field splice of a tub (plans' field splice details). Bolt rows / columns measured from the splice CL.</summary>
public sealed class TubSpliceDetail
{
    public double BoltDiameter { get; set; } = In(1);
    // top flange (each): outer plate full width on top, two inner plates below each side of the web
    public double TopOuterThickness { get; set; } = In(0.75);
    public double TopInnerThickness { get; set; } = In(0.75);
    public double TopInnerWidth { get; set; } = In(9.5);
    public double TopPlateLength { get; set; } = In(38.5);
    /// <summary>Bolt lines across the top flange, from the flange CL (+ = toward the outside of the tub).</summary>
    public List<double> TopLines { get; set; } = new() { In(-8.5), In(-3), In(3), In(8.5) };
    public int TopRowsPerSide { get; set; } = 6;
    public double TopPitch { get; set; } = In(3);
    // webs: two plates, bolt columns each side, rows centred on the plate
    public double WebPlateThickness { get; set; } = In(0.625);
    public double WebPlateWidth { get; set; } = In(22.5);
    public double WebPlateHeight { get; set; } = In(42.5);
    public int WebColumnsPerSide { get; set; } = 3;
    public double WebColumnPitch { get; set; } = In(3.5);
    public int WebRows { get; set; } = 12;
    public double WebRowPitch { get; set; } = In(3.5);
    // bottom flange: two plates (inside + outside), bolt columns across, rows each side
    public double BottomPlateWidth { get; set; } = In(63.5);
    public double BottomPlateLength { get; set; } = In(36.5);
    public int BottomColumns { get; set; } = 18;
    public double BottomColumnPitch { get; set; } = In(3.5);
    public int BottomRowsPerSide { get; set; } = 5;
    public double BottomRowPitch { get; set; } = In(3.5);
    /// <summary>First row / column from the splice CL.</summary>
    public double FirstFromJoint { get; set; } = In(2.25);
    /// <summary>Per splice (plan number): bottom plate thickness and an optional top-flange filler (thickness x width x length).</summary>
    public List<TubSpliceOverride> Splices { get; set; } = new();
    public TubSpliceDetail Clone()
    {
        var c = (TubSpliceDetail)MemberwiseClone();
        c.TopLines = TopLines.ToList(); c.Splices = Splices.Select(s => s.Clone()).ToList();
        return c;
    }
}

public sealed class TubSpliceOverride
{
    public int Number { get; set; }
    public double BottomPlateThickness { get; set; } = In(0.875);
    public double FillerThickness { get; set; }
    public double FillerWidth { get; set; }
    public double FillerLength { get; set; }
    public TubSpliceOverride Clone() => (TubSpliceOverride)MemberwiseClone();
}

/// <summary>Bearing assembly under one tub (plans' bearing assembly schedule): laminated elastomeric pad + beveled plate.</summary>
public sealed class TubBearingDefinition
{
    public int Support { get; set; }
    public string Mark { get; set; } = "";
    /// <summary>Pad "B" along the girder, "C" across, total height "H".</summary>
    public double PadLength { get; set; }
    public double PadWidth { get; set; }
    public double PadHeight { get; set; }
    public int InteriorLayers { get; set; }
    public double InteriorLayer { get; set; }
    public int Laminates { get; set; }
    public double ExteriorLayer { get; set; }
    /// <summary>Beveled plate: thickness at its centre "J", width "K" (across), length "L" (along).</summary>
    public double PlateThickness { get; set; }
    public double PlateWidth { get; set; }
    public double PlateLength { get; set; }
    /// <summary>Plan bevels (check values, %/100): transverse "M" and longitudinal "N" at the plate CL.</summary>
    public double PlanTransverseSlope { get; set; } = double.NaN;
    public double PlanLongitudinalSlope { get; set; } = double.NaN;
    /// <summary>Plan "H": top of slab to the bottom of the bearing assembly at the CL bearing (check value).</summary>
    public double PlanH { get; set; } = double.NaN;
    /// <summary>Plan horizontal offset of the CL bearing from the working point (check value, 3").</summary>
    public double PlanHorizontalOffset { get; set; } = double.NaN;
    public double AnchorDiameter { get; set; } = In(2);
    public double AnchorAcross { get; set; } = In(21.5);
    /// <summary>Anchor bolt rows along the girder from the CL bearing (+ ahead); empty = none.</summary>
    public List<double> AnchorRows { get; set; } = new();
    public double AnchorEmbedment { get; set; } = In(15);
    public TubBearingDefinition Clone() { var c = (TubBearingDefinition)MemberwiseClone(); c.AnchorRows = AnchorRows.ToList(); return c; }
}

/// <summary>Access hatch in a bottom flange: slotted hole (width across x length along, round ends) and a hinged door plate.</summary>
public sealed class TubAccessHatchDefinition
{
    public int Girder { get; set; }
    public TubLocation At { get; set; } = new();
    public double HoleWidth { get; set; } = In(18);
    public double HoleLength { get; set; } = In(33);
    public double DoorThickness { get; set; } = In(0.375);
    public double DoorWidth { get; set; } = In(24);
    public double DoorLength { get; set; } = In(36);
    public TubAccessHatchDefinition Clone() { var c = (TubAccessHatchDefinition)MemberwiseClone(); c.At = At.Clone(); return c; }
}

/// <summary>Studs on the top flanges: equal spaces between two locations (plans: "n SPA. AT x MAX.").</summary>
public sealed class TubStudZone
{
    public int Girder { get; set; }
    public TubLocation From { get; set; } = new();
    public TubLocation To { get; set; } = new();
    public int Spaces { get; set; }
    /// <summary>Plan maximum pitch (check value).</summary>
    public double MaxPitch { get; set; }
    public TubStudZone Clone() => new() { Girder = Girder, From = From.Clone(), To = To.Clone(), Spaces = Spaces, MaxPitch = MaxPitch };
}

public enum TubFlangeZone { Bottom, Top, Both }

/// <summary>Tension flange zone (plans' "TENSION FLANGE" row: which flanges are tension members, fracture critical / Charpy zone 2).</summary>
public sealed class TubTensionZone
{
    public int Girder { get; set; }
    public TubLocation From { get; set; } = new();
    public TubLocation To { get; set; } = new();
    public TubFlangeZone Flanges { get; set; }
    public TubTensionZone Clone() => new() { Girder = Girder, From = From.Clone(), To = To.Clone(), Flanges = Flanges };
}

/// <summary>Deck pour section (plans' pour sequence): data only, reported; the slab solid is not split.</summary>
public sealed class DeckPourDefinition
{
    /// <summary>Plan pour section number (1, 2, 3 ... = pour order).</summary>
    public int Section { get; set; }
    public double FromStation { get; set; }
    public double ToStation { get; set; }
    public string Note { get; set; } = "";
    public DeckPourDefinition Clone() => (DeckPourDefinition)MemberwiseClone();
}

public sealed class TubGirderDefinition
{
    /// <summary>Horizontal distance between the web working points at the top (8'-0"), measured in the rotated section.</summary>
    public double TopWebSpacing { get; set; } = Ft(8);
    /// <summary>Web slope: horizontal per vertical (4 vertical : 1 horizontal = 0.25).</summary>
    public double WebSlope { get; set; } = 0.25;
    /// <summary>The section is rotated about the top working point with the plane of the deck (plans' general note 2).</summary>
    public bool RotateWithDeck { get; set; } = true;
    /// <summary>Plan web length along the slope (check value, 4'-3 9/16").</summary>
    public double PlanWebSlopeLength { get; set; } = double.NaN;
    public List<TubPanelSegment> Panels { get; set; } = new();
    public TubInternalBracingDefinition Internal { get; set; } = new();
    public TubLateralBracingDefinition Lateral { get; set; } = new();
    public TubExternalDiaphragmDefinition External { get; set; } = new();
    public List<TubSupportDiaphragmDefinition> SupportDiaphragms { get; set; } = new();
    public TubSpliceDetail Splice { get; set; } = new();
    public List<TubBearingDefinition> Bearings { get; set; } = new();
    public List<TubAccessHatchDefinition> Hatches { get; set; } = new();
    public List<TubStudZone> StudZones { get; set; } = new();
    /// <summary>Stud positions across each top flange from its CL and the stud size (plans: 3 - 7/8" x 7" @ 7").</summary>
    public List<double> StudOffsets { get; set; } = new() { In(-7), 0, In(7) };
    public double StudDiameter { get; set; } = In(0.875);
    public double StudLength { get; set; } = In(7);
    public List<TubTensionZone> TensionZones { get; set; } = new();
    public List<DeckPourDefinition> DeckPours { get; set; } = new();
    /// <summary>Plan structural steel quantity for the unit (lb) for the check; NaN = not given.</summary>
    public double PlanSteelWeightLb { get; set; } = double.NaN;
    public List<string> Unresolved { get; set; } = new();

    public TubGirderDefinition Clone()
    {
        var c = (TubGirderDefinition)MemberwiseClone();
        c.Panels = Panels.Select(p => p.Clone()).ToList();
        c.Internal = Internal.Clone(); c.Lateral = Lateral.Clone(); c.External = External.Clone();
        c.SupportDiaphragms = SupportDiaphragms.Select(x => x.Clone()).ToList();
        c.Splice = Splice.Clone();
        c.Bearings = Bearings.Select(x => x.Clone()).ToList();
        c.Hatches = Hatches.Select(x => x.Clone()).ToList();
        c.StudZones = StudZones.Select(x => x.Clone()).ToList();
        c.StudOffsets = StudOffsets.ToList();
        c.TensionZones = TensionZones.Select(x => x.Clone()).ToList();
        c.DeckPours = DeckPours.Select(x => x.Clone()).ToList();
        c.Unresolved = Unresolved.ToList();
        return c;
    }
}
