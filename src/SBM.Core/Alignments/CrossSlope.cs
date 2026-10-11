namespace SBM.Core.Alignments;

/// <summary>
/// Cross-slope (superelevation) definition point. Slopes are rise/run (e.g. -0.02 = 2% falling away from
/// the pivot). Left slope applies left of the pivot, right slope applies right of the pivot.
/// </summary>
public sealed class CrossSlopeRow
{
    public double Station { get; set; }
    public double LeftSlope { get; set; } = -0.02;
    public double RightSlope { get; set; } = -0.02;
    /// <summary>Optional slope break on the left: distance from the pivot (0 = none) beyond which
    /// <see cref="LeftOuterSlope"/> applies (e.g. a gore or shoulder sloped differently from the lanes).</summary>
    public double LeftBreak { get; set; }
    public double LeftOuterSlope { get; set; } = double.NaN;
    /// <summary>Optional slope break on the right (see <see cref="LeftBreak"/>).</summary>
    public double RightBreak { get; set; }
    public double RightOuterSlope { get; set; } = double.NaN;
    /// <summary>Further breaks on the left beyond <see cref="LeftBreak"/>, in increasing distance from the pivot (e.g. a
    /// second lane, a curb and a gore each sloped differently); each slope applies beyond its distance.</summary>
    public List<CrossSlopeBreak>? LeftMoreBreaks { get; set; }
    /// <summary>Further breaks on the right (see <see cref="LeftMoreBreaks"/>).</summary>
    public List<CrossSlopeBreak>? RightMoreBreaks { get; set; }

    public CrossSlopeRow() { }
    public CrossSlopeRow(double station, double left, double right)
    {
        Station = station;
        LeftSlope = left;
        RightSlope = right;
    }
}

/// <summary>A slope break: beyond <see cref="Distance"/> from the pivot, <see cref="Slope"/> applies.</summary>
public sealed class CrossSlopeBreak
{
    public double Distance { get; set; }
    public double Slope { get; set; }
    public CrossSlopeBreak() { }
    public CrossSlopeBreak(double distance, double slope) { Distance = distance; Slope = slope; }
}

/// <summary>
/// Station-based cross-slope table, linearly interpolated between rows and held constant beyond the ends.
/// The pivot (crown / profile grade point) is at <see cref="PivotOffset"/> from the alignment (right positive).
/// </summary>
public sealed class CrossSlopeTable
{
    public List<CrossSlopeRow> Rows { get; set; } = new();

    /// <summary>Offset of the profile grade / crown point from the alignment (right positive).</summary>
    public double PivotOffset { get; set; }

    public static CrossSlopeTable NormalCrown(double slope = -0.02) => new() { Rows = { new CrossSlopeRow(0, slope, slope) } };

    public List<string> Validate()
    {
        var errors = new List<string>();
        for (int i = 1; i < Rows.Count; i++)
            if (Rows[i].Station < Rows[i - 1].Station)
                errors.Add($"Cross slope row {i + 1}: stations must be increasing.");
        return errors;
    }

    /// <summary>
    /// One side at a station: slope next to the pivot, then breaks (distance from the pivot, slope beyond it) in
    /// increasing distance. <see cref="Break"/> / <see cref="Outer"/> are the first break.
    /// </summary>
    public readonly record struct Side(double Slope, (double At, double Slope)[]? Breaks = null)
    {
        public double Break => Breaks is { Length: > 0 } b ? b[0].At : 0;
        public double Outer => Breaks is { Length: > 0 } b ? b[0].Slope : Slope;
        public double DeltaAt(double distance)
        {
            double z = 0, s = Slope, x0 = 0;
            if (Breaks != null)
                foreach (var (at, sl) in Breaks)
                {
                    if (distance <= at) break;
                    z += s * (at - x0); x0 = at; s = sl;
                }
            return z + s * (distance - x0);
        }
        /// <summary>Slope in force at a distance from the pivot (the segment ending at or after it; a break applies beyond its distance).</summary>
        public double SlopeAt(double distance)
        {
            double s = Slope;
            if (Breaks != null)
                foreach (var (at, sl) in Breaks)
                {
                    if (distance <= at) break;
                    s = sl;
                }
            return s;
        }
        /// <summary>Segments out to a distance: (from, to, slope).</summary>
        public IEnumerable<(double From, double To, double Slope)> Segments(double distance)
        {
            double s = Slope, x0 = 0;
            if (Breaks != null)
                foreach (var (at, sl) in Breaks)
                {
                    if (distance <= at) break;
                    yield return (x0, at, s); x0 = at; s = sl;
                }
            yield return (x0, distance, s);
        }
    }

    /// <summary>Both sides with their breaks, interpolated like the slopes, break by break. A row with fewer breaks
    /// counts as having the neighbor's further breaks with an unchanged slope (so the surface stays continuous).</summary>
    public (Side left, Side right) SidesAt(double station)
    {
        static Side Of(double slope, double brk, double outer, List<CrossSlopeBreak>? more)
        {
            if (brk <= 0) return new(slope);
            var list = new List<(double, double)> { (brk, double.IsNaN(outer) ? slope : outer) };
            if (more != null)
                foreach (var m in more.Where(m => !double.IsNaN(m.Slope)).OrderBy(m => m.Distance))
                    if (m.Distance > list[^1].Item1) list.Add((m.Distance, m.Slope));
            return new(slope, list.ToArray());
        }
        static Side Lerp(Side a, Side b, double t)
        {
            var ba = a.Breaks ?? Array.Empty<(double At, double Slope)>();
            var bb = b.Breaks ?? Array.Empty<(double At, double Slope)>();
            int n = Math.Max(ba.Length, bb.Length);
            var res = new (double, double)[n];
            for (int k = 0; k < n; k++)
            {
                // A missing break: the neighbour's distance, with this side's slope just inside it.
                double sa = k < ba.Length ? ba[k].Slope : ba.Length > 0 ? ba[^1].Slope : a.Slope;
                double sb = k < bb.Length ? bb[k].Slope : bb.Length > 0 ? bb[^1].Slope : b.Slope;
                double at = k < ba.Length && k < bb.Length ? ba[k].At + (bb[k].At - ba[k].At) * t : k < ba.Length ? ba[k].At : bb[k].At;
                res[k] = (at, sa + (sb - sa) * t);
            }
            return new(a.Slope + (b.Slope - a.Slope) * t, n > 0 ? res : null);
        }
        if (Rows.Count == 0) return (default, default);
        Side L(CrossSlopeRow r) => Of(r.LeftSlope, r.LeftBreak, r.LeftOuterSlope, r.LeftMoreBreaks);
        Side R(CrossSlopeRow r) => Of(r.RightSlope, r.RightBreak, r.RightOuterSlope, r.RightMoreBreaks);
        if (station <= Rows[0].Station) return (L(Rows[0]), R(Rows[0]));
        if (station >= Rows[^1].Station) return (L(Rows[^1]), R(Rows[^1]));
        int i = 1;
        while (Rows[i].Station < station) i++;
        var a = Rows[i - 1];
        var b = Rows[i];
        double span = b.Station - a.Station;
        double t = span > 0 ? (station - a.Station) / span : 1;
        return (Lerp(L(a), L(b), t), Lerp(R(a), R(b), t));
    }

    public (double left, double right) SlopesAt(double station)
    {
        if (Rows.Count == 0) return (0, 0);
        if (station <= Rows[0].Station) return (Rows[0].LeftSlope, Rows[0].RightSlope);
        if (station >= Rows[^1].Station) return (Rows[^1].LeftSlope, Rows[^1].RightSlope);
        int i = 1;
        while (Rows[i].Station < station) i++;
        var a = Rows[i - 1];
        var b = Rows[i];
        double span = b.Station - a.Station;
        double t = span > 0 ? (station - a.Station) / span : 1;
        return (a.LeftSlope + (b.LeftSlope - a.LeftSlope) * t, a.RightSlope + (b.RightSlope - a.RightSlope) * t);
    }

    /// <summary>Elevation difference from the profile grade at the given offset (right positive).</summary>
    public double DeltaElevation(double station, double offset)
    {
        var (l, r) = SidesAt(station);
        double d = offset - PivotOffset;
        return d >= 0 ? r.DeltaAt(d) : l.DeltaAt(-d);
    }

    public CrossSlopeTable Clone() => new()
    {
        PivotOffset = PivotOffset,
        Rows = Rows.Select(r => new CrossSlopeRow(r.Station, r.LeftSlope, r.RightSlope)
        {
            LeftBreak = r.LeftBreak, LeftOuterSlope = r.LeftOuterSlope, RightBreak = r.RightBreak, RightOuterSlope = r.RightOuterSlope,
            LeftMoreBreaks = r.LeftMoreBreaks?.Select(m => new CrossSlopeBreak(m.Distance, m.Slope)).ToList(),
            RightMoreBreaks = r.RightMoreBreaks?.Select(m => new CrossSlopeBreak(m.Distance, m.Slope)).ToList(),
        }).ToList(),
    };
}
