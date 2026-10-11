using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Geometry;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Substructure;

/// <summary>
/// Abutment bars placed from the plan dimensions beyond the footing / backwall: maskwall verticals 5e2 and horizontals 5e3, counterfort verticals 6h2 / 6h3 and
/// hoops 5h4, and the pile uplift anchors 8j3. Every position is a printed dimension or follows from printed values (docs/plan-data/abutment-followups.md section 3);
/// what the plans leave open stays a schedule-only mark (listed in <see cref="UnresolvedAbutmentMarks"/> and reported in one warning).
/// </summary>
public sealed partial class SubstructureGenerator
{
    /// <summary>Marks that are NOT placed in 3D and why (abutment-followups.md 3.1 - 3.9).</summary>
    private static readonly (string[] Marks, string Why)[] UnresolvedAbutmentMarks =
    {
        (new[] { "6h2", "6h3" }, "L bars with a 1'-0 foot whose direction (toward the wall or the fill) is not given"),
        (new[] { "5g2" }, "the end of the 6g1 row at which each dowel sits (position across the backwall) is not given"),
        (new[] { "5h1" }, "the along-wall spacing of the 6 counterfort dowels is not printed (scaled 1'-1, not 1'-0)"),
        (new[] { "5j1" }, "the along-wall positions of the wing-footing U bars are scaled only and their level against the pile heads (3\" clear vs piles 1'-0 high) is not settled"),
        (new[] { "5j2" }, "the hoop positions across the wing footing are not dimensioned (H-H and the legend scale differ by up to 0.25 ft)"),
        (new[] { "5e1", "5e4" }, "the length rule of the wing-wall verticals is not derivable from the cover rules (13'-1 maximum at a 12.98 ft wall)"),
        (new[] { "6f1", "6f2", "6f3", "6f5", "6f6", "6f7" }, "the end cover / lap of the wing-wall horizontal rows is not printed (row lengths are listed, ends are not dimensioned)"),
        (new[] { "6f4", "6g4" }, "the vertical position of the first wing-to-backwall dowel and the 6f4 spacing are not dimensioned"),
        (new[] { "5d3", "5d4", "5d5", "4d6" }, "the first-bar offsets of the corbel / paving-notch strings are not dimensioned (stagger dimensions only)"),
        (new[] { "6g1", "6g5" }, "the elevations of the upper-pour backwall rows (and so the 6g5 rows matching them) are not dimensioned"),
        (new[] { "5d7", "4d8" }, "the start of the extension strings relative to the extension strip is not settled"),
        (new[] { "5m1", "5n1", "5n2" }, "the spacing of the beam-step bars and the elevation of the 5n2 hoop in the step are not dimensioned"),
        (new[] { "5b2", "5b3", "5b4" }, "the end-bar widths / first position (West 5b2), the levels (East / South 5b2) and the path of 5b3 / 5b4 across the notches are not settled"),
    };

    /// <summary>Maskwall, counterfort and uplift bars (cap coordinates -> world through <paramref name="unit"/>); appended to <paramref name="placed"/>.</summary>
    private void AbutmentWingBars(SubstructureUnit unit, AbutmentGeometry g, List<PBar> placed)
    {
        var ad = g.Def;
        var rb = ad.Rebar;
        string name = unit.Name;
        PlanBarLine? Line(string mark) => rb.PlanBarList.FirstOrDefault(l => l.Mark == mark);
        var note = new List<string>();

        // ---------------------------------------------------------------- maskwalls
        var l2 = Line("5e2"); var l3 = Line("5e3");
        if (g.Masks.Count > 0 && l2 != null && l3 != null)
        {
            var s2 = RebarCatalog.Size("#5"); var s3 = RebarCatalog.Size("#5");
            int nMask = g.Masks.Count;
            int n2 = l2.Count / nMask, n3 = l3.Count / (2 * nMask);
            if (l2.Count % nMask != 0 || l3.Count % (2 * nMask) != 0)
                Warn($"{name}: maskwall bars 5e2 ({l2.Count}) / 5e3 ({l3.Count}) do not divide over {nMask} wall(s) - not placed.");
            else
                foreach (var mk in g.Masks)
                {
                    if (mk.EndsAt == null) continue;
                    double dirIn = Math.Sign(mk.EInner - mk.EOuter);
                    double eA = mk.EOuter + dirIn * In(3.5), eB = mk.EInner - dirIn * In(3.5);                              // verticals: 3 1/2" CL from each face (printed from the recessed face; other line not dimensioned)
                    double eHo = mk.EOuter + dirIn * (In(2) + s3.Diameter / 2), eHi = mk.EInner - dirIn * (In(2) + s3.Diameter / 2);   // horizontals outside the verticals at the printed 2" min clear
                    Vec3 At(double e, double t, double z)
                    {
                        var (pb, pf) = mk.EndsAt(e);
                        double lw = (pf - pb).Length;
                        var p = pb + (pf - pb) * (t / lw);
                        return unit.World(p.X, p.Y, z);
                    }
                    double Len(double e) { var (pb, pf) = mk.EndsAt(e); return (pf - pb).Length; }
                    string side = mk.End == AbutmentEnd.Left ? "left" : "right";
                    double zTop2 = mk.TopZ - In(2), len2 = l2.Length;
                    double need = mk.TopZ - In(2) - (mk.BaseZ - Ft(1.5));
                    if (Math.Abs(need - len2) > In(1))
                        Warn($"{name}: " + UnresolvedFlag.Text(ad.Source != "" ? ad.Source + ", bar list" : "abutment bar list", $"maskwall {side}: 5e2 length", $"the list prints one length for all maskwalls ({Sh(len2)}) but this wall needs {Sh(need)} (height + 1'-6 embedment - 2\" cover)", "The bar hangs from the top cover at the listed length."));
                    for (int i = 0; i < n2; i++)
                    {
                        double e = i % 2 == 0 ? eA : eB;
                        double lw = Len(e);
                        double t = In(3) + i * (lw - In(6)) / Math.Max(1, n2 - 1);
                        placed.Add(new PBar("5e2", new[] { At(e, t, zTop2 - len2), At(e, t, zTop2) }, BarShape.Straight(len2)));
                    }
                    double z0 = mk.TopZ - In(3), z1 = mk.BaseZ + In(3);
                    foreach (double e in new[] { eHo, eHi })
                    {
                        double lw = Len(e);
                        if (l3.Length > lw - In(4)) Warn($"{name}: maskwall {side}: 5e3 length {Sh(l3.Length)} is within 2\" of the wall length {Sh(lw)}.");
                        for (int k = 0; k < n3; k++)
                        {
                            double z = z0 + (z1 - z0) * k / Math.Max(1, n3 - 1);
                            placed.Add(new PBar("5e3", new[] { At(e, lw / 2 - l3.Length / 2, z), At(e, lw / 2 + l3.Length / 2, z) }, BarShape.Straight(l3.Length)));
                        }
                    }
                }
            note.Add("maskwall 5e2 / 5e3: the cross-section lines are not all dimensioned (D-D prints 3 1/2\" CL from the recessed face only): verticals alternate between lines 3 1/2\" from each face, horizontals sit at the printed 2\" min clear outside them");
        }

        // ---------------------------------------------------------------- counterforts and uplift anchors
        foreach (var w in g.Wings)
        {
            foreach (var c in w.Counterforts)
            {
                var cf = w.Def.Counterforts[c.Index - 1];
                var R = new Vec2(c.Tangent.Y, -c.Tangent.X);
                Vec3 W(double u, double d, double z)
                {
                    double e = w.EIn - w.SideSign * cf.Projection + w.SideSign * d;            // d from the counterfort's fill-side end face toward the wall
                    var p = c.Origin + c.Tangent * u + R * e;
                    return new Vec3(p.X, p.Y, z);
                }
                double bw = c.BodyWidth > 0 ? c.BodyWidth : c.Width;

                // 6h2 / 6h3 verticals: perimeter ring of 4 + 4 bars on the two 3'-6 faces ("3 SPA. @ 1'-0 = 3'-0", 3" end cover) + 2 interior bars on the end face
                var lv = Line(cf.VerticalMark);
                if (lv != null && cf.VerticalFoot <= 0)
                {
                    double z0 = c.FootingBottom + In(3), z1 = z0 + lv.Length;
                    if (z1 > c.TopZ - In(1.9)) Warn($"{name}: wing {w.Def.Name} counterfort {c.Index}: {cf.VerticalMark} {Sh(lv.Length)} from 3\" above the footing bottom reaches {El(z1)}, above the counterfort top cover ({El(c.TopZ)} - 2\").");
                    double uo = bw / 2 - In(3);
                    var pos = new List<(double u, double d)>();
                    foreach (double us in new[] { -uo, uo }) for (int k = 0; k < 4; k++) pos.Add((us, In(3) + k * Ft(1)));
                    for (int j = 1; j <= 2; j++) pos.Add((-uo + 2 * uo * j / 3, In(3)));
                    if (pos.Count != cf.VerticalBarsPerSide) Warn($"{name}: counterfort {c.Index}: ring of {pos.Count} bars vs input {cf.VerticalBarsPerSide}.");
                    foreach (var (u, d) in pos) placed.Add(new PBar(cf.VerticalMark, new[] { W(u, d, z0), W(u, d, z1) }, BarShape.Straight(lv.Length)));
                }

                // 5h4 hoops: first 3" below the counterfort top, last 3" above the footing top, equal spaces; U staple (closed end at the fill-side face, legs through the counterfort into the wall)
                var lh = Line("5h4");
                if (lh != null && lh.Dims.Length == 5)
                {
                    var size = RebarCatalog.Size("#5"); double r = size.Diameter / 2;
                    double top = lh.Dims[2], foot = lh.Dims[0], leg = lh.Dims[1];
                    double ul = top / 2 - r, d0 = In(2) + r, dL = In(2) + leg - r, ut = top / 2 - foot;
                    var shape = ScheduleShape(lh, size);
                    int n = cf.HoopSpaces + 1;
                    double zLo = c.FootingTop + In(3), zHi = c.TopZ - In(3);
                    for (int j = 0; j < n; j++)
                    {
                        double z = zLo + (zHi - zLo) * j / Math.Max(1, cf.HoopSpaces);
                        placed.Add(new PBar("5h4", new[] { W(-ut, dL, z), W(-ul, dL, z), W(-ul, d0, z), W(ul, d0, z), W(ul, dL, z), W(ut, dL, z) }, shape));
                    }
                }
            }

            // 8j3 uplift anchors (2 bars per circled pile, 4" apart along the web, V across the web, hole 6" above the footing bottom)
            var l8 = Line("8j3");
            if (l8 != null && l8.Shape == "V" && l8.Dims.Length == 3)
            {
                var size = RebarCatalog.Size("#8");
                double pin = l8.Pin > 0 ? l8.Pin : size.MinPinDiameter, half = l8.Dims[1] / 2, rise = l8.Dims[2], rc = pin / 2 + size.Diameter / 2;
                var bends = new List<(Vec2, double)>();
                var local = BarShape.Rounded(new[] { new Vec2(-half, rise), new Vec2(0, 0), new Vec2(half, rise) }, rc, bends);
                double yMin = local.Min(q => q.Y);
                local = local.Select(q => new Vec2(q.X, q.Y - yMin)).ToArray();           // the inside of the V bend rests in the hole
                var shape = ScheduleShape(l8, size);
                foreach (var up in w.Def.UpliftPiles)
                {
                    if (up.Footing < 1 || up.Footing > w.Footings.Count || up.Pile < 1 || up.Pile > w.Footings[up.Footing - 1].Piles.Count)
                    { Warn($"{name}: wing {w.Def.Name}: uplift pile (footing {up.Footing}, pile {up.Pile}) does not exist."); continue; }
                    var f = w.Footings[up.Footing - 1]; var p = f.Piles[up.Pile - 1];
                    double fl = p.WebN, fs = -p.WebS;                                        // flange direction (S, N) = across the web
                    double zHole = f.BottomZ + In(6);
                    for (int k = 0; k < 2; k++)
                    {
                        double u = (k - 0.5) * In(4);
                        double cs = p.S + p.WebS * u, cn = p.N + p.WebN * u;
                        placed.Add(new PBar("8j3", local.Select(q => unit.World(cs + fl * q.X, cn + fs * q.X, zHole + q.Y)).ToArray(), shape));
                    }
                }
            }
        }
        if (note.Count > 0) Warn($"{name}: placed from printed dimensions with one reading to confirm - {string.Join("; ", note)}.");
    }

    /// <summary>One warning that lists every schedule-only mark with the reason it is not placed.</summary>
    private void ReportUnresolvedAbutmentMarks(string name, string source, IEnumerable<string> scheduleOnlyMarks)
    {
        var rest = new HashSet<string>(scheduleOnlyMarks);
        var parts = new List<string>();
        foreach (var (marks, why) in UnresolvedAbutmentMarks)
        {
            var hit = marks.Where(rest.Remove).ToList();
            if (hit.Count > 0) parts.Add($"{string.Join("/", hit)}: {why}");
        }
        if (rest.Count > 0) parts.Add($"{string.Join(", ", rest)}: no placement dimensions in the plans");
        if (parts.Count > 0) Warn($"{name}: UNRESOLVED ({(source != "" ? source + ", " : "")}abutment bar list and details; schedule only, no 3D bar) - {string.Join("; ", parts)}.");
    }
}
