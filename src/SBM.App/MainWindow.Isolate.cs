using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using SBM.App.Viewport;
using SBM.Core.Geometry;
using MeshGeometry3D = HelixToolkit.SharpDX.MeshGeometry3D;
using OrthographicCamera = HelixToolkit.Wpf.SharpDX.OrthographicCamera;

namespace SBM.App;

/// <summary>
/// Isolate (right-click ▸ Isolate element / group / layer, I, Shift+I, Esc / I to exit). The state and the save / restore logic are in
/// <see cref="ViewIsolation"/> (SBM.Core, tested). Isolating switches every other layer off through the normal layer items (so the
/// layer panel, detail tiles, VRAM residency and the snap index follow the usual paths) and, for elements, filters the merged
/// meshes / instances of the kept layers with the same predicate as hidden elements (<see cref="TagHidden"/>: section cut,
/// picking, snaps and the orbit pivot all honor it). Exit restores the layers and hidden elements from before the first isolate.
/// </summary>
public partial class MainWindow
{
    private readonly ViewIsolation _iso = new();
    /// <summary>Zoom extents on the isolated set right after isolating (context menu option).</summary>
    private bool _isoZoomOnIsolate;
    /// <summary>Layer item changes made in one batch: the viewport is updated once at the end.</summary>
    private bool _layerBatch;
    /// <summary>The database group highlighted from the database panel (a multi-element selection), with its pick objects.</summary>
    private (string name, List<object> tags, MeshGeometry3D geo)? _groupSel;

    /// <summary>Element hidden by the user or outside the isolated set: not drawn, not pickable, not snapped to.</summary>
    private bool TagHidden(object t) => (_hidden.Count > 0 && _hidden.Contains(t)) || !_iso.Shows(t);

    /// <summary>Whether merged meshes / instances must be filtered per element.</summary>
    private bool ElementFilterOn => _hidden.Count > 0 || _iso.FiltersElements;

    /// <summary>Plan view draws only 2D linework, except the 3D layers of an isolate (a top view of what is isolated); 3D omits plan-only layers.</summary>
    private bool LayerModeOk(string name) =>
        _planMode ? !name.StartsWith("3D-") || _iso.ShowsInPlan(name)
                  : !PlanOnlyLayers.Contains(name) && !name.StartsWith(ViewModels.MainViewModel.PlanPrefix, StringComparison.Ordinal);   // projected plan = plan view only

    private void SetLayersBatch(Func<string, bool> on)
    {
        _layerBatch = true;
        try { foreach (var li in _vm.LayerItems) li.IsVisible = on(li.Name); }
        finally { _layerBatch = false; }
        ApplyLayerVisibility();
    }

    /// <summary>Layers whose solids contain any of the tags (scene solids: pick tables of merged meshes, instance tags).</summary>
    private List<string> LayersContaining(IReadOnlyCollection<object> tags)
    {
        var set = tags as HashSet<object> ?? tags.ToHashSet();
        var res = new List<string>();
        if (_scene == null) return res;
        foreach (var solid in _scene.Solids)
        {
            bool any = false;
            foreach (var (mesh, _) in solid.Parts)
            {
                if (SceneBuilder.Picks.TryGetValue(mesh, out var table) && table.Tags.Any(set.Contains)) { any = true; break; }
                if (SceneBuilder.InstanceTags.TryGetValue(mesh, out var it) && it.Any(set.Contains)) { any = true; break; }
            }
            if (any) res.Add(solid.Name);
        }
        return res;
    }

    private void IsolateElements(string name, IEnumerable<object> tags)
    {
        var set = tags.ToHashSet();
        var layers = LayersContaining(set);
        if (set.Count == 0 || layers.Count == 0) { StatusText("Nothing to isolate here."); return; }
        _iso.IsolateElements(name, set, layers, _vm.LayerItems.Select(l => KeyValuePair.Create(l.Name, l.IsVisible)), _hiddenList);
        AfterIsolate();
    }

    private void IsolateLayer(string layer)
    {
        if (_vm.LayerItems.All(l => l.Name != layer)) return;
        _iso.IsolateLayer(layer, _vm.LayerItems.Select(l => KeyValuePair.Create(l.Name, l.IsVisible)), _hiddenList);
        AfterIsolate();
    }

    private void AfterIsolate()
    {
        ClearSelection();
        SetLayersBatch(_iso.LayerOn);
        ApplySection();
        UpdateIsolateBanner();
        if (_isoZoomOnIsolate) ZoomToIsolated(true);
        StatusText($"Isolated: {_iso.Name} — Esc (nothing selected) or I to exit; Zoom in the banner");
        ViewModels.MainViewModel.Log($"ISOLATE: {_iso.Kind} '{_iso.Name}', {_iso.Tags.Count} elements, layers {string.Join(", ", _iso.KeptLayers)}");        RefreshSubElementEdges();
    }

    private void ExitIsolate()
    {
        if (_iso.Exit() is not { } s) return;
        _hidden.Clear();
        _hiddenList.Clear();
        foreach (var (tag, label) in s.Hidden)
            if (_hidden.Add(tag)) _hiddenList.Add((tag, label));
        SetLayersBatch(s.LayerVisible);
        ApplySection();
        UpdateIsolateBanner();
        StatusText("Isolate ended: previous visibility restored");
        ViewModels.MainViewModel.Log($"ISOLATE exit: {_vm.LayerItems.Count(l => l.IsVisible)} of {_vm.LayerItems.Count} layers on, {_hiddenList.Count} hidden");        RefreshSubElementEdges();
    }

    /// <summary>After a regeneration (called with the fresh objects keyed by <see cref="HideKey"/>): the isolate follows the new objects.</summary>
    private void RemapIsolation(Dictionary<string, object> fresh)
    {
        if (!_iso.Active) return;
        int left = _iso.Remap(t => fresh.TryGetValue(HideKey(t), out var n) ? n : null, LayersContaining);
        if (_iso.FiltersElements && left == 0)
        {
            ViewModels.MainViewModel.Log("ISOLATE: the isolated elements no longer exist - isolate ended");
            Dispatcher.BeginInvoke(ExitIsolate);
            return;
        }
        // Layers that appeared with the regeneration are off while isolated (the layer items keep the isolated visibility by name).
        _layerBatch = true;
        try { foreach (var li in _vm.LayerItems) li.IsVisible = _iso.LayerOn(li.Name); }
        finally { _layerBatch = false; }
    }

    /// <summary>Reference layers that appear while isolated (references reloaded) stay off.</summary>
    private void IsolateNewLayers()
    {
        if (!_iso.Active || _iso.Saved is not { } saved) return;
        _layerBatch = true;
        try { foreach (var li in _vm.LayerItems) if (!saved.Layers.ContainsKey(li.Name)) li.IsVisible = _iso.LayerOn(li.Name); }
        finally { _layerBatch = false; }
    }

    private void ClearSelection()
    {
        ClearHighlight();
        _selectedMember = null;
        _selectedTag = null;
        _selectedModel = null;
        _groupSel = null;
        _vm.SelectedProperties.Clear();
    }

    private void UpdateIsolateBanner()
    {
        bool on = _iso.Active && !SheetsMode;
        IsolateBanner.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (!_iso.Active) return;
        string what = _iso.Kind == IsolationKind.Layer ? $"layer {_iso.Name}" : _iso.Tags.Count > 1 ? $"{_iso.Name} ({_iso.Tags.Count} elements)" : _iso.Name;
        IsolateText.Text = $"Isolated: {what}";
        IsolateBanner.ToolTip = _iso.Kind == IsolationKind.Elements ? "Layers: " + string.Join(", ", _iso.KeptLayers) : null;
    }

    private void OnIsolateExit(object sender, RoutedEventArgs e) => ExitIsolate();

    private void OnIsolateZoom(object sender, RoutedEventArgs e) => ZoomToIsolated(true);

    /// <summary>Keyboard: I = isolate the selection (element or database group), again / nothing selected = exit; Shift+I = isolate the
    /// selected element's layer. Returns true when the key was used.</summary>
    private bool IsolateKey(KeyEventArgs e)
    {
        if (e.Key != Key.I || Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase || SheetsMode || MeasureActive) return false;
        var mods = Keyboard.Modifiers;
        if (mods == ModifierKeys.None)
        {
            if (_groupSel is { } g) IsolateElements(g.name, g.tags);
            else if (_selectedTag is { } t) IsolateElements(_selectedTitle, new[] { t });
            else if (_iso.Active) ExitIsolate();
            else { StatusText("Select an element (click it) to isolate it, or right-click ▸ Isolate layer"); return true; }
            return true;
        }
        if (mods == ModifierKeys.Shift && _selectedModel is { } m && LayerOf(m) is { } layer) { IsolateLayer(layer); return true; }
        return false;
    }

    /// <summary>Snapshot options: <c>--isolate fx,fy</c> (pick at the view fraction, isolate it), <c>--isolatelayer NAME</c>,
    /// <c>--isolatezoom</c> (zoom to the isolated set), <c>--isolatepick fx,fy</c> (pick while isolated), <c>--isolateexit</c>.
    /// Logs ISOLATE: lines (layers on, hidden, drawn triangles / instances).</summary>
    private void SnapshotIsolate(string[] args)
    {
        double[] Frac(string opt) => Array.IndexOf(args, opt) is int i && i >= 0 && i + 1 < args.Length
            ? args[i + 1].Split(',').Select(x => double.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray() : Array.Empty<double>();
        void Report(string what)
        {
            long tris = 0, inst = 0;
            foreach (var m in AllModels().OfType<MeshGeometryModel3D>().Where(m => m.Visibility == Visibility.Visible))
            {
                if (m.Instances is { Count: > 0 } ins) inst += ins.Count;
                else tris += (m.Geometry?.Indices?.Count ?? 0) / 3;
            }
            ViewModels.MainViewModel.Log($"ISOLATE {what}: active {_iso.Active} ({_iso.Kind} '{_iso.Name}'), {_vm.LayerItems.Count(l => l.IsVisible)} of {_vm.LayerItems.Count} layers on, {_hiddenList.Count} hidden, drawn {tris:N0} triangles + {inst:N0} instances, banner {IsolateBanner.Visibility}");
        }
        var p = Frac("--isolate");
        int li = Array.IndexOf(args, "--isolatelayer");
        if (p.Length < 2 && li < 0) return;
        Report("before");
        if (p.Length >= 2)
        {
            Pick(new Point(p[0] * View.ActualWidth, p[1] * View.ActualHeight));
            if (_selectedTag != null) IsolateElements(_selectedTitle, new[] { _selectedTag });
            else ViewModels.MainViewModel.Log("ISOLATE: nothing picked");
        }
        else if (li + 1 < args.Length) IsolateLayer(args[li + 1]);
        if (args.Contains("--isolatezoom")) ZoomToIsolated(false);
        Report("after");
        var q = Frac("--isolatepick");
        if (q.Length >= 2)
        {
            Pick(new Point(q[0] * View.ActualWidth, q[1] * View.ActualHeight));
            ViewModels.MainViewModel.Log("ISOLATE pick: " + (_selectedTag != null ? _selectedTitle : "nothing") + "; pivot there: " + ChoosePivot(new Point(q[0] * View.ActualWidth, q[1] * View.ActualHeight)).what);
            ClearSelection();
        }
        // Let the banner / layer panel lay out before the window is rendered.
        UpdateLayout();
        Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        if (args.Contains("--isolateexit") && Array.IndexOf(args, "--snapshot") is int si && si + 1 < args.Length)
        {
            // The isolated state as images too (the final snapshot shows the restored view).
            string stem = System.IO.Path.ChangeExtension(args[si + 1], null);
            SaveElement((FrameworkElement)Content, stem + "_isolated.png");
            if (View.RenderBitmap() is { } vb) SavePng(vb, stem + "_isolated_view.png");
            ExitIsolate();
            Report("exit");
            UpdateLayout();
        }
    }

    // ------------------------------------------------------------------ zoom to the isolated set

    /// <summary>Zoom extents on what is isolated (scene-local box of the isolated elements / the isolated layer). False when there is nothing.</summary>
    private bool ZoomToIsolated(bool animate)
    {
        if (!_iso.Active || IsolatedBounds() is not { } b) return false;
        _pivot = null;
        var size = Vector3.Max((b.max - b.min) * 1.08f, new Vector3(1f)); // a small margin around the isolated set
        var c = (b.min + b.max) / 2;
        if (_planMode)
        {
            double aspect = View.ActualHeight > 0 ? View.ActualWidth / View.ActualHeight : 1.6;
            if (View.Camera is OrthographicCamera cam)
            {
                cam.Position = new Point3D(c.X, c.Y, 1500);
                cam.LookDirection = new Vector3D(0, 0, -1500);
                cam.UpDirection = new Vector3D(0, 1, 0);
                cam.Width = Math.Max(Math.Max(size.X, size.Y * aspect) * 1.15, 1);
            }
        }
        else View.ZoomExtents(new Rect3D(c.X - size.X / 2, c.Y - size.Y / 2, c.Z - size.Z / 2, size.X, size.Y, size.Z), animate ? 300 : 0);
        KeepCameraInRange();
        return true;
    }

    private (Vector3 min, Vector3 max)? IsolatedBounds()
    {
        var lo = new Vector3(float.MaxValue);
        var hi = new Vector3(float.MinValue);
        void Add(Vector3 p) { lo = Vector3.Min(lo, p); hi = Vector3.Max(hi, p); }
        foreach (var layer in _iso.KeptLayers)
        {
            if (!_layerModels.TryGetValue(layer, out var models) && !_refModels.TryGetValue(layer, out models)) continue;
            foreach (var m in models)
            {
                if (_lodOverlays.Contains(m)) continue;
                switch (m)
                {
                    case MeshGeometryModel3D im when im.Tag is List<Matrix4x4> all && im.Geometry is MeshGeometry3D ig:
                    {
                        var tags = SceneBuilder.InstanceTags.TryGetValue(ig, out var t) ? t : null;
                        var gb = ig.Bound;
                        for (int i = 0; i < all.Count; i++)
                        {
                            if (_iso.FiltersElements && (tags == null || i >= tags.Count || !_iso.Shows(tags[i]))) continue;
                            Add(Vector3.Transform(gb.Minimum, all[i]));
                            Add(Vector3.Transform(gb.Maximum, all[i]));
                        }
                        break;
                    }
                    case MeshGeometryModel3D mm when _fullMeshes.TryGetValue(mm, out var full):
                    {
                        if (!_iso.FiltersElements) { Add(full.Bound.Minimum); Add(full.Bound.Maximum); break; }
                        if (!SceneBuilder.Picks.TryGetValue(full, out var table)) break;
                        int n = full.Indices!.Count / 3;
                        for (int i = 0; i < table.Tags.Count; i++)
                        {
                            if (!_iso.Shows(table.Tags[i])) continue;
                            int t1 = i + 1 < table.TriStart.Count ? table.TriStart[i + 1] : n;
                            for (int k = 3 * table.TriStart[i]; k < 3 * t1; k++) Add(full.Positions![full.Indices[k]]);
                        }
                        break;
                    }
                    case LineGeometryModel3D lm when !_iso.FiltersElements && _fullLines.TryGetValue(lm, out var lines) && lines.Positions is { Count: > 0 }:
                        lines.UpdateBounds();
                        Add(lines.Bound.Minimum); Add(lines.Bound.Maximum);
                        break;
                }
            }
        }
        return lo.X <= hi.X ? (lo, hi) : null;
    }
}
