using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Concrete;

/// <summary>
/// Places <see cref="PlanBarsDefinition"/> sets (generic plan bars: abutment / pedestal / wall / diaphragm / barrier bars) and builds their schedule
/// marks, compared with the plan bill (<see cref="PlanBarsDefinition.BarList"/>). Pure function of the framing and the definition; called by the
/// concrete generator (superstructure sets, unit "") and the substructure generator (sets of each support, unit = the unit name).
/// </summary>
public static class PlanBarPlacer
{
    public sealed record Result(List<BarMark> Marks, List<BarPlacement> Bars, List<Diagnostic> Diagnostics);

    private const double LbPerKg = 2.2046226218;

    /// <summary>World point of a bar vertex.</summary>
    public static Vec3 World(BridgeFraming f, PlanBarSet s, PlanBarPoint p, double shift)
    {
        var def = f.Definition;
        var al = f.Alignment;
        double w = p.W + shift;
        Vec2 q;
        if (s.Frame == PlanBarFrame.Alignment) q = al.PointAt(w, p.U);
        else
        {
            var sl = f.SupportLine(s.Support);
            var tan = Vec2.FromAngle(al.AngleAt(sl.Station));
            var n = sl.Direction.Left;
            if (Vec2.Dot(n, tan) < 0) n = n * -1;
            var sup = def.Supports.Concat(def.AuxiliarySupports).ElementAt(s.Support);
            double xs = sup.Type == SupportType.Abutment && s.Support == def.Supports.Count - 1 ? -1 : 1;
            q = sl.AlignmentPoint + sl.Direction * w + n * (xs * p.U);
        }
        double z = p.Dz;
        if (p.Ref == PlanBarZRef.DeckTop)
        {
            var so = al.Inverse(q);
            z += def.Deck.FinishedElevation(al, so.Station, so.Offset);
        }
        return q.WithZ(z);
    }

    public static Result Place(BridgeFraming f, bool superstructure, int support, string unit)
    {
        var pb = f.Definition.PlanBars;
        var res = new Result(new(), new(), new());
        if (pb == null) return res;
        void Warn(string m) => res.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, m));
        void Info(string m) => res.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info, m));
        string where = superstructure ? "superstructure" : unit;
        bool Mine(bool sup, int j) => superstructure ? sup : !sup && j == support;
        var sets = pb.Sets.Where(s => Mine(s.Superstructure, s.Support)).ToList();
        var lines = pb.BarList.Where(l => Mine(l.Superstructure, l.Support)).ToList();
        if (superstructure) foreach (var u in pb.Unresolved) Warn("UNRESOLVED: " + u);
        if (sets.Count == 0 && lines.Count == 0) return res;

        var marks = new Dictionary<string, BarMark>();
        var counts = new Dictionary<string, int>();
        var devLen = new Dictionary<string, double>();
        string Group(PlanBarSet s) => s.Location.Contains("BARRIER", StringComparison.OrdinalIgnoreCase) ? "Rail"
            : s.Location.Contains("DIAPH", StringComparison.OrdinalIgnoreCase) ? "Diaphragm" : superstructure ? "Deck" : "Abutment";
        foreach (var s in sets)
        {
            var size = RebarCatalog.Size(s.Size);
            int placed = 0;
            if (s.Unresolved != "")
                Warn($"UNRESOLVED ({where}, {s.Mark} {s.Name}{(s.Source != "" ? ", " + s.Source : "")}): {s.Unresolved} - {s.UnplacedCount} bar(s) scheduled, not placed.");
            else if (s.Path.Count < 2) Warn($"{where}: plan bar set {s.Mark} {s.Name} has fewer than 2 path points - not placed.");
            else
            {
                var shifts = s.Positions.Count > 0 ? s.Positions : new List<double> { 0 };
                foreach (double sh in shifts)
                {
                    var pts = s.Path.Select(p => World(f, s, p, sh)).ToArray();
                    res.Bars.Add(new BarPlacement { Mark = s.Mark, Points = pts, Group = Group(s), Unit = unit });
                    placed++;
                    if (!devLen.ContainsKey(s.Mark)) devLen[s.Mark] = Enumerable.Range(1, pts.Length - 1).Sum(i => (pts[i] - pts[i - 1]).Length);
                }
            }
            int n = placed + (s.Unresolved != "" ? s.UnplacedCount : 0);
            counts[s.Mark] = counts.GetValueOrDefault(s.Mark) + n;
            if (!marks.ContainsKey(s.Mark))
            {
                var first = res.Bars.FirstOrDefault(b => b.Mark == s.Mark && b.Unit == unit);
                marks[s.Mark] = new BarMark
                {
                    Mark = s.Mark, Size = size, Shape = ShapeOf(first?.Points, s, size), Location = s.Location + (placed == 0 ? " (schedule only)" : ""), Unit = unit,
                    Epoxy = s.Coating.Contains("epoxy", StringComparison.OrdinalIgnoreCase), Coating = CoatingNote(s.Coating),
                };
            }
        }
        foreach (var (mk, bm) in marks) bm.Count = counts[mk];

        // ---- plan bill: counts / lengths / weights; bill lines with no set stay in the schedule (no 3D bar)
        foreach (var l in lines)
        {
            var size = RebarCatalog.Size(l.Size);
            if (!marks.TryGetValue(l.Mark, out var bm))
            {
                bm = new BarMark
                {
                    Mark = l.Mark, Size = size, Shape = BarShape.Straight(l.Length), Location = l.Location + " (schedule only)", Unit = unit,
                    Epoxy = l.Coating.Contains("epoxy", StringComparison.OrdinalIgnoreCase), Coating = CoatingNote(l.Coating),
                    Count = l.Count,
                };
                marks[l.Mark] = bm;
                Warn($"{where}: plan bar {l.Mark} ({l.Count} x {FtIn(l.Length)}, {l.Location}) has no placement data - schedule only, not modeled.");
                continue;
            }
            double lb = bm.Count * l.Length * size.MassPerLength * LbPerKg;
            string dl = devLen.TryGetValue(l.Mark, out var d) && Math.Abs(d - l.Length) > In(1)
                ? $"; placed centerline (sharp corners) {FtIn(d)} vs listed {FtIn(l.Length)} (bill = out-to-out sum)" : "";
            if (bm.Count != l.Count)
                Warn($"{where}: plan bar {l.Mark}: {bm.Count} in the model vs {l.Count} in the plan bill ({l.Location}){dl}.");
            else Info($"{where}: plan bar {l.Mark}: {bm.Count} bars = plan bill, {lb:0.0} lb vs printed {l.WeightLb:0.0} lb{dl}.");
        }
        foreach (var bm in marks.Values.Where(m => !lines.Any(l => l.Mark == m.Mark)))
            if (lines.Count > 0) Warn($"{where}: plan bar {bm.Mark} is placed but not in the plan bill.");
        res.Marks.AddRange(marks.Values.OrderBy(m => m.Mark, StringComparer.Ordinal));
        return res;
    }

    /// <summary>Coating note of a mark: "" for plain epoxy / plain bars, else the listed coating ("chromium-epoxy (4% Cr)", "stainless 75 ksi").</summary>
    private static string CoatingNote(string c) => c.Trim().ToLowerInvariant() is "epoxy" or "plain" or "" ? "" : c;

    private static string FtIn(double m)
    {
        double inch = Math.Round(m / In(1) * 16) / 16;
        int ft = (int)Math.Floor(inch / 12 + 1e-9);
        double r = inch - ft * 12;
        return $"{ft}'-{r:0.###}\"";
    }

    /// <summary>Schedule shape: the first placed bar unfolded into its plane (sharp corners), the listed length as the developed length.</summary>
    private static BarShape ShapeOf(Vec3[]? pts, PlanBarSet s, RebarSize size)
    {
        double listed = s.ListedLength;
        if (pts == null || pts.Length < 2) return listed > 0 ? BarShape.Straight(listed) : BarShape.Straight(In(1));
        var p0 = pts[0];
        var e1 = (pts[1] - p0).Normalized();
        Vec3? nrm = null;
        for (int i = 2; i < pts.Length && nrm == null; i++)
        {
            var c = Vec3.Cross(e1, pts[i] - p0);
            if (c.Length > 1e-6) nrm = c.Normalized();
        }
        double len = Enumerable.Range(1, pts.Length - 1).Sum(i => (pts[i] - pts[i - 1]).Length);
        if (nrm == null) return new BarShape
        {
            Code = "STR", Description = s.Shape != "" ? s.Shape : "Straight", Centerline = new[] { Vec2.Zero, new Vec2(len, 0) }, FixedLength = listed > 0 ? listed : null,
            Dimensions = { new BarDimension("A", listed > 0 ? listed : len, Vec2.Zero, new Vec2(len, 0), new Vec2(0, 1)) },
        };
        var e2 = Vec3.Cross(nrm.Value, e1);
        var c2 = pts.Select(p => new Vec2(Vec3.Dot(p - p0, e1), Vec3.Dot(p - p0, e2))).ToArray();
        var shape = new BarShape
        {
            Code = "PLAN", Description = s.Shape != "" ? s.Shape : "Bent (plan bend diagram)", Centerline = c2, FixedLength = listed > 0 ? listed : null,
        };
        for (int i = 0; i + 1 < c2.Length; i++)
        {
            double v = i < s.Dims.Count ? s.Dims[i] : Vec2.Distance(c2[i], c2[i + 1]);
            var dir = (c2[i + 1] - c2[i]).Normalized();
            shape.Dimensions.Add(new BarDimension(((char)('A' + i)).ToString(), v, c2[i], c2[i + 1], new Vec2(-dir.Y, dir.X)));
        }
        return shape;
    }
}
