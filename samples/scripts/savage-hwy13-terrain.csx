// Terrain stress test: Hwy 13 over the north-south road at the Hwy 13 / 101 interchange, Savage MN (UTM 15N, NAD83(2011), m).
// Data from fetch_mn_lidar.py (MnGeo statewide 0.5 m lidar DEM + Esri World Imagery), 3 km x 3 km = 36 M cells.
//   python samples\scripts\fetch_mn_lidar.py C:\SBMData\Savage_Hwy13 470075 4958480 1500 8192
//   sbm new "samples\Savage Hwy13 Terrain Test.sbm"
//   sbm run "samples\scripts\savage-hwy13-terrain.csx" "samples\Savage Hwy13 Terrain Test.sbm"
using SBM.Core.Alignments;
using SBM.Core.References;

const string Data = @"C:\SBMData\Savage_Hwy13";
var p = Doc.Project;
p.Name = "Savage Hwy 13 Terrain Test";
p.References.RemoveAll(r => r.Name == "Lidar DEM 2025");
p.References.Add(new ReferenceModelDefinition
{
    Name = "Lidar DEM 2025", Path = Data + @"\dem.flt", Kind = ReferenceKind.Terrain, Format = ReferenceFormat.Grid,
    UnitScale = 1.0, ImagePath = Data + @"\ortho.jpg", ContourInterval = Ft(2), R = 150, G = 140, B = 120,
});

// Hwy 13 centerline traced from the imagery (between the EB and WB roadways), PIs in UTM meters.
p.Alignments.RemoveAll(a => a.Name == "TH 13");
var pis = new[]
{
    new PiRow(4958840, 468900),
    new PiRow(4958470, 470060, 900),
    new PiRow(4958395, 471560, 1500),
    new PiRow(4958480, 472900),
};
var al = Doc.AddAlignment("TH 13", Sta("100+00"), pis);
var sw = System.Diagnostics.Stopwatch.StartNew();
Doc.Regenerate(); // loads the terrain (36 M cells)
Print($"Terrain loaded in {sw.ElapsedMilliseconds} ms.");

// Profile: lidar ground on the roadway (bare earth; the DEM removes bridge decks, so stations near the bridge are skipped).
double bridgeSta = al.Inverse(new SBM.Core.Geometry.Vec2(470075, 4958480)).Station;
var vpis = new List<Vpi>();
for (double s = al.StartStation + 50; s < al.EndStation - 50; s += 150)
{
    if (Math.Abs(s - bridgeSta) < 120) continue;
    if (Doc.GroundAt(al.PointAt(s, 0)) is double z) vpis.Add(new Vpi { Station = s, Elevation = z, LengthIn = 50, LengthOut = 50 });
}
al.Profile = new VerticalProfile { Vpis = vpis };
Print($"TH 13: STA {Doc.Units.FormatStation(al.StartStation)} to {Doc.Units.FormatStation(al.EndStation)}, bridge near STA {Doc.Units.FormatStation(bridgeSta)}, {vpis.Count} VPIs from lidar.");
Doc.Save();
