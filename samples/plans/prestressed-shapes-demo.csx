// Prestressed state-standard shapes demo: small TEST bridges (not the plan bridges) for the standard shape catalog,
// standard-length beams per span, harped strands by grid column and strand debonding. Re-runnable (replaces its own
// alignment and bridges):
//   sbm new  "temp\ps-demo.sbm"
//   sbm run  "samples\plans\prestressed-shapes-demo.csx" "temp\ps-demo.sbm"        (run from the repo root)
//
//   1. "Demo IA BTD55"      one span, CL-CL bearing 55'-0" = the BTD55 standard (Woodbury Design 817 p.27-28): no check warning.
//   2. "Demo IA BTD60/105"  two spans with per-span standard beams BTD60 / BTD105 (p.29 / p.30, deflected strands at grid columns,
//                           hold-downs 6'-0" / 10'-6" from midspan); span 2 is laid out 1" longer than the standard, so the
//                           generator reports the CHECK and - placement not set - an UNRESOLVED warning (beam NOT stretched).
//   3. "Demo MN 30MH"       one span 99'-3", MnDOT 30MH with the Br02069 strand pattern and debonding (A 4 @ 9'-0",
//                           B 6 @ 13'-0", C 4 @ 17'-0" each end; docs/plan-data/br02069-th65/strands-30MH-B1.csv, p16).
// Section properties are computed from the exact outlines (true radius fillets) and compared with the printed values.
using System.IO;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Prestressed;

string DataDir()
{
    for (var d = new DirectoryInfo(Environment.CurrentDirectory); d != null; d = d.Parent)
    {
        var p = Path.Combine(d.FullName, "docs", "plan-data", "br02069-th65");
        if (Directory.Exists(p)) return p;
    }
    throw new DirectoryNotFoundException("docs/plan-data/br02069-th65 not found above the current directory - run from the repo.");
}

// 9'-0" -> meters
double FtIn(string s)
{
    s = s.Trim().Trim('"');
    var parts = s.Split('\'');
    return Ft(double.Parse(parts[0])) + (parts.Length > 1 && parts[1].Trim('-', ' ', '"').Length > 0 ? In(double.Parse(parts[1].Trim('-', ' ', '"'))) : 0);
}

// ---------------------------------------------------------------- shapes
foreach (var s in StandardBeamCatalog.StateShapes)
{
    int arcs = s.Profile!.Segments.Count(g => g.IsArc);
    Print($"{s.Designation}: {s.Profile.Segments.Count} segments ({arcs} arcs), A {s.Area:F2} in2, yb {s.Yb:F3} in, I {s.Ix:F0} in4, {s.WeightPlf:F0} plf  [{s.Source}]");
    foreach (var r in StandardBeamCatalog.PropertyCheck(s))
        Print($"   {r.Property}: computed {r.Computed:F3}, printed {r.Printed:F3}, difference {r.Difference:+0.000;-0.000} ({r.Difference / r.Printed * 100:+0.00;-0.00} %)");
}

// ---------------------------------------------------------------- alignment
const string AlName = "PS demo";
Doc.Project.Alignments.RemoveAll(a => a.Name == AlName);
var al = Doc.AddAlignment(AlName, Ft(1000), new[] { new PiRow(0, 0), new PiRow(Ft(2000), 0) });
al.Profile = new VerticalProfile { Vpis = { new Vpi(al.StartStation, Ft(100)), new Vpi(al.EndStation, Ft(100) + 0.01 * (al.EndStation - al.StartStation)) } };

BridgeDefinition NewBridge(string name, params (string name, SupportType type, double station)[] sups)
{
    Doc.Project.Bridges.RemoveAll(x => x.Name == name);
    var b = new BridgeDefinition { Name = name, AlignmentName = AlName };
    b.FieldSplices.Clear();
    b.Studs.Enabled = false;
    foreach (var (n, t, st) in sups)
        b.Supports.Add(new SupportDefinition { Name = n, Type = t, Station = st, SkewDegrees = 0, Substructure = SubstructureDefinition.Default(t) });
    b.Girders.Count = 4;
    b.Girders.FirstGirderOffset = -Ft(10.5);
    b.Girders.Spacings = Enumerable.Repeat(Ft(7), 3).ToList();
    b.Girders.Kind = GirderKind.TxDotPrestressed;
    b.Deck.LeftEdgeOffset = -Ft(14);
    b.Deck.RightEdgeOffset = Ft(14);
    b.Deck.Reinforcement.Enabled = false;
    Doc.Project.Bridges.Add(b);
    return b;
}

// 1. Iowa BTD55 at its standard length (the BTD standard's 8" end of beam to CL bearing).
var b1 = NewBridge("Demo IA BTD55", ("Abut 1", SupportType.Abutment, Ft(1100)), ("Abut 2", SupportType.Abutment, Ft(1155)));
b1.Girders.Prestressed = StandardBeamCatalog.Beam("BTD55")!;
// Pads: no pad size entered (not part of the standard beam) -> UNRESOLVED warning, IGEB placeholder pad.

// 2. Per-span standard beams.
// Layout: CL brg at the pier 3" + 8" (EndGapAtBent + BearingToEnd) from the CL pier; span 1 = 60'-0", span 2 = 105'-1".
var b2 = NewBridge("Demo IA BTD60/105", ("Abut 1", SupportType.Abutment, Ft(1300)), ("Pier 1", SupportType.Pier, Ft(1360) + In(11)),
    ("Abut 2", SupportType.Abutment, Ft(1360) + In(22) + Ft(105) + In(1)));
var p2 = StandardBeamCatalog.Beam("BTD60")!;
p2.SpanBeams = new() { new PrestressedSpanBeam { Spans = new() { 1 }, Beam = StandardBeamCatalog.Beam("BTD105")! } };
// Placement of a standard beam between bearings that differ from its standard length: NOT set (user decision pending).
b2.Girders.Prestressed = p2;

// 3. MnDOT 30MH with the Br02069 strands and debonding.
var b3 = NewBridge("Demo MN 30MH", ("Abut 1", SupportType.Abutment, Ft(1500)), ("Abut 2", SupportType.Abutment, Ft(1599.25)));
var strands = new List<StrandPoint>();
foreach (var r in File.ReadAllLines(Path.Combine(DataDir(), "strands-30MH-B1.csv")).Skip(1).Where(l => l.Trim().Length > 0).Select(l => l.Split(',')))
{
    var sp = new StrandPoint { X = In(double.Parse(r[2])), Y = In(double.Parse(r[3])), YEnd = In(double.Parse(r[3])) };
    if (r[4].Trim().Length > 0) { sp.DebondSymbol = r[4].Trim(); sp.DebondStart = sp.DebondEnd = FtIn(r[5]); }
    strands.Add(sp);
}
b3.Girders.Prestressed = new PrestressedBeamDefinition
{
    Standard = "MN 30MH", Designation = "30MH", StrandLayout = strands, StrandCount = strands.Count,
    StrandDiameter = In(0.6), StrandStrength = 300, ConcreteStrength = 9.5, ReleaseStrength = 8.0,
    BearingToEnd = In(7.5), PadLength = In(12), PadWidth = In(30), PadThickness = In(3.25),
    Source = "Br02069 PDF p.16 (30MH-101)",
};

// ---------------------------------------------------------------- regenerate and report
var res = Doc.Regenerate();
Print($"Regenerated {res.Bridges} bridge(s), {res.Errors.Count} errors, {res.Warnings.Count} warnings.");
foreach (var e in res.Errors) Print("  ERROR " + e);
foreach (var name in new[] { b1.Name, b2.Name, b3.Name })
{
    var ps = Doc.Prestressed(name);
    Print($"== {name}");
    foreach (var bm in ps.Beams.Where(x => x.Girder == 0))
        Print($"  {bm.Mark} {bm.TypeName}: L {bm.Length / Ft(1):F3} ft, CL-CL brg {bm.BearingSpan / Ft(1):F3} ft (layout {bm.LayoutBearingSpan / Ft(1):F3}), " +
              $"{bm.Strands.Count} strands ({bm.Strands.Count(s => s.Depressed)} deflected, {bm.Strands.Count(s => s.Debonded)} debonded), exact body {(bm.Body != null ? "yes" : "no")}");
    Print($"  debonded strand length {ps.DebondedLength / Ft(1):F1} ft");
    foreach (var d in ps.Diagnostics) Print($"  {d.Severity}: {d.Message}");
}
Doc.Save();
Print($"Saved {Doc.FilePath}");
