// NCDOT B-2500 Bonner / Marc Basnight Bridge: georeference + topobathymetric terrain for a Bonner model (re-runnable; works on whichever model it is run on):
//   sbm run "samples\plans\terrain-bonner.csx" "samples\plans\Bonner Unit 5 segmental.sbm"
//   sbm run "samples\plans\terrain-bonner.csx" "samples\plans\Bonner Unit 6 FIB.sbm"
// Needs the models built with the NC State Plane placement (build-bonner-unit5.csx / build-bonner-unit6.csx: PC 2 at its localized coordinates) and the raster made by
// temp/bonner-geo/make_terrain.py (NCEI 1/9 arc-second topobathy 2019v2, NAVD88; see docs/plan-data/bonner-b2500/TERRAIN.md). The terrain file is NOT in git.
// Plan datum statement (S0-34 / S0-35, p.35 / p.36): "LOCALIZED PROJECT COORDINATES ARE BASED ON THE NAD83/NSRS 2007 (CORS96) STATE PLANE COORDINATES FOR B2500-2
// - N 751499.622 E 3031964.117 (US FT) - USING AN AVERAGE COMBINED GRID FACTOR (LOCALIZED TO GRID) OF 0.99991846. VERTICAL DATUM IS NAVD88."
using SBM.Core.Project;
using SBM.Core.References;

const double UsFt = 1200.0 / 3937.0;
const string TerrainName = "NCEI 1/9 arc-second topobathy 2019v2 (Oregon Inlet)";
const string TerrainFile = @"C:\SBM\dist\terrain\bonner\bonner-oregon-inlet-ncei-ninth-3m-ncsp.flt";

Doc.Project.Georeference = new GeoreferenceDefinition
{
    CrsName = "EPSG:2264",
    Description = "NAD83 / North Carolina (ftUS), NCDOT localized project coordinates (NSRS 2007 CORS96); vertical NAVD88",
    GeodeticDatum = "NAD83",
    MapProjection = "Lambert Conformal Conic",
    MapZone = "North Carolina",
    MapUnitUsSurveyFoot = true,
    GridScale = 0.99991846,                                   // average combined grid factor (grid / ground), plan p.35 / p.36
    ScaleCenterX = 3031964.117 * UsFt, ScaleCenterY = 751499.622 * UsFt,   // localized about the control point B2500-2 (-L- STA 26+47.55, 23.82' RT)
    Basis = "B-2500 structure plans S0-34 / S0-35 (p.35 / p.36): monument B2500-2 N 751499.622 / E 3031964.117 US ft, combined grid factor 0.99991846, NAVD88; "
          + "-L- placed from that monument along curve 1 (docs/plan-data/bonner-b2500/L-coordinates.csv). Model coordinates = US survey ft x 1200 / 3937 (m).",
};

Doc.Project.References.RemoveAll(x => x.Name == TerrainName);
Doc.Project.References.Add(new ReferenceModelDefinition
{
    Name = TerrainName, Path = TerrainFile,
    Kind = ReferenceKind.Terrain, Format = ReferenceFormat.Grid, UnitScale = 1.0,
    ContourInterval = Ft(2), R = 120, G = 135, B = 150,
});

var rep = Doc.Regenerate();
foreach (var e in rep.Errors) Print("ERROR " + e);
Print($"Regenerated in {rep.Elapsed.TotalSeconds:F1} s; georeference {Doc.Project.Georeference.CrsName}, scale {Doc.Project.Georeference.GridScale}, terrain {TerrainName}.");
foreach (var br in Doc.Project.Bridges)
    foreach (var s in br.Supports.Take(40))
        if (Doc.GroundAt(Doc.Alignment(br.AlignmentName).PointAt(s.Station, 0)) is double z)
            Print($"  {br.Name} {s.Name} STA {Doc.Units.FormatStation(s.Station)}: ground / water bottom {z / 0.3048:F1} ft NAVD88");
Doc.Save();
