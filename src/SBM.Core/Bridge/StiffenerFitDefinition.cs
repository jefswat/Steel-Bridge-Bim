namespace SBM.Core.Bridge;

/// <summary>How a face of a steel part meets the part it bears on (<see cref="SBM.Core.Steel.PartFit"/>).</summary>
public enum FitCondition
{
    /// <summary>End milled (machined) to bear on the other part.</summary>
    MillToBear,
    /// <summary>End finished to bear (ground / flame cut and finished) on the other part.</summary>
    FinishToBear,
    /// <summary>Tight fit against the other part (no bearing requirement, no gap).</summary>
    TightFit,
    /// <summary>Welded to the other part.</summary>
    Welded,
}

/// <summary>Stiffener kind a fit input applies to. Jacking = the plumb jacking stiffeners of <see cref="SupportDefinition.JackingStiffenerOffsets"/>
/// (they take the Bearing entries when none is given for them).</summary>
public enum StiffenerFitKind { Bearing, Jacking, ConnectionPlate, Intermediate }

/// <summary>End of a transverse stiffener: Top = against the top flange, Bottom = against the bottom flange.</summary>
public enum StiffenerEnd { Top, Bottom }

/// <summary>
/// OPTIONAL plan fit condition of one end of a stiffener kind (<see cref="StiffenerDefinition.EndFits"/>, per support
/// <see cref="SupportDefinition.BearingStiffenerFits"/>). Fit conditions come ONLY from these inputs (plan values with a source); an end without an entry
/// has no fit condition (none assumed) and, where AASHTO LRFD sets a requirement, a "POTENTIAL NONCOMPLIANCE" flag. When entries are given for an end and
/// none is <see cref="FitCondition.Welded"/>, that end gets no flange weld.
/// </summary>
public sealed class StiffenerEndFit
{
    public StiffenerFitKind Kind { get; set; }
    public StiffenerEnd End { get; set; }
    public FitCondition Condition { get; set; }
    /// <summary>Plan page / note the condition is read from (e.g. "p.32 F55").</summary>
    public string Source { get; set; } = "";
    public StiffenerEndFit() { }
    public StiffenerEndFit(StiffenerFitKind kind, StiffenerEnd end, FitCondition condition, string source)
    { Kind = kind; End = end; Condition = condition; Source = source; }
    public StiffenerEndFit Clone() => (StiffenerEndFit)MemberwiseClone();
}

/// <summary>Resolution of the stiffener end fits (plan inputs only) and the AASHTO LRFD requirement an end without one may not satisfy. Shared by the
/// steel generator and the stiffener detail sheet so both say the same.</summary>
public static class StiffenerFits
{
    public const string NoncomplianceStatus = "POTENTIAL NONCOMPLIANCE";
    public const string BearingArticle = "AASHTO LRFD 6.10.11.2";
    public const string IntermediateArticle = "AASHTO LRFD 6.10.11.1";
    public const string ConnectionArticle = "AASHTO LRFD 6.6.1.3.1";

    /// <summary>The plan entries for one end (empty = none given). The support's own entries win over the bridge's; jacking stiffeners take the bearing
    /// entries when none is given for them.</summary>
    public static IReadOnlyList<StiffenerEndFit> Of(StiffenerDefinition sd, SupportDefinition? support, StiffenerFitKind kind, StiffenerEnd end)
    {
        foreach (var l in new[] { support?.BearingStiffenerFits, sd.EndFits })
        {
            if (l == null) continue;
            var hit = l.Where(x => x.Kind == kind && x.End == end).ToList();
            if (hit.Count == 0 && kind == StiffenerFitKind.Jacking) hit = l.Where(x => x.Kind == StiffenerFitKind.Bearing && x.End == end).ToList();
            if (hit.Count > 0) return hit;
        }
        return Array.Empty<StiffenerEndFit>();
    }

    /// <summary>The AASHTO LRFD requirement for an end (article, plain text), or null when the specification sets none (bearing stiffener top, intermediate
    /// stiffener bottom = tension flange).</summary>
    public static (string Article, string Requirement)? Requirement(StiffenerFitKind kind, StiffenerEnd end) => kind switch
    {
        StiffenerFitKind.Bearing or StiffenerFitKind.Jacking when end == StiffenerEnd.Bottom =>
            (BearingArticle, "bearing stiffeners must be milled to bear against, or welded to, the flange through which they receive the reaction (the bottom flange on the bearing)"),
        StiffenerFitKind.ConnectionPlate =>
            (ConnectionArticle, "transverse connection plates must be welded or bolted to both the compression and the tension flange"),
        StiffenerFitKind.Intermediate when end == StiffenerEnd.Top =>
            (IntermediateArticle, "stiffeners not used as connection plates must be tight fit at the compression flange (the top flange in positive moment; the model does not identify negative moment regions)"),
        _ => null,
    };

    /// <summary>True when the given conditions meet <see cref="Requirement"/>: bearing stiffener bottom = milled / finished to bear (a weld of unknown
    /// type is not taken as the CJP groove weld the article allows); connection plate = welded; intermediate top = tight fit, bearing or welded.</summary>
    public static bool Satisfies(StiffenerFitKind kind, StiffenerEnd end, IEnumerable<FitCondition> given)
    {
        var g = given.ToList();
        return kind switch
        {
            StiffenerFitKind.Bearing or StiffenerFitKind.Jacking => g.Any(c => c is FitCondition.MillToBear or FitCondition.FinishToBear),
            StiffenerFitKind.ConnectionPlate => g.Contains(FitCondition.Welded),
            _ => g.Count > 0,
        };
    }
}
