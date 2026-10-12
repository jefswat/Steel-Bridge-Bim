// TxDOT CSJ 0047-07-228 (Northaven Trail, US 75 pedestrian underpass) - PEDESTRIAN RAILING, CURBS, EXPANSION JOINTS, MOW STRIPS.
// Called by build-nht.csx (approach units, before its regeneration) and by arch-nht.csx (arch span 10, before its final regeneration); re-runnable: replaces
// the curbs / railings / joints / mow strips it sets. Works on whatever NHT bridges exist (Structure "US 75 Ped. Underpass at Northaven Trail ...").
//   sbm run samples\plans\rail-joints-nht.csx "samples\plans\Northaven NHT.sbm"     (then regenerate / save from the caller)
// Plan data (PDF "C:\SBMData\0047-07-228 NHT 100% Plans_09-04-2020_comments_Reduced.pdf", no text layer: values read on 300-500 dpi rasters and on the
// page VECTORS, PyMuPDF get_drawings, p.89 at 1.5 pt per inch = the 1/2" = 1'-0 typical sections printed at half size):
//   p.83 general notes: railing posts and hardware stainless steel AISI 316; design dead load HIGH pedestrian rail 100 PLF, LOW 50 PLF; exposed concrete
//        edges chamfered 3/4".
//   p.84-88 layout: RAIL (PED)(LOW) 307.93 LT / 270.95 RT, RAIL (PED)(HIGH) 571.28 LT / 572.97 RT (measured along the face of rail = curb line, 7'-0 from
//        CL NHT: 289.44 ft of CL NHT + 7.00 x 2.6411 rad of curves 103 / 104 = 307.93 / 270.95); note 2 "the number and spacing of pedestrian railing posts
//        is diagrammatic only"; SEJ-B(MOD)(4") and BS-EJCP at Abut 1, P4, P7, P10, P11, P13, Abut 16.
//   p.89 typical approach sections: 15'-8 = 10" curb + 14'-0 + 10" curb, curb 4" high, curb line 7'-0 from CL NHT; LOW post 4'-3 (STA 20+49.30 to 23+38.74),
//        HIGH post 9'-10 (23+38.74 to 24+17.10 and 26+20.42 to 29+10.87); handrail 3'-0 above the trail; "for railing and fencing details see
//        'Pedestrian railing details' sheets" (NOT in this reduced set).
//   p.90 arch section: HIGH railing posts on the 10" x 4" arch curbs (curb lines 7'-0 from CL NHT), "see 'Pedestrian railing details, RAIL (PED)' sheets".
//   p.91 quantities: 5110 RAIL (PED)(LOW) 579.0 LF, (HIGH) 1,144.2 LF; 0454 SEJ-B(MOD) 4 in 107 LF (75 + 32); BS-EJCP 1,382 lb (194 per joint, 206 at
//        P10 / P11); riprap (mow strip)(4 in) 6.1 CY; T80SS(MOD) 78 LF.
//   p.97 / 99 pier details: mow strip 4" reinforced concrete, "2'-0 MAX, each face of column", at existing ground, 1'-0 min over the top of the drilled shaft.
//   p.113 deck plan: "CL SEJ at end of span 9 is not coincident with CL pier, offset 9 1/4" from CL Pier 10"; slab lengths to the CL SEJ.
//   p.105-106 arch piers: T80SS(MOD) traffic barrier along US 75 at Piers 10 / 11.
// Rules (user): model the plans; nothing invented - UNRESOLVED items name the page and the reason; 1/16"; differences reported.
using SBM.Core;
using SBM.Core.Bridge;
using SBM.Core.Geometry;

const string Structure = "US 75 Ped. Underpass at Northaven Trail (CSJ 0047-07-228)";
var unresolved = new List<string>();
string U(string pages, string item, string why, string then = "") { var t = UnresolvedFlag.Text(pages, item, why, then); unresolved.Add(t); return t; }
var nht = Doc.Project.Bridges.Where(b => b.Structure == Structure).ToList();
var units = nht.Where(b => b.Arch == null && b.Name.StartsWith("NHT Unit")).OrderBy(b => b.Name).ToList();
var arch = nht.FirstOrDefault(b => b.Arch != null);
Print($"rail-joints-nht: {units.Count} approach units{(arch != null ? " + " + arch.Name : "")}");

// ================================================================ 1. curbs on the approach decks (p.89, p.113)
// 10" wide x 4" high at each deck edge (curb line 7'-0 from CL NHT, deck edge 7'-10), exposed edges chamfered 3/4" (p.83 concrete note 3; the p.89 sections
// show both top corners chamfered). Section R = inches inward from the deck edge, H = inches above the deck at the edge: the deck falls 1 % to the edge, so
// the curb-line top is H 4.1 (= 4" above the trail at the curb line) and the top stays 4" above the deck across the curb.
BarrierProfilePoint P(double r, double h, double chamfer = 0) => new(In(r), In(h), 0, In(chamfer));
foreach (var b in units)
{
    var bar = b.Deck.Barrier;
    bar.Type = BarrierType.Custom;
    bar.IsCurb = true;
    bar.Left = true; bar.Right = true;
    bar.Reinforcement = false;                       // curb bars C (curb, top) / CS (p.113 bar table) belong to the deck reinforcing (nht-rebar)
    bar.Profile = new() { P(10, 0), P(10, 4.1, 0.75), P(0, 4.0, 0.75), P(0, 0) };
    bar.ProfileName = "Curb 10\" x 4\" (p.89, p.113)";
    bar.PerpendicularToDeck = false;
    bar.Source = "p.89 typical approach sections (10\" curb, 4\" typ.), p.113 typical transverse section (10\" curb), p.83 note 3 (3/4\" chamfers)";
    bar.BeginExtension = null; bar.EndExtension = null; bar.Zones = null;
}

// ================================================================ 2. pedestrian railing RAIL (PED) low / high (p.83, 84-91)
// Reference line = the curb line (face of rail for payment, 7'-0 from CL NHT); the railing stands on the curb top (base probed 5" outward = mid-curb).
// Section (R outward from the curb line, H above the curb top) from the p.89 page vectors: x = page points, curb line x0, curb top y0, 1.5 pt per inch.
double Rx(double x, double x0) => (x - x0) / 1.5;
double Hy(double y) => (326.9 - y) / 1.5;
RailPoint Q(double r, double h) => new(In(r), In(h));
// LOW post (right section, curb line x 471.1): outer face vertical 2" inside the deck edge (x 483.1), inner face tapered x 474.1 at the base to 476.6 at the
// top (y 256.4) -> 6" at the base, 4 5/16" at the top; top 4'-3 above the trail at the curb line (dimensioned) = 47" above the curb top.
var lowSec = new List<RailPoint> { Q(Rx(474.1, 471.1), 0), Q(Rx(483.1, 471.1), 0), Q(Rx(483.1, 471.1), 51 - 4), Q(Rx(476.6, 471.1), 51 - 4) };
// HIGH post (right section, curb line x 856.7): straight part like the low post to y 223.4, then curved inward (two cubic Beziers each face) to the tip at
// 9'-10 above the trail (dimensioned) = 114" above the curb top.
List<RailPoint> Bez((double x, double y)[] c, double x0, int n = 8) =>
    Enumerable.Range(1, n).Select(i =>
    {
        double t = (double)i / n, u1 = 1 - t;
        double x = u1 * u1 * u1 * c[0].x + 3 * u1 * u1 * t * c[1].x + 3 * u1 * t * t * c[2].x + t * t * t * c[3].x;
        double y = u1 * u1 * u1 * c[0].y + 3 * u1 * u1 * t * c[1].y + 3 * u1 * t * t * c[2].y + t * t * t * c[3].y;
        return Q(Rx(x, x0), Hy(y));
    }).ToList();
var highSec = new List<RailPoint> { Q(Rx(859.7, 856.7), 0), Q(Rx(868.7, 856.7), 0), Q(Rx(868.7, 856.7), Hy(223.4)) };
highSec.AddRange(Bez(new[] { (868.7, 223.4), (868.7, 210.6), (865.9, 197.9), (860.4, 186.2) }, 856.7));
highSec.AddRange(Bez(new[] { (860.4, 186.2), (855.0, 174.5), (847.0, 164.2), (837.2, 155.9) }, 856.7));
highSec.Add(Q(Rx(835.5, 856.7), Hy(158.0)));                                  // tip: outer curve end -> inner curve start
highSec.AddRange(Bez(new[] { (835.5, 158.0), (844.6, 166.2), (851.9, 176.3), (856.7, 187.6) }, 856.7));
highSec.AddRange(Bez(new[] { (856.7, 187.6), (861.5, 198.9), (863.8, 211.2), (863.3, 223.4) }, 856.7));   // ends at the inner straight face, closes to the base
double tipH = highSec.Max(q => q.H) / In(1) + 4;
Print($"HIGH post section: tip {tipH:0.00}\" above the trail at the curb line (plan 9'-10 = 118\"), {highSec.Count} points");
// Handrail: pipe drawn 1.87" OD (scaled) on a bracket inside the post, top 3'-0 above the trail at the curb line (dimensioned).
double hrOd = In(1.87);
RailMemberDefinition Handrail(double rIn) => new()
{
    Name = "HR", Kind = RailMemberKind.Handrail, Diameter = hrOd, Center = new RailPoint(In(rIn), In(36 - 4) - hrOd / 2), Description = "Handrail pipe, 1.87\" OD scaled (p.89)",
    MassPerLength = 0,
    Unresolved = "UNRESOLVED (p.89): handrail pipe size, wall and bracket - drawn only; the 'Pedestrian railing details' sheets are not in the set",
    Basis = "pipe OD 1.87\" and position scaled from the p.89 vectors; top 3'-0 above the trail (dimensioned); mass inside the plan design dead load of the envelope",
};
// TxDOT standard check (2026-10-12, C:\SBM\dist\standards): the Northaven RAIL (PED)(LOW) / (HIGH) is NOT a TxDOT standard rail type. RL-PR11 / PR22 are 3'-6 high
// galvanized A36 plate posts (PL 3/4 / PL 5/8) carrying 5-6 HSS rails (PR22 on a 1'-1 parapet), RL-PR3 is 4'-0 concrete pilasters with HSS + square bars on a
// 6" curb, RL-PR3-HD is the 3'-0 handrail for PR3 pilasters, RL-CLF-RO is an 8 ft chain link fence on HSS 3.500 posts on a concrete rail. The p.89 sections
// draw a tapered stainless (p.83 note 8, AISI 316) post 4'-3 / 9'-10 above the trail on a 10" x 4" curb, curved inward 1'-1 at the HIGH top, with a single
// bracket handrail and no HSS rails; the pay item is 5110 (a special specification item, p.91) 'RAIL (PED)(LOW / HIGH)', not Item 450 'RAIL (TY PRxx)'; the
// p.83 design dead loads 50 / 100 PLF differ from PR11's 30 plf. The only TxDOT pedestrian rail standard in the plan set is PR11(MOD) (p.65-66, sheets 116-117,
// retaining walls, 3'-6). So the post / fence / anchor details stay on the missing project sheets.
string why = "the project 'Pedestrian railing details' / RAIL (PED) sheets that size the posts, rails, fencing, base plates and anchors are not in this reduced set (p.89, p.90 refer to them), p.84 note 2 says the number and spacing of the posts is diagrammatic only, and the railing is not a TxDOT standard type (RL-PR3 / PR11 / PR22 / PR3-HD / CLF-RO 2019 checked: 3'-6 / 4'-0 galvanized plate-post or pilaster rails and an 8 ft chain link fence vs the p.89 4'-3 / 9'-10 tapered stainless posts, Item 5110 special pay item)";
string unrPosts = U("p.84 note 2, p.89, p.90", "railing posts, post spacing, base plates, anchor bolts, rails and fencing", why,
    "Posts / base plates / anchors not modeled; each run is a continuous envelope of the p.89 post section (flagged) plus the 3'-0 handrail.");
string unrTrans = U("p.85, p.89", "low-to-high railing transition at STA 23+38.74", "the transition detail is on the missing railing sheets; p.89 gives only the section limits",
    "The low run ends and the high run begins at the radial line at 23+38.74.");
PedestrianRailingDefinition Rail(string name, bool high, bool left, double? b0 = null, double? b1 = null) => new()
{
    Name = name, TypeName = high ? "RAIL (PED)(HIGH)" : "RAIL (PED)(LOW)", Kind = RailingKind.Guardrail,
    Offset = (left ? -1 : 1) * Ft(7), BeginStation = b0, EndStation = b1, BaseProbe = In(5),
    Material = "Stainless steel AISI 316 (p.83)", Density = 7980,
    Envelope = high ? highSec : lowSec,
    EnvelopeMassPerLength = (high ? 100 : 50) * 1.48816394,      // p.83 design dead load (lb/ft -> kg/m), handrail included
    EnvelopeUnresolved = $"UNRESOLVED (p.84, 89, 90): railing posts / fencing - {why}",
    EnvelopeBasis = high ? "p.89 HIGH post outline swept continuously (scaled; 9'-10 dimensioned); mass = p.83 design dead load 100 PLF"
                         : "p.89 LOW post outline swept continuously (scaled; 4'-3 dimensioned); mass = p.83 design dead load 50 PLF",
    Members = { Handrail(high ? Rx(857.65, 856.7) : Rx(472.1, 471.1)) },
    Source = "p.83, 84-88, 89, 90, 91",
};
foreach (var b in units)
{
    b.Railings = new();
    foreach (bool left in new[] { true, false })
    {
        string s = left ? "LT" : "RT";
        switch (b.Name)
        {
            case "NHT Unit 1": case "NHT Unit 2": b.Railings.Add(Rail($"Low {s}", false, left)); break;
            case "NHT Unit 3":
                b.Railings.Add(Rail($"Low {s}", false, left, null, Sta("23+38.74")));
                b.Railings.Add(Rail($"High {s}", true, left, Sta("23+38.74"), null));
                break;
            default: b.Railings.Add(Rail($"High {s}", true, left)); break;
        }
    }
}
if (arch != null)
{
    // Arch deck (p.90): HIGH railing on the 10" x 4" curbs; the arch deck runs joint to joint (CL SEJ 9 1/4" beyond CL Pier 10 / 11, p.113 note 7).
    arch.Railings = new();
    foreach (bool left in new[] { true, false })
    {
        var r = Rail($"High {(left ? "LT" : "RT")}", true, left);
        r.BeginLimitOffset = In(9.25); r.EndLimitOffset = In(9.25);
        arch.Railings.Add(r);
    }
}
foreach (var b in nht.Where(x => x.Railings != null)) foreach (var r in b.Railings!) { r.Unresolved.Clear(); }
if (units.FirstOrDefault()?.Railings?.FirstOrDefault() is { } r0) { r0.Unresolved.Add(unrPosts); }
if (units.FirstOrDefault(b => b.Name == "NHT Unit 3")?.Railings?.FirstOrDefault(r => r.Name == "High LT") is { } r3) r3.Unresolved.Add(unrTrans);

// ================================================================ 3. expansion joints SEJ-B(MOD) 4" + BS-EJCP cover plate + IGTS thickened slab ends
// Plans (p.84-88, 91, 109-117): SEJ-B(MOD)(4") and BS-EJCP at Abut 1, P4, P7, P10, P11, P13, Abut 16; slabs dimensioned to the CL SEJ (P10 / P11: 9 1/4"
// from CL pier, p.113 note 7); "see IGTS" for the thickened slab ends (p.113 note 3 and plan labels at every joint). Standards (C:\SBM\dist\standards, NOT in the
// plan set; the 2024 revisions POST-DATE the 09/2020 plans): TxDOT MS-SEJ-B (10-2024), MS-BS-EJCP (10-2024), IG-IGTS (08-2017, rev. 11-24 "flipped top mat").
//  SEJ-B joint section: armor PL 1/2 x 4 (top flush with the slab), Bar 1/2 x 1/4 seal retainer at the plate bottom (1/4 above it, projecting 1/4 into the
//    opening), 5/8" stud anchors at 6" C.C. max (first 3" from the plate end, end view) 1 1/2" below the top, 4" +- horizontal then 4" +- inclined (bend
//    18.4 deg, measured on the standard's vectors); opening at 70 F per the manufacturer table: D.S. Brown V-400 2 1/4", R.J. Watson SF-400 2 1/2", SSI SSS-400
//    2 1/2", Watson Bowman Acme SPS-400 2" -> 2 1/2" modeled (the standard's drawn joint section is the R.J. Watson seal). "At sidewalk": armor ends at the toe
//    of the sidewalk (here the curb toe, 10" from the slab edge) and the seal turns up the curb face ("for curbs trim seal 1/2" below top surface").
//  BS-EJCP: 1/4" slip resistant plate 1'-4 wide centred on the joint, bent 1/4" at the CL, 3/8" x 2 1/2" flat head sleeve anchors in two staggered rows 1 1/2"
//    and 4 1/2" from the plate edge on one side, 3" from the ends, "Eq Spa at 6" Max"; ends 1" (rail) - 2" (sidewalk) max from the curb: 1" modeled (p.91
//    194 lb = 14 plf x 13.83 ft between the curb toes less 1" each end; 206 lb at the 19.58 deg skewed P10 / P11 joints).
//  IGTS: thickened slab end between the girders, 4'-0 from the CL joint (face of backwall at the abutments) square to it, "slab thickness plus 2" min" deep =
//    the plan X = 10 1/2" at CL brg (8 1/2" slab + 2" haunch), 45 deg chamfer to the slab soffit, sides at the girder top flange edges (Tx28 3'-0).
// Deck ends: the slab faces are the armor faces, opening / 2 each side of the CL SEJ at the piers; at the abutments the far face is the face of backwall
// (SEJ-B "joints at abutments") so the bridge slab ends one opening short of the support line.
double open70 = In(2.5);
const string sejStd = "TxDOT MS-SEJ-B (10-2024)", ejcpStd = "TxDOT MS-BS-EJCP (10-2024)", igtsStd = "TxDOT IG-IGTS (08-2017 rev. 11-24)";
string verNote = "standard version post-dates the plans (09/2020): the 2024 revision in hand is used";
string unrSej = U("p.84-88, 91, 113", "SEJ-B(MOD) modification and joint seal manufacturer",
    $"the plans call for SEJ-B(MOD); the (MOD) sheet / note is not in the set, and the SEJ-B standard gives the 70 F opening per manufacturer (2\" to 2 1/2\"); {verNote}",
    "SEJ-B (10-2024) armor PL 1/2 x 4 + Bar 1/2 x 1/4 + 5/8\" studs at 6\" max between the curb toes, 2 1/2\" opening (R.J. Watson SF-400, the standard's drawn section); seal = envelope in the opening, its upturns at the curbs not modeled.");
string unrSkew = U("p.113, 115 (P10 / P11)", "SEJ-B armor at the 19.58 deg skewed P10 / P11 joints",
    "SEJ-B 'skews over 15 deg' terminates the armor at a square slab breakback 2'-0 from each slab edge, but the p.113 / 115 deck plans draw a straight skewed slab end at P10 / P11 (part of the unknown (MOD)?)",
    "Straight skewed slab end as drawn; armor along the skewed joint between the curb toes like the other joints.");
string unrEjcp = U("p.91", "BS-EJCP cover plate version", $"{verNote}; the clear distance to the curb is a maximum (1\" at a rail, 2\" at a sidewalk)",
    "1/4\" x 1'-4 plate from 1\" inside each curb toe (reproduces the p.91 194 / 206 lb at the standard's 14 plf estimate); 1\" x 1\" corner clips and edge bevels not modeled.");
string unrIgts = U("p.109-117 (plan notes 'see IGTS')", "IGTS thickened slab end", $"{verNote} (rev. 11-24 'flipped top mat' changes the bars, not the concrete outline); bars G / H / J / M not placed (R3: the bar arrangement of the plans' 2017 version cannot be told from the revised sheet)",
    "Thickened slab end per IGTS 'partial plan for slabs without breakback' / section A-A between the girder top flanges.");
var txTop = SBM.Core.Prestressed.TxGirderCatalog.Get(TxGirderType.Tx28).TopWidth;
double BaseDeckEnd(string sup) => sup is "Pier 10" or "Pier 11" ? -In(9.25) : 0;   // build-nht.csx: CL SEJ (abutments: face of backwall)
JointArmorDefinition Armor() => new()
{
    Standard = sejStd, PlateThickness = In(0.5), PlateDepth = In(4), BarProjection = In(0.25), BarHeight = In(0.5), BarAboveBottom = In(0.25),
    EdgeInsetLeft = In(10), EdgeInsetRight = In(10), FarPlate = true,
    StudDiameter = In(0.625), StudDrop = In(1.5), StudLength = In(4), StudBentLength = In(4), StudBendDegrees = 18.4, StudHeadDiameter = In(1.25), StudHeadHeight = In(0.3125),
    StudSpacing = In(6), StudEndDistance = In(3), Material = "ASTM A36 (SEJ-B), galvanized",
    Unresolved = unrSej, Basis = "SEJ-B (10-2024) joint section; studs: the top stud anchor (bent) of the joint section, legs 4\" +- each, bend scaled 18.4 deg",
};
JointCoverPlateDefinition Cover() => new()
{
    Standard = ejcpStd, Thickness = In(0.25), Width = Ft(1) + In(4), Crown = In(0.25), EdgeInsetLeft = In(10), EdgeInsetRight = In(10), EndClearance = In(1),
    AnchorDiameter = In(0.375), AnchorLength = In(2.5), AnchorRows = new() { In(1.5), In(4.5) }, AnchorSpacing = In(6), AnchorEndDistance = In(3),
    Material = "Slip resistant steel plate (BS-EJCP), hot-dip galvanized", Unresolved = unrEjcp, Basis = "BS-EJCP (10-2024) plan / section A-A / bending diagram",
};
// IGTS block between the girder top flanges at a deck end (offsets along the support line from the span layout x cos(skew) = alignment offsets)
DeckEndBlockDefinition Igts(BridgeDefinition b, bool begin, bool abut)
{
    var lay = begin ? b.Girders.SpanLayouts.First(l => l.Span == 0).Begin : b.Girders.SpanLayouts.First(l => l.Span == b.Supports.Count - 2).End;
    var sup = begin ? b.Supports[0] : b.Supports[^1];
    double cs = Math.Cos(sup.SkewDegrees * Math.PI / 180);
    double g1 = lay.Offsets[0], g2 = g1 + lay.Spacings[0];
    return new DeckEndBlockDefinition
    {
        Depth = In(8.5 + 2), FlatLength = Ft(4) - (abut ? open70 : open70 / 2), ChamferRun = In(2),
        LeftOffset = (g1 + txTop / 2) * cs, RightOffset = (g2 - txTop / 2) * cs,
        Source = igtsStd, Unresolved = unrIgts, Basis = "4'-0 from the CL joint / face of backwall, 10 1/2\" deep (slab + 2\"), 45 deg chamfer, between the Tx28 top flanges",
    };
}
ExpansionJointDefinition Sej(BridgeDefinition b, bool begin, bool owner)
{
    var sup = begin ? b.Supports[0] : b.Supports[^1];
    bool abut = sup.Type == SupportType.Abutment;
    return new ExpansionJointDefinition
    {
        Type = ExpansionJointType.Sealed, GapAt50F = open70, BodyCentered = false, BlockoutWidth = 0, BlockoutDepth = In(4), SetDown = In(1), EdgeInset = null, Body = owner,
        Designation = owner ? "SEJ-B(MOD) 4 in sealed expansion joint + BS-EJCP cover plate" : null,
        Armor = owner ? Armor() : null, CoverPlate = owner ? Cover() : null, EndBlock = Igts(b, begin, abut),
        Unresolved = owner ? unrSej : null,
        Basis = owner ? "strip seal envelope in the 2 1/2\" opening between the armor faces, 1\" below the top of slab to the armor bottom, between the curb toes" : null,
    };
}
foreach (var b in units)
{
    foreach (var s in b.Supports) s.ExpansionJoint = null;
    var first = b.Supports[0]; var last = b.Supports[^1];
    // slab ends at the armor faces
    foreach (var s in new[] { first, last })
        s.DeckEndOffset = BaseDeckEnd(s.Name) - (s.Type == SupportType.Abutment ? open70 : open70 / 2);
    switch (b.Name)
    {
        case "NHT Unit 1": first.ExpansionJoint = Sej(b, true, true); last.ExpansionJoint = Sej(b, false, true); break;     // Abut 1, P4
        case "NHT Unit 2": first.ExpansionJoint = Sej(b, true, false); last.ExpansionJoint = Sej(b, false, true); break;    // (P4), P7
        case "NHT Unit 3": first.ExpansionJoint = Sej(b, true, false); last.ExpansionJoint = Sej(b, false, true); break;    // (P7), P10 (arch beyond)
        case "NHT Unit 4": first.ExpansionJoint = Sej(b, true, true); last.ExpansionJoint = Sej(b, false, true); break;     // P11 (arch beyond), P13
        case "NHT Unit 5": first.ExpansionJoint = Sej(b, true, false); last.ExpansionJoint = Sej(b, false, true); break;    // (P13), Abut 16
    }
}

// ================================================================ 4. mow strips at the approach piers (p.85-88, 91, 97, 99)
// "MOW STRIP (2'-0" MAX, EACH FACE OF COLUMN)", 4" reinforced concrete (#3 @ 18"), at existing ground, 1'-0 min above the top of the drilled shaft (EL. D).
// 2'-0 on every face of the 12 approach columns gives 8 x 0.494 + 4 x 0.543 = 6.12 CY = the p.91 riprap (mow strip)(4 in) quantity 6.1 CY.
string unrMow = U("p.97, 99", "mow strip top elevation and width", "the existing ground at the piers is not given (the details show the strip at existing ground, at least 1'-0 above the top of shaft) and the width 'varies, 2'-0 max'",
    "Strips 2'-0 wide on each column face (reproduces the 6.1 CY quantity), top at EL. D + 1'-0 (the minimum cover), flagged.");
var mowPiers = new HashSet<string> { "Pier 2", "Pier 3", "Pier 4", "Pier 5", "Pier 6", "Pier 7", "Pier 8", "Pier 9", "Pier 12", "Pier 13", "Pier 14", "Pier 15" };
int mowSet = 0;
foreach (var b in units)
    foreach (var s in b.Supports.Where(x => mowPiers.Contains(x.Name) && x.Substructure is { Enabled: true }))
    {
        s.Substructure!.MowStrip = new MowStripDefinition
        {
            Width = Ft(2), Thickness = In(4), TopAboveFoundation = Ft(1), Source = "p.97 / 99 mow strip detail, p.91 6.1 CY",
            Unresolved = unrMow, Basis = "2'-0 each face (p.91 quantity reproduced), top = top of drilled shaft + 1'-0 (minimum)",
        };
        mowSet++;
    }
Print($"Mow strips set on {mowSet} approach piers (plan: 12).");
U("p.85, p.95, p.96", "riprap at the abutments", "p.85 points to the retaining wall sheets ('riprap, see retaining wall sheets'); the bridge sheets show no mow strip at Abutments 1 / 16", "No mow strip at the abutments.");

// ================================================================ 5. arch pier traffic barrier T80SS(MOD) (p.105-106; TxDOT RL-T80SS 09-2019)
// p.105 section D-D (3/16" = 1'-0, VECTORS 1.125 pt/in): barrier 4'-6 high (dimensioned) on replacement concrete pavement 2'-4 wide (dimensioned), 1'-1 deep
// (p.106 section A-A), pavement top 2'-9 above the drilled shaft top (dimensioned), barriers both sides of the 4'-6 column, back faces 9 1/2" from the column
// faces (SCALED), traffic faces outward. The drawn MOD section = the T80SS 'on bridge slab' section (RL-T80SS sheet 2, vectors 3 pt/in: 1'-5 base below the
// back step, 1" top chamfers, 1/2" x 2" reveals, traffic face 0.1905 batter) with the 19 1/2" lower back face lengthened by 1'-0 (D-D: step 31 1/2" above
// the base, base 1'-7 5/16" = 17" + 12 x 0.1905, top flat 7"; all within the scaled plan precision). p.106 elevation: 9'-1 + 4'-6 (column) + 7'-11 = 21'-6 at
// 4'-6, 15'-0 taper at 1:10 to the existing 3'-0 barrier (south, p.105 plan), 1'-8 at the north end under the UHMW transition wedge (11'-8 - 10'-0),
// = 38'-2 per side = the p.105 "38'-2 limits of cutting and restoring pavement".
var pier10 = units.SelectMany(b => b.Supports).FirstOrDefault(s => s.Name == "Pier 10" && s.Substructure is { Enabled: true });
string unrBar = U("p.105, 106", "T80SS(MOD) barrier base / existing pavement and taper section",
    "the barrier stands on the existing US 75 pavement, whose profile / cross slope are not given (only section D-D's 2'-9 from the pavement top to the shaft top); the 'Traffic rail T80SS(MOD)' sheet (p.105 note 6) is not in the set, so the section at the 1:10 taper / north end is not drawn; the p.105 plan draws the column centred in the 21'-6 (8'-6 + 4'-6 + 8'-6 scaled) while the p.106 elevation dimensions 9'-1 / 7'-11",
    "Level base at shaft top + 2'-9; MOD section = RL-T80SS bridge-slab section + 1'-0 lower back face (D-D vectors); the dimensioned 9'-1 on the taper (south) side; taper = the section shortened from the base (top shape kept); back faces 9 1/2\" (scaled) from the column faces.");
string unrP11 = U("p.91, 105, 106", "T80SS(MOD) barrier at Pier 11", "the 'arch piers 10 and 11 traffic barrier details' draw and dimension Pier 10 only; p.91 lists 78 LF for 'Piers 10 & 11' = 2 x 39 LF, about the two 38'-2 Pier 10 barriers",
    "Barriers modeled at Pier 10 only (2 x 38'-2 = 76.33 LF vs 78 LF reported).");
U("p.106", "T80SS(MOD) reinforcing (bill R / RH / H1 / S1 / U, 1,823 lb one side)", "the bar arrangement is on the missing 'Traffic rail T80SS(MOD)' sheet; the p.106 bill (for contractor's information) does not fit the RL-T80SS spacing (40 S1 at 6\" max cover 20 ft of the 38'-2 barrier) - positions would be guesses",
    "Bars not placed; the bill is a check value only.");
U("p.105, 106", "T80SS(MOD) UHMW-PE transition wedge (north end)", "it is anchored on the existing concrete back-up wall whose height and outline are not given",
    "Not modeled; the barrier ends 1'-8 north of the construction joint.");
if (pier10 != null)
{
    double sTop = 639.08, xBack = 492.81;
    double Fi(double ft, double inch) => Ft(ft) + In(inch);
    RailPoint T(double x, double y) => new(In((x - xBack) / 3), In((sTop - y) / 3 + 12));
    var prof = new List<RailPoint>
    {
        new(In(1.5), 0),                                   // base, lower back face (standard x 497.31)
        T(497.31, 580.59), T(492.81, 576.09),              // back step
        T(492.81, 559.59), T(494.31, 558.09), T(494.31, 552.08), T(492.81, 550.58),   // reveal 1
        T(492.81, 538.58), T(494.31, 537.08), T(494.31, 531.08), T(492.81, 529.58),   // reveal 2
        T(492.81, 516.08), T(495.81, 513.08), T(516.81, 513.08), T(520.37, 516.03),   // top with 1" chamfers
        new(In((543.81 - xBack) / 3 + 12 * (543.81 - 520.37) / (639.08 - 516.03)), 0),  // toe: the traffic face continued 1'-0 down
    };
    double colHalf = Ft(2.25), sS = -In(7), gapBack = In(9.5);
    double sNorth = sS - colHalf - Fi(7, 11), sSouth = sS + colHalf + Fi(9, 1);
    double[] ss = { sNorth - Fi(1, 8), sNorth, sSouth, sSouth + Ft(15) };
    double[] hh = { Fi(4, 6) - Fi(1, 8) / 10, Fi(4, 6), Fi(4, 6), Ft(3) };
    pier10.Substructure!.GroundBarriers = new();
    foreach (int side in new[] { 1, -1 })      // +1 = east (up-station, +N) barrier
    {
        double n = side * (colHalf + gapBack);
        pier10.Substructure.GroundBarriers.Add(new GroundBarrierDefinition
        {
            Name = side > 0 ? "T80SS(MOD) barrier east" : "T80SS(MOD) barrier west", TypeName = "T80SS(MOD)",
            Profile = prof, FullHeight = Fi(4, 6), StretchAt = 0,
            Path = ss.Select((x, i) => new GroundBarrierVertex(x, n, hh[i])).ToList(), TrafficSide = side > 0 ? -1 : 1,
            BaseAboveFoundation = Fi(2, 9), PavementR0 = -In(1.25), PavementWidth = Fi(2, 4), PavementThickness = Fi(1, 1),
            PlanLength = Fi(38, 2), Source = "p.105-106, RL-T80SS (09-2019) sheet 2", Unresolved = unrBar,
            Basis = "level base at shaft top + 2'-9 (D-D); RL-T80SS bridge-slab section + 1'-0 (D-D vectors); 9'-1 south / 7'-11 north of the column (p.106)",
        });
    }
}
else Print("rail-joints-nht: Pier 10 substructure not found - T80SS(MOD) not set");

foreach (var t in unresolved) Print("UNRESOLVED " + t);
