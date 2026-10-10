// Parametric study: steel weight vs. web depth.
// Run from the app (Home ▸ Run Script) or the command line:
//   sbm run "samples\scripts\web-depth-study.csx" "samples\Sample Project.sbm"
// Globals: Doc (SbmDocument), Print(...), Ft(..), In(..), Sta("12+34.56"), Len("12'-6\"").

var original = Doc.Bridge().Girders.Section.WebDepth;
foreach (var depth in new[] { 48, 54, 60, 66, 72 })
{
    Doc.Set("Bridges[0].Girders.Section.WebDepth", $"{depth}\"");
    var r = Doc.Regenerate();
    Print($"D = {depth}\"   steel {r.SteelMassKg / 907.18474,6:F1} tons   bolts {r.Bolts,5}   studs {r.Studs,5}   ({r.Elapsed.TotalMilliseconds:F0} ms)");
}
Doc.Bridge().Girders.Section.WebDepth = original; // leave the model as it was
