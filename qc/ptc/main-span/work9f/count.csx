// rivets / dropped drawn rivets of the main span (read-only)
const string BN = "PTC Arch truss 18-20";
Doc.Project.Bridges.RemoveAll(b => b.Name != BN);
Doc.Regenerate();
var s = Doc.Model(BN).Steel!;
Print($"rivets {s.Bolts.Count(b => b.Kind == SBM.Core.Steel.BoltKind.Rivet)} dropped drawn {s.Truss!.DroppedRivets.Count} at L11: {s.Truss.DroppedRivets.Count(d => d.Point.StartsWith("L11"))} at U11: {s.Truss.DroppedRivets.Count(d => d.Point.StartsWith("U11"))}");
