using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Steel;

/// <summary>
/// The one weld termination rule of every fillet weld generator (handoff 6e, user 2026-10-19 "welds don't respect hold backs"): a bead stops a hold-back
/// distance short of (a) free plate edges and corners, (b) corner clips / copes (the weld stops at the clip, it does not cross it) and (c) the end of
/// contact; the weld solid ends square there. Hold-back = the plan value (<see cref="WeldTerminationDefinition.HoldBack"/> on the bridge or a per-rule
/// override), else the AWS D1.5 practice default (weld size, min 1/4") flagged UNRESOLVED on the weld part.
/// Analytic generators (stiffeners, cross frame laps, sole plates, cross girders) shorten each run with <see cref="AddHeld"/>; generators that find the
/// joints on the meshes (<see cref="WeldFinder.TJoint"/>: arch rules, tub plates) chain the found beads and trim the chain ends (<see cref="Terminate"/>).
/// </summary>
public static class WeldTermination
{
    /// <summary>Shortest bead kept after trimming (1/16"); shorter runs are dropped.</summary>
    public const double MinBead = 0.0254 / 16;

    /// <summary>Hold-back for a weld of <paramref name="size"/>: the rule override when given (not NaN, >= 0), else the bridge's plan value, else the default.</summary>
    public static double HoldBack(WeldTerminationDefinition? d, double size, double overrideHb = double.NaN) =>
        !double.IsNaN(overrideHb) && overrideHb >= 0 ? overrideHb
        : d != null && !double.IsNaN(d.HoldBack) && d.HoldBack >= 0 ? d.HoldBack
        : WeldTerminationDefinition.Default(size);

    /// <summary>True when the hold-back comes from the plans (bridge value or rule override).</summary>
    public static bool FromPlans(WeldTerminationDefinition? d, double overrideHb = double.NaN) =>
        (!double.IsNaN(overrideHb) && overrideHb >= 0) || (d != null && !double.IsNaN(d.HoldBack) && d.HoldBack >= 0);

    /// <summary>UNRESOLVED dimension flag of a weld part's termination: the default (no plan value) or a plan value marked as an extrapolation; null = from the plans.</summary>
    public static Assumption? Flag(WeldTerminationDefinition? d, double size, Func<double, string> fmt, double overrideHb = double.NaN)
    {
        double hb = HoldBack(d, size, overrideHb);
        if (!FromPlans(d, overrideHb))
            return Assumption.UnresolvedDimension(
                UnresolvedFlag.Text("general notes / weld details", "fillet weld hold-back", "the plans give no termination of the fillet welds at plate edges, clips or the end of contact"),
                $"AWS D1.5 practice: beads stop {fmt(hb)} (weld size, min 1/4\") short of free edges, clips and the end of contact");
        if (double.IsNaN(overrideHb) && d is { Unresolved.Length: > 0 })
            return Assumption.UnresolvedDimension(d.Unresolved, $"beads stop {fmt(hb)} short of free edges, clips and the end of contact{(d.Source.Length > 0 ? $" ({d.Source})" : "")}");
        return null;
    }

    /// <summary>Text for weld descriptions: "held back 1/4" (p.120)".</summary>
    public static string Note(WeldTerminationDefinition? d, double size, Func<double, string> fmt, double overrideHb = double.NaN, string overrideSource = "")
    {
        double hb = HoldBack(d, size, overrideHb);
        string src = !double.IsNaN(overrideHb) && overrideHb >= 0 ? overrideSource : FromPlans(d) ? d!.Source : "AWS default, UNRESOLVED";
        return $"held back {fmt(hb)}{(src.Length > 0 ? $" ({src})" : "")}";
    }

    /// <summary>Shortens p0 -> p1 by <paramref name="h0"/> at p0 and <paramref name="h1"/> at p1; false when less than <see cref="MinBead"/> is left.</summary>
    public static bool Shorten(ref Vec3 p0, ref Vec3 p1, double h0, double h1)
    {
        var d = p1 - p0; double len = d.Length;
        if (len - h0 - h1 < MinBead) return false;
        d *= 1 / len;
        p0 += d * h0; p1 -= d * h1;
        return true;
    }

    /// <summary>Fillet bead p0 -> p1 held back <paramref name="h0"/> / <paramref name="h1"/> at its ends (leg directions need not be unit). Returns the root length built.</summary>
    public static double AddHeld(this Mesh m, Vec3 p0, Vec3 p1, Vec3 leg1, Vec3 leg2, double size, double h0, double h1)
    {
        if (!Shorten(ref p0, ref p1, h0, h1)) return 0;
        MeshBuilder.AddFilletWeld(m, p0, p1, leg1.Normalized(), leg2.Normalized(), size);
        return (p1 - p0).Length;
    }

    /// <summary>
    /// Beads found on the meshes (one per plate perimeter edge stretch) joined into runs - beads meeting end to end with the same legs and turning less
    /// than <paramref name="mergeDeg"/> (tessellated straight / curved edges); a sharper turn is a plate corner and ends both runs - and every run
    /// trimmed <paramref name="hb"/> at both ends (the end of contact, a corner, a clip). Closed loops are kept whole. Runs shorter than 2 hold-backs vanish.
    /// </summary>
    public static List<WeldFinder.Bead> Terminate(IReadOnlyList<WeldFinder.Bead> beads, double hb, double mergeDeg = 30)
    {
        var res = new List<WeldFinder.Bead>();
        var bs = beads.Where(b => (b.P1 - b.P0).Length > 1e-6).ToList();
        if (hb <= 0) { res.AddRange(bs); return res; }
        int n = bs.Count;
        static (long, long, long) K(Vec3 p) => ((long)Math.Round(p.X * 2e4), (long)Math.Round(p.Y * 2e4), (long)Math.Round(p.Z * 2e4));   // 50 um
        var at = new Dictionary<(long, long, long), List<(int bead, int end)>>();
        void Reg(Vec3 p, int i, int e)
        {
            var k = K(p);
            if (!at.TryGetValue(k, out var l)) at[k] = l = new List<(int, int)>();
            l.Add((i, e));
        }
        for (int i = 0; i < n; i++) { Reg(bs[i].P0, i, 0); Reg(bs[i].P1, i, 1); }
        double cosMerge = Math.Cos(mergeDeg * Math.PI / 180);
        Vec3 Dir(int i) => (bs[i].P1 - bs[i].P0).Normalized();
        // next[i, end] = the bead joined at that end (and which of its ends), or -1.
        var link = new (int bead, int end)[n, 2];
        for (int i = 0; i < n; i++) { link[i, 0] = (-1, 0); link[i, 1] = (-1, 0); }
        foreach (var l in at.Values)
        {
            if (l.Count != 2 || l[0].bead == l[1].bead) continue;
            var (i, ei) = l[0]; var (j, ej) = l[1];
            // direction leaving i through its end ei, and entering j through ej
            var di = ei == 1 ? Dir(i) : -Dir(i);
            var dj = ej == 0 ? Dir(j) : -Dir(j);
            if (Vec3.Dot(di, dj) < cosMerge) continue;
            if (Vec3.Dot(bs[i].LegOnA.Normalized(), bs[j].LegOnA.Normalized()) < 0.7 || Vec3.Dot(bs[i].LegOnB.Normalized(), bs[j].LegOnB.Normalized()) < 0.7) continue;
            link[i, ei] = (j, ej); link[j, ej] = (i, ei);
        }
        var used = new bool[n];
        void Emit(List<(int bead, bool fwd)> chain, bool closed)
        {
            var segs = chain.Select(c => c.fwd ? bs[c.bead] : new WeldFinder.Bead(bs[c.bead].P1, bs[c.bead].P0, bs[c.bead].LegOnA, bs[c.bead].LegOnB)).ToList();
            if (closed) { res.AddRange(segs); return; }
            double total = segs.Sum(s => (s.P1 - s.P0).Length);
            if (total - 2 * hb < MinBead) return;
            double s0 = hb, s1 = total - hb, acc = 0;
            foreach (var s in segs)
            {
                double l = (s.P1 - s.P0).Length, a = acc, b = acc + l;
                acc = b;
                double lo = Math.Max(a, s0), hi = Math.Min(b, s1);
                if (hi - lo < 1e-7) continue;
                var d = (s.P1 - s.P0) * (1 / l);
                res.Add(s with { P0 = s.P0 + d * (lo - a), P1 = s.P0 + d * (hi - a) });
            }
        }
        // open chains: start at a bead end with no link
        for (int i = 0; i < n; i++)
        {
            if (used[i]) continue;
            int startEnd = link[i, 0].bead < 0 ? 0 : link[i, 1].bead < 0 ? 1 : -1;
            if (startEnd < 0) continue;
            var chain = new List<(int, bool)>();
            int cur = i; int entry = startEnd;
            while (cur >= 0 && !used[cur])
            {
                used[cur] = true;
                chain.Add((cur, entry == 0));
                var nx = link[cur, 1 - entry];
                cur = nx.bead; entry = nx.end;
            }
            Emit(chain, false);
        }
        // remaining = closed loops
        for (int i = 0; i < n; i++)
        {
            if (used[i]) continue;
            var chain = new List<(int, bool)>();
            int cur = i, entry = 0;
            while (cur >= 0 && !used[cur])
            {
                used[cur] = true;
                chain.Add((cur, entry == 0));
                var nx = link[cur, 1 - entry];
                cur = nx.bead; entry = nx.end;
            }
            Emit(chain, true);
        }
        return res;
    }

    /// <summary>Root length of a bead list.</summary>
    public static double Length(IEnumerable<WeldFinder.Bead> beads) => beads.Sum(b => (b.P1 - b.P0).Length);

    /// <summary>Default hold-back text (inches) for messages without a unit system.</summary>
    public static string Inches(double m) => $"{m / 0.0254:0.###}\"";
}
