namespace SBM.Core.Alignments;

/// <summary>
/// Vertical point of intersection. LengthIn/LengthOut are the horizontal lengths of the parabolic vertical
/// curve before/after the VPI; equal values give a symmetric curve, zero gives a grade break.
/// </summary>
public sealed class Vpi
{
    public double Station { get; set; }
    public double Elevation { get; set; }
    public double LengthIn { get; set; }
    public double LengthOut { get; set; }

    public Vpi() { }
    public Vpi(double station, double elevation, double curveLength = 0)
    {
        Station = station;
        Elevation = elevation;
        LengthIn = LengthOut = curveLength / 2;
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public double CurveLength => LengthIn + LengthOut;
}

/// <summary>Profile grade line made of tangent grades and (optionally unsymmetrical) parabolic vertical curves.</summary>
public sealed class VerticalProfile
{
    public List<Vpi> Vpis { get; set; } = new();

    public bool IsEmpty => Vpis.Count == 0;

    /// <summary>Returns validation errors (overlapping curves, unsorted stations).</summary>
    public List<string> Validate()
    {
        var errors = new List<string>();
        for (int i = 1; i < Vpis.Count; i++)
        {
            var a = Vpis[i - 1];
            var b = Vpis[i];
            if (b.Station <= a.Station)
                errors.Add($"VPI {i + 1}: station must be greater than VPI {i}.");
            else if (a.Station + a.LengthOut > b.Station - b.LengthIn + 1e-9)
                errors.Add($"Vertical curves at VPI {i} and VPI {i + 1} overlap.");
        }
        if (Vpis.Count > 0 && (Vpis[0].CurveLength > 0 || Vpis[^1].CurveLength > 0))
            errors.Add("First and last VPI cannot have vertical curves.");
        return errors;
    }

    private double GradeBetween(int i) // grade from VPI i to VPI i+1
    {
        var a = Vpis[i];
        var b = Vpis[i + 1];
        return (b.Elevation - a.Elevation) / (b.Station - a.Station);
    }

    /// <summary>
    /// Describes how the elevation at a station is obtained (tangent or vertical curve with its parameters), using the
    /// given formatters for stations, elevations and lengths. The returned elevation equals <see cref="ElevationAt"/>.
    /// </summary>
    public (double elevation, string how) Explain(double station, Func<double, string> sta, Func<double, string> elev, Func<double, string> len)
    {
        string G(double g) => (g * 100).ToString("+0.0000;-0.0000", System.Globalization.CultureInfo.InvariantCulture) + "%";
        int n = Vpis.Count;
        if (n == 0) return (0, "No profile defined (elevation 0)");
        if (n == 1) return (Vpis[0].Elevation, $"Single VPI at {sta(Vpis[0].Station)}");
        int idx = 0;
        while (idx < n - 1 && station > Vpis[idx + 1].Station) idx++;
        for (int k = Math.Max(1, idx); k <= Math.Min(n - 2, idx + 1); k++)
        {
            var v = Vpis[k];
            double pvc = v.Station - v.LengthIn, pvt = v.Station + v.LengthOut;
            if (v.CurveLength <= 0 || station < pvc || station > pvt) continue;
            double g1 = GradeBetween(k - 1), g2 = GradeBetween(k);
            double l1 = v.LengthIn, l2 = v.LengthOut;
            string kind = Math.Abs(l1 - l2) < 1e-9 ? $"L = {len(v.CurveLength)}" : $"L1 = {len(l1)}, L2 = {len(l2)} (unsymmetrical)";
            string type = g2 < g1 ? "crest" : "sag";
            double e = l1 * l2 * (g2 - g1) / (2 * (l1 + l2));
            if (station <= v.Station)
            {
                double x = station - pvc, elevPvc = v.Elevation - g1 * l1, a = e / (l1 * l1);
                double z = elevPvc + g1 * x + a * x * x;
                return (z, $"{type} VC at VPI {sta(v.Station)} ({kind}), g1 {G(g1)}, g2 {G(g2)}: PVC {sta(pvc)} elev {elev(elevPvc)} + g1·x + (g2−g1)/(2L)·x², x = {len(x)}");
            }
            else
            {
                double x = pvt - station, elevPvt = v.Elevation + g2 * l2, a = e / (l2 * l2);
                double z = elevPvt - g2 * x + a * x * x;
                return (z, $"{type} VC at VPI {sta(v.Station)} ({kind}), g1 {G(g1)}, g2 {G(g2)}: PVT {sta(pvt)} elev {elev(elevPvt)} − g2·x + (g2−g1)/(2L)·x², x = {len(x)} back from PVT");
            }
        }
        int seg = Math.Clamp(idx, 0, n - 2);
        double g = GradeBetween(seg);
        double zt = Vpis[seg].Elevation + g * (station - Vpis[seg].Station);
        return (zt, $"tangent: VPI {sta(Vpis[seg].Station)} elev {elev(Vpis[seg].Elevation)} {G(g)} × {len(station - Vpis[seg].Station)}");
    }

    public double ElevationAt(double station) => Evaluate(station).elevation;

    public double GradeAt(double station) => Evaluate(station).grade;

    public (double elevation, double grade) Evaluate(double station)
    {
        int n = Vpis.Count;
        if (n == 0) return (0, 0);
        if (n == 1) return (Vpis[0].Elevation, 0);

        // Find tangent index i such that station is nearest VPI i's region.
        int idx = 0;
        while (idx < n - 1 && station > Vpis[idx + 1].Station) idx++;
        // Now Vpis[idx].Station <= station <= Vpis[idx+1].Station (or extrapolated)

        // Check the curve at VPI idx (its out half) and at VPI idx+1 (its in half).
        for (int k = Math.Max(1, idx); k <= Math.Min(n - 2, idx + 1); k++)
        {
            var v = Vpis[k];
            double pvc = v.Station - v.LengthIn, pvt = v.Station + v.LengthOut;
            if (v.CurveLength <= 0 || station < pvc || station > pvt) continue;

            double g1 = GradeBetween(k - 1), g2 = GradeBetween(k);
            double l1 = v.LengthIn, l2 = v.LengthOut;
            // Unsymmetrical parabola: offset at VPI e = l1·l2·(g2−g1) / (2(l1+l2)).
            double e = l1 * l2 * (g2 - g1) / (2 * (l1 + l2));
            if (station <= v.Station)
            {
                double x = station - pvc;
                double elevPvc = v.Elevation - g1 * l1;
                double a = e / (l1 * l1);
                return (elevPvc + g1 * x + a * x * x, g1 + 2 * a * x);
            }
            else
            {
                double x = pvt - station;
                double elevPvt = v.Elevation + g2 * l2;
                double a = e / (l2 * l2);
                return (elevPvt - g2 * x + a * x * x, g2 - 2 * a * x);
            }
        }

        int seg = Math.Clamp(idx, 0, n - 2);
        double g = GradeBetween(seg);
        return (Vpis[seg].Elevation + g * (station - Vpis[seg].Station), g);
    }

    public VerticalProfile Clone() => new()
    {
        Vpis = Vpis.Select(v => new Vpi { Station = v.Station, Elevation = v.Elevation, LengthIn = v.LengthIn, LengthOut = v.LengthOut }).ToList(),
    };
}
