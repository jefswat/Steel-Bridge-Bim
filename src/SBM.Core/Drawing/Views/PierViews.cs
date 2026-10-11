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
        {
            d.Rect(SheetLayers.Steel, p.S - p.LengthS / 2, p.N - p.WidthN / 2, p.S + p.LengthS / 2, p.N + p.WidthN / 2);
            d.Text($"G{p.Girder + 1}", new Vec2(p.S, p.N), d.SmallTextH, 0, TextAlign.Center);
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
        string pileName = sd.PileType == PileType.SteelHP ? sd.PileSection : d.Short(sd.PileSize) + " SQ";
        int fi = 0;
        foreach (var fd in unit.Foundations)
        {
            fi++;
            if (fd.SizeS > 0)
            {
                var pts = fd.Type == FoundationType.DrilledShaft ? Shape(ColumnShape.Round, fd.S, fd.N, fd.SizeS, fd.SizeN) : Shape(ColumnShape.Rectangular, fd.S, fd.N, fd.SizeS, fd.SizeN);
                d.Poly(SheetLayers.Concrete, pts, true);
            }
            foreach (var pile in fd.Piles)
            {
                double h = Math.Max(In(14), 0) / 2;
                d.Poly(SheetLayers.Steel, new[] { new Vec2(pile.S - h, pile.N - h), new Vec2(pile.S + h, pile.N - h), new Vec2(pile.S + h, pile.N + h), new Vec2(pile.S - h, pile.N + h) }, true);
                var w = new Vec2(pile.WebS, pile.WebN);
                if (w.Length > 0.5 && sd.PileType == PileType.SteelHP) d.Line(SheetLayers.Steel, new Vec2(pile.S, pile.N) - w * h * 0.9, new Vec2(pile.S, pile.N) + w * h * 0.9);
                if (pile.IsBattered)
                {
                    d.Line(SheetLayers.SteelHidden, new Vec2(pile.S, pile.N), new Vec2(pile.TipS, pile.TipN));
                    d.Circle(SheetLayers.SteelHidden, new Vec2(pile.TipS, pile.TipN), h * 0.5);
                }
            }
            if (fd.Piles.Count > 0)
            {
                double ss0 = fd.Piles.Min(p => p.S), ss1 = fd.Piles.Max(p => p.S);
                var xs = Distinct(fd.Piles.Select(p => p.S), 0.005);
                var ys = Distinct(fd.Piles.Select(p => p.N), 0.005);
                double ftop = fd.N + fd.SizeN / 2, fbot = fd.N - fd.SizeN / 2, fl = fd.S - fd.SizeS / 2, fr = fd.S + fd.SizeS / 2;
                if (xs.Count > 1) d.DimChain(xs.Select(x => new Vec2(x, fbot)).ToList(), -0.35, i => d.Short(xs[i + 1] - xs[i]));
                if (ys.Count > 1) d.DimChain(ys.Select(y => new Vec2(fr, y)).ToList(), -0.5, i => d.Short(ys[i + 1] - ys[i]));
                if (fd.SizeS > 0) d.Dim(new Vec2(fl, fbot), new Vec2(fr, fbot), -0.75, d.Short(fd.SizeS));
                int nb = fd.Piles.Count(p => p.IsBattered);
                d.Text($"FTG {fi}: {fd.Piles.Count} {pileName}{(nb > 0 ? $"\n{nb} BATTERED" : "")}\nTIP EL {d.Elev(fd.Piles.Min(p => p.TipZ))}\nBOT. EL {d.Elev(fd.BottomZ)}",
                    new Vec2(fd.S, ftop + d.Paper(0.35)), d.SmallTextH, 0, TextAlign.Center);
            }
            else if (fd.SizeS > 0)
                d.Text($"FOOTING {fi}\nBOTTOM EL {d.Elev(fd.BottomZ)}", new Vec2(fd.S, fd.N + fd.SizeN / 2 + d.Paper(0.3)), d.SmallTextH, 0, TextAlign.Center);
        }
        var box = dwg.Extents();
        d.Title($"{unit.Name.ToUpperInvariant()} — FOOTING AND PILE LAYOUT", $"Plan at the bottom of the footing; battered piles: dashed line to the tip; positions in S (along the support line) and N · Scale {DrawingOptions.ScaleText(d.Scale, u)}",
            new Vec2((box.MinX + box.MaxX) / 2, box.MinY - d.Paper(0.45)));
        return new SheetView($"{unit.Name} pile layout", unit.Name.Replace(" ", "") + "-Piles", dwg, d.Scale, "Footing and pile layout.");
    }
}
