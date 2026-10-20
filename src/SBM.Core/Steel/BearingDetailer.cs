using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Steel;

/// <summary>
/// Bearings under each girder at each support.
/// Elastomeric: tapered sole plate + steel-reinforced elastomeric pad.
/// Disc: tapered sole plate (welded to the flange) / upper bearing plate (lengthened for expansion; stainless +
/// PTFE sliding surface) / polyether-urethane disc with central shear-restriction pin / masonry (lower bearing) plate with
/// oversized holes / anchor bolts with plate washers and nuts; guide bars for guided expansion.
/// Generic (vendor-designed): tapered sole plate / one bearing body sized to the assumed total height / masonry plate with
/// the anchor bolts as given (spacing, size, embedment) / optional preformed masonry pad.
/// Bearings are plumb and level; the sole plate takes the girder grade.
/// </summary>
public sealed partial class SteelModelGenerator
{
    private void BuildBearing(BearingResult b)
    {
        var g = _f.Girders[b.Girder];
        int nSpan = g.Spans.Count;
        int span = Math.Min(b.Support, nSpan - 1);
        double s = !double.IsNaN(b.PathDistance) ? b.PathDistance : g.DistanceAt(span, b.Support == nSpan ? 1 : 0);
        var (p, t, sta) = g.Path.At(s);
        double bfT = _bot[g.Index].Thickness(s), bfW = _bot[g.Index].Width(s);
        double zbf = p.Z - Sec.WebDepth - bfT;
        double grade = g.Path.GradeAt(s);
        var bd = _def.BearingsAt(b.Support);
        var sup = _def.SupportAt(b.Support);
        var fix = sup.FixityOf(g.Index);

        var tx = new Vec3(t.X, t.Y, 0);
        // Frame: x along girder (plan), y up, z across (right).
        var frame = new Frame3(new Vec3(p.X, p.Y, zbf), tx, Up, Vec3.Cross(tx, Up));
        string supName = _f.SupportLine(b.Support).Name;
        string mark = $"B-{supName.Replace(" ", "")}-{g.Name}";
        string fixName = fix switch
        {
            BearingFixity.Fixed => "fixed",
            BearingFixity.GuidedExpansion => "guided expansion",
            _ => "expansion",
        };
        _assemblies.Add(new SteelAssembly { Mark = mark, Kind = AssemblyKind.Bearing, Girder = g.Index,
            Description = $"{bd.Type} bearing ({fixName}), {supName}, {g.Name}" });

        // Tapered sole plate: top matches the flange grade, bottom level.
        double ls = bd.SolePlateLength, ws = bd.Type switch
        {
            BearingType.Disc => Math.Max(bd.UpperPlateWidth, bfW + 2 * bd.SolePlateProjection),
            BearingType.Generic => Math.Max(bd.GenericBodyWidth, bfW + 2 * bd.SolePlateProjection),
            _ => Math.Max(bd.PadWidth, bfW) + 2 * bd.SolePlateProjection,
        };
        double zb = -Math.Abs(grade) * ls / 2 - bd.SolePlateMinThickness;
        double tmax = bd.SolePlateMinThickness + Math.Abs(grade) * ls;
        var sole = new Mesh();
        MeshBuilder.AddExtrusion(sole, new[]
        {
            new Vec2(-ls / 2, zb), new Vec2(ls / 2, zb), new Vec2(ls / 2, grade * ls / 2), new Vec2(-ls / 2, -grade * ls / 2),
        }, frame, -ws / 2, ws / 2);
        _parts.Add(new SteelPart { Mark = $"{mark}-SP", Category = PartCategory.SolePlate, Assembly = mark, Mesh = sole, Length = ls,
            Description = $"Sole {Pl(ws, tmax)} x {In(ls)} (tapered, min {In(bd.SolePlateMinThickness)})" });

        // Field weld, sole plate to bottom flange (both sides along the girder).
        var sw = new Mesh();
        double a = _def.Stiffeners.WeldSize;
        double half = Math.Min(ws, bfW) / 2;
        double spLen = 0;
        foreach (int side in new[] { 1, -1 })                   // stops short of the sole plate ends
            spLen += sw.AddHeld(frame.ToWorld(-ls / 2, -grade * ls / 2, side * half), frame.ToWorld(ls / 2, grade * ls / 2, side * half), -Up, frame.Z * side, a, HoldBack(a), HoldBack(a));
        _parts.Add(new SteelPart { Mark = $"{mark}-SPW", Category = PartCategory.Weld, Assembly = mark, Mesh = sw, Length = spLen, Material = "E70XX",
            Description = $"Fillet weld {In(a)}, sole plate to flange (field){HoldNote(a)}", Assumption = WeldFlag(a) });

        double seat;
        if (bd.Type == BearingType.Elastomeric)
        {
            var pad = new Mesh();
            MeshBuilder.AddBox(pad, frame, -bd.PadLength / 2, bd.PadLength / 2, zb - bd.PadHeight, zb, -bd.PadWidth / 2, bd.PadWidth / 2);
            _parts.Add(new SteelPart { Mark = $"{mark}-PAD", Category = PartCategory.BearingPad, Assembly = mark, Mesh = pad, Length = bd.PadLength,
                Material = "Steel-reinforced elastomer (AASHTO M 251)",
                Description = $"Elastomeric pad {In(bd.PadLength)} x {In(bd.PadWidth)} x {In(bd.PadHeight)}" });
            seat = zbf + zb - bd.PadHeight;
        }
        else if (bd.Type == BearingType.Generic)
        {
            // Vendor-designed bearing: one body from the sole plate down to the masonry plate, height so the assembly is the
            // assumed total height at the CL bearing.
            double hb = bd.GenericBodyHeight(grade);
            if (hb < Inch(1))
                Warn($"{mark}: sole plate + masonry plate + pad leave {In(hb)} for the bearing within the assumed height {In(bd.GenericHeight)}.");
            hb = Math.Max(hb, 0);
            var body = new Mesh();
            MeshBuilder.AddBox(body, frame, -bd.GenericBodyLength / 2, bd.GenericBodyLength / 2, zb - hb, zb, -bd.GenericBodyWidth / 2, bd.GenericBodyWidth / 2);
            _parts.Add(new SteelPart { Mark = $"{mark}-BRG", Category = PartCategory.BearingDisc, Assembly = mark, Mesh = body, Length = bd.GenericBodyLength,
                Material = "By bearing manufacturer",
                Description = $"Bearing ({fixName}, by manufacturer), assumed height {In(bd.GenericHeight)}" });
            seat = zbf + BuildMasonry(bd, mark, frame, zb - hb, fix, 0);
        }
        else seat = zbf + BuildDisc(bd, mark, frame, zb, fix);

        var (fpL, fpW) = bd.Footprint(fix);
        _seats.Add(new BearingSeat(supName, g.Index, b.Station, seat, tmax, bd.Type.ToString(), fixName, FootprintLength: fpL, FootprintWidth: fpW));
    }

    /// <summary>Builds the disc bearing below the sole plate (frame y = up, origin at the bottom of flange). Returns seat z.</summary>
    private double BuildDisc(BearingDefinition bd, string mark, Frame3 frame, double zTop, BearingFixity fix)
    {
        bool expansion = fix != BearingFixity.Fixed;
        double z = zTop;

        // Upper bearing plate (with stainless sliding surface for expansion), lengthened for movement.
        double lu = bd.UpperPlateLength + (expansion ? bd.ExpansionPlateExtension : 0), wu = bd.UpperPlateWidth;
        var up = new Mesh();
        MeshBuilder.AddBox(up, frame, -lu / 2, lu / 2, z - bd.UpperPlateThickness, z, -wu / 2, wu / 2);
        _parts.Add(new SteelPart { Mark = $"{mark}-UBP", Category = PartCategory.BearingPlate, Assembly = mark, Mesh = up, Length = lu,
            Description = $"Upper bearing {Pl(wu, bd.UpperPlateThickness)} x {In(lu)}" + (expansion ? " w/ stainless sliding surface" : "") });
        z -= bd.UpperPlateThickness;

        var disc = Frame3.FromXZ(frame.ToWorld(0, 0, 0), frame.X, frame.Y);
        Frame3 At(double zz) => disc with { Origin = frame.ToWorld(0, zz, 0) };
        if (expansion)
        {
            var ptfe = new Mesh();
            MeshBuilder.AddPrism(ptfe, At(0), bd.DiscDiameter / 2, 32, z - bd.PtfeThickness, z);
            _parts.Add(new SteelPart { Mark = $"{mark}-PTFE", Category = PartCategory.Ptfe, Assembly = mark, Mesh = ptfe, Length = bd.DiscDiameter,
                Material = "PTFE (recessed, lubricated)", Description = $"PTFE sliding disc {In(bd.DiscDiameter)} dia x {In(bd.PtfeThickness)}" });
            z -= bd.PtfeThickness;
        }

        // Polyether urethane disc and shear-restriction pin.
        var dm = new Mesh();
        MeshBuilder.AddPrism(dm, At(0), bd.DiscDiameter / 2, 48, z - bd.DiscHeight, z);
        _parts.Add(new SteelPart { Mark = $"{mark}-DISC", Category = PartCategory.BearingDisc, Assembly = mark, Mesh = dm, Length = bd.DiscDiameter,
            Material = "Polyether urethane", Description = $"Urethane disc {In(bd.DiscDiameter)} dia x {In(bd.DiscHeight)}" });
        var pin = new Mesh();
        // The pin passes through the disc's central hole into both bearing plates (shown as the exposed shank).
        MeshBuilder.AddPrism(pin, At(0), bd.ShearPinDiameter / 2, 24, z - bd.DiscHeight - Inch(0.01), z + Inch(0.01));
        _parts.Add(new SteelPart { Mark = $"{mark}-PIN", Category = PartCategory.ShearPin, Assembly = mark, Mesh = pin, Length = bd.DiscHeight,
            Description = $"Shear restriction pin {In(bd.ShearPinDiameter)} dia" });
        z -= bd.DiscHeight;

        double zSeat = BuildMasonry(bd, mark, frame, z, fix,
            fix == BearingFixity.GuidedExpansion ? wu / 2 + Inch(0.0625) + bd.GuideBarWidth + bd.AnchorBoltDiameter : 0);
        var (lm, _) = bd.MasonryPlate(fix);

        // Guide bars for guided expansion (flank the upper plate, longitudinal movement only).
        if (fix == BearingFixity.GuidedExpansion)
        {
            double gap = Inch(0.0625), bw = bd.GuideBarWidth;
            double topZ = zTop - bd.UpperPlateThickness / 2;
            for (int side = -1; side <= 1; side += 2)
            {
                var gb = new Mesh();
                double y0 = side * (wu / 2 + gap), y1 = side * (wu / 2 + gap + bw);
                MeshBuilder.AddBox(gb, frame, -(lm / 2 - Inch(1)), lm / 2 - Inch(1), z, topZ, Math.Min(y0, y1), Math.Max(y0, y1));
                _parts.Add(new SteelPart { Mark = $"{mark}-GB{(side < 0 ? 1 : 2)}", Category = PartCategory.GuideBar, Assembly = mark, Mesh = gb,
                    Length = lm - Inch(2), Description = $"Guide bar {In(bw)} x {In(topZ - z)}" });
            }
        }
        return zSeat;
    }

    /// <summary>
    /// Masonry (lower bearing) plate with oversized anchor bolt holes, the optional preformed masonry pad below it and the
    /// anchor bolts; <paramref name="z"/> = top of the masonry plate. Anchor bolts closer to the bearing CL (across) than
    /// <paramref name="clearAcross"/> are a warning (guide bars). Returns the seat z (bottom of the pad).
    /// </summary>
    private double BuildMasonry(BearingDefinition bd, string mark, Frame3 frame, double z, BearingFixity fix, double clearAcross)
    {
        var (lm, wm) = bd.MasonryPlate(fix);
        double tm = bd.MasonryPlateThickness, e = bd.EdgeDistance(fix);
        var anchors = new List<Vec2>();
        int n = Math.Max(2, bd.AnchorBoltCount);
        if (n == 2) { anchors.Add(new Vec2(0, -(wm / 2 - e))); anchors.Add(new Vec2(0, wm / 2 - e)); }
        else
        {
            int perSide = n / 2;
            for (int i = 0; i < perSide; i++)
            {
                double x = perSide == 1 ? 0 : -(lm / 2 - e) + i * (lm - 2 * e) / (perSide - 1);
                anchors.Add(new Vec2(x, -(wm / 2 - e)));
                anchors.Add(new Vec2(x, wm / 2 - e));
            }
        }
        var mp = new Mesh();
        // Plate outline in (x along girder, across); thickness vertical.
        var mFrame = new Frame3(frame.ToWorld(0, z, 0), frame.X, frame.Z * -1, frame.Y); // x, y = left, z = up
        PlateBuilder.AddPlanar(mp, new[] { new Vec2(-lm / 2, -wm / 2), new Vec2(lm / 2, -wm / 2), new Vec2(lm / 2, wm / 2), new Vec2(-lm / 2, wm / 2) },
            anchors.Select(q => new Hole(q, bd.AnchorBoltDiameter + bd.AnchorHoleOversize)).ToList(), mFrame, -tm, 0);
        _parts.Add(new SteelPart { Mark = $"{mark}-MP", Category = PartCategory.MasonryPlate, Assembly = mark, Mesh = mp, Length = lm,
            Description = $"Masonry {Pl(wm, tm)} x {In(lm)}, {anchors.Count} holes {In(bd.AnchorBoltDiameter + bd.AnchorHoleOversize)}" });
        if (clearAcross > 0 && wm / 2 - e < clearAcross)
            Warn($"{mark}: guide bars crowd the anchor bolts; widen the masonry plate.");

        double tp = bd.MasonryPadThickness;
        if (tp > 0)
        {
            var pad = new Mesh();
            MeshBuilder.AddBox(pad, frame, -lm / 2, lm / 2, z - tm - tp, z - tm, -wm / 2, wm / 2);
            _parts.Add(new SteelPart { Mark = $"{mark}-MPAD", Category = PartCategory.BearingPad, Assembly = mark, Mesh = pad, Length = lm,
                Material = "Preformed fabric pad", Description = $"Preformed masonry pad {In(tp)}" });
        }

        foreach (var q in anchors)
        {
            var o = mFrame.ToWorld(q.X, q.Y, 0);
            _bolts.Add(new BoltInstance
            {
                Frame = new Frame3(o, frame.X, Vec3.Cross(Up, frame.X), Up), Grip = tm + tp, Length = bd.AnchorBoltEmbedment + tm + tp, Assembly = mark,
                Kind = BoltKind.Anchor, Diameter = bd.AnchorBoltDiameter, Embedment = bd.AnchorBoltEmbedment,
            });
        }
        return z - tm - tp;
    }
}
