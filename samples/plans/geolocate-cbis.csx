// Moves the local-coordinate model into NAD83 / UTM zone 15N (meters), from the plan's location data:
// STA 7595+67.24, 41.08' LT CL I-80 (WB CL) = latitude 41.231824°, longitude -95.836127° (situation plan).
// UTM coordinates of that point (pyproj, EPSG:26915): E 262302.16, N 4568371.47. The plan north arrow is taken as true
// north; UTM grid north is rotated -1.870° from true north here (meridian convergence), checked against Esri imagery.
// Run once, after the build scripts (running it twice moves the model twice).
using SBM.Core.Alignments;
using SBM.Core.Geometry;

var target = new Vec2(262302.16, 4568371.47);
double rot = -1.870 * Math.PI / 180;      // local (plan north) -> UTM grid
var wb = Doc.Alignment("I-80 WB");
var anchor = wb.PointAt(Sta("7595+67.24"), 0);
Vec2 Move(Vec2 p) => target + (p - anchor).Rotate(rot);

for (int i = 0; i < Doc.Project.Alignments.Count; i++)
{
    var a = Doc.Project.Alignments[i];
    if (a.PiDefinition == null) { Print($"{a.Name}: no PI table, not moved"); continue; }
    var def = new PiDefinition
    {
        Pis = a.PiDefinition.Pis.Select(p => { var q = Move(p.Point); return new PiRow(q.Y, q.X, p.Radius, p.SpiralIn, p.SpiralOut); }).ToList(),
    };
    var moved = new Alignment(def.Build(out _))
    {
        Name = a.Name, Description = a.Description, StartStation = a.StartStation, StationEquations = a.StationEquations,
        Profile = a.Profile, CrossSlope = a.CrossSlope, PiDefinition = def,
    };
    Doc.Project.Alignments[i] = moved;
    Print($"{a.Name}: moved to UTM 15N (start E {moved.PointAt(moved.StartStation).X:F2}, N {moved.PointAt(moved.StartStation).Y:F2})");
}
var check = Doc.Alignment("I-80 WB").PointAt(Sta("7595+67.24"), 0);
Print($"Check: STA 7595+67.24 WB CL at E {check.X:F2}, N {check.Y:F2}");
// Existing ground: USGS 3DEP 1 m lidar DEM + Esri World Imagery (fetched to C:\SBMData\CouncilBluffs_I80, UTM 15N m, NAVD88 m).
Doc.Project.References.RemoveAll(x => x.Name == "3DEP lidar 1 m");
Doc.Project.References.Add(new SBM.Core.References.ReferenceModelDefinition
{
    Name = "3DEP lidar 1 m", Path = @"C:\SBMData\CouncilBluffs_I80\dem.flt", Kind = SBM.Core.References.ReferenceKind.Terrain,
    Format = SBM.Core.References.ReferenceFormat.Grid, UnitScale = 1.0, ImagePath = @"C:\SBMData\CouncilBluffs_I80\ortho.jpg",
    ContourInterval = Ft(2), R = 150, G = 140, B = 120,
});
var r = Doc.Regenerate();
Print($"Regenerated {r.Bridges} bridges in {r.Elapsed.TotalSeconds:F1} s, {r.Errors.Count} errors.");
Doc.Save();
