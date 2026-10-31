using SBM.Core.Checks;
using SBM.Core.Checks.Measure;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Solids;

namespace SBM.Core.Utility;

/// <summary>A solid a utility may clash with: bridge steel / concrete / substructure, a retaining wall element, a road course.</summary>
public sealed record UtilityObstacle(string Kind, string Name, Mesh Mesh)
{
    /// <summary>Bridge name for bridge solids ("" otherwise): a pipe hung from that bridge is not checked against it for clearance.</summary>
    public string Bridge { get; init; } = "";
}

/// <summary>One clash (overlap deeper than the tolerance) or clearance shortfall of a utility element.</summary>
public sealed record UtilityClash(string Network, string Element, string OtherKind, string Other, bool Overlap, double Value, double Required, Vec3 At)
{
    /// <summary>"CLASH" (solids overlap by <see cref="Value"/>) or "CLEARANCE" (distance <see cref="Value"/> less than <see cref="Required"/>).</summary>
    public string Kind => Overlap ? "CLASH" : "CLEARANCE";
}

/// <summary>
/// Clash and clearance check of the utility networks (design note section 4.2; nothing is moved). Clashes: the existing solid interference engine
/// (<see cref="SteelClashCheck"/>, 1/16" contact tolerance) between every utility structure / pipe and the bridges' solids, the retaining walls, the road
/// pavement courses (pipes only: a structure passes through the pavement by design) and the other networks' elements; a pipe and the structures it
/// connects, and pipes sharing a structure, are not tested. Clearances only where the plans give a value: <see cref="UtilityNetworkDefinition.MinClearance"/>
/// to structures / walls / roads, <see cref="UtilityNetworkDefinition.MinSeparation"/> to other utilities (the larger of the two networks' values); none
/// given = only overlaps are reported. A pipe hung from a bridge (<see cref="UtilityPipeDefinition.Hangers"/>) is not clearance-checked against that bridge.
/// </summary>
public static class UtilityClashCheck
{
    /// <summary>The obstacles of the generated bridges, walls and roads.</summary>
    public static List<UtilityObstacle> Obstacles(IEnumerable<BridgeModel?>? bridges, IEnumerable<Wall.RetainingWallModel>? walls, IEnumerable<Road.RoadModel>? roads)
    {
        var res = new List<UtilityObstacle>();
        foreach (var b in bridges ?? Enumerable.Empty<BridgeModel?>())
        {
            if (b == null) continue;
            foreach (var (n, m, sub) in Road.RoadClearanceCheck.Solids(b))
                if (!m.IsEmpty) res.Add(new UtilityObstacle(sub ? "substructure" : "bridge", $"{b.Definition.Name} {n}", m) { Bridge = b.Definition.Name });
        }
        foreach (var w in walls ?? Enumerable.Empty<Wall.RetainingWallModel>())
        {
            foreach (var p in w.Panels)
            {
                foreach (var m in p.Stems) res.Add(new UtilityObstacle("wall", $"{w.Definition.Name} stem {p.Index}", m));
                foreach (var m in p.Footings) res.Add(new UtilityObstacle("wall", $"{w.Definition.Name} footing {p.Index}", m));
            }
            foreach (var e in w.Elements)
                foreach (var m in e.Solids) res.Add(new UtilityObstacle(e.Role == Wall.WallElementRole.ReinforcedZone ? "wall reinforced zone" : "wall", e.Name, m));
        }
        foreach (var r in roads ?? Enumerable.Empty<Road.RoadModel>())
            foreach (var p in r.Parts.Where(p => p.Subgrade == null))
                foreach (var m in p.Pieces ?? new List<Mesh> { p.Mesh })
                    if (!m.IsEmpty) res.Add(new UtilityObstacle("road", $"{r.Definition.Name} {p.Name}", m));
        return res;
    }

    private sealed record El(UtilityModel Net, UtilityStructure? S, UtilityPipe? P, string Name, Mesh Mesh, UtilityObstacle? Obstacle, bool Hanger = false)
    {
        public UtilityDuctBank? Bank { get; init; }
    }

    /// <summary>Runs the check, fills <see cref="UtilityModel.Clashes"/> and adds one diagnostic per result (+ a summary per network).</summary>
    public static void Run(IReadOnlyList<UtilityModel> networks, IReadOnlyList<UtilityObstacle> obstacles, Units.UnitSystem? units = null)
    {
        var els = new List<El>();
        foreach (var n in networks)
        {
            n.Clashes.Clear();
            foreach (var s in n.Structures.Where(s => s.IsBuilt)) els.Add(new El(n, s, null, s.Definition.Name, s.Mesh!, null));
            foreach (var p in n.Pipes.Where(p => p.IsBuilt)) els.Add(new El(n, null, p, p.Definition.Name, p.Mesh!, null));
            foreach (var h in n.Hangers.Where(h => !h.Mesh.IsEmpty)) els.Add(new El(n, null, h.Pipe, h.Name, h.Mesh, null, true));
            foreach (var b in n.DuctBanks) els.Add(new El(n, null, null, b.Definition.Name, b.Encasement, null) { Bank = b });
        }
        if (els.Count == 0) return;
        int nu = els.Count;
        // obstacles near any utility only (bounding boxes grown by the largest clearance asked for)
        double reach = networks.SelectMany(n => new[] { n.Definition.MinClearance, n.Definition.MinSeparation }).Where(v => !double.IsNaN(v)).DefaultIfEmpty(0).Max() + 0.01;
        var ub = els.Select(e => e.Mesh.Bounds()).ToList();
        var all = Box3.Empty; foreach (var b in ub) { all.Add(b.Min); all.Add(b.Max); }
        foreach (var o in obstacles)
        {
            var b = o.Mesh.Bounds();
            if (Geo3.BoxDistance(b, all) > reach) continue;
            if (!ub.Any(x => Geo3.BoxDistance(x, b) <= reach)) continue;
            els.Add(new El(networks[0], null, null, o.Name, o.Mesh, o));
        }
        var items = els.Select((e, i) => new ClashItem(e.Obstacle?.Kind ?? "utility", i.ToString(), "", e.Name, e.Mesh)).ToList();
        int Ix(ClashItem c) => int.Parse(c.Mark);

        bool Pair(El a, El b, out double required)
        {
            required = double.NaN;
            if (a.Obstacle != null && b.Obstacle != null) return false;
            if (a.Obstacle != null || b.Obstacle != null)
            {
                var u = a.Obstacle == null ? a : b; var o = a.Obstacle ?? b.Obstacle!;
                if (u.S != null && o.Kind == "road") return false;   // a structure passes through the pavement by design
                bool hungHere = u.P?.Definition.Hangers is { } hd && o.Bridge.Length > 0 && string.Equals(hd.Bridge, o.Bridge, StringComparison.OrdinalIgnoreCase);
                if (u.Hanger && hungHere) return false;   // a hanger rod on its own bridge
                if (!hungHere) required = u.Net.Definition.MinClearance;
                return true;
            }
            if (a.Net == b.Net)
            {
                var sa = Nodes(a); var sb = Nodes(b);
                if (a.P != null && a.P == b.P) return false;          // a pipe and its own hangers
                if (sa.Overlaps(sb)) return false;                    // connected (pipe - its structure, pipes sharing a structure)
                return true;                                          // same network, not connected: overlap only
            }
            double ra = a.Net.Definition.MinSeparation, rb = b.Net.Definition.MinSeparation;
            required = double.IsNaN(ra) ? rb : double.IsNaN(rb) ? ra : Math.Max(ra, rb);
            return true;
        }

        var opt = new ClashOptions();
        var rep = SteelClashCheck.Run("utilities", items, opt, (x, y) => Pair(els[Ix(x)], els[Ix(y)], out _));
        var clashed = new HashSet<(int, int)>();
        var results = new List<(El U, El O, UtilityClash C)>();
        foreach (var c in rep.Clashes)
        {
            int i = Ix(c.A), j = Ix(c.B);
            clashed.Add((Math.Min(i, j), Math.Max(i, j)));
            var (u, o) = els[i].Obstacle == null ? (els[i], els[j]) : (els[j], els[i]);
            Pair(u, o, out double req);
            results.Add((u, o, new UtilityClash(u.Net.Definition.Name, u.Name, OtherKind(o), OtherName(o), true, c.Depth, req, c.At)));
        }
        // clearances where the plans give a value
        for (int i = 0; i < nu; i++)
            for (int j = i + 1; j < els.Count; j++)
            {
                if (clashed.Contains((i, j))) continue;
                if (!Pair(els[i], els[j], out double req) || double.IsNaN(req)) continue;
                var bi = els[i].Mesh.Bounds(); var bj = els[j].Mesh.Bounds();
                if (Geo3.BoxDistance(bi, bj) >= req) continue;
                var (pa, pb, d) = MeasureQueries.MinDistance(els[i].Mesh, els[j].Mesh);
                if (d >= req - 1e-6) continue;
                results.Add((els[i], els[j], new UtilityClash(els[i].Net.Definition.Name, els[i].Name, OtherKind(els[j]), OtherName(els[j]), false, d, req, (pa + pb) * 0.5)));
            }
        string L(double m) => units?.FormatLength(m) ?? $"{m:0.###} m";
        foreach (var (u, o, c) in results.OrderBy(r => r.C.Network).ThenBy(r => r.C.Element).ThenBy(r => r.C.Other))
        {
            u.Net.Clashes.Add(c);
            string where = $"at {c.At.X:0.##}, {c.At.Y:0.##}, el. {c.At.Z:0.###}";
            u.Net.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, c.Overlap
                ? $"Network {c.Network}: CLASH {c.Element} x {c.OtherKind} {c.Other}: overlap {L(c.Value)} {where} (reported, not moved)."
                : $"Network {c.Network}: CONFLICT (clearance) {c.Element} x {c.OtherKind} {c.Other}: {L(c.Value)} clear, required {L(c.Required)} {where} (reported, not moved)."));
            // the other network sees it too
            if (o.Obstacle == null && o.Net != u.Net) o.Net.Clashes.Add(c with { Network = o.Net.Definition.Name, Element = o.Name, OtherKind = $"utility ({u.Net.Definition.Name})", Other = u.Name });
        }
        foreach (var n in networks)
        {
            var d = n.Definition;
            string req = (double.IsNaN(d.MinClearance) ? "no clearance to structures given" : $"clearance {L(d.MinClearance)} to structures")
                + ", " + (double.IsNaN(d.MinSeparation) ? "no separation to other utilities given" : $"separation {L(d.MinSeparation)} to other utilities");
            n.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info,
                $"Network {d.Name}: clash check against {els.Count - nu} nearby solids and the other networks: {n.Clashes.Count(c => c.Overlap)} clashes, {n.Clashes.Count(c => !c.Overlap)} clearance conflicts ({req})."));
        }
    }

    private static HashSet<UtilityStructure> Nodes(El e)
    {
        var h = new HashSet<UtilityStructure>();
        if (e.S != null) h.Add(e.S);
        if (e.P?.FromStructure != null) h.Add(e.P.FromStructure);
        if (e.P?.ToStructure != null) h.Add(e.P.ToStructure);
        if (e.Bank?.FromStructure != null) h.Add(e.Bank.FromStructure);
        if (e.Bank?.ToStructure != null) h.Add(e.Bank.ToStructure);
        return h;
    }

    private static string OtherKind(El e) => e.Obstacle?.Kind ?? $"utility ({e.Net.Definition.Name})";
    private static string OtherName(El e) => e.Name;
}
