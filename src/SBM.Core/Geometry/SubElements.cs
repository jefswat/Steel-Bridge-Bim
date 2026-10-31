using SBM.Core.Steel;

namespace SBM.Core.Geometry;

/// <summary>What a click in the 3D view selects when parts are sub-elements of a built-up member (<see cref="SteelPart.Member"/>, truss PartPerPiece).</summary>
public enum PickLevel
{
    /// <summary>The whole member / joint gusset stack (default).</summary>
    Member,
    /// <summary>The single piece (ply, angle, cover plate, gusset layer, ...).</summary>
    Part,
}

/// <summary>One sub-element row of a member (database / properties panel list).</summary>
public sealed record SubElementRow(string Mark, string Piece, string Description, double MassKg, double LengthM, SteelPart Part);

/// <summary>Pure logic behind the app's pick level, sub-element list and "Show sub-elements" (no UI; unit tested).</summary>
public static class SubElements
{
    /// <summary>The level a click selects: the setting, flipped while Alt is held.</summary>
    public static PickLevel Effective(PickLevel setting, bool alt) => alt ? (setting == PickLevel.Member ? PickLevel.Part : PickLevel.Member) : setting;

    /// <summary>Parent member / joint stack of a picked object (a <see cref="SteelPart"/> with <see cref="SteelPart.Member"/>), else null.</summary>
    public static string? ParentOf(object? tag) => tag is SteelPart { Member: { Length: > 0 } m } ? m : null;

    /// <summary>What a click on <paramref name="tag"/> selects at <paramref name="level"/>: the parent member mark (Member level and the part has one), or null = the part itself.</summary>
    public static string? Resolve(object? tag, PickLevel level) => level == PickLevel.Member ? ParentOf(tag) : null;

    /// <summary>The tags (pick-table objects) that belong to <paramref name="member"/>; <paramref name="raw"/> maps a tag to its model object.</summary>
    public static List<T> TagsOf<T>(IEnumerable<T> tags, string member, Func<T, object?> raw) where T : notnull =>
        tags.Where(t => ParentOf(raw(t)) == member).ToList();

    /// <summary>The pieces of <paramref name="member"/> in mark order (code, then number: "-A2" before "-A10").</summary>
    public static List<SubElementRow> List(IEnumerable<SteelPart> parts, string member) =>
        parts.Where(p => p.Member == member)
            .OrderBy(p => Code(p.Mark, member)).ThenBy(p => Number(p.Mark, member)).ThenBy(p => p.Mark, StringComparer.Ordinal)
            .Select(p => new SubElementRow(p.Mark, p.Piece ?? "", p.Description, p.Mass, p.Length, p))
            .ToList();

    /// <summary>Alternating tint index of a piece within its member (0 / 1 / 2 ...), for "Show sub-elements": neighbours in the list differ.</summary>
    public static Dictionary<SteelPart, int> Tints(IEnumerable<SteelPart> parts, int colors = 3)
    {
        var res = new Dictionary<SteelPart, int>();
        foreach (var g in parts.Where(p => p.Member != null).GroupBy(p => p.Member!))
        {
            int i = 0;
            foreach (var r in List(g, g.Key)) res[r.Part] = i++ % Math.Max(2, colors);
        }
        return res;
    }

    private static string Suffix(string mark, string member) => mark.StartsWith(member + "-", StringComparison.Ordinal) ? mark[(member.Length + 1)..] : mark;
    private static string Code(string mark, string member) => new(Suffix(mark, member).TakeWhile(c => !char.IsDigit(c)).ToArray());
    private static int Number(string mark, string member)
    {
        var s = Suffix(mark, member);
        var d = new string(s.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(d, out int n) ? n : 0;
    }
}
