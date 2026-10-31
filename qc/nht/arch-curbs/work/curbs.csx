// Agent review of the NHT arch curbs: measurements only (no Save).
using System.Globalization;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
using SBM.Core.Concrete;

var br = Doc.Project.Bridges.First(b => b.Arch != null);
var bm = Doc.Model(br.Name);
var am = bm.Steel!.Arch!;
var al = Doc.Alignment(br.AlignmentName);
double IN(double m) => m / 0.0254;
double FT(double m) => m / 0.3048;
string S(double sta) { double f = FT(sta); int h = (int)Math.Floor(f / 100); return $"{h}+{f - 100 * h:00.00}"; }
Print($"bridge {br.Name}, alignment {al.Name}");
foreach (var c in br.Arch!.Deck!.Curbs) Print($"curb def {c.Name}: inner {FT(c.InnerOffset):F3} ft, width {IN(c.Width):F2} in, height {IN(c.Height):F2} in");
var rb = br.Arch.Deck.Rebar!;
Print($"rebar: S1 {rb.CurbStirrupSize} {IN(rb.CurbStirrupWidth)} x {IN(rb.CurbStirrupHeight)} topcover {IN(rb.CurbTopCover)} hook {IN(rb.CurbStirrupHook)} count {rb.CurbStirrupCount} endcover {IN(rb.EndCover)}");

// ---------- triangle grid index
sealed class TriIndex
{
    public readonly List<(Vec3 a, Vec3 b, Vec3 c, string tag)> T = new();
    readonly Dictionary<(int, int), List<int>> grid = new();
    const double Cell = 0.5;
    public void Add(Mesh m, string tag)
    {
        for (int i = 0; i < m.Indices.Count; i += 3)
        {
            var a = m.Positions[m.Indices[i]]; var b = m.Positions[m.Indices[i + 1]]; var c = m.Positions[m.Indices[i + 2]];
            int k = T.Count; T.Add((a, b, c, tag));
            int x0 = (int)Math.Floor(Math.Min(a.X, Math.Min(b.X, c.X)) / Cell), x1 = (int)Math.Floor(Math.Max(a.X, Math.Max(b.X, c.X)) / Cell);
            int y0 = (int)Math.Floor(Math.Min(a.Y, Math.Min(b.Y, c.Y)) / Cell), y1 = (int)Math.Floor(Math.Max(a.Y, Math.Max(b.Y, c.Y)) / Cell);
            for (int x = x0; x <= x1; x++) for (int y = y0; y <= y1; y++) { if (!grid.TryGetValue((x, y), out var l)) grid[(x, y)] = l = new(); l.Add(k); }
        }
    }
    public IEnumerable<int> Near(Vec3 p, double r)
    {
        var seen = new HashSet<int>();
        int x0 = (int)Math.Floor((p.X - r) / Cell), x1 = (int)Math.Floor((p.X + r) / Cell), y0 = (int)Math.Floor((p.Y - r) / Cell), y1 = (int)Math.Floor((p.Y + r) / Cell);
        for (int x = x0; x <= x1; x++) for (int y = y0; y <= y1; y++) if (grid.TryGetValue((x, y), out var l)) foreach (var k in l) if (seen.Add(k)) yield return k;
    }
    // vertical ray parity (+Z)
    public bool Inside(Vec3 p)
    {
        int n = 0;
        foreach (var k in Near(p, 0))
        {
            var (a, b, c, _) = T[k];
            double d = (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);
            if (Math.Abs(d) < 1e-14) continue;
            double u = ((p.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (p.Y - a.Y)) / d, v = ((b.X - a.X) * (p.Y - a.Y) - (p.X - a.X) * (b.Y - a.Y)) / d;
            if (u < 0 || v < 0 || u + v > 1) continue;
            double z = a.Z + u * (b.Z - a.Z) + v * (c.Z - a.Z);
            if (z > p.Z) n++;
        }
        return n % 2 == 1;
    }
    public (double d, string tag) Dist(Vec3 p, double r)
    {
        double best = double.MaxValue; string tag = "";
        foreach (var k in Near(p, r)) { var (a, b, c, t) = T[k]; double d = PtTri(p, a, b, c); if (d < best) { best = d; tag = t; } }
        return (best, tag);
    }
    static double PtTri(Vec3 p, Vec3 a, Vec3 b, Vec3 c)
    {
        var ab = b - a; var ac = c - a; var ap = p - a;
        double d1 = Vec3.Dot(ab, ap), d2 = Vec3.Dot(ac, ap);
        if (d1 <= 0 && d2 <= 0) return (p - a).Length;
        var bp = p - b; double d3 = Vec3.Dot(ab, bp), d4 = Vec3.Dot(ac, bp);
        if (d3 >= 0 && d4 <= d3) return (p - b).Length;
        double vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0) { double v = d1 / (d1 - d3); return (p - (a + ab * v)).Length; }
        var cp = p - c; double d5 = Vec3.Dot(ab, cp), d6 = Vec3.Dot(ac, cp);
        if (d6 >= 0 && d5 <= d6) return (p - c).Length;
        double vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0) { double w = d2 / (d2 - d6); return (p - (a + ac * w)).Length; }
        double va = d3 * d6 - d5 * d4;
        if (va <= 0 && (d4 - d3) >= 0 && (d5 - d6) >= 0) { double w = (d4 - d3) / ((d4 - d3) + (d5 - d6)); return (p - (b + (c - b) * w)).Length; }
        double den = 1 / (va + vb + vc); double vv = vb * den, ww = vc * den;
        return (p - (a + ab * vv + ac * ww)).Length;
    }
}

var curbs = am.Curbs!;
Print($"curb mesh: {curbs.TriangleCount} tris, volume {Math.Abs(curbs.Volume()) / 0.764554857984:F3} CY, exact {(curbs.ExactVolume() is double ev ? (Math.Abs(ev) / 0.764554857984).ToString("F3") : "-")}");
// split vertices by side
var curbIdx = new TriIndex(); curbIdx.Add(curbs, "curb");
var conc = new TriIndex(); conc.Add(am.Slab!, "slab"); if (am.SlabEnds != null) conc.Add(am.SlabEnds, "slabends"); conc.Add(curbs, "curb");
var steel = new TriIndex();
foreach (var p in am.Steel.Parts) steel.Add(p.Mesh, p.Mark);
foreach (var side in new[] { -1, 1 })
{
    var c = br.Arch.Deck.Curbs.First(x => Math.Sign(x.InnerOffset) == side);
    var vs = curbs.Positions.Select(p => (p, so: al.Inverse(p.XY))).Where(v => Math.Sign(v.so.Offset) == side).ToList();
    var offs = vs.Select(v => FT(Math.Abs(v.so.Offset))).ToList();
    Print($"== {c.Name}: {vs.Count} vertices, |offset| {offs.Min():F4}..{offs.Max():F4} ft; stations {S(vs.Min(v => v.so.Station))} .. {S(vs.Max(v => v.so.Station))}");
    // heights: bottom vertices vs finished grade, top vs grade at inner offset
    double maxBot = 0, minTop = 1, maxTop = 0, maxTopTilt = 0;
    foreach (var v in vs)
    {
        double g = br.Deck.FinishedElevation(al, v.so.Station, v.so.Offset);
        double gi = br.Deck.FinishedElevation(al, v.so.Station, c.InnerOffset);
        double dz = v.p.Z - g;
        if (dz < 0.05) maxBot = Math.Max(maxBot, Math.Abs(dz));
        else { double h = v.p.Z - gi; minTop = Math.Min(minTop, h); maxTop = Math.Max(maxTop, h); maxTopTilt = Math.Max(maxTopTilt, v.p.Z - g); }
    }
    Print($"   bottom vs finished grade max |dz| {IN(maxBot):F3} in; top above grade at inner face {IN(minTop):F3}..{IN(maxTop):F3} in; max top above grade below the vertex {IN(maxTopTilt):F3} in");
    // inner-face length (bottom inner vertices sorted by station)
    var inner = vs.Where(v => Math.Abs(Math.Abs(v.so.Offset) - Math.Abs(c.InnerOffset)) < 0.002).OrderBy(v => v.so.Station).Select(v => v.p).ToList();
    double len = 0; for (int i = 1; i < inner.Count; i++) len += (inner[i].XY - inner[i - 1].XY).Length;
    Print($"   inner face plan length (both edges incl.) ~{FT(len / 2):F3} ft (vertex polyline / 2: top + bottom); stations inner {S(inner.Min(p => al.Inverse(p.XY).Station))}..{S(inner.Max(p => al.Inverse(p.XY).Station))}");
    var outer = vs.Where(v => Math.Abs(Math.Abs(v.so.Offset) - Math.Abs(c.InnerOffset) - c.Width) < 0.002).ToList();
    Print($"   outer face stations {S(outer.Min(v => v.so.Station))}..{S(outer.Max(v => v.so.Station))}");
    // end vertices: distance to steel and to the slab-ends edge
    foreach (var end in new[] { 0, 1 })
    {
        var ev2 = (end == 0 ? vs.OrderBy(v => v.so.Station) : vs.OrderByDescending(v => v.so.Station)).Take(1).First();
        var endPts = vs.Where(v => Math.Abs(v.so.Station - ev2.so.Station) < 0.4).ToList();
        // the 4 corner vertices at the end: min / max station at inner and outer
        foreach (var (lbl, sel) in new[] { ("inner", inner.Select(p => (p, so: al.Inverse(p.XY))).ToList()), ("outer", outer) })
        {
            var e = end == 0 ? sel.OrderBy(v => v.so.Station).First() : sel.OrderByDescending(v => v.so.Station).First();
            var (ds, tag) = steel.Dist(e.p, 0.6);
            Print($"   end {(end == 0 ? "west" : "east")} {lbl} bottom corner {S(e.so.Station)} off {FT(e.so.Offset):F3}: nearest steel {IN(ds):F2} in ({tag})");
        }
    }
    // steel parts with vertices inside this curb
    var inside = new Dictionary<string, int>();
    foreach (var p in am.Steel.Parts)
    {
        var bb = p.Mesh.Bounds();
        int k = 0;
        foreach (var q in p.Mesh.Positions) { var so = al.Inverse(q.XY); if (Math.Sign(so.Offset) != side) continue; if (curbIdx.Inside(q)) k++; }
        if (k > 0) inside[p.Mark] = k;
    }
    Print($"   steel parts with vertices inside the curb: {string.Join(", ", inside.Select(kv => kv.Key + "(" + kv.Value + ")"))}");
}

// ---------- bars
var con = am.Concrete;
foreach (var mk in con.Marks.Where(m => m.Mark is "S1" or "D4")) Print($"mark {mk.Mark}: {mk.Size.Name} count {mk.Count} length {IN(mk.Length):F2} in ({FT(mk.Length):F3} ft), total {FT(mk.TotalLength):F1} ft, {mk.TotalMass * 2.20462:F0} lb; {mk.Location}");
var s1 = con.Bars.Where(b => b.Mark == "S1").ToList();
var d4 = con.Bars.Where(b => b.Mark == "D4").ToList();
foreach (var side in new[] { -1, 1 })
{
    var c = br.Arch.Deck.Curbs.First(x => Math.Sign(x.InnerOffset) == side);
    var ss = s1.Select(b => (b, so: al.Inverse(new Vec2(b.Points.Average(p => p.X), b.Points.Average(p => p.Y))))).Where(x => Math.Sign(x.so.Offset) == side).OrderBy(x => x.so.Station).ToList();
    var sp = ss.Zip(ss.Skip(1), (a, b) => (b.so.Station - a.so.Station)).ToList();
    Print($"== S1 {c.Name}: {ss.Count} bars, stations {S(ss[0].so.Station)}..{S(ss[^1].so.Station)}, spacing (CL) {IN(sp.Min()):F3}..{IN(sp.Max()):F3} in mean {IN(sp.Average()):F3}");
    // spacing at the curb inner face (curved curb: CL vs curb line)
    double rad = IN(new Vec2(1, 0).X);
    double r = 0.0254 * 0.25;  // #4 radius
    double minTop = 9, maxTop = 0, minIn = 9, minOut = 9, minBotBelowGrade = 9, maxBotBelowGrade = 0; int outsideConc = 0, hookOuter = 0, hookInner = 0; double minSteel = 9; string steelTag = "";
    foreach (var (b, so) in ss)
    {
        double gi = br.Deck.FinishedElevation(al, so.Station, c.InnerOffset);
        double top = gi + c.Height;
        double zmax = b.Points.Max(p => p.Z), zmin = b.Points.Min(p => p.Z);
        minTop = Math.Min(minTop, top - zmax - r); maxTop = Math.Max(maxTop, top - zmax - r);
        double ob = b.Points.Min(p => Math.Abs(al.Inverse(p.XY).Offset)), oo = b.Points.Max(p => Math.Abs(al.Inverse(p.XY).Offset));
        minIn = Math.Min(minIn, ob - r - Math.Abs(c.InnerOffset)); minOut = Math.Min(minOut, Math.Abs(c.InnerOffset) + c.Width - oo - r);
        double g = br.Deck.FinishedElevation(al, so.Station, c.InnerOffset + side * c.Width / 2);
        minBotBelowGrade = Math.Min(minBotBelowGrade, g - zmin); maxBotBelowGrade = Math.Max(maxBotBelowGrade, g - zmin);
        // hook tails: first point
        double offHook = Math.Abs(al.Inverse(b.Points[0].XY).Offset);
        if (offHook > Math.Abs(c.InnerOffset) + c.Width / 2) hookOuter++; else hookInner++;
        foreach (var p in b.Points) { if (!conc.Inside(p)) outsideConc++; var (d, t) = steel.Dist(p, 0.3); if (d < minSteel) { minSteel = d; steelTag = t; } }
    }
    Print($"   top clear (curb top - bar top - r) {IN(minTop):F3}..{IN(maxTop):F3} in; inner-face clear min {IN(minIn):F3} in; outer-face clear min {IN(minOut):F3} in");
    Print($"   bar bottom below grade (mid-curb) {IN(minBotBelowGrade):F3}..{IN(maxBotBelowGrade):F3} in; hooks at outer {hookOuter} / inner {hookInner}; bar points outside the concrete {outsideConc}; min CL distance to steel {IN(minSteel):F2} in ({steelTag})");
    var bb0 = ss[0].b; Print($"   first S1 points: {string.Join(" ", bb0.Points.Take(3).Select(p => $"({FT(al.Inverse(p.XY).Offset):F3},{IN(p.Z - br.Deck.FinishedElevation(al, ss[0].so.Station, c.InnerOffset)):F2})"))} n={bb0.Points.Length}");
    // D4
    var dd = d4.Select(b => (b, so0: al.Inverse(b.Points[0].XY), so1: al.Inverse(b.Points[^1].XY))).Where(x => Math.Sign(x.so0.Offset) == side).ToList();
    double s0 = dd.Min(x => Math.Min(x.so0.Station, x.so1.Station)), s1e = dd.Max(x => Math.Max(x.so0.Station, x.so1.Station));
    Print($"== D4 {c.Name}: {dd.Count} bars, stations {S(s0)}..{S(s1e)}, lengths {string.Join(", ", dd.Select(x => { double L = 0; for (int i = 1; i < x.b.Points.Length; i++) L += (x.b.Points[i] - x.b.Points[i - 1]).Length; return FT(L).ToString("F2"); }))}");
    int dOut = 0; double dMinSteel = 9; string dTag = ""; double rd = 0.0254 * 0.625 / 2;
    double dTop = 9, dIn = 9, dOutF = 9;
    foreach (var (b, _, _) in dd) foreach (var p in b.Points)
    {
        if (!conc.Inside(p)) dOut++;
        var (d, t) = steel.Dist(p, 0.3); if (d < dMinSteel) { dMinSteel = d; dTag = t; }
        var so = al.Inverse(p.XY);
        double gi = br.Deck.FinishedElevation(al, so.Station, c.InnerOffset);
        dTop = Math.Min(dTop, gi + c.Height - p.Z - rd);
        dIn = Math.Min(dIn, Math.Abs(so.Offset) - Math.Abs(c.InnerOffset) - rd); dOutF = Math.Min(dOutF, Math.Abs(c.InnerOffset) + c.Width - Math.Abs(so.Offset) - rd);
    }
    Print($"   D4 points outside concrete {dOut}; min CL distance to steel {IN(dMinSteel):F2} in ({dTag}); top clear {IN(dTop):F3} in, inner clear {IN(dIn):F3} in, outer clear {IN(dOutF):F3} in");
}

// ---------- railing
foreach (var run in bm.Steel.Railings?.Runs ?? new())
{
    int side = Math.Sign(run.Offset);
    var c = br.Arch.Deck.Curbs.First(x => Math.Sign(x.InnerOffset) == side);
    Print($"== railing {run.Name}: offset {FT(run.Offset):F3}, {S(run.BeginStation)}..{S(run.EndStation)}, length {FT(run.Length):F2} ft, {run.Posts} posts at {string.Join(" ", run.PostStations.Take(3).Select(S))} .. {string.Join(" ", run.PostStations.TakeLast(3).Select(S))}");
    // base line vs curb top
    double maxDz = 0; int offCurb = 0;
    foreach (var q in run.Line)
    {
        var so = al.Inverse(q.XY);
        bool onCurb = curbIdx.Inside(q - new Vec3(0, 0, 0.01));
        double gi = br.Deck.FinishedElevation(al, so.Station, c.InnerOffset);
        if (!onCurb) { offCurb++; Print($"   base point {S(so.Station)} off {FT(so.Offset):F3}: z - (grade at curb line + 4\") = {IN(q.Z - gi - c.Height):F2} in, NOT over the curb"); }
        else maxDz = Math.Max(maxDz, Math.Abs(q.Z - gi - c.Height));
    }
    Print($"   base line {run.Line.Count} pts, over the curb max |z - curb top| {IN(maxDz):F3} in; {offCurb} pts not over the curb");
    // posts: check each post base on the curb
    var ps = bm.Steel.Parts.Where(p => p.Assembly == run.Assembly && p.Category == PartCategory.RailPost).ToList();
    int pOff = 0; double minBase = 9, maxBase = -9;
    foreach (var p in ps)
    {
        var bbx = p.Mesh.Bounds();
        var bot = p.Mesh.Positions.Where(v => v.Z < bbx.Min.Z + 0.002).ToList();
        int notOn = bot.Count(v => !curbIdx.Inside(v - new Vec3(0, 0, 0.01)));
        var so = al.Inverse(new Vec2(bot.Average(v => v.X), bot.Average(v => v.Y)));
        double gi = br.Deck.FinishedElevation(al, so.Station, c.InnerOffset);
        double dzb = bbx.Min.Z - (gi + c.Height); minBase = Math.Min(minBase, dzb); maxBase = Math.Max(maxBase, dzb);
        var bo = bot.Select(v => FT(Math.Abs(al.Inverse(v.XY).Offset))).ToList();
        if (notOn > 0) { pOff++; Print($"   post {p.Mark} {S(so.Station)}: {notOn}/{bot.Count} base vertices not over the curb; base |offset| {bo.Min():F3}..{bo.Max():F3} ft; base - curb top {IN(dzb):F2} in"); }
    }
    Print($"   {ps.Count} posts; base - curb top {IN(minBase):F3}..{IN(maxBase):F3} in; {pOff} posts with base vertices off the curb");
    var bo2 = ps.SelectMany(p => { var bb = p.Mesh.Bounds(); return p.Mesh.Positions.Where(v => v.Z < bb.Min.Z + 0.002); }).Select(v => FT(Math.Abs(al.Inverse(v.XY).Offset))).ToList();
    Print($"   post base |offset| range {bo2.Min():F3}..{bo2.Max():F3} ft (curb {FT(Math.Abs(c.InnerOffset)):F3}..{FT(Math.Abs(c.InnerOffset) + c.Width):F3})");
}
foreach (var d in am.Diagnostics.Where(d => d.Message.ToLowerInvariant().Contains("curb"))) Print($"DIAG {d.Severity}: {d.Message}");
