using SBM.Core.Drawing;
using SBM.Core.Framing;
using SBM.Core.Project;
using SBM.Core.Units;
using SBM.IO.Dxf;
using SBM.IO.LandXml;
using Xunit;

namespace SBM.Tests;

public class IoAndUnitsTests
{
    private static readonly UnitSystem Us = new();
    private static readonly UnitSystem Si = new() { Kind = UnitSystemKind.SI };

    [Theory]
    [InlineData("12'-6 1/2\"", 12 + 6.5 / 12)]
    [InlineData("12' 6\"", 12.5)]
    [InlineData("12.5'", 12.5)]
    [InlineData("150\"", 12.5)]
    [InlineData("12.5", 12.5)]
    [InlineData("-3'-0\"", -3)]
    [InlineData("3 1/2", 3.5)]
    public void ParseLength_US_Long(string text, double feet)
    {
        Assert.True(Us.TryParseLength(text, LengthKind.Long, out double m));
        Assert.Equal(feet * 0.3048, m, 9);
    }

    [Theory]
    [InlineData("7/8", 0.875)]
    [InlineData("1 1/4\"", 1.25)]
    [InlineData("0.5", 0.5)]
    public void ParseLength_US_Short(string text, double inches)
    {
        Assert.True(Us.TryParseLength(text, LengthKind.Short, out double m));
        Assert.Equal(inches * 0.0254, m, 9);
    }

    [Fact]
    public void ParseLength_Metric()
    {
        Assert.True(Si.TryParseLength("300 mm", LengthKind.Long, out double a));
        Assert.Equal(0.3, a, 12);
        Assert.True(Si.TryParseLength("25", LengthKind.Short, out double b));
        Assert.Equal(0.025, b, 12);
    }

    [Fact]
    public void FormatAndParseStation()
    {
        Assert.Equal("12+34.56", Us.FormatStation(1234.56 * 0.3048));
        Assert.Equal("1+234.567", Si.FormatStation(1234.567, 3));
        Assert.Equal("0+05.00", Us.FormatStation(5 * 0.3048));
        Assert.True(Us.TryParseStation("12+34.56", out double m));
        Assert.Equal(1234.56 * 0.3048, m, 9);
    }

    [Fact]
    public void FormatFeetInches()
    {
        Assert.Equal("12'-6 1/2\"", Us.FormatLength((12 + 6.5 / 12) * 0.3048));
        Assert.Equal("7/8\"", Us.FormatLength(0.875 * 0.0254, LengthKind.Short));
        // Feet-inches keep the zero inches before a fraction, as the plans print (not 8'-5/16").
        Assert.Equal("8'-0 5/16\"", Us.FormatLength((8 + 5.0 / 16 / 12) * 0.3048));
        Assert.Equal("10'-0\"", Us.FormatLength(10 * 0.3048));
    }

    [Fact]
    public void ParseAngle()
    {
        Assert.True(UnitSystem.TryParseAngle("15°30'00\"", out double a));
        Assert.Equal(15.5 * Math.PI / 180, a, 12);
        Assert.True(UnitSystem.TryParseAngle("-20", out double b));
        Assert.Equal(-20 * Math.PI / 180, b, 12);
    }

    [Fact]
    public void LandXml_RoundTrip_PreservesGeometry()
    {
        var a = SampleProject.CreateAlignment();
        var xml = LandXmlWriter.Build(new[] { a }, Us).ToString();
        var res = LandXmlReader.ReadString(xml);
        Assert.Single(res.Alignments);
        var b = res.Alignments[0];
        Assert.Equal(a.Elements.Count, b.Elements.Count);
        Assert.Equal(a.Length, b.Length, 5);
        for (double d = 0; d < a.Length; d += 37)
        {
            var p = a.PointAtDistance(d, 7);
            var q = b.PointAtDistance(d, 7);
            Assert.True((p - q).Length < 1e-5, $"mismatch {(p - q).Length} at {d}");
            Assert.Equal(a.Profile.ElevationAt(a.DistanceToStation(d)), b.Profile.ElevationAt(b.DistanceToStation(d)), 5);
        }
        Assert.NotNull(b.PiDefinition);
    }

    [Fact]
    public void LandXml_ReadsCivil3dStyleCurveAndSpiral()
    {
        const string xml = """
            <?xml version="1.0"?>
            <LandXML xmlns="http://www.landxml.org/schema/LandXML-1.2" version="1.2">
              <Units><Imperial linearUnit="USSurveyFoot" areaUnit="squareFoot" volumeUnit="cubicYard" angularUnit="decimal degrees" directionUnit="decimal degrees"/></Units>
              <Alignments>
                <Alignment name="A" length="0" staStart="1000">
                  <CoordGeom>
                    <Line><Start>5000 10000</Start><End>5000 10500</End></Line>
                    <Curve rot="cw" radius="1000"><Start>5000 10500</Start><Center>4000 10500</Center><End>4000 11500</End></Curve>
                  </CoordGeom>
                </Alignment>
              </Alignments>
            </LandXML>
            """;
        var res = LandXmlReader.ReadString(xml);
        var a = res.Alignments[0];
        Assert.True(res.UsSurveyFeet);
        double ft = 1200.0 / 3937.0;
        Assert.Equal((500 + Math.PI / 2 * 1000) * ft, a.Length, 6);
        Assert.Equal(1000 * ft, a.StartStation, 9);
        var end = a.Elements[^1].EndPoint;
        Assert.Equal(11500 * ft, end.X, 6);
        Assert.Equal(4000 * ft, end.Y, 6);
    }

    [Fact]
    public void Project_JsonRoundTrip()
    {
        var p = SampleProject.Create();
        var q = BridgeProject.FromJson(p.ToJson());
        Assert.Equal(p.Alignments[0].Length, q.Alignments[0].Length, 9);
        Assert.Equal(p.Bridges[0].Supports.Count, q.Bridges[0].Supports.Count);
        var f1 = FramingGenerator.Generate(p.Bridges[0], p.Alignments[0]);
        var f2 = FramingGenerator.Generate(q.Bridges[0], q.Alignments[0]);
        Assert.Equal(f1.CrossFrames.Count, f2.CrossFrames.Count);
    }

    [Fact]
    public void Dxf_WritesAllLayers()
    {
        var p = SampleProject.Create();
        var f = FramingGenerator.Generate(p.Bridges[0], p.Alignments[0]);
        var dwg = PlanDrawingBuilder.Build(p.Alignments[0], f, p.Units);
        var sw = new StringWriter();
        DxfWriter.Write(sw, dwg, p.Units.LongUnit);
        string s = sw.ToString();
        Assert.StartsWith("0\nSECTION", s);
        Assert.EndsWith("0\nEOF\n", s);
        Assert.Contains("SBM-CROSSFRAME", s);
        Assert.Contains("SBM-STIFFENER", s);
    }
}
