using SBM.Core.Geometry;

namespace SBM.Core.Concrete;

/// <summary>
/// TRUE BEND RADII for every placed bar (user rule 2026-10-13: "no kinks anywhere - every bend has a pin diameter").
/// Generators may hand over a bar centerline with sharp corners (plan bend diagrams are out-to-out, script polylines, hooks given as one
/// vertex); <see cref="Apply"/> turns every corner into a circular arc of centerline radius pin / 2 + db / 2, tangent to both legs. It runs in ONE
/// place: <see cref="Project.RegenerationCache"/> right after each bridge's concrete / prestressed / substructure models are generated (before the
/// deck rebar check and before the models are cached), so the viewport, IFC (arcs come back as IfcArcIndex through <see cref="ArcFit"/>), the
/// database, drawings and checks all read the bent centerline.
/// <para>Pin diameter of a bar: the mark's <see cref="BarShape.PinDiameter"/> (the printed plan pin, or the generator's) when given, else
/// <see cref="StandardPin"/> = AASHTO LRFD 5.10.2.3 minimum bend diameters (the agencies of the sample plan sets - Iowa DOT, MnDOT, TxDOT, NCDOT,
/// ODOT - specify bends per AASHTO / CRSI; no state table that differs is known, so there is no per-state override).</para>
/// <para>Unchanged: straight bars, spirals (code SPIRAL), runs that are already sampled arcs or curves (a generator's own rounded bends, circular
/// hoops, bars following a curved deck - detected per vertex, see <see cref="Round"/>), turns too small to see (the corner is within 0.05 mm of its
/// arc). Scheduled lengths (marks) are NOT changed: the plan convention is the out-to-out sum; the bent 3D centerline is shorter and that
/// difference is expected.</para>
/// </summary>
public static class RebarBends
{
    /// <summary>Largest angle per arc segment (the generators' <see cref="BarShape.Rounded"/> uses the same 15 degrees).</summary>
    public const double StepDeg = 15;
    /// <summary>Corners closer than this to their arc are left as they are (invisible; e.g. bars following a curved deck).</summary>
    public const double MinDeviation = 0.00005;
    /// <summary>Longest segment between two vertices that count as samples of one existing curve.</summary>
    public const double CurveSampleSpacing = 0.3;
    /// <summary>Consecutive points closer than this (m) are merged before rounding.</summary>
    public const double MergeDistance = 0.0001;

    /// <summary>Shape codes of ties / stirrups / hoops (AASHTO "stirrups and ties": 4 db up to #5).</summary>
    public static bool IsLigature(string code) => code is "TIE" or "HAIRPIN" or "HOOP" or "CROSSTIE" or "STIRRUP" or "SSTR-S";

    /// <summary>
    /// Minimum inside bend (pin) diameter, AASHTO LRFD 5.10.2.3: stirrups and ties #3-#5 = 4 db; otherwise #3-#8 = 6 db, #9-#11 = 8 db, #14 / #18 = 10 db.
    /// </summary>
    public static double StandardPin(RebarSize size, bool ligature) =>
        ligature && size.Name is "#3" or "#4" or "#5" ? 4 * size.Diameter : size.MinPinDiameter;

    /// <summary>Pin diameter used for a mark's bends: the shape's (plan / generator) pin, else the standard minimum.</summary>
    public static double PinFor(BarMark m) => m.Shape.PinDiameter > 0 ? m.Shape.PinDiameter : StandardPin(m.Size, IsLigature(m.Shape.Code));

    /// <summary>Centerline bend radius of a mark: pin / 2 + db / 2.</summary>
    public static double CenterRadius(BarMark m) => PinFor(m) / 2 + m.Size.Diameter / 2;

    /// <summary>Counts from one <see cref="Apply"/> call.</summary>
    public sealed record Summary(int Bars, int BentBars, int Bends, int Reduced, int Unrounded)
    {
        public string Text => $"Rebar bends: {Bends} bends in {BentBars} of {Bars} placed bars rounded to the pin radius (pin / 2 + db / 2)" +
            (Reduced > 0 ? $"; {Reduced} with a smaller radius because a leg is shorter than the tangent length" : "") +
            (Unrounded > 0 ? $"; {Unrounded} reversals (> 175 deg) left sharp" : "") + ".";
    }

    /// <summary>
    /// Rounds the bends of every bar in <paramref name="bars"/> (marks looked up by (Unit, Mark), then Mark). Idempotent: a bar is processed once
    /// (<see cref="BarPlacement.BendsRounded"/>). Bars without a mark are left as they are.
    /// </summary>
    public static Summary Apply(IEnumerable<BarMark> marks, IList<BarPlacement> bars)
    {
        var byKey = new Dictionary<(string, string), BarMark>();
        var byMark = new Dictionary<string, BarMark>();
        foreach (var m in marks) { byKey.TryAdd((m.Unit, m.Mark), m); byMark.TryAdd(m.Mark, m); }
        int bent = 0, bends = 0, reduced = 0, sharp = 0, n = 0;
        var lk = new object();
        Parallel.For(0, bars.Count, () => (0, 0, 0, 0, 0), (i, _, acc) =>
        {
            var b = bars[i];
            if (b.BendsRounded) return acc;
            b.BendsRounded = true;
            if (!byKey.TryGetValue((b.Unit, b.Mark), out var mk) && !byMark.TryGetValue(b.Mark, out mk)) return acc;
            acc.Item1++;
            if (mk.Shape.Code == "SPIRAL" || b.Points.Length < 3) return acc;
            var res = Round(b.Points, CenterRadius(mk), out var st);
            if (st.Bends > 0) { b.Points = res; acc.Item2++; acc.Item3 += st.Bends; acc.Item4 += st.Reduced; }
            acc.Item5 += st.Unrounded;
            return acc;
        }, acc => { lock (lk) { n += acc.Item1; bent += acc.Item2; bends += acc.Item3; reduced += acc.Item4; sharp += acc.Item5; } });
        return new Summary(n, bent, bends, reduced, sharp);
    }

    /// <summary>Per-call counts of <see cref="Round"/>.</summary>
    public readonly record struct Stats(int Bends, int Reduced, int Unrounded, double MaxDeviation, double MinRadius = 0);

    /// <summary>
    /// Centerline with every CORNER replaced by a tangent circular arc of centerline radius <paramref name="radius"/> (sampled at most
    /// <see cref="StepDeg"/> per segment, at least 3 segments so <see cref="ArcFit"/> recovers it). A vertex is a corner unless
    /// (a) its arc would lie within <see cref="MinDeviation"/> of it, (b) it reverses (> 175 deg; counted, left sharp), or (c) it is a sample of
    /// an existing curve: turn &lt;= 25 deg and a neighbouring vertex within <see cref="CurveSampleSpacing"/> turns the same way by a similar angle
    /// (0.4 - 2.5 x) - a generator's rounded bend, a circular hoop, a helix. Adjacent corners share a short leg in proportion to their tangent
    /// lengths; a corner whose tangent does not fit gets the largest radius that does (counted as reduced). Closed loops (first = last point)
    /// round the closing corner too. With <paramref name="fitRadius"/> every corner takes the LARGEST radius that fits its legs, up to
    /// <paramref name="radius"/> (smooth paths through given points, e.g. PT tendons; <see cref="Stats.MaxDeviation"/> = largest corner-to-arc distance).
    /// </summary>
    public static Vec3[] Round(IReadOnlyList<Vec3> input, double radius, out Stats stats, bool fitRadius = false)
    {
        stats = default;
        var p = new List<Vec3>(input.Count);
        // Points closer than 0.1 mm are one point (generators' clipped ends can leave micro segments whose "corner" would be a 90 deg turn).
        for (int i = 0; i < input.Count; i++)
        {
            var v = input[i];
            if (p.Count == 0 || (v - p[^1]).Length > MergeDistance) p.Add(v);
            else if (i == input.Count - 1 && p.Count > 1) p[^1] = v;
        }
        int n = p.Count;
        if (n < 3 || radius <= 0) return input as Vec3[] ?? input.ToArray();
        bool closed = n >= 4 && (p[0] - p[^1]).Length < 1e-6;
        if (closed) { p.RemoveAt(n - 1); n--; }
        // Vertex i of a closed loop has neighbours i-1 / i+1 modulo n; an open polyline has corners 1 .. n-2.
        int Prev(int i) => closed ? (i - 1 + n) % n : i - 1;
        int Next(int i) => closed ? (i + 1) % n : i + 1;
        bool Interior(int i) => closed || (i > 0 && i < n - 1);
        var theta = new double[n];
        var bin = new Vec3[n];
        for (int i = 0; i < n; i++)
        {
            if (!Interior(i)) continue;
            var u = (p[i] - p[Prev(i)]).Normalized();
            var w = (p[Next(i)] - p[i]).Normalized();
            theta[i] = Math.Acos(Math.Clamp(Vec3.Dot(u, w), -1, 1));
            var c = Vec3.Cross(u, w);
            bin[i] = c.Length > 1e-12 ? c * (1 / c.Length) : default;
        }
        double maxTurn = 25 * Math.PI / 180;
        var corner = new bool[n];
        var t = new double[n];
        int unrounded = 0;
        for (int i = 0; i < n; i++)
        {
            if (!Interior(i) || theta[i] < 1e-9) continue;
            if (theta[i] > 175 * Math.PI / 180) { unrounded++; continue; }
            if (!fitRadius && radius * (1 / Math.Cos(theta[i] / 2) - 1) < MinDeviation) continue;
            if (!fitRadius && theta[i] <= maxTurn && IsCurveSample(i)) continue;
            corner[i] = true;
            t[i] = radius * Math.Tan(theta[i] / 2);
        }
        bool IsCurveSample(int i)
        {
            foreach (int j in new[] { Prev(i), Next(i) })
            {
                if (j < 0 || j >= n || !Interior(j) || theta[j] < 1e-9) continue;
                double r = theta[j] / theta[i];
                if (r < 0.4 || r > 2.5) continue;
                if ((p[j] - p[i]).Length > CurveSampleSpacing) continue;
                if (Vec3.Dot(bin[i], bin[j]) < 0.5) continue;
                return true;
            }
            return false;
        }
        if (!corner.Any(c => c)) { stats = new Stats(0, 0, unrounded, 0); return input as Vec3[] ?? input.ToArray(); }
        // Share each leg between the corners at its ends.
        int segs = closed ? n : n - 1;
        var scale = Enumerable.Repeat(1.0, n).ToArray();
        for (int k = 0; k < segs; k++)
        {
            int a = k, b = (k + 1) % n;
            double need = (corner[a] ? t[a] : 0) + (corner[b] ? t[b] : 0);
            // Smooth paths keep half of an end leg straight (e.g. a tendon's anchorage zone).
            if (fitRadius && !closed && (a == 0 || b == n - 1)) need *= 2;
            double len = (p[b] - p[a]).Length * (1 - 1e-9);
            if (need > len && need > 0)
            {
                double f = len / need;
                if (corner[a]) scale[a] = Math.Min(scale[a], f);
                if (corner[b]) scale[b] = Math.Min(scale[b], f);
            }
        }
        int bends = 0, reduced = 0;
        double maxDev = 0, minR = double.MaxValue;
        var arcs = new List<Vec3>[n];
        for (int i = 0; i < n; i++)
        {
            if (!corner[i]) continue;
            double ti = t[i] * scale[i];
            if (scale[i] < 0.99 && !fitRadius) reduced++;
            double r = ti / Math.Tan(theta[i] / 2);
            maxDev = Math.Max(maxDev, r * (1 / Math.Cos(theta[i] / 2) - 1));
            minR = Math.Min(minR, r);
            arcs[i] = Arc(p[Prev(i)], p[i], p[Next(i)], ti, r, theta[i]);
            bends++;
        }
        var o = new List<Vec3>(n + bends * 6);
        void Add(Vec3 q) { if (o.Count == 0 || (q - o[^1]).Length > 1e-7) o.Add(q); }
        if (closed)
        {
            // Start at the end of the first vertex's arc (or at the vertex), finish with that arc again.
            if (arcs[0] != null) Add(arcs[0][^1]); else Add(p[0]);
            for (int i = 1; i < n; i++) { if (arcs[i] != null) foreach (var q in arcs[i]) Add(q); else Add(p[i]); }
            if (arcs[0] != null) foreach (var q in arcs[0]) Add(q); else Add(p[0]);
        }
        else
            for (int i = 0; i < n; i++) { if (arcs[i] != null) foreach (var q in arcs[i]) Add(q); else Add(p[i]); }
        stats = new Stats(bends, reduced, unrounded, maxDev, minR);
        return o.ToArray();
    }

    /// <summary>Arc at corner <paramref name="b"/> between legs from <paramref name="a"/> and to <paramref name="c"/>: tangent length
    /// <paramref name="tl"/>, radius <paramref name="r"/>, turn <paramref name="th"/>; from the tangent point on the first leg to the one on the second.</summary>
    private static List<Vec3> Arc(Vec3 a, Vec3 b, Vec3 c, double tl, double r, double th)
    {
        var u = (b - a).Normalized();
        var v = (c - b).Normalized();
        var start = b - u * tl;
        var toC = (v - u * Vec3.Dot(u, v)).Normalized();     // from the first tangent point toward the center
        var center = start + toC * r;
        var rs = start - center;
        var k = Vec3.Cross(Vec3.Cross(u, v).Normalized(), rs);
        int steps = Math.Max(3, (int)Math.Ceiling(th / (StepDeg * Math.PI / 180) - 1e-9));
        var res = new List<Vec3>(steps + 1);
        for (int s = 0; s <= steps; s++)
        {
            double ang = th * s / steps;
            res.Add(center + rs * Math.Cos(ang) + k * Math.Sin(ang));
        }
        res[^1] = b + v * tl;                                  // exact tangent point
        return res;
    }
}
