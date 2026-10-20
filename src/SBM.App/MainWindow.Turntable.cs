using System.Globalization;
using System.Windows;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf.SharpDX;
using SBM.Core.Geometry;
using OrthographicCamera = HelixToolkit.Wpf.SharpDX.OrthographicCamera;

namespace SBM.App;

public partial class MainWindow
{
    /// <summary>True while a turntable run owns the camera: camera moves must not hide / re-show the detail tiles (no flicker between frames).</summary>
    private bool _turntableFreeze;

    /// <summary>
    /// Snapshot option <c>--turntable N[,degrees[,pitch]]</c> (N frames, default 120; degrees default 360; pitch = degrees the camera looks down,
    /// default the current one): orbits the orthographic camera about the scene centre (yaw about world Z) and writes the viewport only to
    /// <c>&lt;out&gt;_frame0001.png</c> ... Detail tiles (welds, rivets, bolts, near rebar) are OFF in every frame unless <c>--detail</c> is given
    /// (then the set drawn at the start stays drawn). Logs TURNTABLE: lines.
    /// </summary>
    private async Task RunTurntable(string spec, string path, bool keepDetail)
    {
        if (_scene == null || _scene.Extents.IsEmpty || View.Camera is not OrthographicCamera cam)
        {
            ViewModels.MainViewModel.Log("TURNTABLE: no 3D scene, nothing written");
            return;
        }
        var inv = CultureInfo.InvariantCulture;
        var a = spec.Split(',', StringSplitOptions.RemoveEmptyEntries);
        int n = a.Length > 0 ? Math.Max(1, (int)double.Parse(a[0], inv)) : 120;
        double degrees = a.Length > 1 ? double.Parse(a[1], inv) : 360;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        _turntableFreeze = true;
        _navTimer?.Stop();
        if (!keepDetail)
        {
            _navigating = true;
            foreach (var m in _detailBounds.Keys) m.Visibility = Visibility.Collapsed;
        }

        var e = _scene.FocusExtents;
        var center = new Vec3(e.Center.X - _scene.Origin.X, e.Center.Y - _scene.Origin.Y, 0);
        double radius = Math.Sqrt(e.Width * e.Width + e.Height * e.Height + 40.0 * 40.0) / 2;
        double aspect = View.ActualHeight > 0 ? View.ActualWidth / View.ActualHeight : 16.0 / 9;
        double width = Turntable.WidthForSphere(radius, aspect);
        var cur = CurrentView();
        var look0 = cur?.Look ?? Turntable.Look(0.8, 35);
        if (a.Length > 2)
        {
            double heading = Math.Atan2(look0.Y, look0.X);
            look0 = Turntable.Look(heading, double.Parse(a[2], inv));
        }
        ViewModels.MainViewModel.Log($"TURNTABLE: {n} frames, {degrees:F0} deg, pitch {-ViewNav.Pitch(look0) * 180 / Math.PI:F1} deg down, width {width:F1} m, centre E {e.Center.X:F1} N {e.Center.Y:F1}, detail {(keepDetail ? "frozen as settled" : "off")}, view {View.ActualWidth:F0} x {View.ActualHeight:F0}");

        string stem = System.IO.Path.ChangeExtension(path, null);
        string? dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(stem));
        if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);
        _planMode = false;
        int written = 0;
        for (int i = 0; i < n; i++)
        {
            var v = Turntable.Frame(center, look0, 1000, width, i, n, degrees);
            cam.UpDirection = new Vector3D(0, 0, 1);
            cam.LookDirection = new Vector3D(v.Look.X, v.Look.Y, v.Look.Z) * 1000;
            cam.Position = new Point3D(v.Position.X, v.Position.Y, v.Position.Z);
            cam.Width = v.Width;
            KeepCameraInRange();
            // RenderBitmap lags one frame: let the new camera render, flush once, then capture.
            await Dispatcher.InvokeAsync(() => View.InvalidateRender(), System.Windows.Threading.DispatcherPriority.Render);
            await Task.Delay(60);
            View.RenderBitmap();
            await Task.Delay(60);
            if (View.RenderBitmap() is { } vb)
            {
                SavePng(vb, $"{stem}_frame{i + 1:0000}.png");
                written++;
            }
            if ((i + 1) % 20 == 0 || i + 1 == n) ViewModels.MainViewModel.Log($"TURNTABLE: frame {i + 1}/{n} ({sw.Elapsed.TotalSeconds:F1} s)");
        }
        ViewModels.MainViewModel.Log($"TURNTABLE: {written} frames written as {stem}_frameNNNN.png in {sw.Elapsed.TotalSeconds:F1} s");
        _turntableFreeze = false;
    }
}
