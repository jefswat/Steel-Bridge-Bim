using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Concrete;

/// <summary>Cover result of one placed bar of a <see cref="PlanBarSet"/> that violates its clear cover (bar kept as the plan places it).</summary>
public sealed record PlanBarCoverViolation(string Unit, string Mark, string Location, int Bar, double Excess, Vec3 At, string Face);

/// <summary>Result of <see cref="PlanBarSetBuilder.Build"/>.</summary>
public sealed class PlanBarSetResult
{
    public List<BarMark> Marks { get; } = new();
    public List<BarPlacement> Bars { get; } = new();
    public List<string> Warnings { get; } = new();
    public List<string> Notes { get; } = new();
    public List<PlanBarCoverViolation> Violations { get; } = new();
    /// <summary>Bars checked for cover (all placed bars of sets with CheckCover).</summary>
    public int Checked { get; set; }
}

/// <summary>
/// Turns <see cref="PlanBarSet"/>s into scheduled marks and placed bars: mark = the plan mark, count = placed + unplaced bars, scheduled length = the
/// plan's listed length (or the developed geometry), shape = the bar's own polyline unfolded into its plane with the printed dimensions; spirals are
/// generated. Every placed bar is checked for clear cover against the host concrete (<see cref="RebarCover"/>); violations are reported, never fixed.
/// </summary>
public static class PlanBarSetBuilder
{
    public static PlanBarSetResult Build(IEnumerable<PlanBarSet> sets, Func<Vec3, Vec3> toWorld, string unit, string group, IReadOnlyList<Mesh> hosts, bool epoxy = false)
    {
        var res = new PlanBarSetResult();
        var cover = new RebarCover(hosts);
        var seen = new HashSet<string>();
        foreach (var set in sets)
        {
            if (!seen.Add(set.Mark)) res.Warnings.Add($"{(unit == "" ? "Deck" : unit)}: plan bar mark {set.Mark} appears in more than one set - the marks must be unique.");
            var size = RebarCatalog.Size(set.Size);
            var bars = set.Bars.Select(b => PlanBarSet.Decode(b).Select(toWorld).ToArray()).Where(b => b.Length >= 2).ToList();
            foreach (var sp in set.Spirals) bars.Add(sp.Points().Select(toWorld).ToArray());
            double Dev(Vec3[] b) { double l = 0; for (int i = 1; i < b.Length; i++) l += (b[i] - b[i - 1]).Length; return l; }
            var lens = bars.Select(Dev).ToList();
            BarShape shape;
            List<double>? varying = null;
            if (set.Spirals.Count > 0 && set.Bars.Count == 0)
            {
                var sp = set.Spirals[0];
                var circle = Enumerable.Range(0, 25).Select(k => Vec2.FromAngle(2 * Math.PI * k / 24) * sp.Radius).ToArray();
                shape = new BarShape { Code = "SPIRAL", Description = $"Spiral, pitch {sp.Pitch / In(1):0.##} in.", Centerline = circle, FixedLength = set.ListedLength > 0 ? set.ListedLength : lens.Max() };
                if (set.ListedLength <= 0 && lens.Max() - lens.Min() > In(0.5)) varying = lens.ToList();
            }
            else if (bars.Count > 0)
            {
                shape = Unfold(bars[0], set);
                if (set.ListedLength <= 0 && lens.Max() - lens.Min() > In(0.5))
                {
                    varying = lens.ToList();
                    for (int k = 0; k < set.Unplaced; k++) varying.Add(lens.Average());
                }
            }
            else shape = set.ListedLength > 0 ? Fixed(BarShape.Straight(set.ListedLength), set) : BarShape.Straight(In(12));
            string loc = set.Location + (set.Unplaced > 0 ? $" ({set.Unplaced} of {bars.Count + set.Unplaced} not modeled - UNRESOLVED)" : "");
            res.Marks.Add(new BarMark
            {
                Mark = set.Mark, Size = size, Shape = shape, Location = loc, Unit = unit, Count = bars.Count + set.Unplaced,
                Epoxy = epoxy && set.Coating == "", Coating = set.Coating, VaryingLengths = varying,
            });
            foreach (var b in bars) res.Bars.Add(new BarPlacement { Mark = set.Mark, Points = b, Group = group, Unit = unit });
            if (set.Unresolved != "") res.Warnings.Add(set.Unresolved);
            if (set.Note != "") res.Notes.Add($"{(unit == "" ? "" : unit + " ")}{set.Mark}: {set.Note}");
            if (set.ListedLength > 0 && bars.Count > 0 && set.Spirals.Count == 0)
            {
                double avg = lens.Average();
                if (Math.Abs(avg - set.ListedLength) > In(1))
                    res.Notes.Add($"{(unit == "" ? "" : unit + " ")}{set.Mark}: modeled centerline {Ft(avg)} (avg of {bars.Count}) vs listed {Ft(set.ListedLength)} out-to-out (schedule uses the listed length).");
            }
            if (!set.CheckCover || !cover.HasHosts) continue;
            double rad = size.Diameter / 2;
            for (int i = 0; i < bars.Count; i++)
            {
                res.Checked++;
                var (slack, at, face) = cover.Check(bars[i], rad, set.Cover, set.CoverBottom, set.CoverTop);
                if (slack < -SubstructureToleranceForCover)
                    res.Violations.Add(new PlanBarCoverViolation(unit, set.Mark, set.Location, i, -slack, at, face));
            }
        }
        foreach (var g in res.Violations.GroupBy(v => v.Mark))
        {
            var w = g.MaxBy(v => v.Excess)!;
            res.Warnings.Add($"{(unit == "" ? "Deck" : unit)}: {g.Count()} bar(s) of mark {g.Key} ({w.Location}) violate the plan clear cover (tolerance 1/8\"), worst {In16(w.Excess)} at the {w.Face} - kept as the plan places them.");
        }
        return res;
    }

    /// <summary>Tolerance of the cover check (1/8 in.).</summary>
    public static readonly double SubstructureToleranceForCover = In(0.125);

    private static string Ft(double m)
    {
        double inch = Math.Round(m / In(1) * 16) / 16;
        int ft = (int)Math.Floor(inch / 12 + 1e-9);
        return $"{ft}'-{inch - ft * 12:0.###}\"";
    }

    private static string In16(double m) => $"{Math.Round(m / In(1) * 16) / 16:0.###}\"";

    private static BarShape Fixed(BarShape s, PlanBarSet set) => set.ListedLength > 0
        ? new BarShape { Code = s.Code, Description = s.Description, Centerline = s.Centerline, FixedLength = set.ListedLength, PinDiameter = s.PinDiameter }.WithDimsFrom(s)
        : s;

    private static BarShape WithDimsFrom(this BarShape t, BarShape s) { t.Dimensions.AddRange(s.Dimensions); return t; }

    /// <summary>The bar's polyline in its own plane (best plane through the points), with the printed dimensions on its legs when they match.</summary>
    internal static BarShape Unfold(Vec3[] pts, PlanBarSet set)
    {
        var p0 = pts[0];
        Vec3 u = default, n = default;
        for (int i = 1; i < pts.Length && u.Length < 1e-9; i++) { var d = pts[i] - p0; if (d.Length > 1e-6) u = d * (1 / d.Length); }
        double bestA = 0;
        for (int i = 1; i < pts.Length; i++)
        {
            var c = Vec3.Cross(u, pts[i] - p0);
            if (c.Length > bestA) { bestA = c.Length; n = c * (1 / c.Length); }
        }
        Vec2[] cl;
        if (bestA < 1e-6) { double l = 0; for (int i = 1; i < pts.Length; i++) l += (pts[i] - pts[i - 1]).Length; cl = new[] { Vec2.Zero, new Vec2(l, 0) }; }
        else
        {
            var v = Vec3.Cross(n, u);
            cl = pts.Select(p => new Vec2(Vec3.Dot(p - p0, u), Vec3.Dot(p - p0, v))).ToArray();
        }
        var shape = new BarShape
        {
            Code = string.IsNullOrEmpty(set.Shape) ? (cl.Length == 2 ? "STR" : "BENT") : set.Shape,
            Description = set.Location, Centerline = cl, FixedLength = set.ListedLength > 0 ? set.ListedLength : null,
        };
        if (set.Dims.Length > 0 && set.Dims.Length == cl.Length - 1)
            for (int i = 1; i < cl.Length; i++)
            {
                var d = cl[i] - cl[i - 1]; double l = d.Length;
                var nn = l > 1e-9 ? new Vec2(-d.Y / l, d.X / l) : new Vec2(0, 1);
                shape.Dimensions.Add(new BarDimension(((char)('A' + i - 1)).ToString(), set.Dims[i - 1], cl[i - 1], cl[i], nn));
            }
        else if (cl.Length == 2)
            shape.Dimensions.Add(new BarDimension("A", set.ListedLength > 0 ? set.ListedLength : (cl[1] - cl[0]).Length, cl[0], cl[1], new Vec2(0, 1)));
        return shape;
    }
}
