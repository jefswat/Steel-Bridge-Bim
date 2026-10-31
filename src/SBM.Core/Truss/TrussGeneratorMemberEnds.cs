using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Truss;

public static partial class TrussGenerator
{
    private sealed partial class Gen
    {
        // ------------------------------------------------------------------ member end cuts, notches and holes (TrussMemberDefinition.StartCut / EndCut,
        // Notches, Holes; TrussEndConnection.TrimMember) - floor beam / bracket ends (PTC floor beam connections, TRUSS-DESIGN.md section 25)

        /// <summary>Horizontal unit direction of the member axis (normal of a vertical end cut); null = the member is vertical.</summary>
        private static Vec3? HorizontalAxis(Inst m)
        {
            var h = m.Axis - Up * Vec3.Dot(m.Axis, Up);
            return h.Length < 1e-6 ? null : h.Normalized();
        }

        /// <summary>Moves the vertices of the square end face(s) of a member mesh along the axis onto the vertical plane through the cut point on the axis
        /// (<see cref="TrussEndCut.Vertical"/>), in either direction. The exact-extrusion recipes are dropped (IFC face set).</summary>
        private static Mesh VerticalCutMesh(Inst m, Mesh mesh, Vec3 a0, double cut)
        {
            if (HorizontalAxis(m) is not { } n) return mesh;
            double den = Vec3.Dot(n, m.Axis);
            if (Math.Abs(den) < 0.2) return mesh;
            bool va = m.Def.StartCut == TrussEndCut.Vertical, vb = m.Def.EndCut == TrussEndCut.Vertical;
            var pa = a0; var pb = a0 + m.Axis * cut;
            var o = new Mesh();
            foreach (var p0 in mesh.Positions)
            {
                var p = p0;
                double s = Vec3.Dot(p - a0, m.Axis);
                if (va && Math.Abs(s) < 1e-6) p -= m.Axis * Math.Clamp(Vec3.Dot(n, p - pa) / den, -cut / 2, cut / 2);
                else if (vb && Math.Abs(s - cut) < 1e-6) p -= m.Axis * Math.Clamp(Vec3.Dot(n, p - pb) / den, -cut / 2, cut / 2);
                o.Add(p);
            }
            o.Indices.AddRange(mesh.Indices);
            return o;
        }

        /// <summary>Moves every vertex of <paramref name="mesh"/> lying on the plane normal to <paramref name="dir"/> at distance <paramref name="sFrom"/> from
        /// <paramref name="o"/> to <paramref name="sTo"/> (a square member end trimmed / extended along its axis). Recipes dropped.</summary>
        private static Mesh MoveEndFace(Mesh mesh, Vec3 o, Vec3 dir, double sFrom, double sTo, Vec3 yAxis = default, double yBelow = 0, double dy = 0)
        {
            // OPTIONAL (tapered members): vertices of the lowered part (section y below yBelow, measured along yAxis from o) also move by dy along yAxis, so the
            // sloping bottom continues on its line
            var r = new Mesh();
            foreach (var p in mesh.Positions)
            {
                if (Math.Abs(Vec3.Dot(p - o, dir) - sFrom) >= 1e-6) { r.Add(p); continue; }
                var q = p + dir * (sTo - sFrom);
                if (dy != 0 && Vec3.Dot(p - o, yAxis) < yBelow - 1e-9) q += yAxis * dy;
                r.Add(q);
            }
            r.Indices.AddRange(mesh.Indices);
            return r;
        }

        /// <summary>Moves the vertices of <paramref name="mesh"/> on the plane normal to <paramref name="dir"/> at <paramref name="s"/> (from <paramref name="o"/>)
        /// along <paramref name="dir"/> onto the vertical plane through <paramref name="o"/> + dir s whose normal is <paramref name="n"/> (a web extension
        /// following a vertical member end cut). Recipes dropped.</summary>
        private static Mesh VerticalFace(Mesh mesh, Vec3 o, Vec3 dir, double s, Vec3 n)
        {
            double den = Vec3.Dot(n, dir);
            if (Math.Abs(den) < 0.2) return mesh;
            var p0 = o + dir * s;
            var r = new Mesh();
            foreach (var p in mesh.Positions)
                r.Add(Math.Abs(Vec3.Dot(p - o, dir) - s) < 1e-6 ? p - dir * (Vec3.Dot(n, p - p0) / den) : p);
            r.Indices.AddRange(mesh.Indices);
            return r;
        }

        /// <summary>A copy of a part with another mesh and length (the mesh is init-only).</summary>
        private static Steel.SteelPart CopyPart(Steel.SteelPart p, Mesh mesh, double length) => new()
        {
            Mark = p.Mark, Category = p.Category, Assembly = p.Assembly, Description = p.Description, Mesh = mesh, Material = p.Material, Role = p.Role, Length = length,
            NominalWeightLbPerFt = p.NominalWeightLbPerFt, Assumption = p.Assumption, Density = p.Density, Fabrication = p.Fabrication, MassNotCounted = p.MassNotCounted,
            Member = p.Member, Piece = p.Piece,
        };

        private readonly record struct NotchBox(double S0, double S1, double X0, double X1, double Y0, double Y1, string Label);

        /// <summary>The member's notches in piece coordinates (s from the cut start along the axis, section x / y as the pieces).</summary>
        private List<NotchBox> MemberNotches(Inst m, double cut)
        {
            var res = new List<NotchBox>();
            if (m.Def.Notches is not { Count: > 0 } ns) return res;
            foreach (var n in ns)
            {
                if (n.Length <= 0 || n.XMax <= n.XMin || n.YMax <= n.YMin) { Warn($"Truss: notch '{n.Label}' of {m.Mark} - empty box; ignored."); continue; }
                if (n.Unresolved.Length > 0) Warn(n.Unresolved);
                foreach (bool start in new[] { true, false })
                {
                    if (n.End == TrussMemberEnd.Start && !start || n.End == TrussMemberEnd.End && start) continue;
                    double s0 = start ? n.From : cut - n.From - n.Length, s1 = s0 + n.Length;
                    res.Add(new NotchBox(Math.Max(0, s0), Math.Min(cut, s1), n.XMin, n.XMax, n.YMin, n.YMax, n.Label));
                }
            }
            if (res.Count > 0) { _notchMembers++; _notchCount += res.Count; }
            return res;
        }

        /// <summary>The member's holes in piece coordinates (s from the cut start, section y as the pieces, diameter).</summary>
        private List<(double s, double y, double d)> MemberHoles(Inst m, Vec3 a0, double cut)
        {
            var res = new List<(double, double, double)>();
            if (m.Def.Holes is not { Count: > 0 } hs) return res;
            double len = (m.B - m.A).Length;
            foreach (var h in hs.Where(h => h.Diameter > 0))
                foreach (bool start in new[] { true, false })
                {
                    if (h.End == TrussMemberEnd.Start && !start || h.End == TrussMemberEnd.End && start) continue;
                    res.Add((start ? h.Distance - m.SA : len - m.SA - h.Distance, h.Y, h.Diameter));
                }
            return res;
        }

        /// <summary>Polygon minus an axis-aligned box (x0..x1, y0..y1): up to four simple pieces (left, right, below, above).</summary>
        private static List<List<Vec2>> Subtract(IReadOnlyList<Vec2> poly, double x0, double x1, double y0, double y1)
        {
            var res = new List<List<Vec2>>();
            double px0 = poly.Min(q => q.X), px1 = poly.Max(q => q.X), py0 = poly.Min(q => q.Y), py1 = poly.Max(q => q.Y);
            if (px1 <= x0 + 1e-9 || px0 >= x1 - 1e-9 || py1 <= y0 + 1e-9 || py0 >= y1 - 1e-9) { res.Add(poly.ToList()); return res; }
            void Keep(List<Vec2> p) { if (p.Count >= 3 && Math.Abs(MeshBuilder.SignedArea(p.ToArray())) > 1e-10) res.Add(p); }
            Keep(PlateBuilder.ClipHalfPlane(poly, new Vec2(-1, 0), -x0));                                      // x <= x0
            Keep(PlateBuilder.ClipHalfPlane(poly, new Vec2(1, 0), x1));                                        // x >= x1
            var mid = PlateBuilder.ClipHalfPlane(PlateBuilder.ClipHalfPlane(poly, new Vec2(1, 0), x0), new Vec2(-1, 0), -x1);
            if (mid.Count >= 3)
            {
                Keep(PlateBuilder.ClipHalfPlane(mid, new Vec2(0, -1), -y0));                                   // y <= y0
                Keep(PlateBuilder.ClipHalfPlane(mid, new Vec2(0, 1), y1));                                     // y >= y1
            }
            return res;
        }

        private static bool IsBox(IReadOnlyList<Vec2> p)
        {
            if (p.Count != 4) return false;
            double x0 = p.Min(q => q.X), x1 = p.Max(q => q.X), y0 = p.Min(q => q.Y), y1 = p.Max(q => q.Y);
            return p.All(q => (Math.Abs(q.X - x0) < 1e-9 || Math.Abs(q.X - x1) < 1e-9) && (Math.Abs(q.Y - y0) < 1e-9 || Math.Abs(q.Y - y1) < 1e-9));
        }

        private int _notchHolesCut, _notchMembers, _notchCount;

        /// <summary>Builds a section piece split by the member's notches / holes. False = the piece is not affected (the caller builds it as usual).</summary>
        private bool NotchedPiece(Mesh mesh, Inst m, Vec3 a0, TrussSectionPiece pc, double ps, double pl, List<NotchBox> notches, List<(double s, double y, double d)> holes)
        {
            double pe = ps + pl;
            double bx0 = pc.Polygon.Min(q => q.X), bx1 = pc.Polygon.Max(q => q.X), by0 = pc.Polygon.Min(q => q.Y), by1 = pc.Polygon.Max(q => q.Y);
            var act = notches.Where(n => n.S0 < pe - 1e-9 && n.S1 > ps + 1e-9 && n.X0 < bx1 - 1e-9 && n.X1 > bx0 + 1e-9 && n.Y0 < by1 - 1e-9 && n.Y1 > by0 + 1e-9).ToList();
            // holes through a rectangular piece (web plate) that the hole axis crosses entirely inside the piece
            var hs = IsBox(pc.Polygon) ? holes.Where(h => h.y - h.d / 2 > by0 + 1e-6 && h.y + h.d / 2 < by1 - 1e-6 && h.s - h.d / 2 > ps + 1e-6 && h.s + h.d / 2 < pe - 1e-6).ToList() : new();
            if (act.Count == 0 && hs.Count == 0) return false;
            var cuts = new SortedSet<double> { ps, pe };
            foreach (var n in act) { if (n.S0 > ps && n.S0 < pe) cuts.Add(n.S0); if (n.S1 > ps && n.S1 < pe) cuts.Add(n.S1); }
            var cl = cuts.ToList();
            for (int i = 0; i + 1 < cl.Count; i++)
            {
                double u0 = cl[i], u1 = cl[i + 1];
                if (u1 - u0 < 1e-6) continue;
                var polys = new List<List<Vec2>> { pc.Polygon.ToList() };
                foreach (var n in act.Where(n => n.S0 <= u0 + 1e-9 && n.S1 >= u1 - 1e-9))
                    polys = polys.SelectMany(p => Subtract(p, n.X0, n.X1, n.Y0, n.Y1)).ToList();
                var hh = polys.Count == 1 && IsBox(polys[0]) ? hs.Where(h => h.s - h.d / 2 > u0 + 1e-6 && h.s + h.d / 2 < u1 - 1e-6).ToList() : new();
                foreach (var p in polys)
                {
                    if (hh.Count > 0)
                    {
                        // plate in (s along the axis, y) with round holes, thickness across x (frame z = axis x y = -section x)
                        double x0 = p.Min(q => q.X), x1 = p.Max(q => q.X), y0 = p.Min(q => q.Y), y1 = p.Max(q => q.Y);
                        var f = new Frame3(a0, m.Axis, m.Y, Vec3.Cross(m.Axis, m.Y));
                        var outline = new[] { new Vec2(u0, y0), new Vec2(u1, y0), new Vec2(u1, y1), new Vec2(u0, y1) };
                        PlateBuilder.AddPlanar(mesh, outline, hh.Select(h => new Hole(new Vec2(h.s, h.y), h.d)).ToList(), f, -x1, -x0);
                        _notchHolesCut += hh.Count;
                        continue;
                    }
                    var poly = p.ToArray();
                    if (MeshBuilder.SignedArea(poly) < 0) Array.Reverse(poly);
                    var ra = poly.Select(q => a0 + m.Axis * u0 + m.X * q.X + m.Y * q.Y).ToArray();
                    MeshBuilder.AddLoft(mesh, poly, new[] { ra, ra.Select(q => q + m.Axis * (u1 - u0)).ToArray() });
                }
            }
            return true;
        }
    }
}
