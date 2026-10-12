using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Steel;

namespace SBM.Core.Project;

/// <summary>Generated results for one bridge.</summary>
public sealed record BridgeModel(BridgeDefinition Definition, BridgeFraming Framing, SteelModel? Steel, bool FromCache, Concrete.ConcreteModel? Concrete = null,
    Substructure.SubstructureModel? Substructure = null, Prestressed.PrestressedModel? Prestressed = null);

/// <summary>
/// Incremental regeneration: each bridge is keyed by a hash of its definition, its alignment and the units. Bridges
/// whose inputs did not change are reused instead of regenerated; the rest regenerate in parallel.
/// Thread-safe.
/// </summary>
public sealed class RegenerationCache
{
    private readonly Dictionary<string, (BridgeFraming framing, SteelModel? steel, Concrete.ConcreteModel? concrete, Prestressed.PrestressedModel? ps)> _super = new();
    private readonly Dictionary<string, Substructure.SubstructureModel?> _sub = new();
    private readonly object _lock = new();

    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() },
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    /// <summary>Existing ground at a plan point (terrain), for automatic foundation tops. Change <see cref="GroundVersion"/> when it changes.</summary>
    public Func<Geometry.Vec2, double?>? Ground { get; set; }
    public int GroundVersion { get; set; }

    public int Hits { get; private set; }
    public int Misses { get; private set; }

    public static string Key(BridgeDefinition b, Alignment? a, Units.UnitSystem u)
    {
        var sb = new StringBuilder();
        sb.Append(JsonSerializer.Serialize(b, Json));
        if (a != null)
        {
            // The alignment's geometry, profile and cross slope (via the project file format).
            var tmp = new BridgeProject { Alignments = { a } };
            sb.Append(tmp.ToJson());
        }
        sb.Append(u.Kind).Append(u.Foot);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    /// <summary>Regenerates (or reuses) every bridge in the project. Steel can be skipped for fast framing-only runs.</summary>
    public List<BridgeModel?> Regenerate(BridgeProject project, bool includeSteel = true)
    {
        var bridges = project.Bridges.ToArray();
        var keys = new string?[bridges.Length];
        var parts = new (BridgeFraming f, SteelModel? s, Concrete.ConcreteModel? cm, Prestressed.PrestressedModel? ps, bool hit)?[bridges.Length];
        var used = new HashSet<string>();

        // ---- phase 1: framing, girders (steel or prestressed) and deck, per bridge (cached, parallel)
        Parallel.For(0, bridges.Length, i =>
        {
            var b = bridges[i];
            var a = project.FindAlignment(b.AlignmentName);
            if (a == null) return;
            var geo = GeometrySettings.Active(project);
            string key = Key(b, a, project.Units) + (includeSteel ? "+steel" : "")
                + (geo == null ? "" : $"+nurbs:{geo.FitToleranceFor(project.Units):R}:{geo.ChordTolerance:R}");
            keys[i] = key;
            lock (_lock) used.Add(key);
            (BridgeFraming, SteelModel?, Concrete.ConcreteModel?, Prestressed.PrestressedModel?) hit;
            bool found;
            lock (_lock) found = _super.TryGetValue(key, out hit);
            if (found) { parts[i] = (hit.Item1, hit.Item2, hit.Item3, hit.Item4, true); return; }
            var f = FramingGenerator.Generate(b, a);
            SteelModel? st = null;
            Concrete.ConcreteModel? cm = null;
            Prestressed.PrestressedModel? ps = null;
            if (includeSteel && !f.HasErrors && b.Arch != null)
            {
                // Steel tied arch: its own generator; steel parts + seats in a SteelModel, the slab / curbs in a ConcreteModel.
                var am = SBM.Core.Arch.ArchGenerator.Generate(f, project.Units);
                st = am.Steel;
                cm = am.Concrete;
            }
            else if (includeSteel && !f.HasErrors && b.Girders.IsPrestressed)
            {
                // Prestressed concrete girders instead of steel.
                var tp = Task.Run(() => Prestressed.PrestressedGenerator.Generate(f, project.Units));
                cm = Concrete.ConcreteGenerator.Generate(f, project.Units, geo);
                ps = tp.Result;
                if (b.PsSteelDiaphragms is { } dd) st = SteelDiaphragms(dd, ps);
            }
            else if (includeSteel && !f.HasErrors)
            {
                // Steel and concrete are independent: generate them concurrently.
                var ts = Task.Run(() => SteelModelGenerator.Generate(f, project.Units, geo));
                cm = Concrete.ConcreteGenerator.Generate(f, project.Units, geo);
                st = ts.Result;
                if (cm != null && st != null) { Checks.DeckEndBlockCheck.Run(f, st, cm, project.Units); Checks.DeckRebarCheck.Run(f, st, cm, project.Units); }
            }
            lock (_lock) _super[key] = (f, st, cm, ps);
            parts[i] = (f, st, cm, ps, false);
        });

        // ---- phase 2: substructure. Shared piers are built once (owner = first bridge with the id and an enabled
        // substructure) from the bearing seats of every bridge on them. When every enabled support of a group has an
        // as-designed cap profile, each of them is an owner (side-by-side piers on one line, e.g. "Pier 11WB & 19G"):
        // each builds its own cap and takes the seats that fall on it.
        var candidates = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < bridges.Length; i++)
            foreach (var sp in bridges[i].Supports.Where(x => x.SharedPier != "" && x.ResolvedSubstructure.Enabled))
            {
                if (!candidates.TryGetValue(sp.SharedPier, out var l)) candidates[sp.SharedPier] = l = new List<int>();
                if (!l.Contains(i)) l.Add(i);
            }
        var owners = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, list) in candidates)
        {
            bool allDesigned = list.Count > 1 && list.All(i => bridges[i].Supports.Where(x => x.SharedPier.Equals(id, StringComparison.OrdinalIgnoreCase))
                .All(x => x.ResolvedSubstructure.CapProfile.Count >= 3));
            owners[id] = allDesigned ? list.ToHashSet() : new HashSet<int> { list[0] };
        }
        bool IsOwner(string id, int i) => owners.TryGetValue(id, out var set) && set.Contains(i);
        var results = new BridgeModel?[bridges.Length];
        Parallel.For(0, bridges.Length, i =>
        {
            if (parts[i] is not { } pt || keys[i] is not { } key) return;
            var b = bridges[i];
            var shared = b.Supports.Select(x => x.SharedPier).Where(x => x != "").ToHashSet(StringComparer.OrdinalIgnoreCase);
            var linked = Enumerable.Range(0, bridges.Length).Where(k => k != i && keys[k] != null &&
                bridges[k].Supports.Any(x => x.SharedPier != "" && shared.Contains(x.SharedPier))).ToList();
            string subKey = key + "/g" + GroundVersion + string.Concat(linked.Select(k => "|" + keys[k]));
            lock (_lock) used.Add(subKey);
            Substructure.SubstructureModel? sub = null;
            bool found;
            lock (_lock) found = _sub.TryGetValue(subKey, out sub);
            // A tied-arch bridge builds no substructure: its bearings reach the approach units' piers as foreign seats.
            if (!found && includeSteel && !pt.f.HasErrors && (pt.s != null || pt.ps != null) && b.Arch == null)
            {
                var seats = (IReadOnlyList<BearingSeat>?)pt.ps?.BearingSeats ?? pt.s!.BearingSeats;
                var foreign = new Dictionary<int, List<(string, BearingSeat)>>();
                for (int j = 0; j < b.Supports.Count; j++)
                {
                    string id = b.Supports[j].SharedPier;
                    if (id == "" || !IsOwner(id, i)) continue;
                    var list = new List<(string, BearingSeat)>();
                    foreach (int k in linked)
                    {
                        if (parts[k] is not { } other) continue;
                        int jj = bridges[k].Supports.FindIndex(x => x.SharedPier.Equals(id, StringComparison.OrdinalIgnoreCase));
                        if (jj >= 0) list.AddRange(ForeignSeats(bridges[k], other.f, other.s, other.ps, jj));
                    }
                    foreign[j] = list;
                }
                bool Skip(int j) => b.SupportAt(j).SharedPier is { Length: > 0 } id && owners.ContainsKey(id) && !IsOwner(id, i);
                sub = Substructure.SubstructureGenerator.Generate(pt.f, seats, project.Units, Ground, foreign, Skip);
                if (sub != null && (b.Misc != null || b.Drains.Any(d => d.Trench != null)))
                    sub.Misc = Misc.MiscItemsGenerator.Generate(pt.f, pt.s, sub, project.Units, Ground);
                lock (_lock) _sub[subKey] = sub;
            }
            // Concrete end / pier diaphragms: superstructure elements built after the beams and the substructure (cheap; always rebuilt).
            if (includeSteel && pt.ps != null && !pt.f.HasErrors)
                pt.ps.ConcreteDiaphragms = Diaphragms.ConcreteDiaphragmGenerator.Generate(pt.f, pt.ps, project.Units);
            // Architectural surface treatments (form liner / thin brick) on the barrier, substructure and diaphragm faces: after everything they sit on
            // (cheap; always rebuilt). Kept on the substructure model, which every consumer (viewport, IFC, database) already receives.
            if (sub != null)
                sub.SurfaceTreatments = includeSteel && !pt.f.HasErrors
                    ? Surface.SurfaceTreatmentGenerator.Generate(pt.f, pt.cm, sub, pt.ps?.ConcreteDiaphragms, project.Units,
                        pt.ps?.Beams.Select(x => x.Mesh).ToList()) : null;
            lock (_lock) { if (pt.hit && found) Hits++; else Misses++; }
            results[i] = new BridgeModel(b, pt.f, pt.s, pt.hit && found, pt.cm, sub, pt.ps);
        });
        // Drop entries no longer used by the project (keeps memory bounded).
        lock (_lock)
        {
            foreach (var stale in _super.Keys.Where(k => !used.Contains(k)).ToList()) _super.Remove(stale);
            foreach (var stale in _sub.Keys.Where(k => !used.Contains(k)).ToList()) _sub.Remove(stale);
        }
        return results.ToList();
    }

    /// <summary>Steel intermediate diaphragms between the prestressed beams (T6 hook-up of the T4 detailer): detailed from the generated
    /// beams, kept on <see cref="Prestressed.PrestressedModel.SteelDiaphragms"/> and returned as the bridge's steel model (diaphragm
    /// parts, bolts and assemblies only) so every steel consumer (viewport, IFC, database, quantities) includes them. Diagnostics go
    /// to the prestressed model.</summary>
    internal static SteelModel SteelDiaphragms(PsSteelDiaphragmDefinition dd, Prestressed.PrestressedModel ps)
    {
        var lines = ps.Beams.Select(be => Diaphragms.PsDiaphragmBeamLine.FromPrestressed(be, be.Shape.Depth, be.Shape.WebWidth)).ToList();
        var dm = Diaphragms.PsSteelDiaphragmDetailer.Detail(dd, lines);
        ps.SteelDiaphragms = dm;
        ps.Diagnostics.AddRange(dm.Diagnostics);
        var st = new SteelModel { Bolt = new BoltGeometry(dd.Connection.BoltDiameter), BoltGrade = dd.BoltGrade };
        st.Assemblies.AddRange(dm.Assemblies);
        st.Parts.AddRange(dm.Parts);
        st.Bolts.AddRange(dm.Bolts);
        return st;
    }

    /// <summary>Another bridge's bearing seats on its support <paramref name="j"/>, located in plan (for a shared pier).</summary>
    private static IEnumerable<(string, BearingSeat)> ForeignSeats(BridgeDefinition b, BridgeFraming f, SteelModel? s, Prestressed.PrestressedModel? ps, int j)
    {
        if (f.HasErrors || j < 0 || j >= f.Supports.Count) yield break;
        var sl = f.Supports[j];
        if (ps != null)
        {
            foreach (var seat in ps.BearingSeats.Where(x => x.Support == sl.Name))
                yield return ($"{b.Name} G{seat.Girder + 1}", seat);
            yield break;
        }
        if (s == null) yield break;
        if (s.Arch != null)
        {
            // Tied arch: seats carry their plan location and axis.
            foreach (var seat in s.BearingSeats.Where(x => x.Support == sl.Name && x.Location != null))
                yield return ($"{b.Name} {(s.Arch.BearingNames.ElementAtOrDefault(seat.Girder) ?? "B" + (seat.Girder + 1))}", seat);
            yield break;
        }
        foreach (var seat in s.BearingSeats)
        {
            var br = sl.Bearings.ElementAtOrDefault(seat.Girder);
            if (br == null || Math.Abs(br.Station - seat.Station) > 1e-6) continue;
            var g = f.Girders[seat.Girder];
            int nSpan = g.Spans.Count;
            double sg = g.DistanceAt(Math.Min(j, nSpan - 1), j == nSpan ? 1 : 0);
            var tan = g.Path.At(sg).Item2;
            yield return ($"{b.Name} G{seat.Girder + 1}", seat with { Location = br.TopOfSteel.XY, Axis = tan });
        }
    }

    public void Clear()
    {
        lock (_lock) { _super.Clear(); _sub.Clear(); }
    }
}
