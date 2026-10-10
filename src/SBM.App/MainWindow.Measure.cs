using System.Numerics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using SBM.App.Editors;
using SBM.App.Viewport;
using SBM.Core.Alignments;
using SBM.Core.Geometry;
using SBM.Core.Units;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Line = System.Windows.Shapes.Line;
using MeshGeometry3D = HelixToolkit.SharpDX.MeshGeometry3D;
using OrthographicCamera = HelixToolkit.Wpf.SharpDX.OrthographicCamera;
using Path = System.Windows.Shapes.Path;
using Point = System.Windows.Point;

namespace SBM.App;

/// <summary>
/// Interactive MEASURE (distance, angle), station / offset / elevation QUERY and OBJECT SNAPS in the 3D and plan views.
/// Snapping uses <see cref="SnapIndex"/> (a BVH over the scene's triangles, line work and fastener instances, built lazily
/// after each scene rebuild); overlay graphics (snap marker, rubber band, results) are WPF shapes on <c>MeasureOverlay</c>
/// reprojected whenever the camera moves. Results are kept in world coordinates, so they survive regeneration.
/// </summary>
public partial class MainWindow
{
    private enum MeasureTool { None, Distance, Angle, Query }

    private sealed class MeasureRecord
    {
        public required List<Vec3> Pts { get; init; }
        public bool IsAngle { get; init; }
    }

    private MeasureTool _msTool = MeasureTool.None;
    private readonly List<Vec3> _msPending = new();       // points picked for the measurement in progress
    private readonly List<MeasureRecord> _msRecords = new();
    private readonly List<(Vec3 p, string note)> _msPins = new(); // Station/offset query pins
    private SnapHit? _msHover;
    private Vec3? _msHoverWorld;
    private SnapStats? _msStats;
    private SnapModes _msModes = SnapModes.All;
    private bool _msSnapOn = true, _msXRay;
    private string? _msAlignment;       // null = nearest alignment
    private SnapIndex? _snapIndex;
    private Task? _snapBuild;
    private bool _snapDirty = true;
    private int _snapVersion;
    private Point _msLastMouse;
    private System.Windows.Threading.DispatcherTimer? _msTimer;
    private bool _msUpdatingUi;

    private bool MeasureActive => _msTool != MeasureTool.None;

    private void InitMeasure()
    {
        View.PreviewMouseMove += (_, e) => OnMeasureMouseMove(e.GetPosition(View));
        View.MouseLeave += (_, _) => { _msHover = null; _msHoverWorld = null; RedrawMeasureOverlay(); };
        View.CameraChanged += (_, _) => { if (MeasureOverlay.Children.Count > 0) RedrawMeasureOverlay(); };
        View.SizeChanged += (_, _) => RedrawMeasureOverlay();
        PreviewKeyDown += OnMeasureKey;
        InputBindings.Add(new KeyBinding(new RelayKeyCommand(() => ToggleMeasureTool(MeasureTool.Distance)), Key.M, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayKeyCommand(() => ToggleMeasureTool(MeasureTool.Query)), Key.I, ModifierKeys.Control));
        _msTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(12) };
        _msTimer.Tick += (_, _) => { _msTimer!.Stop(); UpdateHover(_msLastMouse); };
    }

    private sealed class RelayKeyCommand(Action a) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => a();
    }

    // ================================================================== tool state

    private void ToggleMeasureTool(MeasureTool t)
    {
        if (_msTool == t) { EndMeasureTool(); return; }
        StartMeasureTool(t);
    }

    private void StartMeasureTool(MeasureTool t)
    {
        if (ModeSheets.IsChecked == true) ModeModel.IsChecked = true;
        _msTool = t;
        _msPending.Clear();
        _msHover = null;
        RefreshMeasureAlignments();
        MeasurePanel.Visibility = Visibility.Visible;
        MeasureTitle.Text = t switch { MeasureTool.Distance => "MEASURE DISTANCE", MeasureTool.Angle => "MEASURE ANGLE", _ => "STATION / OFFSET / ELEVATION" };
        View.Cursor = Cursors.Cross;
        EnsureSnapIndex();
        UpdateMeasureHint();
        UpdateMeasurePanel();
        RedrawMeasureOverlay();
        StatusText(t switch
        {
            MeasureTool.Distance => "Measure: click the first point (snaps on - F3 toggles, Esc cancels).",
            MeasureTool.Angle => "Measure angle: click the first point, then the vertex, then the third point.",
            _ => "Station / offset: hover for a live readout, click to pin a point.",
        });
    }

    private void EndMeasureTool()
    {
        _msTool = MeasureTool.None;
        _msPending.Clear();
        _msHover = null;
        _msHoverWorld = null;
        View.Cursor = null;
        MeasurePanel.Visibility = Visibility.Collapsed;
        RedrawMeasureOverlay();
    }

    private void UpdateMeasureHint()
    {
        MeasureHint.Text = _msTool switch
        {
            MeasureTool.Distance => _msPending.Count == 0 ? "Click point A.  Right-click or Esc cancels; Esc again exits." : "Click point B (Esc cancels).",
            MeasureTool.Angle => _msPending.Count switch { 0 => "Click the first point.", 1 => "Click the vertex.", _ => "Click the third point." },
            _ => "Hover = live readout. Click pins a point (up to 6).",
        };
    }

    private void OnMeasureKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F3)
        {
            SetSnapsOn(!_msSnapOn);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && MeasureActive && Keyboard.FocusedElement is not TextBox)
        {
            if (_msPending.Count > 0) { _msPending.Clear(); UpdateMeasureHint(); UpdateMeasurePanel(); RedrawMeasureOverlay(); }
            else EndMeasureTool();
            e.Handled = true;
        }
    }

    /// <summary>Right-click while measuring: cancels the measurement in progress (returns false when there is none).</summary>
    private bool MeasureCancelInProgress()
    {
        if (!MeasureActive || _msPending.Count == 0) return false;
        _msPending.Clear();
        UpdateMeasureHint(); UpdateMeasurePanel(); RedrawMeasureOverlay();
        return true;
    }

    private void SetSnapsOn(bool on)
    {
        _msSnapOn = on;
        SnapMaster.IsChecked = on;
        StatusText(on ? "Object snaps ON (F3)" : "Object snaps OFF (F3) - surface points only");
        if (MeasureActive) UpdateHover(_msLastMouse);
    }

    // ================================================================== ribbon + panel handlers

    private void OnMeasureDistance(object sender, RoutedEventArgs e) => ToggleMeasureTool(MeasureTool.Distance);
    private void OnMeasureAngle(object sender, RoutedEventArgs e) => ToggleMeasureTool(MeasureTool.Angle);
    private void OnQueryPoint(object sender, RoutedEventArgs e) => ToggleMeasureTool(MeasureTool.Query);
    private void OnMeasureClose(object sender, RoutedEventArgs e) => EndMeasureTool();

    private void OnMeasureClear(object sender, RoutedEventArgs e)
    {
        _msRecords.Clear();
        _msPins.Clear();
        _msPending.Clear();
        UpdateMeasureHint(); UpdateMeasurePanel(); RedrawMeasureOverlay();
        StatusText("Measurements cleared.");
    }

    private void OnMeasureCopy(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(BuildReadout(false)); StatusText("Measurement results copied to the clipboard."); }
        catch (Exception ex) { StatusText("Could not copy: " + ex.Message); }
    }

    private void OnMeasureAlignmentChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_msUpdatingUi) return;
        _msAlignment = MeasureAlignment.SelectedIndex <= 0 ? null : MeasureAlignment.SelectedItem as string;
        UpdateMeasurePanel();
    }

    private void RefreshMeasureAlignments()
    {
        _msUpdatingUi = true;
        var names = _vm.Project.Alignments.Select(a => a.Name).ToList();
        var items = new List<string> { names.Count > 1 ? "Auto (nearest alignment)" : "Auto" };
        items.AddRange(names);
        MeasureAlignment.ItemsSource = items;
        MeasureAlignment.SelectedIndex = _msAlignment != null && names.Contains(_msAlignment) ? names.IndexOf(_msAlignment) + 1 : 0;
        if (MeasureAlignment.SelectedIndex == 0) _msAlignment = null;
        _msUpdatingUi = false;
    }

    private void OnSnapMaster(object sender, RoutedEventArgs e) => SetSnapsOn(SnapMaster.IsChecked == true);

    private void OnSnapMode(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton tb || tb.Tag is not string name) return;
        if (name == "XRay") { _msXRay = tb.IsChecked == true; return; }
        var m = Enum.Parse<SnapModes>(name);
        _msModes = tb.IsChecked == true ? _msModes | m : _msModes & ~m;
        if (MeasureActive) UpdateHover(_msLastMouse);
    }

    private void OnSnapSettings(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = sender as UIElement, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        MenuItem Check(string header, bool on, Action<bool> set, string tip = "")
        {
            var mi = new MenuItem { Header = header, IsCheckable = true, IsChecked = on, StaysOpenOnClick = true, ToolTip = tip.Length > 0 ? tip : null };
            mi.Click += (_, _) => { set(mi.IsChecked); SyncSnapBar(); };
            return mi;
        }
        menu.Items.Add(Check("Object snaps on (F3)", _msSnapOn, v => SetSnapsOn(v)));
        menu.Items.Add(new Separator());
        foreach (var (m, name) in SnapModeNames)
            menu.Items.Add(Check(name, (_msModes & m) != 0, v => _msModes = v ? _msModes | m : _msModes & ~m));
        menu.Items.Add(new Separator());
        menu.Items.Add(Check("Snap through solids (X-ray)", _msXRay, v => _msXRay = v, "Also snap to geometry hidden behind opaque solids"));
        menu.IsOpen = true;
    }

    private static readonly (SnapModes, string)[] SnapModeNames =
    {
        (SnapModes.Endpoint, "Endpoint (vertices, line ends, bar bends)"), (SnapModes.Midpoint, "Midpoint (segments, mesh edges)"),
        (SnapModes.Center, "Center (bolts, studs, columns)"), (SnapModes.Intersection, "Intersection (apparent, in the view)"),
        (SnapModes.Perpendicular, "Perpendicular (from the previous point)"), (SnapModes.Nearest, "Nearest (on lines and bars)"),
        (SnapModes.Domain, "Bridge points (bearings, cross-frame work points, splices, web lines)"),
    };

    private void SyncSnapBar()
    {
        SnapMaster.IsChecked = _msSnapOn;
        foreach (var tb in SnapBar.Children.OfType<ToggleButton>())
        {
            if (tb.Tag is not string n) continue;
            tb.IsChecked = n == "XRay" ? _msXRay : (_msModes & Enum.Parse<SnapModes>(n)) != 0;
        }
    }

    // ================================================================== camera ray / projection (orthographic)

    private readonly record struct ViewRay(Vector3 Origin, Vector3 Dir, Vector3 Right, Vector3 Up, float Pixel);

    private ViewRay? RayAt(Point screen)
    {
        if (View.Camera is not OrthographicCamera cam || View.ActualWidth < 2 || View.ActualHeight < 2) return null;
        var look = cam.LookDirection; look.Normalize();
        var up0 = cam.UpDirection;
        var right = Vector3D.CrossProduct(look, up0); right.Normalize();
        var up = Vector3D.CrossProduct(right, look); up.Normalize();
        double pix = cam.Width / View.ActualWidth;
        var pos = cam.Position;
        var o = pos + right * ((screen.X - View.ActualWidth / 2) * pix) + up * ((View.ActualHeight / 2 - screen.Y) * pix);
        return new ViewRay(new Vector3((float)o.X, (float)o.Y, (float)o.Z), new Vector3((float)look.X, (float)look.Y, (float)look.Z),
            new Vector3((float)right.X, (float)right.Y, (float)right.Z), new Vector3((float)up.X, (float)up.Y, (float)up.Z), (float)pix);
    }

    /// <summary>Screen position of a world point.</summary>
    private Point? ToScreen(Vec3 world)
    {
        if (_scene == null || View.Camera is not OrthographicCamera cam || View.ActualWidth < 2) return null;
        var look = cam.LookDirection; look.Normalize();
        var right = Vector3D.CrossProduct(look, cam.UpDirection); right.Normalize();
        var up = Vector3D.CrossProduct(right, look); up.Normalize();
        double pix = cam.Width / View.ActualWidth;
        var d = new Vector3D(world.X - _scene.Origin.X, world.Y - _scene.Origin.Y, world.Z - _scene.Origin.Z) - (Vector3D)cam.Position;
        return new Point(View.ActualWidth / 2 + Vector3D.DotProduct(d, right) / pix, View.ActualHeight / 2 - Vector3D.DotProduct(d, up) / pix);
    }

    private Vec3 ToWorld(Vector3 local) => new(local.X + _scene!.Origin.X, local.Y + _scene.Origin.Y, local.Z + _scene.Origin.Z);

    // ================================================================== snap index (lazy)

    /// <summary>The scene changed (regeneration, references, section...): the index is rebuilt on next use.</summary>
    private void InvalidateSnapIndex()
    {
        _snapDirty = true;
        _snapIndex = null; // never query an index of geometry that is no longer in the scene
        _snapVersion++;
        // Debounced: the scene and the reference models are rebuilt in quick succession at startup / after a regeneration, and
        // one index build (seconds on a big model) must serve them all. The first scene is also indexed before any tool is used.
        if (!MeasureActive && _snapPrebuilt) return;
        _snapDebounce ??= new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(600) };
        _snapDebounce.Tick -= OnSnapDebounce;
        _snapDebounce.Tick += OnSnapDebounce;
        _snapDebounce.Stop();
        _snapDebounce.Start();
    }

    private System.Windows.Threading.DispatcherTimer? _snapDebounce;
    private bool _snapPrebuilt;

    private void OnSnapDebounce(object? sender, EventArgs e)
    {
        _snapDebounce?.Stop();
        _snapPrebuilt = true;
        EnsureSnapIndex();
    }

    private void EnsureSnapIndex()
    {
        if (_scene == null || !_snapDirty) return;
        _snapDirty = false;
        int version = _snapVersion;
        var index = new SnapIndex(_scene.Origin) { ExtraPoints = ExtraSnapPoints, MeshIsNoise = t => t is RebarPick or PsStrandPick };
        var o = _scene.Origin;
        Vector3 L(Vec3 p) => new((float)(p.X - o.X), (float)(p.Y - o.Y), (float)(p.Z - o.Z));

        void AddModels(string layer, IEnumerable<Element3D> models, bool reference)
        {
            foreach (var m in models)
            {
                switch (m)
                {
                    case MeshGeometryModel3D mm when mm.Tag is List<Matrix4x4> inst && mm.Geometry is MeshGeometry3D ig:
                        SceneBuilder.InstanceTags.TryGetValue(ig, out var itags);
                        index.Add(new SnapSource { Mesh = ig, Instances = inst, InstanceTags = itags, Model = mm, Layer = layer, Opaque = !mm.IsTransparent });
                        break;
                    case MeshGeometryModel3D mm when FullMeshOf(mm, reference) is { } full:
                        SceneBuilder.Picks.TryGetValue(full, out var table);
                        index.Add(new SnapSource
                        {
                            Mesh = full, Table = table, Model = mm, Layer = layer, Opaque = !mm.IsTransparent,
                            Label = reference ? layer.StartsWith("REF-") ? "Reference " + layer[4..].ToLowerInvariant() : layer : "",
                        });
                        break;
                    case LineGeometryModel3D lm when _fullLines.TryGetValue(lm, out var fl):
                        index.Add(new SnapLines { Geometry = fl, Model = lm, Layer = layer });
                        break;
                    case LineGeometryModel3D lm2 when reference && !layer.EndsWith("-CONTOURS", StringComparison.Ordinal) && lm2.Geometry is LineGeometry3D rl:
                        index.Add(new SnapLines { Geometry = rl, Model = lm2, Layer = layer, Label = "Reference line" });
                        break;
                }
            }
        }
        foreach (var (name, list) in _layerModels) AddModels(name, list, false);
        foreach (var (name, list) in _refModels) AddModels(name, list, true);

        // Domain points and lines from the bridge framing.
        var pts = new List<SnapPoint>();
        foreach (var (bridge, f) in _vm.Framings)
        {
            foreach (var s in f.Supports.Concat(f.AuxSupports))
                foreach (var b in s.Bearings.OfType<SBM.Core.Framing.BearingResult>())
                {
                    pts.Add(new SnapPoint(L(b.TopOfSteel), $"Bearing G{b.Girder + 1} at {s.Name} - top of steel"));
                    pts.Add(new SnapPoint(L(new Vec3(b.TopOfSteel.X, b.TopOfSteel.Y, b.BottomOfSteelElevation)), $"Bearing G{b.Girder + 1} at {s.Name} - bottom of steel", true));
                }
            foreach (var cf in f.CrossFrames)
            {
                pts.Add(new SnapPoint(L(cf.Left), $"{cf.Mark} work point (G{cf.Bay + 1})"));
                pts.Add(new SnapPoint(L(cf.Right), $"{cf.Mark} work point (G{cf.RightGirder + 1})"));
            }
            foreach (var sp in f.Splices)
                pts.Add(new SnapPoint(L(sp.Location), $"Field splice FS{sp.Number} on G{sp.Girder + 1}"));
            foreach (var g in f.Girders)
            {
                Vector3[] Pairs(IReadOnlyList<Vec3> poly)
                {
                    var a = new Vector3[Math.Max(0, poly.Count - 1) * 2];
                    for (int i = 0; i + 1 < poly.Count; i++) { a[2 * i] = L(poly[i]); a[2 * i + 1] = L(poly[i + 1]); }
                    return a;
                }
                index.Add(new SnapLines { Pairs = Pairs(g.WebLine), Layer = "Girder web line", Label = $"{g.Name} web line (top of web)", Only3D = true, Domain = true });
                index.Add(new SnapLines { Pairs = Pairs(g.TopFlangeLeft), Layer = "Girder top flange", Label = $"{g.Name} top flange edge", Only3D = true, Domain = true });
                index.Add(new SnapLines { Pairs = Pairs(g.TopFlangeRight), Layer = "Girder top flange", Label = $"{g.Name} top flange edge", Only3D = true, Domain = true });
            }
            if (f.Deck is { } deck && deck.Outline.Count > 1)
            {
                var a = new List<Vector3>();
                for (int i = 0; i < deck.Outline.Count; i++) { a.Add(L(deck.Outline[i])); a.Add(L(deck.Outline[(i + 1) % deck.Outline.Count])); }
                index.Add(new SnapLines { Pairs = a.ToArray(), Layer = "Deck outline", Label = "Deck edge (top of deck)", Only3D = true, Domain = true });
            }
        }
        foreach (var sm in _vm.SteelModels.Values)
            foreach (var seat in sm.BearingSeats.Where(s => s.Location != null))
                pts.Add(new SnapPoint(L(seat.Location!.Value.WithZ(seat.SeatElevation)), $"Bearing seat G{seat.Girder + 1} at {seat.Support} (top of seat)"));
        index.SetPoints(pts);
        _snapIndex = null;
        _snapBuild = Task.Run(() => index.Build()).ContinueWith(t =>
        {
            if (t.IsFaulted) { ViewModels.MainViewModel.Log("snap index failed: " + t.Exception?.GetBaseException()); return; }
            Dispatcher.BeginInvoke(() =>
            {
                if (version != _snapVersion) return; // superseded by a newer scene
                _snapIndex = index;
                ViewModels.MainViewModel.Log($"SNAP INDEX: {index.LeafCount:N0} leaves, {index.TriangleCount:N0} triangles, built in {index.BuildMs:F0} ms");
                if (MeasureActive) UpdateHover(_msLastMouse);
            });
        }, TaskScheduler.Default);
    }

    /// <summary>The unclipped mesh of a solid model (reference surfaces are not registered in <c>_fullMeshes</c> after a regeneration).</summary>
    private MeshGeometry3D? FullMeshOf(MeshGeometryModel3D mm, bool reference) =>
        _fullMeshes.TryGetValue(mm, out var full) ? full : reference ? mm.Geometry as MeshGeometry3D : null;

    /// <summary>Waits for the index (snapshot runs; blocks the UI thread).</summary>
    private SnapIndex? WaitForSnapIndex()
    {
        var until = DateTime.UtcNow.AddSeconds(90);
        while (_snapIndex == null && DateTime.UtcNow < until)
        {
            _snapDebounce?.Stop();
            EnsureSnapIndex();
            try { _snapBuild?.Wait(60_000); } catch { }
            // The completion posts to the dispatcher: pump it (also lets a newer scene invalidate this index).
            for (int i = 0; i < 20 && _snapIndex == null; i++)
                Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
            if (_snapIndex == null) System.Threading.Thread.Sleep(20);
        }
        return _snapIndex;
    }

    private IEnumerable<(Vec3 p, SnapKind kind, string label)>? ExtraSnapPoints(object tag)
    {
        switch (tag)
        {
            case RebarPick r:
                return r.Bar.Points.Select(p => (p, SnapKind.Endpoint, $"Bar {r.Mark.Mark} vertex"));
            case PsStrandPick ps:
                return ps.Strand.Points.Select(p => (p, SnapKind.Endpoint, "Strand vertex"));
            case SubPick sp when sp.Solid.Kind is SBM.Core.Substructure.SubElementKind.Column or SBM.Core.Substructure.SubElementKind.DrilledShaft
                or SBM.Core.Substructure.SubElementKind.Pile && sp.Solid.IsConcrete:
            {
                var b = sp.Solid.Mesh.Bounds();
                double cx = (b.Min.X + b.Max.X) / 2, cy = (b.Min.Y + b.Max.Y) / 2;
                return new[]
                {
                    (new Vec3(cx, cy, b.Max.Z), SnapKind.Center, $"{sp.Solid.Name} - center, top"),
                    (new Vec3(cx, cy, b.Min.Z), SnapKind.Center, $"{sp.Solid.Name} - center, bottom"),
                };
            }
        }
        return null;
    }

    private static string Describe(object? tag) => tag switch
    {
        SBM.Core.Steel.SteelPart p => $"{p.Mark} {p.Description}",
        SBM.Core.Steel.BoltInstance b => $"Bolt ({b.Assembly})",
        SBM.Core.Steel.StudInstance => "Shear stud",
        RebarPick r => $"Bar {r.Mark.Mark}" + (r.Mark.Unit.Length > 0 ? $" ({r.Mark.Unit})" : ""),
        SubPick s => s.Solid.Name,
        NamedPick n => n.Name,
        PsBeamPick b => $"Beam {b.Beam.Mark}",
        PsStrandPick => "Strand",
        PsPadPick p => $"Pad {p.Pad.Mark}",
        _ => "",
    };

    private static string KindName(SnapKind k) => k switch
    {
        SnapKind.Endpoint => "Endpoint", SnapKind.Midpoint => "Midpoint", SnapKind.Center => "Center", SnapKind.Intersection => "Intersection",
        SnapKind.Perpendicular => "Perpendicular", SnapKind.Nearest => "Nearest", SnapKind.Domain => "Bridge point", SnapKind.Surface => "On surface", _ => "Point",
    };

    private string HitText(SnapHit h)
    {
        string what = Describe(h.Tag);
        if (what.Length == 0) what = h.Text;
        if (what.Length == 0) what = h.Layer;
        return what.Length > 0 && h.Kind != SnapKind.Domain ? $"{KindName(h.Kind)} - {what}" : h.Kind == SnapKind.Domain ? $"{what}" : KindName(h.Kind);
    }

    // ================================================================== query

    private SnapEnvironment MakeEnv() => new()
    {
        Plan = _planMode,
        XRay = _msXRay,
        ModelVisible = m => m == null || m.Visibility == Visibility.Visible,
        IsHidden = t => _hidden.Count > 0 && _hidden.Contains(t),
        Kept = Kept,
    };

    /// <summary>The snapped / picked point under a screen position, or null (outside the model with no ground plane hit).</summary>
    private SnapHit? SnapAt(Point screen, out SnapStats? stats, SnapModes? modes = null)
    {
        stats = null;
        if (_scene == null || RayAt(screen) is not { } ray) return null;
        SnapHit? hit = null;
        if (_snapIndex != null)
        {
            var req = new SnapRequest
            {
                Origin = ray.Origin, Dir = ray.Dir, ViewRight = ray.Right, ViewUp = ray.Up, PixelSize = ray.Pixel,
                Modes = modes ?? (_msSnapOn ? _msModes : SnapModes.None),
                Previous = _msPending.Count > 0 ? ToLocal(_msPending[^1]) : null,
            };
            hit = _snapIndex.Find(req, MakeEnv(), out var st);
            stats = st;
        }
        if (hit != null) return hit;
        // Nothing under the pixel: a free point on the horizontal plane through the view target (3D) or the ground / grade (plan).
        if (View.Camera is not OrthographicCamera cam) return null;
        float zPlane = (float)(cam.Position + cam.LookDirection).Z;
        if (_planMode)
        {
            var wp = ToWorld(ray.Origin + ray.Dir * ((zPlane - ray.Origin.Z) / (Math.Abs(ray.Dir.Z) < 1e-6f ? 1e-6f : ray.Dir.Z)));
            double? z = _vm.GroundElevation(wp.XY);
            string zNote = z != null ? "Z from ground" : "";
            if (z == null && AlignmentFor(wp) is { } al)
            {
                var so = al.Inverse(wp.XY);
                if (so.OnAlignment) { z = al.ElevationAt(so.Station, so.Offset); zNote = "Z from finished grade"; }
            }
            var p = ray.Origin + ray.Dir * ((zPlane - ray.Origin.Z) / (Math.Abs(ray.Dir.Z) < 1e-6f ? 1e-6f : ray.Dir.Z));
            if (z != null) p.Z = (float)(z.Value - _scene.Origin.Z);
            return new SnapHit { P = p, Kind = SnapKind.Free, Text = zNote.Length > 0 ? zNote : "Z = view plane" };
        }
        if (Math.Abs(ray.Dir.Z) < 0.02f) return null;
        float t = (zPlane - ray.Origin.Z) / ray.Dir.Z;
        return new SnapHit { P = ray.Origin + ray.Dir * t, Kind = SnapKind.Free, Text = "on view plane" };
    }

    private Vector3 ToLocal(Vec3 w) => new((float)(w.X - _scene!.Origin.X), (float)(w.Y - _scene.Origin.Y), (float)(w.Z - _scene.Origin.Z));

    private Alignment? AlignmentFor(Vec3 p)
    {
        if (_msAlignment != null && _vm.Project.FindAlignment(_msAlignment) is { } named) return named;
        return PointQuery.QueryNearest(_vm.Project.Alignments, p) is { } r ? _vm.Project.FindAlignment(r.Alignment) : _vm.Project.Alignments.FirstOrDefault();
    }

    private PointReadout? ReadoutAt(Vec3 p)
    {
        if (_msAlignment != null && _vm.Project.FindAlignment(_msAlignment) is { } named) return PointQuery.Query(named, p);
        return PointQuery.QueryNearest(_vm.Project.Alignments, p);
    }

    // ================================================================== mouse

    private void OnMeasureMouseMove(Point pt)
    {
        if (!MeasureActive) return;
        _msLastMouse = pt;
        if (_zoomWindowArmed) return;
        if (_msTimer is { IsEnabled: false }) _msTimer.Start();
    }

    private void UpdateHover(Point pt)
    {
        if (!MeasureActive || _scene == null) return;
        if (_snapIndex == null) { EnsureSnapIndex(); StatusText(_snapBuild is { IsCompleted: false } ? "Building the snap index..." : _vm.StatusText); }
        _msHover = SnapAt(pt, out _msStats);
        _msHoverWorld = _msHover != null ? ToWorld(_msHover.P) : null;
        if (_msHoverWorld is { } w) _vm.CursorText = CursorLine(w, _msHover);
        UpdateMeasurePanel();
        RedrawMeasureOverlay();
    }

    private string CursorLine(Vec3 w, SnapHit? h)
    {
        var u = _vm.Units;
        string s = $"E {ValueFormat.Format(u, ValueKind.Coordinate, w.X)}   N {ValueFormat.Format(u, ValueKind.Coordinate, w.Y)}   Z {u.FormatLength(w.Z, LengthKind.Coordinate)}";
        if (ReadoutAt(w) is { } r)
            s += $"   |   {r.Alignment}: STA {u.FormatStation(r.Station)}  OFF {OffsetText(u, r.Offset)}";
        if (h != null) s += $"   |   {HitText(h)}";
        return s;
    }

    private void MeasureClick(Point pt)
    {
        _msLastMouse = pt;
        var hit = SnapAt(pt, out _msStats);
        if (hit == null) return;
        var w = ToWorld(hit.P);
        switch (_msTool)
        {
            case MeasureTool.Query:
                if (_msPins.Count >= 6) _msPins.RemoveAt(0);
                _msPins.Add((w, HitText(hit)));
                break;
            case MeasureTool.Distance:
                _msPending.Add(w);
                if (_msPending.Count == 2)
                {
                    _msRecords.Add(new MeasureRecord { Pts = new List<Vec3>(_msPending) });
                    ViewModels.MainViewModel.Log("MEASURE: " + FormatRecord(_msRecords[^1], _msRecords.Count).Replace("\n", " | "));
                    var last = _msPending[1];
                    _msPending.Clear();
                    if (MeasureChain.IsChecked == true) _msPending.Add(last);
                }
                break;
            case MeasureTool.Angle:
                _msPending.Add(w);
                if (_msPending.Count == 3)
                {
                    _msRecords.Add(new MeasureRecord { Pts = new List<Vec3>(_msPending), IsAngle = true });
                    ViewModels.MainViewModel.Log("MEASURE: " + FormatRecord(_msRecords[^1], _msRecords.Count).Replace("\n", " | "));
                    _msPending.Clear();
                }
                break;
        }
        UpdateMeasureHint();
        UpdateHover(pt);
    }

    // ================================================================== readout text

    private static string OffsetText(UnitSystem u, double off) => $"{u.FormatLength(Math.Abs(off))} {(off < 0 ? "LT" : "RT")}";

    private string Coord(Vec3 p)
    {
        var u = _vm.Units;
        return $"E {ValueFormat.Format(u, ValueKind.Coordinate, p.X)}  N {ValueFormat.Format(u, ValueKind.Coordinate, p.Y)}  Z {u.FormatLength(p.Z, LengthKind.Coordinate)}";
    }

    private string StationLine(Vec3 p)
    {
        var u = _vm.Units;
        if (ReadoutAt(p) is not { } r) return "  (no alignment)";
        return $"  {r.Alignment}: STA {u.FormatStation(r.Station)}  OFF {OffsetText(u, r.Offset)}{(r.OnAlignment ? "" : "  (beyond the ends)")}";
    }

    /// <summary>Readout of all grades at a point: station / offset, profile grade, finished grade, ΔZ and ground.</summary>
    private string QueryBlock(Vec3 p, string title)
    {
        var u = _vm.Units;
        var sb = new StringBuilder();
        sb.AppendLine(title);
        sb.AppendLine("  " + Coord(p));
        if (ReadoutAt(p) is { } r)
        {
            sb.AppendLine($"  Alignment  {r.Alignment}{(r.OnAlignment ? "" : "  (beyond the ends)")}");
            sb.AppendLine($"  Station    {u.FormatStation(r.Station)}");
            sb.AppendLine($"  Offset     {OffsetText(u, r.Offset)}   ({(r.Offset >= 0 ? "+" : "-")}{Math.Abs(r.Offset / u.LongUnit):F3} {u.LongUnitName})");
            sb.AppendLine($"  Profile grade       {u.FormatLength(r.ProfileElevation, LengthKind.Coordinate)}");
            sb.AppendLine($"  Finished grade      {u.FormatLength(r.FinishedGrade, LengthKind.Coordinate)}   (profile + cross slope)");
            sb.AppendLine($"  Z - finished grade  {Signed(u, r.DeltaToFinished)}");
        }
        else sb.AppendLine("  (no alignment in the project)");
        if (_vm.GroundElevation(p.XY) is { } g)
            sb.AppendLine($"  Ground              {u.FormatLength(g, LengthKind.Coordinate)}   (Z - ground {Signed(u, p.Z - g)})");
        return sb.ToString().TrimEnd();
    }

    private static string Signed(UnitSystem u, double meters) => (meters >= 0 ? "+" : "-") + u.FormatLength(Math.Abs(meters));

    private string FormatRecord(MeasureRecord rec, int number)
    {
        var u = _vm.Units;
        var sb = new StringBuilder();
        if (rec.IsAngle)
        {
            var (a, b, c) = (rec.Pts[0], rec.Pts[1], rec.Pts[2]);
            double ang = MeasureResult.AngleAt(a, b, c);
            sb.AppendLine($"#{number}  Angle at vertex");
            sb.AppendLine($"  Angle      {ang:F4} deg   ({UnitSystem.FormatAngleDms(ang * Math.PI / 180, false)})");
            sb.AppendLine($"  Leg 1      {u.FormatLength((a - b).Length)}   Leg 2  {u.FormatLength((c - b).Length)}");
            sb.AppendLine($"  A {Coord(a)}");
            sb.AppendLine($"  V {Coord(b)}");
            sb.Append($"  C {Coord(c)}");
            return sb.ToString();
        }
        var m = new MeasureResult(rec.Pts[0], rec.Pts[1]);
        sb.AppendLine($"#{number}  Distance");
        sb.AppendLine($"  3D distance    {u.FormatLength(m.Distance3D)}");
        sb.AppendLine($"  Plan distance  {u.FormatLength(m.PlanDistance)}");
        sb.AppendLine($"  dE {Signed(u, m.DX)}   dN {Signed(u, m.DY)}   dZ {Signed(u, m.DZ)}");
        sb.AppendLine(double.IsNaN(m.Slope) ? "  Slope          (plumb)" : $"  Slope          {UnitSystem.FormatSlope(m.Slope)}  ({m.ElevationAngleDegrees:F3} deg)");
        sb.AppendLine(double.IsNaN(m.PlanAngle) ? "  Plan bearing   -" : $"  Plan bearing   {UnitSystem.FormatBearing(m.PlanAngle)}   (azimuth {m.AzimuthDegrees:F4} deg)");
        sb.AppendLine("  A " + Coord(m.A));
        sb.AppendLine("   " + StationLine(m.A).TrimStart());
        sb.AppendLine("  B " + Coord(m.B));
        sb.Append("   " + StationLine(m.B).TrimStart());
        return sb.ToString();
    }

    private string BuildReadout(bool includeHover)
    {
        var sb = new StringBuilder();
        if (includeHover && _msHoverWorld is { } hw && _msHover != null)
            sb.AppendLine(QueryBlock(hw, $"CURSOR  [{HitText(_msHover)}]").Replace("\r", "")).AppendLine();
        for (int i = 0; i < _msPins.Count; i++)
            sb.AppendLine(QueryBlock(_msPins[i].p, $"PIN {i + 1}  [{_msPins[i].note}]")).AppendLine();
        if (_msPins.Count > 1 && !includeHover || _msPins.Count > 1)
        {
            var m = new MeasureResult(_msPins[^2].p, _msPins[^1].p);
            var u = _vm.Units;
            sb.AppendLine($"PIN {_msPins.Count - 1} to {_msPins.Count}: {u.FormatLength(m.Distance3D)} (plan {u.FormatLength(m.PlanDistance)}, dZ {Signed(u, m.DZ)})").AppendLine();
        }
        for (int i = 0; i < _msRecords.Count; i++)
            sb.AppendLine(FormatRecord(_msRecords[i], i + 1)).AppendLine();
        if (_msPending.Count > 0)
            sb.AppendLine("In progress: " + string.Join("  ", _msPending.Select((p, i) => $"{(char)('A' + i)}: STA/OFF" + StationLine(p).Trim()))).AppendLine();
        return sb.ToString().TrimEnd();
    }

    private void UpdateMeasurePanel()
    {
        if (!MeasureActive) return;
        string text = BuildReadout(true);
        if (_msStats != null && _msHover != null) text += $"\n\n[snap query {_msStats.Ms:F2} ms, {_msStats.Leaves} chunks, {_msStats.Triangles:N0} tris]";
        MeasureReadout.Text = text.Length > 0 ? text : (_snapBuild is { IsCompleted: false } ? "Building the snap index..." : "Move the cursor over the model.");
    }

    // ================================================================== overlay

    private static readonly Brush HaloBrush = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0));
    private static readonly Brush SnapBrush = new SolidColorBrush(Color.FromRgb(255, 225, 77));
    private static readonly Brush DomainBrush = new SolidColorBrush(Color.FromRgb(77, 210, 255));
    private static readonly Brush ResultBrush = new SolidColorBrush(Color.FromRgb(255, 154, 46));
    private static readonly Brush PinBrush = new SolidColorBrush(Color.FromRgb(120, 230, 140));

    private void RedrawMeasureOverlay()
    {
        var cv = MeasureOverlay;
        cv.Children.Clear();
        if (_scene == null || ModeSheets.IsChecked == true) return;

        void Seg(Point a, Point b, Brush color, double w = 1.8, bool dashed = false)
        {
            foreach (var (br, th) in new[] { (HaloBrush, w + 2.4), (color, w) })
            {
                var l = new Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = br, StrokeThickness = th, StrokeLineJoin = PenLineJoin.Round };
                if (dashed && br == color) l.StrokeDashArray = new DoubleCollection { 4, 3 };
                cv.Children.Add(l);
            }
        }
        void Label(string text, Point at, Brush fg, double dx = 8, double dy = -8)
        {
            var tb = new TextBlock { Text = text, Foreground = fg, FontFamily = new FontFamily("Consolas"), FontSize = 12, Margin = new Thickness(5, 1, 5, 1) };
            var b = new Border { Child = tb, Background = new SolidColorBrush(Color.FromArgb(215, 28, 28, 30)), CornerRadius = new CornerRadius(3), BorderBrush = fg, BorderThickness = new Thickness(1) };
            Canvas.SetLeft(b, at.X + dx);
            Canvas.SetTop(b, at.Y + dy - 10);
            cv.Children.Add(b);
        }
        void Dot(Point c, Brush color, double r = 3.5)
        {
            var e = new Ellipse { Width = 2 * r, Height = 2 * r, Fill = color, Stroke = Brushes.Black, StrokeThickness = 1 };
            Canvas.SetLeft(e, c.X - r); Canvas.SetTop(e, c.Y - r);
            cv.Children.Add(e);
        }

        var u = _vm.Units;
        // Finished results.
        for (int i = 0; i < _msRecords.Count; i++)
        {
            var rec = _msRecords[i];
            var sp = rec.Pts.Select(ToScreen).ToList();
            if (sp.Any(p => p == null)) continue;
            if (rec.IsAngle)
            {
                Seg(sp[0]!.Value, sp[1]!.Value, ResultBrush); Seg(sp[1]!.Value, sp[2]!.Value, ResultBrush);
                foreach (var p in sp) Dot(p!.Value, ResultBrush);
                Label($"{MeasureResult.AngleAt(rec.Pts[0], rec.Pts[1], rec.Pts[2]):F3}°", sp[1]!.Value, ResultBrush, 10, 14);
            }
            else
            {
                Seg(sp[0]!.Value, sp[1]!.Value, ResultBrush);
                Dot(sp[0]!.Value, ResultBrush); Dot(sp[1]!.Value, ResultBrush);
                var m = new MeasureResult(rec.Pts[0], rec.Pts[1]);
                var mid = new Point((sp[0]!.Value.X + sp[1]!.Value.X) / 2, (sp[0]!.Value.Y + sp[1]!.Value.Y) / 2);
                Label($"#{i + 1}  {u.FormatLength(m.Distance3D)}", mid, ResultBrush);
            }
        }
        for (int i = 0; i < _msPins.Count; i++)
            if (ToScreen(_msPins[i].p) is { } pp) { Marker(cv, SnapKind.Free, pp, PinBrush); Label($"P{i + 1}", pp, PinBrush, 10, -6); }

        // In-progress measurement: picked points and a rubber band to the cursor.
        var pend = _msPending.Select(ToScreen).ToList();
        for (int i = 0; i < pend.Count; i++)
        {
            if (pend[i] is not { } pc) continue;
            Dot(pc, ResultBrush);
            if (i > 0 && pend[i - 1] is { } pq) Seg(pq, pc, ResultBrush);
        }
        if (_msHover != null && ToScreen(ToWorld(_msHover.P)) is { } hs)
        {
            if (pend.Count > 0 && pend[^1] is { } last)
            {
                Seg(last, hs, ResultBrush, 1.4, true);
                var wl = _msPending[^1];
                var m = new MeasureResult(wl, ToWorld(_msHover.P));
                Label(u.FormatLength(m.Distance3D), new Point((last.X + hs.X) / 2, (last.Y + hs.Y) / 2), ResultBrush);
            }
            if (MeasureActive)
            {
                var color = _msHover.Kind == SnapKind.Domain ? DomainBrush : SnapBrush;
                Marker(cv, _msHover.Kind, hs, color);
                if (_msHover.Kind != SnapKind.Free || _planMode) Label(HitText(_msHover), hs, color, 14, 22);
            }
        }
    }

    private static void Marker(Canvas cv, SnapKind kind, Point c, Brush color)
    {
        string geo = kind switch
        {
            SnapKind.Endpoint => "M-7,-7 L7,-7 L7,7 L-7,7 Z",
            SnapKind.Midpoint => "M0,-8 L8,6 L-8,6 Z",
            SnapKind.Center => "M-7,0 A7,7 0 1 0 7,0 A7,7 0 1 0 -7,0 Z",
            SnapKind.Intersection => "M-7,-7 L7,7 M-7,7 L7,-7",
            SnapKind.Perpendicular => "M-7,7 L7,7 M-7,7 L-7,-7 M-7,0 L0,0 L0,7",
            SnapKind.Nearest => "M-7,-7 L7,-7 L-7,7 L7,7 Z",
            SnapKind.Domain => "M0,-9 L9,0 L0,9 L-9,0 Z M-3,0 L3,0 M0,-3 L0,3",
            SnapKind.Surface => "M-6,0 L6,0 M0,-6 L0,6",
            _ => "M-5,0 L5,0 M0,-5 L0,5",
        };
        var g = Geometry.Parse(geo);
        foreach (var (br, th) in new[] { (HaloBrush, 5.0), (color, 2.2) })
        {
            var p = new Path { Data = g, Stroke = br, StrokeThickness = th, StrokeLineJoin = PenLineJoin.Round, RenderTransform = new TranslateTransform(c.X, c.Y) };
            cv.Children.Add(p);
        }
    }

    // ================================================================== snapshot / benchmark / remote

    /// <summary>
    /// Snapshot options: <c>--measure fx1,fy1,fx2,fy2[,fx3,fy3]</c> (screen fractions; 2 points = distance, 3 = angle) clicks the
    /// points with snapping and logs MEASURE:/SNAP: lines; <c>--query fx,fy</c> logs QUERY:; <c>--snapbench N</c> logs query timings.
    /// </summary>
    private void RunMeasureSnapshot(string[] args)
    {
        int mi = Array.IndexOf(args, "--measure"), qi = Array.IndexOf(args, "--query"), bi = Array.IndexOf(args, "--snapbench");
        if (mi < 0 && qi < 0 && bi < 0) return;
        double[] Nums(int i) => args[i + 1].Split(',').Select(x => double.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        // Let the camera finish its zoom-extents animation first (queries must see the view that gets rendered).
        var delay = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
        delay.Tick += (_, _) =>
        {
            delay.Stop();
            try
            {
                StartMeasureTool(MeasureTool.Distance);
                var index = WaitForSnapIndex();
                var log = ViewModels.MainViewModel.Log;
                if (index == null) { log("SNAP: index not available"); return; }
                Point P(double fx, double fy) => new(fx * View.ActualWidth, fy * View.ActualHeight);
                if (mi >= 0 && mi + 1 < args.Length)
                {
                    var v = Nums(mi);
                    StartMeasureTool(v.Length >= 6 ? MeasureTool.Angle : MeasureTool.Distance);
                    for (int k = 0; k + 1 < v.Length; k += 2)
                    {
                        var pt = P(v[k], v[k + 1]);
                        RayCheck(pt);
                        var h = SnapAt(pt, out var st);
                        log($"SNAP: ({v[k]:F2},{v[k + 1]:F2}) -> {(h == null ? "none" : $"{h.Kind} '{HitText(h)}' px={h.ScreenDistance:F1} E={ToWorld(h.P).X:F3} N={ToWorld(h.P).Y:F3} Z={ToWorld(h.P).Z:F3}")} in {st?.Ms:F2} ms ({st?.Leaves} chunks, {st?.Triangles} tris, {st?.Candidates} cands)");
                        MeasureClick(pt);
                    }
                }
                if (qi >= 0 && qi + 1 < args.Length)
                {
                    var v = Nums(qi);
                    StartMeasureTool(MeasureTool.Query);
                    var pt = P(v[0], v[1]);
                    MeasureClick(pt);
                    log("QUERY: " + (_msPins.Count > 0 ? QueryBlock(_msPins[^1].p, "pin " + _msPins[^1].note).Replace("\n", " | ") : "no point"));
                }
                if (bi >= 0 && bi + 1 < args.Length) SnapBenchmark(int.Parse(args[bi + 1]), index);
                UpdateMeasurePanel();
                RedrawMeasureOverlay();
            }
            catch (Exception ex) { ViewModels.MainViewModel.Log("MEASURE snapshot failed: " + ex); }
        };
        delay.Start();
    }

    /// <summary>Logs how far the own orthographic ray / projection math is from Helix's unprojection (should be ~0).</summary>
    private void RayCheck(Point pt)
    {
        if (RayAt(pt) is not { } r || View.Camera is not OrthographicCamera cam) return;
        var plane = cam.Position + cam.LookDirection;
        var hx = View.UnProjectOnPlane(pt, plane, cam.LookDirection);
        if (hx is not { } h) { ViewModels.MainViewModel.Log("RAYCHECK: Helix unprojection failed"); return; }
        // Intersect my ray with the same plane and compare, then project back to the screen.
        var look = new Vector3((float)cam.LookDirection.X, (float)cam.LookDirection.Y, (float)cam.LookDirection.Z);
        look = Vector3.Normalize(look);
        float t = Vector3.Dot(new Vector3((float)plane.X, (float)plane.Y, (float)plane.Z) - r.Origin, look);
        var mine = r.Origin + look * t;
        double err = Math.Sqrt((mine.X - h.X) * (mine.X - h.X) + (mine.Y - h.Y) * (mine.Y - h.Y) + (mine.Z - h.Z) * (mine.Z - h.Z));
        var back = ToScreen(ToWorld(mine));
        ViewModels.MainViewModel.Log($"RAYCHECK: ray vs Helix unproject {err:E2} m; screen round trip ({back?.X - pt.X:F2}, {back?.Y - pt.Y:F2}) px; pixel {r.Pixel:F4} m; view {View.ActualWidth:F0}x{View.ActualHeight:F0}");
    }

    private void SnapBenchmark(int n, SnapIndex index)
    {
        var log = ViewModels.MainViewModel.Log;
        var rnd = new Random(7);
        var times = new List<double>();
        int hits = 0;
        long cands = 0, leaves = 0, tris = 0;
        var kinds = new Dictionary<SnapKind, int>();
        int trunc = 0; double travSum = 0, occSum = 0, extraSum = 0; var slow = new List<string>();
        int terrainHits = 0;
        bool prevSnap = _msSnapOn;
        _msSnapOn = true;
        for (int i = 0; i < n + 5; i++)
        {
            var pt = new Point((0.15 + 0.7 * rnd.NextDouble()) * View.ActualWidth, (0.15 + 0.7 * rnd.NextDouble()) * View.ActualHeight);
            var h = SnapAt(pt, out var st);
            if (i < 5 || st == null) continue; // warm-up
            times.Add(st.Ms); if (st.Truncated) trunc++; travSum += st.TravMs; occSum += st.OccMs; extraSum += st.ExtraMs;
            if (st.Ms > 8) slow.Add($"{st.Ms:F0}ms(trav {st.TravMs:F0}, occ {st.OccMs:F0}/{st.OcclusionTests}, extra {st.ExtraMs:F0}, {st.Leaves} chunks, {st.Triangles} tris)");
            leaves += st.Leaves; tris += st.Triangles; cands += st.Candidates;
            if (h != null && h.Kind != SnapKind.Free) { hits++; kinds[h.Kind] = kinds.GetValueOrDefault(h.Kind) + 1; }
            if (h != null && h.Layer.StartsWith("REF-")) terrainHits++;
        }
        _msSnapOn = prevSnap;
        if (times.Count == 0) { log("SNAPBENCH: no queries"); return; }
        times.Sort();
        log($"SNAPBENCH split (mean ms): traverse {travSum / times.Count:F2}, occlusion {occSum / times.Count:F2}, extras {extraSum / times.Count:F2}; {trunc} truncated; GCs {GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}; slow: {string.Join("; ", slow.Take(12))}");
        log($"SNAPBENCH: {times.Count} queries, mean {times.Average():F2} ms, median {times[times.Count / 2]:F2} ms, p95 {times[(int)(times.Count * 0.95)]:F2} ms, max {times[^1]:F2} ms; " +
            $"avg {leaves / times.Count} chunks, {tris / times.Count} triangles, {cands / times.Count} candidates; {hits} on-object ({string.Join(", ", kinds.Select(k => $"{k.Key} {k.Value}"))}), {terrainHits} on terrain; index {index.LeafCount:N0} chunks / {index.TriangleCount:N0} triangles built in {index.BuildMs:F0} ms");
    }

    /// <summary>
    /// Remote commands. <c>measure A B</c> and <c>station-offset P</c> take points as <c>E,N[,Z]</c> in the project's coordinate
    /// units (ft for US, m for SI; "m" / "ft" suffixes are accepted). Optional last argument <c>alignment=NAME</c>.
    /// </summary>
    private string MeasureRemote(string cmd, string[] a)
    {
        var u = _vm.Units;
        string? align = a.FirstOrDefault(x => x.StartsWith("alignment=", StringComparison.OrdinalIgnoreCase))?[10..];
        var pts = a.Where(x => !x.StartsWith("alignment=", StringComparison.OrdinalIgnoreCase)).Select(x => ParsePoint(u, x)).ToList();
        string? prev = _msAlignment;
        _msAlignment = align ?? _msAlignment;
        try
        {
            if (cmd.Equals("measure", StringComparison.OrdinalIgnoreCase))
            {
                if (pts.Count is not (2 or 3)) throw new ArgumentException("measure needs two points (distance) or three (angle): measure E,N,Z E,N,Z [E,N,Z] [alignment=NAME]");
                return FormatRecord(new MeasureRecord { Pts = pts, IsAngle = pts.Count == 3 }, 1);
            }
            if (pts.Count != 1) throw new ArgumentException("station-offset needs one point: station-offset E,N[,Z] [alignment=NAME]");
            return QueryBlock(pts[0], "POINT");
        }
        finally { _msAlignment = prev; }
    }

    private static Vec3 ParsePoint(UnitSystem u, string text)
    {
        var parts = text.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length is < 2 or > 3) throw new ArgumentException($"Can't read the point '{text}' (use E,N or E,N,Z).");
        var v = new double[3];
        for (int i = 0; i < parts.Length; i++)
            if (!ValueFormat.TryParse(u, ValueKind.Coordinate, parts[i], out v[i])) throw new ArgumentException($"Can't read the coordinate '{parts[i]}' in '{text}'.");
        return new Vec3(v[0], v[1], v[2]);
    }
}
