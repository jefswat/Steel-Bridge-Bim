// Agent review of curbs (scratch, read-only): what does the model have in the gore / crash cushion areas?
using SBM.Core.Geometry;
using SBM.Core.Solids;

double R80 = Ft(2400), offWb = Ft(41.08), Rwb = R80 - offWb, pc80 = Sta("7597+73.11");
double Wb(string i80) { double s = Sta(i80); return s <= pc80 ? s : pc80 + (s - pc80) * Rwb / R80; }
var al = Doc.Alignment("I-80 WB");
double ftm = 0.3048;

// highest top of any solid over a plan point
double TopAt(Mesh m, Vec2 p)
{
    double best = double.NaN;
    var P = m.Positions; var I = m.Indices;
    for (int t = 0; t < I.Count; t += 3)
    {
        var a = P[I[t]]; var b = P[I[t + 1]]; var c = P[I[t + 2]];
        double d = (b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y);
        if (Math.Abs(d) < 1e-12) continue;
        double u = ((p.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (p.Y - a.Y)) / d;
        double v = ((b.X - a.X) * (p.Y - a.Y) - (p.X - a.X) * (b.Y - a.Y)) / d;
        if (u < -1e-9 || v < -1e-9 || u + v > 1 + 1e-9) continue;
        double z = a.Z + u * (b.Z - a.Z) + v * (c.Z - a.Z);
        if (double.IsNaN(best) || z > best) best = z;
    }
    return best;
}

Doc.Regenerate();
var u2def = Doc.Project.Bridges.First(b => b.Name == "I-80 WB Unit 2");
var u2 = Doc.Model("I-80 WB Unit 2");
var conc = u2.Concrete;
Print($"Unit 2 barriers: {string.Join(", ", conc.Barriers.Select(b => b.Name))}; bumps {conc.DeckBumps.Count}; deck parts {conc.DeckParts.Count}");
Print($"Barrier def: Type {u2def.Deck.Barrier.Type} H {u2def.Deck.Barrier.Height / 0.0254:F2} in base {u2def.Deck.Barrier.BaseWidth / 0.0254:F2} in top {u2def.Deck.Barrier.TopWidth / 0.0254:F2} in IsCurb {u2def.Deck.Barrier.IsCurb}");
var misc = u2.Substructure?.Misc;
Print($"Misc elements (crash cushion group): {misc?.Elements.Count(e => e.Group.ToString().Contains("Crash"))}");
foreach (var d in misc?.Diagnostics.Where(d => d.Message.Contains("end post") || d.Message.Contains("cushion")) ?? Enumerable.Empty<SBM.Core.Framing.Diagnostic>())
    Print($"  {d.Severity}: {d.Message}");
foreach (var b in Doc.Project.Bridges) Print($"  {b.Name}: barrier IsCurb {b.Deck.Barrier.IsCurb}, misc end posts {b.Misc?.EndPosts.Count ?? 0}");

void Probe(string label, string i80sta, double i80LtFt, SBM.Core.Bridge.BridgeDefinition def, SBM.Core.Project.BridgeModel m)
{
    double s = Wb(i80sta), o = -(i80LtFt - 41.08) * ftm;
    var p = al.PointAt(s, o);
    double fg = def.Deck.FinishedElevation(al, s, o);
    double deck = m.Concrete?.Deck is { } dm ? TopAt(dm, p) : double.NaN;
    double bar = double.NaN; string bn = "";
    foreach (var (n, bm) in m.Concrete?.Barriers ?? new()) { var z = TopAt(bm, p); if (!double.IsNaN(z) && (double.IsNaN(bar) || z > bar)) { bar = z; bn = n; } }
    double mi = double.NaN;
    foreach (var e in m.Substructure?.Misc?.Elements ?? new()) { var z = TopAt(e.Mesh, p); if (!double.IsNaN(z) && (double.IsNaN(mi) || z > mi)) mi = z; }
    Print($"{label,-38} I-80 {i80sta} {i80LtFt,6:F2} LT: finished grade {fg / ftm:F3}  deck top {deck / ftm:F3}  barrier {(double.IsNaN(bar) ? "-" : (bar / ftm).ToString("F3") + " " + bn)}  misc {(double.IsNaN(mi) ? "-" : (mi / ftm).ToString("F3"))}");
}
var d2 = u2def;
Probe("Ramp A end post: taper start S", "7591+08.51", 65.08 + 0.3, d2, u2);
Probe("Ramp A end post: taper start N", "7591+08.51", 68.51 - 0.3, d2, u2);
Probe("Ramp A end post: centre", "7591+14.75", 66.79, d2, u2);
Probe("Cushion strip: 7591+30 mid", "7591+30.00", 66.92, d2, u2);
Probe("Cushion strip: 7591+50 @65.08", "7591+50.00", 65.08, d2, u2);
Probe("Cushion strip: 7591+50 @68.76", "7591+50.00", 68.76, d2, u2);
Probe("Gutter WP 6A @65.08 (7591+06.76)", "7591+07.20", 65.08, d2, u2);
Probe("Gutter WP 6A @68.76 (7591+06.74)", "7591+07.20", 68.76, d2, u2);
var u3def = Doc.Project.Bridges.First(b => b.Name == "I-80 WB Unit 3");
// Ramp G end post at Pier 11WB: which unit owns that deck? try both
foreach (var (dn, dd) in new[] { ("I-80 WB Unit 2", d2), ("I-80 WB Unit 3", u3def) })
{
    var mm = Doc.Model(dn);
    Probe($"{dn}: Ramp G end post centre", "7603+21.50", 78.79, dd, mm);
    Probe($"{dn}: Ramp G taper start mid", "7603+15.50", 79.08, dd, mm);
}
