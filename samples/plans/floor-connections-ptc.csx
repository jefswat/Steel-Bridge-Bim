// PTC / NJTA DELAWARE RIVER BRIDGE (1954-57, AS-BUILT) - FLOOR SYSTEM CONNECTIONS (step 3, 2026-10-21; TRUSS-DESIGN.md section 18):
//   floor beam / bracket END CONNECTIONS (connection angles + fills, web extensions, end / pin plates) with their rivets,
//   floor beam / bracket web STIFFENERS (angles + fills, rivets) under the stringer lines, stringer DIAPHRAGM clip angles + rivets,
//   PERFORATED cover / web plates: the access holes are cut (main truss members and bracing, approach box struts).
// The PTC sheets show NO lacing bars: every built-up member uses perforated cover plates (e.g. "1-Pl (24-12) x 5/8"); there are no batten / tie
// plates either (checked on p.47 / p.57 / p.69-71 member tables and the joint sheets). The generic TrussLacing input is therefore not used.
// Generic inputs used: TrussMemberDefinition.EndConnections, TrussStiffenerSet (FillThickness / FillHeight / Top / LegToward / Rivets),
// TrussLineItemDefinition.ClipAngles, TrussPlate.Perforation / TrussPly.Perforation.
// Re-runnable (replaces its own inputs); run AFTER truss-ptc-arch / truss-ptc-approach / girders-ptc / deck-ptc / connections-ptc (the diaphragm line items
// and the girder stiffeners come from there), before or after rivets-ptc:
//   sbm run samples\plans\floor-connections-ptc.csx "samples\plans\PTC Delaware River Bridge.sbm"
// Pages: p.55 (approach floor beam / bracket, stringer diaphragms), p.45 (girder spans: floor beam / bracket to girder, "details not shown same as Sh. 15"),
// p.85 (main framed floor beam + rigid hanger W14), p.94 (main suspended floor beam end, diaphragm angles), p.71 / p.74 / p.81 / p.58 (manholes).
// Every scaled value is a dimension-only flag (page + reason); values the sheets do not give are UNRESOLVED flags; nothing is adjusted.
using SBM.Core;
using SBM.Core.Bridge;
using SBM.Core.Steel;
using SBM.Core.Truss;

string outPath = Args.Length > 0 ? Args[0] : (Doc.FilePath ?? @"samples\plans\PTC Delaware River Bridge.sbm");
string U(string pages, string item, string why, string then = "") => UnresolvedFlag.Text(pages, item, why, then);
double FI(double ft, double inch) => Ft(ft) + In(inch);
TrussRivetColumn Col(double gageIn, int n, double pitchIn, double firstIn) => new TrussRivetColumn(In(gageIn), n, In(pitchIn), In(firstIn));
var report = new List<string>();
// angles / stiffeners between the flange angles stop 5/16 in. short of the flange angles' horizontal legs: the AISC root fillet of the 6 x 6 / 8 x 8 flange
// angles fills that corner (the plans mark these ends "Fin." = milled / ground to fit; the clip is not drawn)
double clip = In(5.0 / 16);

var approach = new[] { "PTC 3-span truss 11-13", "PTC 4-span truss 14-17", "PTC 4-span truss 21-24", "PTC 3-span truss 25-27" };
var girders = new[] { "PTC Girder spans 1-10", "PTC Girder spans 28-31" };
const string Main = "PTC Arch truss 18-20";
TrussDefinition T(string n) => Doc.Project.Bridges.First(b => b.Name == n).Truss ?? throw new InvalidOperationException($"{n}: no truss (run the truss scripts first)");
// re-runnable: clear this script's inputs
foreach (var n in approach.Concat(girders).Append(Main))
{
    var t = T(n);
    foreach (var m in t.Members.Where(m => m.Role == TrussMemberRole.FloorBeam))
    {
        m.EndConnections = null;
        m.Stiffeners?.RemoveAll(s => s.Source.StartsWith("floor-connections"));
        if (m.Stiffeners is { Count: 0 }) m.Stiffeners = null;
    }
    foreach (var li in t.LineItems ?? new()) li.ClipAngles = null;
    foreach (var s in t.Sections) { foreach (var p in s.TopPlates.Concat(s.BottomPlates)) p.Perforation = null; foreach (var p in s.WebPlies) p.Perforation = null; }
}

// ================================================================ common flags
string fbStiffFlag(string pg) => U(pg, "floor beam / bracket stiffener positions and rivet pitch",
    "the elevations draw the stiffeners (2-Ls 5 x 3 1/2 x 7/16 + 2-Fills 3 1/2 x 1/2; main 2-Ls 6 x 4 x 7/16 + 2-Fills 4 x 3/4) under the stringers but the beams are drawn with break lines, so only the stiffeners next to the truss scale; the rivet count per stiffener is not printed",
    "one stiffener pair under every stringer line (heel on the stringer CL, web legs toward the higher offset / the member end), the bracket intermediate stiffener and the tip pair scaled, fills between the flange angle legs, rivets in one gage line at the printed maximum pitch.");

// ================================================================ 1. approach trusses (p.55): floor beams F3 / F4 and brackets B1 to the truss verticals
// p.55 elevation (section at the truss): "2-Ls 4 x 4 x 3/4, 2-Fills 7 x 1/2" on the floor beam and on the bracket side of the hanger / post (F.2.E.), outstanding legs
// riveted to the vertical's flange (and the top chord gussets at the top), 3" max rivet pitch; counted on the drawing: 24 rivets in the gage line of the leg on the web,
// 11-12 in the fill line beyond the leg toe, 13 through each outstanding leg. Fill thickness 1/2 = the 6 x 6 x 1/2 flange angles: the connection angles run over the
// flange angle legs between the flange angle backs (drawn flush with the backs of the floor beam / bracket angles), the fills between the flange angle legs.
// Rivets 7/8 carbon (p.55 RIVETS note). The angle backs bear on the first steel face (gusset / vertical flange) found from the member end (probe).
string apConnFlag = U("p.55", "approach floor beam / bracket end connection angles (2-Ls 4 x 4 x 3/4, 2-Fills 7 x 1/2)",
    "the section at the truss draws them without dimensions: length (full depth between the flange angle backs), gages and pitches are scaled / counted from the drawing (24 + 12 rivets per angle in the web, 13 per outstanding leg); the 7 in. fills are taken under the leg on the web (fill = flange angle thickness)",
    "angles flush with the backs of the flange angles less 1/2 in. (under the horizontal legs), backs on the first steel face (the top chord gussets, which reach about half the beam depth down), web gage 2 1/2, fill line 5 1/2, outstanding leg gage 2 1/2; below the gussets the angles stand off the vertical's flange by the gusset + its fill (about 1 1/2 in., no fill drawn there): those outstanding-leg rivets grip the leg only (reported as CHECK).");
string apBentFlag = U("p.55", "3/4 bent plate and the lower angles '2-Ls 4 x 4 x 3/4 on hangers / 2-Ls 5 1/4 x 4 x 3/4 on posts' under the floor beam at the truss",
    "drawn at overview scale only (no dimensions)", "not built.");
// brackets taper from the truss end (their bottom angles rise 0.43 in. per in.): the connection angle / fill bottoms are raised by the rise over their width
TrussEndConnection ApproachEnd(TrussMemberEnd end, double fbHalf, double lenCut = 0, double fillCut = 0) => new TrussEndConnection
{
    End = end, Label = "2-Ls 4x4x3/4, 2-Fills 7x1/2 (F.2.E.)", Angle = "L4x4x3/4", Top = fbHalf - In(0.5) - clip, Length = 2 * fbHalf - In(1) - 2 * clip - lenCut, ExtendWeb = true,
    FillThickness = In(0.5), FillWidth = In(7), FillTop = fbHalf - In(6), FillLength = 2 * fbHalf - In(12) - fillCut,
    WebRivets = new() { Col(2.5, 24, 3, 2.0), Col(5.5, 11, 6, 8.75) }, OutRivets = new() { Col(2.5, 13, 5.75, 2.75) },
    RivetDiameter = In(0.875), RivetMaterial = "7/8 in carbon steel rivet", Material = "C", Source = "p.55", Unresolved = apConnFlag,
};
TrussStiffenerSet Stiff(string label, string angle, double fill, double top, double height, double fillHeight, List<double> d, int toward, int perFace, double pitchIn, double gageIn, string pg, double rivetIn = 0.875) => new TrussStiffenerSet
{
    Label = label, Angle = angle, PerFace = perFace, Height = height, Top = top, Distances = d, FillThickness = fill, FillWidth = 0, FillHeight = fillHeight, LegToward = perFace >= 2 ? 0 : toward,
    Material = "C", Source = "floor-connections-ptc " + pg, Unresolved = fbStiffFlag(pg), RivetDiameter = In(rivetIn),
    Rivets = new() { Col(gageIn, (int)Math.Floor((height - In(6)) / In(pitchIn)) + 1, pitchIn, 3.0) },
};
int nConn = 0, nStiff = 0;
foreach (var bn in approach)
{
    var t = T(bn);
    double half = In(75.5 / 2);                                         // web 75 + 1/4 projection: back of the flange angles
    var lines = t.Floor!.Stringers.Select(s => s.Offset).Distinct().OrderBy(o => o).ToList();
    double tr = Ft(29.0), tip = Ft(29.0 + 10.75), aT = In(7.0 / 16);
    foreach (var m in t.Members.Where(m => m.Role == TrussMemberRole.FloorBeam))
    {
        bool bracket = m.Section == "B1";
        if (!bracket)
        {
            m.EndConnections = new() { ApproachEnd(TrussMemberEnd.Both, half) };
            // stiffeners under the stringer lines between the trusses (FB from the N truss at -29'-0)
            var d = lines.Where(o => Math.Abs(o) < tr - Ft(1) && Math.Abs(o) > Ft(0.5)).Select(o => o + tr - aT / 2).ToList();
            m.Stiffeners ??= new();
            m.Stiffeners.Add(Stiff("2-Ls 5x3 1/2x7/16 + 2-Fills 3 1/2x1/2 (under the stringers)", "L5x3-1/2x7/16", In(0.5), half - In(0.5) - clip, 2 * half - In(1) - 2 * clip, 2 * half - In(12), d, 1, 1, 4.5, 2.0, "p.55"));
            nConn += 2; nStiff += d.Count;
        }
        else
        {
            m.EndConnections = new() { ApproachEnd(TrussMemberEnd.End, half, In(2.0), In(3.25)) };
            // bracket from the tip (start, 39'-9 from the CL) to the truss: stiffeners at the 33'-0 stringer (F.I.E.), the intermediate one scaled 7'-1 1/2 from the
            // truss (p.55: 7.13 ft at 44.2 px/ft from the 10'-9 dimension) and the tip pair (4-Ls, 2-Fills 11 x 1/2) centred under S5 (39'-1)
            double rise(double dFromTip) => In(75.5 - 24.5) * (1 - dFromTip / (tip - tr - In(9.5)));
            var sets = new[] { (Ft(39.75 - 33.0) - aT / 2, 1, 1), (Ft(10.75 - 7.125) - aT / 2, 1, 1), (Ft(39.75 - (39 + 1 / 12.0)), 0, 2) };
            m.Stiffeners ??= new();
            foreach (var (dd, toward, pf) in sets)
            {
                double h = 2 * half - In(1) - 2 * clip - rise(pf == 2 ? dd - In(3.5) : dd);     // largest rise under the legs (tip side)
                m.Stiffeners.Add(Stiff(pf == 2 ? "4-Ls 5x3 1/2x7/16 + 2-Fills 11x1/2 (bracket tip, under S5)" : "2-Ls 5x3 1/2x7/16 F.I.E. + 2-Fills 3 1/2x1/2", "L5x3-1/2x7/16", In(0.5),
                    half - In(0.5) - clip, h, Math.Max(In(6), h - In(11)), new() { dd }, toward, pf, 3.5, 2.0, "p.55"));
                nStiff++;
            }
            nConn++;
        }
    }
    if (!t.Unresolved.Contains(apBentFlag)) t.Unresolved.Add(apBentFlag);
}
report.Add($"approach: {nConn} floor beam / bracket end connections, {nStiff} stiffener positions");

// ================================================================ 2. girder spans (p.45 + p.55 by the p.45 note): floor beam / bracket webs lapped on the girder stiffener
// (2-Ls 6 x 4 x 1/2 at the floor beams) with 1 in carbon rivets (p.45 Section A: "1" carbon rivets, holes to be reamed to a metal templet"), 22 counted per web;
// the bottom corner plates "2-7/16 Pls, 2-1/2 Fills" (p.45, both sides) scaled; stiffeners as the approach (p.45: details not shown same as Sh. 15 = p.55).
string gConnFlag = U("p.45", "floor beam / bracket connection to the girder (web lapped on the girder stiffener, 2-7/16 corner plates on 1/2 fills)",
    "Section A / the elevation draw it without dimensions: web end, rivet line (22 x 1 in, 3 1/2 in.), corner plate outlines scaled at 5.6 px/in (+-1 in.)",
    "web extended to 1/2 in. clear of the girder web + its 1 1/8 flange angle legs, on the side of the girder stiffener's outstanding leg (the stiffener moved by half the floor beam web to lie against it), one rivet line (21 x 1 in @ 3 1/2) at 3 1/2 in. from the girder CL, corner plates 7/16 on 1/2 fills.");
double gTw = In(7.0 / 16), gWebHalf = In(7.0 / 32) + In(1.125);     // web face of the girder + its flange angle legs (8 x 8 x 1 1/8) the web end must clear
foreach (var bn in girders)
{
    var t = T(bn);
    double half = In(75.5 / 2);
    // girder stiffeners at the floor beams: outstanding leg beside the floor beam web (was centred on it -> overlap)
    foreach (var g in t.Members.Where(m => m.Role == TrussMemberRole.Girder))
        foreach (var s in g.Stiffeners?.Where(s => s.Label.StartsWith("stiffeners 2-Ls 6x4x1/2 at the floor beams")) ?? Enumerable.Empty<TrussStiffenerSet>())
        {
            if (!s.Label.Contains("(beside the floor beam web)")) { s.Distances = s.Distances.Select(x => x + gTw / 2).ToList(); s.Label += " (beside the floor beam web)"; }
            s.Plumb = true;       // plumb like the floor beam web it is riveted to (the girder is on grade: normal to it the leg would lean 1 1/2 in. over its height)
        }
    // end stiffeners 4-Ls 7x4x3/4 at the CL bearings (= the end floor beam lines): the two outstanding legs straddle the floor beam / bracket web
    // (they were back to back with no gap: the web overlapped both legs) -> one angle per side of the web, legs away from it
    foreach (var g in t.Members.Where(m => m.Role == TrussMemberRole.Girder))
    {
        var es = g.Stiffeners?.FirstOrDefault(s => s.Label.StartsWith("end stiffeners 4-Ls 7x4x3/4") && s.PerFace == 2);
        if (es == null) continue;
        es.PerFace = 1; es.LegToward = -1; es.Plumb = true; es.Distances = es.Distances.Select(x => x - gTw / 2).ToList(); es.Label += " (one side of the floor beam web)";
        var es2 = new TrussStiffenerSet { Label = "end stiffeners 4-Ls 7x4x3/4 (other side of the floor beam web)", Angle = es.Angle, PerFace = 1, LegToward = 1, Height = es.Height, Plumb = true,
            Distances = es.Distances.Select(x => x + gTw).ToList(), Material = es.Material, Source = es.Source, Unresolved = es.Unresolved };
        g.Stiffeners!.Insert(g.Stiffeners.IndexOf(es) + 1, es2);
    }
    var lines = t.Floor!.Stringers.Select(s => s.Offset).Distinct().OrderBy(o => o).ToList();
    double gl = Ft(29.0), aT = In(7.0 / 16);
    // corner plate outlines (u from the back = girder web face along the member, v = section y): p.45 elevation scaled
    // (bottom 1/2 in. above the flange angle backs: on the vertical legs of the bottom flange angles, clear of their horizontal legs)
    double vb = -half + In(0.5) + clip;
    var plF = new List<TrussUV> { new(0, vb), new(In(21), vb), new(In(21), vb + In(3)), new(In(8), -half + In(19.25)), new(0, -half + In(19.25)) };
    // bracket: its bottom angles slope up from the member cut (11 in. from the girder CL) at 51 in. over 10'-9 - 11 in.: the plate's bottom edge follows them
    double uCut = In(11) - (gWebHalf + In(0.5)), kB = In(75.5 - 24.5) / (Ft(10.75) - In(11));
    var plB = new List<TrussUV> { new(0, vb), new(uCut, vb), new(In(21), vb + kB * (In(21) - uCut)), new(In(14), -half + In(19.25)), new(0, -half + In(19.25)) };
    foreach (var m in t.Members.Where(m => m.Role == TrussMemberRole.FloorBeam))
    {
        bool bracket = m.Section == "B1";
        var corner = bracket ? plB : plF;
        // the girder stiffener's outstanding leg under the plate: 7 x 3/4 end stiffeners at the end floor beams (CL bearings), 6 x 1/2 at the others; the fill
        // (of the leg thickness) starts beyond the leg toe
        bool endFb = System.Text.RegularExpressions.Regex.IsMatch(m.Name, @"-[04]$");
        double uFill = (endFb ? In(7) : In(6)) + In(7.0 / 32) - (gWebHalf + In(0.5)) + In(0.375);   // 3/8 in. beyond the leg toe (1/4 left the end stiffener legs 0.08 in. into the fill, step 4)
        m.EndConnections = new()
        {
            new TrussEndConnection
            {
                End = TrussMemberEnd.Start, Label = "web to girder stiffener (1 in carbon rivets)", Back = gWebHalf, WebClearance = In(0.5), ExtendWeb = true,
                Top = half + In(0.5) - In(13.0 / 16 + 1.125 + 0.25), Length = half + In(0.5) - In(13.0 / 16 + 1.125 + 0.25) + half,
                WebRivets = new() { Col(2.125, 21, 3.5, 1.75) }, RivetDiameter = In(1), RivetMaterial = "1 in carbon steel rivet", Material = "S", Source = "p.45", Unresolved = gConnFlag,
            },
            new TrussEndConnection
            {
                End = TrussMemberEnd.Start, Label = "2-7/16 corner Pls", Back = gWebHalf + In(0.5), PlateOutline = corner, PlateThickness = In(7.0 / 16), StandOff = endFb ? In(0.75) : In(0.5),
                Top = -half + In(19.25), Length = In(18.75), Material = "S", Source = "p.45", Unresolved = gConnFlag,
                WebRivets = new() { Col(9.0, 3, 3.5, 3.5), Col(13.0, 2, 3.5, 7.0) }, RivetDiameter = In(0.875), RivetMaterial = "7/8 in carbon steel rivet",
            },
            new TrussEndConnection
            {
                End = TrussMemberEnd.Start, Label = "2-1/2 fills under the corner Pls", Back = gWebHalf + In(0.5) + uFill,
                PlateOutline = corner.Select(q => new TrussUV(Math.Max(0, q.U - uFill), Math.Max(q.V, -half + In(6.25) + (bracket ? kB * Math.Max(0, q.U - uCut) : 0)))).ToList(), PlateThickness = endFb ? In(0.75) : In(0.5),
                Top = -half + In(19.25), Length = In(13), Material = "C", Source = "p.45", Unresolved = gConnFlag,
            },
        };
        // stiffeners under the stringer lines (from the girder; FB halves end at the CL: the CL stringer stiffener on the N half only)
        double len = gl;
        List<double> d;
        if (!bracket)
        {
            bool north = m.From.StartsWith("N:");
            d = lines.Where(o => Math.Abs(o) < gl - Ft(1) && (north ? o <= 0 : o > 0)).Select(o => gl - Math.Abs(o) - aT / 2).ToList();
            m.Stiffeners ??= new();
            m.Stiffeners.Add(Stiff("2-Ls 5x3 1/2x7/16 + 2-Fills 3 1/2x1/2 (under the stringers)", "L5x3-1/2x7/16", In(0.5), half - In(0.5) - clip, 2 * half - In(1) - 2 * clip, 2 * half - In(12), d, 1, 1, 4.5, 2.0, "p.45 / p.55"));
            nStiff += d.Count;
        }
        else
        {
            // bracket from the girder (start) to the tip (end, taper DepthReductionEnd): 33'-0 stringer, intermediate 7'-1 1/2, tip pair under S1 (39'-1)
            double cut = Ft(10.75) - In(11);
            double rise(double dFromGirder) => In(75.5 - 24.5) * Math.Clamp((dFromGirder - In(11)) / cut, 0, 1);
            var sets = new[] { (Ft(33.0 - 29.0) - aT / 2, 1, 1), (Ft(7.125) - aT / 2, 1, 1), (Ft(39 + 1 / 12.0 - 29.0), 0, 2) };
            m.Stiffeners ??= new();
            foreach (var (dd, toward, pf) in sets)
            {
                double h = 2 * half - In(1) - 2 * clip - rise(dd + In(3.5));     // largest rise under the legs (tip side)
                m.Stiffeners.Add(Stiff(pf == 2 ? "4-Ls 5x3 1/2x7/16 + 2-Fills 11x1/2 (bracket tip, under S1)" : "2-Ls 5x3 1/2x7/16 F.I.E. + 2-Fills 3 1/2x1/2", "L5x3-1/2x7/16", In(0.5),
                    half - In(0.5) - clip, h, Math.Max(In(6), h - In(11)), new() { dd }, toward, pf, 3.5, 2.0, "p.45 / p.55"));
                nStiff++;
            }
        }
        nConn++;
    }
}
report.Add($"girder spans: {nConn} end connections in total so far, {nStiff} stiffener positions");

// ================================================================ 3. main span (p.85 framed beams at points 0-14, p.94 suspended beams 15-21; F7 at W22 not built)
// p.85 "Typical framed intermediate floor beam": end connection angles "2-Ls 8 x 4 1/2 x 3/4, 2-Fills 11 1/2 x 3/4" with "38 - 1 1/8 dia H.S. rivets - ream to metal templet"
// (= the field rivets through the outstanding legs into the post: 19 per angle, one gage line); the 8 in. leg lies on the web (two gage lines, ~3 in. pitch, drawn) with the
// fill extending 3 1/2 in. past its toe (third line, fill between the 8 x 8 x 3/4 flange angle legs); end stiffener "2-Ls 6 x 4 x 7/16, 2-Fills 4 x 3/4" next to the fill.
// Floor beam rivets 7/8 carbon (p.85 RIVETS note). W14 (rigid hanger, p.85 left): the same angles with 1 1/4 in H.S. rivets into the hanger.
string mConnFlag = U("p.85", "main framed floor beam end connection (2-Ls 8 x 4 1/2 x 3/4, 2-Fills 11 1/2 x 3/4, 38 x 1 1/8 H.S. field rivets)",
    "the elevation draws the angles full depth between the flange angle backs but does not dimension the gages / web rivet pitch (scaled at 9.9 px/in: web lines 2 and 5 in. from the heel, fill line 9 3/4, ~3 in. pitch) nor which leg lies on the web (the two web gage lines + the 11 1/2 fill show the 8 in. leg)",
    "8 in. leg on the web, angles from 3/4 in. under the top flange angle backs to 3/4 in. above the bottom ones, rivet lines as scaled, 19 field rivets per outstanding leg equally spaced.");
string mStiffEnd = U("p.85", "end stiffener 2-Ls 6 x 4 x 7/16 + 2-Fills 4 x 3/4 of the framed floor beams", "drawn next to the end connection fill without a dimension", "heel 13 in. from the typical angle back (1'-2 1/4 from the truss CL), clear of the 11 1/2 in. fill also at the wider L11 post, web leg toward the CL.");
string susFlag = U("p.94", "suspended floor beam end (2-Pls 25 1/2 x 3/4, 2-Pls 17 x 13/16 at the 7 1/4 pin hole, 2-Ls 7 x 4 x 1/2 at the edge, 2-Ls 7 x 4 x 3/4 bent at the bottom)",
    "the end is drawn at 3/4 in. = 1 ft with the dimensions 1'-6 (pin below the back of the flange angles) and 6'-10 1/4 / 11'-1 3/4 that do not close with the 8'-10 1/2 beam depth; plate heights, the pin plate extent and the rivet grid are scaled (5.2 px/in)",
    "end plates 25 1/2 wide centred on the pin (truss CL), between the flange angle legs, web extended to their outer edge, pin plates 14 1/4 wide from the edge angle toe, edge angles 7 x 4 x 1/2 (4 in. outstanding), 7 rivet lines @ 5 in. (3 lines stop under the pin); pin hole not cut, bent bottom angles not built.");
string f7Flag = U("p.93, p.94", "end of the two-web floor beam F7 at W22 (pin plates of both webs)", "the W22 end detail (p.93) is drawn at overview scale only", "not built (the beam ends at the truss CL as before).");
string fb14Flag = U("p.85", "floor beam W14 end at the rigid hanger", "the beam frames into the rigid hanger 14 (p.85 left: 'Details for floor beam and rigid hanger at panel pt W14') like the typical framed beams; the earlier model ran it to the truss CL",
    "framed: ends 1'-2 1/2 from the truss CL (hanger webs 2'-5 o. to o.), same connection angles with 1 1/4 H.S. rivets into the hanger.");
{
    var t = T(Main);
    clip = In(7.0 / 16);      // the 8 x 8 x 3/4 flange angles have a larger root fillet
    double half = In(106.5 / 2);
    var lines = t.Floor!.Stringers.Select(s => s.Offset).Distinct().OrderBy(o => o).ToList();
    double tr = Ft(43.5), aT = In(7.0 / 16);
    int fr = 0, su = 0;
    foreach (var m in t.Members.Where(m => m.Role == TrussMemberRole.FloorBeam))
    {
        var k = System.Text.RegularExpressions.Regex.Match(m.Name, @"^FB(\d+)('?)$");
        if (!k.Success) continue;
        int n = int.Parse(k.Groups[1].Value);
        double L = Ft(87.0);                                                       // N truss CL to S truss CL along the beam (level)
        var stl = lines.Where(o => Math.Abs(o) < tr - Ft(1) && Math.Abs(o) > Ft(0.5)).Select(o => o + tr - aT / 2).ToList();
        m.Stiffeners ??= new();
        if (n == 22) { if (!t.Unresolved.Contains(f7Flag)) t.Unresolved.Add(f7Flag); continue; }
        if (n <= 14)
        {
            if (n == 14) { m.StartSetback = FI(1, 2.5); m.EndSetback = FI(1, 2.5); m.Unresolved = string.Join(" ", new[] { m.Unresolved, fb14Flag }.Where(x => x.Length > 0).Distinct()); }
            m.EndConnections = new()
            {
                new TrussEndConnection
                {
                    End = TrussMemberEnd.Both, Label = "2-Ls 8x4 1/2x3/4, 2-Fills 11 1/2x3/4", Angle = "L4-1/2x8x3/4", Top = half - In(0.75) - clip, Length = 2 * half - In(1.5) - 2 * clip, ExtendWeb = true,
                    FillThickness = In(0.75), FillWidth = In(11.5), FillTop = half - In(8), FillLength = 2 * half - In(16),
                    WebRivets = new() { Col(2.0, 34, 3, 2.0), Col(5.0, 34, 3, 3.5), Col(9.75, 15, 6, 10.25) }, OutRivets = new() { Col(2.5, 19, 5.5, 3.0) },
                    RivetDiameter = In(0.875), OutRivetDiameter = n == 14 ? In(1.25) : In(1.125), RivetMaterial = n == 14 ? "1 1/4 in H.S. rivet (into the hanger), 7/8 in carbon (web)" : "1 1/8 in H.S. field rivet (into the post), 7/8 in carbon (web)",
                    Material = "S", Source = "p.85", Unresolved = mConnFlag + (n is 0 or 13 ? " " + U(n == 0 ? "p.72, p.73" : "p.72, p.95", $"end connection of floor beam {(n == 0 ? "F5 at L0" : "F8 at L13")}",
                        "its sheet draws the beam end together with the strut / wind chord connections, not read in this pass", "the typical framed detail of p.85.") : ""),
                },
            };
            double back = FI(1, 2.5) - In(0.25);
            m.Stiffeners.Add(Stiff("2-Ls 6x4x7/16 + 2-Fills 4x3/4 (end stiffener)", "L6x4x7/16", In(0.75), half - In(0.75) - clip, 2 * half - In(1.5) - 2 * clip, 2 * half - In(16), new() { back + In(13) }, 1, 1, 4.5, 2.5, "p.85"));
            m.Stiffeners.Add(Stiff("2-Ls 6x4x7/16 + 2-Fills 4x3/4 (end stiffener)", "L6x4x7/16", In(0.75), half - In(0.75) - clip, 2 * half - In(1.5) - 2 * clip, 2 * half - In(16), new() { L - back - In(13) }, -1, 1, 4.5, 2.5, "p.85"));
            m.Stiffeners[^1].Unresolved = mStiffEnd; m.Stiffeners[^2].Unresolved = mStiffEnd;
            fr++;
        }
        else
        {
            double top = half - In(8), len = 2 * half - In(16);
            m.EndConnections = new()
            {
                new TrussEndConnection { End = TrussMemberEnd.Both, Label = "2-Pls 25 1/2x3/4 (end plates)", Back = -In(12.75), PlateWidth = In(25.5), PlateThickness = In(0.75), ExtendWeb = true, WebClearance = 0,
                    Top = top, Length = len, Material = "S", Source = "p.94", Unresolved = susFlag,
                    WebRivets = new() { Col(2.5, 18, 5, 1.75), Col(5.5, 18, 5, 4.25), Col(9.25, 15, 5, 16.5), Col(13.0, 15, 5, 16.5), Col(16.75, 15, 5, 16.5), Col(20.5, 18, 5, 1.75), Col(24.0, 18, 5, 4.25) },
                    RivetDiameter = In(0.875), RivetMaterial = "7/8 in carbon steel rivet" },
                new TrussEndConnection { End = TrussMemberEnd.Both, Label = "2-Ls 7x4x1/2 (edge angles)", Angle = "L4x7x1/2", Back = -In(12.75), StandOff = In(0.75), Top = top, Length = len, Material = "C", Source = "p.94", Unresolved = susFlag },
                new TrussEndConnection { End = TrussMemberEnd.Both, Label = "2-Pls 17x13/16 (pin plates)", Back = -In(5.75), PlateWidth = In(14.25), PlateThickness = In(13.0 / 16), StandOff = In(0.75),
                    Top = top, Length = In(22), Material = "S", Source = "p.94", Unresolved = susFlag },
            };
            su++;
        }
        // stiffeners under the stringer lines (S13 = "Fin. top end"), web legs toward the CL from each end
        var dN = stl.Where(d => d < L / 2).ToList(); var dS = stl.Where(d => d >= L / 2).Select(d => d + aT).ToList();
        m.Stiffeners.Add(Stiff("2-Ls 6x4x7/16 + 2-Fills 4x3/4 (under the stringers)", "L6x4x7/16", In(0.75), half - In(0.75) - clip, 2 * half - In(1.5) - 2 * clip, 2 * half - In(16), dN, 1, 1, 4.5, 2.5, "p.94 / p.85"));
        m.Stiffeners.Add(Stiff("2-Ls 6x4x7/16 + 2-Fills 4x3/4 (under the stringers)", "L6x4x7/16", In(0.75), half - In(0.75) - clip, 2 * half - In(1.5) - 2 * clip, 2 * half - In(16), dS, -1, 1, 4.5, 2.5, "p.94 / p.85"));
        nStiff += stl.Count;
        if (m.Stiffeners.Count == 0) m.Stiffeners = null;
    }
    nConn += 2 * (fr + su);
    report.Add($"main: {fr} framed floor beams (end angles), {su} suspended (end plates), F7 not built");
}

// ================================================================ 4. stringer diaphragms 15 [ 33.9: clip angle at each end (p.55 "1-L 4 x 3 1/2 x 7/16 - Typical", p.94 "L-4 x 3 1/2 x 7/16 (C)";
// girder spans via the p.45 note). Drawn: 4 rivets through the stringer web (shared with the angle of the next bay on the other face) and the leg on the channel web.
string diaFlag = U("p.55, p.94, p.45", "diaphragm clip angles (L 4 x 3 1/2 x 7/16, one per end)", "drawn with 4 rivets per leg but no length / gage dimensions",
    "angle on the back of the channel web, 4 in. leg on the channel web, 3 1/2 in. leg on the stringer web, 12 1/2 in. long from 1 1/4 in. under the channel top, 4 rivets @ 3 in. per leg (gages 2 1/2 / 2 in.).");
int nClip = 0;
var ch = SteelCatalog.FindChannel("C15x33.9")!;
foreach (var bn in approach.Concat(girders).Append(Main))
    foreach (var li in T(bn).LineItems!.Where(li => li.Name.StartsWith("diaphragm 15[33.9")))
    {
        double vTop = li.Profile.Max(q => q.V);
        li.ClipAngles = new TrussClipAngles
        {
            Angle = "L4x3-1/2x7/16", HeelU = -ch.FlangeWidth / 2, Toward = -1, Top = vTop - In(1.25), Length = In(12.5), RivetDiameter = In(0.875), Material = "C",
            ItemLegRivets = Col(2.5, 4, 3, 1.75), WebLegRivets = Col(2.0, 4, 3, 1.75), Unresolved = diaFlag,
        };
        nClip++;
    }
report.Add($"diaphragm clip angles on {nClip} channels");

// ================================================================ 5. perforated plates: the access holes are cut
// Main truss (p.81 NOTES "MANHOLES: for all web members 12 x 20 @ 4'-0 c.c.; for chord members as shown" - chords "12 x 20 M.H. 3'-6 ctrs" on p.74 / p.81; p.74 web members
// "12 x 20 Manholes @ 4'-0 +- c/c", "2'-0 +- alt." between the two faces, first hole 10 in. clear of the gusset); bracing (p.71 sections: struts / laterals 13 1/2 x 24 @ 4'-6,
// sway 6 x 12 H.H. @ 2'-6, L11 sway 12 x 20 @ 4'-0, L11 strut 16 x 24 @ 4'-6, L0 strut 13 1/2 x 24 @ 4'-6 alt.; "manholes to be alternated on opposite sides of member",
// "manhole spacing shown on details is approximate"); approach box struts 2-12 [ 25 + 2-Pls (13-6) x 3/8: 6 x 12 H.H. @ 2'-6 (p.58).
// Hole width = the second number of the "(w-h)" plate callout; holes centred between the end gussets. Obround holes (semicircular ends).
string perfMain = U("p.74, p.81", "manhole layout of the main truss members", "the sheets give size and spacing (12 x 20 @ 3'-6 chords, @ 4'-0 web members, '2'-0 alt.') and 10 in. from the gusset only on the drawn web members; the chord manholes are drawn but the first hole is not dimensioned",
    "holes centred between the member's end gussets at the printed spacing, web members 10 in. and chords 1'-0 clear of the gussets, the bottom plate of web members shifted half a space.");
string perfBr = U("p.71", "manhole / hand hole layout of the bracing members", "p.71 gives size and spacing ('approximate') and 'alternated on opposite sides'; the first hole position is not given",
    "holes centred between the member's end gussets at the printed spacing, 1'-0 clear of the ends, the bottom plate shifted half a space.");
string perfAp = U("p.58, p.56", "hand holes of the approach box struts (6 x 12 @ 2'-6)", "the first hole position is not given", "holes centred at 2'-6, 1'-0 clear of the ends, bottom plate shifted half a space.");
int nPerf = 0;
double HoleWidth(string label)
{
    var mm = System.Text.RegularExpressions.Regex.Match(label, @"\(\s*([\d ./]+)\s*-\s*([\d ./]+)\s*\)");
    if (!mm.Success) return 0;
    double v = 0; foreach (var part in mm.Groups[2].Value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)) v += part.Contains('/') ? double.Parse(part.Split('/')[0]) / double.Parse(part.Split('/')[1]) : double.Parse(part);
    return In(v);
}
void Perf(TrussSectionDefinition s, double lengthIn, double pitchIn, double clearIn, string label, string flag, double widthIn = 0)
{
    foreach (var (p, bottom) in s.TopPlates.Select(p => (p, false)).Concat(s.BottomPlates.Select(p => (p, true))))
    {
        if (!p.Perforated) continue;
        double w = HoleWidth(p.Label);
        if (w <= 0) w = HoleWidth(s.Label);
        if (w <= 0) w = In(widthIn);
        if (w <= 0) { report.Add($"perforation: no hole width for {s.Name} {p.Label}"); continue; }
        p.Perforation = new TrussPerforation { Width = w, Length = In(lengthIn), Pitch = In(pitchIn), EndClear = In(clearIn), Stagger = bottom, Label = $"{w / 0.0254:0.##} x {lengthIn:0.##} @ {pitchIn / 12:0.##} ft", Unresolved = flag };
        nPerf++;
    }
}
{
    var t = T(Main);
    foreach (var s in t.Sections)
    {
        if (s.Name.StartsWith("CH ")) Perf(s, 20, 42, 12, "12 x 20 M.H. 3'-6 ctrs", perfMain);
        else if (s.Name.StartsWith("WM ")) Perf(s, 20, 48, 10, "12 x 20 M.H. @ 4'-0", perfMain);
        else if (s.Name is "STRUT" or "LAT31" or "LAT50") Perf(s, 24, 54, 12, "13 1/2 x 24 M.H. @ 4'-6", perfBr);
        else if (s.Name is "SWAY" or "L13-W14") Perf(s, 12, 30, 12, "6 x 12 H.H. @ 2'-6", perfBr);
        else if (s.Name == "FLAT21") Perf(s, 12, 30, 12, "6 x 12 H.H. @ 2'-6", perfBr, 6);       // p.71 floor laterals W21-W22: 2-Pls (13-6) x 3/8
        else if (s.Name == "SWAY11") Perf(s, 20, 48, 12, "12 x 20 M.H. @ 4'-0", perfBr);
    }
    // box struts at L0 / L11: perforated WEB plies (30-13 1/2) x 1/2 and (40-16) x 9/16 (p.71): holes in the webs, alternated
    foreach (var (nm, hw, hl, pitch) in new[] { ("STRUT L0", 13.5, 24.0, 54.0), ("STRUT L11", 16.0, 24.0, 54.0) })
        if (t.Section(nm) is { } s)
        {
            s.WebPlies[0].Perforation = new TrussPerforation { Width = In(hw), Length = In(hl), Pitch = In(pitch), EndClear = In(12), Label = $"{hw} x {hl} M.H. @ 4'-6 alt.", Unresolved = perfBr };
            nPerf++;
            s.Unresolved = s.Unresolved.Replace(" Web manholes 13 1/2 x 24 @ 4'-6 not cut.", "").Replace(" Web manholes 16 x 24 @ 4'-6 not cut.", "");
        }
}
foreach (var bn in approach)
    if (T(bn).Section("STRUTBOX") is { } s) Perf(s, 12, 30, 12, "6 x 12 H.H. @ 2'-6", perfAp);
report.Add($"perforated plates with cut access holes: {nPerf} plate definitions");
// note: box strut web plies are perforated on both webs with the same layout (the generator staggers only cover plates: 'Stagger' per plate)

// ================================================================ regenerate + save
var rep = Doc.Regenerate();
foreach (var bn in approach.Concat(girders).Append(Main))
{
    var st = Doc.Model(bn).Steel; var tm = st?.Truss;
    if (st == null || tm == null) { Print($"{bn}: no model"); continue; }
    double lb(PartCategory c) => st.Parts.Where(p => p.Category == c).Sum(p => p.Mass) * 2.20462262;
    Print($"{bn}: connection angles {st.Parts.Count(p => p.Category == PartCategory.TrussConnectionAngle)} parts ({lb(PartCategory.TrussConnectionAngle):N0} lb), connection plates / fills {st.Parts.Count(p => p.Category == PartCategory.TrussConnectionPlate)} ({lb(PartCategory.TrussConnectionPlate):N0} lb), "
        + $"stiffeners {lb(PartCategory.IntermediateStiffener):N0} lb, perforated plates {tm.Perforations.Count} ({tm.Perforations.Sum(x => x.Holes)} holes), rivets {tm.Rivets:N0}, steel {st.SteelMass * 2.20462262:N0} lb");
    foreach (var d in st.Diagnostics.Where(d => d.Message.Contains("end connection") || d.Message.Contains("line item rivets") || d.Message.Contains("perforated") || d.Message.Contains("clip angles")).Take(12)) Print("   " + d.Severity + ": " + d.Message);
    foreach (var e in rep.Errors.Where(e => e.StartsWith(bn))) Print("  ERROR " + e);
}
foreach (var s in report) Print("  " + s);
Doc.Save(outPath);
Print($"Saved {outPath}");
