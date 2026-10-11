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

/// <summary>
/// Standard text of an UNRESOLVED warning (user rule: every flag names the plan page AND says in plain words WHY the item is open, e.g.
/// "UNRESOLVED (p.17): CMP casing bottom - it is the bottom of the EPS fill, which is in the road plans, not this set."). Diagnostics have no
/// structured reason field, so the reason lives in the message text; build new flags with <see cref="Text"/>.
/// </summary>
public static class UnresolvedFlag
{
    /// <summary>"UNRESOLVED (pages): item - why. then" (<paramref name="then"/> = what the model does instead; optional).</summary>
    public static string Text(string pages, string item, string why, string then = "")
    {
        string p = string.IsNullOrWhiteSpace(pages) ? "page not given" : pages.Trim();
        string s = $"UNRESOLVED ({p}): {item.Trim()} - {why.Trim().TrimEnd('.')}.";
        return then.Trim().Length > 0 ? $"{s} {then.Trim()}" : s;
    }
}
