using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Alignments;

namespace SBM.Core.Substructure;

public enum SubElementKind { Cap, Pedestal, Backwall, Column, DrilledShaft, Footing, Pile, DeckDrain, DrainPipe, SplashBasin, Coupler, Wingwall, Counterfort, Maskwall, WingFooting, PileCasing, MowStrip }

/// <summary>A solid of the substructure with its quantity data.</summary>
public sealed record SubSolid(SubElementKind Kind, string Name, string Description, Mesh Mesh, bool IsConcrete = true)
{
    /// <summary>Geometry the plans do not fix (OPTIONAL; IFC Pset SBM_Status, database status).</summary>
    public Assumption? Assumption { get; init; }
    /// <summary>CIP pipe pile: steel shell wall thickness of the concrete-filled pipe (0 = not a pipe pile; NaN = pipe pile, shell not given).
    /// The mesh is the outside diameter; IFC writes one IfcPile COMPOSITE with the shell as a property.</summary>
    public double ShellThickness { get; init; }
}

/// <summary>One plan-listed bar outside the clear-cover envelope: Excess = how far (m) the bar surface reaches into the cover zone at its worst point,
/// Face = the governing concrete face (cap top / cap soffit / cap end / cap side N0 / cap side N1 / footing ... / column ...).</summary>
public sealed record CoverViolation(string Location, string Kind, double Excess, string Face, double S, double N, double Z);

/// <summary>Pedestal (bearing seat build-up) under one bearing. S/N = center in cap coordinates.</summary>
public sealed record PedestalInfo(int Girder, double S, double N, double LengthS, double WidthN, double CapTopZ, double SeatZ)
{
    public double Height => SeatZ - CapTopZ;
    /// <summary>The bearing seat's own position when the pedestal geometry (S, N, size) is a shared plan block of several seats; NaN = the pedestal is the seat's.</summary>
    public double SeatS { get; init; } = double.NaN;
    public double SeatN { get; init; } = double.NaN;
    public double RowS => double.IsNaN(SeatS) ? S : SeatS;
    public double RowN => double.IsNaN(SeatN) ? N : SeatN;
    /// <summary>Drawing label when the pedestal carries no bearing of this bridge (standalone pedestal, e.g. "FUTURE BEAM"; Girder = -1). "" = G{Girder + 1}.</summary>
    public string Label { get; init; } = "";
    public string Tag => Label != "" ? Label : $"G{Girder + 1}";
}

/// <summary>A bearing seat with a FIXED plan elevation on a <see cref="CapBlockKind.Pedestal"/> cap block (not a seat of this bridge's superstructure):
/// name of the block, seat point (S, N) in cap coordinates and the block top Z there. World point: <see cref="SubstructureUnit.World"/>.</summary>
public sealed record FixedSeatInfo(string Name, double S, double N, double Z);

/// <summary>Column (pier) in cap coordinates; top is where it meets the cap bottom at its center.</summary>
public sealed record ColumnInfo(int Index, double S, double N, double TopZ, double BottomZ, ColumnShape Shape, double SizeS, double SizeN);

/// <summary>A pile in cap coordinates: (S, N, TopZ) is the head centre (cut horizontal), (TipS, TipN, TipZ) the tip centre; they
/// differ in plan for a battered pile. Plumb piles: TipS = S, TipN = N. (WebS, WebN) = unit plan direction of the H-pile web.</summary>
public sealed record PileInfo(double S, double N, double TopZ, double TipZ, double TipS, double TipN, double WebS = 0, double WebN = 1)
{
    public PileInfo(double s, double n, double topZ, double tipZ) : this(s, n, topZ, tipZ, s, n) { }
    public bool IsBattered => Math.Abs(TipS - S) > 1e-9 || Math.Abs(TipN - N) > 1e-9;
    /// <summary>Length along the pile axis, head to tip.</summary>
    public double Length => Math.Sqrt((TipS - S) * (TipS - S) + (TipN - N) * (TipN - N) + (TopZ - TipZ) * (TopZ - TipZ));
}

/// <summary>Foundation element (shaft, footing or pile cap with piles) in cap coordinates.</summary>
public sealed class FoundationInfo
{
    public required FoundationType Type { get; init; }
    public required double S { get; init; }
    public required double N { get; init; }
    public required double TopZ { get; init; }
    public required double BottomZ { get; init; }
    /// <summary>Shaft diameter, or footing size along the cap.</summary>
    public required double SizeS { get; init; }
    /// <summary>Shaft diameter, or footing size normal to the cap.</summary>
    public required double SizeN { get; init; }
    public List<PileInfo> Piles { get; } = new();
    public double GroundZ { get; init; } = double.NaN;
}

/// <summary>
/// Substructure at one support. Cap coordinates: S along the support line from the alignment point (right positive),
/// N normal to it (up-station positive), Z elevation.
/// </summary>
public sealed class SubstructureUnit
{
    public required int SupportIndex { get; init; }
    public required string Name { get; init; }
    public required SupportType Type { get; init; }
    public required SubstructureDefinition Definition { get; init; }
    public required Vec2 Origin { get; init; }
    /// <summary>Unit direction along the support line (left → right).</summary>
    public required Vec2 D { get; init; }
    /// <summary>Unit normal to the support line, pointing up-station.</summary>
    public required Vec2 N { get; init; }
    public required double Station { get; init; }

    public double CapS0 { get; set; }
    public double CapS1 { get; set; }
    public double CapN0 { get; set; }
    public double CapN1 { get; set; }
    /// <summary>Cap top profile (S, Z) left to right, with breaks.</summary>
    public List<Vec2> CapTop { get; } = new();
    /// <summary>Cap bottom profile (S, Z) left to right.</summary>
    public List<Vec2> CapBottom { get; } = new();
    /// <summary>Stepped caps: top of the up-station half (N greater than <see cref="CapMidN"/>); empty = same as CapTop.</summary>
    public List<Vec2> CapTopAhead { get; } = new();
    public double CapMidN { get; set; }
    /// <summary>As-designed cap outline in (S, Z) when the cap came from a plan profile (and of the ahead half when stepped).</summary>
    public List<Vec2> CapOutline { get; } = new();
    public List<Vec2> CapOutlineAhead { get; } = new();
    public string CapTopDescription { get; set; } = "";
    /// <summary>Texture band lines on the cap face (S, Z), clipped to the cap: the texture edge and each layer line.</summary>
    public List<List<Vec2>> ReliefLines { get; } = new();
    /// <summary>Per entry of <see cref="ReliefLines"/>: the face it belongs to (-1 = back face N0, +1 = ahead face N1) and its layer index (lowest line first).
    /// A layer line clipped to the cap can come in several runs; the two faces have their own lines (6A: own back / ahead profiles).</summary>
    public List<(int Side, int Layer)> ReliefLineTags { get; } = new();
    /// <summary>One entry per bearing seat (SeatZ / Height as the elevation checks need them; with a shared plan pedestal the geometry is the block's).</summary>
    public List<PedestalInfo> Pedestals { get; } = new();
    /// <summary>The pedestal solids: one per block (= <see cref="Pedestals"/> unless a plan pedestal carries several seats). The pedestal bars are placed per block.</summary>
    public List<PedestalInfo> PedestalBlocks { get; } = new();
    /// <summary>Seats on fixed-elevation pedestal cap blocks (<see cref="CapBlockKind.Pedestal"/>), e.g. the arch bearings on an arch pier.</summary>
    public List<FixedSeatInfo> FixedSeats { get; } = new();
    public List<ColumnInfo> Columns { get; } = new();
    public List<FoundationInfo> Foundations { get; } = new();
    /// <summary>Abutment backwall: front/back faces (N) and top profile (S, Z); bottom is the cap top.</summary>
    public (double N0, double N1, List<Vec2> Top)? Backwall { get; set; }
    public List<SubSolid> Solids { get; } = new();
    /// <summary>Plan-listed bars outside the clear-cover envelope (one row per bar at its worst point; filled by the containment check).</summary>
    public List<CoverViolation> CoverViolations { get; } = new();
    /// <summary>As-designed abutment data (footing, backwall, steps, wings...) when the support has an <see cref="AbutmentDefinition"/>; else null.</summary>
    public AbutmentGeometry? Abutment { get; set; }

    public Vec2 Plan(double s, double n) => Origin + D * s + N * n;
    public Vec3 World(double s, double n, double z) => Plan(s, n).WithZ(z);

    public double CapTopAt(double s) => Interp(CapTop, s);
    /// <summary>The top line of the cap: the top envelope without steep end segments (raked / beak end faces of
    /// as-designed caps, steeper than 1:1). Its ends are the top corners; interpolation extrapolates it linearly.</summary>
    public List<Vec2> CapTopLine()
    {
        var t = CapTop.ToList();
        static bool Steep(Vec2 a, Vec2 b) => Math.Abs(b.Y - a.Y) > Math.Abs(b.X - a.X);
        while (t.Count > 2 && Steep(t[0], t[1])) t.RemoveAt(0);
        while (t.Count > 2 && Steep(t[^2], t[^1])) t.RemoveAt(t.Count - 1);
        return t;
    }
    /// <summary>Cap top at (s, n): the ahead half of a stepped cap has its own top.</summary>
    public double CapTopAt(double s, double n) => CapTopAhead.Count > 0 && n > CapMidN ? Interp(CapTopAhead, s) : Interp(CapTop, s);
    public double CapBottomAt(double s) => Interp(CapBottom, s);

    public static double Interp(List<Vec2> pl, double s)
    {
        if (pl.Count == 0) return double.NaN;
        if (s <= pl[0].X) return pl[0].Y + (pl.Count > 1 ? (s - pl[0].X) * (pl[1].Y - pl[0].Y) / Math.Max(pl[1].X - pl[0].X, 1e-9) : 0);
        for (int i = 1; i < pl.Count; i++)
            if (s <= pl[i].X)
            {
                double t = (s - pl[i - 1].X) / Math.Max(pl[i].X - pl[i - 1].X, 1e-12);
                return pl[i - 1].Y + t * (pl[i].Y - pl[i - 1].Y);
            }
        int k = pl.Count - 1;
        return pl[k].Y + (k > 0 ? (s - pl[k].X) * (pl[k].Y - pl[k - 1].Y) / Math.Max(pl[k].X - pl[k - 1].X, 1e-9) : 0);
    }
}

public sealed class SubstructureModel
{
    public List<SubstructureUnit> Units { get; } = new();
    public List<BarMark> Marks { get; } = new();
    public List<BarPlacement> Bars { get; } = new();
    /// <summary>Deck drainage: inlets, downspouts / collector pipes, splash basins (not tied to one support).</summary>
    public List<SubSolid> Drainage { get; } = new();
    /// <summary>Optional misc items (Ramp G drainage, sign supports, end posts, lighting); set by the regeneration cache after the substructure.</summary>
    public Misc.MiscModel? Misc { get; set; }
    /// <summary>Architectural surface treatments (form liner, thin brick) of the bridge, rebuilt after the diaphragms (<see cref="Surface.SurfaceTreatmentGenerator"/>).</summary>
    public Surface.SurfaceTreatmentModel? SurfaceTreatments { get; set; }
    public List<string> Notes { get; } = new();
    public List<Diagnostic> Diagnostics { get; } = new();
    public TimeSpan GenerationTime { get; set; }
    public double ConcreteVolume => Units.SelectMany(u => u.Solids).Where(s => s.IsConcrete).Sum(s => Math.Abs(s.Mesh.Volume()));
    public double RebarMass => Marks.Sum(m => m.TotalMass);
}

/// <summary>A beam step (seat block) of an as-designed abutment.</summary>
public sealed record AbutmentStepInfo(int Girder, double S, double S0, double S1, double TopZ, double BaseZ, double N0, double N1)
{
    /// <summary>Standalone pedestal label (Girder = -1, e.g. "FUTURE BEAM"); "" = G{Girder + 1}.</summary>
    public string Label { get; init; } = "";
    public string Tag => Label != "" ? Label : $"G{Girder + 1}";
}

/// <summary>A counterfort with its wing footing (plan frame: X along the wall rearward, Y = alignment offset).</summary>
public sealed record CounterfortInfo(int Index, double Start, double Width, double Projection, double TopZ, double FootingTop, double FootingBottom,
    double YWall0, double YWall1, double YFoot0, double YFoot1, double YCf0, double YCf1, double StationC, Vec2 Origin, Vec2 Tangent)
{
    /// <summary>Width of the counterfort body along the wall (the footing is <see cref="Width"/>).</summary>
    public double BodyWidth { get; init; }
}

/// <summary>A wingwall of an as-designed abutment: path along the gutter line and elevation functions of u (distance rearward from the CL bearing line).</summary>
public sealed class WingInfo
{
    public required WingwallDefinition Def { get; init; }
    public required double SideSign { get; init; }
    /// <summary>Offsets of the fill-side (inner) and outer faces.</summary>
    public required double EIn { get; init; }
    public required double EOut { get; init; }
    public required double UBack { get; init; }
    public required double Length { get; init; }
    public required Func<double, double> Sta { get; init; }
    public required Func<double, double> Top { get; init; }
    public required Func<double, double> Bottom { get; init; }
    public List<CounterfortInfo> Counterforts { get; } = new();
    /// <summary>Wing footing foundations in counterfort order (index = counterfort number - 1).</summary>
    public List<FoundationInfo> Footings { get; } = new();
    public Vec2 Point(Alignment a, double u, double off) => a.PointAt(Sta(u), off);
}

public sealed record MaskInfo(AbutmentEnd End, double EOuter, double EInner, double TopZ, double BaseZ)
{
    /// <summary>The two ends of the wall's line at the given alignment offset, in cap coordinates (S, N): back (fill side) and front (span side) end.</summary>
    public Func<double, (Vec2 Back, Vec2 Front)>? EndsAt { get; init; }
}

/// <summary>Geometry of an as-designed abutment, shared by the rebar generator, the views and the exporters.</summary>
public sealed class AbutmentGeometry
{
    public required AbutmentDefinition Def { get; init; }
    public required double Sigma { get; init; }
    public required double XFootBack { get; init; }
    public required double XBwBack { get; init; }
    public required double XBwFront { get; init; }
    public required double XStepFront { get; init; }
    public required double XFootFront { get; init; }
    public required Func<double, double> FootingBottom { get; init; }
    public required Func<double, double> BackwallTop { get; init; }
    /// <summary>N to S of the footing end lines and of the backwall ends (planes).</summary>
    public required Func<double, double> LeftEndS { get; init; }
    public required Func<double, double> RightEndS { get; init; }
    public required Func<double, double> BwLeftS { get; init; }
    public required Func<double, double> BwRightS { get; init; }
    public required double JointDepth { get; init; }
    /// <summary>N to S of the corbel ends (= the backwall ends unless <see cref="AbutmentDefinition.CorbelLeftOffset"/> / Right are set).</summary>
    public Func<double, double>? CorbelLeftS { get; set; }
    public Func<double, double>? CorbelRightS { get; set; }
    /// <summary>S (at the backwall back face) from which the backwall vertical strings are measured.</summary>
    public double StringStartS { get; set; } = double.NaN;
    public double N(double x) => Sigma * x;
    public double FootingTopZ(double s) => FootingBottom(s) + Def.FootingDepth;
    public double SeatBaseZ(double s) => FootingBottom(s) + Def.FootingDepth + Def.WashHeight;
    public double BackwallBaseZ(double s) => FootingBottom(s) + Def.BackwallBaseAboveFootingBottom;
    public List<AbutmentStepInfo> Steps { get; } = new();
    public List<WingInfo> Wings { get; } = new();
    public List<MaskInfo> Masks { get; } = new();
    /// <summary>S range of the backwall extension, when present.</summary>
    public (AbutmentEnd End, double S0, double S1, double TopZ)? Extension { get; set; }
    /// <summary>Pile positions of the main footing (S, N).</summary>
    public List<(double S, double N)> PileXY { get; } = new();
    /// <summary>Plan outline of the main footing as built (X = S, Y = N), closed, counter-clockwise or clockwise as generated: back face, right end
    /// (with its notches / corner pieces), front face, left end. Same planes as the footing solid.</summary>
    public List<Vec2> FootingOutline { get; } = new();
    public double[] PileStationsS { get; set; } = Array.Empty<double>();
    // ---- integral abutment (AbutmentKind.Integral; SubstructureAbutmentIntegral.cs)
    /// <summary>Stepped cap top (level per girder step, vertical risers); null = stub abutment or riser positions UNRESOLVED.</summary>
    public Func<double, double>? StepTop { get; set; }
    /// <summary>S of the step risers as built (empty = none / UNRESOLVED).</summary>
    public List<double> StepRisersS { get; } = new();
    /// <summary>Cap depth (end step top - bottom) at the left / right end on the cap centreline (NaN = not available).</summary>
    public double DepthAtLeftEnd { get; set; } = double.NaN;
    public double DepthAtRightEnd { get; set; } = double.NaN;
    /// <summary>Pile casings (CMP) as scheduled; Bottom / Length NaN = UNRESOLVED.</summary>
    public List<PileCasingInfo> Casings { get; } = new();
}

/// <summary>A pile casing: pile position (S, N), top (bottom of the concrete) and bottom elevation (NaN = UNRESOLVED).</summary>
public sealed record PileCasingInfo(double S, double N, double Top, double Bottom, double Diameter)
{
    public double Length => Top - Bottom;
}
