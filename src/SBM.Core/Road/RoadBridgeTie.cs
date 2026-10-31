using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Project;

namespace SBM.Core.Road;

/// <summary>Which end of a bridge a road end meets.</summary>
public enum BridgeEndKind { Begin, End }

/// <summary>
/// OPTIONAL tie of a road end to a bridge end (<see cref="RoadDefinition.BeginAtBridge"/> / <see cref="RoadDefinition.EndAtBridge"/>): the road's end
/// section follows the far end of that end's approach slab (<see cref="ApproachSlabDefinition"/>), or the deck end line when there is no approach slab,
/// offset by offset (skewed and kinked ends). The road and the bridge must share the alignment; otherwise the tie is reported and the station is used.
/// </summary>
public sealed class RoadBridgeTie
{
    public string Bridge { get; set; } = "";
    public BridgeEndKind End { get; set; }
}

public static class RoadBridgeTies
{
    /// <summary>Station of the tied road end as a function of the signed offset, or null (reported) when the tie cannot be made.</summary>
    public static Func<double, double>? StationAt(RoadDefinition road, RoadBridgeTie tie, Alignment roadAlignment, IReadOnlyList<BridgeModel?> bridges, List<Diagnostic> diag)
    {
        void Warn(string s) => diag.Add(new Diagnostic(DiagnosticSeverity.Warning, $"Road {road.Name}: {s}"));
        var b = bridges.OfType<BridgeModel>().FirstOrDefault(x => string.Equals(x.Definition.Name, tie.Bridge, StringComparison.OrdinalIgnoreCase));
        if (b == null) { Warn($"tie to bridge '{tie.Bridge}' - no such bridge; the road end station is used."); return null; }
        if (!string.Equals(b.Definition.AlignmentName, road.AlignmentName, StringComparison.OrdinalIgnoreCase))
        { Warn($"tie to {tie.Bridge}: the bridge is on alignment '{b.Definition.AlignmentName}', the road on '{road.AlignmentName}' - not tied; the road end station is used."); return null; }
        if (b.Framing.Deck is not { } deck) { Warn($"tie to {tie.Bridge}: the bridge has no deck - not tied."); return null; }
        bool begin = tie.End == BridgeEndKind.Begin;
        var slab = b.Concrete?.ApproachSlabs.FirstOrDefault(x => x.AtBegin == begin);
        // signed distance from the deck end line, + into the deck
        double F(double s, double o) { var p = roadAlignment.PointAt(s, o); var xy = new Geometry.Vec2(p.X, p.Y); return begin ? deck.FromBegin(xy) : deck.FromEnd(xy); }
        double guess = roadAlignment.Inverse(begin ? deck.BeginLinePoint : deck.EndLinePoint).Station;
        double Solve(double o, double target)
        {
            // bracket by symmetric expansion about the CL deck-end station, then bisection
            double a = guess - 1, bS = guess + 1, fa = F(a, o) - target, fb = F(bS, o) - target;
            for (int k = 0; k < 40 && Math.Sign(fa) == Math.Sign(fb); k++)
            {
                double w = (bS - a) * 0.8;
                a -= w; bS += w; fa = F(a, o) - target; fb = F(bS, o) - target;
            }
            if (Math.Sign(fa) == Math.Sign(fb)) return double.NaN;
            for (int it = 0; it < 80 && Math.Abs(bS - a) > 1e-9; it++)
            {
                double m = 0.5 * (a + bS), fm = F(m, o) - target;
                if (Math.Sign(fm) == Math.Sign(fa)) { a = m; fa = fm; } else { bS = m; fb = fm; }
            }
            return 0.5 * (a + bS);
        }
        var memo = new Dictionary<double, double>();
        double gapLen = slab == null ? 0 : slab.Definition.Gap + slab.Definition.Length;
        bool along = slab?.Definition.LengthMeasure is null or ApproachSlabLengthMeasure.AlongAlignment;
        return o =>
        {
            if (memo.TryGetValue(o, out double v)) return v;
            v = slab == null ? Solve(o, 0)
                : along ? Solve(o, -slab.Definition.Gap) + (begin ? -1 : 1) * slab.Definition.Length
                : Solve(o, -gapLen);
            if (double.IsNaN(v)) v = begin ? road.EndStation : road.BeginStation;
            memo[o] = v;
            return v;
        };
    }
}
