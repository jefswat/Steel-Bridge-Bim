using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Diaphragms;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Substructure;
using SBM.Core.Units;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Surface;

/// <summary>One treated region: its faces' area and the relief layer (thin solid on the faces).</summary>
public sealed class SurfaceTreatmentItem
{
    public required SurfaceTreatmentDefinition Treatment { get; init; }
    public required SurfaceRegion Region { get; init; }
    /// <summary>Unique within the bridge: "{treatment} - {region}".</summary>
    public required string Name { get; init; }
    /// <summary>Support index of the host (-1 = barrier / deck).</summary>
    public int SupportIndex { get; init; } = -1;
    /// <summary>Area of the host faces covered (m2).</summary>
    public double Area { get; set; }
    public int Faces { get; set; }
    /// <summary>Relief layer: the covered faces offset outward by the treatment's relief depth (closed prisms, one per face piece).</summary>
    public Mesh Mesh { get; } = new();
    public List<Mesh> Pieces { get; } = new();
    public Assumption? Assumption { get; set; }
    public string Description { get; set; } = "";
}

public sealed class SurfaceTreatmentModel
{
    public List<SurfaceTreatmentItem> Items { get; } = new();
    public List<Diagnostic> Diagnostics { get; } = new();
    public bool IsEmpty => Items.Count == 0;
    /// <summary>Total area per treatment name.</summary>
    public IEnumerable<(SurfaceTreatmentDefinition Treatment, double Area)> Totals =>
        Items.GroupBy(i => i.Treatment).Select(g => (g.Key, g.Sum(i => i.Area)));
}

/// <summary>
/// Builds <see cref="BridgeDefinition.SurfaceTreatments"/> on the generated model (after the substructure and the concrete diaphragms). Faces come from
/// the custom barrier's tagged surfaces or from the host solids (exposed, near-vertical, facing filter), are clipped to the region (S range, lower / upper
/// limit lines, excluded bands) and get an area and a relief layer. Pure function of its inputs.
/// </summary>
public static class SurfaceTreatmentGenerator
{
    public static SurfaceTreatmentModel? Generate(BridgeFraming f, ConcreteModel? concrete, SubstructureModel? sub, ConcreteDiaphragmModel? dia, UnitSystem u,
        IReadOnlyList<Mesh>? beams = null)
    {
        var def = f.Definition;
        if (def.SurfaceTreatments is not { Count: > 0 }) return null;
        var model = new SurfaceTreatmentModel();
        void Warn(string m) => model.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "Surface treatment " + m));
        void Info(string m) => model.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info, "Surface treatment " + m));
        string Area(double m2) => $"{m2 / Math.Pow(Ft(1), 2):N1} SF";
        foreach (var t in def.SurfaceTreatments)
        {
            foreach (var r in t.Regions)
            {
                string name = $"{t.Name} - {r.Name}";
                try
                {
                    var item = BuildRegion(f, concrete, sub, dia, t, r, name, beams);
                    if (r.Unresolved != "")
                    {
                        item.Assumption = Assumption.Unresolved(r.Unresolved, r.Basis);
                        Warn($"{name}: UNRESOLVED {r.Unresolved} - {r.Basis}.");
                    }
                    if (item.Faces == 0) { Warn($"{name}: no faces found on the host - nothing built."); continue; }
                    item.Description = $"{t.Kind} {t.Pattern}".Trim() + $", {Area(item.Area)}, relief {u.FormatLength(t.Relief, LengthKind.Short)}" + (r.Source != "" ? $" ({r.Source})" : "");
                    model.Items.Add(item);
                }
                catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
                {
                    Warn($"{name}: not built - {ex.Message}");
                }
            }
            double total = model.Items.Where(i => i.Treatment == t).Sum(i => i.Area);
            string cmp = double.IsNaN(t.PlanQuantity) ? "no plan quantity" : $"plan {Area(t.PlanQuantity)} ({t.QuantityItem}), model - plan {(total - t.PlanQuantity) / Math.Pow(Ft(1), 2):+0.0;-0.0} SF ({(total / t.PlanQuantity - 1) * 100:+0.0;-0.0} %)";
            Info($"{t.Name}: {Area(total)} on {model.Items.Count(i => i.Treatment == t)} region(s); {cmp}.");
            foreach (var x in t.Unresolved) Warn($"{t.Name}: UNRESOLVED {x}");
        }
        return model;
    }

    private static SurfaceTreatmentItem BuildRegion(BridgeFraming f, ConcreteModel? concrete, SubstructureModel? sub, ConcreteDiaphragmModel? dia,
        SurfaceTreatmentDefinition t, SurfaceRegion r, string name, IReadOnlyList<Mesh>? beams)
    {
        var def = f.Definition;
        var al = f.Alignment;
        // support frame
        Vec2 O = default, D = new(1, 0), N = new(0, 1);
        int supIndex = -1;
        if (r.Host != SurfaceHost.Barrier)
        {
            supIndex = def.Supports.FindIndex(s => s.Name.Equals(r.Support, StringComparison.OrdinalIgnoreCase));
            if (supIndex < 0) throw new ArgumentException($"support '{r.Support}' not found");
            var sl = f.SupportLine(supIndex);
            O = sl.AlignmentPoint; D = sl.Direction;
            var tg = Vec2.FromAngle(al.AngleAt(sl.Station));
            N = D.Left; if (Vec2.Dot(N, tg) < 0) N = -N;
        }
        double S(Vec3 p) => Vec2.Dot(p.XY - O, D);
        double LevelZ(SurfaceLevel lv, Vec3 p)
        {
            if (lv.Ref == SurfaceLevelRef.DeckTop)
            {
                var so = al.Inverse(p.XY);
                return def.Deck.FinishedElevation(al, so.Station, so.Offset) + lv.Offset;
            }
            if (lv.Points.Count == 0) throw new ArgumentException("a level has no points");
            var pts = lv.Points.OrderBy(q => q.S).Select(q => new Vec2(q.S, q.Elevation)).ToList();
            return SubstructureUnit.Interp(pts, S(p)) + lv.Offset;
        }
        var item = new SurfaceTreatmentItem { Treatment = t, Region = r, Name = name, SupportIndex = supIndex };

        // candidate faces: (polygon, outward unit normal)
        var faces = new List<(Vec3[] poly, Vec3 n)>();
        if (r.Host == SurfaceHost.Barrier)
        {
            if (concrete == null) throw new InvalidOperationException("no concrete model (barrier)");
            foreach (var ts in concrete.TaggedSurfaces.Where(x => x.Tag.Equals(r.Tag, StringComparison.OrdinalIgnoreCase)))
                faces.Add((ts.Polygon, ts.Outward));
        }
        else
        {
            var hosts = new List<Mesh>();
            var cover = new List<Mesh>();                      // every solid of the support: a face inside one of them is not exposed
            var unit = sub?.Units.FirstOrDefault(x => x.SupportIndex == supIndex);
            var cd = dia?.Diaphragms.FirstOrDefault(x => x.SupportIndex == supIndex);
            if (unit != null) cover.AddRange(unit.Solids.Where(s => s.IsConcrete && !s.Mesh.IsEmpty).Select(s => s.Mesh));
            if (cd != null) cover.AddRange(cd.Pieces.Count > 0 ? cd.Pieces : new List<Mesh> { cd.Mesh });
            // beams and haunches run into the diaphragm notches: those faces are not exposed either
            if (beams != null) cover.AddRange(beams.Where(m => !m.IsEmpty));
            if (concrete?.Haunches is { IsEmpty: false } hn) cover.Add(hn);
            if (r.Host == SurfaceHost.Substructure)
            {
                if (unit == null) throw new InvalidOperationException($"no substructure unit at {r.Support}");
                var kinds = r.Elements.Select(e => Enum.TryParse<SubElementKind>(e, true, out var k) ? k : throw new ArgumentException($"unknown element kind '{e}'")).ToHashSet();
                hosts.AddRange(unit.Solids.Where(s => s.IsConcrete && (kinds.Count == 0 || kinds.Contains(s.Kind)) && (r.NameContains == "" || s.Name.Contains(r.NameContains, StringComparison.OrdinalIgnoreCase)))
                    .Select(s => s.Mesh));
            }
            else
            {
                if (cd == null) throw new InvalidOperationException($"no concrete diaphragm at {r.Support}");
                hosts.AddRange(cd.Pieces.Count > 0 ? cd.Pieces : new List<Mesh> { cd.Mesh });
            }
            Vec3? want = null;
            if (r.FacingOffsetSide != 0)
            {
                // outward from the alignment on that side, at the support station
                var sl = f.SupportLine(supIndex);
                var rn = al.RightNormalAt(sl.Station) * r.FacingOffsetSide;
                want = new Vec3(rn.X, rn.Y, 0).Normalized();
            }
            else if (r.FacingS != 0 || r.FacingN != 0) { var w = D * r.FacingS + N * r.FacingN; want = new Vec3(w.X, w.Y, 0).Normalized(); }
            double cosMax = Math.Cos(r.MaxAngleDeg * Math.PI / 180);
            foreach (var m in hosts)
            {
                double sign = m.Volume() < 0 ? -1 : 1;           // outward normals: flip an inward-oriented mesh
                for (int i = 0; i < m.Indices.Count; i += 3)
                {
                    var a = m.Positions[m.Indices[i]]; var b = m.Positions[m.Indices[i + 1]]; var c = m.Positions[m.Indices[i + 2]];
                    var cr = Vec3.Cross(b - a, c - a);
                    double len = cr.Length;
                    if (len < 1e-10) continue;
                    var n = cr * (sign / len);
                    if (Math.Abs(n.Z) > 0.5) continue;           // near-vertical faces only
                    if (want is { } wv)
                    {
                        var nh = new Vec3(n.X, n.Y, 0).Normalized();
                        if (Vec3.Dot(nh, wv) < cosMax) continue;
                    }
                    faces.Add((new[] { a, b, c }, n));
                }
            }
            // exposure: drop faces whose centroid, moved 5 mm outward, lies inside another solid of the support
            faces = faces.Where(fc =>
            {
                var ctr = new Vec3(fc.poly.Average(p => p.X), fc.poly.Average(p => p.Y), fc.poly.Average(p => p.Z)) + fc.n * 0.005;
                return !cover.Any(m => Inside(m, ctr));
            }).ToList();
        }

        // clip to the region and build the relief layer
        var limits = new List<(SurfaceLevel? lo, SurfaceLevel? hi)>();
        {
            // the region minus the excluded bands = consecutive (lower, upper) pairs
            var bands = r.Exclude.ToList();
            SurfaceLevel? lo = r.Lower;
            foreach (var bd in bands) { limits.Add((lo, bd.Lower)); lo = bd.Upper; }
            limits.Add((lo, r.Upper));
        }
        foreach (var (poly, n) in faces)
            foreach (var (lo, hi) in limits)
            {
                var pg = poly.ToList();
                if (!double.IsNaN(r.SMin)) pg = Clip(pg, p => S(p) - r.SMin);
                if (!double.IsNaN(r.SMax)) pg = Clip(pg, p => r.SMax - S(p));
                if (lo != null) pg = Clip(pg, p => p.Z - LevelZ(lo, p));
                if (hi != null) pg = Clip(pg, p => LevelZ(hi, p) - p.Z);
                if (pg.Count < 3) continue;
                double area = 0;
                for (int k = 1; k + 1 < pg.Count; k++) area += Vec3.Cross(pg[k] - pg[0], pg[k + 1] - pg[0]).Length / 2;
                if (area < 1e-8) continue;
                item.Area += area;
                item.Faces++;
                if (t.Relief > 0) AddLayer(item, pg, n, t.Relief);
            }
        return item;
    }

    /// <summary>Thin prism on a planar face polygon, offset outward by the relief.</summary>
    private static void AddLayer(SurfaceTreatmentItem item, List<Vec3> pg, Vec3 n, double relief)
    {
        var x = (pg[1] - pg[0]).Normalized();
        var y = Vec3.Cross(n, x).Normalized();
        var sec = pg.Select(p => new Vec2(Vec3.Dot(p - pg[0], x), Vec3.Dot(p - pg[0], y))).ToList();
        var bot = pg.ToArray();
        var top = pg.Select(p => p + n * relief).ToArray();
        if (MeshBuilder.SignedArea(sec) < 0) { sec.Reverse(); Array.Reverse(bot); Array.Reverse(top); }
        var m = new Mesh();
        MeshBuilder.AddLoft(m, sec, new[] { bot, top });
        item.Mesh.Append(m);
        item.Pieces.Add(m);
    }

    /// <summary>Sutherland-Hodgman: keep f >= 0 (f linear along the edges).</summary>
    private static List<Vec3> Clip(List<Vec3> poly, Func<Vec3, double> f)
    {
        var res = new List<Vec3>();
        int n = poly.Count;
        for (int i = 0; i < n; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % n];
            double fa = f(a), fb = f(b);
            if (fa >= 0) res.Add(a);
            if ((fa >= 0) != (fb >= 0)) res.Add(a + (b - a) * (fa / (fa - fb)));
        }
        return res;
    }

    /// <summary>Point inside a closed mesh (ray parity along a slightly tilted direction; coincident internal faces cancel in pairs).</summary>
    private static bool Inside(Mesh m, Vec3 p)
    {
        var bb = m.Bounds();
        if (p.X < bb.Min.X || p.Y < bb.Min.Y || p.Z < bb.Min.Z || p.X > bb.Max.X || p.Y > bb.Max.Y || p.Z > bb.Max.Z) return false;
        var dir = new Vec3(0.0123, 0.0071, 1).Normalized();
        int hits = 0;
        for (int i = 0; i < m.Indices.Count; i += 3)
        {
            var a = m.Positions[m.Indices[i]]; var b = m.Positions[m.Indices[i + 1]]; var c = m.Positions[m.Indices[i + 2]];
            var e1 = b - a; var e2 = c - a;
            var pv = Vec3.Cross(dir, e2);
            double det = Vec3.Dot(e1, pv);
            if (Math.Abs(det) < 1e-14) continue;
            double inv = 1 / det;
            var tv = p - a;
            double uu = Vec3.Dot(tv, pv) * inv;
            if (uu < 0 || uu > 1) continue;
            var q = Vec3.Cross(tv, e1);
            double vv = Vec3.Dot(dir, q) * inv;
            if (vv < 0 || uu + vv > 1) continue;
            if (Vec3.Dot(e2, q) * inv > 1e-9) hits++;
        }
        return (hits & 1) == 1;
    }
}
