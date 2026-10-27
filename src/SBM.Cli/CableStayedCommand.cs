using System.Globalization;
using System.Text.Json;
using SBM.Api;
using SBM.Core.CableStayed;
using SBM.Core.Units;

namespace SBM.Cli;

/// <summary><c>sbm cable-stayed</c>: report of a cable-stayed bridge (geometry, stays, first-cut forces, pylon balance, clearances) and its analysis
/// wireframe (JSON / CSV) for a frame / cable program. Read-only.</summary>
public static class CableStayedCommand
{
    public const string Help = """
        sbm cable-stayed <model.sbm> [--bridge NAME] [--json analysis.json] [--nodes nodes.csv --members members.csv] [--stays stays.csv]
          Report: deck section properties and weights, every stay (anchors, length, angle, tributary length, DL / DL+LL force, required vs given area),
          pylon legs (length, self weight, stay pull), pylon base resultant (longitudinal / transverse overturning: + = toward the pylon frame +X / +Y),
          clearance targets. Forces are FIRST-CUT statics (tributary deck lengths), a starting point for the shape study; run the exported wireframe
          (nodes, members with A / Iy / Iz / J / E / density, cables, supports; world metres, SI units) in a frame / cable analysis program for design.
        """;

    public static int Run(string[] args)
    {
        if (args.Length < 2) { Console.WriteLine(Help); return 0; }
        string? Opt(string name) { int i = Array.IndexOf(args, name); return i > 0 && i + 1 < args.Length ? args[i + 1] : null; }
        var doc = SbmDocument.Open(args[1]);
        doc.Regenerate();
        var names = Opt("--bridge") is { } b ? new[] { b } : doc.Project.Bridges.Where(x => x.CableStayed != null).Select(x => x.Name).ToArray();
        if (names.Length == 0) { Console.WriteLine("No cable-stayed bridge in the model."); return 1; }
        var u = doc.Units;
        string L(double m) => u.FormatLength(m);
        string F(double n) => u.IsUS ? $"{n / 4448.2216:0.0} kip" : $"{n / 1000:0.0} kN";
        string W(double npm) => u.IsUS ? $"{npm / 4448.2216 * 0.3048:0.000} kip/ft" : $"{npm / 1000:0.00} kN/m";
        string M(double nm) => u.IsUS ? $"{nm / 4448.2216 / 0.3048:0} kip-ft" : $"{nm / 1000:0} kN-m";
        string A(double m2) => u.IsUS ? $"{m2 / (0.0254 * 0.0254):0.00} in²" : $"{m2 * 1e6:0} mm²";
        foreach (var name in names)
        {
            var m = doc.Model(name).Steel?.CableStayed;
            if (m == null) { Console.WriteLine($"{name}: not generated (see the regeneration warnings)."); continue; }
            var f = m.Forces;
            Console.WriteLine($"== {name}");
            Console.WriteLine($"Deck sta {u.FormatStation(m.DeckBegin)} - {u.FormatStation(m.DeckEnd)} ({L(m.DeckEnd - m.DeckBegin)}), section {L(m.DeckProps.Width)} x {L(m.DeckProps.Depth)}, " +
                              $"A {A(m.DeckProps.A)}, self weight {W(f.DeckSelfWeight)}, barriers {W(f.BarrierWeight)}, superimposed {W(f.SuperimposedDead)}, live {W(f.LiveLoad)}");
            foreach (var leg in m.Legs) Console.WriteLine($"Leg {leg.Name}: {L(leg.Length)} long, tip {L(leg.Points[^1].Z)} el.");
            Console.WriteLine($"Stays ({m.Stays.Count}):");
            Console.WriteLine($"  {"stay",-10} {"group",-12} {"deck sta",10} {"length",10} {"angle",6} {"trib",8} {"DL",11} {"DL+LL",11} {"req A",10} {"given A",10} ratio");
            foreach (var s in m.Stays)
            {
                var sf = f.Stays.FirstOrDefault(x => x.Stay == s.Name);
                Console.WriteLine($"  {s.Name,-10} {s.Group,-12} {(double.IsNaN(s.Station) ? "ground" : u.FormatStation(s.Station)),10} {L(s.Length),10} {s.AngleDeg,6:0.0} " +
                                  (sf == null ? "(not estimated)" : $"{L(sf.Tributary),8} {F(sf.Dead),11} {F(sf.Total),11} {A(sf.RequiredArea),10} {A(sf.Area),10} {sf.Ratio:0.00}"));
            }
            Console.WriteLine($"Pylon self weight {F(f.PylonSelfWeight)}");
            foreach (var lb in f.Legs) Console.WriteLine($"  {lb.Leg}: self weight {F(lb.SelfWeight)}, stay pull DL (x {F(Proj(lb.StayForceDead, m.PylonFrame.X))}, y {F(Proj(lb.StayForceDead, m.PylonFrame.Y))}, z {F(lb.StayForceDead.Z)})");
            Console.WriteLine($"Pylon base (stays + pylon self weight about the base point): vertical DL {F(-f.BaseForceDead.Z)} / DL+LL {F(-f.BaseForceTotal.Z)}; " +
                              $"horizontal along X DL {F(Proj(f.BaseForceDead, m.PylonFrame.X))} / DL+LL {F(Proj(f.BaseForceTotal, m.PylonFrame.X))}");
            Console.WriteLine($"  longitudinal overturning (+ = tipping toward pylon frame +X): DL {M(f.BaseMomentLongDead)}, DL+LL {M(f.BaseMomentLongTotal)}");
            Console.WriteLine($"  transverse overturning (+ = toward +Y, right): DL {M(f.BaseMomentTransDead)}, DL+LL {M(f.BaseMomentTransTotal)}");
            foreach (var c in m.Clearances) Console.WriteLine($"Clearance {c.Name}: {L(c.Provided)} at {u.FormatStation(c.AtStation)}, required {L(c.Required)} {(c.Ok ? "OK" : "NOT MET")}");
            foreach (var n in f.Notes) Console.WriteLine("Note: " + n);
            var an = m.Analysis;
            Console.WriteLine($"Analysis wireframe: {an.Nodes.Count} nodes, {an.Members.Count} members ({string.Join(", ", an.Members.GroupBy(x => x.Kind).Select(g => $"{g.Count()} {g.Key}"))}), {an.Supports.Count} supports");

            string Suffix(string path) => names.Length > 1 ? Path.Combine(Path.GetDirectoryName(path) ?? "", Path.GetFileNameWithoutExtension(path) + "-" + name + Path.GetExtension(path)) : path;
            var inv = CultureInfo.InvariantCulture;
            if (Opt("--json") is { } json)
            {
                var o = new
                {
                    bridge = name, units = "SI (m, N, Pa, kg/m3); world coordinates (UTM / project grid)",
                    nodes = an.Nodes.Select(n => new { n.Id, n.Name, x = n.P.X, y = n.P.Y, z = n.P.Z }),
                    members = an.Members.Select(x => new { x.Id, x.Name, x.Kind, x.N1, x.N2, x.Section.A, x.Section.Iy, x.Section.Iz, x.Section.J, x.Section.Width, x.Section.Depth,
                        x.E, x.Density, x.Cable, widthAxis = new[] { x.WidthAxis.X, x.WidthAxis.Y, x.WidthAxis.Z } }),
                    supports = an.Supports.Select(s => new { s.Node, s.Name, s.Fixed }),
                    loads = new { deckSelfWeight = f.DeckSelfWeight, barriers = f.BarrierWeight, superimposedDead = f.SuperimposedDead, live = f.LiveLoad },
                    stays = f.Stays.Select(s => new { s.Stay, s.Tributary, s.Dead, s.Total, s.RequiredArea, s.Area }),
                };
                File.WriteAllText(Suffix(json), JsonSerializer.Serialize(o, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"  -> {Suffix(json)}");
            }
            if (Opt("--nodes") is { } nc)
            {
                File.WriteAllLines(Suffix(nc), new[] { "id,name,x_m,y_m,z_m" }.Concat(an.Nodes.Select(n => string.Join(",", n.Id, Q(n.Name), n.P.X.ToString("R", inv), n.P.Y.ToString("R", inv), n.P.Z.ToString("R", inv)))));
                Console.WriteLine($"  -> {Suffix(nc)}");
            }
            if (Opt("--members") is { } mc)
            {
                File.WriteAllLines(Suffix(mc), new[] { "id,name,kind,n1,n2,A_m2,Iy_m4,Iz_m4,J_m4,E_Pa,density_kg_m3,cable,width_axis_x,width_axis_y,width_axis_z" }
                    .Concat(an.Members.Select(x => string.Join(",", x.Id, Q(x.Name), x.Kind, x.N1, x.N2, D(x.Section.A), D(x.Section.Iy), D(x.Section.Iz), D(x.Section.J), D(x.E), D(x.Density),
                        x.Cable ? 1 : 0, D(x.WidthAxis.X), D(x.WidthAxis.Y), D(x.WidthAxis.Z)))));
                Console.WriteLine($"  -> {Suffix(mc)}");
            }
            if (Opt("--stays") is { } sc)
            {
                File.WriteAllLines(Suffix(sc), new[] { "stay,group,leg,deck_station_m,lower_x,lower_y,lower_z,upper_x,upper_y,upper_z,length_m,angle_deg,diameter_m,area_m2,tributary_m,dead_N,total_N,required_area_m2" }
                    .Concat(m.Stays.Select(s =>
                    {
                        var sf = f.Stays.FirstOrDefault(x => x.Stay == s.Name);
                        return string.Join(",", s.Name, Q(s.Group), s.Leg, D(s.Station), D(s.Lower.X), D(s.Lower.Y), D(s.Lower.Z), D(s.Upper.X), D(s.Upper.Y), D(s.Upper.Z),
                            D(s.Length), D(s.AngleDeg), D(s.Diameter), D(s.Area), D(sf?.Tributary ?? double.NaN), D(sf?.Dead ?? double.NaN), D(sf?.Total ?? double.NaN), D(sf?.RequiredArea ?? double.NaN));
                    })));
                Console.WriteLine($"  -> {Suffix(sc)}");
            }
            string D(double v) => double.IsNaN(v) ? "" : v.ToString("R", inv);
            static string Q(string s) => s.Contains(',') ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }
        return 0;
    }

    private static double Proj(SBM.Core.Geometry.Vec3 v, SBM.Core.Geometry.Vec3 axis) => SBM.Core.Geometry.Vec3.Dot(v, axis);
}
