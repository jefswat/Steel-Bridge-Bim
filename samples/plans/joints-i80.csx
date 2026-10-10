// Expansion joints of I-80 WB (Design 116): MODULAR joints at both abutments and at Piers 5WB, 11WB (design sheets 202-203,
// slab sections on 178, 183, 188). Run after build-i80-wb.csx (re-runnable; sets the deck-end offsets of the plans).
//  * "C" = opening between the deck-end faces at 50 F (9" piers = 4 1/2" each side of the CL pier; 6" abutments to the backwall face).
//  * "D" = blockout width from the deck-end face, perpendicular to the joint (25" piers, 18" abutments); depth 1'-2; device 1/8" below top of slab.
//  * CL bearing to deck end: 1'-1 1/2 Pier 5WB (both units), 1'-3 1/2 Pier 11WB (both units), 1'-4 abutments.
//  * One device body per joint (spans the whole opening): Unit 1 owns 5WB, Unit 2 owns 11WB. The joint line is the deck-end line (parallel to the support line).
//  * NOT modeled: the Pier 6A joint (a separate line beside 5WB for girders A-E; the model has one straight deck end per unit),
//    the Pier 19G joint on this side (Ramp G Unit 4 owns it), barrier slider plates, the manufacturer's beams/support boxes/anchors.
using SBM.Core.Bridge;

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
    };
}

Joint(units[0].Supports[0], FI(1, 4), 6, 18, true);                  // West Abutment
Joint(units[0].Supports[^1], FI(1, 1.5), 9, 25, true);               // Pier 5WB (Unit 1 end) - owns the body
Joint(units[1].Supports[0], FI(1, 1.5), 9, 25, false);               // Pier 5WB (Unit 2 start)
Joint(units[1].Supports[^1], FI(1, 3.5), 9, 25, true);               // Pier 11WB (Unit 2 end) - owns the body
Joint(units[2].Supports[0], FI(1, 3.5), 9, 25, false);               // Pier 11WB (Unit 3 start)
Joint(units[2].Supports[^1], FI(1, 4), 6, 18, true);                 // East Abutment

// Checks against the plans (printed, nothing is adjusted).
void Gap(string pier, SupportDefinition a, SupportDefinition b, double cIn)
{
    double actual = (b.Station - a.Station) - a.DeckEndOffset - b.DeckEndOffset;   // along the alignment at the CL
    Print($"  {pier}: model opening between deck ends {actual / In(1):F2} in, plan C = {cIn} in" + (Math.Abs(actual - In(cIn)) > In(0.25) ? "   <-- CONFLICT (check support stations)" : ""));
}
Gap("Pier 5WB", units[0].Supports[^1], units[1].Supports[0], 9);
Gap("Pier 11WB", units[1].Supports[^1], units[2].Supports[0], 9);
foreach (var (u, s, name) in new[] { (units[0], units[0].Supports[0], "West Abut"), (units[2], units[2].Supports[^1], "East Abut") })
{
    var sd = s.ResolvedSubstructure;
    Print($"  {name}: backwall front face is {(s.DeckEndOffset + sd.BackwallGap) / In(1):F2} in from CL bearing; plans: {(s.DeckEndOffset + In(6)) / In(1):F2} in (deck end + C)." + (Math.Abs(sd.BackwallGap - In(6)) > In(0.25) ? "   <-- check BackwallGap" : ""));
}
var res = Doc.Regenerate();
Print($"Regenerated {res.Bridges} bridges in {res.Elapsed.TotalSeconds:F1} s, {res.Errors.Count} errors.");
foreach (var e in res.Errors) Print("  ERROR " + e);
foreach (var u in units)
{
    var cm = Doc.Concrete(u.Name);
    foreach (var j in cm.Joints)
        Print($"  {u.Name} / {j.Support} ({j.Definition.TypeName}): blockout {j.BlockoutVolume / 0.764554857984:F2} CY, joint length {j.Length / Ft(1):F1} ft{(j.Body == null ? "" : ", device body " + (j.BodyVolume / 0.764554857984).ToString("F3") + " CY")}");
    foreach (var d in cm.Diagnostics.Where(d => d.Message.Contains("xpansion joint"))) Print("  WARNING " + d.Message);
}
Doc.Save();
Print("Expansion joints set (6 joint ends).");
