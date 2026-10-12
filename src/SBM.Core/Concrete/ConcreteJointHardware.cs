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
    private (SteelAssembly? asm, List<SteelPart> parts) BuildJointHardware(bool begin, SupportDefinition sup, ExpansionJointDefinition j)
    {
        var parts = new List<SteelPart>();
        if (!j.Body || (j.Armor == null && j.CoverPlate == null)) return (null, parts);
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
                if (ar.Unresolved is { Length: > 0 } aw2) Warn($"{where}: {aw2}");
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
                            var dirH = (JP(o, X(t + 0.1), ar.StudDrop) - a);
                            dirH = new Vec3(dirH.X, dirH.Y, 0).Normalized();
                            var b = a + dirH * (ar.StudLength - (ar.StudBentLength > 0 ? 0 : ar.StudHeadHeight));
                            var m = new Mesh();
                            Cyl(m, a, b, ar.StudDiameter / 2);
                            var tip = b; var tdir = dirH;
                            if (ar.StudBentLength > 0)
                            {
                                double ang = ar.StudBendDegrees * Math.PI / 180;
                                tdir = (dirH * Math.Cos(ang) + new Vec3(0, 0, -Math.Sin(ang))).Normalized();
                                tip = b + tdir * (ar.StudBentLength - ar.StudHeadHeight);
                                Cyl(m, b, tip, ar.StudDiameter / 2);
                            }
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
                if (cp.Unresolved is { Length: > 0 } cw2) Warn($"{where}: {cw2}");
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
        if (parts.Count == 0) return (null, parts);
        var asm = new SteelAssembly
        {
            Mark = mark, Kind = AssemblyKind.ExpansionJoint, ObjectType = j.Designation,
            Description = $"{j.Designation ?? j.TypeName + " expansion joint"} hardware at {sup.Name}",
        };
        return (asm, parts);
    }
}
