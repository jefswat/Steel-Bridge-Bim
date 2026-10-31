using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Truss;

public static partial class TrussGenerator
{
    private sealed partial class Gen
    {
        /// <summary><see cref="TrussShoeDefinition.Fasteners"/>: rivets through the shoe angles and the gusset stack (grip probed along the shank over the parts
        /// built so far) and tap bolts screwed into the shoe (bolt solid with its frame at the bottom of the tapped hole: hex + washer on the plate).</summary>
        private void BuildShoeFasteners(TrussShoeDefinition s, string asm, Vec3 pin, Vec3 u, Vec3 n, string pt)
        {
            foreach (var f in s.Fasteners!)
            {
                string what = $"shoe {s.Name} fasteners '{(f.Name.Length > 0 ? f.Name : f.Kind.ToString())}'";
                if (f.Diameter <= 0 || f.Us.Count == 0 || f.Ns.Count == 0 || f.Zs.Count == 0) { Warn($"Truss: {what} - diameter / positions missing; not built."); continue; }
                if (f.Kind == TrussShoeFastenerKind.TapBolt && f.Axis != TrussShoePieceAxis.Vertical) { Warn($"Truss: {what} - tap bolts are built vertical only (axis {f.Axis}); not built."); continue; }
                Assumption? asu = f.Unresolved.Length > 0 ? RivetFlag(f.Unresolved, $"{f.Kind} positions as scaled / derived") : null;
                var (ax, axName) = f.Axis switch { TrussShoePieceAxis.Along => (u, "u"), TrussShoePieceAxis.Vertical => (Up, "z"), _ => (n, "n") };
                int placed = 0, missed = 0;
                // parts near the shoe for the rivet probe
                SteelLineProbe? probe = null;
                if (f.Kind == TrussShoeFastenerKind.Rivet)
                {
                    double r = 0, h = 0;
                    foreach (var x in f.Us) r = Math.Max(r, Math.Abs(x));
                    foreach (var x in f.Ns) r = Math.Max(r, Math.Abs(x));
                    foreach (var x in f.Zs) h = Math.Max(h, Math.Abs(x));
                    double reach = Math.Max(r, h) + In(24);
                    var lo = pin - new Vec3(reach, reach, reach); var hi = pin + new Vec3(reach, reach, reach);
                    probe = new SteelLineProbe();
                    int k = 0;
                    foreach (var p in Model.Steel.Parts)
                    {
                        k++;
                        if (p.Mesh is not { IsEmpty: false } m) continue;
                        var b = m.Bounds();
                        if (b.Max.X < lo.X || b.Min.X > hi.X || b.Max.Y < lo.Y || b.Min.Y > hi.Y || b.Max.Z < lo.Z || b.Min.Z > hi.Z) continue;
                        probe.Add(m, k, lo, hi);
                    }
                }
                foreach (int mu in f.MirrorU ? new[] { 1, -1 } : new[] { 1 })
                    foreach (int mn in f.MirrorN ? new[] { 1, -1 } : new[] { 1 })
                        foreach (var fu in f.Us)
                            foreach (var fn in f.Ns)
                                foreach (var fz in f.Zs)
                                {
                                    var at = pin + u * (mu * fu) + n * (mn * fn) + Up * fz;
                                    if (f.Kind == TrussShoeFastenerKind.TapBolt)
                                    {
                                        // tap bolt: head-bearing face at 'at', shank down the axis (-z for Vertical) through the plate and into the tapped hole
                                        var down = -Up;
                                        double len = f.Grip + f.TapDepth;
                                        var bottom = at + down * len;
                                        var zz = -down;
                                        var xx = Vec3.Cross(zz, Math.Abs(zz.Z) < 0.9 ? Up : u).Normalized();
                                        Model.Steel.Bolts.Add(new BoltInstance
                                        {
                                            Frame = new Frame3(bottom, xx, Vec3.Cross(zz, xx).Normalized(), zz), Grip = len, Length = len, Assembly = asm, Kind = BoltKind.Structural,
                                            Diameter = f.Diameter, Material = f.Material ?? "Tap bolt", Assumption = asu,
                                        });
                                        placed++;
                                        continue;
                                    }
                                    // rivet: contiguous steel along the shank through the point
                                    var iv = probe!.At(at, ax, In(48));
                                    if (iv is not { } g || g.t1 - g.t0 < In(1.0 / 8)) { missed++; continue; }
                                    // head at the end away from the truss plane (Across), up (Vertical), +u (Along)
                                    double side = f.Axis == TrussShoePieceAxis.Across ? Math.Sign(Vec3.Dot(at - pin, n)) : 1;
                                    if (side == 0) side = 1;
                                    var head = side > 0 ? at + ax * g.t1 : at + ax * g.t0;
                                    var shank = ax * -side;
                                    if (MergeRivet(head, shank, u, g.t1 - g.t0, f.Diameter, asm, f.Material, asu)) { }
                                    placed++;
                                }
                if (missed > 0) Warn($"Truss CHECK {what} ({pt}): {missed} of {placed + missed} rivet positions are not in steel along {axName}; not placed{(f.Source.Length > 0 ? " (" + f.Source + ")" : "")}.");
                Info($"Truss: {what} ({pt}): {placed} {(f.Kind == TrussShoeFastenerKind.Rivet ? "rivets" : "tap bolts")} {TrussSections.FormatIn(f.Diameter)}{(f.Source.Length > 0 ? " (" + f.Source + ")" : "")}.");
            }
        }
    }
}
