namespace SBM.Core.Bridge;

/// <summary>Where the stations of a <see cref="BeamBarSet"/> are measured from.</summary>
public enum BeamBarEnds
{
    /// <summary>Stations from EACH end of the beam (symmetric standards); the set is placed at both ends, mirrored along the beam.
    /// A station from the far end that coincides (within 1/16") with one from the near end is placed once.</summary>
    Both,
    /// <summary>Stations from the START end of the beam over the whole length (e.g. a centre bar 5a2).</summary>
    StartOnly,
}

/// <summary>One corner of a bar centerline in beam coordinates (meters): A along the beam from the bar's station TOWARD MIDSPAN,
/// X right of the beam CL (looking from the start end toward the end), Y above the beam bottom (vertical section).</summary>
public sealed class BeamBarPoint
{
    public double A { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public BeamBarPoint() { }
    public BeamBarPoint(double a, double x, double y) { A = a; X = x; Y = y; }
}

/// <summary>Translation of one copy of a bar set (bars at several levels / lines): X right, Y up (meters).</summary>
public sealed class BeamBarOffset
{
    public double X { get; set; }
    public double Y { get; set; }
    public BeamBarOffset() { }
    public BeamBarOffset(double x, double y) { X = x; Y = y; }
}

/// <summary>
/// One bar mark of a precast beam: its centerline (sharp corners + bend radius) in beam coordinates and the stations along the beam
/// where it is placed (chains from the end of the beam, as the standards dimension them). Every placed bar is a <c>BarPlacement</c>
/// of the beam (schedule, IFC IfcReinforcingBar, viewport, database).
/// </summary>
public sealed class BeamBarSet
{
    public string Mark { get; set; } = "";
    /// <summary>Bar size ("#4").</summary>
    public string Size { get; set; } = "#4";
    public bool Epoxy { get; set; }
    /// <summary>Other coating note ("stainless"); "" = plain or epoxy per <see cref="Epoxy"/>.</summary>
    public string Coating { get; set; } = "";
    /// <summary>Shape code for schedules / IFC role: STR, STIRRUP, HAIRPIN, U, L, Z, C, ... (STIRRUP / HAIRPIN / TIE = IFC LIGATURE).</summary>
    public string ShapeCode { get; set; } = "STR";
    /// <summary>Shape / role text for the schedule ("web hairpin, projects 5 in above the beam").</summary>
    public string Description { get; set; } = "";
    /// <summary>Centerline corners (sharp) in beam coordinates relative to the station; computed from the bend diagram's out-to-out
    /// dimensions and the section clearances (centerline = outside face - db/2).</summary>
    public List<BeamBarPoint> Path { get; set; } = new();
    /// <summary>Centerline bend radius at every interior corner (= pin / 2 + db / 2); 0 = sharp corners (pin not shown).</summary>
    public double BendRadius { get; set; }
    /// <summary>Also place the copy mirrored about the beam CL (x -> -x): bar pairs ("2-6b3").</summary>
    public bool Mirror { get; set; }
    /// <summary>Copies of the path (lines / levels); null = one copy at (0, 0).</summary>
    public List<BeamBarOffset>? Copies { get; set; }
    /// <summary>Stations (meters) along the beam CL from the end of the beam (see <see cref="Ends"/>).</summary>
    public List<double> Stations { get; set; } = new();
    public BeamBarEnds Ends { get; set; } = BeamBarEnds.Both;
    /// <summary>Longitudinal bars: the station is the CLEAR distance from the end face AT THE BAR'S OWN x (a skewed end face moves with x);
    /// false = stations are measured along the beam CL from the end face at the CL (transverse bars, square to the beam).</summary>
    public bool StationAtBar { get; set; }
    /// <summary>Added to every station toward midspan: a bar tied against another one at the same dimensioned station (drawn adjacent).</summary>
    public double AlongOffset { get; set; }
    /// <summary>Out-to-out values printed on the bend diagram (schedule dimensions A, B, C, ...).</summary>
    public List<double> PrintedDims { get; set; } = new();
    /// <summary>Bar list length (meters); 0 = the centerline developed length.</summary>
    public double ListedLength { get; set; }
    /// <summary>Location text for the schedule.</summary>
    public string Location { get; set; } = "";
    /// <summary>Non-empty = NOT placed: the plans do not fix it; raised as this warning (UnresolvedFlag text with page and reason).</summary>
    public string Unresolved { get; set; } = "";

    public BeamBarSet Clone()
    {
        var c = (BeamBarSet)MemberwiseClone();
        c.Path = Path.Select(p => new BeamBarPoint(p.A, p.X, p.Y)).ToList();
        c.Copies = Copies?.Select(o => new BeamBarOffset(o.X, o.Y)).ToList();
        c.Stations = Stations.ToList();
        c.PrintedDims = PrintedDims.ToList();
        return c;
    }
}

/// <summary>One row of the bar list of a beam (counts PER BEAM) for the check.</summary>
public sealed class BeamBarListRow
{
    public string Mark { get; set; } = "";
    public int Count { get; set; }
    /// <summary>Listed length (meters).</summary>
    public double Length { get; set; }
}

/// <summary>
/// Mild reinforcing of a precast prestressed beam (OPTIONAL on <see cref="PrestressedBeamDefinition.Reinforcement"/>; null = no beam bars,
/// output unchanged). Taken from the beam STANDARD by designation (<c>StandardBeamRebar</c>) and overridable per bridge (plan-modified
/// standards). Bars are placed along the beam as built (standard length, skewed or square ends); debonding does not matter.
/// </summary>
public sealed class BeamReinforcementDefinition
{
    /// <summary>Standard / plan reference (sheet, edition, PDF pages) - quoted in the checks.</summary>
    public string Source { get; set; } = "";
    /// <summary>Reinforcing steel yield (ksi) for the material name.</summary>
    public double SteelYieldKsi { get; set; } = 60;
    public List<BeamBarSet> Bars { get; set; } = new();
    /// <summary>Plan / standard items not fixed by the sheets: each raised as a warning (UnresolvedFlag text: page + plain reason).</summary>
    public List<string> Unresolved { get; set; } = new();
    /// <summary>Reading notes (how a plan item was resolved), raised as Info.</summary>
    public List<string> Notes { get; set; } = new();
    /// <summary>The bar list per beam (check of the placed counts / lengths); null = none printed.</summary>
    public List<BeamBarListRow>? BarList { get; set; }
    /// <summary>Printed reinforcing weight per beam (lb); 0 = not printed.</summary>
    public double ListedWeightLb { get; set; }
    /// <summary>Where <see cref="ListedWeightLb"/> is printed.</summary>
    public string ListedWeightSource { get; set; } = "";

    public BeamReinforcementDefinition Clone()
    {
        var c = (BeamReinforcementDefinition)MemberwiseClone();
        c.Bars = Bars.Select(b => b.Clone()).ToList();
        c.Unresolved = Unresolved.ToList();
        c.Notes = Notes.ToList();
        c.BarList = BarList?.Select(r => new BeamBarListRow { Mark = r.Mark, Count = r.Count, Length = r.Length }).ToList();
        return c;
    }
}
