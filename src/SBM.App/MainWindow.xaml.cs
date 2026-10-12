using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using SBM.App.ViewModels;
using SBM.App.Viewport;
using SBM.Core.Drawing;
using SBM.Core.Geometry;
using OrthographicCamera = HelixToolkit.Wpf.SharpDX.OrthographicCamera;

namespace SBM.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private readonly Dictionary<string, List<Element3D>> _layerModels = new();
    private SceneData? _scene;
    private bool _planMode = true;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        View.EffectsManager = new DefaultEffectsManager();
        View.BackgroundColor = Color.FromRgb(0x1B, 0x1D, 0x21);
        View.MSAA = MSAALevel.Four;
        // D3DImage composition: GPU-rendered, but composes correctly with the surrounding WPF UI.
        View.EnableSwapChainRendering = false;
        View.Camera = new OrthographicCamera
        {
            Position = new Point3D(0, 0, 1500), LookDirection = new Vector3D(0, 0, -1500), UpDirection = new Vector3D(0, 1, 0),
            Width = 500, NearPlaneDistance = 1, FarPlaneDistance = 4000, // tight range keeps depth precision sub-millimeter
        };
        SetPlanMode(true);

        _vm.SceneReady += OnSceneReady;
        _vm.ProjectOpened += () => { if (_iso.Active) ExitIsolate(); };
        _vm.ZoomExtentsRequested += ZoomExtents;
        _vm.ViewModeRequested += SetPlanMode;
        _vm.LayerVisibilityChanged += (_, _) => { if (!_layerBatch) ApplyLayerVisibility(); };
        _vm.ReferencesReady += OnReferencesReady;
        _vm.SheetsRequested += () => ModeSheets.IsChecked = true;
        View.CameraChanged += (_, _) => KeepCameraInRange();
        InitNavigation();
        InitMeasure();
        InitDatabase();
        View.PreviewMouseLeftButtonDown += OnViewportMouseDown;
        View.PreviewMouseLeftButtonUp += OnViewportMouseUp;
        // Right button down / up go through MainWindow.Navigation (OnNavDown / OnNavUp): right-drag orbits, a right click opens the menu.
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.S && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) { OnToggleSection(this, new RoutedEventArgs()); e.Handled = true; }
            else if (e.Key == Key.Escape && Keyboard.FocusedElement is not System.Windows.Controls.TextBox)
            {
                // Esc: cancel the zoom window / clear the selection; with nothing selected it ends an isolate.
                bool selected = _selectedTag != null || _groupSel != null || _vm.SelectedProperties.Count > 0;
                CancelZoomWindow();
                if (!selected && _iso.Active && !MeasureActive && !SheetsMode) ExitIsolate();
                else ClearSelection();
            }
            else if (IsolateKey(e)) e.Handled = true;
        };

        View.Items.Add(new AmbientLight3D { Color = Color.FromRgb(90, 90, 95) });
        View.Items.Add(new DirectionalLight3D { Color = Color.FromRgb(210, 210, 205), Direction = new Vector3D(-0.4, -0.6, -1) });
        View.Items.Add(new DirectionalLight3D { Color = Color.FromRgb(90, 95, 110), Direction = new Vector3D(0.6, 0.5, 0.4) });

        Loaded += (_, _) =>
        {
            // A project file on the command line (double-click / "--open path") opens instead of the sample.
            var cl = Environment.GetCommandLineArgs();
            int oi = Array.IndexOf(cl, "--open");
            string? file = oi >= 0 && oi + 1 < cl.Length ? cl[oi + 1]
                : cl.Skip(1).FirstOrDefault(x => x.EndsWith(".sbm", StringComparison.OrdinalIgnoreCase) && System.IO.File.Exists(x));
            if (file != null) _vm.OpenPath(file);
            else _vm.LoadSampleCommand.Execute(null);
        };
        InitVram();
        SetupSnapshot();
        // Remote control for SbmApp / "sbm app …" (not in snapshot runs).
        if (!Environment.GetCommandLineArgs().Contains("--snapshot")) StartRemote();
        Closed += (_, _) => _remoteCts?.Cancel();
    }

    /// <summary>
    /// Command line: --snapshot out.png [--3d] renders the window and the viewport to PNG files and exits.
    /// Used for automated visual checks.
    /// </summary>
    private void SetupSnapshot()
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "--snapshot");
        if (i < 0 || i + 1 >= args.Length) return;
        string path = args[i + 1];
        bool threeD = args.Contains("--3d");
        bool done = false;
        _vm.SceneReady += (_, _) =>
        {
            if (done) return;
            done = true;
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                int ci = Array.IndexOf(args, "--closeup");
                if (ci >= 0 && ci + 1 < args.Length) Closeup(args[ci + 1], args.Contains("--below"));
                else if (threeD) SetPlanMode(false);
                int si = Array.IndexOf(args, "--section");
                if (si >= 0 && si + 1 < args.Length)
                {
                    var sp = args[si + 1].Split(',');
                    if (_vm.Units.TryParseStation(sp[0], out double ssta))
                        SectionAtStation(ssta, sp.Length > 1 ? double.Parse(sp[1], System.Globalization.CultureInfo.InvariantCulture) : 0);
                }
                // --off PREFIX,PREFIX: hide layers whose names start with any prefix (e.g. REF-,3D-DECK).
                int oi = Array.IndexOf(args, "--off");
                if (oi >= 0 && oi + 1 < args.Length)
                {
                    var pre = args[oi + 1].Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var li in _vm.LayerItems.Where(l => pre.Any(p => l.Name.StartsWith(p, StringComparison.OrdinalIgnoreCase)))) li.IsVisible = false;
                    ApplyLayerVisibility();
                }
                // --camera E,N,Z,dx,dy,dz,width[,distance]: orthographic 3D view of the world point (E, N, Z in meters) looking along (dx, dy, dz).
                int ki = Array.IndexOf(args, "--camera");
                if (ki >= 0 && ki + 1 < args.Length && _scene != null && View.Camera is OrthographicCamera kc)
                {
                    var kv = args[ki + 1].Split(',').Select(x => double.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                    _planMode = false; View.IsRotationEnabled = true; SyncViewSwitch(false); ApplyLayerVisibility();
                    var kt = new Vec3(kv[0], kv[1], kv[2]) - _scene.Origin;
                    var klk = new Vector3D(kv[3], kv[4], kv[5]);
                    klk.Normalize();
                    double kd = kv.Length > 7 ? kv[7] : 50;   // optional 8th value: camera distance (m) in front of the target
                    kc.LookDirection = klk * kd;
                    kc.Position = new Point3D(kt.X, kt.Y, kt.Z) - klk * kd;
                    kc.UpDirection = new Vector3D(0, 0, 1);
                    kc.Width = kv[6];
                    KeepCameraInRange();
                }
                if (args.Contains("--noterrain")) SetTerrainVisible(false);
                // --width W: orthographic view width in meters (after --closeup / --camera), e.g. to look at bars.
                int wi = Array.IndexOf(args, "--width");
                if (wi >= 0 && wi + 1 < args.Length && View.Camera is OrthographicCamera wc) wc.Width = double.Parse(args[wi + 1], System.Globalization.CultureInfo.InvariantCulture);
                if (args.Contains("--sheets")) ModeSheets.IsChecked = true;
                int hi = Array.IndexOf(args, "--hide");
                if (hi >= 0 && hi + 1 < args.Length)
                {
                    var hp = args[hi + 1].Split(',').Select(x => double.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                    Dispatcher.BeginInvoke(() =>
                    {
                        Pick(new Point(hp[0] * View.ActualWidth, hp[1] * View.ActualHeight));
                        if (_selectedTag != null) HideElement(_selectedTag, _selectedTitle);
                        ViewModels.MainViewModel.Log($"HIDE: {_hiddenList.Count} hidden: {string.Join(", ", _hiddenList.Select(h => h.label))}");
                    }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                }
                int pi = Array.IndexOf(args, "--pick");
                if (pi >= 0 && pi + 1 < args.Length)
                {
                    var pp = args[pi + 1].Split(',').Select(x => double.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                    Dispatcher.BeginInvoke(() =>
                    {
                        Pick(new Point(pp[0] * View.ActualWidth, pp[1] * View.ActualHeight));
                        ViewModels.MainViewModel.Log("PICK: " + string.Join(" | ", _vm.SelectedProperties.Select(r => $"{r.Name}={r.Value}")));
                    }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                }
                int obi = Array.IndexOf(args, "--orbit");
                if (obi >= 0 && obi + 1 < args.Length) SnapshotOrbit(args[obi + 1]);
                UpdateRebarLod();
                RunMeasureSnapshot(args);
                int lodWaits = 0;
                bool frameBenchDone = false;
                var t2 = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(threeD ? 1.5 : args.Any(x => x is "--measure" or "--query" or "--snapbench") ? 1.5 : 0.1) };
                t2.Tick += (_, _) =>
                {
                    int fbi = Array.IndexOf(args, "--framebench");
                    if (!_lodBusy && fbi >= 0 && !frameBenchDone)
                    {
                        frameBenchDone = true;
                        double secs = fbi + 1 < args.Length ? double.Parse(args[fbi + 1], System.Globalization.CultureInfo.InvariantCulture) : 3;
                        FrameBench(secs);
                        t2.Interval = TimeSpan.FromSeconds(secs + 0.5);
                        return;
                    }
                    if (_lodBusy && ++lodWaits < 120) { t2.Interval = TimeSpan.FromMilliseconds(500); return; } // let the near-view rebar finish
                    // terrain / references (loaded after the scene) and a scene rebuilt by the memory manager
                    if ((_vm.Project.References.Count > 0 && _vm.ReferenceLoads == 0 || _vm.IsBusy) && ++lodWaits < 240) { t2.Interval = TimeSpan.FromMilliseconds(500); return; }
                    t2.Stop();
                    if (args.Contains("--noterrain")) SetTerrainVisible(false); // terrain may have finished loading after the first call
                    // --pickafter fx,fy / --hideafter fx,fy: like --pick / --hide but once the near-view rebar is in place.
                    foreach (var opt in new[] { "--hideafter", "--pickafter" })
                    {
                        int oi2 = Array.IndexOf(args, opt);
                        if (oi2 < 0 || oi2 + 1 >= args.Length) continue;
                        var pp2 = args[oi2 + 1].Split(',').Select(x => double.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                        Pick(new Point(pp2[0] * View.ActualWidth, pp2[1] * View.ActualHeight));
                        ViewModels.MainViewModel.Log("PICKAFTER: " + string.Join(" | ", _vm.SelectedProperties.Select(r => $"{r.Name}={r.Value}")) + $" (smooth bars {_lodSkip.Count})");
                        if (opt == "--hideafter" && _selectedTag != null) { HideElement(_selectedTag, _selectedTitle); ViewModels.MainViewModel.Log($"HIDEAFTER: {_hiddenList.Count} hidden: {string.Join(", ", _hiddenList.Select(h => h.label))}"); }
                    }
                    SnapshotIsolate(args);
                    LogDetailVisibility();
                    LogVideoMemory("snapshot");
                    SaveSnapshot(path);
                    SnapshotEditors(path);
                    SnapshotRecentDialog(path);
                    _vm.IsDirty = false;
                    Close();
                };
                t2.Start();
            };
            timer.Start();
        };
    }

    /// <summary>Snapshot option <c>--framebench SECONDS</c>: renders continuously and logs the frame interval statistics (FRAMEBENCH:).</summary>
    private void FrameBench(double seconds)
    {
        var stamps = new List<double>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        EventHandler? h = null;
        h = (_, _) =>
        {
            stamps.Add(sw.Elapsed.TotalMilliseconds);
            if (sw.Elapsed.TotalSeconds >= seconds)
            {
                System.Windows.Media.CompositionTarget.Rendering -= h;
                var d = new List<double>();
                for (int i = 1; i < stamps.Count; i++) d.Add(stamps[i] - stamps[i - 1]);
                d.Sort();
                if (d.Count > 0)
                    ViewModels.MainViewModel.Log($"FRAMEBENCH: {d.Count} frames in {seconds:F1} s, mean {d.Average():F1} ms, median {d[d.Count / 2]:F1} ms, p95 {d[(int)(d.Count * 0.95)]:F1} ms, max {d[^1]:F1} ms; first frame at {stamps[0]:F0} ms, then +{(stamps.Count > 2 ? stamps[1] - stamps[0] : 0):F0}, +{(stamps.Count > 3 ? stamps[2] - stamps[1] : 0):F0}, +{(stamps.Count > 4 ? stamps[3] - stamps[2] : 0):F0} ms");
                return;
            }
            View.InvalidateRender();
        };
        System.Windows.Media.CompositionTarget.Rendering += h;
    }

    /// <summary>Points the 3D camera at a cross frame (by mark) for close inspection.</summary>
    private void Closeup(string mark, bool below)
    {
        if (_scene == null || View.Camera is not OrthographicCamera cam0) return;
        if (mark == "RAIL" && _vm.Framings.Keys.FirstOrDefault() is { } br && _vm.Project.FindAlignment(br.AlignmentName) is { } al)
        {
            // Rail close-up: the left SSTR and deck edge near the first span's midpoint, seen from outside and above.
            _planMode = false; View.IsRotationEnabled = true; SyncViewSwitch(false); ApplyLayerVisibility();
            // "--below" here means: end-on view looking up-station at the begin end (section through rail and slab).
            double sta = below ? br.Supports[0].Station - br.Supports[0].DeckEndOffset - 0.3 : (br.Supports[0].Station + br.Supports[1].Station) / 2;
            var p = al.PointAt(sta, br.Deck.LeftEdgeOffset + 0.5);
            var z = al.ElevationAt(sta, br.Deck.LeftEdgeOffset);
            var c = new Vec3(p.X, p.Y, z) - _scene.Origin;
            var rn = al.RightNormalAt(sta);
            var t = rn.Left;
            var lk = below ? new Vector3D(t.X, t.Y, 0) : new Vector3D(rn.X * 1.0 + t.X * 0.7, rn.Y * 1.0 + t.Y * 0.7, -0.55);
            lk.Normalize();
            cam0.LookDirection = lk * 50;
            cam0.Position = new Point3D(c.X, c.Y, c.Z + 0.3) - lk * 50;
            cam0.UpDirection = new Vector3D(0, 0, 1);
            cam0.Width = 3.2;
            return;
        }
        // EBLKn[@Bridge] = n-th deck end block (0-based, in joint order) of that bridge: longitudinal cut through the block at mid-deck, seen
        // from the left side, showing the soffit, the girder ends embedded in the block and the end cross frame beneath it.
        if (mark.StartsWith("EBLK") && int.TryParse((mark.Contains('@') ? mark[4..mark.IndexOf('@')] : mark[4..]), out int ebNo))
        {
            string ebBridge = mark.Contains('@') ? mark[(mark.IndexOf('@') + 1)..] : "";
            var ebm = (ebBridge == "" ? _vm.ConcreteModels.FirstOrDefault() : _vm.ConcreteModels.FirstOrDefault(kv => kv.Key.Name == ebBridge));
            var eblk = ebm.Value?.Joints.Where(j => j.EndBlock != null).ElementAtOrDefault(ebNo)?.EndBlock;
            if (eblk != null && _vm.Project.FindAlignment(ebm.Key.AlignmentName) is { } eal)
            {
                _planMode = false; View.IsRotationEnabled = true; SyncViewSwitch(false); ApplyLayerVisibility();
                var bb = eblk.Mesh.Bounds();
                var ebMid = (bb.Min + bb.Max) * 0.5;
                var so = eal.Inverse(ebMid.XY);
                double offC = (ebm.Key.Deck.LeftEdgeOffset + ebm.Key.Deck.RightEdgeOffset) / 2;
                // Cross-section cut (keeps the up-station side) through the flat soffit zone, halfway along it, seen from down-station.
                double xt = Math.Min(eblk.Soffit[1].X, eblk.Soffit[^1].X) / 2;
                double F(double st) => eblk.PlanX(eal.PointAt(st, offC)) - xt;
                double s0 = so.Station, s1 = so.Station + 0.5;
                for (int it = 0; it < 12; it++)
                {
                    double f0 = F(s0), f1 = F(s1);
                    if (Math.Abs(f1 - f0) < 1e-12) break;
                    double s2 = s1 - f1 * (s1 - s0) / (f1 - f0);
                    s0 = s1; s1 = s2;
                    if (Math.Abs(F(s1)) < 1e-4) break;
                }
                SectionAtStation(s1, 0);
                var pc = eal.PointAt(s1, offC);
                var c = new Vec3(pc.X, pc.Y, ebMid.Z - 0.3) - _scene.Origin;
                var tg = eal.RightNormalAt(s1).Left;
                var lk = new Vector3D(tg.X, tg.Y, 0.12);
                lk.Normalize();
                cam0.LookDirection = lk * 50;
                cam0.Position = new Point3D(c.X, c.Y, c.Z) - lk * 50;
                cam0.UpDirection = new Vector3D(0, 0, 1);
                cam0.Width = 4.5;
                KeepCameraInRange();
                return;
            }
        }
        // SUBn = unit n of the first bridge's substructure; SUBn@Bridge name = of that bridge.
        string subBridge = mark.Contains('@') ? mark[(mark.IndexOf('@') + 1)..] : "";
        string subMark = mark.Contains('@') ? mark[..mark.IndexOf('@')] : mark;
        // FTGn[@Bridge] = foundation (footings + piles) of unit n, seen from the side and slightly above; width 110 ft.
        bool ftg = subMark.StartsWith("FTG");
        if (ftg) subMark = "SUB" + subMark[3..];
        if (subMark.StartsWith("SUB") && int.TryParse(subMark[3..], out int subNo)
            && (subBridge == "" ? _vm.SubstructureModels.Values.FirstOrDefault() : _vm.SubstructureModels.FirstOrDefault(kv => kv.Key.Name == subBridge).Value)?.Units.ElementAtOrDefault(subNo) is { } su)
        {
            // Substructure close-up: the bent seen from up-station, a little above (or below) and skewed.
            _planMode = false; View.IsRotationEnabled = true; SyncViewSwitch(false); ApplyLayerVisibility();
            // As-designed caps (hammerheads) can extend past CapS0/S1: frame the outline when there is one.
            double s0 = su.CapOutline.Count > 0 ? Math.Min(su.CapS0, su.CapOutline.Min(p => p.X)) : su.CapS0;
            double s1 = su.CapOutline.Count > 0 ? Math.Max(su.CapS1, su.CapOutline.Max(p => p.X)) : su.CapS1;
            double zMid = su.CapBottom.Average(p => p.Y) - (below ? 0 : 1);
            var c = su.World((s0 + s1) / 2, 0, zMid) - _scene.Origin;
            var lk = new Vector3D(-su.N.X + su.D.X * 0.45, -su.N.Y + su.D.Y * 0.45, below ? 0.15 : -0.3);
            lk.Normalize();
            // The section cut removes the span between the camera and the bent.
            // Other bridges may sit on another alignment (Ramp G): cut along the unit's own support line, just up-station of
            // the cap, keeping the bent side (the camera looks down-station along -N).
            if (subBridge == "") SectionAtSupport(su.SupportIndex);
            else SectionAtWorldPlane(su.World((su.CapS0 + su.CapS1) / 2, su.CapN1 + 0.6, 0), -su.N);
            cam0.LookDirection = lk * 50;
            cam0.Position = new Point3D(c.X, c.Y, c.Z) - lk * 50;
            cam0.UpDirection = new Vector3D(0, 0, 1);
            cam0.Width = (s1 - s0) * 1.6;
            if (ftg && su.Foundations.Count > 0)
            {
                var f0 = su.Foundations[0];
                var tip = su.Foundations.SelectMany(q => q.Piles).Select(q => q.TipZ).DefaultIfEmpty(f0.BottomZ).Min();
                var cfm = su.World(f0.S, f0.N, (f0.BottomZ + tip) / 2) - _scene.Origin;
                var lkf = new Vector3D(-su.N.X + su.D.X * 0.25, -su.N.Y + su.D.Y * 0.25, 0.12);
                lkf.Normalize();
                cam0.LookDirection = lkf * 50;
                cam0.Position = new Point3D(cfm.X, cfm.Y, cfm.Z) - lkf * 50;
                cam0.Width = Math.Max(f0.SizeS * 2.5, (f0.BottomZ - tip) * 1.9);
            }
            KeepCameraInRange();
            return;
        }
        if (mark.StartsWith("PS") && _vm.PrestressedModels.Values.FirstOrDefault() is { } psm
            && psm.Beams.FirstOrDefault(b => b.Mark == mark[2..]) is { } psb
            && _vm.Framings.Keys.FirstOrDefault() is { } pbr && _vm.Project.FindAlignment(pbr.AlignmentName) is { } pal)
        {
            // Prestressed close-up (e.g. PSG3-S1): section cut across the span near the beam's far end, looking
            // down-station at the cut I-shapes and strands (--below: from the side and below, at the bearing end).
            _planMode = false; View.IsRotationEnabled = true; SyncViewSwitch(false); ApplyLayerVisibility();
            var e = psb.TopEnd;
            var tv = (psb.TopEnd - psb.TopStart).XY.Normalized();
            if (!below)
            {
                var cutPt = e.XY - tv * 2.0;
                double sta = pal.Inverse(cutPt).Station;
                SectionAtStation(sta, 0);
                var cmid = new Vec3(cutPt.X, cutPt.Y, e.Z - psb.Shape.Depth * 0.4) - _scene.Origin;
                var lk2 = new Vector3D(tv.X, tv.Y, -0.01); // the cut keeps the up-station side: look at it from down-station
                lk2.Normalize();
                cam0.LookDirection = lk2 * 50;
                cam0.Position = new Point3D(cmid.X, cmid.Y, cmid.Z) - lk2 * 50;
                cam0.UpDirection = new Vector3D(0, 0, 1);
                cam0.Width = psb.Shape.TopWidth * 5;
                KeepCameraInRange();
                return;
            }
            var c = new Vec3(e.X, e.Y, e.Z - psb.Shape.Depth / 2) - _scene.Origin;
            var lk = new Vector3D(-tv.Y * 1.4 + tv.X * 0.8, tv.X * 1.4 + tv.Y * 0.8, 0.35);
            lk.Normalize();
            cam0.LookDirection = lk * 50;
            cam0.Position = new Point3D(c.X, c.Y, c.Z) - lk * 50;
            cam0.UpDirection = new Vector3D(0, 0, 1);
            cam0.Width = 5;
            KeepCameraInRange();
            return;
        }
        if (System.Text.RegularExpressions.Regex.Match(mark, @"^FS(\d+)(?:-G(\d+))?$") is { Success: true } fsm)
        {
            // Field splice close-up (FS4 = on G2, FS4-G1 = on G1): look at the splice from below and to the side.
            int fsNo = int.Parse(fsm.Groups[1].Value), fsG = fsm.Groups[2].Success ? int.Parse(fsm.Groups[2].Value) - 1 : 1;
            var sp = _vm.Framings.Values.SelectMany(f => f.Splices).FirstOrDefault(s => s.SpliceIndex == fsNo - 1 && s.Girder == fsG);
            if (sp == null) return;
            _planMode = false; View.IsRotationEnabled = true; SyncViewSwitch(false); ApplyLayerVisibility();
            var c = sp.Location - _scene.Origin;
            var tn = sp.GirderTangent;
            var lk = new Vector3D(-tn.Y * 2 + tn.X * 0.6, tn.X * 2 + tn.Y * 0.6, below ? 0.9 : -0.9);
            lk.Normalize();
            var tgt = new Point3D(c.X, c.Y, c.Z - 0.8);
            cam0.LookDirection = lk * 50;
            cam0.Position = tgt - lk * 50;
            cam0.UpDirection = new Vector3D(0, 0, 1);
            cam0.Width = 3.5;
            return;
        }
        // MARK@Bridge name limits the search to that bridge (inspection walkway marks repeat in every bridge).
        var steelSets = subBridge == "" ? _vm.SteelModels.Values : _vm.SteelModels.Where(kv => kv.Key.Name == subBridge).Select(kv => kv.Value);
        if (steelSets.SelectMany(m => m.Parts).Where(p => p.Assembly == subMark).ToList() is { Count: > 0 } aparts
            && !mark.StartsWith("CF") && !mark.StartsWith("FS"))
        {
            // Any steel assembly (bearings B-…, cross girders XG-…): look at its bounding box.
            _planMode = false; View.IsRotationEnabled = true; SyncViewSwitch(false); ApplyLayerVisibility();
            var bb = aparts[0].Mesh.Bounds();
            foreach (var ap in aparts.Skip(1)) { var b2 = ap.Mesh.Bounds(); bb.Add(b2.Min); bb.Add(b2.Max); }
            var ctr = (bb.Min + bb.Max) * 0.5 - _scene.Origin;
            var lk = new Vector3D(0.7, 0.9, below ? 0.35 : -0.35);
            lk.Normalize();
            cam0.LookDirection = lk * 50;
            cam0.Position = new Point3D(ctr.X, ctr.Y, ctr.Z) - lk * 50;
            cam0.UpDirection = new Vector3D(0, 0, 1);
            cam0.Width = Math.Max(2.2, (bb.Max - bb.Min).Length * 1.6);
            KeepCameraInRange();
            return;
        }
        var cf = _vm.Framings.Values.SelectMany(f => f.CrossFrames).FirstOrDefault(c => c.Mark == mark);
        if (cf == null || View.Camera is not OrthographicCamera cam) return;
        _planMode = false;
        View.IsRotationEnabled = true;
        SyncViewSwitch(false);
        ApplyLayerVisibility();
        var mid = Vec3.Lerp(cf.Left, cf.Right, 0.5) - _scene.Origin;
        var n = cf.Direction.Left;
        var look = new Vector3D(-n.X * 3 + cf.Direction.X, -n.Y * 3 + cf.Direction.Y, below ? 1.2 : -1.0);
        look.Normalize();
        var target = new Point3D(mid.X, mid.Y, mid.Z - 0.7);
        cam.LookDirection = look * 50;
        cam.Position = target - look * 50;
        cam.UpDirection = new Vector3D(0, 0, 1);
        cam.Width = cf.PlanLength * 1.4;
    }

    private void SaveSnapshot(string path)
    {
        SaveElement((FrameworkElement)Content, path);
        if (View.RenderBitmap() is { } vb) SavePng(vb, System.IO.Path.ChangeExtension(path, null) + "_view.png");
    }

    /// <summary>With --editors, also renders each input window (every tab) to PNG.</summary>
    private void SnapshotEditors(string path)
    {
        if (!Environment.GetCommandLineArgs().Contains("--editors")) return;
        string stem = System.IO.Path.ChangeExtension(path, null);
        var project = _vm.Project;
        var cmd = Environment.GetCommandLineArgs();
        // --editbridge NAME: the bridge shown in the bridge editor renders (default: the first bridge); --editsupport NAME: the support
        // selected on its Substructure / Deck Ends tabs.
        int ebi = Array.IndexOf(cmd, "--editbridge");
        var editBridge = (ebi >= 0 && ebi + 1 < cmd.Length ? project.Bridges.FirstOrDefault(b => b.Name == cmd[ebi + 1]) : null) ?? project.Bridges[0];
        foreach (var (name, window) in new (string, Window)[]
                 {
                     ("alignment", Editors.AlignmentEditorWindow.CreateForPreview(project.Alignments[0], project.Units)),
                     ("bridge", Editors.BridgeEditorWindow.CreateForPreview(editBridge, project, project.Units,
                         Array.IndexOf(cmd, "--editsupport") is int esi && esi >= 0 && esi + 1 < cmd.Length ? cmd[esi + 1] : null)),
                 })
        {
            window.Owner = this;
            window.Show();
            var tabs = FindChild<System.Windows.Controls.TabControl>(window);
            int count = tabs?.Items.Count ?? 1;
            for (int t = 0; t < count; t++)
            {
                if (tabs != null) tabs.SelectedIndex = t;
                window.UpdateLayout();
                Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                SaveElement((FrameworkElement)window.Content, $"{stem}_{name}{t}.png");
                // Long field lists: also render the rest of the tab a page at a time (_p1, _p2, ...).
                if (tabs != null && FindChild<System.Windows.Controls.ScrollViewer>(tabs) is { ScrollableHeight: > 0 } sv)
                {
                    for (int page = 1; page <= 16 && sv.VerticalOffset < sv.ScrollableHeight - 0.5; page++)
                    {
                        sv.ScrollToVerticalOffset(sv.VerticalOffset + Math.Max(50, sv.ViewportHeight - 40));
                        window.UpdateLayout();
                        Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                        SaveElement((FrameworkElement)window.Content, $"{stem}_{name}{t}_p{page}.png");
                    }
                    sv.ScrollToTop();
                }
            }
            window.Close();
        }

        if (_vm.ConcreteModels.Values.FirstOrDefault() is { } cm)
        {
            var bs = new Editors.BarScheduleWindow(project.Bridges[0].Name, cm, _vm.SubstructureModels.GetValueOrDefault(project.Bridges[0]), project.Units) { Owner = this };
            bs.Show();
            var grid = FindChild<System.Windows.Controls.DataGrid>(bs)!;
            foreach (string mark in new[] { "S", "U", "T1", "L3", "Pier 1/S", "Pier 1/P" })
            {
                var items = ((IEnumerable<Editors.BarRow>)grid.ItemsSource).ToList();
                int idx = items.FindIndex(r => (r.Unit == Editors.BarScheduleWindow.Superstructure ? r.Mark : r.Unit + "/" + r.Mark) == mark);
                if (idx < 0) continue;
                grid.SelectedIndex = idx;
                bs.UpdateLayout();
                Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                SaveElement((FrameworkElement)bs.Content, $"{stem}_bars_{mark.Replace("/", "-").Replace(" ", "")}.png");
            }
            bs.Close();
        }

        if (_vm.Framings.Values.FirstOrDefault() is { } fr)
        {
            var ec = new Editors.ElevationCheckWindow(fr, _vm.SteelModels.GetValueOrDefault(project.Bridges[0]), project.Units, _vm.SubstructureModels.GetValueOrDefault(project.Bridges[0]), _vm.PrestressedModels.GetValueOrDefault(project.Bridges[0])) { Owner = this };
            ec.Show();
            var grid = (System.Windows.Controls.DataGrid)ec.FindName("BearingGrid");
            foreach (int idx in new[] { 0, grid.Items.Count / 2 })
            {
                grid.SelectedIndex = idx;
                ec.UpdateLayout();
                Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                SaveElement((FrameworkElement)ec.Content, $"{stem}_elev{idx}.png");
            }
            ec.Close();
        }
        if (_vm.BuildDrawings() is { } dr)
        {
            // Offscreen renders of every view; --dwgzoom x0,y0,x1,y1 (fractions of the extents) adds a zoomed render.
            var args = Environment.GetCommandLineArgs();
            int zi = Array.IndexOf(args, "--dwgzoom");
            double[]? win = zi >= 0 && zi + 1 < args.Length
                ? args[zi + 1].Split(',').Select(x => double.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray() : null;
            for (int v = 0; v < dr.views.Count; v++)
            {
                Viewport.DrawingCanvas.RenderToPng(dr.views[v].Drawing, $"{stem}_dwg{v}_{dr.views[v].FileStem}.png", 1800, 1200);
                if (win != null) Viewport.DrawingCanvas.RenderToPng(dr.views[v].Drawing, $"{stem}_dwg{v}_{dr.views[v].FileStem}_zoom.png", 1800, 1200, win);
            }
            // Composed 11x17 sheets (paper colors), plus a PDF of the set.
            if (_vm.BuildDrawingContext() is { } sctx)
            {
                var sheets = SBM.Core.Drawing.Sheets.SheetSet.Build(sctx, new SBM.Core.Drawing.Sheets.SheetFormat(17, 11, project.Name, sctx.Framing.Definition.Name, "SNAPSHOT"));
                for (int i = 0; i < sheets.Count; i++)
                {
                    var cv = new Viewport.DrawingCanvas { Paper = true, PaperSize = (17, 11) };
                    Viewport.DrawingCanvas.RenderToPng(sheets[i].Drawing, $"{stem}_sheet{i + 1:00}.png", 1870, 1210, null, paper: true);
                    if (win != null) Viewport.DrawingCanvas.RenderToPng(sheets[i].Drawing, $"{stem}_sheet{i + 1:00}_zoom.png", 1870, 1210, win, paper: true);
                }
                SBM.IO.Pdf.PdfWriter.Write($"{stem}_sheets.pdf", sheets, sctx.Framing.Definition.Name);
            }
        }
        // Command search popup (Ctrl+K) with a query.
        CommandSearch.Focus();
        CommandSearch.Text = "elev";
        UpdateLayout();
        Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        if (CommandPopup.Child is FrameworkElement pc) SaveElement(pc, $"{stem}_search.png");
        CommandSearch.Text = "";
    }

    private static T? FindChild<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var c = VisualTreeHelper.GetChild(root, i);
            if (c is T t) return t;
            if (FindChild<T>(c) is { } found) return found;
        }
        return null;
    }

    private static void SaveElement(FrameworkElement root, string path)
    {
        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(root);
        SavePng(rtb, path);
    }

    private static void SavePng(System.Windows.Media.Imaging.BitmapSource b, string p)
    {
        var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
        enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(b));
        using var fs = System.IO.File.Create(p);
        enc.Save(fs);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_vm.CanClose()) e.Cancel = true;
        base.OnClosing(e);
    }

    // ------------------------------------------------------------------ scene

    private void OnSceneReady(SceneData scene, bool zoom)
    {
        _scene = scene;
        ClearHighlight();
        foreach (var list in _layerModels.Values)
            foreach (var m in list) View.Items.Remove(m);
        _layerModels.Clear();
        _fullLines.Clear();
        _fullText.Clear();
        _fullMeshes.Clear();
        _visibleInstances.Clear();
        ResetRebarLod();
        // Hidden elements are objects of the previous model: keep only those that still exist (by mark) after regeneration.
        RemapHidden(scene);

        foreach (var layer in scene.Layers)
        {
            var models = new List<Element3D>();
            var color = Color.FromRgb(layer.Style.R, layer.Style.G, layer.Style.B);
            bool visible = _vm.IsLayerVisible(layer.Style.Name);
            if (layer.Lines != null)
            {
                var lm = new LineGeometryModel3D
                {
                    Geometry = layer.Lines,
                    Color = color,
                    Thickness = layer.Style.Weight,
                    Smoothness = 0,
                    IsHitTestVisible = false,
                    Visibility = visible ? Visibility.Visible : Visibility.Collapsed,
                };
                _fullLines[lm] = layer.Lines;
                models.Add(lm);
            }
            if (layer.Text != null)
            {
                var tm = new BillboardTextModel3D
                {
                    Geometry = layer.Text,
                    FixedSize = false, // model-sized text, like CAD annotation
                    IsHitTestVisible = false,
                    Visibility = visible ? Visibility.Visible : Visibility.Collapsed,
                };
                _fullText[tm] = layer.Text;
                models.Add(tm);
            }
            foreach (var m in models) View.Items.Add(m);
            _layerModels[layer.Style.Name] = models;
        }

        foreach (var solid in scene.Solids)
        {
            var models = new List<Element3D>();
            var mat = new PhongMaterial
            {
                DiffuseColor = new HelixToolkit.Maths.Color4(solid.R / 255f, solid.G / 255f, solid.B / 255f, solid.Opacity),
                AmbientColor = new HelixToolkit.Maths.Color4(0.25f, 0.25f, 0.25f, 1f),
                SpecularColor = new HelixToolkit.Maths.Color4(0.25f, 0.25f, 0.25f, 1f),
                SpecularShininess = 30f,
            };
            foreach (var (mesh, instances) in solid.Parts)
                models.Add(NewSolidModel(mesh, mat, solid.Opacity < 0.999f, instances));
            foreach (var m in models) View.Items.Add(m);
            _layerModels[solid.Name] = models;
        }
        RegisterDetailModels();
        RegisterVramModels(scene);
        ApplyLayerVisibility();
        PlanVram("scene");
        LogFirstFrame("scene");
        if (_sectionOn) InitSectionInputs();
        if (_sectionOn || _hidden.Count > 0) ApplySection();

        if (zoom) Dispatcher.BeginInvoke(ZoomExtents, System.Windows.Threading.DispatcherPriority.Background);
        MarkSheetsStale();
        InvalidateSnapIndex();
        Dispatcher.BeginInvoke(UpdateRebarLod, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void ZoomExtents()
    {
        _pivot = null;
        if (_iso.Active && ZoomToIsolated(true)) return; // isolated: zoom extents = the isolated set
        if (_scene == null || _scene.Extents.IsEmpty) { View.ZoomExtents(0); return; }
        if (_planMode)
        {
            var e = _scene.FocusExtents;
            var c = e.Center;
            double cx = c.X - _scene.Origin.X, cy = c.Y - _scene.Origin.Y;
            double aspect = View.ActualHeight > 0 ? View.ActualWidth / View.ActualHeight : 1.6;
            double width = Math.Max(e.Width, e.Height * aspect) * 1.08;
            if (View.Camera is OrthographicCamera cam)
            {
                cam.Position = new Point3D(cx, cy, 1500);
                cam.LookDirection = new Vector3D(0, 0, -1500);
                cam.UpDirection = new Vector3D(0, 1, 0);
                cam.Width = Math.Max(width, 1);
            }
        }
        else
        {
            var e = _scene.FocusExtents;
            View.ZoomExtents(new Rect3D(e.MinX - _scene.Origin.X, e.MinY - _scene.Origin.Y, -20, e.Width, e.Height, 40), 300);
        }
    }

    /// <summary>Framing-plan linework shown only in plan view; shaded solids only in 3D.</summary>
    private static readonly HashSet<string> PlanOnlyLayers = new()
    {
        Layers.GirderWeb.Name, Layers.GirderFlange.Name, Layers.Stiffener.Name, Layers.CrossFrame.Name,
        Layers.FieldSplice.Name, Layers.Bearing.Name, Layers.Labels.Name,
    };

    /// <summary>Reference models live outside the regenerated scene; they are only rebuilt when references change.</summary>
    private readonly Dictionary<string, List<Element3D>> _refModels = new();

    private void OnReferencesReady(List<ReferenceRender> renders)
    {
        foreach (var list in _refModels.Values)
            foreach (var m in list) View.Items.Remove(m);
        _refModels.Clear();
        _refRadius = 0;
        foreach (var r in renders)
        {
            var def = r.Definition;
            var models = new List<Element3D>();
            if (r.Mesh != null)
            {
                float a = (float)Math.Clamp(def.Opacity, 0.05, 1);
                var mat = new PhongMaterial
                {
                    DiffuseColor = new HelixToolkit.Maths.Color4(def.R / 255f, def.G / 255f, def.B / 255f, a),
                    AmbientColor = new HelixToolkit.Maths.Color4(0.2f, 0.2f, 0.2f, 1f),
                    SpecularColor = new HelixToolkit.Maths.Color4(0.05f, 0.05f, 0.05f, 1f), SpecularShininess = 5f,
                };
                if (r.ImageFile != null)
                {
                    // Draped photo: unlit-looking (bright ambient, white diffuse) so the imagery keeps its colors.
                    mat.DiffuseColor = new HelixToolkit.Maths.Color4(1f, 1f, 1f, a);
                    mat.AmbientColor = new HelixToolkit.Maths.Color4(0.55f, 0.55f, 0.55f, 1f);
                    mat.DiffuseMap = new HelixToolkit.SharpDX.TextureModel(TerrainImageStream(r.ImageFile, r.ImageMaxPixels, r.ImageWidth, r.ImageHeight), true);
                }
                var rm = NewSolidModel(r.Mesh, mat, a < 0.999, null);
                rm.CullMode = SharpDX.Direct3D11.CullMode.None;
                rm.IsHitTestVisible = false;
                models.Add(rm);
                var bb = r.Mesh.Bound;
                _refRadius = Math.Max(_refRadius, (bb.Maximum - bb.Minimum).Length() / 2);
            }
            if (r.Lines != null)
                models.Add(new LineGeometryModel3D { Geometry = r.Lines, Color = Color.FromRgb(def.R, def.G, def.B), Thickness = 1, IsHitTestVisible = false });
            _refModels[r.LayerName] = models;
            var contours = new List<Element3D>();
            if (r.MinorContours != null)
                contours.Add(new LineGeometryModel3D { Geometry = r.MinorContours, Color = Color.FromRgb(110, 90, 62), Thickness = 0.7, IsHitTestVisible = false });
            if (r.MajorContours != null)
                contours.Add(new LineGeometryModel3D { Geometry = r.MajorContours, Color = Color.FromRgb(165, 135, 90), Thickness = 1.3, IsHitTestVisible = false });
            if (contours.Count > 0) _refModels[r.ContourLayerName] = contours;
            foreach (var m in models.Concat(contours)) View.Items.Add(m);
        }
        IsolateNewLayers();
        ApplyLayerVisibility();
        if (_sectionOn) ApplySection();
        KeepCameraInRange();
        InvalidateSnapIndex();
        bool own = _vramTerrainReload;
        _vramTerrainReload = false;
        PlanVram(own ? "terrain reload" : "references");
        LogFirstFrame("references");
    }

    /// <summary>Logs when the first frames after a scene / reference change were rendered (GPU upload included) and the DXGI usage then.</summary>
    private void LogFirstFrame(string what)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int frames = 0;
        EventHandler? h = null;
        h = (_, _) =>
        {
            // The viewport renders on the frame after the models were attached; wait a few frames.
            if (++frames < 3) return;
            System.Windows.Media.CompositionTarget.Rendering -= h;
            ViewModels.MainViewModel.Log($"VRAM: first frames after the {what} change in {sw.ElapsedMilliseconds} ms");
            LogVideoMemory($"after {what}");
        };
        System.Windows.Media.CompositionTarget.Rendering += h;
    }

    private void ApplyLayerVisibility()
    {
        foreach (var (name, list) in _refModels)
        {
            var mode = _vm.ReferenceLayerModes.FirstOrDefault(m => m.name == name);
            bool modeOk = _planMode ? !mode.threeDOnly : !mode.planOnly;
            bool visible = modeOk && _vm.IsLayerVisible(name);
            foreach (var m in list) m.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
        foreach (var (name, list) in _layerModels)
        {
            bool visible = LayerModeOk(name) && _vm.IsLayerVisible(name);
            foreach (var m in list) m.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        }
        ApplyDetailVisibility();
    }

    private bool _syncingSwitch;

    /// <summary>Keeps the Plan / 3D switch in step with the view (ribbon buttons, close-ups, section cuts).</summary>
    private void SyncViewSwitch(bool plan)
    {
        _syncingSwitch = true;
        ViewPlanButton.IsChecked = plan;
        View3DButton.IsChecked = !plan;
        _syncingSwitch = false;
    }

    // ------------------------------------------------------------------ terrain toggle & compass views

    private void OnTerrainToggle(object sender, RoutedEventArgs e) => SetTerrainVisible(TerrainToggle.IsChecked == true);

    public void SetTerrainVisible(bool on)
    {
        TerrainToggle.IsChecked = on;
        var names = _vm.Project.References.Where(r => r.Kind == SBM.Core.References.ReferenceKind.Terrain)
            .SelectMany(r => new[] { "REF-" + r.Name.ToUpperInvariant(), "REF-" + r.Name.ToUpperInvariant() + "-CONTOURS" }).ToHashSet();
        foreach (var li in _vm.LayerItems.Where(l => names.Contains(l.Name))) li.IsVisible = on;
        ApplyLayerVisibility();
    }

    private double _compassAngle = 45;
    private static readonly (string name, double az)[] Compass =
        { ("N", 0), ("NE", 45), ("E", 90), ("SE", 135), ("S", 180), ("SW", 225), ("W", 270), ("NW", 315) };

    private void OnCompassViews(object sender, RoutedEventArgs e)
    {
        CompassViewButton.IsChecked = false;
        var menu = new System.Windows.Controls.ContextMenu { PlacementTarget = CompassViewButton, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom };
        foreach (var (name, az) in Compass)
        {
            var mi = new System.Windows.Controls.MenuItem { Header = $"From {name}" };
            mi.Click += (_, _) => SetCompassView(az);
            menu.Items.Add(mi);
        }
        menu.Items.Add(new System.Windows.Controls.Separator());
        var box = new System.Windows.Controls.TextBox { Text = _compassAngle.ToString("0.#"), Width = 50, Margin = new Thickness(6, 0, 0, 0) };
        box.LostFocus += (_, _) => { if (double.TryParse(box.Text.Trim('°'), out double v) && v > 0 && v < 90) _compassAngle = v; else box.Text = _compassAngle.ToString("0.#"); };
        box.KeyDown += (_, ke) => { if (ke.Key == Key.Enter) { if (double.TryParse(box.Text.Trim('°'), out double v) && v > 0 && v < 90) _compassAngle = v; menu.IsOpen = false; } };
        var row = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        row.Children.Add(new System.Windows.Controls.TextBlock { Text = "Look-down angle (°)", VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(box);
        menu.Items.Add(new System.Windows.Controls.MenuItem { Header = row, StaysOpenOnClick = true });
        var rbox = new System.Windows.Controls.TextBox { Text = _detailRadius > 0 ? _vm.Units.FormatLength(_detailRadius) : "0", Width = 70, Margin = new Thickness(6, 0, 0, 0) };
        void SetRadius()
        {
            if (rbox.Text.Trim() == "0") _detailRadiusUser = 0;
            else if (_vm.Units.TryParseLength(rbox.Text, SBM.Core.Units.LengthKind.Long, out double v) && v >= 0) _detailRadiusUser = v;
            if (_vramLevel != null) ApplyVramLevel(_vramLevel);
            else { _detailRadius = _detailRadiusUser; ApplyDetailVisibility(); }
        }
        rbox.LostFocus += (_, _) => SetRadius();
        rbox.KeyDown += (_, ke) => { if (ke.Key == Key.Enter) { SetRadius(); menu.IsOpen = false; } };
        var row2 = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        row2.Children.Add(new System.Windows.Controls.TextBlock { Text = "Bolts / studs / rebar drawn within (0 = all)", VerticalAlignment = VerticalAlignment.Center });
        row2.Children.Add(rbox);
        menu.Items.Add(new System.Windows.Controls.MenuItem { Header = row2, StaysOpenOnClick = true, ToolTip = "Distance from the line of sight through the view center. Detail is hidden while the view moves." });
        menu.IsOpen = true;
    }

    /// <summary>3D view from a compass direction (azimuth clockwise from grid north), looking down at the set angle,
    /// keeping the current target and zoom.</summary>
    public void SetCompassView(double azDeg)
    {
        if (_planMode) SetPlanMode(false);
        if (View.Camera is not HelixToolkit.Wpf.SharpDX.ProjectionCamera cam) return;
        var target = cam.Position + cam.LookDirection;
        double dist = Math.Max(cam.LookDirection.Length, 1);
        double az = azDeg * Math.PI / 180, el = _compassAngle * Math.PI / 180;
        var look = new Vector3D(-Math.Sin(az) * Math.Cos(el), -Math.Cos(az) * Math.Cos(el), -Math.Sin(el));
        cam.LookDirection = look * dist;
        cam.Position = target - look * dist;
        cam.UpDirection = new Vector3D(0, 0, 1);
        KeepCameraInRange();
    }

    private void OnViewSwitch(object sender, RoutedEventArgs e)
    {
        if (_syncingSwitch || ViewPlanButton == null || View3DButton == null) return;
        bool plan = sender == ViewPlanButton;
        if (plan != _planMode) SetPlanMode(plan);
    }

    private void SetPlanMode(bool plan)
    {
        _planMode = plan;
        ApplyLayerVisibility();
        View.IsRotationEnabled = !plan;
        SyncViewSwitch(plan);
        if (View.Camera is not OrthographicCamera cam) return;
        if (plan)
        {
            ZoomExtents();
        }
        else
        {
            var target = cam.Position + cam.LookDirection;
            var dir = new Vector3D(1, 1, -0.8);
            dir.Normalize();
            dir *= 1500;
            cam.Position = target - dir;
            cam.LookDirection = dir;
            cam.UpDirection = new Vector3D(0, 0, 1);
            ZoomExtents();
        }
    }

    private void OnViewportMouseMove(object sender, MouseEventArgs e)
    {
        if (_scene == null || MeasureActive) return; // the measure tools write their own (snapped) readout
        var p = View.UnProjectOnPlane(e.GetPosition(View), new Point3D(0, 0, 0), new Vector3D(0, 0, 1));
        if (p is not { } hit) return;
        _vm.UpdateCursor(new Vec2(hit.X + _scene.Origin.X, hit.Y + _scene.Origin.Y));
    }

    // ------------------------------------------------------------------ explorer

    private void OnExplorerSelected(object sender, RoutedPropertyChangedEventArgs<object> e) =>
        _vm.SelectedNode = e.NewValue as ExplorerNode;

    private void OnExplorerDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ExplorerTree.SelectedItem is ExplorerNode n && n.Open != null)
        {
            e.Handled = true;
            n.Open();
        }
    }
}
