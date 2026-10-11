using SBM.Core.Concrete;
using SBM.Core.Geometry;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Substructure;

/// <summary>
/// Bent-bar shapes built from the plans' bend diagrams: every dimension is OUT-TO-OUT as the plans print it (the bar-list length is their
/// plain sum, no bend deductions - "ALL DIMENSIONS ARE OUT TO OUT. D = PIN DIAMETER."). The bar centerline is the outline inset by half
/// a bar diameter; the scheduled length stays the listed length.
/// </summary>
public sealed partial class SubstructureGenerator
{
    /// <summary>
    /// Bent bar from the OUTSIDE polyline <paramref name="outside"/> (tips and outside corners, 2D). <paramref name="side"/> = +1 when the bar
    /// body lies to the LEFT of the travel direction, -1 to the right. Returns the shape (rounded centerline, plan dimensions, plan pin) and
    /// the SHARP centerline vertices (for placing the 3D bar).
    /// </summary>
    internal static (BarShape shape, Vec2[] sharp) PlanBent(RebarSize size, IReadOnlyList<Vec2> outside, double[] dims, int side, string code, string desc, double pin, double listed)
    {
        double r = size.Diameter / 2;
        int n = outside.Count;
        var dir = new Vec2[n - 1];
        for (int i = 0; i < n - 1; i++) dir[i] = (outside[i + 1] - outside[i]).Normalized();
        var line = new (Vec2 p, Vec2 d)[n - 1];
        for (int i = 0; i < n - 1; i++) line[i] = (outside[i] + dir[i].Left * (side * r), dir[i]);
        var c = new Vec2[n];
        c[0] = line[0].p;
        c[n - 1] = line[n - 2].p + dir[n - 2] * Vec2.Dot(outside[n - 1] - outside[n - 2], dir[n - 2]);
        for (int k = 1; k < n - 1; k++)
        {
            var (p0, d0) = line[k - 1]; var (p1, d1) = line[k];
            double den = Vec2.Cross(d0, d1);
            c[k] = Math.Abs(den) < 1e-9 ? p1 : p0 + d0 * (Vec2.Cross(p1 - p0, d1) / den);
        }
        if (pin <= 0) pin = size.MinPinDiameter;
        double rc = pin / 2 + r;
        var bends = new List<(Vec2, double)>();
        var shape = new BarShape
        {
            Code = code, Description = desc, Centerline = BarShape.Rounded(c, rc, bends), PinDiameter = pin, FixedLength = listed,
        };
        shape.Bends.AddRange(bends);
        for (int i = 0; i < Math.Min(dims.Length, n - 1); i++)
            shape.Dimensions.Add(new BarDimension(((char)('A' + i)).ToString(), dims[i], outside[i], outside[i + 1], dir[i].Left * -side));
        return (shape, c);
    }

    /// <summary>U bar: legs <paramref name="leg1"/> / <paramref name="leg2"/> (from the OUTSIDE face of the base to the tips) and the base, local (u along the base, v away from the base's outside face toward the legs).</summary>
    internal static (BarShape shape, Vec2[] sharp) PlanU(RebarSize size, double leg1, double baseLen, double leg2, double pin, double listed)
        => PlanBent(size, new[] { new Vec2(0, leg1), new Vec2(0, 0), new Vec2(baseLen, 0), new Vec2(baseLen, leg2) }, new[] { leg1, baseLen, leg2 }, +1,
            "U", $"U bar {Dim(leg1)} + {Dim(baseLen)} + {Dim(leg2)} out to out", pin, listed);

    /// <summary>L bar: leg and run (out to out), local u along the run, v along the leg.</summary>
    internal static (BarShape shape, Vec2[] sharp) PlanL(RebarSize size, double leg, double run, double pin, double listed)
        => PlanBent(size, new[] { new Vec2(0, leg), new Vec2(0, 0), new Vec2(run, 0) }, new[] { leg, run }, +1,
            "L", $"L bar {Dim(leg)} + {Dim(run)} out to out", pin, listed);

    private static string Dim(double m) => new SBM.Core.Units.UnitSystem().FormatLength(m, SBM.Core.Units.LengthKind.Short);

    /// <summary>
    /// End bar 5a14 (plan bend diagram): vertical leg (24"), top run (4'-0, to the corner of the slanted leg) and a slanted leg (10", 2 1/2" horizontal).
    /// Local: u along the top run, v up; the top's outside face is v = 0, the vertical leg hangs at u = 0, the slanted leg leaves the far end downward
    /// and OUTWARD (+u), parallel to the raked end face.
    /// </summary>
    internal static (BarShape shape, Vec2[] sharp) PlanA14(RebarSize size, double vLeg, double top, double slant, double slantRun, double pin, double listed)
    {
        double drop = Math.Sqrt(Math.Max(slant * slant - slantRun * slantRun, 0));
        return PlanBent(size, new[] { new Vec2(0, -vLeg), new Vec2(0, 0), new Vec2(top, 0), new Vec2(top + slantRun, -drop) }, new[] { vLeg, top, slant }, -1,
            "A14", $"Bar {Dim(vLeg)} + {Dim(top)} + {Dim(slant)} slanted ({Dim(slantRun)} run), out to out", pin, listed);
    }

    /// <summary>
    /// Hoop of the plan's bend diagram (6c1 / 6c2 / 5n2): rectangle <paramref name="w"/> x <paramref name="h"/> out to out with two straight tails that lap
    /// <paramref name="lapV"/> down the right side and <paramref name="lapH"/> along the top (list length = 2 (w + h) + the two laps). The tails run
    /// one bar diameter inside the closing pass. Local: u across, v up; the bar starts on the right side, runs up, left along the top, down, along the bottom, up the right
    /// side and ends along the top.
    /// </summary>
    internal static (BarShape shape, Vec2[] sharp) PlanHoop(RebarSize size, double w, double h, double lapV, double lapH, double pin, double listed)
    {
        double db = size.Diameter, r = db / 2;
        var c = new List<Vec2>
        {
            new Vec2(w - r - db, h - lapV), new Vec2(w - r - db, h - r - db), new Vec2(r, h - r - db), new Vec2(r, r), new Vec2(w - r, r), new Vec2(w - r, h - r),
            new Vec2(w - r - lapH, h - r),
        };
        // the tails are inset one bar diameter: start tail inside the final rise, final top pass outside the first top pass
        c[0] = new Vec2(w - r - db, h - lapV); c[1] = new Vec2(w - r - db, h - r - db); c[2] = new Vec2(r, h - r - db);
        c[5] = new Vec2(w - r, h - r); c[6] = new Vec2(w - r - lapH, h - r);
        if (pin <= 0) pin = size.MinPinDiameter;
        double rc = pin / 2 + r;
        var bends = new List<(Vec2, double)>();
        var shape = new BarShape
        {
            Code = "HOOP", Description = $"Hoop {Dim(w)} x {Dim(h)} with {Dim(lapV)} / {Dim(lapH)} lap tails, out to out", Centerline = BarShape.Rounded(c, rc, bends), PinDiameter = pin, FixedLength = listed,
            Dimensions =
            {
                new BarDimension("A", h, new Vec2(0, 0), new Vec2(0, h), new Vec2(-1, 0)), new BarDimension("B", w, new Vec2(0, h), new Vec2(w, h), new Vec2(0, 1)),
                new BarDimension("C", lapV, new Vec2(w, h - lapV), new Vec2(w, h), new Vec2(1, 0)), new BarDimension("D", lapH, new Vec2(w - lapH, h), new Vec2(w, h), new Vec2(0, 1)),
            },
        };
        shape.Bends.AddRange(bends);
        return (shape, c.ToArray());
    }
}
