// FINISH (required, see CLAUDE.md "New plan-set model checklist" / docs/MODEL-PIPELINE.md): `sbm run <this script> <model.sbm>` runs `sbm pipeline` after the
// script saves = regenerate, terrain (georeferenced: 3DEP for the model extents), companion database; UNRESOLVED items are printed.
// BLATNIK BRIDGE REPLACEMENT - TWO-TOWER CABLE-STAYED ALTERNATIVE (concept; user 2026-10-27). I-535 / US 53 over St. Louis Bay, Superior WI - Duluth MN.
// NO DRAWINGS: the selected replacement is a tied arch (Ames-Kraemer JV, design-build, open by 2031-10-31); this model is a cable-stayed STARTING SHAPE for a
// parametric study, not a design. Sources (read 2026-10-27):
//   - alignment: OpenStreetMap I-535 carriageways (ways 119012021 + 778140088 northbound, 48535656 + 778140087 southbound), centerline = mean of the two,
//     three tangents fitted (rms 0.16 / 0.32 m), curve radii fitted to the carriageway points (855 / 841 m) -> NAD83 / UTM 15N;
//   - navigation: NOAA ENC (encdirect enc_harbour, Bridge_area, SORDAT 2006-08-30): main channel span VERCLR 37.4 m / HORCLR 140.2 m, crossing the CL
//     between the two points below (West Gate Basin channel); Howard's Bay span VERCLR 31.3 m / HORCLR 45.7 m (Howards Bay channel);
//   - MnDOT project bulletin (2026): new main span 720 ft, Howard's Pocket span 485 ft, four 12 ft lanes + full shoulders + 12 ft shared-use path, about
//     120 ft above the river;
//   - ground / water: USGS 3DEP point elevations (EPQS, NAVD88 ft) at every support; water surface 601.96.
//   sbm new "samples\plans\Blatnik cable-stayed.sbm"
//   sbm run samples\plans\build-blatnik-cable-stayed.csx "samples\plans\Blatnik cable-stayed.sbm" -- "samples\plans\Blatnik cable-stayed.sbm"
//   sbm cable-stayed "samples\plans\Blatnik cable-stayed.sbm"        (stays, pylon balance, clearances, analysis wireframe)
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Geometry;

string outPath = Args.Length > 0 ? Args[0] : (Doc.FilePath ?? @"samples\plans\Blatnik cable-stayed.sbm");

// ================================================================ alignment (NAD83 / UTM 15N, metres)
const string AlName = "CL I-535 Blatnik Bridge replacement (concept)";
Doc.Project.Alignments.RemoveAll(a => a.Name == AlName);
// start 250 m south of the south bridge end on the Superior tangent (az 359.40), PI 1 (az 359.40 -> 336.12, R 855 m), PI 2 (-> 315.44, R 841 m), end 400 m past PI 2.
var al = Doc.AddAlignment(AlName, Sta("100+00"), new[]
{
    new PiRow(5175848.452, 568930.331),
    new PiRow(5177113.189, 568917.043, 855.0),
    new PiRow(5178328.212, 568379.116, 841.0),
    new PiRow(5178613.224, 568098.459),
});
al.Description = "I-535 / US 53 Blatnik Bridge centerline: mean of the OpenStreetMap carriageways (2026-10-27), tangents and radii fitted; concept for a replacement on the existing line";
Doc.Project.Georeference = new SBM.Core.Project.GeoreferenceDefinition
{
    CrsName = "EPSG:26915", Description = "NAD83 / UTM zone 15N (m); project X/Y = UTM metres", GeodeticDatum = "NAD83", MapProjection = "Transverse Mercator",
    MapZone = "UTM 15N", MapUnitUsSurveyFoot = false, GridScale = 1,
    Basis = "OpenStreetMap I-535 carriageways (2026-10-27) projected with tools/terrain geotiff.utm(15); elevations NAVD88 ft from USGS 3DEP (EPQS point queries)",
};
double StaAt(double e, double n) => al.Inverse(new Vec2(e, n)).Station;

// ================================================================ supports (UTM E, N; 3DEP ground / water NAVD88 ft at the point)
// Span layout (concept): main span 720 ft centred on the NOAA channel span, side spans 288 ft (0.4 x main) to the anchor piers, Howard's Pocket span
// 485 ft centred on the Howards Bay channel, approach spans equal and <= 240 ft (234.3 / 238.3 / 228.0 ft).
var sup = new (string Name, double E, double N, double Ground)[]
{
    ("South abutment", 568932.095, 5176098.484, 625.97),
    ("Pier 1", 568926.510, 5176168.988, 610.15), ("Pier 2", 568925.869, 5176240.394, 606.81), ("Pier 3", 568925.228, 5176311.800, 606.37),
    ("Pier 4", 568924.587, 5176383.205, 605.88), ("Pier 5", 568923.946, 5176454.611, 605.66), ("Pier 6", 568923.305, 5176526.017, 605.56),
    ("Pier 7", 568922.663, 5176597.422, 605.53), ("Pier 8", 568922.022, 5176668.828, 605.45), ("Pier 9", 568921.381, 5176740.234, 605.79),
    ("Pier 10", 568920.551, 5176811.637, 602.09), ("Pier 11", 568918.054, 5176959.444, 601.96),
    ("Pier 12", 568915.211, 5177031.976, 601.96), ("Pier 13", 568905.346, 5177103.906, 603.90), ("Pier 14", 568886.927, 5177174.112, 604.61),
    ("Pier 15", 568861.606, 5177242.173, 605.14), ("Pier 16", 568831.807, 5177308.408, 606.28), ("Pier 17", 568801.477, 5177374.406, 604.80),
    ("Pier 18", 568772.027, 5177440.802, 604.19),
    ("Anchor pier A", 568742.576, 5177507.198, 603.60), ("Tower A", 568706.986, 5177587.442, 601.96),
    ("Tower B", 568618.393, 5177788.221, 601.96), ("Anchor pier B", 568582.604, 5177868.376, 603.66),
    ("Pier 19", 568554.341, 5177931.868, 605.36), ("Pier 20", 568526.170, 5177995.400, 606.42), ("Pier 21", 568498.259, 5178059.049, 606.21),
    ("Pier 22", 568470.381, 5178122.711, 607.41), ("Pier 23", 568442.086, 5178186.184, 605.37), ("Pier 24", 568412.104, 5178248.880, 605.09),
    ("Pier 25", 568376.669, 5178308.647, 605.54),
    ("North abutment", 568336.428, 5178365.279, 614.08),
};
const double Water = 601.96;   // 3DEP water surface (NAVD88 ft)
// foundation tops: on land 3 ft below the ground, in the water at the water surface (pile cap tops at the waterline) - both concept choices
double BaseFt(double ground) => ground <= Water + 0.2 ? Water : ground - 3.0;

// ================================================================ profile
// Two charted clearances govern (NOAA ENC, above Lake Superior Low Water Datum 601.1 ft IGLD 85): 37.4 m over the main channel span and 31.3 m over
// the Howard's Bay span, both kept over the whole charted span width + the deck depth (grade to the bottom of the edge girders). VPIs: alignment start, Howard's Bay span centre (crest curve 400 ft),
// main channel centre (crest curve 900 ft = AASHTO K 151 for 60 mph x ~6 %), alignment end on a -4.0 % grade toward Duluth. The three VPI elevations are
// SOLVED so that both clearances are met exactly and the deck at the south abutment is 20 ft above the 3DEP ground (concept choice); grades follow.
double chanA = StaAt(568693.587, 5177617.848), chanB = StaAt(568631.802, 5177757.818);
double howA = StaAt(568919.758, 5176858.544), howB = StaAt(568918.846, 5176912.537);
double pvi = (chanA + chanB) / 2, pviH = (howA + howB) / 2, g2 = -0.040;
// deck depth = grade to the bottom of the edge girders (the TwoTower default section)
double deckDepth = CableStayedConcepts.TwoTower("a", 0, 0, 1, "b", 1, 0, 1, 0, 0).Deck.EdgeGirders!.Depth;
double lwd = Ft(601.1), mainClr = 37.4, howClr = 31.3;
double staSA = StaAt(568932.095, 5176098.484), gradeSA = Ft(625.97 + 20);
VerticalProfile Prof(double e0, double e1, double e2) => new VerticalProfile
{
    Vpis = { new Vpi(al.StartStation, e0), new Vpi(pviH, e1, Ft(400)), new Vpi(pvi, e2, Ft(900)), new Vpi(al.EndStation, e2 + g2 * (al.EndStation - pvi)) },
};
double MinOver(VerticalProfile pr, double a, double b) { double lo = double.MaxValue; for (int i = 0; i <= 200; i++) lo = Math.Min(lo, pr.ElevationAt(a + (b - a) * i / 200.0)); return lo; }
double[] Res(double[] e)
{
    var pr = Prof(e[0], e[1], e[2]);
    return new[] { pr.ElevationAt(staSA) - gradeSA, MinOver(pr, howA, howB) - (lwd + howClr + deckDepth), MinOver(pr, chanA, chanB) - (lwd + mainClr + deckDepth) };
}
// the residuals are linear in the VPI elevations away from a min-location switch: Newton with a finite-difference Jacobian, a few passes
var e = new[] { gradeSA, Ft(712), Ft(743) };
for (int it = 0; it < 6; it++)
{
    var r0 = Res(e);
    var J = new double[3, 3];
    for (int j = 0; j < 3; j++) { var ej = (double[])e.Clone(); ej[j] += 0.1; var rj = Res(ej); for (int i = 0; i < 3; i++) J[i, j] = (rj[i] - r0[i]) / 0.1; }
    // 3x3 solve (Cramer)
    double Det(double[,] m) => m[0,0]*(m[1,1]*m[2,2]-m[1,2]*m[2,1]) - m[0,1]*(m[1,0]*m[2,2]-m[1,2]*m[2,0]) + m[0,2]*(m[1,0]*m[2,1]-m[1,1]*m[2,0]);
    double d = Det(J);
    for (int j = 0; j < 3; j++)
    {
        var Jj = (double[,])J.Clone();
        for (int i = 0; i < 3; i++) Jj[i, j] = -r0[i];
        e[j] += Det(Jj) / d;
    }
}
al.Profile = Prof(e[0], e[1], e[2]);
double g0 = (e[1] - e[0]) / (pviH - al.StartStation), g1 = (e[2] - e[1]) / (pvi - pviH);
Print($"Profile: VPI {Doc.Units.FormatStation(pviH)} el {e[1] / 0.3048:0.00} ft, crest VPI {Doc.Units.FormatStation(pvi)} el {e[2] / 0.3048:0.00} ft; grades {g0 * 100:+0.00;-0.00} % / {g1 * 100:+0.00;-0.00} % / {g2 * 100:+0.00;-0.00} %");
if (Math.Abs(g0) > 0.05 || Math.Abs(g1) > 0.05) Print("CHECK: a grade exceeds 5 %");

// ================================================================ bridge
const string Name = "Blatnik replacement (two-tower cable-stayed)";
Doc.Project.Bridges.RemoveAll(b => b.Name == Name);
var br = new BridgeDefinition { Name = Name, AlignmentName = al.Name, Structure = "I-535 Blatnik Bridge replacement (cable-stayed concept)" };
br.FieldSplices.Clear();
br.Girders.Count = 0;
br.LateralBracing.Enabled = false;
foreach (var s in sup)
{
    var t = s.Name.Contains("abutment") ? SupportType.Abutment : SupportType.Pier;
    var d = new SupportDefinition { Name = s.Name, Type = t, Station = StaAt(s.E, s.N), SkewDegrees = 0 };
    d.Substructure = SubstructureDefinition.Default(t).Clone();
    d.Substructure.Enabled = false;   // the cable-stayed generator builds its own piers
    br.Supports.Add(d);
}
double SupStation(string n) => br.Supports.First(s => s.Name == n).Station;
double staA = SupStation("Tower A"), staB = SupStation("Tower B");
double baseA = Ft(BaseFt(sup.First(s => s.Name == "Tower A").Ground)), baseB = Ft(BaseFt(sup.First(s => s.Name == "Tower B").Ground));
Print($"Towers at {Doc.Units.FormatStation(staA)} / {Doc.Units.FormatStation(staB)}: main span {(staB - staA) / 0.3048:0.0} ft; side spans "
    + $"{(staA - SupStation("Anchor pier A")) / 0.3048:0.0} / {(SupStation("Anchor pier B") - staB) / 0.3048:0.0} ft");

// Deck (CableStayedConcepts.TwoTower defaults, user 2026-10-27): the New NY (Tappan Zee) Bridge system with the redundancy truss replaced by ONE longitudinal
// support on the CL (user): steel edge girders at +-53'-6 (TZ sizes: web 5'-6", top flange 28 x 1 1/4, bottom flange 36 x 2 1/2), floorbeams every 15 ft
// from each tower (4'-8" deep, 5'-6" at the stay anchorages), longitudinal support between the floorbeams on the CL, 10 3/4" precast deck panels with
// cast-in-place closure pours over every member (edge girders, the longitudinal support, each floorbeam), 110 ft wide (4 x 12 ft lanes + 10 / 4 ft
// shoulders, median barrier, 12 ft path fit inside the edge barriers); stays every 45 ft at the floorbeams; "rocket launcher" anchorages bolted to the
// outside of the girder webs over the full girder depth (TZ Fig. 10: stem mounting plate, stem plate, anchor top / bottom flanges, anchor pipe past both
// flanges); field splices just past each anchorage (moved past the next floorbeam when they would meet it); TUNING FORK towers (hollow concrete legs outside the deck,
// concrete strut below the deck only, steel anchorage trays cast in the upper legs, New NY Fig. 8). The approach spans (outside the unit) stay an envelope of the same section: not designed.
double deckTopA = al.Profile.ElevationAt(staA), deckTopB = al.Profile.ElevationAt(staB);
var cs = CableStayedConcepts.TwoTower("Tower A", staA, baseA, deckTopA - baseA, "Tower B", staB, baseB, deckTopB - baseB,
    backReachA: staA - SupStation("Anchor pier A") - Ft(10), backReachB: SupStation("Anchor pier B") - staB - Ft(10),
    unitFrom: SupStation("Anchor pier A"), unitTo: SupStation("Anchor pier B"));
cs.Concept = "Blatnik Bridge replacement - two-tower cable-stayed alternative on the existing I-535 line (starting proportions)";
cs.Unresolved.Add("UNRESOLVED (site): alignment = the existing I-535 line from OpenStreetMap, not a survey or the project's design line; the replacement's own alignment is not public.");
cs.Unresolved.Add("UNRESOLVED (site): profile = concept (VPIs solved for the charted clearances, deck 20 ft above the ground at the south abutment, -4.0 % toward Duluth, curve lengths 400 / 900 ft); the approach tie-ins (Hammond Ave, Garfield Ave) are not designed.");
cs.Unresolved.Add("UNRESOLVED (navigation): clearance reference = NOAA ENC charted VERCLR above Lake Superior Low Water Datum 601.1 ft (IGLD 85); the IGLD 85 - NAVD88 difference at Duluth is not applied (3DEP water 601.96 NAVD88). USCG bridge permit values not checked.");
cs.Unresolved.Add("UNRESOLVED (deck): cross-section widths from the MnDOT bulletin (4 x 12 ft lanes, full shoulders, 12 ft path); shoulder widths 10 / 4 ft (AASHTO Interstate) and barriers assumed - the project's section is not public.");
cs.Unresolved.Add("UNRESOLVED (deck): member sizes = the New NY (Tappan Zee) Bridge published values (Scollard et al., TAC) for a 1,200 ft main span, not designed for 720 ft; floorbeam flanges, the longitudinal support, panel seats (2 in), splice and anchorage plate sizes are concept values; floorbeam / stringer connections, bolts, shear studs, deck reinforcing and PT are not modeled.");
cs.Unresolved.Add("UNRESOLVED (towers): hollow concrete tuning fork legs 14 x 20 -> 10 x 16 ft, 2 ft walls, concrete strut 12 x 10 ft below the deck, anchorage trays 3'-6 x 5'-6 at 6 ft levels (New NY Fig. 8 arrangement, no dimensions published) - concept values; tray studs, spacer plates, wall / end plate openings and the tower reinforcing are not modeled.");
cs.Unresolved.Add("UNRESOLVED (anchorage): rocket launcher geometry read from the New NY Bridge isometric (Fig. 10, no dimensions): pipe 5 ft above the top of steel to 2 ft below the bottom flange, axis 2'-8 outboard of the web - concept values.");
cs.Unresolved.Add("UNRESOLVED (approaches): the approach spans outside the cable-stayed unit are an envelope of the edge girder section - their structure (girder type for 230 - 485 ft spans) is not designed.");
cs.Unresolved.Add("UNRESOLVED (foundations): footing tops 3 ft below the 3DEP ground on land and at the water surface in the bay; no geotechnical data; pier protection (MnDOT: modern pier protection) not modeled.");
foreach (var s in sup.Where(s => s.Name.StartsWith("Pier") || s.Name.StartsWith("Anchor")))
    cs.Piers.BaseElevations[s.Name] = Ft(BaseFt(s.Ground));
cs.Analysis.Clearances.Add(new CsClearance("Main channel (West Gate Basin), NOAA ENC span", chanA, chanB, lwd, mainClr, "NOAA ENC VERCLR 37.4 m above LWD (existing bridge)"));
cs.Analysis.Clearances.Add(new CsClearance("Howards Bay channel, NOAA ENC span", howA, howB, lwd, howClr, "NOAA ENC VERCLR 31.3 m above LWD (existing bridge)"));
br.CableStayed = cs;
Doc.Project.Bridges.Add(br);

var r = Doc.Regenerate();
var m = Doc.Model(Name);
var csm = m.Steel?.CableStayed;
Print($"Regenerated: {r.Elapsed.TotalSeconds:0.0} s; {m.Steel?.Parts.Count ?? 0} steel parts, {csm?.Stays.Count ?? 0} stays, {m.Substructure?.Units.Count ?? 0} substructure units");
foreach (var d in csm?.Diagnostics ?? new()) Print($"  {d.Severity}: {d.Message}");
foreach (var s in new[] { "South abutment", "North abutment" })
{
    var x = sup.First(q => q.Name == s);
    Print($"{s}: grade {al.Profile.ElevationAt(SupStation(s)) / 0.3048:0.0} ft, ground {x.Ground:0.0} ft");
}
Doc.Save(outPath);
Print($"Saved {outPath}");
