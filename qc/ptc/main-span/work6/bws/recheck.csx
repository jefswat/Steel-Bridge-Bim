// QC main span bearings / walkways / sections (read-only, NO save). sbm run dump.csx "<PTC model>" -- <outdir>
// Frame: u = station ft along the tangent, n = offset right ft, z = elevation ft.
using System.IO;
using System.Globalization;
using SBM.Core.Geometry;
using SBM.Core.Steel;
using SBM.Core.Truss;
using SBM.Core.Bridge;
using SBM.Core.Solids;
var inv = CultureInfo.InvariantCulture;
string outDir = Args[0];
Directory.CreateDirectory(outDir);
const string BN = "PTC Arch truss 18-20";
var keepPiers = new[] { "Pier 17", "Pier 18", "Pier 19", "Pier 20" };
var keep = Doc.Project.Bridges.Where(b => b.Name == BN || b.Supports.Any(s => keepPiers.Any(k => s.Name.StartsWith(k)) && s.Substructure?.Plan != null)).Select(b => b.Name).ToList();
Print("keep: " + string.Join(" | ", keep));
var bd = Doc.Project.Bridges.First(b => b.Name == BN);
string F(double x) => x.ToString("F4", inv);
string Q(string s) => "\"" + (s ?? "").Replace("\"", "'").Replace("\n", " ") + "\"";
double IN = 0.0254;

// ---------- sections (no regeneration needed)
using (var w = new StreamWriter(Path.Combine(outDir, "sections.txt")))
{
    var t = bd.Truss!;
    foreach (var s in t.Sections)
    {
        var r = TrussSections.Build(s);
        var users = t.Members.Where(m => m.Section == s.Name).Select(m => (m.Plane.Length > 0 ? m.Plane + ":" : "") + m.Name).ToList();
        w.WriteLine($"=== {s.Name} kind={s.Kind} label={s.Label}");
        w.WriteLine($"  area={r.Area / IN / IN:0.00} sqin  x[{r.XMin / IN:0.###},{r.XMax / IN:0.###}] y[{r.YMin / IN:0.###},{r.YMax / IN:0.###}] webOuter={r.WebOuterX / IN:0.###} webInner={r.WebInnerX / IN:0.###} halfAtJoints={r.HalfWidthAtJoints / IN:0.###} lbft={r.LbPerFt:0.##}");
        w.WriteLine($"  def: WebDepth={s.WebDepth / IN:0.###} OtO={s.WebsOutToOut / IN:0.###} BtB={s.WebsBackToBack / IN:0.###} Angle={s.Angle} legOnWeb={s.AngleLegOnWeb / IN:0.###} proj={s.AngleProjection / IN:0.###} anglesOut={s.AnglesOutside} edges={s.AngleEdges} channel={s.Channel} b2b={s.BackToBack / IN:0.###} flangesOut={s.FlangesOut} desig={s.Designation} rotated={s.Rotated} webVertical={s.WebVertical} shapeArea={s.ShapeArea / IN / IN:0.##}");
        foreach (var p in r.Pieces)
        {
            double a = Math.Abs(MeshBuilder.SignedArea(p.Polygon));
            w.WriteLine($"   piece {p.Label,-45} x[{p.Polygon.Min(q => q.X) / IN,8:0.###},{p.Polygon.Max(q => q.X) / IN,8:0.###}] y[{p.Polygon.Min(q => q.Y) / IN,8:0.###},{p.Polygon.Max(q => q.Y) / IN,8:0.###}] A={a / IN / IN:0.00} len={p.Length / 0.3048:0.##}ft start={(double.IsNaN(p.Start) ? "" : (p.Start / 0.3048).ToString("0.##"))} betw={p.BetweenGussets} perf={(p.Perforation != null)}");
        }
        foreach (var n in r.Notes) w.WriteLine("  NOTE " + n);
        if (r.Flag != null) w.WriteLine("  FLAG " + r.Flag);
        w.WriteLine("  users: " + string.Join(", ", users));
    }
    w.WriteLine();
    w.WriteLine("=== MEMBERS");
    foreach (var m in t.Members) w.WriteLine($"{m.Name}\t{m.Plane}\t{m.From}\t{m.To}\t{m.Section}\t{m.Role}\t{m.System}\tsa={m.StartSetback}\tsb={m.EndSetback}\t{m.Source}");
    w.WriteLine("=== SHOES");
    foreach (var s in t.Shoes)
    {
        w.WriteLine($"{s.Name} pt={s.Point} sup={s.Support} type={s.Type} pinD={s.PinDiameter / IN:0.###} pinBelow={s.PinBelowPoint / IN:0.###} H={s.Height / IN:0.###} masonry {s.MasonryLength / IN:0.##}x{s.MasonryWidth / IN:0.##}x{s.MasonryThickness / IN:0.##} seat={s.SeatElevation / 0.3048:0.####}");
        foreach (var p in s.Pieces ?? new()) w.WriteLine($"   piece {p.Name} {p.Kind} {p.Axis} from={p.From / IN:0.###} to={p.To / IN:0.###} prof u[{(p.Profile.Count > 0 ? p.Profile.Min(q => q.U) / IN : 0):0.###},{(p.Profile.Count > 0 ? p.Profile.Max(q => q.U) / IN : 0):0.###}] v[{(p.Profile.Count > 0 ? p.Profile.Min(q => q.V) / IN : 0):0.###},{(p.Profile.Count > 0 ? p.Profile.Max(q => q.V) / IN : 0):0.###}] mirN={p.MirrorN} mirU={p.MirrorU}");
    }
}
Print("sections written");

Doc.Project.Bridges.RemoveAll(b => !keep.Contains(b.Name));
var sw = System.Diagnostics.Stopwatch.StartNew();
var rep = Doc.Regenerate();
Print($"regen {sw.Elapsed.TotalSeconds:0.0}s");
Print("sections written");
using (var w = new StreamWriter(Path.Combine(outDir, "plane-items.txt")))
    foreach (var pi in bd.Truss!.PlaneItems ?? new()) w.WriteLine($"{pi.Name}\t{pi.Category}\t{pi.Assembly}\tsec{pi.Section.Count} path{pi.Path.Count} {pi.PointA}-{pi.PointB}\tframe={(pi.Frame != null ? $"{pi.Frame.Station / 0.3048:0.###}/{pi.Frame.Offset / 0.3048:0.###}/{pi.Frame.Elevation / 0.3048:0.####}" : "")}\t{pi.Source}\tU={pi.Unresolved}");
using (var w = new StreamWriter(Path.Combine(outDir, "line-items.txt")))
    foreach (var li in bd.Truss!.LineItems ?? new()) w.WriteLine($"{li.Name}\t{li.Category}\t{li.Assembly}\ttr={li.Transverse}\tsta={li.Station / 0.3048:0.###}\tref={li.ReferenceOffset / 0.3048:0.###}\t{li.From / 0.3048:0.###}..{li.To / 0.3048:0.###}\tpitch={li.RepeatPitch / IN:0.##}\t{li.Source}\tU={li.Unresolved}");
var m0 = Doc.Model(BN);
var st = m0.Steel!;
var tm = st.Truss!;
var al = Doc.Project.Alignments.First(a => a.Name == bd.AlignmentName);
(double u, double n, double z) L(Vec3 p) { var inv2 = al.Inverse(new Vec2(p.X, p.Y)); return (inv2.Station / 0.3048, inv2.Offset / 0.3048, p.Z / 0.3048); }
string LS(Vec3 p) { var q = L(p); return F(q.u) + "," + F(q.n) + "," + F(q.z); }

// meshes for ray casting
var meshes = new List<(string owner, string name, string cat, Mesh mesh, Box3 bb)>();
foreach (var p in st.Parts) if (!p.Mesh.IsEmpty) meshes.Add(("steel", p.Mark, p.Category.ToString() + "|" + p.Assembly, p.Mesh, p.Mesh.Bounds()));
foreach (var bn in keep)
{
    var sub = Doc.Model(bn).Substructure;
    if (sub == null) continue;
    foreach (var u in sub.Units)
        foreach (var s in u.Solids) if (!s.Mesh.IsEmpty) meshes.Add(("sub:" + bn + ":" + u.Name, s.Name, s.Kind.ToString(), s.Mesh, s.Mesh.Bounds()));
    if (bn != BN && Doc.Model(bn).Steel is { } st2)
        foreach (var p in st2.Parts.Where(p => p.Assembly.StartsWith("SHOE")))
            if (!p.Mesh.IsEmpty) meshes.Add(("steel:" + bn, p.Mark, p.Category.ToString() + "|" + p.Assembly, p.Mesh, p.Mesh.Bounds()));
}
List<(double z0, double z1, string who)> Ray(double x, double y)
{
    var res = new List<(double, double, string)>();
    x += 1.37e-7; y -= 0.91e-7;
    foreach (var (owner, name, cat, mesh, bb) in meshes)
    {
        if (x < bb.Min.X || x > bb.Max.X || y < bb.Min.Y || y > bb.Max.Y) continue;
        var zs = new List<double>();
        var pos = mesh.Positions; var idx = mesh.Indices;
        for (int i = 0; i + 2 < idx.Count; i += 3)
        {
            var A = pos[idx[i]]; var B = pos[idx[i + 1]]; var C = pos[idx[i + 2]];
            double d = (B.Y - C.Y) * (A.X - C.X) + (C.X - B.X) * (A.Y - C.Y);
            if (Math.Abs(d) < 1e-14) continue;
            double l1 = ((B.Y - C.Y) * (x - C.X) + (C.X - B.X) * (y - C.Y)) / d, l2 = ((C.Y - A.Y) * (x - C.X) + (A.X - C.X) * (y - C.Y)) / d, l3 = 1 - l1 - l2;
            if (l1 < -1e-12 || l2 < -1e-12 || l3 < -1e-12) continue;
            zs.Add(l1 * A.Z + l2 * B.Z + l3 * C.Z);
        }
        if (zs.Count == 0) continue;
        zs.Sort();
        // pair up entry/exit
        var ded = new List<double>();
        foreach (var z in zs) if (ded.Count == 0 || Math.Abs(z - ded[^1]) > 1e-7) ded.Add(z);
        for (int k = 0; k + 1 < ded.Count; k += 2) res.Add((ded[k], ded[k + 1], owner + ":" + name + " [" + cat + "]"));
        if (ded.Count % 2 == 1) res.Add((ded[^1], ded[^1], owner + ":" + name + " [" + cat + "] (odd)"));
    }
    return res.OrderBy(r => r.Item1).ToList();
}

using (var w = new StreamWriter(Path.Combine(outDir, "shoes.txt")))
{
    foreach (var sh in tm.Shoes)
    {
        var def = bd.Truss!.Shoes.First(s => s.Name == sh.Name);
        var wp = tm.Points.First(p => p.Plane == def.Plane && p.Name == def.Point);
        var lw = L(wp.World); var lp = L(sh.Pin);
        w.WriteLine($"##### SHOE {sh.Name} support={sh.Support} type={sh.Type} WP {def.Point} u={lw.u:0.0000} n={lw.n:0.0000} z={lw.z:0.0000} | pin u={lp.u:0.0000} n={lp.n:0.0000} z={lp.z:0.0000} (WP-pin {(lw.z - lp.z) * 12:0.000} in, du {(lp.u - lw.u) * 12:0.000} in, dn {(lp.n - lw.n) * 12:0.000} in) seat={sh.SeatElevation / 0.3048:0.0000}");
        // tangent direction
        var pA = al.PointAt(wp.Station, 0); var pB = al.PointAt(wp.Station + 1, 0);
        var tdir = new Vec2(pB.X - pA.X, pB.Y - pA.Y); tdir = tdir * (1 / tdir.Length);
        var ndir = new Vec2(tdir.Y, -tdir.X); // right
        foreach (var (du, dn) in new[] { (0.0, 0.0), (0.0, 6.0), (0.0, -6.0), (0.0, 12.0), (0.0, -12.0), (6.0, 0.0), (-6.0, 0.0), (9.0, 0.0), (-9.0, 0.0), (12.0, 0.0), (-12.0, 0.0), (15.0, 0.0), (-15.0, 0.0), (20.0, 0.0), (-20.0, 0.0), (10.0, 10.0), (-10.0, -10.0), (30.0, 0.0), (-30.0, 0.0), (0.0, 18.0), (0.0, -18.0) })
        {
            double x = sh.Pin.X + (tdir.X * du + ndir.X * dn) * IN, y = sh.Pin.Y + (tdir.Y * du + ndir.Y * dn) * IN;
            var hits = Ray(x, y).Where(h => h.z1 > wp.World.Z - Ft(16) && h.z0 < wp.World.Z + Ft(3)).ToList();
            w.WriteLine($"  -- ray du={du} dn={dn} in (from pin)");
            double prevTop = double.NaN;
            foreach (var h in hits.OrderByDescending(h => h.z1))
            {
                string gap = double.IsNaN(prevTop) ? "" : $" gap below previous = {(prevTop - h.z1) / IN:0.000} in";
                w.WriteLine($"     z {h.z0 / 0.3048:0.0000}..{h.z1 / 0.3048:0.0000} ft  (rel WP {(h.z0 - wp.World.Z) / IN:0.000}..{(h.z1 - wp.World.Z) / IN:0.000} in){gap}  {h.who}");
                prevTop = double.IsNaN(prevTop) ? h.z0 : Math.Min(prevTop, h.z0);
            }
        }
        w.WriteLine("  shoe parts:");
        foreach (var p in st.Parts.Where(p => p.Assembly.Contains(sh.Name) || p.Mark.Contains(sh.Name)))
        {
            var b = p.Mesh.Bounds();
            w.WriteLine($"     {p.Mark} [{p.Category}|{p.Assembly}] z {b.Min.Z / 0.3048:0.0000}..{b.Max.Z / 0.3048:0.0000} rel WP {(b.Min.Z - wp.World.Z) / IN:0.00}..{(b.Max.Z - wp.World.Z) / IN:0.00} in  {p.Description}");
        }
    }
    w.WriteLine();
    w.WriteLine("##### SUBSTRUCTURE UNITS");
    foreach (var bn in keep)
    {
        var sub = Doc.Model(bn).Substructure;
        if (sub == null) { w.WriteLine(bn + ": no substructure"); continue; }
        foreach (var u in sub.Units.Where(u => keepPiers.Any(k => u.Name.StartsWith(k))))
        {
            w.WriteLine($"{bn} / {u.Name} station {u.Station / 0.3048:0.000}");
            foreach (var fs in u.FixedSeats) { var pw = u.World(fs.S, fs.N, fs.Z); w.WriteLine($"   seat {fs.Name} S={fs.S / 0.3048:0.###} N={fs.N / 0.3048:0.###} Z={fs.Z / 0.3048:0.####} world {LS(pw)}"); }
        }
    }
    w.WriteLine();
    w.WriteLine("##### BEARING SEATS");
    foreach (var s in st.BearingSeats) w.WriteLine($"{s.Label} girder={s.Girder} station={s.Station / 0.3048:0.###} seatEl={s.SeatElevation / 0.3048:0.####}");
}
Print("shoes written");
using (var w = new StreamWriter(Path.Combine(outDir, "walk-parts.csv")))
{
    w.WriteLine("mark,category,assembly,minu,minn,minz,maxu,maxn,maxz,mass_kg,status,description");
    foreach (var p in st.Parts.Where(p => p.Assembly.Contains("WALK") || p.Assembly.Contains("LADDER") || p.Assembly.Contains("PLATFORM") || p.Category.ToString().StartsWith("Walkway") || p.Category == PartCategory.Grating || p.Category == PartCategory.InspectionCable || (p.Description ?? "").Contains("walk", StringComparison.OrdinalIgnoreCase) || (p.Description ?? "").Contains("platform", StringComparison.OrdinalIgnoreCase)))
    {
        double mu = 1e9, mn = 1e9, mz = 1e9, xu = -1e9, xn = -1e9, xz = -1e9;
        foreach (var v in p.Mesh.Positions) { var q = L(v); mu = Math.Min(mu, q.u); mn = Math.Min(mn, q.n); mz = Math.Min(mz, q.z); xu = Math.Max(xu, q.u); xn = Math.Max(xn, q.n); xz = Math.Max(xz, q.z); }
        w.WriteLine(string.Join(",", Q(p.Mark), p.Category, Q(p.Assembly), F(mu), F(mn), F(mz), F(xu), F(xn), F(xz), F(p.Mass), Q(p.Assumption?.Status ?? ""), Q(p.Description)));
    }
}
Print("walk written");
// whole members: local bounds from bbox corners
using (var w = new StreamWriter(Path.Combine(outDir, "whole.tsv")))
{
    w.WriteLine("mark\tcategory\tassembly\tmass_kg\tlength_m\tminu\tminn\tminz\tmaxu\tmaxn\tmaxz\tdescription");
    foreach (var p in st.WholeMembers())
    {
        if (p.Mesh.IsEmpty) continue;
        var b = p.Mesh.Bounds();
        double mu = 1e9, mn = 1e9, mz = b.Min.Z / 0.3048, xu = -1e9, xn = -1e9, xz = b.Max.Z / 0.3048;
        foreach (var c in new[] { new Vec3(b.Min.X, b.Min.Y, 0), new Vec3(b.Max.X, b.Min.Y, 0), new Vec3(b.Min.X, b.Max.Y, 0), new Vec3(b.Max.X, b.Max.Y, 0) }) { var q = L(c); mu = Math.Min(mu, q.u); mn = Math.Min(mn, q.n); xu = Math.Max(xu, q.u); xn = Math.Max(xn, q.n); }
        w.WriteLine($"{p.Mark}\t{p.Category}\t{p.Assembly}\t{p.Mass:0.0}\t{p.Length:0.000}\t{F(mu)}\t{F(mn)}\t{F(mz)}\t{F(xu)}\t{F(xn)}\t{F(xz)}\t{p.Description}");
    }
}
using (var w = new StreamWriter(Path.Combine(outDir, "pieces.tsv")))
    foreach (var p in st.Parts.Where(p => p.Member != null)) w.WriteLine($"{p.Mark}\t{p.Member}\t{p.Piece}\t{p.Category}\t{p.Mass:0.0}\t{p.Description}");
using (var w = new StreamWriter(Path.Combine(outDir, "members-res.tsv")))
    foreach (var m in tm.Members) w.WriteLine($"{m.Name}\t{m.Mark}\t{m.Plane}\t{m.From}\t{m.To}\t{m.Role}\t{m.Section}\t{m.WorkLength / 0.3048:0.0000}\t{m.CutLength / 0.3048:0.0000}\t{tm.PanelOf.GetValueOrDefault(m.Mark)}\t{LS(m.A)}\t{LS(m.B)}");
using (var w = new StreamWriter(Path.Combine(outDir, "points.csv")))
{
    w.WriteLine("plane,name,station_ft,u,n,z");
    foreach (var p in tm.Points) w.WriteLine($"{p.Plane},{p.Name},{F(p.Station / 0.3048)},{LS(p.World)}");
}
using (var w = new StreamWriter(Path.Combine(outDir, "diag.txt")))
{
    foreach (var d in rep.Warnings.Where(x => x.StartsWith(BN))) w.WriteLine("W " + d);
    foreach (var c in tm.Checks) w.WriteLine($"CHECK {(c.Ok ? "OK" : "DIFF")} {c.Item} | plan {c.Plan} | model {c.Model} | {c.Note}");
}
Print($"steel {st.SteelMass * 2.20462262:N0} lb parts {st.Parts.Count}");
Print("done");
