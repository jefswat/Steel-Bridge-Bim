namespace SBM.Core.Concrete;

/// <summary>Mark of a placed bar: marks are unique per (Unit, Mark) - the same plan mark can exist in several units (e.g. segmental "W1" per segment
/// group, #6 or #7). Looks up by (Unit, Mark) first, then by Mark alone (models whose bars do not carry the mark's unit).</summary>
public sealed class BarMarkLookup
{
    private readonly Dictionary<(string, string), BarMark> _byUnit = new();
    private readonly Dictionary<string, BarMark> _byMark = new();

    public BarMarkLookup(IEnumerable<BarMark> marks)
    {
        foreach (var m in marks)
        {
            _byUnit.TryAdd((m.Unit, m.Mark), m);
            _byMark.TryAdd(m.Mark, m);
        }
    }

    public BarMark? Find(BarPlacement b) => _byUnit.TryGetValue((b.Unit, b.Mark), out var m) ? m : _byMark.GetValueOrDefault(b.Mark);

    public BarMark this[BarPlacement b] => Find(b) ?? throw new KeyNotFoundException($"No bar mark '{b.Mark}' (unit '{b.Unit}').");
}
