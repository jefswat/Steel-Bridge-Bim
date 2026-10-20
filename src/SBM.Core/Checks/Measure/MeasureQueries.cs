using SBM.Core.Concrete;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Solids;
using SBM.Core.Units;

namespace SBM.Core.Checks.Measure;

public sealed class DistanceResult
{
    public string Query { get; init; } = "distance";
    public required ElementRef A { get; init; }
    public required ElementRef B { get; init; }
    /// <summary>Minimum distance between the two solids; negative = they overlap by that depth.</summary>
    public required Len Distance { get; init; }
    public bool Overlap { get; init; }
    public double[] PointA { get; init; } = Array.Empty<double>();
    public double[] PointB { get; init; } = Array.Empty<double>();
    /// <summary>Unit direction A -> B of the closest approach.</summary>
    public double[]? Direction { get; init; }
    public string? Location { get; init; }
    public List<string> Notes { get; init; } = new();
}

public sealed class PointHit
{
    public required ElementRef Element { get; init; }
    public string Category { get; init; } = "";
    /// <summary>Top / bottom elevation of the element on the vertical line (project long units), and its depth there.</summary>
    public double Top { get; init; }
    public double Bottom { get; init; }
    public Len Depth { get; init; }
    public List<double[]> Intervals { get; init; } = new();
    public bool ContainsPoint { get; init; }
}

public sealed class PointResult
{
    public string Query { get; init; } = "point";
    public double[] At { get; init; } = Array.Empty<double>();
    public string? Alignment { get; init; }
    public string? Station { get; init; }
    public double? Offset { get; init; }
    public double? ProfileGrade { get; init; }
    public double? FinishedGrade { get; init; }
    public List<PointHit> Elements { get; init; } = new();
    /// <summary>Elevations a checker compares: deck top / bottom, top of web of the nearest girder at the station, bearing seats nearby.</summary>
    public Dictionary<string, double> Elevations { get; init; } = new();
    public List<string> Notes { get; init; } = new();
}

public sealed class BarBend
{
    public int No { get; init; }
    public double AngleDeg { get; init; }
    /// <summary>Centerline radius (0 = a sharp corner in the model centerline).</summary>
    public Len Radius { get; init; }
    /// <summary>Inside pin diameter = 2 R - db.</summary>
    public Len PinDiameter { get; init; }
    public double PinOverDb { get; init; }
    public double[] At { get; init; } = Array.Empty<double>();
}

public sealed class BarResult
{
    public string Query { get; init; } = "bar";
    public required ElementRef Bar { get; init; }
    public string Size { get; init; } = "";
    public string Shape { get; init; } = "";
    public string Location { get; init; } = "";
    public int BarsOfMark { get; init; }
    public Len Diameter { get; init; }
    public Len DevelopedLength { get; init; }
    public Len? ScheduledLength { get; init; }
    public Len? PrintedPin { get; init; }
    public Len MinPinAashto { get; init; }
    public List<BarBend> Bends { get; init; } = new();
    /// <summary>Straight lengths between bends (centerline, tangent to tangent).</summary>
    public List<Len> Straights { get; init; } = new();
    public List<string> Hooks { get; init; } = new();
    public List<double[]> Centerline { get; init; } = new();
    public string? StartStation { get; init; }
    public string? EndStation { get; init; }
    public List<string> Notes { get; init; } = new();
}

public sealed class CoverRow
{
    public required ElementRef Bar { get; init; }
    /// <summary>Minimum clear cover per host face type ("top", "bottom", "side") and overall.</summary>
    public Dictionary<string, Len> ByFace { get; init; } = new();
    public Len? Min { get; init; }
    public string? MinFace { get; init; }
    public double[]? MinAt { get; init; }
    public bool Outside { get; init; }
}

public sealed class CoverResult
{
    public string Query { get; init; } = "cover";
    public string Mark { get; init; } = "";
    public string Size { get; init; } = "";
    public int Bars { get; init; }
    public string Hosts { get; init; } = "";
    public Dictionary<string, Len> MinByFace { get; init; } = new();
    public List<CoverRow> PerBar { get; init; } = new();
    public List<string> Notes { get; init; } = new();
}

public sealed class SpacingResult
{
    public string Query { get; init; } = "spacing";
    public string Mark { get; init; } = "";
    public string Along { get; init; } = "";
    public int Bars { get; init; }
    public int Lines { get; init; }
    /// <summary>Position of each bar (distance along the layout direction from the first bar; stations when along the alignment).</summary>
    public List<double> Positions { get; init; } = new();
    public string? FirstStation { get; init; }
    public string? LastStation { get; init; }
    public string Chain { get; init; } = "";
    public List<SpaceRun> Runs { get; init; } = new();
    public Len Total { get; init; }
    public List<string> Notes { get; init; } = new();
}

public sealed class LapRow
{
    public required string BarA { get; init; }
    public required string BarB { get; init; }
    public required Len Lap { get; init; }
    /// <summary>Clear distance between the lapped bars (0 = contact).</summary>
    public required Len Clear { get; init; }
    public double Center { get; init; }
    public string? CenterStation { get; init; }
    public int Line { get; init; }
}

public sealed class LapResult
{
    public string Query { get; init; } = "laps";
    public string Mark { get; init; } = "";
    public string Size { get; init; } = "";
    public int Bars { get; init; }
    public List<LapRow> Laps { get; init; } = new();
    public Len? MinLap { get; init; }
    public Len? MaxLap { get; init; }
    public Len? ClassBLap { get; init; }
    /// <summary>Smallest distance between lap centers of adjacent lines (stagger), and the largest share of a line group's laps within one lap length.</summary>
    public Len? MinStagger { get; init; }
    public double? MaxShareSplicedPct { get; init; }
    public List<string> Notes { get; init; } = new();
}

/// <summary>Distances, point queries and rebar measurements on the generated model.</summary>
public static class MeasureQueries
{
    // ------------------------------------------------------------------ distance

    public static DistanceResult Distance(MeasureModel mm, string a, string b, string? bridge)
    {
        var notes = new List<string>();
        var ea = Find(mm, a, bridge, notes); var eb = Find(mm, b, bridge, notes);
        var u = mm.Units;
        // overlap via the clash engine (exact edge-in-solid depth)
        var items = new List<ClashItem> { new(ea.Category, ea.Ref.Mark, ea.Ref.Assembly ?? "", ea.Description, ea.Mesh), new(eb.Category, eb.Ref.Mark, eb.Ref.Assembly ?? "", eb.Description, eb.Mesh) };
        var rep = SteelClashCheck.Run("", items, new ClashOptions { Tolerance = 1e-5 });
        if (rep.Clashes.Count > 0)
        {
            var c = rep.Clashes[0];
            return new DistanceResult { A = ea.Ref, B = eb.Ref, Distance = Len.Of(-c.Depth), Overlap = true, PointA = PlateMeasure.World(u, c.At), PointB = PlateMeasure.World(u, c.At), Notes = notes,
                Location = "deepest point of one solid's edges inside the other (clash engine)" };
        }
        var (pa, pb, d) = MinDistance(ea.Mesh, eb.Mesh);
        if (d < 1e-6 && !rep.OpenMeshes.Any()) notes.Add("the solids touch");
        return new DistanceResult
        {
            A = ea.Ref, B = eb.Ref, Distance = Len.Of(d), PointA = PlateMeasure.World(u, pa), PointB = PlateMeasure.World(u, pb),
            Direction = d > 1e-9 ? Geo3.Arr((pb - pa).Normalized()) : null, Notes = notes,
        };
    }

    private static MeasureElement Find(MeasureModel mm, string mark, string? bridge, List<string> notes)
    {
        var els = mm.Elements(bridge);
        var hit = els.FirstOrDefault(e => e.Ref.Kind == "part" && e.Ref.Mark.Equals(mark, StringComparison.OrdinalIgnoreCase))
                  ?? els.FirstOrDefault(e => e.Ref.Mark.Equals(mark, StringComparison.OrdinalIgnoreCase))
                  ?? els.FirstOrDefault(e => e.Ref.GlobalId == mark);
        if (hit == null)
        {
            var near = els.Where(e => e.Ref.Mark.Contains(mark, StringComparison.OrdinalIgnoreCase)).Take(10).Select(e => e.Ref.Mark).ToList();
            throw new ArgumentException($"No element '{mark}'.{(near.Count > 0 ? " Similar: " + string.Join(", ", near) : "")}");
        }
        if (els.Count(e => e.Ref.Mark.Equals(mark, StringComparison.OrdinalIgnoreCase)) > 1) notes.Add($"'{mark}' names more than one element; the first ({hit.Ref.Bridge}) is used");
        return hit;
    }

    /// <summary>Closest points of two triangle meshes (vertex-triangle both ways + edge-edge), culled by bounding boxes.</summary>
    public static (Vec3 A, Vec3 B, double D) MinDistance(Mesh a, Mesh b)
    {
        var o = a.Positions[0];
        var ta = Tris(a, o); var tb = Tris(b, o);
        double best = double.MaxValue; Vec3 pa = default, pb = default;
        // order candidate pairs by box distance
        var ba = ta.Select(Box).ToArray(); var bb = tb.Select(Box).ToArray();
        var boxB = Union(bb); var boxA = Union(ba);
        var ia = Enumerable.Range(0, ta.Length).OrderBy(i => Geo3.BoxDistance(ba[i], boxB)).ToArray();
        var ib = Enumerable.Range(0, tb.Length).OrderBy(j => Geo3.BoxDistance(bb[j], boxA)).ToArray();
        foreach (int i in ia)
        {
            if (Geo3.BoxDistance(ba[i], boxB) > best) break;
            foreach (int j in ib)
            {
                if (Geo3.BoxDistance(bb[j], boxA) > best) break;
                if (Geo3.BoxDistance(ba[i], bb[j]) > best) continue;
                var (x, y, d) = TriTri(ta[i], tb[j]);
                if (d < best) { best = d; pa = x; pb = y; }
            }
        }
        return (pa + o, pb + o, best);
    }

    private static (Vec3 A, Vec3 B, Vec3 C)[] Tris(Mesh m, Vec3 o)
    {
        var r = new (Vec3, Vec3, Vec3)[m.TriangleCount];
        for (int t = 0; t < r.Length; t++) r[t] = (m.Positions[m.Indices[3 * t]] - o, m.Positions[m.Indices[3 * t + 1]] - o, m.Positions[m.Indices[3 * t + 2]] - o);
        return r;
    }
    private static Box3 Box((Vec3 A, Vec3 B, Vec3 C) t) { var b = Box3.Empty; b.Add(t.A); b.Add(t.B); b.Add(t.C); return b; }
    private static Box3 Union(Box3[] bs) { var b = Box3.Empty; foreach (var x in bs) { b.Add(x.Min); b.Add(x.Max); } return b; }

    private static (Vec3, Vec3, double) TriTri((Vec3 A, Vec3 B, Vec3 C) s, (Vec3 A, Vec3 B, Vec3 C) t)
    {
        double best = double.MaxValue; Vec3 pa = default, pb = default;
        foreach (var p in new[] { s.A, s.B, s.C }) { var q = Geo3.ClosestOnTriangle(p, t.A, t.B, t.C); double d = (q - p).Length; if (d < best) { best = d; pa = p; pb = q; } }
        foreach (var p in new[] { t.A, t.B, t.C }) { var q = Geo3.ClosestOnTriangle(p, s.A, s.B, s.C); double d = (q - p).Length; if (d < best) { best = d; pa = q; pb = p; } }
        var es = new[] { (s.A, s.B), (s.B, s.C), (s.C, s.A) }; var et = new[] { (t.A, t.B), (t.B, t.C), (t.C, t.A) };
        foreach (var (a0, a1) in es) foreach (var (b0, b1) in et)
            {
                var (x, y) = Geo3.ClosestSegSeg(a0, a1, b0, b1);
                double d = (y - x).Length;
                if (d < best) { best = d; pa = x; pb = y; }
            }
        return (pa, pb, best);
    }

    // ------------------------------------------------------------------ point

    public static PointResult Point(MeasureModel mm, Vec2 xy, double? z, string? bridge)
    {
        var u = mm.Units;
        var hits = new List<PointHit>();
        var notes = new List<string>();
        foreach (var e in mm.Elements(bridge))
        {
            var b = e.Bounds;
            if (xy.X < b.Min.X || xy.X > b.Max.X || xy.Y < b.Min.Y || xy.Y > b.Max.Y) continue;
            var zs = VerticalHits(e.Mesh, xy);
            if (zs.Count < 2) continue;
            zs.Sort();
            var iv = new List<double[]>();
            for (int i = 0; i + 1 < zs.Count; i += 2) iv.Add(new[] { Fmt.Long(u, zs[i]), Fmt.Long(u, zs[i + 1]) });
            bool contains = z is { } zz && Enumerable.Range(0, zs.Count / 2).Any(i => zz >= zs[2 * i] && zz <= zs[2 * i + 1]);
            hits.Add(new PointHit { Element = e.Ref, Category = e.Category, Top = Fmt.Long(u, zs[^1]), Bottom = Fmt.Long(u, zs[0]), Depth = Len.Of(zs[^1] - zs[0]), Intervals = iv, ContainsPoint = contains });
        }
        hits = hits.OrderByDescending(h => h.Top).ToList();
        var elev = new Dictionary<string, double>();
        var deck = hits.Where(h => h.Element.Kind == "deck").ToList();
        if (deck.Count > 0) { elev["deck top"] = deck.Max(h => h.Top); elev["deck bottom"] = deck.Min(h => h.Bottom); }
        string? al = null, sta = null; double? off = null, pg = null, fg = null;
        foreach (var m in mm.Filter(bridge))
        {
            var a = m.Framing.Alignment;
            if (PointQuery.Query(a, new Vec3(xy.X, xy.Y, z ?? 0)) is { } rd && al == null)
            {
                al = rd.Alignment; sta = u.FormatStation(rd.Station, 3); off = Fmt.Long(u, rd.Offset); pg = Fmt.Long(u, rd.ProfileElevation);
                fg = Fmt.Long(u, m.Definition.Deck.FinishedElevation(a, rd.Station, rd.Offset));
            }
            var (g, s, dist) = MeasureModel.NearestGirder(m, new Vec3(xy.X, xy.Y, 0));
            if (g != null && dist < 3.0)
            {
                var (p, _, gs) = g.Path.At(s);
                elev[$"top of web {m.Definition.Name} {MeasureModel.GirderName(m, g)} (web CL, {Fmt.FtIn(dist)} away)"] = Fmt.Long(u, p.Z);
            }
            foreach (var seat in m.Steel?.BearingSeats ?? new List<Steel.BearingSeat>())
            {
                var loc = seat.Location ?? (m.Framing.Supports.Count > 0 ? (Vec2?)null : null);
                if (loc is { } lp && (lp - xy).Length < 1.5) elev[$"bearing seat {seat.Support} G{seat.Girder + 1}"] = Fmt.Long(u, seat.SeatElevation);
            }
            if (m.Substructure is { } sub)
                foreach (var unit in sub.Units)
                    foreach (var pd in unit.Pedestals)
                        if ((unit.Plan(pd.S, pd.N) - xy).Length < 1.0) elev[$"seat (pedestal top) {unit.Name} G{pd.Girder + 1}"] = Fmt.Long(u, pd.SeatZ);
        }
        if (hits.Count == 0) notes.Add("no solid on the vertical line through the point");
        return new PointResult
        {
            At = new[] { Fmt.Long(u, xy.X), Fmt.Long(u, xy.Y), z is { } z2 ? Fmt.Long(u, z2) : double.NaN }, Alignment = al, Station = sta, Offset = off, ProfileGrade = pg, FinishedGrade = fg,
            Elements = hits, Elevations = elev, Notes = notes,
        };
    }

    /// <summary>Elevations where the vertical line through xy crosses the mesh triangles.</summary>
    public static List<double> VerticalHits(Mesh m, Vec2 xy)
    {
        var zs = new List<double>();
        double px = xy.X + 1.234567e-7, py = xy.Y + 2.345678e-7;
        var pos = m.Positions;
        for (int t = 0; t + 2 < m.Indices.Count; t += 3)
        {
            var a = pos[m.Indices[t]]; var b = pos[m.Indices[t + 1]]; var c = pos[m.Indices[t + 2]];
            if (px < Math.Min(a.X, Math.Min(b.X, c.X)) || px > Math.Max(a.X, Math.Max(b.X, c.X)) || py < Math.Min(a.Y, Math.Min(b.Y, c.Y)) || py > Math.Max(a.Y, Math.Max(b.Y, c.Y))) continue;
            double d = (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
            if (Math.Abs(d) < 1e-14) continue;
            double w0 = ((b.X - px) * (c.Y - py) - (b.Y - py) * (c.X - px)) / d;
            double w1 = ((c.X - px) * (a.Y - py) - (c.Y - py) * (a.X - px)) / d;
            double w2 = 1 - w0 - w1;
            if (w0 < 0 || w1 < 0 || w2 < 0) continue;
            zs.Add(w0 * a.Z + w1 * b.Z + w2 * c.Z);
        }
        return zs;
    }

    // ------------------------------------------------------------------ rebar

    public static BarResult Bar(MeasureModel mm, string spec, string? bridge, string? unit)
    {
        var u = mm.Units;
        var bars = mm.BarsOf(spec, bridge, unit, out int? only);
        var notes = new List<string>(); if (mm.BarsNote != null) notes.Add(mm.BarsNote);
        int pick = only is { } n ? n - 1 : 0;
        if (pick < 0 || pick >= bars.Count) throw new ArgumentException($"Mark {bars[0].Mark.Mark} has {bars.Count} bars; #{pick + 1} does not exist.");
        if (only == null && bars.Count > 1) notes.Add($"{bars.Count} bars carry this mark; bar #1 is shown (use {bars[0].Mark.Mark}#n)");
        var bi = bars[pick];
        var mk = bi.Mark;
        var pts = bi.Bar.Points;
        double db = mk.Size.Diameter;
        var (bends, straights) = Bends(pts, db);
        double dev = 0;
        for (int i = 1; i < pts.Length; i++) dev += (pts[i] - pts[i - 1]).Length;
        var hooks = new List<string>();
        if (bends.Count > 0 && bends.All(x => x.Radius.In < 1e-6)) notes.Add("the model centerline has sharp corners at the bends (no bend radius modeled): pin diameters cannot be checked on this bar");
        if (bends.Count > 0 && straights.Count == bends.Count + 1)
        {
            void Hook(BarBend bnd, double tail, string end)
            {
                double ang = bnd.AngleDeg;
                string type = Math.Abs(ang - 90) < 8 ? "90°" : Math.Abs(ang - 135) < 8 ? "135°" : Math.Abs(ang - 180) < 8 ? "180°" : null!;
                if (type != null && tail <= 24 * db) hooks.Add($"{end}: {type} hook, tail {Fmt.FtIn(tail)} = {tail / db:0.0} db, pin {bnd.PinDiameter.Text} = {bnd.PinOverDb:0.0} db");
            }
            Hook(bends[0], straights[0].In * 0.0254, "start");
            Hook(bends[^1], straights[^1].In * 0.0254, "end");
        }
        double? sched = mk.Varies ? null : mk.Length;
        string? s0 = null, s1 = null;
        var al = bi.Model.Framing.Alignment;
        if (al.Elements.Count > 0)
        {
            var a0 = al.Inverse(pts[0].XY); var a1 = al.Inverse(pts[^1].XY);
            s0 = $"{u.FormatStation(a0.Station, 3)}, offset {u.FormatLength(a0.Offset)}"; s1 = $"{u.FormatStation(a1.Station, 3)}, offset {u.FormatLength(a1.Offset)}";
        }
        if (sched is { } sl && Math.Abs(sl - dev) > 0.0254 / 2) notes.Add($"developed centerline {Fmt.FtIn(dev)} vs scheduled {Fmt.FtIn(sl)} (difference {Fmt.FtIn(dev - sl)}; plan lengths are usually out-to-out sums)");
        return new BarResult
        {
            Bar = bi.Ref, Size = mk.Size.Name, Shape = $"{mk.Shape.Code} {mk.Shape.Description}", Location = mk.Location, BarsOfMark = bars.Count, Diameter = Len.Of(db),
            DevelopedLength = Len.Of(dev), ScheduledLength = sched is { } x ? Len.Of(x) : null, PrintedPin = mk.Shape.PinDiameter > 0 ? Len.Of(mk.Shape.PinDiameter) : null,
            MinPinAashto = Len.Of(mk.Size.MinPinDiameter), Bends = bends, Straights = straights, Hooks = hooks,
            Centerline = pts.Select(p => PlateMeasure.World(u, p)).ToList(), StartStation = s0, EndStation = s1, Notes = notes,
        };
    }

    /// <summary>Bends of a sampled centerline: runs of vertices with a turn (sampled arcs) merged into one bend with its radius; straights between.</summary>
    public static (List<BarBend> Bends, List<Len> Straights) Bends(IReadOnlyList<Vec3> p, double db)
    {
        int n = p.Count;
        var turn = new double[n];
        for (int i = 1; i + 1 < n; i++)
        {
            var a = (p[i] - p[i - 1]).Normalized(); var b = (p[i + 1] - p[i]).Normalized();
            turn[i] = Math.Acos(Math.Clamp(Vec3.Dot(a, b), -1, 1)) * 180 / Math.PI;
        }
        var bends = new List<BarBend>();
        var straights = new List<Len>();
        double maxArcStep = Math.Max(6 * db, 0.0254 * 3);
        int i0 = 1;
        double lastEnd = 0;   // arc length position of the last tangent point
        double along = 0;
        var cum = new double[n];
        for (int i = 1; i < n; i++) cum[i] = cum[i - 1] + (p[i] - p[i - 1]).Length;
        int k = 1;
        while (k < n - 1)
        {
            if (turn[k] < 0.5) { k++; continue; }
            int s = k, e = k;
            while (e + 1 < n - 1 && turn[e + 1] >= 0.5 && (p[e + 1] - p[e]).Length <= maxArcStep && turn[e + 1] <= 46) e++;
            double sweep = 0; for (int j = s; j <= e; j++) sweep += turn[j];
            double r = 0;
            if (e > s)
            {
                double chord = (p[e] - p[s]).Length;
                r = chord / (2 * Math.Sin(Math.Min(Math.PI, sweep * Math.PI / 180) / 2));
                if (sweep > 179) { var (_, rr) = Circle3(p[s], p[(s + e) / 2], p[e]); if (!double.IsNaN(rr)) r = rr; }
            }
            double arcLen = r * sweep * Math.PI / 180;
            straights.Add(Len.Of(Math.Max(0, cum[s] - lastEnd)));
            lastEnd = cum[e];
            bends.Add(new BarBend
            {
                No = bends.Count + 1, AngleDeg = Fmt.R(sweep, 2), Radius = Len.Of(r), PinDiameter = Len.Of(Math.Max(0, 2 * r - db)), PinOverDb = Fmt.R(Math.Max(0, 2 * r - db) / db, 2),
                At = new[] { Fmt.R(p[(s + e) / 2].X, 4), Fmt.R(p[(s + e) / 2].Y, 4), Fmt.R(p[(s + e) / 2].Z, 4) },
            });
            k = e + 1;
        }
        straights.Add(Len.Of(Math.Max(0, cum[n - 1] - lastEnd)));
        _ = i0; _ = along;
        return (bends, straights);
    }

    private static (Vec3 C, double R) Circle3(Vec3 a, Vec3 b, Vec3 c)
    {
        var ab = b - a; var ac = c - a;
        var n = Vec3.Cross(ab, ac);
        double d = 2 * Vec3.Dot(n, n);
        if (d < 1e-24) return (a, double.NaN);
        var cc = a + (Vec3.Cross(n, ab) * Vec3.Dot(ac, ac) + Vec3.Cross(ac, n) * Vec3.Dot(ab, ab)) * (1 / d);
        return (cc, (cc - a).Length);
    }

    /// <summary>Host solids of a bar: the deck slab / barriers / haunches (deck bars), the unit's concrete solids, the prestressed beam.</summary>
    private static List<MeasureElement> HostsOf(MeasureModel mm, BarItem b)
    {
        var els = mm.Elements(b.Model.Definition.Name);
        return b.Host switch
        {
            "sub" => els.Where(e => e.Ref.Kind == "sub" && e.Ref.Unit == b.Ref.Unit && e.IsConcrete).ToList(),
            "psbeam" => els.Where(e => e.Ref.Kind == "psbeam" && e.Ref.Mark == b.Ref.Assembly).ToList(),
            _ => b.Bar.Group switch
            {
                "Rail" => els.Where(e => e.Ref.Kind is "barrier").ToList(),
                "Diaphragm" => els.Where(e => e.Ref.Kind is "diaphragm" or "deck").ToList(),
                _ => els.Where(e => e.Ref.Kind is "deck" or "haunch" or "joint" or "caphaunch" && e.IsConcrete).ToList(),
            },
        };
    }

    public static CoverResult Cover(MeasureModel mm, string spec, string? bridge, string? unit, double step = 0.0254 * 3)
    {
        var bars = mm.BarsOf(spec, bridge, unit, out int? only);
        if (only is { } o1) bars = new List<BarItem> { bars[o1 - 1] };
        var notes = new List<string>(); if (mm.BarsNote != null) notes.Add(mm.BarsNote);
        var rows = new List<CoverRow>();
        var byHost = bars.GroupBy(b => (b.Model, b.Host, b.Ref.Unit, b.Host == "psbeam" ? b.Ref.Assembly : b.Bar.Group));
        string hostsText = "";
        foreach (var g in byHost)
        {
            var hosts = HostsOf(mm, g.First());
            if (hosts.Count == 0) { notes.Add($"no host concrete found for {g.Count()} bars ({g.Key.Host})"); continue; }
            hostsText = string.Join(", ", hosts.Select(h => h.Ref.Mark).Distinct().Take(8));
            var cover = new RebarCover(hosts.Select(h => h.Mesh), 0.6);
            var tris = HostTriangles(hosts);
            foreach (var bi in g)
            {
                double r = bi.Mark.Size.Diameter / 2;
                var by = new Dictionary<string, double>();
                var at = new Dictionary<string, Vec3>();
                bool outside = false;
                foreach (var p in Samples(bi.Bar.Points, step))
                {
                    if (!cover.Contains(p)) { outside = true; continue; }
                    foreach (var (face, d) in FaceDistances(tris, p, 0.6, cover))
                    {
                        double c = d - r;
                        if (!by.TryGetValue(face, out double cur) || c < cur) { by[face] = c; at[face] = p; }
                    }
                }
                var min = by.Count > 0 ? by.MinBy(kv => kv.Value) : default;
                rows.Add(new CoverRow
                {
                    Bar = bi.Ref, ByFace = by.ToDictionary(kv => kv.Key, kv => Len.Of(kv.Value)), Min = by.Count > 0 ? Len.Of(min.Value) : null, MinFace = by.Count > 0 ? min.Key : null,
                    MinAt = by.Count > 0 ? PlateMeasure.World(mm.Units, at[min.Key]) : null, Outside = outside,
                });
            }
        }
        if (rows.Any(r => r.Outside)) notes.Add($"{rows.Count(r => r.Outside)} bars have centerline points outside the host concrete");
        var minBy = new Dictionary<string, Len>();
        foreach (var r in rows) foreach (var (f, l) in r.ByFace) if (!minBy.TryGetValue(f, out var cur) || l.In < cur.In) minBy[f] = l;
        notes.Add("distances are to the tessellated host meshes (chord sag of curved decks is a mesh limit); faces shared between touching host solids are not counted");
        return new CoverResult { Mark = bars[0].Mark.Mark, Size = bars[0].Mark.Size.Name, Bars = bars.Count, Hosts = hostsText, MinByFace = minBy, PerBar = rows, Notes = notes };
    }

    private static IEnumerable<Vec3> Samples(Vec3[] pts, double step)
    {
        for (int i = 0; i < pts.Length; i++)
        {
            yield return pts[i];
            if (i + 1 < pts.Length)
            {
                int n = (int)((pts[i + 1] - pts[i]).Length / step);
                for (int k = 1; k <= n; k++) yield return Vec3.Lerp(pts[i], pts[i + 1], k / (n + 1.0));
            }
        }
    }

    private sealed class TriSet
    {
        public required Vec3[] A, B, C, N;
        public required Box3[] Box;
    }

    private static TriSet HostTriangles(List<MeasureElement> hosts)
    {
        var a = new List<Vec3>(); var b = new List<Vec3>(); var c = new List<Vec3>(); var n = new List<Vec3>(); var bx = new List<Box3>();
        foreach (var h in hosts)
        {
            var m = h.Mesh;
            for (int t = 0; t + 2 < m.Indices.Count; t += 3)
            {
                var p0 = m.Positions[m.Indices[t]]; var p1 = m.Positions[m.Indices[t + 1]]; var p2 = m.Positions[m.Indices[t + 2]];
                var cr = Vec3.Cross(p1 - p0, p2 - p0);
                if (cr.Length < 1e-14) continue;
                a.Add(p0); b.Add(p1); c.Add(p2); n.Add(cr.Normalized());
                var box = Box3.Empty; box.Add(p0); box.Add(p1); box.Add(p2); bx.Add(box);
            }
        }
        return new TriSet { A = a.ToArray(), B = b.ToArray(), C = c.ToArray(), N = n.ToArray(), Box = bx.ToArray() };
    }

    /// <summary>Nearest distance from p to host faces of each type within reach; faces whose outside is inside another host (shared faces) are skipped.</summary>
    private static IEnumerable<(string Face, double D)> FaceDistances(TriSet t, Vec3 p, double reach, RebarCover union)
    {
        var best = new Dictionary<string, double>();
        for (int i = 0; i < t.A.Length; i++)
        {
            var b = t.Box[i];
            if (p.X < b.Min.X - reach || p.X > b.Max.X + reach || p.Y < b.Min.Y - reach || p.Y > b.Max.Y + reach || p.Z < b.Min.Z - reach || p.Z > b.Max.Z + reach) continue;
            var q = Geo3.ClosestOnTriangle(p, t.A[i], t.B[i], t.C[i]);
            double d = (q - p).Length;
            if (d > reach) continue;
            string face = t.N[i].Z > 0.7 ? "top" : t.N[i].Z < -0.7 ? "bottom" : "side";
            if (best.TryGetValue(face, out double cur) && d >= cur) continue;
            if (union.Contains(q + t.N[i] * 0.003)) continue;   // shared with another host solid: not a free face
            best[face] = d;
        }
        return best.Select(kv => (kv.Key, kv.Value));
    }

    public static SpacingResult Spacing(MeasureModel mm, string spec, string? bridge, string? unit, string along, double tol)
    {
        var u = mm.Units;
        var bars = mm.BarsOf(spec, bridge, unit, out _);
        var notes = new List<string>(); if (mm.BarsNote != null) notes.Add(mm.BarsNote);
        var m = bars[0].Model;
        var cents = bars.Select(b => Centroid(b.Bar.Points)).ToList();
        List<double> pos;
        string desc;
        double? s0 = null;
        if (along.StartsWith("girder:", StringComparison.OrdinalIgnoreCase))
        {
            var g = MeasureModel.Girder(m, along[7..]) ?? throw new ArgumentException($"No girder '{along[7..]}'.");
            pos = cents.Select(c => Project(g.Path, c)).ToList();
            desc = $"distance along girder {MeasureModel.GirderName(m, g)} (top of web path)";
        }
        else if (along.Equals("alignment", StringComparison.OrdinalIgnoreCase))
        {
            var al = m.Framing.Alignment;
            pos = cents.Select(c => al.StationToDistance(al.Inverse(c.XY).Station)).ToList();
            desc = $"distance along alignment {al.Name} (bar centroids projected radially)";
            s0 = 0;
        }
        else
        {
            // principal axis of the centroids, or the given vector
            Vec3 axis;
            if (along.Contains(',')) { axis = Geo3.Parse(along).Normalized(); desc = $"along ({axis.X:0.###}, {axis.Y:0.###}, {axis.Z:0.###})"; }
            else { axis = Principal(cents); desc = $"along the principal axis of the bar centroids ({axis.X:0.###}, {axis.Y:0.###}, {axis.Z:0.###})"; }
            var o = cents[0];
            pos = cents.Select(c => Vec3.Dot(c - o, axis)).ToList();
        }
        // lines: bars at the same position (layers / pieces of one line) collapse
        var order = Enumerable.Range(0, bars.Count).OrderBy(i => pos[i]).ToList();
        var (centers, of) = Geo2.Cluster(pos, Math.Max(tol, 0.0254 / 4));
        int perLine = centers.Count > 0 ? (int)Math.Round(bars.Count / (double)centers.Count) : 1;
        if (centers.Count < bars.Count) notes.Add($"{bars.Count} bars on {centers.Count} lines (bars within {Fmt.FtIn(Math.Max(tol, 0.0254 / 4))} of the same position = one line: layers or lapped pieces)");
        var sp = centers.Zip(centers.Skip(1), (a, b) => b - a).ToList();
        string? fs = null, ls = null;
        if (along.Equals("alignment", StringComparison.OrdinalIgnoreCase) && centers.Count > 0)
        {
            var al = m.Framing.Alignment;
            fs = u.FormatStation(al.DistanceToStation(centers[0]), 3); ls = u.FormatStation(al.DistanceToStation(centers[^1]), 3);
        }
        double first = centers.Count > 0 ? centers[0] : 0;
        return new SpacingResult
        {
            Mark = bars[0].Mark.Mark, Along = desc, Bars = bars.Count, Lines = centers.Count, Positions = centers.Select(c => Fmt.Short(u, c - first)).ToList(),
            FirstStation = fs, LastStation = ls, Chain = Fmt.Chain(sp, Math.Max(tol, 0.0254 / 8)), Runs = Fmt.Runs(sp, Math.Max(tol, 0.0254 / 8)),
            Total = Len.Of(centers.Count > 1 ? centers[^1] - centers[0] : 0), Notes = notes,
        };
    }

    private static Vec3 Centroid(Vec3[] pts)
    {
        double w = 0; var c = Vec3.Zero;
        for (int i = 0; i + 1 < pts.Length; i++) { double l = (pts[i + 1] - pts[i]).Length; c += (pts[i] + pts[i + 1]) * (0.5 * l); w += l; }
        return w > 0 ? c * (1 / w) : pts[0];
    }

    private static double Project(Framing.GirderPath path, Vec3 c)
    {
        double best = double.MaxValue, bs = 0;
        for (int i = 0; i + 1 < path.Count; i++)
        {
            var a = path.WebTop[i].XY; var b = path.WebTop[i + 1].XY;
            var ab = b - a; double l2 = ab.LengthSquared;
            double t = l2 < 1e-18 ? 0 : Math.Clamp(Vec2.Dot(c.XY - a, ab) / l2, 0, 1);
            double d = (a + ab * t - c.XY).Length;
            if (d < best) { best = d; bs = path.S[i] + (path.S[i + 1] - path.S[i]) * t; }
        }
        return bs;
    }

    private static Vec3 Principal(List<Vec3> pts)
    {
        if (pts.Count < 2) return new Vec3(1, 0, 0);
        var c = new Vec3(pts.Average(p => p.X), pts.Average(p => p.Y), pts.Average(p => p.Z));
        double xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
        foreach (var p in pts) { var d = p - c; xx += d.X * d.X; xy += d.X * d.Y; xz += d.X * d.Z; yy += d.Y * d.Y; yz += d.Y * d.Z; zz += d.Z * d.Z; }
        var v = (pts[^1] - pts[0]).Length > 1e-9 ? (pts[^1] - pts[0]).Normalized() : new Vec3(1, 0, 0);
        for (int it = 0; it < 60; it++)
        {
            var w = new Vec3(xx * v.X + xy * v.Y + xz * v.Z, xy * v.X + yy * v.Y + yz * v.Z, xz * v.X + yz * v.Y + zz * v.Z);
            if (w.Length < 1e-15) break;
            v = w.Normalized();
        }
        return v;
    }

    public static LapResult Laps(MeasureModel mm, string spec, string? bridge, string? unit, double? fcKsi = null)
    {
        var u = mm.Units;
        var bars = mm.BarsOf(spec, bridge, unit, out _);
        var mk = bars[0].Mark;
        var notes = new List<string>(); if (mm.BarsNote != null) notes.Add(mm.BarsNote);
        var m = bars[0].Model;
        // partners: bars of the same size in the same host / group (other marks included)
        var all = mm.Bars(m.Definition.Name).Where(b => b.Mark.Size.Name == mk.Size.Name && b.Host == bars[0].Host && b.Ref.Unit == bars[0].Ref.Unit && b.Bar.Group == bars[0].Bar.Group).Union(bars).ToList();
        double db = mk.Size.Diameter;
        double near = Math.Max(6 * db, 0.0254 * 6);
        var rows = new List<LapRow>();
        var seen = new HashSet<(int, int)>();
        var idx = all.Select((b, i) => (b, i)).ToDictionary(x => x.b, x => x.i);
        // grid on the bar end points for candidates
        foreach (var a in bars)
        {
            var pa0 = a.Bar.Points[0]; var pa1 = a.Bar.Points[^1];
            var da = pa1 - pa0; double la = da.Length;
            if (la < 1e-6) continue;
            var ua = da * (1 / la);
            foreach (var b in all)
            {
                if (ReferenceEquals(a, b)) continue;
                int ia = idx[a], ib = idx[b];
                var key = (Math.Min(ia, ib), Math.Max(ia, ib));
                if (seen.Contains(key)) continue;
                var bb = b.Bounds; var ab = a.Bounds;
                if (bb.Min.X > ab.Max.X + near || bb.Max.X < ab.Min.X - near || bb.Min.Y > ab.Max.Y + near || bb.Max.Y < ab.Min.Y - near || bb.Min.Z > ab.Max.Z + near || bb.Max.Z < ab.Min.Z - near) continue;
                var pb0 = b.Bar.Points[0]; var pb1 = b.Bar.Points[^1];
                var db3 = pb1 - pb0; double lb = db3.Length;
                if (lb < 1e-6) continue;
                if (Math.Abs(Vec3.Dot(ua, db3 * (1 / lb))) < 0.999) continue;   // parallel within 2.5 deg
                double t0 = Vec3.Dot(pb0 - pa0, ua), t1 = Vec3.Dot(pb1 - pa0, ua);
                double lo = Math.Max(0, Math.Min(t0, t1)), hi = Math.Min(la, Math.Max(t0, t1));
                double lap = hi - lo;
                if (lap <= db) continue;
                if (lap > 0.9 * Math.Min(la, lb)) continue;   // a full overlap is a neighbouring bar, not a lap
                var mid = pa0 + ua * ((lo + hi) / 2);
                var foot = pb0 + db3 * (Vec3.Dot(mid - pb0, db3) / (lb * lb));
                double lateral = (mid - foot).Length;
                if (lateral > near) continue;
                seen.Add(key);
                double center = (lo + hi) / 2;
                string? sta = null;
                var al = m.Framing.Alignment;
                if (al.Elements.Count > 0) sta = u.FormatStation(al.Inverse(mid.XY).Station, 2);
                rows.Add(new LapRow
                {
                    BarA = $"{a.Mark.Mark}#{bars.IndexOf(a) + 1}", BarB = $"{b.Mark.Mark}#{all.Where(x => x.Mark.Mark == b.Mark.Mark).ToList().IndexOf(b) + 1}",
                    Lap = Len.Of(lap), Clear = Len.Of(Math.Max(0, lateral - db)), Center = Fmt.Long(u, Vec3.Dot(mid, ua)), CenterStation = sta,
                });
            }
        }
        // lines and stagger: group laps by the lateral position of their bars
        double? minStagger = null, share = null;
        if (rows.Count > 1)
        {
            var dir = (bars[0].Bar.Points[^1] - bars[0].Bar.Points[0]).Normalized();
            var lat = Vec3.Cross(new Vec3(0, 0, 1), dir).Normalized();
            var lapPts = rows.Select(r => r.Center).ToList();
            double lapLen = rows.Average(r => r.Lap.In) * 0.0254;
            // share of laps whose centers fall within one lap length of each other (crude section count)
            int maxIn = 0;
            var sortedC = lapPts.Select(x => u.FromDisplay(x, LengthKind.Long)).OrderBy(x => x).ToList();
            for (int i = 0, j = 0; i < sortedC.Count; i++) { while (sortedC[i] - sortedC[j] > lapLen) j++; maxIn = Math.Max(maxIn, i - j + 1); }
            share = Fmt.R(100.0 * maxIn / rows.Count, 1);
            var diffs = new List<double>();
            for (int i = 1; i < sortedC.Count; i++) diffs.Add(sortedC[i] - sortedC[i - 1]);
            if (diffs.Count > 0) minStagger = diffs.Min();
            _ = lat;
        }
        Len? classB = null;
        if (fcKsi is { } fc) classB = Len.Of(RebarCatalog.ClassBLap(mk.Size, fc, 60, mk.Epoxy, 0.0254 * 2, 0.0254 * 6));
        if (rows.Count == 0) notes.Add("no lapped pairs found (parallel bars of the same size overlapping end to end within 6 db laterally)");
        return new LapResult
        {
            Mark = mk.Mark, Size = mk.Size.Name, Bars = bars.Count, Laps = rows.OrderBy(r => r.Center).ToList(), MinLap = rows.Count > 0 ? Len.Of(rows.Min(r => r.Lap.In) * 0.0254) : null,
            MaxLap = rows.Count > 0 ? Len.Of(rows.Max(r => r.Lap.In) * 0.0254) : null, ClassBLap = classB, MinStagger = minStagger is { } ms ? Len.Of(ms) : null,
            MaxShareSplicedPct = share, Notes = notes,
        };
    }
}
