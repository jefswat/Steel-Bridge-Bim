// PTC / NJTA DELAWARE RIVER BRIDGE (1954-57, AS-BUILT) - SERVICE WALKWAYS, LADDERS AND INSPECTION PLATFORMS (p.101-106):
//   approach truss spans (p.101 / p.102, piers 20-27 opposite hand): CL walkway at the floor beam bottom flange level (through the floor beam manholes),
//   cross walkways at the piers (pier 10 / 27 truss to truss, the other piers CL to the north truss), each 2'-0 grating between 10 [ 15.3 walk channels
//   (2'-0 b.b.) with hand railing both sides (top rail L 3x3x5/16, mid rail L 3x2 1/2x5/16, posts L 3x3x5/16 @ 6'-0 +-), safety ladders (Ls 3x2 1/2x3/8,
//   3/4 in. rungs @ 12 in.) from the walkway down to the inspection platform on the pier top (3/4 grating on 8 [ 13.75 frame, hand railing);
//   main span (p.103 / p.104 / p.106): walkway along the floor beam at L11 / L11' and on the L11 bottom strut, CL walkway W15 - W15', cross walks at
//   W15 / W20 / W19' to the navigation light supports, platforms at the pier 18 / 19 tops with a ladder from the floor beam walkway.
// Girder spans: no walkway sheet in the set (p.42 lists grating for them) -> flag only.
// Generic inputs: TrussDefinition.LineItems (incl. RepeatPitch for posts, CopiesV for ladder rungs). Re-runnable (replaces its own items); run AFTER
// deck-ptc.csx (which replaces the line items):
//   sbm run samples\plans\walkways-ptc.csx "samples\plans\PTC Delaware River Bridge.sbm"
// Rules: as-built set only; positions only drawn (not dimensioned) are scaled from the key plans and flagged; nothing is adjusted.
using SBM.Core;
using SBM.Core.Bridge;
using SBM.Core.Steel;

string outPath = Args.Length > 0 ? Args[0] : (Doc.FilePath ?? @"samples\plans\PTC Delaware River Bridge.sbm");
string U(string pages, string item, string why, string then = "") => UnresolvedFlag.Text(pages, item, why, then);
double FI(double ft, double inch) => Ft(ft) + In(inch);
TrussUV UV(double uIn, double vIn) => new TrussUV { U = In(uIn), V = In(vIn) };
List<TrussUV> P(params double[] xy) { var l = new List<TrussUV>(); for (int i = 0; i + 1 < xy.Length; i += 2) l.Add(UV(xy[i], xy[i + 1])); return l; }

var names = new[] { "PTC Girder spans 1-10", "PTC 3-span truss 11-13", "PTC 4-span truss 14-17", "PTC Arch truss 18-20", "PTC 4-span truss 21-24", "PTC 3-span truss 25-27", "PTC Girder spans 28-31" };
var bridges = names.ToDictionary(n => n, n => Doc.Project.Bridges.First(b => b.Name == n));
if (bridges.Values.Any(b => b.Truss?.LineItems == null)) throw new InvalidOperationException("Run deck-ptc.csx first (it creates the truss line items).");
var ownAsm = new[] { "WALKWAYS", "LADDERS", "PLATFORMS" };
foreach (var b in bridges.Values) b.Truss!.LineItems!.RemoveAll(li => ownAsm.Contains(li.Assembly));     // re-runnable
var model0 = names.ToDictionary(n => n, n => Doc.Model(n));
var alOf = names.ToDictionary(n => n, n => Doc.Project.Alignments.First(a => a.Name == bridges[n].AlignmentName));
(double zMin, double zMax, double sta)? PartBox(string bridge, string mark)
{
    var p = model0[bridge].Steel?.WholeMember(mark);
    if (p == null || p.Mesh.IsEmpty) return null;
    var bb = p.Mesh.Bounds();
    return (bb.Min.Z, bb.Max.Z, alOf[bridge].Inverse(new SBM.Core.Geometry.Vec2((bb.Min.X + bb.Max.X) / 2, (bb.Min.Y + bb.Max.Y) / 2)).Station);
}

// ---------------------------------------------------------------- materials (p.101 / p.102 / p.103 material lists)
double gratingDensity = 7.0 * 4.88242764 / In(0.75);   // 7 lb/sq ft over a 3/4 in plate (kg/m3)
string gratingFlag = U("p.101", "grating weight", "the sheet names the grating (3/4 x 3/16 bearing bars, Irving X-Bar type AA) but prints no weight", "7 lb/sq ft (catalogue value for the type, not from the plans).");
string railFlag = U("p.101, p.102", "hand railing heights and post layout", "the material lists give top rail L 3x3x5/16, mid rail L 3x2 1/2x5/16 and posts L 3x3x5/16 'spaced @ 6'-0 +-'; the platform section prints 1'-5 / 1'-6 rail spaces, the walkway rail heights are drawn only",
    "mid rail 1'-6, top rail 3'-0 above the grating, posts every 6'-0 from 6 in. after the run start, bolted to the walk channel; rails both sides of every run.");
string channelFlag = U("p.101", "walk channels", "'10 [ @ 15.3' with Conn. Ls 6x6x3/8 and the grating 'weld to walk Ls at 9 in. ctrs', 2'-0 b.b. on p.101; the channel connection to the floor beams / hangers (L 4x4x3/8, Conn. Ls 9x4x1/2 to stringers) is drawn, not located",
    "two C10x15.3 2'-0 back to back, tops flush with the grating, continuous over the run; hangers, connection angles and the floor beam manholes not built.");

// C10x15.3 (d 10, bf 2.60, tw 0.24, tf 0.436); flanges toward the grating (backs 2'-0 apart)
List<TrussUV> Channel(double side, double top, double d = 10, double bf = 2.6, double tw = 0.24, double tf = 0.436, double back = 12)
{
    double w0 = side * back, w1 = side * (back - tw), f = side * (back - bf);
    return P(w0, top - d, w0, top, f, top, f, top - tf, w1, top - tf, w1, top - d + tf, f, top - d + tf, f, top - d);
}
// rail angle with the horizontal leg on top, pointing outward from U = side * at
List<TrussUV> RailAngle(double side, double at, double top, double hor, double ver, double t) =>
    P(side * at, top - ver, side * at, top, side * (at + hor), top, side * (at + hor), top - t, side * (at + t), top - t, side * (at + t), top - ver);

TrussLineItemDefinition Li(string name, string asm, string desc, PartCategory cat, bool transverse, double reference, double from, double to, bool fromGrade, List<TrussUV> prof,
    string src, string flag, double density = 0, double lbft = 0, double pitch = 0, double width = 0, double start = 0, List<double>? copies = null) => new TrussLineItemDefinition
{
    Name = name, Kind = TrussLineItemKind.Steel, Category = cat, Transverse = transverse, Station = transverse ? reference : 0, ReferenceOffset = transverse ? 0 : reference,
    From = from, To = to, Side = 1, FromGrade = fromGrade, Profile = prof, Material = cat == PartCategory.Grating ? "Galvanized steel grating" : "C", Density = density,
    NominalWeightLbPerFt = lbft, Assembly = asm, AssemblyDescription = desc, Source = src, Unresolved = flag, RepeatPitch = pitch, RepeatWidth = width, RepeatStart = start, CopiesV = copies,
};

// one walkway run: grating 2'-0 between two walk channels, rails + posts both sides. vTop = grating top (in.; above the grade when fromGrade, else absolute elevation)
var counts = new Dictionary<string, (double walkLf, int ladders, int platforms)>();
// singleRail (main span CL walkway, p.106 MATERIAL BETWEEN FLOOR BEAMS, user decision 2026-10-28 BW-W12): one rail angle 3 x 2 1/2 x 5/16 per side, no posts / mid rail
string singleRailFlag = U("p.106", "main span CL walkway hand railing", "MATERIAL BETWEEN FLOOR BEAMS lists 'Hand Railing 3 x 2 1/2 x 5/16 (grind all sheared edges smooth)' (re-read 2026-10-30, batch 8g; was read 3/8) with 'Hand Rail Conn. to Fl. Beams 6x4x3/8 Ls', 1 PL 14 x 3/8 x 2'-0 and 2 conn. Ls 4x4x3/8 - no posts or mid rail; the rail height and its connection positions are drawn only",
    "one rail angle L 3x2 1/2x5/16 per side (3 in. leg horizontal, outward), top 3'-0 above the grating like the other walkway rails, continuous between the floor beam webs; connection angles and plates not built.");
void Walk(string bridge, string name, bool transverse, double reference, double from, double to, double vTopIn, bool fromGrade, string pages, string flag, bool singleRail = false)
{
    var t = bridges[bridge].Truss!;
    string desc = $"{bridge} - service walkways ({pages})";
    var items = t.LineItems!;
    double top = vTopIn;
    items.Add(Li($"{name} grating", "WALKWAYS", desc, PartCategory.Grating, transverse, reference, from, to, fromGrade, P(-11.76, top - 1.25, 11.76, top - 1.25, 11.76, top - 0.5, -11.76, top - 0.5), pages, flag + " " + gratingFlag, gratingDensity));
    foreach (int sd in new[] { -1, 1 })
    {
        string s = sd < 0 ? "L" : "R";
        items.Add(Li($"{name} channel {s}", "WALKWAYS", desc, PartCategory.WalkwayStringer, transverse, reference, from, to, fromGrade, Channel(sd, top), pages, channelFlag, lbft: 15.3));
        if (singleRail)
        {
            items.Add(Li($"{name} hand rail {s}", "WALKWAYS", desc, PartCategory.WalkwayRail, transverse, reference, from, to, fromGrade, RailAngle(sd, 15, top + 36, 3, 2.5, 0.3125), pages, singleRailFlag, lbft: 5.6));
            continue;
        }
        items.Add(Li($"{name} top rail {s}", "WALKWAYS", desc, PartCategory.WalkwayRail, transverse, reference, from, to, fromGrade, RailAngle(sd, 15, top + 36, 3, 3, 0.3125), pages, railFlag, lbft: 6.1));
        items.Add(Li($"{name} mid rail {s}", "WALKWAYS", desc, PartCategory.WalkwayRail, transverse, reference, from, to, fromGrade, RailAngle(sd, 15, top + 18, 2.5, 3, 0.3125), pages, railFlag, lbft: 5.6));
        // posts L 3x3x5/16 outside the channel web from the channel bottom to the top rail: leg across (3 in. x 5/16 along the run) + leg along (5/16 x 3 in.)
        double len = to - from, pitch = Ft(6);
        int n = Math.Max(1, (int)Math.Floor((len - In(12)) / pitch) + 1);
        double start = (len - (n - 1) * pitch - In(3)) / 2;
        items.Add(Li($"{name} posts {s} a", "WALKWAYS", desc, PartCategory.WalkwayPost, transverse, reference, from, to, fromGrade, P(sd * 12, top - 10, sd * 15, top - 10, sd * 15, top + 33, sd * 12, top + 33), pages, railFlag,
            pitch: pitch, width: In(0.3125), start: start));
        items.Add(Li($"{name} posts {s} b", "WALKWAYS", desc, PartCategory.WalkwayPost, transverse, reference, from, to, fromGrade, P(sd * 12, top - 10, sd * 12.3125, top - 10, sd * 12.3125, top + 33, sd * 12, top + 33), pages, railFlag,
            pitch: pitch, width: In(3 - 0.3125), start: start + In(0.3125)));   // the along leg starts beyond the across leg (no overlap at the heel)
    }
    var c = counts.GetValueOrDefault(bridge);
    counts[bridge] = (c.walkLf + (to - from) / 0.3048, c.ladders, c.platforms);
}

// CL walkway between the floor beams (p.101 floor beam elevation / key plan, p.106 plan of walkway at W15 and expansion detail at piers 17 / 20, p.43 Section A-A):
// the walkway is centred 2'-0 NORTH of the CL bridge (1'-0 to 3'-0: p.106 '1'-0 | Walkway 2'-0' from the CL, p.101 manhole and walk 2'-0 from the CL, p.43 'CL of
// inspection walk 2'-0'), seated between the floor beams on their bottom flanges and interrupted at every floor beam web (the inspector passes the 12 x 20 manhole;
// the walk channels are connected to the webs). Pieces run from floor beam web to floor beam web (1 3/4 in. clear of the web CL), the 10 [ 15.3 channels
// standing on the floor beam bottom flange (angle legs + cover plates, probed; approach / girder spans) or with their bottom 2'-2 1/2 above the floor beam bottom
// (main span, p.106 expansion detail); grating between the channel webs just under their top flanges.
const double ClOffsetFt = -2.0;
double? FbBottomAt(SBM.Core.Steel.SteelPart p, SBM.Core.Alignments.Alignment al, double sta, double off)
{
    var xy = al.PointAt(sta, off);
    double best = double.PositiveInfinity;
    var pos = p.Mesh.Positions; var idx = p.Mesh.Indices;
    for (int i = 0; i + 2 < idx.Count; i += 3)
    {
        var A = pos[idx[i]]; var B = pos[idx[i + 1]]; var C = pos[idx[i + 2]];
        double d = (B.Y - C.Y) * (A.X - C.X) + (C.X - B.X) * (A.Y - C.Y);
        if (Math.Abs(d) < 1e-14) continue;
        double l1 = ((B.Y - C.Y) * (xy.X - C.X) + (C.X - B.X) * (xy.Y - C.Y)) / d, l2 = ((C.Y - A.Y) * (xy.X - C.X) + (A.X - C.X) * (xy.Y - C.Y)) / d, l3 = 1 - l1 - l2;
        if (l1 < -1e-9 || l2 < -1e-9 || l3 < -1e-9) continue;
        best = Math.Min(best, l1 * A.Z + l2 * B.Z + l3 * C.Z);
    }
    return double.IsPositiveInfinity(best) ? null : best;
}
// top of the bottom flange stack (angle legs + cover plates) beside the web: the second surface a vertical ray meets, 4 in. each side of the floor beam centre
double? FbFlangeTopAt(SBM.Core.Steel.SteelPart p, SBM.Core.Alignments.Alignment al, double sta, double off)
{
    double best = double.NegativeInfinity;
    foreach (double ds in new[] { -In(4), In(4) })
    {
        var xy = al.PointAt(sta + ds, off) + new SBM.Core.Geometry.Vec2(1.37e-6, -0.83e-6);   // off the triangle edges (a diagonal hit counts twice)
        var zs = new List<double>();
        var pos = p.Mesh.Positions; var idx = p.Mesh.Indices;
        for (int i = 0; i + 2 < idx.Count; i += 3)
        {
            var A = pos[idx[i]]; var B = pos[idx[i + 1]]; var C = pos[idx[i + 2]];
            double d = (B.Y - C.Y) * (A.X - C.X) + (C.X - B.X) * (A.Y - C.Y);
            if (Math.Abs(d) < 1e-14) continue;
            double l1 = ((B.Y - C.Y) * (xy.X - C.X) + (C.X - B.X) * (xy.Y - C.Y)) / d, l2 = ((C.Y - A.Y) * (xy.X - C.X) + (A.X - C.X) * (xy.Y - C.Y)) / d, l3 = 1 - l1 - l2;
            if (l1 < -1e-9 || l2 < -1e-9 || l3 < -1e-9) continue;
            zs.Add(l1 * A.Z + l2 * B.Z + l3 * C.Z);
        }
        zs.Sort();
        // touching pieces (cover plate under the angle leg) give an exit + entry at one level: drop such pairs so u[1] is the top of the whole stack
        var u = new List<double>();
        for (int i = 0; i < zs.Count; i++)
        {
            if (i + 1 < zs.Count && zs[i + 1] - zs[i] < 1e-4) { i++; continue; }
            u.Add(zs[i]);
        }
        if (u.Count >= 2) best = Math.Max(best, u[1]);
    }
    return double.IsNegativeInfinity(best) ? null : best;
}
int WalkCl(string bridge, string name, double from, double to, double aboveFbBottomIn, string pages, string flag, bool onFlange = false, bool singleRail = false)
{
    var al = alOf[bridge];
    double off = Ft(ClOffsetFt), clear = In(1.75);
    // floor beams crossing the walkway line (not the dam diaphragms of the girder spans), by station
    // (lo, hi) = stations of the floor beam web over the walkway channel height: a floor beam set NORMAL TO GRADE (girder spans, p.43 / p.44 notes) leans,
    // its web lies G x (z - axis) down-grade of its axis station (section 25); plumb beams: lo = hi = the station
    var fbs = new List<(double sta, double lo, double hi, double zBot)>();
    foreach (var p in model0[bridge].Steel!.WholeMembers().Where(p => p.Category == SBM.Core.Steel.PartCategory.TrussFloorBeam && !p.Mark.StartsWith("dam ") && p.Mesh is { IsEmpty: false }))
    {
        var bb = p.Mesh.Bounds();
        var c = al.Inverse(new SBM.Core.Geometry.Vec2((bb.Min.X + bb.Max.X) / 2, (bb.Min.Y + bb.Max.Y) / 2));
        if (c.Station < from - 0.01 || c.Station > to + 0.01) continue;
        var e1 = al.Inverse(new SBM.Core.Geometry.Vec2(bb.Min.X, bb.Min.Y)); var e2 = al.Inverse(new SBM.Core.Geometry.Vec2(bb.Max.X, bb.Max.Y));
        if (Math.Max(e1.Offset, e2.Offset) < off + In(6) || Math.Min(e1.Offset, e2.Offset) > off - In(6)) continue;
        if ((onFlange ? FbFlangeTopAt(p, al, c.Station, off) : FbBottomAt(p, al, c.Station, off)) is not { } zb) continue;   // onFlange: channel bottom on the bottom flange stack
        if (fbs.Any(f => Math.Abs(f.sta - c.Station) < 0.3)) continue;
        var tdef = bridges[bridge].Truss!;
        var mdef = tdef.Members.FirstOrDefault(mm => mm.Name == p.Mark);
        double lean = mdef is { NormalToGrade: true } ? al.Profile.GradeAt(c.Station) : 0;
        // a two-web (box) floor beam (main span F7 at W22, p.93): the pieces stop clear of its OUTER web faces (the rule below is 1 3/4 in. from a single web CL)
        var sdef = mdef == null ? null : tdef.Sections.FirstOrDefault(sc => sc.Name == mdef.Section);
        double boxHalf = sdef is { Kind: TrussSectionKind.Box, WebsOutToOut: > 0 } ? sdef.WebsOutToOut / 2 : 0;
        double zc = (bb.Min.Z + bb.Max.Z) / 2, w1 = c.Station - lean * (zb - zc), w2 = c.Station - lean * (zb + In(11) - zc);
        fbs.Add((c.Station, Math.Min(w1, w2) - boxHalf, Math.Max(w1, w2) + boxHalf, zb));
    }
    fbs = fbs.OrderBy(f => f.sta).ToList();
    if (fbs.Count == 0) { Print($"{bridge}: {name} - no floor beam on the walkway line"); return 0; }
    // stops: the run ends + both web faces of every floor beam
    var cuts = new List<(double a, double b, double zA, double zB)>();
    double prev = from, zPrev = fbs[0].zBot;
    foreach (var f in fbs)
    {
        if (f.lo - clear > prev + In(6)) cuts.Add((prev, f.lo - clear, zPrev, f.zBot));
        prev = Math.Max(prev, f.hi + clear); zPrev = f.zBot;
    }
    if (to > prev + In(6)) cuts.Add((prev, to, zPrev, zPrev));
    int k = 0;
    foreach (var (a, b, zA, zB) in cuts)
    {
        double sm = (a + b) / 2;
        double vTop = ((zA + zB) / 2 - al.ElevationAt(sm, off)) / In(1) + aboveFbBottomIn;
        Walk(bridge, $"{name} {++k}", false, off, a, b, vTop, true, pages, flag, singleRail);
    }
    return k;
}

// safety ladder (p.101 / p.102): 2 stiles L 3x2 1/2x3/8 (16 in. apart, rungs 3/4 in. dia. @ 12 in. along the alignment) at (station, offset), from zBot to zTop (m, absolute)
string ladderFlag = U("p.101, p.102", "safety ladder width, top extension and cage", "the ladder material is listed (Ls 3x2 1/2x3/8, 3/4 in. rungs 12 in. ctrs, cage bars 2 1/2 x 3/8, hoops @ 5'-0); the stile spacing and the extension above the walkway are drawn only",
    "stiles 1'-4 apart, rungs from 12 in. above the platform at 12 in., stiles 3'-6 above the walkway grating; cage bars / hoops, bent hand bars and the anchors not built.");
// half = inner face of the stiles from the ladder CL (in.; p.101 / p.102 approach 8, main span p.103 / p.104 '9 | 9'); flag = the ladder's own flag text
void Ladder(string bridge, string name, double sta, double off, double zBot, double zTop, string pages, double half = 8, string? flag = null)
{
    var t = bridges[bridge].Truss!;
    string desc = $"{bridge} - safety ladders ({pages})";
    string lf = flag ?? ladderFlag;
    double zb = zBot / In(1), zt = (zTop + FI(3, 6)) / In(1);
    foreach (int sd in new[] { -1, 1 })
    {
        // stile angle: 3 in. leg across (transverse range = offsets), 2 1/2 in. leg along the alignment (U)
        t.LineItems!.Add(Li($"{name} stile {(sd < 0 ? "B" : "A")} 1", "LADDERS", desc, PartCategory.WalkwayPost, true, sta, off - In(1.5), off + In(1.5), false,
            P(sd * half, zb, sd * (half + 0.375), zb, sd * (half + 0.375), zt, sd * half, zt), pages, lf));
        t.LineItems.Add(Li($"{name} stile {(sd < 0 ? "B" : "A")} 2", "LADDERS", desc, PartCategory.WalkwayPost, true, sta, off + In(1.125), off + In(1.5), false,
            P(sd * (half + 0.375), zb, sd * (half + 2.5), zb, sd * (half + 2.5), zt, sd * (half + 0.375), zt), pages, lf));
    }
    var copies = new List<double>();
    for (double z = 12; zb + z < zTop / In(1) - 1e-6; z += 12) copies.Add(In(z));
    double h = 0.3323;   // square of the 3/4 in. rod's area
    t.LineItems!.Add(Li($"{name} rungs", "LADDERS", desc, PartCategory.WalkwayRail, true, sta, off - In(h), off + In(h), false, P(-half, zb - h, half, zb - h, half, zb + h, -half, zb + h), pages, lf, copies: copies));
    var c = counts.GetValueOrDefault(bridge);
    counts[bridge] = (c.walkLf, c.ladders + 1, c.platforms);
}

// inspection platform (p.102 / p.104): grating on 8 [ 13.75 edge channels across, railing on both long sides; centre (station, offset), size along x across (in.), grating top zTop (m)
string platformFlagMain = U("p.104", "main span inspection platform frame and hangers", "INSPECTION PLATFORM AT PIER 18 (View B, Section A) gives 8'-0 along x 3'-3 1/16 across, 5'-4 inboard of the CL truss, grating 8 13/16 in. above the top of pier (El. 87.701; fraction read at 300 dpi); the hangers (4-Ls 4x4x3/8 to the bottom strut), cross channels and conn. Ls 6x6x3/8 are drawn without full dimensions",
    "grating on two 8 [ 13.75 channels along its long edges, rails on those edges, posts at the corners and mid-length; hangers / end rails not built.");
string platformFlag(string pages) => pages == "p.104" ? platformFlagMain : U(pages, "inspection platform position, frame and hangers", "the platform is drawn on the pier top beside the truss shoe with its plan size, 9 3/8 in. from the top of pier to the grating (p.102 Section A); the hangers (2-Ls 4x4x3/8 to the truss), cross channels and the end railing are drawn without full dimensions",
    "grating on two 8 [ 13.75 channels along its long edges, rails on those edges, posts at the corners and mid-length; hangers / end rails / toe plates not built.");
void Platform(string bridge, string name, double sta, double off0, double off1, double alongIn, double zTop, string pages)
{
    var t = bridges[bridge].Truss!;
    string desc = $"{bridge} - inspection platforms ({pages})";
    double h = alongIn / 2, top = zTop / In(1);
    var items = t.LineItems!;
    items.Add(Li($"{name} grating", "PLATFORMS", desc, PartCategory.Grating, true, sta, off0, off1, false, P(-h + 2.343, top - 0.75, h - 2.343, top - 0.75, h - 2.343, top, -h + 2.343, top), pages, platformFlag(pages) + " " + gratingFlag, gratingDensity));
    foreach (int sd in new[] { -1, 1 })
    {
        string s = sd < 0 ? "B" : "A";
        items.Add(Li($"{name} channel {s}", "PLATFORMS", desc, PartCategory.WalkwayStringer, true, sta, off0, off1, false, Channel(sd, top, 8, 2.343, 0.303, 0.39, h), pages, platformFlag(pages), lbft: 13.75));
        items.Add(Li($"{name} top rail {s}", "PLATFORMS", desc, PartCategory.WalkwayRail, true, sta, off0, off1, false, RailAngle(sd, h, top + 35.5, 3, 3, 0.3125), pages, railFlag, lbft: 6.1));
        items.Add(Li($"{name} mid rail {s}", "PLATFORMS", desc, PartCategory.WalkwayRail, true, sta, off0, off1, false, RailAngle(sd, h, top + 17, 2.5, 3, 0.3125), pages, railFlag, lbft: 5.6));
        double len = off1 - off0;
        items.Add(Li($"{name} posts {s}", "PLATFORMS", desc, PartCategory.WalkwayPost, true, sta, off0, off1, false, P(sd * h, top - 8, sd * (h + 3), top - 8, sd * (h + 3), top + 32.5, sd * h, top + 32.5), pages, railFlag,
            pitch: (len - In(3)) / 2, width: In(3), start: 0));
    }
    var c = counts.GetValueOrDefault(bridge);
    counts[bridge] = (c.walkLf, c.ladders, c.platforms + 1);
}

// ================================================================ approach trusses (p.101 / p.102)
// unit: (bridge, sign of 'east' in the sheet's hand, list of (joint, ladder distance from the floor beam station (ft, signed toward the sheet's east), cross walkway extent, ladder / platform))
const double TrussOff = 29.0, LadderOff = 26.0 + 7.0 / 12;      // 58'-0 c.c. trusses, ladders 53'-2 c.c. (2'-5 inside the trusses)
string crossFlag = U("p.101, p.102", "cross walkway station", "the key plans put the cross walkways at the pier floor beams and the ladders 4'-9 3/8 from the CL bearing of the end shoe (piers 10 / 13 / 17; at 13 / 17 the 4-span shoe 2'-0 from the CL pier) or 6'-0 3/8 from the CL pier (piers 11 / 12 / 14 / 15 / 16); the walkway's own station is drawn only",
    "cross walkway centred on the ladder station (pier 13 / 24 of the 3-span unit: beside its end floor beam, no ladder); north half only except piers 10 / 27 (p.101 note 'omit this section of walkways at piers 13, 15 and 17'); NJ units opposite hand along the bridge, still to the north truss.");
string clFlag = U("p.101, p.106", "CL walkway level and floor beam connections", "p.101 / p.106 dimension the walkway 2'-0 wide from 1'-0 to 3'-0 north of the CL bridge (p.101 floor beam elevation: manhole and walk 2'-0 from the CL; p.106: '1'-0 | Walkway 2'-0' from the CL) and draw it seated between the floor beams on their bottom flanges (channels connected to the webs, manhole above); the connection angles / hangers and the exact seat are drawn, not dimensioned",
    "pieces from floor beam web to floor beam web (1 3/4 in. clear of the web CL), centred 2'-0 north of the CL, the 10 [ 15.3 walk channels standing on the floor beam bottom flange (angle legs + cover plates, probed), grating between the channel webs just under their top flanges, rails and posts per piece; manholes not cut, connection angles not built. (Before 2026-10-21 the walkway was centred 1'-0 north, through the webs at the bottom flange: it clashed with the hanger bracing / sway members on the CL.)");
var units = new (string bridge, int east, (string fb, double ladderFt, bool full, bool ladder, bool platformBoth)[] pts)[]
{
    ("PTC 3-span truss 11-13", 1, new[] { ("FB0", 4 + 9.375 / 12, true, true, true), ("FB8", 6 + 0.375 / 12, false, true, false), ("FB8'", 6 + 0.375 / 12, false, true, false), ("FB0'", double.NaN, false, false, false) }),
    ("PTC 4-span truss 14-17", 1, new[] { ("FB0", 4 + 9.375 / 12, false, true, false), ("FB10", 6 + 0.375 / 12, false, true, false), ("FB20", 6 + 0.375 / 12, false, true, false), ("FB10'", 6 + 0.375 / 12, false, true, false), ("FB0'", -(4 + 9.375 / 12), false, true, false) }),
    ("PTC 3-span truss 25-27", -1, new[] { ("FB0", 4 + 9.375 / 12, true, true, true), ("FB8", 6 + 0.375 / 12, false, true, false), ("FB8'", 6 + 0.375 / 12, false, true, false), ("FB0'", double.NaN, false, false, false) }),
    ("PTC 4-span truss 21-24", -1, new[] { ("FB0", 4 + 9.375 / 12, false, true, false), ("FB10", 6 + 0.375 / 12, false, true, false), ("FB20", 6 + 0.375 / 12, false, true, false), ("FB10'", 6 + 0.375 / 12, false, true, false), ("FB0'", -(4 + 9.375 / 12), false, true, false) }),
};
// pier top (= bottom of the truss shoe) at a floor beam station: the nearest shoe seat of the bridge
double PierTop(string bridge, double sta, int north) =>
    model0[bridge].Steel!.BearingSeats.Where(s => s.Location != null)
        .OrderBy(s => Math.Abs(s.Station - sta) + (Math.Sign(alOf[bridge].Inverse(s.Location!.Value).Offset) == north ? 0 : 1000)).First().SeatElevation;
foreach (var (bn, east, pts) in units)
{
    var al = alOf[bn];
    var dk = bridges[bn].Truss!.Floor!.Deck!;
    var fb0 = PartBox(bn, "FB" + (bn.Contains("3-span") ? "8" : "10"));
    if (fb0 == null) { Print($"{bn}: floor beams not found - walkways not built"); continue; }
    double vTop = (fb0.Value.zMin - al.ElevationAt(fb0.Value.sta, 0)) / In(1) + 1.75;    // grating top above the grade (in.)
    WalkCl(bn, "walkway CL", dk.BeginStation, dk.EndStation, 10.25, "p.101", clFlag, onFlange: true);   // 1/4 in. over the flange: the walk follows the 3 % grade, the floor beam flange is level
    foreach (var (fb, ladderFt, full, ladder, both) in pts)
    {
        var box = PartBox(bn, fb);
        if (box == null) { Print($"{bn}: {fb} not found"); continue; }
        double sta = double.IsNaN(ladderFt) ? box.Value.sta - east * (In(9.5) + FI(1, 2)) : box.Value.sta + east * Ft(ladderFt);
        double zw = box.Value.zMin + In(1.75);   // grating top (absolute)
        double from = full ? -Ft(LadderOff) : -Ft(LadderOff), to = full ? Ft(LadderOff) : Ft(ClOffsetFt) - Ft(1);   // north cross walks end at the CL walkway's north edge (3'-0 north)
        Walk(bn, $"cross walkway {fb}", true, sta, from, to, zw / In(1), false, "p.101", crossFlag);
        if (!ladder) continue;
        foreach (int north in both ? new[] { -1, 1 } : new[] { -1 })
        {
            double off = north * Ft(LadderOff);
            double zp = PierTop(bn, box.Value.sta, north) + In(9.375);
            Ladder(bn, $"ladder {fb} {(north < 0 ? "N" : "S")}", sta, off, zp, zw, "p.101, p.102");
            // platform: 7'-0 3/8 across (5'-5 5/16 outboard / 1'-7 1/16 inboard of the ladder, p.102 Section A), 3'-6 3/8 along from 2'-0 beyond the CL bearing
            // (p.102 View B: 2'-0 + 3'-6 3/8 with the ladder 4'-9 3/8 from the bearing = platform from the ladder - 2'-9 3/8 to the ladder + 9"; it was centred
            // on the ladder, which put it over the end shoe masonry plates at piers 13 / 17, QC I023)
            double o0 = north < 0 ? off - FI(5, 5.3125) : off - FI(1, 7.0625), o1 = north < 0 ? off + FI(1, 7.0625) : off + FI(5, 5.3125);
            double dir = Math.Sign(sta - box.Value.sta);
            Platform(bn, $"platform {fb} {(north < 0 ? "N" : "S")}", sta - dir * (FI(2, 9.375) - FI(1, 9.1875)), o0, o1, 42.375, zp, "p.102");
        }
    }
}

// safety cage of a main span ladder (p.103 Section A / plan, p.104 'End Cage'; rev. 1 vertical bars inside the hoops): hoops 2 1/2 x 3/8 bars, R 1'-0 3/8, 'abt. 5'-0 ctrs.',
// from 6'-9 above the platform up to the last one 10 in. under the walkway grating, 5 vertical bars 2 1/2 x 3/8 inside the hoops. inb = the climbing side (+1 = increasing offsets).
// The hoop is a semicircle R 1'-0 3/8 (bar CL) about the ladder CL in the stile plane with its ends beside the stiles: the cage depth and its fastening to the stiles are
// drawn only (flag). Plane items in explicit frames (TrussItemFrame).
void Cage(string bridge, string name, double sta, double off, int inb, double zBot, double zTop, string flag)
{
    var t = bridges[bridge].Truss!;
    t.PlaneItems ??= new();
    string desc = $"{bridge} - safety ladders (p.103, p.104)";
    string cf = flag + " " + U("p.103, p.104", "ladder cage depth", "the hoop radius R 1'-0 3/8 is printed but the cage depth from the rungs and the hoop-to-stile connection are drawn only",
        "semicircular hoops R 1'-0 3/8 (bar CL) about the ladder CL in the stile plane, legs and connection to the stiles not built.");
    double R = In(12.375), z0 = zBot + FI(6, 9), z1 = zTop - In(10);
    if (z1 <= z0) { Print($"{bridge}: {name} - ladder too short for a cage"); return; }
    int n = Math.Max(1, (int)Math.Round((z1 - z0) / Ft(5)));
    double pitch = (z1 - z0) / n;
    var bar = new List<TrussUV> { new() { U = -In(1.25), V = -In(0.1875) }, new() { U = In(1.25), V = -In(0.1875) }, new() { U = In(1.25), V = In(0.1875) }, new() { U = -In(1.25), V = In(0.1875) } };
    for (int k = 0; k <= n; k++)
    {
        // horizontal frame: U = toward the climbing side, V = up-station (left of U when inb = +1), W = up; semicircle through (R, 0) from V = -R to V = +R
        double ang = inb > 0 ? 90 : -90;
        var path = new List<TrussPathVertex> { new(0, -inb * R), new(R, -inb * R, R), new(R, inb * R, R), new(0, inb * R) };
        t.PlaneItems.Add(new TrussPlaneItemDefinition { Name = $"{name} hoop {k + 1}", Label = "cage hoop 2 1/2 x 3/8", Kind = TrussPlaneItemKind.Sweep,
            Frame = new TrussItemFrame { Station = sta, Offset = off, Elevation = z0 + k * pitch, AngleDegrees = ang, Horizontal = true }, Path = path, Section = bar,
            Category = PartCategory.WalkwayRail, AssemblyKind = SBM.Core.Steel.AssemblyKind.InspectionWalkway, Assembly = "LADDERS", AssemblyDescription = desc, Material = "C", Unresolved = cf, Source = "p.103, p.104" });
    }
    // 5 vertical bars inside the hoops (bar outer face on the hoop inner face), equally spaced round the semicircle (30 deg ... 150 deg)
    // bar CL radius: the flat bar's outer corners (+-1 1/4 along the tangent) touch the hoop's inner face (R - 3/16); at R - 3/8 they reached 1/16 in. past it
    double r = Math.Sqrt(Math.Pow(R - In(0.1875), 2) - Math.Pow(In(1.25), 2)) - In(0.1875);
    for (int i = 1; i <= 5; i++)
    {
        double th = i * Math.PI / 6;
        double across = inb * r * Math.Sin(th), along = r * Math.Cos(th);
        // tangent of the hoop there: along = -sin, across = inb cos -> angle from the up-station tangent toward the right
        double angT = Math.Atan2(inb * Math.Cos(th), -Math.Sin(th)) * 180 / Math.PI;
        t.PlaneItems.Add(new TrussPlaneItemDefinition { Name = $"{name} bar {i}", Label = "cage bar 2 1/2 x 3/8", Kind = TrussPlaneItemKind.Plate,
            Frame = new TrussItemFrame { Station = sta + along, Offset = off + across, Elevation = z0, AngleDegrees = angT },
            Outline = new List<TrussUV> { new() { U = -In(1.25), V = 0 }, new() { U = In(1.25), V = 0 }, new() { U = In(1.25), V = z1 - z0 }, new() { U = -In(1.25), V = z1 - z0 } },
            W0 = -In(0.1875), W1 = In(0.1875),
            Category = PartCategory.WalkwayPost, AssemblyKind = SBM.Core.Steel.AssemblyKind.InspectionWalkway, Assembly = "LADDERS", AssemblyDescription = desc, Material = "C", Unresolved = cf, Source = "p.103, p.104" });
    }
}

// ================================================================ main span (p.103 - p.106; batch 6 2026-10-28, qc/ptc/main-span/TODO.md 6a-6c)
// generic helpers: the highest steel top a vertical ray meets at (station, offset) among the given parts, within [zLo, zHi]
double? TopAt(IEnumerable<SBM.Core.Steel.SteelPart> parts, SBM.Core.Alignments.Alignment al, double sta, double off, double zLo, double zHi)
{
    var xy = al.PointAt(sta, off) + new SBM.Core.Geometry.Vec2(1.37e-6, -0.83e-6);
    double best = double.NegativeInfinity;
    foreach (var p in parts)
    {
        var bb = p.Mesh.Bounds();
        if (xy.X < bb.Min.X || xy.X > bb.Max.X || xy.Y < bb.Min.Y || xy.Y > bb.Max.Y || bb.Max.Z < zLo || bb.Min.Z > zHi) continue;
        var pos = p.Mesh.Positions; var idx = p.Mesh.Indices;
        for (int i = 0; i + 2 < idx.Count; i += 3)
        {
            var A = pos[idx[i]]; var B = pos[idx[i + 1]]; var C = pos[idx[i + 2]];
            double d = (B.Y - C.Y) * (A.X - C.X) + (C.X - B.X) * (A.Y - C.Y);
            if (Math.Abs(d) < 1e-14) continue;
            double l1 = ((B.Y - C.Y) * (xy.X - C.X) + (C.X - B.X) * (xy.Y - C.Y)) / d, l2 = ((C.Y - A.Y) * (xy.X - C.X) + (A.X - C.X) * (xy.Y - C.Y)) / d, l3 = 1 - l1 - l2;
            if (l1 < -1e-9 || l2 < -1e-9 || l3 < -1e-9) continue;
            double z = l1 * A.Z + l2 * B.Z + l3 * C.Z;
            if (z >= zLo && z <= zHi) best = Math.Max(best, z);
        }
    }
    return double.IsNegativeInfinity(best) ? null : best;
}
// rail angle / post / toe plate pieces of a railed edge (heights in in. above the floor zF (m)); along = the edge runs along the alignment at offset 'at'
// (sd = outward side, +1 right), else across at station 'at' (sd = outward along the alignment); from / to = range (stations or offsets)
string railMainFlag = U("p.105, p.106", "hand railing of the main span platforms", "MATERIAL FOR PLATFORM lists top rail 3x3x5/16, mid rail 3x2 1/2x5/16, posts 3x3x5/16, toe PL 5x5/16; p.105 prints top rail 3'-0 / mid rail 1'-9 above the U22 platform, p.106 Section B 1'-6 + 1'-6 above the navigation light platform grating (its end elevation 1'-10 1/2 + 1'-10 1/2 = 3'-9: reported as a CONFLICT); post positions and the toe plate height are drawn only",
    "rails with the horizontal leg outward, posts L 3x3x5/16 at the drawn positions (corners, mid-sides, 6'-0 on the U22 strut), toe plates 5 in. high from the floor on the outside face of the posts (bolting not built).");
void Edge(string bridge, string asm, string desc, string name, bool along, double at, int sd, double from, double to, double zF, double topIn, double midIn, bool toe, string pages, string flag)
{
    var t = bridges[bridge].Truss!;
    double f = zF / In(1);
    double atIn = along ? 0 : at;            // transverse items: U = along the alignment about the station 'at'
    double refOff = along ? at : 0;
    double station = along ? 0 : at;
    TrussLineItemDefinition Item(string nm, PartCategory cat, List<TrussUV> prof, double lbft) => along
        ? Li(nm, asm, desc, cat, false, refOff, from, to, false, prof, pages, flag, lbft: lbft)
        : Li(nm, asm, desc, cat, true, station, from, to, false, prof, pages, flag, lbft: lbft);
    // profile U = across (along items, offset from refOff) or along the alignment (across items, from the station): rails at U = 0, outward = sd
    t.LineItems!.Add(Item($"{name} top rail", PartCategory.WalkwayRail, RailAngle(sd, 0, f + topIn, 3, 3, 0.3125), 6.1));
    t.LineItems.Add(Item($"{name} mid rail", PartCategory.WalkwayRail, RailAngle(sd, 0, f + midIn, 2.5, 3, 0.3125), 5.6));
    // toe plate on the OUTSIDE face of the posts (the posts' legs fill the 3 in. inside the edge line)
    if (toe) t.LineItems.Add(Item($"{name} toe plate", PartCategory.WalkwayPlate, P(0, f, sd * 0.3125, f, sd * 0.3125, f + 5, 0, f + 5), 5.3));
}
// a post L 3x3x5/16 at (station, offset) from zF up to the top rail (in.)
void Post(string bridge, string asm, string desc, string name, double sta, double off, int sdAlong, int sdAcross, double zF, double topIn, string pages, string flag)
{
    var t = bridges[bridge].Truss!;
    double f = zF / In(1);
    double o0 = Math.Min(off, off + sdAcross * In(3)), o1 = Math.Max(off, off + sdAcross * In(3));
    t.LineItems!.Add(Li($"{name} a", asm, desc, PartCategory.WalkwayPost, true, sta, o0, o1, false, P(0, f, sdAlong * 0.3125, f, sdAlong * 0.3125, f + topIn - 3, 0, f + topIn - 3), pages, flag));
    double p0 = Math.Min(off, off + sdAcross * In(0.3125)), p1 = Math.Max(off, off + sdAcross * In(0.3125));
    t.LineItems.Add(Li($"{name} b", asm, desc, PartCategory.WalkwayPost, true, sta, p0, p1, false, P(sdAlong * 0.3125, f, sdAlong * 3, f, sdAlong * 3, f + topIn - 3, sdAlong * 0.3125, f + topIn - 3), pages, flag));
}
{
    string bn = "PTC Arch truss 18-20";
    var al = alOf[bn];
    var tdef = bridges[bn].Truss!;
    var steel0 = model0[bn].Steel!;
    double PtSta(string n) => tdef.Points.First(p => p.Name == n).Station;
    double PtZ(string n) => tdef.Points.First(p => p.Name == n).Elevation;
    double trussN = tdef.Planes.First(p => p.Name == "N").Offset, trussS = tdef.Planes.First(p => p.Name == "S").Offset;
    // top chord steel incl. the chord splice / fill plates on it (catwalk posts stand on whichever is highest)
    var chordParts = steel0.Parts.Where(p => p.Category is PartCategory.TrussChord or PartCategory.SplicePlate or PartCategory.FillerPlate && p.Mesh is { IsEmpty: false }).ToList();
    var fb15 = PartBox(bn, "FB15");
    var fb15p = PartBox(bn, "FB15'");
    // cross walk grating top per floor beam (kept from before: walk channels at the floor beam bottom flange level)
    var crossTop = new Dictionary<string, (double sta, double z)>();
    if (fb15 != null && fb15p != null)
    {
        WalkCl(bn, "walkway CL W15-W15'", Math.Min(fb15.Value.sta, fb15p.Value.sta), Math.Max(fb15.Value.sta, fb15p.Value.sta), 36.5, "p.106",
            U("p.106", "main span CL walkway", "the key plan for navigation light supports and walkways draws the CL walkway from W15 to W15' (material 10 [ 15.3, grating, rails), the plan of walkway at W15 puts it 1'-0 to 3'-0 north of the CL bridge and the expansion detail at piers 17 / 20 draws the walk channels 2'-2 1/2 above the main floor beam bottom, connected to the webs; the connections are drawn, not dimensioned",
                "pieces from floor beam web to floor beam web (1 3/4 in. clear of the web CL), centred 2'-0 north of the CL, grating top 3'-0 1/2 above the floor beam bottom; connection angles not built. (Before 2026-10-21: centred 1'-0 north at the floor beam bottom, clashing with the floor lateral plates.)"), singleRail: true);
        // 6c (BW-W11): the cross walks keep the 10 [ 15.3 of the p.106 note ('Cross walks, hand rail, support & material similar to cross walk at pier 10, see Sht. 61' = p.101);
        // the p.106 WALKWAY MATERIAL list (8 [ 13.75, conn. Ls 6x6x3/8) points at the walk along the wind chord to the light platform (built below with 8 [). Reported as a CONFLICT.
        string cwConflict = "CONFLICT (p.106 vs p.101): cross walk channels - the p.106 WALKWAY MATERIAL list gives 8 [ 13.75 (its leader points at the walk along the wind chord), the p.106 note says the cross walks are 'similar to cross walk at pier 10, see Sht. 61' = p.101 10 [ 15.3; the model keeps 10 [ 15.3 for the cross walks and uses 8 [ 13.75 for the walks along the wind chord. ";
        // 6b: W20 / W19' are 'similar' to W15 (p.106): the cross walk lies on the light side of the floor beam (the lights are 17'-0 / 12'-0 / 7'-0 UP-station of W15 / W20 / W19',
        // key plan) - W19' was mirrored to the down-station side before; W15' (no light) keeps the down-station side
        foreach (var j in new[] { "15", "20", "19'", "15'" })
        {
            var box = PartBox(bn, "FB" + j);
            if (box == null) continue;
            int side = j == "15'" ? -1 : 1;
            double sta = box.Value.sta + side * (In(9.5) + FI(1, 3));
            double zc = box.Value.zMin + In(1.75);
            crossTop[j] = (sta, zc);
            string cwFlag = U("p.106", $"cross walk at W{j} to the navigation light supports", "the key plan shows cross walks at W15, W20 and W19' to the navigation light platforms on the wind chords, 40'-9 1/2 each side of the CL; the plan of walkway at W15 ends them at the longitudinal walkway (1'-0 / 3'-0 north of the CL); the cross walk's level and its station beside the floor beam are drawn, not dimensioned",
                "2'-0 walkway beside the floor beam (light side) at its bottom flange level, from 40'-9 1/2 north / south of the CL to the longitudinal walkway's edges.");
            Walk(bn, $"cross walk W{j} N", true, sta, -FI(40, 9.5), Ft(ClOffsetFt) - Ft(1), zc / In(1), false, "p.106", cwConflict + cwFlag);
            Walk(bn, $"cross walk W{j} S", true, sta, Ft(ClOffsetFt) + Ft(1), FI(40, 9.5), zc / In(1), false, "p.106", cwConflict + cwFlag);
        }
    }

    // 6b (BW-W3, W13): NAVIGATION LIGHT SUPPORT platforms (p.106 Section B, key plan; north and south wind chords at W15 / W20 / W19'): light 17'-0 / 12'-0 / 7'-0 up-station
    // of the panel point; platform 6'-0 along from 6 in. down-station of the light, 4'-6 1/2 across from 10 in. outboard of the wind chord CL; grating 3/4 on 8 [ 13.75 frame
    // seated on the wind chord top flange (5/16 fill), 'platform to be level'; walk along the wind chord (8 [ 13.75, 2'-0, 1'-8 1/2 - 3'-8 1/2 inboard of the wind chord CL) from the
    // cross walk to the platform
    string navFlag = U("p.106", "navigation light platform frame, level and hangers", "Section B gives the platform 6'-0 x 4'-6 1/2 (10 in. + 3'-8 1/2 about the wind chord CL), the light 6 in. inside its down-station end and 1'-9 outboard of the wind chord CL, '17'-0 horizontal' from the floor beam (key plan: 17'-0 / 12'-0 / 7'-0 at W15 / W20 / W19'), 'platform to be level' with 'fill as reqd'; the frame channels, the hangers Ls 4x4x3/8 (conn. to str. 8x4x7/16 Ls), the 12 [ 25 light post and the light are drawn without positions",
        "grating top level with the 8 [ 13.75 frame top, the frame seated (5/16 fill) on the highest wind chord top under the platform; channels along the two long edges; rails + toe plates on the outboard side and both ends (inboard side open to the walk); hangers, fills at the low end, light post and light not built.");
    string navWalkFlag = U("p.106", "walk along the wind chord to the navigation light platform", "the plan of walkway at W15 draws a 2'-0 walk (WALKWAY MATERIAL 8 [ 13.75, conn. Ls 6x6x3/8, grating 3/4 x 3/16, rails, posts @ 6'-0 +-) 1'-8 1/2 to 3'-8 1/2 inboard of the wind chord CL from the cross walk to the platform; its level is not given - the Navigation Light Support plan marks the walk 'Parallel to Wind Chord' and the platform 'to be Level' (re-read 2026-10-30, batch 8f / recheck N2: the walk climbs with the wind chord to the platform, no step is drawn)",
        "2'-0 walk on two 8 [ 13.75 from the cross walk grating level at its edge, rising in a straight line to the platform grating at the platform's down-station end (the wind chord between is not followed point by point; posts L 3x3x5/16 @ 6'-0); connection angles not built.");
    var navSupport = new[] { ("15", 17.0), ("20", 12.0), ("19'", 7.0) };
    foreach (var (j, dFt) in navSupport)
    {
        if (!crossTop.TryGetValue(j, out var cw)) continue;
        double light = PtSta("W" + j) + Ft(dFt);
        double a0 = light - In(6), a1 = light + FI(5, 6);
        foreach (int sd in new[] { -1, 1 })        // -1 = north
        {
            string s = sd < 0 ? "N" : "S";
            double wc = sd < 0 ? trussN : trussS;
            int inb = -sd;
            double oOut = wc + sd * In(10), oIn = wc + inb * FI(3, 8.5);
            double o0 = Math.Min(oOut, oIn), o1 = Math.Max(oOut, oIn), oc = (o0 + o1) / 2, halfW = (o1 - o0) / 2 / In(1);
            double zW = PtZ("W" + j);
            var tops = new[] { a0, light, a1 }.Select(st => TopAt(chordParts, al, st, wc, zW - 0.3, zW + 0.6)).Where(z => z != null).Select(z => z!.Value).ToList();
            if (tops.Count == 0) { Print($"{bn}: wind chord top not found under the navigation light platform W{j} {s} - not built"); continue; }
            double zg = tops.Max() + In(0.3125) + In(8);
            string desc = $"{bn} - navigation light platforms (p.106)";
            var items = tdef.LineItems!;
            items.Add(Li($"nav platform W{j} {s} grating", "PLATFORMS", desc, PartCategory.Grating, false, oc, a0, a1, false, P(-halfW + 2.343, zg / In(1) - 0.75, halfW - 2.343, zg / In(1) - 0.75, halfW - 2.343, zg / In(1), -halfW + 2.343, zg / In(1)), "p.106", navFlag + " " + gratingFlag, gratingDensity));
            foreach (int e in new[] { -1, 1 })
                items.Add(Li($"nav platform W{j} {s} channel {(e < 0 ? "L" : "R")}", "PLATFORMS", desc, PartCategory.WalkwayStringer, false, oc, a0, a1, false, Channel(e, zg / In(1), 8, 2.343, 0.303, 0.39, halfW), "p.106", navFlag, lbft: 13.75));
            // rails: outboard long side (at oOut, outward = sd) and the two ends (across, outward along the alignment), posts at the corners + mid outboard side
            Edge(bn, "PLATFORMS", desc, $"nav platform W{j} {s} outboard", true, oOut, sd, a0, a1, zg, 36, 18, true, "p.106", railMainFlag);
            Edge(bn, "PLATFORMS", desc, $"nav platform W{j} {s} end A", false, a0, -1, o0, o1, zg, 36, 18, true, "p.106", railMainFlag);
            Edge(bn, "PLATFORMS", desc, $"nav platform W{j} {s} end B", false, a1, 1, o0, o1, zg, 36, 18, true, "p.106", railMainFlag);
            int k = 0;
            foreach (var (pst, sa) in new[] { (a0, 1), ((a0 + a1) / 2 - In(1.5), 1), (a1, -1) })
                Post(bn, "PLATFORMS", desc, $"nav platform W{j} {s} post {++k}", pst, oOut, sa, inb, zg, 36, "p.106", railMainFlag);
            Post(bn, "PLATFORMS", desc, $"nav platform W{j} {s} post {++k}", a0, oIn, 1, sd, zg, 36, "p.106", railMainFlag);
            Post(bn, "PLATFORMS", desc, $"nav platform W{j} {s} post {++k}", a1, oIn, -1, sd, zg, 36, "p.106", railMainFlag);
            // walk along the wind chord: cross walk edge (its station + 1'-0) -> platform end
            double ow = wc + inb * FI(2, 8.5);
            double w0 = cw.sta + Ft(1), w1 = a0;
            if (w1 - w0 > In(6))
            {
                var t8 = tdef.LineItems!;
                string wd = $"{bn} - service walkways (p.106)";
                double top = cw.z / In(1);
                t8.Add(Li($"nav walk W{j} {s} grating", "WALKWAYS", wd, PartCategory.Grating, false, ow, w0, w1, false, P(-12 + 2.343, top - 0.75, 12 - 2.343, top - 0.75, 12 - 2.343, top, -12 + 2.343, top), "p.106", navWalkFlag + " " + gratingFlag, gratingDensity));
                foreach (int e in new[] { -1, 1 })
                {
                    string es = e < 0 ? "L" : "R";
                    t8.Add(Li($"nav walk W{j} {s} channel {es}", "WALKWAYS", wd, PartCategory.WalkwayStringer, false, ow, w0, w1, false, Channel(e, top, 8, 2.343, 0.303, 0.39, 12), "p.106", navWalkFlag, lbft: 13.75));
                    t8.Add(Li($"nav walk W{j} {s} top rail {es}", "WALKWAYS", wd, PartCategory.WalkwayRail, false, ow, w0, w1, false, RailAngle(e, 15, top + 36, 3, 3, 0.3125), "p.106", railFlag, lbft: 6.1));
                    t8.Add(Li($"nav walk W{j} {s} mid rail {es}", "WALKWAYS", wd, PartCategory.WalkwayRail, false, ow, w0, w1, false, RailAngle(e, 15, top + 18, 2.5, 3, 0.3125), "p.106", railFlag, lbft: 5.6));
                    // batch 8f (recheck N1, 2026-10-30): posts L 3x3x5/16 @ 6'-0 +- (p.101 / p.102 rail material, as the cross walks) - the rails floated without them
                    double nlen = w1 - w0, npitch = Ft(6);
                    int nn = Math.Max(1, (int)Math.Floor((nlen - In(12)) / npitch) + 1);
                    double nst = (nlen - (nn - 1) * npitch - In(3)) / 2;
                    t8.Add(Li($"nav walk W{j} {s} posts {es} a", "WALKWAYS", wd, PartCategory.WalkwayPost, false, ow, w0, w1, false, P(e * 12, top - 10, e * 15, top - 10, e * 15, top + 33, e * 12, top + 33), "p.106", railFlag,
                        pitch: npitch, width: In(0.3125), start: nst));
                    t8.Add(Li($"nav walk W{j} {s} posts {es} b", "WALKWAYS", wd, PartCategory.WalkwayPost, false, ow, w0, w1, false, P(e * 12, top - 10, e * 12.3125, top - 10, e * 12.3125, top + 33, e * 12, top + 33), "p.106", railFlag,
                        pitch: npitch, width: In(3 - 0.3125), start: nst + In(0.3125)));
                }
                // 'Parallel to Wind Chord' (Navigation Light Support plan): the walk climbs from the cross walk level at its start to the level platform grating at its end
                foreach (var it in t8.Where(x => x.Name.StartsWith($"nav walk W{j} {s} "))) it.Rise = zg - cw.z;
                var c0 = counts.GetValueOrDefault(bn);
                counts[bn] = (c0.walkLf + (w1 - w0) / 0.3048, c0.ladders, c0.platforms);
            }
            Print($"{bn}: navigation light platform W{j} {s}: grating top El {zg / 0.3048:0.000} ft, cross walk / walk along the chord El {cw.z / 0.3048:0.000} ft (walk rises {(zg - cw.z) / In(1):0.0} in. to the platform), walk {Math.Max(0, w1 - w0) / 0.3048:0.0} ft");
            var cc = counts.GetValueOrDefault(bn);
            counts[bn] = (cc.walkLf, cc.ladders, cc.platforms + 1);
        }
    }

    // 6b (BW-W1): CAT WALK FROM U0 TO U22 ON NORTH TRUSS (WEST HALF OF SPAN ONLY), p.105: two 1/2 in. wire rope handlines 1'-0 each side of the north truss CL
    // (Section A, plan at U0 / U22), 3'-0 above the top chord on posts 1-L 2 1/2 x 2 1/2 x 3/8 'about 10'-4 ctrs. measured horiz.' (TYPICAL POST ON TOP CHORD)
    {
        double u0 = PtSta("U0"), u22 = PtSta("U22");
        int n = (int)Math.Round((u22 - u0) / FI(10, 4));
        double pitch = (u22 - u0) / n;
        string cwkFlag = U("p.105", "top chord catwalk posts and rope", $"the posts are 'about 10'-4 ctrs. measured horiz.' (no count), 3'-0 high with the rope through a hole at the top, on 1-L 6x4x5/8 (no length); the end posts at U0 / U22 (8 [ 13.75 inclined, sheave, PL 8x3/8, fill, pin, 3/4 turnbuckle + open socket, 3/4 bent PL, 7:12 backstay) are drawn without lengths; the 19-wire strand weight is not printed",
            $"{n - 1} posts per line at {pitch / 0.3048:0.000} ft (equal spaces U0 - U22), plumb, base on the top chord steel, top at the rope CL 3'-0 above it; rope = a solid 1/2 in. round from U0 to U22 through the post tops (steel density); base angles, end posts and rope hardware not built.");
        string desc = $"{bn} - top chord catwalk (p.105)";
        double ZcAt(double st)
        {
            // chord CL elevation interpolated between the upper chord points (window for the probe)
            var ups = tdef.Points.Where(p => p.Name.StartsWith("U") && !p.Name.Contains("'") && int.TryParse(p.Name.Substring(1), out _)).OrderBy(p => p.Station).ToList();
            for (int i = 0; i + 1 < ups.Count; i++)
                if (st >= ups[i].Station - 1e-6 && st <= ups[i + 1].Station + 1e-6)
                    return ups[i].Elevation + (ups[i + 1].Elevation - ups[i].Elevation) * (st - ups[i].Station) / (ups[i + 1].Station - ups[i].Station);
            return ups.Last().Elevation;
        }
        var ropeSec = Enumerable.Range(0, 12).Select(i => new TrussUV { U = In(0.25) * Math.Cos(i * Math.PI / 6), V = In(0.25) * Math.Sin(i * Math.PI / 6) }).ToList();
        tdef.PlaneItems ??= new();
        tdef.PlaneItems.RemoveAll(pi => pi.Assembly is "WALKWAYS" or "LADDERS" or "PLATFORMS");
        foreach (int sd in new[] { -1, 1 })
        {
            double o = trussN + sd * Ft(1);
            string ln = sd < 0 ? "N" : "S";
            var path = new List<TrussPathVertex>();
            double? lastTop = null;
            for (int k = 0; k <= n; k++)
            {
                double st = u0 + k * pitch, zc = ZcAt(st);
                // the post's footprint: highest chord steel under it (heel at the rope line, legs 2 1/2 in.)
                // grid over both legs (along leg o +- 3/16, across leg to o + sd x 2 1/2): the chord top is not level across (cover plates), a probe on the rope line only
                // let the leg edge toward the truss CL sink 3/16 in. into the cover plate
                var hits = new[] { -In(1.25), -In(0.625), 0, In(0.625), In(1.25) }.SelectMany(du => new[] { -In(0.1875), 0.0, In(0.1875), sd * In(1.25), sd * In(2.5) }.Select(dv => TopAt(chordParts, al, st + du, o + dv, zc, zc + 1.5)))
                    .Where(z => z != null).Select(z => z!.Value).ToList();
                double? top = hits.Count > 0 ? hits.Max() : lastTop;
                if (top == null) continue;
                lastTop = top;
                path.Add(new TrussPathVertex(st - u0, top.Value + Ft(3)));
                if (k == 0 || k == n) continue;
                // post: along leg on the rope line (3/8 across), across leg outboard (away from the chord CL) at the down-station end
                double f = top.Value / In(1), zt = f + 36;
                tdef.LineItems!.Add(Li($"catwalk post {ln}{k:00} a", "WALKWAYS", desc, PartCategory.WalkwayPost, true, st, o - In(0.1875), o + In(0.1875), false, P(-1.25, f, 1.25, f, 1.25, zt, -1.25, zt), "p.105", cwkFlag));
                double q0 = Math.Min(o + sd * In(0.1875), o + sd * In(2.5 - 0.1875)), q1 = Math.Max(o + sd * In(0.1875), o + sd * In(2.5 - 0.1875));
                tdef.LineItems.Add(Li($"catwalk post {ln}{k:00} b", "WALKWAYS", desc, PartCategory.WalkwayPost, true, st, q0, q1, false, P(-1.25, f, -0.875, f, -0.875, zt, -1.25, zt), "p.105", cwkFlag));
            }
            if (path.Count >= 2)
                tdef.PlaneItems.Add(new TrussPlaneItemDefinition { Name = $"catwalk rope {ln}", Label = "1/2 in. wire rope handline (19-wire galvanized strand)", Kind = TrussPlaneItemKind.Sweep,
                    Frame = new TrussItemFrame { Station = u0, Offset = o, Elevation = 0 }, Path = path, Section = ropeSec, Category = PartCategory.InspectionCable,
                    AssemblyKind = SBM.Core.Steel.AssemblyKind.InspectionWalkway, Assembly = "WALKWAYS", AssemblyDescription = desc, Material = "Galvanized 19-wire strand", Unresolved = cwkFlag, Source = "p.105" });
        }
        Print($"{bn}: top chord catwalk U0 - U22: 2 lines x {n - 1} posts @ {pitch / 0.3048:0.000} ft");
    }

    // 6b (BW-W2, W13): U22 flashing air beacon platform 6'-6 across x 5'-1 along on the strut at U22 + hand railing on the strut from the north truss (p.105 plan at U22, Section B):
    // posts @ 6 of 6'-0 from 4'-3 inboard of the north truss CL to the platform edge 3'-3 north of the CL, rails 1'-6 each side of the strut CL; top rail 3'-0 / mid rail 1'-9 / toe PL 5x5/16
    {
        var strut = PartBox(bn, "US-U22");
        double u22 = PtSta("U22");
        if (strut == null) Print($"{bn}: strut US-U22 not found - U22 platform not built");
        else
        {
            string bFlag = U("p.105", "U22 beacon platform floor and fastening", "the platform (6'-6 x 5'-1) and the strut railing stand on the strut at U22 (El. 262.762 = the U22 working point); no floor plate / grating is listed in MATERIAL FOR PLATFORM and the floor level, the 1-L 7x4x3/8 / clip Ls 6x4x3/8 and the beacon bracket are drawn only",
                "railing posts, rails and toe plates standing on the top of the strut steel (highest top under the platform); the strut railing heights as the platform's; beacon (by others) and clip angles not built.");
            // floor = the highest steel top under the platform footprint (strut + its connection / filler plates at U22), probed on a 1 ft grid
            var notOwn = steel0.Parts.Where(p => p.Mesh is { IsEmpty: false } && !(p.Assembly.Contains("WALKWAYS") || p.Assembly.Contains("LADDERS") || p.Assembly.Contains("PLATFORMS"))).ToList();
            double zF = strut.Value.zMax;
            for (double ds = -FI(2, 6.5); ds <= FI(2, 6.5) + 1e-6; ds += FI(2, 6.5) / 3)
                for (double dof = -FI(3, 3); dof <= FI(3, 3) + 1e-6; dof += FI(3, 3) / 3)
                    if (TopAt(notOwn, al, PtSta("U22") + ds, dof, strut.Value.zMax - 0.3, strut.Value.zMax + 0.5) is { } zt && zt > zF) zF = zt;
            string desc = $"{bn} - U22 beacon platform (p.105)";
            double h = FI(2, 6.5), oE = FI(3, 3);
            // platform: E / W sides across (outward along the alignment), S side along at +3'-3, N side: two pieces outside the strut walk (+-1'-6)
            Edge(bn, "PLATFORMS", desc, "U22 platform W", false, u22 - h, -1, -oE, oE, zF, 36, 21, true, "p.105", bFlag + " " + railMainFlag);
            Edge(bn, "PLATFORMS", desc, "U22 platform E", false, u22 + h, 1, -oE, oE, zF, 36, 21, true, "p.105", bFlag + " " + railMainFlag);
            Edge(bn, "PLATFORMS", desc, "U22 platform S", true, oE, 1, u22 - h, u22 + h, zF, 36, 21, true, "p.105", bFlag + " " + railMainFlag);
            Edge(bn, "PLATFORMS", desc, "U22 platform N1", true, -oE, -1, u22 - h, u22 - FI(1, 6), zF, 36, 21, true, "p.105", bFlag + " " + railMainFlag);
            Edge(bn, "PLATFORMS", desc, "U22 platform N2", true, -oE, -1, u22 + FI(1, 6), u22 + h, zF, 36, 21, true, "p.105", bFlag + " " + railMainFlag);
            int k = 0;
            foreach (var (ps, sa) in new[] { (u22 - h, 1), (u22 + h, -1) })
                foreach (var (po, sx) in new[] { (-oE, 1), (0.0, 1), (oE, -1) })
                    Post(bn, "PLATFORMS", desc, $"U22 platform post {++k}", ps, po, sa, sx, zF, 36, "p.105", bFlag + " " + railMainFlag);
            // strut walk railing (no floor of its own: the strut top), both sides of the strut CL
            double r0 = trussN + FI(4, 3), r1 = -oE;
            foreach (int sa in new[] { -1, 1 })
            {
                double rs = u22 + sa * FI(1, 6);
                Edge(bn, "PLATFORMS", desc, $"U22 strut rail {(sa < 0 ? "W" : "E")}", false, rs, sa, r0, r1, zF, 36, 21, false, "p.105", bFlag + " " + railMainFlag);
                for (int i = 0; i <= 6; i++)
                    Post(bn, "PLATFORMS", desc, $"U22 strut post {(sa < 0 ? "W" : "E")}{i}", rs, r0 + i * Ft(6), -sa, i == 6 ? -1 : 1, zF, 36, "p.105", bFlag + " " + railMainFlag);
            }
            Print($"{bn}: U22 platform on the strut top El {zF / 0.3048:0.000} ft (U22 working point El {PtZ("U22") / 0.3048:0.000}, p.105 El. 262.762)");
            var cc = counts.GetValueOrDefault(bn);
            counts[bn] = (cc.walkLf, cc.ladders, cc.platforms + 1);
        }
    }

    // walkway on the bottom strut, walkway at the floor beam at L11 / L11', pier platforms and ladders (p.103 / p.104)
    foreach (var (j, east) in new[] { ("L11", 1), ("L11'", -1) })
    {
        string fbj = "FB" + j.Substring(1);
        var strut = PartBox(bn, "LS-" + j);
        if (strut != null)
            Walk(bn, $"walkway on strut {j}", true, strut.Value.sta, -Ft(11), Ft(11), strut.Value.zMax / In(1) + 0.75, false, "p.103, p.104",
                U("p.103", $"walkway on the bottom strut {j}", "the key plan gives 22'-0 x 2'-0 on the strut; its fastening is drawn only", "2'-0 walkway centred on the strut top, 22'-0 long."));
        var fb = PartBox(bn, fbj);
        if (fb == null) continue;
        // 6a (BW-W4, W5): p.103 Section A - walkway 3'-0 3/8 to 5'-0 3/8 from the CL floor beam (4'-0 3/8 / 5'-0 3/8, walk 2'-0), grating top El. 141.6035 at pier 18
        // (2'-2 1/2 above the floor beam bottom); pier 19 'opp. hand' = the same height above its floor beam
        var fb11 = PartBox(bn, "FB11");
        double zw = j == "L11" || fb11 == null ? Ft(141.6035) : fb.Value.zMin + (Ft(141.6035) - fb11.Value.zMin);
        double sta = fb.Value.sta + east * FI(4, 0.375);
        string fbwFlag = U("p.103", $"walkway along the floor beam {fbj} (U11-L11)", "Section A gives the walkway 3'-0 3/8 to 5'-0 3/8 from the CL floor beam with its grating at El. 141.6035 (pier 18; pier 19 'opp. hand'); the key plan draws it from the north ladder (6'-6 3/8 inboard of the north truss) to the CL bridge, where a short 2'-0 piece turns along the bridge ('2'-0 / 1'-0'); the hangers to the stringers are drawn only",
            $"walkway from the north ladder line to the CL at El. 141.6035 (pier 18) / the same height above FB11' (pier 19, {(zw - fb.Value.zMin) / In(1):0.00} in. above the floor beam bottom); the short piece along the bridge at the CL and the hangers not built.");
        Walk(bn, $"walkway at {fbj}", true, sta, -Ft(43.5) + FI(6, 6.375), 0, zw / In(1), false, "p.103", fbwFlag);
        Print($"{bn}: walkway at {fbj}: grating top El {zw / 0.3048:0.0000} ft, {(zw - fb.Value.zMin) / In(1):0.00} in. above the floor beam bottom, {(sta - fb.Value.sta) / 0.3048:0.000} ft from the floor beam");
        // platforms on the pier tops INBOARD of the shoe (p.104 INSPECTION PLATFORM AT PIER 18: View B / Section A = 8'-0 along the bridge, 4'-0 each side of the
        // CL pin; 5'-4 to 5'-4 + 3'-3 1/16 inboard of the CL truss; north pier shown, south opposite hand); 6c (BW-W7): grating 8 13/16 above the top of pier (p.104)
        // ladders (p.103 key plans, p.104): CL 2'-3 from the CL floor beam / strut, 6'-6 3/8 inboard of the truss, stiles 9 in. each side of the CL; north = 53'-2 to the
        // floor beam walkway (cage), south = 15'-0 1/2 up to the bottom strut level (BW-W9)
        double lsta = fb.Value.sta + east * FI(2, 3);
        string mlFlag = U("p.103, p.104", "main span ladder top extension, cage end and fastening", "the ladders are dimensioned (CL 2'-3 from the floor beam / strut, 6'-6 3/8 inboard of the truss, '9 | 9' stiles, 53'-2 and 15'-0 1/2 long); the stile extension above the walkway, the ladder support angles and the safety cage's top end are drawn only",
            "stiles 3'-6 above the top level, rungs from 12 in. above the platform at 12 in.; cage hoops (p.103: 2 1/2 x 3/8, R 1'-0 3/8, 'abt. 5'-0 ctrs.') from 6'-9 above the platform (p.104) up to the last one 10 in. under the walkway grating, 5 vertical bars 2 1/2 x 3/8 inside the hoops (rev. 1); ladder supports not built.");
        foreach (int north in new[] { -1, 1 })
        {
            double pierTop = PierTop(bn, fb.Value.sta, north) + In(8.8125);
            double tl = north < 0 ? trussN : trussS, inb = -north;           // inboard = toward the CL
            double a0 = tl + inb * FI(5, 4), a1 = tl + inb * (FI(5, 4) + FI(3, 3.0625));
            Platform(bn, $"platform {j} {(north < 0 ? "N" : "S")}", fb.Value.sta, Math.Min(a0, a1), Math.Max(a0, a1), 96, pierTop, "p.104");
            double lo = tl + inb * FI(6, 6.375);
            if (north < 0)
            {
                Ladder(bn, $"ladder {j} N", lsta, lo, pierTop, zw, "p.103, p.104", 9, mlFlag);
                Cage(bn, $"ladder {j} N cage", lsta, lo, (int)inb, pierTop, zw, mlFlag);
            }
            else Ladder(bn, $"ladder {j} S", lsta, lo, pierTop, pierTop + FI(15, 0.5), "p.104", 9, mlFlag);
        }
    }
}
// ================================================================ girder spans: CL inspection walk (p.43 Section A-A 'CL of Inspection Walk' 2'-0 from the CL bridge, p.44 note
// 'Omit walkway manholes in End Fl. Beams adjacent to Abutments'; no own walkway detail sheet: the p.101 walkway details (same floor beam depth 6'-3 1/2, same manholes) are used)
string gwFlag = U("p.43, p.44, p.101", "girder-span CL inspection walk", "p.43 Section A-A draws the inspection walk through the floor beam manholes with its CL 2'-0 from the CL bridge and p.44 omits the manholes in the end floor beams at the abutments; there is no walkway detail sheet for the girder spans (the p.101 approach details: 10 [ 15.3 channels 2'-0 b.b., grating, rails, the same 6'-3 1/2 floor beams) and the side of the CL is read from the section's orientation (north, as the approach / main span walks)",
    "as the approach CL walkway (p.101): pieces between the floor beam webs centred 2'-0 north of the CL, channels on the floor beam bottom flange, from the end floor beam at the abutment (no manhole: the walk ends there) to the deck end at pier 10 / 27 (continuing into the approach unit's walkway).");
foreach (var n in new[] { "PTC Girder spans 1-10", "PTC Girder spans 28-31" })
{
    var ul = bridges[n].Truss!.Unresolved;
    ul.RemoveAll(x => x.Contains("service walkway in the girder spans"));
    var dk = bridges[n].Truss!.Floor!.Deck!;
    // the abutment end floor beam (first floor beam of span 1 / last of span 31): the walk ends at its web
    var fbSt = model0[n].Steel!.WholeMembers().Where(p => p.Category == PartCategory.TrussFloorBeam && !p.Mark.StartsWith("dam ") && p.Mesh is { IsEmpty: false })
        .Select(p => { var bb = p.Mesh.Bounds(); return alOf[n].Inverse(new SBM.Core.Geometry.Vec2((bb.Min.X + bb.Max.X) / 2, (bb.Min.Y + bb.Max.Y) / 2)).Station; }).OrderBy(s => s).ToList();
    bool west = n.Contains("1-10");
    double from = west ? fbSt.First() : dk.BeginStation, to = west ? dk.EndStation : fbSt.Last();
    int k = WalkCl(n, "walkway CL", from, to, 10.25, "p.43, p.44, p.101", gwFlag, onFlange: true);
    Print($"{n}: CL inspection walk in {k} pieces");
}

var rep = Doc.Regenerate();
foreach (var n in names)
{
    var st = Doc.Model(n).Steel;
    if (st == null) continue;
    var w = st.Parts.Where(p => p.Assembly.Contains("WALKWAYS") || p.Assembly.Contains("LADDERS") || p.Assembly.Contains("PLATFORMS")).ToList();
    if (w.Count == 0) continue;
    var c = counts.GetValueOrDefault(n);
    Print($"{n}: walkways {c.walkLf:0} ft, {c.ladders} ladders, {c.platforms} platforms; grating {w.Where(p => p.Category == PartCategory.Grating).Sum(p => p.Length) / 0.3048 * 2:0} sq ft (2 ft runs; platforms counted at 2 ft too), steel {w.Where(p => p.Category != PartCategory.Grating).Sum(p => p.Mass) * 2.20462262:N0} lb + grating {w.Where(p => p.Category == PartCategory.Grating).Sum(p => p.Mass) * 2.20462262:N0} lb");
    foreach (var e in rep.Errors.Where(e => e.StartsWith(n))) Print("  ERROR " + e);
}
Doc.Save(outPath);
Print($"Saved {outPath}");
