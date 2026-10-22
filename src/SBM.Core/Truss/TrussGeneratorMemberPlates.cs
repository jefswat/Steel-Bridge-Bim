using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;

namespace SBM.Core.Truss;

public static partial class TrussGenerator
{
    private sealed partial class Gen
    {
        // ------------------------------------------------------------------ joint plates on one member (TrussJointPlate.Member: fills / splice bars of rolled members, QC PTC I051)

        /// <summary>Level of a member face within a band, from the section polygons scanned across the band: WebOutside = the largest outer x of the
        /// outermost solid run (+x half), WebInside = the largest inner x of that run (the plate goes inward from it), Top / Bottom = the extreme y of the
        /// material in an x band. Null = no material in the band.</summary>
        private static double? FaceLevel(TrussBuiltSection sec, double offsetZ, TrussPlateFace face, double b0, double b1, Func<TrussSectionPiece, bool>? present = null)
        {
            double? best = null;
            const int n = 9;
            for (int k = 0; k < n; k++)
            {
                double s = b0 + (b1 - b0) * (k + 0.5) / n;
                var iv = new List<(double lo, double hi)>();
                bool across = face is TrussPlateFace.Top or TrussPlateFace.Bottom;
                foreach (var pc in sec.Pieces)
                {
                    if (present != null ? !present(pc) : (pc.Length > 0 || !double.IsNaN(pc.Start) || pc.BetweenGussets)) continue;   // only the pieces present under the plate
                    var poly = pc.Polygon;
                    var xs = new List<double>();
                    for (int a = 0, b = poly.Length - 1; a < poly.Length; b = a++)
                    {
                        var p = poly[a]; var q = poly[b];
                        // across: vertical scan line x = s -> y values; webs: horizontal scan line y = s -> x values
                        double pa = across ? p.X : p.Y + offsetZ, qa = across ? q.X : q.Y + offsetZ;
                        if ((pa > s) == (qa > s)) continue;
                        double t = (s - pa) / (qa - pa);
                        xs.Add(across ? p.Y + offsetZ + (q.Y - p.Y) * t : p.X + (q.X - p.X) * t);
                    }
                    xs.Sort();
                    for (int i = 0; i + 1 < xs.Count; i += 2) iv.Add((xs[i], xs[i + 1]));
                }
                if (iv.Count == 0) continue;
                double v;
                switch (face)
                {
                    case TrussPlateFace.Top: v = iv.Max(r => r.hi); best = best is { } b1t ? Math.Max(b1t, v) : v; break;
                    case TrussPlateFace.Bottom: v = iv.Min(r => r.lo); best = best is { } b2 ? Math.Min(b2, v) : v; break;
                    default:
                        // outermost run on the +x side, grown inward over touching runs
                        var pos = iv.Where(r => r.hi > 1e-6).OrderByDescending(r => r.hi).ToList();
                        if (pos.Count == 0) continue;
                        double hi = pos[0].hi, lo = pos[0].lo;
                        bool grew = true;
                        while (grew) { grew = false; foreach (var r in pos) if (r.lo < lo - 1e-9 && r.hi >= lo - 5e-4) { lo = r.lo; grew = true; } }
                        if (face == TrussPlateFace.WebOutside) { v = hi; best = best is { } b3 ? Math.Max(b3, v) : v; }
                        else { if (lo <= 1e-4) continue; v = lo; best = best is { } b4 ? Math.Max(b4, v) : v; }   // inner face; a web across the centre = no inner face here
                        break;
                }
            }
            return best;
        }

        private void BuildMemberJointPlates()
        {
            foreach (var jdef in _d.Joints.Where(j => j.Plates is { Count: > 0 } && j.Plates.Any(p => p.Member != null)))
                foreach (var pl in _d.Planes.Where(p => jdef.Plane.Length == 0 || p.Name.Equals(jdef.Plane, StringComparison.OrdinalIgnoreCase)))
                {
                    if (!_pts.TryGetValue((pl.Name.ToUpperInvariant(), jdef.Point.ToUpperInvariant()), out var P)) continue;
                    var here = At(pl.Name, jdef.Point).Where(i => i.Plane.Equals(pl.Name, StringComparison.OrdinalIgnoreCase)).Concat(Through(pl.Name, jdef.Point)).Distinct().ToList();
                    string asm = Asm($"TRUSS-{pl.Name}", AssemblyKind.TrussLine, $"{_d.Name} - truss line {pl.Name}");
                    // placed plates per (member, face): (a0, a1, b0, b1, top level) for the stacking
                    var placed = new List<(Inst m, TrussPlateFace f, double a0, double a1, double b0, double b1, double top)>();
                    int k = 0;
                    foreach (var p in jdef.Plates!.Where(p => p.Member != null).OrderBy(p => p.Fill ? 0 : 1))
                    {
                        k++;
                        var m = here.FirstOrDefault(i => i.Def.Name.Equals(p.Member, StringComparison.OrdinalIgnoreCase) || i.Mark.Equals(p.Member, StringComparison.OrdinalIgnoreCase));
                        if (m == null) { Warn($"Truss: plate '{p.Label}' at joint {jdef.Point} ({pl.Name}) - member '{p.Member}' does not frame into the joint; not built."); continue; }
                        if (!(p.Width > 0 && p.Thickness > 0 && p.Length > 0)) { Warn($"Truss: plate '{p.Label}' at joint {jdef.Point} ({pl.Name}) - width / thickness / length missing; not built."); continue; }
                        bool start = StartsAt(m, pl.Name, jdef.Point);
                        bool through = !start && !(m.PlaneB.Equals(pl.Name, StringComparison.OrdinalIgnoreCase) && m.PointB.Equals(jdef.Point, StringComparison.OrdinalIgnoreCase));
                        var ax = through ? m.Axis : Away(m, start);
                        double a0 = p.Offset - p.Length / 2, a1 = p.Offset + p.Length / 2;
                        // a plate on a member lies on the member: clipped to the member's solid along the axis (from the cut end at the joint)
                        double len = (m.B - m.A).Length;
                        double r0 = through ? Vec3.Dot(m.A + m.Axis * m.SA - P, ax) : (start ? m.SA : m.SB);
                        double r1 = through ? Vec3.Dot(m.B - m.Axis * m.SB - P, ax) : len - (start ? m.SB : m.SA);
                        if (a0 < r0 - 1e-6 || a1 > r1 + 1e-6)
                        {
                            a0 = Math.Max(a0, r0); a1 = Math.Min(a1, r1);
                            if (a1 - a0 < 1e-3) { Warn($"Truss: plate '{p.Label}' at joint {jdef.Point} ({pl.Name}) - it lies beyond the end of member {m.Mark}; not built."); continue; }
                        }
                        bool web = p.Face is TrussPlateFace.WebOutside or TrussPlateFace.WebInside;
                        double yc = (m.Sec.WebDepth > 0 ? m.Sec.WebCenterY : 0) + m.Def.OffsetZ;
                        double c0 = (web ? yc : 0) + p.CenterOffset;
                        double b0 = c0 - p.Width / 2, b1 = c0 + p.Width / 2;
                        int g = p.Face is TrussPlateFace.Top or TrussPlateFace.WebOutside ? 1 : -1;
                        // the member's pieces present along the plate (partial pieces / cover plates between the end gussets only where they run)
                        double cutLen = len - m.SA - m.SB, pos0 = Vec3.Dot(P - m.A, m.Axis) - m.SA, sgn = Vec3.Dot(ax, m.Axis) >= 0 ? 1 : -1;
                        double s0 = Math.Min(pos0 + sgn * a0, pos0 + sgn * a1), s1 = Math.Max(pos0 + sgn * a0, pos0 + sgn * a1);
                        bool Present(TrussSectionPiece pc) => PieceSpan(m, pc, cutLen) is { } sp && sp.ps < s1 - 1e-4 && sp.ps + sp.pl > s0 + 1e-4;
                        double? lv = FaceLevel(m.Sec, m.Def.OffsetZ, p.Face, b0, b1, Present);
                        if (lv == null) { Warn($"Truss: plate '{p.Label}' at joint {jdef.Point} ({pl.Name}) - member {m.Mark} has no {p.Face} face in the plate's band; not built."); continue; }
                        double baseLevel = lv.Value;
                        foreach (var q in placed)
                            if (q.m == m && q.f == p.Face && q.a1 > a0 + 1e-4 && q.a0 < a1 - 1e-4 && q.b1 > b0 + 1e-5 && q.b0 < b1 - 1e-5)
                                baseLevel = g > 0 ? Math.Max(baseLevel, q.top) : Math.Min(baseLevel, q.top);
                        double outer = baseLevel + g * p.Thickness;
                        placed.Add((m, p.Face, a0, a1, b0, b1, outer));
                        var mesh = new Mesh();
                        void Box(double x0, double x1, double y0, double y1)
                        {
                            var poly = new[] { new Vec2(Math.Min(x0, x1), Math.Min(y0, y1)), new Vec2(Math.Max(x0, x1), Math.Min(y0, y1)), new Vec2(Math.Max(x0, x1), Math.Max(y0, y1)), new Vec2(Math.Min(x0, x1), Math.Max(y0, y1)) };
                            var ra = poly.Select(q => P + ax * a0 + m.X * q.X + m.Y * q.Y).ToArray();
                            MeshBuilder.AddLoft(mesh, poly, new[] { ra, ra.Select(q => q + ax * (a1 - a0)).ToArray() });
                        }
                        if (web)
                        {
                            foreach (int sx in new[] { -1, 1 }) Box(sx * baseLevel, sx * outer, b0, b1);
                            var reg = (pl.Name.ToUpperInvariant(), jdef.Point.ToUpperInvariant(), P, ax, m.Y, a0, a1, b0, b1, p.Thickness);
                            if (p.Face == TrussPlateFace.WebOutside) _outsidePlates.Add(reg); else _insidePlates.Add(reg);
                        }
                        else
                        {
                            // Top / Bottom: in the member's section y; b0 / b1 across x
                            var poly = new[] { new Vec2(b0, Math.Min(baseLevel, outer)), new Vec2(b1, Math.Min(baseLevel, outer)), new Vec2(b1, Math.Max(baseLevel, outer)), new Vec2(b0, Math.Max(baseLevel, outer)) };
                            var ra = poly.Select(q => P + ax * a0 + m.X * q.X + m.Y * q.Y).ToArray();
                            MeshBuilder.AddLoft(mesh, poly, new[] { ra, ra.Select(q => q + ax * (a1 - a0)).ToArray() });
                        }
                        string mark = $"{pl.Name}-{(p.Fill ? "F" : "SP")}{jdef.Point}-M{k}";
                        Assumption? asu = p.Unresolved.Length > 0 ? Assumption.UnresolvedDimension(p.Unresolved, "plate as listed, extent scaled") : null;
                        if (asu != null) Warn(p.Unresolved);
                        Model.Steel.Parts.Add(new SteelPart
                        {
                            Mark = mark, Category = p.Fill ? PartCategory.FillerPlate : PartCategory.SplicePlate, Assembly = asm, Mesh = mesh, Material = Material(p.Material), Length = a1 - a0, Assumption = asu,
                            Description = $"{(p.Label.Length > 0 ? p.Label : (p.Fill ? "fill" : "splice") + " PL")} {TrussSections.FormatIn(p.Width)} x {TrussSections.FormatIn(p.Thickness)} x {TrussSections.FormatIn(a1 - a0)} ({p.Face}) on {m.Mark} at {jdef.Point}"
                                + (web ? " (pair)" : ""),
                        });
                        Model.PanelOf[mark] = $"Joint {jdef.Point}";
                        Model.SplicePlates++;
                    }
                }
        }
    }
}
