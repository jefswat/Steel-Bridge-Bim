using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Steel;
using SBM.Core.Substructure;
using Xunit;

namespace SBM.Tests;

/// <summary>Features added for the Nebraska St. Ramp B QC answers (2026-10-19): plan stud zones, flange deflectors, battered abutment piles, one-sided column
/// projection. All OPTIONAL: defaults leave other bridges unchanged.</summary>
public class QcNebraskaAnswersTests
{
    private static double Ft(double x) => x * 0.3048;
    private static double In(double x) => x * 0.0254;

    [Fact]
    public void StudZones_PlaceEqualSpacesAndShareTheJoiningRowOnce()
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        b.Studs.RowsAcross = 3;
        b.Studs.Zones = new()
        {
            new StudZone { From = new StudLocation(StudLocationKind.GirderStart, 0, Ft(1)), To = new StudLocation(StudLocationKind.GirderStart, 0, Ft(6)), Spaces = 5 },
            new StudZone { From = new StudLocation(StudLocationKind.GirderStart, 0, Ft(6)), To = new StudLocation(StudLocationKind.GirderStart, 0, Ft(16)), Spaces = 5 },
        };
        doc.Regenerate();
        var st = doc.Model().Steel!;
        for (int g = 0; g < b.Girders.Count; g++)
        {
            int n = st.Studs.Count(s => s.Assembly.StartsWith($"G{g + 1}-"));
            Assert.Equal(11 * 3, n);   // 6 + 6 rows, the row at 6 ft once
        }
    }

    [Fact]
    public void StudZones_AtASupport_StayOnTheGirder()
    {
        // regression: a Support location took the span LENGTH as the span fraction, so the zone ran kilometres past the girder end
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        b.Studs.RowsAcross = 1;
        b.Studs.Zones = new()
        {
            new StudZone { From = new StudLocation(StudLocationKind.Support, 1, -Ft(10)), To = new StudLocation(StudLocationKind.Support, 1, -Ft(2)), Spaces = 4 },
        };
        doc.Regenerate();
        var m = doc.Model();
        var g = m.Framing.Girders[0];
        double atSupport = g.DistanceAt(0, 1.0);
        var rows = m.Steel!.Studs.Where(s => s.Assembly.StartsWith("G1-")).Select(s => s.Frame.Origin).ToList();
        Assert.Equal(5, rows.Count);
        var (p, _, _) = g.Path.At(atSupport);
        foreach (var o in rows)
        {
            double d = Math.Sqrt((o.X - p.X) * (o.X - p.X) + (o.Y - p.Y) * (o.Y - p.Y));
            Assert.InRange(d, Ft(2) - 0.01, Ft(10) + 0.01);
        }
    }

    [Fact]
    public void StudEdgeDistance_MakesTheTransverseSpacingWidthDependent()
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        b.Studs.RowsAcross = 3; b.Studs.EdgeDistance = In(1.5);
        doc.Regenerate();
        var st = doc.Model().Steel!;
        var first = st.Studs.Where(s => s.Assembly.StartsWith("G1-")).Take(3).ToList();   // the first row (one row per iteration, rows in order)
        Assert.Equal(3, first.Count);
        double w = b.Girders.Section.TopFlange.Width;
        double span = (first[2].Frame.Origin.XY - first[0].Frame.Origin.XY).Length;
        Assert.InRange(span, (w - In(3)) - In(0.05), (w - In(3)) + In(0.05));
    }

    [Fact]
    public void FlangeDeflectors_AreBuiltOnBothExteriorGirders_AndRegisterBoltHoles()
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        b.FlangeDeflectors = new FlangeDeflectorDefinition { Supports = new() { 1 } };
        doc.Regenerate();
        var st = doc.Model().Steel!;
        Assert.Equal(2, st.Assemblies.Count(a => a.Kind == AssemblyKind.FlangeDeflector));
        Assert.Equal(2, st.Parts.Count(p => p.Category == PartCategory.Angle && p.Assembly.Contains("-FD")));
        Assert.Equal(2, st.Parts.Count(p => p.Mark.EndsWith("-PL") && p.Assembly.Contains("-FD")));
        Assert.All(st.Parts.Where(p => p.Assembly.Contains("-FD")), p => Assert.NotNull(p.Assumption));
        Assert.True(st.Parts.Where(p => p.Assembly.Contains("-FD")).All(p => Math.Abs(p.Mesh.Volume()) > 0));
        // removing the option restores the model exactly
        b.FlangeDeflectors = null;
        doc.Regenerate();
        Assert.DoesNotContain(doc.Model().Steel!.Assemblies, a => a.Kind == AssemblyKind.FlangeDeflector);
    }

    [Fact]
    public void ColumnProjectionBack_AddsOneFaceProjection()
    {
        var sd = SubstructureDefinition.Default(SupportType.Pier);
        sd.ColumnShape = ColumnShape.Rectangular; sd.ColumnWidth = Ft(5.97); sd.ColumnDepth = Ft(7.31);
        sd.ColumnProjectionS = In(4); sd.ColumnProjectionWidth = Ft(5.97);
        var o0 = SubstructureGenerator.ColumnSectionOutline(sd);
        Assert.Equal(12, o0.Length);
        sd.ColumnProjectionBack = In(4); sd.ColumnProjectionBackWidth = Ft(3.98);
        var o = SubstructureGenerator.ColumnSectionOutline(sd);
        Assert.Equal(16, o.Length);
        Assert.Equal(sd.ColumnDepth + In(4), o.Max(q => q.Y) - o.Min(q => q.Y), 6);
        Assert.Equal(sd.ColumnWidth + 2 * In(4), o.Max(q => q.X) - o.Min(q => q.X), 6);
        Assert.Equal(o0.Max(q => q.Y), o.Max(q => q.Y), 9);   // the projection is on the -N face only
    }

    [Fact]
    public void AbutmentPileBatter_BattersTheFrontRowOutward()
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        var u0 = doc.Substructure().Units.First(u => u.SupportIndex == 0);
        var al = doc.Alignment();
        double Off(double s) => al.Inverse(u0.Plan(s, 0)).Offset;
        double sMin = u0.Pedestals.Min(p => p.S) - Ft(3), sMax = u0.Pedestals.Max(p => p.S) + Ft(3);
        double zf = u0.Pedestals.Min(p => p.SeatZ) - Ft(5);
        var ad = new AbutmentDefinition { LeftEndOffset = Off(sMin), RightEndOffset = Off(sMax), PlanFootingWidth = In(80.5), BackwallFrontToBearing = In(22) };
        ad.FootingBottom.Add(new CapProfilePoint(0, zf));
        ad.BackwallTop.Add(new CapProfilePoint(sMin, zf + Ft(11))); ad.BackwallTop.Add(new CapProfilePoint(sMax, zf + Ft(11.5)));
        foreach (var p in u0.Pedestals) ad.Steps.Add(new AbutmentStepDefinition(p.Girder + 1, p.SeatZ));
        for (int i = 0; i < 5; i++) ad.PileStations.Add(sMin + Ft(4) + Ft(6.8) * i);
        ad.PileBatterRatio = 4; ad.PileBatterRows = new() { 1 };
        var sd = b.Supports[0].ResolvedSubstructure.Clone();
        sd.Abutment = ad; sd.PileSection = "HP14x73";
        b.Supports[0].Substructure = sd;
        doc.Regenerate();
        var u = doc.Substructure().Units.First(x => x.SupportIndex == 0);
        var piles = u.Foundations.SelectMany(f => f.Piles).ToList();
        Assert.Equal(10, piles.Count);
        var bat = piles.Where(p => p.IsBattered).ToList();
        Assert.Equal(5, bat.Count);
        // the tip moves toward the span (+N at the first support), away from the CL bearing; the plain row stays plumb
        Assert.All(bat, p => Assert.True(p.TipN > p.N + 0.1));
        Assert.All(piles.Where(p => !p.IsBattered), p => Assert.Equal(p.N, p.TipN, 6));
    }
}
