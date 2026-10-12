using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Arch;

public static partial class ArchGenerator
{
    private sealed partial class Gen
    {
        /// <summary>Deck parametrisation saved by BuildDeck: ties, knots (south / north distance), end knuckles.</summary>
        private (TieGeo ts, TieGeo tn, List<(double s, double n)> knots, ArchKnuckleDefinition? ka, ArchKnuckleDefinition? kb)? _deck;

        /// <summary>Structure CL of the deck (local plan): samples (point, unit across south -> north, half width), cumulative length; extended at both ends to
        /// the end web inside faces.</summary>
        private (List<Vec2> m, List<Vec2> e, List<double> S)? DeckLine()
        {
            if (_deck is not { } dd) return null;
            var m = new List<Vec2>(); var e = new List<Vec2>();
            for (int k = 0; k + 1 < dd.knots.Count; k++)
                for (int i = 0; i < 8; i++)
                {
                    double t = i / 8.0;
                    var a = dd.ts.At(dd.knots[k].s + (dd.knots[k + 1].s - dd.knots[k].s) * t).p;
                    var b = dd.tn.At(dd.knots[k].n + (dd.knots[k + 1].n - dd.knots[k].n) * t).p;
                    m.Add((a + b) * 0.5); e.Add((b - a).Normalized());
                }
            {
                var a = dd.ts.At(dd.knots[^1].s).p; var b = dd.tn.At(dd.knots[^1].n).p;
                m.Add((a + b) * 0.5); e.Add((b - a).Normalized());
            }
            // Extend to the end web inside faces.
            double ext0 = dd.ka is { } k0 ? k0.EndWebOffset - k0.EndWebThickness : 0, ext1 = dd.kb is { } k1 ? k1.EndWebOffset - k1.EndWebThickness : 0;
            var t0 = (m[1] - m[0]).Normalized(); var t1 = (m[^1] - m[^2]).Normalized();
            m.Insert(0, m[0] - t0 * ext0); e.Insert(0, e[0]);
            m.Add(m[^1] + t1 * ext1); e.Add(e[^1]);
            var S = new List<double> { 0 };
            for (int i = 1; i < m.Count; i++) S.Add(S[^1] + (m[i] - m[i - 1]).Length);
            return (m, e, S);
        }

        private static (Vec2 m, Vec2 e) AtS((List<Vec2> m, List<Vec2> e, List<double> S) dl, double s)
        {
            int i = dl.S.BinarySearch(s);
            if (i < 0) i = ~i;
            i = Math.Clamp(i, 1, dl.S.Count - 1);
            double t = (s - dl.S[i - 1]) / Math.Max(1e-12, dl.S[i] - dl.S[i - 1]);
            return (Vec2.Lerp(dl.m[i - 1], dl.m[i], t), (dl.e[i - 1] + (dl.e[i] - dl.e[i - 1]) * t).Normalized());
        }

        private void AddBars(string mark, RebarSize size, string location, List<Vec3[]> bars, BarShape? shape = null)
        {
            if (bars.Count == 0) return;
            var lens = bars.Select(b => Enumerable.Range(1, b.Length - 1).Sum(i => (b[i] - b[i - 1]).Length)).ToList();
            bool same = lens.Max() - lens.Min() < In(0.5);
            Model.Concrete.Marks.Add(new BarMark
            {
                Mark = mark, Size = size, Shape = shape ?? BarShape.Straight(lens.Max()), Location = location, Count = bars.Count,
                VaryingLengths = same || shape != null ? null : lens,
            });
            foreach (var b in bars) Model.Concrete.Bars.Add(new BarPlacement { Mark = mark, Points = b, Group = "Deck" });
        }

        /// <summary>Deck bars (p.152 / 154): A (top transverse, centred on CL NHT), B (bottom transverse, centred on the structure CL) at the transverse spacing
        /// along the structure CL; D1 / D2 longitudinal lines on the grid (bottom ones in the corrugations), split into bars of the plan length with the plan
        /// lap from the west end; curb stirrups along the curbs at the transverse spacing with 4 corner bars.</summary>
        private void BuildDeckRebar(ArchDeckDefinition dk, ArchDeckRebar rb)
        {
            if (DeckLine() is not { } dl) return;
            var sz = RebarCatalog.Size(rb.Size);
            double dA = sz.Diameter, Lt = dl.S[^1];
            double zD2 = dk.FormBottomZ + rb.BottomClear + dA / 2, zB = zD2 + dA;
            Vec3 Top(Vec2 p, double below) => W(p.WithZ(GradeZ(p.X, p.Y) - below));
            // Transverse bars.
            var aBars = new List<Vec3[]>(); var bBars = new List<Vec3[]>();
            int nT = (int)Math.Floor((Lt - 2 * rb.EndCover) / rb.TransSpacing) + 1;
            double s0 = (Lt - (nT - 1) * rb.TransSpacing) / 2;
            for (int i = 0; i < nT; i++)
            {
                var (m, e) = AtS(dl, s0 + i * rb.TransSpacing);
                // B: centred on the structure CL.
                bBars.Add(new[] { W((m - e * rb.BottomTransLength / 2).WithZ(zB)), W((m + e * rb.BottomTransLength / 2).WithZ(zB)) });
                // A: centred where the ring crosses CL NHT, following the crown (3 points per foot).
                double Off(double y) => _al.Inverse(W((m + e * y).WithZ(0)).XY).Offset;
                double lo = -rb.BottomTransLength / 2, hi = rb.BottomTransLength / 2, flo = Off(lo);
                for (int it = 0; it < 50; it++) { double md = (lo + hi) / 2, fm = Off(md); if (Math.Sign(fm) == Math.Sign(flo)) { lo = md; flo = fm; } else hi = md; }
                double yc = (lo + hi) / 2;
                int np = Math.Max(2, (int)Math.Ceiling(rb.TopTransLength / Ft(1)) * 2);
                aBars.Add(Enumerable.Range(0, np + 1).Select(k => Top(m + e * (yc - rb.TopTransLength / 2 + rb.TopTransLength * k / np), rb.TopCover + dA / 2)).ToArray());
            }
            // Longitudinal lines: grid about the structure CL, split into bars with laps from the west end.
            var d1 = new List<Vec3[]>(); var d2 = new List<Vec3[]>();
            double sA = rb.EndCover, sB = Lt - rb.EndCover;
            foreach (int j in Enumerable.Range(0, rb.LongCount))
            {
                double y = (j - (rb.LongCount - 1) / 2.0) * rb.LongSpacing;
                int np = Math.Max(2, (int)Math.Ceiling((sB - sA) / Ft(1)));
                var top = new List<Vec3>(); var bot = new List<Vec3>();
                for (int k = 0; k <= np; k++)
                {
                    var (m, e) = AtS(dl, sA + (sB - sA) * k / np);
                    var p = m + e * y;
                    top.Add(Top(p, rb.TopCover + dA + dA / 2));
                    bot.Add(W(p.WithZ(zD2)));
                }
                d1.AddRange(SplitBars(top, rb.LongBarLength, rb.LongLap));
                d2.AddRange(SplitBars(bot, rb.LongBarLength, rb.LongLap));
            }
            AddBars(rb.TopTransMark, sz, "Arch deck top transverse (p.152)", aBars);
            AddBars(rb.BottomTransMark, sz, "Arch deck bottom transverse (p.152)", bBars);
            AddBars(rb.TopLongMark, sz, "Arch deck top longitudinal (p.152)", d1);
            AddBars(rb.BottomLongMark, sz, "Arch deck bottom longitudinal, one in each corrugation (p.152)", d2);
            // Curbs: stirrups (closed) centred in the curb width at the transverse spacing, 4 corner bars.
            if (dk.Curbs.Count > 0 && _deck is { } dd && dd.ka != null && dd.kb != null)
            {
                var st = RebarCatalog.Size(rb.CurbStirrupSize);
                var sBars = new List<Vec3[]>(); var cBars = new List<Vec3[]>();
                var mA = AtS(dl, rb.EndCover).m; var mB = AtS(dl, Lt - rb.EndCover).m;
                foreach (var c in dk.Curbs)
                {
                    double oc = c.InnerOffset + Math.Sign(c.InnerOffset) * c.Width / 2;
                    double st0 = _al.Inverse(W(mA.WithZ(0)).XY).Station, st1 = _al.Inverse(W(mB.WithZ(0)).XY).Station;
                    if (st1 < st0) (st0, st1) = (st1, st0);
                    double hw = rb.CurbStirrupWidth / 2 - st.Diameter / 2, hh = rb.CurbStirrupHeight - st.Diameter;
                    Vec3[] Loop(double sta)
                    {
                        var p0 = _al.PointAt(sta, oc);
                        var dir = _al.PointAt(sta, oc + 1) - p0;            // across (toward + offset)
                        double top = _b.Deck.FinishedElevation(_al, sta, c.InnerOffset) + c.Height - rb.CurbTopCover - st.Diameter / 2;
                        Vec3 Pt(double u, double z) => (p0 + dir.Normalized() * u).WithZ(z);
                        return new[] { Pt(-hw, top), Pt(hw, top), Pt(hw, top - hh), Pt(-hw, top - hh), Pt(-hw, top) };
                    }
                    int ns = (int)Math.Floor((st1 - st0) / rb.TransSpacing) + 1;
                    double a0 = st0 + ((st1 - st0) - (ns - 1) * rb.TransSpacing) / 2;
                    for (int i = 0; i < ns; i++) sBars.Add(Loop(a0 + i * rb.TransSpacing));
                    // Corner bars inside the stirrup corners, lapped like the deck bars.
                    foreach (var (u, zz) in new[] { (-1, 0), (1, 0), (-1, 1), (1, 1) })
                    {
                        int np = Math.Max(2, (int)Math.Ceiling((st1 - st0) / Ft(1)));
                        var line = Enumerable.Range(0, np + 1).Select(k =>
                        {
                            var lp = Loop(st0 + (st1 - st0) * k / np);
                            var cnr = zz == 1 ? (u < 0 ? lp[0] : lp[1]) : (u < 0 ? lp[3] : lp[2]);
                            var inward = new Vec3(0, 0, zz == 1 ? -1 : 1) * (st.Diameter / 2 + dA / 2);
                            var side = (u < 0 ? lp[1] - lp[0] : lp[0] - lp[1]).Normalized() * (st.Diameter / 2 + dA / 2);
                            return cnr + inward + side;
                        }).ToList();
                        cBars.AddRange(SplitBars(line, rb.LongBarLength, rb.LongLap));
                    }
                }
                var sShape = new BarShape { Code = "STR", Description = "Closed curb stirrup",
                    Centerline = new[] { new Vec2(0, 0), new Vec2(rb.CurbStirrupWidth, 0), new Vec2(rb.CurbStirrupWidth, rb.CurbStirrupHeight), new Vec2(0, rb.CurbStirrupHeight), new Vec2(0, 0) } };
                AddBars(rb.CurbStirrupMark, st, "Arch curb stirrups (p.152)", sBars, sShape);
                AddBars(rb.CurbLongMark, sz, "Arch curb longitudinal (p.152)", cBars);
            }
            if (rb.Unresolved.Length > 0) Warn(rb.Unresolved);
        }

        /// <summary>A polyline cut into bars of the given length overlapping by the lap, from its start; the last bar takes the remainder.</summary>
        private static List<Vec3[]> SplitBars(List<Vec3> line, double barLength, double lap)
        {
            var cum = new List<double> { 0 };
            for (int i = 1; i < line.Count; i++) cum.Add(cum[^1] + (line[i] - line[i - 1]).Length);
            double total = cum[^1];
            Vec3 At(double s)
            {
                int i = cum.BinarySearch(s); if (i < 0) i = ~i; i = Math.Clamp(i, 1, cum.Count - 1);
                double t = (s - cum[i - 1]) / Math.Max(1e-12, cum[i] - cum[i - 1]);
                return line[i - 1] + (line[i] - line[i - 1]) * t;
            }
            var res = new List<Vec3[]>();
            for (double a = 0; a < total - 1e-6; a += barLength - lap)
            {
                double b = Math.Min(total, a + barLength);
                var pts = new List<Vec3> { At(a) };
                for (int i = 0; i < cum.Count; i++) if (cum[i] > a + 1e-6 && cum[i] < b - 1e-6) pts.Add(line[i]);
                pts.Add(At(b));
                res.Add(pts.ToArray());
                if (b >= total - 1e-6) break;
            }
            return res;
        }

        /// <summary>Plan bar list vs the modeled marks (count / length).</summary>
        private void CheckBarList(ArchDeckRebar rb)
        {
            foreach (var pl in rb.PlanList)
            {
                var mk = Model.Concrete.Marks.FirstOrDefault(x => x.Mark == pl.Name);
                double planCount = pl.X, planLen = pl.Y;
                Model.Checks.Add(new ArchCheck($"deck bar {pl.Name}: count / length (p.154)", $"{planCount:0} x {L(planLen)}",
                    mk == null ? "not modeled" : $"{mk.Count} x {L(mk.Length)}{(mk.Varies ? $" (varies, min {L(mk.MinLength)})" : "")}",
                    mk == null ? planCount : mk.Count - planCount, mk != null && (int)planCount == mk.Count && Math.Abs(mk.Length - planLen) < In(1)));
            }
        }

        /// <summary>PT tendon solids (p.155): each tendon from the end web opening of one knuckle through the floorbeam slots to the other; strands side by side
        /// in the flat duct, flat anchorage envelopes against the end web inside faces, S2 anchorage-zone bars.</summary>
        private void BuildPt(ArchDeckDefinition dk, ArchPtSystem pt)
        {
            Model.PtStrandDiameter = pt.StrandDiameter; Model.PtStrandArea = pt.StrandArea;
            var asu = pt.Unresolved.Length > 0 ? Assumption.Unresolved(pt.Unresolved, $"strand spacing {L(pt.StrandSpacing)}, anchorage {L(pt.AnchorWidth)} x {L(pt.AnchorHeight)} x {L(pt.AnchorLength)}") : null;
            if (asu != null) Warn(pt.Unresolved);
            var zs = new List<Vec3[]>();
            foreach (var t in dk.Tendons)
            {
                if (t.Path.Count < 2) continue;
                var path = t.Path.Select(p => p.Vec).ToList();
                // Ends: the nearest end web PT opening of the nearest knuckle (inside face of the end web).
                Vec3? Opening(Vec3 near)
                {
                    Vec3? best = null; double bd = double.MaxValue;
                    foreach (var k in _d.Knuckles)
                    {
                        if (EndLine(k) is not { } el) continue;
                        double zef = k.EndFlangeBottomZ + k.EndFlangeThickness;
                        foreach (double x in k.EndWebOpeningsAt)
                        {
                            var q = (el.ctr + el.t * x + el.n * (-k.EndWebOffset + k.EndWebThickness)).WithZ(zef + k.EndWebOpeningZ);
                            double d = (q - near).Length;
                            if (d < bd) { bd = d; best = q; }
                        }
                    }
                    return bd < Ft(25) ? best : null;
                }
                if (Opening(path[0]) is { } o0) path.Insert(0, o0);
                if (Opening(path[^1]) is { } o1) path.Add(o1);
                string asm = $"PT-{t.Name}";
                AddAssembly(asm, AssemblyKind.ArchDeckForm, $"Longitudinal PT tendon {t.Name}: {pt.StrandsPerTendon} - {L(pt.StrandDiameter)} strands, {pt.JackingForceKips:0} kips jacking (p.155)");
                for (int i = 0; i < pt.StrandsPerTendon; i++)
                {
                    double off = (i - (pt.StrandsPerTendon - 1) / 2.0) * pt.StrandSpacing;
                    var pts = new List<Vec3>();
                    for (int k = 0; k < path.Count; k++)
                    {
                        var d = (path[Math.Min(path.Count - 1, k + 1)] - path[Math.Max(0, k - 1)]).XY.Normalized();
                        pts.Add(W(path[k] + d.Left.WithZ(0) * off));
                    }
                    var m = new Mesh();
                    TubeBuilder.AddMitredTube(m, pts, pt.StrandDiameter / 2, 8);
                    double len = Enumerable.Range(1, pts.Count - 1).Sum(k => (pts[k] - pts[k - 1]).Length);
                    AddPart($"{asm}-S{i + 1}", PartCategory.PtStrand, asm, $"{L(pt.StrandDiameter)} PT strand {i + 1} of {t.Name}", m, pt.StrandMaterial, len, 0, asu);
                }
                foreach (var (p0, p1) in new[] { (path[0], path[1]), (path[^1], path[^2]) })
                {
                    var dx = (p1 - p0).XY.Normalized().WithZ(0);
                    var fr = new Frame3(W(p0), Wd(dx), Wd(dx.XY.Left.WithZ(0)), _fr.Z);
                    var m = new Mesh();
                    MeshBuilder.AddBox(m, fr, 0, pt.AnchorLength, -pt.AnchorWidth / 2, pt.AnchorWidth / 2, -pt.AnchorHeight / 2, pt.AnchorHeight / 2);
                    AddPart($"{asm}-A{(p0 == path[0] ? 1 : 2)}", PartCategory.PtAnchorage, asm, $"PT flat anchorage of {t.Name} (envelope)", m, pt.AnchorMaterial, pt.AnchorLength, 0, asu);
                    foreach (double at in pt.ZoneBarsAt)
                    {
                        var c = p0 + dx * at;
                        var across = dx.XY.Left.WithZ(0);
                        double hw = pt.ZoneBarWidth / 2, hh = pt.ZoneBarHeight / 2;
                        Vec3 Q(double u, double v) => W(c + across * u + new Vec3(0, 0, v));
                        zs.Add(new[] { Q(-hw, -hh), Q(hw, -hh), Q(hw, hh), Q(-hw, hh), Q(-hw, -hh) });
                    }
                }
            }
            if (zs.Count > 0)
            {
                var shape = new BarShape { Code = "STR", Description = "Closed anchorage zone bar",
                    Centerline = new[] { new Vec2(0, 0), new Vec2(pt.ZoneBarWidth, 0), new Vec2(pt.ZoneBarWidth, pt.ZoneBarHeight), new Vec2(0, pt.ZoneBarHeight), new Vec2(0, 0) } };
                AddBars(pt.ZoneBarMark, RebarCatalog.Size(pt.ZoneBarSize), "Arch PT anchorage zone (p.155)", zs, shape);
            }
        }
    }
}
