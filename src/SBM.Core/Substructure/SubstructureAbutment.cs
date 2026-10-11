using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Substructure;

/// <summary>
/// As-designed abutment (<see cref="AbutmentDefinition"/>): footing, backwall pours (lower / upper with the corbel / device pour), beam steps,
/// maskwalls, backwall extension, piles at the plan stations, wingwalls with counterforts and piled wing footings. Cap coordinates as
/// elsewhere: S along the support line from the alignment point, N normal (up-station +); section distances x run from the CL bearing
/// toward the span (N = Sigma * x). Plan end lines are alignment offsets, so their S depends on N (they are parallel to the alignment).
/// </summary>
public sealed partial class SubstructureGenerator
{
    private delegate double Plane(double n);

    /// <summary>A section vertex of a prism between two planes: its N and the elevation as a function of S.</summary>
    private readonly record struct SV(double N, Func<double, double> Z);

    /// <summary>
    /// Solid between two vertical planes (S = plane(N)) whose section (vertices in order around it, at constant N each) has elevations that are
    /// piecewise linear in S (each vertex follows its own line); intermediate rings at the given constant-S breaks keep every face planar.
    /// </summary>
    private static void AddSlab(Mesh mesh, SubstructureUnit unit, IReadOnlyList<SV> verts, Plane a, Plane b, IEnumerable<double> breaks)
    {
        var sec = verts.Select(v => new Vec2(v.N, v.Z(a(v.N)))).ToList();
        double maxA = verts.Max(v => a(v.N)), minB = verts.Min(v => b(v.N));
        var cuts = breaks.Where(x => x > maxA + 1e-6 && x < minB - 1e-6).Distinct().OrderBy(x => x).ToList();
        var rings = new List<Vec3[]>();
        Vec3[] Ring(Plane p) => verts.Select(v => { double s = p(v.N); return unit.World(s, v.N, v.Z(s)); }).ToArray();
        rings.Add(Ring(a));
        foreach (double c in cuts) rings.Add(Ring(_ => c));
        rings.Add(Ring(b));
        MeshBuilder.AddLoft(mesh, sec, rings);
    }

    private static List<Vec2> PolyPts(IEnumerable<CapProfilePoint> p) => p.OrderBy(x => x.S).Select(x => new Vec2(x.S, x.Elevation)).ToList();

    /// <summary>Distance rearward along an offset line from the CL bearing line: u (m) to station and back.</summary>
    private sealed class OffsetPath
    {
        public readonly List<(double u, double sta)> T = new();
        public OffsetPath(Alignment a, double sta0, double dirSta, double e, double uMax)
        {
            double u = 0, sta = sta0;
            var prev = a.PointAt(sta, e);
            T.Add((0, sta0));
            while (u < uMax)
            {
                sta += dirSta * 0.5;
                var p = a.PointAt(sta, e);
                u += (p - prev).Length;
                prev = p;
                T.Add((u, sta));
            }
        }
        public double Sta(double u)
        {
            if (u <= 0) return T[0].sta + (T[1].sta - T[0].sta) * u / Math.Max(T[1].u, 1e-9);
            for (int i = 1; i < T.Count; i++)
                if (u <= T[i].u) return T[i - 1].sta + (T[i].sta - T[i - 1].sta) * (u - T[i - 1].u) / Math.Max(T[i].u - T[i - 1].u, 1e-12);
            return T[^1].sta;
        }
        public double U(double sta)
        {
            double d = Math.Sign(T[1].sta - T[0].sta);
            for (int i = 1; i < T.Count; i++)
                if ((sta - T[i].sta) * d <= 0) return T[i - 1].u + (T[i].u - T[i - 1].u) * (sta - T[i - 1].sta) / (T[i].sta - T[i - 1].sta);
            return T[^1].u;
        }
    }

    private SubstructureUnit BuildDesignedAbutment(int j, SubstructureUnit unit, SubstructureDefinition sd, SupportLineResult sl, List<Seat> seats)
    {
        var ad = sd.Abutment!;
        var a = _f.Alignment;
        var def = _f.Definition;
        var sup = def.SupportAt(j);
        string name = sl.Name;
        double sg = j == 0 ? 1 : -1;                        // span side: +N for the first support, -N for the last
        var R = a.RightNormalAt(unit.Station);
        double dr = Vec2.Dot(unit.D, R), nr = Vec2.Dot(unit.N, R);
        if (Math.Abs(dr) < 0.2) throw new InvalidOperationException("the support line is nearly parallel to the alignment");
        double Offs(double s, double n) => a.Inverse(unit.Plan(s, n)).Offset;
        double SolveS(double e, double n)
        {
            double s = (e - nr * n) / dr;
            for (int i = 0; i < 6; i++) { double f = Offs(s, n) - e; if (Math.Abs(f) < 1e-8) break; s -= f / dr; }
            return s;
        }
        Plane EndPlane(double e) => n => SolveS(e, n);
        Plane ConstPlane(double c) => _ => c;

        var fbPts = PolyPts(ad.FootingBottom);
        var topPts = PolyPts(ad.BackwallTop);
        if (fbPts.Count == 0) throw new InvalidOperationException("the abutment needs a footing-bottom elevation");
        if (topPts.Count == 0) throw new InvalidOperationException("the abutment needs a backwall top (crown line) elevation");
        double FB(double s) => SubstructureUnit.Interp(fbPts, s);
        double Crown(double s) => SubstructureUnit.Interp(topPts, s);

        double xBwF = -ad.BackwallFrontToBearing, xBwB = xBwF - ad.BackwallThickness, xFB = xBwB - ad.FootingBackToBackwallBack, xFF = ad.BearingToFootingFront;
        double xStepF = ad.BearingToStepFront;
        double D = ad.FootingDepth, W = ad.WashHeight, K = ad.BackwallBaseAboveFootingBottom;
        double Nx(double x) => sg * x;
        double cjD = ad.LedgeBelowTop + ad.CorbelVertical + ad.CorbelTaperRise + ad.JointBelowTaper;
        double nMid = Nx((xFB + xFF) / 2);

        if (!double.IsNaN(ad.PlanFootingWidth) && Math.Abs(ad.FootingWidth - ad.PlanFootingWidth) > In(0.25))
            Warn($"{name}: footing width from the section chain is {Sh(ad.FootingWidth)} but the plan prints {Sh(ad.PlanFootingWidth)}.");

        // ---- plan planes
        var leftEnd = EndPlane(ad.LeftEndOffset);
        var rightEnd = EndPlane(ad.RightEndOffset);
        if (leftEnd(nMid) >= rightEnd(nMid)) throw new InvalidOperationException("the left end offset must be less than the right end offset");
        Plane bwL = leftEnd, bwR = rightEnd;
        var maskL = ad.Masks.FirstOrDefault(m => m.End == AbutmentEnd.Left);
        var maskR = ad.Masks.FirstOrDefault(m => m.End == AbutmentEnd.Right);
        if (!double.IsNaN(ad.BackwallLeftOffset)) bwL = EndPlane(ad.BackwallLeftOffset);
        else if (maskL != null) bwL = EndPlane(ad.LeftEndOffset + maskL.Setback + maskL.Thickness);
        if (!double.IsNaN(ad.BackwallRightOffset)) bwR = EndPlane(ad.BackwallRightOffset);
        else if (maskR != null) bwR = EndPlane(ad.RightEndOffset - maskR.Setback - maskR.Thickness);
        var ext = ad.Extension;

        // ---- footing plan outline: notches between a face and the end line follow a line of constant STATION (West p86); the kinks need section vertices
        (double s, double n) AtStaOff(double sta, double e) { var p = a.PointAt(sta, e); return (Vec2.Dot(p - unit.Origin, unit.D), Vec2.Dot(p - unit.Origin, unit.N)); }
        Plane StationPlane(double sta) => n =>
        {
            double s = AtStaOff(sta, 0).s;
            for (int i = 0; i < 12; i++)
            {
                var r = a.Inverse(unit.Plan(s, n));
                double f = r.Station - sta;
                if (Math.Abs(f) < 1e-9) break;
                double d = Vec2.Dot(unit.D, Vec2.FromAngle(a.AngleAt(r.Station)));
                s -= f / (Math.Abs(d) < 0.2 ? 0.2 * Math.Sign(d == 0 ? 1 : d) : d);
            }
            return s;
        };
        var kinks = new List<double>();                        // x of the section vertices the notched end planes need
        var addOns = new List<(bool front, double e, List<Vec2> poly)>();     // outline parts beyond the faces (end line longer than the face-to-face parallelogram)
        Plane Notched(Plane basic, double e, FootingNotchDefinition? nt)
        {
            if (nt == null) return basic;
            double xTf = double.NaN, xTb = double.NaN;
            Plane pf = basic, pb = basic;
            if (!double.IsNaN(nt.FrontStation)) { xTf = sg * AtStaOff(nt.FrontStation, e).n; pf = StationPlane(nt.FrontStation); }
            if (!double.IsNaN(nt.BackStation)) { xTb = sg * AtStaOff(nt.BackStation, e).n; pb = StationPlane(nt.BackStation); }
            bool remF = !double.IsNaN(xTf) && xTf < xFF - 1e-6, remB = !double.IsNaN(xTb) && xTb > xFB + 1e-6;
            if (remF) kinks.Add(xTf);
            if (remB) kinks.Add(xTb);
            void AddOn(bool front, double sta, double xT)
            {
                double nFace = Nx(front ? xFF : xFB);
                var p3 = AtStaOff(sta, e);
                addOns.Add((front, e, new List<Vec2> { new Vec2(StationPlane(sta)(nFace), nFace), new Vec2(basic(nFace), nFace), new Vec2(p3.s, p3.n) }));
            }
            if (!double.IsNaN(xTf) && !remF && xTf > xFF + 1e-6) AddOn(true, nt.FrontStation, xTf);
            if (!double.IsNaN(xTb) && !remB && xTb < xFB - 1e-6) AddOn(false, nt.BackStation, xTb);
            return n =>
            {
                double x = sg * n;
                if (remF && x > xTf) return pf(n);
                if (remB && x < xTb) return pb(n);
                return basic(n);
            };
        }
        var footL = Notched(leftEnd, ad.LeftEndOffset, ad.FootingNotches.FirstOrDefault(k => k.End == AbutmentEnd.Left));
        var footR = Notched(rightEnd, ad.RightEndOffset, ad.FootingNotches.FirstOrDefault(k => k.End == AbutmentEnd.Right));
        // end line extent (N) of each end: the notch intersections (maskwalls span the end line)
        (Vec2 back, Vec2 front) EndsOf(AbutmentEnd end, double e)
        {
            var nt = ad.FootingNotches.FirstOrDefault(k => k.End == end);
            Vec2 At(bool front)
            {
                double st = nt == null ? double.NaN : front ? nt.FrontStation : nt.BackStation;
                if (!double.IsNaN(st)) { var p = AtStaOff(st, e); return new Vec2(p.s, p.n); }
                double nf = Nx(front ? xFF : xFB);
                return new Vec2(SolveS(e, nf), nf);
            }
            return (At(false), At(true));
        }
        double EndX(AbutmentEnd end, bool front)
        {
            var nt = ad.FootingNotches.FirstOrDefault(k => k.End == end);
            double e = end == AbutmentEnd.Left ? ad.LeftEndOffset : ad.RightEndOffset;
            double st = nt == null ? double.NaN : front ? nt.FrontStation : nt.BackStation;
            return double.IsNaN(st) ? (front ? xFF : xFB) : sg * AtStaOff(st, e).n;
        }
        double s0Mid = leftEnd(nMid), s1Mid = rightEnd(nMid);

        var geo = new AbutmentGeometry
        {
            Def = ad, Sigma = sg, XFootBack = xFB, XBwBack = xBwB, XBwFront = xBwF, XStepFront = xStepF, XFootFront = xFF,
            FootingBottom = FB, BackwallTop = Crown, LeftEndS = n => leftEnd(n), RightEndS = n => rightEnd(n),
            BwLeftS = n => bwL(n), BwRightS = n => bwR(n), JointDepth = cjD,
        };
        unit.Abutment = geo;
        unit.CapS0 = s0Mid; unit.CapS1 = s1Mid;
        unit.CapN0 = Math.Min(Nx(xFB), Nx(xFF)); unit.CapN1 = Math.Max(Nx(xFB), Nx(xFF)); unit.CapMidN = nMid;
        var capBreaks = fbPts.Select(p => p.X).Concat(topPts.Select(p => p.X)).Where(x => x > s0Mid && x < s1Mid).Distinct().OrderBy(x => x).ToList();
        foreach (double s in new[] { s0Mid }.Concat(capBreaks).Append(s1Mid))
        {
            unit.CapTop.Add(new Vec2(s, FB(s) + D));
            unit.CapBottom.Add(new Vec2(s, FB(s)));
        }
        unit.CapTopDescription = "as-designed abutment footing";
        var bwTop = new[] { s0Mid }.Concat(topPts.Select(p => p.X).Where(x => x > s0Mid && x < s1Mid)).Append(s1Mid).Select(s => new Vec2(s, Crown(s))).ToList();
        unit.Backwall = (Math.Min(Nx(xBwB), Nx(xBwF)), Math.Max(Nx(xBwB), Nx(xBwF)), bwTop);

        // ---- footing (three pours' worth of cross-section: plain end zones, keyed under the backwall); notched ends have extra section vertices at the kinks
        var footMesh = new Mesh();
        {
            var plain = new List<(double x, double h)> { (xFB, 0), (xFF, 0), (xFF, D + W), (xBwF, D + W), (xBwF, D), (xFB, D) };
            var keyed = new List<(double x, double h)> { (xFB, 0), (xFF, 0), (xFF, D + W), (xBwF, D + W), (xBwF, K), (xBwB, K), (xBwB, D), (xFB, D) };
            static List<(double x, double h)> WithKinks(List<(double x, double h)> poly, IEnumerable<double> xs)
            {
                var r = poly.ToList();
                foreach (double xk in xs)
                    for (int i = 0; i < r.Count; i++)
                    {
                        var p0 = r[i]; var p1 = r[(i + 1) % r.Count];
                        if (Math.Abs(p0.h - p1.h) < 1e-12 && xk > Math.Min(p0.x, p1.x) + 1e-9 && xk < Math.Max(p0.x, p1.x) - 1e-9) { r.Insert(i + 1, (xk, p0.h)); break; }
                    }
                return r;
            }
            SV[] V(List<(double x, double h)> poly) => WithKinks(poly, kinks).Select(p => new SV(Nx(p.x), s => FB(s) + p.h)).ToArray();
            var fb = fbPts.Select(p => p.X).ToList();
            bool leftZone = Math.Abs(bwL(nMid) - leftEnd(nMid)) > 1e-4, rightZone = Math.Abs(bwR(nMid) - rightEnd(nMid)) > 1e-4;
            if (leftZone) AddSlab(footMesh, unit, V(plain), footL, bwL, fb);
            AddSlab(footMesh, unit, V(keyed), leftZone ? bwL : footL, rightZone ? bwR : footR, fb);
            if (rightZone) AddSlab(footMesh, unit, V(plain), bwR, footR, fb);
            foreach (var (front, _, poly) in addOns)
            {
                double zb = poly.Average(q => FB(q.X));
                if (poly.Max(q => FB(q.X)) - poly.Min(q => FB(q.X)) > In(0.125))
                    Warn($"{name}: footing outline corner piece spans a sloping footing bottom; built level at the mean bottom {El(zb)}.");
                MeshBuilder.AddExtrusion(footMesh, poly, Frame(unit, 0, 0), zb, zb + D + (front ? W : 0));
            }
        }
        unit.Solids.Add(new SubSolid(SubElementKind.Footing, $"{name} footing",
            $"Abutment footing {L(s1Mid - s0Mid)} long (along the line), {Sh(ad.FootingWidth)} wide x {Sh(D)} deep (+{Sh(W)} wash in front of the backwall), ends parallel to the alignment{(ad.FootingNotches.Count > 0 ? " with the plan's end notches" : "")}, as designed", footMesh));

        // ---- backwall: lower pour, upper pour with the corbel, device pour above the ledge
        var bw = new List<double>(topPts.Select(p => p.X)).Concat(fbPts.Select(p => p.X)).ToList();
        {
            var lower = new Mesh();
            AddSlab(lower, unit, new[]
            {
                new SV(Nx(xBwF), s => FB(s) + K), new SV(Nx(xBwB), s => FB(s) + K),
                new SV(Nx(xBwB), s => Crown(s) - cjD), new SV(Nx(xBwF), s => Crown(s) - cjD),
            }, bwL, bwR, bw);
            unit.Solids.Add(new SubSolid(SubElementKind.Backwall, $"{name} backwall (lower)", $"Backwall below the construction joint, {Sh(ad.BackwallThickness)} thick, keyed into the footing, joint {Sh(cjD)} under the crown line", lower));

            double L1 = ad.LedgeBelowTop, Cv = ad.CorbelVertical, Tr = ad.CorbelTaperRise, P = ad.CorbelProjection;
            var upperPoly = new (double x, double d)[] { (xBwF, cjD), (xBwF, L1), (xBwB, L1), (xBwB - P, L1), (xBwB - P, L1 + Cv), (xBwB, L1 + Cv + Tr), (xBwB, cjD) };
            var upper = new Mesh();
            AddSlab(upper, unit, upperPoly.Select(p => new SV(Nx(p.x), s => Crown(s) - p.d)).ToArray(), bwL, bwR, bw);
            unit.Solids.Add(new SubSolid(SubElementKind.Backwall, $"{name} backwall (upper, corbel)", $"Upper backwall pour with the corbel / paving notch: ledge {Sh(L1)} under the top, corbel {Sh(P)} projection, {Sh(Cv)} vertical face, {Sh(Tr)} 45 deg taper", upper));

            var cap = new Mesh();
            AddSlab(cap, unit, new[]
            {
                new SV(Nx(xBwF), s => Crown(s) - L1), new SV(Nx(xBwF), s => Crown(s)), new SV(Nx(xBwB), s => Crown(s)), new SV(Nx(xBwB), s => Crown(s) - L1),
            }, bwL, bwR, bw);
            unit.Solids.Add(new SubSolid(SubElementKind.Backwall, $"{name} backwall (device pour)", $"Top {Sh(L1)} of the backwall, poured after the expansion device is set", cap));
        }
        double gap = ad.BackwallFrontToBearing - sup.DeckEndOffset;
        Note($"{name}: backwall front face {Sh(ad.BackwallFrontToBearing)} behind the CL bearing; deck end {Sh(sup.DeckEndOffset)} from the CL bearing, so {Sh(gap)} between the deck end and the backwall face (plans: 6\" at 50 F).");
        if (Math.Abs(gap - sd.BackwallGap) > In(0.125))
            Warn($"{name}: the as-designed backwall front is {Sh(gap)} from the deck end but BackwallGap is {Sh(sd.BackwallGap)}; the as-designed abutment dimension governs (BackwallGap is not used).");

        // ---- backwall extension: the backwall strip continues BEYOND the footing end line to a cap perpendicular to the strip, bottom at the footing-bottom line
        if (ext != null)
        {
            var em = new Mesh();
            double nBw = Nx((xBwF + xBwB) / 2);
            bool extR = ext.End == AbutmentEnd.Right;
            double sCap = extR ? rightEnd(nBw) + ext.Width : leftEnd(nBw) - ext.Width;
            Plane pa = extR ? rightEnd : ConstPlane(sCap);
            Plane pb = extR ? ConstPlane(sCap) : leftEnd;
            double zt = ext.TopElevation;
            AddSlab(em, unit, new[]
            {
                new SV(Nx(xBwF), s => FB(s)), new SV(Nx(xBwB), s => FB(s)), new SV(Nx(xBwB), _ => zt), new SV(Nx(xBwF), _ => zt),
            }, pa, pb, fbPts.Select(p => p.X));
            unit.Solids.Add(new SubSolid(SubElementKind.Backwall, $"{name} backwall extension", $"Backwall extension {Sh(ext.Width)} long beyond the footing end line at the {ext.End.ToString().ToLowerInvariant()} end (perpendicular cap), from the footing-bottom line to the level top {El(zt)}", em));
            geo.Extension = (ext.End, extR ? rightEnd(nBw) : sCap, extR ? sCap : leftEnd(nBw), zt);
        }

        // ---- maskwalls: the OUTERMOST part of the wall zone, outer face at Setback (plans: 0 = flush) from the footing end line; they span the end line of the outline
        foreach (var mk in ad.Masks)
        {
            bool left = mk.End == AbutmentEnd.Left;
            double eo = left ? ad.LeftEndOffset + mk.Setback : ad.RightEndOffset - mk.Setback;
            double ei = left ? eo + mk.Thickness : eo - mk.Thickness;
            Plane pa = EndPlane(left ? eo : ei), pb = EndPlane(left ? ei : eo);
            double sEnd = left ? leftEnd(nMid) : rightEnd(nMid);
            double zBase = FB(sEnd) + D + W, zt = zBase + mk.Height;
            double xMB = EndX(mk.End, false), xMF = EndX(mk.End, true);
            var mm = new Mesh();
            AddSlab(mm, unit, new[]
            {
                new SV(Nx(xMB), s => FB(s) + D), new SV(Nx(xBwF), s => FB(s) + D), new SV(Nx(xBwF), s => FB(s) + D + W), new SV(Nx(xMF), s => FB(s) + D + W),
                new SV(Nx(xMF), _ => zt), new SV(Nx(xMB), _ => zt),
            }, pa, pb, fbPts.Select(p => p.X));
            double width = Math.Abs(SolveS(left ? eo : ei, Nx(xMF)) - SolveS(left ? eo : ei, Nx(xMB)));
            unit.Solids.Add(new SubSolid(SubElementKind.Maskwall, $"{name} maskwall {(left ? "left" : "right")}",
                $"End panel {Sh(mk.Thickness)} thick, {Sh(mk.Height)} above the footing top (top {El(zt)}), spanning the end line ({Sh(width)} along the wall face), {Sh(mk.Setback)} from the footing end line", mm));
            geo.Masks.Add(new MaskInfo(mk.End, eo, ei, zt, zBase) { EndsAt = e => EndsOf(mk.End, e) });
        }

        // ---- beam steps (seat blocks) in front of the backwall, one per bearing
        foreach (var seat in seats.Where(x => x.Girder < 1000).OrderBy(x => x.S))
        {
            var sdef = ad.Steps.FirstOrDefault(x => x.Girder == seat.Girder + 1);
            if (sdef == null) { Warn($"{name}: no beam step top elevation given for girder G{seat.Girder + 1}; no step built there."); continue; }
            double s0 = seat.S - ad.StepLength / 2, s1 = seat.S + ad.StepLength / 2, top = sdef.TopElevation;
            if (Math.Abs(top - seat.Z) > In(0.125))
                Warn($"{name}: beam step G{seat.Girder + 1} top {El(top)} (plan) differs from the bearing seat {El(seat.Z)} by {Sh(Math.Abs(top - seat.Z))} - the step follows the plan; check the bearing stack.");
            double baseMax = Math.Max(FB(s0), FB(s1)) + D + W;
            if (top <= Math.Min(FB(s0), FB(s1)) + D + W + 1e-4) { Warn($"{name}: beam step G{seat.Girder + 1} top {El(top)} is not above the seat base ({El(baseMax)}); no step built."); continue; }
            var sm = new Mesh();
            AddSlab(sm, unit, new[]
            {
                new SV(Nx(xBwF), s => FB(s) + D + W), new SV(Nx(xStepF), s => FB(s) + D + W), new SV(Nx(xStepF), _ => top), new SV(Nx(xBwF), _ => top),
            }, ConstPlane(s0), ConstPlane(s1), fbPts.Select(p => p.X));
            double hMid = top - (FB(seat.S) + D + W);
            unit.Solids.Add(new SubSolid(SubElementKind.Pedestal, $"{name} beam step G{seat.Girder + 1}",
                $"Beam step {Sh(ad.StepLength)} x {Sh(xStepF - xBwF)}, top {El(top)}, {Sh(hMid)} above the wash at the girder", sm));
            geo.Steps.Add(new AbutmentStepInfo(seat.Girder, seat.S, s0, s1, top, FB(seat.S) + D + W, Math.Min(Nx(xBwF), Nx(xStepF)), Math.Max(Nx(xBwF), Nx(xStepF))));
            unit.Pedestals.Add(new PedestalInfo(seat.Girder, seat.S, Nx((xBwF + xStepF) / 2), ad.StepLength, xStepF - xBwF, FB(seat.S) + D + W, top));
        }

        // ---- main footing foundation and piles at the plan stations (two rows normal to the line)
        var main = new FoundationInfo { Type = FoundationType.PileCap, S = (s0Mid + s1Mid) / 2, N = nMid, TopZ = FB((s0Mid + s1Mid) / 2) + D, BottomZ = FB((s0Mid + s1Mid) / 2), SizeS = s1Mid - s0Mid, SizeN = ad.FootingWidth };
        unit.Foundations.Add(main);
        geo.PileStationsS = ad.PileStations.ToArray();
        string where = $"{name} pile group 1";
        foreach (double ps in ad.PileStations.OrderBy(x => x))
            foreach (double xr in new[] { -ad.PileBackRowToBearing, ad.PileFrontRowToBearing })
            {
                double pn = Nx(xr);
                if (ps < leftEnd(pn) + In(6) || ps > rightEnd(pn) - In(6))
                    Warn($"{name}: pile at S {L(ps)}, x {Sh(xr)} is within 6\" of (or beyond) the footing end line.");
                geo.PileXY.Add((ps, pn));
                AddPilesAt(unit, sd, main, 0, 0, FB(ps), new List<FootingPoint> { new(ps, pn) }, ad.PileLength, 0, where, embedment: ad.PileTopAboveFootingBottom);
            }

        // ---- wingwalls, counterforts, wing footings with piles
        foreach (var wd in ad.Wings) BuildWing(unit, sd, geo, wd, a, sg, name, Nx(xBwB), SolveS);

        Note($"{name}: as-designed abutment, footing {L(s1Mid - s0Mid)} along the line, {ad.PileStations.Count * 2} footing piles + {unit.Foundations.Skip(1).Sum(f => f.Piles.Count)} wing piles, {ad.Wings.Count} wingwalls ({ad.Source}).");
        return unit;
    }

    private void BuildWing(SubstructureUnit unit, SubstructureDefinition sd, AbutmentGeometry geo, WingwallDefinition wd, Alignment a, double sg, string name,
        double nBack, Func<double, double, double> solveS)
    {
        var ad = geo.Def;
        double side = wd.GutterOffset < 0 ? -1 : 1;                 // outboard direction of the wall in offset
        double eIn = wd.GutterOffset, eOut = eIn + side * wd.Thickness;
        double dirSta = -sg;                                       // rearward = away from the span
        double sCl = solveS(eIn, 0);
        double sta0 = a.Inverse(unit.Plan(sCl, 0)).Station;
        var path = new OffsetPath(a, sta0, dirSta, eIn, wd.Length + Ft(8));
        double NOf(Vec2 p) => Vec2.Dot(p - unit.Origin, unit.N);
        double CrossSta(double e)
        {
            double lo = sta0 - dirSta * 3, hi = sta0 + dirSta * 12;
            double g(double s) => NOf(a.PointAt(s, e)) - nBack;
            double glo = g(lo);
            for (int i = 0; i < 60; i++)
            {
                double mid = (lo + hi) / 2, gm = g(mid);
                if (Math.Sign(gm) == Math.Sign(glo)) { lo = mid; glo = gm; } else hi = mid;
            }
            return (lo + hi) / 2;
        }
        double sIn = CrossSta(eIn), sOut = CrossSta(eOut);
        double ub = path.U(sIn);
        double len = wd.Length;
        double bEnd = double.IsNaN(wd.BottomAtEnd) ? wd.TopAtEnd - wd.EndHeight : wd.BottomAtEnd;
        double Top(double u) => wd.TopAtBackwall + (wd.TopAtEnd - wd.TopAtBackwall) * (u - ub) / Math.Max(len - ub, 1e-9);
        double lvl = ub + wd.LevelReach;
        double Bot(double u) => u <= lvl ? wd.BottomAtBackwall : wd.BottomAtBackwall + (bEnd - wd.BottomAtBackwall) * (u - lvl) / Math.Max(len - lvl, 1e-9);
        var info = new WingInfo { Def = wd, SideSign = side, EIn = eIn, EOut = eOut, UBack = ub, Length = len, Sta = path.Sta, Top = Top, Bottom = Bot };
        geo.Wings.Add(info);

        // rings: [near end at the backwall back face] .. breaks .. [free end]
        var us = new List<double> { ub };
        if (lvl > ub + 1e-6 && lvl < len - 1e-6) us.Add(lvl);
        us.Add(len);
        var uu = new List<double>();
        for (int i = 0; i + 1 < us.Count; i++)
        {
            int n = Math.Max(1, (int)Math.Ceiling((us[i + 1] - us[i]) / 2.7 - 1e-9));
            // straight alignment: a single chunk is exact
            bool straight = Math.Abs(a.AngleAt(path.Sta(us[i])) - a.AngleAt(path.Sta(us[i + 1]))) < 2e-5;
            if (straight) n = 1;
            for (int k = 0; k < n; k++) uu.Add(us[i] + (us[i + 1] - us[i]) * k / n);
        }
        uu.Add(len);
        var sec = new[] { new Vec2(0, 0), new Vec2(wd.Thickness, 0), new Vec2(wd.Thickness, 1), new Vec2(0, 1) };
        var rings = new List<Vec3[]>();
        Vec3[] RingAt(double staIn, double staOut)
        {
            double uI = path.U(staIn), uO = path.U(staOut);
            var pi = a.PointAt(staIn, eIn); var po = a.PointAt(staOut, eOut);
            return new[] { pi.WithZ(Bot(uI)), po.WithZ(Bot(uO)), po.WithZ(Top(uO)), pi.WithZ(Top(uI)) };
        }
        rings.Add(RingAt(sIn, sOut));
        for (int i = 1; i < uu.Count; i++) { double s = path.Sta(uu[i]); if (uu[i] > ub + 1e-6) rings.Add(RingAt(s, s)); }
        var wm = new Mesh();
        MeshBuilder.AddLoft(wm, sec, rings);
        string wname = string.IsNullOrEmpty(wd.Name) ? $"wing {(wd.End == AbutmentEnd.Left ? "left" : "right")}" : wd.Name;
        unit.Solids.Add(new SubSolid(SubElementKind.Wingwall, $"{name} wingwall {wname}",
            $"Wingwall {Sh(wd.Thickness)} thick outboard of the gutter line, {L(len - ub)} from the backwall face, top {El(wd.TopAtBackwall)} to {El(wd.TopAtEnd)}, bottom {El(wd.BottomAtBackwall)} to {El(bEnd)}", wm));

        // counterforts and wing footings
        int ci = 0;
        foreach (var cf in wd.Counterforts)
        {
            ci++;
            double uc = cf.Start + cf.Width / 2;
            double sta = path.Sta(uc);
            var p0 = a.PointAt(sta, 0);
            var t = Vec2.FromAngle(a.AngleAt(sta));
            var frame = new Frame3(p0.WithZ(0), new Vec3(t.X, t.Y, 0), new Vec3(-t.Y, t.X, 0), new Vec3(0, 0, 1));
            // Offset (right +) is along -y of this frame (left normal): local y = -offset.
            double yw0 = Math.Min(eIn, eOut), yw1 = Math.Max(eIn, eOut);
            double f0 = Math.Min(eIn - side * wd.FootingFillSide, eIn + side * (wd.Thickness + wd.FootingOutside)), f1 = Math.Max(eIn - side * wd.FootingFillSide, eIn + side * (wd.Thickness + wd.FootingOutside));
            double c0 = Math.Min(eIn, eIn - side * cf.Projection), c1 = Math.Max(eIn, eIn - side * cf.Projection);
            double fBot = cf.FootingTop - wd.FootingDepth, cfTop = cf.FootingTop + cf.Height;
            Vec2[] Rect(double x0, double x1, double e0, double e1) => new[] { new Vec2(x0, -e0), new Vec2(x1, -e0), new Vec2(x1, -e1), new Vec2(x0, -e1) };
            var fm = new Mesh();
            MeshBuilder.AddExtrusion(fm, Rect(-cf.Width / 2, cf.Width / 2, f0, f1), frame, fBot, cf.FootingTop);
            unit.Solids.Add(new SubSolid(SubElementKind.WingFooting, $"{name} wing footing {wname} {ci}",
                $"Wing footing {Sh(cf.Width)} along the wall x {Sh(f1 - f0)} across x {Sh(wd.FootingDepth)} deep, top {El(cf.FootingTop)}", fm));
            var cm = new Mesh();
            double bw = cf.BodyWidthResolved;
            MeshBuilder.AddExtrusion(cm, Rect(-bw / 2, bw / 2, c0, c1), frame, cf.FootingTop, cfTop);
            unit.Solids.Add(new SubSolid(SubElementKind.Counterfort, $"{name} counterfort {wname} {ci}",
                $"Counterfort {Sh(bw)} wide x {Sh(cf.Projection)} projection, {Sh(cf.Height)} above the footing, top {El(cfTop)}", cm));
            info.Counterforts.Add(new CounterfortInfo(ci, cf.Start, cf.Width, cf.Projection, cfTop, cf.FootingTop, fBot, yw0, yw1, f0, f1, c0, c1, sta, p0, t) { BodyWidth = bw });
            // wing footing piles: two across the footing, centred
            var foot = new FoundationInfo { Type = FoundationType.PileCap, S = Vec2.Dot(p0 - unit.Origin, unit.D), N = NOf(p0), TopZ = cf.FootingTop, BottomZ = fBot, SizeS = cf.Width, SizeN = f1 - f0 };
            unit.Foundations.Add(foot);
            info.Footings.Add(foot);
            // pile web across the wall (the 7'-0 direction), flanges parallel to the wall: read from the H symbols of the plan legends (p86 / p90 / p81)
            var across = new Vec2(-t.Y, t.X);
            double webS = Vec2.Dot(across, unit.D), webN = Vec2.Dot(across, unit.N);
            double yc = (f0 + f1) / 2;
            for (int pi = 0; pi < 2; pi++)
            {
                // pile 1 = the outside pile
                double e = yc + side * wd.FootingPileSpacing / 2 * (pi == 0 ? 1 : -1);
                var pp = a.PointAt(sta, 0) + (a.PointAt(sta, e) - a.PointAt(sta, 0));
                var pt = new FootingPoint(Vec2.Dot(pp - unit.Origin, unit.D), NOf(pp)) { WebDirS = webS, WebDirN = webN };
                AddPilesAt(unit, sd, foot, 0, 0, fBot, new List<FootingPoint> { pt }, ad.PileLength, 0, $"{name} wing footing {wname} {ci}", embedment: wd.FootingPileTopAbove);
            }
        }
    }
}
