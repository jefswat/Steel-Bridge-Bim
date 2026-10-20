// Northaven NHT: existing ground = USGS 3DEP 1 m lidar DEM (TX_Pecos_Dallas_2018_D19, tile x70y365, NAD83 UTM 14N m, NAVD88 m).
// The DEM is resampled by tools/terrain/terrain_northaven.py INTO THE MODEL'S OWN COORDINATES: plan X / Y numbers (TxDOT surface coordinates =
// EPSG:2276 ftUS grid x Dallas SAF 1.000136) in ft, cell = 1 m = 3.2808 ft, z = NAVD88 ft. Reference UnitScale 0.3048 (ft -> model metres).
// The model is not moved; the georeference (EPSG:2276 x SAF) is set again here so this script is complete on its own.
// Re-runnable: sbm run samples/plans/terrain-northaven.csx "samples/plans/Northaven NHT.sbm"
Doc.Project.Georeference ??= new SBM.Core.Project.GeoreferenceDefinition
{
    CrsName = "EPSG:2276", Description = "NAD83 / Texas North Central (ftUS); project X/Y = TxDOT surface coordinates (Dallas County SAF 1.000136), plan numbers used as ft",
    GeodeticDatum = "NAD83", MapProjection = "Lambert Conformal Conic", MapZone = "Texas North Central (4202)", MapUnitUsSurveyFoot = false, GridScale = 1 / 1.000136,
    Basis = "User GPS check 2026-10-11: 32.901380, -96.769012 -> EPSG:2276 2499728.33 / 7015245.07, x 1.000136 = 2500068.29 / 7016199.14 vs model arch centre 2500079.15 / 7016198.76",
};
Doc.Project.References.RemoveAll(x => x.Name == "3DEP lidar 1 m (Northaven)");
Doc.Project.References.Add(new SBM.Core.References.ReferenceModelDefinition
{
    Name = "3DEP lidar 1 m (Northaven)", Path = @"C:\SBM\dist\terrain\northaven\northaven-nht-3dep-1m-txnc-sft.flt",
    Kind = SBM.Core.References.ReferenceKind.Terrain, Format = SBM.Core.References.ReferenceFormat.Grid, UnitScale = 0.3048,
    ContourInterval = Ft(2), R = 150, G = 140, B = 120,
});
var rep = Doc.Regenerate();
foreach (var e in rep.Errors) Print("ERROR " + e);
foreach (var w in rep.Warnings.Take(30)) Print("warning " + w);
Doc.Save();
Print("terrain reference set, regenerated and saved");
