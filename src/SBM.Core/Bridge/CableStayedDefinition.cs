using SBM.Core.Geometry;

namespace SBM.Core.Bridge;

/// <summary>
/// CABLE-STAYED footbridge (OPTIONAL <see cref="BridgeDefinition.CableStayed"/>, null = none; own bridge with <c>Girders.Count = 0</c> like the arch / truss):
/// a deck swept along the alignment and profile between its end supports, ONE pylon on a named support line (a trunk from the foundation to the leg
/// junction, any number of curved legs = cubic curves from start / tip points and tangent angles, cross beams between the legs, point-to-point struts),
/// stay groups from deck anchors (or explicit ground anchors) to points along a leg, and simple piers / abutment seats at the other support lines.
/// Everything is a parameter: the tool is for finding the shape (analysis and design), so the starting values of a concept are inputs, not rules
/// (<see cref="CableStayedConcepts"/>). Coordinates: meters; the PYLON FRAME has its origin at the pylon support's alignment point at
/// <see cref="CsPylonDefinition.BaseElevation"/>, x = the alignment tangent there turned by <see cref="CsPylonDefinition.RotationDeg"/>, y = RIGHT,
/// z = up. Generator <c>SBM.Core.CableStayed.CableStayedGenerator</c>.
/// </summary>
public sealed class CableStayedDefinition
{
    /// <summary>What this is (e.g. "Richmond Greenway osprey concept, values fitted to the renderings").</summary>
    public string Concept { get; set; } = "";
    /// <summary>Where the values come from (renderings, sketches, a design).</summary>
    public string Source { get; set; } = "";
    /// <summary>Open items (page / source + plain reason), raised as UNRESOLVED warnings; parts are flagged dimension-only when this is not empty.</summary>
    public List<string> Unresolved { get; set; } = new();
    public CsDeckDefinition Deck { get; set; } = new();
    public CsPylonDefinition Pylon { get; set; } = new();
    public List<CsStayGroup> Stays { get; set; } = new();
    /// <summary>Straight members between two pylon-frame points (the concept's "legs" / props below the deck, ties).</summary>
    public List<CsStrut> Struts { get; set; } = new();
    public CsPierDefinition Piers { get; set; } = new();
    public CsAnalysisDefinition Analysis { get; set; } = new();

    public CableStayedDefinition Clone()
    {
        var c = (CableStayedDefinition)MemberwiseClone();
        c.Unresolved = Unresolved.ToList();
        c.Deck = Deck.Clone();
        c.Pylon = Pylon.Clone();
        c.Stays = Stays.Select(s => s.Clone()).ToList();
        c.Struts = Struts.Select(s => s.Clone()).ToList();
        c.Piers = Piers.Clone();
        c.Analysis = Analysis.Clone();
        return c;
    }

    public List<string> Validate()
    {
        var e = new List<string>();
        if (Deck.Section.Count < 3 && (Deck.TopWidth <= 0 || Deck.Depth <= 0)) e.Add("Cable-stayed deck: give Deck.Section (3+ points) or TopWidth and Depth.");
        if (Pylon.Legs.Count == 0) e.Add("Cable-stayed pylon: at least one leg is required.");
        if (string.IsNullOrWhiteSpace(Pylon.Support)) e.Add("Cable-stayed pylon: Pylon.Support (support line name) is required.");
        if (double.IsNaN(Pylon.BaseElevation)) e.Add("Cable-stayed pylon: Pylon.BaseElevation is required.");
        foreach (var s in Stays)
        {
            if (s.Leg < 0 || s.Leg >= Pylon.Legs.Count) e.Add($"Stay group '{s.Name}': leg {s.Leg} does not exist.");
            if (s.Count < 1 && s.GroundAnchors.Count == 0) e.Add($"Stay group '{s.Name}': Count must be 1 or more.");
        }
        return e;
    }
}

public enum CsMaterial { Steel, Concrete }

/// <summary>Point of the cable-stayed inputs (JSON-friendly; converts to / from <see cref="Vec3"/>).</summary>
public sealed record CsXyz(double X, double Y, double Z)
{
    public static implicit operator Vec3(CsXyz p) => new(p.X, p.Y, p.Z);
    public static implicit operator CsXyz(Vec3 v) => new(v.X, v.Y, v.Z);
}

/// <summary>2D point of the cable-stayed inputs (deck section: offset right +, height).</summary>
public sealed record CsXy(double X, double Y)
{
    public static implicit operator Vec2(CsXy p) => new(p.X, p.Y);
    public static implicit operator CsXy(Vec2 v) => new(v.X, v.Y);
}

/// <summary>Deck: one closed section swept along the alignment / profile (no cross slope: give a sloped section for a crossfall).</summary>
public sealed class CsDeckDefinition
{
    /// <summary>Section outline in (offset RIGHT +, height above the finished grade at the CL), counter-clockwise; empty = trapezoid from the widths below.</summary>
    public List<CsXy> Section { get; set; } = new();
    public double TopWidth { get; set; }
    public double BottomWidth { get; set; }
    /// <summary>Structural depth at the CL.</summary>
    public double Depth { get; set; }
    /// <summary>Height of the vertical fascia at each edge (0 = the sloped web runs to the top corner).</summary>
    public double EdgeDepth { get; set; }
    public CsMaterial Material { get; set; } = CsMaterial.Steel;
    /// <summary>Equivalent wall thickness of a steel box deck (analysis section = hollow outline); 0 = solid section.</summary>
    public double WallThickness { get; set; }
    /// <summary>Deck ends: stations (NaN = the first / last support station + <see cref="EndOverhang"/>).</summary>
    public double BeginStation { get; set; } = double.NaN;
    public double EndStation { get; set; } = double.NaN;
    public double EndOverhang { get; set; }
    /// <summary>Distance between deck rings along the alignment (smaller = smoother curved decks).</summary>
    public double RingSpacing { get; set; } = 1.5;
    /// <summary>Barrier panels on both edges (glass / screen): height above the deck top, thickness, inset from the edge; 0 height = none.</summary>
    public double BarrierHeight { get; set; }
    public double BarrierThickness { get; set; } = 0.025;
    public double BarrierInset { get; set; } = 0.15;
    public string BarrierMaterial { get; set; } = "glass";
    public double BarrierDensity { get; set; } = 2500;
    public CsDeckDefinition Clone() { var c = (CsDeckDefinition)MemberwiseClone(); c.Section = Section.ToList(); return c; }

    /// <summary>The outline used: <see cref="Section"/> or the trapezoid (top flat at height 0).</summary>
    public List<Vec2> Outline()
    {
        if (Section.Count >= 3) return Section.Select(p => (Vec2)p).ToList();
        double t = TopWidth / 2, b = (BottomWidth > 0 ? BottomWidth : TopWidth) / 2, e = Math.Min(EdgeDepth, Depth);
        var o = new List<Vec2> { new(-b, -Depth), new(b, -Depth) };
        if (e > 0) { o.Add(new(t, -e)); o.Add(new(t, 0)); o.Add(new(-t, 0)); o.Add(new(-t, -e)); }
        else { o.Add(new(t, 0)); o.Add(new(-t, 0)); }
        return o;
    }
}

/// <summary>Rectangular (optionally hollow) section that may taper: width = horizontal, normal to the member; depth = the other way.</summary>
public sealed class CsSection
{
    public double Width { get; set; }
    public double Depth { get; set; }
    /// <summary>At the far end (NaN = constant).</summary>
    public double EndWidth { get; set; } = double.NaN;
    public double EndDepth { get; set; } = double.NaN;
    /// <summary>Wall thickness of a box (analysis properties); 0 = solid.</summary>
    public double Wall { get; set; }
    public CsMaterial Material { get; set; } = CsMaterial.Steel;
    public CsSection() { }
    public CsSection(double w, double d, double ew = double.NaN, double ed = double.NaN, double wall = 0) { Width = w; Depth = d; EndWidth = ew; EndDepth = ed; Wall = wall; }
    public double WidthAt(double u) => double.IsNaN(EndWidth) ? Width : Width + (EndWidth - Width) * u;
    public double DepthAt(double u) => double.IsNaN(EndDepth) ? Depth : Depth + (EndDepth - Depth) * u;
    public CsSection Clone() => (CsSection)MemberwiseClone();
}

public sealed class CsPylonDefinition
{
    /// <summary>Support line (name) the pylon stands on: the pylon frame origin is its alignment point.</summary>
    public string Support { get; set; } = "";
    /// <summary>Elevation of the pylon frame origin = top of the foundation (required).</summary>
    public double BaseElevation { get; set; } = double.NaN;
    /// <summary>Pylon frame x turned from the alignment tangent at the support (plan degrees, counter-clockwise +).</summary>
    public double RotationDeg { get; set; }
    /// <summary>Frame origin moved along x / y (pylon off the CL).</summary>
    public double OriginX { get; set; }
    public double OriginY { get; set; }
    /// <summary>Trunk: base (z = 0) to the leg junction (z = <see cref="TrunkHeight"/>); 0 = no trunk.</summary>
    public double TrunkHeight { get; set; }
    public CsSection TrunkSection { get; set; } = new();
    /// <summary>Lean of the trunk top from its base (x, y in the frame).</summary>
    public double TrunkTopX { get; set; }
    public double TrunkTopY { get; set; }
    public List<CsPylonLeg> Legs { get; set; } = new();
    public List<CsCrossBeam> CrossBeams { get; set; } = new();
    /// <summary>Footing under the base: size along x / y and thickness (0 = none).</summary>
    public double FootingLength { get; set; }
    public double FootingWidth { get; set; }
    public double FootingThickness { get; set; }
    /// <summary>Footing centre along the pylon frame x (feet of legs that lean in side view).</summary>
    public double FootingX { get; set; }
    /// <summary>The deck bears on the pylon (vertical support at the pylon station in the analysis model and the force estimate).</summary>
    public bool DeckBearsOnPylon { get; set; } = true;
    public CsPylonDefinition Clone()
    {
        var c = (CsPylonDefinition)MemberwiseClone();
        c.TrunkSection = TrunkSection.Clone(); c.Legs = Legs.Select(l => l.Clone()).ToList(); c.CrossBeams = CrossBeams.Select(b => b.Clone()).ToList();
        return c;
    }
}

/// <summary>
/// One pylon leg: a cubic curve from <see cref="Start"/> to <see cref="Tip"/> (pylon frame). Its start and tip tangents are given as LEAN (degrees from
/// plumb in the x-z plane, + toward +x) and SPLAY (degrees from plumb in the y-z plane, + toward +y); the handle lengths are <see cref="StartHandle"/> /
/// <see cref="TipHandle"/> x the chord. Straight leg: both directions along the chord (<see cref="Straight"/>).
/// </summary>
public sealed class CsPylonLeg
{
    public string Name { get; set; } = "";
    public CsXyz Start { get; set; } = new(0, 0, 0);
    public CsXyz Tip { get; set; } = new(0, 0, 0);
    public double StartLeanDeg { get; set; }
    public double StartSplayDeg { get; set; }
    public double TipLeanDeg { get; set; }
    public double TipSplayDeg { get; set; }
    public double StartHandle { get; set; } = 1.0 / 3;
    public double TipHandle { get; set; } = 1.0 / 3;
    public bool Straight { get; set; }
    /// <summary>Section at the start (Width / Depth) tapering to the tip (EndWidth / EndDepth).</summary>
    public CsSection Section { get; set; } = new();
    /// <summary>Sweep segments along the curve.</summary>
    public int Segments { get; set; } = 32;
    public CsPylonLeg Clone() { var c = (CsPylonLeg)MemberwiseClone(); c.Section = Section.Clone(); return c; }

    /// <summary>Mirror about the pylon x-z plane (the other leg of a symmetric pair).</summary>
    public CsPylonLeg Mirrored(string name)
    {
        var c = Clone();
        c.Name = name;
        c.Start = new CsXyz(Start.X, -Start.Y, Start.Z);
        c.Tip = new CsXyz(Tip.X, -Tip.Y, Tip.Z);
        c.StartSplayDeg = -StartSplayDeg; c.TipSplayDeg = -TipSplayDeg;
        return c;
    }
}

/// <summary>Cross beam between two legs at the points of the legs nearest the given height (pylon frame z).</summary>
public sealed class CsCrossBeam
{
    public string Name { get; set; } = "";
    public int LegA { get; set; }
    public int LegB { get; set; } = 1;
    public double Height { get; set; }
    public CsSection Section { get; set; } = new();
    public CsCrossBeam Clone() { var c = (CsCrossBeam)MemberwiseClone(); c.Section = Section.Clone(); return c; }
}

/// <summary>Straight member between two pylon-frame points.</summary>
public sealed class CsStrut
{
    public string Name { get; set; } = "";
    public CsXyz From { get; set; } = new(0, 0, 0);
    public CsXyz To { get; set; } = new(0, 0, 0);
    public CsSection Section { get; set; } = new();
    /// <summary>Footing at the lower end (size x, y, thickness; 0 = none).</summary>
    public double FootingLength { get; set; }
    public double FootingWidth { get; set; }
    public double FootingThickness { get; set; }
    public CsStrut Clone() { var c = (CsStrut)MemberwiseClone(); c.Section = Section.Clone(); return c; }
}

/// <summary>
/// Stay group: <see cref="Count"/> stays from deck anchors equally spaced from <see cref="FirstStation"/> to <see cref="LastStation"/> (at
/// <see cref="DeckOffset"/>, <see cref="DeckAnchorHeight"/> above the finished grade at the CL) to points of leg <see cref="Leg"/> equally spaced
/// along its length from <see cref="PylonFrom"/> to <see cref="PylonTo"/> (fractions of the leg length; equal = fan). The deck anchor farthest from the
/// pylon goes to the highest pylon point (no crossing stays). <see cref="GroundAnchors"/> (pylon frame points) replace the deck anchors (back stays).
/// </summary>
public sealed class CsStayGroup
{
    public string Name { get; set; } = "";
    public int Leg { get; set; }
    public int Count { get; set; }
    public double FirstStation { get; set; }
    public double LastStation { get; set; }
    public double DeckOffset { get; set; }
    public double DeckAnchorHeight { get; set; }
    public List<CsXyz> GroundAnchors { get; set; } = new();
    public double PylonFrom { get; set; } = 0.5;
    public double PylonTo { get; set; } = 1.0;
    /// <summary>Stay diameter (solid envelope; analysis area = <see cref="Area"/> when > 0, else 0.6 x the circle = parallel strand void ratio).</summary>
    public double Diameter { get; set; } = 0.05;
    public double Area { get; set; }
    public string Type { get; set; } = "parallel strand stay";
    public CsStayGroup Clone() { var c = (CsStayGroup)MemberwiseClone(); c.GroundAnchors = GroundAnchors.ToList(); return c; }
}

/// <summary>Piers at the support lines other than the pylon's: one column under the deck CL (round when <see cref="ColumnDepth"/> = 0) on a footing;
/// the first / last support get an abutment seat block instead (0 sizes = nothing built).</summary>
public sealed class CsPierDefinition
{
    public double ColumnWidth { get; set; }
    public double ColumnDepth { get; set; }
    /// <summary>Column base elevation (NaN = ground unknown: columns not built, UNRESOLVED).</summary>
    public double BaseElevation { get; set; } = double.NaN;
    /// <summary>Per support line (name): column base elevation; overrides <see cref="BaseElevation"/>.</summary>
    public Dictionary<string, double> BaseElevations { get; set; } = new();
    public double FootingLength { get; set; }
    public double FootingWidth { get; set; }
    public double FootingThickness { get; set; }
    /// <summary>Abutment seat: length along the alignment, extra width beyond the deck, depth below the deck soffit.</summary>
    public double AbutmentLength { get; set; }
    public double AbutmentExtraWidth { get; set; }
    public double AbutmentDepth { get; set; }
    public CsPierDefinition Clone() { var c = (CsPierDefinition)MemberwiseClone(); c.BaseElevations = new(BaseElevations); return c; }
    public double BaseFor(string support) => BaseElevations.TryGetValue(support, out var z) ? z : BaseElevation;
}

/// <summary>Preliminary analysis inputs (the generated analysis model + first-cut stay forces; not a design check by itself).</summary>
public sealed class CsAnalysisDefinition
{
    /// <summary>Superimposed dead load on the deck (surfacing, barriers beyond the modeled panels, lighting), N/m along the deck.</summary>
    public double SuperimposedDeadLoad { get; set; }
    /// <summary>Pedestrian live load, Pa (AASHTO LRFD Guide Spec. for pedestrian bridges: 90 psf = 4309 Pa).</summary>
    public double PedestrianLoad { get; set; } = 4309;
    /// <summary>Stay tensile strength (Pa, 270 ksi strand = 1862 MPa) and the allowed fraction under service load (PTI: 0.45).</summary>
    public double StayUltimateStrength { get; set; } = 1.862e9;
    public double StayAllowableFraction { get; set; } = 0.45;
    public double SteelDensity { get; set; } = 7850;
    public double ConcreteDensity { get; set; } = 2400;
    public double SteelModulus { get; set; } = 200e9;
    public double ConcreteModulus { get; set; } = 30e9;
    public double StayModulus { get; set; } = 195e9;
    /// <summary>Analysis node spacing along the deck between anchors / supports (m).</summary>
    public double DeckNodeSpacing { get; set; } = 3.0;
    /// <summary>Vertical clearance targets under the deck (roads, tracks): the lowest deck soffit over the station range vs the surface + required.</summary>
    public List<CsClearance> Clearances { get; set; } = new();
    public CsAnalysisDefinition Clone() { var c = (CsAnalysisDefinition)MemberwiseClone(); c.Clearances = Clearances.Select(x => x with { }).ToList(); return c; }
}

/// <summary>Clearance target: from / to station along the alignment, elevation of the surface below (top of rail, road crown), required clearance.</summary>
public sealed record CsClearance(string Name, double FromStation, double ToStation, double SurfaceElevation, double Required, string Source = "");
