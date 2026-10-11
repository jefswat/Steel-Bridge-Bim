// Ramp G (Design 316) deck drainage, design sheets 152-159 (PDF 154-161): 25 trench inlets (drain table, PDF 160), the 12" span 19G collection pipe,
// and the deck-joint collection systems at Piers 5G, 9G, 14G, 19G. Data and sources: docs/plan-data/misc-items-data.md (a1-a4), misc/*.csv.
// Re-runnable, after build-ramp-g.csx, deck-edge-strip.csx (gutter line = deck edge - 1'-7) and the pier / joint scripts. OPTIONAL: nothing here exists without it.
//  * Each inlet is positioned FROM THE GUTTER LINE exactly as dimensioned (trench from 1/2" clear of the gutter line, tube centre 3'-0 1/2 from it).
//  * Free fall (21 drains) with a 10' x 10' x 2' splash basin where the table says YES (19); piped (22-25, left side) to the 12" collection pipe,
//    the tube 1'-8 from CL girder A (only CHECKED against the model, the drain is never moved).
//  * Collection pipe: vertical dims from the deck top at CL pipe (PDF 159 table; the cross frame rows have no station and are not used), 1.78%.
//  * Pier systems: 5G / 9G / 14G collect at the right (girder D side), 19G at the left (girder A side).
//  * UNRESOLVED items are reported by the generator (warnings), nothing is guessed.
using SBM.Core.Bridge;

double G = Ft(1) + In(7);                       // gutter line from the deck edge (the 1'-7 level strip)
// unit, row, location text, station, cross slope %, left?, piped?, basin?
var rows = new (int unit, int n, string loc, string sta, double slope, bool left, bool piped, bool basin)[]
{
    (1, 1, "South Abut. ahead", "67466+32.70", 2.00, true, false, false),
    (1, 2, "South Abut. ahead", "67466+37.70", 2.00, true, false, false),
    (1, 3, "Pier 1G ahead", "67467+87.70", 2.00, true, false, true),
    (1, 4, "Pier 2G ahead", "67469+77.70", 1.67, false, false, true),
    (1, 5, "Pier 2G ahead", "67469+87.70", 2.02, false, false, true),
    (1, 6, "Pier 3G ahead", "67471+67.70", 5.90, false, false, true),
    (1, 7, "Pier 4G back", "67473+17.70", 5.90, false, false, true),
    (2, 8, "Pier 5G ahead", "67475+02.70", 5.90, false, false, true),
    (2, 9, "Pier 6G back", "67476+22.70", 5.90, false, false, true),
    (2, 10, "Pier 6G ahead", "67476+82.70", 5.90, false, false, true),
    (2, 11, "Pier 7G back", "67478+55.70", 5.90, false, false, true),
    (2, 12, "Pier 8G back", "67480+27.70", 5.90, false, false, true),
    (3, 13, "Pier 9G ahead", "67482+67.70", 5.90, false, false, true),
    (3, 14, "Pier 10G back", "67484+17.70", 5.90, false, false, true),
    (3, 15, "Pier 11G ahead", "67487+12.70", 5.90, false, false, true),
    (3, 16, "Pier 12G back", "67488+92.70", 5.90, false, false, true),
    (3, 17, "Pier 13G back", "67491+17.70", 5.90, false, false, true),
    (3, 18, "Pier 14G back", "67492+97.70", 5.90, false, false, true),
    (4, 19, "Pier 15G back", "67494+61.70", 5.16, false, false, true),
    (4, 20, "Pier 15G ahead", "67495+62.70", 1.76, false, false, true),
    (4, 21, "Pier 15G ahead", "67495+87.70", 0.91, false, false, true),
    (4, 22, "Pier 18G ahead", "67501+50.70", 5.90, true, true, false),
    (4, 23, "Pier 18G ahead", "67502+00.70", 5.90, true, true, false),
    (4, 24, "Pier 19G back", "67502+50.70", 5.90, true, true, false),
    (4, 25, "Pier 19G back", "67502+85.70", 5.90, true, true, false),
};
var units = Doc.Project.Bridges.Where(b => b.Name.StartsWith("Ramp G Unit")).OrderBy(b => b.Name).ToList();
if (units.Count != 4) throw new Exception("Expected Ramp G Units 1-4 (run build-ramp-g.csx first).");
const string Coll = "Span 19G collection pipe";
foreach (var u in units) { u.Drains.RemoveAll(d => d.Trench != null); u.Misc ??= new MiscItemsDefinition(); u.Misc.Collectors.Clear(); u.Misc.PierDrains.Clear(); }
foreach (var r in rows)
{
    var u = units[r.unit - 1];
    u.Drains.Add(new DeckDrainDefinition
    {
        Station = Sta(r.sta), Left = r.left, Type = "Trench",
        Outlet = r.piped ? DrainOutlet.PipedToPier : DrainOutlet.FreeFall, SplashBasin = r.basin,
        Trench = new DrainTrenchDefinition
        {
            GutterFromEdge = G, TableCrossSlope = r.slope / 100, Label = $"{r.n} ({r.loc})",
            Collector = r.piped ? Coll : "", TubeFromGirderCl = r.piped ? Ft(1) + In(8) : null, ReferenceGirder = 0,
        },
    });
}
// 12" collection pipe of span 19G (Unit 4): drain 22 .. 25, vertical dims (PDF 159), couplers 4'-0 upstream of drains 23 and 24, 2'-0 upstream of 25
var u4 = units[3];
double F(int ft, double inch) => Ft(ft) + In(inch);
u4.Misc!.Collectors.Add(new DrainCollectorDefinition
{
    Name = Coll, NominalDiameter = In(12), ReferenceGirder = 0, OffsetFromGirder = F(1, 8), Slope = 0.0178,
    StartStation = Sta("67501+50.70"), EndSupport = "Pier 19G", HangerSpacing = Ft(5),
    Points =
    {
        new() { Station = Sta("67501+50.70"), DeckTopToCl = F(2, 9), Note = "drain 22" },
        new() { Station = Sta("67502+00.70"), DeckTopToCl = F(3, 4.875), Note = "drain 23" },
        new() { Station = Sta("67502+50.70"), DeckTopToCl = F(4, 2.375), Note = "drain 24" },
        new() { Station = Sta("67502+85.70"), DeckTopToCl = F(4, 9.25), Note = "drain 25" },
    },
    CouplerStations = { Sta("67502+00.70") - Ft(4), Sta("67502+50.70") - Ft(4), Sta("67502+85.70") - Ft(2) },
});
// pier joint collection systems (PDF 154-158): the unit that ENDS at the pier owns the joint body
PierDrainDefinition Pier(bool right, double slope, double boxFromGutter, double capBoxFromColumn) => new()
{
    CollectRight = right, TroughSlope = slope, BoxFromGutter = boxFromGutter, PrintedTroughDrop = F(1, 8), CapBoxFromColumn = capBoxFromColumn,
};
foreach (var (u, name) in new[] { (units[0], "Pier 5G"), (units[1], "Pier 9G"), (units[2], "Pier 14G") })
{
    var p = Pier(true, 0.059, F(3, 6), Ft(12.5)); p.Support = name; u.Misc!.PierDrains.Add(p);
}
var p19 = Pier(false, 0.049, F(4, 0), -F(18, 3.875));
p19.Support = "Pier 19G"; p19.CapPipeNominalDiameter = In(12); p19.SupportSpacing = Ft(4); p19.Outlet = PierDrainOutlet.Pavement; p19.PavementElevation = Ft(981); p19.Collector = Coll;
u4.Misc!.PierDrains.Add(p19);

var res = Doc.Regenerate();
Print($"Regenerated {res.Bridges} bridges in {res.Elapsed.TotalSeconds:F1} s, {res.Errors.Count} errors.");
foreach (var e in res.Errors) Print("  ERROR " + e);
int total = 0, conflicts = 0;
foreach (var u in units)
{
    var misc = Doc.Substructure(u.Name).Misc;
    if (misc == null) { Print($"{u.Name}: no misc model"); continue; }
    total += misc.Elements.Count;
    Print($"{u.Name}: {misc.Assemblies.Count} assemblies, {misc.Elements.Count} elements, {misc.Clearances.Count} clearance records");
    foreach (var c in misc.Clearances.OrderBy(c => c.Station))
    {
        if (c.Clearance <= 0) conflicts++;
        Print($"   {(c.Clearance <= 0 ? "CONFLICT" : "ok      ")} {c.Drain} / {c.Element}: {Doc.Units.FormatLength(c.Clearance)} to {c.Against}");
    }
    foreach (var d in misc.Diagnostics.Where(d => d.Severity != SBM.Core.Framing.DiagnosticSeverity.Info)) Print($"   {d.Severity}: {d.Message}");
}
Print($"Ramp G drainage: {total} elements, {conflicts} clearance conflicts.");
Doc.Save();
