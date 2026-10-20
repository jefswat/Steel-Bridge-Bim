// PTC / NJTA DELAWARE RIVER BRIDGE (1954-57) - SUBSTRUCTURE, phase 4: the 30 piers and the 2 abutments from DRB-2 (PA, p.5-14), DRB-1 (river piers 17-20,
// p.16-21, 23, 25, 27, 29) and DRB-3 (NJ, p.32-40). Re-runnable on "samples\plans\PTC Delaware River Bridge.sbm" (replaces every support's Substructure):
//   sbm run "samples\plans\substructure-ptc.csx" "samples\plans\PTC Delaware River Bridge.sbm"
// (build-ptc-delaware-river.csx calls it once after the supports exist.) Data: docs/plan-data/ptc-delaware-river/piers.csv (one row per pier, every row cites
// its pages); everything else below is plan geometry written out with its page. "p." = PDF page of "Original Existing Bridge Plans from PTC.pdf".
// Built with SubstructureDefinition.Plan (generic plan-drawn substructure: lofted / extruded solids, pile groups, fixed seats; works on SupportLinesOnly
// bridges, no bearings needed). Rules: plan values only; whatever cannot be read is UNRESOLVED with page + plain reason, raised as generator warnings and
// carried on the solid. No reinforcement (bar marks / bar lists are on the sheets: piers p.6-10, 17-21, 27, 29, 33-37; footings p.10, 27; abutments p.12, 39).
// Not modeled yet (noted): fender system / dolphins (p.25), cofferdam sheet piling / bracing (p.18-19), anchor bolts (p.13, 23, 27, 40), scoring (p.13, 40),
// shear-lock pedestals (p.29), the 48in RC pipe at pier 1 (p.10, 14).
using System.IO;
using System.Globalization;
using SBM.Core;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Substructure;

bool noSave = Args.Contains("--nosave");
string modelPath = Args.FirstOrDefault(a0 => a0 != "--nosave") ?? Doc.FilePath ?? @"samples\plans\PTC Delaware River Bridge.sbm";
var inv = CultureInfo.InvariantCulture;
string dataDir = null;
for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
{
    var p = Path.Combine(dir.FullName, "docs", "plan-data", "ptc-delaware-river");
    if (Directory.Exists(p)) { dataDir = p; break; }
}
if (dataDir == null) throw new InvalidOperationException("Run from the repo: docs/plan-data/ptc-delaware-river not found above the current directory.");
var csv = new List<string[]>();
foreach (var line in File.ReadAllLines(Path.Combine(dataDir, "piers.csv")).Skip(1))
{
    if (line.Trim() == "") continue;
    var cells = new List<string>(); var cur = new System.Text.StringBuilder(); bool q = false;
    foreach (char c in line)
    {
        if (c == '"') q = !q;
        else if (c == ',' && !q) { cells.Add(cur.ToString()); cur.Clear(); }
        else cur.Append(c);
    }
    cells.Add(cur.ToString());
    csv.Add(cells.ToArray());
}
double D(string s) => double.Parse(s, inv);

// ---------------------------------------------------------------- helpers (feet in, metres out through Ft())
FootingPoint P(double s, double n) => new(Ft(s), Ft(n));
List<FootingPoint> Rect(double s0, double s1, double n0, double n1) => new() { P(s0, n0), P(s1, n0), P(s1, n1), P(s0, n1) };
PlanSolidSection Sec(double z, List<FootingPoint> poly) => new(Ft(z), poly);
PlanSolidDefinition Loft(string name, SubElementKind kind, string src, params PlanSolidSection[] secs) =>
    new() { Name = name, Kind = kind, Source = src, Sections = secs.ToList() };
string FtIn(double f) { int ft = (int)Math.Floor(f + 1e-9); double inch = (f - ft) * 12; return $"{ft}'-{inch:0.##}\""; }

// dog-bone footing plan (end pads + chamfered transition + neck), centred at nc along N
List<FootingPoint> DogBone(double L, double pad, double ch, double neckW, double padW, double nc)
{
    double h = L / 2, pw = padW / 2, nw = neckW / 2;
    var pts = new (double s, double n)[]
    {
        (-h, -pw), (-h + pad, -pw), (-h + pad + ch, -nw), (h - pad - ch, -nw), (h - pad, -pw), (h, -pw),
        (h, pw), (h - pad, pw), (h - pad - ch, nw), (-h + pad + ch, nw), (-h + pad, pw), (-h, pw),
    };
    return pts.Select(t => P(t.s, t.n + nc)).ToList();
}
// stadium (two semicircles + straights), arc centres at +-cx, radius r, centred at nc
List<FootingPoint> Stadium(double cx, double r, double nc, int n = 24)
{
    var l = new List<FootingPoint>();
    for (int i = 0; i <= n; i++) { double a = -Math.PI / 2 + Math.PI * i / n; l.Add(P(cx + r * Math.Cos(a), nc + r * Math.Sin(a))); }
    for (int i = 0; i <= n; i++) { double a = Math.PI / 2 + Math.PI * i / n; l.Add(P(-cx + r * Math.Cos(a), nc + r * Math.Sin(a))); }
    return l;
}
// tapered leg: right leg (sg = +1) has the vertical (inner) face at S = so - a, the outer face (S = so at the top) leans out by r per unit drop; across N both faces lean
// baseA / baseB (> 0) = the PRINTED shaft size at the footing top (across / along; piers.csv base_*_printed, piers-review.md): the plan dimension governs over
// the 1/4in per 12in batter (they differ by up to 3/8in from rounding), so the faces run straight from the printed top to the printed base.
PlanSolidDefinition Leg(string name, int sg, double so, double a, double b, double nc, double zTop, double zBot, double r, string src, double nHi = double.NaN, bool bothFaces = false,
    double baseA = 0, double baseB = 0)
{
    double dzB = zTop - zBot;
    List<FootingPoint> At(double z)
    {
        double dz = zTop - z, e = r * dz, t = dzB > 0 ? dz / dzB : 0;
        double eA = baseA > 0 ? (bothFaces ? (baseA - a) / 2 : baseA - a) * t : e;   // growth of the leaning across face(s)
        double eB = baseB > 0 ? (baseB - b) / 2 * t : e;                              // growth of each along face
        double s0, s1;
        if (bothFaces) { s0 = sg * so - a / 2 - eA; s1 = sg * so + a / 2 + eA; }
        else if (sg > 0) { s0 = so - a; s1 = so + eA; }
        else { s0 = -so - eA; s1 = -so + a; }
        return Rect(s0, s1, nc - b / 2 - eB, nc + b / 2 + eB);
    }
    return Loft(name, SubElementKind.Column, src + (baseA > 0 ? $" (top {FtIn(a)} x {FtIn(b)}, printed base {FtIn(baseA)} x {FtIn(baseB)})" : ""), Sec(zBot, At(zBot)), Sec(zTop, At(zTop)));
}
PlanSolidDefinition Footing(string name, double zBot, double zTop, List<FootingPoint> plan, string src, string flag = "") =>
    new() { Name = name, Kind = SubElementKind.Footing, Source = src, Flag = flag, Sections = { Sec(zBot, plan), Sec(zTop, plan.Select(p => p.Clone()).ToList()) } };

var piersBySupport = new Dictionary<string, SubstructureDefinition>();
var summary = new List<string>();
const double TaperApproach = 0.25 / 12;           // 1/4 in per 12 in (p.6-10, 17, 33-37)

SubstructureDefinition NewSd(string pile = "HP12x74", double embed = 0.75)
{
    var sd = new SubstructureDefinition { Enabled = true, Plan = new PlanSubstructureDefinition(), Unresolved = new List<string>() };
    sd.PileType = PileType.SteelHP; sd.PileSection = pile; sd.PileEmbedment = Ft(embed);
    sd.Reinforcement.Enabled = false;
    return sd;
}
void Seat(SubstructureDefinition sd, string name, double s, double n, double z, string src) =>
    sd.Plan.Seats.Add(new PlanSeatDefinition { Name = name, S = Ft(s), N = Ft(n), Elevation = Ft(z), Source = src });
void U(SubstructureDefinition sd, string pages, string item, string why, string then = "") => sd.Unresolved.Add(UnresolvedFlag.Text(pages, item, why, then));

// ================================================================ piers from piers.csv
foreach (var r in csv)
{
    string pier = r[0], support = r[1], cls = r[2], pages = r[3];
    double seat = D(r[4]), fb = D(r[5]), ft = D(r[6]);
    string fplan = r[7]; double fl = D(r[8]), fw = D(r[9]);
    double so = D(r[10]), a = D(r[11]), b = D(r[12]), capEnd = D(r[13]), capMid = D(r[14]), cl = D(r[15]);
    double baseA = D(r[16]), baseB = D(r[17]);
    var sd = NewSd();
    string pg = pages;
    switch (cls)
    {
        case "free":
        {
            // two free-standing shafts 58'-0 c.c. (shoe lines, p.10 / p.37), no cap beam; 6'-6 square at the top, 1/4in per 12in on all four faces
            var ftgPlan = fplan == "A" ? DogBone(80, 24, 8, 8, 22, 0) : Rect(-42.5, 42.5, -8, 8);
            sd.Plan.Solids.Add(Footing("footing", fb, ft, ftgPlan, pg + (fplan == "A" ? " (type A: 80' x 22', end pads 24', 8' transitions, 8' neck, 5' thick)" : " (type B: 85' x 16', 5' thick)")));
            foreach (int sg in new[] { -1, 1 })
                sd.Plan.Solids.Add(Leg(sg < 0 ? "shaft W" : "shaft E", sg, 29, a, b, 0, seat, ft, TaperApproach, pg, bothFaces: true, baseA: baseA, baseB: baseB));
            foreach (int sg in new[] { -1, 1 })
                foreach (int k in new[] { -1, 1 })
                    Seat(sd, $"shoe {(sg < 0 ? "left" : "right")} {(k < 0 ? "back" : "ahead")}", 29 * sg, 1.25 * k, seat, "p.10 / p.37 (shoe lines 58'-0 c.c.); bearings 1'-3 each side of the CL pier: p.42 / p.43 note (simple girder spans)");
            U(sd, pg, "shaft top chamfer / bush-hammer detail and anchor-bolt layout", "details are on p.13 (miscellaneous) and not modeled; the shaft is built to the seat elevation", "Shaft top = plan seat El " + seat.ToString("0.000", inv) + ".");
            if (fplan == "A" || fplan == "B")
                U(sd, pg, "footing key (6in) and the dowel / reinforcement layout", "reinforcement is out of scope for this phase", "Plain concrete footing.");
            break;
        }
        case "cap":
        {
            // two tapered legs 58'-0 c.c. with a haunched cap beam between them (p.6-8, p.33-35); outer faces lean 1/4in per 12in, inner faces vertical
            var ftgPlan = Rect(-fl / 2, fl / 2, -fw / 2 + cl, fw / 2 + cl);
            sd.Plan.Solids.Add(Footing("footing", fb, ft, ftgPlan, pg + $" (footing {FtIn(fl)} x {FtIn(fw)}, {FtIn(ft - fb)} thick)"));
            sd.Plan.Solids.Add(Leg("leg W", -1, so, a, b, cl, seat, ft, TaperApproach, pg, baseA: baseA, baseB: baseB));
            sd.Plan.Solids.Add(Leg("leg E", 1, so, a, b, cl, seat, ft, TaperApproach, pg, baseA: baseA, baseB: baseB));
            double si = so - a;
            sd.Plan.Solids.Add(new PlanSolidDefinition
            {
                Name = "cap beam", Kind = SubElementKind.Cap, Source = pg + $" (beam between the legs: {FtIn(capMid)} deep at the CL bridge, {FtIn(capEnd)} at the inner leg faces)",
                N0 = Ft(cl - b / 2), N1 = Ft(cl + b / 2),
                Profile = { new(Ft(-si), Ft(seat)), new(Ft(si), Ft(seat)), new(Ft(si), Ft(seat - capEnd)), new(0, Ft(seat - capMid)), new(Ft(-si), Ft(seat - capEnd)) },
            });
            double shoeN = pier.Length > 0 && (pier == "13" || pier == "24") ? 2.0 : 0.0;
            if (shoeN > 0)
            {
                foreach (int sg in new[] { -1, 1 }) { Seat(sd, $"shoe {(sg < 0 ? "left" : "right")} W", 29 * sg, cl - 2.0, seat, "p.7 / p.34 / p.1: bearings 2'-0 each side of the CL pier, 4'-0 c.c.");
                                                      Seat(sd, $"shoe {(sg < 0 ? "left" : "right")} E", 29 * sg, cl + 2.0, seat, "p.7 / p.34 / p.1: bearings 2'-0 each side of the CL pier, 4'-0 c.c."); }
            }
            else foreach (int sg in new[] { -1, 1 }) Seat(sd, $"shoe {(sg < 0 ? "left" : "right")}", 29 * sg, cl, seat, "p.5 / p.32: CL pier = shoe (58'-0 c.c., p." + pg.Replace("p.", "").Split(',')[0].Trim() + ")");
            U(sd, pg, "leg top chamfer, 1/2in bush-hammer and the anchor-bolt layout", "the details are on p.13 / p.40 and not modeled", "Leg built to the seat elevation; no chamfer.");
            U(sd, pg, "cap beam width across the bridge", "the beam is drawn as wide as the leg top (section F-F / N-N); the legs lean outward below it", "Beam = leg top width, constant.");
            break;
        }
        case "pier10":
        case "pier27":
        {
            // expansion-dam pier: two legs 6'-6 along the pier line, stepped tops 76.918 (girder side) / 55.962 (truss side), dog-bone footing 86'-6 x 24' x 7' (p.9, p.36)
            double girderN = cls == "pier10" ? 0.0 : 4.729, trussN = cls == "pier10" ? 4.729 : 0.0;
            double fcN = cls == "pier10" ? 2.229 : 2.5;              // CL footing station vs the support line: 658+63.88 / 706+96.88
            double hiN0 = girderN - 2.979, hiN1 = girderN + 1.75;     // high block 4'-8 3/4 wide from 2'-11 3/4 outside the girder shoe (p.9 section B-B)
            double loN0 = cls == "pier10" ? hiN1 : trussN - 2.979, loN1 = cls == "pier10" ? trussN + 2.979 : hiN0;
            if (cls == "pier27") { hiN0 = girderN - 1.75; hiN1 = girderN + 2.979; loN0 = trussN - 2.979; loN1 = hiN0; }
            double zLo = 55.962;
            sd.Plan.Solids.Add(Footing("footing", fb, ft, DogBone(86.5, 24, 6, 12, 24, fcN), pg + " (86'-6 x 24', end pads 24', 6' transitions, 12' neck, 7' thick; CL footing 658+63.88 / 706+96.88)"));
            foreach (int sg in new[] { -1, 1 })
            {
                string w = sg < 0 ? "W" : "E";
                // lower body: whole width, to the lower step; face offsets measured from the high step top 76.918 (hi side) / 55.962 (lo side)
                double zTopHi = seat;
                List<FootingPoint> LowAt(double z)
                {
                    double eS = TaperApproach * (zTopHi - z);
                    double eHi = TaperApproach * (zTopHi - z), eLo = TaperApproach * (zLo - z);
                    double n0 = cls == "pier10" ? hiN0 - eHi : loN0 - eLo, n1 = cls == "pier10" ? loN1 + eLo : hiN1 + eHi;
                    return Rect(29 * sg - 3.25 - eS, 29 * sg + 3.25 + eS, n0, n1);
                }
                sd.Plan.Solids.Add(Loft($"shaft {w} lower", SubElementKind.Column, pg, Sec(ft, LowAt(ft)), Sec(zLo, LowAt(zLo))));
                List<FootingPoint> HiAt(double z)
                {
                    double e = TaperApproach * (zTopHi - z);
                    double n0 = hiN0 - (cls == "pier10" ? e : 0), n1 = hiN1 + (cls == "pier10" ? 0 : e);
                    return Rect(29 * sg - 3.25 - e, 29 * sg + 3.25 + e, n0, n1);
                }
                sd.Plan.Solids.Add(Loft($"shaft {w} upper (girder side)", SubElementKind.Column, pg, Sec(zLo, HiAt(zLo)), Sec(seat, HiAt(seat))));
                Seat(sd, $"girder shoe {w}", 29 * sg, girderN, seat, "p.9 / p.36: girder shoe line (El. +76.918, bush-hammered)");
                Seat(sd, $"truss shoe {w}", 29 * sg, trussN, zLo, "p.9 / p.36: truss shoe line (El. +55.962, bush-hammered)");
            }
            U(sd, pg, "which step carries which bearing line, and the exact step position", "the stepped top is read from section B-B (4'-8 3/4 high block, 1'-9 past the girder shoe) but the sheet does not name the bearing on each step in the scan",
              "High step (76.918) under the girder shoe line, low step (55.962) under the truss shoe line (deck-truss end bearing 21 ft lower, as the truss seats on p.49 / p.59).");
            U(sd, pg, "across-bridge width of the shafts below the step and the batter on the step face", "only the top dimensions 2'-11 3/4 / 4'-8 3/4 / 4'-11 are legible", "Shafts lean 1/4in per 12in on every face from the high top; the low part leans from the low top.");
            U(sd, "p.9, p.36", "existing PRR 6-duct tile line near the footing (p.9 note 3)", "belongs to the railroad, not the pier", "Not modeled.");
            break;
        }
        case "tall":
        {
            // piers 17 and 20 (p.17, p.27, p.29): 33' x 121'-1 1/2 pile cap El -7.0 to +5.0, two legs 87'-0 c.c. with a gabled cap beam, top El +107.682
            double fc = pier == "17" ? -0.25 : 0.25;            // CL footing 675+96.13 / 689+64.63 vs CL pier 675+96.38 / 689+64.38 (p.27)
            double nc = cl + 0.0;                                // pier CL relative to the support line
            sd.Plan.Solids.Add(Footing("pile cap", fb, ft, DogBone(121.125, 25, 9, 15, 33, nc + fc), pg + " (121'-1 1/2 x 33', end pads 25', 9' transitions, 15' neck, 12' thick)"));
            sd.Plan.Solids.Add(Leg("leg W", -1, so, a, b, nc, seat, ft, TaperApproach, pg));
            sd.Plan.Solids.Add(Leg("leg E", 1, so, a, b, nc, seat, ft, TaperApproach, pg));
            double si = so - a;   // 37.5
            sd.Plan.Solids.Add(new PlanSolidDefinition
            {
                Name = "cap beam", Kind = SubElementKind.Cap, Source = pg + $" (beam between the legs: {FtIn(capEnd)} deep at the inner leg faces, {FtIn(capMid)} at the CL bridge)",
                N0 = Ft(nc - b / 2), N1 = Ft(nc + b / 2),
                Profile = { new(Ft(-si), Ft(seat)), new(Ft(si), Ft(seat)), new(Ft(si), Ft(seat - capEnd)), new(0, Ft(seat - capMid)), new(Ft(-si), Ft(seat - capEnd)) },
            });
            double apprN = pier == "17" ? nc - 2.0 : nc + 2.0, mainN = pier == "17" ? nc + 2.0 : nc - 2.0;
            foreach (int sg in new[] { -1, 1 })
            {
                Seat(sd, $"approach shoe {(sg < 0 ? "W" : "E")}", 29 * sg, apprN, seat, "p.17 / p.29: approach-span shoe lines 58'-0 c.c., 2'-0 from the CL pier");
                Seat(sd, $"main span shoe {(sg < 0 ? "W" : "E")}", 43.5 * sg, mainN, seat, "p.17 / p.29: main-span shoe lines 87'-0 c.c., 2'-0 on the river side");
            }
            // piles (p.27): 12 H 74, cut-off El -6.25 (9in into the cap), 11 rows a-k at 3'-0 (30'), columns 1-7 at 1'-6 / 5'-0 / 8'-6 / 12'-0 / 16'-0 / 20'-0 / 24'-0 from the end, 8-14 toward the CL
            var pile = new PlanPileGroupDefinition { Name = "piles", FootingBottom = Ft(fb), HeadEmbedment = Ft(0.75), BatterRatio = 4, TipElevation = Ft(-61.9), Source = "p.27 (pile plan), p.30 (test pile)",
                Flag = UnresolvedFlag.Text("p.27, p.30", "pile length / tip elevation", "the plan says 'driven to 100 tons' (note 3) and prints no tip; the only tip in the set is the pier 17 test pile (El -61.9, p.30)", "All tips at El -61.9.") };
            double half = 60.5625;
            var cols = new List<(double d, int[] rows)>();   // d = distance from the footing centre toward the end
            double[] padCols = { 1.5, 5.0, 8.5, 12.0, 16.0, 20.0, 24.0 };
            int[] all11 = Enumerable.Range(-5, 11).ToArray();
            for (int i = 0; i < 7; i++) cols.Add((half - padCols[i], all11));
            cols.Add((32.5625 + 0.0, new[] { -4, -3, -2, -1, 0, 1, 2, 3, 4 }.Where(k => Math.Abs(k * 3) <= 11.0).ToArray()));
            cols.Add((28.5625, new[] { -2, -1, 0, 1, 2 }));
            cols.Add((24.0, new[] { -1, 1 }.Select(k => k).ToArray()));
            cols.Add((19.75, new[] { 0 }));
            cols.Add((15.5, new[] { -1, 1 }));
            cols.Add((7.0, new[] { -1, 1 }));
            for (int side = -1; side <= 1; side += 2)
                for (int ci = 0; ci < cols.Count; ci++)
                {
                    var (d, rows) = cols[ci];
                    foreach (int k in rows)
                    {
                        double ny = ci >= 9 ? k * 4.5 : k * 3.0;     // neck columns 10-13 carry rows 4'-6 either side of the CL (3'-0 / 9'-0 / 3'-0 across the 15'-0 neck)
                        var fp = new FootingPoint(Ft(side * d), Ft(nc + fc + ny));
                        if (ci < 2) { fp.BatterDirS = side; fp.BatterDirN = 0; }   // columns 1-2 of each end, 1:4 outward (section B-B)
                        pile.Piles.Add(fp);
                    }
                }
            pile.Piles.AddRange(new[] { P(0, nc + fc - 4.5), P(0, nc + fc + 4.5) });
            sd.Plan.PileGroups.Add(pile);
            U(sd, "p.27", "pile positions in the transition zone (columns 8-14), the pile count and the batter", "the grid is read from the half pile plan; the neck and transition piles are placed on the dimensioned column lines at the row spacings read from the scan, and the end piles are battered 1:4 outward from section B-B (the sheet does not list a count)",
              $"{pile.Piles.Count} piles, 12 H 74, columns 1-2 of each end battered 1:4 outward.");
            U(sd, "p.29", "shear-lock pedestal on the cap and the concrete areas at the shoes (El 107.724 before bush-hammering)", "detail sections G / H and the plan are small and partly cut; dimensions not read", "Not modeled; seats at the final El. +107.682.");
            U(sd, "p.17, p.29", "7in top chamfer of the legs and the 10'-0 x 10'-6 flat top", "detail only", "Square top at El. +107.682.");
            break;
        }
        case "river":
        {
            // piers 18 / 19 (p.17, p.19-21, p.23): cofferdam footing 158'-10 x 53' on piles with a 6' tremie seal, 4' base slab, stadium-shaped pier body, twin tapered shafts 87'-0 c.c.
            double L = 158.8333, W = 53.0, hl = L / 2, hw = W / 2, c4 = 4.0;
            List<FootingPoint> Oct() => new() { P(-hl + c4, -hw), P(hl - c4, -hw), P(hl, -hw + c4), P(hl, hw - c4), P(hl - c4, hw), P(-hl + c4, hw), P(-hl, hw - c4), P(-hl, -hw + c4) };
            sd.Plan.Solids.Add(Footing("tremie seal", -25.0, -19.0, Oct(), "p.17 (El. -25.0 to -19.0, 6'-0 thick), p.19, p.20 (tremie seal 2500 psi)",
                UnresolvedFlag.Text("p.17, p.19", "tremie seal plan outline", "the seal fills the cofferdam inside the ZP 38 sheeting; the plan gives 158'-10 x 53'-0 face to face of sheeting and 4'-0 corner cuts, so the seal is taken as that outline", "Seal = 158'-10 x 53'-0 with 4'-0 corner cuts, El -25.0 to -19.0.")));
            sd.Plan.Solids.Add(Footing("pile cap slab", -19.0, -15.0, Oct(), "p.17 / p.19 (4'-0 min slab, top El. -15.0, pile cut-off El. -18.25)"));
            // pier body: vertical stadium (R 20'-3) El -15 to -7, then tapering to R 18'-0 at El +12.0
            double cx = 54.875;
            sd.Plan.Solids.Add(Loft("pier body lower", SubElementKind.Footing, "p.17 (40'-6 wide, R 20'-3, El. -15.0 to -7.0), p.20", Sec(-15.0, Stadium(cx, 20.25, 0)), Sec(-7.0, Stadium(cx, 20.25, 0))));
            sd.Plan.Solids.Add(Loft("pier body", SubElementKind.Footing, "p.17 (40'-6 at El -7.0 to 36'-0 at El +12.0, R 20'-3 to 18'-0), p.20", Sec(-7.0, Stadium(cx, 20.25, 0)), Sec(12.0, Stadium(cx, 18.0, 0))));
            double tr = (D(r_base_a(csv, pier)) - a) / (seat - 12.27);
            sd.Plan.Solids.Add(Leg("shaft W", -1, 43.5 + 6.75, a, b, 0, seat, 12.0, tr, "p.17 (13'-6 x 14'-0 at the top, 18'-1 1/2 x 23'-3 at El 12.27)"));
            sd.Plan.Solids.Add(Leg("shaft E", 1, 43.5 + 6.75, a, b, 0, seat, 12.0, tr, "p.17 (13'-6 x 14'-0 at the top, 18'-1 1/2 x 23'-3 at El 12.27)"));
            foreach (int sg in new[] { -1, 1 }) Seat(sd, $"shoe {(sg < 0 ? "W" : "E")}", 43.5 * sg, 0, seat, "p.17 / p.96: shoes 87'-0 c.c. (fixed rocker shoes)");
            // piles (p.18): 3 bands x 4 cells + an end cell per half; cell columns 16'-0 apart; rows 2'-8 1/2 (top / bottom band) and 2'-8 (middle band)
            var pile = new PlanPileGroupDefinition { Name = "piles", FootingBottom = Ft(-19.0), HeadEmbedment = Ft(0.75), BatterRatio = 4, TipElevation = Ft(-61.9), Source = "p.18 (pile location plan)",
                Flag = UnresolvedFlag.Text("p.18, p.30", "pile positions inside each cell, the end cells, the pile count and the pile length", "the cells show a staggered pattern whose exact grid points cannot all be read in the scan, no count is printed and no tip elevation is given ('driven to 100 tons'; the nearest tip in the set is the pier 17 test pile El -61.9, p.30)",
                    "Cells modeled as the 4 + 3 + 4 + 3 + 4 staggered pattern read from the plan (middle band 3 x 5); end cells 3 x 5; the battered row next to each long face; all tips at El -61.9.") };
            double[] rowTop = { 4.0, 6.7083, 9.4167, 12.125, 14.8333 };
            double[] rowMid = { 21.1667, 23.8333, 26.5, 29.1667, 31.8333 };
            double[] rowBot = { 38.1667, 40.875, 43.5833, 46.2917, 49.0 };
            var cells = new List<(int band, double x0, double pitch, int cols)>();
            for (int band = 0; band < 3; band++)
            {
                if (band == 1) { cells.Add((band, 4.25, 1.5833, 6)); for (int k = 0; k < 4; k++) cells.Add((band, 15.4167 + 16.0 * k + 3.4167, 1.8333, 6)); }
                else { cells.Add((band, 4.75, 1.5, 6)); for (int k = 0; k < 4; k++) cells.Add((band, 15.4167 + 16.0 * k + 3.25, 1.5833, 7)); }
            }
            foreach (var (band, x0, pitch, cols2) in cells)
            {
                var rows = band == 0 ? rowTop : band == 1 ? rowMid : rowBot;
                for (int ri = 0; ri < 5; ri++)
                {
                    bool offset = ri % 2 == 1;
                    for (int ci = 0; ci < cols2; ci++)
                    {
                        // 7-column cells: rows 0, 2, 4 take columns 1, 3, 5, 7 and rows 1, 3 take 2, 4, 6; 6-column cells: odd rows shifted by one
                        bool take = cols2 == 7 ? (ci % 2 == (offset ? 1 : 0)) : (ci % 2 == (offset ? 1 : 0));
                        if (!take) continue;
                        double x = x0 + ci * pitch, n = 26.5 - rows[ri];
                        // the row next to the long face carries the battered piles (top band row 2, bottom band row 4), tips toward that face
                        bool battered = band != 1 && offset && ((band == 0 && ri == 1) || (band == 2 && ri == 3));
                        foreach (int side in new[] { -1, 1 })
                        {
                            var fp = new FootingPoint(Ft(side * (79.4167 - x)), Ft(n));
                            if (battered) { fp.BatterDirS = 0; fp.BatterDirN = band == 0 ? 1 : -1; }
                            pile.Piles.Add(fp);
                        }
                    }
                }
            }
            sd.Plan.PileGroups.Add(pile);
            U(sd, "p.17, p.20", "interior of the pier body (solid, or cored / cellular)", "the plan elevation / sections show the outline and the shaft reinforcing but no interior voids could be read in the scan", "Pier body modeled SOLID, so its concrete quantity is an upper bound.");
            U(sd, "p.17, p.20", "slope of the top of the pier body (1/2in per ft from El +12.0 to +12.75 at the shafts) and the 1'-6 masonry-faced crown", "detail only", "Pier body top flat at El +12.0; shafts start there.");
            U(sd, "p.17, p.21", "7in top chamfer of the shafts", "detail only", "Square top at El. +87.701.");
            U(sd, "p.18, p.19, p.25", "cofferdam sheet piling (ZP 38), bracing, the fender system and the dolphins", "temporary / fender works, not part of this phase", "Not modeled.");
            break;
        }
    }
    sd.Unresolved.Add("NOT MODELED (this phase): reinforcement - bar marks and bar lists are on " + pg);
    piersBySupport[support] = sd;
    summary.Add($"{pier}: {sd.Plan.Solids.Count} solids, {sd.Plan.PileGroups.Sum(g => g.Piles.Count)} piles, {sd.Plan.Seats.Count} seats");
}
string r_base_a(List<string[]> rows, string pier) => rows.First(x => x[0] == pier)[16];

// ================================================================ abutments (p.11-12 west, p.38-39 east)
// frame: B = distance behind the CL shoe (toward the wings / embankment); N = -B at the west abutment (wings run west = down-station), +B at the east one
SubstructureDefinition Abutment(string name, int sgnN, string pg, double footBot, double footTop, double seat, double wallTop, double wallTopEnd, double wallBaseThk, double wallTopThk,
    double wallFace, List<(double s, double b)> footPlan, double wingEnd, double wingFootEnd, List<(double b, double z)> wingBottom, double pedS, double pedN, double pedBaseZ, double pedTopFlagZ)
{
    var sd = NewSd(name.StartsWith("E") ? "HP12x53" : "HP12x74", 0.5);
    FootingPoint PB(double s, double b) => P(s, sgnN * b);
    // footing
    var fp = footPlan.Select(t => PB(t.s, t.b)).ToList();
    sd.Plan.Solids.Add(new PlanSolidDefinition { Name = "footing", Kind = SubElementKind.Footing, Source = pg, Sections = { Sec(footBot, fp), Sec(footTop, fp.Select(x => x.Clone()).ToList()) } });
    // U wall (front wall + both side walls) on the footing: plan polygon at the base and at the top
    double sw = 41.0833;                                           // 82'-2 overall (p.11, p.38)
    List<FootingPoint> Wall(double thk, double endB)
    {
        double f = wallFace, bk = wallFace + thk;
        return new List<FootingPoint> { PB(-sw, f), PB(sw, f), PB(sw, endB), PB(sw - thk, endB), PB(sw - thk, bk), PB(-sw + thk, bk), PB(-sw + thk, endB), PB(-sw, endB) };
    }
    sd.Plan.Solids.Add(Loft("wall (front wall + side walls on the footing)", SubElementKind.Backwall, pg + " (section A-A: base thickness " + FtIn(wallBaseThk) + ", top " + FtIn(wallTopThk) + ")",
        Sec(footTop, Wall(wallBaseThk, wingFootEnd)), Sec(wallTop, Wall(wallTopThk, wingFootEnd))));
    // wing extension beyond the footing: profile (B, Z) extruded across S, bottom stepped, top following the wall top
    foreach (int side in new[] { -1, 1 })
    {
        double t = (wallBaseThk + wallTopThk) / 2;
        double s0 = side > 0 ? sw - t : -sw, s1 = side > 0 ? sw : -sw + t;
        var prof = new List<CapProfilePoint>();
        double b0 = wingFootEnd;
        prof.Add(new(Ft(sgnN * b0), Ft(wallTop)));
        prof.Add(new(Ft(sgnN * wingEnd), Ft(wallTopEnd)));
        for (int i = wingBottom.Count - 1; i >= 0; i--)
        {
            double bEnd = i == wingBottom.Count - 1 ? wingEnd : wingBottom[i + 1].b;
            prof.Add(new(Ft(sgnN * bEnd), Ft(wingBottom[i].z)));
            prof.Add(new(Ft(sgnN * wingBottom[i].b), Ft(wingBottom[i].z)));
        }
        sd.Plan.Solids.Add(new PlanSolidDefinition
        {
            Name = side > 0 ? "wing extension E" : "wing extension W", Kind = SubElementKind.Wingwall, ProfileOverN = true, N0 = Ft(s0), N1 = Ft(s1), Profile = prof,
            Source = pg + " (stepped bottom, top following the gutter profile)",
            Flag = UnresolvedFlag.Text(pg, "wing wall thickness and the exact stepped bottom", "the wall batter (inner face 3/4in per 12in) is not drawn in a section through the wing and the steps are read from the side elevation", "Constant thickness = mean of base and top thickness; steps as read."),
        });
    }
    // pedestals under the shoes (p.11 front elevation / p.38 general plan): from the footing top to the seat, 1/4in per 12in lean on every face
    foreach (int sg in new[] { -1, 1 })
    {
        double z0 = footTop, z1 = seat;
        List<FootingPoint> At(double z) { double e = TaperApproach * (z1 - z); return Rect(29 * sg - pedS / 2 - e, 29 * sg + pedS / 2 + e, sgnN * 0 - pedN / 2 - e, pedN / 2 + e); }
        var ped = Loft($"pedestal {(sg < 0 ? "W" : "E")}", SubElementKind.Pedestal, pg, Sec(z0, At(z0)), Sec(z1, At(z1)));
        sd.Plan.Solids.Add(ped);
        Seat(sd, $"shoe {(sg < 0 ? "W" : "E")}", 29 * sg, 0, seat, pg + ": CL shoe, 58'-0 c.c.");
    }
    return sd;
}

// west abutment: footing 99'-6 x (26' front strip + 22' side strips to 52'), 4'-6 thick El 16.0-20.5; walls to the gutter El ~56.4; wing ends 74'-0 from the footing front edge (p.11)
{
    var fpW = new List<(double s, double b)> { (-49.75, -6.25), (49.75, -6.25), (49.75, 45.75), (27.75, 45.75), (27.75, 19.75), (-27.75, 19.75), (-27.75, 45.75), (-49.75, 45.75) };
    var sd = Abutment("W Abutment", -1, "p.11", 16.0, 20.5, 41.974, 56.378, 55.297, 5.2292, 2.8333, 2.0, fpW, 67.75, 45.75,
        new List<(double b, double z)> { (45.75, 42.0), (59.75, 49.0) }, 6.5, 4.0, 20.5, 42.0);
    U(sd, "p.11, p.12", "wall batter and thickness profile along the height", "section A-A shows the back face leaning 3/4in per 12in and the front 1/4in per 12in, but the sections on p.12 (B-B ... F-F) are not used here", "Straight taper from 5'-2 3/4 at the base (El 20.5) to 2'-10 at the top (5in + 2'-5 curb section, p.11 plan).");
    U(sd, "p.11", "wall top elevation", "the top follows the gutter profile (El 56.378 at the gutter line, 56.778 at the CL roadway, 54.878 / 53.50 lower lines)", "Flat top at El 56.378; wing extension top descends to 55.297 at the wing end.");
    U(sd, "p.11", "seat El 41.974 vs 42.00", "revised 7-27-54: 'Final El. 41.974' (p.11 section A-A, p.5)", "Seat El 41.974.");
    U(sd, "p.11, p.14", "west-end culvert (48in RC pipe, manhole, outfall grating)", "the culvert on p.14 serves the Penn. Canal at pier 1, it is not part of the abutment", "Not modeled (noted).");
    U(sd, "p.11, p.12", "weep holes, copper water stop, construction joints, the 1:2 embankment slope lines", "details / embankment 'by others'", "Not modeled.");
    sd.Unresolved.Add("NOT MODELED (this phase): reinforcement - bar marks and bar lists are on p.12");
    piersBySupport["W Abutment"] = sd;
}
// east abutment: pile footing 88'-8 x 24'-6 (notched), El 51.154-55.154, 12 H 53 piles, walls to El 76.4, wing ends 33'-1 3/4 behind the front wall (p.38 / p.39)
{
    var fpE = new List<(double s, double b)>
    {
        (-44.3333, -5.25), (-19.6667, -5.25), (-16.6667, -2.25), (16.6667, -2.25), (19.6667, -5.25), (44.3333, -5.25), (44.3333, 19.25), (28.6667, 19.25), (28.6667, 11.75),
        (-28.6667, 11.75), (-28.6667, 19.25), (-44.3333, 19.25),
    };
    var sd = Abutment("E Abutment", +1, "p.38", 51.154, 55.154, 62.033, 76.417, 75.435, 3.9583, 2.8333, 2.0, fpE, 34.9, 19.25,
        new List<(double b, double z)> { (19.25, 62.033) }, 6.5, 4.0, 55.154, 62.075);
    U(sd, "p.38, p.39", "wall top elevation and the cross-fall", "the top is 76.817 at the CL roadway and 76.417 at the curb line (wing top 75.435 at its end)", "Flat top at 76.417; wing extension descends to 75.435.");
    U(sd, "p.38", "east pedestal sizes", "the general plan prints 6'-9 1/2 x 4'-0 at one shoe and 6'-6 x 4'-0 at the other, and the top El 62.075 (pedestal) vs 62.033 (shoe)", "Both pedestals 6'-6 x 4'-0 to El 62.033.");
    U(sd, "p.38, p.39", "wing wall bottom beyond the footing", "section A-A shows El 62.075 / 62.033 and 60.00 lines; the stepped bottom is not dimensioned", "Wing extension bottom at El 62.033.");
    U(sd, "p.38, p.39", "wall batter and thickness along the height", "base 3'-11 1/2, back face 3/4in per 12in, front 1/4in per 12in; the sections on p.39 are not used here", "Straight taper from 3'-11 1/2 at the base to 2'-10 at the top.");
    // 12 H 53 piles (p.38 pile plan): rows from the back edge 1.5 / 7.5 / 13 / 17 / 20 / 23 (ends), 9 / 13 / 17 / 20 / 23 (centre bay); columns per the dimension chain
    var pileE = new PlanPileGroupDefinition { Name = "piles", FootingBottom = Ft(51.154), HeadEmbedment = Ft(0.5), BatterRatio = 4, TipElevation = Ft(51.654 - 40.0), Source = "p.38 (pile plan, section A-A)",
        Flag = UnresolvedFlag.Text("p.38", "pile positions, count, batter and length", "section A-A shows battered 12 H 53 piles whose batter direction and ratio per row are not legible, the pile plan is dense and partly overprinted, and no tip elevation is given (60 tons by Engineering News formula, note 3)",
            "Piles built plumb on the dimensioned column / row grid (end blocks 6 rows, centre bay 5 rows), 40 ft below the cut-off El 51.654.") };
    double[] colsE = { 1.5, 7.8333, 14.1667, 19.1667, 24.1667, 27.6667, 33.1667, 38.6667, 44.3333 };
    double[] endRows = { 1.5, 7.5, 13.0, 17.0, 20.0, 23.0 }, midRows = { 9.0, 13.0, 17.0, 20.0, 23.0 };
    var sCols = colsE.Select(x => x - 44.3333).Concat(colsE.Select(x => 44.3333 - x)).Distinct().OrderBy(x => x).ToList();
    foreach (double s in sCols)
    {
        bool endBlock = Math.Abs(s) > 28.6667;
        foreach (double d in endBlock ? endRows : midRows)
            pileE.Piles.Add(P(s, 19.25 - d));
    }
    sd.Plan.PileGroups.Add(pileE);
    sd.Unresolved.Add("NOT MODELED (this phase): reinforcement - bar marks and bar lists are on p.39");
    piersBySupport["E Abutment"] = sd;
}

// ================================================================ apply to the model
int applied = 0;
foreach (var br in Doc.Project.Bridges)
    foreach (var sup in br.Supports)
    {
        if (piersBySupport.TryGetValue(sup.Name, out var sd)) { sup.Substructure = sd; applied++; }
        else sup.Substructure = new SubstructureDefinition { Enabled = false };
    }
Print($"Substructure set on {applied} supports ({piersBySupport.Count} units: 30 piers + 2 abutments expected 32).");
foreach (var s in summary) Print("  " + s);
var rep = Doc.Regenerate();
Print($"Regenerated {rep.Bridges} bridge(s) in {rep.Elapsed.TotalSeconds:0.0} s; {rep.Warnings.Count} warnings, {rep.Errors.Count} errors");
foreach (var e in rep.Errors) Print("  E: " + e);
foreach (var w in rep.Warnings.Where(w => !w.Contains("UNRESOLVED") && !w.Contains("NOT MODELED")).Take(20)) Print("  W: " + w);
Print($"  {rep.Warnings.Count(w => w.Contains("UNRESOLVED"))} UNRESOLVED flags, {rep.Warnings.Count(w => w.Contains("NOT MODELED"))} not-modeled notes");
if (!noSave) { Doc.Save(modelPath); Print("Saved " + modelPath); }
