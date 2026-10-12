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
            var m = new List<Vec2>(); var e = new List<Vec2>(); var hw = new List<double>();
            for (int k = 0; k + 1 < dd.knots.Count; k++)
                for (int i = 0; i < 8; i++)
                {
                    double t = i / 8.0;
                    var a = dd.ts.At(dd.knots[k].s + (dd.knots[k + 1].s - dd.knots[k].s) * t).p;
                    var b = dd.tn.At(dd.knots[k].n + (dd.knots[k + 1].n - dd.knots[k].n) * t).p;
                    m.Add((a + b) * 0.5); e.Add((b - a).Normalized()); hw.Add((b - a).Length / 2);
                }
            {
                var a = dd.ts.At(dd.knots[^1].s).p; var b = dd.tn.At(dd.knots[^1].n).p;
                m.Add((a + b) * 0.5); e.Add((b - a).Normalized()); hw.Add((b - a).Length / 2);
            }
            hw.Insert(0, hw[0]); hw.Add(hw[^1]);
            _deckHalfWidth = hw;
            // Extend to the end web inside faces.
            double ext0 = dd.ka is { } k0 ? k0.EndWebOffset - k0.EndWebThickness : 0, ext1 = dd.kb is { } k1 ? k1.EndWebOffset - k1.EndWebThickness : 0;
            var t0 = (m[1] - m[0]).Normalized(); var t1 = (m[^1] - m[^2]).Normalized();
            m.Insert(0, m[0] - t0 * ext0); e.Insert(0, e[0]);
            m.Add(m[^1] + t1 * ext1); e.Add(e[^1]);
            var S = new List<double> { 0 };
            for (int i = 1; i < m.Count; i++) S.Add(S[^1] + (m[i] - m[i - 1]).Length);
            return (m, e, S);
        }

        /// <summary>Half distance between the tie CLs along each <see cref="DeckLine"/> sample (same indices), set by DeckLine.</summary>
        private List<double>? _deckHalfWidth;

        private double HalfWidthAt((List<Vec2> m, List<Vec2> e, List<double> S) dl, double s)
        {
            var hw = _deckHalfWidth!;
            int i = dl.S.BinarySearch(s);
            if (i < 0) i = ~i;
            i = Math.Clamp(i, 1, dl.S.Count - 1);
            double t = (s - dl.S[i - 1]) / Math.Max(1e-12, dl.S[i] - dl.S[i - 1]);
            return hw[i - 1] + (hw[i] - hw[i - 1]) * Math.Clamp(t, 0, 1);
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
            int nT = rb.TransCount > 1 ? rb.TransCount : (int)Math.Floor((Lt - 2 * rb.EndCover) / rb.TransSpacing) + 1;
            double sp = rb.TransCount > 1 ? (Lt - 2 * rb.EndCover) / (nT - 1) : rb.TransSpacing;
            double s0 = (Lt - (nT - 1) * sp) / 2;
            if (rb.TransCount > 1 && sp > rb.TransSpacing + In(1.0 / 32))
                Warn($"Arch deck: {nT} transverse rows (bill count) over {L(Lt - 2 * rb.EndCover)} are {sp / In(1):0.00}\" apart, over the {rb.TransSpacing / In(1):0.##}\" maximum of the deck plan - the bill count is modeled.");
            var hookInfo = new List<(double lap, double dev)>();
            for (int i = 0; i < nT; i++)
            {
                double sRow = s0 + i * sp;
                var (m, e) = AtS(dl, sRow);
                // B: centred on the structure CL.
                bBars.Add(new[] { W((m - e * rb.BottomTransLength / 2).WithZ(zB)), W((m + e * rb.BottomTransLength / 2).WithZ(zB)) });
                // A: centred where the ring crosses CL NHT, following the crown (3 points per foot).
                double Off(double y) => _al.Inverse(W((m + e * y).WithZ(0)).XY).Offset;
                double lo = -rb.BottomTransLength / 2, hi = rb.BottomTransLength / 2, flo = Off(lo);
                for (int it = 0; it < 50; it++) { double md = (lo + hi) / 2, fm = Off(md); if (Math.Sign(fm) == Math.Sign(flo)) { lo = md; flo = fm; } else hi = md; }
                double yc = (lo + hi) / 2;
                if (rb.TopTransPerRow == 2)
                {
                    // two hooked bars per row (p.152): drop leg TopTransHookOffset inside each tie CL, return on top of the bottom bar, top run = listed - drop - return
                    double hw = HalfWidthAt(dl, sRow);
                    double zRet = zB + dA;
                    double inner0 = double.NaN, inner1 = double.NaN;
                    foreach (int side in new[] { -1, 1 })
                    {
                        double yHook = side * (hw - rb.TopTransHookOffset);
                        var top0 = Top(m + e * yHook, rb.TopCover + dA / 2);
                        var bot0 = W((m + e * yHook).WithZ(zRet));
                        double drop = (top0 - bot0).Length;
                        double run = rb.TopTransLength - drop - rb.TopTransHookReturn;
                        double yIn = yHook - side * run;
                        int npt = Math.Max(2, (int)Math.Ceiling(run / Ft(1)) * 2);
                        var pts = new List<Vec3> { W((m + e * (yHook - side * rb.TopTransHookReturn)).WithZ(zRet)), bot0 };
                        for (int k = 0; k <= npt; k++) pts.Add(Top(m + e * (yHook + (yIn - yHook) * k / npt), rb.TopCover + dA / 2));
                        pts.Reverse();
                        aBars.Add(pts.ToArray());
                        if (side < 0) inner0 = yIn; else inner1 = yIn;
                        double dev = 0; for (int k = 1; k < pts.Count; k++) dev += (pts[k] - pts[k - 1]).Length;
                        hookInfo.Add((0, dev));
                    }
                    hookInfo[^1] = (inner0 - inner1, hookInfo[^1].dev);
                }
                else
                {
                    int np = Math.Max(2, (int)Math.Ceiling(rb.TopTransLength / Ft(1)) * 2);
                    aBars.Add(Enumerable.Range(0, np + 1).Select(k => Top(m + e * (yc - rb.TopTransLength / 2 + rb.TopTransLength * k / np), rb.TopCover + dA / 2)).ToArray());
                }
            }
            if (hookInfo.Count > 0)
            {
                var laps = hookInfo.Where(h => h.lap != 0).Select(h => h.lap).ToList();
                Model.Checks.Add(new ArchCheck($"deck bar {rb.TopTransMark}: two hooked bars per row, centre lap (p.152 '2'-8\" min. lap')", $">= {L(Ft(2) + In(8))}",
                    $"{L(laps.Min())} - {L(laps.Max())} (listed {L(rb.TopTransLength)}, hook {L(rb.TopTransHookOffset)} inside the tie CL, return {L(rb.TopTransHookReturn)})",
                    laps.Min() - (Ft(2) + In(8)), laps.Min() >= Ft(2) + In(8) - In(1.0 / 16)));
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
            if (rb.TopTransPerRow == 2)
            {
                var aShape = new BarShape
                {
                    Code = "HOOK", Description = "Top transverse, hooked down at the deck edge with a return lapping the bottom bar (p.152)", FixedLength = rb.TopTransLength,
                    Centerline = new[] { new Vec2(0, 0), new Vec2(Ft(10), 0), new Vec2(Ft(10), -In(5)), new Vec2(Ft(10) - rb.TopTransHookReturn, -In(5)) },
                };
                AddBars(rb.TopTransMark, sz, "Arch deck top transverse, two per row lapped at CL NHT, hooked at the tie (p.152)", aBars, aShape);
            }
            else AddBars(rb.TopTransMark, sz, "Arch deck top transverse (p.152)", aBars);
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
                    int ns = rb.CurbStirrupCount > 1 ? rb.CurbStirrupCount : (int)Math.Floor((st1 - st0) / rb.TransSpacing) + 1;
                    double csp = rb.CurbStirrupCount > 1 ? (st1 - st0) / (ns - 1) : rb.TransSpacing;
                    double a0 = st0 + ((st1 - st0) - (ns - 1) * csp) / 2;
                    for (int i = 0; i < ns; i++) sBars.Add(Loop(a0 + i * csp));
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
            // Edge bars (D3): one per tie per panel between consecutive floorbeam lines, centred, along the tie at EdgeBarOffset inside its CL, bottom bar level,
            // up-hooks with returns at both ends ("BAR D3": 5'-9 + 2 x (5 + 5)).
            if (rb.EdgeBarMark != "" && rb.EdgeBarLength > 0 && _deck is { } d3)
            {
                var dBars = new List<Vec3[]>();
                double zE = zD2, h = rb.EdgeBarHook;
                for (int k = 0; k + 1 < d3.knots.Count; k++)
                    foreach (var (tie, other, s0k, s1k) in new[] { (d3.ts, d3.tn, d3.knots[k].s, d3.knots[k + 1].s), (d3.tn, d3.ts, d3.knots[k].n, d3.knots[k + 1].n) })
                    {
                        double sm = (s0k + s1k) / 2;
                        var (pc, ang) = tie.At(sm);
                        var t = Vec2.FromAngle(ang);
                        var oth = other == d3.ts ? d3.ts.At(d3.knots[k].s).p : d3.tn.At(d3.knots[k].n).p;
                        var inward = t.Left; if (Vec2.Dot(inward, oth - pc) < 0) inward = -inward;
                        var c = pc + inward * rb.EdgeBarOffset;
                        // the bar follows the tie chord between the hook ends (panels are 5'-10 to 6'-1 3/4 on R 342'-4 / 360'-8: the sagitta is 1/64")
                        double half = rb.EdgeBarLength / 2 - dA / 2;
                        Vec3 P(double u, double z) => W((c + t * u).WithZ(z));
                        double ri = half + dA / 2 - h;  // return end: the hook's out-to-out return measured from the outside of the up leg
                        dBars.Add(new[] { P(-ri, zE + h - dA), P(-half, zE + h - dA), P(-half, zE), P(half, zE), P(half, zE + h - dA), P(ri, zE + h - dA) });
                    }
                var dShape = new BarShape
                {
                    Code = "U2H", Description = "Edge bar, up-hooks with returns at both ends (p.154 BAR D3)", FixedLength = rb.EdgeBarLength + 4 * rb.EdgeBarHook,
                    Centerline = new[] { new Vec2(h, h), new Vec2(0, h), new Vec2(0, 0), new Vec2(rb.EdgeBarLength, 0), new Vec2(rb.EdgeBarLength, h), new Vec2(rb.EdgeBarLength - h, h) },
                };
                AddBars(rb.EdgeBarMark, sz, "Arch deck edge bars along the tie webs, one per tie per floorbeam panel (p.152 / 154)", dBars, dShape);
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
