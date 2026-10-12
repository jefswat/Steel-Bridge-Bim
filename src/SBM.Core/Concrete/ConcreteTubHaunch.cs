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
                MeshBuilder.AddLoft(mesh, sec!, rings);
            }
        }
        _m.Haunches = mesh;
    }
}
