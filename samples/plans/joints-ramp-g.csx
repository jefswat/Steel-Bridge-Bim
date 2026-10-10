// Expansion joints of Ramp G (Design 316): MODULAR at Piers 5G, 9G, 14G and 19G; STRIP SEAL at the South Abutment
// (design sheets 148-149 strip seal; 150-151 modular; slab sections 135-136, Unit 4 slab sheet 138). Re-runnable, after build-ramp-g.csx.
//  * C/D as on I-80: C 9" (10" at 14G), D 25"; depth 1'-2; the plans give no blockout width for the strip seal pocket
//    ("4 +/-" flared each side of the gland, part plan on sheet 148): 4" and depth 8 1/16" (end block) are APPROXIMATE.
//  * Strip seal opening at 50 F: end-of-concrete dimension D + 1/2 = 2" + 1/2" = 2 1/2" (sheet 148 section C-C, sheet 135 section A-A).
//  * CL bearing to deck end: 1'-2 1/2 South Abut, 1'-1 1/2 Piers 5G and 9G, 1'-1 Pier 14G, 1'-3 1/2 Pier 19G (sheet 138 Unit 4 slab).
//  * One body per joint: Unit 1 owns 5G, Unit 2 owns 9G, Unit 3 owns 14G; Unit 4 owns 19G (the other side, I-80 Unit 3, has no 19G line in the model).
using SBM.Core.Bridge;

double FI(int ft, double inch) => Ft(ft) + In(inch);
var units = Doc.Project.Bridges.Where(b => b.Name.StartsWith("Ramp G Unit")).OrderBy(b => b.Name).ToList();
if (units.Count != 4) throw new Exception("Expected Ramp G Units 1-4 (run build-ramp-g.csx first).");

void Joint(SupportDefinition s, double deckEnd, double cIn, double dIn, bool body, ExpansionJointType type = ExpansionJointType.Modular, double depth = 0, double setDownIn = 0.125)
{
    s.DeckEndOffset = deckEnd;
    s.ExpansionJoint = new ExpansionJointDefinition
    {
        Type = type, GapAt50F = In(cIn), BlockoutWidth = In(dIn), BlockoutDepth = depth > 0 ? depth : FI(1, 2), SetDown = In(setDownIn),
        DeckEndOffset = deckEnd, SealCount = null, Body = body,
    };
}

Joint(units[0].Supports[0], FI(1, 2.5), 2.5, 4, true, ExpansionJointType.StripSeal, In(8.0625), 0);   // South Abutment (strip seal)
Joint(units[0].Supports[^1], FI(1, 1.5), 9, 25, true);      // Pier 5G
Joint(units[1].Supports[0], FI(1, 1.5), 9, 25, false);
Joint(units[1].Supports[^1], FI(1, 1.5), 9, 25, true);      // Pier 9G
Joint(units[2].Supports[0], FI(1, 1.5), 9, 25, false);
Joint(units[2].Supports[^1], FI(1, 1), 10, 25, true);       // Pier 14G (C = 10", 5" each side)
Joint(units[3].Supports[0], FI(1, 1), 10, 25, false);
Joint(units[3].Supports[^1], FI(1, 3.5), 9, 25, true);      // Pier 19G

void Gap(string pier, SupportDefinition a, SupportDefinition b, double cIn)
{
    double actual = (b.Station - a.Station) - a.DeckEndOffset - b.DeckEndOffset;
    Print($"  {pier}: model opening between deck ends {actual / In(1):F2} in, plan C = {cIn} in" + (Math.Abs(actual - In(cIn)) > In(0.25) ? "   <-- CONFLICT (check support stations)" : ""));
}
Gap("Pier 5G", units[0].Supports[^1], units[1].Supports[0], 9);
Gap("Pier 9G", units[1].Supports[^1], units[2].Supports[0], 9);
Gap("Pier 14G", units[2].Supports[^1], units[3].Supports[0], 10);
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
Print("Expansion joints set (8 joint ends).");
