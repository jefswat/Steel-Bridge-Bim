namespace SBM.Core;

/// <summary>
/// A modeled element whose position / size the plans do not fix. Carried by the element (e.g. <c>MiscElement.Assumption</c>) so every consumer can filter by it:
/// the viewport shows such elements in the <c>3D-UNRESOLVED</c> layer with the reason in the pick properties, IFC writes Pset <c>SBM_Status</c>,
/// the companion database stores status / reason / basis in the element properties (view <c>v_unresolved</c>). Reusable by any generator.
/// </summary>
/// <param name="Status">Currently always "UNRESOLVED" (kept as text so other states can be added).</param>
/// <param name="Reason">What the plans lack, with the plan page (PDF) where it should be.</param>
/// <param name="Basis">What was assumed to model the element anyway.</param>
public sealed record Assumption(string Status, string Reason, string Basis)
{
    public const string UnresolvedStatus = "UNRESOLVED";
    public static Assumption Unresolved(string reason, string basis) => new(UnresolvedStatus, reason, basis);
}
