using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using HelixToolkit;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using SBM.App.Viewport;
using SBM.Core.Drawing.Projection;
using SBM.Core.Geometry;
using SBM.Core.Steel;
using MeshGeometry3D = HelixToolkit.SharpDX.MeshGeometry3D;

namespace SBM.App;

/// <summary>Sub-elements of built-up members (<see cref="SteelPart.Member"/>, truss PartPerPiece): pick level (a click selects the MEMBER / joint gusset stack by
/// default, Alt+click or the "Pick: part" toggle the single piece), "Sub-elements" view option (boundary edges between the pieces of the selected / isolated
/// members + alternating tints in the selection highlight). Pure logic in <see cref="SubElements"/>. NOT visually verified (no screen in the build sandbox).</summary>
public partial class MainWindow
{
    private PickLevel _pickLevel = PickLevel.Member;
    private bool _showSubElements;
    private LineGeometryModel3D? _subEdges;
    /// <summary>Member / stack of the current member selection (null = none).</summary>
    private string? _selectedMember;

    private void OnPickLevelToggle(object sender, RoutedEventArgs e)
    {
        _pickLevel = PickLevelToggle.IsChecked == true ? PickLevel.Part : PickLevel.Member;
        PickLevelToggle.Content = _pickLevel == PickLevel.Member ? "Pick: member" : "Pick: part";
    }

    private void OnSubElementsToggle(object sender, RoutedEventArgs e)
    {
        _showSubElements = SubElementsToggle.IsChecked == true;
        if (_groupSel is { } g && _selectedMember != null) ShowMemberHighlight(_selectedMember, g.tags);
        RefreshSubElementEdges();
    }

    /// <summary>Called by <see cref="Pick"/> with the picked tag: at member level a piece selects its whole member / stack. True = handled.</summary>
    private bool PickMember(object tag, Vector3 hitLocal)
    {
        _selectedMember = null;
        var level = SubElements.Effective(_pickLevel, (Keyboard.Modifiers & ModifierKeys.Alt) != 0);
        if (SubElements.Resolve(RawModelObject(tag), level) is not { } mem) { RefreshSubElementEdges(); return false; }
        var tags = new List<object>();
        foreach (var kv in SceneBuilder.Picks)
            tags.AddRange(SubElements.TagsOf(kv.Value.Tags.Where(t => !TagHidden(t)), mem, RawModelObject));
        if (tags.Count == 0) return false;
        _vm.ShowPicked(tag);
        _selectedTitle = $"{mem} ({tags.Count} sub-elements)";
        _selectedTag = tag;
        _selectedMember = mem;
        ShowMemberHighlight(mem, tags);
        _vm.StatusText = $"Selected member {mem}: {tags.Count} sub-elements (Alt+click = one piece; Pick toggle = always pieces)";
        DbOnPicked(tag, hitLocal, parentLevel: "member");
        ClickViewsOnPicked(hitLocal);
        RefreshSubElementEdges();
        return true;
    }

    /// <summary>Highlight of all pieces of a member (pick-table triangle ranges); with "Sub-elements" on, alternating tints per piece.</summary>
    private void ShowMemberHighlight(string mem, List<object> tags)
    {
        var set = new HashSet<object>(tags);
        var tints = _showSubElements ? SubElements.Tints(tags.Select(RawModelObject).OfType<SteelPart>()) : null;
        var pos = new Vector3Collection(); var nrm = new Vector3Collection(); var idx = new IntCollection(); var col = new Color4Collection();
        var shades = new[] { new HelixToolkit.Maths.Color4(1f, 0.85f, 0.1f, 1f), new HelixToolkit.Maths.Color4(0.2f, 0.75f, 1f, 1f), new HelixToolkit.Maths.Color4(0.95f, 0.35f, 0.75f, 1f) };
        foreach (var kv in SceneBuilder.Picks)
        {
            var full = kv.Key; var table = kv.Value;
            for (int i = 0; i < table.Tags.Count; i++)
            {
                if (!set.Contains(table.Tags[i])) continue;
                var c = tints != null && RawModelObject(table.Tags[i]) is SteelPart sp && tints.TryGetValue(sp, out int ti) ? shades[ti % shades.Length] : shades[0];
                int t0 = table.TriStart[i], t1 = i + 1 < table.TriStart.Count ? table.TriStart[i + 1] : full.Indices!.Count / 3;
                for (int t = t0; t < t1; t++)
                    for (int k = 0; k < 3; k++)
                    {
                        int v = full.Indices![3 * t + k];
                        idx.Add(pos.Count); pos.Add(full.Positions![v]); nrm.Add(full.Normals![v]); col.Add(c);
                    }
            }
        }
        ClearHighlight();
        var geo = new MeshGeometry3D { Positions = pos, Normals = nrm, Indices = idx };
        if (tints != null) geo.Colors = col;
        geo.UpdateBounds();
        if (pos.Count > 0) ShowHighlight(geo);
        if (tints != null && _highlight?.Material is PhongMaterial pm) pm.VertexColorBlendingFactor = 1;
        _groupSel = (mem, tags, geo);
    }

    /// <summary>"Sub-elements" view option: feature edges of every piece of the selected member, or of the isolated elements that are pieces (the joints
    /// between plies / angles / covers / gusset layers become visible). Drawn over the shaded model.</summary>
    private void RefreshSubElementEdges()
    {
        if (_subEdges != null) { View.Items.Remove(_subEdges); _subEdges = null; }
        if (!_showSubElements || _scene == null) return;
        IEnumerable<object> src = _groupSel is { } g && _selectedMember != null ? g.tags : _iso.FiltersElements ? IsolatedTags() : Array.Empty<object>();
        var parts = src.Select(RawModelObject).OfType<SteelPart>().Where(p => p.Member != null).Distinct().Take(20000).ToList();
        if (parts.Count == 0) return;
        var o = _scene.Origin;
        var pos = new Vector3Collection(); var idx = new IntCollection();
        foreach (var p in parts)
            foreach (var e in FeatureEdges.Of(p.Mesh).Edges)
            {
                if (e.Kind == EdgeKind.Smooth) continue;
                idx.Add(pos.Count); pos.Add(new Vector3((float)(e.A.X - o.X), (float)(e.A.Y - o.Y), (float)(e.A.Z - o.Z)));
                idx.Add(pos.Count); pos.Add(new Vector3((float)(e.B.X - o.X), (float)(e.B.Y - o.Y), (float)(e.B.Z - o.Z)));
            }
        if (pos.Count == 0) return;
        var lines = new LineGeometry3D { Positions = pos, Indices = idx };
        lines.UpdateBounds();
        _subEdges = new LineGeometryModel3D { Geometry = lines, Thickness = 1.6, Smoothness = 0, IsHitTestVisible = false, Color = Colors.Black, DepthBias = -400 };
        View.Items.Add(_subEdges);
    }

    /// <summary>Pick-table tags inside the current isolate (for the sub-element edges).</summary>
    private IEnumerable<object> IsolatedTags()
    {
        foreach (var kv in SceneBuilder.Picks)
            foreach (var t in kv.Value.Tags)
                if (!TagHidden(t)) yield return t;
    }
}
