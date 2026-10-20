using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Bridge;

/// <summary>
/// OPTIONAL weld termination of a bridge (<see cref="BridgeDefinition.Welds"/>; null = the AWS D1.5 practice default, flagged UNRESOLVED on every weld part):
/// every fillet weld generator stops its beads <see cref="HoldBack"/> short of free plate edges and corners, corner clips / copes and the end of contact
/// (<c>Steel.WeldTermination</c>); the weld solids end square there. Per-rule overrides: <c>ArchWeldRule.HoldBack</c>, <c>ArchFinSettings.RibWeldHoldBack</c>.
/// </summary>
public sealed class WeldTerminationDefinition
{
    /// <summary>Hold-back from free plate edges / clips / the end of contact (plans, e.g. ODOT "terminated 3/8 (+/- 1/8) from edges"). NaN = the default
    /// (= weld size, at least 1/4") and flagged UNRESOLVED.</summary>
    public double HoldBack { get; set; } = double.NaN;
    /// <summary>Plan basis of <see cref="HoldBack"/> (page + note), shown in the weld descriptions / checks.</summary>
    public string Source { get; set; } = "";
    /// <summary>OPTIONAL UNRESOLVED reason (page + why) when the value is an extrapolation of a plan detail; empty = not flagged.</summary>
    public string Unresolved { get; set; } = "";
    /// <summary>OPTIONAL stiffener-to-web weld end measured from the TOE of the flange-to-web weld (Iowa DOT 1021W "end of weld to toe of flange to web
    /// weld" X = <see cref="StiffenerWebEndFactor"/> x web thickness, at least <see cref="StiffenerWebEndMin"/>). 0 / 0 = the hold-back from the corner clip only.</summary>
    public double StiffenerWebEndFactor { get; set; }
    public double StiffenerWebEndMin { get; set; }
    public string StiffenerWebEndSource { get; set; } = "";

    /// <summary>AWS D1.5 practice used when the plans give nothing: hold back = the weld size, at least 1/4".</summary>
    public static double Default(double size) => Math.Max(size, In(0.25));

    public WeldTerminationDefinition Clone() => (WeldTerminationDefinition)MemberwiseClone();
}
