using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Steel;

/// <summary>
/// Geometry of one integral steel pier cap (<see cref="IntegralSteelCapDefinition"/>) in its own frame, shared by the steel detailer
/// (<c>SteelModelGenerator</c>, IntegralCapDetailer.cs) and the concrete generator (cap haunch, <c>ConcreteGenerator</c> ConcreteCapHaunch.cs) so both
/// see the SAME cap: x along the sloped axis from the first CL bearing (bottom of the bottom flange), y = N (normal to the support line, up-station +),
/// z normal to the axis (up). The cap is the FINAL shape: bottom = chord of the top of bearing elevations, webs plumb, flanges square to the webs.
/// Plan positions along the cap are horizontal S on the support line from the alignment point (right +).
/// </summary>
public class IntegralCapFrame
{
    public required int Support { get; init; }
    public required string SupName { get; init; }
    public required IntegralSteelCapDefinition D { get; init; }
    public required Vec2 O { get; init; }
    public required Vec2 Dir { get; init; }
    public required Vec2 Nrm { get; init; }
    public required double M { get; init; }
    public required List<double> Sb { get; init; }
    public required List<double> Tob { get; init; }
    public double K => Math.Sqrt(1 + M * M);
    public double SE0 => Sb[0] - D.EndOverhang;
    public double SE1 => Sb[^1] + D.EndOverhang;
    public Vec3 Origin => new(O.X + Dir.X * Sb[0], O.Y + Dir.Y * Sb[0], Tob[0]);
    public Vec3 X => new Vec3(Dir.X, Dir.Y, M) * (1 / K);
    public Vec3 Y => new(Nrm.X, Nrm.Y, 0);
    public Vec3 Z => new Vec3(-M * Dir.X, -M * Dir.Y, 1) * (1 / K);
    public Frame3 Fs => new(Origin, X, Y, Z);
    public double Xc(double s) => (s - Sb[0]) * K;
    public double ZTow => D.BottomFlange.Thickness + D.WebDepth;
    public double ZTop => ZTow + D.TopFlange.Thickness;
    public Vec2 Plan(double s, double n) => O + Dir * s + Nrm * n;
    public double S(Vec2 p) => Vec2.Dot(p - O, Dir);
    public double N(Vec2 p) => Vec2.Dot(p - O, Nrm);
    /// <summary>Elevation of the cap surface at local z = c at the horizontal plan position s.</summary>
    public double SurfaceZ(double s, double c) => Tob[0] + M * (s - Sb[0]) + c * K;
    /// <summary>Horizontal plan S of the cap point at local (x, z = c).</summary>
    public double PlanS(double x, double c) => Sb[0] + (x - M * c) / K;
    public (double x, double y, double z) Local(Vec3 w) { var v = w - Origin; return (Vec3.Dot(v, X), Vec3.Dot(v, Y), Vec3.Dot(v, Z)); }
    /// <summary>Web CL of member m (twin: -WO, +WO; single: 0).</summary>
    public double WebN(int m) => D.Members >= 2 ? (m == 0 ? -D.WebOffset : D.WebOffset) : 0;
    public int MemberCount => D.Members >= 2 ? 2 : 1;
    public string Mark => D.Mark;
    /// <summary>Out-to-out half width of the top flanges across the cap (twin: web offset + half the flange width).</summary>
    public double TopHalfWidth => (MemberCount == 2 ? D.WebOffset : 0) + D.TopFlange.Width / 2;

    /// <summary>Plan S range of a cover plate (the same rule the detailer builds it with: distances from its CL bearing toward the cap interior,
    /// <see cref="CapCoverPlate.FromSoleEdge"/> = from the modeled sole plate edge, <paramref name="soleHalf"/> = half the sole plate length).</summary>
    public (double s0, double s1) CoverRange(CapCoverPlate cp, double soleHalf)
    {
        int k = Math.Clamp(cp.Bearing, 0, Sb.Count - 1);
        int sgn = k == Sb.Count - 1 && Sb.Count > 1 ? -1 : 1;
        double from = cp.FromSoleEdge ? soleHalf + cp.From : cp.From;
        double sA = Sb[k] + sgn * from, sB = Sb[k] + sgn * cp.To;
        return (Math.Min(sA, sB), Math.Max(sA, sB));
    }

    /// <summary>Support line axes of a cap: alignment point, unit direction along the support line (right +) and the normal pointing up-station.</summary>
    public static (Vec2 O, Vec2 Dir, Vec2 Nrm) Axes(BridgeFraming f, int support)
    {
        var sl = f.SupportLine(support);
        var t = Vec2.FromAngle(f.Alignment.AngleAt(sl.Station));
        var d = sl.Direction.Normalized();
        var n = d.Left;
        if (Vec2.Dot(n, t) < 0) n = -n;
        return (sl.AlignmentPoint, d, n);
    }

    /// <summary>The cap frame of support <paramref name="support"/>, or null when the support has no cap or the cap lacks its bearing data
    /// (the detailer raises that UNRESOLVED item).</summary>
    public static IntegralCapFrame? Create(BridgeFraming f, int support)
    {
        var cd = f.Definition.Supports[support].IntegralSteelCap;
        if (cd == null || cd.BearingOffsets.Count < 2 || cd.BearingTopElevations.Count != cd.BearingOffsets.Count || cd.BearingTopElevations.Any(double.IsNaN)) return null;
        var (o, d, n) = Axes(f, support);
        var sb = cd.BearingOffsets.ToList();
        var tob = cd.BearingTopElevations.ToList();
        double m = (tob[^1] - tob[0]) / (sb[^1] - sb[0]);
        return new IntegralCapFrame { Support = support, SupName = f.SupportLine(support).Name, D = cd, O = o, Dir = d, Nrm = n, M = m, Sb = sb, Tob = tob };
    }
}
