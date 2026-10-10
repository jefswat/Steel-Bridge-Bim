// Bearings per the plan bearing data tables: I-80 WB Design 116 sheets 156-158, Ramp G Design 316 sheets 84-87
// (PDF pages 129-131). The disc bearings are designed by the manufacturer, so they are modeled GENERIC: the beveled sole
// plate (1 1/4" min., 2" wider than the flange), one bearing body sized so the assembly is the table's "bearing height"
// (assumed total height incl. the 1/8" preformed masonry pad, which set the pedestal elevations), a masonry plate whose
// size follows the anchor bolt layout (3" beyond the bolts, 5" at fixed bearings) and the anchor bolts as tabulated
// (4 per bearing; X / Y from the CL bearing; size and minimum embedment). Bearing type per girder: MD = non-guided
// expansion, GE = guided expansion, FX = fixed.
// Assumed (vendor items, not in the plans): bearing body 16" x 16" in plan, masonry plate 1 3/4" thick.
// Re-runnable; run after the build / girder / pier scripts.
using SBM.Core.Bridge;

double I(double inch) => In(inch);
// types: one letter per girder index (M = MD, G = GE, F = FX, '.' = no bearing there).
record Row(string Bridge, string Support, string Types, double HeightIn, double X, double Y, double BoltIn, double EmbedFt);
var rows = new List<Row>
{
    // I-80 WB Unit 1 (sheet 156)
    new("I-80 WB Unit 1", "West Abut", "MGGGGM", 9, 15, 6, 1.5, 1.5),
    new("I-80 WB Unit 1", "Pier 1WB", "MGGGGM", 9, 22, 9, 1.5, 1.5),
    new("I-80 WB Unit 1", "Pier 2WB", "MGGGGM", 9, 22, 9, 1.5, 1.5),
    new("I-80 WB Unit 1", "Pier 3WB", "MFFFFM", 9, 22, 9, 2.5, 2.5),
    new("I-80 WB Unit 1", "Pier 4WB", "MGGGGM", 9, 22, 9, 1.5, 1.5),
    new("I-80 WB Unit 1", "Pier 5WB", "MGGGGM", 9, 15, 6, 1.5, 1.5),
    // I-80 WB Unit 2 (sheet 157); 5WB / 6A ahead: A-E on Pier 6A (9"), F-K on Pier 5WB (9 5/8")
    new("I-80 WB Unit 2", "Pier 6A", "MMMGG......", 9, 15, 6, 1.5, 1.5),
    new("I-80 WB Unit 2", "Pier 5WB", ".....GGGMMM", 9.625, 15, 6, 1.5, 1.5),
    new("I-80 WB Unit 2", "Pier 6WB", "MMMGGGGGGMM", 9, 22, 9, 1.5, 1.5),
    new("I-80 WB Unit 2", "Pier 7WB", "MMM.FFFFFMM", 9, 22, 9, 2.5, 2.5),
    new("I-80 WB Unit 2", "Pier 8WB", "MMM..GGGGGM", 9, 22, 9, 1.5, 1.5),
    new("I-80 WB Unit 2", "Pier 9WB", "MM...GGGGGM", 9, 22, 9, 1.5, 1.5),
    new("I-80 WB Unit 2", "Pier 10WB", "M....GGGGGM", 9, 22, 9, 1.5, 1.5),
    new("I-80 WB Unit 2", "Pier 11WB", "M....GGGGGM", 9, 15, 6, 1.5, 1.5),
    // I-80 WB Unit 3 (sheet 158)
    new("I-80 WB Unit 3", "Pier 11WB", "MMGGGGGMMM", 9, 15, 6, 1.5, 1.5),
    new("I-80 WB Unit 3", "Pier 12WB", "MMMGGGGGMM", 9, 22, 9, 1.5, 1.5),
    new("I-80 WB Unit 3", "Pier 13WB", "M.MFFFFFMM", 9, 22, 9, 2.5, 2.5),
    new("I-80 WB Unit 3", "Pier 14WB", "M..MGGGGGM", 9, 22, 9, 1.5, 1.5),
    new("I-80 WB Unit 3", "Pier 15WB", "M..MGGGGGM", 9, 22, 9, 1.5, 1.5),
    new("I-80 WB Unit 3", "East Abut", "M..MGGGGGM", 9, 15, 6, 1.5, 1.5),
    // Ramp G Units 1 & 2 (sheet 85)
    new("Ramp G Unit 1", "South Abut", "GGGG", 9, 15, 6, 1.5, 1.5),
    new("Ramp G Unit 1", "Pier 1G", "GGGG", 9, 22, 9, 1.5, 1.5),
    new("Ramp G Unit 1", "Pier 2G", "FFFF", 9, 22, 9, 2.5, 2.5),
    new("Ramp G Unit 1", "Pier 3G", "GGGG", 9, 22, 9, 1.5, 1.5),
    new("Ramp G Unit 1", "Pier 4G", "GGGG", 9, 22, 9, 1.5, 1.5),
    new("Ramp G Unit 1", "Pier 5G", "GGGG", 9, 15, 6, 1.5, 1.5),
    new("Ramp G Unit 2", "Pier 5G", "GGGG", 10.1875, 15, 6, 1.5, 1.5),
    new("Ramp G Unit 2", "Pier 6G", "GGGG", 9, 22, 9, 1.5, 1.5),
    new("Ramp G Unit 2", "Pier 7G", "FFFF", 9, 22, 9, 2.5, 2.5),
    new("Ramp G Unit 2", "Pier 8G", "GGGG", 9, 22, 9, 1.5, 1.5),
    new("Ramp G Unit 2", "Pier 9G", "GGGG", 9, 15, 6, 1.5, 1.5),
    // Ramp G Units 3 & 4 (sheet 86)
    new("Ramp G Unit 3", "Pier 9G", "GGGG", 9, 15, 6, 1.5, 1.5),
    new("Ramp G Unit 3", "Pier 10G", "GGGG", 9, 22, 9, 1.5, 1.5),
    new("Ramp G Unit 3", "Pier 11G", "FFFF", 9, 22, 9, 2.5, 2.5),
    new("Ramp G Unit 3", "Pier 12G", "FFFF", 9, 22, 9, 2.5, 2.5),
    new("Ramp G Unit 3", "Pier 13G", "GGGG", 9, 22, 9, 1.5, 1.5),
    new("Ramp G Unit 3", "Pier 14G", "GGGG", 9.375, 15, 6, 1.5, 1.5),
    new("Ramp G Unit 4", "Pier 14G", "GGGG", 9, 15, 6, 1.5, 1.5),
    new("Ramp G Unit 4", "Pier 15G", "GGGG", 9, 22, 9, 1.5, 1.5),
    new("Ramp G Unit 4", "Pier 16G", "GGGG", 9, 22, 9, 1.5, 2.5),
    new("Ramp G Unit 4", "Pier 17G", "GGGG", 9, 22, 9, 1.5, 2.5),
    new("Ramp G Unit 4", "Pier 18G", "FFFF", 9, 22, 9, 2.5, 2.5),
    new("Ramp G Unit 4", "Pier 19G", "GGGG", 9, 15, 6, 1.5, 2.5),
};

BearingFixity Fx(char c) => c switch { 'F' => BearingFixity.Fixed, 'G' => BearingFixity.GuidedExpansion, 'M' => BearingFixity.Expansion, _ => BearingFixity.Auto };
foreach (var b in Doc.Project.Bridges.Where(x => rows.Any(r => r.Bridge == x.Name)))
{
    // Bridge default = the generic bearing (each support below gets its own copy with the tabulated anchors / height).
    var bd = b.Bearings;
    bd.Type = BearingType.Generic;
    bd.SolePlateMinThickness = I(1.25); bd.SolePlateProjection = I(1);
    bd.GenericBodyLength = I(16); bd.GenericBodyWidth = I(16); bd.MasonryPlateThickness = I(1.75);
    bd.MasonryPadThickness = I(0.125);
    bd.AnchorBoltCount = 4; bd.AnchorEdgeDistance = I(3); bd.AnchorEdgeDistanceFixed = I(5);
    bd.AnchorHoleOversize = I(0.25);
    int n = b.Supports.Count + b.AuxiliarySupports.Count;
    for (int j = 0; j < n; j++)
    {
        var sup = b.SupportAt(j);
        var r = rows.FirstOrDefault(x => x.Bridge == b.Name && x.Support == sup.Name);
        if (r == null) { Print($"  {b.Name} {sup.Name}: no bearing table row (kept the bridge default)"); continue; }
        var sb = bd.Clone();
        sb.GenericHeight = I(r.HeightIn);
        sb.AnchorSpacingAcross = I(2 * r.X); sb.AnchorSpacingAlong = I(2 * r.Y);
        sb.AnchorBoltDiameter = I(r.BoltIn); sb.AnchorBoltEmbedment = Ft(r.EmbedFt);
        sb.AnchorBoltGrade = r.BoltIn > 2 ? "F1554 Gr. 55" : "F1554 Gr. 36";
        sup.Bearings = sb;
        var fx = r.Types.Select(Fx).ToList();
        // The support's own fixity = its main type (GE / FX); MD girders are per-girder exceptions.
        sup.Fixity = fx.Contains(BearingFixity.Fixed) ? BearingFixity.Fixed : fx.Contains(BearingFixity.GuidedExpansion) ? BearingFixity.GuidedExpansion : BearingFixity.Expansion;
        sup.GirderFixities = fx;
        if (r.Types.Length != b.Girders.Count) Print($"  WARNING {b.Name} {sup.Name}: {r.Types.Length} girder types for {b.Girders.Count} girders");
    }
}

var res = Doc.Regenerate();
Print($"Regenerated {res.Bridges} bridges in {res.Elapsed.TotalSeconds:F1} s, {res.Errors.Count} errors.");
foreach (var e in res.Errors) Print("  ERROR " + e);
// Seats vs the table: top of bearing (bottom of flange at CL bearing) - bearing height.
foreach (var b in Doc.Project.Bridges.Where(x => rows.Any(r => r.Bridge == x.Name)))
{
    var md = Doc.Model(b.Name);
    foreach (var u in md.Substructure?.Units ?? new())
        if (u.Pedestals.Count > 0)
            Print($"  {b.Name} {u.Name}: pedestals {u.Pedestals.Min(p => p.Height) / In(1):F1} to {u.Pedestals.Max(p => p.Height) / In(1):F1} in");
    foreach (var d in md.Steel!.Diagnostics.Where(d => d.Message.StartsWith("B-")))
        Print("  WARNING " + d.Message);
}
Doc.Save();
