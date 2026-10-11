using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Diaphragms;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Steel;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>Iowa 1036-BTD steel intermediate diaphragms (T4), checked against Woodbury DS 29-30 (PDF p.31-32).</summary>
public class PsSteelDiaphragmTests
{
    private static double FtIn(double ft, double inch) => Ft(ft) + In(inch);
    private static readonly double Web = In(6.5);   // BTD web (p.32 Detail D/F "6 1/2")

    // Woodbury p.31 "Intermediate diaphragm bolt hole locations" (diaphragm-bolt-holes.csv), beam lines D..H = girders 1..5.
    private static readonly double[][][] Chains =
    {
        new[] { new[] { FtIn(27, 5.25), FtIn(1, 6.25), FtIn(27, 4.5) }, new[] { FtIn(27, 5.25), FtIn(1, 5.5), FtIn(27, 5.25) }, new[] { FtIn(28, 10.75), FtIn(27, 5.25) } },
        new[] { new[] { FtIn(52, 4), FtIn(1, 9.125), FtIn(52, 2.875) }, new[] { FtIn(52, 4), FtIn(1, 8), FtIn(52, 4) }, new[] { FtIn(54, 0), FtIn(52, 4) } },
        new[] { new[] { FtIn(29, 8.25), FtIn(2, 0.75), FtIn(29, 7) }, new[] { FtIn(29, 8.25), FtIn(1, 11.5), FtIn(29, 8.25) }, new[] { FtIn(31, 7.75), FtIn(29, 8.25) } },
    };
    // p.31 member table: beam spacing per span.
    private static readonly double[] Spacing = { FtIn(8, 0.3125), FtIn(7, 11.8125), FtIn(7, 11) };

    private static double[] ChainFor(int span, int g) => g == 1 ? Chains[span - 1][0] : g == 5 ? Chains[span - 1][2] : Chains[span - 1][1];

    /// <summary>Synthetic Woodbury-like beams: parallel beams along +X per span, square diaphragms (beam ends staggered by
    /// the hole-chain difference), bottoms falling <paramref name="crossSlope"/> from D to H.</summary>
    private static List<PsDiaphragmBeamLine> Beams(double crossSlope)
    {
        var list = new List<PsDiaphragmBeamLine>();
        double x0 = 0;
        for (int span = 1; span <= 3; span++)
        {
            var mid = ChainFor(span, 2);
            double stagger = mid[1];                      // E/F/G hole difference = beam-end stagger per bay
            double len = mid.Sum();
            for (int g = 1; g <= 5; g++)
            {
                double y = -(g - 1) * Spacing[span - 1];
                double z = 100 + crossSlope * y;
                double xs = x0 - (g - 1) * stagger;
                list.Add(new PsDiaphragmBeamLine(span, g, new Vec3(xs, y, z), new Vec3(xs + len, y, z), Web));
            }
            x0 += len + Ft(2);
        }
        return list;
    }

    private static PsSteelDiaphragmDefinition Woodbury(PsDiaphragmMemberSide side = PsDiaphragmMemberSide.TowardIncreasingStation,
        PsDiaphragmMemberElevation elev = PsDiaphragmMemberElevation.Level)
    {
        var d = new PsSteelDiaphragmDefinition
        {
            MemberSide = side, Elevation = elev, BeamLineNames = new() { "D", "E", "F", "G", "H" }, PlanSteelWeightLb = 4258,
        };
        for (int span = 1; span <= 3; span++)
            for (int g = 1; g <= 5; g++)
                d.Holes.Add(new PsDiaphragmHoleChain { Span = span, Girder = g, Dims = ChainFor(span, g).ToList() });
        double[] c15Len = { FtIn(6, 10), FtIn(6, 9.5), FtIn(6, 8.75) };
        for (int span = 1; span <= 3; span++)
            for (int g = 1; g <= 4; g++)
            {
                // Bay g..g+1: the left beam's first (lower) hole with the right beam's last hole (square to the beams).
                var bay = new PsDiaphragmBay { Span = span, LeftGirder = g, LeftHole = 1, RightHole = g + 1 == 5 ? 1 : 2, PlanLength = c15Len[span - 1] };
                if (span == 2 && g == 4)
                {
                    bay.Member = "W21x50"; bay.Lower = "W14x38"; bay.PlanLength = FtIn(6, 9.625); bay.LowerPlanLength = FtIn(5, 4.875); bay.Connection = "D";
                }
                d.Bays.Add(bay);
            }
        return d;
    }

    [Fact]
    public void IowaBtdPreset_MatchesStandardSheet()
    {
        var c = PsDiaphragmConnection.IowaBtd();
        Assert.Equal(In(20.5), c.LowerHoleAboveBottom, 9);
        Assert.Equal(In(9.375), c.HolePitch, 9);
        Assert.Equal(c.PlateLength, c.HolePitch + 2 * c.PlateEndDistance, 9);        // 2 + 9 3/8 + 2 = 1'-1 3/8
        Assert.Equal(c.SlotHeights[^1] - c.SlotHeights[0], c.HolePitch, 9);           // 3 @ 3 1/8 = 9 3/8
        Assert.NotNull(SteelCatalog.FindChannel("C15x33.9"));
        Assert.NotNull(SteelCatalog.FindWideFlange("W21x50"));
        Assert.NotNull(SteelCatalog.FindWideFlange("W14x38"));
    }

    [Fact]
    public void Woodbury_PartAndBoltCounts_MatchPlanQuantities()
    {
        var m = PsSteelDiaphragmDetailer.Detail(Woodbury(), Beams(0));
        Assert.DoesNotContain(m.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Equal(12, m.Bays.Count);
        Assert.Equal(12, m.Assemblies.Count);
        Assert.All(m.Bays, b => Assert.True(b.Detailed));
        // 11 C15 bays x (member + 2 bent + 2 backing) + W21 bay x (5 + W14 + 2 end PL + 4 shims).
        Assert.Equal(11 * 5 + 12, m.Parts.Count);
        Assert.Equal(24, m.Parts.Count(p => p.Description.StartsWith("Bent PL 9 x 6 x 1/2 x 1'-1 3/8")));
        Assert.Equal(24, m.Parts.Count(p => p.Description == "PL 6 x 3/8 x 1'-1 3/8"));
        Assert.Equal(2, m.Parts.Count(p => p.Description == "PL 6.5 x 3/8 x 1'-2"));
        Assert.Equal(4, m.Parts.Count(p => p.Description == "Shim PL 6.5 x 1/8 x 1'-2"));
        Assert.Equal(11, m.Parts.Count(p => p.Description == "C15X33.9"));
        Assert.Single(m.Parts, p => p.Description == "W21X50");
        Assert.Single(m.Parts, p => p.Description == "W14X38");
        // Bolts (p.31): 24 connections x 2 web bolts 7/8 x 9 1/4; 12 x 8 member bolts 7/8 x 2 3/4; 18 x 7/8 x 2 1/2.
        Assert.Equal(48, m.Bolts.Count(b => Math.Abs(b.Length - In(9.25)) < 1e-9));
        Assert.Equal(96, m.Bolts.Count(b => Math.Abs(b.Length - In(2.75)) < 1e-9));
        Assert.Equal(18, m.Bolts.Count(b => Math.Abs(b.Length - In(2.5)) < 1e-9));
        Assert.All(m.Bolts, b => Assert.Equal(In(0.875), b.Diameter, 9));
        // Cast holes: 9 locations per span (D 2, E/F/G 2 each, H 1).
        Assert.Equal(27, m.Holes.Count);
        Assert.Contains(m.Diagnostics, d => d.Message.Contains("WD-5"));
        Assert.All(m.Parts, p => Assert.True(Math.Abs(p.Mesh.Volume()) > 0));
    }

    [Fact]
    public void Woodbury_MemberLengthsAndWeights_MatchPlanTable()
    {
        var m = PsSteelDiaphragmDetailer.Detail(Woodbury(), Beams(0));
        Assert.DoesNotContain(m.Diagnostics, d => d.Message.Contains("plan length"));     // hole geometry vs table within 1/4"
        Assert.All(m.Bays, b => Assert.InRange(Math.Abs(b.GeometricLength - b.Length), 0, In(0.25)));
        // p.31 table weights per row (lb): W21 340, C15 927 / 691 / 912, W14 205.
        Assert.Equal(340, Math.Round(m.Bays.Where(b => b.Bay.Member == "W21x50").Sum(b => b.MemberWeightLb)));
        Assert.Equal(927, Math.Round(m.Bays.Where(b => b.Bay.Span == 1).Sum(b => b.MemberWeightLb)));
        Assert.Equal(691, Math.Round(m.Bays.Where(b => b.Bay.Span == 2 && b.Bay.Member != "W21x50").Sum(b => b.MemberWeightLb)));
        Assert.Equal(912, Math.Round(m.Bays.Where(b => b.Bay.Span == 3).Sum(b => b.MemberWeightLb)));
        Assert.Equal(205, Math.Round(m.Bays.Sum(b => b.LowerWeightLb)));
        // Square diaphragms (plan p.21 "90 deg 00'00" (TYP.)").
        Assert.All(m.Bays, b => Assert.True(b.OffSquare < 1e-6));
    }

    [Fact]
    public void WebBolts_AtThePlanHoleHeights()
    {
        var beams = Beams(0.05);
        var m = PsSteelDiaphragmDetailer.Detail(Woodbury(), beams);
        foreach (var h in m.Holes)
        {
            var b = beams.Single(x => x.Span == h.Span && x.Girder == h.Girder);
            Assert.Equal(b.BottomStart.Z + In(20.5), h.Lower.Z, 9);                   // 1'-8 1/2 above the beam bottom
            Assert.Equal(In(9.375), h.Upper.Z - h.Lower.Z, 9);                         // 9 3/8
            Assert.Equal(In(1.5), h.Diameter, 9);
        }
        // Each web bolt shank passes through a cast hole centre (bolt axis = hole axis).
        var web = m.Bolts.Where(b => Math.Abs(b.Length - In(9.25)) < 1e-9).ToList();
        foreach (var bolt in web)
        {
            var o = bolt.Frame.Origin;
            Assert.Contains(m.Holes, h => new[] { h.Lower, h.Upper }.Any(c =>
            {
                var d = c - o;
                var perp = d - bolt.Frame.Z * Vec3.Dot(d, bolt.Frame.Z);
                return perp.Length < 1e-6 && Math.Abs(Vec3.Dot(bolt.Frame.Z, h.Axis)) > 0.999999;
            }));
            Assert.Equal(In(3.0 / 8 + 6.5 + 0.5), bolt.Grip, 9);
        }
        // Span 1 beam D: holes 27'-5 1/4 and 28'-11 1/2 from the beam end.
        var d1 = m.Holes.Where(h => h.Span == 1 && h.Girder == 1).Select(h => h.FromBeamEnd).ToArray();
        Assert.Equal(new[] { FtIn(27, 5.25), FtIn(28, 11.5) }, d1, new Tol());
    }

    [Fact]
    public void LevelMember_OnCrossSlope_WarnsSlotToleranceExceeded()
    {
        var m = PsSteelDiaphragmDetailer.Detail(Woodbury(), Beams(0.05));
        var bay = m.Bays.First(b => b.Bay.Span == 1);
        Assert.Equal(-0.05 * Spacing[0], bay.HeightDifference, 6);                    // ~4.8" per bay
        Assert.Equal(0, bay.MemberSlope);
        Assert.False(bay.Fits);
        Assert.Equal(Math.Abs(bay.HeightDifference) / 2, bay.LeftFit!.MemberSlotOffset, 6);
        Assert.Equal(In(0.25), bay.LeftFit.MemberSlotCapacity, 9);                     // 1" x 1 1/2" slot takes 1/2" total
        Assert.Equal(12, m.Diagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("slot tolerance exceeded") && d.Message.Contains("level member")));
        // The member stays level.
        var member = m.Parts.First(p => p.Mark == $"{bay.Mark}-M");
        var zs = member.Mesh.Positions.Select(p => p.Z).ToArray();
        Assert.Equal(In(15), zs.Max() - zs.Min(), 4);
    }

    [Fact]
    public void AlongHoleLine_OnCrossSlope_FitsTheSlots()
    {
        var m = PsSteelDiaphragmDetailer.Detail(Woodbury(elev: PsDiaphragmMemberElevation.AlongHoleLine), Beams(0.05));
        Assert.DoesNotContain(m.Diagnostics, d => d.Message.Contains("slot tolerance exceeded"));
        Assert.All(m.Bays, b => Assert.True(b.Fits));
        var bay = m.Bays.First();
        Assert.Equal(-0.05, bay.MemberSlope, 3);
        Assert.True(bay.LeftFit!.PlateSlotOffset > In(0.1));                           // tilt taken by the horizontal leg slots
    }

    [Fact]
    public void LevelMember_OnLevelBeams_FitsExactly()
    {
        var m = PsSteelDiaphragmDetailer.Detail(Woodbury(), Beams(0));
        Assert.All(m.Bays, b =>
        {
            Assert.True(b.Fits);
            Assert.True(b.LeftFit!.MemberSlotOffset < 1e-9);
            Assert.True(b.LeftFit.OutOfPlane < 1e-9);
        });
        // Member bolts 3 1/2" from the free edge of the 9" leg = 3 1/4 + 9 - 3 1/2 from the CL beam; 1 5/8 from the member end.
        var bay = m.Bays.First();
        var bolts = m.Bolts.Where(b => b.Assembly == bay.Mark && Math.Abs(b.Length - In(2.75)) < 1e-9).ToList();
        Assert.Equal(8, bolts.Count);
        double yL = 0;                                                                  // span 1 beam D at y = 0, bay toward -y
        Assert.Equal(4, bolts.Count(b => Math.Abs(Math.Abs(b.Frame.Origin.Y - yL) - In(3.25 + 9 - 3.5)) < 1e-9));
        var zs = bolts.Select(b => b.Frame.Origin.Z).Distinct(new Tol()).OrderBy(z => z).ToArray();
        Assert.Equal(4, zs.Length);
        Assert.Equal(In(3.125), zs[1] - zs[0], 9);
        Assert.Equal(100 + In(20.5), zs[0], 9);                                         // lower slot on the lower web hole
    }

    [Fact]
    public void MemberSideUnresolved_NoSteel_HolesStillModelled()
    {
        var m = PsSteelDiaphragmDetailer.Detail(Woodbury(PsDiaphragmMemberSide.Unresolved), Beams(0));
        Assert.Empty(m.Parts);
        Assert.Empty(m.Bolts);
        Assert.Equal(27, m.Holes.Count);
        Assert.Contains(m.Diagnostics, d => d.Message.StartsWith("UNRESOLVED WD-3"));
        Assert.All(m.Bays, b => Assert.False(b.Detailed));
    }

    [Fact]
    public void MemberSide_FlipsTheMemberAlongTheBeam()
    {
        var a = PsSteelDiaphragmDetailer.Detail(Woodbury(PsDiaphragmMemberSide.TowardIncreasingStation), Beams(0));
        var b = PsSteelDiaphragmDetailer.Detail(Woodbury(PsDiaphragmMemberSide.TowardDecreasingStation), Beams(0));
        double Cx(PsSteelDiaphragmModel m) => m.Parts.First(p => p.Mark == "ID1-DE-M").Mesh.Positions.Min(p => p.X);
        var hole = a.Holes.First(h => h.Span == 1 && h.Girder == 1 && h.Index == 1).Lower.X;
        Assert.Equal(hole + In(3.5), Cx(a), 6);                                         // web back face on the 9" leg face
        Assert.True(Cx(b) < hole - In(3.5));                                            // flanges toward decreasing station
    }

    [Fact]
    public void Errors_UnknownShape_BadHole_ChainSum()
    {
        var d = Woodbury();
        d.Bays[0].Member = "C15x99";
        d.Bays[1].RightHole = 7;
        d.Holes[0].Dims[^1] += In(1);
        var m = PsSteelDiaphragmDetailer.Detail(d, Beams(0));
        Assert.Contains(m.Diagnostics, x => x.Severity == DiagnosticSeverity.Error && x.Message.Contains("C15x99"));
        Assert.Contains(m.Diagnostics, x => x.Severity == DiagnosticSeverity.Error && x.Message.Contains("hole index"));
        Assert.Contains(m.Diagnostics, x => x.Severity == DiagnosticSeverity.Warning && x.Message.Contains("chain sums"));
        Assert.Equal(10, m.Bays.Count);
    }

    [Fact]
    public void GeneratedPrestressedBeams_FeedTheDetailer()
    {
        var doc = SbmDocument.Sample();
        var br = doc.Bridge();
        br.Girders.Kind = GirderKind.TxDotPrestressed;
        foreach (var s in br.Supports) s.Substructure = SubstructureDefinition.Default(s.Type);
        doc.Regenerate();
        var ps = doc.Model().Prestressed!;
        var beams = ps.Beams.Select(b => PsDiaphragmBeamLine.FromPrestressed(b, b.Shape.Depth, b.Shape.WebWidth)).ToList();
        var b0 = beams.First(b => b.Span == 1 && b.Girder == 1);
        var b1 = beams.First(b => b.Span == 1 && b.Girder == 2);
        Assert.True(b0.BottomStart.X < b0.BottomEnd.X || b0.BottomStart.Y != b0.BottomEnd.Y);
        // One diaphragm at mid-length, square to the beams (start offsets differ by the skew).
        double s0 = b0.Length / 2;
        var p0 = b0.BottomStart + b0.Axis * s0;
        double s1 = Vec3.Dot(p0 - b1.BottomStart, b1.Axis);
        var def = new PsSteelDiaphragmDefinition { MemberSide = PsDiaphragmMemberSide.TowardIncreasingStation };
        def.Holes.Add(new PsDiaphragmHoleChain { Span = 1, Girder = 1, Dims = new() { s0, b0.Length - s0 } });
        def.Holes.Add(new PsDiaphragmHoleChain { Span = 1, Girder = 2, Dims = new() { s1, b1.Length - s1 } });
        def.Bays.Add(new PsDiaphragmBay { Span = 1, LeftGirder = 1 });
        var m = PsSteelDiaphragmDetailer.Detail(def, beams);
        Assert.DoesNotContain(m.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Equal(5, m.Parts.Count);
        Assert.Equal(12, m.Bolts.Count);
        Assert.True(m.Bays[0].OffSquare < 1e-3);
    }

    private sealed class Tol : IEqualityComparer<double>
    {
        public bool Equals(double a, double b) => Math.Abs(a - b) < 1e-9;
        public int GetHashCode(double x) => 0;
    }
}
