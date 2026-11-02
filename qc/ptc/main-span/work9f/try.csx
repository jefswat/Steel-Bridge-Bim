// quick main-span-only trial of the joint fit data (no save): sbm run try.csx <model> --no-pipeline -- <on|off> <clash csv>
using SBM.Core.Bridge;
using SBM.Core.Checks;
using SBM.Core.Steel;
const string BN = "PTC Arch truss 18-20";
Doc.Project.Bridges.RemoveAll(b => b.Name != BN);
var t = Doc.Project.Bridges[0].Truss!;
double In(double x) => x * 0.0254;
if (Args[0] == "on")
{
    var abutTop = new[] { "U11", "U12", "U13", "U14", "U15", "U16", "U17" };
    var abutBot = new[] { "L6", "L8", "L15", "L16", "L17", "L18" };
    foreach (var m in t.Members.Where(m => m.Role == TrussMemberRole.Vertical && m.Plane.Length == 0))
    {
        string a = m.From.TrimEnd('\''), b = m.To.TrimEnd('\'');
        var ab = new List<string>();
        if (abutTop.Contains(a)) ab.Add(m.From);
        if (abutBot.Contains(b)) ab.Add(m.To);
        m.WebsAbutAt = ab.Count > 0 ? ab : null;
        m.SetBackClearAt = a is "U7" or "U9" ? new() { m.From } : b == "L10" ? new() { m.To } : null;
        m.ChordsAbutAt = a == "U11" && b == "L11" ? new() { m.To } : null;
    }
    var j = t.Joints.Last(x => x.Point == "L22" && x.Plane.Length == 0);
    j.GussetSplitGap = In(0.125);
    var lay = j.GussetLayers![0];
    // gusset-layers.csv L22 re-read bottom -7.2 / +7.1 (u) at v -47.7
    lay.Outline = lay.Outline.Select(q => Math.Abs(q.V - In(-47.7)) < In(0.3) ? new TrussUV(q.U < 0 ? In(-7.2) : In(7.1), q.V) : q).ToList();
}
Doc.Regenerate();
var reps = Doc.Clashes(BN, new ClashOptions());
Print($"{reps.Sum(r => r.Clashes.Count)} clashes");
SteelClashCheck.WriteCsv(Args[1], reps, Doc.Units);
string extra = Args.Length > 4 ? Args[4] : "@@none@@";
foreach (var d in Doc.Model(BN).Steel!.Diagnostics.Where(d => d.Message.Contains("abut") || d.Message.Contains("not separated") || d.Message.Contains("split") || d.Message.Contains("overlapping at their panel")
    || System.Text.RegularExpressions.Regex.IsMatch(d.Message, extra)))
    Print($"{d.Severity}: {d.Message}");
// optional: triangles of the parts matching a regex (Args[2]) -> Args[3] (dump.csx tris.csv format)
if (Args.Length > 3)
{
    var st = Doc.Model(BN).Steel!;
    var inv = System.Globalization.CultureInfo.InvariantCulture;
    using var w = new System.IO.StreamWriter(Args[3]);
    w.WriteLine("mark,cat,ax,ay,az,bx,by,bz,cx,cy,cz");
    var sel = st.Parts.Where(p => System.Text.RegularExpressions.Regex.IsMatch(p.Mark, Args[2])).ToList();
    foreach (var p in sel)
    {
        var P = p.Mesh.Positions; var I = p.Mesh.Indices;
        for (int i = 0; i < I.Count; i += 3)
            w.WriteLine(string.Join(",", "\"" + p.Mark + "\"", p.Category, string.Join(",", new[] { P[I[i]], P[I[i + 1]], P[I[i + 2]] }.SelectMany(v => new[] { v.X, v.Y, v.Z }).Select(x => x.ToString("F4", inv)))));
    }
    // rivets inside the selection's box (+ 0.1 m) as small 3-point "triangles" (category Rivet) at the head centre
    if (Args.Length > 5 && sel.Count > 0)
    {
        var lo = new SBM.Core.Geometry.Vec3(sel.Min(p => p.Mesh.Bounds().Min.X) - 0.1, sel.Min(p => p.Mesh.Bounds().Min.Y) - 0.1, sel.Min(p => p.Mesh.Bounds().Min.Z) - 0.1);
        var hi = new SBM.Core.Geometry.Vec3(sel.Max(p => p.Mesh.Bounds().Max.X) + 0.1, sel.Max(p => p.Mesh.Bounds().Max.Y) + 0.1, sel.Max(p => p.Mesh.Bounds().Max.Z) + 0.1);
        using var rw = new System.IO.StreamWriter(Args[5]);
        rw.WriteLine("x,y,z,dx,dy,dz,dia,grip");
        foreach (var b in st.Bolts.Where(b => b.Kind == BoltKind.Rivet))
        {
            var o = b.Frame.Origin;
            if (o.X < lo.X || o.X > hi.X || o.Y < lo.Y || o.Y > hi.Y || o.Z < lo.Z || o.Z > hi.Z) continue;
            rw.WriteLine(string.Join(",", new[] { o.X, o.Y, o.Z, b.Frame.Z.X, b.Frame.Z.Y, b.Frame.Z.Z, b.Diameter, b.Grip }.Select(x => x.ToString("F4", inv))));
        }
    }
}
