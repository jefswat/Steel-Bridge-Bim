using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Steel;
using SBM.IO.Ifc;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Steel tub girders (OPTIONAL; GirderLayoutDefinition.Tub): section math, generated bracing, IFC entities, and that I-girder
/// bridges are untouched. Section values are those of ODOT Bridge O spans 14-16 (p.273: 50" webs at 4:1, 8'-0 between the top working
/// points, 77" bottom flange, 2 x 21" top flanges).</summary>
public class TubGirderTests
{
    private static TubGirderDefinition Section() => new() { TopWebSpacing = Ft(8), WebSlope = 0.25, RotateWithDeck = true };

    /// <summary>The sample bridge (2 spans, 3 supports) turned into 2 tubs 17'-8 apart, 10 equal bracing panels per girder and span.</summary>
    internal static (BridgeProject p, BridgeFraming f, SteelModel m) Model(Action<BridgeDefinition>? tweak = null)
    {
        var p = SampleProject.Create();
        var b = p.Bridges[0];
        foreach (var s in b.Supports) s.SkewDegrees = 0;
        b.LateralBracing.Enabled = false;
        b.Girders.Count = 2;
        b.Girders.Spacings = new() { Ft(17) + In(8) };
        b.Girders.FirstGirderOffset = -(Ft(8) + In(10));
        var sec = b.Girders.Section;
        sec.WebDepth = In(50);
        sec.WebThickness = In(0.625);
        sec.TopFlange = new PlateSize(In(21), In(1.25));
        sec.BottomFlange = new PlateSize(In(77), In(1.25));
        sec.PlateChanges = new();
        var t = Section();
        for (int g = 0; g < 2; g++)
            for (int j = 0; j < 2; j++)
                t.Panels.Add(new TubPanelSegment { Girder = g, Span = j, Spaces = 10 });
        t.External.Points.Add(new TubPanelRef(0, 4));
        t.SupportDiaphragms.Add(new TubSupportDiaphragmDefinition { Support = 1, AccessOpeningWidth = Ft(1.5), AccessOpeningHeight = Ft(2) + In(8), AccessOpeningRadius = In(8), AccessOpeningBottom = In(11) });
        t.Hatches.Add(new TubAccessHatchDefinition { Girder = 0, At = TubLocation.AtSupport(0, Ft(5)) });
        b.Girders.Tub = t;
        tweak?.Invoke(b);
        var f = FramingGenerator.Generate(b, p.Alignments[0]);
        Assert.False(f.HasErrors, string.Join("\n", f.Diagnostics));
        return (p, f, SteelModelGenerator.Generate(f, p.Units));
    }

    [Fact]
    public void Section_WebSlopeAndPlateAreas()
    {
        var t = Section();
        double D = In(50);
        // 4 vertical : 1 horizontal - web CL at the bottom 2'-11 1/2" from the tub CL (p.273), 3" inside the 77" bottom flange edge.
        Assert.Equal(In(35.5), TubSection.WebX(t, 1, -D), 9);
        Assert.Equal(-In(35.5), TubSection.WebX(t, -1, -D), 9);
        Assert.Equal(In(48), TubSection.WebX(t, 1, 0), 9);                       // top working point of the web
        Assert.Equal(In(77) / 2 - In(3), TubSection.WebX(t, 1, -D), 9);
        // Web length on the slope 4'-3 9/16" (plan, rounded to 1/16): 50 x sqrt(17) / 4.
        double L = TubSection.WebSlopeLength(t, D);
        Assert.Equal(In(50) * Math.Sqrt(1.0625), L, 12);
        Assert.True(Math.Abs(L - In(51.5625)) < In(1.0 / 32));
        var down = TubSection.WebDown(t, 1);
        Assert.Equal(-0.25, down.X / -down.Y, 12);                                  // 1 horizontal (inward) per 4 down
        Assert.Equal(L, (TubSection.WebPoint(t, 1, L, 0) - new Vec2(In(48), 0)).Length, 12);
        Assert.Equal(-D, TubSection.WebPoint(t, 1, L, 0).Y, 9);
        Assert.Equal(0, Vec2.Dot(TubSection.WebOut(t, 1), down), 12);
        // Horizontal width of a 5/8" web: tw x sqrt(1 + 1/16).
        Assert.Equal(In(0.625) * Math.Sqrt(1.0625), TubSection.HorizontalThickness(t, In(0.625)), 12);
        // Area: 2 PL 21 x 1 1/4 + PL 77 x 1 1/4 + 2 webs L x 5/8.
        double a = TubSection.Area(t, D, In(21), In(1.25), In(0.625), In(77), In(1.25));
        Assert.Equal(2 * 21 * 1.25 + 77 * 1.25 + 2 * 50 * Math.Sqrt(1.0625) * 0.625, a / (In(1) * In(1)), 9);
    }

    [Fact]
    public void Frame_RotatesAboutTheTopWorkingPoint()
    {
        var wp = new Vec3(10, 20, 30);
        var f = TubFrame.Create(wp, new Vec2(1, 0), 0.06);
        Assert.Equal(0, (f.At(0, 0) - wp).Length, 12);                             // the working point does not move
        Assert.Equal(0, Vec3.Dot(f.Lateral, f.Up), 12);
        Assert.Equal(1, f.Lateral.Length, 12);
        Assert.Equal(1, f.Up.Length, 12);
        Assert.Equal(0.06, f.Lateral.Z / Math.Sqrt(f.Lateral.X * f.Lateral.X + f.Lateral.Y * f.Lateral.Y), 12);   // lateral axis in the deck plane
        Assert.Equal(Math.Cos(Math.Atan(0.06)), f.Up.Z, 12);
        // the bottom of the tub CL lies off the working point's vertical by D sin(theta), toward the high side (normal to the deck)
        double D = In(50);
        var bot = f.At(0, -D);
        Assert.Equal(D * Math.Sin(Math.Atan(0.06)), Vec2.Dot((bot - wp).XY, new Vec2(1, 0).Right), 9);
        Assert.Equal(-D * Math.Cos(Math.Atan(0.06)), bot.Z - wp.Z, 9);
    }

    [Fact]
    public void Generated_BracingCountsAndPlates()
    {
        var (_, f, m) = Model();
        var tr = Assert.IsType<TubGirderReport>(m.Tub);
        Assert.DoesNotContain(m.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        // 2 girders x 2 spans x 10 spaces: 9 interior panel points each; K frames at the even points (2, 4, 6, 8).
        Assert.Equal(36, tr.PanelPoints.Count);
        Assert.Equal(36, tr.TopStruts);
        Assert.Equal(16, tr.InternalCrossFrames);
        Assert.Equal(40, tr.LateralMembers);                                           // one top lateral per panel
        Assert.Equal(1, tr.ExternalDiaphragms);
        Assert.Equal(1, tr.SupportDiaphragms);
        Assert.Equal(1, tr.Hatches);
        Assert.True(tr.Stiffeners >= 2 * 36, $"{tr.Stiffeners} stiffeners");                // at least one per web at every panel point
        // Panel points equally spaced between the CL bearings.
        foreach (var grp in tr.PanelPoints.GroupBy(p => (p.Girder, p.Span)))
        {
            double a = tr.BearingS[grp.Key.Girder][grp.Key.Span], b = tr.BearingS[grp.Key.Girder][grp.Key.Span + 1];
            int k = 1;
            foreach (var pp in grp.OrderBy(p => p.S)) Assert.Equal(a + k++ * (b - a) / 10, pp.S, 6);
        }
        // Plates: 2 top flanges + 2 webs + 1 bottom flange per field section, all closed solids; web pieces on the slope.
        var sections = m.Assemblies.Where(a => a.Kind == AssemblyKind.GirderFieldSection).Select(a => a.Mark).ToHashSet();
        Assert.Equal(sections.Count * 2, m.Parts.Count(p => p.Category == PartCategory.WebPlate && sections.Contains(p.Assembly)));
        Assert.Equal(sections.Count, m.Parts.Count(p => p.Category == PartCategory.FlangePlate && sections.Contains(p.Assembly) && p.Description.StartsWith("Bottom")));
        Assert.All(m.Parts, p => Assert.True(p.Mesh.Volume() > 0, $"{p.Mark} volume {p.Mesh.Volume()}"));
        Assert.Contains(m.Parts, p => p.Category == PartCategory.RolledTee);
        Assert.Contains(m.Parts, p => p.Category == PartCategory.AccessDoor);
        // Web plate volume = slope length x thickness x length (straight girders on a tangent: exact up to the hole-free extrusion).
        var web = m.Parts.First(p => p.Category == PartCategory.WebPlate && p.Mark.Contains("-WL"));
        double expect = TubSection.WebSlopeLength(Section(), In(50)) * In(0.625) * web.Length;
        Assert.Equal(1, web.Mesh.Volume() / expect, 2);
    }

    [Fact]
    public void Ifc_TubEntities()
    {
        var (p, f, m) = Model();
        var sw = new StringWriter();
        IfcExporter.Export(sw, "tub.ifc", p, new[] { new IfcBridgeData(f, m) });
        string ifc = sw.ToString();
        var lines = ifc.Split('\n');
        // WT top struts are the frames' top chords (as I-girder cross frame top struts), WT top laterals braces.
        var strut = m.Parts.First(x => x.Category == PartCategory.RolledTee && x.Description.StartsWith("Top strut"));
        Assert.Contains(lines, l => l.Contains("IFCMEMBER(") && l.Contains($"'{strut.Mark}'") && l.Contains(".CHORD."));
        var lat = m.Parts.First(x => x.Category == PartCategory.RolledTee && x.Description.StartsWith("Top lateral"));
        Assert.Contains(lines, l => l.Contains("IFCMEMBER(") && l.Contains($"'{lat.Mark}'") && l.Contains(".BRACE."));
        var door = m.Parts.First(x => x.Category == PartCategory.AccessDoor);
        Assert.Contains(lines, l => l.Contains("IFCPLATE(") && l.Contains($"'{door.Mark}'") && l.Contains("'ACCESS_DOOR'") && l.Contains(".USERDEFINED."));
        var fl = m.Parts.First(x => x.Category == PartCategory.FlangePlate);
        Assert.Contains(lines, l => l.Contains("IFCPLATE(") && l.Contains($"'{fl.Mark}'") && l.Contains(".FLANGE_PLATE."));
    }

    [Fact]
    public void IGirderBridges_AreUnchanged()
    {
        var p = SampleProject.Create();
        Assert.Null(p.Bridges[0].Girders.Tub);
        Assert.DoesNotContain("\"Tub\"", p.ToJson());
        var f = FramingGenerator.Generate(p.Bridges[0], p.Alignments[0]);
        var m = SteelModelGenerator.Generate(f, p.Units);
        Assert.Null(m.Tub);
        Assert.DoesNotContain(m.Parts, x => x.Category is PartCategory.RolledTee or PartCategory.AccessDoor);
        Assert.NotEmpty(f.CrossFrames);
        // A tub round-trips through the project file and gives the same model.
        var (tp, _, tm) = Model();
        var p2 = BridgeProject.FromJson(tp.ToJson());
        Assert.NotNull(p2.Bridges[0].Girders.Tub);
        var f2 = FramingGenerator.Generate(p2.Bridges[0], p2.Alignments[0]);
        var m2 = SteelModelGenerator.Generate(f2, p2.Units);
        Assert.Equal(tm.Parts.Count, m2.Parts.Count);
        Assert.Equal(tm.SteelMass, m2.SteelMass, 6);
    }
}
