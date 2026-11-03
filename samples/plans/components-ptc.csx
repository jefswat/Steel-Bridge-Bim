// PTC / NJTA DELAWARE RIVER BRIDGE (1954-57, AS-BUILT) - MAIN SPAN JOINT COMPONENTS the joint sheets draw (agent review 2026-11-02, TRUSS-DESIGN.md section 38):
//   1. lug angles 2-Ls 8 x 4 x 1/2 on the web members (p.82, p.84, p.87-89)
//   2. rope suspender support diaphragms at L15-L22 (p.86 "TYPICAL CONNECTION OF ROPE SUSPENDER TO TRUSSES" = Sheet 46, referred to by p.87-89)
//   3. diagonal fills at U21 / L21 (p.89; defined by connections-ptc.csx batch 8b but not built: their extent was measured to 0)
//   4. items read but not built (flags): L12 fills, L14 Section E diaphragm, L1 / L3 strut angles + fills; L22 jacking brackets / jacking scheme / erection bolts = erection
//      equipment, not built (user decision 2026-11-03: L22 in its final condition only)
// Re-runnable (replaces its own inputs); run AFTER details-ptc.csx and BEFORE girder-details-ptc.csx (CLAUDE.md PTC build order):
//   sbm run samples\plans\components-ptc.csx "samples\plans\PTC Delaware River Bridge.sbm"
// The geometry the components sit on (cover plate faces, member ends, gusset inner faces, suspender pins) is read from a regeneration of the main span made
// inside this script. Every scaled value is a dimension-only flag (page, scale, reason); values the sheets do not give are UNRESOLVED; nothing is adjusted.
using SBM.Core;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Steel;
using SBM.Core.Truss;

string outPath = Args.Length > 0 ? Args[0] : (Doc.FilePath ?? @"samples\plans\PTC Delaware River Bridge.sbm");
string U(string pages, string item, string why, string then = "") => UnresolvedFlag.Text(pages, item, why, then);
const string Main = "PTC Arch truss 18-20";
const string Tag = "(components-ptc)";
var report = new List<string>();
var br = Doc.Project.Bridges.First(b => b.Name == Main);
var t = br.Truss!;
void Flag(string key, string? text) { t.Unresolved.RemoveAll(s => s.Contains(key)); if (text != null) t.Unresolved.Add(text); }
string Prime(string m) => System.Text.RegularExpressions.Regex.Replace(m, @"([A-Z]+)(\d+)", x => int.Parse(x.Groups[2].Value) == 22 ? x.Value : x.Value + "'");
string Bare(string n) => n.Contains(':') ? n[(n.IndexOf(':') + 1)..] : n;
string F(double m) => TrussSections.FormatIn(m);

// ---------------------------------------------------------------- own inputs out, then the main span regenerated alone to read the geometry
t.PlaneItems ??= new();
t.PlaneItems.RemoveAll(p => p.Name.StartsWith("CMP-"));
foreach (var j in t.Joints) j.Plates?.RemoveAll(p => p.Source.Contains(Tag));
var fillFix = new (string pt, string mem)[] { ("U21", "U21-L22"), ("L21", "U20-L21") };
foreach (var (pt0, mem0) in fillFix)
    foreach (bool pr in new[] { false, true })
    {
        string pt = pr ? pt0 + "'" : pt0, mem = pr ? Prime(mem0) : mem0;
        foreach (var j in t.Joints.Where(j => j.Point == pt)) j.Plates?.RemoveAll(p => p.Member == mem);
    }
var allBridges = Doc.Project.Bridges.ToList();
Doc.Project.Bridges.RemoveAll(b => b != br);
Doc.Regenerate();
var st = Doc.Model(Main).Steel!;
var tm = st.Truss!;
var Up = new Vec3(0, 0, 1);

// frame of a plane item through two points of a plane (as TrussGenerator.BuildPlaneItems): origin A, U = horizontal toward B, V = up, W = U x V
(Vec3 a, Vec3 u, Vec3 w, double L)? Frame(string pl, string pa, string pb)
{
    var A = tm.Point(pl, pa); var B = tm.Point(pl, pb);
    if (A == null || B == null) return null;
    var d = new Vec3(B.World.X - A.World.X, B.World.Y - A.World.Y, 0);
    double L = d.Length;
    var u = d * (1 / L);
    return (A.World, u, Vec3.Cross(u, Up).Normalized(), L);
}
(double U, double V, double W) To((Vec3 a, Vec3 u, Vec3 w, double L) f, Vec3 x) => (Vec3.Dot(x - f.a, f.u), x.Z - f.a.Z, Vec3.Dot(x - f.a, f.w));
IEnumerable<Vec3> Verts(SteelPart p) => p.Mesh.Positions;
List<SteelPart> Gussets(string pl, string pt) => st.Parts.Where(p => p.Category == PartCategory.TrussGusset && p.Mark.StartsWith($"{pl}-G{pt}-")).ToList();
// gusset faces of a joint projected on the frame's (U, V): point-in-plate test on the built gussets (no outline convention to get wrong)
List<(double ax, double ay, double bx, double by, double cx, double cy)> Tris((Vec3 a, Vec3 u, Vec3 w, double L) f, List<SteelPart> gus)
{
    var r = new List<(double, double, double, double, double, double)>();
    foreach (var g in gus)
    {
        var P = g.Mesh.Positions; var I = g.Mesh.Indices;
        for (int i = 0; i + 2 < I.Count; i += 3)
        {
            var A = To(f, P[I[i]]); var B = To(f, P[I[i + 1]]); var C = To(f, P[I[i + 2]]);
            if (Math.Abs((B.U - A.U) * (C.V - A.V) - (B.V - A.V) * (C.U - A.U)) < 1e-8) continue;
            r.Add((A.U, A.V, B.U, B.V, C.U, C.V));
        }
    }
    return r;
}
// triangles of the parts whose W range overlaps g * [w0, w1] (projected on U, V)
List<(double ax, double ay, double bx, double by, double cx, double cy)> TrisW((Vec3 a, Vec3 u, Vec3 w, double L) f, List<SteelPart> parts, double w0, double w1, int g)
{
    var r = new List<(double, double, double, double, double, double)>();
    double lo = Math.Min(g * w0, g * w1), hi = Math.Max(g * w0, g * w1);
    foreach (var pa in parts)
    {
        var P = pa.Mesh.Positions; var I = pa.Mesh.Indices;
        double pw0 = double.MaxValue, pw1 = double.MinValue;
        foreach (var x in P) { double ww = Vec3.Dot(x - f.a, f.w); pw0 = Math.Min(pw0, ww); pw1 = Math.Max(pw1, ww); }
        if (pw1 < lo + 1e-4 || pw0 > hi - 1e-4) continue;
        for (int i = 0; i + 2 < I.Count; i += 3)
        {
            var A = To(f, P[I[i]]); var B = To(f, P[I[i + 1]]); var C = To(f, P[I[i + 2]]);
            if (Math.Abs((B.U - A.U) * (C.V - A.V) - (B.V - A.V) * (C.U - A.U)) < 1e-8) continue;
            r.Add((A.U, A.V, B.U, B.V, C.U, C.V));
        }
    }
    return r;
}
bool InTris(List<(double ax, double ay, double bx, double by, double cx, double cy)> tr, double x, double y)
{
    foreach (var (ax, ay, bx, by, cx, cy) in tr)
    {
        double d1 = (x - bx) * (ay - by) - (ax - bx) * (y - by), d2 = (x - cx) * (by - cy) - (bx - cx) * (y - cy), d3 = (x - ax) * (cy - ay) - (cx - ax) * (y - ay);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        if (!(neg && pos)) return true;
    }
    return false;
}
// last distance along (d, offset o along n) from t0 still on the gusset (first exit)
double Exit(List<(double ax, double ay, double bx, double by, double cx, double cy)> tr, (double U, double V) d, (double U, double V) n, double o, double t0)
{
    double r = t0;
    for (double s = t0; s < t0 + 6; s += 0.002) { if (InTris(tr, d.U * s + n.U * o, d.V * s + n.V * o)) r = s; else break; }
    return r;
}

// lowest V where the vertical line U = u0 (|W| < wLim) meets the triangles of the parts (MaxValue = none)
double MinVAt(List<SteelPart> parts, (Vec3 a, Vec3 u, Vec3 w, double L) f, double u0, double wLim)
{
    double best = double.MaxValue;
    foreach (var pa in parts)
    {
        var P = pa.Mesh.Positions; var I = pa.Mesh.Indices;
        for (int i = 0; i + 2 < I.Count; i += 3)
        {
            var q = new[] { To(f, P[I[i]]), To(f, P[I[i + 1]]), To(f, P[I[i + 2]]) };
            for (int e = 0; e < 3; e++)
            {
                var A = q[e]; var B = q[(e + 1) % 3];
                if ((A.U - u0) * (B.U - u0) > 0 || Math.Abs(B.U - A.U) < 1e-9) continue;
                double tt = (u0 - A.U) / (B.U - A.U), vv = A.V + (B.V - A.V) * tt, ww = A.W + (B.W - A.W) * tt;
                if (Math.Abs(ww) < wLim) best = Math.Min(best, vv);
            }
        }
    }
    return best;
}
// ================================================================ 1. lug angles 2-Ls 8 x 4 x 1/2
// p.87 U16 / U17 / L17: "Lug Ls 8 x 4 x 1/2 (12-1 in. Rivets (C) in o.s. leg)"; p.82 U15 "2-Ls 8 x 4 x 1/2, 16-1 in. (C) Riv. o.s. leg", U13 / U14 the same callout on the vertical
// (12 / 16); p.84 L14 (14), p.88 U18 / L18 ("10-1 in. Riv. o.s. leg of all lug Ls") / U19 / L19, p.89 U20 / L20 (12-1 in. Carbon Rivets O.S. Leg). The elevations (500 / 600 dpi
// re-read): the angles run along the member edges inside the gusset outline, from the member end to the gusset edge; the outstanding-leg rivets are drawn edge-on
// (side views with both heads) on the edge line = shanks normal to the cover plate; beyond the edge line a dashed outline about 4 - 5 in. wide carries one row of
// gusset rivets (U16, p.87). Read as: one angle per gusset on each cover edge, 4 in. leg on the gusset inner face beyond the cover plate, 8 in. leg on the cover plate.
// Diagonals: both cover edges (the leaders point at both edges at U18 / L18 / L19, and the dashed edge bands are drawn on both); verticals U13 / U14: the edge the
// leader points at (away from the diagonal). Rivet count = per angle (two staggered lines in the 8 in. leg); no count printed at U18 / U19 / L19 -> no rivets.
var lugs = new (string pt, string mem, int page, int? rivets, bool vertical)[]
{
    ("U13", "U13-L13", 82, 12, true), ("U14", "U14-L14", 82, 16, true), ("U15", "U15-L16", 82, 16, false),
    ("L14", "U13-L14", 84, 14, false),
    ("U16", "U16-L17", 87, 12, false), ("U17", "U17-L18", 87, 12, false), ("L17", "U16-L17", 87, 12, false),
    ("U18", "U18-L19", 88, null, false), ("L18", "U17-L18", 88, 10, false), ("U19", "U19-L20", 88, null, false), ("L19", "U18-L19", 88, null, false),
    ("U20", "U20-L21", 89, 12, false), ("L20", "U19-L20", 89, 12, false),
};
double legG = In(4), legO = In(8), lt = In(0.5);
int nLug = 0, nLugRiv = 0; double lugLb = 0;
var lugSkip = new List<string>(); var lugLens = new List<string>(); var lugClear = new List<string>();
foreach (var (pt0, mem0, page, nRiv, vertical) in lugs)
    foreach (bool pr in new[] { false, true })
        foreach (var pl in new[] { "N", "S" })
        {
            string pt = pr ? pt0 + "'" : pt0, mem = pr ? Prime(mem0) : mem0;
            var mr = tm.Members.FirstOrDefault(m => m.Plane == pl && m.Name == mem);
            if (mr == null) { lugSkip.Add($"{pl}-{mem} at {pt}: member not found"); continue; }
            string other = Bare(mr.From) == pt ? Bare(mr.To) : Bare(mr.From);
            // frame point B: the member's far end, or (a vertical: plumb over the joint, no horizontal direction) the chord neighbour of the joint
            string fb = other;
            if (tm.Point(pl, other) is { } po && tm.Point(pl, pt) is { } pp && new Vec3(po.World.X - pp.World.X, po.World.Y - pp.World.Y, 0).Length < 0.01)
                fb = tm.Members.Where(m => m.Plane == pl && (m.Role is TrussMemberRole.UpperChord or TrussMemberRole.LowerChord) && (Bare(m.From) == pt || Bare(m.To) == pt))
                    .Select(m => Bare(m.From) == pt ? Bare(m.To) : Bare(m.From)).FirstOrDefault() ?? other;
            if (Frame(pl, pt, fb) is not { } fr || fr.L < 0.01) { lugSkip.Add($"{pl}-{mem} at {pt}: points not found"); continue; }
            var B = To(fr, tm.Point(pl, other)!.World);
            double bl = Math.Sqrt(B.U * B.U + B.V * B.V);
            var d = (B.U / bl, B.V / bl); var n = (-d.Item2, d.Item1);
            double sgn = tm.Point(pl, other)!.Station > tm.Point(pl, pt)!.Station ? 1 : -1;
            var gus = Gussets(pl, pt);
            var parts = st.Parts.Where(p => p.Member == mr.Mark).ToList();
            var covers = parts.Where(p => p.Mark.StartsWith(mr.Mark + "-C")).ToList();
            if (gus.Count == 0 || covers.Count == 0) { lugSkip.Add($"{pl}-{mem} at {pt}: gussets / cover plates missing"); continue; }
            var tr = Tris(fr, gus);
            // the other members at the joint (the lug starts where it is clear of them)
            var others = tm.Members.Where(m => m.Plane == pl && m.Mark != mr.Mark && (Bare(m.From) == pt || Bare(m.To) == pt)).Select(m => m.Mark).ToHashSet();
            var otherParts = st.Parts.Where(p => p.Member != null && others.Contains(p.Member)).ToList();
            // gusset inner faces (+W / -W)
            double wgP = double.MaxValue, wgM = double.MaxValue;
            foreach (var g in gus) foreach (var x in Verts(g)) { var q = To(fr, x); if (q.W > 0) wgP = Math.Min(wgP, q.W); else wgM = Math.Min(wgM, -q.W); }
            // cover plate faces / ends per side of the axis
            var sides = new List<int>();
            if (vertical)
            {
                // the edge away from the diagonal that frames into the joint
                var dg = tm.Members.Where(m => m.Plane == pl && (Bare(m.From) == pt || Bare(m.To) == pt) && m.Role == TrussMemberRole.Diagonal).ToList();
                double sum = 0;
                foreach (var m in dg) { var o2 = To(fr, Bare(m.From) == pt ? m.B : m.A); sum += o2.U * n.Item1 + o2.V * n.Item2; }
                sides.Add(sum > 0 ? -1 : 1);
            }
            else { sides.Add(1); sides.Add(-1); }
            foreach (int s in sides)
            {
                double hc = 0, t0 = double.MaxValue;
                foreach (var c in covers)
                    foreach (var x in Verts(c))
                    {
                        var q = To(fr, x);
                        double o = (q.U * n.Item1 + q.V * n.Item2) * s, tt = q.U * d.Item1 + q.V * d.Item2;
                        if (o <= 0) continue;
                        hc = Math.Max(hc, o); t0 = Math.Min(t0, tt);
                    }
                if (hc <= 0) { lugSkip.Add($"{pl}-{mem} at {pt} side {s}: no cover plate on that side"); continue; }
                double t1 = Math.Min(Exit(tr, d, n, s * hc, t0), Exit(tr, d, n, s * (hc + legG), t0));
                double tC = t0;
                foreach (int g0 in new[] { 1, -1 })
                {
                    double wg0 = g0 > 0 ? wgP : wgM;
                    var oLeg = TrisW(fr, otherParts, wg0 - legO, wg0, g0); var cLeg = TrisW(fr, otherParts, wg0 - lt, wg0, g0);
                    bool Hit(double tt) => InTris(oLeg, d.Item1 * tt + n.Item1 * s * (hc + In(0.02)), d.Item2 * tt + n.Item2 * s * (hc + In(0.02)))
                        || InTris(oLeg, d.Item1 * tt + n.Item1 * s * (hc + lt), d.Item2 * tt + n.Item2 * s * (hc + lt))
                        || InTris(cLeg, d.Item1 * tt + n.Item1 * s * (hc + legG / 2), d.Item2 * tt + n.Item2 * s * (hc + legG / 2))
                        || InTris(cLeg, d.Item1 * tt + n.Item1 * s * (hc + legG), d.Item2 * tt + n.Item2 * s * (hc + legG));
                    double tt0 = t0;
                    while (tt0 < t1 && Hit(tt0)) tt0 += 0.002;
                    if (tt0 > t0) tC = Math.Max(tC, tt0 + In(0.125));
                }
                if (tC > t0) lugClear.Add($"{pl}-{pt} {mem} {(s > 0 ? "L" : "R")} +{(tC - t0) / In(1):0.0}");
                t0 = tC;
                if (t1 - t0 < In(6)) { lugSkip.Add($"{pl}-{mem} at {pt} side {s}: {F(t1 - t0)} between the cover end and the gusset edge"); continue; }
                double len = t1 - t0;
                if (pl == "S" && !pr) lugLens.Add($"{pt} {mem} {(s > 0 ? "L" : "R")} {F(len)} from {F(t0)}");
                string flag = U($"p.{page}", $"lug angles 2-Ls 8 x 4 x 1/2 on {mem} at {pt}",
                    "the callout gives the size" + (nRiv is { } c0 ? $" and {c0} rivets in the outstanding leg" : " but no rivet count") + "; the elevation draws the angles along the member edge inside the gusset, edge-on, without length, gages or which leg lies on the gusset",
                    $"one angle per gusset on {(vertical ? "the outer cover edge (the edge the leader points at)" : "both cover edges")}: 4 in. leg on the gusset inner face beyond the cover plate, 8 in. leg on the cover plate, from the cover end at the joint to the gusset edge (drawn rule)"
                    + (nRiv != null ? $"; {nRiv} rivets 1 in. per angle in the 8 in. leg in two staggered lines at the AISC workable gages 3 / 6 in. from the heel, equal pitch, 1 1/2 in. end distance." : "; no rivets (count not printed)."));
                foreach (int g in new[] { 1, -1 })
                {
                    double wg = g > 0 ? wgP : wgM;
                    if (wg > 1) continue;
                    var sec = new List<TrussUV>
                    {
                        new(g * wg, s * hc), new(g * wg, s * (hc + legG)), new(g * (wg - lt), s * (hc + legG)), new(g * (wg - lt), s * (hc + lt)),
                        new(g * (wg - legO), s * (hc + lt)), new(g * (wg - legO), s * hc),
                    };
                    var rivets = new List<TrussPlaneItemRivet>();
                    if (nRiv is { } nr && nr > 1)
                    {
                        double e = In(1.5), p = (len - 2 * e) / (nr - 1);
                        if (p < In(1.5)) report.Add($"CHECK lug {pl}-{mem} at {pt}: {nr} rivets on {F(len)} = {F(2 * p)} pitch per gage line (under 3 d; the printed count on the drawn length)");
                        for (int k = 0; k < nr; k++)
                        {
                            double tt = t0 + e + k * p, gg = k % 2 == 0 ? In(3) : In(6), oo = s * (hc + lt / 2);
                            rivets.Add(new TrussPlaneItemRivet
                            {
                                U = d.Item1 * tt + n.Item1 * oo, V = d.Item2 * tt + n.Item2 * oo, W = g * (wg - gg), AxisU = n.Item1 * s, AxisV = n.Item2 * s, AxisW = 0,
                                Diameter = In(1), Material = "1 in carbon steel rivet", Unresolved = flag,
                            });
                        }
                    }
                    string name = $"CMP-LUG-{pl}-{pt}-{mem}-{(s > 0 ? "L" : "R")}{(g > 0 ? "P" : "M")}";
                    t.PlaneItems.Add(new TrussPlaneItemDefinition
                    {
                        Name = name, Label = $"lug angle L 8 x 4 x 1/2 x {F(len)} on {mem} at {pt}", PointA = $"{pl}:{pt}", PointB = $"{pl}:{fb}", Kind = TrussPlaneItemKind.Sweep,
                        Path = new() { new TrussPathVertex(d.Item1 * t0, d.Item2 * t0), new TrussPathVertex(d.Item1 * t1, d.Item2 * t1) }, Section = sec,
                        Category = PartCategory.TrussConnectionAngle, Assembly = $"TRUSS-{pl}", AssemblyKind = AssemblyKind.TrussLine, AssemblyDescription = $"truss line {pl}",
                        Panel = $"Joint {pt}", Material = "C", Unresolved = flag, Source = $"p.{page} {Tag}", Rivets = rivets.Count > 0 ? rivets : null,
                    });
                    nLug++; nLugRiv += rivets.Count; lugLb += len / 0.3048 * 19.6;
                }
            }
        }
report.Add($"1. lug angles: {nLug} angles L 8 x 4 x 1/2 (about {lugLb:N0} lb), {nLugRiv} rivets in the outstanding legs; S lengths: " + string.Join(", ", lugLens) + "; started clear of the other members (in.): " + string.Join(", ", lugClear) + (lugSkip.Count > 0 ? "; not built: " + string.Join("; ", lugSkip) : ""));
Flag("lug angles 2-Ls 8 x 4 x 1/2 (main span)", null);

// ================================================================ 2. rope suspender support diaphragms (p.86 typical connection, Sheet 46)
// p.86 (600 dpi, 17.4 px/in calibrated on the printed 1'-6 and the 29 in. web): "4-Ls 5 x 5 x 3/4, 1-Web Pl. 29 x 3/4", "Fin. Web" between the gusset inner faces, pin hole
// 7 1/4 + 1/32 1'-6 above the finished bottom of the web (the bottom lies on the gusset bottom line), web plate transverse (the pin runs along the bridge through it, p.94),
// two angles per gusset (one on each face of the web), 6 rivets per angle in the web leg (through angle - web - angle) and, in the joint elevations, one column of 6 in each
// angle's gusset leg; p.87 note "1 1/8 high strength rivets (Also in diaph. web)". Scaled: web / angle height 25 1/2 in., rivet rows 2 5/8 in. above the bottom at 4 1/4 in.,
// web-leg gage 2 3/4 in. from the gusset face, gusset-leg column 4 3/4 in. from the pin CL (joint elevation p.86 L15).
string diaFlag = U("p.86, p.87-89", "rope suspender support diaphragm (4-Ls 5 x 5 x 3/4 + 1-Web Pl. 29 x 3/4) at the suspender joints",
    "Sheet 46 (p.86) details it in one section along the bridge: plate width (29 in., finished between the gussets), angles, pin hole and the 1'-6 pin to the bottom are given, the height, rivet pitch and gages are drawn, not dimensioned",
    "web plate between the gusset inner faces, its bottom 1'-6 below the suspender pin (the pin height is unchanged: 2'-11 1/4 below the chord CL), 25 1/2 in. high (scaled 17.4 px/in at 600 dpi), pin hole 7 9/32 in.; angles the web height, 6 rivets 1 1/8 in. H.S. per angle in each leg at 4 1/4 in. from 2 5/8 in. above the bottom (scaled); the web bottom lands 1/16 - 15/16 in. below the gusset bottom line p.86 draws it on (the p.94 pin height and the traced gusset outlines differ by that much: reported, not adjusted).");
double webT = In(0.75), hDia = In(25.5), pinR = In(7.28125) / 2, aL = In(5), aT = In(0.75);
int nDia = 0, nDiaRiv = 0;
var diaNotes = new List<string>();
foreach (var sp in tm.Suspenders)
{
    string pl = sp.Plane, pt = Bare(sp.Name.Length > 0 ? ("L" + sp.Name.Substring(1)) : "");
    if (tm.Point(pl, pt) == null) { diaNotes.Add($"{sp.Name}: joint {pt} not found"); continue; }
    var ch = tm.Members.FirstOrDefault(m => m.Plane == pl && m.Role == TrussMemberRole.LowerChord && (Bare(m.From) == pt || Bare(m.To) == pt) && m.Name.StartsWith("L"));
    if (ch == null) { diaNotes.Add($"{sp.Name}: no lower chord at {pt}"); continue; }
    string other = Bare(ch.From) == pt ? Bare(ch.To) : Bare(ch.From);
    var fr = Frame(pl, pt, other)!.Value;
    var pin = To(fr, sp.UpperPin);
    var gus = Gussets(pl, pt);
    double wP = double.MaxValue, wM = double.MaxValue, gBot = double.MaxValue;
    foreach (var g in gus) foreach (var x in Verts(g)) { var q = To(fr, x); if (q.W > 0) wP = Math.Min(wP, q.W); else wM = Math.Min(wM, -q.W); }
    gBot = MinVAt(gus, fr, pin.U, 10);
    double vb = pin.V - In(18), vt = vb + hDia;
    // the chord's lowest steel over the plate (the plate must stay below it)
    double chBot = double.MaxValue;
    var chords = tm.Members.Where(m => m.Plane == pl && m.Role == TrussMemberRole.LowerChord && (Bare(m.From) == pt || Bare(m.To) == pt)).Select(m => m.Mark).ToHashSet();
    chBot = MinVAt(st.Parts.Where(p => p.Member != null && chords.Contains(p.Member)).ToList(), fr, pin.U, Math.Min(wP, wM) - 1e-4);
    if (chBot < vt) diaNotes.Add($"{pl}-{pt}: plate top {F(vt - chBot)} above the chord bottom -> stopped at the chord");
    vt = Math.Min(vt, chBot);
    diaNotes.Add($"{pl}-{pt}: web bottom {(vb - gBot) / In(1):0.00} in. from the gusset bottom (p.86 draws it on the gusset bottom line; the pin height comes from p.94), in to in {(wP + wM) / In(1):0.00} in.");
    var path = new List<TrussPathVertex> { new(pin.U - webT / 2, 0), new(pin.U + webT / 2, 0) };
    var bot = new List<TrussUV> { new(-wM, vb), new(wP, vb), new(wP, pin.V), new(pin.W + pinR, pin.V) };
    for (int k = 1; k < 16; k++) { double th = -Math.PI * k / 16; bot.Add(new(pin.W + pinR * Math.Cos(th), pin.V + pinR * Math.Sin(th))); }
    bot.Add(new(pin.W - pinR, pin.V)); bot.Add(new(-wM, pin.V));
    var top = new List<TrussUV> { new(-wM, pin.V), new(pin.W - pinR, pin.V) };
    for (int k = 1; k < 16; k++) { double th = Math.PI - Math.PI * k / 16; top.Add(new(pin.W + pinR * Math.Cos(th), pin.V + pinR * Math.Sin(th))); }
    top.Add(new(pin.W + pinR, pin.V)); top.Add(new(wP, pin.V)); top.Add(new(wP, vt)); top.Add(new(-wM, vt));
    TrussPlaneItemDefinition Item(string nm, string label, List<TrussPathVertex> pth, List<TrussUV> sec, PartCategory cat, List<TrussPlaneItemRivet>? rv) => new()
    {
        Name = nm, Label = label, PointA = $"{pl}:{pt}", PointB = $"{pl}:{other}", Kind = TrussPlaneItemKind.Sweep, Path = pth, Section = sec, Category = cat,
        Assembly = $"TRUSS-{pl}", AssemblyKind = AssemblyKind.TrussLine, AssemblyDescription = $"truss line {pl}", Panel = $"Joint {pt}", Material = "S",
        Unresolved = diaFlag, Source = $"p.86 {Tag}", Rivets = rv,
    };
    t.PlaneItems.Add(Item($"CMP-SSD-{pl}-{pt}-W1", $"suspender support diaphragm web PL 29 x 3/4 (below the pin) at {pt}", path, bot, PartCategory.TrussConnectionPlate, null));
    t.PlaneItems.Add(Item($"CMP-SSD-{pl}-{pt}-W2", $"suspender support diaphragm web PL 29 x 3/4 (above the pin) at {pt}", path, top, PartCategory.TrussConnectionPlate, null));
    // angles: vertical path; section (W, Y) with Y = -(U - pin.U) (left of an upward path = -U)
    var vpath = new List<TrussPathVertex> { new(pin.U, vb), new(pin.U, vt) };
    var rows = Enumerable.Range(0, 6).Select(k => vb + In(2.625) + k * In(4.25)).Where(v => v < vt - In(1.25)).ToList();
    foreach (int g in new[] { 1, -1 })
    {
        double gw = g > 0 ? wP : -wM;
        // web-leg rivets: one shank through angle - web - angle (along U), on this gusset's side
        var webRiv = rows.Select(v => new TrussPlaneItemRivet { U = pin.U, V = v, W = gw - g * In(2.75), AxisU = 1, AxisW = 0, Diameter = In(1.125), Material = "1 1/8 in high strength rivet (p.87 note)", Unresolved = diaFlag }).ToList();
        foreach (int f in new[] { 1, -1 })
        {
            double h0 = webT / 2;
            var sec = new List<TrussUV>
            {
                new(gw, -f * h0), new(gw, -f * (h0 + aL)), new(gw - g * aT, -f * (h0 + aL)), new(gw - g * aT, -f * (h0 + aT)), new(gw - g * aL, -f * (h0 + aT)), new(gw - g * aL, -f * h0),
            };
            var gRiv = rows.Select(v => new TrussPlaneItemRivet { U = pin.U + f * In(4.75), V = v, W = gw - g * aT / 2, AxisU = 0, AxisW = g, Diameter = In(1.125), Material = "1 1/8 in high strength rivet (p.87 note)", Unresolved = diaFlag }).ToList();
            if (f > 0) gRiv.AddRange(webRiv);
            t.PlaneItems.Add(Item($"CMP-SSD-{pl}-{pt}-A{(g > 0 ? "P" : "M")}{(f > 0 ? "A" : "B")}", $"suspender support diaphragm L 5 x 5 x 3/4 at {pt}", vpath, sec, PartCategory.TrussConnectionAngle, gRiv));
            nDiaRiv += gRiv.Count;
        }
    }
    nDia++;
}
report.Add($"2. suspender support diaphragms: {nDia} (web plate in two pieces round the pin hole + 4 angles each), {nDiaRiv} rivet positions; " + string.Join("; ", diaNotes));
// the suspender pin flag said p.86 was not read: replace it
string suspOld = "the suspender support diaphragm at the lower chord is on Sheet 46 (p.86, not read)";
string suspNew = "p.86 (Sheet 46) gives the pin hole in the 29 x 3/4 diaphragm web 1'-6 above the web's finished bottom but no dimension from the pin to the chord or the W.P.";
foreach (var sp in t.Suspenders) sp.Unresolved = sp.Unresolved.Replace(suspOld, suspNew);
for (int i = 0; i < t.Unresolved.Count; i++) t.Unresolved[i] = t.Unresolved[i].Replace(suspOld, suspNew);

// ================================================================ 3. diagonal fills at U21 / L21 (p.89)
// connections-ptc.csx batch 8b lists them ("2-18 x 1 1/8 Fills (C)" on U21-L22 at U21, "2-21 x 1 1/8 Fills" on U20-L21 at L21: gussets 2'-7 1/4 in. to in., the 2'-5 diagonals stand
// 1 1/8 off them) with the extent "to the gusset edge + the drawn length beyond", but its reach along the member came out 0 and the generator dropped the plates ("lies beyond
// the end of member"). Rebuilt here with the reach measured from the model member axis on the joint outline.
var fills = new (string pt, string mem, double w, double beyond, string label)[] { ("U21", "U21-L22", 18, 4.8, "2-18 x 1 1/8 Fills (C)"), ("L21", "U20-L21", 21, 18.6, "2-21 x 1 1/8 Fills") };
int nFill = 0;
foreach (var (pt0, mem0, w, beyond, label) in fills)
    foreach (bool pr in new[] { false, true })
    {
        string pt = pr ? pt0 + "'" : pt0, mem = pr ? Prime(mem0) : mem0;
        var mr = tm.Members.FirstOrDefault(m => m.Plane == "S" && m.Name == mem);
        var jd = t.Joints.LastOrDefault(x => x.Point == pt && x.Outline is { Count: >= 3 }) ?? t.Joints.LastOrDefault(x => x.Point == pt);
        if (mr == null || jd == null) { report.Add($"3. fill {mem} at {pt}: member / joint not found"); continue; }
        string other = Bare(mr.From) == pt ? Bare(mr.To) : Bare(mr.From);
        var fr = Frame("S", pt, other)!.Value;
        var B = To(fr, tm.Point("S", other)!.World);
        double bl = Math.Sqrt(B.U * B.U + B.V * B.V);
        double sgn = tm.Point("S", other)!.Station > tm.Point("S", pt)!.Station ? 1 : -1;
        var trF = Tris(fr, Gussets("S", pt));
        double reach = Exit(trF, (B.U / bl, B.V / bl), (0, 0), 0, 0.01);
        double hi = reach + In(beyond);
        jd.Plates ??= new List<TrussJointPlate>();
        jd.Plates.Add(new TrussJointPlate
        {
            Member = mem, Face = TrussPlateFace.WebOutside, Fill = true, Width = In(w), Thickness = In(1.125), Length = hi, Offset = hi / 2,
            Label = label, Material = "C", Source = $"p.89 {Tag}",
            Unresolved = U("p.89", $"{label} on {mem} at {pt}", "the callout gives width x thickness only; the fill extent (member end to a few inches past the gusset edge, where it carries field rivets) is drawn, not dimensioned",
                $"fill from the member end to {hi / In(1):0.0} in. from the WP along the member: gusset edge on the axis {reach / In(1):0.0} in. + {beyond:0.0} in. drawn beyond it (scaled 3/4 in. = 1 ft-0, +-1 in.)."),
        });
        nFill++;
    }
report.Add($"3. diagonal fills U21 / L21: {nFill} definitions (both planes each)");

// ================================================================ 4. read, not built (flags with the reading)
Flag("L12 web member fills 2-Fills 26 x 1", U("p.81", "L12 web member fills 2-Fills 26 x 1 on U11-L12 and 2-Fills 28 x 1 on U12-L12",
    "the joint elevation draws them beyond the gusset edge with three rows of field rivets, but the gussets are 2'-5 in. to in. and the model builds these members 2'-5 out to out (the member width is itself not given, p.70): a 1 in. fill each side means the members are 2'-3 out to out, which the sheets do not state",
    "not built (it needs the member width of U11-L12 / U12-L12 decided first)."));
// L22 = FINAL CONDITION ONLY (user decision 2026-11-03: "Fix L22 to be only the final condition"; TRUSS-DESIGN.md section 42). p.90 note "Flame cut 1" gusset plates
// at L22 on lines "X" after field rivets have been driven": the corner pieces of the 1 in. half gussets outside lines X (gusset-outlines.csv L22 = the plate left after
// the cut) carry the crown jacking brackets - chord-end diaphragms, end angles bearing on the jacking slabs, fastened to the plate inside line X by "1" Shop Bolts".
// They, the jacking slabs / rods / bodies (Section A / B) and the "1 1/8 Erection Bolts" are erection equipment: not built.
Flag("L22 chord diaphragms (Diaph. Material 4-Ls 5 x 3 1/2 x 1/2", null);   // old open-question texts (2026-11-02)
Flag("L22 crown jacking scheme", null);
Flag("L22 FINAL CONDITION: crown jacking brackets", "USER DECISION (p.90, 2026-11-03): L22 is modeled in its FINAL CONDITION only. The crown jacking brackets - Diaph. Material "
    + "4-Ls 5 x 3 1/2 x 1/2 + 1-1/2 Web Pl. (F.1.E.), 1-L 4 x 4 x 1/2 (F.2.E.), end angles 2-Ls 8 x 6 x 1/2 (C) / 2-Ls 4 x 4 x 1/2 (C) on the FIN. gusset ends - sit on the "
    + "corner pieces of the 1 in. gussets that the p.90 note 'Flame cut 1\" gusset plates at L22 on lines X after field rivets have been driven' removes, fastened inside "
    + "line X by '1\" Shop Bolts'; with the crown jacking scheme of Section A / B (2-Slabs 22 x 10 1/2 (C), 2 jacking rods, jacking bodies, jack 2'-4) they are erection "
    + "equipment and are not built; the 1 in. gussets are built as left after the cut (gusset-outlines.csv) and the field rivets of the cut-off pieces are not placed.");
Flag("L22 shop bolt holes after the cut", U("p.90", "the holes of the '1\" Shop Bolts' that fastened the crown jacking brackets to the 1 in. gussets just inside lines X (3 per band row above the chord, about 4 per side under the 1 1/8 splice gusset below it)",
    "the sheet does not say whether the bolts are removed with the brackets, left in place, or replaced by rivets in the final condition",
    "no fasteners placed in them (the generator cuts no holes)."));
Flag("L22 erection bolts", U("p.90", "the piece fastened by '1 1/8 Erection Bolts' above the L22 gusset east of the CL (2-3/8 Gus. Pls. (C) bracket on the gusset top edge and a member drawn broken above it; U22-L22 is on the CL, so it is not a truss member)",
    "the sheet names only its fasteners (erection bolts) and draws the piece without a name, size or entry in the joint's material lists",
    "treated as erection equipment under the final-condition decision (user 2026-11-03): not built, its 6 drawn rivets not placed (rivet-count-zones.csv SKIP)."));
Flag("L14 internal chord diaphragm (p.84 Section E)", U("p.84", "internal diaphragm at L14 (Section E: DIAPHRAGM 4-Ls 8 x 7 1/2 x 5/8, 3-Ls 5 x 5 x 5/8, 1-L 6 1/2 x 6 x 5/8, 1-3/4 Web Pl., 2-Fills 12 1/4 x 1; 1 1/8 rivets, 2'-5 in. to in. gussets)",
    "Section E gives the make-up across the member but the elevation does not dimension where along the member it sits or its length, and the angles' lengths are not given",
    "not built."));
Flag("L1 / L3 strut connection angles", U("p.75, p.74, p.97", "L1 / L3 1-L 5 x 5 x 7/16 (C) each side of the vertical on the chord web and 1-Fill 6 x 1/2 (C) top and bottom",
    "read on p.75 (L1 'similar to L3', p.74): the angles are the bottom strut LS-L1 / LS-L3 end connection to the chord web (p.97 L1 cross section, 'use similar details for L2 to L8'), the fills pack the lateral bracing plates S-BGL1 / S-BGL3 on the chord flange angles; both are bracing-connection items (bracing-ptc.csx)",
    "not built here: left to the bracing connections (bottom strut angles L1-L8 are the same detail)."));

// ---------------------------------------------------------------- restore the bridges and save
Doc.Project.Bridges.Clear();
Doc.Project.Bridges.AddRange(allBridges);
foreach (var r in report) Print(r);
Doc.Save(outPath); Print($"Saved {outPath}");
