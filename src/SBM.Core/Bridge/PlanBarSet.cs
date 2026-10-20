using System.Globalization;
using System.Text;
using SBM.Core.Geometry;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Bridge;

/// <summary>
/// OPTIONAL explicit plan reinforcing: one mark of a plan bar table placed as the plan draws it, with every bar given as a centerline
/// polyline computed by a plan-data script from the sheet's dimensions (spacings, covers, bend diagrams). Generic: on a substructure unit
/// (<see cref="SubstructureReinforcementDefinition.PlanBarSets"/>, coordinates = the unit frame S along the support line, N normal,
/// Z = elevation) or on a deck (<see cref="DeckReinforcementDefinition.PlanBarSets"/>, coordinates = project plan X / Y and elevation Z).
/// When a unit / deck has sets, the generic generator does not run for it: the sets ARE the plan's bars. Counts and listed lengths are the
/// plan's; bars are never moved for cover - the generic clear-cover check (<c>Concrete.PlanBarSetBuilder</c>) reports violations.
/// </summary>
public sealed class PlanBarSet
{
    /// <summary>Plan mark ("A", "S2", "V1"); unique within the unit / deck.</summary>
    public string Mark { get; set; } = "";
    public string Size { get; set; } = "#5";
    /// <summary>Where the bars are ("Pier 2 cap stirrups"), shown in the schedule / properties.</summary>
    public string Location { get; set; } = "";
    /// <summary>Element the bars belong to for reports ("cap", "column", "drilled shaft", "deck", "curb"...).</summary>
    public string Element { get; set; } = "";
    /// <summary>Coating ("galvanized", "epoxy"); "" = uncoated.</summary>
    public string Coating { get; set; } = "";
    /// <summary>Plan bend-diagram code ("STR", "U", "L", "HOOP", "TIE", "HAIRPIN", "SPIRAL", "BENT") and its printed out-to-out dimensions (meters, letter order A, B, ...).</summary>
    public string Shape { get; set; } = "STR";
    public double[] Dims { get; set; } = Array.Empty<double>();
    /// <summary>Listed (scheduled) length from the plan bar table, meters; 0 = the geometry's developed length (bars with different lengths get a varying mark).</summary>
    public double ListedLength { get; set; }
    /// <summary>OPTIONAL printed plan bend pin diameter D (0 = the standard minimum, <see cref="SBM.Core.Concrete.RebarBends.StandardPin"/>).</summary>
    public double Pin { get; set; }
    /// <summary>Clear cover the plan requires for these bars (meters) to every concrete face; <see cref="CoverBottom"/> / <see cref="CoverTop"/> override it at faces looking down / up.</summary>
    public double Cover { get; set; } = In(2);
    public double CoverBottom { get; set; } = double.NaN;
    public double CoverTop { get; set; } = double.NaN;
    /// <summary>false = no cover check (bars that leave the concrete by design, e.g. bearing dowels projecting above the seat).</summary>
    public bool CheckCover { get; set; } = true;
    /// <summary>Bar centerlines: one string per bar, points "x,y,z" separated by spaces (meters, 4 decimals). See <see cref="Encode"/>.</summary>
    public List<string> Bars { get; set; } = new();
    /// <summary>Spirals (generated): one per entry.</summary>
    public List<PlanSpiral> Spirals { get; set; } = new();
    /// <summary>Bars of the plan table that are not placed (location not shown on the sheets): counted in the mark, no 3D bar, reason in <see cref="Unresolved"/>.</summary>
    public int Unplaced { get; set; }
    /// <summary>"UNRESOLVED (p.x): ..." text for anything about this mark the plans do not settle (raised as a warning); "" = none.</summary>
    public string Unresolved { get; set; } = "";
    /// <summary>Plan basis / notes (Info): how the positions were read ("scaled", "per Section A-A").</summary>
    public string Note { get; set; } = "";
    /// <summary>Plan table values for the check (count, weight lb); null = not tabulated.</summary>
    public int? PlanCount { get; set; }
    public double? PlanWeightLb { get; set; }
    /// <summary>Plan page / sheet.</summary>
    public string Source { get; set; } = "";

    public PlanBarSet Clone()
    {
        var c = (PlanBarSet)MemberwiseClone();
        c.Dims = Dims.ToArray();
        c.Bars = Bars.ToList();
        c.Spirals = Spirals.Select(s => s.Clone()).ToList();
        return c;
    }

    /// <summary>Encodes a polyline as the <see cref="Bars"/> string (meters, 4 decimals = 0.1 mm).</summary>
    public static string Encode(IEnumerable<Vec3> pts)
    {
        var sb = new StringBuilder();
        foreach (var p in pts)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(p.X.ToString("0.####", CultureInfo.InvariantCulture)).Append(',')
              .Append(p.Y.ToString("0.####", CultureInfo.InvariantCulture)).Append(',')
              .Append(p.Z.ToString("0.####", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    public static Vec3[] Decode(string s) =>
        s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(t =>
        {
            var v = t.Split(',');
            return new Vec3(double.Parse(v[0], CultureInfo.InvariantCulture), double.Parse(v[1], CultureInfo.InvariantCulture), double.Parse(v[2], CultureInfo.InvariantCulture));
        }).ToArray();

    public void Add(IEnumerable<Vec3> pts) => Bars.Add(Encode(pts));

    public int PlacedCount => Bars.Count + Spirals.Count;
}

/// <summary>
/// A circular spiral (drilled shaft / column cage): centre (in the set's coordinates), centerline radius, from <see cref="TopZ"/> down to
/// <see cref="BottomZ"/> at <see cref="Pitch"/>, with <see cref="FlatTurns"/> flat (level) turns at each end (TxDOT "one and a half flat turns
/// top and bottom"). Optional second pitch zone: <see cref="UpperPitch"/> from the top down to <see cref="UpperZoneBottomZ"/>.
/// </summary>
public sealed class PlanSpiral
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Radius { get; set; }
    public double TopZ { get; set; }
    public double BottomZ { get; set; }
    public double Pitch { get; set; } = In(6);
    public double FlatTurns { get; set; } = 1.5;
    public double UpperPitch { get; set; }
    public double UpperZoneBottomZ { get; set; } = double.NaN;
    /// <summary>Bar diameter: the flat end turns climb one bar diameter per turn (they rest on each other), m.</summary>
    public double BarDiameter { get; set; } = In(0.5);
    public PlanSpiral Clone() => (PlanSpiral)MemberwiseClone();

    /// <summary>Centerline points (about 24 per turn), top to bottom.</summary>
    public List<Vec3> Points()
    {
        var pts = new List<Vec3>();
        const int perTurn = 24;
        double ang = 0, z = TopZ;
        void Turn(double turns, double pitch)
        {
            int n = Math.Max(1, (int)Math.Round(turns * perTurn));
            double dA = turns * 2 * Math.PI / n, dZ = turns * pitch / n;
            for (int i = 0; i < n; i++)
            {
                ang += dA; z -= dZ;
                pts.Add(new Vec3(X + Radius * Math.Cos(ang), Y + Radius * Math.Sin(ang), z));
            }
        }
        pts.Add(new Vec3(X + Radius, Y, z));
        Turn(FlatTurns, BarDiameter);
        double total = TopZ - BottomZ - 2 * FlatTurns * BarDiameter;
        if (UpperPitch > 0 && !double.IsNaN(UpperZoneBottomZ) && UpperZoneBottomZ > BottomZ && UpperZoneBottomZ < TopZ)
        {
            Turn((TopZ - FlatTurns * BarDiameter - UpperZoneBottomZ) / UpperPitch, UpperPitch);
            Turn((UpperZoneBottomZ - BottomZ - FlatTurns * BarDiameter) / Pitch, Pitch);
        }
        else Turn(total / Pitch, Pitch);
        Turn(FlatTurns, BarDiameter);
        return pts;
    }
}
