using SBM.Core.Solids;

namespace SBM.Core.Steel;

/// <summary>Whole-member views of a steel model whose built-up members are split into sub-element parts (<see cref="SteelPart.Member"/>, truss PartPerPiece):
/// scripts / checks that measured "the member's part" use these and work the same with or without the split.</summary>
public static class SteelPartGroups
{
    /// <summary>Every element as one part: parts without a parent as they are, the pieces of a member / joint stack merged into one part (mark = the member,
    /// mesh = the pieces' meshes appended, mass and volume = the sum, category / assembly / material of the first piece), in first-appearance order.</summary>
    public static List<SteelPart> WholeMembers(this SteelModel st)
    {
        var res = new List<SteelPart>();
        var groups = new Dictionary<string, List<SteelPart>>();
        var order = new List<object>();
        foreach (var p in st.Parts)
        {
            if (p.Member == null) { order.Add(p); continue; }
            if (!groups.TryGetValue(p.Member, out var l)) { groups[p.Member] = l = new(); order.Add(p.Member); }
            l.Add(p);
        }
        foreach (var o in order) res.Add(o is SteelPart sp ? sp : Merge((string)o, groups[(string)o]));
        return res;
    }

    /// <summary>The element with this mark as one part (a whole part, or the merged pieces of the member / stack); null = none.</summary>
    public static SteelPart? WholeMember(this SteelModel st, string mark)
    {
        var pieces = st.Parts.Where(p => p.Member == mark).ToList();
        if (pieces.Count > 0) return Merge(mark, pieces);
        return st.Parts.FirstOrDefault(p => p.Mark == mark);
    }

    private static SteelPart Merge(string mark, List<SteelPart> pieces)
    {
        var mesh = new Mesh();
        foreach (var p in pieces) mesh.Append(p.Mesh);
        var f = pieces[0];
        double mass = pieces.Sum(p => p.Mass), len = pieces.Max(p => p.Length);
        return new SteelPart
        {
            Mark = mark, Category = f.Category, Assembly = f.Assembly, Description = f.Description.Contains(" of ") ? f.Description[(f.Description.IndexOf(" of ", StringComparison.Ordinal) + 4)..] : f.Description,
            Mesh = mesh, Material = f.Material, Role = f.Role, Length = len, Assumption = f.Assumption,
            // the merged part weighs what its pieces weigh
            NominalWeightLbPerFt = len > 0 ? mass / (1.48816394 * len) : 0,
        };
    }
}
