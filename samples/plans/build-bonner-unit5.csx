// FINISH (required, see CLAUDE.md "New plan-set model checklist" / docs/MODEL-PIPELINE.md): `sbm run <this script> <model.sbm>` runs `sbm pipeline` after the
// script saves = regenerate, terrain (georeferenced projects), plan sheet index, companion database + sheet links; UNRESOLVED items are printed.
// NCDOT B-2500 (Bonner Bridge replacement, NC 12 over Oregon Inlet), NAVIGATION UNIT 5 = spans 19-29, 3,550'-0" along -L-:
// precast post-tensioned SEGMENTAL concrete box girder, balanced cantilever from bents 19-28, closure joints, end segments at bents 18 / 29.
// From scratch (re-runnable; replaces the -L- alignment and the bridge "Bonner Unit 5"), run from the repo root:
//   sbm new  "samples\plans\Bonner Unit 5 segmental.sbm"
//   sbm run  "samples\plans\build-bonner-unit5.csx" "samples\plans\Bonner Unit 5 segmental.sbm"
//   sbm run  "samples\plans\check-bonner-unit5.csx" "samples\plans\Bonner Unit 5 segmental.sbm"
// Data: docs/plan-data/bonner-b2500/SUMMARY.md (general drawings) and docs/plan-data/bonner-b2500/segmental-unit5/ (SUMMARY.md + CSVs, read by
// this script); design note segmental-unit5/SEGMENTAL-DESIGN.md. PDF "C:\SBMData\B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf",
// "p." = PDF page. Rules (user): model the plans; nothing invented - UNRESOLVED items name the page and a plain reason; GAP = no program
// feature / not modeled; precision 1/16" and 0.01 ft; differences reported, never fixed.
using System.IO;
using System.Globalization;
using SBM.Core;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Geometry;

string outPath = Args.Length > 0 ? Args[0] : (Doc.FilePath ?? @"samples\plans\Bonner Unit 5 segmental.sbm");
var inv = CultureInfo.InvariantCulture;
var unresolved = new List<string>();
var reported = new List<string>();
var gaps = new List<string>();
void U(string pages, string item, string why, string then = "") => unresolved.Add(UnresolvedFlag.Text(pages, item, why, then));
void R(string s) => reported.Add(s);
void G(string s) => gaps.Add(s);
double F(double ft, double inch = 0) => Ft(ft) + In(inch);

// ---------------------------------------------------------------- data folder + CSV reader
string dataDir = null;
for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
{
    var p = Path.Combine(dir.FullName, "docs", "plan-data", "bonner-b2500", "segmental-unit5");
    if (Directory.Exists(p)) { dataDir = p; break; }
}
if (dataDir == null) throw new InvalidOperationException("Run from the repo: docs/plan-data/bonner-b2500/segmental-unit5 not found above the current directory.");
List<string[]> Csv(string name)
{
    var rows = new List<string[]>();
    foreach (var line in File.ReadAllLines(Path.Combine(dataDir, name)).Skip(1))
    {
        if (line.Trim() == "") continue;
        var cells = new List<string>(); var cur = new System.Text.StringBuilder(); bool q = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (q) { if (c == '"') { if (i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; } else q = false; } else cur.Append(c); }
            else if (c == '"') q = true;
            else if (c == ',') { cells.Add(cur.ToString()); cur.Clear(); }
            else cur.Append(c);
        }
        cells.Add(cur.ToString());
        rows.Add(cells.ToArray());
    }
    return rows;
}
double D(string s) => double.Parse(s, inv);

// ================================================================ 1. alignment -L- over Unit 5 (general drawings p3-16, ../SUMMARY.md section 1)
// The unit (57+45.00 - 92+95.00) lies on curve 2 (PC 53+29.58, R 2,815.00 RT, delta 35 24'44.6", L 1,739.85, PT 70+69.43) and the tangent
// N 45 04'44.5" W to PC 3 102+46.84 (curve 3 R 6,050.00 RT). Back tangent N 80 29'29.1" W. No spirals, no station equations.
U("p.35 / p.36 (S0-34 / S0-35 location sketches)", "-L- state plane coordinates of the PIs / PCs", "the structure plans print no N / E for the -L- PI / PC / PT; only the GPS monument B2500-2 (N 751499.622, E 3031964.117 US ft, -L- STA 26+47.55, 23.82' RT) is given", "The alignment is placed by that monument (curve 1 PI 27+27.13 R 2,347 LT, printed tangent bearings, p.21-34 curve data; check: long chord 794.52 ft vs 794'-6 1/4\" printed); the direction of increasing stations (towards N 0 44' E) rests on the printed bearings + the location sketch. PC / PT N / E in docs/plan-data/bonner-b2500/L-coordinates.csv.");
const string AlName = "-L- (NC 12)";
Doc.Project.Alignments.RemoveAll(a => a.Name == AlName);
double Brg(int d, int m, double s, bool west) { double a = (d + m / 60.0 + s / 3600.0) * Math.PI / 180; return west ? Math.PI / 2 + a : Math.PI / 2 - a; }   // N ... W/E -> math angle
double Dms(int d, int m, double s) => (d + m / 60.0 + s / 3600.0) * Math.PI / 180;
double heading = Brg(80, 29, 29.1, true);
var elems = new List<HorizontalElement>();
// PC 2 (STA 53+29.58) in the plans' LOCALIZED NC State Plane (EPSG:2264, US survey ft; project metres = US ft x 1200 / 3937): derived from the GPS monument
// B2500-2 (p.35 / p.36) along curve 1 and the 1,773.51 ft tangent (docs/plan-data/bonner-b2500/L-coordinates.csv, tools: temp/bonner-geo/lgeom.py).
const double UsFt = 1200.0 / 3937.0;
var pc2 = new Vec2(3029357.322 * UsFt, 752089.964 * UsFt);
var cur = pc2 - Vec2.FromAngle(heading) * Ft(129.58);              // start 52+00.00 on the back tangent
void Tan(double lenFt) { var e = new HorizontalElement(cur, heading, Ft(lenFt), 0, 0); elems.Add(e); cur = e.EndPoint; }
void Arc(double rFt, double lenFt, bool right) { double k = (right ? -1 : 1) / Ft(rFt); var e = new HorizontalElement(cur, heading, Ft(lenFt), k, k); elems.Add(e); cur = e.EndPoint; heading = e.EndAngle; }
Tan(129.58);                                   // 52+00.00 - PC 53+29.58
Arc(2815.00, 1739.85, true);                   // curve 2 PC 53+29.58 - PT 70+69.43
Tan(10246.84 - 7069.43);                       // PT 70+69.43 - PC 3 102+46.84
Arc(6050.00, 300.0, true);                     // part of curve 3 (beyond the unit)
var al = new Alignment(elems) { Name = AlName, StartStation = Sta("52+00.00") };
al.Description = "-L- (NC 12), B-2500 general drawings p3-16; localized NC State Plane (PC 2 from monument B2500-2, p.35)";
Doc.Project.Alignments.Add(al);
{
    double dDelta = (Ft(1739.85) / Ft(2815.00) - Dms(35, 24, 44.6)) * Ft(2815.00);
    double tangentDiff = (heading - (elems[2].StartAngle)) ;
    double ahead = Brg(45, 4, 44.5, true);
    double dAng = (elems[2].StartAngle - ahead) * 180 / Math.PI * 3600;
    R($"Alignment: curve 2 R x delta - printed L = {dDelta / Ft(1):+0.000;-0.000} ft; ahead tangent from R / L vs printed N 45 04'44.5\" W: {dAng:+0.0;-0.0}\"");
    _ = tangentDiff;
}
// Profile: the 8 PVIs of the general drawings (p3-5, 8, 10, 16).
al.Profile = new VerticalProfile
{
    Vpis =
    {
        new Vpi(Sta("27+18.00"), Ft(6.57), Ft(490)), new Vpi(Sta("33+65.00"), Ft(32.34), Ft(602)), new Vpi(Sta("47+73.00"), Ft(32.34), Ft(1564)),
        new Vpi(Sta("59+35.00"), Ft(90.40), Ft(760)), new Vpi(Sta("91+45.00"), Ft(90.40), Ft(760)), new Vpi(Sta("103+02.00"), Ft(32.56), Ft(1554)),
        new Vpi(Sta("172+75.00"), Ft(32.56), Ft(650)), new Vpi(Sta("178+80.00"), Ft(7.38), Ft(560)),
    },
};
// Cross slope (S0-03 p4): .045 from 53+81.00 to 70+28.93, varies to 70+82.93, .025 to 102+46.34. DIRECTION: the geometry control section (S3-19
// p267, RASTER) "BENTS 18 THRU 29 (LOOKING UPSTATION)" draws the % CROSS SLOPE arrow down to the RIGHT (box CL rotated, DIM "B" to the right) -
// one plane across the box, high on the left (outside of the right-hand curve 2). Transition shape: S0-03 says "varies" ("see roadway plans").
al.CrossSlope = new CrossSlopeTable
{
    PivotOffset = 0,
    Rows = { new CrossSlopeRow(Sta("53+81.00"), 0.045, -0.045), new CrossSlopeRow(Sta("70+28.93"), 0.045, -0.045),
             new CrossSlopeRow(Sta("70+82.93"), 0.025, -0.025), new CrossSlopeRow(Sta("102+46.34"), 0.025, -0.025) },
};
R("Cross slope direction RESOLVED from the S3-19 (p267) section 'BENTS 18 THRU 29 (LOOKING UPSTATION)': slope arrow down to the right, box CL offset DIM \"B\" to the right.");
U("p4", "cross slope transition 70+28.93 - 70+82.93 (4.5% to 2.5%)", "S0-03 prints 'varies' and refers to the roadway plans, which are not in this set", "Linear in station between the two printed slopes.");

// ================================================================ 2. supports = bents 18-29 (S3-19 p267, ../bents.csv)
const string BridgeName = "Bonner Unit 5";
Doc.Project.Bridges.RemoveAll(b => b.Name == BridgeName);
var bentSta = new (int bent, string sta)[] { (18, "57+45.00"), (19, "59+45.00"), (20, "62+95.00"), (21, "66+45.00"), (22, "69+95.00"), (23, "73+45.00"),
    (24, "76+95.00"), (25, "80+45.00"), (26, "83+95.00"), (27, "87+45.00"), (28, "90+95.00"), (29, "92+95.00") };
var br = new BridgeDefinition { Name = BridgeName, AlignmentName = AlName, Structure = "B-2500 Bonner Bridge replacement (NC 12 over Oregon Inlet)" };
br.FieldSplices.Clear();
br.Girders.Count = 0;
br.Girders.Spacings = new();
br.Deck.Barrier.Left = false; br.Deck.Barrier.Right = false;
br.Deck.Reinforcement.Enabled = false;
br.Deck.LeftEdgeOffset = -F(21, 3.5); br.Deck.RightEdgeOffset = F(21, 3.5);
foreach (var (n, sta) in bentSta)
    br.Supports.Add(new SupportDefinition
    {
        Name = $"Bent {n}", Type = SupportType.Pier, Station = Sta(sta), SkewDegrees = 0,
        Fixity = n is 23 or 24 ? BearingFixity.Fixed : BearingFixity.Expansion,
        Substructure = new SubstructureDefinition { Enabled = false },     // the segmental generator builds the bents
    });

// ================================================================ 3. segmental definition
var sg = new SegmentalDefinition { Source = "p.249-392 (S3)" };
// Box section S3-20 p268 (box-section-geometry.csv): the defaults of SegmentalBoxSection ARE the plan values (42'-7", 9", 5'-1", 5'-6", 2'-2", 10",
// 4'-1", 9'-4", 4:1, 1'-4", 6" x 2'-5 1/2", R 1'-0"); set explicitly here so the script states its source.
sg.Box = new SegmentalBoxSection
{
    TopWidth = F(42, 7), TopSlab = In(9), TopSlabFlat = F(5, 1), TopHaunchRun = F(5, 6), WebTopDepth = F(2, 2), WingTip = In(10), WingFlat = F(4, 1),
    TipToWeb = F(9, 4), WebSlope = 4, WebThickness = F(1, 4), BottomHaunchRise = In(6), BottomHaunchRun = F(2, 5.5), FilletRadius = F(1),
};
foreach (var r in Csv("box-sections.csv"))
    sg.DepthTable.Add(new SegmentalDepthSection { Number = int.Parse(r[0]), X = Ft(D(r[1])), Depth = In(D(r[3])), HalfSoffitPrinted = In(D(r[6])), BottomSlab = In(D(r[10])) });
// Depth curve between the printed sections: RECONSTRUCTION (flagged by the generator): parabola, vertex x = 159 ft at 9'-0", through section 1
// (19'-0" at x = 5 ft): H = 108 in + (120 / 154^2) in/ft^2 x (159 - x)^2; max residual vs the table 0.051".
sg.ParabolaVertexX = Ft(159); sg.ParabolaVertexDepth = In(108);
sg.ParabolaCoefficient = In(120.0 / (154.0 * 154.0)) / (Ft(1) * Ft(1));
// Bent segment (S3-29 p277, RASTER 130 dpi): 19'-3" at the bent CL; the soffit is 3" lower than at the joint (section "2'-3"" = 2'-0" slab + 3"),
// the step drawn within the 1'-6" next to the bulkhead face; pier diaphragm from 1'-6" off the bulkhead face to the split CL (3'-6" each half).
sg.BentSoffitTransition = F(1, 6);
sg.BentDiaphragm = F(3, 6);
sg.ConstantDepth = F(9); sg.ConstantBottomSlab = In(10);
// Spans and segments: segments.csv (275 rows, x along -L- from the span's start bent).
var spans = new Dictionary<int, SegmentalSpan>();
foreach (var r in Csv("segments.csv"))
{
    int span = int.Parse(r[0]);
    if (!spans.TryGetValue(span, out var sp)) spans[span] = sp = new SegmentalSpan { Span = span - 19, Name = $"Span {span}" };
    string name = r[1];
    var type = r[2] switch { "end_segment" => SegmentType.End, "closure_joint" => SegmentType.Closure, "bent_half" => SegmentType.BentHalf, _ => SegmentType.Typical };
    string bent = ""; int idx = 0;
    if (type is SegmentType.Typical or SegmentType.BentHalf)
    {
        var parts = name.Split('-');
        bent = "Bent " + parts[0];
        idx = parts[1] is "UB" or "DB" ? 0 : int.Parse(parts[1].Substring(1));
    }
    sp.Segments.Add(new SegmentDefinition { Name = name, Type = type, X0 = Ft(D(r[3])), X1 = Ft(D(r[4])), Bent = bent, Index = idx });
}
sg.Spans = spans.OrderBy(k => k.Key).Select(k => k.Value).ToList();
U("p.263, 268", "relation of the span lengths (horizontal, along -L-) to the segment lengths (chords along the girder CL)",
  "the plans state both conventions and give no conversion or casting geometry", "Joints are placed at their -L- stations; each segment is the straight 3D chord between its joint WPs; the chord offset from -L- and the chord-minus-station length are reported by the generator.");
U("p.271", "end segment start 10\" from the bent 18 / 29 control line", "derived (9'-6\" to the closure face minus 8'-8\"), not printed", "Used as derived.");
U("p.267", "casting curve / camber / segment survey geometry", "not in the plans (geometry control gives final WP positions only)", "Box modeled at the final profile, no camber.");

// Bents: depth at the CL, bearings (bearing-data.csv S3-71 p319; positions p354 / p359 / p363 / p367 / p371 / p277; sizes p318) and piers.
var bearingRows = Csv("bearing-data.csv");
var pedTop = new Dictionary<string, (double L, double R)>   // CIP bearing pedestal top EL (cap sheets' ELEVATION TABLE / front view)
{
    ["18"] = (69.93, 69.19), ["19"] = (65.24, 64.71), ["20"] = (69.74, 69.27), ["21"] = (69.74, 69.27), ["22"] = (69.74, 69.27),
    ["23"] = (69.62, 69.36), ["24"] = (69.62, 69.36), ["25"] = (69.62, 69.36), ["26"] = (69.62, 69.36), ["27"] = (69.62, 69.36), ["28"] = (66.04, 65.78),
    ["29"] = (71.20, 70.79),
};
var pierRows = Csv("piers.csv").ToDictionary(r => r[0]);
foreach (var (n, _) in bentSta)
{
    string k = n.ToString();
    bool end = n is 18 or 29;
    var b = new SegmentalBent { Name = $"Bent {n}", DepthAtCl = end ? double.NaN : F(19, 3) };
    foreach (var side in new[] { "LEFT", "RIGHT" })
    {
        var row = bearingRows.First(r => r[0] == $"BENT {n} {side}");
        bool left = side == "LEFT";
        var bb = new SegmentalBearing
        {
            Name = left ? "left bearing" : "right bearing", Type = row[1], Height = In(D(row[16])),
            // end bents: CL bearing 8'-3" each side of CL girder (p354 / p371) and 5'-6" from the bent control line into the span (p267, p354);
            // cantilever bents: 5'-3" each side of CL (p359 / p363 / p367 pedestal plans "SYM. ABOUT CL - 5'-3" - GIRDER BRG. LINE", p277 section) on the bent CL.
            Offset = (left ? -1 : 1) * (end ? F(8, 3) : F(5, 3)),
            Along = n == 18 ? F(5, 6) : n == 29 ? -F(5, 6) : 0,
            PedestalTop = Ft(left ? pedTop[k].L : pedTop[k].R),
            PedestalAcross = end ? F(6, 2) : F(6), PedestalAlong = end ? F(4, 2) : F(6),
        };
        // Bearing body = masonry plate plan maxima (p318 PLAN DIMENSION SUMMARY TABLE C x D): 18: 3'-10" x 3'-6"; 19-22, 25-28: 5'-6" x 5'-6"; 29: 3'-8" x 3'-6".
        if (n is 23 or 24)
            bb.Unresolved = UnresolvedFlag.Text("p.318", $"bearing size at bent {n}", "the plan dimension summary table covers bents 18-22 and 25-29 only; bents 23 & 24 have their own masonry plate plan without a size table", "Bearing recorded (type, height, position), no solid.");
        else
        {
            (bb.SizeAcross, bb.SizeAlong) = n == 18 ? (F(3, 10), F(3, 6)) : n == 29 ? (F(3, 8), F(3, 6)) : (F(5, 6), F(5, 6));
            bb.Unresolved = UnresolvedFlag.Text("p.318", "disc bearing body", "the plans give the masonry plate MAX plan size (C x D, orientation of C not stated) and an assumed bearing height, not the vendor bearing",
                "Generic block: C across the bent line x D along -L- x assumed height, top on the pedestal top + height.");
        }
        b.Bearings.Add(bb);
    }
    // Pier (piers.csv; cap sheets p354-375; column sheets p376-381; foundation layout p251-253).
    var pr = pierRows[k];
    var pier = new SegmentalPier
    {
        CapTop = Ft(D(pr[2])), CapHeight = end ? F(5) : F(5, 6), ColumnBaseTop = Ft(17.00),
        FootingTop = Ft(D(pr[3].Split(' ')[0])), FootingBottom = Ft(D(pr[4])), PileCount = int.Parse(pr[8]),
    };
    if (end)
    {
        // T-shaped cap (p354 bent 18; p371 bent 29 opposite hand): 23'-0" x 9'-6" toward the unit-5 span + 43'-6" x 6'-6" toward the adjoining unit.
        double sgn = n == 18 ? 1 : -1;
        var stem = new[] { (-11.5, 0.0), (11.5, 0.0), (11.5, 9.5), (-11.5, 9.5) };
        pier.CapOutline = new List<PlanPoint> { new PlanPoint(-Ft(21.75), -sgn * Ft(6.5)), new PlanPoint(Ft(21.75), -sgn * Ft(6.5)), new PlanPoint(Ft(21.75), 0), new PlanPoint(Ft(11.5), 0), new PlanPoint(Ft(11.5), sgn * Ft(9.5)),
            new PlanPoint(-Ft(11.5), sgn * Ft(9.5)), new PlanPoint(-Ft(11.5), 0), new PlanPoint(-Ft(21.75), 0) };
        _ = stem;
        pier.Unresolved.Add(UnresolvedFlag.Text(n == 18 ? "p.354" : "p.371", $"bent {n} cap re-entrant corners", "the 1'-0\" chamfers at the T junction are drawn, the outline is simplified", "Square T outline."));
    }
    else
    {
        // p359 / p363 / p367: 18'-0" x 13'-0", 5'-6" high, lower 3'-0" tapered to 17'-4" x 12'-4" (4" per side).
        pier.CapOutline = new List<PlanPoint> { new PlanPoint(-Ft(9), -Ft(6.5)), new PlanPoint(Ft(9), -Ft(6.5)), new PlanPoint(Ft(9), Ft(6.5)), new PlanPoint(-Ft(9), Ft(6.5)) };
        pier.CapTaperInset = In(4); pier.CapTaperHeight = F(3);
    }
    if (n is 23 or 24)
        pier.Unresolved.Add(UnresolvedFlag.Text("p.380", $"bent {n} precast column size", "the column segment sheet for bents 23 & 24 was not resolved to an outside size", "No column solid."));
    else
    {
        // p376-381: precast hollow column 16'-0" x 11'-0" (16'-0" = 1'-6" + 6'-6" + 6'-6" + 1'-6" walls along the bent line), match cast.
        pier.ColumnS = F(16); pier.ColumnN = F(11);
        pier.Unresolved.Add(UnresolvedFlag.Text("p.376-381", $"bent {n} column", "the hollow core (wall in the 11'-0\" direction), the CIP base outline and the 16'-0\" orientation are not resolved",
            "Solid 16'-0\" (along the bent line) x 11'-0\" envelope from the footing top to the cap bottom (incl. the 3\" grout bed)."));
    }
    // Footings: p252 (bents 22-25) read on the raster: 51'-0" across -L- x 36'-0" along -L-; 23, 24 octagon 51'-0" x 58'-6" with 15'-0" x 21'-3" corner cuts;
    // lateral split of the 51'-0" about -L-: bent 22 26'-4 3/8" left / 24'-7 5/8" right, 23-25 25'-11 3/4" / 25'-0 1/4".
    string plan = pr[9];
    double across, along;
    if (plan.StartsWith("58'-6\"")) { across = Ft(51); along = F(58, 6); }
    else if (plan.Contains("43'-6\"")) { across = F(43, 6); along = Ft(36); }
    else if (plan.Contains("51'-0\"")) { across = Ft(51); along = Ft(36); }
    else { across = Ft(36); along = Ft(36); }
    double sOff = n == 22 ? -(F(26, 4.375) - F(24, 7.625)) / 2 : n is 23 or 24 or 25 ? -(F(25, 11.75) - F(25, 0.25)) / 2 : 0;
    if (n is 23 or 24)
        pier.FootingOutline = new List<PlanPoint> { new PlanPoint(sOff - across / 2, -Ft(8)), new PlanPoint(sOff - across / 2, Ft(8)), new PlanPoint(sOff - across / 2 + Ft(15), along / 2), new PlanPoint(sOff + across / 2 - Ft(15), along / 2),
            new PlanPoint(sOff + across / 2, Ft(8)), new PlanPoint(sOff + across / 2, -Ft(8)), new PlanPoint(sOff + across / 2 - Ft(15), -along / 2), new PlanPoint(sOff - across / 2 + Ft(15), -along / 2) };
    else
        pier.FootingOutline = new List<PlanPoint> { new PlanPoint(sOff - across / 2, -along / 2), new PlanPoint(sOff + across / 2, -along / 2), new PlanPoint(sOff + across / 2, along / 2), new PlanPoint(sOff - across / 2, along / 2) };
    if (n is not (22 or 23 or 24 or 25))
        pier.Unresolved.Add(UnresolvedFlag.Text(n <= 21 ? "p.251" : "p.253", $"bent {n} footing orientation / position", "only foundation layout sheet 2 (p252, bents 22-25) was read on the raster",
            $"{plan} footing with the larger size across -L-, centred on the bent WP."));
    pier.Unresolved.Add(UnresolvedFlag.Text("p.251-253, 334-353", $"bent {n} footing top", $"the top is sloped to the edges ({pr[3]})", "Flat footing at the CL elevation."));
    b.Pier = pier;
    sg.Bents.Add(b);
}
G("Piles: 284 x 36\" sq PS piles at 2:12 batter (p.251-253 layouts) - counts on the footings only, piles not modeled.");

// Tendons: pt-tendon-schedule.csv (S3-57 / 58), anchor segments cantilever-tendon-anchors.csv (DERIVED from plan labels p285-296).
// Ducts (S3-35 p283, RASTER 140 dpi): top slab rows A (7 1/4" below the top) and B (9" lower), ducts 1-11 per half at 15'-8 1/2", 14'-11 1/2",
// 14'-2 1/2", 13'-5 1/2", 12'-4 1/2", 11'-3 1/2", 10'-2 1/2", 9'-1 1/2", 8'-4 1/2", 7'-7 1/2", 6'-10 1/2" from the CL; row C (bottom slab) ducts 1-7
// at 9" from 1'-6" inside the soffit corner (mid-span section, W/2 = 10'-3").
double[] rowA = { F(15, 8.5), F(14, 11.5), F(14, 2.5), F(13, 5.5), F(12, 4.5), F(11, 3.5), F(10, 2.5), F(9, 1.5), F(8, 4.5), F(7, 7.5), F(6, 10.5) };
double rowADepth = In(7.25), rowBDepth = In(16.25);
double[] rowC = Enumerable.Range(0, 7).Select(i => F(10, 3) - F(1, 6) - In(9) * i).ToArray();
string ductFlag = UnresolvedFlag.Text("p.283-306", "tendon duct assignment and profiles", "S3-35 gives the duct grid but no table of which designation uses which duct; profiles (P.I. heights, anchor blisters) are drawn, not tabulated",
    "Cantilever Tn in row A duct n (n <= 11), T12 / T13 / T14 in row B ducts 4 / 6 / 8; C1 / C2 in row B ducts 5 / 7; B1-B7 in row C ducts 1-7 at mid bottom slab (5\" above the soffit); straight runs joint to joint.");
string layoutFlag = UnresolvedFlag.Text("p.285-306", "anchor segments of the bottom (B) and closure (C) tendons", "the schedule gives lengths only and the layouts are drawn without anchor stations",
    "Spans 20-28: centred on the closure joint; spans 19 / 29: from the end segment anchor face, plan length.");
var anchors = Csv("cantilever-tendon-anchors.csv");
double ParseFtIn(string s) => Len(s);
foreach (var r in Csv("pt-tendon-schedule.csv"))
{
    string loc = r[0], name = r[1];
    var t = new TendonDefinition
    {
        Name = name, Count = int.Parse(r[2]), Strands = int.Parse(r[3]), PlanLength = ParseFtIn(r[4]), PlanWeightEachLb = D(r[5]),
        JackingForceKip = D(r[7]), StressingEnd = r[8], Location = loc.StartsWith("BENT") ? "Bent " + loc.Substring(5) : "Span " + loc.Substring(5),
    };
    int num = int.Parse(name.Substring(1));
    if (name[0] == 'T')
    {
        t.Group = TendonGroup.Cantilever; t.Layout = TendonLayout.Cantilever;
        var a = anchors.First(x => x[0] == loc.Substring(5) && x[1] == name);
        int idx = int.Parse(a[5]);
        t.AnchorDown = idx; t.AnchorUp = idx;
        if (loc == "BENT 19" && name == "T14") { t.AnchorDown = 13; t.AnchorUp = 12; }
        if (loc == "BENT 28" && name == "T14") { t.AnchorDown = 12; t.AnchorUp = 13; }
        if (num <= 11) { t.DuctOffset = rowA[num - 1]; t.DuctDepth = rowADepth; }
        else { t.DuctOffset = rowA[(num == 12 ? 4 : num == 13 ? 6 : 8) - 1]; t.DuctDepth = rowBDepth; }
        t.Unresolved = ductFlag;
    }
    else if (name[0] == 'B')
    {
        t.Group = TendonGroup.Bottom;
        t.Layout = loc is "SPAN 19" or "SPAN 29" ? TendonLayout.FromEndSegment : TendonLayout.CentredOnClosure;
        t.DuctOffset = rowC[num - 1]; t.DuctDepth = In(5); t.AboveSoffit = true;
        t.Unresolved = ductFlag + " " + layoutFlag;
    }
    else
    {
        t.Group = TendonGroup.Closure;
        t.Layout = loc is "SPAN 19" or "SPAN 29" ? TendonLayout.FromEndSegment : TendonLayout.CentredOnClosure;
        t.DuctOffset = rowA[(num == 1 ? 5 : 7) - 1]; t.DuctDepth = rowBDepth;
        t.Unresolved = ductFlag + " " + layoutFlag;
    }
    sg.Tendons.Add(t);
}
U("p.305", "T6 only at bents 22-25 and T14 only at bents 19 and 28", "printed so in the schedule without explanation (T6 drawn dashed with T5)", "Modeled as printed.");
// Transverse PT (transverse-pt-stressing.csv): 4 x 0.6" strands, 41'-7", 45.5 kip; positions along the segment not dimensioned.
string trFlag = UnresolvedFlag.Text("p.272, 278, 282", "transverse tendon positions", "the sheets give counts per segment, length and stressing, the duct positions are drawn only",
    "Equally spaced along each segment, straight across at 4 1/2\" below the top (mid top slab at the CL).");
void Tr(SegmentType type, double len, string name, int per, double wPerSeg) =>
    sg.Transverse.Add(new TransverseTendonDefinition { SegmentType = type, SegmentLength = len, Name = name, PerSegment = per, Strands = 4, PlanLength = F(41, 7),
        PlanWeightPerSegmentLb = wPerSeg, JackingForceKip = 45.5, Depth = In(4.5), Unresolved = trFlag });
Tr(SegmentType.Typical, 0, "TT", 4, 492.4);
Tr(SegmentType.Closure, F(4), "TT", 2, 246.2);
Tr(SegmentType.Closure, F(3, 6), "TT", 1, 123.1);
Tr(SegmentType.BentHalf, 0, "F", 2, 245.6);
Tr(SegmentType.End, 0, "E", 4, 492.4);
U("p.278", "number of bent segments with transverse tendons F1 & F2 / vertical PT bars", "the S3-30 table lists 18 (and 9 for the longitudinal bars) while 10 cantilever bents have 20 split halves",
  "Each of the 20 bent halves gets F1 & F2 (difference reported by the check).");

// Plan items not modeled (GAP) and remaining open items.
G("Temporary 1 3/4\" PT bars and their anchor blocks (p299-301, 313); bent segment vertical / longitudinal PT bars and end segment vertical bars (p272, 278) - not modeled.");
G("Future PT pipes F1-F4 in the closure joint deviators (p308) and the deviator / anchor blocks (p297-302) - not modeled.");
G("Shear keys, alignment keys, epoxy joints (p283) - joints are plain planes; mild reinforcing (p269-282) - not modeled.");
G("Parapets / 2-bar metal rail (S3-75..83, cast after all PT), modular expansion joints at bents 18 / 29 (S2 / S4 sheets), lighting, access openings, survey blockouts - not modeled.");
U("S2 / S4 sheets", "expansion joint gap and devices at bents 18 and 29", "the joint details are bound with the S2 / S4 drawings (not read)", "The end segments end at their derived faces; no joint.");
sg.Unresolved.AddRange(unresolved);
sg.Reported.AddRange(reported);
br.Segmental = sg;
Doc.Project.Bridges.Add(br);

// ================================================================ 4. regenerate, save, report
var rep = Doc.Regenerate();
Print($"Regenerated {rep.Bridges} bridge(s) in {rep.Elapsed.TotalSeconds:0.0} s; {rep.Warnings.Count} warnings, {rep.Errors.Count} errors");
foreach (var e in rep.Errors) Print("ERROR " + e);
var sm = Doc.Segmental(BridgeName);
Print($"Segments {sm.Segments.Count}, tendons {sm.Tendons.Count}, bearings {sm.Bearings.Count}, bents with substructure {sm.Substructure?.Units.Count ?? 0}");
Print($"Box concrete {sm.ConcreteVolume / Math.Pow(Ft(1), 3) / 27:0} CY");
Print("GAP (not modeled):");
foreach (var g in gaps) Print("  " + g);
Doc.Save(outPath);
Print($"Saved {outPath}");
