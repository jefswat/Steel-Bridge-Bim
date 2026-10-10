using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Substructure;

/// <summary>
/// Substructure reinforcing. Caps: top and bottom layers following the cap profile, side-face skin bars, closed
/// stirrups with 135° hooks. Columns and shafts: vertical bars on a circle (or around a rectangle) with a spiral (or
/// ties); column bars lap into the shaft or stop above the footing mat. Footings: top and bottom mats. Backwalls: both
/// faces each way. Bars over the maximum length are lap spliced (AASHTO Class B). Marks are per support (unit).
/// </summary>
public sealed partial class SubstructureGenerator
{
    private sealed record RawBar(string Series, RebarSize Size, BarShape Shape, string Location, Vec3[] Points);

    private void BuildRebar(SubstructureModel m)
    {
        var perUnit = new List<(SubstructureUnit unit, List<RawBar> bars)>();
        foreach (var unit in m.Units)
        {
            var r = unit.Definition.Reinforcement;
            if (!r.Enabled) continue;
            var bars = new List<RawBar>();
            CapBars(unit, bars);
            if (unit.Backwall != null) BackwallBars(unit, bars);
            foreach (var c in unit.Columns) ColumnBars(unit, c, bars);
            foreach (var f in unit.Foundations)
            {
                if (f.Type == FoundationType.DrilledShaft) ShaftBars(unit, f, bars);
                else if (f.Type is FoundationType.SpreadFooting or FoundationType.PileCap && f.SizeS > 0) FootingBars(unit, f, bars);
            }
            perUnit.Add((unit, bars));
        }
        foreach (var (unit, bars) in perUnit) AssignMarks(m, unit, bars);
    }

    private double Lap(RebarSize size, SubstructureReinforcementDefinition r, double cover, double spacing) =>
        RebarCatalog.ClassBLap(size, r.ConcreteStrengthKsi, r.SteelYieldKsi, r.EpoxyCoated, cover, spacing);

    /// <summary>Adds a bar along a polyline, splitting it into pieces no longer than the maximum with lap splices.</summary>
    private void AddRun(List<RawBar> bars, string series, RebarSize size, string loc, IReadOnlyList<Vec3> pts, double maxLen, double lap)
    {
        double total = 0;
        for (int i = 1; i < pts.Count; i++) total += (pts[i] - pts[i - 1]).Length;
        int pieces = total <= maxLen ? 1 : (int)Math.Ceiling((total - lap) / (maxLen - lap));
        double piece = pieces == 1 ? total : (total + (pieces - 1) * lap) / pieces;
        for (int k = 0; k < pieces; k++)
        {
            double a = k * (piece - lap), b = Math.Min(total, a + piece);
            var seg = SubPolyline(pts, a, b);
            bars.Add(new RawBar(series, size, ShapeFor(seg), loc, seg));
        }
    }

    private static Vec3[] SubPolyline(IReadOnlyList<Vec3> pts, double a, double b)
    {
        var res = new List<Vec3>();
        double acc = 0;
        for (int i = 1; i < pts.Count; i++)
        {
            double len = (pts[i] - pts[i - 1]).Length;
            double s0 = acc, s1 = acc + len;
            if (s1 >= a && s0 <= b && len > 1e-12)
            {
                if (res.Count == 0) res.Add(Vec3.Lerp(pts[i - 1], pts[i], Math.Clamp((a - s0) / len, 0, 1)));
                if (s1 < b) res.Add(pts[i]);
                else { res.Add(Vec3.Lerp(pts[i - 1], pts[i], Math.Clamp((b - s0) / len, 0, 1))); break; }
            }
            acc = s1;
        }
        return res.ToArray();
    }

    /// <summary>Straight bar, or a bent bar (legs A, B, … in its own plane) when the polyline has kinks.</summary>
    private static BarShape ShapeFor(Vec3[] pts)
    {
        double len = 0;
        for (int i = 1; i < pts.Length; i++) len += (pts[i] - pts[i - 1]).Length;
        if (pts.Length == 2 || IsStraight(pts)) return BarShape.Straight(len);
        // Unfold into 2D: x along the chord in plan, y = elevation.
        var dir = (pts[^1].XY - pts[0].XY).Normalized();
        var cl = pts.Select(p => new Vec2(Vec2.Dot(p.XY - pts[0].XY, dir), p.Z - pts[0].Z)).ToArray();
        var shape = new BarShape { Code = "BENT", Description = "Bent at slope change", Centerline = cl };
        for (int i = 1; i < cl.Length; i++)
        {
            var n = (cl[i] - cl[i - 1]).Normalized().Left;
            shape.Dimensions.Add(new BarDimension(((char)('A' + i - 1)).ToString(), Vec2.Distance(cl[i - 1], cl[i]), cl[i - 1], cl[i], n));
        }
        return shape;
    }

    private static bool IsStraight(Vec3[] pts)
    {
        var d = (pts[^1] - pts[0]).Normalized();
        for (int i = 1; i < pts.Length - 1; i++)
        {
            var v = pts[i] - pts[0];
            var perp = v - d * Vec3.Dot(v, d);
            if (perp.Length > In(0.25)) return false;
        }
        return true;
    }

    // ------------------------------------------------------------------ caps

    private void CapBars(SubstructureUnit unit, List<RawBar> bars)
    {
        var sd = unit.Definition;
        var r = sd.Reinforcement;
        double cov = r.CapCover;
        var st = RebarCatalog.Size(r.CapStirrupSize);
        var top = RebarCatalog.Size(r.CapTopBarSize);
        var bot = RebarCatalog.Size(r.CapBottomBarSize);
        var side = RebarCatalog.Size(r.CapSideBarSize);
        double s0 = unit.CapS0 + cov, s1 = unit.CapS1 - cov;
        double n0 = unit.CapN0 + cov + st.Diameter, n1 = unit.CapN1 - cov - st.Diameter;
        var ss = unit.CapTop.Select(p => p.X).Concat(unit.CapBottom.Select(p => p.X)).Append(s0).Append(s1)
            .Where(x => x >= s0 - 1e-9 && x <= s1 + 1e-9).Distinct().OrderBy(x => x).ToList();
        string loc = $"{unit.Name} cap";
        // The top line of the cap: the top envelope without steep end faces (raked / beak ends of as-designed caps), so the
        // top bars run straight along the top and stop at the end faces instead of following the faces down to the tips.
        var topLine = unit.CapTopLine();
        double TopAt(double s) => SubstructureUnit.Interp(topLine, s);
        // Longest stretch (around the cap middle) where a bar at z(s) keeps the cover to the concrete above and below,
        // inset by the cover at its ends; the bar is the samples inside it plus its two ends.
        List<double> Within(Func<double, double> z, double halfDia)
        {
            bool Ok(double s) => z(s) + halfDia + cov <= unit.CapTopAt(s) + 1e-9 && z(s) - halfDia - cov >= unit.CapBottomAt(s) - 1e-9;
            double mid = (s0 + s1) / 2;
            if (!Ok(mid)) return new();
            double Edge(double outer)
            {
                double good = mid, step = (outer - mid) / 200;
                double x = mid;
                for (int k = 1; k <= 200; k++, x += step) { if (!Ok(mid + step * k)) break; good = mid + step * k; }
                double bad = Math.Abs(good - outer) < 1e-9 ? outer : good + step;
                for (int k = 0; k < 40 && Math.Abs(bad - good) > 1e-4; k++) { double c = (good + bad) / 2; if (Ok(c)) good = c; else bad = c; }
                return good;
            }
            double lo = Edge(s0), hi = Edge(s1);
            // Ends that stop inside the concrete (not at the cap end) keep the cover horizontally too.
            if (lo > s0 + 1e-6) lo += cov;
            if (hi < s1 - 1e-6) hi -= cov;
            if (hi - lo < In(12)) return new();
            return ss.Where(x => x > lo + 1e-6 && x < hi - 1e-6).Prepend(lo).Append(hi).ToList();
        }

        void Layer(string series, RebarSize size, int count, Func<double, double> z, string what)
        {
            if (count < 1) return;
            double inner = n1 - n0 - size.Diameter;
            double spacing = count > 1 ? inner / (count - 1) : inner;
            double lap = Lap(size, r, cov, spacing);
            var run = Within(z, size.Diameter / 2);
            if (run.Count < 2) return;
            for (int i = 0; i < count; i++)
            {
                double n = count == 1 ? (n0 + n1) / 2 : n0 + size.Diameter / 2 + inner * i / (count - 1);
                var pts = run.Select(s => unit.World(s, n, z(s))).ToList();
                AddRun(bars, series, size, $"{loc} {what}", pts, r.MaxBarLength, lap);
            }
        }
        Layer("A", top, r.CapTopBarCount, s => TopAt(s) - cov - st.Diameter - top.Diameter / 2, "top");
        Layer("B", bot, r.CapBottomBarCount, s => unit.CapBottomAt(s) + cov + st.Diameter + bot.Diameter / 2, "bottom");
        // Side-face skin bars at equal spaces between the layers (between the bottom and the top line).
        for (int k = 1; k <= r.CapSideBarsPerFace; k++)
        {
            double f = k / (r.CapSideBarsPerFace + 1.0);
            Func<double, double> zs = s => unit.CapBottomAt(s) + (TopAt(s) - unit.CapBottomAt(s)) * f;
            var run = Within(zs, side.Diameter / 2);
            if (run.Count < 2) continue;
            foreach (double n in new[] { n0 + side.Diameter / 2, n1 - side.Diameter / 2 })
            {
                var pts = run.Select(s => unit.World(s, n, zs(s))).ToList();
                AddRun(bars, "C", side, $"{loc} side faces", pts, r.MaxBarLength, Lap(side, r, cov, In(12)));
            }
        }
        // Closed stirrups at equal spaces not exceeding the maximum (none where the section is too shallow for one).
        double len = unit.CapS1 - unit.CapS0 - 2 * cov - In(2);
        int nSt = (int)Math.Ceiling(len / r.CapStirrupSpacing - 1e-9) + 1;
        double w = unit.CapN1 - unit.CapN0 - 2 * cov;
        for (int i = 0; i < nSt; i++)
        {
            double s = unit.CapS0 + cov + In(1) + len * i / Math.Max(1, nSt - 1);
            double zt = unit.CapTopAt(s) - cov, zb = unit.CapBottomAt(s) + cov;
            if (zt - zb < In(12)) continue;
            var (shape, local) = ClosedStirrup(w, zt - zb, st);
            var pts = local.Select(p => unit.World(s, unit.CapN0 + cov + p.X, zb + p.Y)).ToArray();
            bars.Add(new RawBar("S", st, shape, $"{loc} stirrups", pts));
        }
    }

    /// <summary>
    /// Closed rectangular stirrup / tie, out-to-out w × h (AASHTO 5.10.2): bent around a pin of 4 db (#5 and smaller) or
    /// 6 db (#6–#8) at every corner, both ends finished with 135° hooks at the top-left corner, each with a straight
    /// extension of 6 db (3" min) into the core. Centerline in the (w, h) plane, origin at the outside bottom-left.
    /// </summary>
    private static (BarShape shape, Vec2[] local) ClosedStirrup(double w, double h, RebarSize size)
    {
        double db = size.Diameter;
        double pin = (size.Name is "#6" or "#7" or "#8" ? 6 : 4) * db;
        double rc = pin / 2 + db / 2;                          // centerline bend radius
        double x0 = db / 2, x1 = w - db / 2, y0 = db / 2, y1 = h - db / 2;
        double ext = Math.Max(6 * db, In(3));
        var dIn = new Vec2(1, -1).Normalized();               // hook tails point into the core
        // Bend centers (inside the corners); every corner, including the hook corner, bends around the same pin.
        var cTL = new Vec2(x0 + rc, y1 - rc);
        var cTR = new Vec2(x1 - rc, y1 - rc);
        var cBR = new Vec2(x1 - rc, y0 + rc);
        var cBL = new Vec2(x0 + rc, y0 + rc);
        var pts = new List<Vec2>();
        void Arc(Vec2 c, double a0, double a1)
        {
            int n = Math.Max(3, (int)Math.Ceiling(Math.Abs(a1 - a0) / (Math.PI / 12)));
            for (int k = 0; k <= n; k++) pts.Add(c + Vec2.FromAngle(a0 + (a1 - a0) * k / n) * rc);
        }
        double deg = Math.PI / 180;
        // Hook 1 (top leg end): tail → 135° around the corner bar (from 225° back to 90°) → along the top.
        var h1 = cTL + Vec2.FromAngle(225 * deg) * rc;
        pts.Add(h1 + dIn * ext);
        Arc(cTL, 225 * deg, 90 * deg);
        Arc(cTR, 90 * deg, 0);
        Arc(cBR, 0, -90 * deg);
        Arc(cBL, -90 * deg, -180 * deg);
        // Hook 2 (left leg end): up the left side → 135° around the corner bar (180° to 45°) → tail into the core.
        Arc(cTL, 180 * deg, 45 * deg);
        var h2 = cTL + Vec2.FromAngle(45 * deg) * rc;
        pts.Add(h2 + dIn * ext);
        var bends = new List<(Vec2, double)> { (cTL, rc), (cTR, rc), (cBR, rc), (cBL, rc) };
        var shape = new BarShape
        {
            Code = "TIE", Description = $"Closed stirrup / tie, 135° hooks, {new SBM.Core.Units.UnitSystem().FormatLength(ext, SBM.Core.Units.LengthKind.Short)} ext.", Centerline = pts.ToArray(), PinDiameter = pin,
            Dimensions =
            {
                new BarDimension("A", w, new Vec2(0, 0), new Vec2(w, 0), new Vec2(0, -1)),
                new BarDimension("B", h, new Vec2(w, 0), new Vec2(w, h), new Vec2(1, 0)),
            },
        };
        shape.Bends.AddRange(bends);
        return (shape, pts.ToArray());
    }

    private void BackwallBars(SubstructureUnit unit, List<RawBar> bars)
    {
        var sd = unit.Definition;
        var r = sd.Reinforcement;
        var (bn0, bn1, top) = unit.Backwall!.Value;
        var size = RebarCatalog.Size(r.BackwallBarSize);
        double cov = r.CapCover, sp = r.BackwallBarSpacing;
        double s0 = unit.CapS0 + cov, s1 = unit.CapS1 - cov;
        string loc = $"{unit.Name} backwall";
        double embed = Math.Min(sd.CapDepth - r.CapCover - In(3), 12 * size.Diameter + In(6));
        foreach (double n in new[] { bn0 + cov + size.Diameter / 2, bn1 - cov - size.Diameter / 2 })
        {
            int nv = (int)Math.Ceiling((s1 - s0) / sp - 1e-9) + 1;
            for (int i = 0; i < nv; i++)
            {
                double s = s0 + (s1 - s0) * i / Math.Max(1, nv - 1);
                double zt = SubstructureUnit.Interp(top, s) - cov, zb = unit.CapTopAt(s) - embed;
                bars.Add(new RawBar("W", size, BarShape.Straight(zt - zb), $"{loc} verticals", new[] { unit.World(s, n, zb), unit.World(s, n, zt) }));
            }
            double hMin = top.Zip(top.Select(p => unit.CapTopAt(p.X))).Min(p => p.First.Y - p.Second);
            int nh = Math.Max(1, (int)Math.Floor((hMin - cov - In(3)) / sp) + 1);
            for (int k = 0; k < nh; k++)
            {
                double dz = In(3) + k * sp;
                var pts = new[] { s0, s1 }.Select(s => unit.World(s, n, unit.CapTopAt(s) + dz)).ToList();
                AddRun(bars, "H", size, $"{loc} horizontals", pts, r.MaxBarLength, Lap(size, r, cov, sp));
            }
        }
    }

    // ------------------------------------------------------------------ columns, shafts, footings

    private void ColumnBars(SubstructureUnit unit, ColumnInfo c, List<RawBar> bars)
    {
        var sd = unit.Definition;
        var r = sd.Reinforcement;
        var v = RebarCatalog.Size(r.ColumnBarSize);
        var sp = RebarCatalog.Size(r.ColumnSpiralSize);
        double cov = r.ColumnCover;
        var f = unit.Foundations.FirstOrDefault(x => Math.Abs(x.S - c.S) < 1e-6);
        double zTop = unit.CapTopAt(c.S) - r.CapCover - In(3);
        double zBot = f?.Type switch
        {
            FoundationType.DrilledShaft => c.BottomZ - Lap(v, r, cov, In(6)),
            FoundationType.SpreadFooting or FoundationType.PileCap => f.BottomZ + r.FootingCover + 2 * RebarCatalog.Size(r.FootingBarSize).Diameter + v.Diameter / 2,
            _ => c.BottomZ + In(3),
        };
        string loc = $"{unit.Name} column {c.Index + 1}";
        if (c.Shape == ColumnShape.Round)
        {
            foreach (var p in PerimeterBars(c.Shape, c.SizeS, c.SizeN, cov + sp.Diameter + v.Diameter / 2, r.ColumnBarCount))
                AddRun(bars, "V", v, $"{loc} verticals", new[] { unit.World(c.S + p.X, c.N + p.Y, zBot), unit.World(c.S + p.X, c.N + p.Y, zTop) },
                    r.MaxBarLength, Lap(v, r, cov, In(6)));
            Confinement(unit, bars, c.Shape, c.S, c.N, c.SizeS, c.SizeN, cov, sp, r.ColumnSpiralPitch, c.BottomZ + In(3), c.TopZ - In(1), "P", $"{loc} spiral");
            return;
        }

        // Rectangular / chamfered (octagonal) column: bars on the faces (dimensioned along the outside face, set in by
        // cover + tie + db/2, so they follow the chamfers); perimeter ties following the section (one closed tie, or two
        // overlapping hoops each around one end); optional crossties between the middle chamfer bars on each side.
        var sec = Octagon(c.SizeS, c.SizeN, sd.ColumnChamferS, sd.ColumnChamferN);
        double inset = cov + sp.Diameter + v.Diameter / 2;
        var vb = FaceBars(sec, inset, r.ColumnFaceBarsS, r.ColumnFaceSpacingS, r.ColumnFaceBarsN, r.ColumnFaceSpacingN,
            r.ColumnChamferBars, r.ColumnChamferSpacing, r.ColumnChamferBarPositions, r.ColumnBarCount, $"{loc} verticals");
        if (r.ColumnBarEmbedInCap > 0) zTop = c.TopZ + r.ColumnBarEmbedInCap;
        if (r.ColumnSplice != ColumnSpliceType.Continuous && f is { Type: FoundationType.SpreadFooting or FoundationType.PileCap })
            SplicedVerticals(unit, c, f, vb, v, InsetConvex(sec, inset), zTop, loc, bars);
        else
            foreach (var p in vb)
                AddRun(bars, "V", v, $"{loc} verticals", new[] { unit.World(c.S + p.X, c.N + p.Y, zBot), unit.World(c.S + p.X, c.N + p.Y, zTop) },
                    r.MaxBarLength, Lap(v, r, cov, In(6)));
        double z0 = c.BottomZ + In(3), z1 = c.TopZ - In(3);
        var tieLine = InsetConvex(sec, cov + sp.Diameter / 2);
        double dz = 0;
        if (r.ColumnHoopOverlap > 0 && r.ColumnHoopOverlap < c.SizeS)
        {
            // Two closed hoops per level, each around one end, overlapping in the middle (stacked one tie diameter apart).
            // The overlap is dimensioned on the outside face; a leg maps onto the tie line the way the S-face corner does
            // (an overlap equal to the S face puts the legs exactly through the tie line's S-face corners).
            double shift = sd.ColumnChamferS > 0 ? tieLine[1].X - sec[1].X : 0;
            double h = r.ColumnHoopOverlap / 2 + shift;
            SectionTies(unit, bars, c.S, c.N, ClipX(tieLine, -h, true), sp, r.ColumnSpiralPitch, z0, z1, 0, "P", $"{loc} hoops");
            SectionTies(unit, bars, c.S, c.N, ClipX(tieLine, h, false), sp, r.ColumnSpiralPitch, z0, z1, sp.Diameter, "P", $"{loc} hoops");
            dz = 2 * sp.Diameter;
        }
        else
        {
            SectionTies(unit, bars, c.S, c.N, tieLine, sp, r.ColumnSpiralPitch, z0, z1, 0, "P", $"{loc} ties");
            dz = sp.Diameter;
        }
        if (r.ColumnCrosstieSize != "" && sd.ColumnChamferS > 0 && sd.ColumnChamferN > 0)
        {
            var ct = RebarCatalog.Size(r.ColumnCrosstieSize);
            // Chamfer bars of the upper chamfers (N > 0, |S| beyond the S face), nearest each chamfer's middle.
            double halfFace = c.SizeS / 2 - sd.ColumnChamferS;
            foreach (int side in new[] { -1, 1 })
            {
                var mid = new Vec2(side * (c.SizeS / 2 - sd.ColumnChamferS / 2), c.SizeN / 2 - sd.ColumnChamferN / 2);
                // Chamfer bars only: beyond the S face along S and above the top of the N face.
                var cand = vb.Where(q => q.Y > c.SizeN / 2 - sd.ColumnChamferN - In(0.5) && side * q.X > halfFace + In(0.5)
                    && side * q.X < c.SizeS / 2 - inset - In(0.5)).ToList();
                if (cand.Count == 0) continue;
                var top = r.ColumnCrosstieBar > 0
                    ? cand.OrderBy(q => side * q.X).ElementAt(Math.Min(r.ColumnCrosstieBar, cand.Count) - 1)
                    : cand.OrderBy(q => Vec2.Distance(q, mid)).First();
                var bot = vb.Where(q => q.Y < 0).OrderBy(q => Vec2.Distance(q, new Vec2(top.X, -top.Y))).First();
                var (sh, local) = Crosstie(top, bot, -side, v.Diameter, ct);
                int nt = (int)Math.Ceiling((z1 - z0) / r.ColumnSpiralPitch - 1e-9) + 1;
                for (int i = 0; i < nt; i++)
                {
                    double z = z0 + dz + (z1 - z0) * i / Math.Max(1, nt - 1);
                    bars.Add(new RawBar("P", ct, sh, $"{loc} crossties", local.Select(q => unit.World(c.S + q.X, c.N + q.Y, z)).ToArray()));
                }
            }
        }

        // Flared base: its own cage around the larger section, from the foundation to below the flare top.
        if (!double.IsNaN(sd.ColumnFlareElevation) && sd.ColumnFlareElevation > c.BottomZ && sd.ColumnFlareElevation < c.TopZ
            && r.FlareBarSize != "" && r.FlareFaceBarsS + r.FlareFaceBarsN + r.FlareChamferBars > 0)
        {
            var fv = RebarCatalog.Size(r.FlareBarSize);
            var ft = RebarCatalog.Size(r.FlareTieSize);
            var fsec = Octagon(sd.ColumnFlareWidth, sd.ColumnFlareDepth, sd.ColumnFlareChamferS, sd.ColumnFlareChamferN);
            double fTopBar = sd.ColumnFlareElevation - r.FlareCover;
            foreach (var p in FaceBars(fsec, r.FlareCover + ft.Diameter + fv.Diameter / 2, r.FlareFaceBarsS, r.FlareFaceSpacingS, r.FlareFaceBarsN, r.FlareFaceSpacingN,
                r.FlareChamferBars, 0, null, 0, $"{loc} flare bars"))
                AddRun(bars, "F", fv, $"{loc} flare bars", new[] { unit.World(c.S + p.X, c.N + p.Y, zBot), unit.World(c.S + p.X, c.N + p.Y, fTopBar) },
                    r.MaxBarLength, Lap(fv, r, r.FlareCover, In(6)));
            SectionTies(unit, bars, c.S, c.N, InsetConvex(fsec, r.FlareCover + ft.Diameter / 2), ft, r.FlareTieSpacing,
                c.BottomZ + In(3), sd.ColumnFlareElevation - r.FlareCover, 0, "P", $"{loc} flare ties");
        }
    }

    // ------------------------------------------------------------------ footing dowels and column splices

    /// <summary>Default dowel hook leg (out-to-out) and pin diameter by bar size, from the plan bent-bar details.</summary>
    private static (double hook, double pin) DowelDefaults(RebarSize size) => size.Name switch
    {
        "#11" => (Ft(2), Ft(1)), "#14" => (Ft(2) + In(7), Ft(1) + In(6.25)), "#18" => (Ft(3) + In(5), Ft(2)),
        _ => (12 * size.Diameter, 6 * size.Diameter),
    };

    /// <summary>L-shaped dowel: vertical leg A (out-to-out, from the underside of the hook leg to the top) and hook leg B
    /// (out-to-out from the outside of the vertical leg), bent around the pin. Local frame: x along the hook, y up from the
    /// underside of the hook leg, origin under the vertical leg's centreline. The scheduled length is A + B (plan bar lists).</summary>
    private static (BarShape shape, Vec2[] local) DowelL(double a, double b, RebarSize size, double pin)
    {
        double db = size.Diameter, rc = pin / 2 + db / 2;
        var bends = new List<(Vec2, double)>();
        var local = BarShape.Rounded(new[] { new Vec2(0, a), new Vec2(0, db / 2), new Vec2(b - db / 2, db / 2) }, rc, bends);
        var shape = new BarShape
        {
            Code = "L", Description = "L-shaped dowel, 90° hook", Centerline = local, PinDiameter = pin, FixedLength = a + b,
            Dimensions =
            {
                new BarDimension("A", a, new Vec2(0, 0), new Vec2(0, a), new Vec2(-1, 0)),
                new BarDimension("B", b, new Vec2(0, 0), new Vec2(b, 0), new Vec2(0, -1)),
            },
        };
        shape.Bends.AddRange(bends);
        return (shape, local);
    }

    /// <summary>
    /// Column verticals spliced to the footing: an L-shaped dowel per column bar (hook on the bottom mat, hook leg pointing
    /// outward along S at the end faces and chamfers, along N at the long faces), then either a contact lap (column bars start at
    /// the footing top, dowels stand beside them on the inside) or a coupler at the bar's splice level. Bars are taken in
    /// counter-clockwise order from +S to read <see cref="SubstructureReinforcementDefinition.ColumnSplicePattern"/>.
    /// </summary>
    private void SplicedVerticals(SubstructureUnit unit, ColumnInfo c, FoundationInfo f, List<Vec2> vb, RebarSize v, Vec2[] barLine,
        double zTop, string loc, List<RawBar> bars)
    {
        var r = unit.Definition.Reinforcement;
        double db = v.Diameter;
        var (hookDef, pinDef) = DowelDefaults(v);
        double hook = r.DowelHookLength > 0 ? r.DowelHookLength : hookDef, pin = r.DowelPin > 0 ? r.DowelPin : pinDef;
        double zHook = f.BottomZ + r.DowelBottomAboveFooting;
        bool coupler = r.ColumnSplice == ColumnSpliceType.Coupler;
        double lap = r.ColumnLapLength > 0 ? r.ColumnLapLength : Lap(v, r, r.ColumnCover, In(6));
        string pat = r.ColumnSplicePattern ?? "";
        if (pat != "" && pat.Length != vb.Count)
        {
            Warn($"{loc}: splice pattern has {pat.Length} letters but the column has {vb.Count} vertical bars — alternating H/L used.");
            pat = "";
        }
        static double Ang(Vec2 p) { double a = Math.Atan2(p.Y, p.X); return a < -1e-9 ? a + 2 * Math.PI : a; }
        var order = vb.OrderBy(Ang).ToList();
        int k = 0;
        foreach (var p in order)
        {
            char ch = pat != "" ? char.ToUpperInvariant(pat[k]) : (coupler ? (k % 2 == 0 ? 'H' : 'L') : 'H');
            k++;
            // Face normal at the bar (nearest edge of the bar line).
            Vec2 outN = Vec2.Zero; double best = double.MaxValue;
            for (int i = 0; i < barLine.Length; i++)
            {
                var a = barLine[i]; var b = barLine[(i + 1) % barLine.Length];
                var e = b - a; double t = Math.Clamp(Vec2.Dot(p - a, e) / Math.Max(e.LengthSquared, 1e-12), 0, 1);
                double d = Vec2.Distance(p, a + e * t);
                if (d < best) { best = d; outN = e.Normalized().Right; }
            }
            var hookDir = Math.Abs(outN.Y) >= 0.9 ? new Vec2(0, Math.Sign(outN.Y)) : new Vec2(outN.X >= 0 ? 1 : -1, 0);
            double level = coupler ? (ch == 'H' ? r.CouplerHigh : r.CouplerLow) : lap;
            double zJoint = f.TopZ + level;
            var pDowel = coupler ? p : p - outN * db;                      // contact lap: dowel on the inside of the bar
            double zBar0 = coupler ? zJoint : f.TopZ;
            // Column bar(s).
            if (!coupler && r.ColumnMidLapStart > 0)
            {
                double zMid = f.TopZ + r.ColumnMidLapStart;
                bars.Add(StraightBar(unit, c, p, v, loc, zBar0, zMid + lap));
                bars.Add(StraightBar(unit, c, p, v, loc, zMid, zTop));
            }
            else
            {
                double top = ch == 'T' && r.ColumnTerminatingBarLength > 0 ? zBar0 + r.ColumnTerminatingBarLength : zTop;
                bars.Add(StraightBar(unit, c, p, v, loc, zBar0, top));
            }
            // Dowel.
            var (shape, loc2d) = DowelL(zJoint - zHook, hook, v, pin);
            var pts = loc2d.Select(q => unit.World(c.S + pDowel.X + hookDir.X * q.X, c.N + pDowel.Y + hookDir.Y * q.X, zHook + q.Y)).ToArray();
            bars.Add(new RawBar("L", v, shape, $"{loc} footing dowels", pts));
            if (coupler)
            {
                double cl = r.CouplerLength > 0 ? r.CouplerLength : 6 * db, cd = r.CouplerDiameter > 0 ? r.CouplerDiameter : 1.75 * db;
                var m = new Mesh();
                MeshBuilder.AddExtrusion(m, Circle(cd / 2, 24), Frame(unit, c.S + p.X, c.N + p.Y), zJoint - cl / 2, zJoint + cl / 2);
                unit.Solids.Add(new SubSolid(SubElementKind.Coupler, $"{loc} coupler {k}",
                    $"Mechanical splice coupler (generic) for {v.Name} bars, {Sh(cl)} long x {Sh(cd)} dia, at {El(zJoint)}", m, IsConcrete: false));
            }
        }
    }

    private static RawBar StraightBar(SubstructureUnit unit, ColumnInfo c, Vec2 p, RebarSize v, string loc, double z0, double z1)
    {
        var pts = new[] { unit.World(c.S + p.X, c.N + p.Y, z0), unit.World(c.S + p.X, c.N + p.Y, z1) };
        return new RawBar("V", v, BarShape.Straight(z1 - z0), $"{loc} verticals", pts);
    }

    /// <summary>Convex counter-clockwise polygon offset inward by d (edge lines moved in, adjacent ones intersected).</summary>
    private static Vec2[] InsetConvex(Vec2[] poly, double d)
    {
        int n = poly.Length;
        var res = new Vec2[n];
        for (int i = 0; i < n; i++)
        {
            Vec2 a0 = poly[(i - 1 + n) % n], a1 = poly[i], b1 = poly[(i + 1) % n];
            var ea = (a1 - a0).Normalized(); var eb = (b1 - a1).Normalized();
            Vec2 pa = a1 + ea.Left * d, pb = a1 + eb.Left * d;
            double den = Vec2.Cross(ea, eb);
            res[i] = Math.Abs(den) < 1e-12 ? pa : pa + ea * (Vec2.Cross(pb - pa, eb) / den);
        }
        return res;
    }

    /// <summary>
    /// Vertical bar positions for a section outline (counter-clockwise, from the start of the bottom S face) and the inset
    /// to the bar centers: per face type (S faces, N faces, chamfers) a group centred on the outside face at the spacing
    /// (or chamfer bars at given distances along the outside face from its S-face end), set in normal to the face; or equal
    /// spaces over the inset face with shared corner bars (spacing 0). All counts 0 = <paramref name="autoCount"/> bars at
    /// equal spaces around the inset section.
    /// </summary>
    private List<Vec2> FaceBars(Vec2[] sec, double inset, int nS, double sS, int nN, double sN, int nC, double sC, List<double>? cPos,
        int autoCount, string what)
    {
        var pts = new List<Vec2>();
        int n = sec.Length;
        var line = InsetConvex(sec, inset);
        if (nS + nN + nC == 0 && (cPos == null || cPos.Count == 0))
        {
            double per = 0;
            for (int i = 0; i < n; i++) per += Vec2.Distance(line[i], line[(i + 1) % n]);
            for (int k = 0; k < autoCount; k++)
            {
                double t = per * k / autoCount;
                for (int i = 0; i < n; i++)
                {
                    double l = Vec2.Distance(line[i], line[(i + 1) % n]);
                    if (t <= l) { pts.Add(Vec2.Lerp(line[i], line[(i + 1) % n], t / l)); break; }
                    t -= l;
                }
            }
            return pts;
        }
        // Face types in order: octagon S, C, N, C, S, C, N, C; rectangle S, N, S, N.
        bool Chamfer(int i) => n == 8 && i % 2 == 1;
        (int count, double spacing) Kind(int i) => n == 8 ? (i % 2 == 1 ? (nC, sC) : i % 4 == 0 ? (nS, sS) : (nN, sN)) : (i % 2 == 0 ? (nS, sS) : (nN, sN));
        bool Equal(int i) => !(Chamfer(i) && cPos is { Count: > 0 }) && Kind(i) is { count: > 1, spacing: <= 0 };
        for (int i = 0; i < n; i++)
        {
            Vec2 a = sec[i], b = sec[(i + 1) % n];
            double l = Vec2.Distance(a, b);
            var dir = (b - a) / l;
            var inward = dir.Left * inset;
            if (Chamfer(i) && cPos is { Count: > 0 })
            {
                // Distances from the chamfer's S-face end: chamfers 1 and 5 start at the S face, 3 and 7 end at it.
                bool fromA = i % 4 == 1;
                foreach (double t in cPos)
                {
                    if (t < 0 || t > l + 1e-6) Warn($"{what}: chamfer bar at {Sh(t)} is off the {Sh(l)} chamfer face.");
                    pts.Add((fromA ? a + dir * t : b - dir * t) + inward);
                }
                continue;
            }
            var (k, sp) = Kind(i);
            if (k <= 0) continue;
            if (sp > 0 || k == 1)
            {
                if ((k - 1) * sp > l + 1e-6) Warn($"{what}: {k} bars at {Sh(sp)} do not fit on a {Sh(l)} face.");
                var mid = (a + b) / 2 + inward;
                for (int j = 0; j < k; j++) pts.Add(mid + dir * ((j - (k - 1) / 2.0) * sp));
            }
            else
            {
                // Equal spaces over the inset face with bars at both corners: this face places its start corner and the
                // interior bars; the end corner belongs to the next face unless that face is not equally spaced.
                Vec2 ia = line[i], ib = line[(i + 1) % n];
                int last = Equal((i + 1) % n) ? k - 1 : k;
                for (int j = 0; j < last; j++) pts.Add(Vec2.Lerp(ia, ib, (double)j / (k - 1)));
            }
        }
        return pts;
    }

    /// <summary>Convex polygon clipped to x ≥ x0 (keepRight) or x ≤ x0: the closed hoop around one end of the section.</summary>
    private static Vec2[] ClipX(Vec2[] poly, double x0, bool keepRight)
    {
        var res = new List<Vec2>();
        int n = poly.Length;
        bool Keep(Vec2 q) => keepRight ? q.X >= x0 - 1e-12 : q.X <= x0 + 1e-12;
        for (int i = 0; i < n; i++)
        {
            Vec2 a = poly[i], b = poly[(i + 1) % n];
            if (Keep(a)) res.Add(a);
            if (Keep(a) != Keep(b)) res.Add(Vec2.Lerp(a, b, (x0 - a.X) / (b.X - a.X)));
        }
        // Drop repeated points (a clip through a vertex) and collinear ones.
        var clean = new List<Vec2>();
        foreach (var q in res) if (clean.Count == 0 || Vec2.Distance(clean[^1], q) > 1e-6) clean.Add(q);
        if (clean.Count > 1 && Vec2.Distance(clean[0], clean[^1]) < 1e-6) clean.RemoveAt(clean.Count - 1);
        for (int i = clean.Count - 1; i >= 0 && clean.Count > 3; i--)
        {
            Vec2 p0 = clean[(i - 1 + clean.Count) % clean.Count], p1 = clean[i], p2 = clean[(i + 1) % clean.Count];
            if (Math.Abs(Vec2.Cross((p1 - p0).Normalized(), (p2 - p1).Normalized())) < 1e-9) clean.RemoveAt(i);
        }
        return clean.ToArray();
    }

    /// <summary>
    /// Straight crosstie between two bars (centers), its leg tangent to them on the <paramref name="inner"/> side (−1 = −S,
    /// +1 = +S), with 135° hooks around both bars and 6 db (3" min) tails.
    /// </summary>
    private static (BarShape shape, Vec2[] local) Crosstie(Vec2 top, Vec2 bot, int inner, double barDia, RebarSize size)
    {
        double db = size.Diameter;
        double pin = (size.Name is "#6" or "#7" or "#8" ? 6 : 4) * db;
        double rc = Math.Max(barDia / 2 + db / 2, pin / 2 + db / 2);
        double ext = Math.Max(6 * db, In(3));
        double a0 = inner < 0 ? Math.PI : 0;                // leg side of the bars
        double sweep = 0.75 * Math.PI;
        List<Vec2> Hook(Vec2 c, double dirSign)
        {
            var h = new List<Vec2>();
            int k = 9;
            for (int j = 0; j <= k; j++) h.Add(c + Vec2.FromAngle(a0 + dirSign * sweep * j / k) * rc);
            double ae = a0 + dirSign * sweep;
            h.Add(h[^1] + Vec2.FromAngle(ae + dirSign * Math.PI / 2) * ext);
            return h;
        }
        // Over the top bar (toward +N) and under the bottom bar: the arc direction depends on the leg side. A bend larger
        // than the bar bears on its outer side, so the bend centres sit inside the bar centres by the difference.
        double sTop = inner < 0 ? -1 : 1;
        var shift = new Vec2(0, Math.Max(0, rc - barDia / 2 - db / 2));
        var bottom = Hook(bot + shift, -sTop);
        bottom.Reverse();
        var pts = bottom.Concat(Hook(top - shift, sTop)).ToList();
        double len = Vec2.Distance(top, bot);
        var shape = new BarShape
        {
            Code = "CROSSTIE", Centerline = pts.ToArray(), PinDiameter = pin,
            Description = $"Crosstie, 135° hooks both ends, {new SBM.Core.Units.UnitSystem().FormatLength(ext, SBM.Core.Units.LengthKind.Short)} ext.",
            Dimensions = { new BarDimension("A", len, bot + Vec2.FromAngle(a0) * rc, top + Vec2.FromAngle(a0) * rc, new Vec2(inner, 0)) },
        };
        return (shape, pts.ToArray());
    }

    /// <summary>Closed polygon ties (centerline <paramref name="tieLine"/>, relative to the column center) at equal spaces ≤ spacing.</summary>
    private void SectionTies(SubstructureUnit unit, List<RawBar> bars, double s, double n, Vec2[] tieLine, RebarSize size, double spacing,
        double z0, double z1, double dz, string series, string loc)
    {
        if (z1 <= z0) return;
        var (sh, local) = PolygonTie(tieLine, size);
        int nt = (int)Math.Ceiling((z1 - z0) / spacing - 1e-9) + 1;
        for (int i = 0; i < nt; i++)
        {
            double z = z0 + dz + (z1 - z0) * i / Math.Max(1, nt - 1);
            bars.Add(new RawBar(series, size, sh, loc, local.Select(p => unit.World(s + p.X, n + p.Y, z)).ToArray()));
        }
    }

    /// <summary>
    /// Closed tie following a convex counter-clockwise centerline polygon (rectangle or chamfered octagon): every corner bent
    /// around the same pin, 135° hooks at the first corner with 6 db (3" min) tails into the core.
    /// </summary>
    private static (BarShape shape, Vec2[] local) PolygonTie(Vec2[] line, RebarSize size)
    {
        double db = size.Diameter;
        double pin = (size.Name is "#6" or "#7" or "#8" ? 6 : 4) * db;
        double rc = pin / 2 + db / 2;
        double ext = Math.Max(6 * db, In(3));
        int n = line.Length;
        // The hooks go at the sharpest corner (largest turn), where the 135° tails point into the core.
        double Turn(int i)
        {
            var u0 = (line[i] - line[(i - 1 + n) % n]).Normalized(); var w0 = (line[(i + 1) % n] - line[i]).Normalized();
            return Math.Atan2(Vec2.Cross(u0, w0), Vec2.Dot(u0, w0));
        }
        int k0 = Enumerable.Range(0, n).OrderByDescending(Turn).First();
        line = line.Skip(k0).Concat(line.Take(k0)).ToArray();
        var c = new Vec2[n]; var a1 = new double[n]; var sw = new double[n];
        for (int i = 0; i < n; i++)
        {
            Vec2 v = line[i];
            var u = (v - line[(i - 1 + n) % n]).Normalized();
            var w = (line[(i + 1) % n] - v).Normalized();
            double phi = Math.Atan2(Vec2.Cross(u, w), Vec2.Dot(u, w));
            var t1 = v - u * (rc * Math.Tan(phi / 2));
            c[i] = t1 + u.Left * rc;
            a1[i] = Math.Atan2(t1.Y - c[i].Y, t1.X - c[i].X);
            sw[i] = phi;
        }
        var pts = new List<Vec2>();
        void Arc(Vec2 cc, double from, double to)
        {
            int k = Math.Max(3, (int)Math.Ceiling(Math.Abs(to - from) / (Math.PI / 12)));
            for (int j = 0; j <= k; j++) pts.Add(cc + Vec2.FromAngle(from + (to - from) * j / k) * rc);
        }
        Vec2 Tangent(double ang) => Vec2.FromAngle(ang + Math.PI / 2);      // direction of travel (counter-clockwise)
        double hook = 135 * Math.PI / 180;
        double a2 = a1[0] + sw[0];
        double h1 = a2 - hook;
        pts.Add(c[0] + Vec2.FromAngle(h1) * rc - Tangent(h1) * ext);
        Arc(c[0], h1, a2);
        for (int i = 1; i < n; i++) Arc(c[i], a1[i], a1[i] + sw[i]);
        double h2 = a1[0] + hook;
        Arc(c[0], a1[0], h2);
        pts.Add(c[0] + Vec2.FromAngle(h2) * rc + Tangent(h2) * ext);

        // Outside dimensions (centerline polygon + db/2).
        var outer = InsetConvex(line, -db / 2);
        double minX = outer.Min(p => p.X), maxX = outer.Max(p => p.X), minY = outer.Min(p => p.Y), maxY = outer.Max(p => p.Y);
        var shape = new BarShape
        {
            Code = "TIE", Centerline = pts.ToArray(), PinDiameter = pin,
            Description = $"Closed {(n == 4 ? "tie" : "octagonal tie")}, 135° hooks, {new SBM.Core.Units.UnitSystem().FormatLength(ext, SBM.Core.Units.LengthKind.Short)} ext.",
            Dimensions =
            {
                new BarDimension("A", maxX - minX, new Vec2(minX, minY), new Vec2(maxX, minY), new Vec2(0, -1)),
                new BarDimension("B", maxY - minY, new Vec2(maxX, minY), new Vec2(maxX, maxY), new Vec2(1, 0)),
            },
        };
        if (n == 8)
        {
            // Chamfer legs of the outside: from the top-right corner of the bounding box to the octagon vertices.
            var top = outer.Where(p => Math.Abs(p.Y - maxY) < 1e-9).Max(p => p.X);
            var right = outer.Where(p => Math.Abs(p.X - maxX) < 1e-9).Max(p => p.Y);
            shape.Dimensions.Add(new BarDimension("C", maxX - top, new Vec2(top, maxY), new Vec2(maxX, maxY), new Vec2(0, 1)));
            shape.Dimensions.Add(new BarDimension("D", maxY - right, new Vec2(maxX, right), new Vec2(maxX, maxY), new Vec2(1, 0)));
        }
        return (shape, pts.ToArray());
    }

    private void ShaftBars(SubstructureUnit unit, FoundationInfo f, List<RawBar> bars)
    {
        var sd = unit.Definition;
        var r = sd.Reinforcement;
        var v = RebarCatalog.Size(r.ShaftBarSize);
        var sp = RebarCatalog.Size(r.ShaftSpiralSize);
        double cov = r.ShaftCover;
        bool underCap = unit.Type == SupportType.Abutment;
        double zTop = underCap ? unit.CapTopAt(f.S) - r.CapCover - In(3) : f.TopZ - In(3);
        double zBot = f.BottomZ + cov;
        string loc = $"{unit.Name} drilled shafts";
        foreach (var p in PerimeterBars(ColumnShape.Round, f.SizeS, f.SizeN, cov + sp.Diameter + v.Diameter / 2, r.ShaftBarCount))
            AddRun(bars, "D", v, $"{loc} verticals", new[] { unit.World(f.S + p.X, f.N + p.Y, zBot), unit.World(f.S + p.X, f.N + p.Y, zTop) },
                r.MaxBarLength, Lap(v, r, cov, In(6)));
        Confinement(unit, bars, ColumnShape.Round, f.S, f.N, f.SizeS, f.SizeN, cov, sp, r.ShaftSpiralPitch, f.BottomZ + cov, f.TopZ - In(3), "E", $"{loc} spiral");
    }

    private static IEnumerable<Vec2> PerimeterBars(ColumnShape shape, double sizeS, double sizeN, double inset, int count)
    {
        if (count < 1) yield break;
        if (shape == ColumnShape.Round)
        {
            double rr = sizeS / 2 - inset;
            for (int i = 0; i < count; i++) yield return Vec2.FromAngle(2 * Math.PI * (i + 0.5) / count) * rr;
            yield break;
        }
        // Rectangular: equal spacing around the inset perimeter, starting at a corner.
        double a = sizeS / 2 - inset, b = sizeN / 2 - inset;
        var corners = new[] { new Vec2(-a, -b), new Vec2(a, -b), new Vec2(a, b), new Vec2(-a, b) };
        double per = 4 * (a + b);
        for (int i = 0; i < count; i++)
        {
            double t = per * i / count;
            for (int k = 0; k < 4; k++)
            {
                double l = Vec2.Distance(corners[k], corners[(k + 1) % 4]);
                if (t <= l) { yield return Vec2.Lerp(corners[k], corners[(k + 1) % 4], t / l); break; }
                t -= l;
            }
        }
    }

    /// <summary>Spiral (round, 1.5 extra flat turns at each end) or closed ties (rectangular) between two elevations.</summary>
    private void Confinement(SubstructureUnit unit, List<RawBar> bars, ColumnShape shape, double s, double n, double sizeS, double sizeN,
        double cov, RebarSize size, double pitch, double z0, double z1, string series, string loc)
    {
        if (z1 <= z0) return;
        if (shape == ColumnShape.Rectangular)
        {
            double w = sizeS - 2 * cov, h = sizeN - 2 * cov;
            int nt = (int)Math.Ceiling((z1 - z0) / pitch - 1e-9) + 1;
            for (int i = 0; i < nt; i++)
            {
                double z = z0 + (z1 - z0) * i / Math.Max(1, nt - 1);
                var (sh, local) = ClosedStirrup(w, h, size);
                var pts = local.Select(p => unit.World(s - sizeS / 2 + cov + p.X, n - sizeN / 2 + cov + p.Y, z)).ToArray();
                bars.Add(new RawBar(series, size, sh, loc, pts));
            }
            return;
        }
        double rs = sizeS / 2 - cov - size.Diameter / 2;
        double height = z1 - z0;
        // 1.5 extra (anchorage) turns at each end, wound close: they climb one bar diameter per turn so the bar never
        // overlaps itself. The pitched body takes the rest of the height.
        const double extra = 1.5;
        double db = size.Diameter;
        double closeRise = extra * db;
        double body = Math.Max(0, height - 2 * closeRise);
        double turns = body / pitch;
        const int perTurn = 64;                                     // chords within ~0.12% of the radius of the true helix
        var ptsH = new List<Vec3>();
        double total = turns + 2 * extra;
        int nTot = (int)Math.Ceiling(total * perTurn);
        for (int i = 0; i <= nTot; i++)
        {
            double tTurn = Math.Min(total, (double)i / perTurn);   // turns from the start (incl. bottom close turns)
            double z = tTurn < extra ? z0 + tTurn * db
                : tTurn > extra + turns ? z1 - (total - tTurn) * db
                : z0 + closeRise + (tTurn - extra) * pitch;
            var p = Vec2.FromAngle(2 * Math.PI * tTurn) * rs;
            ptsH.Add(unit.World(s + p.X, n + p.Y, z));
        }
        // Exact helix length (the samples are chords).
        double circ = 2 * Math.PI * rs;
        double length = 2 * extra * Math.Sqrt(circ * circ + db * db) + turns * Math.Sqrt(circ * circ + pitch * pitch);
        double dOut = 2 * rs + size.Diameter;
        var circle = Enumerable.Range(0, 49).Select(i => Vec2.FromAngle(2 * Math.PI * i / 48) * rs).ToArray();
        var shapeS = new BarShape
        {
            Code = "SPIRAL", Description = $"Spiral, pitch {pitch / In(1):0.##} in, {turns:0.#} turns + 1.5 close turns each end, height {height / Ft(1):0.##} ft",
            Centerline = circle, FixedLength = length,
            Dimensions =
            {
                new BarDimension("D", dOut, new Vec2(-dOut / 2, 0), new Vec2(dOut / 2, 0), new Vec2(0, -1)),
                new BarDimension("H", height, new Vec2(0, 0), new Vec2(0, 0), new Vec2(0, 0)),
                new BarDimension("P", pitch, new Vec2(0, 0), new Vec2(0, 0), new Vec2(0, 0)),
            },
        };
        bars.Add(new RawBar(series, size, shapeS, loc, ptsH.ToArray()));
    }

    private void FootingBars(SubstructureUnit unit, FoundationInfo f, List<RawBar> bars)
    {
        var r = unit.Definition.Reinforcement;
        var size = RebarCatalog.Size(r.FootingBarSize);
        double cov = r.FootingCover, sp = r.FootingBarSpacing;
        string loc = $"{unit.Name} footings";
        double db = size.Diameter;
        // Bottom mat (two layers), top mat (two layers); bars run each way.
        foreach (var (z, series, what, alongS) in new[]
                 {
                     (f.BottomZ + cov + db / 2, "F", "bottom mat", true), (f.BottomZ + cov + 1.5 * db, "F", "bottom mat", false),
                     (f.TopZ - cov - db / 2, "G", "top mat", true), (f.TopZ - cov - 1.5 * db, "G", "top mat", false),
                 })
        {
            double lenRun = (alongS ? f.SizeS : f.SizeN) - 2 * cov, width = (alongS ? f.SizeN : f.SizeS) - 2 * cov;
            int nb = (int)Math.Ceiling(width / sp - 1e-9) + 1;
            for (int i = 0; i < nb; i++)
            {
                double c = -width / 2 + width * i / Math.Max(1, nb - 1);
                var a = alongS ? unit.World(f.S - lenRun / 2, f.N + c, z) : unit.World(f.S + c, f.N - lenRun / 2, z);
                var b = alongS ? unit.World(f.S + lenRun / 2, f.N + c, z) : unit.World(f.S + c, f.N + lenRun / 2, z);
                AddRun(bars, series, size, $"{loc} {what}", new[] { a, b }, r.MaxBarLength, Lap(size, r, cov, sp));
            }
        }
    }

    // ------------------------------------------------------------------ marks

    private static string ShapeKey(BarShape s) =>
        s.Code + ":" + string.Join(",", s.Dimensions.Select(d => $"{d.Letter}{Math.Round(d.Value / In(0.25))}")) + ":" + Math.Round(s.DevelopedLength / In(1));

    private static int SeriesOrder(string s) => s switch
    {
        "A" => 0, "B" => 1, "C" => 2, "S" => 3, "W" => 4, "H" => 5, "V" => 6, "L" => 6, "P" => 7, "D" => 8, "E" => 9, _ => 10,
    };

    private static void AssignMarks(SubstructureModel m, SubstructureUnit unit, List<RawBar> bars)
    {
        bool epoxy = unit.Definition.Reinforcement.EpoxyCoated;
        var marks = new List<BarMark>();
        var places = new List<BarPlacement>();
        // Straight bars within 2" of each other share a mark, cut to the shortest (cover only increases).
        var snapped = new List<RawBar>();
        foreach (var grp in bars.GroupBy(b => (b.Series, b.Size.Name, Straight: b.Shape.Code == "STR")))
        {
            if (!grp.Key.Straight) { snapped.AddRange(grp); continue; }
            var cluster = new List<RawBar>();
            double top = double.NaN;
            void Flush()
            {
                double min = Math.Round(cluster.Min(b => b.Shape.DevelopedLength) / In(1)) * In(1);
                snapped.AddRange(cluster.Select(b => b with { Shape = BarShape.Straight(min) }));
                cluster.Clear();
            }
            foreach (var b in grp.OrderByDescending(b => b.Shape.DevelopedLength))
            {
                if (cluster.Count > 0 && top - b.Shape.DevelopedLength > In(2)) Flush();
                if (cluster.Count == 0) top = b.Shape.DevelopedLength;
                cluster.Add(b);
            }
            if (cluster.Count > 0) Flush();
        }
        bars = snapped;
        foreach (var series in bars.GroupBy(b => b.Series).OrderBy(g => SeriesOrder(g.Key)).ThenBy(g => g.Key))
        {
            var groups = series.GroupBy(b => (b.Size.Name, Key: ShapeKey(b.Shape)))
                .OrderByDescending(g => g.First().Shape.DevelopedLength).ToList();
            int k = 1;
            foreach (var g in groups)
            {
                string mark = groups.Count == 1 ? series.Key : series.Key + k++;
                var first = g.First();
                marks.Add(new BarMark
                {
                    Mark = mark, Size = first.Size, Shape = first.Shape, Location = string.Join(", ", g.Select(x => x.Location).Distinct()),
                    Unit = unit.Name, Epoxy = epoxy, Count = g.Count(),
                });
                places.AddRange(g.Select(b => new BarPlacement { Mark = mark, Points = b.Points, Group = "Sub", Unit = unit.Name }));
            }
        }
        lock (m)
        {
            m.Marks.AddRange(marks);
            m.Bars.AddRange(places);
        }
    }
}
