// OPTIONAL, PROJECT-SPECIFIC (Iowa DOT Designs 116 / 316): the 1'-7 strip outside the gutter line is labelled "LEVEL" on the typical sections
// (I-80 PDF 94-96, Ramp G PDF 85): "WHERE THE DECK SLOPES DOWNWARD TOWARDS THE BARRIER RAIL, THE DECK UNDER THE BARRIER RAIL SHALL BE PLACED
// LEVEL ... WHERE THE DECK SLOPES DOWNWARD AWAY FROM THE BARRIER RAIL, [IT] SHALL BE PLACED ALONG THE SAME CROSS SLOPE AS THE DECK".
// DeckDefinition.EdgeStrip = Width measured inboard from the deck edge (follows the variable north edge of I-80 Units 2/3);
// per side and station the strip is level at the gutter elevation where that side's cross slope falls toward the edge, else the plane continues.
// Re-runnable, any time after the build scripts (all units of both bridges). Default for other projects: OFF.
using SBM.Core.Bridge;
double strip = Ft(1) + In(7);
foreach (var b in Doc.Project.Bridges)
{
    b.Deck.EdgeStrip = new DeckEdgeStrip { Width = strip, LowSideLevel = true };
    Print($"{b.Name}: edge strip {Doc.Units.FormatLength(strip)} level on the low side");
}
var res = Doc.Regenerate();
Print($"Regenerated {res.Bridges} bridges in {res.Elapsed.TotalSeconds:F1} s, {res.Errors.Count} errors.");
Doc.Save();
