using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Truss;

public static partial class TrussGenerator
{
    private sealed partial class Gen
    {
        /// <summary><see cref="Bridge.TrussDefinition.JointRivetGripsFromSteel"/>: the joint rivets [<paramref name="from"/>, <paramref name="to"/>) take their
        /// grip from the steel along the shank (<see cref="SteelLineProbe"/> over the parts built so far):
        /// head = outer face of the steel the head face lies in (followed outward up to 1 1/2 in.) or, when the head floats, the first steel under it inside
        /// the section-rule grip; far end = far face of the last steel that STARTS inside the section-rule grip, contiguous steel continuing past the
        /// section-rule end followed up to 1 1/2 in. (beyond that the section-rule end is kept). Air between (missing fills / plies) stays in the grip and is
        /// reported; nothing is moved to close it.</summary>
        private void RegripJointRivets(int from, int to)
        {
            if (to <= from) return;
            var bolts = Model.Steel.Bolts;
            var parts = Model.Steel.Parts;
            var boxes = parts.Select(p => p.Mesh is { IsEmpty: false } m ? m.Bounds() : default).ToList();
            double reachOut = In(1.5), tol = In(1.0 / 32), q16 = In(1.0 / 16);
            // rivets by 1 m cell of their head point: one probe per cell
            var cells = new Dictionary<(long, long, long), List<int>>();
            for (int i = from; i < to; i++)
            {
                var b = bolts[i];
                if (b.Kind != BoltKind.Rivet) continue;
                var o = b.Frame.Origin;
                var k = ((long)Math.Floor(o.X), (long)Math.Floor(o.Y), (long)Math.Floor(o.Z));
                if (!cells.TryGetValue(k, out var l)) cells[k] = l = new();
                l.Add(i);
            }
            int n = 0, changed = 0, headBlocked = 0, headOut = 0, headIn = 0, noSteel = 0, shorter = 0, longer = 0, withAir = 0, keptEnd = 0;
            double headOutMax = 0, headInMax = 0, shortMax = 0, longMax = 0, airMax = 0, airSum = 0;
            foreach (var (_, ids) in cells.OrderBy(c => c.Key))
            {
                double x0 = double.MaxValue, y0 = double.MaxValue, z0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue, z1 = double.MinValue;
                foreach (int i in ids)
                {
                    var b = bolts[i];
                    foreach (var p in new[] { b.Frame.Origin - b.Frame.Z * (reachOut + 0.01), b.Frame.Origin + b.Frame.Z * (b.Grip + reachOut + 0.01) })
                    {
                        x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y); z0 = Math.Min(z0, p.Z);
                        x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y); z1 = Math.Max(z1, p.Z);
                    }
                }
                var lo = new Vec3(x0 - 0.01, y0 - 0.01, z0 - 0.01); var hi = new Vec3(x1 + 0.01, y1 + 0.01, z1 + 0.01);
                var pr = new SteelLineProbe();
                for (int i = 0; i < parts.Count; i++)
                {
                    var bb = boxes[i];
                    if (parts[i].Mesh is not { IsEmpty: false } || bb.Max.X < lo.X || bb.Min.X > hi.X || bb.Max.Y < lo.Y || bb.Min.Y > hi.Y || bb.Max.Z < lo.Z || bb.Min.Z > hi.Z) continue;
                    pr.Add(parts[i].Mesh, i, lo, hi);
                }
                foreach (int i in ids)
                {
                    var b = bolts[i];
                    var z = b.Frame.Z;
                    double g0 = b.Grip;
                    n++;
                    var ivs = pr.Intervals(b.Frame.Origin, z, g0 + reachOut + 0.005);
                    // head: steel at the head face (just inside) -> its outer face (within reachOut); else the first steel starting inside the grip
                    double hs = double.NaN;
                    foreach (var iv in ivs)
                        if (iv.t0 <= In(1.0 / 64) && iv.t1 > In(1.0 / 64))
                        {
                            // steel continuing outward more than reachOut = another part standing on the face over the head (an edge angle leg along the
                            // shank): a clash, not a ply - the head stays on the stack face
                            if (iv.t0 < -reachOut) { hs = 0; headBlocked++; } else hs = iv.t0;
                            break;
                        }
                    if (double.IsNaN(hs))
                        foreach (var iv in ivs)
                            if (iv.t0 > 0 && iv.t0 < g0 - tol) { hs = iv.t0; break; }
                    if (double.IsNaN(hs)) { noSteel++; continue; }
                    // far end: last steel starting before the section-rule end; contiguous steel past it followed up to reachOut
                    double e = double.NaN;
                    foreach (var iv in ivs)
                    {
                        if (iv.t1 <= hs + 1e-6 || iv.t0 >= g0 - tol) continue;
                        e = double.IsNaN(e) ? iv.t1 : Math.Max(e, iv.t1);
                    }
                    if (double.IsNaN(e)) { noSteel++; continue; }
                    if (e > g0 + reachOut) { e = g0; keptEnd++; }
                    // air inside [hs, e]
                    double air = 0, cur = hs;
                    foreach (var iv in ivs)
                    {
                        if (iv.t1 <= cur || iv.t0 >= e) continue;
                        if (iv.t0 > cur) air += iv.t0 - cur;
                        cur = Math.Max(cur, iv.t1);
                    }
                    if (cur < e) air += e - cur;
                    if (air > q16) { withAir++; airMax = Math.Max(airMax, air); airSum += air; }
                    double g = Grip16(e - hs);
                    if (hs < -In(1.0 / 64)) { headOut++; headOutMax = Math.Max(headOutMax, -hs); }
                    else if (hs > In(1.0 / 64)) { headIn++; headInMax = Math.Max(headInMax, hs); }
                    if (g < g0 - q16 / 2) { shorter++; shortMax = Math.Max(shortMax, g0 - g); }
                    else if (g > g0 + q16 / 2) { longer++; longMax = Math.Max(longMax, g - g0); }
                    if (Math.Abs(hs) < 1e-5 && Math.Abs(g - g0) < 1e-5) continue;
                    changed++;
                    bolts[i] = new BoltInstance
                    {
                        Frame = new Frame3(b.Frame.Origin + z * hs, b.Frame.X, b.Frame.Y, z), Grip = g, Length = g, Assembly = b.Assembly, Kind = b.Kind, Diameter = b.Diameter,
                        Embedment = b.Embedment, Assumption = b.Assumption, TopHeight = b.TopHeight, Material = b.Material, FlushEnd = b.FlushEnd,
                    };
                }
            }
            Info($"Truss: joint rivet grips from the steel along the shank - {n} rivets, {changed} changed: head moved out onto the outer steel {headOut} (max {L(headOutMax)}), "
                + $"head moved in onto the first steel {headIn} (max {L(headInMax)}), grip shorter {shorter} (max {L(shortMax)}), longer {longer} (max {L(longMax)})"
                + $"{(keptEnd > 0 ? $", {keptEnd} with steel continuing more than {L(reachOut)} past the section-rule end (that end kept)" : "")}{Src}.");
            if (headBlocked > 0) Warn($"Truss CHECK joint rivets: {headBlocked} rivet heads lie inside other steel that stands on the gusset face for more than {L(reachOut)} along the shank (e.g. a bracing edge angle leg over the rivet field) - head left on the stack face; the rivet and that part clash{Src}.");
            if (noSteel > 0) Warn($"Truss CHECK joint rivets: {noSteel} rivets find no steel along their shank inside the section-rule grip - left as placed{Src}.");
            if (withAir > 0)
                Warn($"Truss CHECK joint rivets: {withAir} of {n} rivets have air inside their grip (over 1/16\"; largest {L(airMax)}, total {airSum / 0.0254:N0} in.) - fills, web plies or splice / inlay plates under the gussets are missing in the model; nothing moved{Src}.");
        }
    }
}
