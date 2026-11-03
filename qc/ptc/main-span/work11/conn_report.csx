// Bracing connection results of the generator (TrussModel.BracingConnections: placed rivets whose shank crosses both parts) -> csv (read-only).
using System.IO;
const string BN = "PTC Arch truss 18-20";
Doc.Project.Bridges.RemoveAll(b => b.Name != BN);
Doc.Regenerate();
var tm = Doc.Model(BN).Steel!.Truss!;
var w = new StreamWriter(Args[0]);
w.WriteLine("where,kind,name,n1,n2,ok");
foreach (var c in tm.BracingConnections)
{
    foreach (var e in c.Ends) w.WriteLine($"\"{c.Where}\",end,\"{e.Member}{(e.Start ? " (start)" : " (end)")}\",{e.Rivets},,{(e.Rivets > 0 ? 1 : 0)}");
    foreach (var p in c.Plates) w.WriteLine($"\"{c.Where}\",plate,\"{p.Plate}\",{p.Direct},{p.ViaAngles},{(p.Fastened ? 1 : 0)}");
    foreach (var a in c.Angles) w.WriteLine($"\"{c.Where}\",angle,\"{a.Angle}\",{a.ToPlate},{a.ToStack},{(a.ToStack > 0 ? 1 : 0)}");
}
w.Close();
Print($"{tm.BracingConnections.Count} connections");
