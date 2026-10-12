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

/// <summary>Closed lofts through placed sections. Every ring has the outer outline; the void is present on a contiguous run of rings that
/// includes the first or the last ring (hollow box, solid outline, or a hollow part closed by a solid diaphragm = ONE closed shell).</summary>
public static class SegmentalLoft
{
    public static Mesh Build(IReadOnlyList<(SectionFrame f, List<Vec2> outer, List<Vec2>? voidRing)> rings, int outerCl, int voidCl)
    {
        var m = new Mesh();
        int no = rings[0].outer.Count;
        var baseIdx = new int[rings.Count];
        var voidIdx = new int[rings.Count];
        for (int k = 0; k < rings.Count; k++)
        {
            var (f, o, v) = rings[k];
            baseIdx[k] = m.Positions.Count;
            foreach (var p in o) m.Add(f.At(p));
            voidIdx[k] = -1;
            if (v != null) { voidIdx[k] = m.Positions.Count; foreach (var p in v) m.Add(f.At(p)); }
        }
        int nv = rings.FirstOrDefault(r => r.voidRing != null).voidRing?.Count ?? 0;
        // Orientation: rings CCW in section (x, y-down) = normal along the sweep (+T); outer faces (e x T) outward, void faces reversed.
        for (int k = 0; k + 1 < rings.Count; k++)
        {
            int a0 = baseIdx[k], b0 = baseIdx[k + 1];
            for (int i = 0; i < no; i++)
            {
                int i1 = (i + 1) % no;
                m.Tri(a0 + i, a0 + i1, b0 + i1); m.Tri(a0 + i, b0 + i1, b0 + i);
            }
            if (voidIdx[k] >= 0 && voidIdx[k + 1] >= 0)
            {
                int va = voidIdx[k], vb = voidIdx[k + 1];
                for (int i = 0; i < nv; i++)
                {
                    int i1 = (i + 1) % nv;
                    m.Tri(va + i, vb + i1, va + i1); m.Tri(va + i, vb + i, vb + i1);
                }
            }
        }
        // end caps (start faces -T, end faces +T): two half polygons each, so the hollow cap is two simple C-shaped polygons
        void Cap(int k, bool start)
        {
            var (_, o, v) = rings[k];
            var halves = new List<List<int>>();
            var right = Enumerable.Range(0, outerCl + 1).ToList();
            var left = Enumerable.Range(outerCl, no - outerCl).Append(0).ToList();
            if (v != null)
            {
                for (int j = voidCl; j >= 0; j--) right.Add(no + j);
                left.Add(no + 0);
                for (int j = nv - 1; j >= voidCl; j--) left.Add(no + j);
            }
            halves.Add(right); halves.Add(left);
            var sec = o.Concat(v ?? new List<Vec2>()).ToList();
            int b = baseIdx[k];
            int Map(int i) => i < no ? b + i : voidIdx[k] + (i - no);
            foreach (var h in halves)
                foreach (var (a, bb, c) in MeshBuilder.Triangulate(h.Select(i => sec[i]).ToList()))
                {
                    if (start) m.Tri(Map(h[a]), Map(h[c]), Map(h[bb]));
                    else m.Tri(Map(h[a]), Map(h[bb]), Map(h[c]));
                }
        }
        Cap(0, true);
        Cap(rings.Count - 1, false);
        // void closed by a solid zone: cap the void where it ends (facing into the void)
        for (int k = 0; k + 1 < rings.Count; k++)
        {
            bool here = voidIdx[k] >= 0, next = voidIdx[k + 1] >= 0;
            if (here == next) continue;
            int kv = here ? k : k + 1;                    // ring holding the void at the transition
            bool voidBefore = here;                      // void on the -T side of the transition ring
            var v = rings[kv].voidRing!;
            foreach (var (a, b, c) in MeshBuilder.Triangulate(v))
            {
                // the concrete face points into the void: -T when the void lies before (like a start cap), +T otherwise
                if (voidBefore) m.Tri(voidIdx[kv] + a, voidIdx[kv] + c, voidIdx[kv] + b);
                else m.Tri(voidIdx[kv] + a, voidIdx[kv] + b, voidIdx[kv] + c);
            }
        }
        if (m.Volume() < 0)
            for (int i = 0; i < m.Indices.Count; i += 3) (m.Indices[i + 1], m.Indices[i + 2]) = (m.Indices[i + 2], m.Indices[i + 1]);
        return m;
    }
}
