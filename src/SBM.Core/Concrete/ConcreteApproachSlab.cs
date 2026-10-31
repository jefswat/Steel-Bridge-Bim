using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Units;

namespace SBM.Core.Concrete;

/// <summary>An approach slab (and its sleeper slab) beyond a deck end (<see cref="ApproachSlabDefinition"/>).</summary>
public sealed class ApproachSlabResult
{
    public required string Support { get; init; }
    public required bool AtBegin { get; init; }
    public required ApproachSlabDefinition Definition { get; init; }
    public required Mesh Mesh { get; init; }
    public Mesh? Sleeper { get; init; }
    /// <summary>Flag when the ownership / length measure is not given (IFC SBM_Status, database status).</summary>
    public Assumption? Assumption { get; init; }
    /// <summary>Stations of the slab along the bridge alignment (min, max), for matching it to a road.</summary>
    public double StationMin { get; init; }
    public double StationMax { get; init; }
    public string Name => $"Approach slab at {Support}";
    public double Volume => Math.Abs(Mesh.Volume());
    public double SleeperVolume => Sleeper == null ? 0 : Math.Abs(Sleeper.Volume());
}

public sealed partial class ConcreteGenerator
{
    /// <summary>Approach slabs on the first / last support: rows every &lt;= 1.5 m along the slab so its top follows the profile and the plan curve.</summary>
    private void BuildApproachSlabs()
    {
        var res = new List<ApproachSlabResult>();
        foreach (bool begin in new[] { true, false })
        {
            var sup = _def.Supports[begin ? 0 : _def.Supports.Count - 1];
            if (sup.ApproachSlab is not { } a) continue;
            string where = $"Approach slab at {sup.Name}";
            if (!(a.Length > 1e-6) || !(a.Thickness > 1e-6)) { Warn($"{where}: length and thickness must be given - not built."); continue; }
            var flags = new List<string>();
            if (a.Owner == null) flags.Add(UnresolvedFlag.Text(a.Source, $"{where} ownership", "the inputs do not say whether it belongs to the bridge or to the road", "kept with the bridge."));
            if (a.LengthMeasure == null) flags.Add(UnresolvedFlag.Text(a.Source, $"{where} length measure", "the inputs do not say whether the length is along the alignment or normal to the deck end", "measured along the alignment."));
            if (a.Unresolved.Length > 0) flags.Add(a.Unresolved);
            foreach (var w in flags) Warn(w);
            bool normal = a.LengthMeasure == ApproachSlabLengthMeasure.NormalToDeckEnd;
            var cols = JointColumns(a.EdgeInsetLeft, a.EdgeInsetRight);
            // distance along the alignment of a point x beyond the deck end (x >= 0 outward) on column k
            double[] dEnd = cols.Select(c => EndD(c, begin, -a.Gap)).ToArray();
            double D(int k, double x) => normal ? EndD(cols[k], begin, -(a.Gap + x)) : dEnd[k] + (begin ? -x : x);
            Mesh Block(double x0, double x1, Func<double, double, double> topDrop, Func<double, double, double> botDrop)
            {
                int ni = Math.Max(1, (int)Math.Ceiling((x1 - x0) / 1.5)) + 1;
                var top = new Vec3[ni, cols.Count]; var bot = new Vec3[ni, cols.Count];
                for (int i = 0; i < ni; i++)
                    for (int k = 0; k < cols.Count; k++)
                    {
                        double x = x0 + (x1 - x0) * i / (ni - 1), d = D(k, x), o = cols[k](d), z = Top(d, o);
                        var p = _a.PointAtDistance(d, o);
                        top[i, k] = p.WithZ(z - topDrop(d, o)); bot[i, k] = p.WithZ(z - botDrop(d, o));
                    }
                return GridSolid(top, bot);
            }
            var slab = Block(0, a.Length, (_, _) => 0, (_, _) => a.Thickness);
            Mesh? sleeper = null;
            if (a.Sleeper is { } sl)
            {
                if (!(sl.Width > 1e-6) || !(sl.Depth > 1e-6)) Warn($"{where}: sleeper slab width and depth must be given - sleeper not built.");
                else sleeper = Block(a.Length + sl.BeyondEnd - sl.Width, a.Length + sl.BeyondEnd, (_, _) => a.Thickness, (_, _) => a.Thickness + sl.Depth);
            }
            var stas = slab.Positions.Select(p => _a.Inverse(p.XY).Station).ToList();
            res.Add(new ApproachSlabResult
            {
                Support = sup.Name, AtBegin = begin, Definition = a, Mesh = slab, Sleeper = sleeper, StationMin = stas.Min(), StationMax = stas.Max(),
                Assumption = flags.Count > 0 ? Assumption.Unresolved(string.Join(" ", flags), "") : null,
            });
            lock (_m.Diagnostics) _m.Diagnostics.Add(new(DiagnosticSeverity.Info, $"{where}: {Math.Abs(slab.Volume()) / 0.764554858:0.0} CY" +
                (sleeper != null ? $", sleeper slab {Math.Abs(sleeper.Volume()) / 0.764554858:0.0} CY" : "") + $", owner {a.Owner?.ToString() ?? "UNRESOLVED (bridge)"}."));
        }
        if (res.Count > 0) lock (_m.ApproachSlabs) _m.ApproachSlabs.AddRange(res);
    }
}
