using System.Text.RegularExpressions;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Misc;
using SBM.Core.Solids;
using SBM.Core.Substructure;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Segmental;

// OPTIONAL misc items of a segmental unit (SegmentalDefinition.Misc, Bridge/SegmentalMisc.cs): piles under the bent footings (added to the
// segmental bents' SubstructureUnits, so viewport / IFC IfcPile / database / quantities take them like any pile), deck drains and expansion joint
// items as a MiscModel on the bents' SubstructureModel (IFC ExportMisc, database misc groups, viewport misc layers), end segment joint blockouts as
// openings. Every plan-vs-model figure goes to SegmentalModel.Checks; nothing is adjusted.
public sealed partial class SegmentalGenerator
{
    /// <summary>Lateral column centre of a bent (S, right +) from the OPTIONAL foundation data; 0 = on the bent's alignment point.</summary>
    private double ColumnOffsetS(string bent) => _d.Misc?.Foundation(bent)?.ColumnOffsetS ?? 0;

    private void BuildMisc()
    {
        if (_d.Misc is not { } md) return;
        foreach (var u in md.Unresolved) Warn(u);
        BuildPiles(md);
        var misc = new MiscModel();
        foreach (var dd in md.Drains) BuildDrains(dd, misc);
        foreach (var jd in md.Joints) BuildJoint(jd, misc);
        foreach (var fx in md.Fixtures) BuildFixtures(fx, misc);
        if (misc.IsEmpty) return;
        _m.Substructure ??= new SubstructureModel();
        _m.Substructure.Misc = misc;
    }

    // ------------------------------------------------------------------ piles

    private void BuildPiles(SegmentalMiscDefinition md)
    {
        foreach (var fd in md.Foundations.Where(f => f.Piles.Count > 0))
        {
            string where = fd.Bent;
            var unit = _m.Substructure?.Units.FirstOrDefault(u => u.Name.Equals(fd.Bent, StringComparison.OrdinalIgnoreCase));
            var f = unit?.Foundations.FirstOrDefault();
            if (unit == null || f == null) { Warn($"Segmental piles: {where} has no footing in the model - {fd.Piles.Count} piles not built."); continue; }
            if (double.IsNaN(fd.PileSize) || double.IsNaN(fd.Embedment) || double.IsNaN(fd.TipElevation))
            {
                Warn(UnresolvedFlag.Text(fd.Source.Length > 0 ? fd.Source : Pages, $"{where} piles",
                    double.IsNaN(fd.PileSize) ? "the pile size is not given" : double.IsNaN(fd.Embedment) ? "the pile head embedment in the footing is not given" : "no tip elevation is given",
                    $"{fd.Piles.Count} piles not built."));
                continue;
            }
            var sd = unit.Definition;
            sd.PileType = PileType.PrecastConcrete; sd.PileSize = fd.PileSize; sd.PileEmbedment = fd.Embedment;
            if (fd.PileLabel.Length > 0) sd.PileSection = fd.PileLabel;
            var flag = fd.Unresolved.Length > 0 ? Assumption.UnresolvedDimension(fd.Unresolved, "Piles built at the plan positions to the modeled tip.") : null;
            if (fd.Unresolved.Length > 0) Warn(fd.Unresolved);
            double a2 = fd.PileSize / 2, ch = Math.Clamp(fd.PileChamfer, 0, a2 * 0.9);
            var section = ch > 0
                ? new[] { new Vec2(-a2 + ch, -a2), new Vec2(a2 - ch, -a2), new Vec2(a2, -a2 + ch), new Vec2(a2, a2 - ch), new Vec2(a2 - ch, a2), new Vec2(-a2 + ch, a2), new Vec2(-a2, a2 - ch), new Vec2(-a2, -a2 + ch) }
                : new[] { new Vec2(-a2, -a2), new Vec2(a2, -a2), new Vec2(a2, a2), new Vec2(-a2, a2) };
            bool hollow = fd.PileVoidDiameter > 0 && fd.PileVoidDiameter < fd.PileSize;
            var voidSec = hollow ? Enumerable.Range(0, 32).Select(i => new Vec2(fd.PileVoidDiameter / 2 * Math.Cos(2 * Math.PI * i / 32), fd.PileVoidDiameter / 2 * Math.Sin(2 * Math.PI * i / 32))).ToArray() : null;
            double h = fd.BatterRatio > 0 ? 1.0 / fd.BatterRatio : 0;
            double total = 0;
            int k = 0, noVoid = 0;
            foreach (var p in fd.Piles)
            {
                k++;
                double emb = double.IsNaN(p.Embedment) ? fd.Embedment : p.Embedment;
                double top = f.BottomZ + emb;
                double drop = top - fd.TipElevation;
                if (drop <= 0) { Warn($"Segmental piles: {where} pile {k} head EL {top / Ft(1):0.00} is not above the tip EL {fd.TipElevation / Ft(1):0.00} - not built."); continue; }
                double bl = Math.Sqrt(p.BatterS * p.BatterS + p.BatterN * p.BatterN);
                bool battered = bl > 1e-9 && h > 0;
                double us = battered ? p.BatterS / bl : 0, un = battered ? p.BatterN / bl : 0, hh = battered ? h : 0;
                var ax = new Vec3(-us * hh, -un * hh, 1).Normalized();          // axis in the bent frame (S, N, Z), tip to head
                double hs = p.S - us * hh * emb, hn = p.N - un * hh * emb;       // plan position given at the footing bottom: head slightly inside
                double ts = hs + us * hh * drop, tn = hn + un * hh * drop;
                var info = new PileInfo(hs, hn, top, fd.TipElevation, ts, tn);
                // Section perpendicular to the axis, plumb orientation rotated minimally onto the axis (Rodrigues); head and tip cut horizontal.
                Vec3 ex = new(1, 0, 0), ey = new(0, 1, 0);
                if (battered)
                {
                    var kk = Vec3.Cross(new Vec3(0, 0, 1), ax);
                    double sinT = kk.Length, cosT = ax.Z;
                    kk = kk.Normalized();
                    Vec3 Rot(Vec3 v) => v * cosT + Vec3.Cross(kk, v) * sinT + kk * (Vec3.Dot(kk, v) * (1 - cosT));
                    ex = Rot(ex); ey = Rot(ey);
                }
                var m = new Mesh();
                var headRing = new Vec3[section.Length]; var tipRing = new Vec3[section.Length];
                for (int i = 0; i < section.Length; i++)
                {
                    var q = ex * section[i].X + ey * section[i].Y;
                    q += ax * (-q.Z / ax.Z);
                    headRing[i] = unit.World(hs + q.X, hn + q.Y, top);
                    tipRing[i] = unit.World(hs + q.X + us * hh * drop, hn + q.Y + un * hh * drop, fd.TipElevation);
                }
                MeshBuilder.AddLoft(m, section, new[] { tipRing, headRing }, !hollow);
                string voidText = "";
                if (hollow)
                {
                    double len = drop / ax.Z, v0 = fd.VoidSolidAtHead, v1 = len - fd.VoidSolidAtTip;
                    if (v1 - v0 > 1e-3)
                    {
                        // Cavity = a closed loft with its triangles reversed (the solid has an inner shell): volume = outer - void.
                        Vec3[] Ring(double a) => voidSec!.Select(c => { var q = ex * c.X + ey * c.Y - ax * a; return unit.World(hs + q.X, hn + q.Y, top + q.Z); }).ToArray();
                        var cav = new Mesh();
                        MeshBuilder.AddLoft(cav, voidSec!, new[] { Ring(v1), Ring(v0) }, false);
                        int b0 = m.Positions.Count;
                        foreach (var v in cav.Positions) m.Add(v);
                        for (int t = 0; t < cav.Indices.Count; t += 3) m.Tri(b0 + cav.Indices[t], b0 + cav.Indices[t + 2], b0 + cav.Indices[t + 1]);
                        voidText = $", {fd.PileVoidDiameter / In(1):0.##} in void ({fd.VoidSolidAtHead / Ft(1):0.##} ft solid at the head, {fd.VoidSolidAtTip / Ft(1):0.##} ft at the tip)";
                    }
                    else noVoid++;
                }
                string batter = battered ? $", battered 1:{fd.BatterRatio:0.##} (H:V) toward {(Math.Abs(us) > 0.01 ? (us > 0 ? "+S" : "-S") : "")}{(Math.Abs(un) > 0.01 ? (un > 0 ? "+N" : "-N") : "")}" : "";
                batter += voidText + (ch > 0 ? $", {ch / In(1):0.##} in chamfers" : "");
                f.Piles.Add(info);
                total += info.Length;
                unit.Solids.Add(new SubSolid(SubElementKind.Pile, $"{where} pile {f.Piles.Count}",
                    $"Pile {(fd.PileLabel.Length > 0 ? fd.PileLabel : $"{fd.PileSize / In(1):0.##} in square")}, {info.Length / Ft(1):0.00} ft along the axis{batter}, head EL {top / Ft(1):0.00}, tip EL {fd.TipElevation / Ft(1):0.00}" +
                    (fd.PileMaterial.Length > 0 ? $" ({fd.PileMaterial})" : ""), m) { Assumption = flag });
            }
            if (noVoid > 0) Info($"Segmental piles {where}: {noVoid} piles shorter than the two solid end sections - built solid.");
            if (fd.PlanCount > 0)
                _m.Checks.Add(new SegmentalCheck($"{where}: pile count", f.Piles.Count, fd.PlanCount, f.Piles.Count - fd.PlanCount, fd.Source));
            if (!double.IsNaN(fd.PlanTotalLength))
                _m.Checks.Add(new SegmentalCheck($"{where}: total pile length (model head to tip vs plan bill of material)", total, fd.PlanTotalLength, total - fd.PlanTotalLength,
                    $"tip EL {fd.TipElevation / Ft(1):0.0} (plan 'pile tip no higher than'); {fd.Source}"));
            // Pile heads inside the footing outline (plan positions are at the footing bottom; heads move inward with the batter).
            var bent = _d.Bent(fd.Bent);
            if (bent?.Pier?.FootingOutline is { Count: >= 3 } ol)
            {
                var poly = ol.Select(v => v.ToVec2()).ToList();
                int outside = f.Piles.Count(pi => !Inside(poly, new Vec2(pi.S, pi.N), fd.PileSize / 2));
                _m.Checks.Add(new SegmentalCheck($"{where}: piles whose head is not fully inside the footing outline", outside, 0, outside, "plan positions at the footing bottom plane"));
            }
            string scour = double.IsNaN(fd.ScourCriticalElevation) ? "" : $", scour critical EL {fd.ScourCriticalElevation / Ft(1):0.0}";
            string load = double.IsNaN(fd.MaxFactoredAxialTons) ? "" : $", max factored axial {fd.MaxFactoredAxialTons:0} tons/pile";
            Info($"Segmental piles {where}: {f.Piles.Count} piles, {total / Ft(1):0.0} LF{load}{scour}.");
        }
    }

    /// <summary>Point (with a margin) inside a simple polygon.</summary>
    private static bool Inside(List<Vec2> poly, Vec2 p, double margin)
    {
        bool In(Vec2 q)
        {
            bool c = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                if ((poly[i].Y > q.Y) != (poly[j].Y > q.Y) && q.X < (poly[j].X - poly[i].X) * (q.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X) c = !c;
            return c;
        }
        return In(p + new Vec2(margin, margin)) && In(p + new Vec2(-margin, margin)) && In(p + new Vec2(margin, -margin)) && In(p + new Vec2(-margin, -margin));
    }

    // ------------------------------------------------------------------ deck drains

    /// <summary>Underside of the wing / top slab outside the webs at |x| (section y down from the top).</summary>
    private double WingUnderside(double ax)
    {
        var b = _d.Box;
        double rt = b.TopWidth / 2;
        if (ax >= rt - b.WingFlat) return b.WingTip;
        if (ax <= b.WebRootX) return b.WebTopDepth;
        double u = (ax - b.WebRootX) / (rt - b.WingFlat - b.WebRootX);
        return b.WebTopDepth + (b.WingTip - b.WebTopDepth) * u;
    }

    private void BuildDrains(SegmentalDeckDrainDefinition dd, MiscModel misc)
    {
        if (double.IsNaN(dd.Diameter) || double.IsNaN(dd.EdgeDistance))
        {
            Warn(UnresolvedFlag.Text(dd.Source.Length > 0 ? dd.Source : Pages, dd.Name, double.IsNaN(dd.Diameter) ? "the drain diameter is not given" : "the drain position across the deck is not given", "Drains not built."));
            return;
        }
        if (dd.Unresolved.Length > 0) Warn(dd.Unresolved);
        var rx = dd.SegmentPattern.Length > 0 ? new Regex(dd.SegmentPattern, RegexOptions.IgnoreCase) : null;
        double ax = _d.Box.TopWidth / 2 - dd.EdgeDistance;
        if (ax <= _d.Box.WebRootX) { Warn($"Segmental {dd.Name}: the drain at {ax / Ft(1):0.00} ft from the CL is inside the outer web face - not built (only wing drains are supported)."); return; }
        int n = 0, left = 0, right = 0;
        foreach (var (r, d, _) in _segs)
        {
            if (!dd.SegmentTypes.Contains(d.Type) || (rx != null && !rx.IsMatch(r.Name))) continue;
            double len = r.Station1 - r.Station0;
            bool fromStart = d.Bent == "" || !_bentSta.TryGetValue(d.Bent, out double bs) || Math.Abs(r.Station0 - bs) <= Math.Abs(r.Station1 - bs);
            double sta = fromStart ? r.Station0 + dd.AlongFraction * len : r.Station1 - dd.AlongFraction * len;
            var fr = FrameOnChord(sta);
            int side = dd.Side switch { SegmentalDrainSide.Left => -1, SegmentalDrainSide.Right => 1, _ => fr.Lat.Z <= 0 ? 1 : -1 };
            if (side > 0) right++; else left++;
            double x = side * ax;
            var top = fr.At(new Vec2(x, dd.TopBelowSurface));
            var bot = fr.At(new Vec2(x, WingUnderside(ax)));
            var mesh = MiscMesh.Cylinder(bot, top, dd.Diameter / 2, 24);
            string asm = $"{r.Name} {dd.Name}";
            string desc = $"{dd.Diameter / In(1):0.##} in {dd.Name}, CL {dd.EdgeDistance / Ft(1):0.###} ft from the {(side > 0 ? "right" : "left")} deck edge, normal to the deck, top {dd.TopBelowSurface / In(1):0.##} in below the surface, at {sta / Ft(1):0.00}" +
                          (dd.Source.Length > 0 ? $" ({dd.Source})" : "");
            misc.Assemblies.Add(new MiscAssembly(MiscGroup.Drainage, asm, desc, "deck drain"));
            misc.Elements.Add(new MiscElement(MiscGroup.Drainage, MiscKind.DrainTrench, asm, asm, desc, mesh, dd.Material, Location: r.Name));
            n++;
        }
        Info($"Segmental {dd.Name}: {n} built ({right} on the right, {left} on the left side).");
        _m.Checks.Add(new SegmentalCheck($"{dd.Name}: count", n, n, 0, $"{string.Join(" / ", dd.SegmentTypes)} segments{(rx != null ? " matching " + dd.SegmentPattern : "")}; right {right}, left {left}"));
    }

    // ------------------------------------------------------------------ expansion joints

    /// <summary>Point in the section frame at a joint face, moved <paramref name="along"/> (horizontal, + up-station) along the alignment tangent.</summary>
    private static Vec3 At(SectionFrame fr, double x, double y, double along)
    {
        double hl = Math.Sqrt(fr.Tangent.X * fr.Tangent.X + fr.Tangent.Y * fr.Tangent.Y);
        return fr.At(new Vec2(x, y)) + fr.Tangent * (along / hl);
    }

    /// <summary>Closed box from 8 corners: x0..x1 across, y0..y1 (section y, down +), a0..a1 along.</summary>
    private static Mesh Box(SectionFrame fr, double x0, double x1, double y0, double y1, double a0, double a1)
    {
        var m = new Mesh();
        var sec = new[] { new Vec2(x0, y0), new Vec2(x1, y0), new Vec2(x1, y1), new Vec2(x0, y1) };
        Vec3[] Ring(double a) => sec.Select(p => At(fr, p.X, p.Y, a)).ToArray();
        MeshBuilder.AddLoft(m, sec, new[] { Ring(Math.Min(a0, a1)), Ring(Math.Max(a0, a1)) });
        return m;
    }

    private void BuildJoint(SegmentalExpansionJointDefinition jd, MiscModel misc)
    {
        string name = jd.Name.Length > 0 ? jd.Name : $"Expansion joint at {jd.Support}";
        string src = jd.Source.Length > 0 ? jd.Source : Pages;
        if (!_bentSta.TryGetValue(jd.Support, out double sSup) || _segs.Count == 0) { Warn($"Segmental {name}: support '{jd.Support}' is not a support of this bridge - not built."); return; }
        foreach (var u in jd.Unresolved) Warn(u);
        // This unit's deck end next to the support = the segment end nearest to it.
        var (r, _, _) = _segs.OrderBy(x => Math.Min(Math.Abs(x.r.Station0 - sSup), Math.Abs(x.r.Station1 - sSup))).First();
        bool atStart = Math.Abs(r.Station0 - sSup) <= Math.Abs(r.Station1 - sSup);
        double sFace = atStart ? r.Station0 : r.Station1;
        int into = atStart ? 1 : -1;                       // + = into the unit along the stations
        var fr = Frame(sFace) with { Origin = atStart ? r.Top0 : r.Top1 };
        var b = _d.Box;
        double half = b.TopWidth / 2;
        Assumption? devFlag = jd.Unresolved.Count > 0 ? Assumption.Unresolved(string.Join(" ", jd.Unresolved), "Envelope of the joint device (by others).") : null;
        misc.Assemblies.Add(new MiscAssembly(MiscGroup.ExpansionJoint, name, $"{jd.Designation} at {jd.Support} ({src})", "expansion joint"));
        // Device envelope: opening centred on the CL joint, from this unit's deck end face across the gap.
        if (double.IsNaN(jd.CentreFromSupport))
            Warn(UnresolvedFlag.Text(src, $"{name}: CL joint position", "the plans do not locate the joint", "No device envelope."));
        else
        {
            double sJ = sSup + jd.CentreFromSupport;
            double toJoint = (sJ - sFace) * into;           // negative = the CL joint lies outside the unit (expected)
            if (toJoint >= 0)
                Warn($"CONFLICT (plan vs model) {name}: the CL joint ({sJ / Ft(1):0.00}) lies inside the unit's deck end ({sFace / Ft(1):0.00}) - device envelope not built.");
            else
            {
                double opening = double.IsNaN(jd.Opening) ? 2 * -toJoint : jd.Opening;
                if (double.IsNaN(jd.Opening))
                    devFlag = Assumption.Unresolved((devFlag != null ? devFlag.Reason + " " : "") + UnresolvedFlag.Text(src, $"{name} opening", "the setting table (DIM \"A\") is not in this plan set",
                        $"Opening = twice the distance deck end - CL joint = {opening / In(1):0.##} in."), "Envelope of the joint device (by others).");
                double depth = double.IsNaN(jd.DeviceDepth) ? jd.BlockoutDepth : jd.DeviceDepth;
                double hw = double.IsNaN(jd.HalfWidth) ? half : jd.HalfWidth;
                if (double.IsNaN(depth)) Warn(UnresolvedFlag.Text(src, $"{name}: device depth", "neither the device nor the blockout depth is given", "No device envelope."));
                else
                {
                    var mesh = Box(fr, -hw, hw, 0, depth, 0, -into * opening);
                    misc.Elements.Add(new MiscElement(MiscGroup.ExpansionJoint, MiscKind.JointDevice, name, $"{name} device",
                        $"{jd.Designation} envelope: opening {opening / In(1):0.##} in at the setting temperature, {2 * hw / Ft(1):0.##} ft wide, {depth / In(1):0.##} in deep, CL joint {Math.Abs(jd.CentreFromSupport) / In(1):0.##} in {(jd.CentreFromSupport < 0 ? "down" : "up")}station of the {jd.Support} line",
                        mesh, "Modular expansion joint (by others)", Placeholder: true, PlaceholderBasis: $"device by others ({src}); envelope = opening x width x depth", Location: jd.Support, Assumption: devFlag));
                    _m.Checks.Add(new SegmentalCheck($"{name}: opening at the setting temperature (model)", opening, double.IsNaN(jd.Opening) ? double.NaN : jd.Opening,
                        double.IsNaN(jd.Opening) ? double.NaN : 0, $"deck end {sFace / Ft(1):0.00}, CL joint {sJ / Ft(1):0.00}"));
                }
            }
        }
        // Blockout in this unit's deck end: the notch is recorded as an opening of the end segment (concrete clipped to the modeled section).
        if (!double.IsNaN(jd.BlockoutLength) && !double.IsNaN(jd.BlockoutDepth))
        {
            double inset = double.IsNaN(jd.BlockoutEdgeInset) ? 0 : jd.BlockoutEdgeInset;
            double x1 = half - inset, mg = In(0.5);
            var mesh = Box(fr, -x1, x1, -mg, jd.BlockoutDepth, -into * mg, into * jd.BlockoutLength);
            var (hSec, _, hollow) = SectionAt(sFace + into * jd.BlockoutLength / 2);
            double removed = 0; int nx = 200;
            for (int i = 0; i < nx; i++)
            {
                double xx = -x1 + (i + 0.5) * 2 * x1 / nx, a = Math.Abs(xx);
                double under = a > b.WebRootX ? WingUnderside(a) : hollow ? b.TopSlab : hSec;
                removed += Math.Min(jd.BlockoutDepth, under) * 2 * x1 / nx * jd.BlockoutLength;
            }
            _m.Openings.Add(new SegmentalOpeningResult
            {
                Name = $"{r.Name} {name} blockout", Kind = SegmentalOpeningKind.TopSlab, Segment = r.Name, Mesh = mesh, RemovedVolume = removed,
                Description = $"Expansion joint blockout {jd.BlockoutLength / In(1):0.##} in from the deck end x {jd.BlockoutDepth / In(1):0.##} in deep, ends {inset / In(1):0.##} in from the deck edges ({src})",
            });
            Info($"Segmental {name}: blockout {removed / Math.Pow(Ft(1), 3) / 27:0.00} CY removed from {r.Name} (the depth is clipped to the modeled wing / top slab).");
        }
        // Parapet end posts (the part above the parapet) and the cover plates on the parapet traffic faces.
        if (jd.EndPost is { } ep)
        {
            if (new[] { ep.Length, ep.Width, ep.InsideFace, ep.ParapetHeight, ep.HeightAboveParapet }.Any(double.IsNaN))
                Warn(UnresolvedFlag.Text(src, $"{name}: parapet end post", "a dimension of the end post is not given", "End posts not built."));
            else
                foreach (int side in new[] { -1, 1 })
                {
                    double xa = side * ep.InsideFace, xb = side * (ep.InsideFace + ep.Width);
                    var mesh = Box(fr, Math.Min(xa, xb), Math.Max(xa, xb), -(ep.ParapetHeight + ep.HeightAboveParapet), -ep.ParapetHeight, 0, into * ep.Length);
                    string sn = side < 0 ? "left" : "right";
                    misc.Elements.Add(new MiscElement(MiscGroup.ExpansionJoint, MiscKind.JointEndPost, name, $"{name} {sn} end post",
                        $"Parapet end post {ep.Length / In(1):0.##} in long x {ep.Width / In(1):0.##} in, {ep.HeightAboveParapet / In(1):0.##} in above the {ep.ParapetHeight / In(1):0.##} in parapet (the parapet below it is the parapet solid) ({src})",
                        mesh, "Class AA concrete", Location: jd.Support));
                }
        }
        if (jd.CoverPlate is { } cp && !double.IsNaN(jd.CentreFromSupport) && jd.EndPost is { } ep2 && !double.IsNaN(ep2.InsideFace))
        {
            if (new[] { cp.Thickness, cp.Height, cp.TowardUnit }.Any(double.IsNaN))
                Warn(UnresolvedFlag.Text(src, $"{name}: parapet cover plates", "a cover plate dimension is not given", "Cover plates not built."));
            else
            {
                double aJ = (sSup + jd.CentreFromSupport - sFace);   // CL joint relative to the face (along, + up-station)
                foreach (int side in new[] { -1, 1 })
                {
                    double len = side < 0 ? cp.LengthLeft : cp.LengthRight;
                    if (double.IsNaN(len)) { Warn(UnresolvedFlag.Text(src, $"{name}: {(side < 0 ? "left" : "right")} cover plate length", "not given", "Plate not built.")); continue; }
                    double xa = side * ep2.InsideFace, xb = side * (ep2.InsideFace + cp.Thickness);
                    double a0 = aJ + into * cp.TowardUnit, a1 = aJ - into * (len - cp.TowardUnit);
                    var mesh = Box(fr, Math.Min(xa, xb), Math.Max(xa, xb), -(cp.Bottom + cp.Height), -cp.Bottom, a0, a1);
                    string sn = side < 0 ? "left" : "right";
                    misc.Elements.Add(new MiscElement(MiscGroup.ExpansionJoint, MiscKind.JointCoverPlate, name, $"{name} {sn} cover plate",
                        $"Cover plate {cp.Thickness / In(1):0.###} in x {cp.Height / In(1):0.##} in x {len / In(1):0.##} in on the {sn} parapet traffic face (recessed flush), {cp.TowardUnit / In(1):0.##} in on this unit's side of the CL joint ({src})",
                        mesh, cp.Material, Location: jd.Support));
                }
            }
        }
    }
}
