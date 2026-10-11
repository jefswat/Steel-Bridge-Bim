// Cross frames of Ramp G as designed: frame types from the cross frame details (Design 316 sheets 102-103) and frame
// lines from the framing plans (sheets 88-89, 93, 95-96, 99-100). Re-runnable; run after build-ramp-g.csx.
//  * Lines radial to the CL Ramp G at "cross frame spacing along girder A"; each span's string is anchored at girder A's
//    CL bearing as it is in the model (the model's span lengths along A differ from the plan's girder data by 0.2-0.4 ft,
//    and at Span 16G by 2.3 ft - see the open issue), so frames keep their plan distances from the bearings.
//  * Defaults: Type 1; Type 5 plate diaphragms on the 3rd line of Span 1G; Types 2/3 through the skewed straddle bents
//    16G and 17G and before the skewed Pier 19G end; Type 4 end frames at the unit ends; abutment frames at the South Abut.
//  * Bolts 7/8" in 15/16" holes (sheet 88 notes), two columns 3" apart, 1 1/2" edges, 3" pitch.
// Confirmed on the plans (docs/plan-data/cross-frame-gaps.md sections 4-5): top gusset bolts 2 rows x 2 columns (gage 3, edge 1 1/2);
// the abutment frame W24x68 top is 4" below the TOP OF THE TOP FLANGE (not the top of web): the work point is (4" - tf) + 23.7"/2
// below the top of web, tf = the girder's top flange thickness at the abutment end (read from the model, printed below); the Pier 17G
// zone = one frame line through EACH girder's CL bearing (L1 A, L2 B, L3 C, L4 D) plus the regular line 22'-5 ahead, with the types
// of the plan (the earlier version had the types one line late and no B line).
// NOT dimensioned (UNRESOLVED, nothing invented, raised as warnings by the generator through CrossFrameStyle.Unresolved): tie plate
// length (the model's 10" stays), the abutment frame's WT5x15 bottom bolt row pitch, the lengthwise stud pitch of the studs on the
// W top struts (studs are not modeled). The Pier 16G zone (sheet 98) is also one line per girder bearing (types read from the plan).
using SBM.Core.Bridge;

double I(double inch) => In(inch);
CrossFrameStyle Frame(string name, string top, string diag, string bot, double gusset, int rows, double topWp, double botWp,
    int topRows = 2, bool tiePlates = false, CrossFrameStyleKind kind = CrossFrameStyleKind.InvertedK, string[]? unresolved = null) => new()
{
    Name = name, Kind = kind, TopChord = top, Diagonal = diag, BottomChord = bot, GussetThickness = I(gusset),
    BoltDiameter = I(0.875), BoltColumns = 2, BoltGage = I(3), BoltEdgeDistance = I(1.5), BoltPitch = I(3),
    BoltsPerConnection = rows, TopBoltRows = topRows, TopWorkPointOffset = I(topWp), BottomWorkPointOffset = I(botWp),
    TiePlateThickness = I(0.5), TiePlateLength = I(10), TiePlateOffsets = tiePlates ? new() { -I(16), I(16) } : new(),
    // Lowest bottom bolt 3" above the bottom flange ("3 (TYP.)" on the details); the group grows upward from there.
    BottomBoltOffset = I(Math.Max(0, 3 + (rows - 1) * 3.0 / 2 - botWp)),
    Unresolved = (tiePlates ? new[] { "tie plate length is not dimensioned (Ramp G Types 2, 3): the model keeps 10 in; a length scaled from the elevation is NOT used" } : Array.Empty<string>())
        .Concat(unresolved ?? Array.Empty<string>()).ToList(),
};
const string StudNote = "shear studs on the W top strut (2 across) are not modeled: their lengthwise pitch / number per strut is not dimensioned";
double TopFlangeAtStart(BridgeDefinition b)
{
    // Thickness (in) of the top flange plate at the start of the bridge: as-designed layout run if the girder has one, else the plate changes.
    var ts = new List<double>();
    for (int g = 0; g < b.Girders.Count; g++)
    {
        var lay = b.Girders.Section.Layouts.FirstOrDefault(l => l.Girders.Contains(g));
        var runs = lay?.Runs.Where(r => r.Plate == GirderPlate.TopFlange).ToList();
        ts.Add(runs is { Count: > 0 } ? runs[0].Thickness / In(1) : b.Girders.Section.SegmentAt(GirderPlate.TopFlange, b.Supports[0].Station - 1e-6).Thickness / In(1));
    }
    if (ts.Max() - ts.Min() > 1e-6) Print($"  WARNING: the top flange at the abutment end differs between girders ({string.Join(", ", ts.Select(t => t.ToString("F3")))} in); girder A used");
    return ts[0];
}
List<CrossFrameStyle> Styles(BridgeDefinition b)
{
    // Type 4 end frames: the W10x33 bolt group starts 2'-2 below the top of slab.
    double slabToWeb = (b.Deck.Thickness + b.Deck.Haunch) / In(1);
    // Abutment frame: W24x68 top 4" below the top of the top flange; tf = top flange thickness at the abutment end of girder A..D.
    double tf = b.Supports[0].Type == SupportType.Abutment ? TopFlangeAtStart(b) : 0;
    if (tf > 0) Print($"{b.Name}: top flange at the abutment end {tf:F4} in -> W24x68 work point {(4 - tf) + 23.7 / 2:F4} in below the top of web");
    return new()
    {
        Frame("1", "L6x6x1/2", "L6x6x1/2", "2-MC6x15.3", 0.5, 4, 6, 6),
        Frame("2", "L5x5x7/16", "L7x4x5/8", "MC8x20", 0.625, 5, 6, 8, tiePlates: true),
        Frame("3", "L6x6x7/16", "L6x4x7/8", "MC10x25", 0.625, 7, 6, 10, tiePlates: true),
        Frame("4", "W10x33", "L5x5x3/8", "2-MC6x15.3", 0.5, 4, 26 + 3 - slabToWeb, 6, topRows: 3,
              unresolved: new[] { StudNote + " (Section B-B: 1 1/2 in from the edge)" }),
        Frame("ABUT", "W24x68", "L5x5x3/8", "WT5x15", 0.5, 2, (4 - tf) + 23.7 / 2, 6, topRows: 7, kind: CrossFrameStyleKind.KFrame,
              unresolved: new[] { StudNote + " (Section A-A: 2 in from the edge)", "pitch of the 2 bottom bolt rows on the WT5x15 chord is not dimensioned (the common 3 in pitch is kept)" }),
        new() { Name = "5", Kind = CrossFrameStyleKind.PlateDiaphragm, WebPlateThickness = I(0.5), FlangePlateWidth = I(12), FlangePlateThickness = I(0.75),
                DiaphragmBolts = 12, DiaphragmBoltPitch = 0, DiaphragmBoltEnd = I(3), BoltDiameter = I(0.875), TopWorkPointOffset = I(6), BottomWorkPointOffset = I(6) },
    };
}

List<CrossFrameLine> lines = new();
double x = 0;
double[] brg = Array.Empty<double>();
void L(double ft, int[]? bays = null, params (int bay, string type)[] types)
{
    var l = new CrossFrameLine { Girder = 0, Distance = Ft(ft), Bays = bays?.ToList() };
    foreach (var (b, t) in types) l.BayStyles[b] = t;
    lines.Add(l);
}
void Run(int n, double spacingFt, Func<int, (int, string)[]>? types = null)
{
    for (int k = 1; k <= n; k++) { x += spacingFt; L(x, null, types?.Invoke(k) ?? Array.Empty<(int, string)>()); }
}
void At(int support) => x = brg[support];                 // re-anchor at girder A's bearing (model)
void DropLast() => lines.RemoveAt(lines.Count - 1);        // the string's last space ends at a support line
(int, string)[] All(string t) => new[] { (0, t), (1, t), (2, t) };
(int, string)[] None(int _) => Array.Empty<(int, string)>();
double F(int ft, double inch) => ft + inch / 12;

void Apply(string name, Action build, Dictionary<string, string> supportStyles)
{
    var b = Doc.Project.Bridges.First(z => z.Name == name);
    var f = Doc.Model(name).Framing;
    var gi = f.Girders[0];
    int n = gi.Spans.Count;
    double s0 = gi.DistanceAt(0, 0);
    brg = Enumerable.Range(0, n + 1).Select(j => (gi.DistanceAt(Math.Min(j, n - 1), j == n ? 1 : 0) - s0) / Ft(1)).ToArray();
    lines = new(); x = 0;
    build();
    b.CrossFrames.Styles = Styles(b);
    b.CrossFrames.IntermediateStyle = "1";
    b.CrossFrames.Lines = lines;
    b.CrossFrames.SupportStyles = supportStyles;
    Print($"{name}: {lines.Count} frame lines; girder A bearings (model) {string.Join(", ", brg.Select(v => v.ToString("F2")))}");
}

Apply("Ramp G Unit 1", () =>
{
    Run(4, F(24, 7), k => k == 3 ? All("5") : None(k)); Run(2, F(23, 4)); DropLast();
    At(1); Run(3, F(23, 4)); Run(2, 25); Run(3, F(23, 4)); DropLast();
    At(2); Run(3, F(18, 5)); Run(4, 20); Run(3, F(55, 2.75) / 3); DropLast();
    At(3); Run(3, F(55, 2.8125) / 3); Run(4, F(19, 11)); Run(3, F(18, 8)); DropLast();
    At(4); Run(7, F(18, 8));
}, new() { ["South Abut"] = "ABUT", ["Pier 1G"] = "1", ["Pier 2G"] = "1", ["Pier 3G"] = "1", ["Pier 4G"] = "1", ["Pier 5G"] = "4" });

Apply("Ramp G Unit 2", () =>
{
    Run(1, F(19, 8.9375)); Run(5, F(19, 8)); Run(3, F(18, 9)); DropLast();
    At(1); Run(3, F(18, 9)); Run(5, F(20, 3.625)); Run(3, F(18, 9)); DropLast();
    At(2); Run(3, F(18, 6)); Run(5, F(91, 11.5) / 5); Run(3, F(18, 6)); DropLast();
    At(3); Run(3, F(18, 9)); Run(4, F(20, 7));
}, new() { ["Pier 5G"] = "4", ["Pier 6G"] = "1", ["Pier 7G"] = "1", ["Pier 8G"] = "1", ["Pier 9G"] = "4" });

Apply("Ramp G Unit 3", () =>
{
    Run(1, F(20, 2.375)); Run(6, 20.5); Run(3, F(18, 9)); DropLast();
    At(1); Run(3, F(18, 9)); Run(7, F(143, 8.5) / 7); Run(3, F(18, 9)); DropLast();
    At(2); Run(3, F(18, 9)); Run(5, F(21, 8.5)); Run(3, F(18, 9)); DropLast();
    At(3); Run(3, F(18, 9)); Run(5, F(22, 8.5625)); Run(3, F(18, 9)); DropLast();
    At(4); Run(3, F(18, 9)); Run(5, F(20, 6.5));
}, new() { ["Pier 9G"] = "4", ["Pier 10G"] = "1", ["Pier 11G"] = "1", ["Pier 12G"] = "1", ["Pier 13G"] = "1", ["Pier 14G"] = "4" });

Apply("Ramp G Unit 4", () =>
{
    Run(1, F(21, 0.5)); Run(7, F(20, 9)); DropLast();
    // Span 16G ends at the skewed straddle bent 16G: the 8th space ends at A's bearing, a Type 2 line.
    // The model's Span 16G is 2.26 ft longer along A than the plan's girder data (open issue): the line on A's bearing is
    // anchored at the bearing, so the last regular space takes the difference.
    At(1); Run(1, F(21, 4.5625)); Run(7, F(21, 2.75));
    At(2); L(x, null, All("2"));
    // Pier 16G zone (sheet 98, PDF 100): "3 SPA @ ABT. 14'-7 9/16 = 43'-10 5/8" along A and along D = one radial line through EACH girder's
    // CL bearing (A above, then B, C, D; the model's radials through B, C, D cross A at +14.629, +29.258, +43.887 ft and D at -29.258,
    // -14.629: the plan's "about" spaces to 0.002 ft). Types (circle marks both sides of each frame): A (2,2,2), B (3,3,2), C (2,3,3), D (2,2,2).
    {
        var fr = Doc.Model("Ramp G Unit 4").Framing;
        double BrgG(int g) => (fr.Girders[g].DistanceAt(2, 0) - fr.Girders[g].DistanceAt(0, 0)) / Ft(1);
        void PL(int g, params (int, string)[] types)
        {
            var l = new CrossFrameLine { Girder = g, Distance = Ft(BrgG(g)) };
            foreach (var (bb, t) in types) l.BayStyles[bb] = t;
            lines.Add(l);
        }
        PL(1, (0, "3"), (1, "3"), (2, "2"));
        PL(2, (0, "2"), (1, "3"), (2, "3"));
        PL(3, All("2"));
        Print($"  16G: bearings along B/C/D {BrgG(1):F3}/{BrgG(2):F3}/{BrgG(3):F3} ft; A's at {x:F3}");
    }
    x += F(43, 10.625);                                     // A string: the 3 spaces end on the D line
    Run(1, F(21, 9.625));
    // 9 spaces to A's bearing at the skewed bent 17G = L1 of the pier zone (sheet 99, PDF 101): A-B Type 3, B-C Type 3, C-D Type 2.
    Run(9, F(193, 4.0625) / 9, k => k == 9 ? new[] { (0, "3"), (1, "3"), (2, "2") } : None(k));
    // Pier 17G zone (sheet 99, PDF 101): a frame line through EACH girder's CL bearing (radial to Ramp G). L1 = A (above); L2 = B's bearing
    // (B-C Type 2, new); L3 = C's bearing = A + 21'-11 1/4 (A-B, C-D Type 2); L4 = D's bearing = + 10'-7 11/16 (A-B 2, B-C 3, C-D 3);
    // L5 regular = + 22'-5 (C-D Type 2, others Type 1).
    {
        var fr = Doc.Model("Ramp G Unit 4").Framing;
        var gb = fr.Girders[1];
        double bAtL2 = (gb.DistanceAt(3, 0) - gb.DistanceAt(0, 0)) / Ft(1);
        Print($"  L2: B's CL bearing at 17G is {bAtL2:F3} ft along B (report 627.183); A bearing used {x:F3} (report 616.529)");
        lines.Add(new CrossFrameLine { Girder = 1, Distance = Ft(bAtL2), Bays = new() { 1 }, BayStyles = { [1] = "2" } });
    }
    x += F(21, 11.25);   L(x, new[] { 0, 2 }, (0, "2"), (2, "2"));
    x += F(10, 7.6875);  L(x, null, (0, "2"), (1, "3"), (2, "3"));
    x += F(22, 5);       L(x, null, (2, "2"));
    Run(7, F(154, 7.1875) / 7); DropLast();
    At(4); Run(2, F(45, 1.8125) / 2); Run(4, F(96, 10.1875) / 4, k => k == 4 ? All("2") : None(k));
}, new() { ["Pier 14G"] = "4", ["Pier 15G"] = "1", ["Pier 16G"] = "none", ["Pier 17G"] = "none", ["Pier 18G"] = "1", ["Pier 19G"] = "4" });

var res = Doc.Regenerate();
Print($"Regenerated {res.Bridges} bridges in {res.Elapsed.TotalSeconds:F1} s, {res.Errors.Count} errors.");
foreach (var e in res.Errors) Print("  ERROR " + e);
foreach (var name in new[] { "Ramp G Unit 1", "Ramp G Unit 2", "Ramp G Unit 3", "Ramp G Unit 4" })
{
    var f = Doc.Model(name).Framing;
    Print($"{name}: {f.CrossFrames.Count} frames: " + string.Join(", ", f.CrossFrames.GroupBy(c => c.Style == "" ? "(default)" : c.Style).OrderBy(g => g.Key).Select(g => $"{g.Count()} x {g.Key}")));
    foreach (var d in f.Diagnostics.Concat(Doc.Model(name).Steel!.Diagnostics).Where(d => d.Message.Contains("rame") || d.Message.Contains("CF")).Take(12))
        Print("  " + d.Message);
}
Doc.Save();
