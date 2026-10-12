// Dapped steel girder ends at the expansion joints (OPTIONAL SupportDefinition.GirderEndDap). Re-runnable; run after the build, joint and girder scripts.
// Plan basis (raster-checked 400-800 dpi):
//  * I-80 Design 116, design sheet 104 (PDF 106): DETAIL A (top flange bent down 7 1/2 over a 7 1/2 run, R = 6 bends, lowered from the girder end
//    to 1'-2 past the CL bearing; CL bearing 1'-0 from the end) - called up at the West Abutment (PDF 103) and East Abutment (PDF 118);
//    DETAIL B = the same flange drop + a web end cope 6" deep (10" end tabs at both flanges, 6" transition, R = 6) - called up ("Detail B" /
//    "Detail B similar") at Pier 5WB (PDF 106, 108), Pier 6A (PDF 108 "Pier 5WB or 6A"), Pier 11WB (PDF 111-115, 119).
//    Modular joint sections (PDF 209) show the lowered flange under the joint support box / blockout.
//  * Ramp G Design 316, design sheet 91 (PDF 93): DETAIL A = the I-80 Detail B (flange drop + 6" web cope), called up at Piers 5G, 9G, 14G, 19G
//    (PDF 93, 95, 98, 99, 102). South Abutment (strip seal, PDF 92): plain girder end - no dap.
using SBM.Core.Bridge;

double F(double ft, double inch = 0) => Ft(ft) + In(inch);
GirderEndDapDefinition Dap(bool cope, string src) => new()
{
    FlangeDrop = In(7.5), DropRun = In(7.5), LowFlatFromBearing = F(1, 2), BendRadius = In(6),
    CopeInset = cope ? In(6) : 0, CopeEdge = In(10), CopeRun = In(6), CopeRadius = In(6), Source = src,
};
SupportDefinition Sup(string bridge, string name) =>
    Doc.Project.Bridges.First(b => b.Name == bridge) is var b ? (b.Supports.FirstOrDefault(s => s.Name == name) ?? b.AuxiliarySupports.First(s => s.Name == name)) : null!;

foreach (var b in Doc.Project.Bridges) { foreach (var s in b.Supports) s.GirderEndDap = null; foreach (var s in b.AuxiliarySupports) s.GirderEndDap = null; }

const string iA = "I-80 PDF 106 Detail A", iB = "I-80 PDF 106 Detail B", gA = "Ramp G PDF 93 Detail A";
Sup("I-80 WB Unit 1", "West Abut").GirderEndDap = Dap(false, iA);
Sup("I-80 WB Unit 1", "Pier 5WB").GirderEndDap = Dap(true, iB);
Sup("I-80 WB Unit 2", "Pier 5WB").GirderEndDap = Dap(true, iB);
Sup("I-80 WB Unit 2", "Pier 6A").GirderEndDap = Dap(true, iB);
Sup("I-80 WB Unit 2", "Pier 11WB").GirderEndDap = Dap(true, iB);
Sup("I-80 WB Unit 3", "Pier 11WB").GirderEndDap = Dap(true, iB);
Sup("I-80 WB Unit 3", "East Abut").GirderEndDap = Dap(false, iA);
Sup("Ramp G Unit 1", "Pier 5G").GirderEndDap = Dap(true, gA);
Sup("Ramp G Unit 2", "Pier 5G").GirderEndDap = Dap(true, gA);
Sup("Ramp G Unit 2", "Pier 9G").GirderEndDap = Dap(true, gA);
Sup("Ramp G Unit 3", "Pier 9G").GirderEndDap = Dap(true, gA);
Sup("Ramp G Unit 3", "Pier 14G").GirderEndDap = Dap(true, gA);
Sup("Ramp G Unit 4", "Pier 14G").GirderEndDap = Dap(true, gA);
Sup("Ramp G Unit 4", "Pier 19G").GirderEndDap = Dap(true, gA);

var res = Doc.Regenerate();
Print($"Regenerated {res.Bridges} bridges in {res.Elapsed.TotalSeconds:F1} s, {res.Errors.Count} errors.");
foreach (var b in Doc.Project.Bridges)
{
    var st = Doc.Steel(b.Name);
    foreach (var d in st.Diagnostics.Where(d => d.Message.Contains("dap"))) Print($"  {b.Name}: {d.Severity}: {d.Message}");
    // Clearance of the lowered flange top to the joint blockout bottom at each dapped CL bearing (plan: blockout 1'-2 below the top of slab).
    var f = Doc.Framing(b.Name);
    for (int j = 0; j < b.Supports.Count + b.AuxiliarySupports.Count; j++)
    {
        var sd = b.SupportAt(j);
        if (sd.GirderEndDap is not { } dd) continue;
        var ej = (j == 0 || j == b.Supports.Count - 1) ? sd.ExpansionJoint : b.Supports[0].ExpansionJoint;
        double bo = ej?.BlockoutDepth ?? F(1, 2);
        var vals = f.Bearings.Where(br => br.Support == j).Select(br => (br.DeckElevation - bo) - (br.TopOfSteel.Z - dd.FlangeDrop)).ToList();
        if (vals.Count == 0) continue;
        // the same from the generated top flange plates: highest flange vertex within 1'-1 (plan) of each girder end on this line
        var meshVals = new List<double>();
        foreach (var br in f.Bearings.Where(br => br.Support == j))
        {
            var g = f.Girders[br.Girder];
            var bp = new SBM.Core.Geometry.Vec2(br.TopOfSteel.X, br.TopOfSteel.Y);
            var endPt = (g.StartEnd.XY - bp).Length < (g.EndEnd.XY - bp).Length ? g.StartEnd : g.EndEnd;
            var zs = st.Parts.Where(p => p.Category == SBM.Core.Steel.PartCategory.FlangePlate && p.Mark.StartsWith(g.Name + "-") && p.Mark.Contains("-TF"))
                .SelectMany(p => p.Mesh.Positions).Where(q => (q.XY - endPt.XY).Length < 0.35).Select(q => q.Z).ToList();
            if (zs.Count > 0) meshVals.Add((br.DeckElevation - bo) - zs.Max());
        }
        Print($"  {b.Name} {sd.Name}: lowered top of flange below the blockout bottom by {vals.Min() / 0.0254:F2}\" to {vals.Max() / 0.0254:F2}\" ({vals.Count} bearings; without the dap {(vals.Min() - dd.FlangeDrop) / 0.0254:F2}\" to {(vals.Max() - dd.FlangeDrop) / 0.0254:F2}\")"
            + (meshVals.Count > 0 ? $"; generated flange plates at the girder ends: {meshVals.Min() / 0.0254:F2}\" to {meshVals.Max() / 0.0254:F2}\" below" : ""));
    }
}
Doc.Save();
