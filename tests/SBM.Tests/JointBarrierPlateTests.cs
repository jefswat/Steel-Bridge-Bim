using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

public class JointBarrierPlateTests
{
    [Fact]
    public void BarrierPlateSection_FollowsTheExposedFaces_InsideTheConcrete()
    {
        // F-shape 3'-6 (base 1'-5): plate 3/8 in thick, 1/4 in inside the faces, 1/4 in above the base on both sides
        var b = new BarrierDefinition { Type = BarrierType.FShape, Height = In(42), BaseWidth = In(17), TopWidth = In(8.5) };
        var prof = BarrierProfiles.FShape(b);
        var bp = new JointBarrierPlateDefinition { LengthIn = Ft(2), LengthOut = Ft(2) };
        var sec = ConcreteGenerator.BarrierPlateSection(prof, bp)!;
        Assert.NotNull(sec);
        double area = System.Math.Abs(MeshBuilder.SignedArea(sec));
        // exposed perimeter above the 1/4 in cut: back face + top + traffic face (3 + lower slope + upper slope), less the two bottom 1/4 in
        double per = 0;
        for (int i = 0; i + 1 < prof.Length; i++) if (!(prof[i].Y < 1e-9 && prof[i + 1].Y < 1e-9)) per += (prof[i + 1] - prof[i]).Length;
        per += (prof[0] - prof[^1]).Length;
        Assert.InRange(area / (per * In(0.375)), 0.95, 1.02);
        // every point of the plate lies inside the barrier section (recessed), none below the 1/4 in clearance
        foreach (var q in sec)
        {
            Assert.True(q.Y >= In(0.25) - 1e-9);
            Assert.True(Inside(prof, q), $"{q.X / In(1):0.00}, {q.Y / In(1):0.00} in");
        }
    }

    private static bool Inside(Vec2[] poly, Vec2 p)
    {
        bool c = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y) && p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X) c = !c;
        return c;
    }

    [Fact]
    public void OptionalInputs_OmittedFromJsonWhenUnset()
    {
        var j = new ExpansionJointDefinition();
        Assert.DoesNotContain("BarrierPlate", System.Text.Json.JsonSerializer.Serialize(j));
        var e = new CrashCushionEndPostDefinition();
        var json = System.Text.Json.JsonSerializer.Serialize(e);
        Assert.DoesNotContain("RailRows", json);
        Assert.DoesNotContain("RailHalfProfile", json);
        Assert.DoesNotContain("RightStation", System.Text.Json.JsonSerializer.Serialize(new EndPostPlanRow(1, -2, 2)));
    }
}
