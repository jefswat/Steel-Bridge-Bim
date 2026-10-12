using System.IO;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using SBM.App.Viewport;
using SBM.Core.Geometry;
using MeshGeometry3D = HelixToolkit.SharpDX.MeshGeometry3D;
using ProjectionCamera = HelixToolkit.Wpf.SharpDX.ProjectionCamera;

namespace SBM.App;

/// <summary>
/// Viewport memory manager. After every scene / reference load the GPU memory of every model is estimated (<see cref="VramInventory"/>,
/// HelixToolkit buffer layouts) and compared with the budget (setting, or a share of the adapter's DXGI budget). When it does not fit,
/// the drawing degrades along <see cref="VramPlanner.Ladder"/>: (a) terrain clipped / coarser / image smaller, (b) detail tiles far from the
/// view are RELEASED from the GPU (removed from the viewport, not only hidden) with a shrinking radius, (c) far bridges show primary members
/// only, then the last resorts. The measured DXGI usage after each camera settle and GPU out-of-memory render errors push one level further.
/// The status bar says what was reduced; the "Memory" menu overrides it.
/// </summary>
public partial class MainWindow
{
    private VramSettings _vramSettings = new();
    private List<VramLevel> _vramLadder = new();
    private VramLevel? _vramLevel;
    /// <summary>Lowest level allowed (raised by out-of-memory errors and the usage monitor; reset by "Restore full detail").</summary>
    private int _vramMinLevel;
    /// <summary>Level picked by the user in the Memory menu (-1 = automatic).</summary>
    private int _vramForcedLevel = -1;
    private long _vramBudget;
    private long _vramBaseline = -1;
    private VramInventory? _vramInventory;
    private bool _vramTerrainReload;
    private int _vramEscalations;
    /// <summary>Models removed from the viewport to free their GPU buffers (detail residency, far-bridge LOD).</summary>
    private readonly HashSet<Element3D> _evicted = new();
    /// <summary>Bridge index of each solid model (-1 = not per bridge).</summary>
    private readonly Dictionary<Element3D, int> _modelBridge = new();
    private readonly Dictionary<Element3D, string> _modelLayer = new();
    private readonly Dictionary<int, (Vector3 min, Vector3 max)> _bridgeBounds = new();
    private double _detailRadiusUser = 120;
    private static string VramSettingsPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SteelBridgeModeler", "viewport-memory.json");

    private void InitVram()
    {
        try
        {
            if (File.Exists(VramSettingsPath))
                _vramSettings = System.Text.Json.JsonSerializer.Deserialize<VramSettings>(File.ReadAllText(VramSettingsPath)) ?? new();
        }
        catch (Exception ex) { ViewModels.MainViewModel.Log("viewport memory settings not read: " + ex.Message); }
        var args = Environment.GetCommandLineArgs();
        // Snapshot / test options (not saved): --vram MB (budget), --vramlevel N (force a level), --novramreduce, --noshare (old vertex layout).
        int i = Array.IndexOf(args, "--vram");
        if (i >= 0 && i + 1 < args.Length) _vramSettings.BudgetMb = double.Parse(args[i + 1], System.Globalization.CultureInfo.InvariantCulture);
        i = Array.IndexOf(args, "--vramlevel");
        if (i >= 0 && i + 1 < args.Length) _vramForcedLevel = int.Parse(args[i + 1]);
        if (args.Contains("--novramreduce")) _vramSettings.AutoReduce = false;
        if (args.Contains("--noshare")) SceneBuilder.ShareVertices = false;
        _detailRadiusUser = _detailRadius = _vramSettings.DetailRadius;
        View.RenderExceptionOccurred += OnRenderException;
        _vm.PlanTerrainDisplay = PlanTerrainFor;
    }

    /// <summary>Contour line bytes per terrain layer seen last time (scaled to the full mesh), for planning before the next build.</summary>
    private readonly Dictionary<string, long> _contourBytes = new();

    /// <summary>
    /// Called by the view model before terrain buffers are built: chooses the level with the terrain rasters about to be drawn, so a
    /// terrain that does not fit is built reduced right away (never uploaded at full size first).
    /// </summary>
    private ViewModels.MainViewModel.TerrainDisplayOptions? PlanTerrainFor(IReadOnlyList<VramTerrain> terrains)
    {
        if (_scene == null) return null;
        var q = QueryVideoMemory();
        if (q != null && (_vramBaseline < 0 || q.Usage < _vramBaseline)) _vramBaseline = q.Usage;
        _vramBudget = VramBudget(q);
        var inv = SceneInventory.Build(_scene, _vm.ReferenceRenders.Where(r => r.Definition.Kind != SBM.Core.References.ReferenceKind.Terrain).ToList(), IsDetailLayer);
        foreach (var t in terrains)
            inv.Terrains.Add(t with { ContourBytes = _contourBytes.TryGetValue(t.Layer, out long cb) ? cb : (long)(t.MeshBytes(t.LoadedStride, Box2.Empty) * ContourGuess) });
        var ladder = VramPlanner.Ladder(inv, _vramSettings);
        var level = _vramForcedLevel >= 0 ? ladder[Math.Min(_vramForcedLevel, ladder.Count - 1)]
            : !_vramSettings.AutoReduce ? ladder[0] : VramPlanner.Choose(ladder, _vramBudget, _vramMinLevel);
        ViewModels.MainViewModel.Log($"VRAM terrain pre-plan: budget {Mb(_vramBudget)}, level {level.Index} (~{Mb(level.Estimated)}): {level.Description}");
        return new(Math.Max(1, level.TerrainStride), level.TerrainClip, level.ImageMaxPixels, level.TerrainStride == 0);
    }

    /// <summary>Contour lines relative to the terrain mesh bytes before the first contour build is known (measured on the I-80 DEM, 2 ft interval).</summary>
    private const double ContourGuess = 0.25;

    private void SaveVramSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(VramSettingsPath)!);
            File.WriteAllText(VramSettingsPath, System.Text.Json.JsonSerializer.Serialize(_vramSettings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) { ViewModels.MainViewModel.Log("viewport memory settings not saved: " + ex.Message); }
    }

    // ================================================================== DXGI

    private sealed record VideoMemory(string Adapter, long Dedicated, long Shared, long LocalBudget, long LocalUsage, long NonLocalBudget, long NonLocalUsage)
    {
        /// <summary>The segment the viewport lives in: local (dedicated) when the adapter reports one, else non-local (shared / integrated GPUs).</summary>
        public long Budget => LocalBudget > 0 ? LocalBudget : NonLocalBudget;
        public long Usage => LocalBudget > 0 ? LocalUsage : NonLocalUsage;
    }

    /// <summary>The adapter's video memory budget and this process' usage (DXGI 1.4 QueryVideoMemoryInfo), or null when unavailable.</summary>
    private VideoMemory? QueryVideoMemory()
    {
        try
        {
            if (View.EffectsManager?.Device is not { } device) return null;
            using var dxgi = device.QueryInterface<SharpDX.DXGI.Device>();
            using var adapter = dxgi.Adapter;
            var desc = adapter.Description;
            using var a3 = adapter.QueryInterfaceOrNull<SharpDX.DXGI.Adapter3>();
            if (a3 == null) return new VideoMemory(desc.Description, (long)desc.DedicatedVideoMemory, (long)desc.SharedSystemMemory, 0, 0, 0, 0);
            var local = a3.QueryVideoMemoryInfo(0, SharpDX.DXGI.MemorySegmentGroup.Local);
            var nonLocal = a3.QueryVideoMemoryInfo(0, SharpDX.DXGI.MemorySegmentGroup.NonLocal);
            return new VideoMemory(desc.Description.TrimEnd('\0'), (long)desc.DedicatedVideoMemory, (long)desc.SharedSystemMemory,
                local.Budget, local.CurrentUsage, nonLocal.Budget, nonLocal.CurrentUsage);
        }
        catch (Exception ex)
        {
            ViewModels.MainViewModel.Log("VRAM: DXGI query failed: " + ex.Message);
            return null;
        }
    }

    private static string Mb(long b) => VramPlanner.Mb(b);

    private void LogVideoMemory(string when)
    {
        if (QueryVideoMemory() is not { } q) return;
        ViewModels.MainViewModel.Log($"VRAM usage ({when}): {q.Adapter}: local {Mb(q.LocalUsage)} of budget {Mb(q.LocalBudget)}, non-local {Mb(q.NonLocalUsage)} of {Mb(q.NonLocalBudget)}"
            + $" (dedicated {Mb(q.Dedicated)}, shared {Mb(q.Shared)}); viewport estimate now {Mb(CurrentResidentEstimate())}");
    }

    // ================================================================== planning

    /// <summary>Budget for the viewport's geometry: the setting, else <see cref="VramSettings.AutoFraction"/> of what DXGI leaves this process
    /// beyond the usage measured before any scene was loaded.</summary>
    private long VramBudget(VideoMemory? q)
    {
        if (_vramSettings.BudgetMb > 0) return (long)(_vramSettings.BudgetMb * 1048576);
        if (q == null || q.Budget <= 0) return 3L << 30; // unknown adapter: assume 3 GB
        long baseline = Math.Max(0, _vramBaseline);
        return (long)Math.Max(256L << 20, (q.Budget - baseline) * _vramSettings.AutoFraction);
    }

    /// <summary>Estimates, chooses and applies a level (after a scene rebuild, a reference load or a settings change).</summary>
    private void PlanVram(string why)
    {
        if (_scene == null) return;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var q = QueryVideoMemory();
        if (q != null && (_vramBaseline < 0 || q.Usage < _vramBaseline)) _vramBaseline = q.Usage;
        _vramBudget = VramBudget(q);
        var inv = SceneInventory.Build(_scene, _vm.ReferenceRenders, IsDetailLayer);
        _vramInventory = inv;
        foreach (var t in inv.Terrains) _contourBytes[t.Layer] = t.ContourBytes;
        _vramLadder = VramPlanner.Ladder(inv, _vramSettings);
        VramLevel level;
        if (_vramForcedLevel >= 0) level = _vramLadder[Math.Min(_vramForcedLevel, _vramLadder.Count - 1)];
        else if (why == "terrain reload" && _vramLevel != null) level = _vramLadder[Math.Min(_vramLevel.Index, _vramLadder.Count - 1)]; // our own reload: keep the level
        else if (!_vramSettings.AutoReduce) level = _vramLadder[0];
        else level = VramPlanner.Choose(_vramLadder, _vramBudget, _vramMinLevel);
        if (why != "terrain reload")
        {
            var log = new System.Text.StringBuilder();
            log.Append($"VRAM plan ({why}): budget {Mb(_vramBudget)}" + (q != null ? $" (DXGI {q.Adapter}: budget {Mb(q.Budget)}, usage {Mb(q.Usage)}, baseline {Mb(Math.Max(0, _vramBaseline))})" : " (DXGI not available)"));
            log.Append($"; current buffers {Mb(inv.Total)} (full detail {Mb(inv.FullTotal)}) in {inv.Items.Count:N0} buffers, {inv.Items.Sum(x => x.Triangles):N0} triangles");
            foreach (var (g, bytes, tris, n) in inv.ByGroup()) log.Append($"\n    {g,-13} {Mb(bytes),9}  {tris,13:N0} tri  {n,6:N0} buffers");
            foreach (var b in inv.Items.Where(x => x.Bridge >= 0).GroupBy(x => x.Bridge).OrderBy(x => x.Key))
                log.Append($"\n    bridge {(b.Key < inv.BridgeNames.Count ? inv.BridgeNames[b.Key] : b.Key.ToString())}: {Mb(b.Sum(x => x.Bytes))} (detail {Mb(b.Where(x => x.Detail).Sum(x => x.Bytes))}, primary {Mb(b.Where(x => VramPlanner.PrimaryLayers.Contains(x.Layer)).Sum(x => x.Bytes))})");
            foreach (var t in inv.Terrains) log.Append($"\n    terrain {t.Layer}: grid {t.Nx} x {t.Ny} at {t.Cell:0.##} m, loaded every {t.LoadedStride}: mesh {Mb(t.MeshBytes(t.LoadedStride, Box2.Empty))}, image {t.ImageWidth} x {t.ImageHeight} = {Mb(t.ImageBytes(0))}");
            foreach (var l in _vramLadder) log.Append($"\n    level {l.Index}: {Mb(l.Estimated),9}  {l.Description}{(l == level ? "   <== chosen" : "")}");
            log.Append($"\n    largest layers: " + string.Join(", ", inv.ByLayer().Take(8).Select(x => $"{x.layer} {Mb(x.bytes)}")));
            log.Append($"\n    planned in {sw.ElapsedMilliseconds} ms");
            ViewModels.MainViewModel.Log(log.ToString());
        }
        ApplyVramLevel(level);
    }

    private void ApplyVramLevel(VramLevel level)
    {
        bool changed = _vramLevel != level;
        _vramLevel = level;
        // (a) terrain - reloads the reference renders (cached geometry) when it differs
        var td = new ViewModels.MainViewModel.TerrainDisplayOptions(Math.Max(1, level.TerrainStride), level.TerrainClip, level.ImageMaxPixels, level.TerrainStride == 0);
        if (td != _vm.TerrainDisplayMode)
        {
            _vramTerrainReload = true;
            _ = _vm.SetTerrainDisplay(td);
        }
        // (d) steel plates without bolt holes: the scene is rebuilt from the cached models (no regeneration)
        if (level.PlatesWithoutHoles != SceneBuilder.PlatesWithoutHoles)
        {
            SceneBuilder.PlatesWithoutHoles = level.PlatesWithoutHoles;
            ViewModels.MainViewModel.Log($"VRAM: rebuilding the 3D scene with steel plates {(level.PlatesWithoutHoles ? "without" : "with")} bolt holes");
            _ = _vm.RegenerateAsync(false);
        }
        // (b) detail residency and radius
        _detailRadius = level.DetailResidency ? (_detailRadiusUser > 0 ? Math.Min(_detailRadiusUser, level.DetailRadius) : level.DetailRadius) : _detailRadiusUser;
        ApplyDetailVisibility();
        UpdateVramStatus();
        if (changed) ViewModels.MainViewModel.Log($"VRAM level {level.Index} applied: {(level.IsFull ? "full detail" : VramPlanner.Summary(_vramLadder, level))}");
    }

    /// <summary>Raises the minimum level by one (GPU out-of-memory, usage over the budget).</summary>
    private void EscalateVram(string why)
    {
        if (!_vramSettings.AutoReduce || _vramForcedLevel >= 0 || _vramLevel == null || _vramLadder.Count == 0) return;
        if (_vramLevel.Index >= _vramLadder.Count - 1) { ViewModels.MainViewModel.Log($"VRAM: {why} - already at the last level"); return; }
        _vramEscalations++;
        _vramMinLevel = _vramLevel.Index + 1;
        ViewModels.MainViewModel.Log($"VRAM: {why} - reducing to level {_vramMinLevel}");
        ApplyVramLevel(VramPlanner.Choose(_vramLadder, _vramBudget, _vramMinLevel));
    }

    /// <summary>Measured usage after the camera settles: over the DXGI budget (or the set budget) means one more step.</summary>
    private void CheckVramUsage()
    {
        if (_vramLevel == null || QueryVideoMemory() is not { } q || q.Budget <= 0) return;
        long ours = q.Usage - Math.Max(0, _vramBaseline);
        if (q.Usage > q.Budget * 0.95) EscalateVram($"usage {Mb(q.Usage)} is over 95 % of the DXGI budget {Mb(q.Budget)}");
        else if (_vramSettings.BudgetMb > 0 && ours > _vramBudget * 1.10) EscalateVram($"viewport usage {Mb(ours)} is over the set budget {Mb(_vramBudget)}");
    }

    private void OnRenderException(object? sender, HelixToolkit.SharpDX.Utilities.RelayExceptionEventArgs e)
    {
        var ex = e.Exception;
        bool oom = ex is OutOfMemoryException || (ex is SharpDX.SharpDXException sx && (sx.HResult == unchecked((int)0x8007000E) || sx.HResult == unchecked((int)0x887A0005)
            || sx.Message.Contains("OUTOFMEMORY", StringComparison.OrdinalIgnoreCase)))
            || ex.GetBaseException() is OutOfMemoryException;
        ViewModels.MainViewModel.Log($"VRAM: render exception{(oom ? " (out of memory)" : "")}: {ex.GetType().Name}: {ex.Message}");
        if (!oom) return;
        e.Handled = true;
        Dispatcher.BeginInvoke(() =>
        {
            EscalateVram("GPU out of memory");
            _vm.StatusText = "The graphics card ran out of memory - the 3D view was simplified (see the Memory menu).";
        });
    }

    // ================================================================== residency

    private void SetResident(Element3D m, bool on)
    {
        if (on)
        {
            if (_evicted.Remove(m)) View.Items.Add(m);
        }
        else if (_evicted.Add(m))
        {
            m.Visibility = Visibility.Collapsed;
            View.Items.Remove(m);
        }
    }

    /// <summary>Puts every released model back (full detail / scene rebuilt).</summary>
    private void RestoreAllResident()
    {
        foreach (var m in _evicted.ToList()) SetResident(m, true);
    }

    /// <summary>Far-bridge LOD: bridges whose box is farther than <see cref="VramSettings.FarBridgeDistance"/> from the line of sight keep
    /// only their primary members on the GPU. Called after the detail visibility (camera settled, layers toggled).</summary>
    private void ApplyFarBridgeLod(Vector3? o, Vector3? inv)
    {
        bool lod = _vramLevel?.FarBridgeLod == true && _vramSettings.FarBridgeDistance > 0 && !_planMode;
        var far = new HashSet<int>();
        if (lod && o is { } oo && inv is { } iv)
            foreach (var (b, (lo, hi)) in _bridgeBounds)
                if (!SnapMath.LineHitsBox(oo, iv, lo, hi, (float)_vramSettings.FarBridgeDistance)) far.Add(b);
        foreach (var (m, b) in _modelBridge)
        {
            if (b < 0 || _detailBounds.ContainsKey(m)) continue; // detail tiles are handled with the detail rule
            bool keep = !far.Contains(b) || VramPlanner.PrimaryLayers.Contains(_modelLayer.GetValueOrDefault(m, ""));
            if (!keep) SetResident(m, false);
            else if (_evicted.Contains(m))
            {
                SetResident(m, true);
                string layer = _modelLayer.GetValueOrDefault(m, "");
                m.Visibility = !_planMode && _vm.IsLayerVisible(layer) ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        _farBridges = far;
    }

    private HashSet<int> _farBridges = new();

    /// <summary>After the solid models are created: bridge / layer of each model and each bridge's box (scene-local).</summary>
    private void RegisterVramModels(SceneData scene)
    {
        _evicted.Clear();
        _modelBridge.Clear();
        _modelLayer.Clear();
        _bridgeBounds.Clear();
        _farBridges.Clear();
        foreach (var solid in scene.Solids)
        {
            if (!_layerModels.TryGetValue(solid.Name, out var models)) continue;
            for (int i = 0; i < models.Count && i < solid.Parts.Count; i++)
            {
                int b = solid.BridgeOf(i);
                _modelBridge[models[i]] = b;
                _modelLayer[models[i]] = solid.Name;
                if (b < 0 || solid.Parts[i].mesh is not { } g) continue;
                Vector3 lo = g.Bound.Minimum, hi = g.Bound.Maximum;
                if (solid.Parts[i].instances is { Count: > 0 } inst)
                {
                    lo = new Vector3(float.MaxValue); hi = new Vector3(float.MinValue);
                    foreach (var t in inst) { lo = Vector3.Min(lo, t.Translation); hi = Vector3.Max(hi, t.Translation); }
                }
                _bridgeBounds[b] = _bridgeBounds.TryGetValue(b, out var bb) ? (Vector3.Min(bb.min, lo), Vector3.Max(bb.max, hi)) : (lo, hi);
            }
        }
    }

    /// <summary>Estimated bytes of the models currently in the viewport (resident).</summary>
    private long CurrentResidentEstimate()
    {
        long sum = 0;
        foreach (var m in View.Items)
            switch (m)
            {
                case MeshGeometryModel3D mm when mm.Geometry is MeshGeometry3D g:
                    sum += (long)(g.Positions?.Count ?? 0) * (VramItem.MeshVertexBytes + (g.TextureCoordinates != null ? VramItem.TexCoordBytes : 0))
                        + (long)(g.Indices?.Count ?? 0) * VramItem.IndexBytes + (long)(mm.Instances?.Count ?? 0) * VramItem.InstanceBytes;
                    break;
                case LineGeometryModel3D lm when lm.Geometry is { } lg:
                    sum += (long)(lg.Positions?.Count ?? 0) * VramItem.LineVertexBytes + (long)(lg.Indices?.Count ?? 0) * VramItem.IndexBytes;
                    break;
            }
        return sum;
    }

    // ================================================================== status + menu

    private void UpdateVramStatus()
    {
        if (_vramLevel == null) { _vm.VramText = ""; return; }
        string est = Mb(_vramLevel.Estimated);
        string budget = Mb(_vramBudget);
        string reduced = _vramLevel.IsFull ? "" : " - reduced: " + VramPlanner.Summary(_vramLadder, _vramLevel);
        if (_vramLevel.Index == 0 && _vramLevel.FarBridgeLod) reduced = " - far bridges simplified";
        _vm.VramText = $"GPU ~{est} of {budget}{(_vramForcedLevel >= 0 ? " (level set)" : "")}{reduced}";
        if (_scene?.DetailSkipped == true) _vm.VramText += " - detail not built (out of memory)";
    }

    private void OnVramMenu(object sender, RoutedEventArgs e)
    {
        VramMenuButton.IsChecked = false;
        var menu = new ContextMenu { PlacementTarget = VramMenuButton, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        var q = QueryVideoMemory();
        string Info()
        {
            string s = $"Viewport estimate {Mb(_vramLevel?.Estimated ?? 0)}, budget {Mb(_vramBudget)}";
            if (q != null) s += $"\nGPU {q.Adapter}: this process uses {Mb(q.Usage)} of the {Mb(q.Budget)} the system allows";
            return s;
        }
        menu.Items.Add(new MenuItem { Header = Info(), IsEnabled = false });
        menu.Items.Add(new Separator());
        StackPanel Row(string label, string value, Action<string> set, string tip)
        {
            var box = new TextBox { Text = value, Width = 70, Margin = new Thickness(6, 0, 0, 0) };
            box.LostFocus += (_, _) => set(box.Text);
            box.KeyDown += (_, ke) => { if (ke.Key == Key.Enter) { set(box.Text); menu.IsOpen = false; } };
            var row = new StackPanel { Orientation = Orientation.Horizontal, ToolTip = tip };
            row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Width = 260 });
            row.Children.Add(box);
            return row;
        }
        void Num(string text, Action<double> set) { if (double.TryParse(text.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out double v) && v >= 0) { set(v); Changed(); } }
        void Changed() { SaveVramSettings(); _vramMinLevel = 0; PlanVram("settings"); }
        menu.Items.Add(new MenuItem { Header = Row("GPU memory budget (MB, 0 = automatic)", _vramSettings.BudgetMb.ToString("0"), t => Num(t, v => _vramSettings.BudgetMb = v),
            "Automatic = 80 % of what the graphics driver allows this program. Lower it if the 3D view stutters or fails on a shared GPU."), StaysOpenOnClick = true });
        menu.Items.Add(new MenuItem { Header = Row("Simplify bridges farther than (m, 0 = never)", _vramSettings.FarBridgeDistance.ToString("0"), t => Num(t, v => _vramSettings.FarBridgeDistance = v),
            "Distance from the line of sight through the view center. Far bridges keep deck, barriers, girders and substructure concrete."), StaysOpenOnClick = true });
        menu.Items.Add(new MenuItem { Header = Row("Terrain kept around the bridges (m)", _vramSettings.TerrainClipMargin.ToString("0"), t => Num(t, v => _vramSettings.TerrainClipMargin = Math.Max(50, v)),
            "When terrain is reduced it is first clipped to the bridges' plan box grown by this distance."), StaysOpenOnClick = true });
        var auto = new MenuItem { Header = "Reduce automatically when the GPU memory runs short", IsCheckable = true, IsChecked = _vramSettings.AutoReduce, StaysOpenOnClick = true };
        auto.Click += (_, _) => { _vramSettings.AutoReduce = auto.IsChecked; Changed(); };
        menu.Items.Add(auto);
        var always = new MenuItem { Header = "Always simplify far bridges", IsCheckable = true, IsChecked = _vramSettings.AlwaysSimplifyFarBridges, StaysOpenOnClick = true };
        always.Click += (_, _) => { _vramSettings.AlwaysSimplifyFarBridges = always.IsChecked; Changed(); };
        menu.Items.Add(always);
        menu.Items.Add(new Separator());
        var levels = new MenuItem { Header = "Detail level" };
        var autoLevel = new MenuItem { Header = "Automatic (fit the budget)", IsCheckable = true, IsChecked = _vramForcedLevel < 0 };
        autoLevel.Click += (_, _) => { _vramForcedLevel = -1; _vramMinLevel = 0; PlanVram("settings"); };
        levels.Items.Add(autoLevel);
        foreach (var l in _vramLadder)
        {
            var li = new MenuItem { Header = $"{l.Index}: {l.Description} (~{Mb(l.Estimated)})", IsCheckable = true, IsChecked = _vramForcedLevel == l.Index };
            int idx = l.Index;
            li.Click += (_, _) => { _vramForcedLevel = idx; PlanVram("settings"); };
            levels.Items.Add(li);
        }
        menu.Items.Add(levels);
        var restore = new MenuItem { Header = "Restore full detail (forget earlier out-of-memory steps)", IsEnabled = _vramMinLevel > 0 || _vramForcedLevel >= 0 };
        restore.Click += (_, _) => { _vramMinLevel = 0; _vramForcedLevel = -1; PlanVram("restore"); };
        menu.Items.Add(restore);
        menu.IsOpen = true;
    }

    /// <summary>Draped image for a terrain, reduced so its longer side is at most <paramref name="maxPixels"/> (0 = as is).</summary>
    private static Stream TerrainImageStream(string file, int maxPixels, int w, int h)
    {
        if (maxPixels <= 0 || Math.Max(w, h) <= maxPixels) return new MemoryStream(File.ReadAllBytes(file));
        var bi = new BitmapImage();
        bi.BeginInit();
        bi.UriSource = new Uri(Path.GetFullPath(file));
        bi.CacheOption = BitmapCacheOption.OnLoad;
        if (w >= h) bi.DecodePixelWidth = maxPixels; else bi.DecodePixelHeight = maxPixels;
        bi.EndInit();
        var enc = new JpegBitmapEncoder { QualityLevel = 92 };
        enc.Frames.Add(BitmapFrame.Create(bi));
        var ms = new MemoryStream();
        enc.Save(ms);
        ms.Position = 0;
        return ms;
    }
}
