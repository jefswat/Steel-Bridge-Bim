// I-80 WB (Design 116) piped deck drainage to Piers 11WB and 12WB, design sheets 206-213 (PDF 212-219 / 220): the span 11WB 12" collection pipe,
// the two 8" collection pipes near Pier 12WB, the pier collection boxes on the caps and the pier pipes down the columns.
// Re-runnable, after build-i80-wb.csx and the pier / joint scripts. OPTIONAL: replaces the generic "piped to pier" pipes of these 10 drains.
//  * Span 11WB (Unit 2): 7 Type B drains 7602+44.00 .. 7603+04.00 piped to a 12" pipe 1'-8 right of CL girder A (PDF 219 sections A-A / B-B),
//    7.23%, vertical dims from the deck top at CL drain (PDF 219 "*"; the cross frame rows have no station and are not used), flexible couplers
//    4'-0 upstream of the drains (TYP.), hangers 5'-0 max. The drain tubes are at the pipe line (FromGutter set from girder A + 1'-8).
//  * Pier 11WB (end of Unit 2): deck joint trough collecting at the left (girder A side), trough box 4'-0 from the gutter line, 5.0% min (PDF 212
//    elevation), 8" pipe into the end of the 12" collector over the pier collection box (PDF 219); box on the BACK cap face 8'-7 7/8 north of
//    CL column no. 1 (PDF 217 elevation), 12" pier pipe at 8% along the face to CL column no. 1, Section C-C jog 1'-3 toward the column over 2'-2
//    starting 1 1/2" below the cap bottom (PDF 217), supports 4'-0 max (1'-0 above the cap bottom and 8 1/2" below the jog), outlet 1" above the
//    pavement EL. 981 +- (outlet detail "Pier Nos. 11WB and 12WB", 18" sleeve / collar by the roadway plans - not modeled).
//  * Near Pier 12WB (Unit 3, no joint): 8" pipes parallel to the north gutter line at the drain tube line, 3'-9 5/8 from the barrier toe (PDF 220
//    section C-C, Type A drains), from 7603+86.79 (2'-6, 1.80%) and from 7605+26.79 (2'-6, 2.45%) to the drain 7604+73.67 (3'-10 3/8 / 3'-10),
//    then the 12" drop into the pier collection box (PDF 220 section C-C); box 4'-7 3/8 north of CL column no. 1 (PDF 217), face as drawn on the
//    PDF 220 elevation (ahead); 12" pier pipe, no jog (Section C-C is cut at 5WB / 11WB only), outlet: pavement elevation NOT printed at 12WB
//    (ground line) -> terrain, flagged.
//  * Pier collection box (PDF 216): 1'-10 along the face, 2'-0 out at the top / 1'-4 at the bottom (taper over the lower 1'-3), 2'-6 deep,
//    inlet CL 1'-0 off the face (at 50 F), 4" into the box, outlet 1 1/2" clear of the face; top 1/4" min / 1" max below the TOP OF PEDESTAL (range:
//    UNRESOLVED, the minimum is modeled below the cap top). Pipe joints mitred (drainage notes PDF 218: standard elbows cut as an alternate),
//    drain pipe flanges 1'-6 OD for 12" (flange table PDF 218), cleanouts at the bends.
// NOT modeled here: Piers 5WB / 6A joint systems (PDF 216), abutment drain blocks (PDF 211), drains of other units.
using SBM.Core.Bridge;
using SBM.Core.Geometry;

double F(double ft, double inch) => Ft(ft) + In(inch);
double R80 = Ft(2400), Rwb = R80 - Ft(41.08), pc80 = Sta("7597+73.11");
double Wb(string s) { double v = Sta(s); return v <= pc80 ? v : pc80 + (v - pc80) * Rwb / R80; }
var u2 = Doc.Project.Bridges.First(b => b.Name == "I-80 WB Unit 2");
var u3 = Doc.Project.Bridges.First(b => b.Name == "I-80 WB Unit 3");
foreach (var u in new[] { u2, u3 })
{
    u.Misc ??= new MiscItemsDefinition();
    u.Misc.Collectors.RemoveAll(c => c.Name.StartsWith("I-80 "));
    u.Misc.PierDrains.RemoveAll(p => p.Support is "Pier 11WB" or "Pier 12WB");
}
const string C11 = "I-80 span 11WB collection pipe", C12B = "I-80 Pier 12WB collection pipe (back)", C12A = "I-80 Pier 12WB collection pipe (ahead)";

// ---- span 11WB collector (Unit 2)
var pts11 = new (string sta, double ft, double inch)[] { ("7602+44.00", 2, 9), ("7602+54.00", 3, 4.75), ("7602+64.00", 4, 0.5), ("7602+74.00", 4, 8.25), ("7602+84.00", 5, 4), ("7602+94.00", 5, 11.75), ("7603+04.00", 6, 7.5) };
var c11 = new DrainCollectorDefinition
{
    Name = C11, NominalDiameter = In(12), ReferenceGirder = 0, OffsetFromGirder = F(1, 8), Slope = 0.0723,
    StartStation = Wb("7602+44.00"), EndSupport = "Pier 11WB", HangerSpacing = Ft(5),
};
foreach (var (sta, ft, inch) in pts11) c11.Points.Add(new CollectorPoint { Station = Wb(sta), DeckTopToCl = F(ft, inch), Note = "drain " + sta });
foreach (var (sta, _, _) in pts11.Skip(1)) c11.CouplerStations.Add(Wb(sta) - Ft(4));
u2.Misc!.Collectors.Add(c11);

// ---- collectors near Pier 12WB (Unit 3)
var c12b = new DrainCollectorDefinition
{
    Name = C12B, NominalDiameter = In(8), ReferenceGirder = 0, FromBarrierToe = F(3, 9.625), Slope = 0.018,
    StartStation = Wb("7603+86.79"), EndStation = Wb("7604+73.67"), EndSupport = "Pier 12WB", HangerSpacing = Ft(5),
    Points = { new() { Station = Wb("7603+86.79"), DeckTopToCl = F(2, 6), Note = "drain 7603+86.79" }, new() { Station = Wb("7604+73.67"), DeckTopToCl = F(3, 10.375), Note = "drain 7604+73.67" } },
};
var c12a = new DrainCollectorDefinition
{
    Name = C12A, NominalDiameter = In(8), ReferenceGirder = 0, FromBarrierToe = F(3, 9.625), Slope = 0.0245,
    StartStation = Wb("7605+26.79"), EndStation = Wb("7604+73.67"), EndSupport = "Pier 12WB", HangerSpacing = Ft(5),
    Points = { new() { Station = Wb("7605+26.79"), DeckTopToCl = F(2, 6), Note = "drain 7605+26.79" }, new() { Station = Wb("7604+73.67"), DeckTopToCl = F(3, 10), Note = "drain 7604+73.67" } },
};
u3.Misc!.Collectors.Add(c12b);
u3.Misc.Collectors.Add(c12a);

// ---- drains -> collectors (inlet on the pipe line)
var (toe, _, _) = SBM.Core.Concrete.BarrierProfiles.PlanLines(u2.Deck.Barrier);
var al = Doc.Alignment("I-80 WB");
var gA2 = Doc.Model(u2.Name).Framing.Girders[0];
double GirderAOffset(SBM.Core.Framing.GirderResult g, double sta, double right)
{
    double best = 1e9, sb = 0;
    for (double q = 0; q < g.Length; q += 0.02) { double d = Math.Abs(al.Inverse(g.Path.At(q).point.XY).Station - sta); if (d < best) { best = d; sb = q; } }
    for (double q = sb - 0.05; q < sb + 0.05; q += 0.0005) { double d = Math.Abs(al.Inverse(g.Path.At(q).point.XY).Station - sta); if (d < best) { best = d; sb = q; } }
    var (p, t, _) = g.Path.At(sb);
    return al.Inverse(p.XY + t.Right * right).Offset;
}
foreach (var d in u2.Drains.Where(d => d.Outlet == DrainOutlet.PipedToPier))
{
    d.Collector = C11;
    double off = GirderAOffset(gA2, d.Station, F(1, 8));
    d.FromGutter = off - (u2.Deck.LeftAt(d.Station) + toe);       // PDF 219 section B-B: CL drain tube 1'-8 from CL girder A
}
foreach (var d in u3.Drains.Where(d => d.Outlet == DrainOutlet.PipedToPier))
{
    d.Collector = d.Station > Wb("7604+80.00") ? C12A : C12B;
    d.FromGutter = F(3, 9.625);                                       // PDF 220 section C-C: CL drain tube 3'-9 5/8 from the barrier toe
}

// ---- pier systems
PierDrainDefinition Box(string support) => new()
{
    Support = support, CapBoxFace = CapFaceSide.Back, ColumnNumber = 1, CapBoxAlongFace = F(1, 10), CapBoxOutTop = F(2, 0), CapBoxOutBottom = F(1, 4),
    CapBoxDepth = F(2, 6), CapBoxTaperHeight = F(1, 3), CapBoxInletFromFace = F(1, 0), CapBoxInletEmbed = In(4), CapBoxOutletClear = In(1.5),
    CapPipeNominalDiameter = In(12), CapPipeSlope = 0.08, SupportSpacing = Ft(4), Outlet = PierDrainOutlet.Pavement,
    FlangeDiameter = F(1, 6), Fittings = true,
};
var p11 = Box("Pier 11WB");
p11.CapBoxFromColumn = -F(8, 7.875);
p11.CollectRight = false; p11.BoxFromGutter = F(4, 0); p11.TroughSlope = 0.05; p11.PipeNominalDiameter = In(8);
p11.Collector = C11; p11.PavementElevation = Ft(981);
p11.JogOffset = F(1, 3); p11.JogRise = F(2, 2); p11.JogBelowCap = In(1.5); p11.JogSupportAbove = F(1, 0); p11.JogSupportBelow = In(8.5);
u2.Misc.PierDrains.Add(p11);
var p12 = Box("Pier 12WB");
p12.NoTrough = true; p12.CapBoxFace = CapFaceSide.Ahead; p12.CapBoxFromColumn = -F(4, 7.375);
p12.Collector = C12B + ";" + C12A;
u3.Misc.PierDrains.Add(p12);
u3.Misc.Unresolved.RemoveAll(s => s.StartsWith("[drain]"));
u3.Misc.Unresolved.Add("[drain] UNRESOLVED (I-80 PDF 217): Pier 12WB pier pipe outlet - the elevation shows a ground line and the outlet detail (Piers 11WB and 12WB) ends the 12\" pipe 1\" above the pavement, but no pavement elevation is printed at 12WB; the pipe ends 1\" above the terrain.");
u3.Misc.Unresolved.Add("[drain] UNRESOLVED (I-80 PDF 220): couplers / L3x3x3/8 supports of the 8\" collection pipes near Pier 12WB are dimensioned from cross frames that carry no station (4'-0); not modeled.");

var res = Doc.Regenerate();
Print($"Regenerated {res.Bridges} bridges in {res.Elapsed.TotalSeconds:F1} s, {res.Errors.Count} errors.");
foreach (var u in new[] { u2, u3 })
{
    var misc = Doc.Substructure(u.Name).Misc;
    if (misc == null) { Print($"{u.Name}: no misc model"); continue; }
    Print($"{u.Name}: {misc.Elements.Count(e => e.Group == SBM.Core.Misc.MiscGroup.Drainage)} drainage elements");
    foreach (var c in misc.Clearances.Where(c => c.Clearance <= 0)) Print($"   CONFLICT {c.Drain} / {c.Element}: {Doc.Units.FormatLength(c.Clearance)} to {c.Against}");
    foreach (var d in misc.Diagnostics.Where(d => d.Severity != SBM.Core.Framing.DiagnosticSeverity.Info && (d.Message.Contains("rain") || d.Message.Contains("ollect")))) Print($"   {d.Severity}: {d.Message}");
}
Doc.Save();
