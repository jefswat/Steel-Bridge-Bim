// Containment / cover check of the deck reinforcing of every bridge (generic or plan-driven): top / bottom cover, edge distance, distance from the deck-end lines
// (blockouts) and bars through the top of the steel (top flanges, field splice plates, fillers).  Does not save the model.
//   sbm run samples\plans\check-deck-rebar.csx "temp\model.sbm"
foreach (var b in Doc.Project.Bridges)
{
    var cm = Doc.Concrete(b.Name);
    Print($"== {b.Name}  ({(b.Deck.Reinforcement.Plan != null ? "plan-driven" : "generic")} layout)");
    foreach (var n in cm.Notes.Where(n => n.StartsWith("Deck rebar containment"))) Print("   " + n);
    foreach (var d in cm.Diagnostics.Where(d => d.Message.StartsWith("Deck reinforcing check"))) Print($"   {d.Severity}: {d.Message}");
}
