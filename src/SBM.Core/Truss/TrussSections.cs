using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Truss;

/// <summary>One solid of a built-up section: a simple polygon in the section plane (x across, y up; CCW), swept along the member axis.</summary>
public sealed record TrussSectionPiece(Vec2[] Polygon, string Label, double Length = 0, double Offset = 0);

/// <summary>Lacing bar face of a section: bars lie in the plane y = <see cref="Y"/> .. Y + Thickness * Side (outside the face), spanning x = +/- HalfLines.</summary>
public sealed record TrussLacingFace(double Y, int Side, double HalfLines, TrussLacing Lacing);

/// <summary>A section as built: pieces, extents, rolled weight, lacing faces, notes / flag.</summary>
public sealed class TrussBuiltSection
{
    public required TrussSectionDefinition Definition { get; init; }
    public List<TrussSectionPiece> Pieces { get; } = new();
    public List<TrussLacingFace> Lacing { get; } = new();
    public double XMin { get; set; }
    public double XMax { get; set; }
    public double YMin { get; set; }
    public double YMax { get; set; }
    /// <summary>AISC nominal weight of a rolled shape found in the catalog (lb/ft; 0 = by volume).</summary>
    public double LbPerFt { get; set; }
    /// <summary>Thickness of the outermost ply at the +/- x faces (rivet grip).</summary>
    public double OuterPly { get; set; }
    /// <summary>Thickness of the web (H / rolled: the web; Box: the plies of one side) - stringer end clearance to a floor beam web.</summary>
    public double WebThickness { get; set; }
    /// <summary>UNRESOLVED dimension text (null = fully dimensioned).</summary>
    public string? Flag { get; set; }
    public List<string> Notes { get; } = new();
    public string Description => Definition.Label.Length > 0 ? Definition.Label : Definition.Name;
    public double HalfWidth => Math.Max(Math.Abs(XMin), Math.Abs(XMax));
    public double Depth => YMax - YMin;
    /// <summary>Gross area of the pieces (lacing bars excluded).</summary>
    public double Area => Pieces.Sum(p => Math.Abs(MeshBuilder.SignedArea(p.Polygon)));
}

/// <summary>Generic riveted / rolled truss member sections (<see cref="TrussSectionKind"/>) as pieces for exact extrusions.</summary>
public static class TrussSections
{
    private static Vec2[] Rect(double x0, double x1, double y0, double y1) =>
        new[] { new Vec2(Math.Min(x0, x1), Math.Min(y0, y1)), new Vec2(Math.Max(x0, x1), Math.Min(y0, y1)), new Vec2(Math.Max(x0, x1), Math.Max(y0, y1)), new Vec2(Math.Min(x0, x1), Math.Max(y0, y1)) };

    private static Vec2[] Ccw(IEnumerable<Vec2> p)
    {
        var a = p.ToArray();
        if (MeshBuilder.SignedArea(a) < 0) Array.Reverse(a);
        return a;
    }

    private static string L(double m) => FormatIn(m);

    /// <summary>Inches as plan text (fractions to 1/16).</summary>
    public static string FormatIn(double m)
    {
        double inch = m / 0.0254;
        int whole = (int)Math.Floor(inch + 1e-9);
        int six = (int)Math.Round((inch - whole) * 16);
        if (six == 16) { whole++; six = 0; }
        if (six == 0) return $"{whole}\"";
        int num = six, den = 16;
        while (num % 2 == 0) { num /= 2; den /= 2; }
        return whole > 0 ? $"{whole} {num}/{den}\"" : $"{num}/{den}\"";
    }

    /// <summary>Angle of a section: AISC v16 when the designation exists (legs ordered: on web, outstanding), else the explicit legs (sharp root).</summary>
    private static AngleSize? AngleOf(TrussSectionDefinition s, List<string> notes)
    {
        double lw = s.AngleLegOnWeb, lo = s.AngleLegOut, t = s.AngleThickness;
        if (s.Angle.Length > 0)
        {
            var a = SteelCatalog.FindAngle(s.Angle);
            if (a != null)
            {
                if (lw <= 0) { lw = a.LegA; lo = a.LegB; }
                // legs as placed: on the web = lw, outstanding = lo (the catalog orders long, short)
                return Math.Abs(lw - a.LegA) < 1e-6 ? a : a with { LegA = lw, LegB = lo };
            }
            notes.Add($"angle {s.Angle} is not in AISC v16 - built from the plan designation / dimensions with a sharp root");
            if ((lw <= 0 || lo <= 0 || t <= 0) && ParseAngle(s.Angle) is { } pa) { lw = pa.a; lo = pa.b; t = pa.t; }
        }
        if (lw <= 0 || lo <= 0 || t <= 0) return null;
        return new AngleSize(s.Angle.Length > 0 ? s.Angle : $"L{lw / 0.0254:0.###}x{lo / 0.0254:0.###}x{t / 0.0254:0.###}", lw, lo, t);
    }

    /// <summary>Legs and thickness from a designation "L7x4x7/8" / "L8x8x1-1/8" (inches); null when it does not parse.</summary>
    public static (double a, double b, double t)? ParseAngle(string name)
    {
        var m = System.Text.RegularExpressions.Regex.Match(name.Trim(), @"^L\s*([\d.]+)\s*[xX]\s*([\d.]+)\s*[xX]\s*(?:(\d+)[- ])?(\d+)(?:/(\d+))?$");
        if (!m.Success) return null;
        double F(string v) => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture);
        double t = m.Groups[5].Success ? F(m.Groups[4].Value) / F(m.Groups[5].Value) : F(m.Groups[4].Value);
        if (m.Groups[3].Success) t += F(m.Groups[3].Value);
        return (In(F(m.Groups[1].Value)), In(F(m.Groups[2].Value)), In(t));
    }

    public static TrussBuiltSection Build(TrussSectionDefinition s)
    {
        var r = new TrussBuiltSection { Definition = s };
        if (s.Unresolved.Length > 0) r.Flag = s.Unresolved;
        switch (s.Kind)
        {
            case TrussSectionKind.Box: Box(s, r); break;
            case TrussSectionKind.H: H(s, r); break;
            case TrussSectionKind.LacedChannels: Channels(s, r); break;
            case TrussSectionKind.Angles: Angles(s, r); break;
            case TrussSectionKind.Rolled: Rolled(s, r); break;
            case TrussSectionKind.Plate:
                r.Pieces.Add(new(Rect(-s.Width / 2, s.Width / 2, -s.Depth / 2, s.Depth / 2), $"PL {L(s.Width)} x {L(s.Depth)}"));
                break;
            case TrussSectionKind.Round:
            {
                int n = 24;
                double rr = s.Diameter / 2;
                r.Pieces.Add(new(Enumerable.Range(0, n).Select(i => Vec2.FromAngle(2 * Math.PI * i / n) * rr).ToArray(), $"round {L(s.Diameter)}"));
                break;
            }
            default:
                r.Pieces.Add(new(Rect(-s.Width / 2, s.Width / 2, -s.Depth / 2, s.Depth / 2), $"nominal envelope {L(s.Width)} x {L(s.Depth)}"));
                r.Flag ??= $"UNRESOLVED: section {s.Name} not dimensioned - nominal envelope {L(s.Width)} x {L(s.Depth)}.";
                break;
        }
        if (r.Pieces.Count == 0 || r.Pieces.Any(p => p.Polygon.Length < 3 || Math.Abs(MeshBuilder.SignedArea(p.Polygon)) < 1e-9))
        {
            // Incomplete input: an envelope of whatever is known, flagged.
            double w = s.Width > 0 ? s.Width : s.WebsOutToOut > 0 ? s.WebsOutToOut : In(12), d = s.Depth > 0 ? s.Depth : s.WebDepth > 0 ? s.WebDepth : In(12);
            r.Pieces.Clear();
            r.Pieces.Add(new(Rect(-w / 2, w / 2, -d / 2, d / 2), $"envelope {L(w)} x {L(d)}"));
            r.Flag ??= $"UNRESOLVED: section {s.Name} ({s.Kind}) input incomplete - envelope {L(w)} x {L(d)} built.";
        }
        if (s.WorkingLineOffset != 0)
            for (int i = 0; i < r.Pieces.Count; i++)
                r.Pieces[i] = r.Pieces[i] with { Polygon = r.Pieces[i].Polygon.Select(p => new Vec2(p.X, p.Y - s.WorkingLineOffset)).ToArray() };
        for (int i = 0; i < r.Pieces.Count; i++) r.Pieces[i] = r.Pieces[i] with { Polygon = Ccw(r.Pieces[i].Polygon) };
        var all = r.Pieces.SelectMany(p => p.Polygon).ToList();
        r.XMin = all.Min(p => p.X); r.XMax = all.Max(p => p.X); r.YMin = all.Min(p => p.Y); r.YMax = all.Max(p => p.Y);
        foreach (var lf in r.Lacing.ToList())
        {
            // lacing bars lie outside the face: extents grow
            double y1 = lf.Y + lf.Side * lf.Lacing.BarThickness;
            r.YMin = Math.Min(r.YMin, y1); r.YMax = Math.Max(r.YMax, y1);
        }
        return r;
    }

    // ---- riveted box: two web groups (plies stacked inward from the outer face), 4 angles at the web edges, cover plates, lacing on the open faces
    private static void Box(TrussSectionDefinition s, TrussBuiltSection r)
    {
        double d = s.WebDepth, tw = s.WebPlies.Sum(p => p.Thickness);
        if (d <= 0 || tw <= 0) return;
        var a = AngleOf(s, r.Notes);
        bool angIn = a != null && !s.AnglesOutside && s.AngleEdges != "none";
        double legZone = a != null ? d / 2 + s.AngleProjection - a.LegA : d / 2;   // the angle leg on the web reaches down to this |y|
        // plies reaching the inside angles (from the outside face inward, up to the last ply deeper than the angle leg zone)
        int kLast = 0;
        for (int k = 0; k < s.WebPlies.Count; k++) if ((s.WebPlies[k].Depth > 0 ? s.WebPlies[k].Depth : d) / 2 > legZone + 1e-6) kLast = k;
        double tReach = angIn ? s.WebPlies.Take(kLast + 1).Sum(p => p.Thickness) : tw;
        double outer;
        if (s.WebsOutToOut > 0)
        {
            outer = s.WebsOutToOut / 2;
            if (s.WebsBackToBack > 0 && Math.Abs(s.WebsOutToOut - (s.WebsBackToBack + 2 * tReach)) > In(1.0 / 32))
                r.Notes.Add($"CHECK section {s.Name}: o-o webs {L(s.WebsOutToOut)} vs b-b {L(s.WebsBackToBack)} + 2 x plies {L(tReach)} = {L(s.WebsBackToBack + 2 * tReach)}");
        }
        else if (s.WebsBackToBack > 0) outer = s.WebsBackToBack / 2 + tReach;
        else return;
        r.WebThickness = tw;
        double angleFace = outer - tReach;
        foreach (int sg in new[] { -1, 1 })
        {
            double x0 = outer;
            foreach (var p in s.WebPlies)
            {
                double pd = p.Depth > 0 ? p.Depth : d;
                r.Pieces.Add(new(Rect(sg * x0, sg * (x0 - p.Thickness), -pd / 2, pd / 2), p.Label.Length > 0 ? p.Label : $"web PL {L(pd)} x {L(p.Thickness)}"));
                x0 -= p.Thickness;
            }
        }
        r.OuterPly = s.WebPlies[0].Thickness;
        double c = angleFace;   // inside face the inside angles stand on
        double yTop = d / 2, yBot = -d / 2;
        bool top = s.AngleEdges is "both" or "top", bot = s.AngleEdges is "both" or "bottom";
        if (a != null && (top || bot))
        {
            double pr = s.AngleProjection;
            foreach (int sg in new[] { -1, 1 })
            {
                double face = s.AnglesOutside ? sg * outer : sg * c;
                int o = s.AnglesOutside ? sg : -sg;
                foreach (int e in new[] { 1, -1 })
                {
                    if (e == 1 && !top || e == -1 && !bot) continue;
                    // (m, v) = (y, x): heel at the web edge (+ projection), leg on the web toward the centre, outstanding leg along x
                    var poly = SteelCatalog.AnglePolygon(a, e * (d / 2 + pr), -e, face, o).Select(q => new Vec2(q.Y, q.X)).ToArray();
                    r.Pieces.Add(new(poly, $"L {a.Name}"));
                }
            }
            if (top) yTop = d / 2 + pr;
            if (bot) yBot = -d / 2 - pr;
            if (s.AnglesOutside) r.OuterPly = a.Thickness;
        }
        double y = yTop;
        foreach (var p in s.TopPlates)
        {
            r.Pieces.Add(new(Rect(-p.Width / 2, p.Width / 2, y, y + p.Thickness), p.Label.Length > 0 ? p.Label : $"cover PL {L(p.Width)} x {L(p.Thickness)}", p.Length, p.Offset));
            if (p.Perforated) r.Notes.Add($"section {s.Name}: perforated cover plate {p.Label} built solid (access holes not dimensioned)");
            y += p.Thickness;
        }
        double yb = yBot;
        foreach (var p in s.BottomPlates)
        {
            r.Pieces.Add(new(Rect(-p.Width / 2, p.Width / 2, yb - p.Thickness, yb), p.Label.Length > 0 ? p.Label : $"cover PL {L(p.Width)} x {L(p.Thickness)}", p.Length, p.Offset));
            if (p.Perforated) r.Notes.Add($"section {s.Name}: perforated cover plate {p.Label} built solid (access holes not dimensioned)");
            yb -= p.Thickness;
        }
        if (s.Lacing is { } lac)
        {
            double half = lac.RivetLines > 0 ? lac.RivetLines / 2 : 0.75 * outer;
            if (lac.Faces is "top" or "both" && s.TopPlates.Count == 0) r.Lacing.Add(new(yTop, 1, half, lac));
            if (lac.Faces is "bottom" or "both" && s.BottomPlates.Count == 0) r.Lacing.Add(new(yBot, -1, half, lac));
        }
    }

    // ---- riveted H / I: web + 4 angles (+ flange plates)
    private static void H(TrussSectionDefinition s, TrussBuiltSection r)
    {
        double d = s.WebDepth, tw = s.WebPlies.Count > 0 ? s.WebPlies[0].Thickness : 0;
        if (d <= 0 || tw <= 0) return;
        r.WebThickness = tw;
        Vec2 Map(Vec2 mn) => s.WebVertical ? new Vec2(mn.Y, mn.X) : new Vec2(mn.X, mn.Y);   // (m along the web, n normal to it)
        r.Pieces.Add(new(new[] { new Vec2(-d / 2, -tw / 2), new Vec2(d / 2, -tw / 2), new Vec2(d / 2, tw / 2), new Vec2(-d / 2, tw / 2) }.Select(Map).ToArray(), $"web PL {L(d)} x {L(tw)}"));
        var a = AngleOf(s, r.Notes);
        double edge = d / 2;
        if (a != null)
        {
            double pr = s.AngleProjection;
            foreach (int e in new[] { 1, -1 })
                foreach (int side in new[] { 1, -1 })
                    r.Pieces.Add(new(SteelCatalog.AnglePolygon(a, e * (d / 2 + pr), -e, side * tw / 2, side).Select(Map).ToArray(), $"L {a.Name}"));
            edge = d / 2 + pr;
        }
        foreach (int e in new[] { 1, -1 })
        {
            double m0 = edge;
            // web vertical (floor beam): top edge = TopPlates, bottom edge = BottomPlates; web across (web member): both edges TopPlates unless BottomPlates given
            foreach (var p in e < 0 && (s.WebVertical || s.BottomPlates.Count > 0) ? s.BottomPlates : s.TopPlates)
            {
                var poly = new[] { new Vec2(e * m0, -p.Width / 2), new Vec2(e * (m0 + p.Thickness), -p.Width / 2), new Vec2(e * (m0 + p.Thickness), p.Width / 2), new Vec2(e * m0, p.Width / 2) };
                r.Pieces.Add(new(poly.Select(Map).ToArray(), p.Label.Length > 0 ? p.Label : $"flange PL {L(p.Width)} x {L(p.Thickness)}", p.Length, p.Offset));
                m0 += p.Thickness;
            }
        }
        // outer ply at the gusset faces (web across the plane: the flange plate or the angle outstanding leg)
        r.OuterPly = s.TopPlates.Count > 0 ? s.TopPlates[^1].Thickness : a?.Thickness ?? tw;
    }

    private static void Channels(TrussSectionDefinition s, TrussBuiltSection r)
    {
        var c = SteelCatalog.FindChannel(s.Channel);
        if (c == null)
        {
            if (s.Depth <= 0 || s.FlangeWidth <= 0 || s.WebThickness <= 0 || s.FlangeThickness <= 0) { r.Notes.Add($"channel {s.Channel} not in AISC v16 and not dimensioned"); return; }
            c = new ChannelSize(s.Channel, s.Depth, s.FlangeWidth, s.WebThickness, s.FlangeThickness);
            r.Notes.Add($"channel {s.Channel} not in AISC v16 - plan dimensions, parallel flanges, sharp roots");
        }
        double b = s.BackToBack;
        if (b <= 0) return;
        foreach (int sg in new[] { 1, -1 })
        {
            int o = s.FlangesOut ? sg : -sg;
            r.Pieces.Add(new(SteelCatalog.ChannelPolygon(c, sg * b / 2, o).Select(q => new Vec2(q.Y, q.X)).ToArray(), $"C {c.Name}"));
        }
        r.WebThickness = c.WebThickness; r.OuterPly = c.WebThickness;
        r.LbPerFt = c.WeightPerFootLb > 0 && s.TopPlates.Count == 0 && s.BottomPlates.Count == 0 ? 2 * c.WeightPerFootLb : 0;
        double yt = c.Depth / 2, ybt = -c.Depth / 2;
        foreach (var p in s.TopPlates)
        {
            r.Pieces.Add(new(Rect(-p.Width / 2, p.Width / 2, yt, yt + p.Thickness), p.Label.Length > 0 ? p.Label : $"cover PL {L(p.Width)} x {L(p.Thickness)}", p.Length, p.Offset));
            if (p.Perforated) r.Notes.Add($"section {s.Name}: perforated cover plate {p.Label} built solid (access holes not dimensioned)");
            yt += p.Thickness;
        }
        foreach (var p in s.BottomPlates)
        {
            r.Pieces.Add(new(Rect(-p.Width / 2, p.Width / 2, ybt - p.Thickness, ybt), p.Label.Length > 0 ? p.Label : $"cover PL {L(p.Width)} x {L(p.Thickness)}", p.Length, p.Offset));
            if (p.Perforated) r.Notes.Add($"section {s.Name}: perforated cover plate {p.Label} built solid (access holes not dimensioned)");
            ybt -= p.Thickness;
        }
        if (s.Lacing is { } lac)
        {
            double half = lac.RivetLines > 0 ? lac.RivetLines / 2 : (s.FlangesOut ? b / 2 : b / 2 - c.FlangeWidth / 2);
            if (lac.Faces is "top" or "both") r.Lacing.Add(new(c.Depth / 2, 1, half, lac));
            if (lac.Faces is "bottom" or "both") r.Lacing.Add(new(-c.Depth / 2, -1, half, lac));
            r.LbPerFt = 0;   // lacing adds weight: by volume
        }
    }

    private static void Angles(TrussSectionDefinition s, TrussBuiltSection r)
    {
        var a = AngleOf(s, r.Notes);
        if (a == null) return;
        if (s.AngleCount == 4)
        {
            double w = s.Width, d = s.Depth;
            if (w <= 0 || d <= 0) return;
            foreach (int sx in new[] { 1, -1 })
                foreach (int sy in new[] { 1, -1 })
                    r.Pieces.Add(new(SteelCatalog.AnglePolygon(a, sy * d / 2, -sy, sx * w / 2, -sx).Select(q => new Vec2(q.Y, q.X)).ToArray(), $"L {a.Name}"));
            r.OuterPly = a.Thickness;
            if (s.Lacing is { } lac)
            {
                double half = lac.RivetLines > 0 ? lac.RivetLines / 2 : w / 2 - a.LegB / 2;
                if (lac.Faces is "top" or "both") r.Lacing.Add(new(d / 2, 1, half, lac));
                if (lac.Faces is "bottom" or "both") r.Lacing.Add(new(-d / 2, -1, half, lac));
            }
            else if (a.WeightPerFootLb > 0) r.LbPerFt = 4 * a.WeightPerFootLb;
            return;
        }
        double g = s.BackToBack;
        foreach (int sg in new[] { 1, -1 })
            r.Pieces.Add(new(SteelCatalog.AnglePolygon(a, a.LegA / 2, -1, sg * g / 2, sg).Select(q => new Vec2(q.Y, q.X)).ToArray(), $"L {a.Name}"));
        r.OuterPly = a.Thickness;
        if (a.WeightPerFootLb > 0) r.LbPerFt = 2 * a.WeightPerFootLb;
    }

    private static void Rolled(TrussSectionDefinition s, TrussBuiltSection r)
    {
        string n = s.Designation.Trim();
        Vec2 Map(Vec2 q) => s.Rotated ? new Vec2(q.X, q.Y) : new Vec2(q.Y, q.X);   // (m along the depth, v across)
        if (SteelCatalog.FindWideFlange(n) is { } w)
        {
            r.Pieces.Add(new(SteelCatalog.WideFlangePolygon(w, -w.WebThickness / 2).Select(Map).ToArray(), w.Name));
            r.LbPerFt = w.WeightPerFootLb; r.WebThickness = w.WebThickness; r.OuterPly = w.FlangeThickness;
            return;
        }
        if (SteelCatalog.FindChannel(n) is { } c)
        {
            r.Pieces.Add(new(SteelCatalog.ChannelPolygon(c, -c.WebThickness / 2, 1).Select(Map).ToArray(), c.Name));
            r.LbPerFt = c.WeightPerFootLb; r.WebThickness = c.WebThickness; r.OuterPly = c.WebThickness;
            return;
        }
        if (SteelCatalog.FindAngle(n) is { } a)
        {
            r.Pieces.Add(new(SteelCatalog.AnglePolygon(a, a.LegA / 2, -1, 0, 1).Select(Map).ToArray(), a.Name));
            r.LbPerFt = a.WeightPerFootLb; r.WebThickness = a.Thickness; r.OuterPly = a.Thickness;
            return;
        }
        if (SteelCatalog.FindTee(n) is { } t)
        {
            r.Pieces.Add(new(SteelCatalog.TeePolygon(t, -t.StemThickness / 2).Select(q => Map(new Vec2(-q.X, q.Y))).ToArray(), t.Name));
            r.LbPerFt = t.WeightPerFootLb; r.WebThickness = t.StemThickness; r.OuterPly = t.FlangeThickness;
            return;
        }
        // Not in AISC v16 (1950s rolled shapes): plan dimensions (W-like, sharp roots), else nothing (-> flagged envelope).
        if (s.Depth > 0 && s.FlangeWidth > 0 && s.WebThickness > 0 && s.FlangeThickness > 0)
        {
            var ws = new WideFlangeSize(n, s.Depth, s.FlangeWidth, s.WebThickness, s.FlangeThickness);
            r.Pieces.Add(new(SteelCatalog.WideFlangePolygon(ws, -ws.WebThickness / 2).Select(Map).ToArray(), n));
            r.WebThickness = s.WebThickness; r.OuterPly = s.FlangeThickness;
            r.LbPerFt = s.WeightLbPerFt;
            r.Notes.Add($"rolled shape {n} is not in the program's AISC v16 extract (W, C, MC, L, WT) - built from the given dimensions (sharp roots){(s.WeightLbPerFt > 0 ? $", {s.WeightLbPerFt:0.#} lb/ft" : "")}");
            return;
        }
        r.Notes.Add($"rolled shape '{n}' is not in AISC v16 and has no dimensions");
        r.Flag ??= $"UNRESOLVED: rolled shape '{n}' of section {s.Name} is not in AISC v16 and its dimensions are not given - envelope built.";
    }
}
