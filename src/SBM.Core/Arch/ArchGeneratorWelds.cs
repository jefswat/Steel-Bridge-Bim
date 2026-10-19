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
        /// plate)", 1/4" +- 1/8" from each end of the contact, elevation symbol both sides). The beads lie ONLY where the fin edge sits on the flange: the contact
        /// stretch is found on the meshes (<see cref="WeldFinder.TJoint"/> fin onto the rib segments), each bead starts the hold-back (+ <see cref="ArchFinSettings.RibWeldSetback"/>)
        /// below the top of the contact (end of the top clip) and runs D toward the pin, but never past the hold-back above the end of the contact (flange tip
        /// or end of the straight side). D longer than that = CHECK row (plan vs model). Fin coordinates: u across (binormal, from the pin), v toward the rib
        /// web, t normal to the plate.</summary>
        private void RibFinWelds(ArchHangerDefinition h, ArchRibFin rf, ArchFinSettings fs, Vec3 hc, Vec3 bn, Vec3 e, Vec3 nf,
            double xl, double xr, double yTop, double clip, WideFlangeSize w, Mesh fin)
        {
            Vec3 P(double u, double v, double t) => hc + bn * u + e * v + nf * t;
            double ySb = yTop - rf.P3;                       // end of the straight side (tangent to the pin radius)
            double hb = WeldTermination.HoldBack(_b.Welds, fs.RibWeldSize, fs.RibWeldHoldBack);
            string nm = $"fin plate {h.Name} at rib";
            {
                double v0p = yTop - clip - fs.RibWeldSetback - hb, over = ySb + hb - (v0p - rf.WeldLength);
                Model.Checks.Add(new ArchCheck($"{nm}: weld length D on the straight side along the flange (p.148)",
                    $"D {L(rf.WeldLength)} within P3 {L(rf.P3)} less the clip and 2 hold-backs", $"weld from {L(v0p - yTop)} below the plate top, side straight to {L(-rf.P3)}",
                    Math.Max(0, over), over <= Tol, "plan vs plan; the model weld stops at the contact (next row)"));
            }
            // Contact of the fin side edges with the rib (world), per side line (u, t).
            var fb = fin.Bounds();
            var found = new List<WeldFinder.Bead>();
            foreach (var r in Model.Steel.Parts.Where(p => p.Assembly == RibAsm(h.Rib) && p.Category is PartCategory.ArchRib or PartCategory.KnucklePlate && !p.Mesh.IsEmpty))
                if (Touch(fb, r.Mesh.Bounds(), Tol + In(0.25)))
                    found.AddRange(WeldFinder.TJoint(r.Mesh, fin, Tol, out _, 3, true, null, false));
            var eW = Wd(e).Normalized();
            double ht = fs.RibThickness / 2;
            var beads = new List<Bead>();
            double modeled = 0, shortBy = 0;
            var contacts = new List<double>(); var lens = new List<double>();
            int lines = 0;
            foreach (var (u, inward) in new[] { (xl, 1.0), (xr, -1.0) })
                foreach (double t in new[] { -1.0, 1.0 })
                {
                    lines++;
                    var o = W(P(u, 0, t * ht));
                    var iv = new List<(double lo, double hi)>();
                    double V(Vec3 q) => Vec3.Dot(q - o, eW);
                    double Off(Vec3 q) { var d = q - o; return (d - eW * Vec3.Dot(d, eW)).Length; }
                    foreach (var b in found)
                    {
                        if (Off(b.P0) > 0.001 || Off(b.P1) > 0.001) continue;
                        double a = V(b.P0), c = V(b.P1);
                        iv.Add((Math.Min(a, c), Math.Max(a, c)));
                    }
                    if (iv.Count == 0) { shortBy += rf.WeldLength; continue; }
                    // merged stretches; the weld uses the one at the top of the side (below the clip)
                    iv.Sort();
                    var runs = new List<(double lo, double hi)>();
                    foreach (var x in iv)
                        if (runs.Count > 0 && x.lo <= runs[^1].hi + 1e-4) runs[^1] = (runs[^1].lo, Math.Max(runs[^1].hi, x.hi)); else runs.Add(x);
                    var (cLo, cHi) = runs.OrderByDescending(x => x.hi).First();
                    contacts.Add(cHi - cLo);
                    double v0 = cHi - hb - fs.RibWeldSetback, v1 = Math.Max(v0 - rf.WeldLength, cLo + hb);
                    if (v0 - v1 < WeldTermination.MinBead) { shortBy += rf.WeldLength; continue; }
                    beads.Add(new Bead(P(u, v0, t * ht), P(u, v1, t * ht), nf * t, bn * inward));
                    modeled += v0 - v1; lens.Add(v0 - v1);
                    shortBy += rf.WeldLength - (v0 - v1);
                }
            string Rng(List<double> l) => l.Count == 0 ? "none" : l.Max() - l.Min() < Tol ? L(l[0]) : $"{L(l.Min())} - {L(l.Max())}";
            Model.Checks.Add(new ArchCheck($"{nm}: weld length D vs the flange contact less 2 hold-backs (p.148)",
                $"4 x D {L(rf.WeldLength)}", $"{beads.Count} x {Rng(lens)} (flange contact {Rng(contacts)}, hold-back {L(hb)} at both ends; total {L(modeled)} of {L(lines * rf.WeldLength)})",
                Math.Max(0, shortBy), shortBy <= 4 * Tol,
                shortBy <= 4 * Tol ? "" : "D is longer than the fin edge's contact with the rib flange less the hold-backs: the weld is modeled on the contact only (never extended past it)"));
            var asu = fs.RibWeldUnresolved.Length > 0 ? Assumption.UnresolvedDimension(fs.RibWeldUnresolved, $"weld from {L(clip + hb + fs.RibWeldSetback)} below the plate top, on the flange contact only")
                : WeldTermination.Flag(_b.Welds, fs.RibWeldSize, L, fs.RibWeldHoldBack);
            string hold = WeldTermination.Note(_b.Welds, fs.RibWeldSize, L, fs.RibWeldHoldBack, fs.RibWeldHoldBackSource);
            AddWeldPart($"{RibAsm(h.Rib)}-FIN-{h.Name}-W", RibAsm(h.Rib), $"{L(fs.RibWeldSize)} fillet, both sides, fin plate {h.Name} to the rib flanges, 4 x D {L(rf.WeldLength)} (p.148), on the flange contact, {hold}",
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
                double hb = WeldTermination.HoldBack(_b.Welds, r.Size, r.HoldBack);
                var asu = r.Unresolved.Length > 0 ? Assumption.UnresolvedDimension(r.Unresolved, $"{L(r.Size)} fillet along the full contact, held back {L(hb)} from its ends")
                    : WeldTermination.Flag(_b.Welds, r.Size, L, r.HoldBack);
                string hold = WeldTermination.Note(_b.Welds, r.Size, L, r.HoldBack, r.HoldBackSource);
                Func<Vec3, Vec3, bool>? keep = SideRule(r);
                int built = 0, missing = 0; double total = 0, overhang = 0, heldBack = 0; var noContact = new List<string>();
                foreach (var b in bs)
                {
                    // "$1" ... in Onto = the capture groups of Parts on this attached part's mark (e.g. the gusset's own connection plate).
                    var As = r.Onto.Contains('$') ? asAll.Where(a => System.Text.RegularExpressions.Regex.IsMatch(a.Mark, rxB.Match(b.Mark).Result(r.Onto))).ToList() : asAll;
                    var bb = bounds[b];
                    var m = new Mesh(); double len = 0;
                    var found = new List<WeldFinder.Bead>();
                    foreach (var a in As)
                    {
                        if (ReferenceEquals(a, b) || !Touch(bb, bounds[a], r.Tolerance + In(0.25))) continue;
                        found.AddRange(WeldFinder.TJoint(a.Mesh, b.Mesh, r.Tolerance, out double oh, r.AngleTolDeg, !r.OneSide, keep));
                        overhang += oh;
                    }
                    // runs along the contact (across adjoining parts), each stopped the hold-back short of its ends (corners, clips, end of contact)
                    var held = WeldTermination.Terminate(found, hb);
                    heldBack += WeldTermination.Length(found) - WeldTermination.Length(held);
                    len += WeldFinder.AddBeads(m, held, r.Size);
                    if (m.IsEmpty) { missing++; noContact.Add(b.Mark); continue; }
                    built++; total += len;
                    Model.Steel.Parts.Add(new SteelPart
                    {
                        Mark = $"{b.Mark}-W{(r.Name.Length > 0 ? "-" + r.Name : "")}", Category = PartCategory.Weld, Assembly = b.Assembly, Mesh = m, Length = len,
                        Material = r.Material, Assumption = asu,
                        Description = $"{L(r.Size)} fillet{(r.OneSide ? ", one side" : ", both sides")}, {b.Description.Split(',')[0]} to {r.Name} ({r.Source}), {hold}",
                    });
                }
                if (asu != null && built > 0) Warn(asu.Reason);
                Info($"Arch welds {r.Name}: {built} parts welded ({L(total)} of {L(r.Size)} fillet roots){(missing > 0 ? $", {missing} matching parts without a contact ({string.Join(", ", noContact.Take(6))}{(noContact.Count > 6 ? ", ..." : "")})" : "")}{(overhang > Tol ? $", {L(overhang)} of plate edge overhanging the joint (not welded)" : "")}, {L(heldBack)} of contact left unwelded at the run ends ({hold}) [{r.Source}].");
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
