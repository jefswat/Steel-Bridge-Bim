// Inspection access of WB I-80 as designed: inspection cable details (design sheets 163-164 = PDF 167-168), inspection
// walkway details (sheets 165-170 = PDF 169-174), access plans (sheets 159-162 = PDF 163-166). Re-runnable; run after
// build-i80-wb.csx / cross-frames-i80.csx / cross-girders-i80.csx.
//  * Cables: 1/2" IWRC 6x19 galvanized wire rope 4'-6 above the bottom of the web, 3" off the OUTSIDE face of the exterior girders
//    (A and the last girder of each unit: F / K / J), on WT5x44 end anchorages and intermediate supports (6 - 7/8" bolts; sections
//    between 45'-0 and 200'-0) and WT5x15 support brackets (4 - 7/8" bolts) at the cross frame stiffeners. Begin / end 8" from the
//    CL bearing of the unit's first / last support. Rope hardware (thimbles, clips, shackles, turnbuckles) is not modeled.
//  * Walkways: Type "A" (Units 1 and 2: C7 stringers by span, PL 3/8 x 5 diaphragms, 2'-1 clear) and Type "B" (Unit 3: L8x4x1/2
//    stringers, L3 1/2 x 3 1/2 x 5/16 diaphragms, 2'-2 clear) in every bay, on chords between the cross frame lines, 2 1/2" above
//    the bottom of the web; posts equally spaced at 8'-6 max; top rail L3x3x5/16 at 3'-6, intermediate rail PL 3/8 x 4 at 2'-0 (all
//    above the grating), toe board PL 3/8 x 4 (Type A). Segments end 1" clear of the cross girder webs.
// Assumed / approximated (not dimensioned or not resolvable on the sheets): Unit 1 walkway starts (A-B, B-D, D-F at three successive
//    lines, taken as the 3rd / 4th / 5th cross frame line of each bay); WT piece lengths (9" anchorage, 7" bracket), 4" end clearance of
//    each walkway segment to the cross frame line; the cables between girders at the cross girders and the 2'-8 / 1'-0 offsets near
//    the sign supports of Unit 2 (sheet 161) are not placed; chains, bent end angles and bolts of the walkway ends are not modeled.
using SBM.Core.Bridge;

foreach (var b in Doc.Project.Bridges.Where(b => b.Name.StartsWith("I-80 WB Unit ")))
{
    bool unit3 = b.Name.EndsWith("3");
    var ia = new InspectionAccessDefinition
    {
        DefaultWalkway = unit3 ? WalkwayType.B : WalkwayType.A,
        AnchorBracket = "WT5x44", SupportBracket = "WT5x15",
    };
    int last = b.Girders.Count - 1;
    // As designed (2026-10-12, clash check): walkway supports, ends and rails from the walkway details, rope holes / ends from the cable details.
    ia.SupportOnFrames = true; ia.Source = "I-80 PDF 167-174";
    //  stringers bear on the cross frame bottom strut on 1/2" bent plates, end 1 1/2 +- beyond it, gratings 1" apart over the strut, rails /
    //  posts 1 1/2 clear of the diagonals (PDF 170 Sections G-G, partial section near cross frame; PDF 173 Section T-T, Type B)
    ia.StringerEndFromFrame = In(1.5); ia.GratingGapAtFrame = In(0.5); ia.RailClearOfFrame = In(1.5);
    //  plate diaphragms: top of grating 1'-4 (Type 7, PDF 169 partial section near diaphragm Type 7) / 6" (Type 8, PDF 174) over the top
    //  of the diaphragm bottom flange at CL walkway; stringers 1 1/2 +- from the diaphragm web (Sections E-E, S-S)
    ia.GratingAboveDiaphragmFlange = new() { ["7"] = In(16), ["8"] = In(6) };
    ia.StringerEndFromWeb = In(1.5); ia.GratingClearOfWeb = In(1);   // cross girder: K-K / O-O "1 1/2 +-", "1" CL. END OF WALKWAY GRATING"
    foreach (var sec in new[] { ia.TypeA, ia.TypeB })
    {
        sec.GratingWidth = In(24);                 // typical sections: WALKWAY GRATING (LEVEL) (2'-0) (PDF 169)
        sec.TopRailInside = true;                  // typical sections: top rail on the post top, vertical leg inside the posts
        sec.RailEndFromCrossGirderWeb = In(30);    // 2'-6 (BOTH SIDES) cross girder web to the end post (PDF 171 K-K, PDF 172 O-O / R-R)
    }
    ia.TypeA.GratingAboveCrossGirderWebBottom = In(12);    // PDF 171 Section A-A: 1'-0 (TYP.) top of grating over the cross girder bottom flange
    ia.TypeB.GratingAboveCrossGirderWebBottom = In(2.5);   // PDF 172 Sections C-C / D-D: 2 1/2" top of walkway grating to bottom of web
    ia.TypeB.EndDiaphragmClearOfCrossGirderFlange = In(1.5);   // PDF 172 Section O-O: L3 1/2 end diaphragm beside the bottom flange, "1 1/2 +-"
    //  rope: 7/8" hole in the stiffeners it passes, 1 1/2" at Piers 12WB-15WB (PDF 167 Section C-C); rope ends at the anchorage shackle;
    //  support brackets only at cross frame stiffeners on the OTHER side of the web (PDF 167 support bracket detail), intermediate
    //  supports at 1/2 the stiffener spacing (typical cable elevation)
    ia.StiffenerRopeHole = In(0.875);
    ia.StiffenerRopeHoleAtSupports = new() { ["Pier 12WB"] = In(1.5), ["Pier 13WB"] = In(1.5), ["Pier 14WB"] = In(1.5), ["Pier 15WB"] = In(1.5) };
    ia.RopeEndsAtAnchorStem = true;
    ia.Cables.Add(new InspectionCableDefinition { Girder = 0, LeftFace = true, BeginSupport = 0, BeginOffset = In(8), EndSupport = -1, EndOffset = -In(8) });
    ia.Cables.Add(new InspectionCableDefinition { Girder = last, LeftFace = false, BeginSupport = 0, BeginOffset = In(8), EndSupport = -1, EndOffset = -In(8) });
    if (b.Name.EndsWith("1"))
    {
        // Unit 1: walkways begin after the abutment diaphragm lines: A-B first, B-D next, D-F next (sheet 159).
        ia.Walkways.Add(new InspectionWalkwayDefinition { Bay = 0, Type = WalkwayType.A, FirstLine = 2 });
        ia.Walkways.Add(new InspectionWalkwayDefinition { Bay = 1, Type = WalkwayType.A, FirstLine = 3 });
        ia.Walkways.Add(new InspectionWalkwayDefinition { Bay = 2, Type = WalkwayType.A, FirstLine = 3 });
        ia.Walkways.Add(new InspectionWalkwayDefinition { Bay = 3, Type = WalkwayType.A, FirstLine = 4 });
        ia.Walkways.Add(new InspectionWalkwayDefinition { Bay = 4, Type = WalkwayType.A, FirstLine = 4 });
    }
    b.InspectionAccess = ia;
}
var r = Doc.Regenerate();
foreach (var b in Doc.Project.Bridges.Where(b => b.InspectionAccess != null))
{
    var sm = Doc.Steel(b.Name);
    var ac = sm.Assemblies.Where(a => a.Kind == SBM.Core.Steel.AssemblyKind.InspectionCable).ToList();
    var aw = sm.Assemblies.Where(a => a.Kind == SBM.Core.Steel.AssemblyKind.InspectionWalkway).ToList();
    var ip = sm.Parts.Where(p => p.Category >= SBM.Core.Steel.PartCategory.InspectionBracket).ToList();
    Print($"{b.Name}: {ac.Count} cables, {sm.Parts.Count(p => p.Category == SBM.Core.Steel.PartCategory.InspectionBracket)} brackets, {aw.Count} walkway segments, {ip.Count} parts, {ip.Sum(p => p.Mass) / 907.18474:F1} tons");
    foreach (var d in sm.Diagnostics.Where(d => d.Message.Contains("nspection") || d.Message.Contains("walkway") || d.Message.Contains("cable"))) Print("  " + d.Message);
}
Print($"Regenerated: {r.Parts:N0} parts, {r.Bolts:N0} bolts, {r.Elapsed.TotalSeconds:F1} s.");
Doc.Save();
