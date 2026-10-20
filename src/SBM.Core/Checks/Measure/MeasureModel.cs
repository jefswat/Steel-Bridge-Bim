using SBM.Core.Concrete;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Solids;
using SBM.Core.Steel;
using SBM.Core.Units;

namespace SBM.Core.Checks.Measure;

/// <summary>One placed bar with its mark, identity and host (the concrete it is cast in).</summary>
public sealed class BarItem
{
    public required ElementRef Ref { get; init; }
    public required BarPlacement Bar { get; init; }
    public required BarMark Mark { get; init; }
    public required BridgeModel Model { get; init; }
    /// <summary>"deck", "sub" (unit = <see cref="ElementRef.Unit"/>), "psbeam" (beam mark = <see cref="ElementRef.Assembly"/>).</summary>
    public required string Host { get; init; }
    private Box3? _box;
    public Box3 Bounds { get { if (_box == null) { var b = Box3.Empty; foreach (var p in Bar.Points) b.Add(p); _box = b; } return _box.Value; } }
}

/// <summary>
/// Read-only view of generated bridges for the measure tools: finds parts / assemblies / bars by mark, lists every solid with its identity, and gives
/// the girder / alignment directions used for frames. Nothing here changes the model.
/// </summary>
public sealed class MeasureModel
{
    public UnitSystem Units { get; }
    public IReadOnlyList<BridgeModel> Bridges { get; }
    /// <summary>IFC GlobalId of an element (set by the API layer, which owns the key patterns); null = unknown.</summary>
    public Func<ElementRef, string?>? GlobalIds { get; init; }

    public MeasureModel(IEnumerable<BridgeModel> bridges, UnitSystem units)
    {
        Bridges = bridges.ToList();
        Units = units;
    }

    public IEnumerable<BridgeModel> Filter(string? bridge) =>
        bridge == null ? Bridges : Bridges.Where(b => b.Definition.Name.Equals(bridge, StringComparison.OrdinalIgnoreCase)) is var ex && ex.Any() ? ex
            : Bridges.Where(b => b.Definition.Name.Contains(bridge, StringComparison.OrdinalIgnoreCase));

    public ElementRef Ref(string bridge, string kind, string mark, string? assembly = null, int? index = null, string? unit = null)
    {
        var r = new ElementRef { Bridge = bridge, Kind = kind, Mark = mark, Assembly = assembly, Index = index, Unit = unit };
        r.GlobalId = GlobalIds?.Invoke(r);
        return r;
    }

    public (BridgeModel M, SteelPart P) Part(string mark, string? bridge, List<string> notes)
    {
        var hits = Filter(bridge).Where(m => m.Steel != null).SelectMany(m => m.Steel!.Parts.Where(p => p.Mark.Equals(mark, StringComparison.OrdinalIgnoreCase)).Select(p => (m, p))).ToList();
        if (hits.Count == 0)
        {
            var near = Filter(bridge).Where(m => m.Steel != null).SelectMany(m => m.Steel!.Parts).Where(p => p.Mark.Contains(mark, StringComparison.OrdinalIgnoreCase)).Take(12).Select(p => p.Mark).ToList();
            throw new ArgumentException($"No part '{mark}'{(bridge != null ? $" in '{bridge}'" : "")}.{(near.Count > 0 ? " Similar: " + string.Join(", ", near) : "")}");
        }
        if (hits.Select(h => h.m).Distinct().Count() > 1)
            notes.Add($"'{mark}' exists in {hits.Select(h => h.m).Distinct().Count()} bridges ({string.Join(", ", hits.Select(h => h.m.Definition.Name).Distinct())}); measured in '{hits[0].m.Definition.Name}' (use --bridge)");
        return hits[0];
    }

    public (BridgeModel M, SteelAssembly A) Assembly(string mark, string? bridge, List<string> notes)
    {
        var hits = Filter(bridge).Where(m => m.Steel != null).SelectMany(m => m.Steel!.Assemblies.Where(a => a.Mark.Equals(mark, StringComparison.OrdinalIgnoreCase)).Select(a => (m, a))).ToList();
        if (hits.Count == 0)
        {
            var near = Filter(bridge).Where(m => m.Steel != null).SelectMany(m => m.Steel!.Assemblies).Where(a => a.Mark.Contains(mark, StringComparison.OrdinalIgnoreCase)).Take(12).Select(a => a.Mark).ToList();
            throw new ArgumentException($"No assembly '{mark}'{(bridge != null ? $" in '{bridge}'" : "")}.{(near.Count > 0 ? " Similar: " + string.Join(", ", near) : "")}");
        }
        if (hits.Count > 1) notes.Add($"'{mark}' exists in {hits.Count} bridges; measured in '{hits[0].m.Definition.Name}' (use --bridge)");
        return hits[0];
    }

    private readonly Dictionary<string, List<MeasureElement>> _elements = new();

    /// <summary>Every solid of the selected bridges (steel parts, prestressed beams and pads, deck slab, haunches, barriers, joints, concrete diaphragms,
    /// substructure solids, misc items); bolts / studs are not included.</summary>
    public List<MeasureElement> Elements(string? bridge = null)
    {
        string key = bridge ?? "";
        if (_elements.TryGetValue(key, out var cached)) return cached;
        var res = new List<MeasureElement>();
        foreach (var m in Filter(bridge))
        {
            string bn = m.Definition.Name;
            if (m.Steel is { } s)
                foreach (var p in s.Parts)
                    if (!p.Mesh.IsEmpty) res.Add(new MeasureElement { Ref = Ref(bn, "part", p.Mark, p.Assembly), Mesh = p.Mesh, Category = p.Category.ToString(), Description = p.Description });
            if (m.Prestressed is { } ps)
            {
                foreach (var b in ps.Beams) res.Add(new MeasureElement { Ref = Ref(bn, "psbeam", b.Mark), Mesh = b.Mesh, Category = "PrestressedBeam", Description = ps.Shape.Name, IsConcrete = true });
                foreach (var pad in ps.Pads) res.Add(new MeasureElement { Ref = Ref(bn, "pad", pad.Mark), Mesh = pad.Mesh, Category = "BearingPad", Description = "bearing pad" });
                if (ps.ConcreteDiaphragms is { } cd)
                    foreach (var d in cd.Diaphragms) if (!d.Mesh.IsEmpty) res.Add(new MeasureElement { Ref = Ref(bn, "diaphragm", d.Name, d.Support), Mesh = d.Mesh, Category = "Diaphragm", Description = d.Description, IsConcrete = true });
            }
            if (m.Concrete is { } c)
            {
                if (c.Deck is { IsEmpty: false } deck) res.Add(new MeasureElement { Ref = Ref(bn, "deck", "Deck slab"), Mesh = deck, Category = "Deck", Description = "deck slab", IsConcrete = true });
                if (c.Haunches is { IsEmpty: false } h) res.Add(new MeasureElement { Ref = Ref(bn, "haunch", "Haunches"), Mesh = h, Category = "Haunch", Description = "haunches", IsConcrete = true });
                foreach (var (name, mesh) in c.Barriers) if (!mesh.IsEmpty) res.Add(new MeasureElement { Ref = Ref(bn, "barrier", name), Mesh = mesh, Category = "Barrier", Description = name, IsConcrete = true });
                foreach (var (name, mesh) in c.DeckBumps) if (!mesh.IsEmpty) res.Add(new MeasureElement { Ref = Ref(bn, "deck", name), Mesh = mesh, Category = "Deck", Description = name, IsConcrete = true });
                foreach (var ch in c.CapHaunches)
                    if (!ch.Mesh.IsEmpty) res.Add(new MeasureElement { Ref = Ref(bn, "caphaunch", ch.Name, ch.Support), Mesh = ch.Mesh, Category = "CapHaunch", Description = ch.Name, IsConcrete = true });
                foreach (var j in c.Joints)
                {
                    if (!j.Blockout.IsEmpty) res.Add(new MeasureElement { Ref = Ref(bn, "joint", $"Joint blockout {j.Support}"), Mesh = j.Blockout, Category = "JointBlockout", Description = "joint blockout", IsConcrete = true });
                    if (j.Body is { IsEmpty: false } jb) res.Add(new MeasureElement { Ref = Ref(bn, "joint", $"Joint device {j.Support}"), Mesh = jb, Category = "JointDevice", Description = "joint device" });
                }
            }
            if (m.Substructure is { } sub)
            {
                foreach (var unit in sub.Units)
                {
                    int k = 0;
                    foreach (var so in unit.Solids)
                    {
                        if (so.Mesh.IsEmpty) continue;
                        res.Add(new MeasureElement { Ref = Ref(bn, "sub", so.Name, unit.Name, k++, unit.Name), Mesh = so.Mesh, Category = so.Kind.ToString(), Description = so.Description, IsConcrete = so.IsConcrete });
                    }
                }
                if (sub.Misc is { } misc)
                    foreach (var e in misc.Elements) if (!e.Mesh.IsEmpty) res.Add(new MeasureElement { Ref = Ref(bn, "misc", e.Name, e.Assembly), Mesh = e.Mesh, Category = e.Kind.ToString(), Description = e.Description });
                foreach (var d in sub.Drainage) if (!d.Mesh.IsEmpty) res.Add(new MeasureElement { Ref = Ref(bn, "drain", d.Name), Mesh = d.Mesh, Category = d.Kind.ToString(), Description = d.Description });
            }
        }
        _elements[key] = res;
        return res;
    }

    private readonly Dictionary<string, List<BarItem>> _bars = new();

    /// <summary>Every placed bar of the selected bridges (deck / barrier / diaphragm bars, substructure unit bars, prestressed beam bars) with the index the
    /// IFC export numbers it by.</summary>
    public List<BarItem> Bars(string? bridge = null)
    {
        string key = bridge ?? "";
        if (_bars.TryGetValue(key, out var cached)) return cached;
        var res = new List<BarItem>();
        foreach (var m in Filter(bridge))
        {
            string bn = m.Definition.Name;
            if (m.Concrete is { } c)
            {
                var marks = c.Marks.GroupBy(x => x.Mark).ToDictionary(g => g.Key, g => g.First());
                for (int i = 0; i < c.Bars.Count; i++)
                    if (marks.TryGetValue(c.Bars[i].Mark, out var mk))
                        res.Add(new BarItem { Ref = Ref(bn, "deckbar", c.Bars[i].Mark, c.Bars[i].Group, i, null), Bar = c.Bars[i], Mark = mk, Model = m, Host = "deck" });
            }
            if (m.Substructure is { } sub)
                foreach (var unit in sub.Units)
                {
                    var marks = sub.Marks.Where(x => x.Unit == unit.Name).GroupBy(x => x.Mark).ToDictionary(g => g.Key, g => g.First());
                    int i = 0;
                    foreach (var b in sub.Bars.Where(b => b.Unit == unit.Name))
                    {
                        if (marks.TryGetValue(b.Mark, out var mk)) res.Add(new BarItem { Ref = Ref(bn, "subbar", b.Mark, unit.Name, i, unit.Name), Bar = b, Mark = mk, Model = m, Host = "sub" });
                        i++;
                    }
                }
            if (m.Prestressed is { } ps)
            {
                foreach (var beam in ps.Beams)
                    for (int i = 0; i < beam.Bars.Count; i++)
                        if (ps.MarkOf(beam.Bars[i]) is { } mk)
                            res.Add(new BarItem { Ref = Ref(bn, "psbar", beam.Bars[i].Mark, beam.Mark, i, string.IsNullOrEmpty(mk.Unit) ? null : mk.Unit), Bar = beam.Bars[i], Mark = mk, Model = m, Host = "psbeam" });
            }
        }
        _bars[key] = res;
        return res;
    }

    /// <summary>Bars of a mark ("5a1"; "Pier 2:5c5" or --unit selects the structural unit; "#n" = one bar by its number among the mark's bars, 1-based),
    /// or a bar's IFC GlobalId.</summary>
    public List<BarItem> BarsOf(string spec, string? bridge, string? unit, out int? only)
    {
        only = null;
        var all = Bars(bridge);
        var byId = all.Where(b => b.Ref.GlobalId == spec).ToList();
        if (byId.Count > 0) return byId;
        string mark = spec;
        int hash = spec.LastIndexOf('#');
        if (hash > 0 && int.TryParse(spec[(hash + 1)..], out int n)) { only = n; mark = spec[..hash]; }
        int colon = mark.LastIndexOf(':');
        if (colon > 0) { unit = mark[..colon]; mark = mark[(colon + 1)..]; }
        var hits = all.Where(b => b.Mark.Mark.Equals(mark, StringComparison.OrdinalIgnoreCase)
                                  && (unit == null || (b.Mark.Unit ?? "").Equals(unit, StringComparison.OrdinalIgnoreCase) || (b.Ref.Unit ?? "").Equals(unit, StringComparison.OrdinalIgnoreCase))).ToList();
        if (hits.Count == 0)
        {
            var near = all.Select(b => b.Mark.Mark).Distinct().Where(x => x.Contains(mark, StringComparison.OrdinalIgnoreCase)).Take(15).ToList();
            throw new ArgumentException($"No bar mark '{mark}'{(unit != null ? $" in unit '{unit}'" : "")}.{(near.Count > 0 ? " Similar: " + string.Join(", ", near) : "")}");
        }
        BarsNote = null;
        var sets = hits.GroupBy(b => (b.Model.Definition.Name, b.Host, b.Ref.Unit)).OrderByDescending(g => g.Count()).ToList();
        if (sets.Count > 1 && only == null)
        {
            BarsNote = $"mark {mark} exists in {sets.Count} places ({string.Join(", ", sets.Select(g => $"{g.Key.Name} {g.Key.Host}{(g.Key.Unit != null ? " " + g.Key.Unit : "")}: {g.Count()}"))}); " +
                       $"the largest set ({sets[0].Key.Name} {sets[0].Key.Host}{(sets[0].Key.Unit != null ? " " + sets[0].Key.Unit : "")}) is used - pick another with --unit or --bridge";
            return sets[0].ToList();
        }
        return hits;
    }

    /// <summary>Set by <see cref="BarsOf"/> when the mark exists in several places and one set was chosen.</summary>
    public string? BarsNote { get; private set; }

    /// <summary>The girder whose path passes closest (in plan) to a point; s = path distance of the closest point.</summary>
    public static (GirderResult? G, double S, double Dist) NearestGirder(BridgeModel m, Vec3 p)
    {
        GirderResult? best = null; double bs = 0, bd = double.MaxValue;
        foreach (var g in m.Framing.Girders)
        {
            var path = g.Path;
            for (int i = 0; i + 1 < path.Count; i++)
            {
                var a = path.WebTop[i].XY; var b = path.WebTop[i + 1].XY;
                var ab = b - a; double l2 = ab.LengthSquared;
                double t = l2 < 1e-18 ? 0 : Math.Clamp(Vec2.Dot(p.XY - a, ab) / l2, 0, 1);
                double d = (a + ab * t - p.XY).Length;
                if (d < bd) { bd = d; best = g; bs = path.S[i] + (path.S[i + 1] - path.S[i]) * t; }
            }
        }
        return (best, bs, bd);
    }

    /// <summary>Path distance of the point of a girder path closest (in plan) to p.</summary>
    public static double NearestGirderS(GirderResult g, Vec3 p)
    {
        var path = g.Path;
        double bd = double.MaxValue, bs = 0;
        for (int i = 0; i + 1 < path.Count; i++)
        {
            var a = path.WebTop[i].XY; var b = path.WebTop[i + 1].XY;
            var ab = b - a; double l2 = ab.LengthSquared;
            double t = l2 < 1e-18 ? 0 : Math.Clamp(Vec2.Dot(p.XY - a, ab) / l2, 0, 1);
            double d = (a + ab * t - p.XY).LengthSquared;
            if (d < bd) { bd = d; bs = path.S[i] + (path.S[i + 1] - path.S[i]) * t; }
        }
        return bs;
    }

    /// <summary>3D unit tangent of a girder path at s, incl. grade (top of web).</summary>
    public static Vec3 GirderTangent(GirderResult g, double s)
    {
        var (_, t, _) = g.Path.At(s);
        double grade = g.Path.GradeAt(s);
        return new Vec3(t.X, t.Y, grade).Normalized();
    }

    public static string GirderName(BridgeModel m, GirderResult g) =>
        m.Definition.Girders.PlanNames is { } pn && g.Index < pn.Count ? pn[g.Index] : g.Name;

    /// <summary>Girder by name ("G1", plan name "A") in a bridge.</summary>
    public static GirderResult? Girder(BridgeModel m, string name)
    {
        foreach (var g in m.Framing.Girders)
            if (g.Name.Equals(name, StringComparison.OrdinalIgnoreCase) || GirderName(m, g).Equals(name, StringComparison.OrdinalIgnoreCase)) return g;
        return int.TryParse(name.TrimStart('G', 'g'), out int k) && k >= 1 && k <= m.Framing.Girders.Count ? m.Framing.Girders[k - 1] : null;
    }

    /// <summary>3D unit tangent of the alignment at a station, incl. the profile grade.</summary>
    public static Vec3 AlignmentTangent(Alignments.Alignment a, double station)
    {
        double ang = a.AngleAt(station);
        double g = (a.Profile.ElevationAt(station + 0.5) - a.Profile.ElevationAt(station - 0.5)) / 1.0;
        return new Vec3(Math.Cos(ang), Math.Sin(ang), g).Normalized();
    }

    /// <summary>Bolt solid in world coordinates.</summary>
    public static Mesh BoltWorldMesh(SteelModel s, BoltInstance b)
    {
        var local = b.Kind == BoltKind.Anchor ? SteelModel.AnchorBoltMesh(b.Diameter, b.Grip, b.Embedment)
            : SteelModel.BoltMesh(b.Diameter > 0 && Math.Abs(b.Diameter - s.Bolt.Diameter) > 1e-6 ? new BoltGeometry(b.Diameter) : s.Bolt, b.Grip, b.Length);
        var m = new Mesh();
        var f = b.Frame;
        foreach (var p in local.Positions) m.Positions.Add(f.ToWorld(p));
        m.Indices.AddRange(local.Indices);
        return m;
    }

    /// <summary>Bolt diameter of an instance.</summary>
    public static double BoltDiameter(SteelModel s, BoltInstance b) => b.Diameter > 0 ? b.Diameter : s.Bolt.Diameter;
}
