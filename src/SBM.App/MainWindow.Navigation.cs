using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;
using HelixToolkit.Wpf.SharpDX;
using SBM.App.Viewport;
using SBM.Core.Geometry;
using ProjectionCamera = HelixToolkit.Wpf.SharpDX.ProjectionCamera;
using OrthographicCamera = HelixToolkit.Wpf.SharpDX.OrthographicCamera;
using PerspectiveCamera = HelixToolkit.Wpf.SharpDX.PerspectiveCamera;

namespace SBM.App;

/// <summary>
/// CAD / Tekla-style viewport navigation (all camera moves are ours; Helix's default gestures are switched off with
/// <c>UseDefaultGestures="False"</c>). The mouse bindings are the table <see cref="NavPresets.Cad"/>:
/// <list type="bullet">
/// <item>Middle-drag = pan (the grabbed point stays under the cursor). Double-click middle = zoom extents.</item>
/// <item>Ctrl / Shift + middle-drag, or right-drag = orbit (a right CLICK without dragging opens the context menu).
/// Turntable: yaw about the vertical, pitch clamped to ±89° (no roll, never over the pole), no inertia.</item>
/// <item>Orbit pivot = the model surface point under the cursor when the drag starts (BVH ray hit on visible geometry,
/// <see cref="SnapIndex"/>); nothing there → the model point at the screen center → the previous pivot if it is on screen →
/// the view target. With "Pivot: selection" on and something selected, the selection's centroid. A marker shows the pivot
/// while orbiting.</item>
/// <item>Wheel = zoom 15 % per notch about the point under the cursor.</item>
/// <item>Detail layers (bolts, studs, anchor bolts, welds, rebar) are split into spatial tiles. They are hidden while the
/// camera moves and come back ~¼ s after it stops; only tiles within <see cref="_detailRadius"/> of the view-center LINE
/// of sight are drawn (0 = all).</item>
/// <item>Zoom window: drag a rectangle to zoom to it.</item>
/// </list>
/// The camera math is in <see cref="ViewNav"/> (unit tested).
/// </summary>
public partial class MainWindow
{
    private static readonly string[] DetailLayerPrefixes = { SceneBuilder.BoltLayer, SceneBuilder.StudLayer, SceneBuilder.AnchorLayer, "3D-WELDS", "3D-REBAR" };
    /// <summary>Detail models (tiles) with their layer and scene-local bounding box.</summary>
    private readonly Dictionary<Element3D, (string layer, Vector3 min, Vector3 max)> _detailBounds = new();
    private bool _navigating;
    private DispatcherTimer? _navTimer;
    /// <summary>Detail tiles farther than this (m) from the line of sight through the view center are not drawn; 0 = draw all.</summary>
    private double _detailRadius = 120;
    private DateTime _lastVramCheck;

    /// <summary>The active mouse bindings (a table, so other presets can be added).</summary>
    private readonly IReadOnlyList<NavBinding> _navBindings = NavPresets.Cad;

    private void InitNavigation()
    {
        RebarLod.ParseArgs(Environment.GetCommandLineArgs());
        View.UseDefaultGestures = false; // Helix's own rotate / pan / zoom bindings would fight ours
        View.InputBindings.Clear();
        View.EnableMouseButtonHitTest = false;
        View.EnableCursorPosition = false;
        View.RotateAroundMouseDownPoint = false;
        View.ZoomAroundMouseDownPoint = false;
        View.IsInertiaEnabled = false;
        // Normal priority: a Background timer can starve while the viewport keeps rendering.
        _navTimer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(250) };
        _navTimer.Tick += (_, _) =>
        {
            _navTimer!.Stop();
            _navigating = false;
            ApplyDetailVisibility();
            UpdateRebarLod();
            if ((DateTime.UtcNow - _lastVramCheck).TotalSeconds > 2) { _lastVramCheck = DateTime.UtcNow; CheckVramUsage(); }
        };
        View.CameraChanged += (_, _) => { OnCameraMoved(); UpdatePivotMarker(); };
        View.PreviewMouseWheel += OnViewportWheel;
        View.PreviewMouseDown += OnNavDown;
        View.PreviewMouseMove += OnNavMove;
        View.PreviewMouseUp += OnNavUp;
        View.LostMouseCapture += (_, _) => EndNavDrag();
        View.PreviewMouseLeftButtonDown += OnZoomWindowDown;
        View.PreviewMouseMove += OnZoomWindowMove;
        View.PreviewMouseLeftButtonUp += OnZoomWindowUp;
        NavHelp.ToolTip = "Mouse (Tekla / CAD style)\n" + NavPresets.Describe(_navBindings)
            + "\n\nOrbit pivot: the model point under the cursor when the drag starts (marked with a cross);"
            + "\nnothing under the cursor → the model point at the screen center."
            + "\n'Pivot: selection' orbits around the selected element instead.";
    }

    private bool IsDetailLayer(string name) => DetailLayerPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal));

    /// <summary>Called after the solid models are created: remembers each detail model's bounds for distance culling.</summary>
    private void RegisterDetailModels()
    {
        _detailBounds.Clear();
        foreach (var (name, list) in _layerModels)
        {
            if (!IsDetailLayer(name)) continue;
            foreach (var m in list.OfType<MeshGeometryModel3D>())
            {
                if (m.Geometry is not { } g) continue;
                var gb = g.Bound;
                if (m.Instances is IList<Matrix4x4> inst && inst.Count > 0)
                {
                    // Instances: box of the origins, grown by the local mesh's reach.
                    float reach = Math.Max(gb.Minimum.Length(), gb.Maximum.Length());
                    var lo = new Vector3(float.MaxValue);
                    var hi = new Vector3(float.MinValue);
                    foreach (var t in inst) { lo = Vector3.Min(lo, t.Translation); hi = Vector3.Max(hi, t.Translation); }
                    _detailBounds[m] = (name, lo - new Vector3(reach), hi + new Vector3(reach));
                }
                else _detailBounds[m] = (name, gb.Minimum, gb.Maximum);
            }
        }
    }

    /// <summary>What the orthographic view shows: view direction, width and the eye projected on the plane normal to the view
    /// (sliding the eye along the view, as <see cref="KeepCameraInRange"/> does, changes nothing on screen).</summary>
    private (Vec3 look, double width, Vec3 eye)? _lastViewKey;
    private int _cameraEvents, _cameraMoves;

    private bool ViewReallyChanged()
    {
        if (CurrentView() is not { } v) return true;
        var eye = v.Position - v.Look * Vec3.Dot(v.Position, v.Look);
        if (_lastViewKey is { } k && (k.look - v.Look).Length < 1e-7 && Math.Abs(k.width - v.Width) <= 1e-7 * Math.Max(1, v.Width)
            && (k.eye - eye).Length < 1e-5) return false;
        _lastViewKey = (v.Look, v.Width, eye);
        return true;
    }

    private void OnCameraMoved()
    {
        if (_detailBounds.Count == 0 || _navTimer == null) return;
        _cameraEvents++;
        // CameraChanged also fires for updates that change nothing on screen (KeepCameraInRange re-setting the eye depth and
        // the look length); those must not count as navigation, or the timer restarts forever and detail (rebar) never returns.
        if (!ViewReallyChanged()) return;
        _cameraMoves++;
        if (!_navigating)
        {
            _navigating = true;
            foreach (var m in _detailBounds.Keys) m.Visibility = Visibility.Collapsed;
        }
        _navTimer.Stop();
        _navTimer.Start();
    }

    /// <summary>
    /// Shows the detail tiles of visible layers near the line of sight through the view center (layer visibility already
    /// applied). The distance is measured to that LINE, not to a point: the orthographic eye sits outside the whole scene,
    /// so "camera position + look direction" is the depth of the scene center, which in an oblique view can be hundreds of
    /// meters from what is on screen (rebar of Ramp G Pier 7G disappeared close up that way).
    /// </summary>
    private void ApplyDetailVisibility()
    {
        if (_detailBounds.Count == 0) return;
        Vector3? o = null, inv = null;
        if (View.Camera is ProjectionCamera cam && cam.LookDirection.Length > 1e-9)
        {
            var d = cam.LookDirection; d.Normalize();
            o = new Vector3((float)cam.Position.X, (float)cam.Position.Y, (float)cam.Position.Z);
            inv = SnapMath.Reciprocal(new Vector3((float)d.X, (float)d.Y, (float)d.Z));
        }
        float radius = (float)_detailRadius;
        var layerOnCache = new Dictionary<string, bool>();
        // Viewport memory manager: tiles out of reach are RELEASED from the GPU (removed from the viewport) instead of only hidden;
        // detail of far (simplified) bridges and all detail at the last level go the same way. Residency changes only when the
        // camera has settled (all detail is hidden while it moves anyway).
        bool residency = _vramLevel is { DetailResidency: true } || _vramLevel is { DropDetail: true };
        bool drop = _vramLevel is { DropDetail: true };
        if (!_navigating) ApplyFarBridgeLod(o, inv);
        foreach (var (m, (layer, lo, hi)) in _detailBounds)
        {
            if (!layerOnCache.TryGetValue(layer, out bool lon)) layerOnCache[layer] = lon = (!_planMode || _iso.ShowsInPlan(layer)) && _vm.IsLayerVisible(layer);
            bool near = _detailRadius <= 0 || o is not { } oo || SnapMath.LineHitsBox(oo, inv!.Value, lo, hi, radius);
            bool farBridge = _farBridges.Count > 0 && _modelBridge.TryGetValue(m, out int mb) && _farBridges.Contains(mb);
            bool show = lon && near && !drop && !farBridge;
            if (!_navigating)
            {
                if (residency || farBridge) SetResident(m, show);
                else SetResident(m, true);
            }
            m.Visibility = show && !_navigating ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    /// <summary>Snapshot diagnostics: per detail layer, tiles drawn / total (DETAIL: lines in the session log).</summary>
    private void LogDetailVisibility()
    {
        if (_farBridges.Count > 0 || _evicted.Count > 0)
            ViewModels.MainViewModel.Log($"DETAIL: far (simplified) bridges: {string.Join(", ", _farBridges.Select(b => _scene != null && b < _scene.BridgeNames.Count ? _scene.BridgeNames[b] : b.ToString()))}; {_evicted.Count} models released from the GPU");
        ViewModels.MainViewModel.Log($"DETAIL: {_cameraEvents} camera events, {_cameraMoves} real view changes, navigating: {_navigating}, nav timer running: {_navTimer?.IsEnabled}");
        foreach (var g in _detailBounds.GroupBy(kv => kv.Value.layer))
        {
            // For comparison: the old rule (horizontal distance from camera position + look direction).
            int old = 0;
            if (View.Camera is ProjectionCamera cam)
            {
                var t = cam.Position + cam.LookDirection;
                old = g.Count(kv => { var c = (kv.Value.min + kv.Value.max) / 2; return new Vector2(c.X - (float)t.X, c.Y - (float)t.Y).Length() <= _detailRadius; });
            }
            ViewModels.MainViewModel.Log($"DETAIL {g.Key}: {g.Count(kv => kv.Key.Visibility == Visibility.Visible)} of {g.Count()} tiles drawn, {g.Count(kv => _evicted.Contains(kv.Key))} released from the GPU (layer on: {_vm.IsLayerVisible(g.Key)}, radius {_detailRadius} m; old target-point rule would draw {old})");
        }
    }

    // ------------------------------------------------------------------ camera <-> ViewNav

    private OrthoView? CurrentView() =>
        View.Camera is OrthographicCamera c && c.LookDirection.Length > 1e-9
            ? new OrthoView(V(c.Position), V(c.LookDirection).Normalized(), V(c.UpDirection), c.Width) : null;

    private void SetView(OrthoView v)
    {
        if (View.Camera is not OrthographicCamera c) return;
        double len = Math.Max(c.LookDirection.Length, 1);
        c.UpDirection = new Vector3D(v.Up.X, v.Up.Y, v.Up.Z);
        c.LookDirection = new Vector3D(v.Look.X, v.Look.Y, v.Look.Z) * len;
        c.Position = new Point3D(v.Position.X, v.Position.Y, v.Position.Z);
        c.Width = v.Width;
    }

    private static Vec3 V(Point3D p) => new(p.X, p.Y, p.Z);
    private static Vec3 V(Vector3D p) => new(p.X, p.Y, p.Z);
    private static Vec3 V(Vector3 p) => new(p.X, p.Y, p.Z);
    private (double, double) S(Point p) => (p.X, p.Y);

    // ------------------------------------------------------------------ mouse: pan / orbit / zoom extents

    private MouseButton? _dragButton;
    private NavAction _dragAction;
    private Point _dragStart, _dragFrom;
    private bool _dragActive;
    private Vec3 _orbitPivot;
    /// <summary>The last orbit pivot (scene-local): kept as the reference for the next orbit when nothing is under the cursor.</summary>
    private Vec3? _pivot;
    /// <summary>Orbit around the selection's centroid when something is selected (toolbar "Pivot: selection").</summary>
    private bool _orbitAroundSelection;

    private static NavMods CurrentMods()
    {
        var k = Keyboard.Modifiers;
        return (k.HasFlag(ModifierKeys.Control) ? NavMods.Ctrl : 0) | (k.HasFlag(ModifierKeys.Shift) ? NavMods.Shift : 0) | (k.HasFlag(ModifierKeys.Alt) ? NavMods.Alt : 0);
    }

    private void OnNavDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton is not (MouseButton.Middle or MouseButton.Right) || _dragButton != null) return;
        var pos = e.GetPosition(View);
        var btn = e.ChangedButton == MouseButton.Middle ? NavButton.Middle : NavButton.Right;
        var action = NavPresets.Resolve(_navBindings, btn, CurrentMods(), e.ClickCount == 2);
        e.Handled = true; // ours, not Helix's
        if (e.ChangedButton == MouseButton.Right) OnViewportRightDown(sender, e); // remembers the press for the context menu
        if (action == NavAction.ZoomExtents) { ZoomExtents(); return; }
        if (action == NavAction.Orbit && _planMode)
        {
            if (btn == NavButton.Middle) StatusText("Plan view is locked to the top view: switch to 3D to orbit.");
            action = NavAction.None;
        }
        if (action == NavAction.None && btn == NavButton.Middle) return;
        _dragButton = e.ChangedButton;
        _dragAction = action;
        _dragStart = _dragFrom = pos;
        _dragActive = btn == NavButton.Middle; // a right press becomes an orbit only once it moves (a click = context menu)
        View.CaptureMouse();
        if (_dragActive) BeginDragAction();
    }

    private void BeginDragAction()
    {
        if (_dragAction == NavAction.Pan) View.Cursor = Cursors.SizeAll;
        else if (_dragAction == NavAction.Orbit)
        {
            var (p, what) = ChoosePivot(_dragStart);
            _orbitPivot = p;
            _pivot = p;
            ShowPivotMarker(p);
            StatusText("Orbit about " + what);
            View.Cursor = Cursors.Hand;
        }
    }

    private void OnNavMove(object sender, MouseEventArgs e)
    {
        if (_dragButton is not { } b) return;
        var state = b == MouseButton.Middle ? e.MiddleButton : e.RightButton;
        if (state != MouseButtonState.Pressed) { EndNavDrag(); return; }
        var to = e.GetPosition(View);
        if (!_dragActive)
        {
            if ((to - _dragStart).Length <= 4 || _dragAction == NavAction.None) return;
            _dragActive = true;
            BeginDragAction();
        }
        e.Handled = true;
        if (CurrentView() is not { } v || View.ActualWidth < 2) { _dragFrom = to; return; }
        if (_dragAction == NavAction.Pan)
            SetView(ViewNav.Pan(v, S(_dragFrom), S(to), View.ActualWidth, View.ActualHeight));
        else if (_dragAction == NavAction.Orbit)
        {
            var (yaw, pitch) = ViewNav.DragToAngles(to.X - _dragFrom.X, to.Y - _dragFrom.Y);
            SetView(ViewNav.Orbit(v, _orbitPivot, yaw, pitch));
        }
        _dragFrom = to;
    }

    private void OnNavUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragButton != e.ChangedButton) return;
        bool wasOrbitDrag = _dragActive;
        EndNavDrag();
        if (e.ChangedButton == MouseButton.Right)
        {
            if (!wasOrbitDrag) OnViewportRightUp(sender, e); // a click: context menu
            else _rightDownAt = null;
        }
        e.Handled = true;
    }

    private void EndNavDrag()
    {
        if (_dragButton == null) return;
        bool orbit = _dragActive && _dragAction == NavAction.Orbit;
        _dragButton = null;
        _dragActive = false;
        if (Mouse.Captured == View) View.ReleaseMouseCapture();
        View.Cursor = _zoomWindowArmed ? Cursors.Cross : null;
        if (orbit) HidePivotMarkerSoon();
    }

    // ------------------------------------------------------------------ orbit pivot

    /// <summary>
    /// The orbit pivot for a drag starting at <paramref name="screen"/> and a description for the status bar:
    /// selection centroid (when "Pivot: selection" is on and something is selected) → model surface under the cursor →
    /// model surface at the screen center → previous pivot if still on screen → the view target. Never an arbitrary plane point.
    /// </summary>
    private (Vec3 p, string what) ChoosePivot(Point screen)
    {
        if (_orbitAroundSelection && SelectionCentroid() is { } c) return (V(c), "the selection centroid");
        if (SurfaceHit(screen) is { } h) return (V(h.P), Describe(h.Tag) is { Length: > 0 } d ? d + " (point under the cursor)" : "the point under the cursor");
        var mid = new Point(View.ActualWidth / 2, View.ActualHeight / 2);
        if (SurfaceHit(mid) is { } hc) return (V(hc.P), "the model at the screen center (nothing under the cursor)");
        if (_pivot is { } last && CurrentView() is { } v)
        {
            var (x, y) = ViewNav.ToScreen(v, last, View.ActualWidth, View.ActualHeight);
            if (x >= 0 && y >= 0 && x <= View.ActualWidth && y <= View.ActualHeight) return (last, "the previous pivot");
        }
        var cam = (ProjectionCamera)View.Camera;
        return (V(cam.Position + cam.LookDirection), "the view target");
    }

    /// <summary>Nearest visible model surface under a screen point (BVH ray cast, section cut and hidden elements respected).</summary>
    private SnapHit? SurfaceHit(Point screen)
    {
        if (_snapIndex == null) { EnsureSnapIndex(); return null; }
        if (RayAt(screen) is not { } ray) return null;
        return _snapIndex.RayCast(ray.Origin, ray.Dir, MakeEnv());
    }

    private Point3D? SelectionCentroid()
    {
        if (_highlight?.Geometry is not { Positions: { Count: > 0 } pos }) return null;
        var s = Vector3.Zero;
        foreach (var p in pos) s += p;
        s /= pos.Count;
        return new Point3D(s.X, s.Y, s.Z);
    }

    private void OnPivotModeToggle(object sender, RoutedEventArgs e)
    {
        _orbitAroundSelection = PivotSelToggle.IsChecked == true;
        PivotSelToggle.Content = _orbitAroundSelection ? "Pivot: selection" : "Pivot: cursor";
    }

    /// <summary>Snapshot option <c>--orbit fx,fy,dx,dy</c>: an orbit drag starting at the view fraction (fx, fy) by (dx, dy) pixels,
    /// as the mouse would do it; logs the chosen pivot (ORBIT:) and leaves its marker on screen.</summary>
    private void SnapshotOrbit(string spec)
    {
        var a = spec.Split(',').Select(x => double.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        WaitForSnapIndex();
        var start = new Point(a[0] * View.ActualWidth, a[1] * View.ActualHeight);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var (p, what) = ChoosePivot(start);
        double pickMs = sw.Elapsed.TotalMilliseconds;
        _pivot = p;
        ViewModels.MainViewModel.Log($"ORBIT pick: {pickMs:F1} ms");
        var before = ViewNav.ToScreen(CurrentView()!.Value, p, View.ActualWidth, View.ActualHeight);
        var (yaw, pitch) = ViewNav.DragToAngles(a[2], a[3]);
        SetView(ViewNav.Orbit(CurrentView()!.Value, p, yaw, pitch));
        KeepCameraInRange();
        var after = ViewNav.ToScreen(CurrentView()!.Value, p, View.ActualWidth, View.ActualHeight);
        var w = ToWorld(new Vector3((float)p.X, (float)p.Y, (float)p.Z));
        ViewModels.MainViewModel.Log($"ORBIT: pivot = {what} at E {w.X:F2} N {w.Y:F2} Z {w.Z:F2}; cursor ({start.X:F0}, {start.Y:F0}); pivot on screen before ({before.x:F1}, {before.y:F1}) after ({after.x:F1}, {after.y:F1}); pitch {ViewNav.Pitch(CurrentView()!.Value.Look) * 180 / Math.PI:F1} deg");
        ShowPivotMarker(p);
        if (View.Camera is OrthographicCamera oc)
        {
            var lk = oc.LookDirection; lk.Normalize();
            double depth = Vector3D.DotProduct(new Point3D(p.X, p.Y, p.Z) - oc.Position, lk);
            ViewModels.MainViewModel.Log($"ORBIT camera: pos {oc.Position}, look {oc.LookDirection}, up {oc.UpDirection}, width {oc.Width:F2}, near {oc.NearPlaneDistance:F1}, far {oc.FarPlaneDistance:F1}, pivot depth {depth:F1}");
        }
    }

    private void OnNavHelp(object sender, RoutedEventArgs e)
    {
        NavHelp.IsChecked = false;
        MessageBox.Show(this, NavHelp.ToolTip as string ?? "", "Mouse navigation", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ------------------------------------------------------------------ pivot marker (screen overlay: always on top, constant size)

    private Vec3? _markerAt;
    private DispatcherTimer? _markerTimer;

    private void ShowPivotMarker(Vec3 p)
    {
        _markerTimer?.Stop();
        _markerAt = p;
        if (PivotOverlay.Children.Count == 0)
        {
            Shape Halo(Shape s) { s.Stroke = new SolidColorBrush(Color.FromArgb(190, 0, 0, 0)); s.StrokeThickness = 4; return s; }
            Shape Ink(Shape s) { s.Stroke = new SolidColorBrush(Color.FromRgb(255, 225, 77)); s.StrokeThickness = 1.6; return s; }
            foreach (var mk in new Func<Shape, Shape>[] { Halo, Ink })
            {
                PivotOverlay.Children.Add(mk(new Line { X1 = -12, X2 = 12 }));
                PivotOverlay.Children.Add(mk(new Line { Y1 = -12, Y2 = 12 }));
                PivotOverlay.Children.Add(mk(new Ellipse { Width = 12, Height = 12, Margin = new Thickness(-6, -6, 0, 0) }));
            }
        }
        PivotOverlay.Visibility = Visibility.Visible;
        UpdatePivotMarker();
    }

    private void UpdatePivotMarker()
    {
        if (_markerAt is not { } p || PivotOverlay.Visibility != Visibility.Visible || CurrentView() is not { } v) return;
        var (x, y) = ViewNav.ToScreen(v, p, View.ActualWidth, View.ActualHeight);
        foreach (UIElement c in PivotOverlay.Children) { Canvas.SetLeft(c, x); Canvas.SetTop(c, y); }
    }

    private void HidePivotMarkerSoon()
    {
        _markerTimer ??= new DispatcherTimer(TimeSpan.FromSeconds(1.2), DispatcherPriority.Background,
            (_, _) => { _markerTimer!.Stop(); PivotOverlay.Visibility = Visibility.Collapsed; _markerAt = null; }, Dispatcher);
        _markerTimer.Stop();
        _markerTimer.Start();
    }

    // ------------------------------------------------------------------ wheel zoom

    /// <summary>Zoom 15 % per notch about the point under the cursor (it stays under the cursor).</summary>
    private void OnViewportWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
        var pt = e.GetPosition(View);
        double f = ViewNav.WheelFactor(e.Delta);
        if (CurrentView() is { } v && View.ActualWidth >= 2)
        {
            // Orthographic: every point of the cursor's pixel ray stays under the cursor, so the ray origin will do.
            var p = ViewNav.ScreenToPlane(v, pt.X, pt.Y, View.ActualWidth, View.ActualHeight);
            SetView(ViewNav.ZoomAt(v, p, f));
        }
        else if (View.Camera is PerspectiveCamera pc)
        {
            // Perspective: dolly toward the model point under the cursor; never stalls (passes through), near plane follows.
            var target = SurfaceHit(pt) is { } h ? V(h.P) : V(pc.Position + pc.LookDirection);
            var eye = ViewNav.DollyToward(V(pc.Position), target, f, 0.05);
            pc.Position = new Point3D(eye.X, eye.Y, eye.Z);
            pc.NearPlaneDistance = ViewNav.NearPlaneFor((target - eye).Length);
        }
    }

    // ------------------------------------------------------------------ zoom window

    private bool _zoomWindowArmed;
    private Point? _zoomStart;

    private void OnZoomWindowButton(object sender, RoutedEventArgs e)
    {
        _zoomWindowArmed = true;
        View.Cursor = Cursors.Cross;
        _vm.StatusText = "Zoom window: drag a rectangle in the view (Esc to cancel).";
    }

    private void OnZoomExtentsButton(object sender, RoutedEventArgs e) => ZoomExtents();

    private void CancelZoomWindow()
    {
        _zoomWindowArmed = false;
        _zoomStart = null;
        ZoomRect.Visibility = Visibility.Collapsed;
        View.Cursor = null;
    }

    private void OnZoomWindowDown(object sender, MouseButtonEventArgs e)
    {
        if (!_zoomWindowArmed) return;
        e.Handled = true;
        _zoomStart = e.GetPosition(View);
        View.CaptureMouse();
        Canvas.SetLeft(ZoomRect, _zoomStart.Value.X);
        Canvas.SetTop(ZoomRect, _zoomStart.Value.Y);
        ZoomRect.Width = ZoomRect.Height = 0;
        ZoomRect.Visibility = Visibility.Visible;
    }

    private void OnZoomWindowMove(object sender, MouseEventArgs e)
    {
        if (!_zoomWindowArmed || _zoomStart is not { } a) return;
        var b = e.GetPosition(View);
        Canvas.SetLeft(ZoomRect, Math.Min(a.X, b.X));
        Canvas.SetTop(ZoomRect, Math.Min(a.Y, b.Y));
        ZoomRect.Width = Math.Abs(b.X - a.X);
        ZoomRect.Height = Math.Abs(b.Y - a.Y);
    }

    private void OnZoomWindowUp(object sender, MouseButtonEventArgs e)
    {
        if (!_zoomWindowArmed || _zoomStart is not { } a) return;
        e.Handled = true;
        View.ReleaseMouseCapture();
        var b = e.GetPosition(View);
        CancelZoomWindow();
        double w = Math.Abs(b.X - a.X), h = Math.Abs(b.Y - a.Y);
        if (w < 4 || h < 4 || CurrentView() is not { } v) return;
        double vw = View.ActualWidth, vh = View.ActualHeight;
        // Center the window, then scale the width about the center.
        var moved = ViewNav.Pan(v, ((a.X + b.X) / 2, (a.Y + b.Y) / 2), (vw / 2, vh / 2), vw, vh);
        double f = Math.Max(w / vw, h / vh);
        SetView(ViewNav.ZoomAt(moved, moved.Position, f));
    }
}
