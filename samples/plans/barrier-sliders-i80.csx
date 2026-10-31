// I-80 WB barrier cover (slider) plates at the modular joints - run after joints-i80.csx (it is called at the end of it); re-runnable on a built model:
//   sbm run samples\plansarrier-sliders-i80.csx "samples\plans\I-80 WB and Ramp G.sbm"
using SBM.Core;
using SBM.Core.Bridge;
double FI(int ft, double inch) => Ft(ft) + In(inch);
var units = Doc.Project.Bridges.Where(b => b.Name.StartsWith("I-80 WB Unit")).OrderBy(b => b.Name).ToList();
if (units.Count != 3 || units[0].Supports[^1].ExpansionJoint == null) throw new Exception("Run joints-i80.csx first.");
// Barrier cover (slider) plates (agent review I07, sheet 203 = PDF 210 'Modular expansion joint details'): 3/8 in A36 plate bent to the barrier, 1/4 in below its faces
// (Section B-B), down to 1/4 in above the deck / joint top on both faces; fixed (countersunk cap screws) on the oncoming-traffic (east) side, sliding in a recess on the
// other. Lengths per the expansion device plans, taken along the barrier: Pier 6A drawing (Pier 5WB and the East Abutment 'similar'): 4'-0 with 1'-6 beyond the far
// (east) barrier end; Pier 11WB: 5'-0 with 2'-2 (north) / 1'-8 (south) beyond the east deck end. Opening between the barrier ends = the joint C (9 in piers, 6 in abutments).
// West Abutment (4'-10 / 3'-8 / 1'-2 south, 2'-4 / 2'-6 north) not entered: the dimensions run to the backwall face and the end of barrier rail on different sides
// and do not give one plate split - UNRESOLVED. Ramp G (sheet 148) not entered here.
string slFlag(string where, string extra) => UnresolvedFlag.Text("sheet 203 (PDF 210)", $"barrier slider plates at {where}", "the plate lengths along the barrier are dimensioned on the expansion device plans, but the recess in the concrete, the cap screws / bent bolts and the plate bend radii are not modeled" + extra,
    "plate on every barrier face except the base, outer face 1/4 in inside the concrete faces, 1/4 in above the deck on both faces; fasteners and recess not built.");
JointBarrierPlateDefinition Slider(double totalFt, double beyondIn, double cIn, double? rightBeyondIn, string where, string extra) => new()
{
    LengthOut = In(cIn) + In(beyondIn), LengthIn = Ft(totalFt) - In(cIn) - In(beyondIn),
    RightLengthOut = rightBeyondIn is double rb ? In(cIn) + In(rb) : null, RightLengthIn = rightBeyondIn is double rb2 ? Ft(totalFt) - In(cIn) - In(rb2) : null,
    Source = "sheet 203 (PDF 210)", Unresolved = slFlag(where, extra),
};
units[0].Supports[^1].ExpansionJoint!.BarrierPlate = Slider(4, 18, 9, null, "Pier 5WB", "; Pier 5WB is 'similar' to the Pier 6A drawing (north barrier shown), so the south plate takes the same split");
units[1].Supports[^1].ExpansionJoint!.BarrierPlate = Slider(5, 26, 9, 20, "Pier 11WB", "");
units[2].Supports[^1].ExpansionJoint!.BarrierPlate = Slider(4, 18, 6, null, "the East Abutment", "; the East Abutment is 'similar' to the Pier 6A drawing (beyond = past the backwall face)");
var res = Doc.Regenerate();
foreach (var u in units)
    foreach (var p in Doc.Model(u.Name).Steel?.Parts.Where(p => p.Category == SBM.Core.Steel.PartCategory.JointCoverPlate && p.Mark.Contains("-BP")) ?? Enumerable.Empty<SBM.Core.Steel.SteelPart>())
        Print($"  {u.Name}: {p.Mark} {p.Description}, {p.Mass / 0.45359237:0} lb");
foreach (var w in res.Warnings.Where(w => w.Contains("slider") || w.Contains("barrier cover")).Distinct().Take(6)) Print("  warning " + w);
Doc.Save();
