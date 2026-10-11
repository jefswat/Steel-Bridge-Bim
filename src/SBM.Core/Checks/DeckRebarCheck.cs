using System.Collections.Concurrent;
using SBM.Core.Concrete;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Steel;
using SBM.Core.Units;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Checks;

/// <summary>Numbers of one containment run (also written to the concrete model's notes).</summary>
public sealed record DeckRebarCheckResult(int Bars, double MinTopCover, double MinBottomCover, double MinEdgeDistance, double MinBeginDistance, double MinEndDistance,
    int TopCoverShort, int BottomCoverShort, int EdgeShort, int InsideBlockout, int SteelClashBars, double WorstSteelClash);

/// <summary>
/// Containment / cover check of the deck reinforcing (generic and plan-driven). Nothing is moved; everything outside tolerance is a diagnostic.
///  * top cover: finished grade to the TOP of each top-mat bar (bar centerline + radius) at its ends and middle;
///  * bottom cover: the bottom of each bottom-mat bar above the deck soffit (grade - slab thickness);
///  * edge distance: horizontal distance from the bar ends to the deck edges (the deck-edge table follows the station);
///  * ends: distance from the deck-end lines (the faces of the joint blockouts / end dams) and, where a blockout exists, whether the bar enters it;
///  * steel: bars against the highest steel surface under them (top flanges, field splice plates, fillers, bolts heads of the splices) - a bar whose underside is
///    lower than the steel top clashes with it (stud shafts are not tested: the plans leave their position relative to the bars to the field).
/// </summary>
public static class DeckRebarCheck
{
    public const double Tol = 0.0006;     // 0.6 mm drawing tolerance on elevations / distances

    public static DeckRebarCheckResult? Run(BridgeFraming f, SteelModel? steel, ConcreteModel cm, UnitSystem u)
    {
        var def = f.Definition;
        var deck = def.Deck;
        var a = f.Alignment;
        var dr = f.Deck;
        var bars = cm.Bars.Where(b => b.Group == "Deck" && b.Unit == "").ToList();
        if (bars.Count == 0 || dr == null) return null;
        var sizeOf = cm.Marks.Where(m => m.Unit == "").GroupBy(m => m.Mark).ToDictionary(g => g.Key, g => g.First().Size);
        var rd = deck.Reinforcement;
        double t = deck.Thickness;

        // ---- steel height map (cells of 0.1 m): highest up-facing steel surface in the girder field sections / splices
        const double Cell = 0.1;
        var topMap = new ConcurrentDictionary<(long, long), float>();
        if (steel != null)
        {
            var asm = steel.Assemblies.ToDictionary(x => x.Mark, x => x, StringComparer.Ordinal);
            Parallel.ForEach(steel.Parts.Where(p => p.Category is PartCategory.FlangePlate or PartCategory.SplicePlate or PartCategory.FillerPlate
                                                     && asm.TryGetValue(p.Assembly, out var aa) && aa.Kind is AssemblyKind.GirderFieldSection or AssemblyKind.FieldSplice), part =>
            {
                var pos = part.Mesh.Positions; var idx = part.Mesh.Indices;
                for (int i = 0; i + 2 < idx.Count; i += 3)
                {
                    var p0 = pos[idx[i]]; var p1 = pos[idx[i + 1]]; var p2 = pos[idx[i + 2]];
                    var n = Vec3.Cross(p1 - p0, p2 - p0);
                    if (n.Z <= 0.3 * n.Length) continue;           // upward-facing only
                    double zt = Math.Max(p0.Z, Math.Max(p1.Z, p2.Z));
                    long x0 = (long)Math.Floor(Math.Min(p0.X, Math.Min(p1.X, p2.X)) / Cell), x1 = (long)Math.Floor(Math.Max(p0.X, Math.Max(p1.X, p2.X)) / Cell);
                    long y0 = (long)Math.Floor(Math.Min(p0.Y, Math.Min(p1.Y, p2.Y)) / Cell), y1 = (long)Math.Floor(Math.Max(p0.Y, Math.Max(p1.Y, p2.Y)) / Cell);
                    for (long cx = x0; cx <= x1; cx++)
                        for (long cy = y0; cy <= y1; cy++)
                        {
                            double px = (cx + 0.5) * Cell, py = (cy + 0.5) * Cell;
                            if (!InTri(px, py, p0, p1, p2)) continue;
                            double z = PlaneZ(px, py, p0, n);
                            topMap.AddOrUpdate((cx, cy), (float)z, (_, old) => Math.Max(old, (float)z));
                        }
                }
            });
        }

        double minTop = double.MaxValue, minBot = double.MaxValue, minEdge = double.MaxValue, minBegin = double.MaxValue, minEnd = double.MaxValue;
        int topShort = 0, botShort = 0, edgeShort = 0, inBlock = 0, clash = 0;
        double worstClash = 0, maxIntr = 0;
        var worst = new List<(double pen, string where)>();
        var nb = dr.BeginLineDirection.Left; var ne = -dr.EndLineDirection.Left;
        double bw = def.Supports[0].ExpansionJoint?.BlockoutWidth ?? 0, ew = def.Supports[^1].ExpansionJoint?.BlockoutWidth ?? 0;
        var lk = new object();
        Parallel.ForEach(bars, bar =>
        {
            var pts = bar.Points;
            if (pts.Length < 2 || !sizeOf.TryGetValue(bar.Mark, out var size)) return;
            double r = size.Diameter / 2;
            // samples: ends + middle for cover / edges (Inverse is expensive)
            var sampleIdx = pts.Length > 2 ? new[] { 0, pts.Length / 2, pts.Length - 1 } : new[] { 0, pts.Length - 1 };
            double lMinTop = double.MaxValue, lMinBot = double.MaxValue, lMinEdge = double.MaxValue, lBeg = double.MaxValue, lEnd = double.MaxValue;
            bool top = false;
            foreach (int si in sampleIdx)
            {
                var p = pts[si];
                var so = a.Inverse(p.XY);
                double grade = deck.FinishedElevation(a, so.Station, so.Offset);
                double rel = p.Z - grade;
                top = rel > -t / 2;
                if (top) lMinTop = Math.Min(lMinTop, -(rel + r));            // grade to the top of the bar
                else lMinBot = Math.Min(lMinBot, (rel + t) - r);             // bar underside to the soffit
                lMinEdge = Math.Min(lMinEdge, Math.Min(so.Offset - deck.LeftAt(so.Station), deck.RightAt(so.Station) - so.Offset));
                lBeg = Math.Min(lBeg, Vec2.Dot(p.XY - dr.BeginLinePoint, nb));
                lEnd = Math.Min(lEnd, Vec2.Dot(p.XY - dr.EndLinePoint, ne));
            }
            // steel: sample along the bar every <= 0.15 m
            double pen = 0; string at = "";
            if (topMap.Count > 0)
            {
                for (int i = 1; i < pts.Length; i++)
                {
                    var p0 = pts[i - 1]; var p1 = pts[i];
                    double len = (p1 - p0).Length;
                    int n = Math.Max(1, (int)Math.Ceiling(len / 0.15));
                    for (int k = 0; k <= n; k++)
                    {
                        var p = Vec3.Lerp(p0, p1, (double)k / n);
                        if (topMap.TryGetValue(((long)Math.Floor(p.X / Cell), (long)Math.Floor(p.Y / Cell)), out float sz) && sz > p.Z - r + Tol)
                        {
                            double d = sz - (p.Z - r);
                            if (d > pen) { pen = d; at = $"{p.X:F2},{p.Y:F2}"; }
                        }
                    }
                }
            }
            lock (lk)
            {
                minTop = Math.Min(minTop, lMinTop); minBot = Math.Min(minBot, lMinBot); minEdge = Math.Min(minEdge, lMinEdge);
                minBegin = Math.Min(minBegin, lBeg); minEnd = Math.Min(minEnd, lEnd);
                if (lMinTop < rd.TopCover - Tol - In(0.25)) topShort++;
                if (lMinBot < rd.BottomCover - Tol - In(0.25)) botShort++;
                if (lMinEdge < rd.SideCover - Tol) edgeShort++;
                if ((bw > 0 && lBeg < bw - Tol) || (ew > 0 && lEnd < ew - Tol)) { inBlock++; maxIntr = Math.Max(maxIntr, Math.Max(bw - lBeg, ew - lEnd)); }
                if (pen > 0) { clash++; worstClash = Math.Max(worstClash, pen); if (worst.Count < 400) worst.Add((pen, $"{bar.Mark} at {at}")); }
            }
        });

        var res = new DeckRebarCheckResult(bars.Count, minTop, minBot, minEdge, minBegin, minEnd, topShort, botShort, edgeShort, inBlock, clash, worstClash);
        string L(double v) => v == double.MaxValue ? "-" : u.FormatLength(v, LengthKind.Short);
        lock (cm.Notes)
            cm.Notes.Add($"Deck rebar containment check ({bars.Count} bars): min top cover {L(minTop)} (required {u.FormatLength(rd.TopCover, LengthKind.Short)}), min bottom cover {L(minBot)} " +
                         $"(required {u.FormatLength(rd.BottomCover, LengthKind.Short)}), min edge distance {L(minEdge)} (required {u.FormatLength(rd.SideCover, LengthKind.Short)}), " +
                         $"min distance from the begin / end deck-end line {L(minBegin)} / {L(minEnd)}, steel clashes {clash} bars (worst {L(worstClash)}).");
        void D(DiagnosticSeverity s, string m) => cm.Diagnostics.Add(new Diagnostic(s, m));
        if (topShort > 0) D(DiagnosticSeverity.Warning, $"Deck reinforcing check: {topShort} top-mat bars have less than {u.FormatLength(rd.TopCover, LengthKind.Short)} cover to the top of slab (min {L(minTop)}).");
        if (botShort > 0) D(DiagnosticSeverity.Warning, $"Deck reinforcing check: {botShort} bottom-mat bars have less than {u.FormatLength(rd.BottomCover, LengthKind.Short)} cover to the soffit (min {L(minBot)}).");
        if (edgeShort > 0) D(DiagnosticSeverity.Warning, $"Deck reinforcing check: {edgeShort} bars are closer than {u.FormatLength(rd.SideCover, LengthKind.Short)} to a deck edge (min {L(minEdge)}).");
        if (inBlock > 0)
            D(maxIntr > In(1.5) ? DiagnosticSeverity.Warning : DiagnosticSeverity.Info,
                $"Deck reinforcing check: {inBlock} bars end inside the joint blockout / end dam zone by up to {u.FormatLength(maxIntr, LengthKind.Short)} (blockout begin {u.FormatLength(bw, LengthKind.Short)}, end {u.FormatLength(ew, LengthKind.Short)}; " +
                $"nearest bar ends {L(minBegin)} / {L(minEnd)} from the deck-end lines). The plans do not dimension where the slab bars stop at a joint: set DeckPlanReinforcement.Transverse/Longitudinal clears.");
        if (clash > 0)
            D(DiagnosticSeverity.Warning, $"Deck reinforcing check: {clash} bars pass through the top of the steel (top flanges / splice plates), worst {L(worstClash)}: e.g. " +
                string.Join("; ", worst.OrderByDescending(w => w.pen).Take(3).Select(w => $"{w.where} ({u.FormatLength(w.pen, LengthKind.Short)})")));
        return res;
    }

    private static bool InTri(double x, double y, Vec3 a, Vec3 b, Vec3 c)
    {
        double d1 = (x - b.X) * (a.Y - b.Y) - (a.X - b.X) * (y - b.Y);
        double d2 = (x - c.X) * (b.Y - c.Y) - (b.X - c.X) * (y - c.Y);
        double d3 = (x - a.X) * (c.Y - a.Y) - (c.X - a.X) * (y - a.Y);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    private static double PlaneZ(double x, double y, Vec3 p0, Vec3 n) => p0.Z - (n.X * (x - p0.X) + n.Y * (y - p0.Y)) / n.Z;
}
