using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Checks;
using SBM.Core.Substructure;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>The elevation chain below the bearing seat (pedestal → as-designed cap → column → footing → pile head) reproduces the
/// substructure generator from the inputs, for an as-designed stepped hammerhead and for shared pedestal blocks.</summary>
public class ElevationCheckSubstructureTests
{
    private static void AssertRunningSum(ElevationChain c)
    {
        for (int i = 1; i < c.Steps.Count; i++)
        {
            var s = c.Steps[i];
            if (double.IsNaN(s.Change)) continue;
            var prev = c.Steps.Take(i).Last(p => !p.Item.StartsWith("(info)"));
            Assert.Equal(prev.Elevation + s.Change, s.Elevation, 9);
        }
    }

    [Fact]
    public void Hammerhead_SteppedAsDesignedCap_ChainMatchesGenerator()
    {
        var doc = SbmDocument.Sample();
        var pierDef = doc.Bridge().Supports.First(s => s.Type == SupportType.Pier);
        var u0 = doc.Substructure().Units.Single(u => u.Type == SupportType.Pier);
        // Top line through the outer seats, 1'-6 below them (back half) and 1'-3 below (stepped ahead half).
        var peds = u0.Pedestals.OrderBy(p => p.RowS).ToList();
        double m = (peds[^1].SeatZ - peds[0].SeatZ) / (peds[^1].RowS - peds[0].RowS);
        double Line(double s) => peds[0].SeatZ + m * (s - peds[0].RowS);
        double off = peds.Max(p => Line(p.RowS) - p.SeatZ) + Ft(1.5);
        double s0 = u0.CapS0 - Ft(1), s1 = u0.CapS1 + Ft(1), sc = (s0 + s1) / 2;
        double zb = Math.Min(Line(s0), Line(s1)) - off - Ft(9);
        List<CapProfilePoint> Poly(double lift) => new()
        {
            new(s0, Line(s0) - off + lift), new(s1, Line(s1) - off + lift), new(s1, Line(s1) - off + lift - Ft(4.5)),
            new(sc + Ft(5), zb), new(sc - Ft(5), zb), new(s0, Line(s0) - off + lift - Ft(4.5)),
        };
        double flare = zb - Ft(10), fTop = zb - Ft(22);
        var sd = SubstructureDefinition.Default(SupportType.Pier);
        sd.CapProfile = Poly(0);
        sd.CapProfileAhead = Poly(Ft(0.25));
        sd.CapOffset = -Ft(1);                       // CL pier 1'-0 back of the bearing line: the seats are on the ahead half
        sd.CapWidth = Ft(6);
        double l1 = s1 - s0;
        sd.Aesthetics = CapBandBuilder.FromTable(l1, l1 * 0.25, l1 * 0.33, l1 * 0.5, l1 * 1.4, l1 * 0.9, l1 * 0.8, l1 * 0.7, false);
        sd.ColumnCount = 1; sd.FirstColumnOffset = sc;
        sd.ColumnShape = ColumnShape.Rectangular; sd.ColumnWidth = Ft(8); sd.ColumnDepth = Ft(4.5); sd.ColumnChamferS = Ft(2.33); sd.ColumnChamferN = Ft(1.25);
        sd.ColumnFlareElevation = flare; sd.ColumnFlareWidth = Ft(10); sd.ColumnFlareDepth = Ft(6);
        sd.Foundation = FoundationType.PileCap; sd.FoundationTopElevation = fTop; sd.FootingThickness = Ft(5); sd.FootingLength = Ft(16); sd.FootingWidth = Ft(12);
        sd.PileEmbedment = Ft(1);
        sd.Footings = new() { new FootingDefinition { Column = 1, PileLength = Ft(60), Piles = new() { new(-Ft(5), -Ft(4)), new(Ft(5), -Ft(4)), new(-Ft(5), Ft(4)), new(Ft(5), Ft(4)) } } };
        sd.PedestalSizeAlong = Ft(3.5); sd.PedestalSizeAcross = Ft(3);
        pierDef.Substructure = sd;
        doc.Regenerate();

        var unit = doc.Substructure().Units.Single(u => u.Type == SupportType.Pier);
        var chains = doc.BearingElevations().Where(c => c.Title.StartsWith(unit.Name)).ToList();
        Assert.Equal(unit.Pedestals.Count, chains.Count);
        foreach (var c in chains)
        {
            Assert.False(c.Mismatch, c.Title + ": " + string.Join("; ", c.Checks));
            AssertRunningSum(c);
            var v = c.Substructure ?? throw new Exception("no substructure chain");
            // Hand values from the inputs: ahead profile top line, level soffit at the column, flare / footing / pile inputs.
            Assert.True(v.SeatN > unit.CapMidN);
            Assert.Equal(Line(v.SeatS) - off + Ft(0.25), v.CapTop, 6);
            Assert.Equal(zb, v.ColumnTop, 6);
            Assert.Equal(flare, v.FlareZ, 9);
            Assert.Equal(fTop, v.FoundationTop, 9);
            Assert.Equal(fTop - Ft(5), v.FoundationBottom, 9);
            Assert.Equal(fTop - Ft(5) + Ft(1), v.PileHead, 9);
            // ... and the generator's values.
            var ped = unit.Pedestals.Single(p => Math.Abs(p.RowS - v.SeatS) < 1e-6);
            Assert.Equal(ped.CapTopZ, v.CapTop, 6);
            Assert.Equal(unit.Columns[0].TopZ, v.ColumnTop, 6);
            Assert.Equal(unit.Foundations[0].BottomZ, v.FoundationBottom, 6);
            Assert.All(unit.Foundations[0].Piles, p => Assert.Equal(p.TopZ, v.PileHead, 6));
            Assert.Contains(c.Steps, s => s.Item == "(info) Cap section step");
            Assert.Contains(c.Steps, s => s.Item == "(info) Pedestal plan size" && s.Basis.Contains("plan size"));
            Assert.Contains(c.Steps, s => s.Item == "− Pedestal" && s.Basis.Contains("CapProfileAhead"));
            Assert.Contains(c.Steps, s => s.Item == "− Column shaft");
            Assert.Contains(c.Steps, s => s.Item == "− Flared base");
            Assert.Contains(c.Steps, s => s.Item == "+ Pile embedment" && s.Result == "Pile head");
            Assert.Empty(c.Reported);
        }
    }

    [Fact]
    public void SharedPedestal_BlockTopIsTheHighestSeat_DifferencesReported()
    {
        // Prestressed: a back and an ahead seat per girder at the pier; one plan block per girder line carries both.
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        b.Girders.Kind = GirderKind.TxDotPrestressed;
        foreach (var s in b.Supports)
        {
            var sd = SubstructureDefinition.Default(s.Type);
            if (s.Type == SupportType.Pier) { sd.PedestalSizeAlong = Ft(3.5); sd.PedestalSizeAcross = Ft(7); sd.PedestalShared = true; sd.CapWidth = Ft(8); }
            s.Substructure = sd;
        }
        doc.Regenerate();
        var unit = doc.Substructure().Units.Single(u => u.Type == SupportType.Pier);
        Assert.True(unit.PedestalBlocks.Count < unit.Pedestals.Count, "seats should share blocks");
        var chains = doc.BearingElevations().Where(c => c.Substructure?.Unit == unit.Name).ToList();
        Assert.Equal(unit.Pedestals.Count, chains.Count);
        foreach (var c in chains)
        {
            Assert.False(c.Mismatch, c.Title + ": " + string.Join("; ", c.Checks));
            AssertRunningSum(c);
            var v = c.Substructure!;
            var ped = unit.Pedestals.Single(p => Math.Abs(p.RowS - v.SeatS) < 1e-6 && Math.Abs(p.RowN - v.SeatN) < 1e-6);
            var block = unit.PedestalBlocks.Single(k => Math.Abs(k.S - ped.S) < 1e-6 && Math.Abs(k.N - ped.N) < 1e-6);
            var carried = unit.Pedestals.Where(p => Math.Abs(p.S - ped.S) < 1e-6 && Math.Abs(p.N - ped.N) < 1e-6).ToList();
            Assert.Equal(2, carried.Count);
            Assert.Equal(carried.Max(p => p.SeatZ), v.PedestalTop, 9);
            Assert.Equal(block.SeatZ, v.PedestalTop, 9);
            Assert.Equal(ped.CapTopZ, v.CapTop, 9);
            Assert.Equal(ped.SeatZ, v.SeatZ, 6);
            Assert.Contains(c.Steps, s => s.Item == "+ Shared pedestal block");
            // The lower seat of a pair is reported when it is more than 1/8" below the block top; the model is not adjusted.
            bool lower = v.PedestalTop - v.SeatZ > In(0.125);
            Assert.Equal(lower, c.Reported.Any(r => r.Contains("shared pedestal top")));
            Assert.Equal(unit.Columns.OrderBy(k => Math.Abs(k.S - v.SeatS)).First().TopZ, v.ColumnTop, 6);
        }
    }
}
