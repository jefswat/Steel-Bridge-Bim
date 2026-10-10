// Sets a heavier bottom flange over every pier (±0.2 × adjacent span) and switches to 7 girders at 7'-6".
// Changes stay in the project (Undo in the app reverts the whole script).
var b = Doc.Bridge();
var plates = new List<PlateSegment>();
for (int i = 1; i < b.Supports.Count - 1; i++)
{
    double back = b.Supports[i].Station - b.Supports[i - 1].Station;
    double ahead = b.Supports[i + 1].Station - b.Supports[i].Station;
    plates.Add(new PlateSegment { Plate = GirderPlate.BottomFlange, EndStation = b.Supports[i].Station - 0.2 * back, Width = In(18), Thickness = In(1.25) });
    plates.Add(new PlateSegment { Plate = GirderPlate.BottomFlange, EndStation = b.Supports[i].Station + 0.2 * ahead, Width = In(22), Thickness = In(2.5) });
}
plates.Add(new PlateSegment { Plate = GirderPlate.BottomFlange, Width = In(18), Thickness = In(1.25) });
b.Girders.Section.PlateChanges = plates;

b.Girders.Count = 7;
b.Girders.Spacings = Enumerable.Repeat(Ft(7.5), 6).ToList();
b.Girders.FirstGirderOffset = -3 * Ft(7.5);

var r = Doc.Regenerate();
Print($"{Doc.Framing().Girders.Count} girders, {r.CrossFrames} cross frames, {r.SteelMassKg / 907.18474:F1} tons");
foreach (var w in r.Warnings.Take(5)) Print("warning: " + w);
