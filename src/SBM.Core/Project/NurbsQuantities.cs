using SBM.Core.Solids;

namespace SBM.Core.Project;

/// <summary>One element carrying exact NURBS geometry: exact volume / area from the NURBS vs the tessellated mesh.</summary>
public sealed record NurbsQuantityRow(string Bridge, string Element, string Kind, int Solids, int Faces, int Holes,
    double MeshVolume, double ExactVolume, double MeshArea, double ExactArea, double MaxDeviation)
{
    public double VolumeDiffPercent => ExactVolume > 0 ? (MeshVolume - ExactVolume) / ExactVolume * 100 : 0;
}

/// <summary>Opt-in exact quantities (exact-geometry option): every mesh of a bridge model that carries NURBS solids.</summary>
public static class NurbsQuantities
{
    public static List<NurbsQuantityRow> Rows(BridgeModel m)
    {
        var rows = new List<NurbsQuantityRow>();
        void Add(string name, Mesh? mesh)
        {
            if (mesh?.Nurbs is not { Count: > 0 } ns) return;
            double meshArea = 0;
            for (int i = 0; i < mesh.Indices.Count; i += 3)
            {
                var a = mesh.Positions[mesh.Indices[i]];
                meshArea += Geometry.Vec3.Cross(mesh.Positions[mesh.Indices[i + 1]] - a, mesh.Positions[mesh.Indices[i + 2]] - a).Length / 2;
            }
            rows.Add(new NurbsQuantityRow(m.Definition.Name, name, string.Join("+", ns.Select(s => s.Kind).Distinct()), ns.Count,
                ns.Sum(s => s.Faces.Count), ns.Sum(s => s.Faces.Sum(f => f.Holes.Count)) / 2,
                Math.Abs(mesh.Volume()), mesh.ExactVolume() ?? 0, meshArea, mesh.HasExactNurbs ? ns.Sum(s => s.Area()) : double.NaN,
                ns.Max(s => s.MaxDeviation)));
        }
        if (m.Steel != null) foreach (var p in m.Steel.Parts) Add(p.Mark, p.Mesh);
        if (m.Concrete is { } c)
        {
            Add("Deck slab", c.Deck);
            Add("Haunches", c.Haunches);
            foreach (var (n, b) in c.Barriers) Add(n, b);
        }
        return rows;
    }
}
