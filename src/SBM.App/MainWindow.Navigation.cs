using System.Numerics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using HelixToolkit.Wpf.SharpDX;
using SBM.App.Viewport;
using ProjectionCamera = HelixToolkit.Wpf.SharpDX.ProjectionCamera;
using OrthographicCamera = HelixToolkit.Wpf.SharpDX.OrthographicCamera;

namespace SBM.App;

/// <summary>
/// Responsive navigation for very large models:
/// <list type="bullet">
/// <item>No scene-wide hit test on mouse buttons (Helix's default hit-tests every triangle on each press): the
/// rotate point and the zoom-at-cursor point come from the plane through the view target.</item>
/// <item>Detail layers (bolts, studs, anchor bolts, welds, rebar) are split into spatial tiles. They are hidden while the
/// camera moves and come back ~¼ s after it stops, and only tiles within <see cref="_detailRadius"/> of the view
/// center are drawn (0 = all).</item>
/// <item>Zoom window: drag a rectangle to zoom to it.</item>
/// <item>Hold the middle button to pan. Right-drag orbits around the selection's centroid when something is selected,
/// else around the point under the cursor.</item>
/// </list>
/// </summary>
public partial class MainWindow
{
    private static readonly string[] DetailLayerPrefixes = { SceneBuilder.BoltLayer, SceneBuilder.StudLayer, SceneBuilder.AnchorLayer, "3D-WELDS", "3D-REBAR" };
    private readonly Dictionary<Element3D, (string layer, Vector3 c)> _detailCenter = new();
    private bool _navigating;
    private DispatcherTimer? _navTimer;
    /// <summary>Detail tiles farther than this (m, horizontal) from the view center are not drawn; 0 = draw all.</summary>
    private double _detailRadius = 120;

    private void InitNavigation()
    {
        RebarLod.ParseArgs(Environment.GetCommandLineArgs());
        View.EnableMouseButtonHitTest = false;
        View.EnableCursorPosition = false;
        View.RotateAroundMouseDownPoint = false;
        View.ZoomAroundMouseDownPoint = false;
        View.IsZoomEnabled = true;
        _navTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
        _navTimer.Tick += (_, _) =>
        {
            _navTimer!.Stop();
            _navigating = false;
            ApplyDetailVisibility();
            UpdateRebarLod();
        };
        View.CameraChanged += (_, _) => OnCameraMoved();
        View.PreviewMouseWheel += OnViewportWheel;
        View.PreviewMouseRightButtonDown += (_, e) => SetRotationPoint(e.GetPosition(View));
        View.PreviewMouseDown += OnMiddlePanDown;
        View.PreviewMouseMove += OnMiddlePanMove;
        View.PreviewMouseUp += OnMiddlePanUp;
        View.LostMouseCapture += (_, _) => _panFrom = null;
        View.PreviewMouseLeftButtonDown += OnZoomWindowDown;
        View.PreviewMouseMove += OnZoomWindowMove;
        View.PreviewMouseLeftButtonUp += OnZoomWindowUp;
    }

    private bool IsDetailLayer(string name) => DetailLayerPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal));

    /// <summary>Called after the solid models are created: remembers each detail model's center for distance culling.</summary>
    private void RegisterDetailModels()
    {
        _detailCenter.Clear();
        foreach (var (name, list) in _layerModels)
        {
            if (!IsDetailLayer(name)) continue;
            foreach (var m in list.OfType<MeshGeometryModel3D>())
            {
                Vector3 c;
                if (m.Instances is IList<Matrix4x4> inst && inst.Count > 0)
                {
                    var s = Vector3.Zero;
                    foreach (var t in inst) s += new Vector3(t.M41, t.M42, t.M43);
                    c = s / inst.Count;
                }
                else if (m.Geometry is { } g) { var b = g.Bound; c = (b.Minimum + b.Maximum) / 2; }
                else continue;
                _detailCenter[m] = (name, c);
            }
        }
    }

    private void OnCameraMoved()
    {
        if (_detailCenter.Count == 0 || _navTimer == null) return;
        if (!_navigating)
        {
            _navigating = true;
            foreach (var m in _detailCenter.Keys) m.Visibility = Visibility.Collapsed;
        }
        _navTimer.Stop();
        _navTimer.Start();
    }

    /// <summary>Hides detail tiles that are far from the view center (layer visibility already applied).</summary>
    private void ApplyDetailVisibility()
    {
        if (_detailCenter.Count == 0) return;
        Vector3? center = null;
        if (View.Camera is ProjectionCamera cam)
        {
            var t = cam.Position + cam.LookDirection;
            center = new Vector3((float)t.X, (float)t.Y, (float)t.Z);
        }
        var layerOnCache = new Dictionary<string, bool>();
        foreach (var (m, (layer, c)) in _detailCenter)
        {
            if (!layerOnCache.TryGetValue(layer, out bool lon)) layerOnCache[layer] = lon = !_planMode && _vm.IsLayerVisible(layer);
            bool layerOn = lon;
            bool near = _detailRadius <= 0 || center is not { } v || new Vector2(c.X - v.X, c.Y - v.Y).Length() <= _detailRadius;
            m.Visibility = layerOn && !_navigating && near ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // ------------------------------------------------------------------ cheap rotate / zoom points

    private Point3D? PlanePoint(Point screen)
    {
        if (View.Camera is not ProjectionCamera cam) return null;
        var target = cam.Position + cam.LookDirection;
        return View.UnProjectOnPlane(screen, new Point3D(0, 0, target.Z), new Vector3D(0, 0, 1));
    }

    /// <summary>Orbit pivot: the centroid of the selection (vertex average of the highlight mesh) when something is
    /// selected, else the point under the cursor on the plane through the view target.</summary>
    private void SetRotationPoint(Point screen)
    {
        if (SelectionCentroid() is { } c)
        {
            View.FixedRotationPoint = c;
            View.FixedRotationPointEnabled = true;
        }
        else if (PlanePoint(screen) is { } p)
        {
            View.FixedRotationPoint = p;
            View.FixedRotationPointEnabled = true;
        }
    }

    private Point3D? SelectionCentroid()
    {
        if (_highlight?.Geometry is not { Positions: { Count: > 0 } pos }) return null;
        var s = Vector3.Zero;
        foreach (var p in pos) s += p;
        s /= pos.Count;
        return new Point3D(s.X, s.Y, s.Z);
    }

    // ------------------------------------------------------------------ middle-button pan

    private Point? _panFrom;

    /// <summary>Point under <paramref name="screen"/> on the plane through the view target, normal to the view direction
    /// (the cursor stays on the same model point while panning, for orthographic and perspective cameras).</summary>
    private Point3D? ViewPlanePoint(Point screen)
    {
        if (View.Camera is not ProjectionCamera cam) return null;
        var target = cam.Position + cam.LookDirection;
        return View.UnProjectOnPlane(screen, target, cam.LookDirection);
    }

    private void OnMiddlePanDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle || View.Camera is not ProjectionCamera) return;
        e.Handled = true; // our pan, not Helix's middle-button gesture
        _panFrom = e.GetPosition(View);
        View.CaptureMouse();
        View.Cursor = Cursors.SizeAll;
    }

    private void OnMiddlePanMove(object sender, MouseEventArgs e)
    {
        if (_panFrom is not { } from || e.MiddleButton != MouseButtonState.Pressed || View.Camera is not ProjectionCamera cam) return;
        var to = e.GetPosition(View);
        if (ViewPlanePoint(from) is { } a && ViewPlanePoint(to) is { } b)
        {
            cam.Position += a - b;
            _panFrom = to;
        }
        e.Handled = true;
    }

    private void OnMiddlePanUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle || _panFrom == null) return;
        e.Handled = true;
        _panFrom = null;
        View.ReleaseMouseCapture();
        View.Cursor = null;
    }

    /// <summary>Zoom toward the cursor (orthographic camera: scale the width, keep the point under the cursor fixed).</summary>
    private void OnViewportWheel(object sender, MouseWheelEventArgs e)
    {
        if (View.Camera is not OrthographicCamera cam) return;
        e.Handled = true;
        double f = e.Delta > 0 ? 0.8 : 1.25;
        var p = PlanePoint(e.GetPosition(View));
        cam.Width = Math.Clamp(cam.Width * f, 0.5, 200000);
        if (p is { } q)
        {
            var look = cam.LookDirection; look.Normalize();
            var d = q - cam.Position;
            var perp = d - look * Vector3D.DotProduct(d, look);
            cam.Position += perp * (1 - f);
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
        System.Windows.Controls.Canvas.SetLeft(ZoomRect, _zoomStart.Value.X);
        System.Windows.Controls.Canvas.SetTop(ZoomRect, _zoomStart.Value.Y);
        ZoomRect.Width = ZoomRect.Height = 0;
        ZoomRect.Visibility = Visibility.Visible;
    }

    private void OnZoomWindowMove(object sender, MouseEventArgs e)
    {
        if (!_zoomWindowArmed || _zoomStart is not { } a) return;
        var b = e.GetPosition(View);
        System.Windows.Controls.Canvas.SetLeft(ZoomRect, Math.Min(a.X, b.X));
        System.Windows.Controls.Canvas.SetTop(ZoomRect, Math.Min(a.Y, b.Y));
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
        if (w < 4 || h < 4 || View.Camera is not OrthographicCamera cam) return;
        var mid = new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2);
        if (PlanePoint(mid) is not { } c) return;
        double f = Math.Max(w / View.ActualWidth, h / View.ActualHeight);
        var look = cam.LookDirection; look.Normalize();
        var target = cam.Position + cam.LookDirection;
        var d = c - target;
        var perp = d - look * Vector3D.DotProduct(d, look);
        cam.Position += perp;
        cam.Width = Math.Max(0.5, cam.Width * f);
    }
}
