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
    var p = model0[bridge].Steel?.Parts.FirstOrDefault(x => x.Mark == mark);
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
void Walk(string bridge, string name, bool transverse, double reference, double from, double to, double vTopIn, bool fromGrade, string pages, string flag)
{
    var t = bridges[bridge].Truss!;
    string desc = $"{bridge} - service walkways ({pages})";
    var items = t.LineItems!;
    double top = vTopIn;
    items.Add(Li($"{name} grating", "WALKWAYS", desc, PartCategory.Grating, transverse, reference, from, to, fromGrade, P(-11.76, top - 0.75, 11.76, top - 0.75, 11.76, top, -11.76, top), pages, flag + " " + gratingFlag, gratingDensity));
    foreach (int sd in new[] { -1, 1 })
    {
        string s = sd < 0 ? "L" : "R";
        items.Add(Li($"{name} channel {s}", "WALKWAYS", desc, PartCategory.WalkwayStringer, transverse, reference, from, to, fromGrade, Channel(sd, top), pages, channelFlag, lbft: 15.3));
        items.Add(Li($"{name} top rail {s}", "WALKWAYS", desc, PartCategory.WalkwayRail, transverse, reference, from, to, fromGrade, RailAngle(sd, 12, top + 36, 3, 3, 0.3125), pages, railFlag, lbft: 6.1));
        items.Add(Li($"{name} mid rail {s}", "WALKWAYS", desc, PartCategory.WalkwayRail, transverse, reference, from, to, fromGrade, RailAngle(sd, 12, top + 18, 2.5, 3, 0.3125), pages, railFlag, lbft: 5.6));
        // posts L 3x3x5/16 outside the channel web from the channel bottom to the top rail: leg across (3 in. x 5/16 along the run) + leg along (5/16 x 3 in.)
        double len = to - from, pitch = Ft(6);
        int n = Math.Max(1, (int)Math.Floor((len - In(12)) / pitch) + 1);
        double start = (len - (n - 1) * pitch - In(3)) / 2;
        items.Add(Li($"{name} posts {s} a", "WALKWAYS", desc, PartCategory.WalkwayPost, transverse, reference, from, to, fromGrade, P(sd * 12, top - 10, sd * 15, top - 10, sd * 15, top + 33, sd * 12, top + 33), pages, railFlag,
            pitch: pitch, width: In(0.3125), start: start));
        items.Add(Li($"{name} posts {s} b", "WALKWAYS", desc, PartCategory.WalkwayPost, transverse, reference, from, to, fromGrade, P(sd * 12, top - 10, sd * 12.3125, top - 10, sd * 12.3125, top + 33, sd * 12, top + 33), pages, railFlag,
            pitch: pitch, width: In(3), start: start));
    }
    var c = counts.GetValueOrDefault(bridge);
    counts[bridge] = (c.walkLf + (to - from) / 0.3048, c.ladders, c.platforms);
}

// safety ladder (p.101 / p.102): 2 stiles L 3x2 1/2x3/8 (16 in. apart, rungs 3/4 in. dia. @ 12 in. along the alignment) at (station, offset), from zBot to zTop (m, absolute)
string ladderFlag = U("p.101, p.102", "safety ladder width, top extension and cage", "the ladder material is listed (Ls 3x2 1/2x3/8, 3/4 in. rungs 12 in. ctrs, cage bars 2 1/2 x 3/8, hoops @ 5'-0); the stile spacing and the extension above the walkway are drawn only",
    "stiles 1'-4 apart, rungs from 12 in. above the platform at 12 in., stiles 3'-6 above the walkway grating; cage bars / hoops, bent hand bars and the anchors not built.");
void Ladder(string bridge, string name, double sta, double off, double zBot, double zTop, string pages)
{
    var t = bridges[bridge].Truss!;
    string desc = $"{bridge} - safety ladders ({pages})";
    double zb = zBot / In(1), zt = (zTop + FI(3, 6)) / In(1);
    foreach (int sd in new[] { -1, 1 })
    {
        // stile angle: 3 in. leg across (transverse range = offsets), 2 1/2 in. leg along the alignment (U)
        t.LineItems!.Add(Li($"{name} stile {(sd < 0 ? "B" : "A")} 1", "LADDERS", desc, PartCategory.WalkwayPost, true, sta, off - In(1.5), off + In(1.5), false,
            P(sd * 8, zb, sd * 8.375, zb, sd * 8.375, zt, sd * 8, zt), pages, ladderFlag));
        t.LineItems.Add(Li($"{name} stile {(sd < 0 ? "B" : "A")} 2", "LADDERS", desc, PartCategory.WalkwayPost, true, sta, off + In(1.125), off + In(1.5), false,
            P(sd * 8.375, zb, sd * 10.5, zb, sd * 10.5, zt, sd * 8.375, zt), pages, ladderFlag));
    }
    var copies = new List<double>();
    for (double z = 12; zb + z < zTop / In(1) - 1e-6; z += 12) copies.Add(In(z));
    double h = 0.3323;   // square of the 3/4 in. rod's area
    t.LineItems!.Add(Li($"{name} rungs", "LADDERS", desc, PartCategory.WalkwayRail, true, sta, off - In(h), off + In(h), false, P(-8, zb - h, 8, zb - h, 8, zb + h, -8, zb + h), pages, ladderFlag, copies: copies));
    var c = counts.GetValueOrDefault(bridge);
    counts[bridge] = (c.walkLf, c.ladders + 1, c.platforms);
}

// inspection platform (p.102 / p.104): grating on 8 [ 13.75 edge channels across, railing on both long sides; centre (station, offset), size along x across (in.), grating top zTop (m)
string platformFlag(string pages) => U(pages, "inspection platform position, frame and hangers", "the platform is drawn on the pier top beside the truss shoe with its plan size, 9 3/8 in. from the top of pier to the grating (p.102 Section A); the hangers (2-Ls 4x4x3/8 to the truss), cross channels and the end railing are drawn without full dimensions",
    "grating on two 8 [ 13.75 channels along its long edges, rails on those edges, posts at the corners and mid-length; hangers / end rails / toe plates not built.");
void Platform(string bridge, string name, double sta, double off0, double off1, double alongIn, double zTop, string pages)
{
    var t = bridges[bridge].Truss!;
    string desc = $"{bridge} - inspection platforms ({pages})";
    double h = alongIn / 2, top = zTop / In(1);
    var items = t.LineItems!;
    items.Add(Li($"{name} grating", "PLATFORMS", desc, PartCategory.Grating, true, sta, off0, off1, false, P(-h + 0.21, top - 0.75, h - 0.21, top - 0.75, h - 0.21, top, -h + 0.21, top), pages, platformFlag(pages) + " " + gratingFlag, gratingDensity));
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
string crossFlag = U("p.101, p.102", "cross walkway station", "the key plans put the cross walkways at the pier floor beams and the ladders 4'-9 3/8 (piers 10 / 13 / 17) or 6'-0 3/8 (piers 11 / 12 / 14 / 15 / 16) from the pier / bearing CL; the walkway's own station is drawn only",
    "cross walkway centred on the ladder station (pier 13 / 24 of the 3-span unit: beside its end floor beam, no ladder); north half only except piers 10 / 27 (p.101 note 'omit this section of walkways at piers 13, 15 and 17'); NJ units opposite hand along the bridge, still to the north truss.");
string clFlag = U("p.101", "CL walkway position and level", "the key plan draws the walkway band between the CL bridge and 2'-0 north of it at the floor beam bottom flange, through the floor beam manholes (drawn, not dimensioned)",
    "grating top 1 3/4 in. above the floor beam bottom (on the bottom flange angles), centred 1'-0 north of the CL; floor beam webs not cut for the manholes.");
var units = new (string bridge, int east, (string fb, double ladderFt, bool full, bool ladder, bool platformBoth)[] pts)[]
{
    ("PTC 3-span truss 11-13", 1, new[] { ("FB0", 4 + 9.375 / 12, true, true, true), ("FB8", 6 + 0.375 / 12, false, true, false), ("FB8'", 6 + 0.375 / 12, false, true, false), ("FB0'", double.NaN, false, false, false) }),
    ("PTC 4-span truss 14-17", 1, new[] { ("FB0", 2 + 9.375 / 12, false, true, false), ("FB10", 6 + 0.375 / 12, false, true, false), ("FB20", 6 + 0.375 / 12, false, true, false), ("FB10'", 6 + 0.375 / 12, false, true, false), ("FB0'", -(2 + 9.375 / 12), false, true, false) }),
    ("PTC 3-span truss 25-27", -1, new[] { ("FB0", 4 + 9.375 / 12, true, true, true), ("FB8", 6 + 0.375 / 12, false, true, false), ("FB8'", 6 + 0.375 / 12, false, true, false), ("FB0'", double.NaN, false, false, false) }),
    ("PTC 4-span truss 21-24", -1, new[] { ("FB0", 2 + 9.375 / 12, false, true, false), ("FB10", 6 + 0.375 / 12, false, true, false), ("FB20", 6 + 0.375 / 12, false, true, false), ("FB10'", 6 + 0.375 / 12, false, true, false), ("FB0'", -(2 + 9.375 / 12), false, true, false) }),
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
    Walk(bn, "walkway CL", false, -Ft(1), dk.BeginStation, dk.EndStation, vTop, true, "p.101", clFlag);
    foreach (var (fb, ladderFt, full, ladder, both) in pts)
    {
        var box = PartBox(bn, fb);
        if (box == null) { Print($"{bn}: {fb} not found"); continue; }
        double sta = double.IsNaN(ladderFt) ? box.Value.sta - east * (In(9.5) + FI(1, 2)) : box.Value.sta + east * Ft(ladderFt);
        double zw = box.Value.zMin + In(1.75);   // grating top (absolute)
        double from = full ? -Ft(LadderOff) : -Ft(LadderOff), to = full ? Ft(LadderOff) : -Ft(1);
        Walk(bn, $"cross walkway {fb}", true, sta, from, to, zw / In(1), false, "p.101", crossFlag);
        if (!ladder) continue;
        foreach (int north in both ? new[] { -1, 1 } : new[] { -1 })
        {
            double off = north * Ft(LadderOff);
            double zp = PierTop(bn, box.Value.sta, north) + In(9.375);
            Ladder(bn, $"ladder {fb} {(north < 0 ? "N" : "S")}", sta, off, zp, zw, "p.101, p.102");
            // platform: 7'-0 3/8 across (5'-5 5/16 outboard / 1'-7 1/16 inboard of the ladder, p.102 Section A), 3'-6 1/4 along
            double o0 = north < 0 ? off - FI(5, 5.3125) : off - FI(1, 7.0625), o1 = north < 0 ? off + FI(1, 7.0625) : off + FI(5, 5.3125);
            Platform(bn, $"platform {fb} {(north < 0 ? "N" : "S")}", sta, o0, o1, 42.25, zp, "p.102");
        }
    }
}

// ================================================================ main span (p.103 / p.104 / p.106)
{
    string bn = "PTC Arch truss 18-20";
    var al = alOf[bn];
    var fb15 = PartBox(bn, "FB15");
    var fb15p = PartBox(bn, "FB15'");
    if (fb15 != null && fb15p != null)
    {
        double vTop = (fb15.Value.zMin - al.ElevationAt(fb15.Value.sta, 0)) / In(1) + 1.75;
        Walk(bn, "walkway CL W15-W15'", false, -Ft(1), Math.Min(fb15.Value.sta, fb15p.Value.sta), Math.Max(fb15.Value.sta, fb15p.Value.sta), vTop, true, "p.106",
            U("p.106", "main span CL walkway", "the key plan for navigation light supports and walkways draws the CL walkway from W15 to W15' at the floor beams (material 10 [ 15.3, grating, rails); its level is shown on Section D at the floor beam bottom flange",
                "as the approach CL walkway: grating top 1 3/4 in. above the floor beam bottom, centred 1'-0 north of the CL."));
        foreach (var j in new[] { "15", "20", "19'", "15'" })
        {
            var box = PartBox(bn, "FB" + j);
            if (box == null) continue;
            double sta = box.Value.sta + (j.EndsWith("'") ? -1 : 1) * (In(9.5) + FI(1, 2));
            Walk(bn, $"cross walk W{j}", true, sta, -FI(40, 9.5), FI(40, 9.5), (box.Value.zMin + In(1.75)) / In(1), false, "p.106",
                U("p.106", $"cross walk at W{j} to the navigation light supports", "the key plan shows cross walks at W15, W20 and W19' (and W15') to the navigation light platforms on the wind chords, 40'-9 1/2 each side of the CL; the W15' line and the platforms (5'-6 x 6'-0, 1'-8 rail) are read from the key plan only",
                    "2'-0 walkway beside the floor beam at its bottom flange level, 40'-9 1/2 each side of the CL; navigation light platforms and supports not built."));
        }
    }
    foreach (var (j, east) in new[] { ("L11", 1), ("L11'", -1) })
    {
        string fbj = "FB" + j.Substring(1);
        var strut = PartBox(bn, "LS-" + j);
        if (strut != null)
            Walk(bn, $"walkway on strut {j}", true, strut.Value.sta, -Ft(11), Ft(11), strut.Value.zMax / In(1) + 0.75, false, "p.103, p.104",
                U("p.103", $"walkway on the bottom strut {j}", "the key plan gives 22'-0 x 2'-0 on the strut; its fastening is drawn only", "2'-0 walkway centred on the strut top, 22'-0 long."));
        var fb = PartBox(bn, fbj);
        if (fb == null) continue;
        double sta = fb.Value.sta + east * (In(12) + Ft(1));
        double zw = fb.Value.zMin + In(1.75);
        Walk(bn, $"walkway at {fbj}", true, sta, -Ft(40), Ft(40), zw / In(1), false, "p.103",
            U("p.103", $"walkway along the floor beam {fbj} (U11-L11)", "the key plan shows the walkway along the floor beam truss to truss (87'-0 c.c.) with ladders at its ends; the walkway elevations printed (141.67 / 141.60 / 151.06) cannot be assigned",
                "2'-0 walkway beside the floor beam at its bottom flange level, 40'-0 each side of the CL."));
        // platforms on the pier tops beside the 9'-2 shoe (p.104: 8'-0 x 6'-4, north pier shown, south opposite hand), ladder from the floor beam walkway (p.103: north side only)
        foreach (int north in new[] { -1, 1 })
        {
            double pierTop = PierTop(bn, fb.Value.sta, north) + In(9.375);
            double ps = fb.Value.sta + east * (FI(4, 7) + In(3) + Ft(3));     // along the bridge beside the shoe, toward the span
            double tl = north * Ft(43.5);
            Platform(bn, $"platform {j} {(north < 0 ? "N" : "S")}", ps, tl - FI(3, 2), tl + FI(3, 2), 96, pierTop, "p.104");
            if (north < 0) Ladder(bn, $"ladder {j} N", sta, -Ft(40) + In(6), pierTop, zw, "p.103, p.104");
        }
    }
}

// ================================================================ girder spans: no walkway sheet
foreach (var n in new[] { "PTC Girder spans 1-10", "PTC Girder spans 28-31" })
{
    var ul = bridges[n].Truss!.Unresolved;
    ul.RemoveAll(x => x.Contains("service walkway in the girder spans"));
    ul.Add(U("p.42, p.44, p.101", "service walkway in the girder spans", "the walkway sheets p.101-106 cover the approach truss and main spans only (p.101 key plan starts at the girder span / truss span pier 10); p.42 nevertheless lists 2,510 / 1,010 sq ft of grating and 83,000 / 33,000 lb of walkways for the girder spans and p.44 mentions floor beam manholes",
        "not built (the girder span walkway sheet is not in this set)."));
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
