using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Segmental;

// OPTIONAL match-cast joint keys (SegmentalDefinition.Keys): web shear keys and top / bottom slab alignment keys as recorded closed solids
// (SegmentalBlockResult with Kind = ShearKey) on the match-cast face of the segment farther from its cantilever bent, protruding toward the bent
// into the neighbour's female recess. The segment solids are not cut (male = female volume, net zero concrete).
public sealed partial class SegmentalGenerator
{
    private void BuildKeys()
    {
        var defs = _d.Keys!;
        foreach (var k in defs) if (k.Unresolved.Length > 0) Warn(k.Unresolved);
        var bad = defs.Where(k => double.IsNaN(k.Depth) || double.IsNaN(k.BaseWidth) || double.IsNaN(k.FlatWidth) || double.IsNaN(k.Length)).ToList();
        foreach (var k in bad) Warn(UnresolvedFlag.Text(k.Source.Length > 0 ? k.Source : Pages, $"joint keys '{k.Name}'", "the key profile (depth, base / flat width, length) is not complete", "Not built."));
        var use = defs.Except(bad).ToList();
        if (use.Count == 0) return;
        // joints: (male segment, its definition, joint station, section number, sign toward the bent = direction the key protrudes)
        var joints = new List<(SegmentResult r, SegmentDefinition d, double sta, int sec, int sgn)>();
        foreach (var (r, d, _) in _segs)
        {
            if (!_bentSta.TryGetValue(d.Bent, out double bs)) continue;
            if (d.Type == SegmentType.Typical)
            {
                bool down = r.Station1 <= bs + 1e-6;
                double j = down ? r.Station1 : r.Station0;
                int sec = SectionNumberAt(Math.Abs(j - bs));
                if (sec >= 0) joints.Add((r, d, j, sec, down ? 1 : -1));
            }
            else if (d.Type == SegmentType.BentHalf && Math.Abs(r.Station0 - bs) < 1e-4)
                joints.Add((r, d, bs, 0, -1));                                        // bent control line: the up-station half carries the male keys
        }
        int built = 0;
        foreach (var kd in use)
        {
            foreach (var (r, d, sta, sec, sgn) in joints)
            {
                if (sec < kd.FromSection || sec > kd.ToSection) continue;
                var (h, t, hollow) = SectionIn(d, r, sta - sgn * 1e-4);
                var parts = new List<(int side, List<(Vec2 c, Vec2 U, Vec2 V)> keys)>();
                if (kd.Kind == SegmentalKeyKind.WebShear)
                {
                    var row = kd.WebRows?.FirstOrDefault(x => x.Section == sec);
                    if (row == null || row.Count <= 0 || double.IsNaN(row.BottomDistance) || double.IsNaN(kd.Spacing)) continue;
                    var b = _d.Box;
                    double hs = b.HalfSoffit(h);
                    var c0 = new Vec2(hs, h);
                    var uw = new Vec2(b.WebRootX - hs, b.WebTopDepth - h).Normalized();          // up the outside web face (y is down)
                    var n = new Vec2(uw.Y, -uw.X);                                                 // square to the web, into the box
                    if (n.X > 0) n = new Vec2(-n.X, -n.Y);
                    foreach (int side in new[] { -1, 1 })
                    {
                        var list = new List<(Vec2, Vec2, Vec2)>();
                        for (int i = 0; i < row.Count; i++)
                        {
                            var c = c0 + uw * (row.BottomDistance + i * kd.Spacing) + n * (b.WebThickness / 2);
                            list.Add((new Vec2(side * c.X, c.Y), new Vec2(side * uw.X, uw.Y), new Vec2(side * n.X, n.Y)));
                        }
                        parts.Add((side, list));
                    }
                }
                else
                {
                    if (kd.Offsets is not { Count: > 0 } offs || double.IsNaN(kd.CenterDepth)) continue;
                    bool bottom = kd.Kind == SegmentalKeyKind.BottomSlabAlignment;
                    if (bottom && !hollow) continue;
                    double y = bottom ? h - t + kd.CenterDepth : kd.CenterDepth;
                    parts.Add((0, offs.Select(o => (new Vec2(o, y), new Vec2(0, 1), new Vec2(1, 0))).ToList()));
                }
                foreach (var (side, keys) in parts)
                {
                    if (keys.Count == 0) continue;
                    var mesh = new Mesh();
                    foreach (var (c, U, V) in keys) mesh.Append(KeySolid(r, sta, sgn, c, U, V, kd));
                    string sideName = side < 0 ? " left web" : side > 0 ? " right web" : "";
                    var res = new SegmentalBlockResult
                    {
                        Name = $"{r.Name} {kd.Name}{sideName}", Type = kd.Name, Kind = SegmentalBlockKind.ShearKey, Segment = r.Name, Side = side,
                        Label = $"{keys.Count} male keys, joint section {(sec == 0 ? "bent CL" : sec.ToString())}", Mesh = mesh,
                        Description = $"{keys.Count} male {kd.Name} on the {(sgn > 0 ? "up" : "down")}-station (match-cast) face of {r.Name}{sideName}" + (kd.Source.Length > 0 ? $" ({kd.Source})" : ""),
                    };
                    if (kd.Unresolved.Length > 0) res.Assumption = Assumption.UnresolvedDimension(kd.Unresolved, "keys placed as listed in the key definition");
                    _m.Blocks.Add(res);
                    built += keys.Count;
                }
            }
        }
        if (built > 0)
            Info($"Segmental joint keys: {built} male keys on {joints.Count} match-cast joints ({string.Join(", ", _m.Blocks.Where(b => b.Kind == SegmentalBlockKind.ShearKey).GroupBy(b => b.Type).Select(g => $"{g.Key} {g.Count()} sets"))}); recorded solids, segments not cut ({_m.KeyVolume / Math.Pow(Ft(1), 3) / 27:0.00} CY male = female).");
    }

    /// <summary>Section number of the joint at distance x from the cantilever bent CL: 0 at the CL, the depth-table section at that x, the last section beyond
    /// the table; -1 = no table section there.</summary>
    private int SectionNumberAt(double x)
    {
        if (x < 1e-3) return 0;
        if (_d.DepthTable.Count == 0) return -1;
        var hit = _d.DepthTable.FirstOrDefault(s => Math.Abs(s.X - x) < 0.05);
        if (hit != null) return hit.Number;
        var last = _d.DepthTable.OrderBy(s => s.X).Last();
        return x > last.X ? last.Number : -1;
    }

    /// <summary>One trapezoid key: base on the joint plane, protruding <c>Depth</c> toward the neighbour (station sign <paramref name="sgn"/>).</summary>
    private Mesh KeySolid(SegmentResult r, double sta, int sgn, Vec2 c, Vec2 U, Vec2 V, SegmentalKeyDefinition kd)
    {
        var poly = new List<Vec2> { new(-kd.BaseWidth / 2, 0), new(kd.BaseWidth / 2, 0), new(kd.FlatWidth / 2, kd.Depth), new(-kd.FlatWidth / 2, kd.Depth) };
        Vec3 P(Vec2 q, double v) => ChordFrame(r, sta + sgn * q.Y).At(c + U * q.X + V * v);
        var rings = new List<Vec3[]> { poly.Select(q => P(q, -kd.Length / 2)).ToArray(), poly.Select(q => P(q, kd.Length / 2)).ToArray() };
        return Loft(rings, new[] { poly, poly });
    }
}
