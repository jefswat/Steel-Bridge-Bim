using System.Windows;
using SBM.Core.Drawing;
using SBM.Core.Drawing.Projection;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.App;

/// <summary>
/// Click views (ribbon View &gt; 2D projection &gt; Click Views): while the window is open, every pick in the model view projects three views at
/// the picked point with the model projector - PLAN (horizontal cut at the picked elevation, looking down, centred on the point), ELEVATION (looking from the right of the nearest alignment
/// toward it, stations increasing to the right) and SECTION (normal to the alignment through the point, looking up-station, cut at the point,
/// showing a chosen depth beyond). Only visible layers and unhidden / isolated elements are drawn. Closing the window turns it off.
/// The projection items and their plan boxes are cached per scene; each click projects only the items near the point.
/// </summary>
public partial class MainWindow
{
    private Editors.ClickViewsWindow? _clickViews;
    private List<(ProjectionItem item, Box3 box)>? _clickItems;
    private int _clickItemsScene = -1, _clickScenes, _clickRun;
    private Vec3? _clickLast;
    private Task? _clickTask;

    private void InitClickViews() => _vm.SceneReady += (_, _) => { _clickScenes++; _clickItems = null; };

    private void OnClickViews(object sender, RoutedEventArgs e) => OpenClickViews();

    private void OpenClickViews()
    {
        if (_clickViews != null) { _clickViews.Activate(); return; }
        _clickViews = new Editors.ClickViewsWindow(_vm.Units) { Owner = this };
        _clickViews.Closed += (_, _) => { _clickViews = null; StatusText("Click views: off"); };
        _clickViews.OptionsChanged += () => { if (_clickLast is { } p) _ = ClickViewsAtAsync(p); };
        _clickViews.Show();
        StatusText("Click views: on - click an element in the model view (close the window to turn it off)");
        if (_clickLast is { } q) _ = ClickViewsAtAsync(q);
    }

    /// <summary>Called by a pick in the viewport (local hit point).</summary>
    private void ClickViewsOnPicked(System.Numerics.Vector3 hitLocal)
    {
        if (_clickViews == null || _scene == null) return;
        var w = new Vec3(hitLocal.X, hitLocal.Y, hitLocal.Z) + _scene.Origin;
        _clickLast = w;
        _clickTask = ClickViewsAtAsync(w);
    }

    private List<(ProjectionItem item, Box3 box)> ClickItems()
    {
        if (_clickItems != null && _clickItemsScene == _clickScenes) return _clickItems;
        var all = new List<ProjectionItem>();
        foreach (var b in _vm.Project.Bridges)
        {
            if (!_vm.Framings.ContainsKey(b)) continue;
            all.AddRange(ModelProjectionSource.Items(b.Name, _vm.SteelModels.GetValueOrDefault(b), _vm.ConcreteModels.GetValueOrDefault(b),
                _vm.SubstructureModels.GetValueOrDefault(b), _vm.PrestressedModels.GetValueOrDefault(b), new ProjectionSourceOptions()));
        }
        var list = all.AsParallel().AsOrdered().Select(it =>
        {
            var box = it.Mesh?.Bounds() ?? Box3.Empty;
            if (it.Lines != null) foreach (var l in it.Lines) foreach (var p in l) box.Add(p);
            return (it, box);
        }).Where(x => !x.box.IsEmpty).ToList();
        _clickItemsScene = _clickScenes;
        return _clickItems = list;
    }

    /// <summary>Nearest alignment to a plan point (smallest |offset| among the alignments whose station range contains it), its station and offset.</summary>
    private (SBM.Core.Alignments.Alignment al, double sta, double off)? NearestAlignment(Vec2 p)
    {
        (SBM.Core.Alignments.Alignment, double, double)? best = null;
        double bestOff = double.MaxValue;
        foreach (var a in _vm.Project.Alignments)
        {
            var so = a.Inverse(p);
            double d = Math.Abs(so.Offset) + (so.OnAlignment ? 0 : 1e6);
            if (d < bestOff) { bestOff = d; best = (a, so.Station, so.Offset); }
        }
        return best;
    }

    private async Task ClickViewsAtAsync(Vec3 p)
    {
        var win = _clickViews;
        if (win == null) return;
        int run = ++_clickRun;
        var u = _vm.Units;
        double R = win.HalfWidth, beyond = win.Beyond;
        bool hidden = win.HiddenLines, planCut = win.PlanCut;
        // view axes: tangent / right normal of the nearest alignment (none: east / south)
        var near = NearestAlignment(p.XY);
        Vec2 t2 = new(1, 0);
        string where = $"E {u.FormatLength(p.X, SBM.Core.Units.LengthKind.Coordinate)}, N {u.FormatLength(p.Y, SBM.Core.Units.LengthKind.Coordinate)}, EL {u.FormatLength(p.Z, SBM.Core.Units.LengthKind.Coordinate)}";
        if (near is { } n)
        {
            var rn = n.al.RightNormalAt(n.sta);
            t2 = new Vec2(-rn.Y, rn.X); // tangent = right normal turned 90 deg to the left
            where = $"{n.al.Name}: STA {u.FormatStation(n.sta)}, offset {u.FormatLength(Math.Abs(n.off))} {(n.off >= 0 ? "RT" : "LT")}\n" + where;
        }
        var t = new Vec3(t2.X, t2.Y, 0);
        var right = new Vec3(t2.Y, -t2.X, 0);
        win.ShowBusy($"Projecting at {where.Replace("\n", " · ")} …");

        var visible = new Func<ProjectionItem, bool>(it => _vm.IsLayerVisible(it.Layer) && (it.Source == null || !TagHidden(it.Source)));
        var pool = ClickItems().Where(x => visible(x.item)).ToList();
        var res = await Task.Run(() =>
        {
            // plan box around the point (elevation / section windows are within it; the elevation also sees the whole width)
            double r2 = 2 * R;
            bool Near(Box3 b, double r) => b.Max.X >= p.X - r && b.Min.X <= p.X + r && b.Max.Y >= p.Y - r && b.Min.Y <= p.Y + r;
            var planItems = pool.Where(x => Near(x.box, R * 1.42)).Select(x => x.item).ToList();
            var wide = pool.Where(x => Near(x.box, r2)).ToList();
            var wideItems = wide.Select(x => x.item).ToList();
            var tall = new Box2 { MinX = -R, MaxX = R, MinY = -1000, MaxY = 1000 };
            var views = new (IViewMap view, List<ProjectionItem> items, Box2 window)[]
            {
                (new OrthographicView(p, new Vec3(0, 0, -1), new Vec3(0, 1, 0)) { Near = planCut ? 0 : null }, planItems, new Box2 { MinX = -R, MaxX = R, MinY = -R, MaxY = R }),
                (new OrthographicView(p, -right, new Vec3(0, 0, 1)), wide.Where(x => Along(x.box, p, t, R)).Select(x => x.item).ToList(), tall),
                (new OrthographicView(p, t, new Vec3(0, 0, 1)) { Near = 0, Far = Math.Max(beyond, 1e-4) }, wideItems, new Box2 { MinX = -r2, MaxX = r2, MinY = -1000, MaxY = 1000 }),
            };
            var outp = new ProjectionResult?[3];
            var notes = new string[3];
            Parallel.For(0, 3, i =>
            {
                var (view, items, window) = views[i];
                if (items.Count == 0) { notes[i] = "nothing here"; return; }
                long tris = items.Sum(x => (long)(x.Mesh?.TriangleCount ?? 0));
                var o = new ProjectionOptions { Method = tris > 3_000_000 ? HlrMethod.DepthBuffer : HlrMethod.Exact, Window = window,
                    Hidden = hidden ? SBM.Core.Drawing.Projection.HiddenLines.Show : SBM.Core.Drawing.Projection.HiddenLines.None };
                var r = ModelProjector.Project(items, view, o);
                Mark(r.Drawing, i, R, t2);
                outp[i] = r;
                notes[i] = (i == 0 ? (planCut ? "cut at EL of the point, " : "top view, ") : i == 2 ? (beyond > 0 ? $"cut + {_vm.Units.FormatLength(beyond)} beyond, " : "cut only, ") : "") + $"{items.Count:N0} elements";
            });
            return (outp, notes);
        });
        if (run != _clickRun || _clickViews != win) return; // a newer click or the window closed
        win.ShowViews(res.outp, res.notes, where);
        ViewModels.MainViewModel.Log($"CLICKVIEWS: {where.Replace("\n", " | ")} | " + string.Join(" | ", res.outp.Select((r, i) => $"{i}: {res.notes[i]} {r?.Stats}")));
    }

    /// <summary>Elevation candidates: the element's box reaches within the window along the alignment tangent.</summary>
    private static bool Along(Box3 b, Vec3 p, Vec3 t, double R)
    {
        double lo = double.MaxValue, hi = double.MinValue;
        foreach (var x in new[] { b.Min.X, b.Max.X })
            foreach (var y in new[] { b.Min.Y, b.Max.Y })
            {
                double s = (x - p.X) * t.X + (y - p.Y) * t.Y;
                lo = Math.Min(lo, s); hi = Math.Max(hi, s);
            }
        return hi >= -R && lo <= R;
    }

    private static readonly LayerStyle ClickMark = new("CLICK-POINT", 255, 60, 60, 1, LineStyle.Center, 1.5);

    /// <summary>Marks the clicked point: plan = cross + the section line across the alignment; elevation = vertical line at the point; section = cross.</summary>
    private static void Mark(PlanDrawing d, int view, double R, Vec2 t)
    {
        var l = d.Layer(ClickMark);
        double c = R / 25;
        l.Line(new Vec3(-c, 0, 0), new Vec3(c, 0, 0));
        l.Line(new Vec3(0, -c, 0), new Vec3(0, c, 0));
        if (view == 0) l.Line(new Vec3(-t.Y * R, t.X * R, 0), new Vec3(t.Y * R, -t.X * R, 0)); // section line (along the normal)
        if (view == 1)
        {
            var e = d.Extents();
            if (!e.IsEmpty) l.Line(new Vec3(0, e.MinY, 0), new Vec3(0, e.MaxY, 0));
        }
    }
}
