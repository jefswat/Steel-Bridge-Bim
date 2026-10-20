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
double heading = -(Math.PI / 2 - bearingRad);              // S 69 54'15" E as a math angle (east = 0, counter-clockwise)
double staWest = Sta("646+21.463");
double staStart = Sta("638+00.00"), staEnd = Sta("713+30.00");   // profile start (before the west vertical curve) / R/W end (p.31, p.32)
var origin = new Vec2(0, 0);
var startPt = origin - Vec2.FromAngle(heading) * (staWest - staStart);
var al = new Alignment(new List<HorizontalElement> { new HorizontalElement(startPt, heading, staEnd - staStart, 0, 0) })
{
    Name = AlName,
    StartStation = staStart,
    Description = "PTC / NJTA Delaware River Bridge baseline S 69 54'15\" E (p.5, 16, 32, 42); LOCAL grid, origin = CL brg W abutment 646+21.463; not georeferenced",
};
Doc.Project.Alignments.Add(al);
U("p.5, 16, 32, 42", "reference meridian of the bearing S 69 54'15\" E", "the plans do not say whether it is true, grid or magnetic (the north arrows only show the direction)",
  "North = local +Y; the azimuth 110 05'45\" is used as printed, no convergence applied.");
U("p.5, 16, 32, 42", "map coordinates / CRS of the baseline", "the plans give no coordinates, state plane or benchmarks",
  "Local grid, Project.Georeference = null. Fit to aerial / LiDAR (PA South EPSG:2272 + NAVD88) is a later step and waits for the user's download OK.");
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
Doc.Save(outPath);
Print($"Saved {outPath}");
