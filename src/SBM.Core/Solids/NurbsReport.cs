using System.Collections.Concurrent;

namespace SBM.Core.Solids;

/// <summary>What a generator built as exact NURBS (exact-geometry option): counts per kind, fallbacks with reasons, worst fit.</summary>
public sealed class NurbsReport
{
    private readonly ConcurrentDictionary<string, int> _built = new();
    private readonly ConcurrentQueue<string> _fallbacks = new();
    private long _maxDevNm;

    public double Tolerance { get; init; }

    public void Built(string kind, double deviation)
    {
        _built.AddOrUpdate(kind, 1, (_, n) => n + 1);
        long nm = (long)Math.Round(deviation * 1e9);
        long cur;
        while (nm > (cur = Interlocked.Read(ref _maxDevNm)) && Interlocked.CompareExchange(ref _maxDevNm, nm, cur) != cur) { }
    }

    public void Fallback(string kind, string reason) => _fallbacks.Enqueue($"{kind}: {reason}");

    public IReadOnlyDictionary<string, int> BuiltByKind => _built.OrderBy(k => k.Key, StringComparer.Ordinal).ToDictionary(k => k.Key, k => k.Value);
    public IReadOnlyList<string> Fallbacks => _fallbacks.OrderBy(x => x, StringComparer.Ordinal).ToList();
    public int Count => _built.Values.Sum();
    /// <summary>Largest deviation of any NURBS from the exact geometry (m).</summary>
    public double MaxDeviation => Interlocked.Read(ref _maxDevNm) * 1e-9;

    public string Summary() =>
        $"Exact curved geometry: {Count} NURBS solids ({string.Join(", ", BuiltByKind.Select(k => $"{k.Value} {k.Key}"))}), max deviation " +
        $"{MaxDeviation * 1000:0.####} mm (tolerance {Tolerance * 1000:0.####} mm), {Fallbacks.Count} kept as meshes" +
        (Fallbacks.Count > 0 ? ": " + string.Join("; ", Fallbacks.GroupBy(f => f).Select(g => g.Count() > 1 ? $"{g.Key} (x{g.Count()})" : g.Key).Take(8)) : "") + ".";
}
