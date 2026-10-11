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

    /// <summary>Which face of the cap outline (S-Z) is nearest to a point: top (edge flatter than 45 deg above the point), soffit (below) or end.</summary>
    private static string OutlineFace(IReadOnlyList<Vec2> poly, double s, double z)
    {
        var p = new Vec2(s, z); double best = double.MaxValue; string face = "cap outline";
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var a = poly[j]; var b = poly[i]; var e = b - a;
            double t = Math.Clamp(Vec2.Dot(p - a, e) / Math.Max(e.LengthSquared, 1e-12), 0, 1);
            var q = a + e * t; double d = Vec2.Distance(p, q);
            if (d >= best) continue;
            best = d;
            face = Math.Abs(e.Y) < Math.Abs(e.X) ? (q.Y > z ? "cap top" : "cap soffit") : "cap end";
        }
        return face;
    }

    /// <summary>Lateral setback (per face) of the cap section at (s, z): the lower section is StepSetback narrower than the full width;
    /// each relief layer strip above its line gives back its share. No relief at the station = the full setback (conservative).</summary>
    private static double LateralSetback(SubstructureUnit unit, double s, double z)
    {
        var ae = unit.Definition.Aesthetics;
        if (ae is not { Layers.Count: > 0 }) return 0;
        int m = ae.Layers.Count, above = 0;
        if (!unit.ReliefLines.Any(l => l.Count > 1)) return ae.StepSetback;                  // no relief line at all: the full setback (conservative)
        foreach (var line in unit.ReliefLines)
        {
            if (line.Count < 2) continue;
            // beyond the ends of a line (the cap tips) the line continues level at its end elevation: the plans' relief arcs start at the tips
            double sx = Math.Clamp(s, line[0].X, line[^1].X);
            if (SubstructureUnit.Interp(line, sx) > z) above++;
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
            double sbk = LateralSetback(unit, s, z);
            double o = PolyClear(poly, new Vec2(s, z)), a = n - (unit.CapN0 + sbk), b = unit.CapN1 - sbk - n;
            if (o <= a && o <= b) face = OutlineFace(poly, s, z);
            else face = (a <= b ? "cap side N0" : "cap side N1") + (sbk > 1e-9 ? " (lower section)" : "");
            return Math.Min(o, Math.Min(a, b)) - radHere - capCov;
        }
        double CapOverColumn(double s, double n, double z, double radHere)
        {
            double sbk = LateralSetback(unit, s, z), t = unit.CapTopAt(s) - z, a = n - (unit.CapN0 + sbk), b = unit.CapN1 - sbk - n;
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
            bool cap = loc.Contains(" cap "), colBar = loc.Contains(" column ") && !loc.Contains("dowels");
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
                if (cap) c = CapSlack(s, n, p.Z, radHere);
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
        if (total > 0)
            Warn($"{unit.Name}: {total} plan-listed bar(s) violate the clear-cover envelope (bar surface to concrete face minus the plan clear cover, tolerance 1/8\"): "
                + string.Join("; ", bad.OrderBy(k => k.Value.worst).Select(k => $"{k.Key} x{k.Value.n} worst {Sh(-k.Value.worst)} at {k.Value.at}")) + " - kept as listed on the plans.");
        return total;
    }
}
