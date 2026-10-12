namespace SBM.Core.Bridge;

/// <summary>Coordinate frame of a <see cref="PlanBarSet"/>.</summary>
public enum PlanBarFrame
{
    /// <summary>Along a support line: W = distance along the line from the alignment point (+ right, as the substructure S), U = normal to the line
    /// (abutments: + toward the span; piers: + ahead station), as the concrete diaphragm sections.</summary>
    Support,
    /// <summary>Along the alignment: W = station, U = offset (+ right).</summary>
    Alignment,
}

/// <summary>Elevation reference of a <see cref="PlanBarPoint"/>.</summary>
public enum PlanBarZRef
{
    /// <summary>Absolute elevation (<see cref="PlanBarPoint.Dz"/> is the elevation).</summary>
    Elevation,
    /// <summary>Finished deck surface (<see cref="DeckDefinition.FinishedElevation"/>) at the bar point's plan position + Dz.</summary>
    DeckTop,
}

/// <summary>A centerline vertex of a plan bar in the set's frame (sharp corners; the plan's bend diagrams are out to out, the caller puts the centerline db/2 inside).</summary>
public sealed class PlanBarPoint
{
    public double W { get; set; }
    public double U { get; set; }
    public double Dz { get; set; }
    public PlanBarZRef Ref { get; set; }
    public PlanBarPoint() { }
    public PlanBarPoint(double w, double u, PlanBarZRef r, double dz) { W = w; U = u; Ref = r; Dz = dz; }
    public PlanBarPoint Clone() => (PlanBarPoint)MemberwiseClone();
}

/// <summary>
/// OPTIONAL plan-driven bars of one mark placed as the plan sheets dimension them (generic: abutment footings / stems / pedestals / walls, concrete
/// diaphragms, barriers): one centerline <see cref="Path"/> in the frame of a support line or of the alignment, copied at the <see cref="Positions"/>
/// (shifts along W; empty = one bar). Nothing is invented: a set whose position the plans do not give carries <see cref="Unresolved"/> (page + reason)
/// and is NOT placed - its <see cref="UnplacedCount"/> bars stay in the schedule at the listed length.
/// </summary>
public sealed class PlanBarSet
{
    public string Name { get; set; } = "";
    public string Mark { get; set; } = "";
    /// <summary>Bar size "#3" .. "#18".</summary>
    public string Size { get; set; } = "#5";
    /// <summary>Coating / material as listed ("epoxy", "chromium-epoxy (4% Cr)", "stainless", "plain").</summary>
    public string Coating { get; set; } = "epoxy";
    public string Location { get; set; } = "";
    public string Source { get; set; } = "";
    public PlanBarFrame Frame { get; set; }
    /// <summary>Support index (<see cref="PlanBarFrame.Support"/> frame; also the substructure unit of a non-superstructure set).</summary>
    public int Support { get; set; }
    /// <summary>True = superstructure bar (deck / diaphragm / barrier: the bridge's concrete model); false = the substructure unit of <see cref="Support"/>.</summary>
    public bool Superstructure { get; set; }
    public List<PlanBarPoint> Path { get; set; } = new();
    /// <summary>W shifts of the copies (empty = one bar at 0).</summary>
    public List<double> Positions { get; set; } = new();
    /// <summary>Bar-list length (scheduled); 0 = the developed centerline length.</summary>
    public double ListedLength { get; set; }
    /// <summary>Printed out-to-out dimensions of the bend diagram (schedule).</summary>
    public List<double> Dims { get; set; } = new();
    /// <summary>Shape description for the schedule ("U", "L", "closed stirrup" ...).</summary>
    public string Shape { get; set; } = "";
    /// <summary>Non-empty: the set is not placed (what the plans lack + page); <see cref="UnplacedCount"/> bars stay in the schedule.</summary>
    public string Unresolved { get; set; } = "";
    public int UnplacedCount { get; set; }
    /// <summary>Required clear cover (cover check report; 0 = no check).</summary>
    public double Cover { get; set; }
    public PlanBarSet Clone()
    {
        var c = (PlanBarSet)MemberwiseClone();
        c.Path = Path.Select(p => p.Clone()).ToList();
        c.Positions = Positions.ToList();
        c.Dims = Dims.ToList();
        return c;
    }
}

/// <summary>One line of a plan bill of reinforcement (count, length and weight as printed) the placed sets are compared with.</summary>
public sealed class PlanBarListLine
{
    /// <summary>"" = superstructure; otherwise the support index of the substructure unit.</summary>
    public bool Superstructure { get; set; }
    public int Support { get; set; }
    public string Mark { get; set; } = "";
    public string Size { get; set; } = "#5";
    public string Coating { get; set; } = "";
    public string Location { get; set; } = "";
    public int Count { get; set; }
    public double Length { get; set; }
    public double WeightLb { get; set; }
    public PlanBarListLine Clone() => (PlanBarListLine)MemberwiseClone();
}

/// <summary>OPTIONAL plan bars of a bridge (<see cref="BridgeDefinition.PlanBars"/>): placed sets + the plan bills they are checked against. Bill lines
/// with no placed set stay in the schedule (count / length as printed, no 3D bar) and are reported.</summary>
public sealed class PlanBarsDefinition
{
    public List<PlanBarSet> Sets { get; set; } = new();
    public List<PlanBarListLine> BarList { get; set; } = new();
    /// <summary>Plan items not dimensioned (page + reason), raised as UNRESOLVED warnings.</summary>
    public List<string> Unresolved { get; set; } = new();
    public PlanBarsDefinition Clone() => new()
    {
        Sets = Sets.Select(s => s.Clone()).ToList(), BarList = BarList.Select(l => l.Clone()).ToList(), Unresolved = Unresolved.ToList(),
    };
}
