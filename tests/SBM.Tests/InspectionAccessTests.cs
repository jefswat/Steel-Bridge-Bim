using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Steel;
using SBM.IO.Ifc;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Inspection cables on WT brackets and walkways (I-80 sheets 159-170, Ramp G 130-134).</summary>
public class InspectionAccessTests
{
    private static SbmDocument Doc(WalkwayType type = WalkwayType.A, bool cables = true)
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        b.Girders.Section.WebDepth = In(96);
        var ia = new InspectionAccessDefinition { DefaultWalkway = type };
        if (cables)
        {
            ia.Cables.Add(new InspectionCableDefinition { Girder = 0, LeftFace = true });
            ia.Cables.Add(new InspectionCableDefinition { Girder = b.Girders.Count - 1, LeftFace = false });
        }
        b.InspectionAccess = ia;
        return doc;
    }

    [Fact]
    public void NoInspectionAccess_ByDefault()
    {
        var doc = SbmDocument.Sample();
        Assert.Null(doc.Bridge().InspectionAccess);
        doc.Regenerate();
        Assert.DoesNotContain(doc.Steel().Parts, p => p.Category >= PartCategory.InspectionBracket);
    }

    [Fact]
    public void Cable_HasRopeAtThePlanHeightAndBracketsBoltedThroughTheWeb()
    {
        var doc = Doc(WalkwayType.None);
        Assert.Empty(doc.Regenerate().Errors);
        var steel = doc.Steel();
        Assert.Equal(2, steel.Assemblies.Count(a => a.Kind == AssemblyKind.InspectionCable));
        var rope = steel.Parts.Single(p => p.Mark == "IC1-R");
        Assert.Equal(PartCategory.InspectionCable, rope.Category);
        var brackets = steel.Parts.Where(p => p.Assembly == "IC1" && p.Category == PartCategory.InspectionBracket).ToList();
        int anchors = brackets.Count(p => p.Description.StartsWith("WT5x44"));
        int supports = brackets.Count(p => p.Description.StartsWith("WT5x15"));
        Assert.True(anchors >= 2);                                            // end anchorages at least
        Assert.True(supports >= 5);                                           // a bracket at the cross frame stiffeners in between
        var bolts = steel.Bolts.Where(b => b.Assembly == "IC1").ToList();
        Assert.Equal(6 * anchors + 4 * supports, bolts.Count);
        Assert.All(bolts, b => Assert.Equal(In(0.875), b.Diameter, 6));
        // Rope 4'-6 above the bottom of the web: at the first bracket its z is web bottom + 54".
        var f = doc.Framing();
        var path = f.Girders[0].Path;
        var rb = rope.Mesh.Bounds();
        var first = brackets[0].Mesh.Bounds();
        var c = (first.Min + first.Max) * 0.5;
        int i = Enumerable.Range(0, path.Count).MinBy(k => (path.WebTop[k].XY - c.XY).LengthSquared);
        double webBottom = path.WebTop[i].Z - In(96);
        Assert.InRange(c.Z - webBottom, Ft(4.5) - In(1.5), Ft(4.5) + In(1.5));
        Assert.True(rb.Max.Z - rb.Min.Z > 0);
    }

    [Fact]
    public void BracketBolts_CutHolesInTheGirderWeb()
    {
        double WebVolume(SbmDocument d) => d.Steel().Parts.Where(p => p.Category == PartCategory.WebPlate).Sum(p => p.Volume);
        var with = Doc(WalkwayType.None);
        with.Regenerate();
        var without = SbmDocument.Sample();
        without.Bridge().Girders.Section.WebDepth = In(96);
        without.Regenerate();
        int n = with.Steel().Bolts.Count(b => b.Assembly.StartsWith("IC"));
        double hd = new BoltGeometry(In(0.875)).HoleDiameter;
        double tw = with.Bridge().Girders.Section.WebThickness;
        double removed = WebVolume(without) - WebVolume(with);
        // The two exterior girders may have different web thicknesses along the run: within 40% of n * hole area * the section's web.
        Assert.InRange(removed, 0.6 * n * Math.PI / 4 * hd * hd * tw, 1.6 * n * Math.PI / 4 * hd * hd * tw);
    }

    [Fact]
    public void Walkway_StringersBySpan_PostsAtMost8ft6_AndTypeBHasNoToeBoard()
    {
        var a = Doc(WalkwayType.A, cables: false);
        a.Regenerate();
        var sa = a.Steel();
        var segs = sa.Assemblies.Where(x => x.Kind == AssemblyKind.InspectionWalkway).ToList();
        Assert.True(segs.Count > 20);
        foreach (var seg in segs)
        {
            var parts = sa.Parts.Where(p => p.Assembly == seg.Mark).ToList();
            double ls = parts.Single(p => p.Category == PartCategory.Grating).Length;
            string expected = ls > Ft(25) ? "C7x14.75" : "C7x12.25";
            Assert.All(parts.Where(p => p.Category == PartCategory.WalkwayStringer), p => Assert.Equal(expected, p.Description));
            int posts = parts.Count(p => p.Category == PartCategory.WalkwayPost);
            int spaces = posts / 2 - 1;
            Assert.True(ls / spaces <= Ft(8.5) + 1e-6);
            Assert.Equal(2, parts.Count(p => p.Mark.Contains("-TB")));                       // Type A has toe boards
        }
        var b = Doc(WalkwayType.B, cables: false);
        b.Regenerate();
        var sb = b.Steel();
        Assert.DoesNotContain(sb.Parts, p => p.Mark.Contains("-TB"));
        Assert.Contains(sb.Parts, p => p.Category == PartCategory.WalkwayStringer && p.Description == "L8x4x1/2");
        Assert.Contains(sb.Parts, p => p.Category == PartCategory.Angle && p.Description.StartsWith("L3-1/2x3-1/2x5/16 diaphragm"));
    }

    [Fact]
    public void Walkway_SitsBetweenTheGirdersAtTheGratingLevel()
    {
        var doc = Doc(WalkwayType.A, cables: false);
        doc.Regenerate();
        var f = doc.Framing();
        var cf = f.CrossFrames.Where(c => c.Bay == 1).OrderBy(c => c.LeftGlobalDistance).ToList();
        var steel = doc.Steel();
        var gr = steel.Parts.Single(p => p.Mark == "IW-BC-1-GR");                           // between the first two frame lines of bay G2-G3
        var gb = gr.Mesh.Bounds();
        double zA = ((cf[0].Left + cf[0].Right) * 0.5).Z - In(96) + In(2.5), zB = ((cf[1].Left + cf[1].Right) * 0.5).Z - In(96) + In(2.5);
        // Grating top 2 1/2" above the bottom of the web (mean of both girders) at both ends, following the grade between.
        Assert.InRange(gb.Max.Z, Math.Max(zA, zB) - In(2), Math.Max(zA, zB) + In(1));
        Assert.InRange(gb.Min.Z, Math.Min(zA, zB) - In(3.5), Math.Min(zA, zB));
    }

    [Fact]
    public void DefinitionRoundTrips_AndCloneIsDeep()
    {
        var doc = Doc(WalkwayType.B);
        doc.Bridge().InspectionAccess!.Walkways.Add(new InspectionWalkwayDefinition { Bay = 2, Type = WalkwayType.None, FirstLine = 3 });
        var p2 = doc.Project.DeepClone();
        var ia = p2.Bridges[0].InspectionAccess!;
        Assert.Equal(WalkwayType.B, ia.DefaultWalkway);
        Assert.Equal(2, ia.Cables.Count);
        Assert.Equal(3, ia.Walkways.Single().FirstLine);
        Assert.Equal("L8x4x1/2", ia.TypeB.Stringer);
        var c = doc.Bridge().Clone();
        c.InspectionAccess!.Cables[0].Girder = 3;
        Assert.Equal(0, doc.Bridge().InspectionAccess!.Cables[0].Girder);
    }

    [Fact]
    public void Ifc_ExportsRopeBracketsWalkwayAsValidEntities()
    {
        var doc = Doc(WalkwayType.A);
        doc.Regenerate();
        var p = doc.Project;
        var f = doc.Framing();
        var sw = new StringWriter();
        IfcExporter.Export(sw, "t.ifc", p, new[] { new IfcBridgeData(f, doc.Steel()) });
        string ifc = sw.ToString();
        foreach (var e in new[] { ".STRUCTURALCABLE.", ".STRINGER.", ".POST.", ".HANDRAIL.", "IFCRAILING(", "'CABLE_BRACKET'", "'GRATING'", "'WALKWAY_PLATE'", ".ACCESSORY_ASSEMBLY." })
            Assert.Contains(e, ifc);
        // 7/8" bracket bolts and 1" ... have their own fastener type (one IfcMechanicalFastenerType per size).
        Assert.Matches(@"IFCMECHANICALFASTENERTYPE\([^\n]*7/8", ifc);
    }
}
