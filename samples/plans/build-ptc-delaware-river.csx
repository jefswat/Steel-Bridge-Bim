// FINISH (required, see CLAUDE.md "New plan-set model checklist" / docs/MODEL-PIPELINE.md): `sbm run <this script> <model.sbm>` runs `sbm pipeline` after the
// script saves = regenerate, terrain (georeferenced projects), plan sheet index, companion database + sheet links; UNRESOLVED items are printed.
// PTC / NJTA DELAWARE RIVER BRIDGE (Bucks Co. PA - Burlington Co. NJ; ORIGINAL 1954-57 bridge, I-95 / former I-276), phase 1a = LOCATION ONLY:
// alignment + vertical profile + the support lines of all 31 spans, in SEVEN bridges (one per structure unit as the plans group them), each
// `SupportLinesOnly` (no steel, concrete or substructure is generated: the program cannot build riveted trusses / floor-beam girders yet).
// From scratch (re-runnable), run from the repo root:
//   sbm new  "samples\plans\PTC Delaware River Bridge.sbm"
//   sbm run  "samples\plans\build-ptc-delaware-river.csx" "samples\plans\PTC Delaware River Bridge.sbm"
//   sbm run  "samples\plans\check-ptc-delaware-river.csx" "samples\plans\PTC Delaware River Bridge.sbm"
// Data: docs/plan-data/ptc-delaware-river/{supports.csv (read by this script), LOCATION.md, INDEX.md, SUMMARY.md}. PDF "C:\SBMData\Original Existing
// Bridge Plans from PTC.pdf" (108 raster pages), "p." = PDF page. Rules (user): model the 1954-57 AS-BUILT set; nothing invented; UNRESOLVED items name
// the page and a plain reason; differences reported, never fixed.
//
// LOCAL GRID (feet, east / north): origin (0, 0) = the CL bearing of the WEST ABUTMENT, Sta 646+21.463. The baseline is ONE tangent of bearing S 69 54'15" E
// (p.5 / 16 / 32 / 42; azimuth 110 05'45" if the plan bearing is taken from north), so north is up and the line runs down-right.
// GEOREFERENCE: none (null). The plans give no coordinates; the fit to current aerial / LiDAR in NAD83 PA South (EPSG:2272, ftUS) + NAVD88 comes later
// and waits for the user's download OK. Until then X / Y are NOT map coordinates and elevations are NGVD29 as printed.
// STATIONING: PTC stationing, feet as printed (assumed international feet in the model: the plans do not say survey / international - UNRESOLVED).
// PROFILE: the printed profile is the GUTTER LINE ("Gutter Line = Grade Profile", p.107). Cross section p.107: crown at the CL, 1/8" per ft each side, 38'-3" CL to
// curb, so the CL is 38.25 / 96 = 0.3984 ft above the gutter (p.43 prints 0.400). The model profile is on the CL = printed + that rise, with a symmetric crown
// slope of -1/96; then the gutter line of the model = the printed profile.
using System.IO;
using System.Globalization;
using SBM.Core;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Geometry;

string outPath = Args.Length > 0 ? Args[0] : (Doc.FilePath ?? @"samples\plans\PTC Delaware River Bridge.sbm");
var inv = CultureInfo.InvariantCulture;
var unresolved = new List<string>();
void U(string pages, string item, string why, string then = "") => unresolved.Add(UnresolvedFlag.Text(pages, item, why, then));

string dataDir = null;
for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
{
    var p = Path.Combine(dir.FullName, "docs", "plan-data", "ptc-delaware-river");
    if (Directory.Exists(p)) { dataDir = p; break; }
}
if (dataDir == null) throw new InvalidOperationException("Run from the repo: docs/plan-data/ptc-delaware-river not found above the current directory.");
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

// ================================================================ 1. alignment (p.5 / 16 / 32 / 42)
const string AlName = "CL Roadway (PTC)";
const double CrownRiseFt = 38.25 / 96.0;                  // p.107: 38'-3 CL to curb x 1/8" per ft
Doc.Project.Alignments.RemoveAll(a => a.Name == AlName);
double bearingRad = (69 + 54 / 60.0 + 15 / 3600.0) * Math.PI / 180;
double heading = -(Math.PI / 2 - bearingRad);              // S 69 54'15" E as a math angle (east = 0, counter-clockwise), plan (true) bearing
// GEOREFERENCE (docs/plan-data/ptc-delaware-river/GEOREFERENCE.md, user decision: fit to current aerial / LiDAR in PA South EPSG:2272 + NAVD88):
// the plan bearing is TRUE north (fitted true azimuth 110.055 deg vs plan 110.096); in EPSG:2272 GRID the axis runs at 108.162 deg, and the
// W abutment CL brg (646+21.463) sits at E 2,781,357.274 / N 300,154.409 ftUS (APPROXIMATE: +/-10 m along, +/-5 m across, azimuth +/-0.15 deg;
// fit to the approach-fill ends + the LiDAR footprint of the land spans, no control). Model X / Y = EPSG:2272 grid coordinates in metres.
const bool Georeferenced = true;
if (Georeferenced) heading = (90 - 108.162) * Math.PI / 180;
double staWest = Sta("646+21.463");
double staStart = Sta("638+00.00"), staEnd = Sta("713+30.00");   // profile start (before the west vertical curve) / R/W end (p.31, p.32)
var origin = Georeferenced ? new Vec2(2781357.274 * 1200 / 3937.0, 300154.409 * 1200 / 3937.0) : new Vec2(0, 0);
var startPt = origin - Vec2.FromAngle(heading) * (staWest - staStart);
var al = new Alignment(new List<HorizontalElement> { new HorizontalElement(startPt, heading, staEnd - staStart, 0, 0) })
{
    Name = AlName,
    StartStation = staStart,
    Description = "PTC / NJTA Delaware River Bridge baseline S 69 54'15\" E (p.5, 16, 32, 42); " + (Georeferenced ? "EPSG:2272 grid (m), fitted to 3DEP / NAIP (approximate), CL brg W abutment 646+21.463" : "LOCAL grid, origin = CL brg W abutment 646+21.463; not georeferenced"),
};
Doc.Project.Alignments.Add(al);
U("p.5, 16, 32, 42", "reference meridian of the bearing S 69 54'15\" E", "the plans do not say whether it is true, grid or magnetic (the north arrows only show the direction)",
  "North = local +Y; the azimuth 110 05'45\" is used as printed, no convergence applied.");
U("p.5, 16, 32, 42", "map coordinates of the baseline", "the plans give no coordinates, state plane or benchmarks",
  "Fitted to current 3DEP LiDAR + NAIP in PA South EPSG:2272 (GEOREFERENCE.md): approximate +/-10 m along, +/-5 m across, azimuth +/-0.15 deg; R/W monument coordinates from the Turnpike would close it.");
Doc.Project.Georeference = !Georeferenced ? null : new SBM.Core.Project.GeoreferenceDefinition
{
    CrsName = "EPSG:2272", Description = "NAD83 / Pennsylvania South (ftUS); model X / Y = grid coordinates in metres; elevations NGVD29 as printed (NAVD88 = NGVD29 - 1.020 ft, VERTCON 3.0)",
    GeodeticDatum = "NAD83", MapProjection = "Lambert Conformal Conic", MapZone = "Pennsylvania South (3702)", MapUnitUsSurveyFoot = true, GridScale = 0.999976,
    Basis = "Fit 2026-10-19 to USGS 3DEP 1 m + NAIP 2023 (approach-fill ends + land-span LiDAR footprint); plan bearing = true north; no survey control",
};
Doc.Project.References.RemoveAll(x => x.Name.StartsWith("3DEP lidar 1 m (Delaware River"));
if (Georeferenced) Doc.Project.References.Add(new SBM.Core.References.ReferenceModelDefinition
{
    Name = "3DEP lidar 1 m (Delaware River, PA South)", Path = @"C:\SBM\dist\terrain\ptc\ptc-delaware-3dep-1m-pa-south-ftus-ngvd29.flt",
    Kind = SBM.Core.References.ReferenceKind.Terrain, Format = SBM.Core.References.ReferenceFormat.Grid, UnitScale = 0.3048006096,
    ContourInterval = Ft(2), R = 150, G = 140, B = 120,
});
U("p.1, 5, 42", "unit of the printed feet (US survey vs international)", "the plans just say feet", "International feet (0.3048 m), like the other models.");
U("p.5, 42", "entry grade of the west vertical curve", "only the VPI 644+67.14 El 49.454, L = 1060 and M.O. 4.637 are printed; the -0.5 % grade is derived",
  "First VPI at 638+00 on the derived -0.50 % grade (El 53.790, derived).");
U("p.1, 32, 42", "profile beyond the east end of the structure", "the plans print -3.00 % to the abutment and the gutter El 76.478 at the east abutment CL brg (p.43), no east VPI",
  "Last VPI at 713+30.00 (R/W end, p.31) on -3.00 % (El 72.361, derived).");

double Gut(double elev) => Ft(elev + CrownRiseFt);
al.Profile = new VerticalProfile
{
    Vpis =
    {
        new Vpi(staStart, Gut(49.454 + 0.005 * (64467.14 - 63800.0))),
        new Vpi(Sta("644+67.14"), Gut(49.454), Ft(1060.0)),                     // p.5, p.42, p.43: VPI 644+67.140 El 49.454, L 1060, M.O. 4.637
        new Vpi(Sta("682+80.38"), Gut(163.850), Ft(1364.0)),                    // p.1, p.42, p.69: P.V.I. 682+80.380 El 163.850, V.C. 1364.000, M 10.230 (centre of bridge)
        new Vpi(staEnd, Gut(163.850 - 0.03 * (71330.0 - 68280.38))),
    },
};
al.CrossSlope = new CrossSlopeTable { PivotOffset = 0, Rows = { new CrossSlopeRow(staStart, -1.0 / 96, -1.0 / 96) } };   // p.107 slope 1/8" per ft each side of the CL

// ================================================================ 2. the seven bridges (supports.csv)
var rows = Csv("supports.csv").Where(r => r[6] != "").ToList();
var units = rows.Select(r => r[6]).Distinct().ToList();
foreach (var unit in units)
{
    Doc.Project.Bridges.RemoveAll(b => b.Name == unit);
    var br = new BridgeDefinition { Name = unit, AlignmentName = AlName, Structure = $"Delaware River Bridge (PTC / NJTA, 1954-57), {unit.Replace("PTC ", "")}" };
    br.FieldSplices.Clear();
    br.Girders.Count = 0;
    br.Girders.Spacings = new();
    br.SupportLinesOnly = true;
    br.Deck.Barrier.Left = false; br.Deck.Barrier.Right = false;
    br.Deck.Reinforcement.Enabled = false;
    br.Deck.LeftEdgeOffset = -(Ft(41) + In(1)); br.Deck.RightEdgeOffset = Ft(41) + In(1);   // only the DRAWN length of the support lines: abutment width 82'-2" overall (p.11, p.38); no deck is built
    foreach (var r in rows.Where(r => r[6] == unit).OrderBy(r => Sta(r[1])))
        br.Supports.Add(new SupportDefinition
        {
            Name = r[0], Type = r[2] == "Abutment" ? SupportType.Abutment : SupportType.Pier, Station = Sta(r[1]), SkewDegrees = 0,
            Substructure = new SubstructureDefinition { Enabled = false },
        });
    Doc.Project.Bridges.Add(br);
    Print($"{unit}: {br.Supports.Count} supports, {br.Supports[0].Name} {br.Supports[0].Station / 0.3048:0.000} -> {br.Supports[^1].Name} {br.Supports[^1].Station / 0.3048:0.000} ft");
}

// ================================================================ 3. regenerate + save
{ var sr = await SBM.Api.ScriptHost.RunFileAsync(@"samples\plans\substructure-ptc.csx", Doc, new[] { "--nosave" }); Print(sr.Output.TrimEnd()); if (!sr.Success) Print("ERROR in substructure-ptc.csx: " + sr.Error); }   // phase 4: piers + abutments
var rep = Doc.Regenerate();
Print($"Regenerated {rep.Bridges} bridge(s) in {rep.Elapsed.TotalSeconds:0.0} s; {rep.Warnings.Count} warnings, {rep.Errors.Count} errors");
foreach (var w in rep.Warnings.Take(10)) Print("  W: " + w);
foreach (var e in rep.Errors) Print("  E: " + e);
foreach (var u in unresolved) Print("  " + u);
// Plan set (CLAUDE.md checklist step 1; fingerprinted by `sbm review`) = plan set "ptc-drb" in sheet-links-ptc-delaware-river.json.
// "PTC main truss 18-20.sbm" / "PTC approach trusses.sbm" are copies of this model, so they carry it too.
{ const string pdf = @"C:\SBMData\Original Existing Bridge Plans from PTC.pdf";
  if (!Doc.Project.PlanPdfs.Contains(pdf, StringComparer.OrdinalIgnoreCase)) Doc.Project.PlanPdfs.Add(pdf); }
Doc.Save(outPath);
Print($"Saved {outPath}");
