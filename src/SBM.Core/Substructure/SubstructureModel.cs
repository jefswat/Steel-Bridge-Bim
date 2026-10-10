using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Substructure;

public enum SubElementKind { Cap, Pedestal, Backwall, Column, DrilledShaft, Footing, Pile, DeckDrain, DrainPipe, SplashBasin }

/// <summary>A solid of the substructure with its quantity data.</summary>
public sealed record SubSolid(SubElementKind Kind, string Name, string Description, Mesh Mesh, bool IsConcrete = true);

/// <summary>Pedestal (bearing seat build-up) under one bearing. S/N = center in cap coordinates.</summary>
public sealed record PedestalInfo(int Girder, double S, double N, double LengthS, double WidthN, double CapTopZ, double SeatZ)
{
    public double Height => SeatZ - CapTopZ;
}

/// <summary>Column (pier) in cap coordinates; top is where it meets the cap bottom at its center.</summary>
public sealed record ColumnInfo(int Index, double S, double N, double TopZ, double BottomZ, ColumnShape Shape, double SizeS, double SizeN);

/// <summary>A pile in cap coordinates: (S, N, TopZ) is the head centre (cut horizontal), (TipS, TipN, TipZ) the tip centre; they
/// differ in plan for a battered pile. Plumb piles: TipS = S, TipN = N.</summary>
public sealed record PileInfo(double S, double N, double TopZ, double TipZ, double TipS, double TipN)
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
    public List<PedestalInfo> Pedestals { get; } = new();
    public List<ColumnInfo> Columns { get; } = new();
    public List<FoundationInfo> Foundations { get; } = new();
    /// <summary>Abutment backwall: front/back faces (N) and top profile (S, Z); bottom is the cap top.</summary>
    public (double N0, double N1, List<Vec2> Top)? Backwall { get; set; }
    public List<SubSolid> Solids { get; } = new();

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
    public List<string> Notes { get; } = new();
    public List<Diagnostic> Diagnostics { get; } = new();
    public TimeSpan GenerationTime { get; set; }
    public double ConcreteVolume => Units.SelectMany(u => u.Solids).Where(s => s.IsConcrete).Sum(s => Math.Abs(s.Mesh.Volume()));
    public double RebarMass => Marks.Sum(m => m.TotalMass);
}
