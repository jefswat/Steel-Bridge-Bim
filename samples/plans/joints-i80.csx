// Expansion joints of I-80 WB (Design 116): MODULAR joints at both abutments and at Piers 5WB, 11WB (design sheets 202-203,
// slab sections on 178, 183, 188). Run after build-i80-wb.csx (re-runnable; sets the deck-end offsets of the plans).
//  * "C" = opening between the deck-end faces at 50 F (9" piers = 4 1/2" each side of the CL pier; 6" abutments to the backwall face).
//  * "D" = blockout width from the deck-end face, perpendicular to the joint (25" piers, 18" abutments); depth 1'-2; device 1/8" below top of slab.
//  * CL bearing to deck end: 1'-1 1/2 Pier 5WB (both units), 1'-3 1/2 Pier 11WB (both units), 1'-4 abutments.
//  * One device body per joint (spans the whole opening): Unit 1 owns 5WB, Unit 2 owns 11WB. The joint line is the deck-end line (parallel to the support line).
//  * The Pier 6A part of the Unit 2 begin (kinked end of slab, PDF 186 Detail A) is set by deck-end-kinks.csx (SupportDefinition.DeckEndPoints; run it after this script).
//    NOT modeled:
//    the Pier 19G joint on this side (Ramp G Unit 4 owns it), barrier slider plates, the manufacturer's beams/support boxes/anchors.
//  * DECK END BLOCKS ("end dam diaphragm", slab sheets 178/183/188 sections B-B, C-C): 2'-2 deep (1'-2 blockout + 1'-0 below), soffit flat for
//    3'-4 1/2 from the deck end at the piers (1'-3 1/2 + 2'-1, pdf 185 C-C, 190 C-C) and 2'-10 at the abutments (1'-6 + 1'-4, pdf 185 B-B,
//    195 C-C), then rising at 45 degrees to the 8" slab soffit. Set DECK_END_BLOCKS = false below to switch them off.
//    Reinforcing (marks 5d/5e/4d): schedule only, UNRESOLVED (deck-rebar-i80.csx; docs/plan-data/deck-rebar.md section 5).
using SBM.Core.Bridge;
using SBM.Core.Geometry;

const bool DECK_END_BLOCKS = true;
double FI(int ft, double inch) => Ft(ft) + In(inch);
var units = Doc.Project.Bridges.Where(b => b.Name.StartsWith("I-80 WB Unit")).OrderBy(b => b.Name).ToList();
if (units.Count != 3) throw new Exception("Expected I-80 WB Units 1-3 (run build-i80-wb.csx first).");

void Joint(SupportDefinition s, double deckEnd, double cIn, double dIn, bool body, ExpansionJointType type = ExpansionJointType.Modular)
{
    s.DeckEndOffset = deckEnd;
    s.ExpansionJoint = new ExpansionJointDefinition
    {
        Type = type, GapAt50F = In(cIn), BlockoutWidth = In(dIn), BlockoutDepth = FI(1, 2), SetDown = In(0.125),
        DeckEndOffset = deckEnd, SealCount = null, Body = body,
        EndBlock = DECK_END_BLOCKS ? new DeckEndBlockDefinition { Depth = FI(2, 2), FlatLength = dIn < 20 ? FI(2, 10) : FI(3, 4.5) } : null,
    };
}

Joint(units[0].Supports[0], FI(1, 4), 6, 18, true);                  // West Abutment
Joint(units[0].Supports[^1], FI(1, 1.5), 9, 25, true);               // Pier 5WB (Unit 1 end) - owns the body
Joint(units[1].Supports[0], FI(1, 1.5), 9, 25, false);               // Pier 5WB (Unit 2 start)
Joint(units[1].Supports[^1], FI(1, 3.5), 9, 25, true);               // Pier 11WB (Unit 2 end) - owns the body
Joint(units[2].Supports[0], FI(1, 3.5), 9, 25, false);               // Pier 11WB (Unit 3 start)
Joint(units[2].Supports[^1], FI(1, 4), 6, 18, true);                 // East Abutment
// Plan C = 6" at the abutments is the opening from the deck end to the BACKWALL face: BackwallGap = C (default was 1").
foreach (var ab in new[] { units[0].Supports[0], units[2].Supports[^1] })
{
    ab.Substructure = ab.ResolvedSubstructure;
    ab.Substructure.BackwallGap = In(6);
}

// Checks against the plans (printed, nothing is adjusted).
// Opening C between the two deck-end faces = distance between the two support lines measured NORMAL to them (the deck end is
// DeckEndOffset normal to each CL bearing line, see FramingGenerator.DeckEndLine) minus both offsets. Needs the regenerated framing.
void Gap(string pier, BridgeDefinition ba, SupportDefinition a, BridgeDefinition bb, SupportDefinition b, double cIn)
{
    var fa = Doc.Model(ba.Name).Framing.SupportLine(ba.Supports.IndexOf(a));
    var fb = Doc.Model(bb.Name).Framing.SupportLine(bb.Supports.IndexOf(b));
    var n = fa.Direction.Left;
    if (Vec2.Dot(n, fb.AlignmentPoint - fa.AlignmentPoint) < 0) n = -n;
    double between = Vec2.Dot(fb.AlignmentPoint - fa.AlignmentPoint, n);
    double nonPar = Math.Abs(Vec2.Dot(fb.Direction, n));
    double actual = between - a.DeckEndOffset - b.DeckEndOffset;
    Print($"  {pier}: support lines {between / In(1):F2} in apart (normal), model opening between deck ends {actual / In(1):F2} in, plan C = {cIn} in" + (nonPar > 0.001 ? $"   (lines not parallel: {nonPar * 180 / Math.PI * 1:F3} deg)" : "") + (Math.Abs(actual - In(cIn)) > In(1.0 / 16) ? "   <-- CONFLICT (check support lines)" : ""));
}
foreach (var (u, s, name) in new[] { (units[0], units[0].Supports[0], "West Abut"), (units[2], units[2].Supports[^1], "East Abut") })
{
    var sd = s.ResolvedSubstructure;
    Print($"  {name}: backwall front face is {(s.DeckEndOffset + sd.BackwallGap) / In(1):F2} in from CL bearing; plans: {(s.DeckEndOffset + In(6)) / In(1):F2} in (deck end + C)." + (Math.Abs(sd.BackwallGap - In(6)) > In(0.25) ? "   <-- check BackwallGap" : ""));
}
var res = Doc.Regenerate();
Print($"Regenerated {res.Bridges} bridges in {res.Elapsed.TotalSeconds:F1} s, {res.Errors.Count} errors.");
foreach (var e in res.Errors) Print("  ERROR " + e);
Gap("Pier 5WB", units[0], units[0].Supports[^1], units[1], units[1].Supports[0], 9);
Gap("Pier 11WB", units[1], units[1].Supports[^1], units[2], units[2].Supports[0], 9);
foreach (var u in units)
{
    var cm = Doc.Concrete(u.Name);
    foreach (var j in cm.Joints)
        Print($"  {u.Name} / {j.Support} ({j.Definition.TypeName}): blockout {j.BlockoutVolume / 0.764554857984:F2} CY, joint length {j.Length / Ft(1):F1} ft{(j.Body == null ? "" : ", device body " + (j.BodyVolume / 0.764554857984).ToString("F3") + " CY")}");
    foreach (var d in cm.Diagnostics.Where(d => d.Message.Contains("xpansion joint"))) Print("  WARNING " + d.Message);
    foreach (var j in cm.Joints.Where(j => j.EndBlock != null)) Print($"  {u.Name} / {j.Support}: deck end block {j.EndBlock!.Volume / 0.764554857984:F2} CY");
    foreach (var d in cm.Diagnostics.Where(d => d.Message.Contains("Deck end block"))) Print($"  {d.Severity}: {d.Message}");
}
Doc.Save();
Print("Expansion joints set (6 joint ends).");
