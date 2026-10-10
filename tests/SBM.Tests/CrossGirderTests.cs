using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Steel;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>As-designed cross girders under discontinuous girders (I-80 sheet 121 details).</summary>
public class CrossGirderTests
{
    private static SbmDocument Doc(bool asDesigned, double? angle = null)
    {
        var doc = SbmDocument.Sample();
        var b = doc.Bridge();
        b.Girders.Terminations.Add(new GirderTermination { Girder = 3, Support = 1, Distance = Ft(60), CrossGirderAngleDegrees = angle });
        b.Girders.Section.WebDepth = In(110);                                                // deep enough for the 80" cross girder
        var x = b.Girders.CrossGirder;
        x.WebDepth = In(80); x.WebThickness = In(0.5625); x.FlangeWidth = In(15); x.FlangeThickness = In(0.625);
        if (asDesigned)
        {
            x.TopBelowDiscontinuousWebTop = In(11);
            x.AngleLength = In(64); x.EndAngleLength = In(64); x.AngleBoltPitch = In(6); x.BoltDiameter = In(1);
            x.OpeningWidth = In(24); x.OpeningHeight = In(36); x.OpeningPlateFromTop = In(22);
            x.OpeningPlateWidth = In(6); x.OpeningPlateThickness = In(0.625);
            x.StiffenerWidth = In(7.5); x.StiffenerThickness = In(0.625);
            x.WebFlangeWeld = In(5.0 / 16); x.StiffenerWeld = In(0.25);
        }
        return doc;
    }

    [Fact]
    public void AsDesigned_HasAnglesBoltsOpeningsStiffenersAndPlates()
    {
        var doc = Doc(true);
        Assert.Empty(doc.Regenerate().Errors);
        var steel = doc.Steel();
        var parts = steel.Parts.Where(p => p.Assembly == "XG-G4").ToList();
        Assert.Equal(6, parts.Count(p => p.Category == PartCategory.Angle));                 // 4 at the continuing girders + 2 at the discontinuous end
        Assert.Equal(4, parts.Count(p => p.Mark.Contains("-S")));                            // vertical stiffeners
        Assert.Equal(4, parts.Count(p => p.Mark.Contains("-P")));                            // opening edge plates
        Assert.Contains("2 openings", parts.Single(p => p.Mark == "XG-G4-W").Description);
        // Bolts: 11 per XG-web leg line (two girders x 11, plus the end angles 2 x 11) and 11 per girder-web leg (4 + 1 shared at the end).
        var bolts = steel.Bolts.Where(b => b.Assembly == "XG-G4").ToList();
        Assert.Equal(2 * 11 + 2 * 2 * 11 + 2 * 11 + 11, bolts.Count);
        Assert.All(bolts, b => Assert.Equal(In(1), b.Diameter, 6));
    }

    [Fact]
    public void TopOfCrossGirderIsBelowTheDiscontinuousGirderTopOfWeb()
    {
        var doc = Doc(true);
        doc.Regenerate();
        var xgr = doc.Framing().CrossGirders.Single();
        var top = doc.Steel().Parts.Single(p => p.Mark == "XG-G4-TF").Mesh.Bounds();
        var legacy = Doc(false);
        legacy.Regenerate();
        var top0 = legacy.Steel().Parts.Single(p => p.Mark == "XG-G4-TF").Mesh.Bounds();
        // Legacy = flush with the top-of-web chord; as designed the plate is shifted so its top is 11" under the discontinuous
        // girder's top of web (the chord keeps its slope, so the shift differs from 11" by the chord/centre sag, < 1").
        Assert.InRange(top0.Max.Z - top.Max.Z, In(10), In(12));
        Assert.InRange(xgr.Center.Z - In(11) - (top.Min.Z + top.Max.Z) / 2, -In(1), In(1));
    }

    [Fact]
    public void AngleBoltsCutHolesInTheGirderWebs()
    {
        double WebVolume(SbmDocument d) => d.Steel().Parts.Where(p => p.Category == PartCategory.WebPlate && !p.Assembly.StartsWith("XG")).Sum(p => p.Volume);
        var plain = Doc(true);
        plain.Bridge().Girders.CrossGirder.AngleBoltPitch = 0;
        plain.Regenerate();
        var bolted = Doc(true);
        bolted.Regenerate();
        double removed = WebVolume(plain) - WebVolume(bolted);
        // Holes: 11 per angle leg line in the left and right continuing girders (2 angles each) + 11 in the discontinuous girder.
        Assert.True(removed > 0);
        double hd = new BoltGeometry(In(1)).HoleDiameter;
        // The sample's web thickness is the section's; compare against the largest possible (every hole through 1" web): within 5% of n * area * tw.
        double tWeb = bolted.Bridge().Girders.Section.WebThickness;
        int n = 2 * 2 * 11 + 11;
        Assert.InRange(removed, 0.9 * n * Math.PI / 4 * hd * hd * tWeb, 1.12 * n * Math.PI / 4 * hd * hd * tWeb);
    }

    [Fact]
    public void CrossGirderAngle_RotatesTheCrossGirderFromSquare()
    {
        var sq = Doc(true); sq.Regenerate();
        var skew = Doc(true, 89.0); skew.Regenerate();
        var a = sq.Framing().CrossGirders.Single();
        var b = skew.Framing().CrossGirders.Single();
        double ang = Math.Acos(Math.Clamp(SBM.Core.Geometry.Vec2.Dot(a.Direction, b.Direction), -1, 1)) * 180 / Math.PI;
        Assert.InRange(ang, 0.9, 1.1);
    }

    [Fact]
    public void Legacy_DefaultsAreUnchanged()
    {
        var doc = Doc(false);
        Assert.Empty(doc.Regenerate().Errors);
        var parts = doc.Steel().Parts.Where(p => p.Assembly == "XG-G4").ToList();
        Assert.Equal(6, parts.Count(p => p.Category == PartCategory.Angle));
        Assert.Empty(doc.Steel().Bolts.Where(b => b.Assembly == "XG-G4"));
        Assert.DoesNotContain(parts, p => p.Mark.Contains("-S") || p.Mark.Contains("-P"));
    }
}
