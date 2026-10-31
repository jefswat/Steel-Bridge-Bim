namespace SBM.Core.Bridge;

// Precast segmental box: reinforcing placed in EVERY segment from face-referenced section paths (OPTIONAL SegmentalDefinition.SectionBars;
// null = none, omitted from the .sbm, output unchanged). Generator: Segmental/SegmentalGeneratorBars.cs. Plan data: samples/plans/rebar-bonner-unit5.csx.
//
// Section faces (right half, section coordinates x lateral from CL girder, y DOWN from the deck top; prefix "L:" = the mirror face on the left):
//   outer  TOP, TIP (wing tip, vertical), WINGFLAT (wing underside flat at the tip), WING (sloped wing underside), WEBOUT (outside web face), SOFFIT
//   void   TOPIN (top slab underside flat), HAUNCH (top haunch underside), WEBIN (inside web face), BHAUNCH (bottom slab haunch), BOTIN (bottom slab top)
//   ends   H / V = horizontal / vertical line through the previous point (free legs only); CL = the vertical through x = 0.
// Offsets are measured NORMAL to the face, from the face to the bar centerline, positive INTO the concrete. Void faces exist only where the section is
// hollow at the bar's station: a bar that needs one at a solid station is not placed (counted, reported).

/// <summary>One vertex of a bar path in the section: a CORNER (Face @ Offset meets Face2 @ Offset2), a point ON Face @ Offset at a given X or Y,
/// or a FREE leg from the previous vertex (a free FIRST vertex is measured back from the second one): <see cref="Length"/> parallel to Face (or "H" / "V"), in the direction whose dot product with
/// (<see cref="DirX"/>, <see cref="DirY"/>) is positive (DirY positive = DOWN).</summary>
public sealed class SegmentalBarPoint
{
    public string Face { get; set; } = "";
    public double Offset { get; set; }
    public string Face2 { get; set; } = "";
    public double Offset2 { get; set; }
    public double X { get; set; } = double.NaN;
    public double Y { get; set; } = double.NaN;
    public double Length { get; set; } = double.NaN;
    public double DirX { get; set; }
    public double DirY { get; set; }
    /// <summary>Anchor only: used as the previous point of the next (free) vertex, not part of the bar.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool Hidden { get; set; }
    public SegmentalBarPoint Clone() => (SegmentalBarPoint)MemberwiseClone();
}

public enum SegmentalSectionBarKind { Transverse, Longitudinal }

/// <summary>Joint the along-positions of a set are measured from: centred in the segment, its lower / higher station end, or the end toward / away from
/// its cantilever bent (closure joints / end segments: BentSide = Start).</summary>
public enum SegmentalBarAlongRef { Centre, Start, End, BentSide, TipSide }

/// <summary>
/// One plan mark in a group of segments. TRANSVERSE: the bar centerline = <see cref="Path"/> resolved in the section at each along-position (count /
/// spacing, centred or from a joint, or explicit <see cref="Positions"/>), <see cref="Mirror"/> = the mirror image too (both webs / edges).
/// LONGITUDINAL: <see cref="Path"/> = a distribution line (2 or more points); <see cref="Count"/> bars equally spaced over it (ends included; Count 1 = the
/// first point), each running along the segment between the joints less <see cref="EndClear"/>, following the section at <see cref="LongSamples"/> stations.
/// Schedule count per segment = placed + <see cref="Unplaced"/>. <see cref="ListedLength"/> 0 = length from the model (out-to-out segment sum).
/// </summary>
public sealed class SegmentalSectionBarSet
{
    public string Mark { get; set; } = "";
    public string Size { get; set; } = "#4";
    public string Unit { get; set; } = "";
    /// <summary>Regex on the segment names.</summary>
    public string Segments { get; set; } = "";
    public SegmentalSectionBarKind Kind { get; set; }
    public List<SegmentalBarPoint> Path { get; set; } = new();
    public bool Mirror { get; set; }
    public SegmentalBarAlongRef AlongRef { get; set; }
    /// <summary>Distance from the reference joint to the first bar (NaN = the set is centred in the segment, or in the zone).</summary>
    public double First { get; set; } = double.NaN;
    public int Count { get; set; }
    /// <summary>Spacing; 0 with Count > 1 = equal spaces between <see cref="First"/> from both joints (closure "n EQ. SPA.").</summary>
    public double Spacing { get; set; }
    /// <summary>Index of a free leg whose length is solved so the out-to-out length = <see cref="ListedLength"/> (-1 = none).</summary>
    public int FitLeg { get; set; } = -1;
    /// <summary>Explicit distances from the reference joint (transverse; replaces Count / Spacing / First).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public List<double>? Positions { get; set; }
    /// <summary>Longitudinal: clear distance of the bar ends from the joint faces (each end) and the sample stations along the segment.</summary>
    public double EndClear { get; set; }
    public int LongSamples { get; set; } = 2;
    /// <summary>Longitudinal: limit to this length from the reference joint (NaN = the whole segment).</summary>
    public double ZoneLength { get; set; } = double.NaN;
    /// <summary>Counted per segment, not placed (plan count without enough geometry).</summary>
    public int Unplaced { get; set; }
    public double ListedLength { get; set; }
    public string ShapeCode { get; set; } = "STR";
    public string PrintedDims { get; set; } = "";
    public double Cover { get; set; } = double.NaN;
    public double CoverTop { get; set; } = double.NaN;
    public double CoverBottom { get; set; } = double.NaN;
    public string Location { get; set; } = "";
    public bool Epoxy { get; set; }
    public string Source { get; set; } = "";
    /// <summary>Scaled / derived positions (UnresolvedFlag text, dimension only): bars are placed, the text goes into the mark location + one warning.</summary>
    public string Unresolved { get; set; } = "";
    public SegmentalSectionBarSet Clone()
    {
        var c = (SegmentalSectionBarSet)MemberwiseClone();
        c.Path = Path.Select(p => p.Clone()).ToList(); c.Positions = Positions?.ToList();
        return c;
    }
}
