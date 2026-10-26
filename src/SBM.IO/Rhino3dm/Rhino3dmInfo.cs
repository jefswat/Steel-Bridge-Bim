using RF = Rhino.FileIO;
using RG = Rhino.Geometry;

namespace SBM.IO.Rhino3dm;

/// <summary>Read-back summary of a 3DM file (verification of an export): units, layers, objects by layer and geometry type, blocks, notes.</summary>
public sealed class Rhino3dmSummary
{
    public string Units { get; set; } = "";
    public int Objects { get; set; }
    public int Layers { get; set; }
    public int BlockDefinitions { get; set; }
    public List<string> LayerPaths { get; } = new();
    /// <summary>Full layer path -> geometry type -> count.</summary>
    public SortedDictionary<string, SortedDictionary<string, int>> ByLayer { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, int> ByType { get; } = new(StringComparer.Ordinal);
    /// <summary>Block definition name -> number of instance references.</summary>
    public SortedDictionary<string, int> BlockUse { get; } = new(StringComparer.Ordinal);
    public int ObjectsWithGlobalId { get; set; }
    public int ClosedBreps { get; set; }
    public int OpenBreps { get; set; }
    public string Notes { get; set; } = "";
    public Dictionary<string, string> DocumentStrings { get; } = new();
}

public static class Rhino3dmInfo
{
    public static Rhino3dmSummary Read(string path)
    {
        using var f = RF.File3dm.Read(path) ?? throw new IOException($"Cannot read {path} as a 3DM file.");
        var s = new Rhino3dmSummary { Units = f.Settings.ModelUnitSystem.ToString(), Notes = f.Notes?.Notes ?? "" };
        var layerPath = new Dictionary<int, string>();
        foreach (var l in f.AllLayers)
        {
            layerPath[l.Index] = l.FullPath;
            s.LayerPaths.Add(l.FullPath);
        }
        s.Layers = s.LayerPaths.Count;
        var defs = new Dictionary<Guid, string>();
        foreach (var d in f.AllInstanceDefinitions) defs[d.Id] = d.Name;
        s.BlockDefinitions = defs.Count;
        foreach (var o in f.Objects)
        {
            if (o.Attributes.IsInstanceDefinitionObject) continue;      // block member geometry
            s.Objects++;
            string type = o.Geometry switch
            {
                RG.Extrusion => "Extrusion",
                RG.Brep => "Brep",
                RG.Mesh => "Mesh",
                RG.InstanceReferenceGeometry => "Block",
                RG.Curve => "Curve",
                var g => g?.GetType().Name ?? "null",
            };
            if (o.Geometry is RG.Brep b) { if (b.IsSolid) s.ClosedBreps++; else s.OpenBreps++; }
            if (o.Geometry is RG.InstanceReferenceGeometry ir)
            {
                string dn = defs.TryGetValue(ir.ParentIdefId, out var n) ? n : ir.ParentIdefId.ToString();
                s.BlockUse[dn] = s.BlockUse.GetValueOrDefault(dn) + 1;
            }
            string lp = layerPath.TryGetValue(o.Attributes.LayerIndex, out var p) ? p : "?";
            if (!s.ByLayer.TryGetValue(lp, out var t)) s.ByLayer[lp] = t = new SortedDictionary<string, int>(StringComparer.Ordinal);
            t[type] = t.GetValueOrDefault(type) + 1;
            s.ByType[type] = s.ByType.GetValueOrDefault(type) + 1;
            if (!string.IsNullOrEmpty(o.Attributes.GetUserString("GlobalId"))) s.ObjectsWithGlobalId++;
        }
        for (int i = 0; i < f.Strings.Count; i++) s.DocumentStrings[f.Strings.GetKey(i)] = f.Strings.GetValue(i);
        return s;
    }
}
