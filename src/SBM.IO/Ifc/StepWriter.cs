using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SBM.IO.Ifc;

/// <summary>Minimal ISO 10303-21 (STEP physical file) writer: sequential entity ids, IFC-safe value formatting.</summary>
public sealed class StepWriter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private readonly TextWriter _w;
    private int _next = 1;

    public StepWriter(TextWriter w) => _w = w;

    private int _skipped;

    /// <summary>Optional entity filter (checking export). Null for normal exports: output is unchanged.</summary>
    public IfcCheckFilter? Filter { get; set; }

    public int Count => _next - 1 - _skipped;

    /// <summary>Writes an entity and returns its id. <paramref name="body"/> is the attribute list without parentheses.</summary>
    public int Add(string type, string body)
    {
        int id = _next++;
        if (Filter != null)
        {
            // Checking export: the filter may rewrite the attributes or drop the entity (its id stays unused).
            var nb = Filter.Filter(id, type, body);
            if (nb == null) { _skipped++; return id; }
            body = nb;
        }
        _w.Write('#');
        _w.Write(id.ToString(Inv));
        _w.Write('=');
        _w.Write(type);
        _w.Write('(');
        _w.Write(body);
        _w.Write(");\n");
        return id;
    }

    public void Header(string fileName, string schema, string viewDefinition)
    {
        _w.Write("ISO-10303-21;\nHEADER;\n");
        _w.Write($"FILE_DESCRIPTION(('ViewDefinition [{viewDefinition}]'),'2;1');\n");
        _w.Write($"FILE_NAME({S(fileName)},{S(DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", Inv))},(''),(''),'Steel Bridge Modeler','Steel Bridge Modeler 0.2','');\n");
        _w.Write($"FILE_SCHEMA(('{schema}'));\nENDSEC;\nDATA;\n");
    }

    public void Footer() => _w.Write("ENDSEC;\nEND-ISO-10303-21;\n");

    // ---------------- value formatting

    public static string R(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) v = 0;
        string s = v.ToString("0.0#########", Inv);
        return s == "-0.0" ? "0.0" : s;
    }

    /// <summary>Coordinate value: 7 decimals (sub-micron in feet or meters) keeps files compact.</summary>
    public static string C(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v)) v = 0;
        string s = v.ToString("0.0######", Inv);
        return s == "-0.0" ? "0.0" : s;
    }

    public static string Ref(int id) => "#" + id.ToString(Inv);
    public static string I(int v) => v.ToString(Inv);
    public static string Refs(IEnumerable<int> ids) => "(" + string.Join(",", ids.Select(Ref)) + ")";
    public static string Enum(string v) => "." + v + ".";
    public static string B(bool b) => b ? ".T." : ".F.";

    /// <summary>STEP string with ISO 10303-21 escapes (quotes doubled, non-ASCII as \X2\...\X0\).</summary>
    public static string S(string? s)
    {
        if (s == null) return "$";
        var sb = new StringBuilder("'");
        foreach (char c in s)
        {
            if (c == '\'') sb.Append("''");
            else if (c == '\\') sb.Append("\\\\");
            else if (c >= 32 && c < 127) sb.Append(c);
            else sb.Append("\\X2\\").Append(((int)c).ToString("X4", Inv)).Append("\\X0\\");
        }
        return sb.Append('\'').ToString();
    }

    private const string GuidChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_$";

    /// <summary>Deterministic IFC GlobalId (22-char base64) from a key, so re-exports keep the same ids.</summary>
    public static string Guid(string key)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(key));
        UInt128 v = 0;
        foreach (byte b in hash) v = (v << 8) | b;
        var c = new char[22];
        for (int i = 21; i >= 0; i--)
        {
            c[i] = GuidChars[(int)(v & 63)];
            v >>= 6;
        }
        return "'" + new string(c) + "'";
    }
}
