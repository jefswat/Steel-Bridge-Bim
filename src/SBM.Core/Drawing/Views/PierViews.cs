using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Substructure;
using SBM.Core.Units;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Drawing.Views;

/// <summary>Pier plans in cap coordinates (X = S along the skewed support line, Y = N up-station): cap plan with pedestals and columns, footing / pile layout.</summary>
public static class PierViews
{
    private static List<double> Distinct(IEnumerable<double> v, double tol)
    {
        var l = new List<double>();
        foreach (double x in v.OrderBy(x => x)) if (l.Count == 0 || x - l[^1] > tol) l.Add(x);
        return l;
    }

    private static IEnumerable<Vec2> Shape(ColumnShape shape, double s, double n, double sizeS, double sizeN)
    {
        if (shape == ColumnShape.Round)
        {
            for (int i = 0; i < 48; i++) { double a = i * Math.PI / 24; yield return new Vec2(s + sizeS / 2 * Math.Cos(a), n + sizeN / 2 * Math.Sin(a)); }
        }
        else
        {
            yield return new Vec2(s - sizeS / 2, n - sizeN / 2); yield return new Vec2(s + sizeS / 2, n - sizeN / 2);
            yield return new Vec2(s + sizeS / 2, n + sizeN / 2); yield return new Vec2(s - sizeS / 2, n + sizeN / 2);
        }
    }

    /// <summary>Tip elevations of a pile group, plumb and battered piles separately (a battered pile of the same length ends higher):
    /// "TIP EL 966.000" or "TIP EL 966.000 (PLUMB), 969.732 (BATTERED)"; a range when the tips differ otherwise.</summary>
    public static string TipText(IReadOnlyList<PileInfo> piles, Drafter d)
    {
        string Rng(IEnumerable<PileInfo> g)
        {
            double lo = g.Min(p => p.TipZ), hi = g.Max(p => p.TipZ);
            return Math.Abs(hi - lo) < 0.002 ? d.Elev(lo) : $"{d.Elev(lo)} TO {d.Elev(hi)}";
        }
        var plumb = piles.Where(p => !p.IsBattered).ToList();
        var bat = piles.Where(p => p.IsBattered).ToList();
        if (plumb.Count > 0 && bat.Count > 0) return $"TIP EL {Rng(plumb)} (PLUMB), {Rng(bat)} (BATTERED)";
        return piles.Count > 0 ? $"TIP EL {Rng(piles)}" : "";
    }

    /// <summary>Plan position of a pile where its axis crosses elevation <paramref name="z"/> (battered piles move with depth).</summary>
    public static Vec2 PileAt(PileInfo p, double z)
    {
        double h = p.TopZ - p.TipZ;
        double t = h > 1e-9 ? Math.Clamp((p.TopZ - z) / h, 0, 1) : 0;
        return new Vec2(p.S + (p.TipS - p.S) * t, p.N + (p.TipN - p.N) * t);
    }

    public static SheetView CapPlan(ViewContext ctx, SubstructureUnit unit)
    {
        var u = ctx.Units;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, ctx.Options.BentScale);
        d.Rect(SheetLayers.Concrete, unit.CapS0, unit.CapN0, unit.CapS1, unit.CapN1);
        if (unit.Backwall is { } bw) d.Rect(SheetLayers.ConcreteHidden, unit.CapS0, bw.N0, unit.CapS1, bw.N1);
        foreach (var c in unit.Columns)
            d.Poly(SheetLayers.ConcreteHidden, Shape(c.Shape, c.S, c.N, c.SizeS, c.SizeN), true);
        foreach (var p in unit.Pedestals)
            d.Rect(SheetLayers.Steel, p.S - p.LengthS / 2, p.N - p.WidthN / 2, p.S + p.LengthS / 2, p.N + p.WidthN / 2);
        // One label per pedestal; a pedestal carrying the back and ahead beams (two seats) says so instead of repeating the girder.
        foreach (var g in unit.Pedestals.GroupBy(p => (p.Tag, Math.Round(p.S / 0.05))))
        {
            var p = g.First();
            string seats = g.Count() > 1 ? (g.Select(x => Math.Sign(Math.Round(x.RowN / 0.01))).Distinct().Count() > 1 ? "\nBK + AH" : $"\n{g.Count()} SEATS") : "";
            d.Text(p.Tag + seats, new Vec2(p.S, g.Average(x => x.N)), d.SmallTextH, 0, TextAlign.Center);
        }
        double nMid = 0;
        d.Line(SheetLayers.Center, new Vec2(unit.CapS0 - d.Paper(0.3), nMid), new Vec2(unit.CapS1 + d.Paper(0.3), nMid));
        d.Text($"CL BRG {unit.Name.ToUpperInvariant()}", new Vec2(unit.CapS1 + d.Paper(0.35), nMid), d.SmallTextH, 0, TextAlign.Left);
        // Dimensions: overall, pedestal spacing, column spacing (along the support line); cap width.
        d.Dim(new Vec2(unit.CapS0, unit.CapN1), new Vec2(unit.CapS1, unit.CapN1), 0.9, d.Len(unit.CapS1 - unit.CapS0));
        var peds = Distinct(unit.Pedestals.Select(p => p.S), 0.01);
        if (peds.Count > 1)
        {
            var pts = peds.Select(s => new Vec2(s, unit.CapN1)).ToList();
            d.DimChain(pts, 0.45, i => d.Len(peds[i + 1] - peds[i]));
        }
        var cols = unit.Columns.Select(c => c.S).Distinct().OrderBy(x => x).ToList();
        if (cols.Count > 1)
            d.DimChain(cols.Select(s => new Vec2(s, unit.CapN0)).ToList(), -0.45, i => d.Len(cols[i + 1] - cols[i]));
        d.Dim(new Vec2(unit.CapS0, unit.CapN0), new Vec2(unit.CapS0, unit.CapN1), 0.45, d.Short(unit.CapN1 - unit.CapN0));
        var box = dwg.Extents();
        if (unit.Columns.Count > 0)
        {
            // Column heights as generated: bottom of cap at the column to the top of the foundation (bottom of the column).
            var lines = unit.Columns.OrderBy(c => c.S).Select((c, i) => $"C{i + 1}: {d.Len(c.TopZ - c.BottomZ)}   (EL {d.Elev(c.TopZ)} TO {d.Elev(c.BottomZ)})");
            d.Text("COLUMN HEIGHTS (BOTTOM OF CAP TO COLUMN BOTTOM)\n" + string.Join("\n", lines), new Vec2(unit.CapS0, box.MinY - d.Paper(1.1)), d.SmallTextH, 0, TextAlign.Left);
            box = dwg.Extents();
        }
        d.Title($"{unit.Name.ToUpperInvariant()} — CAP PLAN", $"S along the support line (skew {FormatAngleDms(Math.Abs(unit.Definition == null ? 0 : ctx.Framing.SupportLine(unit.SupportIndex).SkewDegrees) * Math.PI / 180, false)}); pedestals at the girder bearings; columns dashed · Scale {DrawingOptions.ScaleText(d.Scale, u)}",
            new Vec2((unit.CapS0 + unit.CapS1) / 2, box.MinY - d.Paper(0.45)));
        return new SheetView($"{unit.Name} cap plan", unit.Name.Replace(" ", "") + "-CapPlan", dwg, d.Scale, "Cap plan with pedestals and columns.");
    }

    public static SheetView PileLayout(ViewContext ctx, SubstructureUnit unit)
    {
        var u = ctx.Units;
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, ctx.Options.BentScale);
        var sd = unit.Definition;
        string pileName = sd.PileLabel(d.Short);
        int fi = 0;
        foreach (var fd in unit.Foundations)
        {
            fi++;
            if (fd.SizeS > 0)
            {
                var pts = fd.Type == FoundationType.DrilledShaft ? Shape(ColumnShape.Round, fd.S, fd.N, fd.SizeS, fd.SizeN) : Shape(ColumnShape.Rectangular, fd.S, fd.N, fd.SizeS, fd.SizeN);
                d.Poly(SheetLayers.Concrete, pts, true);
            }
            // Pile positions at the bottom of the footing (the plan's reference), not at the embedded pile top.
            var at = fd.Piles.Select(p => PileAt(p, fd.BottomZ)).ToList();
            for (int k = 0; k < fd.Piles.Count; k++)
            {
                var pile = fd.Piles[k];
                var c = at[k];
                double h = Math.Max(In(14), 0) / 2;
                d.Poly(SheetLayers.Steel, new[] { new Vec2(c.X - h, c.Y - h), new Vec2(c.X + h, c.Y - h), new Vec2(c.X + h, c.Y + h), new Vec2(c.X - h, c.Y + h) }, true);
                var w = new Vec2(pile.WebS, pile.WebN);
                if (w.Length > 0.5 && sd.PileType == PileType.SteelHP) d.Line(SheetLayers.Steel, c - w * h * 0.9, c + w * h * 0.9);
                if (pile.IsBattered)
                {
                    // Batter direction as a short arrow (the tip is far outside the footing for long piles) with the batter.
                    var dv = new Vec2(pile.TipS - pile.S, pile.TipN - pile.N);
                    double run = dv.Length, rise = pile.TopZ - pile.TipZ;
                    var e = dv.Normalized();
                    var tipA = c + e * (h + d.Paper(0.3));
                    d.Line(SheetLayers.Steel, c + e * h, tipA);
                    d.Poly(SheetLayers.Steel, new[] { tipA, tipA - e * d.Paper(0.06) + e.Left * d.Paper(0.025), tipA - e * d.Paper(0.06) - e.Left * d.Paper(0.025) }, true);
                    if (k == fd.Piles.FindIndex(q => q.IsBattered) && run > 1e-6)
                        d.Text($"BATTER 1:{rise / run:0.##}", tipA + e * d.Paper(0.08), d.SmallTextH * 0.85, 0, e.X >= 0 ? TextAlign.Left : TextAlign.Right);
                }
            }
            if (fd.Piles.Count > 0)
            {
                var xs = Distinct(at.Select(p => p.X), 0.005);
                var ys = Distinct(at.Select(p => p.Y), 0.005);
                double ftop = fd.N + fd.SizeN / 2, fbot = fd.N - fd.SizeN / 2, fl = fd.S - fd.SizeS / 2, fr = fd.S + fd.SizeS / 2;
                if (xs.Count > 1) d.DimChain(xs.Select(x => new Vec2(x, fbot)).ToList(), -0.35, i => d.Short(xs[i + 1] - xs[i]));
                if (ys.Count > 1) d.DimChain(ys.Select(y => new Vec2(fr, y)).ToList(), -0.5, i => d.Short(ys[i + 1] - ys[i]));
                if (fd.SizeS > 0) d.Dim(new Vec2(fl, fbot), new Vec2(fr, fbot), -0.75, d.Short(fd.SizeS));
                int nb = fd.Piles.Count(p => p.IsBattered);
                string note = $"FTG {fi}: {fd.Piles.Count} {pileName}{(nb > 0 ? $"\n{nb} BATTERED" : "")}\n{TipText(fd.Piles, d).Replace(", ", "\n")}\nBOT. EL {d.Elev(fd.BottomZ)}";
                int nl = note.Split('\n').Length;
                // The first line sits at the given point and the rest go down: start high enough to clear the batter arrows.
                d.Text(note, new Vec2(fd.S, ftop + d.Paper(0.5) + (nl - 1) * d.SmallTextH * 1.55), d.SmallTextH, 0, TextAlign.Center);
            }
            else if (fd.SizeS > 0)
                d.Text($"FOOTING {fi}\nBOTTOM EL {d.Elev(fd.BottomZ)}", new Vec2(fd.S, fd.N + fd.SizeN / 2 + d.Paper(0.3)), d.SmallTextH, 0, TextAlign.Center);
        }
        var box = dwg.Extents();
        d.Title($"{unit.Name.ToUpperInvariant()} — FOOTING AND PILE LAYOUT", $"Plan at the bottom of the footing; battered piles: arrow = batter direction; positions in S (along the support line) and N · Scale {DrawingOptions.ScaleText(d.Scale, u)}",
            new Vec2((box.MinX + box.MaxX) / 2, box.MinY - d.Paper(0.45)));
        return new SheetView($"{unit.Name} pile layout", unit.Name.Replace(" ", "") + "-Piles", dwg, d.Scale, "Footing and pile layout.");
    }
}
