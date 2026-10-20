using System.Windows;
using HelixToolkit.Wpf.SharpDX;
using SBM.Core.Drawing.Projection;
using SBM.Core.Geometry;

namespace SBM.App;

/// <summary>
/// "Project view to 2D" (ribbon View > 2D projection, right-click menu): the current camera direction (or the section cut when it is on:
/// view along the cut normal, near plane on the cut), the visible layers and unhidden elements, projected from the solids with hidden-line
/// removal into a <see cref="Editors.ProjectionWindow"/>; and the "Projected plan (model)" toggle for the plan view.
/// </summary>
public partial class MainWindow
{
    private void OnProjectView2D(object sender, RoutedEventArgs e) => _ = ProjectView2DAsync();

    private async void OnProjectedPlanToggle(object sender, RoutedEventArgs e)
    {
        _vm.ProjectedPlanAll = !_vm.ProjectedPlanAll;
        ProjectedPlanLabel.Text = _vm.ProjectedPlanAll ? "Projected Plan: on" : "Projected Plan: off";
        StatusText(_vm.ProjectedPlanAll ? "Projected plan (model): projecting every bridge for the plan view…" : "Projected plan (model): off (tied arch / segmental bridges keep theirs)");
        await _vm.RefreshProjectedPlanAsync();
        if (_vm.ProjectedPlanAll) StatusText("Projected plan (model): " + ViewModels.MainViewModel.LastProjectionInfo);
    }

    /// <summary>The view of the current camera / section cut in world coordinates, and a title.</summary>
    private (IViewMap view, string title)? CurrentProjectionView()
    {
        if (_scene == null || View.Camera is not ProjectionCamera cam) return null;
        var o = _scene.Origin;
        var look = new Vec3(cam.LookDirection.X, cam.LookDirection.Y, cam.LookDirection.Z);
        var up = new Vec3(cam.UpDirection.X, cam.UpDirection.Y, cam.UpDirection.Z);
        var target = new Vec3(cam.Position.X + cam.LookDirection.X, cam.Position.Y + cam.LookDirection.Y, cam.Position.Z + cam.LookDirection.Z) + o;
        if (_sectionOn && _plane is { } pl)
        {
            var n = new Vec3(pl.n.X, pl.n.Y, pl.n.Z).Normalized();
            var p = new Vec3(pl.p.X, pl.p.Y, pl.p.Z) + o;
            return (new OrthographicView(p, n, new Vec3(0, 0, 1)) { Near = 0 }, "Section view");
        }
        if (_planMode) return (OrthographicView.Top(0), "Plan view");
        return (new OrthographicView(target, look, up), "3D view");
    }

    /// <summary>Items of the visible layers, without hidden / non-isolated elements.</summary>
    private List<ProjectionItem> CurrentProjectionItems()
    {
        var items = new List<ProjectionItem>();
        foreach (var b in _vm.Project.Bridges)
        {
            if (!_vm.Framings.ContainsKey(b)) continue;
            items.AddRange(ModelProjectionSource.Items(b.Name, _vm.SteelModels.GetValueOrDefault(b), _vm.ConcreteModels.GetValueOrDefault(b),
                _vm.SubstructureModels.GetValueOrDefault(b), _vm.PrestressedModels.GetValueOrDefault(b),
                new ProjectionSourceOptions { Where = it => _vm.IsLayerVisible(it.Layer) && (it.Source == null || !TagHidden(it.Source)) }));
        }
        return items;
    }

    private async Task ProjectView2DAsync()
    {
        if (CurrentProjectionView() is not { } v) { StatusText("Nothing to project."); return; }
        var items = CurrentProjectionItems();
        if (items.Count == 0) { StatusText("No visible model elements to project."); return; }
        StatusText($"Projecting {items.Count:N0} elements to 2D…");
        long tris = items.Sum(i => (long)(i.Mesh?.TriangleCount ?? 0));
        var o = new ProjectionOptions { Method = tris > 6_000_000 ? HlrMethod.DepthBuffer : HlrMethod.Exact };
        var r = await Task.Run(() => ModelProjector.Project(items, v.view, o));
        ViewModels.MainViewModel.Log($"PROJECT VIEW: {v.view.Describe}: {r.Stats}");
        StatusText($"Projected view ({o.Method}): {r.Stats}");
        new Editors.ProjectionWindow(r, _vm.Units, $"{_vm.Project.Name} {v.title}") { Owner = this }.Show();
    }

    /// <summary>Snapshot options <c>--projectedplan</c> (turn the toggle on first) and <c>--projectview</c> (writes &lt;stem&gt;_projected.png of
    /// "Project view to 2D" for the final camera).</summary>
    private void SnapshotProjection(string path)
    {
        if (!Environment.GetCommandLineArgs().Contains("--projectview") || CurrentProjectionView() is not { } v) return;
        var items = CurrentProjectionItems();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var r = ModelProjector.Project(items, v.view, new ProjectionOptions());
        ViewModels.MainViewModel.Log($"PROJECT VIEW: {v.view.Describe}: {r.Stats} ({sw.ElapsedMilliseconds} ms)");
        string stem = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!, System.IO.Path.GetFileNameWithoutExtension(path));
        Viewport.DrawingCanvas.RenderToPng(r.Drawing, stem + "_projected.png", 1800, 1200);
    }
}
