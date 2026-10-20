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
    public void Laterals_EndSetbackIsAnInput_SixBoltsPerEndAndFlagged()
    {
        var (_, _, m0) = Model(b => b.Girders.Tub!.Lateral.EndSetback = In(4));
        var (_, _, m1) = Model(b => { var l = b.Girders.Tub!.Lateral; l.EndSetback = In(2.875); l.EndSetbackFlag = "UNRESOLVED (p.289): top lateral WT end setback - end setback not dimensioned; scaled."; });
        var a = m0.Parts.First(p => p.Mark.Contains("-LAT1-L1"));
        var b1 = m1.Parts.First(p => p.Mark == a.Mark);
        Assert.True(b1.Length > a.Length + In(1), "a smaller setback gives a longer member");   // 2 x (4 - 2 7/8) across the girder, a little more along the WT
        // 6 bolts per member end (Detail A), all registered as holes in the girder top flange
        string asm = a.Assembly;
        int members = m1.Parts.Count(p => p.Assembly == asm && p.Category == PartCategory.RolledTee);
        Assert.Equal(members * 12, m1.Bolts.Count(x => x.Assembly == asm));
        Assert.Null(a.Assumption);
        Assert.NotNull(b1.Assumption);
        Assert.True(b1.Assumption!.DimensionOnly);
        Assert.Contains("p.289", b1.Assumption.Reason);
        Assert.All(m1.Parts, p => Assert.True(p.Mesh.Volume() > 0, $"{p.Mark} volume {p.Mesh.Volume()}"));
    }

    private static (BridgeProject p, BridgeFraming f, SteelModel m) ModelWithConnections(Action<BridgeDefinition>? tweak = null) => Model(b =>
    {
        var t = b.Girders.Tub!;
        var sd = t.SupportDiaphragms[0];
        sd.TopConnection = new TubTopConnectionDefinition();
        sd.BottomConnection = new TubBottomConnectionDefinition();
        sd.CornerClipLength = In(3.5); sd.CornerClipHeight = In(1.5);
        sd.StrutPlate = new TubStrutPlateDefinition { Thickness = In(1.25), Width = In(8), Flag = "UNRESOLVED (p.287): strut plate - thickness and width not given." };
        sd.BearingStiffenerSpacing = Ft(2);
        sd.InteriorTopFlange = new PlateSize(In(12), In(1.5));
        t.FilletWeld = In(5.0 / 16);
        tweak?.Invoke(b);
    });

    [Fact]
    public void SupportDiaphragm_ConnectionPlatesWeldsAndFlush()
    {
        var (_, _, m) = ModelWithConnections();
        // per girder: interior-flange plate (24 holes) + exterior-flange plate (8), bottom connection plate on the side of the exterior diaphragm (16)
        var tcp = m.Parts.Where(p => p.Mark.Contains("-SD2-TCP")).ToList();
        Assert.Equal(4, tcp.Count);
        Assert.Equal(2, tcp.Count(p => p.Description.Contains("24 holes")));
        Assert.Equal(2, tcp.Count(p => p.Description.Contains("8 holes")));
        Assert.All(tcp, p => Assert.Equal(PartCategory.ConnectorPlate, p.Category));
        Assert.NotNull(tcp.First(p => p.Description.Contains("exterior flange")).Assumption);          // flange edge along the plate: not dimensioned
        var bcp = m.Parts.Where(p => p.Mark.Contains("-SD2-BCP")).ToList();
        Assert.Equal(2, bcp.Count);
        Assert.All(bcp, p => Assert.Contains("16 holes", p.Description));
        
        Assert.Equal(4, m.Parts.Count(p => p.Mark.Contains("-SD2-ST") && p.Category == PartCategory.DiaphragmPlate));
        // bolts of the plates: (24 + 8 + 16) per girder
        Assert.Equal(2 * 48, m.Bolts.Count(x => x.Assembly.EndsWith("-SD2") && x.Assembly.StartsWith("G")));
        // diaphragm flanges flush with the girder flanges: the interior top flange top = top of the girder top flange (same level as the plate seat)
        var tf = m.Parts.First(p => p.Mark == "G1-SD2-TF");
        var topPlate = m.Parts.First(p => p.Mark == "G1-SD2-TCPR");
        Assert.Equal(topPlate.Mesh.Bounds().Min.Z, tf.Mesh.Bounds().Max.Z, 1);   // top plate sits on the flange (rotated tub: within the cross slope over 6")
        // welds: web to flanges per field section, stiffeners, diaphragm webs
        Assert.Contains(m.Parts, p => p.Category == PartCategory.Weld && p.Description.Contains("flange to web"));
        Assert.Contains(m.Parts, p => p.Category == PartCategory.Weld && p.Description.Contains("transverse stiffener to web"));
        Assert.Contains(m.Parts, p => p.Category == PartCategory.Weld && p.Description.Contains("bearing stiffener to diaphragm web"));
        Assert.Contains(m.Parts, p => p.Category == PartCategory.Weld && p.Description.Contains("external diaphragm web to top flange"));
        Assert.All(m.Parts.Where(p => p.Category == PartCategory.Weld), p => Assert.True(p.Volume > 0 && p.Length > 0, p.Mark));
        // welds are not steel quantity (as for the I-girders)
        Assert.Equal(m.Parts.Where(p => p.Category != PartCategory.Weld).Sum(p => p.Mass), m.SteelMass, 3);
        Assert.All(m.Parts, p => Assert.True(p.Mesh.Volume() > 0, $"{p.Mark} volume {p.Mesh.Volume()}"));
    }

    [Fact]
    public void SkewedSupport_EndsTheGirderPlatesOnTheSupportLine()
    {
        var (_, f, m) = Model(b => b.Supports[0].SkewDegrees = 20);
        var line = f.SupportLine(0);
        var nrm = new Vec2(-line.Direction.Y, line.Direction.X);
        foreach (int gi in new[] { 0, 1 })
        {
            // end plane: parallel to the support line through the girder CL end - the plate's end vertices all lie at one distance from the support line
            var tfl = m.Parts.First(p => p.Mark == $"G{gi + 1}-1-TFL1");
            var d = tfl.Mesh.Positions.Distinct().Select(v => Vec2.Dot(v.XY - line.AlignmentPoint, nrm)).ToList();
            double lo = d.Min();
            Assert.True(d.Count(x => Math.Abs(x - lo) < In(0.25)) >= 4, $"G{gi + 1}: end vertices of the top flange on the skewed end plane");
            // a square end (SkewedEnds off) has only its two vertices per flange edge at one distance
            var square = Model(b => { b.Supports[0].SkewDegrees = 20; b.Girders.Tub!.SkewedEnds = false; }).m.Parts.First(p => p.Mark == tfl.Mark);
            var ds = square.Mesh.Positions.Distinct().Select(v => Vec2.Dot(v.XY - line.AlignmentPoint, nrm)).ToList();
            Assert.True(ds.Count(x => Math.Abs(x - ds.Min()) < In(0.25)) < 4);
        }
        Assert.All(m.Parts, p => Assert.True(p.Mesh.Volume() > 0, $"{p.Mark} volume {p.Mesh.Volume()}"));
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

    /// <summary>PROGRAM DEFECT regression (ODOT Bridge O QC back-check I13 / C171): `sbm elevations` built a tub's bearing chain from the default
    /// bearing / sole plate instead of the MODELED tub bearings (beveled plate J + pad H, p.292-293), so its deck-to-seat depth disagreed with the
    /// 3D seat (and with the plan H dimensions) by 1" to 2 7/16". The chain must use the tub bearing's plate and pad and agree with the 3D model.</summary>
    [Fact]
    public void Elevations_UseTheModeledTubBearings()
    {
        var (p, f, m) = Model(b =>
        {
            foreach (int j in new[] { 0, 1, 2 })
                b.Girders.Tub!.Bearings.Add(new TubBearingDefinition
                {
                    Support = j, Mark = $"P{j + 1}", PadLength = In(20), PadWidth = In(30), PadHeight = In(3.75), InteriorLayers = 5, InteriorLayer = In(0.5), Laminates = 6, ExteriorLayer = In(0.25),
                    PlateThickness = In(2.5), PlateWidth = In(31), PlateLength = In(21),
                });
        });
        Assert.NotEmpty(m.BearingSeats);
        var chains = SBM.Core.Checks.ElevationCheck.AllBearings(f, m, p.Units);
        Assert.Equal(6, chains.Count);
        foreach (var ch in chains)
        {
            Assert.False(ch.Mismatch, ch.Title + ": " + string.Join("; ", ch.Checks));
            Assert.Contains(ch.Checks, c => c.StartsWith("Bearing seat matches the 3D model"));
            Assert.Contains(ch.Steps, s => s.Item.Contains("Beveled bearing plate") && Math.Abs(s.Change + In(2.5)) < 1e-9);
            Assert.Contains(ch.Steps, s => s.Item.Contains("Elastomeric pad") && Math.Abs(s.Change + In(3.75)) < 1e-9);
        }
    }
}
