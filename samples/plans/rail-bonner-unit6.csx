// NCDOT B-2500 Bonner Unit 6 - 2 BAR METAL RAIL on the concrete parapets (generic pedestrian railing input), same section as Unit 5
// (misc-bonner-unit5.csx). Called by build-bonner-unit6.csx after joints-parapet-bonner-unit6.csx (deck ends / end posts); re-runnable.
//   sbm run samples\plans\rail-bonner-unit6.csx "<model.sbm>"
// Plan data (p. = PDF page):
//   p.37-38 (S0-36 / S0-37) 2 bar metal rail (aluminum alternate): post 1'-11 1/2" on a 1/2" base, 5 3/4" flange, 4 1/4" deep at the base; base 7 1/2" x 6 13/16";
//          4-bolt anchor assembly, 3/4" bolts, anchor lines 4" and 8 5/8" from the parapet traffic face; rails 4" x 4 3/4", CL 10" and 1'-10" above the parapet top.
//          p.38 anchor assembly table: Unit 6 = 314.
//   p.452 (S4-59) RAIL POST SPACING FOR 2 BAR METAL RAIL UNIT 6 ("all post spacings are measured along the outside edge of deck"; "dimensions are typical for each
//          side of deck unless otherwise noted"), chains from the start bent control line of each span:
//          span 30: 7'-6 1/2" (rev. 1), 2 spa @ 2'-9" = 5'-6", 2 spa @ 6'-0 3/4" = 12'-1 1/2" (rev. 1), 17 spa @ 6'-6" = 110'-6", 4 spa @ 5'-7" = 22'-4" (2'-10" to bent 30)
//          spans 31-34: 2'-10", 4 spa @ 5'-7", 17 spa @ 6'-6", 4 spa @ 5'-7" (2'-10" to the next bent)
//          span 35: 2'-10", 4 spa @ 5'-7", 17 spa @ 6'-6", 3 spa @ 4'-9 11/16" (left, = 14'-5 1/16") / 4'-9 5/16" (right, = 14'-4"), 2 spa @ 2'-9" = 5'-6",
//          then 5'-3 7/16" to the CL joint; edge arcs 160'-10 9/16" (left) / 160'-9 7/16" (right).
//          Per side 26 + 4 x 26 + 27 = 157 posts, 314 in all = the p.38 table.
using SBM.Core;
using SBM.Core.Bridge;
using SBM.Core.Geometry;

var br = Doc.Project.Bridges.First(b => b.Name == "Bonner Unit 6");
var al = Doc.Project.Alignments.First(a => a.Name == br.AlignmentName);
string U(string pages, string item, string why, string then = "") => UnresolvedFlag.Text(pages, item, why, then);
double F(double ft, double inch = 0) => Ft(ft) + In(inch);
double IN(double ft, double inch) => ft + inch / 12;
double edge = F(21, 3.5);
var sup = br.Supports.ToDictionary(s => s.Name, s => s.Station);

(double first, (int n, double s)[] g) Ch(double first, params (int, double)[] g) => (first, g);
var c30 = Ch(IN(7, 6.5), (2, IN(2, 9)), (2, IN(6, 0.75)), (17, 6.5), (4, IN(5, 7)));
var cMid = Ch(IN(2, 10), (4, IN(5, 7)), (17, 6.5), (4, IN(5, 7)));
var c35L = Ch(IN(2, 10), (4, IN(5, 7)), (17, 6.5), (3, IN(4, 9 + 11.0 / 16)), (2, IN(2, 9)));
var c35R = Ch(IN(2, 10), (4, IN(5, 7)), (17, 6.5), (3, IN(4, 9 + 5.0 / 16)), (2, IN(2, 9)));
// (start bent, side, chain, printed end-of-chain to next bent / CL joint, printed edge arc)
var spans = new List<(int start, int side, (double first, (int n, double s)[] g) ch, double toEnd, double arc)>();
foreach (int side in new[] { -1, 1 })
{
    spans.Add((29, side, c30, IN(2, 10), IN(160, 10)));
    foreach (int b in new[] { 30, 31, 32, 33 }) spans.Add((b, side, cMid, IN(2, 10), IN(160, 10)));
    spans.Add((34, side, side < 0 ? c35L : c35R, IN(5, 3 + 7.0 / 16), side < 0 ? IN(160, 10 + 9.0 / 16) : IN(160, 9 + 7.0 / 16)));
}
var posts = new Dictionary<int, List<double>> { [-1] = new(), [1] = new() };
var closure = new List<string>();
double worst = 0;
foreach (var (start, side, ch, toEnd, arc) in spans)
{
    double s0 = sup[$"Bent {start}"], s1 = sup[$"Bent {start + 1}"];
    var sta = new List<double>(); var len = new List<double>();
    Vec2 prev = default; double acc = 0;
    for (double s = s0; ; s = Math.Min(s + Ft(0.1), s1))
    {
        var (p, ang) = al.EvaluateAtDistance(al.StationToDistance(s));
        var e = p + Vec2.FromAngle(ang).Right * (side * edge);
        if (sta.Count > 0) acc += (e - prev).Length;
        sta.Add(s); len.Add(acc); prev = e;
        if (s >= s1 - 1e-9) break;
    }
    double At(double d)
    {
        int i = len.FindIndex(x => x >= d);
        if (i < 0) return sta[^1];
        if (i == 0) return sta[0];
        return sta[i - 1] + (sta[i] - sta[i - 1]) * (d - len[i - 1]) / (len[i] - len[i - 1]);
    }
    double dd = Ft(ch.first);
    posts[side].Add(At(dd));
    foreach (var (n, sp) in ch.g) for (int i = 0; i < n; i++) { dd += Ft(sp); posts[side].Add(At(dd)); }
    double chainEnd = dd / Ft(1) + toEnd;
    worst = Math.Max(worst, Math.Abs(chainEnd - arc));
    closure.Add($"span {start + 1} {(side < 0 ? "L" : "R")}: chain {chainEnd:0.000} / plan arc {arc:0.000} / model edge {acc / Ft(1):0.000}");
}
Print("Rail post chains (ft): " + string.Join("; ", closure));
var s29 = br.Supports.First(s => s.Name == "Bent 29"); var s35 = br.Supports.First(s => s.Name == "Bent 35");
double face0 = s29.Station - s29.DeckEndOffset, face1 = s35.Station + s35.DeckEndOffset;   // deck faces (DeckEndOffset < 0 = inside the span)
string railFlag = U("p.37-38", "2-bar metal rail post / rail sections", "the post is an aluminum T-shape (5 3/4\" flange, 1/4\" plates, tapered web) and the rail a hollow 4\" x 4 3/4\" semi-elliptical extrusion; the generic railing builds outlines",
    "Posts = side outline (4 1/4\" at the base to 1 1/2\" at the top, scaled) x 5 3/4\" (envelope), front face on the base plate's traffic edge (derived); rails = 4 3/4\" x 4\" bars on the post front, CL at the printed 10\" / 1'-10\"; rail weight not counted.");
br.Railings = new List<PedestrianRailingDefinition>();
foreach (int side in new[] { -1, 1 })
    br.Railings.Add(new PedestrianRailingDefinition
    {
        Name = $"2-bar metal rail {(side < 0 ? "left" : "right")}", TypeName = "NCDOT 2 BAR METAL RAIL", Kind = RailingKind.Guardrail, Offset = side * F(20),
        BeginStation = face0 + F(3, 9), EndStation = face1 - F(3, 9), BaseProbe = In(7), Material = "Aluminum ASTM B221 6061-T6", Density = 2700,
        FlagsDimensionOnly = true, Source = "p.37-38 (S0-36 / S0-37), p.452 (S4-59)",
        Post = new RailPostDefinition
        {
            Profile = new() { new RailPoint(In(3.0625), In(0.5)), new RailPoint(In(3.0625 + 4.25), In(0.5)), new RailPoint(In(3.0625 + 1.5), F(2)), new RailPoint(In(3.0625), F(2)) },
            Thickness = In(5.75), Spacing = F(6), EndDistance = 0, FixedStations = posts[side], Description = "NCDOT 2-bar metal rail post (aluminum)",
            BasePlate = new RailBasePlateDefinition { Length = In(7.5), Width = In(6.8125), Thickness = In(0.5), CenterR = In(3.0625 + 6.8125 / 2), AnchorDiameter = In(0.75), AnchorEmbedment = In(2),
                Anchors = new() { new(In(2.75), In(4 - 6.46875)), new(-In(2.75), In(4 - 6.46875)), new(In(2.75), In(8.625 - 6.46875)), new(-In(2.75), In(8.625 - 6.46875)) } },
            Unresolved = railFlag, Basis = "post envelope; base plate 1/2\"; anchors 3/4\" x 2 1/2\" bolts (2\" below the plate) in the 4-bolt anchor assembly",
        },
        Members =
        {
            new RailMemberDefinition { Name = "lower rail", Kind = RailMemberKind.Rail, Width = In(4.75), Height = In(4), Center = new RailPoint(In(3.0625 - 4.75 / 2), In(10)), MassPerLength = 0,
                Description = "4\" x 4 3/4\" aluminum rail (semi-elliptical extrusion, envelope)", Unresolved = railFlag, Basis = "bounding bar" },
            new RailMemberDefinition { Name = "upper rail", Kind = RailMemberKind.Rail, Width = In(4.75), Height = In(4), Center = new RailPoint(In(3.0625 - 4.75 / 2), F(1, 10)), MassPerLength = 0,
                Description = "4\" x 4 3/4\" aluminum rail (semi-elliptical extrusion, envelope)", Unresolved = railFlag, Basis = "bounding bar" },
        },
        Unresolved =
        {
            U("p.37-38", "rail splices, expansion bars and clamp assemblies", "the 3'-0\" splice / expansion bar inside the rail at the parapet joints and the clamp bars are internal parts", "Rails continuous between the end posts."),
            U("p.39", "rail ends at the end posts", "the end-of-rail details attach the rail to the end post face", "Rails end at the end post faces."),
            U("p.452", "span 30 right side post chain", "the chain is printed for the left (top) side; the note says 'dimensions are typical for each side unless otherwise noted' while the right side draws fewer post symbols near bent 29", "Right side = left side chain."),
        },
    });
Print($"2-bar metal rail Unit 6: {posts[-1].Count} + {posts[1].Count} = {posts[-1].Count + posts[1].Count} posts (p.38 table: 314); largest chain vs printed arc {worst * 12:0.000} in");
