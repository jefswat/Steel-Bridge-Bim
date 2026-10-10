// Cross frames of Ramp G as designed: frame types from the cross frame details (Design 316 sheets 102-103) and frame
// lines from the framing plans (sheets 88-89, 93, 95-96, 99-100). Re-runnable; run after build-ramp-g.csx.
//  * Lines radial to the CL Ramp G at "cross frame spacing along girder A"; each span's string is anchored at girder A's
//    CL bearing as it is in the model (the model's span lengths along A differ from the plan's girder data by 0.2-0.4 ft,
//    and at Span 16G by 2.3 ft - see the open issue), so frames keep their plan distances from the bearings.
//  * Defaults: Type 1; Type 5 plate diaphragms on the 3rd line of Span 1G; Types 2/3 through the skewed straddle bents
//    16G and 17G and before the skewed Pier 19G end; Type 4 end frames at the unit ends; abutment frames at the South Abut.
//  * Bolts 7/8" in 15/16" holes (sheet 88 notes), two columns 3" apart, 1 1/2" edges, 3" pitch.
// Assumed / approximated: tie plate length 10"; top gusset bolt rows (2); abutment frame W24x68 top 4" below the top of
// web and its WT5x15 bottom chord (not in the catalog - reported, a chord angle is used); the Pier 17G zone frames are on
// the A string with Type 2 / 3 as read where legible (zone partly approximate).
using SBM.Core.Bridge;

double I(double inch) => In(inch);
CrossFrameStyle Frame(string name, string top, string diag, string bot, double gusset, int rows, double topWp, double botWp,
    int topRows = 2, bool tiePlates = false, CrossFrameStyleKind kind = CrossFrameStyleKind.InvertedK) => new()
{
    Name = name, Kind = kind, TopChord = top, Diagonal = diag, BottomChord = bot, GussetThickness = I(gusset),
    BoltDiameter = I(0.875), BoltColumns = 2, BoltGage = I(3), BoltEdgeDistance = I(1.5), BoltPitch = I(3),
    BoltsPerConnection = rows, TopBoltRows = topRows, TopWorkPointOffset = I(topWp), BottomWorkPointOffset = I(botWp),
    TiePlateThickness = I(0.5), TiePlateLength = I(10), TiePlateOffsets = tiePlates ? new() { -I(16), I(16) } : new(),
    // Lowest bottom bolt 3" above the bottom flange ("3 (TYP.)" on the details); the group grows upward from there.
    BottomBoltOffset = I(Math.Max(0, 3 + (rows - 1) * 3.0 / 2 - botWp)),
};
List<CrossFrameStyle> Styles(BridgeDefinition b)
{
    // Type 4 end frames: the W10x33 bolt group starts 2'-2 below the top of slab.
    double slabToWeb = (b.Deck.Thickness + b.Deck.Haunch) / In(1);
    return new()
    {
        Frame("1", "L6x6x1/2", "L6x6x1/2", "2-MC6x15.3", 0.5, 4, 6, 6),
        Frame("2", "L5x5x7/16", "L7x4x5/8", "MC8x20", 0.625, 5, 6, 8, tiePlates: true),
        Frame("3", "L6x6x7/16", "L6x4x7/8", "MC10x25", 0.625, 7, 6, 10, tiePlates: true),
        Frame("4", "W10x33", "L5x5x3/8", "2-MC6x15.3", 0.5, 4, 26 + 3 - slabToWeb, 6, topRows: 3),
        Frame("ABUT", "W24x68", "L5x5x3/8", "WT5x15", 0.5, 2, 4 + 23.7 / 2, 6, topRows: 7, kind: CrossFrameStyleKind.KFrame),
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
    // Pier 16G zone (sheet 99): 3 spaces of about 14'-7 9/16 from A's bearing.
    double z = F(43, 10.625) / 3;
    x += z; L(x, null, All("3"));
    x += z; L(x, null, (0, "2"), (1, "3"), (2, "3"));
    x += z; L(x, null, All("2"));
    Run(1, F(21, 9.625));
    Run(9, F(193, 4.0625) / 9);
    // Pier 17G zone (sheet 100): mixed Types 2 / 3 (approximate).
    Run(1, F(21, 11.25), _ => new[] { (0, "3"), (1, "3"), (2, "2") });
    Run(1, F(10, 7.6875), _ => new[] { (0, "2"), (1, "2"), (2, "2") });
    Run(1, F(22, 5), _ => new[] { (0, "2"), (1, "3"), (2, "3") });
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
