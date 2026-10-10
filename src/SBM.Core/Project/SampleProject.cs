using SBM.Core.Alignments;
using SBM.Core.Bridge;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Project;

/// <summary>
/// A realistic demonstration project: tangent → spiral → 1500' curve → spiral → tangent with a crest vertical
/// curve and 6% superelevation, carrying a skewed two-span continuous plate girder bridge through the spiral
/// into the circular curve.
/// </summary>
public static class SampleProject
{
    public static BridgeProject Create()
    {
        var align = CreateAlignment();
        var bridge = BridgeDefinition.CreateDefault(Ft(2100), align.Name);
        bridge.Name = "Sample Bridge";
        foreach (var s in bridge.Supports) s.SkewDegrees = 15;
        bridge.Stiffeners.IntermediateMaxSpacing = Ft(12);
        // Disc bearings: fixed at the pier, guided expansion at the abutments.
        bridge.Bearings.Type = BearingType.Disc;
        bridge.Supports[0].Fixity = BearingFixity.GuidedExpansion;
        bridge.Supports[2].Fixity = BearingFixity.GuidedExpansion;
        // Bottom flange lateral bracing in the exterior bays.
        bridge.LateralBracing.Enabled = true;
        bridge.LateralBracing.Bays = new() { 1, 4 };
        // Heavier bottom flange over the pier (shop splices with thickness transitions).
        double pier = bridge.Supports[1].Station;
        bridge.Girders.Section.PlateChanges = new()
        {
            new PlateSegment { Plate = GirderPlate.BottomFlange, EndStation = pier - Ft(25), Width = In(18), Thickness = In(1.25) },
            new PlateSegment { Plate = GirderPlate.BottomFlange, EndStation = pier + Ft(25), Width = In(20), Thickness = In(2) },
            new PlateSegment { Plate = GirderPlate.BottomFlange, Width = In(18), Thickness = In(1.25) },
        };

        return new BridgeProject
        {
            Name = "Sample Project",
            Alignments = { align },
            Bridges = { bridge },
        };
    }

    /// <summary>
    /// Synthetic existing-ground TIN around the sample bridge: gently sloping ground with a creek channel crossing
    /// under span 1. <paramref name="spacing"/> sets the grid spacing (smaller = denser, for performance testing).
    /// </summary>
    public static Solids.Mesh CreateExistingGround(Alignment a, double spacing = 6.0)
    {
        var mesh = new Solids.Mesh();
        double s0 = a.StationToDistance(Ft(1800)), s1 = a.StationToDistance(Ft(2700));
        double halfW = Ft(250);
        int nu = (int)Math.Ceiling((s1 - s0) / spacing), nv = (int)Math.Ceiling(2 * halfW / spacing);
        var creekPt = a.PointAtDistance(a.StationToDistance(Ft(2170)));
        var creekDir = Geometry.Vec2.FromAngle(a.AngleAt(Ft(2170)) + Geometry.Numerics.DegToRad(70));
        for (int i = 0; i <= nu; i++)
            for (int j = 0; j <= nv; j++)
            {
                double d = s0 + (s1 - s0) * i / nu, off = -halfW + 2 * halfW * j / nv;
                var p = a.PointAtDistance(d, off);
                double grade = Ft(512) + 0.015 * (d - s0) - 0.01 * off;
                double dist = Math.Abs(Geometry.Vec2.Cross(creekDir, p - creekPt));
                double channel = Ft(28) * Math.Exp(-Math.Pow(dist / Ft(70), 2));
                double ripple = Ft(1.5) * Math.Sin(d / 37.0) * Math.Cos(off / 23.0);
                mesh.Add(p.WithZ(grade - channel + ripple));
            }
        int W = nv + 1;
        for (int i = 0; i < nu; i++)
            for (int j = 0; j < nv; j++)
            {
                int a0 = i * W + j, a1 = (i + 1) * W + j, a2 = (i + 1) * W + j + 1, a3 = i * W + j + 1;
                mesh.Tri(a0, a1, a2);
                mesh.Tri(a0, a2, a3);
            }
        return mesh;
    }

    public static Alignment CreateAlignment()
    {
        double r = Ft(1500), ls = Ft(200);
        var pis = new PiDefinition
        {
            Pis =
            {
                new PiRow(Ft(5000), Ft(10000)),
                new PiRow(Ft(5000), Ft(12500), r, ls, ls),
                new PiRow(Ft(5000 + 1500), Ft(12500 + 3000 * Math.Cos(Math.PI / 6)), 0),
            },
        };
        var elements = pis.Build(out var curves);
        var align = new Alignment(elements)
        {
            Name = "CL Route 1",
            Description = "Sample alignment",
            StartStation = 0,
            PiDefinition = pis,
            Profile = new VerticalProfile
            {
                Vpis =
                {
                    new Vpi(0, Ft(500)),
                    new Vpi(Ft(2200), Ft(544), Ft(800)),
                    new Vpi(Ft(4000), Ft(526)),
                },
            },
        };

        // Superelevation through the spirals (left curve: left side is the low/inside side).
        double nc = -0.02, e = 0.06;
        var c = curves[0];
        double ts = align.Inverse(c.TS).Station;
        double st = align.Inverse(c.ST).Station;
        double sc = ts + ls, cs = st - ls;
        double runout = ls * Math.Abs(nc) / e;
        align.CrossSlope = new CrossSlopeTable
        {
            Rows =
            {
                new CrossSlopeRow(ts - runout, nc, nc),
                new CrossSlopeRow(ts, nc, 0),
                new CrossSlopeRow(ts + runout, nc, -nc),
                new CrossSlopeRow(sc, -e, e),
                new CrossSlopeRow(cs, -e, e),
                new CrossSlopeRow(st - runout, nc, -nc),
                new CrossSlopeRow(st, nc, 0),
                new CrossSlopeRow(st + runout, nc, nc),
            },
        };
        return align;
    }
}
