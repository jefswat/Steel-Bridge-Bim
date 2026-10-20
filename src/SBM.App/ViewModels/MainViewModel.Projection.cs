using System.Diagnostics;
using System.Runtime.CompilerServices;
using SBM.Core.Drawing;
using SBM.Core.Drawing.Projection;
using SBM.Core.Project;

namespace SBM.App.ViewModels;

/// <summary>
/// Projected plan (model): the plan view's top view projected from the 3D solids (<see cref="ModelProjector"/>). Always drawn for bridges
/// without framing-plan linework (girder count 0: tied arch, segmental box); for every bridge when <see cref="ProjectedPlanAll"/> is on.
/// Layers are the 3D layer names prefixed "PLAN-" (plan view only), lines keep world elevations (Z) and their element tags.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>Ribbon toggle "Projected plan (model)": project every bridge into the plan view (framing linework stays).</summary>
    public bool ProjectedPlanAll { get; set; }

    public const string PlanPrefix = "PLAN-";

    private static readonly ConditionalWeakTable<BridgeModel, PlanDrawing> _projectedPlans = new();

    /// <summary>Last projection timings (status / log).</summary>
    public static string LastProjectionInfo { get; private set; } = "";

    private static void AddProjectedPlans(PlanDrawing merged, IReadOnlyList<BridgeModel?> models, bool all)
    {
        var info = new List<string>();
        foreach (var m in models)
        {
            if (m == null || m.Framing.HasErrors) continue;
            bool noFraming = m.Framing.Girders.Count == 0;
            if (!noFraming && !all) continue;
            if (!_projectedPlans.TryGetValue(m, out var plan))
            {
                var sw = Stopwatch.StartNew();
                plan = ProjectPlan(m, out string how);
                _projectedPlans.AddOrUpdate(m, plan);
                info.Add($"{m.Definition.Name}: {how}");
                Log($"PROJECTED PLAN {m.Definition.Name}: {how} ({sw.ElapsedMilliseconds} ms)");
            }
            foreach (var l in plan.Layers)
            {
                var t = merged.Layer(l.Style);
                t.Polylines.AddRange(l.Polylines);
            }
        }
        if (info.Count > 0) LastProjectionInfo = string.Join("; ", info);
    }

    /// <summary>Top view of one bridge for the plan view: deck / barriers drawn but transparent (a framing plan shows the steel under the deck);
    /// exact hidden lines for small models, the depth buffer (no bolt holes) for large ones.</summary>
    public static PlanDrawing ProjectPlan(BridgeModel m, out string how)
    {
        var items = ModelProjectionSource.Items(m, new ProjectionSourceOptions { Transparent = new[] { "3D-DECK", "3D-BARRIER" }, Layers = new[] { "-3D-ARCH-FORM-DECK" } });
        long tris = items.Sum(i => (long)(i.Mesh?.TriangleCount ?? 0));
        bool big = tris > 3_000_000;
        var o = new ProjectionOptions { KeepDepth = true, Method = big ? HlrMethod.DepthBuffer : HlrMethod.Exact, Holes = !big };
        var r = ModelProjector.Project(items, OrthographicView.Top(0), o);
        r.DropLayers("3D-DECK-HIDDEN", "3D-BARRIER-HIDDEN");
        how = $"{(big ? "depth buffer" : "exact")} HLR, {r.Stats}";
        var dwg = new PlanDrawing();
        foreach (var l in r.Drawing.Layers)
        {
            var s = l.Style;
            var t = dwg.Layer(s with { Name = PlanPrefix + s.Name.Replace("3D-", "") });
            t.Polylines.AddRange(l.Polylines);
        }
        return dwg;
    }

    /// <summary>Re-runs the regeneration (cached models) so the plan view picks up the projected-plan toggle.</summary>
    public Task RefreshProjectedPlanAsync() => RegenerateAsync(false);
}
