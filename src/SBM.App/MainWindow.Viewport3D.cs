using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using HelixToolkit;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using SBM.App.Editors;
using SBM.App.Viewport;
using SBM.Core.Geometry;
using Color = System.Windows.Media.Color;
using MeshGeometry3D = HelixToolkit.SharpDX.MeshGeometry3D;
using OrthographicCamera = HelixToolkit.Wpf.SharpDX.OrthographicCamera;

namespace SBM.App;

/// <summary>
/// 3D viewport behavior: camera depth range (no near-plane clipping), live section cuts in alignment coordinates, and
/// picking objects to show their properties.
/// </summary>
public partial class MainWindow
{
    // ================================================================== camera range

    private bool _adjustingCamera;

    /// <summary>
    /// Orthographic views do not change when the eye slides along the view direction, so the eye is kept just outside a
    /// sphere around the whole scene and the near/far planes bracket it. Nothing can fall in front of the near plane,
    /// however close the zoom or orbit, and depth precision stays good.
    /// </summary>
    private void KeepCameraInRange()
    {
        if (_adjustingCamera || _scene == null || View.Camera is not OrthographicCamera cam) return;
        var e = _scene.Extents;
        if (e.IsEmpty) return;
        var c = new Vector3D(e.Center.X - _scene.Origin.X, e.Center.Y - _scene.Origin.Y, 0);
        double r = Math.Max(Math.Sqrt(e.Width * e.Width + e.Height * e.Height) / 2, 200) + 150 + _refRadius;
        var look = cam.LookDirection;
        if (look.Length < 1e-9) return;
        look.Normalize();
        var p = cam.Position;
        double t = Vector3D.DotProduct(c - (Vector3D)p, look); // distance along the view to the scene center
        double want = r + 5;
        _adjustingCamera = true;
        try
        {
            if (Math.Abs(t - want) > 1) cam.Position = p + look * (t - want);
            cam.LookDirection = look * want;
            cam.NearPlaneDistance = 0.5;
            cam.FarPlaneDistance = 2 * r + 10;
        }
        finally { _adjustingCamera = false; }
    }

    /// <summary>Extra radius for reference models (terrain) that extend beyond the bridge.</summary>
    private double _refRadius;

    // ================================================================== section cut

    private bool _sectionOn;
    private readonly Dictionary<LineGeometryModel3D, LineGeometry3D> _fullLines = new();
    private readonly Dictionary<BillboardTextModel3D, BillboardText3D> _fullText = new();
    private readonly Dictionary<MeshGeometryModel3D, MeshGeometry3D> _fullMeshes = new();
    /// <summary>For clipped meshes: original triangle index of every triangle (so picking still finds the source object).</summary>
    private readonly System.Runtime.CompilerServices.ConditionalWeakTable<MeshGeometry3D, List<int>> _triMap = new();
    private System.Windows.Threading.DispatcherTimer? _sectionTimer;
    private bool _sectionUpdating;

    /// <summary>Current cut plane in scene coordinates (origin-rebased): point and unit normal; the kept side is n·(x − p) ≥ 0.</summary>
    private (Vector3 p, Vector3 n)? _plane;

    /// <summary>Turns the section cut on at a support (used by the close-up snapshot and the explorer).</summary>
    public void SectionAtSupport(int index)
    {
        if (!_sectionOn) OnToggleSection(this, new RoutedEventArgs());
        SectionSupport.SelectedIndex = index + 1;
        _sectionTimer?.Stop();
        ApplySection();
    }

    /// <summary>Turns the section cut on at a station (snapshot option --section STA[,skew]).</summary>
    public void SectionAtStation(double station, double skewDeg)
    {
        if (!_sectionOn) OnToggleSection(this, new RoutedEventArgs());
        _sectionUpdating = true;
        SectionMode.SelectedIndex = 0;
        SectionStationSlider.Value = station;
        SectionStation.Text = _vm.Units.FormatStation(station);
        SectionSkew.Text = $"{skewDeg}°";
        _sectionUpdating = false;
        ApplySection();
    }

    /// <summary>Cut plane given directly in world coordinates (vertical plane through P, keeping the side N points to); set by
    /// close-ups of units on other alignments (station inputs refer to the first bridge's alignment). Cleared by any input edit.</summary>
    private (Vec3 p, Vec2 n)? _planeOverride;

    public void SectionAtWorldPlane(Vec3 p, Vec2 n)
    {
        if (!_sectionOn) OnToggleSection(this, new RoutedEventArgs());
        _sectionTimer?.Stop();
        _planeOverride = (p, n.Normalized());
        ApplySection();
    }

    private void OnToggleSection(object sender, RoutedEventArgs e)
    {
        _sectionOn = !_sectionOn;
        SectionPanel.Visibility = _sectionOn ? Visibility.Visible : Visibility.Collapsed;
        if (_sectionOn)
        {
            if (_planMode) SetPlanMode(false);
            InitSectionInputs();
        }
        ApplySection();
    }

    private void OnSectionClose(object sender, RoutedEventArgs e)
    {
        _sectionOn = false;
        _planeOverride = null;
        SectionPanel.Visibility = Visibility.Collapsed;
        ApplySection();
    }

    private SBM.Core.Framing.BridgeFraming? CurrentFraming() => _vm.Framings.Values.FirstOrDefault();

    private void InitSectionInputs()
    {
        var f = CurrentFraming();
        if (f == null) return;
        _sectionUpdating = true;
        var (b, e) = SBM.Core.Drawing.Views.LayoutViews.DeckEndStations(f);
        double pad = SBM.Core.Units.UnitSystem.Ft(30);
        SectionStationSlider.Minimum = b - pad;
        SectionStationSlider.Maximum = e + pad;
        var deck = f.Definition.Deck;
        SectionOffsetSlider.Minimum = deck.LeftEdgeOffset - pad;
        SectionOffsetSlider.Maximum = deck.RightEdgeOffset + pad;
        if (string.IsNullOrWhiteSpace(SectionStation.Text))
        {
            SectionStationSlider.Value = (f.Supports[0].Station + f.Supports[1].Station) / 2;
            SectionStation.Text = _vm.Units.FormatStation(SectionStationSlider.Value);
            SectionOffset.Text = _vm.Units.FormatLength(0);
            SectionSkew.Text = "0°";
            SectionMode.SelectedIndex = 0;
        }
        SectionSupport.ItemsSource = new[] { "(none)" }.Concat(f.Supports.Select(s => s.Name)).ToList();
        SectionSupport.SelectedIndex = 0;
        _sectionUpdating = false;
    }

    private void OnSectionSlider(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_sectionUpdating) return;
        _sectionUpdating = true;
        if (sender == SectionStationSlider) SectionStation.Text = _vm.Units.FormatStation(SectionStationSlider.Value);
        else SectionOffset.Text = _vm.Units.FormatLength(SectionOffsetSlider.Value);
        _sectionUpdating = false;
        ScheduleSection();
    }

    private void OnSectionText(object sender, RoutedEventArgs e)
    {
        if (_sectionUpdating) return;
        var u = _vm.Units;
        _sectionUpdating = true;
        if (ValueFormat.TryParse(u, ValueKind.Station, SectionStation.Text, out double sta)) SectionStationSlider.Value = sta;
        if (ValueFormat.TryParse(u, ValueKind.LengthLong, SectionOffset.Text, out double off)) SectionOffsetSlider.Value = off;
        _sectionUpdating = false;
        ScheduleSection();
    }

    private void OnSectionTextKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { OnSectionText(sender, e); e.Handled = true; }
    }

    private void OnSectionSupport(object sender, SelectionChangedEventArgs e)
    {
        if (_sectionUpdating || SectionSupport.SelectedIndex <= 0 || CurrentFraming() is not { } f) return;
        var sl = f.Supports[SectionSupport.SelectedIndex - 1];
        _sectionUpdating = true;
        SectionMode.SelectedIndex = 0;
        // Cut just beside the CL bearing along the skew and keep the support's side, so the bent (or abutment) and its
        // bearings are seen in elevation: up-station of piers and the begin abutment (looking down-station), down-station
        // of the end abutment (looking up-station).
        bool last = sl.Index == f.Supports.Count - 1;
        double sta = sl.Station + SBM.Core.Units.UnitSystem.Ft(sl.Type == SBM.Core.Bridge.SupportType.Pier ? 3 : 2.5) * (last ? -1 : 1);
        SectionStationSlider.Value = sta;
        SectionStation.Text = _vm.Units.FormatStation(sta);
        SectionSkew.Text = $"{sl.SkewDegrees:0.###}°";
        SectionFlip.IsChecked = !last;
        _sectionUpdating = false;
        ScheduleSection();
    }

    private void OnSectionChanged(object sender, RoutedEventArgs e)
    {
        if (!_sectionUpdating) ScheduleSection();
    }

    private void ScheduleSection()
    {
        _sectionTimer ??= new System.Windows.Threading.DispatcherTimer(TimeSpan.FromMilliseconds(40), System.Windows.Threading.DispatcherPriority.Render,
            (_, _) => { _sectionTimer!.Stop(); ApplySection(); }, Dispatcher);
        _planeOverride = null;
        _sectionTimer.Stop();
        _sectionTimer.Start();
    }

    /// <summary>Computes the cut plane from the inputs (alignment-local: station, offset, skew; Z vertical).</summary>
    private (Vector3 p, Vector3 n)? ComputePlane(out string info)
    {
        info = "";
        var f = CurrentFraming();
        if (!_sectionOn || f == null || _scene == null) return null;
        if (_planeOverride is { } ov)
        {
            info = "Section through the selected unit";
            return (new Vector3((float)(ov.p.X - _scene.Origin.X), (float)(ov.p.Y - _scene.Origin.Y), 0), new Vector3((float)ov.n.X, (float)ov.n.Y, 0));
        }
        var a = f.Alignment;
        var u = _vm.Units;
        double sta = SectionStationSlider.Value, off = SectionOffsetSlider.Value;
        ValueFormat.TryParse(u, ValueKind.Angle, SectionSkew.Text, out double skewDeg);
        if (double.IsNaN(skewDeg)) skewDeg = 0;
        var t = Vec2.FromAngle(a.AngleAt(sta));
        var rn = a.RightNormalAt(sta);
        Vec2 p, n;
        bool flip = SectionFlip.IsChecked == true;
        if (SectionMode.SelectedIndex == 1)
        {
            // Longitudinal: vertical plane through (station, offset) along the tangent; keep the left side (looking left).
            p = a.PointAt(sta, off);
            n = -rn;
            info = $"Longitudinal cut at offset {u.FormatLength(off)}, tangent at STA {u.FormatStation(sta)} — looking {(flip ? "right" : "left")}";
        }
        else
        {
            // Transverse: the radial line rotated by the skew; keep the up-station side (looking up-station).
            double k = skewDeg * Math.PI / 180;
            var d = rn * Math.Cos(k) + t * Math.Sin(k);
            p = a.PointAt(sta);
            n = d.Left;
            if (Vec2.Dot(n, t) < 0) n = -n;
            info = $"Cross section at STA {u.FormatStation(sta)}, skew {skewDeg:0.##}° — looking {(flip ? "down-station" : "up-station")}";
        }
        if (flip) n = -n;
        var po = new Vector3((float)(p.X - _scene.Origin.X), (float)(p.Y - _scene.Origin.Y), 0);
        return (po, new Vector3((float)n.X, (float)n.Y, 0));
    }

    /// <summary>Applies the cut: solids are clipped exactly at the plane (triangles split), instanced fasteners and text are
    /// kept by their origin, linework is clipped per segment.</summary>
    private void ApplySection()
    {
        _plane = ComputePlane(out string info);
        SectionInfo.Text = info;
        foreach (var model in AllModels()) ApplyToModel(model);
    }

    /// <summary>Re-derives one model's displayed geometry from its full geometry: section cut, hidden elements, near-view rebar LOD.</summary>
    private void ApplyToModel(Element3D model)
    {
        switch (model)
        {
            case MeshGeometryModel3D im when im.Tag is List<Matrix4x4> all:
            {
                var tags = im.Geometry is MeshGeometry3D ig && SceneBuilder.InstanceTags.TryGetValue(ig, out var t) ? t : null;
                bool lod = _lodSkip.Count > 0;
                bool iso = _iso.FiltersElements;
                var vis = Enumerable.Range(0, all.Count).Where(i => Kept(all[i].Translation)
                    && (tags == null || i >= tags.Count ? !iso : !TagHidden(tags[i]) && !(lod && _lodSkip.Contains(tags[i])))).ToList();
                _visibleInstances[im] = vis;
                im.Instances = vis.Count == all.Count ? all : vis.Select(i => all[i]).ToList();
                break;
            }
            case MeshGeometryModel3D mm when _fullMeshes.TryGetValue(mm, out var fullMesh):
            {
                var hiddenRanges = HiddenRanges(fullMesh, !_lodOverlays.Contains(mm));
                mm.Geometry = _plane == null && hiddenRanges.Count == 0 ? fullMesh : ClipMesh(fullMesh, hiddenRanges);
                break;
            }
            case LineGeometryModel3D lm when _fullLines.TryGetValue(lm, out var full):
                lm.Geometry = _plane == null ? full : ClipLines(full);
                break;
            case BillboardTextModel3D tm when _fullText.TryGetValue(tm, out var fullText):
                tm.Geometry = _plane == null ? fullText : ClipText(fullText);
                break;
        }
    }

    private IEnumerable<Element3D> AllModels() => _layerModels.Values.SelectMany(l => l).Concat(_refModels.Values.SelectMany(l => l));

    private bool Kept(Vector3 x) => _plane is not { } pl || Vector3.Dot(pl.n, x - pl.p) >= -1e-3f;

    private LineGeometry3D ClipLines(LineGeometry3D g)
    {
        var (pp, nn) = _plane!.Value;
        var pos = new Vector3Collection();
        var idx = new IntCollection();
        var src = g.Positions!;
        var ind = g.Indices!;
        for (int i = 0; i + 1 < ind.Count; i += 2)
        {
            var a = src[ind[i]];
            var b = src[ind[i + 1]];
            float da = Vector3.Dot(nn, a - pp), db = Vector3.Dot(nn, b - pp);
            if (da < 0 && db < 0) continue;
            if (da < 0) a = a + (b - a) * (da / (da - db));
            else if (db < 0) b = a + (b - a) * (da / (da - db));
            idx.Add(pos.Count); pos.Add(a);
            idx.Add(pos.Count); pos.Add(b);
        }
        var r = new LineGeometry3D { Positions = pos, Indices = idx };
        r.UpdateBounds();
        return r;
    }

    /// <summary>Triangle ranges [start, end) of hidden elements in a merged mesh.</summary>
    private List<(int start, int end)> HiddenRanges(MeshGeometry3D full, bool withLod = true)
    {
        var res = new List<(int, int)>();
        bool lod = withLod && _lodSkip.Count > 0;
        bool filter = ElementFilterOn;
        if (!filter && !lod) return res;
        int n = full.Indices!.Count / 3;
        if (!SceneBuilder.Picks.TryGetValue(full, out var table))
        {
            if (_iso.FiltersElements) res.Add((0, n)); // no pick objects: nothing in it can be isolated
            return res;
        }
        for (int i = 0; i < table.Tags.Count; i++)
            if ((filter && TagHidden(table.Tags[i])) || (lod && _lodSkip.Contains(table.Tags[i])))
                res.Add((table.TriStart[i], i + 1 < table.TriStart.Count ? table.TriStart[i + 1] : n));
        return res;
    }

    /// <summary>
    /// Copy of a flat-shaded triangle mesh without hidden elements and clipped to the kept side of the section plane
    /// (triangles crossing the plane are split exactly).
    /// </summary>
    private MeshGeometry3D ClipMesh(MeshGeometry3D g, List<(int start, int end)>? skip = null)
    {
        var (pp, nn) = _plane ?? (Vector3.Zero, Vector3.Zero);
        bool cut = _plane != null;
        int skipAt = 0;
        var src = g.Positions!;
        var nrm = g.Normals!;
        var ind = g.Indices!;
        var pos = new Vector3Collection(src.Count);
        var nor = new Vector3Collection(src.Count);
        var idx = new IntCollection(ind.Count);
        var map = new List<int>(ind.Count / 3);
        Span<Vector3> poly = stackalloc Vector3[4];
        Span<Vector3> polyN = stackalloc Vector3[4];
        for (int i = 0; i + 2 < ind.Count; i += 3)
        {
            if (skip is { Count: > 0 })
            {
                int tri = i / 3;
                while (skipAt < skip.Count && skip[skipAt].end <= tri) skipAt++;
                if (skipAt < skip.Count && tri >= skip[skipAt].start) continue;
            }
            var a = src[ind[i]];
            var b = src[ind[i + 1]];
            var c = src[ind[i + 2]];
            float da = cut ? Vector3.Dot(nn, a - pp) : 1, db = cut ? Vector3.Dot(nn, b - pp) : 1, dc = cut ? Vector3.Dot(nn, c - pp) : 1;
            var na = nrm[ind[i]];
            var nb = nrm[ind[i + 1]];
            var nc = nrm[ind[i + 2]];
            if (da >= 0 && db >= 0 && dc >= 0)
            {
                idx.Add(pos.Count); pos.Add(a); nor.Add(na);
                idx.Add(pos.Count); pos.Add(b); nor.Add(nb);
                idx.Add(pos.Count); pos.Add(c); nor.Add(nc);
                map.Add(i / 3);
                continue;
            }
            if (da < 0 && db < 0 && dc < 0) continue;
            // Sutherland–Hodgman against one plane: 3 or 4 vertices out.
            int k = 0;
            var v = new[] { a, b, c };
            var d = new[] { da, db, dc };
            var vn = new[] { na, nb, nc };
            for (int j = 0; j < 3; j++)
            {
                int j2 = (j + 1) % 3;
                if (d[j] >= 0) { polyN[k] = vn[j]; poly[k++] = v[j]; }
                if ((d[j] >= 0) != (d[j2] >= 0))
                {
                    float f = d[j] / (d[j] - d[j2]);
                    polyN[k] = Vector3.Normalize(vn[j] + (vn[j2] - vn[j]) * f);
                    poly[k++] = v[j] + (v[j2] - v[j]) * f;
                }
            }
            for (int j = 1; j + 1 < k; j++)
            {
                idx.Add(pos.Count); pos.Add(poly[0]); nor.Add(polyN[0]);
                idx.Add(pos.Count); pos.Add(poly[j]); nor.Add(polyN[j]);
                idx.Add(pos.Count); pos.Add(poly[j + 1]); nor.Add(polyN[j + 1]);
                map.Add(i / 3);
            }
        }
        var r = new MeshGeometry3D { Positions = pos, Normals = nor, Indices = idx };
        r.UpdateBounds();
        _triMap.AddOrUpdate(r, map);
        return r;
    }

    private BillboardText3D ClipText(BillboardText3D g)
    {
        var r = new BillboardText3D();
        foreach (var t in g.TextInfo)
            if (Kept(t.Origin)) r.TextInfo.Add(t);
        return r;
    }

    /// <summary>
    /// Solid models (plain meshes; Helix's cross-section models break the transparent pass). The full geometry is kept so
    /// section cuts can clip it on the CPU; instanced fasteners keep their full instance list in Tag.
    /// </summary>
    private MeshGeometryModel3D NewSolidModel(MeshGeometry3D mesh, PhongMaterial mat, bool transparent, List<Matrix4x4>? instances)
    {
        var m = new MeshGeometryModel3D { Geometry = mesh, Material = mat, IsHitTestVisible = true, IsTransparent = transparent };
        if (instances != null) { m.Instances = instances; m.Tag = instances; }
        else _fullMeshes[m] = mesh;
        return m;
    }

    // ================================================================== picking

    private Point? _downAt;
    private MeshGeometryModel3D? _highlight;

    private void OnViewportMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) _downAt = e.GetPosition(View);
    }

    private void OnViewportMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || _downAt is not { } d0) return;
        var pt = e.GetPosition(View);
        _downAt = null;
        if ((pt - d0).Length > 4) return; // a drag, not a click
        if (MeasureActive) { MeasureClick(pt); return; }
        Pick(pt);
    }

    private object? _selectedTag;
    private MeshGeometryModel3D? _selectedModel;
    private string _selectedTitle = "";
    private readonly HashSet<object> _hidden = new();
    private readonly List<(object tag, string label)> _hiddenList = new();
    private readonly Dictionary<MeshGeometryModel3D, List<int>> _visibleInstances = new();

    private void Pick(Point pt)
    {
        ClearHighlight();
        _selectedTag = null;
        _selectedModel = null;
        _groupSel = null;
        if (_planMode) { StatusText("Selection works in the 3D view (View ▸ 3D)."); return; }
        IList<HitTestResult> hits;
        try { hits = View.FindHits(pt); }
        catch { return; }
        foreach (var hit in hits.OrderBy(h => h.Distance))
        {
            if (hit.ModelHit is not MeshGeometryModel3D model || model.Visibility != Visibility.Visible || model == _highlight) continue;
            if (!Kept(hit.PointHit)) continue; // removed by the section cut
            if (model.Geometry is not MeshGeometry3D geo) continue;
            object? tag = null;
            MeshGeometry3D? hl = null;
            if (model.Instances is { Count: > 0 } inst && SceneBuilder.InstanceTags.TryGetValue(geo, out var itags))
            {
                // Nearest instance origin to the hit point (instances are small: bolts, studs, bars).
                var all = model.Tag as List<Matrix4x4> ?? inst.ToList();
                var candidates = _visibleInstances.TryGetValue(model, out var vis) ? vis : Enumerable.Range(0, all.Count).ToList();
                int best = -1;
                float bestD = float.MaxValue;
                foreach (int i in candidates)
                {
                    float dd = Vector3.DistanceSquared(all[i].Translation, hit.PointHit);
                    if (dd < bestD) { bestD = dd; best = i; }
                }
                if (best < 0 || best >= itags.Count) continue;
                // Refine: the instance whose transformed mesh is closest (origins can be off-center for long bars).
                tag = itags[best];
                hl = Transform(geo, all[best]);
            }
            else if (hit.TriangleIndices is { } tri)
            {
                // Clipped meshes map back to the full mesh (and its pick table).
                var full = _fullMeshes.TryGetValue(model, out var fm) ? fm : geo;
                int triangle = tri.Item1 / 3;
                if (!ReferenceEquals(full, geo) && _triMap.TryGetValue(geo, out var map) && triangle < map.Count) triangle = map[triangle];
                if (!SceneBuilder.Picks.TryGetValue(full, out var table)) continue;
                tag = table.Find(triangle, out int t0, out int t1);
                if (tag != null) hl = Extract(full, t0, Math.Min(t1, full.Indices!.Count / 3));
            }
            if (tag == null || TagHidden(tag)) continue; // hidden / outside the isolate: not pickable
            _selectedTitle = _vm.ShowPicked(tag);
            _selectedTag = tag;
            _selectedModel = model;
            if (hl != null) ShowHighlight(hl);
            DbOnPicked(tag, hit.PointHit);
            return;
        }
        _vm.SelectedProperties.Clear();
        DbOnCleared();
    }

    private void StatusText(string s) => _vm.StatusText = s;

    // ================================================================== context menu: hide / unhide

    private Point? _rightDownAt;

    private void OnViewportRightDown(object sender, MouseButtonEventArgs e) => _rightDownAt = e.GetPosition(View);

    private void OnViewportRightUp(object sender, MouseButtonEventArgs e)
    {
        if (_rightDownAt is not { } d0) return;
        var pt = e.GetPosition(View);
        _rightDownAt = null;
        if ((pt - d0).Length > 4) return; // right-drag orbits
        if (MeasureCancelInProgress()) { e.Handled = true; return; }
        var group = _groupSel;
        if (!_planMode)
        {
            Pick(pt);
            if (group is { } g && (_selectedTag == null || g.tags.Contains(_selectedTag)))
            {
                // Right-click on the highlighted database group (or on empty space) keeps the group as the selection.
                ClearSelection();
                _groupSel = group;
                ShowHighlight(g.geo);
            }
        }
        ShowViewportMenu();
        e.Handled = true;
    }

    private string? LayerOf(Element3D model) =>
        _layerModels.FirstOrDefault(kv => kv.Value.Contains(model)).Key ?? _refModels.FirstOrDefault(kv => kv.Value.Contains(model)).Key;

    private void ShowViewportMenu()
    {
        var menu = new ContextMenu();
        MenuItem Item(string header, Action act, string icon = "", bool enabled = true)
        {
            var mi = new MenuItem { Header = header, IsEnabled = enabled };
            if (icon.Length > 0) mi.Icon = new TextBlock { Text = icon, FontFamily = (System.Windows.Media.FontFamily)FindResource("IconFont") };
            mi.Click += (_, _) => act();
            return mi;
        }
        static string Short(string s) => s.Length > 48 ? s[..45] + "…" : s;
        static string Ic(int code) => ((char)code).ToString();
        if (_groupSel is { } grp)
        {
            var gi = Item($"Isolate {Short(grp.name)} ({grp.tags.Count} elements)", () => IsolateElements(grp.name, grp.tags), Ic(0xE71D));
            gi.InputGestureText = "I";
            menu.Items.Add(gi);
            menu.Items.Add(Item("Clear selection", ClearSelection));
        }
        else if (_selectedTag is { } tag && _selectedModel is { } model)
        {
            string title = _selectedTitle;
            string name = Short(title);
            menu.Items.Add(Item($"Hide {name}", () => HideElement(tag, title), Ic(0xED1A)));
            var layer = LayerOf(model);
            if (layer != null)
                menu.Items.Add(Item($"Hide layer {layer}", () => HideLayer(layer), Ic(0xE8D7)));
            var ie = Item($"Isolate {name}", () => IsolateElements(title, new[] { tag }), Ic(0xE71D));
            ie.InputGestureText = "I";
            menu.Items.Add(ie);
            if (layer != null)
            {
                var il = Item($"Isolate layer {layer}", () => IsolateLayer(layer), Ic(0xE81E));
                il.InputGestureText = "Shift+I";
                menu.Items.Add(il);
            }
            menu.Items.Add(Item("Clear selection", ClearSelection));
        }
        else menu.Items.Add(new MenuItem { Header = _planMode ? "Selection works in the 3D view" : "Nothing selected here", IsEnabled = false });
        if (_iso.Active)
        {
            menu.Items.Add(new Separator());
            var ex = Item($"Exit isolate ({Short(_iso.Name)})", ExitIsolate, Ic(0xE8BB));
            ex.InputGestureText = "Esc / I";
            menu.Items.Add(ex);
            menu.Items.Add(Item("Zoom to isolated", () => ZoomToIsolated(true), Ic(0xE71E)));
        }
        var zoomOpt = new MenuItem { Header = "Zoom to isolated when isolating", IsCheckable = true, IsChecked = _isoZoomOnIsolate };
        zoomOpt.Click += (_, _) => _isoZoomOnIsolate = zoomOpt.IsChecked;
        menu.Items.Add(zoomOpt);
        menu.Items.Add(new Separator());
        var unhide = new MenuItem { Header = $"Unhide ({_hiddenList.Count})", IsEnabled = _hiddenList.Count > 0 };
        foreach (var (htag, label) in _hiddenList.ToList())
            unhide.Items.Add(Item(label, () => UnhideElement(htag)));
        if (_hiddenList.Count > 1)
        {
            unhide.Items.Add(new Separator());
            unhide.Items.Add(Item("Unhide all", UnhideAll));
        }
        menu.Items.Add(unhide);
        var hiddenLayers = _vm.LayerItems.Where(l => !l.IsVisible).ToList();
        var layers = new MenuItem { Header = $"Show layer ({hiddenLayers.Count})", IsEnabled = hiddenLayers.Count > 0 };
        foreach (var l in hiddenLayers) layers.Items.Add(Item(l.Name, () => l.IsVisible = true));
        if (hiddenLayers.Count > 1)
        {
            layers.Items.Add(new Separator());
            layers.Items.Add(Item("Show all layers", ShowAllLayers));
        }
        menu.Items.Add(layers);
        menu.PlacementTarget = View;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    private void ShowAllLayers_Click(object sender, System.Windows.RoutedEventArgs e) => ShowAllLayers();

    private void ShowAllLayers()
    {
        _layerBatch = true;
        try { _vm.ShowAllLayers(); }
        finally { _layerBatch = false; }
        ApplyLayerVisibility();
        StatusText("All layers shown");
    }

    private void HideElement(object tag, string label)
    {
        if (!_hidden.Add(tag)) return;
        _hiddenList.Add((tag, label));
        ClearHighlight();
        _selectedTag = null;
        _vm.SelectedProperties.Clear();
        ApplySection();
        StatusText($"Hidden: {label} ({_hiddenList.Count} hidden — right-click ▸ Unhide)");
    }

    private void UnhideElement(object tag)
    {
        _hidden.Remove(tag);
        _hiddenList.RemoveAll(h => ReferenceEquals(h.tag, tag) || Equals(h.tag, tag));
        ApplySection();
    }

    private void UnhideAll()
    {
        _hidden.Clear();
        _hiddenList.Clear();
        ApplySection();
    }

    /// <summary>Stable identity of a pickable object across regenerations (marks, names, positions).</summary>
    private static string HideKey(object tag) => tag switch
    {
        SBM.Core.Steel.SteelPart p => "part:" + p.Mark,
        SBM.Core.Steel.BoltInstance b => $"bolt:{b.Assembly}:{Math.Round(b.Frame.Origin.X, 3)}:{Math.Round(b.Frame.Origin.Y, 3)}:{Math.Round(b.Frame.Origin.Z, 3)}",
        SBM.Core.Steel.StudInstance s => $"stud:{Math.Round(s.Frame.Origin.X, 3)}:{Math.Round(s.Frame.Origin.Y, 3)}",
        RebarPick r => $"bar:{r.Mark.Unit}:{r.Mark.Mark}:{Math.Round(r.Bar.Points[0].X, 3)}:{Math.Round(r.Bar.Points[0].Y, 3)}:{Math.Round(r.Bar.Points[0].Z, 3)}",
        SubPick sp => "sub:" + sp.Solid.Name,
        NamedPick np => "named:" + np.Name,
        PsBeamPick pb => "ps:" + pb.Beam.Mark,
        PsStrandPick st => $"strand:{st.Beam.Mark}:{Math.Round(st.Strand.Section.X, 4)}:{Math.Round(st.Strand.Section.Y, 4)}",
        PsPadPick pp => "pad:" + pp.Pad.Mark,
        _ => tag.ToString() ?? "",
    };

    /// <summary>After a regeneration, hidden elements are matched to the new objects by mark / name / position.</summary>
    private void RemapHidden(SceneData scene)
    {
        if (_hidden.Count == 0) return;
        var wanted = _hiddenList.Select(h => (key: HideKey(h.tag), h.label)).ToList();
        var fresh = new Dictionary<string, object>();
        foreach (var solid in scene.Solids)
            foreach (var (mesh, _) in solid.Parts)
            {
                if (SceneBuilder.Picks.TryGetValue(mesh, out var table)) foreach (var t in table.Tags) fresh.TryAdd(HideKey(t), t);
                if (SceneBuilder.InstanceTags.TryGetValue(mesh, out var it)) foreach (var t in it) fresh.TryAdd(HideKey(t), t);
            }
        _hidden.Clear();
        _hiddenList.Clear();
        foreach (var (key, label) in wanted)
            if (fresh.TryGetValue(key, out var t) && _hidden.Add(t)) _hiddenList.Add((t, label));
    }

    private void HideLayer(string layer)
    {
        if (_vm.LayerItems.FirstOrDefault(l => l.Name == layer) is { } item) item.IsVisible = false;
        ClearHighlight();
        _selectedTag = null;
        StatusText($"Layer {layer} hidden — right-click ▸ Show layer, or tick it in the Layers panel");
    }

    private static MeshGeometry3D Extract(MeshGeometry3D g, int t0, int t1)
    {
        var pos = new Vector3Collection();
        var nrm = new Vector3Collection();
        var idx = new IntCollection();
        for (int t = t0; t < t1; t++)
            for (int k = 0; k < 3; k++)
            {
                int v = g.Indices![3 * t + k];
                idx.Add(pos.Count);
                pos.Add(g.Positions![v]);
                nrm.Add(g.Normals![v]);
            }
        var r = new MeshGeometry3D { Positions = pos, Normals = nrm, Indices = idx };
        r.UpdateBounds();
        return r;
    }

    private static MeshGeometry3D Transform(MeshGeometry3D g, Matrix4x4 m)
    {
        var pos = new Vector3Collection(g.Positions!.Select(p => Vector3.Transform(p, m)));
        var nrm = new Vector3Collection(g.Normals!.Select(n => Vector3.Normalize(Vector3.TransformNormal(n, m))));
        var r = new MeshGeometry3D { Positions = pos, Normals = nrm, Indices = new IntCollection(g.Indices!) };
        r.UpdateBounds();
        return r;
    }

    private void ShowHighlight(MeshGeometry3D g)
    {
        _highlight = new MeshGeometryModel3D
        {
            Geometry = g, IsHitTestVisible = false, IsTransparent = true, DepthBias = -200,
            Material = new PhongMaterial
            {
                DiffuseColor = new HelixToolkit.Maths.Color4(1f, 0.85f, 0.1f, 0.85f),
                EmissiveColor = new HelixToolkit.Maths.Color4(0.6f, 0.45f, 0f, 1f),
                AmbientColor = new HelixToolkit.Maths.Color4(0.3f, 0.3f, 0.3f, 1f),
            },
        };
        View.Items.Add(_highlight);
    }

    private void ClearHighlight()
    {
        if (_highlight != null) View.Items.Remove(_highlight);
        _highlight = null;
    }
}
