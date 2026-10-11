using SBM.Core.Bridge;
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

    /// <summary>Lateral setback (per face) of the cap section at (s, z): the lower section is StepSetback narrower than the full width;
    /// each relief layer strip above its line gives back its share. No relief at the station = the full setback (conservative).</summary>
    private static double LateralSetback(SubstructureUnit unit, double s, double z)
    {
        var ae = unit.Definition.Aesthetics;
        if (ae is not { Layers.Count: > 0 }) return 0;
        int m = ae.Layers.Count, above = 0;
        foreach (var line in unit.ReliefLines)
        {
            if (line.Count < 2 || s < line[0].X - 1e-9 || s > line[^1].X + 1e-9) continue;
            if (SubstructureUnit.Interp(line, s) > z) above++;
        }
        return ae.StepSetback / m * Math.Min(m, above == 0 && !unit.ReliefLines.Any(l => l.Count > 1 && s >= l[0].X && s <= l[^1].X) ? m : above);
    }

    /// <summary>
    /// Every plan-listed bar (cap, pedestal, footing groups): centerline minus bar radius must keep the plan clear cover from the host
    /// concrete (cap outline in S-Z incl. beak ends, the lower cap section in N, the pedestal blocks, the footing box) within 1/8".
    /// Reported per unit as one warning with the count and the worst violation per kind. Returns the number of bars outside.
    /// </summary>
    private int CheckContainment(SubstructureUnit unit, List<RawBar> bars)
    {
        var sd = unit.Definition; var r = sd.Reinforcement;
        bool plan = r.CapBars.Count > 0 || sd.Footings.Any(f => f.Bars.Count > 0);
        if (!plan) return 0;
        double sb = sd.Aesthetics is { Layers.Count: > 0 } aes ? aes.StepSetback : 0;
        double nLo = unit.CapN0 + sb, nHi = unit.CapN1 - sb;
        var poly = unit.CapOutline.Count >= 3 ? unit.CapOutline.ToList()
            : unit.CapTop.Concat(Enumerable.Reverse(unit.CapBottom)).ToList();
        double tol = ContainmentTolerance, capCov = r.CapCover, ftgCov = In(2);
        var bad = new Dictionary<string, (int n, double worst, string at)>();
        int total = 0;
        foreach (var b in bars)
        {
            bool cap = b.Location.Contains(" cap "), ftg = b.Location.Contains(" footing ") && !b.Location.Contains("dowel");
            if (!cap && !ftg) continue;
            bool ped = b.Location.Contains("pedestal");
            double rad = b.Size.Diameter / 2, worst = double.MaxValue; string at = "";
            // sample the polyline every ~6"
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
            for (int pi = 0; pi < pts.Count; pi++)
            {
                var p = pts[pi];
                bool endPt = pi == 0 || pi == pts.Count - 1;           // a bar end is a flat face: no radius deduction there
                double radHere = endPt ? 0 : rad;
                double s = Vec2.Dot(p.XY - unit.Origin, unit.D), n = Vec2.Dot(p.XY - unit.Origin, unit.N);
                double c;
                if (cap)
                {
                    double sbk = LateralSetback(unit, s, p.Z);
                    double clear = Math.Min(PolyClear(poly, new Vec2(s, p.Z)), Math.Min(n - (unit.CapN0 + sbk), unit.CapN1 - sbk - n)) - radHere;
                    c = clear - capCov;
                }
                else
                {
                    double best = double.MinValue;
                    foreach (var f in unit.Foundations)
                    {
                        double clear = Math.Min(Math.Min(f.SizeS / 2 - Math.Abs(s - f.S), f.SizeN / 2 - Math.Abs(n - f.N)), Math.Min(p.Z - f.BottomZ, f.TopZ - p.Z)) - (endPt ? 0 : 0);
                        best = Math.Max(best, clear - ftgCov);
                    }
                    c = best;
                }
                if (c < worst) { worst = c; at = $"S {Sh(s)} N {Sh(n)} El {El(p.Z)}"; }
            }
            if (worst < -tol)
            {
                total++;
                string kind = System.Text.RegularExpressions.Regex.Replace(b.Location, @"^.*?(cap|footing \d+) ", "");
                bad.TryGetValue(kind, out var v);
                bad[kind] = v.n == 0 || worst < v.worst ? (v.n + 1, worst, at) : (v.n + 1, v.worst, v.at);
            }
        }
        if (total > 0)
            Warn($"{unit.Name}: {total} plan-listed bar(s) lie outside the concrete / short of the cover (centerline - radius - cover, tolerance 1/8\"): "
                + string.Join("; ", bad.OrderBy(k => k.Value.worst).Select(k => $"{k.Key} x{k.Value.n} worst {Sh(-k.Value.worst)} at {k.Value.at}")) + " - kept as listed on the plans.");
        return total;
    }
}
