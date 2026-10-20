// Br. 02069 (T.H. 65 SB over 117th Ave NE, Blaine, Anoka County, MnDOT S.P. 0208-02069): existing ground = USGS 3DEP 1 m lidar DEM
// (MN_CentralMissRiver_B22, tile x48y501, NAD83 UTM 15N m, NAVD88 m; 2022-24 lidar = BEFORE the interchange construction, existing at-grade ground).
// Model coordinates: plan X / Y (p.34 plat: PT SB65 X 508139.723 Y 149812.027, bridge survey control X 508192 Y 153852 at STA 248+67) used as ft.
// The plans do not name the system (U1) but the numbers are the ANOKA COUNTY COORDINATE SYSTEM (NAD83 HARN Adj MN Anoka, ESRI:103708: LCC 2SP
// 45 04' / 45 22', origin 45 02'07" N 93 16' W, FE 500,000 / FN 100,000 ftUS on the county-adjusted ellipsoid a 6378418.941): they put the SB65 / EB117
// control point at 45.18305 N 93.2349 W and the 1 m DEM relief shows T.H. 65 and the 117th Ave crossing there (within ~40 ft of the p.34 control point).
// tools/terrain/terrain_clip.py resamples the DEM INTO THOSE NUMBERS (ft, cell = 1 m = 3.2808 ft, z NAVD88 ft; plan datum N.A.V.D. 88 ADJ., p.34).
// The model is not moved. Re-runnable: sbm run samples/plans/terrain-br02069.csx "samples/plans/Br02069 TH65.sbm"
Doc.Project.Georeference = new SBM.Core.Project.GeoreferenceDefinition
{
    CrsName = "ESRI:103708", Description = "NAD83(HARN) Adj MN Anoka (ftUS) county coordinate system; project X / Y = plan X / Y used as ft. UNRESOLVED: system not named on the plans",
    GeodeticDatum = "NAD83(HARN)", MapProjection = "Lambert Conformal Conic", MapZone = "Anoka County, Minnesota", MapUnitUsSurveyFoot = false, GridScale = 1,
    Basis = "Inferred (plans give X / Y only, p.34): SB65 STA 248+67.045 X 508192.059 Y 153852.460 = 45.18305 N 93.2349 W under ESRI:103708; confirmed on the 3DEP relief (T.H. 65 x 117th Ave intersection within ~40 ft)",
};
Doc.Project.References.RemoveAll(x => x.Name == "3DEP lidar 1 m (T.H. 65 Blaine)");
Doc.Project.References.Add(new SBM.Core.References.ReferenceModelDefinition
{
    Name = "3DEP lidar 1 m (T.H. 65 Blaine)", Path = @"C:\SBM\dist\terrain\br02069\br02069-th65-3dep-1m-anoka-sft.flt",
    Kind = SBM.Core.References.ReferenceKind.Terrain, Format = SBM.Core.References.ReferenceFormat.Grid, UnitScale = 0.3048,
    ContourInterval = Ft(2), R = 150, G = 140, B = 120,
});
var rep = Doc.Regenerate();
foreach (var e in rep.Errors) Print("ERROR " + e);
foreach (var w in rep.Warnings.Take(10)) Print("warning " + w);
Doc.Save();
Print("terrain reference set, regenerated and saved");
