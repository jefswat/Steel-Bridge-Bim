using System.Numerics;
using System.Windows;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using SBM.App.Viewport;
using SBM.Core.Geometry;
using MeshGeometry3D = HelixToolkit.SharpDX.MeshGeometry3D;
using OrthographicCamera = HelixToolkit.Wpf.SharpDX.OrthographicCamera;

namespace SBM.App;

/// <summary>
/// Near-view level of detail for reinforcing bars (see <see cref="SmoothRebar"/>). When the view is zoomed in and the camera has
/// settled, the bars close to the view-center ray are built as smooth lofted tubes on a background thread and shown in an
/// overlay model per rebar layer; the same bars are taken out of the light hexagonal tile meshes (and the instanced shaped bars)
/// through <see cref="_lodSkip"/>, which flows through the section-cut / hide path (<see cref="ApplyToModel"/>). The overlay
/// meshes carry normal pick tables, so picking, hiding, highlighting and the section cut treat them like any other mesh.
/// </summary>
public partial class MainWindow
{
    private sealed record LodBar(RebarPick Tag, MeshGeometryModel3D Model, string Layer, Vector3 Min, Vector3 Max);

    private sealed class LodRegistry
    {
        public List<LodBar> Bars = new();
        public Dictionary<object, MeshGeometryModel3D> ModelOf = new(ReferenceEqualityComparer.Instance);
    }

    /// <summary>Bars currently drawn smooth: skipped in the coarse models (reference equality).</summary>
    private HashSet<object> _lodSkip = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<MeshGeometryModel3D> _lodOverlays = new();
    private Task<LodRegistry>? _lodRegistry;
    private int _lodVersion;
    private bool _lodBusy;

    private static bool IsRebarLayer(string name) => name.StartsWith("3D-REBAR", StringComparison.Ordinal);

    /// <summary>The scene was rebuilt: forget the overlays and the bar registry (their models are gone).</summary>
    private void ResetRebarLod()
    {
        _lodVersion++;
        _lodSkip = new(ReferenceEqualityComparer.Instance);
        _lodOverlays.Clear();
        _lodRegistry = null;
        _lodBusy = false;
    }

    /// <summary>Returns to the light meshes (zoomed out, plan view, feature off).</summary>
    private void ClearRebarLod()
    {
        _lodVersion++;
        _lodBusy = false;
        if (_lodSkip.Count == 0 && _lodOverlays.Count == 0) return;
        var changed = ModelsOf(_lodSkip).ToList();
        _lodSkip = new(ReferenceEqualityComparer.Instance);
        RemoveOverlays();
        foreach (var m in changed) ApplyToModel(m);
    }

    private IEnumerable<MeshGeometryModel3D> ModelsOf(IEnumerable<object> tags)
    {
        if (_lodRegistry is not { IsCompletedSuccessfully: true } t) yield break;
        var seen = new HashSet<MeshGeometryModel3D>();
        foreach (var tag in tags)
            if (t.Result.ModelOf.TryGetValue(tag, out var m) && seen.Add(m)) yield return m;
    }

    private void RemoveOverlays()
    {
        foreach (var ov in _lodOverlays)
        {
            ov.Visibility = Visibility.Collapsed;
            View.Items.Remove(ov);
            _fullMeshes.Remove(ov);
            _detailBounds.Remove(ov);
            foreach (var list in _layerModels.Values) list.Remove(ov);
        }
        _lodOverlays.Clear();
    }

    /// <summary>Called when the camera has settled (and after a scene rebuild): chooses and builds the smooth bars near the view center.</summary>
    private void UpdateRebarLod()
    {
        if (!RebarLod.Enabled || _scene == null || _planMode || View.Camera is not OrthographicCamera cam
            || cam.Width > RebarLod.SwitchWidthFt * 0.3048 || !_layerModels.Keys.Any(IsRebarLayer))
        {
            ClearRebarLod();
            return;
        }
        var scene = _scene;
        var look = cam.LookDirection;
        if (look.Length < 1e-9) return;
        look.Normalize();
        var o = new Vector3((float)cam.Position.X, (float)cam.Position.Y, (float)cam.Position.Z);
        var dir = new Vector3((float)look.X, (float)look.Y, (float)look.Z);
        float radius = (float)(RebarLod.RadiusFt * 0.3048);
        int ver = ++_lodVersion;
        _lodBusy = true;
        _lodRegistry ??= StartLodRegistry(scene);
        var regTask = _lodRegistry;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _ = Task.Run(() =>
        {
            try
            {
                var reg = regTask.Result;
                if (ver != _lodVersion) return;
                var inv = SnapMath.Reciprocal(dir);
                var hits = new List<(LodBar bar, float d2)>();
                foreach (var b in reg.Bars)
                {
                    if (!SnapMath.LineHitsBox(o, inv, b.Min, b.Max, radius)) continue;
                    var c = (b.Min + b.Max) * 0.5f;
                    hits.Add((b, SnapMath.DistanceToRaySq(c, o, dir, out _)));
                }
                hits.Sort((x, y) => x.d2.CompareTo(y.d2));
                var batches = new Dictionary<string, SmoothRebar.Batch>();
                int tris = 0;
                foreach (var (bar, _) in hits)
                {
                    if (tris >= RebarLod.MaxTriangles) break;
                    if (ver != _lodVersion) return;
                    if (!batches.TryGetValue(bar.Layer, out var batch)) batches[bar.Layer] = batch = new SmoothRebar.Batch(scene.Origin);
                    int before = batch.Triangles;
                    batch.Add(bar.Tag);
                    tris += batch.Triangles - before;
                }
                var meshes = batches.Select(kv => (layer: kv.Key, mesh: kv.Value.Finish(), used: kv.Value.Used)).Where(x => x.mesh != null).ToList();
                double ms = sw.Elapsed.TotalMilliseconds;
                Dispatcher.BeginInvoke(() => ApplyRebarLod(ver, meshes!, hits.Count, tris, ms), System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex)
            {
                ViewModels.MainViewModel.Log("Rebar LOD failed: " + ex.GetBaseException());
                Dispatcher.BeginInvoke(() => { if (ver == _lodVersion) _lodBusy = false; });
            }
        });
    }

    private Task<LodRegistry> StartLodRegistry(SceneData scene)
    {
        // Captured on the UI thread: every rebar model with its bars.
        var src = new List<(string layer, MeshGeometryModel3D model, IReadOnlyList<object> tags)>();
        foreach (var (name, list) in _layerModels)
        {
            if (!IsRebarLayer(name)) continue;
            foreach (var m in list.OfType<MeshGeometryModel3D>())
            {
                if (_lodOverlays.Contains(m)) continue;
                if (m.Tag is List<Matrix4x4> && m.Geometry is MeshGeometry3D ig && SceneBuilder.InstanceTags.TryGetValue(ig, out var itags)) src.Add((name, m, itags));
                else if (_fullMeshes.TryGetValue(m, out var full) && SceneBuilder.Picks.TryGetValue(full, out var table)) src.Add((name, m, table.Tags));
            }
        }
        var o = scene.Origin;
        long coarseTris = scene.Solids.Where(l => IsRebarLayer(l.Name)).Sum(l => (long)l.Triangles);
        return Task.Run(() =>
        {
            var reg = new LodRegistry();
            foreach (var (layer, model, tags) in src)
                foreach (var t in tags)
                {
                    if (t is not RebarPick rp || rp.Bar.Points.Length < 2) continue;
                    var lo = new Vector3(float.MaxValue);
                    var hi = new Vector3(float.MinValue);
                    foreach (var p in rp.Bar.Points)
                    {
                        var v = new Vector3((float)(p.X - o.X), (float)(p.Y - o.Y), (float)(p.Z - o.Z));
                        lo = Vector3.Min(lo, v);
                        hi = Vector3.Max(hi, v);
                    }
                    reg.Bars.Add(new LodBar(rp, model, layer, lo, hi));
                    reg.ModelOf[rp] = model;
                }
            foreach (var g in reg.Bars.GroupBy(b => b.Layer))
            {
                var f = g.First().Tag.Bar.Points[0];
                ViewModels.MainViewModel.Log($"REBAR LOD layer {g.Key}: {g.Count():N0} bars, e.g. world {f.X:F2},{f.Y:F2},{f.Z:F2}");
            }
            ViewModels.MainViewModel.Log($"REBAR LOD registry: {reg.Bars.Count:N0} bars in {src.Count} models; light rebar meshes hold {coarseTris:N0} triangles");
            return reg;
        });
    }

    private void ApplyRebarLod(int ver, List<(string layer, MeshGeometry3D mesh, List<RebarPick> used)> meshes, int candidates, int tris, double ms)
    {
        if (ver != _lodVersion) return;
        var swApply = System.Diagnostics.Stopwatch.StartNew();
        _lodBusy = false;
        var newSkip = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var m in meshes) foreach (var t in m.used) newSkip.Add(t);
        if (_lodOverlays.Count > 0 && newSkip.SetEquals(_lodSkip)) return; // the same bars are already smooth
        var changedTags = _lodSkip.Where(t => !newSkip.Contains(t)).Concat(newSkip.Where(t => !_lodSkip.Contains(t)));
        var changed = ModelsOf(changedTags).ToList();
        RemoveOverlays();
        _lodSkip = newSkip;
        foreach (var (layer, mesh, _) in meshes)
        {
            var template = _layerModels.TryGetValue(layer, out var list) ? list.OfType<MeshGeometryModel3D>().FirstOrDefault(m => !_lodOverlays.Contains(m)) : null;
            if (template == null) continue;
            var ov = new MeshGeometryModel3D { Geometry = mesh, Material = template.Material, IsHitTestVisible = true };
            _fullMeshes[ov] = mesh;
            _lodOverlays.Add(ov);
            list!.Add(ov);
            View.Items.Add(ov);
            var b = mesh.Bound;
            _detailBounds[ov] = (layer, b.Minimum, b.Maximum);
            changed.Add(ov);
        }
        foreach (var m in changed) ApplyToModel(m);
        ApplyDetailVisibility();
        ViewModels.MainViewModel.Log($"REBAR LOD: {newSkip.Count:N0} bars smooth ({tris:N0} triangles, {candidates:N0} candidates), {changed.Count} models updated, built in {ms:F0} ms, applied in {swApply.Elapsed.TotalMilliseconds:F0} ms");
    }
}
