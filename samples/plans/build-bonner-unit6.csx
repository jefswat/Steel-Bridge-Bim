// FINISH (required, see CLAUDE.md "New plan-set model checklist" / docs/MODEL-PIPELINE.md): `sbm run <this script> <model.sbm>` runs `sbm pipeline` after the
// script saves = regenerate, terrain (georeferenced projects), plan sheet index, companion database + sheet links; UNRESOLVED items are printed.
// NCDOT B-2500 (Bonner Bridge replacement, NC 12 over Oregon Inlet), UNIT 6 = spans 30-35, bents 29-35, 965'-0" along -L- (6 x 160'-10"):
// four lines of 96" precast prestressed Florida I-Beams (FIB-96), simple spans made continuous for live load, 9" deck, precast caps on precast
// segmental columns, CIP footings on battered 36" square prestressed piles.
// From scratch (re-runnable; replaces the -L- alignment and the bridge "Bonner Unit 6"), run from the repo root:
//   sbm new  "samples\plans\Bonner Unit 6 FIB.sbm"
//   sbm run  "samples\plans\build-bonner-unit6.csx" "samples\plans\Bonner Unit 6 FIB.sbm"
//   sbm run  "samples\plans\check-bonner-unit6.csx" "samples\plans\Bonner Unit 6 FIB.sbm"
// Data: docs/plan-data/bonner-b2500/SUMMARY.md (general drawings) and docs/plan-data/bonner-b2500/fib-unit6/ (SUMMARY.md + CSVs, read by this
// script). PDF "C:\SBMData\B-2500 Bonner - Revision 3 Combined Plan Set - Unofficial.pdf", "p." = PDF page. Rules (user): model the plans;
// nothing invented - UNRESOLVED items name the page and a plain reason; GAP = no program feature / not modeled; precision 1/16" and 0.01 ft;
// differences reported, never fixed.
using System.IO;
using System.Globalization;
using SBM.Core;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Prestressed;

string outPath = Args.Length > 0 ? Args[0] : (Doc.FilePath ?? @"samples\plans\Bonner Unit 6 FIB.sbm");
var inv = CultureInfo.InvariantCulture;
var unresolved = new List<string>();
var gaps = new List<string>();
string U(string pages, string item, string why, string then = "") { var t = UnresolvedFlag.Text(pages, item, why, then); unresolved.Add(t); return t; }
void G(string s) => gaps.Add(s);
double F(double ft, double inch = 0) => Ft(ft) + In(inch);

// ---------------------------------------------------------------- data folder + CSV reader
string dataDir = null;
for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
{
    var p = Path.Combine(dir.FullName, "docs", "plan-data", "bonner-b2500", "fib-unit6");
    if (Directory.Exists(p)) { dataDir = p; break; }
}
if (dataDir == null) throw new InvalidOperationException("Run from the repo: docs/plan-data/bonner-b2500/fib-unit6 not found above the current directory.");
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

// ================================================================ 1. alignment -L- (identical to build-bonner-unit5.csx: same placement)
// Curve 2 (PC 53+29.58, R 2,815.00 RT, L 1,739.85), tangent N 45 04'44.5" W to PC 3 102+46.84, curve 3 R 6,050.00 RT. Unit 6 = 92+95.00 -
// 102+60.00: tangent, span 35 tangent then arc (PC 102+46.84). Placed in localized NC State Plane (PC 2 coordinates below).
U("p.35 / p.36 (S0-34 / S0-35 location sketches)", "-L- state plane coordinates of the PIs / PCs", "the structure plans print no N / E for the -L- PI / PC / PT; only the GPS monument B2500-2 (N 751499.622, E 3031964.117 US ft, -L- STA 26+47.55, 23.82' RT) is given", "The alignment is placed by that monument (curve 1 PI 27+27.13 R 2,347 LT, printed tangent bearings, p.21-34 curve data; check: long chord 794.52 ft vs 794'-6 1/4\" printed); the direction of increasing stations (towards N 0 44' E) rests on the printed bearings + the location sketch. PC / PT N / E in docs/plan-data/bonner-b2500/L-coordinates.csv.");
const string AlName = "-L- (NC 12)";
Doc.Project.Alignments.RemoveAll(a => a.Name == AlName);
double Brg(int d, int m, double s, bool west) { double a = (d + m / 60.0 + s / 3600.0) * Math.PI / 180; return west ? Math.PI / 2 + a : Math.PI / 2 - a; }
double heading = Brg(80, 29, 29.1, true);
var elems = new List<HorizontalElement>();
// PC 2 (STA 53+29.58) in the plans' LOCALIZED NC State Plane (EPSG:2264, US survey ft; project metres = US ft x 1200 / 3937): derived from the GPS monument
// B2500-2 (p.35 / p.36) along curve 1 and the 1,773.51 ft tangent (docs/plan-data/bonner-b2500/L-coordinates.csv, tools: temp/bonner-geo/lgeom.py).
const double UsFt = 1200.0 / 3937.0;
var pc2 = new Vec2(3029357.322 * UsFt, 752089.964 * UsFt);
var cur = pc2 - Vec2.FromAngle(heading) * Ft(129.58);
void Tan(double lenFt) { var e = new HorizontalElement(cur, heading, Ft(lenFt), 0, 0); elems.Add(e); cur = e.EndPoint; }
void Arc(double rFt, double lenFt, bool right) { double k = (right ? -1 : 1) / Ft(rFt); var e = new HorizontalElement(cur, heading, Ft(lenFt), k, k); elems.Add(e); cur = e.EndPoint; heading = e.EndAngle; }
Tan(129.58);                                   // 52+00.00 - PC 53+29.58
Arc(2815.00, 1739.85, true);                   // curve 2
Tan(10246.84 - 7069.43);                       // PT 70+69.43 - PC 3 102+46.84
Arc(6050.00, 300.0, true);                     // part of curve 3 (span 35 and beyond)
var al = new Alignment(elems) { Name = AlName, StartStation = Sta("52+00.00") };
al.Description = "-L- (NC 12), B-2500 general drawings p3-16; localized NC State Plane (PC 2 from monument B2500-2, p.35)";
Doc.Project.Alignments.Add(al);
al.Profile = new VerticalProfile
{
    Vpis =
    {
        new Vpi(Sta("27+18.00"), Ft(6.57), Ft(490)), new Vpi(Sta("33+65.00"), Ft(32.34), Ft(602)), new Vpi(Sta("47+73.00"), Ft(32.34), Ft(1564)),
        new Vpi(Sta("59+35.00"), Ft(90.40), Ft(760)), new Vpi(Sta("91+45.00"), Ft(90.40), Ft(760)), new Vpi(Sta("103+02.00"), Ft(32.56), Ft(1554)),
        new Vpi(Sta("172+75.00"), Ft(32.56), Ft(650)), new Vpi(Sta("178+80.00"), Ft(7.38), Ft(560)),
    },
};
// Cross slope (S0-03 p4): .025 from 70+82.93 to 102+46.34, varies to 102+59.84, .030 beyond. DIRECTION: one plane, high on the LEFT - the
// pedestal elevations of every Unit 6 bent fall from "A" (G1) to "D" (G4) by 0.29 ft per 11'-6" space = 0.025 (p483 / p486), and G1 is the
// LEFT girder: in the curved span 35 its bearing-to-bearing chord is the longest (158'-4 7/16" vs G4 158'-3 9/16", p424), i.e. outside the
// right-hand curve 3. Same plane direction as Unit 5 (S3-19 p267).
al.CrossSlope = new CrossSlopeTable
{
    PivotOffset = 0,
    Rows = { new CrossSlopeRow(Sta("53+81.00"), 0.045, -0.045), new CrossSlopeRow(Sta("70+28.93"), 0.045, -0.045),
             new CrossSlopeRow(Sta("70+82.93"), 0.025, -0.025), new CrossSlopeRow(Sta("102+46.34"), 0.025, -0.025),
             new CrossSlopeRow(Sta("102+59.84"), 0.030, -0.030) },
};
U("p4", "cross slope transition 102+46.34 - 102+59.84 (2.5% to 3.0%)", "S0-03 prints 'varies' and refers to the roadway plans, which are not in this set", "Linear in station between the two printed slopes (span 35, last 13.5 ft).");

// ================================================================ 2. supports = bents 29-35 (p395, unit6-spans-bents.csv)
const string BridgeName = "Bonner Unit 6";
Doc.Project.Bridges.RemoveAll(b => b.Name == BridgeName);
var bentSta = new (int bent, string sta)[] { (29, "92+95.00"), (30, "94+55.83"), (31, "96+16.67"), (32, "97+77.50"), (33, "99+38.33"), (34, "100+99.17"), (35, "102+60.00") };
var br = new BridgeDefinition { Name = BridgeName, AlignmentName = AlName, Structure = "B-2500 Bonner Bridge replacement (NC 12 over Oregon Inlet)" };
br.FieldSplices.Clear();

// ---- substructure per bent (bent-columns-caps.csv, pedestals-cap-elevations.csv, piles.csv; p458-468, p481-486)
var capRows = Csv("bent-columns-caps.csv").ToDictionary(r => int.Parse(r[0]));
var pileRows = Csv("piles.csv").ToDictionary(r => int.Parse(r[0]));
// Footing plans (RASTER p459 bents 30 & 31, p462 bent 32, p465 bents 33 & 35, p468 bent 34): footing size, pile positions (S along the bent
// control line from -L-, N normal) and the 2:12 batter arrows (corner / end piles batter outward along the bent line, the other edge piles
// outward normal to it).
(double L, double W, double[] rowS, double[] endS) Ftg(int n) => n switch
{
    30 or 31 => (F(43, 6), F(21), new[] { -18.75, -11.25, -3.75, 3.75, 11.25, 18.75 }, new[] { -18.75, 18.75 }),
    32 => (F(37), F(21), new[] { -15.5, -7.75, 0, 7.75, 15.5 }, new[] { -15.5, 15.5 }),
    34 => (F(28, 6), F(14), new[] { -11.25, -3.75, 3.75, 11.25 }, Array.Empty<double>()),
    _ => (F(28, 6), F(14), new[] { -11.25, 0, 11.25 }, Array.Empty<double>()),
};
string ftgPage(int n) => n switch { 30 or 31 => "p.459", 32 => "p.462", 34 => "p.468", _ => "p.465" };
SubstructureDefinition Bent(int n)
{
    var c = capRows[n];
    double capTop = Ft(D(c[4])), capH = F(5, 6), col = n <= 32 ? F(6) : F(5);
    var (fl, fw, rowS, endS) = Ftg(n);
    double rowN = n <= 32 ? F(7, 6) : F(4);
    double colS = -F(10, 3);                               // column 1 at 10'-3" left of the work line (p458 / p481)
    var piles = new List<FootingPoint>();
    double maxS = rowS.Max(Math.Abs);
    foreach (double nn in new[] { -rowN, rowN })
        foreach (double s in rowS)
        {
            bool end = Math.Abs(Math.Abs(s) - maxS) < 1e-6;
            piles.Add(new FootingPoint(Ft(s) - colS, nn, end ? Math.Sign(s) : 0, end ? 0 : Math.Sign(nn)));
        }
    foreach (double s in endS) piles.Add(new FootingPoint(Ft(s) - colS, 0, Math.Sign(s), 0));
    int planPiles = int.Parse(pileRows[n][2]);
    if (piles.Count != planPiles) throw new InvalidOperationException($"Bent {n}: {piles.Count} piles placed vs {planPiles} in piles.csv");
    // Pile length: head 4'-0" above the footing bottom (p458 elevation / side view, RASTER), tip at the "pile tip no higher than" elevation of the
    // pile table (p398-399; installed tips are blank) -> along the 2:12 batter. Bents 30 / 31 reproduce the 14 piles = 1,611 LF quantity (p460).
    double bof = Ft(5.50) - Ft(6), head = bof + F(4), tip = Ft(D(pileRows[n][4]));
    double pileLen = (head - tip) * Math.Sqrt(1 + 1.0 / 36);
    var sd = new SubstructureDefinition
    {
        CapWidth = F(6, 6), CapDepth = capH, CapOffset = 0,
        // precast cap 42'-0" x 6'-6" x 5'-6", level top at the printed "TOP OF P/C BENT CAP EL." (p481, p483 / p486)
        CapProfile = { new CapProfilePoint(-Ft(21), capTop - capH), new CapProfilePoint(Ft(21), capTop - capH), new CapProfilePoint(Ft(21), capTop), new CapProfilePoint(-Ft(21), capTop) },
        // CIP pedestals 5'-2" along the bent line (2'-7" each side of CL girder) x 6'-0" (3'-0" each side of the bent control line, p481 plan);
        // back and ahead bearings carry different printed tops ("A" vs "AA", p483) -> two 3'-0" rows abutting at the bent control line.
        PedestalSizeAlong = F(5, 2), PedestalSizeAcross = F(3), PedestalSizeAcrossAhead = F(3), PedestalRowJoint = 0,
        ColumnCount = 2, ColumnSpacings = new() { F(20, 6) }, FirstColumnOffset = colS,
        ColumnShape = ColumnShape.Rectangular, ColumnWidth = col, ColumnDepth = col,
        Foundation = FoundationType.PileCap, FoundationTopElevation = Ft(5.50), FootingThickness = F(6),
        FootingLength = fl, FootingWidth = fw,
        MaxPedestalHeight = F(2),                      // plan pedestals up to 1.59 ft (bent 35, p486)
        PileType = PileType.PrecastConcrete, PileSize = In(36), PileSection = "36\" SQ PS", PileEmbedment = F(4), PileLength = pileLen,
        Footings =
        {
            new FootingDefinition
            {
                Column = 1, BatterRatio = 6, PileLength = pileLen, Piles = piles,
                Outline = { new FootingPoint(-fl / 2 - colS, -fw / 2), new FootingPoint(fl / 2 - colS, -fw / 2), new FootingPoint(fl / 2 - colS, fw / 2), new FootingPoint(-fl / 2 - colS, fw / 2) },
            },
            new FootingDefinition { Column = 2, OnFootingOf = 1 },
        },
        Unresolved = new()
        {
            UnresolvedFlag.Text(n <= 32 ? "p.458, 477" : "p.464, 467, 479", "precast segmental columns", $"the column is {c[2]} with 1 3/4\" PT bars in 3\" ducts and 2\" grout beds; SBM has no segmental column",
                $"One solid {(n <= 32 ? "6'-0\" x 6'-0\"" : "5'-0\" x 5'-0\"")} column from the footing top to the cap soffit (4\" corner radius, segment joints, PT and the 2\" grout bed under the cap not modeled; printed top of column {c[3]})."),
            UnresolvedFlag.Text("p.458", "footing top slope", "the side view shows the footing 5'-9\" deep at the edges and 6'-0\" at the bent control line (top sloped, slope not dimensioned otherwise)",
                "Flat footing, top el 5.50, 6'-0\" thick (bottom el -0.50)."),
            UnresolvedFlag.Text("p.398-399", "pile tips", "the installed tip elevations are blank in the pile table (filled during construction)",
                $"Tips at the 'pile tip no higher than' elevation {D(pileRows[n][4]):0.0}; heads 4'-0\" above the footing bottom (p458); length {pileLen / Ft(1):0.00} ft along the 2:12 batter."),
        },
    };
    sd.Reinforcement.Enabled = false;
    return sd;
}

foreach (var (n, sta) in bentSta)
{
    var sup = new SupportDefinition
    {
        Name = $"Bent {n}", Type = SupportType.Pier, Station = Sta(sta), SkewDegrees = 0,                 // all bents radial (p424: 90 00'00" typ.)
        Fixity = n == 32 ? BearingFixity.Fixed : BearingFixity.Expansion,                                // bent 32 FIX (p423)
    };
    if (n == 29) sup.Substructure = new SubstructureDefinition { Enabled = false };                      // bent 29 is the Unit 5 end bent (Unit 5 sheets)
    else sup.Substructure = Bent(n);
    if (n is 29 or 35) sup.DeckEndOffset = 0;
    br.Supports.Add(sup);
}
U("p.371 (Unit 5 sheets), p.395", "bent 29 substructure (cap, columns, footing C-18, piles)", "bent 29 is shared with Unit 5 and detailed on the Unit 5 sheets; the Unit 5 model builds it",
  "Bent 29 is a support line only in this model (no substructure); its Unit 6 side bearings (E1 + P32 on the Unit 5 bent 29 cap) are modeled.");
U("p.409, p.457", "deck ends at bents 29 and 35 (modular joint at bent 29, CL joint at bent 35)", "the joint gap / device and the deck end relative to the bent control line are on the joint sheets, which were not extracted",
  "Deck ends on the bent 29 and bent 35 control lines; no joint, no blockout.");

// ================================================================ 3. deck (typical section p404, p409-410)
br.Deck.LeftEdgeOffset = -F(21, 3.5); br.Deck.RightEdgeOffset = F(21, 3.5);                 // 42'-7" out to out, symmetric about -L-
br.Deck.Thickness = In(9);                                                                   // 9" uniform incl. grinding / grooving
br.Deck.Haunch = In(5);                                                                      // default only: every span has its own build-ups below
br.Deck.StayInPlaceForms = false;
br.Deck.Reinforcement.Enabled = false;
br.Deck.Barrier.Type = BarrierType.None; br.Deck.Barrier.Left = false; br.Deck.Barrier.Right = false;
G("Deck reinforcing (A101 / B / K / S / U / E / G / H bars, bill of material p455 = 299,412 lb stainless) - not placed (shapes not transcribed).");
G("1'-2\" concrete parapets and rails (p448-454), bent / end diaphragms (2'-4\" / 1'-10\", p409, p415), pour sequence and closures (p457), deck drains - not modeled.");

// ================================================================ 4. girders: FIB-96 (p438), strands (p439), girder data (girders.csv p428-431)
br.Girders.Count = 4;
br.Girders.Kind = GirderKind.TxDotPrestressed;
br.Girders.FirstGirderOffset = -F(17, 3);
br.Girders.Spacings = Enumerable.Repeat(F(11, 6), 3).ToList();
br.Girders.PlanLayout = GirderPlanLayout.Chorded;

// Strand pattern, p439 S4-47 "LOW RELAXATION STRAND LAYOUTS" (read from the VECTOR drawing: dot centres on the 2" grid, 4.5 pt = 1"):
// at CL girder 72 x 0.6": rows 3", 5" 17 strands (x = -16 .. 16), 7" 13, 9" 9, 11" 7, 13" 5, 15" 3, 17" 1. The 8 strands of the x = 0 column
// (3" .. 17" at CL) are DRAPED: at the end of girder they are at 77" .. 91" (the web column of the end view; 96 - 91 = 5" below the top), hold
// downs 5'-0" each side of CL (p428). Top flange: 4 x 1/2" "slack strands" (tensioned to 10,000 lb max) at x = -19 1/2, -1 1/2, 1 1/2, 19 1/2,
// 1 7/16" below the top. Debonding legend symbols on the end view (both ends; one end view "AT END OF GIRDER" is drawn for the girder):
// 4'-0" diamond (x +-4 @ 11", +-6 @ 9", +-14 @ 5"), 14'-0" inverted triangle (+-2 @ 9"), 16'-0" triangle (+-4, +-8 @ 7"), 18'-0" circle
// (+-10 @ 5", +-8 @ 3"), 30'-0" square (+-2, +-6 @ 5") = 20 debonded strands.
List<StrandPoint> Strands()
{
    var rows = new (double y, double maxX)[] { (3, 16), (5, 16), (7, 12), (9, 8), (11, 6), (13, 4), (15, 2) };
    var list = new List<StrandPoint>();
    foreach (var (y, mx) in rows)
        for (double x = -mx; x <= mx + 1e-9; x += 2)
            if (Math.Abs(x) > 1e-9) list.Add(new StrandPoint { X = In(x), Y = In(y), YEnd = In(y) });
    for (double y = 3; y <= 17 + 1e-9; y += 2) list.Add(new StrandPoint { X = 0, Y = In(y), YEnd = In(y) });     // draped column (HarpedColumns)
    foreach (double x in new[] { -19.5, -1.5, 1.5, 19.5 })
        list.Add(new StrandPoint { X = In(x), Y = In(96 - 1.4375), YEnd = In(96 - 1.4375), Diameter = In(0.5), Label = "1/2\" slack strand tensioned to 10,000 lb max (p439)" });
    (double x, double y)[] Pts(params double[] xy) { var r = new List<(double, double)>(); for (int i = 0; i < xy.Length; i += 2) { r.Add((In(xy[i]), In(xy[i + 1]))); r.Add((-In(xy[i]), In(xy[i + 1]))); } return r.ToArray(); }
    int nDb = 0;
    nDb += StrandPatterns.Debond(list, Pts(4, 11, 6, 9, 14, 5), Ft(4), "4'-0\"");
    nDb += StrandPatterns.Debond(list, Pts(2, 9), Ft(14), "14'-0\"");
    nDb += StrandPatterns.Debond(list, Pts(4, 7, 8, 7), Ft(16), "16'-0\"");
    nDb += StrandPatterns.Debond(list, Pts(10, 5, 8, 3), Ft(18), "18'-0\"");
    nDb += StrandPatterns.Debond(list, Pts(2, 5, 6, 5), Ft(30), "30'-0\"");
    if (nDb != 20 || list.Count != 76) throw new InvalidOperationException($"strand pattern: {list.Count} strands, {nDb} debonded");
    return list;
}

// Bearings (bearings.csv; p440 E1 / p442 E2 / p444 E3 plan views and sections; sole plates p440-444): pad = plan size along the girder x across,
// total height as printed. Sole plates 4'-6" across the girder (4" + 3'-0" / 3'-10" + 4"), thicker at the back-station end (elevations), CL bearing
// from the back-station end as dimensioned in the PLAN views ("CL BRG." vs "CL SLOTS": e.g. P36 7 3/4 slots + 5 = 12 3/4; the elevation dimensions
// run to the slots); plates whose plan shows only "CL SLOTS" / "CL HOLES" (P33, P34, P14, P8, P35) are centred on the bearing.
var pads = new Dictionary<string, (double along, double across, double h, string page)>
{
    ["E1"] = (F(1, 2), F(3), In(9 + 1.0 / 16), "p.440"), ["E2"] = (F(1), F(2, 8), In(6 + 7.0 / 16), "p.442"), ["E3"] = (F(1), F(2, 4), In(2 + 7.0 / 16), "p.444"),
};
var plates = new Dictionary<string, (double len, double back, double ahead, double brg, string page)>
{
    ["P32"] = (F(1, 9.25), In(2), In(1.25), In(8), "p.440"),
    ["P40"] = (F(1, 8.25), In(1 + 15.0 / 16), In(1.25), In(12.25), "p.440"),
    ["P33"] = (F(1, 3.25), In(1 + 15.0 / 16), In(1.25), In(7.625), "p.443"),
    ["P34"] = (F(1, 3.25), In(2 + 1.0 / 16), In(1.25), In(7.625), "p.443"),
    ["P14"] = (F(1, 3.25), In(1.875), In(1.25), In(7.625), "p.442"),
    ["P36"] = (F(1, 7.75), In(2.125), In(1.25), In(12.75), "p.443"),
    ["P37"] = (F(1, 7.75), In(1 + 13.0 / 16), In(1.25), In(7), "p.443"),
    ["P38"] = (F(1, 7.75), In(2), In(1.25), In(12.75), "p.443"),
    ["P39"] = (F(1, 7.75), In(1.75), In(1.25), In(7), "p.443"),
    ["P35"] = (F(1, 2), In(1 + 15.0 / 16), In(1.25), In(7), "p.444"),
    ["P8"] = (F(1, 2), In(1.75), In(1.25), In(7), "p.444"),
};
var bearingRows = Csv("bearings.csv");
PrestressedBearingDefinition Bearing(int bent, string side)
{
    var r = bearingRows.First(x => x[0] == bent.ToString() && x[1] == side);
    var (along, across, h, pg) = pads[r[3]];
    var (len, tb, ta, at, ppg) = plates[r[4]];
    return new PrestressedBearingDefinition
    {
        Mark = r[3], Length = along, Width = across, Thickness = h, Description = $"{(r[5] == "FIX" ? "fixed" : "expansion")} ({pg}, {r[6]})",
        SolePlate = new SolePlateDefinition { Mark = r[4], Length = len, Width = F(4, 6), ThicknessBack = tb, ThicknessAhead = ta, BearingFromBack = at, Source = ppg },
    };
}

// Camber / dead-load deflection tables (camber-dead-load-deflection.csv, p446 S4-53): exterior = G1 / G4, interior = G2 / G3; span 35 per girder.
var camberRows = Csv("camber-dead-load-deflection.csv");
double Frac(string s)
{
    s = s.Replace("\"", "").Trim();
    double whole = 0;
    var parts = s.Split(' ');
    foreach (var pp in parts)
        if (pp.Contains('/')) { var q = pp.Split('/'); whole += D(q[0]) / D(q[1]); } else whole += D(pp);
    return whole;
}
List<PrestressedCamberPoint> Camber(int span, int g)
{
    string spans = span == 30 ? "SPAN 30" : span == 35 ? "SPAN 35" : "SPANS 31 - 34";
    string grp = span == 35 ? g switch { 1 => "EXTERIOR GIRDERS (LEFT)", 2 => "INTERIOR GIRDER 2", 3 => "INTERIOR GIRDER 3", _ => "EXTERIOR GIRDERS (RIGHT)" }
                            : g is 1 or 4 ? "EXTERIOR GIRDERS" : "INTERIOR GIRDERS";
    var rows = camberRows.Where(r => r[0] == spans && r[1] == grp).ToList();
    if (rows.Count != 21) throw new InvalidOperationException($"camber table {spans} {grp}: {rows.Count} rows");
    return rows.Select(r => new PrestressedCamberPoint { Fraction = D(r[2]), Camber = Ft(D(r[3])), Deflection = Ft(D(r[4])), FinalCamber = In(Frac(r[5])) }).ToList();
}

var girderRows = Csv("girders.csv");
string[] GirderRow(int span, int g) => girderRows.First(r => (r[0].Split(',').Select(int.Parse).Contains(span)) && (r[1] == "G1-G4" || r[1] == $"G{g}"));
var commonUnresolved = new List<string>
{
    UnresolvedFlag.Text("p.438", "FIB-96 bottom flange corner chamfer", "the 3/4\" chamfers at the bottom corners are drawn (vector drawing 0.73\") but not dimensioned", "3/4\" chamfer as drawn."),
    UnresolvedFlag.Text("p.428-431, p.422-424", "girder length 'A' vs bearing-to-bearing + end distances",
        "A is the length along the sloped girder (b-b + end distances x the grade factor: spans 30 and 32 reproduce within 1/16\"), but ONE A is printed for spans 31-32 and one for 33-34 although their grades differ (vertical curves), so A cannot hold for both spans of a pair",
        "Beams built from the framing plan: layout CL bearings (bent line +- 1'-2\", 3'-2\" at bent 29, 1'-4\" at bent 35) + the 9\" / 11\" end distances along the beam; the check reports A - model."),
    UnresolvedFlag.Text("p.428", "girder end details (end bevel, embedded plates B-1, formed holes, top raking)", "drawn on the girder sheets, not modeled", "Square ends, plain outline."),
};
PrestressedBeamDefinition Beam(int span, int g)
{
    var gr = GirderRow(span, g);
    var layout = Strands();
    var d = new PrestressedBeamDefinition
    {
        Standard = "FL FIB-96", Designation = "FIB-96", StrandDiameter = In(0.6), StrandStrength = 270,
        ConcreteStrength = 8.0, ReleaseStrength = 6.4,                                                  // p428 / p438 notes
        StrandLayout = layout, StrandCount = layout.Count,
        HarpedColumns = new() { 0 }, HarpedTopBelowTopAtEnd = In(5), HarpedEndSpacing = In(2), HoldDownFromMidspan = Ft(5),
        BearingToEnd = In(9),
        // CL bearing to end of girder 11" at the unit-end joints (span 30 at bent 29, span 35 at bent 35), 9" elsewhere (p428-430 clips; p429 covers spans 31-34)
        StartBearingToEnd = span == 30 ? In(11) : 0, EndBearingToEnd = span == 35 ? In(11) : 0,
        StartBearing = Bearing(span - 1, "ahead"), EndBearing = Bearing(span, "back"),
        CamberTable = Camber(span, g), CamberSource = "p.446 S4-53",
        PlanLength = Len(gr[2]), PlanConcreteVolume = D(gr[6]) * Math.Pow(Ft(1), 3) * 27,
        Source = span == 30 ? "p.428 S4-36" : span == 35 ? (g <= 2 ? "p.430 S4-38" : "p.431 S4-39") : "p.429 S4-37",
        Unresolved = new List<string>(commonUnresolved),
    };
    if (span == 30) d.Unresolved.Add(UnresolvedFlag.Text("p.371 (Unit 5 sheets)", "bent 29 pedestal under the span 30 bearings", "bent 29 belongs to Unit 5 (no pedestal elevation on the Unit 6 sheets)", "The E1 bearing seats of bent 29 are reported, not checked."));
    return d;
}
var beamDefs = new List<PrestressedSpanBeam>();
for (int span = 30; span <= 35; span++)
    for (int g = 1; g <= 4; g++)
        beamDefs.Add(new PrestressedSpanBeam { Spans = new() { span - 30 }, Girders = new() { g - 1 }, Beam = Beam(span, g) });
br.Girders.Prestressed = beamDefs[0].Beam.Clone();
br.Girders.Prestressed.SpanBeams = beamDefs;

// Per-span layout (framing plans p422-424, cap plans p481 / p484): girders on the bent control line at -17'-3", -5'-9", 5'-9", 17'-3" along it
// from the work line (G1 left; at bent 35 "radial along bent control line", p424), CL bearing 1'-2" from the bent control line into the span
// (3'-2" at bent 29, p422; 1'-4" from "CL joint & bent 35 control line" at bent 35, p424), measured normal to the radial bent line; straight chord per span (span 35 tangent then arc). Haunch at each bearing = the BUILD-UP at CL bearing (p404 table).
double[] buildBack = { 5, 4.5, 5.25, 5.5, 5.75, 5.75 }, buildAhead = { 5, 5.5, 5.75, 5.75, 5.75, 6 };   // span 30..35 at its start / end bent
GirderSpanEnd End(double bearingOffset, double buildUpIn) => new()
{
    Measure = SpanOffsetMeasure.AlongLine, ReferenceOffset = 0,
    Offsets = { -F(17, 3), -F(5, 9), F(5, 9), F(17, 3) },
    Haunches = Enumerable.Repeat(In(buildUpIn), 4).ToList(),
    BearingOffset = bearingOffset, BearingMeasure = BearingOffsetMeasure.NormalToSupport,
};
br.Girders.SpanLayouts = new();
for (int k = 0; k < 6; k++)
    br.Girders.SpanLayouts.Add(new GirderSpanLayout { Span = k, Count = 4, Begin = End(k == 0 ? F(3, 2) : F(1, 2), buildBack[k]), End = End(k == 5 ? F(1, 4) : F(1, 2), buildAhead[k]) });
U("p.404", "build-up between the bearings", "only the values at the CL bearings are tabulated ('varies along length of girder')", "The haunch follows the straight beam between the two tabulated bearing values (no camber).");
G("Girder mild reinforcing S1-S8, V1, WWF (p428-431: counts and lengths per girder, 3,854 - 3,947 lb) - not placed: bar shapes and positions not transcribed.");
G("Embedded plates B-1, anchor bolts / grout pockets, 1 1/2\" formed web holes, end bevels (p438-444) - not modeled.");

Doc.Project.Bridges.Add(br);

// ================================================================ 5. regenerate, save, report
var rep = Doc.Regenerate();
Print($"Regenerated {rep.Bridges} bridge(s) in {rep.Elapsed.TotalSeconds:0.0} s; {rep.Warnings.Count} warnings, {rep.Errors.Count} errors");
foreach (var e in rep.Errors) Print("ERROR " + e);
var ps = Doc.Prestressed(BridgeName);
var sub = Doc.Substructure(BridgeName);
Print($"Beams {ps.Beams.Count}, strands {ps.Beams.Sum(b => b.Strands.Count)} ({ps.Beams.Sum(b => b.Strands.Count(s => s.Debonded))} debonded), pads {ps.Pads.Count}, sole plates {ps.Pads.Count(p => p.SolePlate != null)}");
Print($"Girder concrete {ps.ConcreteVolume / Math.Pow(Ft(1), 3) / 27:0.0} CY; substructure units {sub?.Units.Count ?? 0}, piles {sub?.Units.Sum(u => u.Foundations.Sum(f => f.Piles.Count)) ?? 0}");
foreach (var d in ps.Diagnostics.Where(d => d.Severity != SBM.Core.Framing.DiagnosticSeverity.Info).Take(40)) Print($"prestressed {d.Severity}: {d.Message}");
if (sub != null) foreach (var d in sub.Diagnostics.Where(d => d.Severity != SBM.Core.Framing.DiagnosticSeverity.Info).Take(40)) Print($"substructure {d.Severity}: {d.Message}");
Print("UNRESOLVED (script):");
foreach (var u in unresolved) Print("  " + u);
Print("GAP (not modeled):");
foreach (var g in gaps) Print("  " + g);
Doc.Save(outPath);
Print($"Saved {outPath}");
