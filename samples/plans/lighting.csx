// Lighting items in the contract (I-80 PDF 233-236 = design 226-229; Ramp G PDF 168-170 = design 166-168): pole bases, conduit in the barrier, junction boxes.
// Poles, luminaires, ITS devices and conductors are NOT in the contract: omitted. Data: docs/plan-data/misc-items-data.md (d), misc/lighting-items.csv, misc/pole-base-exit-sign-rebar.csv.
//  * I-80 south barrier = the RIGHT side of the model (south = smaller Y; verified from the girder coordinates). Pole bases (13 1/2" bolt circle, 4 x 1" x 1'-11):
//    Pier 13WB 5'-0 back of CL pier, Piers 14WB and 15WB at the CL pier (Unit 3); the East Abutment base is "10'-0 max from the end of the superstructure": position
//    UNRESOLVED, not placed (bar list scheduled). Ramp G: Pier 8G base at the CL pier on the right barrier (16" circle), Unit 2.
//  * Junction boxes (LI-104 Type 1, size not in the set: placeholder) 2'-6 each side of the CL pier at Piers 13WB-15WB and 8G; which side carries the power / ITS box is not printed.
//  * Conduit (2" rigid steel): ITS 1'-8 above the barrier base, 5" from the traffic face, in the I-80 south (right) barrier and the Ramp G right barrier; power 7" above the base,
//    Ramp G LEFT barrier, full unit lengths; the I-80 power conduit side is not stated (not modeled). Expansion fittings, slab conduit and stub-outs are schematic: not modeled.
// Re-runnable, after the build scripts.
using SBM.Core.Bridge;

double F(int ft, double inch) => Ft(ft) + In(inch);
var u3 = Doc.Project.Bridges.First(b => b.Name == "I-80 WB Unit 3");
var gu2 = Doc.Project.Bridges.First(b => b.Name == "Ramp G Unit 2");
var i80 = Doc.Project.Bridges.Where(b => b.Name.StartsWith("I-80 WB Unit")).ToList();
var rampG = Doc.Project.Bridges.Where(b => b.Name.StartsWith("Ramp G Unit")).ToList();
foreach (var b in i80.Concat(rampG)) { b.Misc ??= new MiscItemsDefinition(); b.Misc.Lighting = null; b.Misc.Unresolved.RemoveAll(s => s.StartsWith("[light]")); }

List<MiscBarRow> PoleBars() => new()
{
    new() { Mark = "6x1", Size = "#6", Count = 4, Length = F(6, 0), Epoxy = false, Location = "base anchor bottom (coating not given)" },
    new() { Mark = "6x2", Size = "#6", Count = 3, Length = F(3, 9), Epoxy = false, Location = "base anchor middle (coating not given)" },
    new() { Mark = "6x3", Size = "#6", Count = 3, Length = F(5, 3), Epoxy = false, Location = "base anchor top (coating not given)" },
    new() { Mark = "5x4", Size = "#5", Count = 3, Length = F(10, 3), Epoxy = false, Location = "base hoops horizontal (coating not given)" },
    new() { Mark = "5x5", Size = "#5", Count = 5, Length = 0, WeightLb = 42, Epoxy = false, Location = "base hoops transverse vertical, varies (leg dims 1'-5 5/8, 1'-7 1/4, 1'-9 3/4, 2'-1 1/8, 2'-3 3/4; length from the printed weight; coating not given)" },
    new() { Mark = "5x11", Size = "#5", Count = 5, Length = F(7, 5), Epoxy = true, Location = "barrier blockout vertical" },
    new() { Mark = "5x10", Size = "#5", Count = 5, Length = F(6, 0), Epoxy = false, Coating = "stainless", Location = "barrier blockout vertical slab" },
};
PoleBaseDefinition Pole(string name, string support, double along, double circle) => new()
{
    Name = name, Support = support, AlongFromSupport = along, Side = LightingSide.Right, BoltCircleDiameter = circle, Bars = PoleBars(),
};
JunctionBoxDefinition Jb(string name, string support, double along) => new() { Name = name, Support = support, AlongFromSupport = along, Side = LightingSide.Right, Kind = JunctionBoxKind.Unresolved };

u3.Misc!.Lighting = new LightingDefinition();
u3.Misc.Lighting.PoleBases.Add(Pole("Pole base Pier 13WB", "Pier 13WB", -Ft(5), In(13.5)));
u3.Misc.Lighting.PoleBases.Add(Pole("Pole base Pier 14WB", "Pier 14WB", 0, In(13.5)));
u3.Misc.Lighting.PoleBases.Add(Pole("Pole base Pier 15WB", "Pier 15WB", 0, In(13.5)));
var eab = Pole("Pole base East Abutment", "East Abut", 0, In(13.5)); eab.AlongFromSupport = null;
u3.Misc.Lighting.PoleBases.Add(eab);
foreach (var p in new[] { "Pier 13WB", "Pier 14WB", "Pier 15WB" })
{
    u3.Misc.Lighting.JunctionBoxes.Add(Jb($"Junction box {p} back", p, -F(2, 6)));
    u3.Misc.Lighting.JunctionBoxes.Add(Jb($"Junction box {p} ahead", p, F(2, 6)));
}
gu2.Misc!.Lighting = new LightingDefinition();
gu2.Misc.Lighting.PoleBases.Add(Pole("Pole base Pier 8G", "Pier 8G", 0, In(16)));
gu2.Misc.Lighting.JunctionBoxes.Add(Jb("Junction box Pier 8G back", "Pier 8G", -F(2, 6)));
gu2.Misc.Lighting.JunctionBoxes.Add(Jb("Junction box Pier 8G ahead", "Pier 8G", F(2, 6)));

// conduit: ITS on the right barrier (I-80 south, Ramp G right), power on the Ramp G left barrier, full unit length
foreach (var b in i80.Concat(rampG))
{
    b.Misc!.Lighting ??= new LightingDefinition();
    var sups = b.Supports; double s0 = sups[0].Station, s1 = sups[^1].Station;
    b.Misc.Lighting.Conduits.Add(new ConduitRunDefinition { Name = $"ITS conduit {b.Name}", Side = LightingSide.Right, Kind = JunctionBoxKind.Its, FromStation = s0, ToStation = s1, HeightAboveBase = F(1, 8) });
    if (b.Name.StartsWith("Ramp G"))
        b.Misc.Lighting.Conduits.Add(new ConduitRunDefinition { Name = $"Power conduit {b.Name}", Side = LightingSide.Left, Kind = JunctionBoxKind.Power, FromStation = s0, ToStation = s1, HeightAboveBase = In(7) });
}
u3.Misc.Unresolved.Add("[light] (I-80 PDF 233-236) I-80 lighting: East Abutment pole base (10'-0 max from the end of the superstructure), the power conduit side, underdeck junction boxes / stub-outs (7583+40.50, 7588+69.64, 7582+45.00 12'-3 LT, 7582+70.00 57'-11 LT), pier-top Type 2 boxes and expansion fittings are schematic or not dimensioned: not modeled; pole base rebar for the abutment base is scheduled only.");
gu2.Misc.Unresolved.Add("[light] (Ramp G PDF 168-170) Ramp G lighting: which of the two junction boxes at Pier 8G is power / ITS is not printed; underdeck boxes and stub-outs (67474+90.40, 67475+53.29, 67477+44.79, 67486+92.70, 67487+45.83) and expansion fittings are schematic: not modeled.");

var res = Doc.Regenerate();
Print($"Regenerated {res.Bridges} bridges in {res.Elapsed.TotalSeconds:F1} s, {res.Errors.Count} errors.");
foreach (var b in new[] { u3, gu2 })
{
    var misc = Doc.Substructure(b.Name).Misc;
    if (misc == null) { Print($"{b.Name}: no misc model"); continue; }
    Print($"{b.Name}: {misc.Assemblies.Count} assemblies, {misc.Elements.Count} elements, pole-base rebar {misc.Marks.Where(m => m.Unit.StartsWith("Pole")).Sum(m => m.TotalMass) / 0.45359237:F0} lb");
    foreach (var d in misc.Diagnostics.Where(d => d.Severity != SBM.Core.Framing.DiagnosticSeverity.Info && (d.Message.Contains("ole") || d.Message.Contains("unction") || d.Message.Contains("onduit") || d.Message.Contains("UNRESOLVED")))) Print($"   {d.Severity}: {d.Message}");
}
Doc.Save();
