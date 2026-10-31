using System.Globalization;
using SBM.Api;
using SBM.Core.Geometry;
using SBM.Core.Road;
using SBM.IO.LandXml;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Civil 3D corridor feature lines (LandXML PlanFeatures) imported as a road (2026-10-30, generic test inputs).</summary>
public class CorridorImportTests
{
    private static string F(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);

    /// <summary>Feature lines in feet along the sample alignment: EOP-L at -12 ft, CL, EOP-R at +12 ft (lanes -2 %), SHLD-R at +20 ft (-4 %); optional taper of SHLD-R.</summary>
    private static string Xml(SBM.Core.Alignments.Alignment al, double s0, double s1, bool taper = false)
    {
        string Line(string name, Func<double, double> off, Func<double, double> dz)
        {
            var pts = new List<string>();
            for (double s = s0; s <= s1 + 1e-9; s += 10)
            {
                double o = off(s);
                var p = al.PointAt(s, o);
                double z = al.Profile.ElevationAt(s) + dz(o);
                pts.Add($"{F(p.Y / 0.3048)} {F(p.X / 0.3048)} {F(z / 0.3048)}");
            }
            return $"<PlanFeature name=\"{name}\"><CoordGeom><IrregularLine><PntList3D>{string.Join(" ", pts)}</PntList3D></IrregularLine></CoordGeom></PlanFeature>";
        }
        double Lane(double o) => -0.02 * Math.Abs(o);
        double Shld(double o) => -0.02 * Ft(12) - 0.04 * (Math.Abs(o) - Ft(12));
        return $"""
            <?xml version="1.0"?>
            <LandXML xmlns="http://www.landxml.org/schema/LandXML-1.2" version="1.2">
              <Units><Imperial linearUnit="foot"/></Units>
              <PlanFeatures name="Corridor A">
                {Line("EOP-L", _ => -Ft(12), Lane)}
                {Line("CL", _ => 0, Lane)}
                {Line("EOP-R", _ => Ft(12), Lane)}
                {Line("SHLD-R", s => taper ? Ft(20) + (s - s0) * 0.01 : Ft(20), Shld)}
              </PlanFeatures>
            </LandXML>
            """;
    }

    [Fact]
    public void FeatureLines_Read_Road_Courses_Template()
    {
        var d = SbmDocument.Sample();
        var al = d.Alignment();
        double s0 = al.StartStation + 20, s1 = s0 + 100;
        // the sample profile has VPIs; the test lines follow it
        string path = Path.Combine(Path.GetTempPath(), "sbm-corr-" + Guid.NewGuid().ToString("N")[..8] + ".xml");
        try
        {
            File.WriteAllText(path, Xml(al, s0, s1));
            var r = LandXmlFeatureLines.Read(path);
            Assert.Equal(new[] { "EOP-L", "CL", "EOP-R", "SHLD-R" }, r.Lines.Select(l => l.Name));
            Assert.Equal(11, r.Lines[0].Points.Count);
            var p0 = al.PointAt(s0, -Ft(12));
            Assert.Equal(p0.X, r.Lines[0].Points[0].X, 5);
            // ImportLandXml points at the corridor import
            Assert.Contains(d.ImportLandXml(path), m => m.Contains("4 feature lines"));
            var courses = new List<RoadCourse> { new() { Name = "Surface", Thickness = In(4), Material = "HMA" }, new() { Name = "Base", Thickness = In(10), Material = "Aggregate" } };
            var msgs = d.ImportCorridor(path, al.Name, "Corridor A", courses: courses);
            Assert.Contains(msgs, m => m.Contains("4 feature lines"));
            var road = d.Roads().Single(x => x.Definition.Name == "Corridor A");
            Assert.Equal(6, road.Parts.Count);                    // 3 strips x 2 courses
            Assert.Equal(4, road.EdgeLines.Count);
            Assert.Equal(new[] { "CL - EOP-R", "EOP-L - CL", "EOP-R - SHLD-R" }, road.Parts.Select(p => p.Component).Distinct().OrderBy(x => x));
            Assert.Equal(RoadComponentKind.Shoulder, road.Parts.First(p => p.Component == "EOP-R - SHLD-R").Kind);
            // course volume = plan area x thickness (vertical sides, top = the feature lines)
            var shl = road.Parts.Single(p => p.Component == "EOP-R - SHLD-R" && p.Course == "Base");
            double plan = road.TopArea["right EOP-R - SHLD-R"] / Math.Sqrt(1 + 0.04 * 0.04);
            Assert.True(Math.Abs(plan * In(10) - shl.Volume) < 1e-3 * shl.Volume, $"{shl.Volume} vs {plan * In(10)}");   // top area includes the profile grade
            // the top of the road IS the feature lines: the finished ground at a point between EOP-R and SHLD-R
            var fg = d.FinishedGround();
            double sm = (s0 + s1) / 2, om = Ft(16);
            var q = fg.Query(al.PointAt(sm, om));
            Assert.Equal("road", q.source);
            Assert.Equal(al.Profile.ElevationAt(sm) - 0.02 * Ft(12) - 0.04 * Ft(4), q.z!.Value, 3);
            // parametric template: constant offsets -> components with their slopes
            var (t, notes) = RoadGenerator.ToTemplate(d.Project.Roads.Single(x => x.Name == "Corridor A"), al);
            Assert.NotNull(t);
            Assert.Single(t!.Left.Components);
            Assert.Equal(Ft(12), t.Left.Components[0].Width, 4);
            Assert.Equal(-0.02, t.Left.Components[0].Slope!.Value, 4);
            Assert.Equal(new[] { "EOP-R", "SHLD-R" }, t.Right.Components.Select(c => c.Name));
            Assert.Equal(Ft(8), t.Right.Components[1].Width, 4);
            Assert.Equal(-0.04, t.Right.Components[1].Slope!.Value, 4);
            Assert.Equal(2, t.Right.Components[1].Courses.Count);
            // round trip of the feature-line road
            var back = SBM.Core.Project.BridgeProject.FromJson(d.Project.ToJson());
            Assert.Equal(4, back.Roads.Single(x => x.Name == "Corridor A").FeatureLines!.Count);
            // a tapering shoulder line cannot be a constant-width template: kept as feature lines, reported
            File.WriteAllText(path, Xml(al, s0, s1, taper: true));
            var msgs2 = d.ImportCorridor(path, al.Name, "Corridor A", asTemplate: true);
            Assert.Contains(msgs2, m => m.Contains("SHLD-R varies in offset"));
            Assert.Contains(msgs2, m => m.Contains("replaces"));
            Assert.NotNull(d.Project.Roads.Single(x => x.Name == "Corridor A").FeatureLines);
            // no courses: top surface only, flagged
            var top = d.Roads().Single(x => x.Definition.Name == "Corridor A");
            Assert.Empty(top.Parts);
            Assert.True(top.SideSlopes.ContainsKey("finished surface"));
            Assert.Contains(top.Diagnostics, x => x.Message.Contains("UNRESOLVED") && x.Message.Contains("pavement structure"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void FinishedGround_Tin_FromTheRoadSurface()
    {
        var d = SbmDocument.Sample();
        var al = d.Alignment();
        double s0 = al.StartStation + 20;
        d.Project.Roads.Add(new RoadDefinition
        {
            Name = "R", AlignmentName = al.Name, BeginStation = s0, EndStation = s0 + 30,
            Right = new() { Components = { new() { Name = "Lane", Width = 3.6, Slope = -0.02, Courses = { new() { Name = "Surface", Thickness = 0.1 } } } } },
        });
        string path = Path.Combine(Path.GetTempPath(), "sbm-fg-" + Guid.NewGuid().ToString("N")[..8] + ".xml");
        try
        {
            var msgs = d.ExportFinishedGround(path, 0.5, 0);
            Assert.Contains(msgs, m => m.Contains("road:"));
            Assert.Contains(msgs, m => m.Contains("no existing ground"));
            var x = System.Xml.Linq.XDocument.Load(path);
            var pts = x.Descendants().Where(e => e.Name.LocalName == "P").ToList();
            var faces = x.Descendants().Where(e => e.Name.LocalName == "F").ToList();
            Assert.True(pts.Count > 100 && faces.Count > 100);
            // every TIN point lies on the road top (feet in the file)
            var fg = d.FinishedGround();
            foreach (var pe in pts.Take(50))
            {
                var v = pe.Value.Split(' ').Select(t => double.Parse(t, CultureInfo.InvariantCulture) * 0.3048).ToArray();
                Assert.Equal(fg.Query(new Vec2(v[1], v[0])).z!.Value, v[2], 3);
            }
        }
        finally { File.Delete(path); }
    }
}
