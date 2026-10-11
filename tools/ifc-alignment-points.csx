// Exports a model's IFC and dumps SBM's own alignment evaluation for tools/check_ifc_alignment.py.
//   sbm run tools/ifc-alignment-points.csx [model.sbm] -- out.ifc points.json [abv|nurbs|parametric] [--spiral] [--alignments-only]
// (--alignments-only drops the bridges before exporting: a small file for big models.)
// Without a model (or with --spiral) a synthetic alignment is added: tangent - spiral 80 m - arc R 300 m left - spiral 60 m -
// tangent - spiral 50 m - arc R 250 m right - spiral 50 m - tangent, a profile with a symmetric and an unsymmetrical vertical
// curve, and a station equation (+50 m gap).
// points.json: per alignment the samples {d (project units, distance along), x, y, z (grid coordinates, project units)}.
using SBM.Core.Alignments;
using SBM.IO.Ifc;

string ifc = Args[0], json = Args[1];
var mode = Args.Length > 2 && !Args[2].StartsWith("--") ? Args[2] switch { "abv" => IfcGeometryMode.ReferenceView, "nurbs" => IfcGeometryMode.Nurbs, _ => IfcGeometryMode.Parametric } : IfcGeometryMode.Parametric;
if (Doc.Project.Alignments.Count == 0 || Args.Contains("--spiral"))
{
    var al = Doc.AddAlignment("Spiral test", 1000.0, new[]
    {
        new PiRow(5000, 2000), new PiRow(5600, 2350, 300, 80, 60), new PiRow(6000, 3100, 250, 50, 50), new PiRow(6700, 3500),
    }, new[]
    {
        new Vpi(1000, 300), new Vpi(1400, 306, 200), new Vpi(1900, 301) { LengthIn = 120, LengthOut = 60 }, new Vpi(2600, 301),
    });
    // Station equation at distance 1200: stations after it run 50 m ahead (only the last VPI lies beyond it). SBM evaluates
    // the profile by station, so the last grade is flat to keep the elevation continuous across the equation.
    al.StationEquations.Add(new StationEquation { Distance = 1200, StationBack = 2200, StationAhead = 2250 });
    al.Profile.Vpis[^1].Station = 2650;
}
if (Args.Contains("--alignments-only")) Doc.Project.Bridges.Clear();
Doc.ExportIfc(ifc, mode);

double s = 1 / Doc.Units.LongUnit;
var inv = System.Globalization.CultureInfo.InvariantCulture;
var sb = new System.Text.StringBuilder("{");
bool firstAl = true;
foreach (var a in Doc.Project.Alignments.Where(x => x.Elements.Count > 0))
{
    var ds = new SortedSet<double>();
    for (double d = 0; d < a.Length; d += 2.5) ds.Add(d);
    ds.Add(a.Length);
    foreach (var e in a.Elements) { ds.Add(e.StartDistance); ds.Add(e.StartDistance + e.Length / 3); }
    foreach (var v in IfcAlignmentWriter.VerticalSegments(a)) { ds.Add(v.StartDistAlong); ds.Add(v.StartDistAlong + v.HorizontalLength / 2); }
    if (!firstAl) sb.Append(',');
    firstAl = false;
    sb.Append($"\"{a.Name}\":{{\"profile\":{(a.Profile.IsEmpty ? "false" : "true")},\"pts\":[");
    bool f = true;
    foreach (double d in ds.Where(d => d >= 0 && d <= a.Length))
    {
        var p = a.PointAtDistance(d);
        double z = a.Profile.IsEmpty ? 0 : a.Profile.ElevationAt(a.DistanceToStation(d));
        if (!f) sb.Append(',');
        f = false;
        sb.Append(string.Format(inv, "[{0:R},{1:R},{2:R},{3:R}]", d * s, p.X * s, p.Y * s, z * s));
    }
    sb.Append("]}");
}
sb.Append('}');
System.IO.File.WriteAllText(json, sb.ToString());
Print($"IFC ({mode}) -> {ifc}; points -> {json}");
