using System.Globalization;
using SBM.Api;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Utility;
using SBM.IO.LandXml;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Storm drainage networks (<see cref="UtilityGenerator"/>, LandXML PipeNetworks import; 2026-10-30, generic test inputs).</summary>
public class UtilityTests
{
    private static string F(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);

    /// <summary>A Civil 3D style LandXML storm network in feet (sizes in inches): MH-1 -> MH-2 (18 in RCP), inlet CB-1 -> MH-1 (15 in RCP).</summary>
    private static string Xml(Vec2 mh1, Vec2 mh2, Vec2 cb1, double rim, bool withDiameterUnit = true) => $"""
        <?xml version="1.0"?>
        <LandXML xmlns="http://www.landxml.org/schema/LandXML-1.2" version="1.2">
          <Units><Imperial linearUnit="foot" areaUnit="squareFoot" volumeUnit="cubicYard" angularUnit="decimal degrees" {(withDiameterUnit ? "diameterUnit=\"inch\"" : "")}/></Units>
          <PipeNetworks>
            <PipeNetwork name="Storm A" pipeNetType="storm">
              <Structs>
                <Struct name="MH-1" desc="Concentric Cylindrical Structure 48 dia" elevRim="{F(rim)}" elevSump="{F(rim - 8)}">
                  <Center>{F(mh1.Y / 0.3048)} {F(mh1.X / 0.3048)}</Center>
                  <CircStruct diameter="48" thickness="5" floorThickness="8" material="Reinforced Concrete"/>
                  <Invert elev="{F(rim - 7.5)}" flowDir="in" refPipe="P-2"/>
                  <Invert elev="{F(rim - 7.6)}" flowDir="out" refPipe="P-1"/>
                </Struct>
                <Struct name="MH-2" desc="Concentric Cylindrical Structure 48 dia" elevRim="{F(rim)}" elevSump="{F(rim - 9)}">
                  <Center>{F(mh2.Y / 0.3048)} {F(mh2.X / 0.3048)}</Center>
                  <CircStruct diameter="48" thickness="5" floorThickness="8"/>
                  <Invert elev="{F(rim - 8.5)}" flowDir="in" refPipe="P-1"/>
                </Struct>
                <Struct name="CB-1" desc="Rectangular inlet" elevSump="{F(rim - 5)}">
                  <Center>{F(cb1.Y / 0.3048)} {F(cb1.X / 0.3048)}</Center>
                  <RectStruct length="36" width="24" thickness="6" floorThickness="6"/>
                  <InletStruct/>
                  <Invert elev="{F(rim - 4.5)}" flowDir="out" refPipe="P-2"/>
                </Struct>
              </Structs>
              <Pipes>
                <Pipe name="P-1" refStart="MH-1" refEnd="MH-2"><CircPipe diameter="18" thickness="2.5" material="RCP"/></Pipe>
                <Pipe name="P-2" refStart="CB-1" refEnd="MH-1"><CircPipe diameter="15" thickness="2.25" material="RCP"/></Pipe>
              </Pipes>
            </PipeNetwork>
          </PipeNetworks>
        </LandXML>
        """;

    private static (SbmDocument d, Vec2 mh1, Vec2 mh2, Vec2 cb1, double rim) Site()
    {
        var d = SbmDocument.Sample();
        var al = d.Alignment();
        var mh1 = al.PointAt(al.StartStation + 5, Ft(40));
        var mh2 = mh1 + new Vec2(Ft(100), 0);
        var cb1 = mh1 + new Vec2(0, Ft(30));
        return (d, mh1, mh2, cb1, 500.0);   // rim in feet
    }

    [Fact]
    public void LandXml_PipeNetwork_UnitsStructuresPipesInverts()
    {
        var (_, mh1, mh2, cb1, rim) = Site();
        var r = LandXmlPipeNetworks.ReadString(Xml(mh1, mh2, cb1, rim), "test.xml");
        var n = r.Networks.Single();
        Assert.Equal(UtilitySystem.Storm, n.System);
        Assert.Empty(n.Unresolved);
        var m1 = n.Structures.Single(s => s.Name == "MH-1");
        Assert.Equal(UtilityStructureKind.Manhole, m1.Kind);
        Assert.Equal(mh1.X, m1.X, 6); Assert.Equal(mh1.Y, m1.Y, 6);
        Assert.Equal(In(48), m1.InnerDiameter, 9);
        Assert.Equal(In(5), m1.WallThickness, 9);
        Assert.Equal(Ft(rim), m1.RimElevation, 6);
        var cb = n.Structures.Single(s => s.Name == "CB-1");
        Assert.Equal(UtilityStructureKind.Inlet, cb.Kind);
        Assert.Equal(UtilityStructureShape.Box, cb.Shape);
        Assert.True(double.IsNaN(cb.RimElevation));
        var p1 = n.Pipes.Single(p => p.Name == "P-1");
        Assert.Equal(("MH-1", "MH-2"), (p1.From, p1.To));
        Assert.Equal(Ft(rim - 7.6), p1.InvertStart, 6);
        Assert.Equal(Ft(rim - 8.5), p1.InvertEnd, 6);
        Assert.Equal(In(18), p1.InnerDiameter, 9);
        Assert.Equal("RCP", p1.Material);
        // no diameterUnit: sizes in the linear unit, flagged
        var r2 = LandXmlPipeNetworks.ReadString(Xml(mh1, mh2, cb1, rim, withDiameterUnit: false), "test.xml");
        Assert.Contains(r2.Networks[0].Unresolved, x => x.Contains("diameterUnit"));
    }

    [Fact]
    public void Generator_Solids_PipeBetweenInsideFaces_RimFromGround_Cover_Checks()
    {
        var (d, mh1, mh2, cb1, rim) = Site();
        var net = LandXmlPipeNetworks.ReadString(Xml(mh1, mh2, cb1, rim), "test.xml").Networks.Single();
        net.MinCover = Ft(5);
        double gz = Ft(rim);   // flat finished ground at the rims
        var m = UtilityGenerator.Generate(net, d.Project.Alignments, _ => (gz, "existing ground"));
        Assert.Equal(3, m.Structures.Count(s => s.IsBuilt));
        Assert.Equal(2, m.Pipes.Count(p => p.IsBuilt));
        // inlet rim from the finished ground
        var cb = m.Structures.Single(s => s.Definition.Name == "CB-1");
        Assert.Equal(gz, cb.Rim, 9);
        Assert.Equal("existing ground", cb.RimSource);
        // manhole volume: 32-gon ring wall + floor
        var mh = m.Structures.Single(s => s.Definition.Name == "MH-1");
        double k = 32 / (2 * Math.PI) * Math.Sin(2 * Math.PI / 32);   // polygon / circle area
        double ri = In(24), ro = In(29);
        double expect = k * Math.PI * ((ro * ro - ri * ri) * Ft(8) + ro * ro * In(8));
        Assert.Equal(2, mh.Openings.Count);   // P-1 out, P-2 in: the pipes' outside sections through the wall are deducted
        Assert.Equal(expect - mh.Openings.Sum(o => o.Deduction), mh.Volume, 4);
        Assert.Equal(expect, Math.Abs(mh.Mesh!.Volume()), 4);
        // P-1 runs from the inside face of MH-1 to the inside face of MH-2: 100 ft - 2 x 2 ft
        var p1 = m.Pipes.Single(p => p.Definition.Name == "P-1");
        Assert.Equal(Ft(96), p1.Length, 6);
        Assert.Equal(Ft(rim - 7.6), p1.Start.Z, 9);
        Assert.Equal(Ft(0.9) / Ft(96), p1.Slope, 9);
        double pv = k * Math.PI * (Math.Pow(In(9 + 2.5), 2) - Math.Pow(In(9), 2)) * Math.Sqrt(Ft(96) * Ft(96) + Ft(0.9) * Ft(0.9));
        Assert.Equal(pv, p1.Volume, 4);
        // cover over the crown: least at the high (start) end = 7.6 ft - 18 in - 2.5 in
        Assert.Equal(Ft(7.6) - In(20.5), p1.MinCover!.Value, 6);
        // P-2 from the 36 x 24 in box (laid along its outlet pipe: inside face 18 in from the centre) to MH-1 (2 ft): 30 - 1.5 - 2 ft
        var p2 = m.Pipes.Single(p => p.Definition.Name == "P-2");
        Assert.Equal(Ft(26.5), p2.Length, 6);
        Assert.Contains(m.Diagnostics, x => x.Message.Contains("CB-1 plan orientation") && x.Message.Contains("UNRESOLVED"));
        // P-2 cover 4.5 ft - 15 in - 2.25 in < 5 ft required, P-1 5.89 ft is not: CONFLICT; MH-1 outlet (7.6) below its inlet (7.5): no drop warning
        Assert.Contains(m.Diagnostics, x => x.Message.Contains("CONFLICT (cover) pipe P-2"));
        Assert.DoesNotContain(m.Diagnostics, x => x.Message.Contains("CONFLICT (cover) pipe P-1"));
        Assert.DoesNotContain(m.Diagnostics, x => x.Message.Contains("CHECK structure MH-1: outlet"));
    }

    [Fact]
    public void MissingValues_AreUnresolved_AdverseSlope_AndDropAreReported()
    {
        var (d, mh1, mh2, cb1, rim) = Site();
        var net = LandXmlPipeNetworks.ReadString(Xml(mh1, mh2, cb1, rim), "test.xml").Networks.Single();
        net.Structures.Single(s => s.Name == "MH-2").WallThickness = double.NaN;
        var p1 = net.Pipes.Single(p => p.Name == "P-1");
        (p1.InvertStart, p1.InvertEnd) = (p1.InvertEnd, p1.InvertStart);   // rises toward MH-2; MH-1 outlet now above its inlet
        net.Pipes.Single(p => p.Name == "P-2").WallThickness = double.NaN;
        var m = UtilityGenerator.Generate(net, d.Project.Alignments, null);
        Assert.False(m.Structures.Single(s => s.Definition.Name == "MH-2").IsBuilt);
        Assert.Contains(m.Diagnostics, x => x.Message.Contains("UNRESOLVED") && x.Message.Contains("MH-2 wall thickness"));
        Assert.DoesNotContain(m.Pipes, p => p.Definition.Name == "P-2");
        Assert.Contains(m.Diagnostics, x => x.Message.Contains("UNRESOLVED") && x.Message.Contains("pipe P-2 wall thickness"));
        // no ground: the inlet rim is unresolved, the inlet is not built
        Assert.Contains(m.Diagnostics, x => x.Message.Contains("CB-1 rim"));
        Assert.Contains(m.Diagnostics, x => x.Message.Contains("CHECK pipe P-1: adverse slope"));
        Assert.Contains(m.Diagnostics, x => x.Message.Contains("cover not checked"));
        // MH-2 not built: P-1 still runs to its inside face (the inside size is known)
        Assert.Equal(Ft(96), m.Pipes.Single(p => p.Definition.Name == "P-1").Length, 6);
    }

    [Fact]
    public void Import_RoundTrip_Ifc_Database()
    {
        var (d, mh1, mh2, cb1, rim) = Site();
        Assert.DoesNotContain("\"Utilities\"", d.Project.ToJson());
        string xml = Path.Combine(Path.GetTempPath(), "sbm-storm-" + Guid.NewGuid().ToString("N")[..8] + ".xml");
        string ifcPath = Path.ChangeExtension(xml, ".ifc");
        try
        {
            File.WriteAllText(xml, Xml(mh1, mh2, cb1, rim));
            var msgs = d.ImportLandXml(xml);
            Assert.Contains(msgs, x => x.Contains("Storm A") && x.Contains("3 structures"));
            Assert.DoesNotContain(msgs, x => x.Contains("No alignments"));
            var back = BridgeProject.FromJson(d.Project.ToJson());
            Assert.Equal(2, back.Utilities.Single().Pipes.Count);
            // a second import of the same network replaces it
            Assert.Contains(d.ImportLandXml(xml), x => x.Contains("replaces"));
            Assert.Single(d.Project.Utilities);
            // the inlet has no rim and no terrain is loaded: it is UNRESOLVED; give it one for the export
            d.Project.Utilities[0].Structures.Single(s => s.Name == "CB-1").RimElevation = Ft(rim);
            d.ExportIfc(ifcPath);
            string ifc = File.ReadAllText(ifcPath);
            Assert.Contains("IFCDISTRIBUTIONSYSTEM(", ifc);
            Assert.Contains(".STORMWATER.", ifc);
            Assert.Contains(".MANHOLE.", ifc);
            Assert.Contains("'INLET'", ifc);
            Assert.Contains("IFCPIPESEGMENT(", ifc);
            Assert.Contains("IFCRELCONNECTSPORTS(", ifc);
            Assert.Contains("IFCRELSERVICESBUILDINGS(", ifc);
            var um = d.Utilities();
            var tags = SBM.IO.Database.ModelDatabase.BuildTagIndex(d.Project, Array.Empty<SBM.IO.Ifc.IfcBridgeData>(), utilities: um);
            foreach (var s in um[0].Structures) Assert.Contains("'" + tags.Lookup(s) + "'", ifc);
            foreach (var p in um[0].Pipes) Assert.Contains("'" + tags.Lookup(p) + "'", ifc);
        }
        finally { File.Delete(xml); File.Delete(ifcPath); }
    }
}
