// NCDOT B-2500 Bonner Unit 6 - DECK ENDS / EXPANSION JOINTS at bent 29 (modular) and bent 35 (Transflex), PARAPET JOINTS and END POSTS.
// Called by build-bonner-unit6.csx before regeneration (re-runnable on "Bonner Unit 6").
//   sbm run samples\plans\joints-parapet-bonner-unit6.csx "<model.sbm>"
// Plan data (p. = PDF page of "B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf"):
//   p.454 (S4-61) EXPANSION JOINT SEAL DETAILS: CONCRETE JOINT OPENING TABLE - DIM "A" @ 60 F bent 29 = 2'-10" (modular), bent 35 = 4 1/4" (Transflex);
//          Section A-A (bent 29): 6'-0" modular joint blockout from the bent control line, 1'-1" MIN. deep; Section B-B (bents 35 / 40 / 45): CL expansion
//          joint = CL bent control line; 3/4" cover plates in a recess of the parapet face (DIM "B" 6'-3" / 2'-9", 4'-5" high), concrete inserts.
//   p.407 (S4-15) Section K-K + p.450 (S4-57): CL modular joint 1'-2 1/2" (+-) ahead of the bent 29 control line, "VARIES | VARIES" = opening centred on it.
//   p.408 (S4-16) Section N-N: 1'-3 1/2" Transflex joint blockout, 4" deep (formed opening, magnesium ammonium phosphate concrete, p.455 0.6 CY).
//   p.448 (S4-55) CONCRETE PARAPET PLAN UNIT 6: joints in the parapet 28'-5" + 4 spa @ 26'-0" + 28'-5" per span measured along the outside edge of deck
//          from the bent control lines (span 35: 28'-5 9/16" / 28'-4 7/16" last panel = the arc); "END POST AT MODULAR JOINT" at both unit ends.
//   p.450 (S4-57) / p.451 (S4-58) END POSTS: 3'-9" long from the end of the parapet, 1'-2" wide (as the parapet), 4'-8" high (p.454 end view;
//          p.405 "4'-8\" (TYP.)"), 1/2" expansion joint material in the joints (p.450 elevation at expansion joints).
using SBM.Core;
using SBM.Core.Bridge;

var br = Doc.Project.Bridges.First(b => b.Name == "Bonner Unit 6");
double F(double ft, double inch = 0) => Ft(ft) + In(inch);
var s29 = br.Supports.First(s => s.Name == "Bent 29");
var s35 = br.Supports.First(s => s.Name == "Bent 35");

// ---------------------------------------------------------------- deck ends + joints
// bent 29: Unit 6 deck face = CL modular joint 1'-2 1/2" (+-) + DIM A / 2 = 1'-5" -> 2'-7 1/2" ahead of the bent control line (begin end: offset - = into the span).
s29.DeckEndOffset = -F(2, 7.5);
s29.ExpansionJoint = new ExpansionJointDefinition
{
    Type = ExpansionJointType.Modular, GapAt50F = F(2, 10), BlockoutWidth = F(6) - F(2, 7.5), BlockoutDepth = F(1, 1), Body = true,
    Designation = "Modular expansion joint (concrete opening DIM \"A\" 2'-10\" at 60 F, p.454; seal by the manufacturer)",
    Unresolved = UnresolvedFlag.Text("p.454, p.407, p.450", "modular joint device and the Unit 6 deck end at bent 29",
        "the device is designed by the supplier (special provisions); the CL joint is dimensioned 1'-2 1/2\" (+-) from the bent control line and DIM \"A\" is given at 60 F only",
        "Deck face 2'-7 1/2\" ahead of the bent control line (CL joint + 1'-5\"); generic device envelope over the 2'-10\" opening; blockout to the 6'-0\" line, 1'-1\" (MIN.) deep."),
    Basis = "p.454 Section A-A + joint opening table, p.407 Section K-K",
};
// bent 35: CL joint = bent control line (p.454 Section B-B), opening 4 1/4" at 60 F -> deck face 2 1/8" back of the bent control line.
s35.DeckEndOffset = -In(2.125);
s35.ExpansionJoint = new ExpansionJointDefinition
{
    Type = ExpansionJointType.Sealed, GapAt50F = In(4.25), BlockoutWidth = F(1, 3.5), BlockoutDepth = In(4), Body = true,
    Designation = "Transflex expansion joint seal (concrete opening DIM \"A\" 4 1/4\" at 60 F, p.454; 1'-3 1/2\" x 4\" blockout, p.408)",
    Unresolved = UnresolvedFlag.Text("p.454, p.408", "Transflex joint device at bent 35", "the seal is a proprietary device (special provisions, shop drawings); only the blockout and the opening are dimensioned",
        "Generic device envelope over the 4 1/4\" opening; blockout 1'-3 1/2\" x 4\" (magnesium ammonium phosphate concrete, p.455)."),
    Basis = "p.454 Section B-B + joint opening table, p.408 Section N-N",
};

// ---------------------------------------------------------------- parapet joints (1/2" open joints, p.448)
var joints = new List<BarrierJoint>();
string[] bents = { "Bent 29", "Bent 30", "Bent 31", "Bent 32", "Bent 33", "Bent 34" };
foreach (var b in bents)
{
    if (b != "Bent 29") joints.Add(new BarrierJoint { Support = b, Along = 0, Width = In(0.5), Source = "p.448 joint at the bent control line" });
    for (int k = 0; k < 5; k++)
        joints.Add(new BarrierJoint { Support = b, Along = F(28, 5) + k * F(26), Width = In(0.5), Source = "p.448: 28'-5\" + 4 spa @ 26'-0\" from the bent control line, along the outside edge of deck" });
}
br.Deck.Barrier.Joints = joints;

// ---------------------------------------------------------------- end posts (zones at the deck ends)
// End post 3'-9" from the end of the parapet (= the deck face), 1'-2" wide x 4'-8" high (p.450 / p.451 / p.454). The zone is centred by station; it is made
// 1/2" longer toward the deck end so that it ends exactly on the deck end (zones reaching a barrier end are clamped there; the inner face stays 3'-9" from it).
var post = new List<BarrierProfilePoint> { new() { R = In(1.5), H = 0 }, new() { R = In(15.5), H = 0 }, new() { R = In(15.5), H = In(56) }, new() { R = In(1.5), H = In(56) } };
double len = F(3, 9) + In(0.5);
br.Deck.Barrier.Zones = new()
{
    new BarrierZone { Name = "end post at bent 29", Station = s29.Station + F(2, 7.5) + F(3, 9) - len / 2, Length = len, Profile = post, Source = "p.450 (S4-57) plan of end posts at bent 29, p.448" },
    new BarrierZone { Name = "end post at bent 35", Station = s35.Station - In(2.125) - F(3, 9) + len / 2, Length = len, Profile = post, Source = "p.451 (S4-58) plan of end posts at bent 35, p.448" },
};
Print($"Bonner Unit 6: joints at bents 29 / 35, {joints.Count} parapet joints per side, end posts at both unit ends");

// ---------------------------------------------------------------- deck drains (p.409-414 plan of spans, p.404 typical section)
// "11-6\" (span 30) / 12-6\" (spans 31-35) PVC PIPE DECK DRAINS AT APPROX. 13'-8 1/2\" CTS. (MEASURED ALONG -L-)", 18'-8 1/2\" from bent 29, 5'-0\" each side of
// the bents; drawn along the RIGHT (low) gutter; "ALL DRAINS ARE TO BE PLACED VERTICAL" (p.409 note); p.404: 6\" PVC drain pipe through the slab at the parapet face.
br.Drains.RemoveAll(d => d.SlabPipeOnly);
string drainFlag = UnresolvedFlag.Text("p.404, p.409-414", "6\" PVC pipe deck drain positions", "the spacing is 'approx. 13'-8 1/2\"' along -L- and the pipe is drawn against the parapet face without a dimension across",
    "Stations at the printed approximate spacing; pipe centre 3\" (= its radius) from the parapet face, vertical through the slab.");
var drainSta = new List<double>();
for (int k = 0; k < 11; k++) drainSta.Add(br.Supports.First(s => s.Name == "Bent 29").Station + F(18, 8.5) + k * F(13, 8.5));
foreach (var b in new[] { "Bent 30", "Bent 31", "Bent 32", "Bent 33", "Bent 34" })
    for (int k = 0; k < 12; k++) drainSta.Add(br.Supports.First(s => s.Name == b).Station + F(5) + k * F(13, 8.5));
foreach (double st in drainSta)
    br.Drains.Add(new DeckDrainDefinition
    {
        Station = st, Left = false, FromGutter = In(3), Type = "6\" PVC pipe deck drain", PipeDiameter = In(6), SlabPipeOnly = true,
        Outlet = DrainOutlet.FreeFall, Unresolved = drainFlag, Basis = "approximate stations; pipe against the parapet face",
    });
Print($"Bonner Unit 6 deck drains: {drainSta.Count} (11 + 5 x 12, p.409-414)");
