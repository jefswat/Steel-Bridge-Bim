// QC (read-only): main span diagnostics containing a text
const string BN = "PTC Arch truss 18-20";
Doc.Project.Bridges.RemoveAll(b => b.Name != BN);
Doc.Regenerate();
foreach (var d in Doc.Model(BN).Steel!.Diagnostics.Where(d => d.Message.Contains(Args[0])).Take(int.Parse(Args.Length > 1 ? Args[1] : "20"))) Print(d.Severity + " " + (d.Message.Length > 400 ? d.Message[..400] : d.Message));
