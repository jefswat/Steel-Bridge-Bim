// FINISH (required, see CLAUDE.md "New plan-set model checklist" / docs/MODEL-PIPELINE.md): `sbm run <this script> <model.sbm>` runs `sbm pipeline` after the
// script saves = regenerate, terrain (georeferenced: 3DEP for the model extents), companion database; UNRESOLVED items are printed.
// RICHMOND GREENWAY BRIDGE (concept): pedestrian / bicycle cable-stayed bridge closing the Greenway gap at 23rd Street, the UP Martinez Subdivision
// tracks and Carlson Boulevard, Richmond CA. NO DRAWINGS EXIST: Donald MacDonald Architects concept (renderings Dec 2022; Caltrans planning grant
// June 2026 for the 30 % design). This model is the STARTING SHAPE of a parametric study, not a design:
//   - site from OpenStreetMap (Greenway trail ends, 23rd St median, rail, Carlson) and USGS 3DEP point elevations (EPQS), sampled 2026-10-26;
//   - structure = CableStayedConcepts.Osprey (values read from the renderings, all flagged), every value editable here or in the app;
//   - clearances: UP rail 23'-4" (UP guideline for new overhead structures - verify with UP / CPUC GO 26-D), roads 17'-0 (Caltrans new structures).
//   sbm new "samples\plans\Richmond Greenway Bridge.sbm"
//   sbm run samples\plans\build-richmond-greenway.csx "samples\plans\Richmond Greenway Bridge.sbm" -- "samples\plans\Richmond Greenway Bridge.sbm"
//   sbm cable-stayed "samples\plans\Richmond Greenway Bridge.sbm"        (stays, pylon balance, clearances, analysis wireframe)
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Geometry;

string outPath = Args.Length > 0 ? Args[0] : (Doc.FilePath ?? @"samples\plans\Richmond Greenway Bridge.sbm");
Func<string, double> S = Sta;

// ================================================================ site (NAD83 / UTM 10N, metres)
// OSM: Greenway west segment ends 37.931512 / -122.348134, east segment resumes 37.931429 / -122.346402 (152.5 m apart, azimuth 93.06 deg);
// 23rd St divided, median at -122.34792 (62.3 ft east of the west trail end). UTM 10N by tools/terrain (geotiff.utm(10)).
var west = new Vec2(557285.584, 4198416.505);
var east = new Vec2(557437.857, 4198408.362);
var dir = (east - west).Normalized();
double staWest = S("10+00");                       // stationing: 10+00 at the west trail end
const string AlName = "CL Richmond Greenway Bridge (concept)";
Doc.Project.Alignments.RemoveAll(a => a.Name == AlName);
var p0 = west - dir * Ft(800);
var p1 = east + dir * Ft(800);
var al = Doc.AddAlignment(AlName, staWest - Ft(800), new[] { new PiRow(p0.Y, p0.X), new PiRow(p1.Y, p1.X) });
al.Description = "Richmond Greenway gap 23rd St / UP tracks / Carlson Blvd: straight line through the two OSM trail ends (concept; the renderings show curved ramps - set PIs here)";
Doc.Project.Georeference = new SBM.Core.Project.GeoreferenceDefinition
{
    CrsName = "EPSG:26910", Description = "NAD83 / UTM zone 10N (m); project X/Y = UTM metres", GeodeticDatum = "NAD83", MapProjection = "Transverse Mercator",
    MapZone = "UTM 10N", MapUnitUsSurveyFoot = false, GridScale = 1,
    Basis = "OpenStreetMap Greenway trail ends (2026-10-26) projected with tools/terrain geotiff.utm(10); elevations NAVD88 ft from USGS 3DEP (EPQS point queries)",
};

// Ground along the line (USGS EPQS, NAVD88 ft) x = ft from the west trail end: -77 41.9 | 6 41.9 | 34 24.3 (23rd St cut) | 61 25.6 | 89 26.2 | 116 26.2 |
// 144 33.2 | 171 42.7 (UP tracks) | 199 43.1 | 227 41.4 | 254 40.6 (Carlson) | 282 41.1 | 309 41.5 | 337 40.7 | 364 43.3 | 392 44.7 | 419 44.6 | 447 44.3 |
// 475 44.0 | 502 44.7 (east trail end) | 530 45.8 | 557 46.2; 260 m west 33.1, 260 m east 57.6.
// Profile: +/-5 % (accessible route without landings), crest PVI 11+85 el 72.5, VC 200 ft -> rail clearance 23'-4" with a 3'-0 deck; the deck meets the
// ground at about 4+71 and 16+76 (interpolated ground) -> abutments at 4+75 / 16+75. CHECK against the 3DEP terrain the pipeline downloads.
double g = 0.05, pviSta = S("11+85"), pviEl = Ft(72.5);
al.Profile = new VerticalProfile
{
    Vpis =
    {
        new Vpi(al.StartStation, pviEl - g * (pviSta - al.StartStation)),
        new Vpi(pviSta, pviEl, Ft(200)),
        new Vpi(al.EndStation, pviEl - g * (al.EndStation - pviSta)),
    },
};

// ================================================================ bridge
const string Name = "Richmond Greenway Bridge";
Doc.Project.Bridges.RemoveAll(b => b.Name == Name);
var br = new BridgeDefinition { Name = Name, AlignmentName = al.Name, Structure = "Richmond Greenway Bridge (concept)" };
br.FieldSplices.Clear();
br.Girders.Count = 0;
br.LateralBracing.Enabled = false;
SupportDefinition Sup(string n, SupportType t, string sta) => new() { Name = n, Type = t, Station = S(sta), SkewDegrees = 0 };
// pylon in the 23rd St median (62.3 ft east of the west trail end); main span east over the UP tracks and Carlson; approach piers about 130 ft apart.
double pylonSta = staWest + Ft(62.3);
br.Supports.Add(Sup("West abutment", SupportType.Abutment, "4+75"));
br.Supports.Add(Sup("Pier W3", SupportType.Pier, "6+05"));
br.Supports.Add(Sup("Pier W2", SupportType.Pier, "7+35"));
br.Supports.Add(Sup("Pier W1", SupportType.Pier, "8+62"));
br.Supports.Add(new SupportDefinition { Name = "Pylon", Type = SupportType.Pier, Station = pylonSta, SkewDegrees = 0 });
br.Supports.Add(Sup("Pier E1", SupportType.Pier, "14+62"));
br.Supports.Add(Sup("Pier E2", SupportType.Pier, "15+70"));
br.Supports.Add(Sup("East abutment", SupportType.Abutment, "16+75"));
foreach (var s in br.Supports) s.Substructure = SubstructureDefinition.Default(s.Type).Clone();
foreach (var s in br.Supports) s.Substructure!.Enabled = false;   // the cable-stayed generator builds its own piers

double groundAtPylon = Ft(26.0);                    // 3DEP at the 23rd St median
double deckTopAtPylon = al.Profile.ElevationAt(pylonSta);
var cs = CableStayedConcepts.Osprey("Pylon", pylonSta, groundAtPylon, deckTopAtPylon - groundAtPylon, height: Ft(180));
cs.Concept = "Richmond Greenway Bridge - osprey concept, starting shape for the shape study";
cs.Unresolved.Add("UNRESOLVED (site): alignment, profile, pier stations and ramp lengths are a concept reading (straight line through the OSM trail ends, 5 % grades, 3DEP point ground) - the renderings show curved approach ramps.");
cs.Unresolved.Add("UNRESOLVED (site): clearance envelopes (UP 23'-4\" / roads 17'-0) and the ground at the piers are from public data, not a survey.");
// pier column bases: 3 ft below the interpolated 3DEP ground
double Ground(double xFt) => xFt < -77 ? 41.9 + 0.0113 * (xFt + 77) : xFt > 557 ? 46.2 + 0.01466 * (xFt - 557) : 44.0;
foreach (var (n, sta) in new[] { ("Pier W3", "6+05"), ("Pier W2", "7+35"), ("Pier W1", "8+62"), ("Pier E1", "14+62"), ("Pier E2", "15+70") })
    cs.Piers.BaseElevations[n] = Ft(Ground((S(sta) - staWest) / 0.3048) - 3.0);
cs.Analysis.Clearances.Add(new CsClearance("UP Martinez Subdivision (top of rail)", S("11+55"), S("12+05"), Ft(43.1), Ft(23 + 4 / 12.0), "UP guideline for new overhead structures (verify; CPUC GO 26-D 22'-6\")"));
cs.Analysis.Clearances.Add(new CsClearance("23rd Street (roadway in cut)", S("10+30"), S("11+40"), Ft(26.2), Ft(17), "Caltrans HDM new structures"));
cs.Analysis.Clearances.Add(new CsClearance("Carlson Boulevard", S("12+30"), S("13+40"), Ft(41.5), Ft(17), "Caltrans HDM new structures"));
br.CableStayed = cs;
Doc.Project.Bridges.Add(br);

var r = Doc.Regenerate();
var m = Doc.Model(Name);
var csm = m.Steel?.CableStayed;
Print($"Regenerated: {r.Elapsed.TotalSeconds:0.0} s; {m.Steel?.Parts.Count ?? 0} steel parts, {csm?.Stays.Count ?? 0} stays, {m.Substructure?.Units.Count ?? 0} substructure units");
foreach (var d in csm?.Diagnostics ?? new()) Print($"  {d.Severity}: {d.Message}");
Doc.Save(outPath);
Print($"Saved {outPath}");
