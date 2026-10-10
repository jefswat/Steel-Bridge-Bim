// Inspection access of Ramp G as designed: inspection cable details (design sheet 132 = PDF 134), inspection walkway details
// (sheets 133-134 = PDF 135-136; Type "B" walkway, same notes as I-80 Unit 3), access plans (sheet 130-131 = PDF 132-133).
// Re-runnable; run after build-ramp-g.csx / cross-frames-ramp-g.csx.
//  * Cables: 1/2" IWRC 6x19 wire rope 4'-6 above the bottom of the web, 3" off the OUTSIDE web face of the exterior girders A and D
//    (sheet 132 note), WT5x44 end anchorages / intermediate supports (6 - 7/8" bolts) and WT5x15 support brackets (4 - 7/8" bolts) at
//    the cross frame stiffeners; 8" from the CL bearing at pier ends, 74'-5 from the CL bearing at the South Abutment only.
//  * Walkways: Type "B" (L8x4x1/2 stringers, L3 1/2 x 3 1/2 x 5/16 diaphragms, 2'-2 clear, no separate toe board) in all three bays
//    A-B, B-C, C-D of all four units, beginning (Unit 1) at the 4th cross frame line = the first line past the abutment frame and the
//    two Type 5 diaphragms (74'-8, which is also where the cable begins) and ending at the end cross frame of the unit.
// Assumed / approximated: as inspection-i80.csx (WT piece lengths, 4" walkway end clearance; no rope hardware, chains, end angles).
// UNCERTAIN: toe boards on Ramp G (sheet 135 section drawing) - taken as none, like the I-80 Type "B" walkway.
using SBM.Core.Bridge;

foreach (var b in Doc.Project.Bridges.Where(b => b.Name.StartsWith("Ramp G Unit ")))
{
    bool first = b.Name.EndsWith("1");
    var ia = new InspectionAccessDefinition { DefaultWalkway = WalkwayType.B };
    ia.Cables.Add(new InspectionCableDefinition { Girder = 0, LeftFace = true, BeginSupport = 0, BeginOffset = first ? Ft(74 + 5 / 12.0) : In(8), EndSupport = -1, EndOffset = -In(8) });
    ia.Cables.Add(new InspectionCableDefinition { Girder = b.Girders.Count - 1, LeftFace = false, BeginSupport = 0, BeginOffset = first ? Ft(74 + 5 / 12.0) : In(8), EndSupport = -1, EndOffset = -In(8) });
    if (first)
        for (int bay = 0; bay < b.Girders.Count - 1; bay++)
            ia.Walkways.Add(new InspectionWalkwayDefinition { Bay = bay, Type = WalkwayType.B, FirstLine = 3 });
    b.InspectionAccess = ia;
}
var r = Doc.Regenerate();
foreach (var b in Doc.Project.Bridges.Where(b => b.InspectionAccess != null && b.Name.StartsWith("Ramp G")))
{
    var sm = Doc.Steel(b.Name);
    var ip = sm.Parts.Where(p => p.Category >= SBM.Core.Steel.PartCategory.InspectionBracket).ToList();
    Print($"{b.Name}: {sm.Assemblies.Count(a => a.Kind == SBM.Core.Steel.AssemblyKind.InspectionCable)} cables, {ip.Count(p => p.Category == SBM.Core.Steel.PartCategory.InspectionBracket)} brackets, " +
          $"{sm.Assemblies.Count(a => a.Kind == SBM.Core.Steel.AssemblyKind.InspectionWalkway)} walkway segments, {ip.Count} parts, {ip.Sum(p => p.Mass) / 907.18474:F1} tons");
    foreach (var d in sm.Diagnostics.Where(d => d.Message.Contains("nspection") || d.Message.Contains("walkway") || d.Message.Contains("cable"))) Print("  " + d.Message);
}
Print($"Regenerated: {r.Parts:N0} parts, {r.Bolts:N0} bolts, {r.Elapsed.TotalSeconds:F1} s.");
Doc.Save();
