using SBM.Core.Substructure;

namespace SBM.Core.Bridge;

/// <summary>One cross-section of a <see cref="PlanSolidDefinition"/> loft: a plan polygon (S, N in the unit frame: S along the support line, N normal,
/// up-station +) at an elevation. Every section of one solid has the SAME number of vertices in the same order.</summary>
public sealed class PlanSolidSection
{
    public double Elevation { get; set; }
    public List<FootingPoint> Polygon { get; set; } = new();
    public PlanSolidSection() { }
    public PlanSolidSection(double elevation, IEnumerable<FootingPoint> polygon) { Elevation = elevation; Polygon = polygon.ToList(); }
    public PlanSolidSection Clone() => new(Elevation, Polygon.Select(p => p.Clone()));
}

/// <summary>OPTIONAL generic as-drawn solid of a substructure unit (shaft, tapered column, cap beam, footing, pedestal, tremie seal, abutment
/// wall...). Either a LOFT through <see cref="Sections"/> (bottom to top) or a PROFILE extrusion: a closed (S, elevation) polygon
/// (<see cref="Profile"/>) extruded across N from <see cref="N0"/> to <see cref="N1"/>. Built by <c>SubstructureGenerator.BuildPlanStructure</c>.</summary>
public sealed class PlanSolidDefinition
{
    public string Name { get; set; } = "";
    public SubElementKind Kind { get; set; } = SubElementKind.Column;
    /// <summary>Plan page(s) the dimensions come from (shown in the solid description).</summary>
    public string Source { get; set; } = "";
    public List<PlanSolidSection> Sections { get; set; } = new();
    public List<CapProfilePoint> Profile { get; set; } = new();
    public double N0 { get; set; }
    public double N1 { get; set; }
    /// <summary>True = the profile is in (N, elevation) (<see cref="CapProfilePoint.S"/> holds N) and is extruded across S from <see cref="N0"/> (= S start) to <see cref="N1"/> (= S end).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool ProfileOverN { get; set; }
    /// <summary>Non-empty = the solid is flagged UNRESOLVED (plain reason): carried as an <see cref="Assumption"/> (dimension only) on the solid.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public string Flag { get; set; } = "";
    /// <summary>True = the plans do not show the element at all as built (flag puts it on the UNRESOLVED layer); default false = dimension only.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool ElementUncertain { get; set; }
    /// <summary>OPTIONAL ("" = concrete, omitted from the .sbm): material of a non-concrete solid (e.g. treated timber of a pier fender, steel H pile);
    /// with <see cref="Density"/> (kg/m3; 0 = steel 7850). Such solids do not count as concrete and do not set the unit's plan extents when their kind is
    /// <c>SubElementKind.Fender</c>.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public string? Material { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public double Density { get; set; }
    public PlanSolidDefinition Clone() => new()
    {
        Name = Name, Kind = Kind, Source = Source, Sections = Sections.Select(s => s.Clone()).ToList(),
        Profile = Profile.Select(p => new CapProfilePoint(p.S, p.Elevation)).ToList(), N0 = N0, N1 = N1, ProfileOverN = ProfileOverN, Flag = Flag, ElementUncertain = ElementUncertain,
        Material = Material, Density = Density,
    };
}

/// <summary>OPTIONAL fixed seat (bearing seat from the plans, no superstructure bearing exists): published as <c>SubstructureUnit.FixedSeats</c>.</summary>
public sealed class PlanSeatDefinition
{
    public string Name { get; set; } = "";
    public double S { get; set; }
    public double N { get; set; }
    public double Elevation { get; set; }
    public string Source { get; set; } = "";
}

/// <summary>OPTIONAL group of piles at explicit plan positions (S, N at the FOOTING BOTTOM plane, <see cref="FootingPoint.BatterDirS"/> /
/// <see cref="FootingPoint.BatterDirN"/> = batter direction), head <see cref="HeadEmbedment"/> above the footing bottom, tip at <see cref="TipElevation"/>
/// for plumb piles or <see cref="Length"/> along the axis for battered ones. Pile type and section come from the unit's
/// <c>PileType</c> / <c>PileSection</c>.</summary>
public sealed class PlanPileGroupDefinition
{
    public string Name { get; set; } = "";
    public double FootingBottom { get; set; }
    public double HeadEmbedment { get; set; }
    public double BatterRatio { get; set; } = 4;
    /// <summary>Tip elevation of every pile of the group (batter piles: the tip of the axis-length at the same vertical drop).</summary>
    public double TipElevation { get; set; } = double.NaN;
    public string Source { get; set; } = "";
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public string Flag { get; set; } = "";
    public List<FootingPoint> Piles { get; set; } = new();
    public PlanPileGroupDefinition Clone() => new()
    {
        Name = Name, FootingBottom = FootingBottom, HeadEmbedment = HeadEmbedment, BatterRatio = BatterRatio, TipElevation = TipElevation, Source = Source,
        Flag = Flag, Piles = Piles.Select(p => p.Clone()).ToList(),
    };
}

/// <summary>OPTIONAL plan-drawn substructure of one support (generic; used when the structure is read straight from the plans: tapered shafts, cap
/// beams, odd footings, pile groups, abutment walls). Works without bearing seats (support-lines-only bridges). The standard parametric bent is
/// NOT built when this is set; elements come from the three lists.</summary>
public sealed class PlanSubstructureDefinition
{
    public List<PlanSolidDefinition> Solids { get; set; } = new();
    public List<PlanPileGroupDefinition> PileGroups { get; set; } = new();
    public List<PlanSeatDefinition> Seats { get; set; } = new();
    public PlanSubstructureDefinition Clone() => new()
    {
        Solids = Solids.Select(s => s.Clone()).ToList(), PileGroups = PileGroups.Select(s => s.Clone()).ToList(),
        Seats = Seats.Select(s => new PlanSeatDefinition { Name = s.Name, S = s.S, N = s.N, Elevation = s.Elevation, Source = s.Source }).ToList(),
    };
}
