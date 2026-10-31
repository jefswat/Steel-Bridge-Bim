using SBM.Core.Bridge;

namespace SBM.Core.Steel;

/// <summary>
/// Contact / fit condition of one FACE of a steel part (<see cref="SteelPart.Fits"/>): mill to bear, finish to bear, tight fit, welded - with the face
/// ("bottom end", "end flange face"), the part it bears on and the source (plan page / note). Fit conditions come only from plan inputs (user rule
/// 2026-10-27): nothing is assumed. One generic record for stiffener ends, arch knuckle plates and integral cap plates.
/// Consumers: app pick properties, IFC Pset SBM_FitConditions (one per distinct set), database props <c>fit1..n</c>, Rhino user text, stiffener detail notes.
/// </summary>
public sealed record PartFit(string Face, FitCondition Condition, string? Against, string Source)
{
    /// <summary>"Mill to bear: bottom end against G1-1-BF1 (p.32 F55)".</summary>
    public string Text => $"{Words(Condition)}: {Face}{(string.IsNullOrEmpty(Against) ? "" : " against " + Against)} ({Source})";

    public static string Words(FitCondition c) => c switch
    {
        FitCondition.MillToBear => "Mill to bear", FitCondition.FinishToBear => "Finish to bear", FitCondition.TightFit => "Tight fit", _ => "Welded",
    };

    /// <summary>(name, value) pairs of a fit list in a fixed order (property list / IFC / database): "Fit1", "Fit2", ...</summary>
    public static IEnumerable<(string Name, string Value)> Fields(IReadOnlyList<PartFit> fits)
    {
        for (int i = 0; i < fits.Count; i++) yield return ($"Fit{i + 1}", fits[i].Text);
    }
}
