using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Arch;

public static partial class ArchGenerator
{
    /// <summary>One fillet weld bead in LOCAL coordinates: root line p0 -> p1, legs along the two unit directions (one on each joined part's face).</summary>
    private readonly record struct Bead(Vec3 P0, Vec3 P1, Vec3 Leg1, Vec3 Leg2);

    private sealed partial class Gen
    {
        /// <summary>Weld part (the program's generic weld: <see cref="PartCategory.Weld"/>, viewport layer 3D-WELDS, IFC IfcFastener WELD, database weld
        /// group) made of fillet beads with the given leg. Length = the sum of the bead lengths.</summary>
        private void AddWeldPart(string mark, string asm, string desc, IReadOnlyList<Bead> beads, double size, string material, Assumption? asu = null)
        {
            if (beads.Count == 0 || size <= 0) return;
            var m = new Mesh();
            double len = 0;
            foreach (var b in beads)
            {
                if ((b.P1 - b.P0).Length < 1e-6) continue;
                MeshBuilder.AddFilletWeld(m, W(b.P0), W(b.P1), Wd(b.Leg1).Normalized(), Wd(b.Leg2).Normalized(), size);
                len += (b.P1 - b.P0).Length;
            }
            AddPart(mark, PartCategory.Weld, asm, desc, m, material, len, 0, asu);
        }

        /// <summary>Rib fin plate welds (p.148): fillet both sides of the plate along each rib flange inner face (Section A-A "weld length D (typ. in plane of
        /// plate)", elevation symbol both sides), D from the table per cable, from <see cref="ArchFinSettings.RibWeldSetback"/> below the end of the top clip
        /// toward the pin. Fin coordinates: u across (binormal, from the pin), v toward the rib web, t normal to the plate.</summary>
        private void RibFinWelds(ArchHangerDefinition h, ArchRibFin rf, ArchFinSettings fs, Vec3 hc, Vec3 bn, Vec3 e, Vec3 nf,
            double xl, double xr, double yTop, double clip, WideFlangeSize w)
        {
            Vec3 P(double u, double v, double t) => hc + bn * u + e * v + nf * t;
            double v0 = yTop - clip - fs.RibWeldSetback, v1 = v0 - rf.WeldLength;
            double ySb = yTop - rf.P3;                       // end of the straight side (tangent to the pin radius)
            double over = ySb - v1;                         // > 0: the weld runs past the straight side
            Model.Checks.Add(new ArchCheck($"fin plate {h.Name} at rib: weld length D on the straight side along the flange (p.148)",
                $"D {L(rf.WeldLength)} within P3 {L(rf.P3)}", $"weld {L(v0 - yTop)} .. {L(v1 - yTop)} from the plate top, side straight to {L(-rf.P3)}",
                Math.Max(0, over), over <= Tol, "the weld is modeled at its plan length D (never shortened)"));
            double ht = fs.RibThickness / 2;
            var beads = new List<Bead>();
            foreach (var (u, inward) in new[] { (xl, 1.0), (xr, -1.0) })
                foreach (double t in new[] { -1.0, 1.0 })
                    beads.Add(new Bead(P(u, v0, t * ht), P(u, v1, t * ht), nf * t, bn * inward));
            var asu = fs.RibWeldUnresolved.Length > 0 ? Assumption.UnresolvedDimension(fs.RibWeldUnresolved, $"weld from {L(clip + fs.RibWeldSetback)} below the plate top") : null;
            AddWeldPart($"{RibAsm(h.Rib)}-FIN-{h.Name}-W", RibAsm(h.Rib), $"{L(fs.RibWeldSize)} fillet, both sides, fin plate {h.Name} to the rib flanges, 4 x D {L(rf.WeldLength)} (p.148)",
                beads, fs.RibWeldSize, fs.WeldMaterial, asu);
        }

        /// <summary>Weld rules (<see cref="ArchDefinition.Welds"/>): T-joint fillets found on the built parts (world coordinates). One weld part per attached
        /// part and rule; a rule that finds no contact is reported (the plan symbol has no modeled joint). Welds run only along the stretches where the
        /// attached plate edge really lies on the other part; the length of edges that overhang it is reported (CHECK).</summary>
        private void BuildWeldRules()
        {
            var parts = Model.Steel.Parts.Where(p => p.Category != PartCategory.Weld && !p.Mesh.IsEmpty).ToList();
            var bounds = parts.ToDictionary(p => p, p => p.Mesh.Bounds());
            foreach (var r in _d.Welds)
            {
                if (r.Size <= 0 || r.Parts.Length == 0 || r.Onto.Length == 0) continue;
                var rxB = new System.Text.RegularExpressions.Regex(r.Parts); var rxA = new System.Text.RegularExpressions.Regex(r.Onto);
                var bs = parts.Where(p => rxB.IsMatch(p.Mark)).ToList();
                var asAll = r.Onto.Contains('$') ? parts : parts.Where(p => rxA.IsMatch(p.Mark)).ToList();
                var asu = r.Unresolved.Length > 0 ? Assumption.UnresolvedDimension(r.Unresolved, $"{L(r.Size)} fillet along the full contact") : null;
                Func<Vec3, Vec3, bool>? keep = SideRule(r);
                int built = 0, missing = 0; double total = 0, overhang = 0; var noContact = new List<string>();
                foreach (var b in bs)
                {
                    // "$1" ... in Onto = the capture groups of Parts on this attached part's mark (e.g. the gusset's own connection plate).
                    var As = r.Onto.Contains('$') ? asAll.Where(a => System.Text.RegularExpressions.Regex.IsMatch(a.Mark, rxB.Match(b.Mark).Result(r.Onto))).ToList() : asAll;
                    var bb = bounds[b];
                    var m = new Mesh(); double len = 0;
                    foreach (var a in As)
                    {
                        if (ReferenceEquals(a, b) || !Touch(bb, bounds[a], r.Tolerance + In(0.25))) continue;
                        var beads = WeldFinder.TJoint(a.Mesh, b.Mesh, r.Tolerance, out double oh, r.AngleTolDeg, !r.OneSide, keep);
                        len += WeldFinder.AddBeads(m, beads, r.Size);
                        overhang += oh;
                    }
                    if (m.IsEmpty) { missing++; noContact.Add(b.Mark); continue; }
                    built++; total += len;
                    Model.Steel.Parts.Add(new SteelPart
                    {
                        Mark = $"{b.Mark}-W{(r.Name.Length > 0 ? "-" + r.Name : "")}", Category = PartCategory.Weld, Assembly = b.Assembly, Mesh = m, Length = len,
                        Material = r.Material, Assumption = asu,
                        Description = $"{L(r.Size)} fillet{(r.OneSide ? ", one side" : ", both sides")}, {b.Description.Split(',')[0]} to {r.Name} ({r.Source})",
                    });
                }
                if (asu != null && built > 0) Warn(asu.Reason);
                Info($"Arch welds {r.Name}: {built} parts welded ({L(total)} of {L(r.Size)} fillet roots){(missing > 0 ? $", {missing} matching parts without a contact ({string.Join(", ", noContact.Take(6))}{(noContact.Count > 6 ? ", ..." : "")})" : "")}{(overhang > Tol ? $", {L(overhang)} of plate edge overhanging the joint (not welded)" : "")} [{r.Source}].");
                Model.Checks.Add(new ArchCheck($"welds {r.Name} ({r.Source}): attached parts with a joint on the other part", $"{bs.Count}", $"{built}", missing, missing == 0,
                    missing == 0 ? "" : $"parts matching the rule whose edge face does not sit on a matching part (gap > tolerance or not touching) - no weld built there: {string.Join(", ", noContact.Take(6))}{(noContact.Count > 6 ? ", ..." : "")}"));
                Model.Checks.Add(new ArchCheck($"welds {r.Name} ({r.Source}): plate edge overhanging the joint (not welded)", "0", L(overhang), overhang, overhang <= Tol,
                    overhang <= Tol ? "" : "length of the attached plate's edges that lie on the other part over part of their length only (the remainder hangs past it: plan contact vs the modeled extents)"));
            }
        }

        /// <summary>Single-sided weld symbol: which side of the attached plate is welded (<see cref="ArchWeldRule.SideToward"/>): "span" = the side facing
        /// the structure centre line (local Y = 0), "+x" / "-x" = the side facing along the local X axis. Null = both sides (or no hint).</summary>
        private Func<Vec3, Vec3, bool>? SideRule(ArchWeldRule r)
        {
            if (!r.OneSide || r.SideToward.Length == 0) return null;
            var fr = _fr;
            if (r.SideToward == "+x") return (_, n) => Vec3.Dot(n, fr.X) > 0;
            if (r.SideToward == "-x") return (_, n) => Vec3.Dot(n, fr.X) < 0;
            if (r.SideToward == "span")
                return (p, n) =>
                {
                    double y = Vec3.Dot(p - fr.Origin, fr.Y);                // local Y of the weld; the span centre line is Y = 0
                    return Vec3.Dot(n, fr.Y) * (y > 0 ? -1 : 1) > 0;
                };
            return null;
        }

        private static bool Touch(Box3 a, Box3 b, double tol) =>
            a.Max.X + tol >= b.Min.X && b.Max.X + tol >= a.Min.X && a.Max.Y + tol >= b.Min.Y && b.Max.Y + tol >= a.Min.Y && a.Max.Z + tol >= b.Min.Z && b.Max.Z + tol >= a.Min.Z;

        /// <summary>Tie fin plate welds (p.146 Detail 1 / Section C-C / p.149): none. The plate is a 7/8" bent plate bolted to the tie web (4 bolts, 4" x 4" gages) at the
        /// floorbeam CL and none of the three sheets carries a weld symbol on it (the only symbols on p.146 are the stiffener detail, p.149 has none). Kept as the
        /// explicit "no weld on the plans" hook so nobody adds one without a plan symbol.</summary>
        private void TieFinWelds(ArchHangerDefinition h, ArchFinSettings fs, TieGeo tg, string asm, Vec2 p2, Vec2 t2, Vec2 outward, double off, double zb, double hw,
            Vec3 t3, Vec3 v3, Vec3 nUp)
        {
        }
    }
}
