using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Segmental;

/// <summary>
/// The box cross section of S3-20 (p268) in section coordinates: x lateral (right +), y DOWN from the top surface, both measured in the
/// section plane, which is normal to the top surface (the box is cast square to the cross-sloped, graded top). Rings start on the CL so a
/// ring splits into two halves at index 0 and <see cref="OuterCl"/> (void: 0 and <see cref="VoidBottomCl"/>) for the cap triangulation.
/// </summary>
public static class SegmentalSection
{
    /// <summary>Index of the soffit CL point (0, H) in <see cref="Outer"/>.</summary>
    public const int OuterCl = 6;
    public const int FilletPoints = 5;

    /// <summary>Outer outline (12 points): top CL, right tip, wing, web root, soffit corner, soffit CL, then mirrored.</summary>
    public static List<Vec2> Outer(SegmentalBoxSection b, double h)
    {
        double rt = b.TopWidth / 2, w2 = b.HalfSoffit(h);
        var right = new[] { new Vec2(rt, 0), new Vec2(rt, b.WingTip), new Vec2(rt - b.WingFlat, b.WingTip), new Vec2(b.WebRootX, b.WebTopDepth), new Vec2(w2, h) };
        var l = new List<Vec2> { new(0, 0) };
        l.AddRange(right);
        l.Add(new Vec2(0, h));
        for (int i = right.Length - 1; i >= 0; i--) l.Add(new Vec2(-right[i].X, right[i].Y));
        return l;
    }

    /// <summary>Index of the bottom-slab CL point (0, H - T) in <see cref="Void"/>.</summary>
    public static int VoidBottomCl(SegmentalBoxSection b) => 2 + (b.FilletRadius > 0 ? FilletPoints : 1) + 2;

    /// <summary>Inner void: top slab underside (flat, haunch), R fillet at the inner web corner, inner web face (parallel to the outer, 4:1),
    /// bottom haunch, bottom slab top; then mirrored. <paramref name="t"/> = bottom slab thickness at the CL; <paramref name="noFillet"/> =
    /// sharp corner (the plan's gross-area convention).</summary>
    public static List<Vec2> Void(SegmentalBoxSection b, double h, double t, bool noFillet = false)
    {
        double xc = b.WebRootX - b.WebHorizontal;            // inner web face at the top haunch corner (127")
        double yc = b.WebTopDepth;
        double yh = h - t - b.BottomHaunchRise;
        double x4 = xc - (yh - yc) / b.WebSlope;
        double x5 = x4 - b.BottomHaunchRun;
        var a = new Vec2(b.TopSlabFlat, b.TopSlab);
        var corner = new Vec2(xc, yc);
        var c = new Vec2(x4, yh);
        var right = new List<Vec2> { a };
        if (b.FilletRadius > 0 && !noFillet) right.AddRange(Fillet(a, corner, c, b.FilletRadius, FilletPoints));
        else right.Add(corner);
        right.Add(c);
        right.Add(new Vec2(x5, h - t));
        var l = new List<Vec2> { new(0, b.TopSlab) };
        l.AddRange(right);
        l.Add(new Vec2(0, h - t));
        for (int i = right.Count - 1; i >= 0; i--) l.Add(new Vec2(-right[i].X, right[i].Y));
        return l;
    }

    /// <summary>Tangent arc of radius r replacing the corner p between the lines to a and c (n points, tangent points included).</summary>
    public static List<Vec2> Fillet(Vec2 a, Vec2 p, Vec2 c, double r, int n)
    {
        var d1 = (a - p).Normalized();
        var d2 = (c - p).Normalized();
        double cosT = Math.Clamp(Vec2.Dot(d1, d2), -1, 1);
        double half = Math.Acos(cosT) / 2;
        double td = r / Math.Tan(half);
        var t1 = p + d1 * td;
        var t2 = p + d2 * td;
        var centre = p + (d1 + d2).Normalized() * (r / Math.Sin(half));
        double a1 = (t1 - centre).Angle, a2 = (t2 - centre).Angle;
        double da = a2 - a1;
        while (da > Math.PI) da -= 2 * Math.PI;
        while (da < -Math.PI) da += 2 * Math.PI;
        var pts = new List<Vec2>(n);
        for (int i = 0; i < n; i++) pts.Add(centre + Vec2.FromAngle(a1 + da * i / (n - 1)) * r);
        return pts;
    }

    /// <summary>Concrete area of the hollow section (outer minus void).</summary>
    public static double Area(SegmentalBoxSection b, double h, double t, bool noFillet = false) =>
        Math.Abs(MeshBuilder.SignedArea(Outer(b, h))) - Math.Abs(MeshBuilder.SignedArea(Void(b, h, t, noFillet)));

    /// <summary>Area of the full outline (solid diaphragm / end segment).</summary>
    public static double SolidArea(SegmentalBoxSection b, double h) => Math.Abs(MeshBuilder.SignedArea(Outer(b, h)));
}

/// <summary>A section placed in 3D: origin = top CL point, Lat = section x (right), Up = minus section y.</summary>
public readonly record struct SectionFrame(Vec3 Origin, Vec3 Lat, Vec3 Up, Vec3 Tangent)
{
    public Vec3 At(Vec2 p) => Origin + Lat * p.X - Up * p.Y;
}

/// <summary>Closed lofts through placed sections (hollow box, or the solid outline).</summary>
public static class SegmentalLoft
{
    public static Mesh Build(IReadOnlyList<(SectionFrame f, List<Vec2> outer, List<Vec2>? voidRing)> rings, int outerCl, int voidCl)
    {
        var m = new Mesh();
        int no = rings[0].outer.Count;
        bool hollow = rings[0].voidRing != null;
        int nv = hollow ? rings[0].voidRing!.Count : 0;
        int per = no + nv;
        foreach (var (f, o, v) in rings)
        {
            foreach (var p in o) m.Add(f.At(p));
            if (hollow) foreach (var p in v!) m.Add(f.At(p));
        }
        for (int k = 0; k + 1 < rings.Count; k++)
        {
            int a0 = k * per, b0 = (k + 1) * per;
            for (int i = 0; i < no; i++)
            {
                int i1 = (i + 1) % no;
                m.Tri(a0 + i, a0 + i1, b0 + i1); m.Tri(a0 + i, b0 + i1, b0 + i);
            }
            for (int i = 0; i < nv; i++)
            {
                int i1 = (i + 1) % nv;
                int p00 = a0 + no + i, p01 = a0 + no + i1, p10 = b0 + no + i, p11 = b0 + no + i1;
                m.Tri(p00, p11, p01); m.Tri(p00, p10, p11);
            }
        }
        // caps: two half polygons (right: CL top -> right side -> CL bottom [-> void up to its top CL]; left likewise)
        var halves = new List<List<int>>();
        var right = Enumerable.Range(0, outerCl + 1).ToList();
        var left = Enumerable.Range(outerCl, no - outerCl).Append(0).ToList();
        if (hollow)
        {
            for (int j = voidCl; j >= 0; j--) right.Add(no + j);
            // left: outer from the soffit CL round to the top CL, then the void from its top CL round the left side to its bottom CL
            left = Enumerable.Range(outerCl, no - outerCl).Append(0).ToList();
            left.Add(no + 0);
            for (int j = nv - 1; j >= voidCl; j--) left.Add(no + j);
        }
        halves.Add(right);
        halves.Add(left);
        var sec0 = rings[0].outer.Concat(rings[0].voidRing ?? new List<Vec2>()).ToList();
        int last = (rings.Count - 1) * per;
        foreach (var h in halves)
        {
            var poly = h.Select(i => sec0[i]).ToList();
            // section coordinates have y DOWN: flip y so the triangulation's CCW is the geometric CCW of (x, -y)... handled by the volume check below
            foreach (var (a, b, c) in MeshBuilder.Triangulate(poly))
            {
                m.Tri(h[a], h[c], h[b]);
                m.Tri(last + h[a], last + h[b], last + h[c]);
            }
        }
        if (m.Volume() < 0)
            for (int i = 0; i < m.Indices.Count; i += 3) (m.Indices[i + 1], m.Indices[i + 2]) = (m.Indices[i + 2], m.Indices[i + 1]);
        return m;
    }
}
