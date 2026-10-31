// NCDOT B-2500 Bonner Unit 5 segmental box - PILES, BENT POSITIONS, DECK DRAINS, EXPANSION JOINTS (re-runnable; called by build-bonner-unit5.csx with
// "--no-regen", or standalone: sbm run "samples\plans\misc-bonner-unit5.csx" "samples\plans\Bonner Unit 5 segmental.sbm" [out.sbm]).
// Data: docs/plan-data/bonner-b2500/segmental-unit5/MISC-DATA.md (inventory of every misc plan item, built / not built + reason), piles.csv (284 piles read
// from the VECTOR footing plans by tools/bonner_piles.py), piles-bents.csv (pile data tables p.254-259, bills of material, Detail C offsets).
// Generic inputs: SegmentalDefinition.Misc (Bridge/SegmentalMisc.cs). Rules (user): nothing invented - scaled / derived values are flagged (page + plain
// reason); items the plans do not dimension are recorded and NOT built; plan differences reported, never adjusted.
using System.IO;
using System.Globalization;
using SBM.Core;
using SBM.Core.Bridge;
using SBM.Core.Geometry;

const string BridgeName = "Bonner Unit 5";
bool noRegen = Args.Contains("--no-regen");
string outPath = Args.FirstOrDefault(a => !a.StartsWith("--")) ?? Doc.FilePath ?? @"samples\plans\Bonner Unit 5 segmental.sbm";
var br = Doc.Project.Bridges.First(b => b.Name == BridgeName);
var sg = br.Segmental ?? throw new InvalidOperationException("Run build-bonner-unit5.csx first (no segmental definition).");
var inv = CultureInfo.InvariantCulture;
string U(string pages, string item, string why, string then = "") => UnresolvedFlag.Text(pages, item, why, then);
double F(double ft, double inch = 0) => Ft(ft) + In(inch);
double D(string s) => double.Parse(s, inv);

string dataDir = null;
for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
{
    var p = Path.Combine(dir.FullName, "docs", "plan-data", "bonner-b2500", "segmental-unit5");
    if (Directory.Exists(p)) { dataDir = p; break; }
}
if (dataDir == null) throw new InvalidOperationException("Run from the repo: docs/plan-data/bonner-b2500/segmental-unit5 not found.");
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

var md = new SegmentalMiscDefinition();

// ================================================================ 1. bent positions (Detail C) and piles
// Detail C on every substructure sheet (p.335 / 338 / 341 / 344 / 349 / 352): "CL FOOTING, COLUMN AND CAP" is offset from -L- / the bent workline (W.P.) -
// 4 7/8" (bent 18), 10 3/8" (19-22), 5 3/4" (23-28), 2 11/16" (29) - toward the LEFT (p.252 foundation layout: bents 22-25 split 26'-4 3/8" / 24'-7 5/8" and
// 25'-11 3/4" / 25'-0 1/4" about -L-; = DIM "B" of the geometry control, the box soffit CL uphill of the WP). The pedestal plans dimension the bearings
// "SYM. ABOUT CL" of the cap (p.359), so cap, footing, column and bearings are all centred on that line.
var bentRows = Csv("piles-bents.csv").ToDictionary(r => "Bent " + r[0]);
var pileRows = Csv("piles.csv");
foreach (var b in sg.Bents)
{
    if (!bentRows.TryGetValue(b.Name, out var r) || b.Pier is not { } pier) continue;
    int n = int.Parse(b.Name.Split(' ')[1]);
    double c = -In(D(r[12]));                                         // CL footing / column / cap, S (right +)
    void Centre(List<PlanPoint> ol)
    {
        if (ol.Count < 3) return;
        double cur = (ol.Min(v => v.S) + ol.Max(v => v.S)) / 2, d = c - cur;
        foreach (var v in ol) v.S += d;
    }
    Centre(pier.FootingOutline);
    Centre(pier.CapOutline);
    if (b.Bearings.Count == 2)
    {
        double cur = (b.Bearings[0].Offset + b.Bearings[1].Offset) / 2;
        foreach (var bb in b.Bearings) bb.Offset += c - cur;
    }
    // The footing plans (p.335-352) fix the footing orientation (the larger size along the bent control line) and position (Detail C).
    pier.Unresolved.RemoveAll(t => t.Contains("footing orientation / position"));
    // Piles: 36" square prestressed concrete piles (p.42 S0-41: 21" dia. void with 13'-0" / 11'-0" solid ends, 1" chamfers), 2:12 batter, heads 4'-0" above the
    // footing bottom (Section D-D of every footing sheet, '4'-0" (TYP.)'; bents 23 / 24: 1'-3" for the four piles under the column base, p.344 note +
    // leaders), tips at the pile data table's "PILE TIP NO HIGHER THAN" elevation (installed tips are filled in during construction).
    var fd = new SegmentalFoundationDefinition
    {
        Bent = b.Name, ColumnOffsetS = c, PileSize = In(36), PileLabel = "36\" SQ PS", PileMaterial = "precast prestressed concrete, 36-0.6\" strands (p.42)",
        BatterRatio = 6, PileChamfer = In(1), PileVoidDiameter = In(21), VoidSolidAtHead = F(13), VoidSolidAtTip = F(11), Embedment = In(D(r[10])), TipElevation = Ft(D(r[3])), PlanCount = int.Parse(r[1]), PlanTotalLength = Ft(D(r[7])),
        MaxFactoredAxialTons = D(r[2]), ScourCriticalElevation = Ft(D(r[4])),
        Source = $"p.{r[9]} footing plan, p.{r[5]} pile data ({r[6]}), p.{r[8]} bill of material",
        Unresolved = U($"p.{r[5]}", $"{b.Name} pile tips", "the installed tip elevations are blank in the pile data table (filled in during construction)",
            $"Tips at the 'pile tip no higher than' elevation {D(r[3]):0.0}; plan positions taken at the footing bottom plane (the footing plans do not say at which level the battered piles are dimensioned)."),
    };
    foreach (var pr in pileRows.Where(x => x[0] == n.ToString()))
    {
        double s = D(pr[2]), nn = D(pr[3]);
        var p = new SegmentalPileDefinition { S = c + Ft(s), N = Ft(nn), BatterS = D(pr[4]), BatterN = D(pr[5]) };
        if (n is 23 or 24 && Math.Abs(Math.Abs(s) - 7.5) < 0.01 && Math.Abs(Math.Abs(nn) - 3.75) < 0.01) p.Embedment = In(15);
        fd.Piles.Add(p);
    }
    md.Foundations.Add(fd);
}
md.Unresolved.Add(U("p.42", "36\" pile void ends", "the 21\" void has conical end fittings and the 13'-0\" head solid section is measured from the cast head (up to 2'-0\" may be cut off in the field)",
    "Void with flat ends, 13'-0\" solid below the modeled head and 11'-0\" above the tip; 1\" chamfers."));

// ================================================================ 2. deck drains (p.322 S3-74 deck drain details + notes)
// 6" dia. drain, CL 1'-7 1/2" from the deck edge (elevation: extension line on the slab edge), normal to the deck (90 deg), top 1/2" below the surface (note 1),
// PVC pipe with 4-1/2" squared lugs (note 2), on the LOW side of each segment (note 3), one per segment (note 4), equal distance from the segment ends,
// adjusted to be centred between the transverse PT tendons (note 5).
md.Drains.Add(new SegmentalDeckDrainDefinition
{
    Name = "deck drain", SegmentTypes = new() { SegmentType.Typical }, Diameter = In(6), EdgeDistance = F(1, 7.5), TopBelowSurface = In(0.5),
    Side = SegmentalDrainSide.LowSide, AlongFraction = 0.5, Material = "PVC pipe, 6 in. (p.322 note 2)", Source = "p.322 (S3-74)",
    Unresolved = U("p.322, 272, 282", "deck drain position along the segment", "note 5 centres it between the transverse PT tendons, whose positions are drawn only",
        "At the segment centre (= between tendons 2 and 3 of 4 equally spaced)."),
});
md.Unresolved.Add(U("p.322", "deck drains in the bent segments, end segments and closure joints", "note 4 says 'one drain per segment' without saying whether the split bent segments, the end segments and the cast-in-place closures get one",
    "Drains only in the 242 typical segments."));

// ================================================================ 3. modular expansion joints at bents 18 and 29
// CL modular expansion joint assembly @ 60 F: 1'-0 1/2" +- down-station of the bent 18 control line (S2-19 p.161 Section N-N) and 1'-2 1/2" +- up-station of the
// bent 29 control line (S4-15 p.407 Section K-K), i.e. on the side of the adjoining FIB unit. The opening (DIM "A") is in the "MODULAR JOINT MOVEMENT AND SETTINGS"
// table (p.333 legend: bound with the north and south transition plans) - not found in the 633 pages (text search). End segment blockout p.271 Section C-C:
// 2'-2" from the anchor face x 1'-4" deep, ends 9" from the deck edges (scaled on the vector half plan, 3/8" = 1'-0"). Parapet end posts p.327 / p.333:
// 3'-9" long, 1'-2" wide, 2'-2" above the 2'-6" parapet (end view 4'-8"). Cover plates p.333 (revision 1, 08/30/16): 3/4" plate recessed flush in the parapet
// traffic face, 4'-5" high from 3" above the deck, 6'-1" (left) / 6'-3" (right) long, 3'-4 1/2" on one side of the CL joint.
string jointFlag(string bent, string page) => U($"p.333, {page}", $"modular expansion joint at {bent}", "the joint system is by the manufacturer (special provisions) and its setting table (DIM \"A\") is bound with the transition plans, not in this set",
    "Envelope = derived opening x the parapet inside faces (40'-0\") x the blockout depth.");
string plateFlag = U("p.333", "parapet cover plate direction", "the plans draw the left / right side plates with 3'-4 1/2\" on one side of the CL joint but do not say which unit that side belongs to",
    "Long part (3'-4 1/2\") on the segmental unit's side.");
foreach (var (bent, from, page) in new[] { ("Bent 18", -F(1, 0.5), "p.161 (S2-19 Section N-N)"), ("Bent 29", F(1, 2.5), "p.407 (S4-15 Section K-K)") })
{
    var jd = new SegmentalExpansionJointDefinition
    {
        Name = $"Modular expansion joint at {bent}", Support = bent, Designation = "Modular expansion joint seal (by others)", CentreFromSupport = from,
        HalfWidth = F(20), BlockoutLength = F(2, 2), BlockoutDepth = F(1, 4), BlockoutEdgeInset = In(9),
        EndPost = new SegmentalJointEndPost { Length = F(3, 9), Width = F(1, 2), InsideFace = F(20), ParapetHeight = F(2, 6), HeightAboveParapet = F(2, 2) },
        CoverPlate = new SegmentalJointCoverPlate { Thickness = In(0.75), Height = F(4, 5), Bottom = In(3), LengthLeft = F(6, 1), LengthRight = F(6, 3), TowardUnit = F(3, 4.5),
            Material = "AASHTO M270 Gr. 50, metallized (p.333 notes)" },
        Source = $"p.271, 327, 333; {page}",
        Unresolved = new() { jointFlag(bent, page), plateFlag,
            U("p.271", $"end segment blockout ends at {bent}", "the 9\" from the deck edges is scaled on the half plan (not dimensioned)", "Blockout built to 9\" from the edges."),
            U("p.271", $"end segment top slab thickening at {bent}", "Section C-C shows a 2'-2\" thick top slab at the anchor face; the end segment is modeled with the typical outline (solid envelope)",
              "The blockout's removed concrete is clipped to the modeled wing / top slab.") },
    };
    md.Joints.Add(jd);
}
md.Unresolved.Add(U("p.161, 407 vs p.271", "end segment face vs the CL joint", "S3-23 places the anchor face 10\" from the bent control line (Section A-A, scaled) while the S2-19 / S4-15 joint sections draw it ~3\" from it",
    "Model keeps the S3 end segment (10\"); derived joint openings 3'-9\" (bent 18) / 4'-1\" (bent 29) = twice the distance anchor face - CL joint."));

// ================================================================ 4. NCDOT 2-bar metal rail (S0-36 / S0-37 p.37-38, Unit 5 post spacing S3-80..83 p.328-331)
// Aluminum alternate (ASTM B221 6061-T6; the galvanized steel notes are struck out on S0-36). Posts 1'-11 1/2" high on a 1/2" base (top 2'-0" above the parapet),
// 5 3/4" flange, 4 1/4" deep at the base; base 7 1/2" (along) x 6 13/16" (across); 4-bolt anchor assembly, 3/4" x 2 1/2" bolts, anchor lines 4" and 8 5/8" from the
// parapet traffic face (Section thru parapet: 4" + 4 5/8" + 5 3/8" = 1'-2"); rails 4" x 4 3/4" semi-elliptical extrusions, CL 10" and 1'-10" above the parapet top.
// Post chains = "LONGITUDINAL DIMENSIONS ... ALONG THE THEORETICAL EDGE OF DECK" (21'-3 1/2" from -L-), from the start bent control line of each span;
// glyph fractions resolved by the printed arc totals (every chain closes on its printed arc).
{
    var al = Doc.Project.Alignments.First(a => a.Name == br.AlignmentName);
    double edge = F(21, 3.5);
    var sup = br.Supports.ToDictionary(s => s.Name, s => s.Station);
    (double first, (int n, double s)[] g) Ch(double first, params (int, double)[] g) => (first, g);
    double IN(double ft, double inch) => ft + inch / 12;
    var c19L = Ch(IN(5, 5.5), (2, IN(2, 9)), (2, IN(11, 9.5625) / 2), (26, 6), (4, IN(5, 2)));
    var c19R = Ch(IN(5, 5.5), (2, IN(2, 9)), (2, IN(10, 3.4375) / 2), (26, 6), (3, 6));
    var c20L = Ch(IN(2, 7.875), (4, IN(5, 2)), (51, 6), (4, IN(5, 2)));
    var c20R = Ch(IN(2, 8.125), (3, 6), (51, 6), (3, 6));
    var c23L = Ch(IN(1, 7.375), (4, IN(5, 2)), (51, 6), (4, IN(5, 2)));
    var c23R = Ch(IN(2, 11.625), (3, IN(6, 3)), (51, 6), (3, IN(6, 3)));
    var c24 = Ch(IN(3, 3), (3, IN(6, 3)), (51, 6), (3, IN(6, 3)));
    var c29 = Ch(IN(3, 3), (3, IN(6, 3)), (26, 6), (2, IN(11, 0.5) / 2), (2, IN(2, 9)));
    var spans = new List<(int start, int side, (double first, (int n, double s)[] g) ch, double arc)>
    {
        (18, -1, c19L, IN(201, 6.125)), (18, 1, c19R, IN(198, 5.875)),
        (22, -1, c23L, IN(350, 6.75)), (22, 1, c23R, IN(349, 5.25)), (28, -1, c29, 200), (28, 1, c29, 200),
    };
    foreach (int b in new[] { 19, 20, 21 }) { spans.Add((b, -1, c20L, IN(352, 7.75))); spans.Add((b, 1, c20R, IN(347, 4.25))); }
    foreach (int b in new[] { 23, 24, 25, 26, 27 }) { spans.Add((b, -1, c24, 350)); spans.Add((b, 1, c24, 350)); }
    var posts = new Dictionary<int, List<double>> { [-1] = new(), [1] = new() };
    var closure = new List<string>();
    foreach (var (start, side, ch, arc) in spans)
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
        closure.Add($"span {start + 1} {(side < 0 ? "L" : "R")} arc {acc / Ft(1):0.00} vs {arc:0.00}");
    }
    Print("Rail post chains (model edge arc vs plan arc, ft): " + string.Join("; ", closure));
    var segs5 = sg.Spans.SelectMany(s => s.Segments).ToList();
    double face0 = sup["Bent 18"] + segs5.First().X0;
    double face1 = sup["Bent 28"] + segs5.Last().X1;
    string railFlag = U("p.37-38", "2-bar metal rail post / rail sections", "the post is an aluminum T-shape (5 3/4\" flange, 1/4\" plates, tapered web) and the rail a hollow 4\" x 4 3/4\" semi-elliptical extrusion; the generic railing builds outlines",
        "Posts = side outline (4 1/4\" at the base to 1 1/2\" at the top, scaled) x 5 3/4\" (envelope), front face on the base plate's traffic edge (derived); rails = 4 3/4\" x 4\" bars on the post front, CL at the printed 10\" / 1'-10\"; rail weight not counted.");
    br.Railings = new List<PedestrianRailingDefinition>();
    foreach (int side in new[] { -1, 1 })
        br.Railings.Add(new PedestrianRailingDefinition
        {
            Name = $"2-bar metal rail {(side < 0 ? "left" : "right")}", TypeName = "NCDOT 2 BAR METAL RAIL", Kind = RailingKind.Guardrail, Offset = side * F(20),
            BeginStation = face0 + F(3, 9), EndStation = face1 - F(3, 9), BaseProbe = In(7), Material = "Aluminum ASTM B221 6061-T6", Density = 2700,
            FlagsDimensionOnly = true, Source = "p.37-38 (S0-36 / S0-37), p.328-331 (S3-80..83)",
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
                U("p.331", "span 29 right side post chain", "the chain is printed for the left side only; the right side draws the same symbols", "Right side = left side chain."),
            },
        });
    Print($"2-bar metal rail: {posts[-1].Count} + {posts[1].Count} = {posts[-1].Count + posts[1].Count} posts (p.38 anchor assembly table, Unit 5: 1189).");
}

// ================================================================ 4b. fixtures, vent holes, survey blockouts (second plan search 2026-10-30; MISC-DATA.md section 1)
// Located on the plans (the first inventory said "not located" - wrong): solar array support platforms (p.392 / p.322 tables: station + segment XX-U4
// in spans 20-28; p.332 framing), navigational light conduit sleeves (p.392, N.L. on p.263-266), box girder lighting (p.388-391 plans: luminaires at
// arcs along CL span; p.385 mounting heights; p.387 panels), survey disc blockouts (p.266 SURVEY MARKERS table, p.58 detail), vent holes (p.268 note 3
// + Interior Section, p.322), access doors (p.315 / 316 at the A.O. / A.O.B. openings), vermin screens (p.314 at the end segment anchor faces).
{
    var bx = sg.Box;
    var supF = br.Supports.ToDictionary(s => s.Name, s => s.Station);
    md.Fixtures = new List<SegmentalFixtureDefinition>();
    double half = bx.TopWidth / 2;
    double parOut = -(half - F(21, 2));                                 // parapet outside face (inside face 20'-0" + 1'-2") from the deck edge, N + outboard
    double TP = F(2, 6), Zf = TP - F(4, 11);                            // top of parapet; platform floor = top of B1 (p.332 C-C: 4'-11" at CL platform)
    double w8 = In(8.14), b2n0 = parOut + In(1.75), b2n1 = b2n0 + w8;   // P5 1 3/4" on the parapet face, then B2 W8X18 (depth across)
    double zB3 = Zf - F(2);                                              // CL B3 2'-0" below the floor (C-C)
    double xOut = bx.WebRootX - (-zB3 - bx.WebTopDepth) / bx.WebSlope;   // outer web face at the B3 depth (section y = -Z, cross slope ignored)
    double nWeb = xOut - half;
    double nB11 = nWeb + In(1), nP3 = nB11 + In(8), nB3 = nP3 + In(0.5); // wedge (varies, 1"), B11 W8X31 (8"), P3 1/2"
    double[] posts = { -F(5), -F(1, 8), F(1, 8), F(5) };                // 3 SPA. @ 3'-4" = 10'-0" (p.332 PLAN), centred on CL platform
    double ha = F(10, 5.75) / 2, nOut = F(8);
    string steel = "Steel AASHTO M270 Gr 50, metalized";
    string A(double a) => (a / Ft(1)).ToString("+0.##;-0.##", inv);
    var plat = new SegmentalFixtureDefinition
    {
        Name = "Solar array support platform", Group = SBM.Core.Misc.MiscGroup.SolarPlatform, Category = "solar array support platform", Side = -1,
        Reference = SegmentalFixtureReference.DeckEdge, Source = "p.332 (S3-84), p.322 / 392 tables, p.264-266 S.A.P.",
        Stations = Enumerable.Range(0, 9).Select(k => Sta("59+92.83") + F(350 * k)).ToList(),
        Labels = Enumerable.Range(20, 9).Select(s => $"Solar array support platform span {s} ({s - 1}-U4)").ToList(),
        Unresolved = U("p.392 / 332", "solar array platform side and stack", "the side is read from the p.392 plan (north arrow: up-station to the right, platform on the top edge = LEFT of -L-), the wedge plate 'varies' and the floor level is the 4'-11\" from the top of parapet to the top of B1",
            "Built on the left; wedge 1\", grid on top of B1; P1 / P2 clips, B6 braces, bolts and ferrule loop inserts not modeled."),
    };
    foreach (var a in posts)
    {
        plat.Pieces.Add(new() { Name = $"P5 {A(a)}", Kind = SBM.Core.Misc.MiscKind.PlatformPlate, Description = "P5 PL 1 3/4 x 14 x 24 (adhesive anchors to the parapet)", Material = steel,
            A0 = a - In(7), A1 = a + In(7), N0 = parOut, N1 = b2n0, Z0 = TP - F(2), Z1 = TP });
        plat.Pieces.Add(new() { Name = $"B2 {A(a)}", Section = "W8X18", Roll = 90, Description = "B2 W8X18 post", Material = steel,
            A0 = a, A1 = a, N0 = (b2n0 + b2n1) / 2, N1 = (b2n0 + b2n1) / 2, Z0 = zB3 - w8 / 2, Z1 = TP });
        plat.Pieces.Add(new() { Name = $"B1 {A(a)}", Section = "W8X18", Description = "B1 W8X18 floor beam", Material = steel,
            A0 = a, A1 = a, N0 = b2n1, N1 = nOut, Z0 = Zf - w8 / 2, Z1 = Zf - w8 / 2 });
        plat.Pieces.Add(new() { Name = $"B3 {A(a)}", Section = "W8X18", Description = "B3 W8X18 strut to the web", Material = steel,
            A0 = a, A1 = a, N0 = nB3, N1 = b2n0, Z0 = zB3, Z1 = zB3 });
        plat.Pieces.Add(new() { Name = $"P3 {A(a)}", Kind = SBM.Core.Misc.MiscKind.PlatformPlate, Description = "P3 PL 1/2 x 9 x 14 end plate", Material = steel,
            A0 = a - In(4.5), A1 = a + In(4.5), N0 = nP3, N1 = nB3, Z0 = zB3 - In(7), Z1 = zB3 + In(7) });
        plat.Pieces.Add(new() { Name = $"B11 {A(a)}", Section = "W8X31", Roll = 90, Description = "B11 W8X31 x 1'-2\" bearing piece on the web (ferrule loop inserts)", Material = steel,
            A0 = a, A1 = a, N0 = nB11 + In(4), N1 = nB11 + In(4), Z0 = zB3 - In(7), Z1 = zB3 + In(7) });
        plat.Pieces.Add(new() { Name = $"wedge {A(a)}", Kind = SBM.Core.Misc.MiscKind.PlatformPlate, Description = "wedge PL varies x 9 x 14 (taken 1\")", Material = steel,
            A0 = a - In(4.5), A1 = a + In(4.5), N0 = nWeb, N1 = nB11, Z0 = zB3 - In(7), Z1 = zB3 + In(7) });
    }
    plat.Pieces.Add(new() { Name = "grating", Kind = SBM.Core.Misc.MiscKind.PlatformGrating, Description = "1\" x 4\" open grid floor, 3/16 x 3/4 bearing bars", Material = steel,
        A0 = -ha, A1 = ha, N0 = b2n1, N1 = nOut, Z0 = Zf, Z1 = Zf + In(0.75) });
    foreach (var (zr, nm) in new[] { (Zf + F(3, 6), "top rail B8"), (Zf + F(1, 9), "mid rail B4") })
    {
        plat.Pieces.Add(new() { Name = nm + " outer", Section = "L3X3X1/4", Description = nm + " L3X3X1/4", Material = steel, A0 = -ha, A1 = ha, N0 = nOut, N1 = nOut, Z0 = zr, Z1 = zr });
        foreach (int e in new[] { -1, 1 })
            plat.Pieces.Add(new() { Name = $"{nm} end {(e < 0 ? "down" : "up")}", Section = "L3X3X1/4", Description = nm + " L3X3X1/4", Material = steel, A0 = e * ha, A1 = e * ha, N0 = b2n1, N1 = nOut, Z0 = zr, Z1 = zr });
    }
    foreach (var a in posts)
        plat.Pieces.Add(new() { Name = $"B5 post {A(a)}", Section = "L3X3X1/4", Description = "B5 L3X3X1/4 rail post", Material = steel, A0 = a, A1 = a, N0 = nOut, N1 = nOut, Z0 = Zf, Z1 = Zf + F(3, 6) });
    foreach (int e in new[] { -1, 1 })
        plat.Pieces.Add(new() { Name = $"B5 end post {(e < 0 ? "down" : "up")}", Section = "L3X3X1/4", Description = "B5 L3X3X1/4 rail post", Material = steel, A0 = e * ha, A1 = e * ha, N0 = F(4), N1 = F(4), Z0 = Zf, Z1 = Zf + F(3, 6),
            Unresolved = U("p.332", "platform end rail posts", "the bill lists six B5 posts; four line up with the B1 beams on the outer edge, the other two are not dimensioned", "Two end posts at mid depth of the platform.") });
    plat.Pieces.Add(new() { Name = "toe plate B7", Kind = SBM.Core.Misc.MiscKind.PlatformPlate, Description = "B7 PL 1/4 x 5 toe plate", Material = steel, A0 = -ha, A1 = ha, N0 = nOut - In(0.25), N1 = nOut, Z0 = Zf, Z1 = Zf + In(5) });
    foreach (int e in new[] { -1, 1 })
        plat.Pieces.Add(new() { Name = $"toe plate B9 {(e < 0 ? "down" : "up")}", Kind = SBM.Core.Misc.MiscKind.PlatformPlate, Description = "B9 PL 1/4 x 5 toe plate", Material = steel,
            A0 = e > 0 ? ha - In(0.25) : -ha, A1 = e > 0 ? ha : -ha + In(0.25), N0 = b2n1, N1 = nOut, Z0 = Zf, Z1 = Zf + In(5) });
    for (int k = 0; k < 5; k++)
        plat.Pieces.Add(new() { Name = $"rung {k + 1}", Section = "ROD 3/4", Description = "B10 3/4\" rung (6\" + 4 SPA. @ 1'-0\" below the top of parapet)", Material = steel,
            A0 = posts[1], A1 = posts[2], N0 = b2n1 + In(1), N1 = b2n1 + In(1), Z0 = TP - In(6) - F(k), Z1 = TP - In(6) - F(k) });
    string byOthers = U("p.392 (Section B-B), p.17", "solar array and battery box sizes", "they are by others (special provisions); only their outlines are drawn on the NTS section",
        "Envelopes scaled from p.392 Section B-B: array panel 5'-6\" at 30 deg (box envelope), battery box 2'-6\" x 2'-0\" x 10\".");
    plat.Pieces.Add(new() { Name = "solar array", Kind = SBM.Core.Misc.MiscKind.SolarArray, Description = "solar array (by others), 30 deg", Material = "Solar array (by others)", Placeholder = true,
        PlaceholderBasis = "by others (p.392, special provisions)", A0 = -F(2, 9), A1 = F(2, 9), N0 = F(3), N1 = F(7, 9), Z0 = Zf + F(4), Z1 = Zf + F(6, 9), Unresolved = byOthers });
    plat.Pieces.Add(new() { Name = "battery box", Kind = SBM.Core.Misc.MiscKind.Battery, Description = "battery box (by others)", Material = "Battery box (by others)", Placeholder = true,
        PlaceholderBasis = "by others (p.392, special provisions)", A0 = -F(1), A1 = F(1), N0 = F(2, 6), N1 = F(5), Z0 = Zf + In(0.75), Z1 = Zf + In(10.75), Unresolved = byOthers });
    md.Fixtures.Add(plat);

    // Navigational light conduit: 2 1/2" PVC sleeve cast in the slab at each platform station, to the far side (p.392 plan + Section A-A; N.L. p.263-266).
    md.Fixtures.Add(new SegmentalFixtureDefinition
    {
        Name = "Navigational light conduit sleeve", Group = SBM.Core.Misc.MiscGroup.Lighting, Category = "conduit", Side = 1, Reference = SegmentalFixtureReference.DeckSection,
        Source = "p.392 (S3-144), p.263-266 N.L.", Stations = plat.Stations.ToList(), Labels = Enumerable.Range(20, 9).Select(s => $"Nav light conduit sleeve span {s}").ToList(),
        Unresolved = U("p.392", "nav light conduit sleeve position", "the plan marks it 'APPROXIMATE LOCATION' and Section A-A draws it in the top slab without a depth",
            "2 1/2\" PVC sleeve (2.875\" OD) at the platform station, mid-depth of the 9\" top slab, edge to edge (3\" in); the 1 1/4\" stainless conduit inside is not modeled separately."),
        Pieces = { new() { Kind = SBM.Core.Misc.MiscKind.Conduit, Section = "ROD 2 7/8", Description = "2 1/2\" PVC sleeve for the 1 1/4\" stainless steel nav light conduit", Material = "PVC Schedule 80",
            A0 = 0, A1 = 0, N0 = -half + In(3), N1 = half - In(3), Z0 = -bx.TopSlab / 2, Z1 = -bx.TopSlab / 2 } },
    });
    md.Unresolved.Add(U("p.392", "navigational lights, junction boxes and 10/3 MC cable", "they are by others and located only relative to the navigation channel (green CL light, red margin lights 100'-0\" each side, both parapets); the channel CL station is not given (every span 20-28 has a platform)",
        "Not built."));

    // Box girder lighting (p.388-391 plans, p.385 sections, p.387 notes): luminaires on the inside face of the RIGHT web (drawn on the lower web line of
    // the up-station-to-the-right plans), at arcs along CL span from the down-station bent control line; A = 5'-0" mounting height / 20 deg, B = 7'-0" / 15 deg.
    var chains = new List<(int span, double first, string types, double last)>
    {
        (19, F(21, 6 + 3.0 / 16), "AABB", F(25, 11 + 7.0 / 8)),
        (29, F(24, 1.0 / 16), "BBAA", F(24, 11 + 15.0 / 16)),
    };
    for (int s = 20; s <= 28; s++) chains.Add((s, F(24, 1.0 / 16), "BBAAABB", s <= 22 ? F(24, 11.75) : F(24, 11.5)));
    var stA = new List<double>(); var stB = new List<double>(); var lbA = new List<string>(); var lbB = new List<string>();
    var closureL = new List<string>();
    foreach (var (span, first, types, last) in chains.OrderBy(c => c.span))
    {
        double s0 = supF[$"Bent {span - 1}"], s1 = supF[$"Bent {span}"];
        for (int i = 0; i < types.Length; i++)
        {
            double st = s0 + first + F(50) * i;
            (types[i] == 'A' ? stA : stB).Add(st); (types[i] == 'A' ? lbA : lbB).Add($"Luminaire {types[i]} span {span} no. {i + 1}");
        }
        double chainLen = first + F(50) * (types.Length - 1) + last;
        closureL.Add($"span {span} {chainLen / Ft(1):0.00} vs {(s1 - s0) / Ft(1):0.00}");
    }
    Print("Box lighting chains (arc along CL span vs -L- span, ft): " + string.Join("; ", closureL));
    string litFlag = U("p.385-391", "box girder lighting envelopes and positions", "fixture / outlet box sizes are product dimensions (only the 18\" x 18\" x 6\" junction box is given), the sections are NTS, the plans draw the units on the lower web (= right side) and each span's arc chain closes 1'-0\" to 2'-6\" short of the span (taken from the down-station bent)",
        "Luminaire envelope 14\" x 8\" x 5\" at the mounting height, receptacle box 4\" x 4\" x 2 1/2\" centred 18\" above the floor, junction box centred 42\" (A) / 60\" (B) above the floor (p.385 note 3: '42\" or 60\"').");
    foreach (var (kind, sts, lbs, h, jb) in new[] { ("A", stA, lbA, F(5), In(42)), ("B", stB, lbB, F(7), In(60)) })
        md.Fixtures.Add(new SegmentalFixtureDefinition
        {
            Name = $"Box girder luminaire {kind}", Group = SBM.Core.Misc.MiscGroup.Lighting, Category = "box girder lighting", Side = 1, Reference = SegmentalFixtureReference.WebInside,
            Source = "p.388-391 (S3-140..143), p.385 (S3-137)", Stations = sts, Labels = lbs, Unresolved = litFlag,
            Pieces =
            {
                new() { Name = "luminaire", Kind = SBM.Core.Misc.MiscKind.Luminaire, Description = $"LED luminaire 51 W (basis of design Cooper Valet LED tunnel), mounting height {(kind == "A" ? "5'-0\", tilt 20" : "7'-0\", tilt 15")} deg",
                    Material = "Die cast aluminum housing", A0 = -In(7), A1 = In(7), N0 = 0, N1 = In(8), Z0 = h - In(2.5), Z1 = h + In(2.5) },
                new() { Name = "junction box", Kind = SBM.Core.Misc.MiscKind.ElectricalBox, Description = "NEMA 4X nonmetallic junction box 18\" x 18\" x 6\"", Material = "Nonmetallic (NEMA 4X)",
                    A0 = -In(9), A1 = In(9), N0 = 0, N1 = In(6), Z0 = jb - In(9), Z1 = jb + In(9) },
                new() { Name = "receptacle", Kind = SBM.Core.Misc.MiscKind.OutletBox, Description = "GFCI duplex receptacle 20 A 120 V in a weatherproof PVC outlet box", Material = "PVC",
                    A0 = In(12), A1 = In(16), N0 = 0, N1 = In(2.5), Z0 = In(16), Z1 = In(20) },
            },
        });
    string panelFlag = U("p.385, 387, 388, 391", "panelboard size and height", "the 100 A NEMA 4X enclosure size is a product dimension and the 6'-0\" on the NTS main panel section is read as floor to the top of the enclosure",
        "Envelope 20\" wide x 30\" high x 6\" on the right web, top 6'-0\" above the floor.");
    double pA = supF["Bent 18"] + F(65, 5 + 3.0 / 16), pB = supF["Bent 29"] - F(44, 7.25);
    md.Fixtures.Add(new SegmentalFixtureDefinition
    {
        Name = "Panelboard", Group = SBM.Core.Misc.MiscGroup.Lighting, Category = "box girder lighting", Side = 1, Reference = SegmentalFixtureReference.WebInside,
        Source = "p.387 (S3-139 panel schedules), p.388 / 391", Unresolved = panelFlag, Stations = new() { pA, pB }, Labels = new() { "Panel A (span 19)", "Panel B (span 29)" },
        Pieces = { new() { Kind = SBM.Core.Misc.MiscKind.Panelboard, Description = "Panelboard 120/240 V 1-phase 100 A, 50 A main, NEMA 4X", Material = "Nonmetallic (NEMA 4X)",
            A0 = -In(10), A1 = In(10), N0 = 0, N1 = In(6), Z0 = F(6) - In(30), Z1 = F(6) } },
    });
    md.Fixtures.Add(new SegmentalFixtureDefinition
    {
        Name = "Service box in traffic railing", Group = SBM.Core.Misc.MiscGroup.Lighting, Category = "box girder lighting", Side = 1, Reference = SegmentalFixtureReference.DeckEdge,
        Source = "p.385 note 5, p.388 / 391", Stations = new() { pA, pB }, Labels = new() { "Service box span 19", "Service box span 29" },
        Unresolved = U("p.385, 388, 391", "service boxes in the traffic railing", "the span 19 box is dimensioned along the deck edge (64'-11 1/4\", taken at the panel A station), the span 29 box is not dimensioned and the height in the parapet is not given",
            "18\" x 18\" x 8\" box flush with the right parapet traffic face, centred at mid parapet height, at the panel stations."),
        Pieces = { new() { Kind = SBM.Core.Misc.MiscKind.ElectricalBox, Description = "18\" x 18\" x 8\" NEMA 4X junction box with back plate embedded in the traffic railing", Material = "Nonmetallic (NEMA 4X)",
            A0 = -In(9), A1 = In(9), N0 = -(half - F(20)), N1 = -(half - F(20)) + In(8), Z0 = F(1, 3) - In(9), Z1 = F(1, 3) + In(9) } },
    });
    md.Unresolved.Add(U("p.385-391", "box girder lighting conduit runs, hoist receptacles at the bents, grounding", "the conduit layout is left to the contractor's shop drawings (p.387 note 1) and the hoist receptacles / grounding plates at the bents are not dimensioned", "Not built."));

    // Access doors on the A.O. (3'-0" x 4'-0", p.316) and A.O.B. (3'-0" dia., p.315) openings; hinges / stop bars / slide bolts / ladder not modeled.
    double SegCentre(string name)
    {
        var sp = sg.Spans.First(s => s.Segments.Any(x => x.Name == name));
        var sd = sp.Segments.First(x => x.Name == name);
        return supF[$"Bent {sp.Span + 18}"] + (sd.X0 + sd.X1) / 2;      // Span index 0 = span 19 (starts at Bent 18)
    }
    string doorFlag = U("p.316 / 315", "access door assemblies", "hinges, stop bars, slide bolts, base plates and the A.O. ladder are small parts whose positions around the opening are not all dimensioned", "Door leaves only, on the floor, centred on the opening.");
    var ao = sg.Openings?.FirstOrDefault(o => o.Name.StartsWith("A.O. access"));
    if (ao != null)
        md.Fixtures.Add(new SegmentalFixtureDefinition
        {
            Name = "A.O. access door", Group = SBM.Core.Misc.MiscGroup.Access, Category = "access hatch", Side = 1, Reference = SegmentalFixtureReference.Floor, Source = "p.316 (S3-68)",
            Stations = ao.Segments.Select(SegCentre).ToList(), Labels = ao.Segments.Select(s => $"A.O. access door {s}").ToList(), Unresolved = doorFlag,
            Pieces = { new() { Kind = SBM.Core.Misc.MiscKind.AccessDoor, Description = "two 1/4\" access door leaves 3'-3\" x 2'-2\" over the 3'-0\" x 4'-0\" opening", Material = "Steel ASTM A709 Gr 36, metalized",
                A0 = -F(2, 2), A1 = F(2, 2), N0 = -F(1, 7.5), N1 = F(1, 7.5), Z0 = 0, Z1 = In(0.25) } },
        });
    md.Fixtures.Add(new SegmentalFixtureDefinition
    {
        Name = "A.O.B. access door", Group = SBM.Core.Misc.MiscGroup.Access, Category = "access hatch", Side = 1, Reference = SegmentalFixtureReference.Floor, Source = "p.315 (S3-67)",
        Stations = Enumerable.Range(19, 10).Select(n => supF[$"Bent {n}"]).ToList(), Labels = Enumerable.Range(19, 10).Select(n => $"A.O.B. access door Bent {n}").ToList(), Unresolved = doorFlag,
        Pieces = { new() { Kind = SBM.Core.Misc.MiscKind.AccessDoor, Section = "ROD 38", Description = "3/16\" access door 3'-2\" dia. over the 3'-0\" dia. opening", Material = "Steel ASTM A709 Gr 36, metalized",
            A0 = 0, A1 = 0, N0 = 0, N1 = 0, Z0 = 0, Z1 = In(3.0 / 16) } },
    });
    // Vermin screens on the end segment anchor faces (p.314 'ANCHOR FACE SECTION', p.271 note 8).
    string screenFlag = U("p.314, 271", "vermin screen position", "the end segment is a solid envelope in the model (its diaphragm opening is not built) and the screen bottom is read at the top of the 10\" bottom slab of p.271 Section A-A",
        "Envelope 10'-2\" wide to 3'-2\", stepped to the 3'-6\" door at 6'-0\" (sloped sides enveloped), 3\" deep (L3x3 frames), on the anchor face; wire cloth, door, hinges and anchors not modeled separately.");
    var segAll = sg.Spans.SelectMany(s => s.Segments).ToList();
    foreach (var (bent, first) in new[] { ("Bent 18", true), ("Bent 29", false) })
    {
        double face = first ? supF["Bent 18"] + segAll.First().X0 : supF["Bent 28"] + segAll.Last().X1;
        double tB = In(10);
        var fx = new SegmentalFixtureDefinition
        {
            Name = $"Vermin screen at {bent}", Group = SBM.Core.Misc.MiscGroup.Access, Category = "vermin screen", Side = 1, Reference = SegmentalFixtureReference.Floor,
            Source = "p.314 (S3-66), p.271 (S3-23)", Stations = new() { face }, Labels = new() { $"Vermin screen {bent}" }, Unresolved = screenFlag,
        };
        foreach (var (w, z0, z1) in new[] { (F(10, 2), 0.0, F(3, 2)), (F(7), F(3, 2), F(4, 7)), (F(3, 6), F(4, 7), F(6)) })
            fx.Pieces.Add(new() { Kind = SBM.Core.Misc.MiscKind.VerminScreen, Description = "vermin screen (2 mesh wire cloth on L3x3x1/4 frames) with door", Material = "Steel A36 galvanized, metalized wire cloth",
                A0 = first ? -In(3) : 0, A1 = first ? 0 : In(3), N0 = -w / 2, N1 = w / 2, Z0 = z0, Z1 = z1 });
        md.Fixtures.Add(fx);
    }

    // Vent holes (p.268 note 3 + Interior Section: 2" dia. at the centre of each segment on the low side of the cell, CL 4'-1 1/2" in from the soffit
    // corner; the p.322 screen + caulking not modeled) and survey disc blockouts (p.266 SURVEY MARKERS table; p.58: 3 1/2" square, 3/4" to 1" deep;
    // N = a pair 14'-0" left and right of CL, TP = 2'-0" (min.) from the barrier face on the high side).
    sg.Openings ??= new List<SegmentalOpeningDefinition>();
    sg.Openings.RemoveAll(o => o.Name.StartsWith("vent hole") || o.Name.StartsWith("survey disc"));
    var typical = segAll.Where(x => x.Type == SegmentType.Typical).Select(x => x.Name).ToList();
    sg.Openings.Add(new() { Name = "vent hole", Kind = SegmentalOpeningKind.BottomSlab, Diameter = In(2), CenterOffset = F(4, 1.5), FromSoffitCorner = true, Side = SegmentalDrainSide.LowSide,
        Segments = typical, Source = "p.268 (S3-20) note 3 + Interior Section, p.322",
        Unresolved = U("p.268, 271", "vent holes outside the typical segments", "note 3 is on the typical segment sheet; the end segment 2 1/2\" top slab vent is drawn only (p.271)", "Typical segments only.") });
    var smk = Csv("survey-markers.csv").Select(r => (sta: Sta(r[1]), d: r[2])).ToList();
    string survFlag = U("p.58, 263, 266", "survey blockout depth and TP offset", "the blockout is '3/4\" min., 1\" max.' deep and TP is '2'-0\" (MIN.)' from the barrier face",
        "1\" deep x 3 1/2\" square; TP centre 2'-0\" from the barrier face (18'-0\" from -L-), along at the table station.");
    foreach (var (nm, x, sd, d) in new (string, double, SegmentalDrainSide, string)[] {
        ("survey disc blockout N left", F(14), SegmentalDrainSide.Left, "N"),
        ("survey disc blockout N right", F(14), SegmentalDrainSide.Right, "N"),
        ("survey disc blockout TP", F(18), SegmentalDrainSide.HighSide, "TP") })
        sg.Openings.Add(new() { Name = nm, Kind = SegmentalOpeningKind.TopSlab, Width = In(3.5), Length = In(3.5), Depth = In(1), CenterOffset = x, Side = sd,
            Stations = smk.Where(r => r.d == d).Select(r => r.sta).ToList(), Source = "p.266 (S3-18) SURVEY MARKERS table, p.58 (S0-55), p.263 legend", Unresolved = survFlag });
    Print($"Fixtures: {md.Fixtures.Count} definitions ({md.Fixtures.Sum(f => f.Stations.Count)} assemblies); vent holes in {typical.Count} typical segments; survey blockouts {smk.Count(r => r.d == "N") * 2} N + {smk.Count(r => r.d == "TP")} TP.");
}


// ================================================================ 5. plan items inventoried, not built (MISC-DATA.md)
void Gap(string pages, string item, string why) => md.Unresolved.Add(U(pages, item, why, "Not modeled."));
Gap("p.317-320", "disc bearing details", "vendor-designed bearings; the generic bearing block of build-bonner-unit5.csx stays");
Gap("p.44-57 (S0-43..54)", "utility conduit system in spans 19-29 (electrical + fiber optic hangers on -L-, optional floor splice boxes)", "the hanger spacing / conduit profile vary and the splice box locations are per the utility working drawings, not in this set");
sg.Misc = md;
Print($"Misc: {md.Foundations.Count} pile groups ({md.Foundations.Sum(f => f.Piles.Count)} piles), {md.Drains.Count} drain definition, {md.Joints.Count} expansion joints, {md.Fixtures.Count} fixture definitions, {md.Unresolved.Count} unresolved / not built items.");

if (!noRegen)
{
    var rep = Doc.Regenerate();
    Print($"Regenerated in {rep.Elapsed.TotalSeconds:0.0} s; {rep.Errors.Count} errors");
    var sm = Doc.Segmental(BridgeName);
    Print($"Piles {sm.Substructure?.Units.Sum(u => u.Foundations.Sum(f => f.Piles.Count)) ?? 0}, misc elements {sm.Substructure?.Misc?.Elements.Count ?? 0}");
    Doc.Save(outPath);
    Print($"Saved {outPath}");
}
