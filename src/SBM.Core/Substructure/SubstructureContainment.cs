using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Geometry;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Substructure;

/// <summary>Generic cover / containment check of the plan-listed cap, pedestal and footing bars against their host concrete.</summary>
public sealed partial class SubstructureGenerator
{
    /// <summary>Tolerance of the containment check (1/8 in.).</summary>
    public static readonly double ContainmentTolerance = In(0.125);
    /// <summary>Clear distance from the cap faces to the legs of the cap hairpins (5c3 .. 5c6), printed "3 1/2\" CL." at both faces in the cap sections
    /// A-A / B-B (Design 116 PDF 20 and 40, Ramp G equivalents); every other cap bar keeps the general 2" ("UNLESS OTHERWISE NOTED").</summary>
    public static readonly double HairpinFaceClear = In(3.5);

    private static double PolyClear(IReadOnlyList<Vec2> poly, Vec2 p)
    {
        // signed distance: positive inside
        double d = double.MaxValue; bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var a = poly[j]; var b = poly[i];
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
            var e = b - a; double t = Math.Clamp(Vec2.Dot(p - a, e) / Math.Max(e.LengthSquared, 1e-12), 0, 1);
            d = Math.Min(d, Vec2.Distance(p, a + e * t));
        }
        return inside ? d : -d;
    }

    /// <summary>Signed distance (positive inside) like <see cref="PolyClear"/> but measured only to the edges that are not top faces (outward normal
    /// within 45 deg of up): under a pedestal block the cap top is not a free concrete face.</summary>
    private static double PolyClearNoTop(IReadOnlyList<Vec2> poly, Vec2 p, bool underTop)
    {
        double d = double.MaxValue, area = 0; bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++) area += poly[j].X * poly[i].Y - poly[i].X * poly[j].Y;
        double sign = area >= 0 ? 1 : -1;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var a = poly[j]; var b = poly[i];
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
            var e = b - a; double nx = sign * e.Y, ny = -sign * e.X;
            if (ny > Math.Abs(nx)) continue;
            double t = Math.Clamp(Vec2.Dot(p - a, e) / Math.Max(e.LengthSquared, 1e-12), 0, 1);
            d = Math.Min(d, Vec2.Distance(p, a + e * t));
        }
        return inside || underTop ? d : -d;
    }

    /// <summary>Which face of the cap outline (S-Z) is nearest to a point, by the edge's outward normal: top (normal within 45 deg of up),
    /// soffit (of down) or end.</summary>
    private static string OutlineFace(IReadOnlyList<Vec2> poly, double s, double z)
    {
        var p = new Vec2(s, z); double best = double.MaxValue; string face = "cap outline";
        double area = 0;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++) area += poly[j].X * poly[i].Y - poly[i].X * poly[j].Y;
        double sign = area >= 0 ? 1 : -1;                                // counter-clockwise: outward normal of edge e = (e.Y, -e.X)
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var a = poly[j]; var b = poly[i]; var e = b - a;
            double t = Math.Clamp(Vec2.Dot(p - a, e) / Math.Max(e.LengthSquared, 1e-12), 0, 1);
            var q = a + e * t; double d = Vec2.Distance(p, q);
            if (d >= best) continue;
            best = d;
            double nx = sign * e.Y, ny = -sign * e.X;
            face = Math.Abs(ny) > Math.Abs(nx) ? (ny > 0 ? "cap top" : "cap soffit") : "cap end";
        }
        return face;
    }

    /// <summary>Lateral setback of the cap section at (s, z) on one face (<paramref name="side"/> -1 = back face N0, +1 = ahead face N1, 0 = the larger of
    /// the two): the lower section is StepSetback narrower per face than the full width; each relief layer strip above its line gives back its share.
    /// Each face counts its OWN layer lines once per layer (earlier the lines of both faces were pooled: identical back / ahead lines counted every layer
    /// twice - a point above the lowest line read as 2/3 of the setback instead of 1/3 - and on 6A, whose faces differ, the higher face's lines set both).
    /// No relief line on the face = the full setback (conservative).</summary>
    internal static double LateralSetback(SubstructureUnit unit, double s, double z, int side = 0)
    {
        var ae = unit.Definition.Aesthetics;
        if (ae is not { Layers.Count: > 0 }) return 0;
        if (side == 0) return Math.Max(LateralSetback(unit, s, z, -1), LateralSetback(unit, s, z, 1));
        int m = ae.Layers.Count, above = 0;
        bool tagged = unit.ReliefLineTags.Count == unit.ReliefLines.Count;
        var idx = Enumerable.Range(0, unit.ReliefLines.Count).Where(i => unit.ReliefLines[i].Count > 1 && (!tagged || unit.ReliefLineTags[i].Side == side)).ToList();
        if (idx.Count == 0) return ae.StepSetback;                                           // no relief line on this face: the full setback (conservative)
        foreach (var layer in idx.GroupBy(i => tagged ? unit.ReliefLineTags[i].Layer : i))
        {
            // the run of this layer line over s; beyond the ends of the line (the cap tips) it continues level at its nearest end: the plans' relief arcs start at the tips
            var runs = layer.Select(i => unit.ReliefLines[i]).ToList();
            var run = runs.FirstOrDefault(l => s >= l[0].X && s <= l[^1].X)
                ?? runs.OrderBy(l => Math.Min(Math.Abs(s - l[0].X), Math.Abs(s - l[^1].X))).First();
            if (SubstructureUnit.Interp(run, Math.Clamp(s, run[0].X, run[^1].X)) > z) above++;
        }
        return ae.StepSetback / m * Math.Min(m, above);
    }

    /// <summary>
    /// Every plan-listed bar (cap, pedestal, footing groups): centerline minus bar radius must keep the plan clear cover from the host
    /// concrete (cap outline in S-Z incl. beak ends, the lower cap section in N, the pedestal blocks, the footing box) within 1/8".
    /// Reported per unit as one warning with the count and the worst violation per kind. Returns the number of bars outside.
    /// </summary>
    private int CheckContainment(SubstructureUnit unit, List<RawBar> bars)
    {
        var sd = unit.Definition; var r = sd.Reinforcement;
        bool asDesigned = sd.CapProfile != null && sd.CapProfile.Count >= 3;
        bool plan = r.CapBars.Count > 0 || sd.Footings.Any(f => f.Bars.Count > 0) || asDesigned;
        if (!plan) return 0;
        var poly = unit.CapOutline.Count >= 3 ? unit.CapOutline.ToList()
            : unit.CapTop.Concat(Enumerable.Reverse(unit.CapBottom)).ToList();
        double tol = ContainmentTolerance, capCov = r.CapCover;
        double tieDia = RebarCatalog.Size(r.ColumnSpiralSize).Diameter;
        var bad = new Dictionary<string, (int n, double worst, string at)>();
        int total = 0;
        var colSec = new Dictionary<int, Vec2[]>();
        Vec2[] ColSection(ColumnInfo c) => colSec.TryGetValue(c.Index, out var v) ? v
            : colSec[c.Index] = c.Shape == ColumnShape.Round ? Circle(c.SizeS / 2, 48) : Octagon(c.SizeS, c.SizeN, sd.ColumnChamferS, sd.ColumnChamferN);
        Vec2[]? flareSec = !double.IsNaN(sd.ColumnFlareElevation) && sd.ColumnFlareWidth > 0
            ? Octagon(sd.ColumnFlareWidth, sd.ColumnFlareDepth, sd.ColumnFlareChamferS, sd.ColumnFlareChamferN) : null;

        // slack (>= 0 fine) of one point against the cap, a footing or a column section; face = the governing face of the last evaluation
        string face = "";
        double CapSlack(double s, double n, double z, double radHere)
        {
            double sb0 = LateralSetback(unit, s, z, -1), sb1 = LateralSetback(unit, s, z, 1);
            double o = PolyClear(poly, new Vec2(s, z)), a = n - (unit.CapN0 + sb0), b = unit.CapN1 - sb1 - n;
            if (o <= a && o <= b) face = OutlineFace(poly, s, z);
            else face = a <= b ? "cap side N0" + (sb0 > 1e-9 ? " (lower section)" : "") : "cap side N1" + (sb1 > 1e-9 ? " (lower section)" : "");
            return Math.Min(o, Math.Min(a, b)) - radHere - capCov;
        }
        // pedestal bars / hoops: inside a pedestal block footprint the bar may rise above the cap top into the block (cover to the block sides and top);
        // below the cap top under the block the cap top is not a free face (the block sits on it)
        PedestalInfo? BlockAt(double s, double n) => unit.PedestalBlocks.FirstOrDefault(q => q.Height > In(0.125)
            && Math.Abs(s - q.S) <= q.LengthS / 2 + 1e-9 && Math.Abs(n - q.N) <= q.WidthN / 2 + 1e-9);
        double PedestalBarSlack(double s, double n, double z, double radHere)
        {
            var q = BlockAt(s, n);
            if (q == null) return CapSlack(s, n, z, radHere);
            double top = q.SeatZ - z, side = Math.Min(q.LengthS / 2 - Math.Abs(s - q.S), q.WidthN / 2 - Math.Abs(n - q.N));
            if (z > unit.CapTopAt(s, n))
            {
                face = top <= side ? "pedestal top" : "pedestal side";
                return Math.Min(top, side) - radHere - capCov;
            }
            double sb0 = LateralSetback(unit, s, z, -1), sb1 = LateralSetback(unit, s, z, 1);
            // under the block and below the cap top at (s, n) the point is in the concrete even where the S-Z outline (one cross-section) is lower (cross slope / stepped top)
            bool under = s > unit.CapS0 && s < unit.CapS1 && z >= unit.CapTopAt(s) - 1e-6;
            double o = PolyClearNoTop(poly, new Vec2(s, z), under), a = n - (unit.CapN0 + sb0), b = unit.CapN1 - sb1 - n;
            double m = Math.Min(top, Math.Min(o, Math.Min(a, b)));
            face = m == top ? "pedestal top" : m == o ? OutlineFace(poly, s, z) : a <= b ? "cap side N0" + (sb0 > 1e-9 ? " (lower section)" : "") : "cap side N1" + (sb1 > 1e-9 ? " (lower section)" : "");
            return m - radHere - capCov;
        }
        double CapOverColumn(double s, double n, double z, double radHere)
        {
            double t = unit.CapTopAt(s) - z, a = n - (unit.CapN0 + LateralSetback(unit, s, z, -1)), b = unit.CapN1 - LateralSetback(unit, s, z, 1) - n;
            face = t <= a && t <= b ? "cap top (column bar)" : (a <= b ? "cap side N0" : "cap side N1") + " (column bar)";
            return Math.Min(t, Math.Min(a, b)) - radHere - capCov;
        }
        double FootingSlack(FoundationInfo f, double s, double n, double z, double radHere, bool bottomMat, bool leavesTop = false)
        {
            double side = Math.Min(f.SizeS / 2 - Math.Abs(s - f.S), f.SizeN / 2 - Math.Abs(n - f.N)) - radHere - r.FootingSideClear;
            double top = leavesTop ? double.MaxValue : f.TopZ - z - radHere - r.FootingTopClear;   // dowels / column bars leave through the top
            double bot = z - radHere - (f.BottomZ + (bottomMat ? sd.PileEmbedment + r.PileTopClear : r.FootingSideClear));
            face = side <= top && side <= bot ? "footing side" : top <= bot ? "footing top" : bottomMat ? "footing bottom (pile heads)" : "footing bottom";
            return Math.Min(side, Math.Min(top, bot));
        }
        double ColumnSlack(ColumnInfo c, Vec2[] sec, double s, double n, double radHere, double cover)
        {
            face = "column face";
            return PolyClear(sec, new Vec2(s - c.S, n - c.N)) - radHere - cover;
        }

        foreach (var b in bars)
        {
            string loc = b.Location;
            bool hairpin = loc.EndsWith(" hairpins") && loc.Contains(" cap ");
            bool cap = loc.Contains(" cap "), pedBar = cap && (loc.EndsWith("pedestal bars") || loc.EndsWith("pedestal hoops")), colBar = loc.Contains(" column ") && !loc.Contains("dowels");
            bool dowel = loc.Contains("footing dowels"), ftg = loc.Contains("footing") && !dowel && !loc.Contains("column ");
            if (!cap && !colBar && !dowel && !ftg) continue;
            int colIdx = 0; var cm = System.Text.RegularExpressions.Regex.Match(loc, @"column (\d+)");
            if (cm.Success) colIdx = int.Parse(cm.Groups[1].Value) - 1;
            int ftgIdx = -1; var fm = System.Text.RegularExpressions.Regex.Match(loc, @"footing (\d+) ");
            if (fm.Success) ftgIdx = int.Parse(fm.Groups[1].Value) - 1;
            bool uplift = loc.Contains("uplift"), bottomMat = loc.Contains("bottom mat"), flare = loc.Contains("flare");
            bool verticals = loc.EndsWith("verticals") || loc.EndsWith("flare bars");
            double rad = b.Size.Diameter / 2, worst = double.MaxValue; string at = "", worstFace = ""; Vec3 worstP = default;
            var pts = new List<Vec3>();
            for (int i = 0; i < b.Points.Length; i++)
            {
                pts.Add(b.Points[i]);
                if (i + 1 < b.Points.Length)
                {
                    double len = (b.Points[i + 1] - b.Points[i]).Length; int n = (int)(len / In(6));
                    for (int k = 1; k <= n; k++) pts.Add(Vec3.Lerp(b.Points[i], b.Points[i + 1], k / (n + 1.0)));
                }
            }
            var column = colIdx < unit.Columns.Count ? unit.Columns[colIdx] : unit.Columns.FirstOrDefault();
            // dowels / column bars belong to the column whose centre is nearest (marks are shared between the columns of a pier)
            if ((dowel || colBar) && unit.Columns.Count > 1)
            {
                var q0 = b.Points[0]; double s0 = Vec2.Dot(q0.XY - unit.Origin, unit.D), n0 = Vec2.Dot(q0.XY - unit.Origin, unit.N);
                column = unit.Columns.OrderBy(c => Math.Pow(c.S - s0, 2) + Math.Pow(c.N - n0, 2)).First();
            }
            for (int pi = 0; pi < pts.Count; pi++)
            {
                var p = pts[pi];
                bool endPt = pi == 0 || pi == pts.Count - 1;           // a bar end is a flat face: no radius deduction
                double radHere = endPt ? 0 : rad;
                double s = Vec2.Dot(p.XY - unit.Origin, unit.D), n = Vec2.Dot(p.XY - unit.Origin, unit.N);
                double c;
                if (pedBar) c = PedestalBarSlack(s, n, p.Z, radHere);
                else if (cap)
                {
                    c = CapSlack(s, n, p.Z, radHere);
                    if (hairpin)
                    {
                        // sections A-A / B-B of every pier cap print 3 1/2" CL. from BOTH (full-width) cap faces to the hairpin legs (base = width - 7")
                        double h = Math.Min(n - unit.CapN0, unit.CapN1 - n) - radHere - HairpinFaceClear;
                        if (h < c) { c = h; face = (n - unit.CapN0 <= unit.CapN1 - n ? "cap side N0" : "cap side N1") + " (hairpin 3 1/2\" CL.)"; }
                    }
                }
                else if (ftg || dowel || colBar)
                {
                    FoundationInfo? f = ftgIdx >= 0 && ftgIdx < unit.Foundations.Count ? unit.Foundations[ftgIdx]
                        : column == null ? unit.Foundations.FirstOrDefault()
                        : unit.Foundations.FirstOrDefault(x => Math.Abs(x.S - column.S) < 1e-6) ?? unit.Foundations.FirstOrDefault();
                    if (ftg)
                    {
                        if (f == null) continue;
                        if (uplift && p.Z < f.BottomZ + sd.PileEmbedment) continue;           // the anchor passes through the pile web hole
                        c = FootingSlack(f, s, n, p.Z, radHere, bottomMat);
                    }
                    else
                    {
                        if (column == null) continue;
                        // embedded part (in the footing) -> footing cover; between footing top and the column top -> column cage envelope;
                        // above the column top -> the cap's cover envelope (column verticals only; the ties stop below the cap)
                        double zf = f?.TopZ ?? column.BottomZ;
                        if (p.Z < zf - 1e-9 && f != null && f.Type != FoundationType.DrilledShaft) c = FootingSlack(f, s, n, p.Z, radHere, false, true);
                        else if (p.Z > Math.Max(column.TopZ, unit.CapBottomAt(s)) + 1e-9) c = !verticals ? double.MaxValue
                            : PolyClear(ColSection(column), new Vec2(s - column.S, n - column.N)) >= 0
                                ? CapOverColumn(s, n, p.Z, radHere)   // inside the column footprint the soffit is not a free face
                                : CapSlack(s, n, p.Z, radHere);   // the column runs up to the soffit
                        else
                        {
                            bool tie = !verticals && !dowel;
                            Vec2[] sec = flare && flareSec != null ? flareSec : ColSection(column);
                            double cover = flare ? r.FlareCover + (tie ? 0 : RebarCatalog.Size(r.FlareTieSize).Diameter) : r.ColumnCover + (tie ? 0 : tieDia);
                            c = ColumnSlack(column, sec, s, n, radHere, cover);
                        }
                    }
                }
                else continue;
                if (c < worst) { worst = c; worstFace = face; worstP = new Vec3(s, n, p.Z); at =$"S {Sh(s)} N {Sh(n)} El {El(p.Z)}" + (column != null && (colBar || dowel) ? $" [col rel S {Sh(s - column.S)} N {Sh(n - column.N)}, col top {El(column.TopZ)}, soffit {El(unit.CapBottomAt(s))}]" : ""); }
            }
            if (worst < -tol)
            {
                total++;
                string kind = System.Text.RegularExpressions.Regex.Replace(loc, @"^.*?(cap|footing \d+|column \d+) ", "");
                if (dowel) kind = "footing dowels"; else if (colBar) kind = "column " + kind;
                unit.CoverViolations.Add(new CoverViolation(loc, kind, -worst, worstFace, worstP.X, worstP.Y, worstP.Z));
                bad.TryGetValue(kind, out var v);
                bad[kind] = v.n == 0 || worst < v.worst ? (v.n + 1, worst, at) : (v.n + 1, v.worst, v.at);
            }
        }
        if (sd.Aesthetics is { Layers.Count: > 0 } && r.CapBars.Count > 0)
            Warn($"{unit.Name}: UNRESOLVED clear cover at the textured cap face - the plans state 3 1/2\" CL. only to the hairpin legs (both faces, checked) and, for the "
                + "mockup panel only, \"2 INCHES CLEAR TO THE BOTTOM OF THE TEXTURED FACE\" (texture B up to 1 1/2\" deep, Design 116 PDF 83); a production cover for the other bars "
                + "at the textured face is not stated: they are checked against the general 2\" to the form face.");
        if (total > 0)
            Warn($"{unit.Name}: {total} plan-listed bar(s) violate the clear-cover envelope (bar surface to concrete face minus the plan clear cover, tolerance 1/8\"): "
                + string.Join("; ", bad.OrderBy(k => k.Value.worst).Select(k => $"{k.Key} x{k.Value.n} worst {Sh(-k.Value.worst)} at {k.Value.at}")) + " - kept as listed on the plans.");
        return total;
    }
}
