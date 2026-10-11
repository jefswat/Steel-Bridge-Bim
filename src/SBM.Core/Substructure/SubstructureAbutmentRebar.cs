using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Geometry;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Substructure;

/// <summary>
/// Reinforcing of the as-designed abutment. Only bars whose placement the plans PROVE are placed in 3D: the footing longitudinals (rows from the
/// section: 6 / 4 equal spaces), the footing hoops at the pile pitch, and the backwall verticals (placement strings, printed straight lengths).
/// Every other mark of the plan bar list is entered in the schedule at its printed count and length ("schedule only": no 3D bar; the bend diagram is entered where read)
/// and reported in one warning, so nothing is placed by a rule the plans do not state. Count differences between the placement strings and
/// the list are reported, never adjusted.
/// </summary>
public sealed partial class SubstructureGenerator
{
    private sealed record PBar(string Mark, Vec3[] Pts, BarShape? Shape = null);

    private static string SizeOfMark(string mark) => "#" + new string(mark.TakeWhile(char.IsDigit).ToArray());

    private void AbutmentBars(SubstructureModel m, SubstructureUnit unit)
    {
        var g = unit.Abutment!;
        var ad = g.Def;
        var rb = ad.Rebar;
        string name = unit.Name;
        double sg = g.Sigma;
        double D = ad.FootingDepth;
        var placed = new List<PBar>();
        Vec3 P(double s, double x, double z) => unit.World(s, g.N(x), z);
        // integral pile cap: no placement is proven yet (the stub rows / hoops / backwall strings do not apply) - the whole plan list is schedule-only
        bool integral = ad.Kind == AbutmentKind.Integral;
        BarShape? hoopShape = null;
        if (!integral)
        {

        // ---- footing longitudinals: rows from the section (top/bottom rows of FootingRowBars, SideInterior bars on each side face)
        var big = RebarCatalog.Size(SizeOfMark(rb.FootingLongMark));
        var hoop = RebarCatalog.Size("#5");
        double cTop = In(2), cBot = In(3), cSide = In(2);       // printed "2" CL." (top, sides) and "3" CL." (bottom)
        double xa = g.XFootBack + cSide + hoop.Diameter + big.Diameter / 2, xb = g.XFootFront - cSide - hoop.Diameter - big.Diameter / 2;
        double hBot = cBot + hoop.Diameter + big.Diameter / 2, hTop = D - cTop - hoop.Diameter - big.Diameter / 2;
        var rows = new List<(double x, double h)>();
        for (int i = 0; i < rb.FootingRowBars; i++)
        {
            double x = xa + (xb - xa) * i / Math.Max(1, rb.FootingRowBars - 1);
            rows.Add((x, hBot)); rows.Add((x, hTop));
        }
        for (int i = 1; i <= rb.FootingSideInteriorBars; i++)
        {
            double h = hBot + (hTop - hBot) * i / (rb.FootingSideInteriorBars + 1);
            rows.Add((xa, h)); rows.Add((xb, h));
        }
        double endCover = In(2);
        foreach (var (x, h) in rows)
        {
            double n = g.N(x);
            double sL = g.LeftEndS(n) + endCover, sR = g.RightEndS(n) - endCover;
            int pieces = Math.Max(1, rb.FootingLongPieces);
            double piece = ((sR - sL) + (pieces - 1) * rb.FootingLongLap) / pieces;
            for (int k = 0; k < pieces; k++)
            {
                double s0 = sL + k * (piece - rb.FootingLongLap), s1 = s0 + piece;
                placed.Add(new PBar(rb.FootingLongMark, new[] { P(s0, x, g.FootingBottom(s0) + h), P(s1, x, g.FootingBottom(s1) + h) }));
            }
        }

        // ---- footing hoops at the pile pitch (each in a plane normal to the line)
        var stations = ad.PileStations.OrderBy(x => x).ToList();
        var (hoopShape0, hoopLocal) = ClosedStirrup(rb.HoopWidth, rb.HoopHeight, hoop);
        hoopShape = hoopShape0;
        for (int k = 0; k + 1 < stations.Count; k++)
            for (int i = 0; i < rb.HoopsPerPitch; i++)
            {
                double s = stations[k] + rb.HoopFirstOffset + i * rb.HoopSpacing;
                double x0 = g.XFootBack + cSide, z0 = g.FootingBottom(s) + cBot;
                placed.Add(new PBar("5b1", hoopLocal.Select(p => P(s, x0 + p.X, z0 + p.Y)).ToArray()));
            }

        // ---- backwall verticals along the backwall (placement strings)
        double nBack = g.N(g.XBwBack), nFront = g.N(g.XBwFront);
        double bwL = double.IsNaN(g.StringStartS) ? g.BwLeftS(nBack) : g.StringStartS, bwR = g.BwRightS(nBack);
        var vBack = RebarCatalog.Size("#6");
        const string backMark = "6d1", frontMark = "5d2";      // printed marks on all three abutments
        var vFront = RebarCatalog.Size(SizeOfMark(frontMark));
        double xBack = g.XBwBack + rb.BackwallBarCover + vBack.Diameter / 2, xFront = g.XBwFront - rb.BackwallBarCover - vFront.Diameter / 2;
        if (rb.BackFaceBarLength > 0)
            for (int i = 0; ; i++)
            {
                double s = bwL + rb.BackFaceEdge + i * rb.BackFaceSpacing;
                if (rb.BackFaceCount > 0 ? i >= rb.BackFaceCount : s > bwR - rb.BackFaceEdge + 1e-6) break;
                double top = g.BackwallTop(s) - rb.BackwallBarCover;
                placed.Add(new PBar(backMark, new[] { P(s, xBack, top - rb.BackFaceBarLength), P(s, xBack, top) }));
            }
        if (rb.FrontFaceBarLength > 0)
            for (int i = 0; ; i++)
            {
                double s = bwL + rb.FrontFaceEdge + i * rb.FrontFaceSpacing;
                if (rb.FrontFaceCount > 0 ? i >= rb.FrontFaceCount : s > bwR - rb.FrontFaceEdge + 1e-6) break;
                double top = g.BackwallTop(s) - rb.BackwallBarCover;
                placed.Add(new PBar(frontMark, new[] { P(s, xFront, top - rb.FrontFaceBarLength), P(s, xFront, top) }));
            }

        // ---- maskwall, counterfort and uplift bars (SubstructureAbutmentWingBars.cs)
        AbutmentWingBars(unit, g, placed);
        }

        // ---- marks
        bool epoxy = unit.Definition.Reinforcement.EpoxyCoated;
        var marks = new List<BarMark>();
        var places = new List<BarPlacement>();
        var done = new HashSet<string>();
        foreach (var grp in placed.GroupBy(p => p.Mark).OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var size = RebarCatalog.Size(SizeOfMark(grp.Key));
            var lens = grp.Select(p => Length3(p.Pts)).ToList();
            BarShape shape = grp.Key == "5b1" && hoopShape != null ? hoopShape : grp.First().Shape ?? BarShape.Straight(lens.Max());
            bool varies = lens.Max() - lens.Min() > In(1) && grp.Key != "5b1";
            marks.Add(new BarMark
            {
                Mark = grp.Key, Size = size, Shape = shape, Location = LocationOf(grp.Key), Unit = unit.Name, Epoxy = epoxy && grp.Key != "5d5", Count = grp.Count(),
                VaryingLengths = varies ? lens : null,
            });
            places.AddRange(grp.Select(p => new BarPlacement { Mark = grp.Key, Points = p.Pts, Group = "Sub", Unit = unit.Name }));
            done.Add(grp.Key);
        }
        // schedule-only marks: the rest of the plan bar list at its printed count / length
        var scheduleOnly = new List<string>();
        foreach (var line in rb.PlanBarList)
        {
            if (done.Contains(line.Mark)) continue;
            var size = RebarCatalog.Size(SizeOfMark(line.Mark));
            List<double>? varying = null;
            if (line.Varies && line.WeightLb > 0 && line.Count > 1)
            {
                double avg = line.WeightLb * 0.45359237 / size.MassPerLength / line.Count;      // weight-consistent individual length
                varying = Enumerable.Repeat(avg, line.Count).ToList();
                varying[0] = line.Length;                                                         // the printed maximum
            }
            marks.Add(new BarMark
            {
                Mark = line.Mark, Size = size, Shape = ScheduleShape(line, size), Location = LocationOf(line.Mark) + " (schedule only)", Unit = unit.Name,
                Epoxy = epoxy && line.Mark != "5d5", Count = line.Count, VaryingLengths = varying,
            });
            scheduleOnly.Add($"{line.Mark} x{line.Count}");
        }
        foreach (var line in rb.PlanBarList)
        {
            var mk = marks.FirstOrDefault(x => x.Mark == line.Mark);
            if (mk != null && done.Contains(line.Mark) && mk.Count != line.Count)
                Warn($"{name}: bar {line.Mark}: {mk.Count} bars from the placement string / section vs {line.Count} in the plan bar list - NOT adjusted (list rows at the corners / ends are not drawn).");
        }
        if (integral) IntegralPileSpiral(unit, ad, marks, epoxy);
        if (scheduleOnly.Count > 0 && integral)
            Warn($"{name}: reinforcing - integral abutment: no bar placement is proven yet; the plan bar-list rows are in the schedule at the printed count/length: {string.Join(", ", scheduleOnly)}.");
        else if (scheduleOnly.Count > 0)
        {
            Warn($"{name}: reinforcing - placed in 3D: footing longitudinals, footing hoops at the pile pitch, backwall verticals (printed strings), maskwall 5e2 / 5e3, counterfort verticals and 5h4 hoops, pile uplift anchors 8j3; the other plan bar-list rows are in the schedule at the printed count/length with the plan bend diagram where read: {string.Join(", ", scheduleOnly)}.");
            ReportUnresolvedAbutmentMarks(name, scheduleOnly.Select(x => x.Split(' ')[0]));
        }
        if (ad.Wings.Any(w => w.UpliftPiles.Count == 0) && rb.PlanBarList.Any(l => l.Mark == "8j3"))
            Warn($"{name}: UNRESOLVED - no circled (uplift anchor) wing-footing piles are given for every wing; the 8j3 bars of those wings stay in the schedule.");
        lock (m) { m.Marks.AddRange(marks); m.Bars.AddRange(places); }
    }

    /// <summary>Schedule shape of a plan bar-list line from its read bend diagram (no 3D placement); straight when no diagram is entered.</summary>
    private static BarShape ScheduleShape(PlanBarLine line, RebarSize size)
    {
        var d = line.Dims;
        double len = line.Length;
        switch (line.Shape)
        {
            case "U" when d.Length == 3: return PlanU(size, d[0], d[1], d[2], line.Pin, len).shape;
            case "L" when d.Length == 2: return PlanL(size, d[0], d[1], line.Pin, len).shape;
            case "HOOP" when d.Length == 4: return PlanHoop(size, d[0], d[1], d[2], d[3], line.Pin, len).shape;
            case "HAT" when d.Length == 5:
            {
                var c = new[] { new Vec2(0, 0), new Vec2(d[0], 0), new Vec2(d[0], d[1]), new Vec2(d[0] + d[2], d[1]), new Vec2(d[0] + d[2], 0), new Vec2(d[0] + d[2] + d[4], 0) };
                double pin = line.Pin > 0 ? line.Pin : size.MinPinDiameter;
                var bends = new List<(Vec2, double)>();
                var sh = new BarShape { Code = "HAT", Description = "Hat bar, out to out", Centerline = BarShape.Rounded(c, pin / 2 + size.Diameter / 2, bends), PinDiameter = pin, FixedLength = len };
                sh.Bends.AddRange(bends);
                for (int i = 0; i < 5; i++) sh.Dimensions.Add(new BarDimension(((char)('A' + i)).ToString(), d[i], c[i], c[i + 1], new Vec2(0, 1)));
                return sh;
            }
            case "V" when d.Length == 3:
            {
                double pin = line.Pin > 0 ? line.Pin : size.MinPinDiameter;
                var c = new[] { new Vec2(-d[1] / 2, d[2]), new Vec2(0, 0), new Vec2(d[1] / 2, d[2]) };
                var bends = new List<(Vec2, double)>();
                var sh = new BarShape { Code = "V", Description = "V bar, out to out", Centerline = BarShape.Rounded(c, pin / 2 + size.Diameter / 2, bends), PinDiameter = pin, FixedLength = len };
                sh.Bends.AddRange(bends);
                sh.Dimensions.Add(new BarDimension("A", d[0], c[1], c[0], new Vec2(-1, 0)));
                sh.Dimensions.Add(new BarDimension("W", d[1], c[0], c[2], new Vec2(0, 1)));
                return sh;
            }
        }
        return BarShape.Straight(len);
    }

    private static string LocationOf(string mark) => mark switch
    {
        "9a1" or "8a1" => "abutment footing longitudinal", "5b1" => "abutment footing hoops", "6d1" => "backwall back face vertical", "5d2" => "backwall front face vertical",
        "5e2" => "maskwall vertical", "5e3" => "maskwall horizontal", "6h2" or "6h3" => "counterfort vertical", "5h4" => "counterfort hoop (U staple)", "8j3" => "wing-footing pile uplift anchor (V through the web hole)",
        _ => "Abutment (plan bar list)",
    };

    private static double Length3(Vec3[] pts)
    {
        double l = 0;
        for (int i = 1; i < pts.Length; i++) l += (pts[i] - pts[i - 1]).Length;
        return l;
    }
}
