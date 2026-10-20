// ODOT Bridge O tub unit (Ramp 44F, I-235 / I-44, Oklahoma County, job 09033(11)): existing ground = USGS 3DEP 1 m lidar DEM
// (OK_Panhandle_2018_D18, tile x63y394, NAD83 UTM 14N m, NAVD88 m; 2018 lidar = AFTER the Phase 2B construction).
// Model coordinates: plan E / N numbers (p.148, p.254-259) used as ft, X = E, Y = N. The plans do NOT state the datum (UNRESOLVED U-GEO): the
// numbers (E 2.11e6 / N 1.93e5 at Oklahoma County) fit NAD83 Oklahoma North (EPSG:2267, ftUS, central meridian 98 W, FE 1,968,500) and place the
// ramp at 35.5304 N 97.5124 W = the I-44 / I-235 / US-77 interchange, so that is set as the georeference (grid scale 1, no surface factor is stated).
// tools/terrain/terrain_clip.py resamples the DEM INTO THOSE NUMBERS (ft, cell = 1 m = 3.2808 ft, z NAVD88 ft); UnitScale 0.3048 (ft -> model m).
// The model is not moved. Re-runnable: sbm run samples/plans/terrain-odot-o-tub.csx "samples/plans/ODOT Bridge O tub unit.sbm"
Doc.Project.Georeference = new SBM.Core.Project.GeoreferenceDefinition
{
    CrsName = "EPSG:2267", Description = "NAD83 / Oklahoma North (ftUS); project X = Easting, Y = Northing as printed on the plans (used as ft). UNRESOLVED: datum / zone not printed",
    GeodeticDatum = "NAD83", MapProjection = "Lambert Conformal Conic", MapZone = "Oklahoma North (3501)", MapUnitUsSurveyFoot = false, GridScale = 1,
    Basis = "Inferred, not stated on the plans: E 2113643.09 N 193275.14 (Pier 14 working point, p.259) -> lat 35.5304 lon -97.5124 under EPSG:2267 = the I-44 / I-235 / US-77 interchange, Oklahoma City",
};
Doc.Project.References.RemoveAll(x => x.Name == "3DEP lidar 1 m (I-44 / I-235 Oklahoma City)");
Doc.Project.References.Add(new SBM.Core.References.ReferenceModelDefinition
{
    Name = "3DEP lidar 1 m (I-44 / I-235 Oklahoma City)", Path = @"C:\SBM\dist\terrain\odot-o\odot-o-ramp44f-3dep-1m-okn-sft.flt",
    Kind = SBM.Core.References.ReferenceKind.Terrain, Format = SBM.Core.References.ReferenceFormat.Grid, UnitScale = 0.3048,
    ContourInterval = Ft(2), R = 150, G = 140, B = 120,
});
var rep = Doc.Regenerate();
foreach (var e in rep.Errors) Print("ERROR " + e);
foreach (var w in rep.Warnings.Take(10)) Print("warning " + w);
Doc.Save();
Print("terrain reference set, regenerated and saved");
