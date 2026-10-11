using SBM.Core.Geometry;

namespace SBM.Core.Bridge;

/// <summary>Which vertical face of an as-designed cap carries the texture band.</summary>
public enum CapFaceSide
{
    /// <summary>The down-station (back, "near" when looking ahead station) face, N = CapN0.</summary>
    Back,
    /// <summary>The up-station (ahead, "far" when looking ahead station) face, N = CapN1.</summary>
    Ahead,
}

/// <summary>
/// One segment end of a band boundary: the segment runs from the previous node to (X, D). X = distance along the cap top
/// line from its left top corner (right positive, may lie outside the cap), D = depth below the top line, measured
/// perpendicular to it. <see cref="Radius"/> 0 = straight; &gt; 0 = circular arc whose centre is on the deeper side of the
/// chord (a "hill", the curve bulges up toward the cap top); &lt; 0 = centre on the shallower side (a "sag").
/// </summary>
public sealed class BandNode
{
    public double X { get; set; }
    public double D { get; set; }
    public double Radius { get; set; }
    public BandNode() { }
    public BandNode(double x, double d, double radius = 0) { X = x; D = d; Radius = radius; }
}

/// <summary>A band boundary: a chain of straight lines and tangent circular arcs in (X along the top, depth) coordinates.</summary>
public sealed class BandChain
{
    public List<BandNode> Nodes { get; set; } = new();
    public BandChain Clone() => new() { Nodes = Nodes.Select(n => new BandNode(n.X, n.D, n.Radius)).ToList() };

    /// <summary>The chain as a polyline (arcs sampled to <paramref name="sagitta"/> m).</summary>
    public List<Vec2> Sample(double sagitta = 0.001)
    {
        var pts = new List<Vec2>();
        for (int i = 0; i < Nodes.Count; i++)
        {
            var p = new Vec2(Nodes[i].X, Nodes[i].D);
            if (i == 0) { pts.Add(p); continue; }
            var a = pts[^1];
            double r = Math.Abs(Nodes[i].Radius);
            double chord = (p - a).Length;
            if (r < 1e-9 || chord < 1e-9 || chord >= 2 * r) { pts.Add(p); continue; }
            var mid = (a + p) * 0.5;
            var v = (p - a) / chord;
            var perp = new Vec2(-v.Y, v.X); // for v = (1, 0): (0, 1) = deeper
            double h = Math.Sqrt(r * r - chord * chord / 4);
            // hill (R > 0): centre deeper; sag (R < 0): centre shallower. The deeper side is the +D side of a left-to-right chord.
            var c = Nodes[i].Radius > 0 ? mid + perp * h : mid - perp * h;
            double a0 = Math.Atan2(a.Y - c.Y, a.X - c.X), a1 = Math.Atan2(p.Y - c.Y, p.X - c.X);
            double da = a1 - a0;
            while (da > Math.PI) da -= 2 * Math.PI;
            while (da < -Math.PI) da += 2 * Math.PI;
            int n = Math.Max(2, (int)Math.Ceiling(Math.Abs(da) / (2 * Math.Acos(Math.Max(0, 1 - sagitta / r)))));
            for (int k = 1; k < n; k++)
            {
                double t = a0 + da * k / n;
                pts.Add(new Vec2(c.X + r * Math.Cos(t), c.Y + r * Math.Sin(t)));
            }
            pts.Add(p);
        }
        return pts;
    }
}

/// <summary>
/// Architectural cap section of an as-designed pier cap (Iowa DOT pier details, sections A-A / B-B): the cap is full width W
/// above a reverse-curve line (the lower edge of the form-liner band) and narrower below it, set back
/// <see cref="StepSetback"/> (6", W - 1'-0 in all) on BOTH faces. The setback is one step along the curve, or three nested steps
/// (R3, R4, R5 arcs, 2" each) at the fan end; each tread is drafted. The texture band itself (Texture B, 1 1/2" ribs) is on
/// <see cref="Face"/> only and is not modeled as geometry.
/// Optional: null on <see cref="SubstructureDefinition.Aesthetics"/> = plain full-width cap. Requires <see cref="SubstructureDefinition.CapProfile"/>.
/// </summary>
public sealed class CapAestheticsDefinition
{
    /// <summary>The face that carries the texture band (metadata: notes / drawings; the step is on both faces).</summary>
    public CapFaceSide Face { get; set; } = CapFaceSide.Ahead;
    /// <summary>Total setback of the lower cap section from each face (plans: 6"). Split evenly over the layers.</summary>
    public double StepSetback { get; set; } = 6 * 0.0254;
    /// <summary>Draft of the step treads (plans: 7 deg minimum), degrees.</summary>
    public double DraftDegrees { get; set; } = 7;
    /// <summary>Boundary line of each layer, lowest line first (the plans: R5, R4, R3). Layer k is the strip of the full-width cap
    /// above its line, StepSetback / count thick, on each face. Where the lines coincide the layers form one step of their summed thickness.</summary>
    public List<BandChain> Layers { get; set; } = new();
    /// <summary>Where the numbers came from / how the shape was derived (shown in notes).</summary>
    public string Source { get; set; } = "";
    /// <summary>Stepped cap top (own back / ahead profiles): true = the layer lines of BOTH faces are measured from the LOWER half's top line (Pier 6A: the
    /// structural sheets dimension the beak and band depths 5'-0 / 8'-6 from the face-of-cap top 1012.51 of the lower half, section A-A ends the 5c3 hairpins
    /// hung from that top at the step on both faces, and the 5a11 (E.F.) relief bar is one mark for both faces); false = each face from its own half's top.</summary>
    public bool BandFromLowerTop { get; set; }

    public CapAestheticsDefinition Clone() => new()
    {
        Face = Face, StepSetback = StepSetback, DraftDegrees = DraftDegrees, Source = Source, BandFromLowerTop = BandFromLowerTop,
        Layers = Layers.Select(l => l.Clone()).ToList(),
    };
}
