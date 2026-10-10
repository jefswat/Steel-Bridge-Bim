using SBM.Core.Concrete;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Steel;
using SBM.Core.Units;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Checks;

/// <summary>
/// Clash / embedment check between the deck end blocks ("end dam diaphragms") and the steel. Nothing is moved.
/// Steel the plans show cast into the block is reported as INTENDED (Info): the girder ends (top flange, upper web, stiffeners, welds, studs run
/// through the diaphragm: Ramp G pdf 138 partial section near expansion-expansion pier) and a rolled-beam top strut whose flange is embedded flush
/// to the soffit (W24x68 abutment diaphragm, Ramp G pdf 137 section A-A). Anything else above a soffit (gussets, angles, channels, bolts, bearing
/// parts) is a WARNING. Points tested: mesh vertices and triangle centroids of every part, stud origins/tops, bolt heads and nuts.
/// </summary>
public static class DeckEndBlockCheck
{
    /// <summary>Rolled top strut: embedded flange allowed up to this height above the soffit (flange thickness plus tolerance).</summary>
    public static readonly double StrutEmbedTolerance = In(1.0);

    public static void Run(BridgeFraming f, SteelModel steel, ConcreteModel concrete, UnitSystem u)
    {
        var blocks = concrete.Joints.Select(j => j.EndBlock).Where(b => b != null).Select(b => b!).ToList();
        if (blocks.Count == 0) return;
        var asm = steel.Assemblies.ToDictionary(a => a.Mark, a => a, StringComparer.Ordinal);
        foreach (var blk in blocks)
        {
            var rows = new Dictionary<string, (bool intended, HashSet<string> parts, double max, string note)>();
            void Hit(string member, bool intended, string part, double above, string note)
            {
                if (!rows.TryGetValue(member, out var r)) r = (intended, new HashSet<string>(), 0, note);
                r.parts.Add(part);
                r.max = Math.Max(r.max, above);
                rows[member] = (r.intended && intended, r.parts, r.max, r.note);
            }
            foreach (var part in steel.Parts)
            {
                double above = Scan(blk, part.Mesh);
                if (above < 0) continue;
                asm.TryGetValue(part.Assembly, out var a);
                var (name, intended, note) = Classify(part, a, above);
                Hit(name, intended, part.Mark, above, note);
            }
            int studs = 0; double studMax = 0;
            foreach (var st in steel.Studs)
            {
                double above = double.MinValue;
                foreach (var p in new[] { st.Frame.Origin, st.Frame.Origin + st.Frame.Z * st.Length })
                {
                    var h = blk.Probe(p);
                    if (h.Inside) above = Math.Max(above, h.AboveSoffit);
                }
                if (above > double.MinValue) { studs++; studMax = Math.Max(studMax, above); }
            }
            if (studs > 0)
                rows["Shear studs"] = (true, Enumerable.Range(0, studs).Select(i => "stud" + i).ToHashSet(), studMax, "studs on the embedded top flanges are cast into the block");
            var bolts = new Dictionary<string, (int n, double max)>();
            foreach (var b in steel.Bolts)
            {
                double above = double.MinValue;
                foreach (var p in new[] { b.Frame.Origin, b.Frame.Origin + b.Frame.Z * b.Length })
                {
                    var h = blk.Probe(p);
                    if (h.Inside) above = Math.Max(above, h.AboveSoffit);
                }
                if (above == double.MinValue) continue;
                string key = asm.TryGetValue(b.Assembly, out var ba) ? $"Bolts, {ba.Kind}" : "Bolts";
                bolts.TryGetValue(key, out var t);
                bolts[key] = (t.n + 1, Math.Max(t.max, above));
            }
            foreach (var (k, t) in bolts)
                rows[k] = (false, Enumerable.Range(0, t.n).Select(i => "bolt" + i).ToHashSet(), t.max, "bolt inside the end block concrete");

            var intendedRows = new List<string>();
            foreach (var (member, r) in rows.OrderBy(x => !x.Value.intended).ThenBy(x => x.Key))
            {
                concrete.EndBlockClashes.Add(new DeckEndClash(blk.Support, member, r.intended, r.parts.Count, r.max, r.note));
                if (r.intended) { intendedRows.Add($"{member} ({r.parts.Count}, up to {u.FormatLength(r.max, LengthKind.Short)})"); continue; }
                concrete.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning,
                    $"Deck end block at {blk.Support}: CLASH not detailed in the plans: {member} - {r.parts.Count} part(s) up to {u.FormatLength(r.max, LengthKind.Short)} above the soffit ({r.note})."));
            }
            if (intendedRows.Count > 0)
                concrete.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info,
                    $"Deck end block at {blk.Support}: steel embedded as the plans show (girder ends through the end dam diaphragm; heights above the soffit): {string.Join("; ", intendedRows)}."));
            if (rows.Count == 0)
                concrete.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info, $"Deck end block at {blk.Support}: no steel inside the block."));
        }
    }

    /// <summary>Highest point of the part above the soffit among points inside the block, or -1 when none is inside.</summary>
    private static double Scan(DeckEndBlockResult blk, SBM.Core.Solids.Mesh m)
    {
        double best = -1;
        double xmax = blk.Soffit[^1].X + 1e-6;
        var pos = m.Positions;
        bool any = false;
        foreach (var p in pos)
        {
            double x = blk.PlanX(p.XY);
            if (x >= -1e-6 && x <= xmax) { any = true; break; }
        }
        if (!any) return -1;
        foreach (var p in pos)
        {
            var h = blk.Probe(p);
            if (h.Inside) best = Math.Max(best, h.AboveSoffit);
        }
        var idx = m.Indices;
        for (int i = 0; i + 2 < idx.Count; i += 3)
        {
            var c = (pos[idx[i]] + pos[idx[i + 1]] + pos[idx[i + 2]]) * (1.0 / 3);
            var h = blk.Probe(c);
            if (h.Inside) best = Math.Max(best, h.AboveSoffit);
        }
        return best;
    }

    private static (string name, bool intended, string note) Classify(SteelPart part, SteelAssembly? a, double above)
    {
        if (a?.Kind == AssemblyKind.GirderFieldSection)
        {
            string n = part.Category switch
            {
                PartCategory.FlangePlate => "Girder top flange",
                PartCategory.WebPlate => "Girder web (upper part)",
                PartCategory.BearingStiffener => "Girder bearing stiffeners",
                PartCategory.ConnectionStiffener => "Girder connection stiffeners",
                PartCategory.IntermediateStiffener => "Girder intermediate stiffeners",
                PartCategory.Weld => "Girder welds",
                _ => "Girder " + part.Category,
            };
            return (n, true, "girder ends run through the end dam diaphragm");
        }
        if (a?.Kind == AssemblyKind.CrossFrame && part.Role == MemberRole.TopChord && part.Description.StartsWith("W", StringComparison.Ordinal))
        {
            bool ok = above <= StrutEmbedTolerance;
            return ($"Rolled top strut {part.Description}", ok, ok ? "top flange flush with the soffit, as the Ramp G W24x68 abutment diaphragm detail" : "projects above the soffit by more than a flange thickness");
        }
        string kind = a?.Kind.ToString() ?? "Steel";
        string what = part.Role != MemberRole.None ? part.Role.ToString() : part.Category.ToString();
        return ($"{kind} {what}", false, "above the soffit, inside the end block concrete");
    }
}
