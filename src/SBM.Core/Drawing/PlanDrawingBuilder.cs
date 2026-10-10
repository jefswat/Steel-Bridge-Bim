using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Units;

namespace SBM.Core.Drawing;

/// <summary>Builds the framing plan linework from an alignment and generated framing.</summary>
public static class PlanDrawingBuilder
{
    public static PlanDrawing Build(Alignment? align, BridgeFraming? framing, UnitSystem units)
    {
        var dwg = new PlanDrawing();
        double textH = units.IsUS ? UnitSystem.Ft(1.5) : 0.45;
        if (align != null && align.Elements.Count > 0) AddAlignment(dwg, align, units, textH);
        if (framing != null) AddFraming(dwg, framing, units, textH);
        return dwg;
    }

    private static double ElevAt(Alignment a, double d) => a.Profile.IsEmpty ? 0 : a.Profile.ElevationAt(a.DistanceToStation(d));

    private static void AddAlignment(PlanDrawing dwg, Alignment a, UnitSystem units, double textH)
    {
        var line = dwg.Layer(Layers.Alignment);
        var pts = a.SamplePolyline(0, a.Length, 0, 0.005, 10);
        line.Poly(pts.Select(p => p.WithZ(ElevAt(a, a.Inverse(p).Distance))));

        // Station ticks.
        var ticks = dwg.Layer(Layers.AlignmentTicks);
        var text = dwg.Layer(Layers.AlignmentText);
        double unit = units.LongUnit;
        double minor = (units.IsUS ? 50 : 20) * unit, major = 100 * unit;
        double s0 = a.StartStation, s1 = a.EndStation;
        double first = Math.Ceiling(s0 / minor) * minor;
        int guard = 0;
        for (double sta = first; sta <= s1 + 1e-9 && guard++ < 100000; sta += minor)
        {
            bool isMajor = Math.Abs(Math.IEEERemainder(sta, major)) < 1e-6;
            double d = a.StationToDistance(sta);
            var (p, ang) = a.EvaluateAtDistance(d);
            var n = Vec2.FromAngle(ang).Right;
            double len = isMajor ? textH * 1.2 : textH * 0.6;
            double z = ElevAt(a, d);
            ticks.Line((p - n * len).WithZ(z), (p + n * len).WithZ(z));
            if (isMajor)
            {
                text.Texts.Add(new TextItem((p - n * (len + textH)).WithZ(z), units.FormatStation(sta, 0), textH, ang + Math.PI / 2));
            }
        }

        // Geometry points (PC, PT, TS, SC, CS, ST).
        for (int i = 0; i < a.Elements.Count; i++)
        {
            var e = a.Elements[i];
            var prev = i > 0 ? a.Elements[i - 1] : null;
            string? code = PointCode(prev, e);
            if (code == null) continue;
            var (p, ang) = a.EvaluateAtDistance(e.StartDistance);
            var n = Vec2.FromAngle(ang).Right;
            double z = ElevAt(a, e.StartDistance);
            ticks.Line((p - n * textH * 2).WithZ(z), (p + n * textH * 2).WithZ(z));
            text.Texts.Add(new TextItem((p + n * textH * 3.5).WithZ(z),
                $"{code} {units.FormatStation(a.DistanceToStation(e.StartDistance))}", textH * 0.8, ang + Math.PI / 2));
        }
    }

    private static string? PointCode(HorizontalElement? prev, HorizontalElement e)
    {
        if (prev == null) return "POB";
        return (prev.Kind, e.Kind) switch
        {
            (HorizontalElementKind.Line, HorizontalElementKind.Arc) => "PC",
            (HorizontalElementKind.Arc, HorizontalElementKind.Line) => "PT",
            (HorizontalElementKind.Line, HorizontalElementKind.Spiral) => "TS",
            (HorizontalElementKind.Spiral, HorizontalElementKind.Arc) => "SC",
            (HorizontalElementKind.Arc, HorizontalElementKind.Spiral) => "CS",
            (HorizontalElementKind.Spiral, HorizontalElementKind.Line) => "ST",
            (HorizontalElementKind.Arc, HorizontalElementKind.Arc) => Math.Sign(prev.K1) == Math.Sign(e.K0) ? "PCC" : "PRC",
            (HorizontalElementKind.Line, HorizontalElementKind.Line) => "PI",
            _ => null,
        };
    }

    private static void AddFraming(PlanDrawing dwg, BridgeFraming f, UnitSystem units, double textH)
    {
        var def = f.Definition;
        var labels = dwg.Layer(Layers.Labels);

        // Deck & barriers.
        if (f.Deck != null)
        {
            dwg.Layer(Layers.Deck).Poly(f.Deck.Outline, true);
            var bl = dwg.Layer(Layers.Barrier);
            foreach (var l in f.Deck.BarrierLines) bl.Poly(l);
        }

        // Girders.
        var web = dwg.Layer(Layers.GirderWeb);
        var flange = dwg.Layer(Layers.GirderFlange);
        foreach (var g in f.Girders)
        {
            web.Poly(g.WebLine);
            flange.Poly(g.TopFlangeLeft);
            flange.Poly(g.TopFlangeRight);
            flange.Line(g.TopFlangeLeft[0], g.TopFlangeRight[0]);
            flange.Line(g.TopFlangeLeft[^1], g.TopFlangeRight[^1]);
            var t = (g.WebLine[1].XY - g.WebLine[0].XY).Normalized();
            labels.Texts.Add(new TextItem((g.StartEnd.XY - t * textH * 2).WithZ(g.StartEnd.Z), g.Name, textH * 1.2, t.Angle));
        }

        // Support lines and bearings.
        var sl = dwg.Layer(Layers.SupportLine);
        var br = dwg.Layer(Layers.Bearing);
        double ext = textH * 3;
        foreach (var s in f.Supports)
        {
            var bs = s.Bearings.OfType<BearingResult>().ToList();
            if (bs.Count == 0) continue;
            var p0 = bs[0].TopOfSteel;
            var p1 = bs[^1].TopOfSteel;
            var a = (p0.XY - s.Direction * ext).WithZ(p0.Z);
            var b = (p1.XY + s.Direction * ext).WithZ(p1.Z);
            sl.Line(a, b);
            double r = textH * 0.5;
            foreach (var bb in bs)
            {
                var c = bb.TopOfSteel;
                br.Line(new Vec3(c.X - r, c.Y - r, c.Z), new Vec3(c.X + r, c.Y + r, c.Z));
                br.Line(new Vec3(c.X - r, c.Y + r, c.Z), new Vec3(c.X + r, c.Y - r, c.Z));
            }
            double rot = s.Direction.Angle;
            labels.Texts.Add(new TextItem((p0.XY - s.Direction * (ext + textH * 6)).WithZ(p0.Z),
                $"CL BRG {s.Name}", textH, rot));
            labels.Texts.Add(new TextItem((p0.XY - s.Direction * (ext + textH * 6) - s.Direction.Right * textH * 1.6).WithZ(p0.Z),
                $"STA {units.FormatStation(s.Station)}  SKEW {UnitSystem.FormatAngleDms(Numerics.DegToRad(s.SkewDegrees))}", textH * 0.8, rot));
        }

        // Cross frames.
        var cf = dwg.Layer(Layers.CrossFrame);
        foreach (var x in f.CrossFrames)
        {
            cf.Line(x.Left, x.Right);
            if (!x.AtSupport)
            {
                var mid = Vec3.Lerp(x.Left, x.Right, 0.5);
                var dir = (x.Right.XY - x.Left.XY).Normalized();
                labels.Texts.Add(new TextItem((mid.XY + dir.Left * textH * 0.8).WithZ(mid.Z), x.Mark, textH * 0.6, dir.Angle));
            }
        }

        // Stiffeners and connection plates: plate outlines on the web faces.
        var st = dwg.Layer(Layers.Stiffener);
        double tw = def.Girders.Section.WebThickness;
        foreach (var s in f.Stiffeners)
        {
            if (s.Side is StiffenerSide.Both or StiffenerSide.Right) AddPlate(st, s, +1, tw);
            if (s.Side is StiffenerSide.Both or StiffenerSide.Left) AddPlate(st, s, -1, tw);
        }

        // Field splices.
        var fs = dwg.Layer(Layers.FieldSplice);
        double half = def.Girders.Section.TopFlange.Width / 2 + textH * 0.3;
        foreach (var s in f.Splices)
        {
            var n = s.GirderTangent.Right;
            var c = s.Location;
            fs.Line((c.XY - n * half).WithZ(c.Z), (c.XY + n * half).WithZ(c.Z));
            if (s.Girder == 0)
                labels.Texts.Add(new TextItem((c.XY - n * (half + textH)).WithZ(c.Z), $"FS{s.Number}", textH * 0.8, s.GirderTangent.Angle));
        }
    }

    private static void AddPlate(DrawingLayer layer, StiffenerResult s, int side, double tw)
    {
        // Plate lies along its (possibly skewed) direction; thickness is perpendicular to it.
        var n = s.Direction * side;
        var t = s.Direction.Left;
        double ht = s.Plate.Thickness / 2;
        double sin = Math.Max(Math.Abs(Vec2.Cross(s.GirderTangent, s.Direction)), 0.2);
        var c = s.Location.XY + n * (tw / 2 / sin);
        var z = s.Location.Z;
        var p1 = c - t * ht;
        var p2 = c + t * ht;
        var p3 = p2 + n * s.Plate.Width;
        var p4 = p1 + n * s.Plate.Width;
        layer.Poly(new[] { p1.WithZ(z), p2.WithZ(z), p3.WithZ(z), p4.WithZ(z) }, true);
    }
}

/// <summary>Plan outlines of caps, pedestals, columns and foundations (hidden below the deck).</summary>
public static class SubstructurePlanBuilder
{
    public static void Add(PlanDrawing dwg, Substructure.SubstructureModel model)
    {
        foreach (var u in model.Units)
        {
            Vec3 P(double s, double n, double z) => u.World(s, n, z);
            double zc = u.CapTop.Count > 0 ? u.CapTop.Max(p => p.Y) : 0;
            dwg.Layer(Layers.SubCap).Poly(new[] { P(u.CapS0, u.CapN0, zc), P(u.CapS1, u.CapN0, zc), P(u.CapS1, u.CapN1, zc), P(u.CapS0, u.CapN1, zc) }, true);
            if (u.Backwall is { } bw)
                dwg.Layer(Layers.SubCap).Poly(new[] { P(u.CapS0, bw.N0, zc), P(u.CapS1, bw.N0, zc), P(u.CapS1, bw.N1, zc), P(u.CapS0, bw.N1, zc) }, true);
            foreach (var pd in u.Pedestals.Where(p => p.Height >= UnitSystem.In(0.125)))
            {
                double a = pd.LengthS / 2, b = pd.WidthN / 2;
                dwg.Layer(Layers.SubPedestal).Poly(new[] { P(pd.S - a, pd.N - b, pd.SeatZ), P(pd.S + a, pd.N - b, pd.SeatZ), P(pd.S + a, pd.N + b, pd.SeatZ), P(pd.S - a, pd.N + b, pd.SeatZ) }, true);
            }
            foreach (var c in u.Columns)
                dwg.Layer(Layers.SubColumn).Poly(Outline(u, c.S, c.N, c.SizeS, c.SizeN, c.Shape == Bridge.ColumnShape.Round, c.BottomZ), true);
            foreach (var f in u.Foundations)
            {
                if (f.SizeS > 0)
                    dwg.Layer(Layers.SubFoundation).Poly(Outline(u, f.S, f.N, f.SizeS, f.SizeN, f.Type == Bridge.FoundationType.DrilledShaft, f.TopZ), true);
                foreach (var p in f.Piles)
                    dwg.Layer(Layers.SubFoundation).Poly(Outline(u, p.S, p.N, UnitSystem.In(14), UnitSystem.In(14), false, p.TopZ), true);
            }
        }
    }

    internal static IEnumerable<Vec3> Outline(Substructure.SubstructureUnit u, double s, double n, double sizeS, double sizeN, bool round, double z)
    {
        if (round)
            return Enumerable.Range(0, 36).Select(i => { var v = Vec2.FromAngle(2 * Math.PI * i / 36); return u.World(s + v.X * sizeS / 2, n + v.Y * sizeN / 2, z); }).ToList();
        double a = sizeS / 2, b = sizeN / 2;
        return new[] { u.World(s - a, n - b, z), u.World(s + a, n - b, z), u.World(s + a, n + b, z), u.World(s - a, n + b, z) };
    }
}
