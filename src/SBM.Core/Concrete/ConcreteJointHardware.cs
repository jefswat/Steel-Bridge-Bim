using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
using SBM.Core.Units;

namespace SBM.Core.Concrete;

/// <summary>
/// Expansion joint hardware (OPTIONAL <see cref="ExpansionJointDefinition.Armor"/> / <see cref="ExpansionJointDefinition.CoverPlate"/>): armor plates with seal
/// retainer bars and stud anchors on both faces of the opening, a walkway cover plate with its anchors. Geometry in the joint frame of the deck end:
/// x = distance from the deck-end face (perpendicular to the joint line, + into the deck, - into the opening), drop = below the finished top of slab,
/// columns = alignment offsets. Parts are returned on the joint result and merged into the bridge's steel model by the regeneration.
/// </summary>
public sealed partial class ConcreteGenerator
{
    /// <summary>Section (R inward from the deck edge, H above the base) of a plate on every non-base face of the barrier polygon, its outer face
    /// <c>Depress</c> inside the concrete, thickness inward, cut at <c>BottomClear</c> above the base on both ends. Null = no exposed face.</summary>
    public static List<Vec2>? BarrierPlateSection(IReadOnlyList<Vec2> prof, JointBarrierPlateDefinition bp)
    {
        int n = prof.Count;
        int start = -1;
        for (int i = 0; i < n; i++) { var a = prof[i]; var b = prof[(i + 1) % n]; if (a.Y < 1e-9 && b.Y < 1e-9) start = (i + 1) % n; }
        if (start < 0) return null;
        var chain = new List<Vec2>();
        for (int k = 0; k < n; k++) { var p = prof[(start + k) % n]; chain.Add(p); if (k > 0 && p.Y < 1e-9) break; }
        if (chain.Count < 3) return null;
        double area = 0; for (int i = 0; i < n; i++) area += prof[i].X * prof[(i + 1) % n].Y - prof[(i + 1) % n].X * prof[i].Y;
        double sgn = area > 0 ? 1 : -1;   // CCW polygon: inward = left of each edge
        List<Vec2> Offset(double dist)
        {
            var ns = new List<Vec2>();
            for (int i = 0; i + 1 < chain.Count; i++) { var t = (chain[i + 1] - chain[i]).Normalized(); ns.Add(new Vec2(-t.Y, t.X) * sgn); }
            var o = new List<Vec2> { chain[0] + ns[0] * dist };
            for (int i = 1; i + 1 < chain.Count; i++)
            {
                var m = (ns[i - 1] + ns[i]).Normalized(); double c = Vec2.Dot(m, ns[i]);
                o.Add(chain[i] + m * (dist / Math.Max(0.2, c)));
            }
            o.Add(chain[^1] + ns[^1] * dist);
            return o;
        }
        var outer = Offset(bp.Depress); var inner = Offset(bp.Depress + bp.Thickness);
        Vec2 Cut(Vec2 a, Vec2 b, Vec2 keep) { if (Math.Abs(b.Y - a.Y) < 1e-12) return keep; double t = (bp.BottomClear - a.Y) / (b.Y - a.Y); return a + (b - a) * t; }
        outer[0] = Cut(outer[0], outer[1], outer[0]); inner[0] = Cut(inner[0], inner[1], inner[0]);
        outer[^1] = Cut(outer[^2], outer[^1], outer[^1]); inner[^1] = Cut(inner[^2], inner[^1], inner[^1]);
        var sec = new List<Vec2>(outer);
        for (int i = inner.Count - 1; i >= 0; i--) sec.Add(inner[i]);
        return sec;
    }

    private (SteelAssembly? asm, List<SteelPart> parts) BuildJointHardware(bool begin, SupportDefinition sup, ExpansionJointDefinition j)
    {
        var parts = new List<SteelPart>();
        if (!j.Body || (j.Armor == null && j.CoverPlate == null && j.BarrierPlate == null)) return (null, parts);
        string mark = "EJ-" + new string(sup.Name.Select(c => char.IsLetterOrDigit(c) ? char.ToUpperInvariant(c) : '-').ToArray()).Trim('-');
        string where = $"Expansion joint at {sup.Name}";
        double gap = j.GapAt50F;
        Vec3 JP(double o, double x, double drop)
        {
            double d = EndD(_ => o, begin, x);
            return _a.PointAtDistance(d, o).WithZ(Top(d, o) - drop);
        }
        // columns across the joint between two offsets (crown kept), and the plan length along the joint at x
        List<double> Cols(double o0, double o1)
        {
            int n = Math.Max(1, (int)Math.Ceiling((o1 - o0) / 0.3));
            var l = Enumerable.Range(0, n + 1).Select(i => o0 + (o1 - o0) * i / n).ToList();
            foreach (double piv in PivotColumns()) if (piv > o0 + 1e-4 && piv < o1 - 1e-4) l.Add(piv);
            return l.Distinct().OrderBy(v => v).ToList();
        }
        double PlanLen(IReadOnlyList<double> cols, double x)
        {
            double s = 0;
            for (int k = 1; k < cols.Count; k++) s += (JP(cols[k], x, 0) - JP(cols[k - 1], x, 0)).XY.Length;
            return s;
        }
        // offset at arc length s along the joint at x (linear between the columns)
        double OffsetAt(IReadOnlyList<double> cols, double x, double s)
        {
            double acc = 0;
            for (int k = 1; k < cols.Count; k++)
            {
                double l = (JP(cols[k], x, 0) - JP(cols[k - 1], x, 0)).XY.Length;
                if (acc + l >= s - 1e-12) return cols[k - 1] + (cols[k] - cols[k - 1]) * Math.Clamp((s - acc) / Math.Max(l, 1e-12), 0, 1);
                acc += l;
            }
            return cols[^1];
        }
        Mesh Loft(IReadOnlyList<double> cols, IReadOnlyList<Vec2> sec)   // sec = (x, drop)
        {
            var m = new Mesh();
            var s2 = sec.Select(q => new Vec2(q.X, -q.Y)).ToList();
            MeshBuilder.AddLoft(m, s2, cols.Select(o => sec.Select(q => JP(o, q.X, q.Y)).ToArray()).ToList());
            return m;
        }
        void Cyl(Mesh m, Vec3 a, Vec3 b, double r, int sides = 8)
        {
            var ax = b - a;
            if (ax.Length < 1e-9) return;
            var z = ax.Normalized();
            var x = Math.Abs(z.Z) < 0.9 ? Vec3.Cross(new Vec3(0, 0, 1), z).Normalized() : Vec3.Cross(new Vec3(1, 0, 0), z).Normalized();
            var y = Vec3.Cross(z, x);
            MeshBuilder.AddPrism(m, new Frame3(a, x, y, z), r / Math.Cos(Math.PI / sides), sides, 0, ax.Length);
        }
        string Sh(double v) => _u.FormatLength(v, LengthKind.Short);

        // ---------------------------------------------------------------- armor plates + retainer bars + studs
        if (j.Armor is { } ar)
        {
            double o0 = Deck.LeftEdgeOffset + ar.EdgeInsetLeft, o1 = Deck.RightEdgeOffset - ar.EdgeInsetRight;
            if (o1 - o0 < 0.05) Warn($"{where}: armor limits leave no length; armor not built.");
            else
            {
                var flag = ar.Unresolved is { Length: > 0 } aw ? Assumption.Unresolved(aw, ar.Basis ?? "as modeled") : null;
                if (ar.Unresolved is { Length: > 0 } aw2 && aw2 != j.Unresolved) Warn($"{where}: {aw2}");
                var cols = Cols(o0, o1);
                double t = ar.PlateThickness, dp = ar.PlateDepth;
                // near plate section (x, drop): joint face at x = 0, back at x = t; retainer bar on the joint face at the bottom
                var near = new List<Vec2> { new(0, 0), new(t, 0), new(t, dp), new(0, dp) };
                if (ar.BarProjection > 0 && ar.BarHeight > 0)
                {
                    double b0 = dp - ar.BarAboveBottom, b1 = b0 - ar.BarHeight;
                    near.AddRange(new Vec2[] { new(0, b0), new(-ar.BarProjection, b0), new(-ar.BarProjection, b1), new(0, b1) });
                }
                var sides = new List<(string tag, Func<double, double> X, double sign)> { ("N", x => x, 1) };
                if (ar.FarPlate && gap > 1e-6) sides.Add(("F", x => -gap - x, -1));
                string std = ar.Standard.Length > 0 ? $" ({ar.Standard})" : "";
                foreach (var (tag, X, sign) in sides)
                {
                    var sec = near.Select(q => new Vec2(X(q.X), q.Y)).ToList();
                    double len = PlanLen(cols, X(0));
                    parts.Add(new SteelPart
                    {
                        Mark = $"{mark}-A{tag}", Category = PartCategory.JointArmor, Assembly = mark, Mesh = Loft(cols, sec), Length = len, Material = ar.Material,
                        Description = $"Armor PL {Sh(t)} x {Sh(dp)}" + (ar.BarProjection > 0 ? $" + bar {Sh(ar.BarHeight)} x {Sh(ar.BarProjection)}" : "") + $", {(tag == "N" ? "deck" : "far")} side{std}",
                        Assumption = flag,
                    });
                    // studs: first / last StudEndDistance from the plate ends, equally spaced at most StudSpacing
                    if (ar.StudDiameter > 0 && ar.StudLength > 0 && ar.StudSpacing > 0)
                    {
                        double inner = len - 2 * ar.StudEndDistance;
                        int n = inner <= 1e-6 ? 1 : (int)Math.Ceiling(inner / ar.StudSpacing - 1e-9) + 1;
                        for (int i = 0; i < n; i++)
                        {
                            double s = n == 1 ? len / 2 : ar.StudEndDistance + inner * i / (n - 1);
                            double o = OffsetAt(cols, X(0), s);
                            var a = JP(o, X(t), ar.StudDrop);
                            // horizontal, perpendicular to the joint line in plan (the plate's normal), away from the opening
                            var ld = JP(o + 0.05, X(t), ar.StudDrop) - JP(o - 0.05, X(t), ar.StudDrop);
                            var dirH = new Vec3(-ld.Y, ld.X, 0).Normalized();
                            var away = JP(o, X(t + 0.1), ar.StudDrop) - a;
                            if (dirH.X * away.X + dirH.Y * away.Y < 0) dirH = dirH * -1;
                            var b = a + dirH * (ar.StudLength - (ar.StudBentLength > 0 ? 0 : ar.StudHeadHeight));
                            var m = new Mesh();
                            var tip = b; var tdir = dirH;
                            if (ar.StudBentLength > 0)
                            {
                                double ang = ar.StudBendDegrees * Math.PI / 180;
                                tdir = (dirH * Math.Cos(ang) + new Vec3(0, 0, -Math.Sin(ang))).Normalized();
                                tip = b + tdir * (ar.StudBentLength - ar.StudHeadHeight);
                                // ONE closed shank: octagon swept start -> mitred bend -> tip (separate overlapping prisms do not mesh as closed shells)
                                var sx = Vec3.Cross(new Vec3(0, 0, 1), dirH).Normalized();
                                double rc = ar.StudDiameter / 2 / Math.Cos(Math.PI / 8);
                                var oct = Enumerable.Range(0, 8).Select(k => Vec2.FromAngle(2 * Math.PI * k / 8) * rc).ToList();
                                Vec3 Off(Vec3 z, Vec2 q) { var y = Vec3.Cross(z, sx); return sx * q.X + y * q.Y; }
                                var nb = (dirH + tdir).Normalized();
                                var r0 = oct.Select(q => a + Off(dirH, q)).ToArray();
                                var r1 = oct.Select(q => { var v = Off(dirH, q); double tt = -Vec3.Dot(v, nb) / Vec3.Dot(dirH, nb); return b + v + dirH * tt; }).ToArray();
                                var r2 = oct.Select(q => tip + Off(tdir, q)).ToArray();
                                MeshBuilder.AddLoft(m, oct, new[] { r0, r1, r2 });
                            }
                            else Cyl(m, a, b, ar.StudDiameter / 2);
                            Cyl(m, tip, tip + tdir * ar.StudHeadHeight, ar.StudHeadDiameter / 2);
                            parts.Add(new SteelPart
                            {
                                Mark = $"{mark}-S{tag}{i + 1}", Category = PartCategory.JointStud, Assembly = mark, Mesh = m, Length = ar.StudLength + ar.StudBentLength,
                                Material = "ASTM A108 (AWS D1.1 stud)", Description = $"Stud anchor {Sh(ar.StudDiameter)} dia{std}", Assumption = flag,
                            });
                        }
                    }
                }
            }
        }

        // ---------------------------------------------------------------- cover plate + anchors
        if (j.CoverPlate is { } cp)
        {
            double o0 = Deck.LeftEdgeOffset + cp.EdgeInsetLeft, o1 = Deck.RightEdgeOffset - cp.EdgeInsetRight;
            double xc = -gap / 2, hw = cp.Width / 2;
            var cols0 = Cols(o0, o1);
            double full = PlanLen(cols0, xc);
            double len = full - 2 * cp.EndClearance;
            if (len < 0.05 || cp.Thickness <= 0 || cp.Width <= 0) Warn($"{where}: cover plate limits leave no length; cover plate not built.");
            else
            {
                var flag = cp.Unresolved is { Length: > 0 } cw ? Assumption.Unresolved(cw, cp.Basis ?? "as modeled") : null;
                if (cp.Unresolved is { Length: > 0 } cw2 && cw2 != j.Unresolved) Warn($"{where}: {cw2}");
                double c0 = OffsetAt(cols0, xc, cp.EndClearance), c1 = OffsetAt(cols0, xc, full - cp.EndClearance);
                var cols = Cols(c0, c1);
                double t = cp.Thickness, cr = cp.Crown;
                var sec = new List<Vec2> { new(xc - hw, 0), new(xc, -cr), new(xc + hw, 0), new(xc + hw, -t), new(xc, -cr - t), new(xc - hw, -t) };
                string std = cp.Standard.Length > 0 ? $" ({cp.Standard})" : "";
                parts.Add(new SteelPart
                {
                    Mark = $"{mark}-CP", Category = PartCategory.JointCoverPlate, Assembly = mark, Mesh = Loft(cols, sec), Length = PlanLen(cols, xc), Material = cp.Material,
                    Description = $"Cover PL {Sh(t)} x {Sh(cp.Width)}{std}", Assumption = flag,
                });
                if (cp.AnchorDiameter > 0 && cp.AnchorLength > 0 && cp.AnchorSpacing > 0 && cp.AnchorRows.Count > 0)
                {
                    double plen = PlanLen(cols, xc);
                    double inner = plen - 2 * cp.AnchorEndDistance;
                    int n = inner <= 1e-6 ? 1 : (int)Math.Ceiling(inner / cp.AnchorSpacing - 1e-9) + 1;
                    for (int i = 0; i < n; i++)
                    {
                        double s = n == 1 ? plen / 2 : cp.AnchorEndDistance + inner * i / (n - 1);
                        double o = OffsetAt(cols, xc, s);
                        double x = xc + hw - cp.AnchorRows[i % cp.AnchorRows.Count];
                        double rise = cr * Math.Max(0, 1 - Math.Abs(x - xc) / hw);       // plate bottom above the top of slab at x
                        var top = JP(o, x, -rise - t);
                        var bot = JP(o, x, cp.AnchorLength - rise);
                        var m = new Mesh();
                        Cyl(m, bot, top, cp.AnchorDiameter / 2);
                        parts.Add(new SteelPart
                        {
                            Mark = $"{mark}-CA{i + 1}", Category = PartCategory.JointAnchor, Assembly = mark, Mesh = m, Length = (top - bot).Length,
                            Material = "Stainless steel flat head sleeve anchor", Description = $"Sleeve anchor {Sh(cp.AnchorDiameter)} x {Sh(cp.AnchorLength)}{std}", Assumption = flag,
                        });
                    }
                }
            }
        }
        // ---------------------------------------------------------------- barrier cover (slider) plates
        if (j.BarrierPlate is { } bp)
        {
            var prof = BarrierProfiles.For(Deck.Barrier);
            if (prof is not { Length: >= 3 }) Warn($"{where}: barrier cover plate - the bridge has no barrier section; not built.");
            else if (bp.LengthIn + bp.LengthOut < 0.01) Warn($"{where}: barrier cover plate length not given; not built.");
            else
            {
                var flag = bp.Unresolved is { Length: > 0 } bw ? Assumption.UnresolvedDimension(bw, bp.Basis ?? "plate bent to the barrier section") : null;
                if (bp.Unresolved is { Length: > 0 } bw2 && bw2 != j.Unresolved) Warn($"{where}: {bw2}");
                var sec = BarrierPlateSection(prof, bp);
                if (sec == null) Warn($"{where}: barrier cover plate - the barrier section has no exposed faces above the base clearance; not built.");
                else
                    foreach (var (side, on) in new[] { (-1, bp.Left && Deck.Barrier.Left), (1, bp.Right && Deck.Barrier.Right) })
                    {
                        if (!on) continue;
                        double lIn = side < 0 ? bp.LengthIn : bp.RightLengthIn ?? bp.LengthIn, lOut = side < 0 ? bp.LengthOut : bp.RightLengthOut ?? bp.LengthOut;
                        if (lIn + lOut < 0.01) continue;
                        var rings = new List<Vec3[]>();
                        int nx = Math.Max(2, (int)Math.Ceiling((lIn + lOut) / 0.3) + 1);
                        for (int i = 0; i < nx; i++)
                        {
                            double x = -lOut + (lIn + lOut) * i / (nx - 1);
                            double e0 = side < 0 ? Deck.LeftEdgeOffset : Deck.RightEdgeOffset;
                            double d = EndD(_ => e0, begin, x);
                            double edge = side < 0 ? LeftAtD(d) : RightAtD(d);
                            d = EndD(_ => edge, begin, x);
                            rings.Add(sec.Select(q => _a.PointAtDistance(d, edge - side * q.X).WithZ(Top(d, edge) + q.Y)).ToArray());
                        }
                        var m = new Mesh();
                        MeshBuilder.AddLoft(m, sec, rings);
                        parts.Add(new SteelPart
                        {
                            Mark = $"{mark}-BP{(side < 0 ? "L" : "R")}", Category = PartCategory.JointCoverPlate, Assembly = mark, Mesh = m, Length = lIn + lOut, Material = bp.Material,
                            Description = $"Barrier cover (slider) PL {Sh(bp.Thickness)} bent to the barrier, {Sh(lIn + lOut)} long ({Sh(lOut)} beyond the deck end){(bp.Source.Length > 0 ? $" ({bp.Source})" : "")}",
                            Assumption = flag,
                        });
                    }
            }
        }
        if (parts.Count == 0) return (null, parts);
        var asm = new SteelAssembly
        {
            Mark = mark, Kind = AssemblyKind.ExpansionJoint, ObjectType = j.Designation,
            Description = $"{j.Designation ?? j.TypeName + " expansion joint"} hardware at {sup.Name}",
        };
        return (asm, parts);
    }
}
