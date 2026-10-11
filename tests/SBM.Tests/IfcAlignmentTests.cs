using System.Text.RegularExpressions;
using SBM.Core.Alignments;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.IO.Ifc;
using Xunit;

namespace SBM.Tests;

public class IfcAlignmentTests
{
    // Tangent east, spiral 80 m + arc R 300 m + spiral 60 m curving LEFT, tangent, spiral 50 m + arc R 250 m + spiral 50 m RIGHT.
    private static Alignment Spiraled()
    {
        var def = new PiDefinition
        {
            Pis = { new PiRow(0, 0), new PiRow(0, 1000, 300, 80, 60), new PiRow(500, 1500, 250, 50, 50), new PiRow(0, 2000) },
        };
        return new Alignment(def.Build(out _)) { Name = "S", StartStation = 1000, PiDefinition = def };
    }

    [Fact]
    public void HorizontalSegments_TypesRadiiAndSignConvention()
    {
        var al = Spiraled();
        var hs = IfcAlignmentWriter.HorizontalSegments(al);
        Assert.Equal(new[] { "LINE", "CLOTHOID", "CIRCULARARC", "CLOTHOID", "LINE", "CLOTHOID", "CIRCULARARC", "CLOTHOID", "LINE", "LINE" },
            hs.Select(s => s.Type).ToArray());
        // IFC: radius + = curving left (counter-clockwise), - = right, 0 = infinite.
        var arcs = hs.Where(s => s.Type == "CIRCULARARC").ToList();
        Assert.Equal(2, arcs.Count);
        Assert.Equal(300, arcs[0].StartRadius, 6);   // left
        Assert.Equal(-250, arcs[1].StartRadius, 6);  // right
        Assert.Equal(arcs[0].StartRadius, arcs[0].EndRadius);
        var entry = hs[1];
        Assert.Equal(0, entry.StartRadius);
        Assert.Equal(300, entry.EndRadius, 6);
        Assert.Equal(80, entry.Length, 6);
        var exit = hs[3];
        Assert.Equal(300, exit.StartRadius, 6);
        Assert.Equal(0, exit.EndRadius);
        Assert.Equal(60, exit.Length, 6);
        Assert.True(hs[5].EndRadius < 0 && hs[7].StartRadius < 0);
        // Start point / direction from the elements; zero-length LINE at the end point and end direction.
        for (int i = 0; i < al.Elements.Count; i++)
        {
            Assert.Equal(al.Elements[i].Start, hs[i].StartPoint);
            Assert.Equal(al.Elements[i].StartAngle, hs[i].StartDirection);
            Assert.Equal(al.Elements[i].Length, hs[i].Length);
        }
        var end = hs[^1];
        Assert.Equal(("LINE", 0.0, 0.0, 0.0), (end.Type, end.Length, end.StartRadius, end.EndRadius));
        Assert.True(Vec2.Distance(al.Elements[^1].EndPoint, end.StartPoint) < 1e-9);
        Assert.Equal(al.Elements[^1].EndAngle, end.StartDirection, 12);
    }

    [Theory]
    [InlineData(0, 1 / 300.0, 80)]        // entry spiral, left
    [InlineData(1 / 300.0, 0, 60)]        // exit spiral, left
    [InlineData(0, -1 / 250.0, 50)]       // entry spiral, right
    [InlineData(-1 / 250.0, 0, 50)]       // exit spiral, right
    [InlineData(1 / 600.0, 1 / 300.0, 40)] // partial spiral (compound)
    public void Clothoid_ConstantAndStartGiveTheCurvatures(double k0, double k1, double len)
    {
        var (a, t0) = IfcAlignmentWriter.Clothoid(k0, k1, len);
        // IfcClothoid curvature at parent distance t: t / (A·|A|).
        Assert.Equal(k0, t0 / (a * Math.Abs(a)), 12);
        Assert.Equal(k1, (t0 + len) / (a * Math.Abs(a)), 12);
        Assert.Equal(Math.Sqrt(len / Math.Abs(k1 - k0)), Math.Abs(a), 9);
    }

    [Fact]
    public void Clothoid_KnownValues()
    {
        var (a, t0) = IfcAlignmentWriter.Clothoid(0, 1 / 300.0, 80);
        Assert.Equal(Math.Sqrt(80 * 300.0), a, 9); // A² = R·L
        Assert.Equal(0, t0, 12);
        (a, t0) = IfcAlignmentWriter.Clothoid(1 / 300.0, 0, 80);
        Assert.Equal(-Math.Sqrt(80 * 300.0), a, 9);
        Assert.Equal(-80, t0, 9);
    }

    [Fact]
    public void VerticalSegments_SymmetricParabola()
    {
        var al = new Alignment(new[] { HorizontalElement.Line(new Vec2(0, 0), new Vec2(400, 0)) })
        {
            StartStation = 0,
            Profile = new VerticalProfile { Vpis = { new Vpi(0, 100), new Vpi(200, 104, 100), new Vpi(400, 100) } },
        };
        var vs = IfcAlignmentWriter.VerticalSegments(al);
        Assert.Equal(new[] { "CONSTANTGRADIENT", "PARABOLICARC", "CONSTANTGRADIENT", "CONSTANTGRADIENT" }, vs.Select(v => v.Type).ToArray());
        Assert.Equal(0, vs[0].StartDistAlong, 9);
        Assert.Equal(150, vs[0].HorizontalLength, 9);
        Assert.Equal(100, vs[0].StartHeight, 9);
        Assert.Equal(0.02, vs[0].StartGradient, 12);
        Assert.Equal(0.02, vs[0].EndGradient, 12);
        var vc = vs[1];
        Assert.Equal(150, vc.StartDistAlong, 9);
        Assert.Equal(100, vc.HorizontalLength, 9);
        Assert.Equal(103, vc.StartHeight, 9);
        Assert.Equal(0.02, vc.StartGradient, 9);
        Assert.Equal(-0.02, vc.EndGradient, 9);
        Assert.Equal(-2500, vc.Radius!.Value, 6); // L / (g2 - g1): crest negative
        Assert.Null(vs[0].Radius);
        Assert.Equal(-0.02, vs[2].StartGradient, 9);
        var end = vs[^1];
        Assert.Equal(400, end.StartDistAlong, 9);
        Assert.Equal(0, end.HorizontalLength);
        Assert.Equal(100, end.StartHeight, 9);
        for (double d = 0; d <= 400; d += 7.5)
        {
            var seg = vs.Where(v => v.HorizontalLength > 0).Last(v => v.StartDistAlong <= d + 1e-9);
            Assert.Equal(al.Profile.ElevationAt(d), seg.HeightAt(d), 9);
        }
    }

    [Fact]
    public void VerticalSegments_UnsymmetricalCurveIsTwoParabolasMatchingSbm()
    {
        var al = new Alignment(new[] { HorizontalElement.Line(new Vec2(0, 0), new Vec2(1000, 0)) })
        {
            StartStation = 500,
            Profile = new VerticalProfile { Vpis = { new Vpi(400, 50), new Vpi(900, 40) { LengthIn = 120, LengthOut = 60 }, new Vpi(1600, 47) } },
        };
        var vs = IfcAlignmentWriter.VerticalSegments(al);
        // Distances: profile starts before the alignment (station 500 = distance 0); PVC 780 → 280, VPI 900 → 400, PVT 960 → 460.
        Assert.Equal(new[] { 0.0, 280, 400, 460, 1000 }, vs.Select(v => Math.Round(v.StartDistAlong, 9)).ToArray());
        Assert.Equal("PARABOLICARC", vs[1].Type);
        Assert.Equal("PARABOLICARC", vs[2].Type);
        Assert.Equal(vs[1].EndGradient, vs[2].StartGradient, 12);
        Assert.Equal(vs[1].HeightAt(400), vs[2].StartHeight, 9);
        for (double d = 0; d <= 1000; d += 3.7)
        {
            var seg = vs.Where(v => v.HorizontalLength > 0).Last(v => v.StartDistAlong <= d + 1e-9);
            Assert.Equal(al.Profile.ElevationAt(al.DistanceToStation(d)), seg.HeightAt(d), 9);
            Assert.Equal(al.Profile.GradeAt(al.DistanceToStation(d)), seg.StartGradient + (seg.EndGradient - seg.StartGradient) * (d - seg.StartDistAlong) / seg.HorizontalLength, 9);
        }
    }

    [Fact]
    public void ParabolaLength_MatchesNumericIntegration()
    {
        foreach (var (b, c, h) in new[] { (0.02, -0.0002, 100.0), (-0.05, 0.0004, 300.0), (0.01, 0.0, 50.0), (0.0, 1e-6, 1000.0) })
        {
            int n = 20000;
            double sum = 0;
            for (int i = 0; i < n; i++)
            {
                double x = (i + 0.5) * h / n, u = b + 2 * c * x;
                sum += Math.Sqrt(1 + u * u) * h / n;
            }
            Assert.Equal(sum, IfcAlignmentWriter.ParabolaLength(b, c, h), 6);
        }
    }

    private static string Export(BridgeProject p)
    {
        var sw = new StringWriter();
        IfcExporter.Export(sw, "a.ifc", p, Array.Empty<IfcBridgeData>());
        return sw.ToString();
    }

    [Fact]
    public void Export_WritesLayoutGeometryAndStationing()
    {
        var al = Spiraled();
        al.Profile = new VerticalProfile { Vpis = { new Vpi(1000, 10), new Vpi(1800, 18, 200), new Vpi(4000, 12) } };
        al.StationEquations.Add(new StationEquation { Distance = 900, StationBack = 1900, StationAhead = 2000 });
        var p = new BridgeProject { Name = "AlTest", Alignments = { al } };
        string ifc = Export(p);
        foreach (var s in new[] { "IFCALIGNMENTHORIZONTAL(", "IFCALIGNMENTVERTICAL(", ".CLOTHOID.);", ".CIRCULARARC.);", ".PARABOLICARC.);",
                     ".CONSTANTGRADIENT.);", "IFCCLOTHOID(", "IFCCIRCLE(", "IFCPOLYNOMIALCURVE(", "IFCGRADIENTCURVE(", "IFCCOMPOSITECURVE(",
                     "'FootPrint','Curve2D'", "'Axis','Curve3D'", ".STATION.);", "'Pset_Stationing'", "'IncomingStation'", "IFCLINEARPLACEMENT(",
                     "IFCRELREFERENCEDINSPATIALSTRUCTURE(" })
            Assert.Contains(s, ifc);
        Assert.DoesNotContain("IFCALIGNMENTCANT", ifc);
        Assert.DoesNotContain("IFCINDEXEDPOLYCURVE", ifc);
        // GlobalId pattern of the IfcAlignment is unchanged.
        Assert.Contains($"IFCALIGNMENT('{IfcKeys.GlobalId(IfcKeys.Alignment("AlTest", "S"))}'", ifc);
        // Both curves end with a zero-length DISCONTINUOUS segment.
        Assert.Equal(2, Regex.Matches(ifc, @"IFCCURVESEGMENT\(\.DISCONTINUOUS\.,#\d+,IFCLENGTHMEASURE\(0\.0\),IFCLENGTHMEASURE\(0\.0\)").Count);
        // Horizontal layout: the elements + the zero-length end.
        Assert.Equal(al.Elements.Count + 1, Regex.Matches(ifc, "IFCALIGNMENTHORIZONTALSEGMENT\\(").Count);
        Assert.Equal(2, Regex.Matches(ifc, "IFCREFERENT\\(").Count);
    }

    [Fact]
    public void Export_WithoutProfile_IsAxisCurve2D()
    {
        var al = Spiraled();
        var p = new BridgeProject { Name = "AlTest", Alignments = { al } };
        string ifc = Export(p);
        Assert.Contains("'Axis','Curve2D'", ifc);
        Assert.DoesNotContain("IFCGRADIENTCURVE", ifc);
        Assert.DoesNotContain("IFCALIGNMENTVERTICAL", ifc);
    }

    [Fact]
    public void Writer_ExposesCurveIdsPerAlignment()
    {
        var al = Spiraled();
        al.Profile = new VerticalProfile { Vpis = { new Vpi(1000, 10), new Vpi(3000, 12) } };
        var sw = new StringWriter();
        var w = new StepWriter(sw);
        int ctx = w.Add("IFCGEOMETRICREPRESENTATIONCONTEXT", "$,'Model',3,1.0E-5,$,$");
        int pl = w.Add("IFCLOCALPLACEMENT", "$,$");
        var aw = new IfcAlignmentWriter(w, 1.0, Vec3.Zero, ctx);
        var e = aw.Write(al, "K", pl);
        Assert.Same(e, aw.For("s"));
        Assert.Equal(e.GradientCurve, e.AxisCurve);
        string text = sw.ToString();
        Assert.Contains($"#{e.GradientCurve}=IFCGRADIENTCURVE(", text);
        Assert.Contains($"#{e.CompositeCurve}=IFCCOMPOSITECURVE(", text);
        Assert.Contains($"IFCGRADIENTCURVE(", text);
        Assert.Matches($@"#{e.GradientCurve}=IFCGRADIENTCURVE\(\(#[\d,#]+\),\.F\.,#{e.CompositeCurve},\$\)", text);
        int lp = aw.LinearPlacement(e.AxisCurve, al, 100, 2.0);
        Assert.Contains($"#{lp}=IFCLINEARPLACEMENT(", sw.ToString());
    }
}
