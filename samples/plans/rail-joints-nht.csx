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
string why = "the 'Pedestrian railing details' / RAIL (PED) sheets that size the posts, rails, fencing, base plates and anchors are not in this reduced set (p.89, p.90 refer to them), and p.84 note 2 says the number and spacing of the posts is diagrammatic only";
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

// ================================================================ 3. expansion joints SEJ-B(MOD) 4" + BS-EJCP cover plate (p.84-88, 91, 113)
// 7 joints; each physical joint is set on ONE unit (the device is one element): Abut 1 (Unit 1 begin), P4 / P7 / P10 / P13 (end of Units 1 / 2 / 3 / 4),
// P11 (Unit 4 begin), Abut 16 (Unit 5 end). The slab is dimensioned to the CL SEJ (p.109-117), so the deck ends stay there.
string unrJoint = U("p.84-88, 91, 113", "SEJ-B(MOD) joint opening, armor and blockout, BS-EJCP cover plate, IGTS thickened slab end",
    "the SEJ-B(MOD), BS-EJCP and IGTS standards are not in the set; the sheets give only the joint line (CL SEJ; 9 1/4\" from CL pier at Piers 10 / 11), the 4 in size of the pay item and the BS-EJCP weight per joint (p.91)",
    "Deck ends kept at the CL SEJ (plan slab dimensions); each joint is a 4 in wide placeholder centred on the CL SEJ through the slab depth, full deck width, no blockout or thickened end.");
ExpansionJointDefinition Sej(bool body = true) => new()
{
    Type = ExpansionJointType.Sealed, Designation = "SEJ-B(MOD) sealed expansion joint (4 in) + BS-EJCP cover plate", GapAt50F = In(4), BodyCentered = true,
    BlockoutWidth = 0, BlockoutDepth = In(8.5), SetDown = 0, EdgeInset = 0, Body = body,
    Unresolved = unrJoint, Basis = "4 in (pay item size) wide envelope centred on the CL SEJ, top of slab to the slab soffit, between the deck edges",
};
foreach (var b in units)
{
    foreach (var s in b.Supports) s.ExpansionJoint = null;
    var first = b.Supports[0]; var last = b.Supports[^1];
    switch (b.Name)
    {
        case "NHT Unit 1": first.ExpansionJoint = Sej(); last.ExpansionJoint = Sej(); break;     // Abut 1, P4
        case "NHT Unit 2": last.ExpansionJoint = Sej(); break;                                   // P7
        case "NHT Unit 3": last.ExpansionJoint = Sej(); break;                                   // P10 (9 1/4" off CL pier)
        case "NHT Unit 4": first.ExpansionJoint = Sej(); last.ExpansionJoint = Sej(); break;     // P11, P13
        case "NHT Unit 5": last.ExpansionJoint = Sej(); break;                                   // Abut 16
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

// ================================================================ 5. arch pier traffic barrier T80SS(MOD) (p.105-106) - not built
U("p.105, 106", "T80SS(MOD) traffic barrier at Piers 10 / 11 (78 LF)", "the T80SS standard section is not in the set and the barrier stands on the existing US 75 pavement, whose elevation and lane geometry are not given (the sheets dimension it only relative to the column: 4'-6 high, 9'-1 + 4'-6 + 7'-11 = 21'-6 straight plus 7'-6 transitions, 2'-4 footings)",
    "Not modeled.");

foreach (var t in unresolved) Print("UNRESOLVED " + t);
