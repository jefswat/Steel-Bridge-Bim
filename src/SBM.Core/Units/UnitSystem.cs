using System.Globalization;
using System.Text.RegularExpressions;

namespace SBM.Core.Units;

public enum UnitSystemKind { US, SI }

public enum FootDefinition { International, USSurvey }

/// <summary>What a length value represents; controls default display and input units.</summary>
public enum LengthKind
{
    /// <summary>Long dimensions: ft-in (US) or m (SI).</summary>
    Long,
    /// <summary>Plate sizes, small offsets: inches (US) or mm (SI).</summary>
    Short,
    /// <summary>Elevations and coordinates: decimal ft (US) or m (SI).</summary>
    Coordinate,
}

/// <summary>
/// Converts between internal units (meters, radians, slopes as rise/run) and display strings.
/// All model data is stored in meters; this class is the only place display units live.
/// </summary>
public sealed partial class UnitSystem
{
    public UnitSystemKind Kind { get; set; } = UnitSystemKind.US;
    public FootDefinition Foot { get; set; } = FootDefinition.International;

    /// <summary>Denominator for fractional inch display (16 → 1/16").</summary>
    public int InchFraction { get; set; } = 16;

    public static UnitSystem Default { get; } = new();

    public bool IsUS => Kind == UnitSystemKind.US;

    public double FootInMeters => Foot == FootDefinition.USSurvey ? 1200.0 / 3937.0 : 0.3048;
    public double InchInMeters => FootInMeters / 12.0;

    /// <summary>Meters per display "long" unit (ft or m).</summary>
    public double LongUnit => IsUS ? FootInMeters : 1.0;

    public string LongUnitName => IsUS ? "ft" : "m";
    public string ShortUnitName => IsUS ? "in" : "mm";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ---------------- Formatting ----------------

    public double ToDisplay(double meters, LengthKind kind) => kind switch
    {
        LengthKind.Short => IsUS ? meters / InchInMeters : meters * 1000.0,
        _ => meters / LongUnit,
    };

    public double FromDisplay(double value, LengthKind kind) => kind switch
    {
        LengthKind.Short => IsUS ? value * InchInMeters : value / 1000.0,
        _ => value * LongUnit,
    };

    public string FormatStation(double stationMeters, int decimals = 2)
    {
        double v = stationMeters / LongUnit;
        double block = IsUS ? 100 : 1000;
        int digits = IsUS ? 2 : 3;
        bool neg = v < 0;
        v = Math.Round(Math.Abs(v), decimals);
        double major = Math.Floor(v / block);
        double minor = v - major * block;
        if (minor >= block - Math.Pow(10, -decimals) / 2) { major++; minor = 0; }
        string minorStr = minor.ToString("F" + decimals, Inv).PadLeft(digits + (decimals > 0 ? decimals + 1 : 0), '0');
        return (neg ? "-" : "") + major.ToString("F0", Inv) + "+" + minorStr;
    }

    public string FormatLength(double meters, LengthKind kind = LengthKind.Long)
    {
        if (double.IsNaN(meters)) return "";
        if (!IsUS)
        {
            return kind switch
            {
                LengthKind.Short => (meters * 1000).ToString("0.#", Inv) + " mm",
                LengthKind.Coordinate => meters.ToString("F3", Inv),
                _ => meters.ToString("F3", Inv) + " m",
            };
        }
        return kind switch
        {
            LengthKind.Short => FormatInches(meters / InchInMeters),
            LengthKind.Coordinate => (meters / FootInMeters).ToString("F3", Inv),
            _ => FormatFeetInches(meters / FootInMeters),
        };
    }

    /// <summary>Formats decimal feet as 12'-3 1/2".</summary>
    public string FormatFeetInches(double feet)
    {
        bool neg = feet < 0;
        double totalIn = Math.Abs(feet) * 12.0;
        double rounded = Math.Round(totalIn * InchFraction) / InchFraction;
        int ft = (int)Math.Floor(rounded / 12.0 + 1e-9);
        double inches = rounded - ft * 12.0;
        if (inches < 0) inches = 0;
        return (neg ? "-" : "") + ft.ToString(Inv) + "'-" + FormatInchValue(inches) + "\"";
    }

    public string FormatInches(double inches)
    {
        bool neg = inches < 0;
        return (neg ? "-" : "") + FormatInchValue(Math.Abs(inches)) + "\"";
    }

    private string FormatInchValue(double inches)
    {
        double rounded = Math.Round(inches * InchFraction) / InchFraction;
        int whole = (int)Math.Floor(rounded + 1e-9);
        int num = (int)Math.Round((rounded - whole) * InchFraction);
        if (num == 0) return whole.ToString(Inv);
        int den = InchFraction;
        int g = Gcd(num, den);
        num /= g; den /= g;
        return whole == 0 ? $"{num}/{den}" : $"{whole} {num}/{den}";
    }

    private static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);

    public static string FormatSlope(double slope) => (slope * 100).ToString("0.000", Inv) + "%";

    public static string FormatAngleDms(double radians, bool signed = true)
    {
        double deg = radians * 180.0 / Math.PI;
        bool neg = deg < 0;
        deg = Math.Abs(deg);
        double totalSec = Math.Round(deg * 3600.0, 2);
        int d = (int)(totalSec / 3600);
        int m = (int)((totalSec - d * 3600) / 60);
        double s = totalSec - d * 3600 - m * 60;
        return (neg && signed ? "-" : "") + $"{d}°{m:00}'{s.ToString("00.00", Inv)}\"";
    }

    /// <summary>Quadrant bearing from a math angle (CCW from East), e.g. N 45°30'15.00" E.</summary>
    public static string FormatBearing(double mathAngle)
    {
        double az = Math.PI / 2 - mathAngle; // azimuth clockwise from north
        az %= 2 * Math.PI;
        if (az < 0) az += 2 * Math.PI;
        double deg = az * 180 / Math.PI;
        string ns, ew;
        double q;
        if (deg <= 90) { ns = "N"; ew = "E"; q = deg; }
        else if (deg <= 180) { ns = "S"; ew = "E"; q = 180 - deg; }
        else if (deg <= 270) { ns = "S"; ew = "W"; q = deg - 180; }
        else { ns = "N"; ew = "W"; q = 360 - deg; }
        return $"{ns} {FormatAngleDms(q * Math.PI / 180, false)} {ew}";
    }

    // ---------------- Parsing ----------------

    [GeneratedRegex(@"^\s*(?<sign>-)?\s*(?:(?<ft>\d+(?:\.\d*)?)\s*'\s*-?\s*)?(?:(?<in>\d+(?:\.\d*)?)?\s*(?:(?<num>\d+)\s*/\s*(?<den>\d+))?\s*""?)?\s*$")]
    private static partial Regex FeetInchRegex();

    [GeneratedRegex(@"^\s*(?<v>[-+]?\d*\.?\d+(?:[eE][-+]?\d+)?)\s*(?<u>mm|cm|m|ft|in|'|"")?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ValueUnitRegex();

    /// <summary>
    /// Parses a length in any common format: 12'-6 1/2", 12.5', 150", 3 1/2, 1.25 m, 300 mm.
    /// Unitless numbers use the default display unit for <paramref name="kind"/>.
    /// </summary>
    public bool TryParseLength(string? text, LengthKind kind, out double meters)
    {
        meters = double.NaN;
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim().Replace("′", "'").Replace("″", "\"").Replace("''", "\"");

        var vu = ValueUnitRegex().Match(text);
        if (vu.Success && double.TryParse(vu.Groups["v"].Value, NumberStyles.Float, Inv, out double v))
        {
            string u = vu.Groups["u"].Value.ToLowerInvariant();
            meters = u switch
            {
                "mm" => v / 1000,
                "cm" => v / 100,
                "m" => v,
                "ft" or "'" => v * FootInMeters,
                "in" or "\"" => v * InchInMeters,
                _ => FromDisplay(v, kind),
            };
            return true;
        }

        if (!IsUS && !text.Contains('\'') && !text.Contains('"') && !text.Contains('/')) return false;

        var m = FeetInchRegex().Match(text);
        if (!m.Success) return false;
        bool hasFt = m.Groups["ft"].Success, hasIn = m.Groups["in"].Success, hasFrac = m.Groups["num"].Success;
        if (!hasFt && !hasIn && !hasFrac) return false;
        double ft = hasFt ? double.Parse(m.Groups["ft"].Value, Inv) : 0;
        double inch = hasIn ? double.Parse(m.Groups["in"].Value, Inv) : 0;
        if (hasFrac)
        {
            double den = double.Parse(m.Groups["den"].Value, Inv);
            if (den == 0) return false;
            inch += double.Parse(m.Groups["num"].Value, Inv) / den;
        }
        double total;
        if (!hasFt && !text.Contains('"') && kind != LengthKind.Short)
        {
            // "3 1/2" with no marks in a long field means feet.
            total = inch * FootInMeters;
        }
        else total = ft * FootInMeters + inch * InchInMeters;
        meters = m.Groups["sign"].Success ? -total : total;
        return true;
    }

    /// <summary>Parses stations like 12+34.56, 1+234.567 or plain numbers (in display units).</summary>
    public bool TryParseStation(string? text, out double meters)
    {
        meters = double.NaN;
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim();
        bool neg = text.StartsWith('-');
        if (neg) text = text[1..];
        int plus = text.IndexOf('+');
        double v;
        if (plus >= 0)
        {
            if (!double.TryParse(text[..plus], NumberStyles.Float, Inv, out double major)) return false;
            if (!double.TryParse(text[(plus + 1)..], NumberStyles.Float, Inv, out double minor)) return false;
            v = major * (IsUS ? 100 : 1000) + minor;
        }
        else if (!double.TryParse(text, NumberStyles.Float, Inv, out v)) return false;
        meters = (neg ? -v : v) * LongUnit;
        return true;
    }

    /// <summary>Parses slopes: "2%", "-2.5 %", or a rise/run ratio "-0.02".</summary>
    public static bool TryParseSlope(string? text, out double slope)
    {
        slope = double.NaN;
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim();
        bool pct = text.EndsWith('%');
        if (pct) text = text[..^1];
        if (!double.TryParse(text, NumberStyles.Float, Inv, out double v)) return false;
        slope = pct ? v / 100 : v;
        return true;
    }

    [GeneratedRegex(@"^\s*(?<sign>-)?\s*(?<d>\d+(?:\.\d+)?)\s*(?:°|d|-|\s)\s*(?:(?<m>\d+(?:\.\d+)?)\s*(?:'|m|-|\s)?\s*(?:(?<s>\d+(?:\.\d+)?)\s*(?:""|s)?)?)?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex DmsRegex();

    /// <summary>Parses angles in decimal degrees or D°M'S" / D-M-S formats. Returns radians.</summary>
    public static bool TryParseAngle(string? text, out double radians)
    {
        radians = double.NaN;
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim();
        if (double.TryParse(text.TrimEnd('°'), NumberStyles.Float, Inv, out double deg))
        {
            radians = deg * Math.PI / 180;
            return true;
        }
        var m = DmsRegex().Match(text);
        if (!m.Success) return false;
        double d = double.Parse(m.Groups["d"].Value, Inv);
        double mi = m.Groups["m"].Success ? double.Parse(m.Groups["m"].Value, Inv) : 0;
        double s = m.Groups["s"].Success ? double.Parse(m.Groups["s"].Value, Inv) : 0;
        double total = d + mi / 60 + s / 3600;
        radians = (m.Groups["sign"].Success ? -total : total) * Math.PI / 180;
        return true;
    }

    // Convenience constructors for defaults expressed in US units.
    public static double Ft(double feet) => feet * 0.3048;
    public static double In(double inches) => inches * 0.0254;
}
