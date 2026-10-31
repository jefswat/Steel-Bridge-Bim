// NCDOT B-2500 Bonner Unit 5 segmental box - REINFORCING IN EVERY SEGMENT (re-runnable; called by details-bonner-unit5.csx with "--no-regen", or
// standalone: sbm run "samples\plans\rebar-bonner-unit5.csx" "<model.sbm>" [out.sbm]). Generic input: SegmentalDefinition.SectionBars
// (Bridge/SegmentalSectionBars.cs, generator Segmental/SegmentalGeneratorBars.cs): bar centerlines from face-referenced section paths, resolved in every
// segment's own section (variable depth). Data: docs/plan-data/bonner-b2500/segmental-unit5/typical-segment-bar-list.csv (p.270 bill), segment-rebar-variables.csv
// (p.269 REINFORCING VARIABLES), bent-end-segment-bars.csv (p.273 / 279-280 readings). Rules (user): nothing invented - bend legs are the printed out-to-out
// dims (p.270 BAR TYPES), positions on the faces at the printed covers (p.269: 3" CLR. top slab, 2" CLR. TYP. U.N.O.); what is only drawn is SCALED / DERIVED
// and flagged (page + plain reason); counts that are not printed are not invented (UNRESOLVED list).
using System.IO;
using SBM.Core;
using SBM.Core.Bridge;

const string BridgeName = "Bonner Unit 5";
bool noRegen = Args.Contains("--no-regen");
string outPath = Args.FirstOrDefault(a => !a.StartsWith("--")) ?? Doc.FilePath ?? @"samples\plans\Bonner Unit 5 segmental.sbm";
var br = Doc.Project.Bridges.First(b => b.Name == BridgeName);
var sg = br.Segmental ?? throw new InvalidOperationException("Run build-bonner-unit5.csx first (no segmental definition).");
string U(string pages, string item, string why, string then = "") => UnresolvedFlag.Text(pages, item, why, then);
double I(double inch) => In(inch);
double F(double ft, double inch = 0) => Ft(ft) + In(inch);

string dataDir = null;
for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
{
    var p = Path.Combine(dir.FullName, "docs", "plan-data", "bonner-b2500", "segmental-unit5");
    if (Directory.Exists(p)) { dataDir = p; break; }
}
List<string[]> Csv(string name) => File.ReadAllLines(Path.Combine(dataDir, name)).Skip(1).Where(l => l.Trim() != "")
    .Select(l => { var c = new List<string>(); var cur = new System.Text.StringBuilder(); bool q = false;
        foreach (char ch in l) { if (ch == '"') q = !q; else if (ch == ',' && !q) { c.Add(cur.ToString()); cur.Clear(); } else cur.Append(ch); }
        c.Add(cur.ToString()); return c.ToArray(); }).ToList();

// ---------------------------------------------------------------- path helpers (offsets in inches, face to bar centerline, into the concrete)
SegmentalBarPoint C(string f1, double o1, string f2, double o2) => new() { Face = f1, Offset = I(o1), Face2 = f2, Offset2 = I(o2) };
SegmentalBarPoint AtX(string f, double o, double xIn) => new() { Face = f, Offset = I(o), X = I(xIn) };
SegmentalBarPoint AtY(string f, double o, double yIn) => new() { Face = f, Offset = I(o), Y = I(yIn) };
SegmentalBarPoint Leg(string f, double lenIn, double dx, double dy) => new() { Face = f, Length = I(lenIn), DirX = dx, DirY = dy };
SegmentalBarPoint Hide(SegmentalBarPoint p) { p.Hidden = true; return p; }
double Db(string size) => size switch { "#4" => 0.5, "#5" => 0.625, "#6" => 0.75, "#7" => 0.875, "#9" => 1.128, _ => throw new ArgumentException(size) };

var bill = Csv("typical-segment-bar-list.csv").Where(r => r[0] != "" && !r[0].StartsWith("TOTAL")).ToDictionary(r => r[0], r => r);
var vars = Csv("segment-rebar-variables.csv");
var sets = new List<SegmentalSectionBarSet>();
const string Pg = "p.269-270 (S3-21 / S3-22)";
string scaled = U("p.269-270", "typical segment bar positions in the section", "the typical section draws every bar but dimensions only the covers (3\" CLR. top slab, 2\" CLR. TYP. U.N.O.) and the spacings along the segment; the layer order (transverse outside, B1 inside) and where each bend sits on the faces are read from the drawing",
    "Bars placed on the faces at the printed covers with the printed bend legs (p.270 BAR TYPES); flagged dimension-only.");
string botCover = U("p.269", "cover at the top slab underside / wing underside", "the typical section prints 3\" CLR. only at the top of the top slab; the underside takes the general '2\" CLR. (TYP. U.N.O.)'", "2\" clear used.");
string lapRule = U("p.269-270", "W1 / W2 web legs in segments other than XX-D3 / XX-U3", "the bill is printed for XX-D3 / XX-U3 only ('other segments vary'); the web depth changes from segment to segment",
    "W3 / W4 keep their printed legs; the W1 / W2 web legs are the printed XX-D3 legs plus the web length difference at the segment's mid depth (depth table, linear between sections - DERIVED), so the laps stay as in XX-D3; lengths from the model.") + " " +
    U("p.269-270", "W1 top legs (type 3)", "the 2'-4 1/4\" flat and the 1'-6\" leg are printed, but whether the flat runs outward over the wing or inward over the haunch is only drawn; outward, the 1'-6\" leg would leave the wing underside (it is 19\" thick there)", "Flat inward over the top haunch, leg down into the haunch (fits within the 2\" cover).");
string noBill = "no bill for this segment in the set (p.270 prints XX-D3 / XX-U3 only): length from the model";

// depth at the middle of typical segment k (joints at 5' + 14' (k - 1) from the bent CL; depth table p.268, linear between sections) and the web length
var depthTab = Csv("box-sections.csv").Select(r => (x: double.Parse(r[1]), h: double.Parse(r[3]))).OrderBy(t => t.x).ToList();
double DepthAtSegment(int k)
{
    double x = 5 + 14 * (k - 1) + 7;
    if (x >= depthTab[^1].x) return depthTab[^1].h;
    int i = 1; while (depthTab[i].x < x) i++;
    var (a, b) = (depthTab[i - 1], depthTab[i]);
    return a.h + (b.h - a.h) * (x - a.x) / (b.x - a.x);
}
double WebSlant(double hIn, double topOff, double botOff) => (hIn - topOff - botOff) * Math.Sqrt(1 + 1.0 / 16);

// ---------------------------------------------------------------- 1. typical segments XX-Dk / XX-Uk (k = 1..13), p.269-270
foreach (var v in vars)
{
    int k = int.Parse(v[0]);
    string unit = $"typical segments XX-D{k} / XX-U{k}", rx = $@"^\d+-[DU]{k}$";
    string ws = "#" + v[2]; double dW = Db(ws);
    int aWeb = int.Parse(v[3]), bBot = int.Parse(v[4]);
    bool billSeg = k == 3;
    double Listed(string mark, bool depthIndependent) => depthIndependent ? Len(bill[mark][4]) : 0;   // W bars: from the faces + printed legs (compared with the bill in XX-D3 / XX-U3)
    SegmentalSectionBarSet S(string mark, string size, SegmentalSectionBarKind kind, string where, bool depthIndependent, params SegmentalBarPoint[] path)
    {
        var b = bill.GetValueOrDefault(mark);
        var s = new SegmentalSectionBarSet
        {
            Mark = mark, Size = size, Unit = unit, Segments = rx, Kind = kind, Path = path.ToList(), Cover = I(2), CoverTop = I(2),
            ListedLength = b == null ? 0 : Listed(mark, depthIndependent), ShapeCode = b == null ? "STR" : b[3].Trim('.'), Source = Pg,
            Location = $"{where} ({unit}{(billSeg || b == null || depthIndependent ? "" : "; " + noBill)})", Unresolved = scaled,
        };
        sets.Add(s); return s;
    }
    // top slab transverse row: 15 @ 11 1/2" (A1 top; A2 / A3 each side + A4 between the haunches, BOT.)
    var a1 = S("A1", "#4", SegmentalSectionBarKind.Transverse, "top slab TOP mat transverse, tip to tip", true, C("L:TIP", 2, "TOP", 3.25), C("TIP", 2, "TOP", 3.25));
    a1.Count = 15; a1.Spacing = I(11.5); a1.CoverTop = I(3);
    var a2 = S("A2", "#4", SegmentalSectionBarKind.Transverse, "top slab BOTTOM mat transverse at the wing tip, each side", true, Leg("H", 60, -1, 0), C("TIP", 2, "WINGFLAT", 2.25));
    a2.Count = 15; a2.Spacing = I(11.5); a2.Mirror = true; a2.Unresolved = scaled + " " + botCover;
    var a3 = S("A3", "#4", SegmentalSectionBarKind.Transverse, "top slab BOTTOM mat under the wing, across the web top, up the haunch (type 4), each side", true,
        Leg("WINGFLAT", 40, 1, 0), C("WINGFLAT", 2.25, "WING", 2.25), C("WING", 2.25, "HAUNCH", 2.25), C("HAUNCH", 2.25, "TOPIN", 2.25), Leg("H", 11.75, -1, 0));
    a3.Count = 15; a3.Spacing = I(11.5); a3.Mirror = true; a3.FitLeg = 0;
    a3.Unresolved = scaled + " " + botCover + " " + U("p.270", "A3 outer leg", "type 4 prints the sloped legs and the 1'-0\" flat; the outer end along the wing underside is drawn only", "Outer leg along the wing underside solved for the listed 15'-10\".");
    var a4 = S("A4", "#4", SegmentalSectionBarKind.Transverse, "top slab BOTTOM mat transverse between the web haunches", true, AtX("TOPIN", 2.25, -73.5), AtX("TOPIN", 2.25, 73.5));
    a4.Count = 15; a4.Spacing = I(11.5); a4.Unresolved = scaled + " " + botCover;
    // slab edge U bars and parapet dowels: 32-#5A9 per edge, 16-#5S1 (E.F.) SPA. W/ the A9
    var a9 = S("A9", "#5", SegmentalSectionBarKind.Transverse, "U bar round the top slab edge (type 6), each edge", true,
        Leg("H", 42 - 0.3125, -1, 0), C("TIP", 2.3125, "TOP", 3.3125), C("TIP", 2.3125, "WINGFLAT", 2.3125), Leg("H", 15 - 0.3125, -1, 0));
    a9.Count = 32; a9.Spacing = I(5.25); a9.Mirror = true; a9.CoverTop = I(3);
    a9.Unresolved = scaled + " " + U("p.269", "A9 / S1 spacing along the segment", "the half plan gives 32-#5A9 and 16-#5S1 (E.F.) per edge with the end spaces only (1 SPA. @ 7\", 4 1/2\")", "32 A9 @ 5 1/4\" centred (DERIVED), S1 at every second A9.");
    foreach (var (face, xIn) in new[] { ("inside", 240.0 + 2), ("outside", 254.0 - 2) })
    {
        var s1 = S("S1", "#5", SegmentalSectionBarKind.Transverse, "parapet dowel L (type 7), each face, each edge", true,
            Leg("H", 8 - 0.3125, -1, 0), AtX("WINGFLAT", 2.3125, xIn), Leg("V", 33 - 0.3125, 0, -1));
        s1.Count = 16; s1.Spacing = I(10.5); s1.Mirror = true; s1.Cover = double.NaN;
        s1.Unresolved = a9.Unresolved + " " + U("p.269 / 327", $"S1 dowel line ({face} parapet face)", "the dowels stand '2\" CL. to #5S1' inside the parapet faces (parapet 1'-2\" wide, inside face 20'-0\" from -L-); the foot direction is drawn only", "Foot toward CL girder; the leg above the deck is not cover-checked (parapet concrete).");
    }
    // web bars W1 / W2 (upper) and W3 / W4 (lower), 28 @ 6" each web
    double ow = 2 + dW / 2, tw = 3 + dW / 2;
    var w3 = S("W3", ws, SegmentalSectionBarKind.Transverse, "web OUTSIDE face lower bar into the bottom slab (type 1), each web", false,
        Leg("WEBOUT", 67.5 - dW / 2, 0, -1), C("WEBOUT", ow, "SOFFIT", ow), Leg("H", 80 - dW / 2, -1, 0));
    var w4 = S("W4", ws, SegmentalSectionBarKind.Transverse, "web INSIDE face lower bar into the bottom slab (type 1), each web", false,
        Leg("WEBIN", 18 - dW / 2, 0, -1), C("WEBIN", ow, "SOFFIT", ow), Leg("H", 58.25 - dW / 2, -1, 0));
    var wRow = Enumerable.Range(0, 28).ToList();
    List<double> Row(IEnumerable<int> idx) => idx.Select(i => F(7) + I(6) * (i - 13.5)).ToList();   // 28 @ 6" centred in the 14'-0" segment
    foreach (var w in new[] { w3, w4 }) { w.Positions = Row(wRow); w.Mirror = true; w.AlongRef = SegmentalBarAlongRef.Start; }
    // upper web legs: the XX-D3 / XX-U3 printed legs (W1 10'-5 1/4", W2 11'-4") grown by the web length difference to segment k (same end distance above the soffit as in D3)
    double grow = WebSlant(DepthAtSegment(k), tw, ow) - WebSlant(DepthAtSegment(3), tw, ow);
    SegmentalBarPoint[] W1Path() => new[] { Leg("WEBOUT", 125.25 - dW + grow, 0, 1), C("WEBOUT", ow, "TOP", tw), Leg("H", 28.25 - dW / 2, -1, 0), Leg("V", 18 - dW / 2, 0, 1) };
    SegmentalBarPoint[] W2Path() => new[] { Leg("H", 15 - dW / 2, -1, 0), C("WEBIN", ow, "TOP", tw), Leg("WEBIN", 136 - dW + grow, 0, 1), Leg("H", 11 - dW / 2, 1, 0) };
    var w1 = S("W1", ws, SegmentalSectionBarKind.Transverse, "web OUTSIDE face upper bar into the top slab (type 3), each web", false, W1Path());
    var w2 = S("W2", ws, SegmentalSectionBarKind.Transverse, "web INSIDE face upper bar into the top slab (type 5), each web", false, W2Path());
    foreach (var w in new[] { w1, w2 }) { w.Mirror = true; w.AlongRef = SegmentalBarAlongRef.Start; w.Positions = Row(wRow); w.Unresolved = scaled + " " + lapRule; }
    w3.Unresolved = scaled; w4.Unresolved = scaled;
    string subst = U("p.269-270", "W5 / W6 bars (Detail B)", "Detail B draws the substitute bars at the top slab ducts without bend dimensions and they are not in the bill", "Counted, not placed.");
    if (v[5].StartsWith("W1"))   // 7 W1 + 5 W5 + 16 W1
    {
        w1.Positions = Row(wRow.Where(i => i < 7 || i >= 12));
        sets.Add(new() { Mark = "W5", Size = ws, Unit = unit, Segments = rx, Kind = SegmentalSectionBarKind.Transverse, Unplaced = 10, Location = $"web OUTSIDE face substitute for W1 at the top slab ducts (Detail B), 5 each web ({unit})", Source = "p.269-270", Unresolved = subst });
    }
    if (v[5].StartsWith("W2"))   // 9 W2 + 4 W6 + 15 W2
    {
        w2.Positions = Row(wRow.Where(i => i < 9 || i >= 13));
        sets.Add(new() { Mark = "W6", Size = ws, Unit = unit, Segments = rx, Kind = SegmentalSectionBarKind.Transverse, Unplaced = 8, Location = $"web INSIDE face substitute for W2 at the top slab ducts (Detail B), 4 each web ({unit})", Source = "p.269-270", Unresolved = subst });
    }
    // bottom slab: A5 (haunch, type 2), A6 (top mat, type 1, E.S.), A7 (bottom mat, straight)
    var a5 = S("A5", "#4", SegmentalSectionBarKind.Transverse, "bottom slab haunch bar from the outside web face down the haunch into the top mat, each web", true,
        C("WEBOUT", 2 + dW + 0.25, "BHAUNCH", 2.25), C("BHAUNCH", 2.25, "BOTIN", 2.25), Leg("H", 24, -1, 0));
    a5.FitLeg = 2; a5.Mirror = true; a5.AlongRef = SegmentalBarAlongRef.Start; a5.Positions = Row(wRow);
    a5.Unresolved = scaled + " " + U("p.270", "A5 shape", "type 2 is drawn symmetric (two sloped legs about a 3'-8 3/4\" flat) while Section A-A places 28 A5 at EACH web corner", "One leg along the bottom haunch from the outside web face, flat toward CL girder solved for the listed 5'-9\".");
    var a6 = S("A6", "#5", SegmentalSectionBarKind.Transverse, "bottom slab TOP mat transverse bent up the outside web face (type 1), each side", true,
        Leg("WEBOUT", 10 - 0.3125, 0, -1), C("WEBOUT", 2 + dW + 0.3125, "BOTIN", 2.3125), Leg("H", 132 - 0.3125, -1, 0));
    a6.Mirror = true; a6.AlongRef = SegmentalBarAlongRef.Start; a6.Positions = Row(wRow);
    var a7 = S("A7", "#5", SegmentalSectionBarKind.Transverse, "bottom slab BOTTOM mat transverse", true, AtX("SOFFIT", 2.3125, -73.5), AtX("SOFFIT", 2.3125, 73.5));
    a7.Count = 28; a7.Spacing = I(6); a7.Unresolved = "";
    // A8 / A10 / A12 and A16 / D-4 wire: not placed (reasons)
    sets.Add(new() { Mark = "A8", Size = "#4", Unit = unit, Segments = rx, Kind = SegmentalSectionBarKind.Transverse, Unplaced = 30, ListedLength = Len(bill["A8"][4]), ShapeCode = "2",
        Location = $"top slab bar at the web top between the cantilever anchors (type 2) ({unit})", Source = "p.269-270",
        Unresolved = U("p.269-270", "A8 position", "type 2 prints a symmetric hat shape (4'-3 1/4\" flat, 1'-0\" legs) but the typical section shows the label at the web / fillet corner with no bar of that shape dimensioned", "Counted (15 per web), not placed.") });
    foreach (var (m, n) in new[] { ("A10", 6), ("A12", 4) })
        sets.Add(new() { Mark = m, Size = "#4", Unit = unit, Segments = rx, Kind = SegmentalSectionBarKind.Transverse, Unplaced = n, ListedLength = Len(bill[m][4]), ShapeCode = bill[m][3].Trim('.'),
            Location = $"anchor zone at the top slab / web junction, Detail A ({unit})", Source = "p.269-270",
            Unresolved = U("p.269-270", $"{m} (anchor zone, Detail A)", "the plans call '#4A10 OR #4A12' at the cantilever anchors without the split or the positions", "Counted per the XX-D3 bill, not placed.") });
    // B1 longitudinals (14'-0" segment less 2 x 2" = 13'-8"); transverse layer outside, B1 inside it
    SegmentalSectionBarSet B(string where, int count, double spacing, string extra, params SegmentalBarPoint[] line)
    {
        var s = S("B1", "#4", SegmentalSectionBarKind.Longitudinal, where, true, line);
        s.Count = count; s.Spacing = spacing; s.EndClear = I(2); s.LongSamples = 5; s.AlongRef = SegmentalBarAlongRef.Start;
        if (extra.Length > 0) s.Unresolved = scaled + " " + extra;
        return s;
    }
    B("top slab TOP mat longitudinal", 41, F(1), "", AtX("TOP", 3.75, -246), AtX("TOP", 3.75, 246)).CoverTop = I(3);
    B("top slab BOTTOM mat longitudinal along the wing / haunch underside", 39, 0, U("p.269", "B1 bottom mat of the top slab", "'39-#4B1 @ 1'-0\" (BOT.), ALIGNED WITH TOP BARS EXCEPT AS NOTED' along a sloped underside", "39 bars equally spaced along the underside line (inside A2 / A3 / A4)."),
        C("L:TIP", 2.5, "L:WINGFLAT", 2.75), C("L:WINGFLAT", 2.75, "L:WING", 2.75), C("L:WING", 2.75, "L:HAUNCH", 2.75), C("L:HAUNCH", 2.75, "TOPIN", 2.75),
        C("TOPIN", 2.75, "HAUNCH", 2.75), C("HAUNCH", 2.75, "WING", 2.75), C("WING", 2.75, "WINGFLAT", 2.75), C("WINGFLAT", 2.75, "TIP", 2.5));
    string bRule = U("p.269", "B1 positions in the bottom slab and the webs", "REINFORCING VARIABLES give the counts 'A' (each web face) and 'B' with '@ 1'-0\" (MAX.)' only", "Equally spaced between the corners of the face (inside the transverse bars).");
    B($"bottom slab BOTTOM mat longitudinal (B = {bBot})", bBot, 0, bRule, C("L:WEBOUT", 2 + dW + 0.25, "SOFFIT", 2.875), C("WEBOUT", 2 + dW + 0.25, "SOFFIT", 2.875));
    B($"bottom slab TOP mat longitudinal (B = {bBot})", bBot, 0, bRule, C("L:BHAUNCH", 2.875, "BOTIN", 2.875), C("BHAUNCH", 2.875, "BOTIN", 2.875));
    var bo = B($"web OUTSIDE face longitudinal (A = {aWeb}), each web", aWeb, 0, bRule, C("WEBOUT", 2 + dW + 0.25, "WING", 2.75), C("WEBOUT", 2 + dW + 0.25, "SOFFIT", 2.875 + 1));
    var bi = B($"web INSIDE face longitudinal (A = {aWeb}), each web", aWeb, 0, bRule, C("WEBIN", 2 + dW + 0.25, "HAUNCH", 2.75), C("WEBIN", 2 + dW + 0.25, "BHAUNCH", 2.875));
    bo.Mirror = true; bi.Mirror = true;
}
// typical-segment marks with no 3D bar: A16 (bottom slab duct U bars at the joints) and the D-4 anchorage wire
sg.Unresolved.Add("[rebar] " + U("p.270", "#4A16 bottom slab duct bars and the D-4 anchorage wire", "'2-#4A16 at every tendon in the bottom slab crossing a joint' and the D-4 wire have no bend dimensions and are not in the bill; the transverse PT sheet for the wire is not tabulated", "Not scheduled or placed."));

// ---------------------------------------------------------------- 2. closure joints CJ19 .. CJ29 (p.281): typical bar shapes, counts printed on the sheet
{
    const string unit = "closure joints", rx = @"^CJ\d+$";
    string cj = U("p.281", "closure joint bar positions", "the closure sheet prints the counts (8-#6W1, 8-#6W3, 8-#5A6 (E.S.), 8-#5A7 (EQ. SPA.), 41 / 39-#4B1, 19-#4B1 top / bottom) and refers to the typical bar shapes; positions are drawn only",
        "Typical bar paths at the printed covers, 8 bars at equal spaces 3\" from the joint faces; lengths from the model (no bill).");
    SegmentalSectionBarSet T(string mark, string size, string where, int count, params SegmentalBarPoint[] path)
    {
        var s = new SegmentalSectionBarSet { Mark = mark, Size = size, Unit = unit, Segments = rx, Kind = SegmentalSectionBarKind.Transverse, Path = path.ToList(), Mirror = true,
            Count = count, First = I(3), Cover = I(2), CoverTop = I(2), ShapeCode = bill.TryGetValue(mark, out var b) ? b[3].Trim('.') : "STR", Source = "p.281 (S3-33)",
            Location = $"{where} (closure joints; no bill in the set: length from the model)", Unresolved = cj };
        sets.Add(s); return s;
    }
    double ow = 2.375, tw = 3.375;
    double grow = WebSlant(108, tw, ow) - WebSlant(DepthAtSegment(3), tw, ow);
    T("W1", "#6", "web OUTSIDE face upper bar into the top slab", 8, Leg("WEBOUT", 125.25 - 0.75 + grow, 0, 1), C("WEBOUT", ow, "TOP", tw), Leg("H", 27.875, -1, 0), Leg("V", 17.625, 0, 1));
    T("W2", "#6", "web INSIDE face upper bar into the top slab", 8, Leg("H", 14.625, -1, 0), C("WEBIN", ow, "TOP", tw), Leg("WEBIN", 136 - 0.75 + grow, 0, 1), Leg("H", 10.625, 1, 0));
    T("W3", "#6", "web OUTSIDE face lower bar into the bottom slab", 8, Leg("WEBOUT", 67.125, 0, -1), C("WEBOUT", ow, "SOFFIT", ow), Leg("H", 79.625, -1, 0));
    T("W4", "#6", "web INSIDE face lower bar into the bottom slab", 8, Leg("WEBIN", 17.625, 0, -1), C("WEBIN", ow, "SOFFIT", ow), Leg("H", 57.875, -1, 0));
    T("A6", "#5", "bottom slab TOP mat transverse bent up the outside web face, each side", 8, Leg("WEBOUT", 9.6875, 0, -1), C("WEBOUT", 3.0625, "BOTIN", 2.3125), Leg("H", 131.6875, -1, 0));
    var a7 = T("A7", "#5", "bottom slab BOTTOM mat transverse (8 EQ. SPA.)", 8, AtX("SOFFIT", 2.3125, -73.5), AtX("SOFFIT", 2.3125, 73.5)); a7.Mirror = false;
    SegmentalSectionBarSet L(string where, int count, double spacing, params SegmentalBarPoint[] line)
    {
        var s = new SegmentalSectionBarSet { Mark = "B1", Size = "#4", Unit = unit, Segments = rx, Kind = SegmentalSectionBarKind.Longitudinal, Path = line.ToList(), Count = count, Spacing = spacing,
            EndClear = I(2), Cover = I(2), CoverTop = I(2), Source = "p.281 (S3-33)", Location = $"{where} (closure joints)", Unresolved = cj };
        sets.Add(s); return s;
    }
    L("top slab TOP mat longitudinal", 41, F(1), AtX("TOP", 3.75, -246), AtX("TOP", 3.75, 246)).CoverTop = I(3);
    L("top slab BOTTOM mat longitudinal", 39, 0, C("L:TIP", 2.5, "L:WINGFLAT", 2.75), C("L:WINGFLAT", 2.75, "L:WING", 2.75), C("L:WING", 2.75, "L:HAUNCH", 2.75), C("L:HAUNCH", 2.75, "TOPIN", 2.75),
        C("TOPIN", 2.75, "HAUNCH", 2.75), C("HAUNCH", 2.75, "WING", 2.75), C("WING", 2.75, "WINGFLAT", 2.75), C("WINGFLAT", 2.75, "TIP", 2.5));
    L("bottom slab BOTTOM mat longitudinal (19 @ 1'-0\" MAX.)", 19, 0, C("L:WEBOUT", 3, "SOFFIT", 2.875), C("WEBOUT", 3, "SOFFIT", 2.875));
    L("bottom slab TOP mat longitudinal (19 @ 1'-0\" MAX.)", 19, 0, C("L:BHAUNCH", 2.875, "BOTIN", 2.875), C("BHAUNCH", 2.875, "BOTIN", 2.875));
    sg.Unresolved.Add("[rebar] " + U("p.281", "closure joint A1-A4, A8, A9, A13-A15, S1 and web B1 bars", "the closure sheet labels these marks without counts (and #4A13 OR #4A14 without the split)", "Not scheduled or placed."));
}

// ---------------------------------------------------------------- 3. bent segment halves XX-DB / XX-UB (p.279-280; no bill of materials in the set)
string noBillBent = U("p.279-280", "bent segment reinforcing", "the set has no bill of materials for the bent segments (44 marks drawn with counts / spacings, shapes drawn only)", "Marks with printed counts placed at the printed spacings (positions scaled, flagged); lengths from the model; the other marks are not scheduled.");
{
    const string unit = "bent segments", rx = @"^\d+-[DU]B$";
    SegmentalSectionBarSet NB(string mark, string size, SegmentalSectionBarKind kind, string where, string extra, params SegmentalBarPoint[] path)
    {
        var s = new SegmentalSectionBarSet { Mark = mark, Size = size, Unit = unit, Segments = rx, Kind = kind, Path = path.ToList(), Cover = I(2), CoverTop = I(2), AlongRef = SegmentalBarAlongRef.BentSide,
            Source = "p.279-280 (S3-31 / S3-32)", Location = $"{where} (bent segment halves; no bill in the set: length from the model)", Unresolved = noBillBent + (extra.Length > 0 ? " " + extra : "") };
        sets.Add(s); return s;
    }
    double d9 = 1.128;
    var n1 = NB("N1", "#9", SegmentalSectionBarKind.Transverse, "top slab TOP transverse, tip to tip (8 @ 8\")", "", C("L:TIP", 2 + d9 / 2, "TOP", 3 + d9 / 2), C("TIP", 2 + d9 / 2, "TOP", 3 + d9 / 2));
    n1.Count = 8; n1.Spacing = 0; n1.First = I(3); n1.CoverTop = I(3);   // D-D: 3" | 8-#9N1 @ 8" (+-) | 3"
    var n4 = NB("N4", "#5", SegmentalSectionBarKind.Transverse, "top slab BOTTOM transverse, PAIRED (8 @ 8\")",
        U("p.280", "N4 depth", "inside the solid bent diaphragm the top slab has no underside face; the bars are drawn 2\" above the 9\" slab underside level", "Depth 9\" - 2\" - db/2 below the deck top (DERIVED)."),
        AtY("L:TIP", 2.3125, 9 - 2.3125), AtY("TIP", 2.3125, 9 - 2.3125));
    n4.Positions = Enumerable.Range(0, 8).SelectMany(i => new[] { I(3) + (F(5) - I(6)) * i / 7 - I(0.3125), I(3) + (F(5) - I(6)) * i / 7 + I(0.3125) }).ToList();
    var n38 = NB("N38", "#4", SegmentalSectionBarKind.Longitudinal, "top slab TOP longitudinal (20 @ 1'-0\" per half width), each side", "", AtX("TOP", 3 + d9 + 0.25, 6), C("TIP", 2.25, "TOP", 3 + d9 + 0.25));
    n38.Count = 20; n38.Mirror = true; n38.CoverTop = I(3); n38.EndClear = I(2); n38.AlongRef = SegmentalBarAlongRef.Start;
    // diaphragm horizontals across the webs at scaled depths, each face of the 3'-6" solid diaphragm (2" clear), PAIRED = two bars in contact
    string depthFlag = U("p.279", "diaphragm bar depths", "the depths are read off the left dimension chain (10\" wing tip, then 9\", 10\", 1'-0\", 1'-0\", 9\", 9\", 1'-0\", 19 sets @ 6\") with the label leaders only", "SCALED depths; each face of the 3'-6\" diaphragm at 2\" clear.");
    void Dia(string mark, string size, double depthIn, bool paired, int levels = 1, double pitchIn = 0)
    {
        double db = Db(size);
        for (int lv = 0; lv < levels; lv++)
        {
            double y = depthIn + lv * pitchIn;
            var s = NB(mark, size, SegmentalSectionBarKind.Transverse, $"bent diaphragm horizontal across the webs{(paired ? ", PAIRED" : "")} (E.F.)", depthFlag,
                AtY("L:WEBOUT", 2 + db / 2, y), AtY("WEBOUT", 2 + db / 2, y));
            s.Positions = paired ? new() { I(2 + db / 2), I(2 + 1.5 * db), I(42 - 2 - 1.5 * db), I(42 - 2 - db / 2) } : new() { I(2 + db / 2), I(42 - 2 - db / 2) };
        }
    }
    Dia("N3", "#9", 19, true);
    Dia("N12", "#7", 41, false, 2, 12);
    Dia("N5", "#9", 62, true);
    Dia("N6", "#9", 71, true);
    Dia("N7", "#9", 83, true);
    Dia("N9", "#6", 89, false, 19, 6);
    sg.Unresolved.Add("[rebar] " + U("p.279-280", "bent segment marks N2, N8, N10, N11, N13-N37, N39-N44 and S1 / A9 at the bent", "they are drawn with partial counts ('VARIES', '(+/-)') and shapes only, with no bill of materials", "Not scheduled or placed."));
}

// ---------------------------------------------------------------- 4. end segments 18-ES / 29-ES (p.273-276; no bill in the set)
{
    const string unit = "end segments", rx = @"^\d+-ES$";
    string noBillEnd = U("p.273-276", "end segment reinforcing", "the set has no bill of materials for the end segments (48 marks drawn with counts / spacings, shapes drawn only) and the end segment interior is modeled as the solid envelope", "Marks with printed counts placed at the printed spacings (positions scaled, flagged); lengths from the model; the other marks are not scheduled.");
    var e45 = new SegmentalSectionBarSet { Mark = "E45", Size = "#4", Unit = unit, Segments = rx, Kind = SegmentalSectionBarKind.Longitudinal, Mirror = true, Count = 21, EndClear = I(2), Cover = I(2), CoverTop = I(3),
        Path = new() { AtX("TOP", 3.25, 6), C("TIP", 2.25, "TOP", 3.25) }, Source = "p.273 (S3-25)", Location = "top slab TOP longitudinal (21 @ 1'-0\" per half width), each side (end segments; no bill: length from the model)", Unresolved = noBillEnd };
    sets.Add(e45);
    // 7E9 .. 7E15 @ 10": horizontals across the end diaphragm; E9 1'-11 1/2" above the soffit (1'-0" + 11 1/2" at the bottom of the chain), 3 each
    double h = sg.ConstantDepth / In(1);
    for (int i = 0; i < 7; i++)
    {
        double y = h - (23.5 + 10 * i);
        sets.Add(new SegmentalSectionBarSet { Mark = $"E{9 + i}", Size = "#7", Unit = unit, Segments = rx, Kind = SegmentalSectionBarKind.Transverse, Count = 3, First = I(2 + 0.4375), Cover = I(2),
            Path = new() { AtY("L:WEBOUT", 2.4375, y), AtY("WEBOUT", 2.4375, y) }, Source = "p.273 (S3-25)",
            Location = $"end diaphragm horizontal across the webs (#7E9 TO #7E15 @ 10\", 3 each) (end segments; no bill: length from the model)",
            Unresolved = noBillEnd + " " + U("p.273", "E9-E15 positions", "the elevation dimensions '#7E9 TO #7E15 @ 10\"' from the bottom chain (1'-0\", 11 1/2\") and the sections give '3' per mark without positions along the segment", "E9 1'-11 1/2\" above the soffit, @ 10\"; 3 per mark at equal spaces 2\" clear of the segment faces (SCALED / DERIVED).") });
    }
    sg.Unresolved.Add("[rebar] " + U("p.273-276", "end segment marks E1-E8, E16-E44, E46-E48, S1 / S2 / A9", "they are drawn with counts in elevation / section views of the anchor zone but without a bill of materials or bend dimensions, and the end segment interior (diaphragm, blockout) is not modeled", "Not scheduled or placed."));
}

// ---------------------------------------------------------------- 5. block bars (J, T, P, D, H): no bill, no bend dims
sg.Unresolved.Add("[rebar] " + U("p.297-302, 321", "anchor block J / T, erection block P, deviation block D and opening H bars", "the block sheets give counts and spacings with shapes drawn only and no bill of materials; the blocks themselves carry dimension-only flags", "Not scheduled or placed (block solids by details-bonner-unit5.csx)."));

sg.SectionBars = sets;
sg.Bars = null;   // the earlier XX-D3 / XX-U3 straight bars are part of the section bars now
Print($"Section bars: {sets.Count} sets, {sets.Select(s => (s.Unit, s.Mark)).Distinct().Count()} marks in {sets.Select(s => s.Unit).Distinct().Count()} units.");
if (!noRegen)
{
    var rep = Doc.Regenerate();
    Print($"Regenerated in {rep.Elapsed.TotalSeconds:0.0} s; {rep.Errors.Count} errors");
    var sm = Doc.Segmental(BridgeName);
    var withBars = sm.Bars.Select(b => b.Unit).Distinct().ToList();
    Print($"Bars placed {sm.Bars.Count}, scheduled {sm.Marks.Sum(m => m.Count)} ({sm.Marks.Sum(m => m.TotalMass) / 0.45359237:0} lb), cover short {sm.CoverViolations.Count}");
    Doc.Save(outPath);
    Print($"Saved {outPath}");
}
