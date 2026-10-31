using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.IO.Ifc;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Segmental box reinforcing from face-referenced section paths (SegmentalDefinition.SectionBars): corners at the printed covers, free legs, fitted
/// legs, mirror, bars in every matched segment following the variable depth, longitudinal distributions, void faces at solid stations not placed (counted),
/// schedule / cover / IFC / JSON round trip.</summary>
public class SegmentalSectionBarsTests
{
    private static BridgeModel Generate(BridgeProject p) => new RegenerationCache().Regenerate(p).Single()!;

    private static SegmentalBarPoint C(string f1, double o1, string f2, double o2) => new() { Face = f1, Offset = In(o1), Face2 = f2, Offset2 = In(o2) };
    private static SegmentalBarPoint Leg(string f, double len, double dx, double dy) => new() { Face = f, Length = In(len), DirX = dx, DirY = dy };

    private static (BridgeProject p, BridgeDefinition b) Model()
    {
        var (p, b) = SegmentalTests.Model();
        b.Segmental!.SectionBars = new()
        {
            // A1: tip to tip at 3 1/4" below the top, 2" from the tips, 15 @ 11 1/2" in every typical segment
            new() { Mark = "A1", Size = "#4", Unit = "typical", Segments = @"^\d+-[DU]\d+$", Path = { C("L:TIP", 2, "TOP", 3.25), C("TIP", 2, "TOP", 3.25) }, Count = 15, Spacing = In(11.5),
                Cover = In(2), CoverTop = In(3), ListedLength = Ft(42) + In(3) },
            // W3: web leg 5'-7 1/2" up the outside face, corner at the soffit, flat 6'-8" toward CL; both webs, 4 @ 6"
            new() { Mark = "W3", Size = "#6", Unit = "typical", Segments = @"^\d+-[DU]\d+$", Mirror = true, Count = 4, Spacing = In(6), ShapeCode = "1", Cover = In(2),
                Path = { Leg("WEBOUT", 67.125, 0, -1), C("WEBOUT", 2.375, "SOFFIT", 2.375), Leg("H", 79.625, -1, 0) } },
            // A5: leg along the bottom haunch then flat toward CL solved for 5'-9"
            new() { Mark = "A5", Size = "#4", Unit = "typical", Segments = @"^1-D3$", Mirror = true, Count = 1, ListedLength = Ft(5) + In(9), FitLeg = 2, ShapeCode = "2",
                Path = { C("WEBOUT", 3, "BHAUNCH", 2.25), C("BHAUNCH", 2.25, "BOTIN", 2.25), Leg("H", 24, -1, 0) } },
            // a void face in the bent half: hollow only in the last 1'-6" -> bars at the bent CL are counted, not placed
            new() { Mark = "X1", Size = "#4", Unit = "bent", Segments = @"^1-[DU]B$", Path = { C("L:WEBIN", 2.25, "BOTIN", 2.25), C("WEBIN", 2.25, "BOTIN", 2.25) }, Count = 2, Spacing = Ft(4) },
            // longitudinal: 5 bars equally spaced on the top line, 2" end clear
            new() { Mark = "B1", Size = "#4", Unit = "typical", Segments = @"^1-U2$", Kind = SegmentalSectionBarKind.Longitudinal, Count = 5, EndClear = In(2), LongSamples = 3,
                Path = { new() { Face = "TOP", Offset = In(3.75), X = -Ft(10) }, new() { Face = "TOP", Offset = In(3.75), X = Ft(10) } }, Cover = In(2), CoverTop = In(3) },
        };
        return (p, b);
    }

    [Fact]
    public void Paths_CornersLegsFitAndMirror_FollowEverySegment()
    {
        var (p, b) = Model();
        var m = Generate(p);
        var sm = m.Concrete!.Segmental!;
        int typical = sm.Segments.Count(s => s.Type == SegmentType.Typical);
        Assert.Equal(15 * typical, sm.Bars.Count(x => x.Mark == "A1"));
        foreach (var x in sm.Bars.Where(x => x.Mark == "A1")) Assert.Equal(b.Segmental!.Box.TopWidth - In(4), (x.Points[^1] - x.Points[0]).Length, 6);
        // W3: out-to-out = 5'-7 1/2" + 6'-8" (legs given as centerline + db/2 at the bend)
        var w3 = sm.Marks.Single(k => k.Mark == "W3");
        Assert.Equal(8 * typical, w3.Count);
        Assert.InRange(w3.Length, In(147.5 - 0.6), In(147.5 + 0.6));
        // the web leg is parallel to the web: its corner lies 2 3/8" (normal) inside the outside face, in every depth
        foreach (var x in sm.Bars.Where(x => x.Mark == "W3"))
        {
            var leg = (x.Points[0] - x.Points[1]).Normalized();
            Assert.True(leg.Z > 0.9);
        }
        var a5 = sm.Marks.Single(k => k.Mark == "A5");
        Assert.Equal(2, a5.Count);
        Assert.Equal(Ft(5) + In(9), a5.Length, 6);
        var bars5 = sm.Bars.Where(x => x.Mark == "A5").ToList();
        // mirrored copies are at the same station, symmetric about CL girder (equal distance from the CL point at the station)
        Assert.Equal(SegmentalLength(bars5[0].Points), SegmentalLength(bars5[1].Points), 6);
    }

    private static double SegmentalLength(Vec3[] p) { double l = 0; for (int i = 0; i + 1 < p.Length; i++) l += (p[i + 1] - p[i]).Length; return l; }

    [Fact]
    public void VoidFacesAtSolidStations_AreCounted_Longitudinal_AndCover()
    {
        var (p, _) = Model();
        var sm = Generate(p).Concrete!.Segmental!;
        var x1 = sm.Marks.Single(k => k.Mark == "X1");
        // 2 bent halves x 2 stations centred in the 5' half (0'-6" / 4'-6" from its start)
        Assert.Equal(4, x1.Count);
        Assert.Contains("not modeled - UNRESOLVED", x1.Location);
        Assert.Equal(2, sm.Bars.Count(b => b.Mark == "X1"));   // the station 4'-6" from the bent CL is hollow, the one at 0'-6" is in the diaphragm
        var b1 = sm.Bars.Where(x => x.Mark == "B1").ToList();
        Assert.Equal(5, b1.Count);
        foreach (var x in b1) { Assert.Equal(3, x.Points.Length); Assert.Equal(Ft(14) - In(4), SegmentalLength(x.Points), 2); }
        // A1 / B1 meet the printed covers (nothing short)
        Assert.DoesNotContain(sm.CoverViolations, v => v.Mark is "A1" or "B1");
    }

    [Fact]
    public void Ifc_AndJsonRoundTrip_OmittedWhenUnset()
    {
        var (p, _) = Model();
        var m = Generate(p);
        var sw = new StringWriter();
        IfcExporter.Export(sw, "seg.ifc", p, new[] { new IfcBridgeData(m.Framing, m.Steel, m.Concrete, m.Substructure, m.Prestressed) });
        Assert.Equal(m.Concrete!.Segmental!.Bars.Count, sw.ToString().Split('\n').Count(l => l.Contains("=IFCREINFORCINGBAR(")));
        var back = BridgeProject.FromJson(p.ToJson()).Bridges[0].Segmental!;
        Assert.Equal(5, back.SectionBars!.Count);
        Assert.Equal(2, back.SectionBars[2].FitLeg);
        Assert.Equal(In(3.25), back.SectionBars[0].Path[0].Offset2, 9);
        Assert.DoesNotContain("SectionBars", SegmentalTests.Model().p.ToJson());
    }
}
