using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;

namespace SBM.Core.Concrete;

public sealed partial class ConcreteGenerator
{
    /// <summary>Tub girders: a haunch over each of the two top flanges of every tub, from the top of the (rotated) flange to the deck soffit.</summary>
    private void BuildTubHaunches()
    {
        var mesh = new Mesh();
        var t = _def.Girders.Tub!;
        double T = t.TopWebSpacing / 2;
        foreach (var g in _f.Girders)
        {
            var path = g.Path;
            var ss = path.S.ToList();
            if (ss.Count < 2) continue;
            foreach (int side in new[] { -1, 1 })
            {
                var rings = new List<Vec3[]>(ss.Count);
                Vec2[]? sec = null;
                foreach (double s in HaunchSamples(ss))
                {
                    var (p, tan, _) = path.At(s);
                    var f = TubFrame.Create(p, tan, t.RotateWithDeck ? TubSection.DeckSlope(_f, p.XY) : 0);
                    var (w, tf) = g.PlateAt(GirderPlate.TopFlange, s) ?? (0, 0);
                    var a = f.At(side * T - w / 2, tf);
                    var b = f.At(side * T + w / 2, tf);
                    double za = DeckBottomAt(a.XY), zb = DeckBottomAt(b.XY);
                    sec ??= new[] { new Vec2(-w / 2, 0), new Vec2(w / 2, 0), new Vec2(w / 2, 1), new Vec2(-w / 2, 1) };
                    rings.Add(new[] { a, b, b.XY.WithZ(Math.Max(zb, b.Z + 1e-4)), a.XY.WithZ(Math.Max(za, a.Z + 1e-4)) });
                }
                rings = ClipHaunchRings(rings);
                if (rings.Count >= 2) MeshBuilder.AddLoft(mesh, sec!, rings);
            }
        }
        _m.Haunches = mesh;
    }

    /// <summary>Haunch rings (a, b, b top, a top) cut at the deck ends: each bottom edge corner is cut where it crosses the (skewed / kinked) deck end line
    /// (<see cref="DeckResult.FromBegin"/> / <see cref="DeckResult.FromEnd"/>), so a haunch never projects past the deck. Rings already inside are untouched.</summary>
    private List<Vec3[]> ClipHaunchRings(List<Vec3[]> rings)
    {
        int n = rings.Count;
        const double tol = 1e-6;
        double In(Vec2 p) => Math.Min(DR.FromBegin(p), DR.FromEnd(p));
        var first = new int[2]; var last = new int[2];
        for (int c = 0; c < 2; c++)
        {
            first[c] = -1; last[c] = -1;
            for (int i = 0; i < n; i++)
                if (In(rings[i][c].XY) >= -tol) { if (first[c] < 0) first[c] = i; last[c] = i; }
        }
        if (first[0] < 0 && first[1] < 0) return new List<Vec3[]>();
        if (first[0] == 0 && first[1] == 0 && last[0] == n - 1 && last[1] == n - 1) return rings;
        Vec3 Cross(int c, int i, int j, Func<Vec2, double> d)
        {
            Vec3 p = rings[i][c], q = rings[j][c];
            double lo = 0, hi = 1;   // d(p) >= 0 (inside), d(q) < 0
            for (int k = 0; k < 40; k++) { double m = (lo + hi) / 2; if (d(Vec3.Lerp(p, q, m).XY) >= 0) lo = m; else hi = m; }
            return Vec3.Lerp(p, q, lo);
        }
        var cornerAt = new Vec3[2][];
        for (int c = 0; c < 2; c++)
        {
            if (first[c] < 0) { cornerAt[c] = rings.Select(r => r[c]).ToArray(); continue; }
            cornerAt[c] = new Vec3[n];
            Vec3 cb = default, ce = default;
            if (first[c] > 0) cb = Cross(c, first[c], first[c] - 1, DR.FromBegin);
            if (last[c] < n - 1) ce = Cross(c, last[c], last[c] + 1, DR.FromEnd);
            for (int i = 0; i < n; i++)
                cornerAt[c][i] = i < first[c] ? cb : i > last[c] ? ce : rings[i][c];
        }
        int i0 = Math.Max(0, Math.Min(first[0] < 0 ? n : first[0], first[1] < 0 ? n : first[1]) - 1);
        int i1 = Math.Min(n - 1, Math.Max(last[0], last[1]) + 1);
        var res = new List<Vec3[]>();
        for (int i = i0; i <= i1; i++)
        {
            var a = cornerAt[0][i]; var b = cornerAt[1][i];
            res.Add(new[] { a, b, b.XY.WithZ(Math.Max(DeckBottomAt(b.XY), b.Z + 1e-4)), a.XY.WithZ(Math.Max(DeckBottomAt(a.XY), a.Z + 1e-4)) });
        }
        return res;
    }
}
