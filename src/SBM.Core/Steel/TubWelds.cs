using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Steel;

/// <summary>
/// Shop welds of the steel tubs (plans: fillet welds 5/16" both sides TYP. - web to top / bottom flange p.278-279, transverse stiffeners and connection plates p.273
/// note 5, external diaphragm webs p.282, support diaphragm webs / bearing stiffeners p.283-288). Built like the I-girder bridges' welds: fillet bead meshes as
/// <see cref="PartCategory.Weld"/> parts (viewport, IFC, quantities). The web-to-flange welds sweep along the girder (as <c>SteelModelGenerator.BuildGirder</c>
/// does for I-girders, incl. the skewed ends); the rest are found as T-joints between the generated plates (<see cref="WeldFinder.TJoint"/>, as the arch
/// weld rules): where an edge face of one plate lies on a face of another, a bead on each side. Weld size = <see cref="TubGirderDefinition.FilletWeld"/>.
/// </summary>
public sealed partial class SteelModelGenerator
{
    /// <summary>Fillet beads along one web to flange joint (web side <paramref name="side"/>, outer / inner face, at the top or bottom flange) of a web run a..b.</summary>
    private double TubWebWeld(Mesh m, GirderResult g, double a, double b, int side, double tw, bool top, bool outer, double aw, bool holdA = true, bool holdB = true)
    {
        var t = TubDef;
        int gi = g.Index;
        double D = Sec.WebDepth, y = top ? 0 : -D;
        double xr = TubSection.WebFaceX(t, side, y, tw, outer);
        double sa = a, sb = b;
        if (a <= 1e-6 && TubEndSkew(gi, 0) is { } kA) { var fA = TF(gi, a); sa = a + (xr * fA.Cos - y * fA.Sin) * kA; }
        if (b >= g.Length - 1e-6 && TubEndSkew(gi, g.Spans.Count) is { } kB) { var fB = TF(gi, b); sb = b + (xr * fB.Cos - y * fB.Sin) * kB; }
        double hb = HoldBack(aw);                                   // held back from the plate ends (field section / girder ends); web shop splices run through
        if (holdA) sa += hb;
        if (holdB) sb -= hb;
        if (sb <= sa + WeldTermination.MinBead) return 0;
        var ss = g.Path.SamplesBetween(sa, sb).Concat(new[] { sa, sb }).Where(s => s >= sa - 1e-9 && s <= sb + 1e-9).Distinct().OrderBy(s => s).ToList();
        double len = 0;
        for (int i = 0; i + 1 < ss.Count; i++)
        {
            var p0 = TF(gi, ss[i]).At(xr, y);
            var p1 = TF(gi, ss[i + 1]).At(xr, y);
            var fm = TF(gi, (ss[i] + ss[i + 1]) / 2);
            var leg1 = Dir(fm, new Vec2(outer ? side : -side, 0)).Normalized();                                   // along the flange, away from the web
            var down = Dir(fm, TubSection.WebDown(t, side)).Normalized();
            var leg2 = top ? down : -down;                                                                         // along the web face, away from the flange
            MeshBuilder.AddFilletWeld(m, p0, p1, leg1, leg2, aw);
            len += (p1 - p0).Length;
        }
        return len;
    }

    /// <summary>Web to flange welds of one girder field section (both webs, top and bottom, both faces) as one weld part.</summary>
    private void TubSectionWelds(GirderResult g, string mark, double s0, double s1, IEnumerable<(double a, double b, double w, double th)> webPieces)
    {
        double aw = TubDef.FilletWeld;
        if (aw <= 0) return;
        var wm = new Mesh();
        double len = 0;
        var pieces = webPieces.ToList();
        foreach (int side in new[] { -1, 1 })
            foreach (var (a, b, _, tw) in pieces)
                foreach (bool top in new[] { true, false })
                    foreach (bool outer in new[] { true, false })
                        len += TubWebWeld(wm, g, a, b, side, tw, top, outer, aw, a <= s0 + 1e-6, b >= s1 - 1e-6);
        if (wm.IsEmpty) return;
        _parts.Add(new SteelPart
        {
            Mark = $"{mark}-FW", Category = PartCategory.Weld, Assembly = mark, Mesh = wm, Length = len, Material = "E70XX",
            Description = $"Fillet weld {In(aw)}, flange to web, both sides{HoldNote(aw)}", Assumption = WeldFlag(aw),
        });
    }

    private sealed record TubWeldRule(string Name, string Attached, string Onto, bool SameAssembly, string Where);

    private static readonly TubWeldRule[] TubWeldRules =
    {
        new("SW", @"-TS\d+\.\d+[LR]$", @"-W[LR]\d+$", true, "transverse stiffener to web"),
        new("SF", @"-TS\d+\.\d+[LR]$", @"-TF[LR]\d+$", true, "transverse stiffener to top flange"),
        new("TB", @"-K\d+\.\d+-TAB\d+$", @"-BF\d+$", false, "cross frame tab to bottom flange"),
        new("GS", @"-K\d+\.\d+-G$", @"-K\d+\.\d+-TS$", true, "cross frame gusset to top strut"),
        new("IT", @"-SD\d+-W$", @"-SD\d+-TF$", true, "interior support diaphragm web to top flange"),
        new("IB", @"-SD\d+-W$", @"-BF\d+$", false, "interior support diaphragm web to girder bottom flange"),
        new("BW", @"-SD\d+-BS\d+$", @"-SD\d+-W$", true, "bearing stiffener to diaphragm web"),
        new("BB", @"-SD\d+-BS\d+$", @"-BF\d+$", false, "bearing stiffener to girder bottom flange"),
        new("PW", @"-SD\d+-ST\d+$", @"-SD\d+-W$", true, "strut plate to diaphragm web"),
        new("PB", @"-SD\d+-ST\d+$", @"-SD\d+-BS\d+$", true, "strut plate to bearing stiffener"),
        new("XT", @"^(XSD\d+-\d+|XD\d+)-W$", @"-TF$", true, "external diaphragm web to top flange"),
        new("XB", @"^(XSD\d+-\d+|XD\d+)-W$", @"-BF$", true, "external diaphragm web to bottom flange"),
    };

    /// <summary>The mesh with outward-facing triangles (a copy with flipped winding when its signed volume is negative).</summary>
    private static Mesh Outward(Mesh m)
    {
        if (m.Volume() >= 0) return m;
        var f = new Mesh();
        foreach (var p in m.Positions) f.Add(p);
        for (int i = 0; i + 2 < m.Indices.Count; i += 3) f.Tri(m.Indices[i], m.Indices[i + 2], m.Indices[i + 1]);
        return f;
    }

    private static bool BoxesTouch(Box3 a, Box3 b, double tol) =>
        a.Max.X + tol >= b.Min.X && b.Max.X + tol >= a.Min.X && a.Max.Y + tol >= b.Min.Y && b.Max.Y + tol >= a.Min.Y && a.Max.Z + tol >= b.Min.Z && b.Max.Z + tol >= a.Min.Z;

    /// <summary>T-joint fillet welds between the generated tub plates (rules above); one weld part per attached plate and rule. Rules with no contact are reported.</summary>
    private void TubShopWelds()
    {
        double aw = TubDef.FilletWeld;
        if (aw <= 0) return;
        var parts = _parts.Where(p => p.Category != PartCategory.Weld && !p.Mesh.IsEmpty).ToList();
        var bounds = parts.ToDictionary(p => p, p => p.Mesh.Bounds());
        // plates built through a mirrored map (right webs / flanges) come out inside-out: the T-joint search needs outward normals
        var oriented = parts.ToDictionary(p => p, p => Outward(p.Mesh));
        double tol = Inch(0.25);   // plates are placed analytically and the long girder plates are strips of chords (grade, cross slope, skew): up to ~5 mm between the faces
        var added = new List<SteelPart>();
        foreach (var r in TubWeldRules)
        {
            var rxB = new System.Text.RegularExpressions.Regex(r.Attached);
            var rxA = new System.Text.RegularExpressions.Regex(r.Onto);
            var bs = parts.Where(p => rxB.IsMatch(p.Mark)).ToList();
            var asList = parts.Where(p => rxA.IsMatch(p.Mark)).ToList();
            int built = 0; double total = 0;
            foreach (var b in bs)
            {
                var bb = bounds[b];
                var m = new Mesh(); double len = 0;
                foreach (var a in asList)
                {
                    if (ReferenceEquals(a, b) || (r.SameAssembly && a.Assembly != b.Assembly) || !BoxesTouch(bb, bounds[a], tol + Inch(0.25))) continue;
                    // runs end the hold-back short of the plate corners, clips and the end of contact
                    len += WeldFinder.AddBeads(m, WeldTermination.Terminate(WeldFinder.TJoint(oriented[a], oriented[b], tol, 6, true, null, true), HoldBack(aw)), aw);
                }
                if (m.IsEmpty) continue;
                built++; total += len;
                added.Add(new SteelPart
                {
                    Mark = $"{b.Mark}-W{r.Name}", Category = PartCategory.Weld, Assembly = b.Assembly, Mesh = m, Length = len, Material = "E70XX",
                    Description = $"Fillet weld {In(aw)}, {r.Where}, both sides{HoldNote(aw)}", Assumption = WeldFlag(aw),
                });
            }
            Info($"Tub welds {r.Name} ({r.Where}): {built} of {bs.Count} plates welded ({Ft2(total)} of {In(aw)} fillet roots).");
        }
        foreach (var w in added) _parts.Add(w);
    }
}
