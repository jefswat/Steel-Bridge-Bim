using SBM.Api;
using SBM.Core.Bridge;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Units of one structure meeting at a shared expansion pier.</summary>
public class UnitsTests
{
    private static SbmDocument TwoUnits(double gap = 0)
    {
        var doc = SbmDocument.Sample();
        var u1 = doc.Bridge();
        u1.Name = "Unit 1";
        u1.Structure = "Viaduct";
        u1.FieldSplices.Clear();
        // Unit 1 ends at Pier 1 (its CL bearing 1'-3" back of the CL pier); unit 2 starts 1'-3" ahead.
        double pierSta = u1.Supports[1].Station;
        double off = Ft(1.25);
        u1.Supports.RemoveAt(2);
        u1.Supports[1].Station = pierSta - off;
        u1.Supports[1].SharedPier = "Pier 1";
        u1.Supports[1].PierCenterOffset = off;
        u1.Supports[1].GirderEndExtension = In(9);
        var u2 = u1.Clone();
        u2.Name = "Unit 2";
        u2.Supports.Clear();
        u2.Supports.Add(new SupportDefinition { Name = "Pier 1", Type = SupportType.Pier, Station = pierSta + off, SharedPier = "Pier 1", PierCenterOffset = -off });
        u2.Supports.Add(new SupportDefinition { Name = "Abut 2", Type = SupportType.Abutment, Station = pierSta + Ft(140) });
        u2.Girders.Count = 4;
        u2.Girders.Spacings = Enumerable.Repeat(Ft(10), 3).ToList();
        u2.Girders.FirstGirderOffset = Ft(-15);
        doc.Project.Bridges.Add(u2);
        foreach (var b in doc.Project.Bridges)
            foreach (var s in b.Supports) s.Substructure = SubstructureDefinition.Default(s.Type);
        return doc;
    }

    [Fact]
    public void SharedPier_IsBuiltOnce_WithBothUnitsBearings()
    {
        var doc = TwoUnits();
        var r = doc.Regenerate();
        Assert.Empty(r.Errors);
        var m1 = doc.Model("Unit 1");
        var m2 = doc.Model("Unit 2");
        var pier1 = m1.Substructure!.Units.Where(u => u.Name == "Pier 1").ToList();
        Assert.Single(pier1);
        Assert.DoesNotContain(m2.Substructure!.Units, u => u.Name == "Pier 1");
        // 5 girders of unit 1 + 4 of unit 2.
        Assert.Equal(9, pier1[0].Pedestals.Count);
        Assert.Contains(pier1[0].Solids, s => s.Name.Contains("Unit 2 G4"));
        // Cap centered on the CL pier: half way between the two bearing lines.
        Assert.Equal(Ft(1.25), (pier1[0].CapN0 + pier1[0].CapN1) / 2, 3);
        // Girders run past the CL bearing at the expansion pier.
        Assert.Equal(In(9), m1.Framing.Girders[0].Length - m1.Framing.Girders[0].Spans.Sum(s => s.Length) - m1.Framing.Girders[0].StartExtension, 3);
        Assert.Empty(doc.Steel("Unit 2").Diagnostics.Where(d => d.Severity == SBM.Core.Framing.DiagnosticSeverity.Error));
    }

    [Fact]
    public void SharedPier_UpdatesWhenTheOtherUnitChanges()
    {
        var doc = TwoUnits();
        doc.Regenerate();
        Assert.Equal(9, doc.Model("Unit 1").Substructure!.Units.Single(u => u.Name == "Pier 1").Pedestals.Count);
        doc.Bridge("Unit 2").Girders.Count = 3;
        doc.Bridge("Unit 2").Girders.Spacings = Enumerable.Repeat(Ft(10), 2).ToList();
        doc.Regenerate();
        Assert.Equal(8, doc.Model("Unit 1").Substructure!.Units.Single(u => u.Name == "Pier 1").Pedestals.Count);
    }

    [Fact]
    public void VariableWidthDeck_WidensWithTheTable()
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        double s0 = b.Supports[0].Station, s2 = b.Supports[^1].Station;
        b.Deck.LeftEdgeTable = new() { new DeckEdgePoint { Station = s0, Offset = Ft(-21.5) }, new DeckEdgePoint { Station = s2, Offset = Ft(-31.5) } };
        Assert.Empty(doc.Regenerate().Errors);
        var c = doc.Concrete();
        var wide = doc.Model().Framing.Deck!;
        // Area grows by half of 10 ft × length (trapezoid) relative to the constant deck.
        var doc2 = SbmDocument.Sample();
        doc2.Regenerate();
        double extra = wide.Area - doc2.Model().Framing.Deck!.Area;
        double len = (s2 - s0);
        Assert.InRange(extra, 0.4 * Ft(10) * len, 0.6 * Ft(10) * len);
        Assert.True(c.DeckVolume > doc2.Concrete().DeckVolume * 1.05);
        Assert.Equal(2, c.Barriers.Count);
    }

    [Fact]
    public void DiscontinuousGirder_EndsOnACrossGirder_AndBaysBridgeIt()
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        b.Girders.Terminations.Add(new GirderTermination { Girder = 3, Support = 1, Distance = Ft(60) });
        Assert.Empty(doc.Regenerate().Errors);
        var f = doc.Framing();
        var full = f.Girders[2].Length;
        Assert.True(f.Girders[3].Length < full - Ft(70));
        Assert.Null(f.Supports[2].Bearings[3]);
        var xg = Assert.Single(f.CrossGirders);
        Assert.Equal((2, 4), (xg.LeftGirder, xg.RightGirder));
        // Cross frames bridge G3-G5 past the end, none use G4 there.
        Assert.Contains(f.CrossFrames, c => c.Bay == 2 && c.RightGirder == 4);
        Assert.DoesNotContain(f.CrossFrames, c => (c.Bay == 3 || c.RightGirder == 3) && (c.Bay == 3 ? c.LeftGlobalDistance : c.RightGlobalDistance) > f.Girders[3].Length);
        Assert.Contains(doc.Steel().Parts, p => p.Assembly == "XG-G4");
        Assert.DoesNotContain(doc.Steel().BearingSeats, s => s.Girder == 3 && s.Support == f.Supports[2].Name);
    }

    [Fact]
    public void DeckDrains_MakeInletsAndDownspouts()
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        foreach (var s in b.Supports) s.Substructure = SubstructureDefinition.Default(s.Type);
        double s0 = b.Supports[0].Station;
        b.Drains.Add(new DeckDrainDefinition { Station = s0 + Ft(50), Left = true });
        b.Drains.Add(new DeckDrainDefinition { Station = s0 + Ft(100), Left = false, Outlet = DrainOutlet.PipedToPier });
        Assert.Empty(doc.Regenerate().Errors);
        var d = doc.Substructure().Drainage;
        Assert.Equal(2, d.Count(x => x.Kind == SBM.Core.Substructure.SubElementKind.DeckDrain));
        Assert.Equal(4, d.Count(x => x.Kind == SBM.Core.Substructure.SubElementKind.DrainPipe));
    }
}
