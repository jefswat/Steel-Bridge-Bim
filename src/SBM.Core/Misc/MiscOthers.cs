using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
using SBM.Core.Units;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Misc;

public sealed partial class MiscItemsGenerator
{
    private const string PlansSteel = "Structural steel (grade per plans)";

    // ---------------------------------------------------------------- bar lists (schedule only)
    private void AddMarks(string unit, string location, IEnumerable<MiscBarRow> rows, int multiplier = 1)
    {
        foreach (var r in rows)
        {
            var size = RebarCatalog.Size(r.Size);
            double len = r.Length;
            List<double>? varying = r.Lengths is { Count: > 1 } ? r.Lengths.ToList() : null;
            if (len <= 0 && varying == null && r.WeightLb > 0 && r.Count > 0)
            {
                len = r.WeightLb * 0.45359237 / (r.Count * size.MassPerLength);
                Info($"{unit}: bar {r.Mark} length varies (plan prints a range); average length {_u.FormatLength(len, LengthKind.Short)} derived from the printed weight {r.WeightLb:0} lb.");
            }
            if (varying != null) len = varying.Max();
            _m.Marks.Add(new BarMark
            {
                Mark = r.Mark, Size = size, Unit = unit, Location = $"{location} {r.Location}".Trim(), Epoxy = r.Epoxy, Coating = r.Coating, Count = r.Count * multiplier,
                VaryingLengths = varying,
                Shape = new BarShape { Code = "LIST", Description = "Length as listed; bend diagram per plan", Centerline = new[] { Vec2.Zero, new Vec2(len, 0) }, FixedLength = len },
            });
        }
    }

    /// <summary>Anchor bolt with a nut and washer under the plate and a washer and nut on top (standard bolt geometry); local frame at the bolt axis.</summary>
    private static Mesh AnchorBolt(Vec2 xy, double zPlateBottom, double zPlateTop, double dia, double below, double above)
    {
        var m = new Mesh();
        var g = new BoltGeometry(dia);
        var f = new Frame3(xy.WithZ(0), new Vec3(1, 0, 0), new Vec3(0, 1, 0), new Vec3(0, 0, 1));
        double washerT = g.WasherThickness;
        MeshBuilder.AddPrism(m, f, dia / 2, 12, zPlateBottom - below, zPlateTop + above);
        MeshBuilder.AddPrism(m, f, g.WasherOd / 2, 12, zPlateTop, zPlateTop + washerT);
        MeshBuilder.AddPrism(m, f, g.NutAcrossFlats / Math.Sqrt(3), 6, zPlateTop + washerT, zPlateTop + washerT + g.NutHeight);
        MeshBuilder.AddPrism(m, f, g.WasherOd / 2, 12, zPlateBottom - washerT, zPlateBottom);
        MeshBuilder.AddPrism(m, f, g.NutAcrossFlats / Math.Sqrt(3), 6, zPlateBottom - washerT - g.NutHeight, zPlateBottom - washerT);
        return m;
    }

    // ---------------------------------------------------------------- sign supports
    private void BuildSigns()
    {
        foreach (var s in _def.Misc?.SignSupports ?? new())
        {
            try { if (s.Kind == SignSupportKind.OverheadTrussBracket) BuildTrussBrackets(s); else BuildExitBase(s); }
            catch (Exception ex) { Warn($"Sign support {s.Name}: could not be generated ({ex.Message})."); }
        }
    }

    private void BuildTrussBrackets(SignSupportDefinition s)
    {
        int j = SupportIndex(s.Support);
        if (j < 0) { Warn($"{s.Name}: support '{s.Support}' not found."); return; }
        var sl = _f.SupportLine(j);
        int n = 0;
        foreach (var b in s.Brackets)
        {
            n++;
            string asm = $"{s.Name} bracket {n} (girder {(b.Girder < _f.Girders.Count ? _f.Girders[b.Girder].Name : "?")})";
            if (b.Girder < 0 || b.Girder >= _f.Girders.Count || sl.Bearings.ElementAtOrDefault(b.Girder) is not { } br) { Warn($"{asm}: the girder has no bearing at {s.Support}."); continue; }
            var g = _f.Girders[b.Girder];
            var (_, _, sb) = GirderAt(g, br.Station);
            double sPost = sb + b.AheadOfSupport;
            var (web, tan, _) = g.Path.At(sPost);
            var P = web.XY;
            var frame = MiscMesh.Plan(P, tan);                     // x along the girder, y to the left of the girder
            double zTop = b.TopOfBaseElevation, zBot = zTop - b.Height;
            double BL = s.TopPlateLength;
            Asm(MiscGroup.SignSupport, asm, $"Sign truss support bracket at {s.Support} (stub bolted to girder {g.Name}), {Sh(b.Height)} high, top of base {zTop / 0.3048:F2}", "sign support");
            Info($"{asm}: post line {Sh(b.AheadOfSupport)} ahead of CL bearing along the girder, STA {_u.FormatStation(_a.Inverse(P).Station)}; bracket bottom {(zBot - web.Z) / 0.0254:F1}\" from the girder top of web ({web.Z / 0.3048:F2}).");
            Mesh Box(double x0, double x1, double y0, double y1, double z0, double z1) { var m = new Mesh(); MeshBuilder.AddBox(m, frame, x0, x1, y0, y1, z0, z1); return m; }
            double tp = s.TopPlateThickness, bf = s.BottomFlangeThickness, wt = s.WebThickness;
            Add(MiscGroup.SignSupport, MiscKind.SignPlate, asm, asm + " web", $"PL {Sh(wt)} web, {Sh(BL)} x {Sh(b.Height - tp - bf)}", Box(-BL / 2, BL / 2, -wt / 2, wt / 2, zBot + bf, zTop - tp), PlansSteel);
            Add(MiscGroup.SignSupport, MiscKind.SignPlate, asm, asm + " top plate", $"PL {Sh(tp)} x {Sh(s.TopPlateWidth)} x {Sh(BL)} (anchor level; plan length not printed: the {Sh(BL)} footprint dimension is used)",
                Box(-BL / 2, BL / 2, -s.TopPlateWidth / 2, s.TopPlateWidth / 2, zTop - tp, zTop), PlansSteel);
            Add(MiscGroup.SignSupport, MiscKind.SignPlate, asm, asm + " bottom flange plate", $"PL {Sh(bf)} x {Sh(b.BottomFlangeWidth)} x {Sh(BL)}",
                Box(-BL / 2, BL / 2, -b.BottomFlangeWidth / 2, b.BottomFlangeWidth / 2, zBot, zBot + bf), PlansSteel);
            var st = new Mesh();
            foreach (double x in new[] { -s.StiffenerOffset, s.StiffenerOffset })
                foreach (int sg in new[] { -1, 1 })
                    st.Append(Box(x - s.StiffenerThickness / 2, x + s.StiffenerThickness / 2, sg > 0 ? wt / 2 : -wt / 2 - s.StiffenerWidth, sg > 0 ? wt / 2 + s.StiffenerWidth : -wt / 2, zBot + bf, zTop - tp));
            Add(MiscGroup.SignSupport, MiscKind.SignStiffener, asm, asm + " stiffeners", $"4 PL {Sh(s.StiffenerThickness)} x {Sh(s.StiffenerWidth)} stiffeners at {Sh(s.StiffenerOffset)} each side of the post CL (height between the plates)", st, PlansSteel);
            var bolts = new Mesh();
            double r = s.AnchorCircleDiameter / 2;
            for (int i = 0; i < s.AnchorBoltCount; i++)
            {
                double ang = (s.AnchorStartAngleDegrees + 360.0 * i / s.AnchorBoltCount) * Math.PI / 180;
                var c = frame.ToWorld(r * Math.Cos(ang), r * Math.Sin(ang), 0);
                bolts.Append(AnchorBolt(c.XY, zTop - tp, zTop, s.AnchorBoltDiameter, In(2), In(3.5)));
            }
            Add(MiscGroup.SignSupport, MiscKind.SignAnchorBolt, asm, asm + " anchor bolts", $"{s.AnchorBoltCount} anchor bolts {Sh(s.AnchorBoltDiameter)} dia F1554 Gr 105 on a {Sh(s.AnchorCircleDiameter)} dia circle starting {s.AnchorStartAngleDegrees:0.#} deg; nuts, washers; lengths NOT printed (stand-in stick-out)", bolts, "ASTM F1554 Gr 105");
            var bp = MiscMesh.Cylinder(frame.ToWorld(0, 0, zTop), frame.ToWorld(0, 0, zTop + s.BasePlateThickness), s.BasePlateDiameter / 2);
            Add(MiscGroup.SignSupport, MiscKind.SignPlaceholder, asm, asm + " base plate (by others)", $"Base plate {Sh(s.BasePlateDiameter)} dia x {Sh(s.BasePlateThickness)} (not part of this contract)", bp, "By others", true,
                $"Plans: base plate {Sh(s.BasePlateDiameter)} dia x {Sh(s.BasePlateThickness)}, HSS {s.PostOuterDiameter / 0.0254:F3}x{s.PostWall / 0.0254:F3} truss post and the {L(s.TrussSpan)} sign truss are by others; their heights are not in the plans", $"{s.Support}, girder {g.Name}");
        }
        Warn($"{s.Name}: not modeled - the bolted connection to the girder (the bracket web is modeled in the plane of the girder web, so it overlaps the girder web over the height below the top of web; the plans' field-splice-type connection geometry is not resolved), MC18x45.8 flange members, WT5x22.5 diagonal / horizontal bracing, web and flange splice plates and bolts to the girder, 45 deg clips, 3/4\" bar connections (details A-G); anchor bolt lengths are not printed; the HSS post height and the {L(s.TrussSpan)} truss (size, elevation) are not in the plans (placeholder base plate only).");
        if (s.Brackets.Count > 0)
            Info($"{s.Name}: bracket plan geometry follows the plan dimensions 2'-2 x 2'-4; plate lengths along the girder = 2'-4 (modeling convention; details B-E not fully dimensioned in the extract).");
    }

    private void BuildExitBase(SignSupportDefinition s)
    {
        string asm = s.Name;
        AddMarks(asm, "exit sign base", s.Bars);
        Asm(MiscGroup.SignSupport, asm, $"Exit sign support base: {Sh(s.BlockAlong)} x {Sh(s.BlockOut)} x {Sh(s.BlockHeight)} concrete block on an outrigger from girder A", "sign support");
        int j = SupportIndex(s.Support);
        if (j < 0 || s.BackFromBearing is not { } back)
        {
            Warn($"{asm}: exit sign station is UNRESOLVED (sheets 104 / 108 show only a tick on the north girder): block, W6x15 stubs and outrigger bracket NOT placed; bar list scheduled.");
            return;
        }
        var sl = _f.SupportLine(j);
        var g = _f.Girders[s.Girder];
        var br = sl.Bearings.ElementAtOrDefault(s.Girder);
        if (br == null) { Warn($"{asm}: girder has no bearing at {s.Support}."); return; }
        var (_, _, sb) = GirderAt(g, br.Station);
        var (web, tan, _) = g.Path.At(sb - back);
        double sta = _a.Inverse(web.XY).Station;
        bool left = _a.Inverse(web.XY).Offset < 0;
        double edge = left ? _deck.LeftAt(sta) : _deck.RightAt(sta);
        var T = Vec2.FromAngle(_a.AngleAt(sta));
        var outDir = left ? T.Left : T.Right;
        var pe = Pt(sta, edge);
        double zTopDeck = Ztop(sta, edge);
        var m = new Mesh();
        MeshBuilder.AddBox(m, MiscMesh.Plan(pe + outDir * (s.BlockOut / 2), T), -s.BlockAlong / 2, s.BlockAlong / 2, -s.BlockOut / 2, s.BlockOut / 2, zTopDeck, zTopDeck + s.BlockHeight);
        Add(MiscGroup.SignSupport, MiscKind.SignBlock, asm, asm + " block", $"Concrete block {Sh(s.BlockAlong)} x {Sh(s.BlockOut)} x {Sh(s.BlockHeight)} outboard of the barrier (elevation convention: base at the deck edge top)", m, "Class C concrete");
        Warn($"{asm}: block vertical position (outrigger elevation) is not printed: base placed at the deck-edge top; W6x15 stubs and the steel outrigger are not modeled.");
    }

    // ---------------------------------------------------------------- crash cushion end posts
    private void BuildEndPosts()
    {
        foreach (var e in _def.Misc?.EndPosts ?? new())
        {
            string asm = e.Name;
            Asm(MiscGroup.CrashCushion, asm, $"Crash cushion end post at {e.Support}: {Sh(e.Length)} concrete wedge, {Sh(e.HeightRailEnd)} to {Sh(e.HeightAtEnd)} high, {Sh(e.EndWidth)} end", "crash cushion");
            AddMarks(asm, "end post", e.Bars);
            if (e.Station is not { } sta || e.Offset is not { } off || e.DirectionDegrees is not { } dd || e.PierEndWidth is not { } w0)
            {
                Warn($"{asm}: position (distance from the CL pier to the start of the end block), wedge direction and the plan width at the pier end are UNRESOLVED in the plans (Ramp G wedge is skewed 27d27'55\"); wedge, cushion placeholder NOT placed; the bar list is scheduled.");
                continue;
            }
            var p = Pt(sta, off);
            var T = Vec2.FromAngle(_a.AngleAt(sta) + dd * Math.PI / 180);
            double zb = Ztop(sta, off);
            var side = new Vec3(-T.Y, T.X, 0);
            var d3 = new Vec3(T.X, T.Y, 0);
            Vec3[] Ring(double x, double w, double h) => new[]
            {
                p.WithZ(zb) + d3 * x - side * (w / 2), p.WithZ(zb) + d3 * x + side * (w / 2),
                p.WithZ(zb + h) + d3 * x + side * (w / 2), p.WithZ(zb + h) + d3 * x - side * (w / 2),
            };
            var sec = new[] { new Vec2(0, 0), new Vec2(1, 0), new Vec2(1, 1), new Vec2(0, 1) };
            var mesh = new Mesh();
            MeshBuilder.AddLoft(mesh, sec, new[] { Ring(0, w0, e.HeightRailEnd), Ring(e.TaperLength, e.EndWidth, e.HeightAtEnd), Ring(e.Length, e.EndWidth, e.HeightAtEnd) });
            Add(MiscGroup.CrashCushion, MiscKind.EndPostConcrete, asm, asm + " concrete", $"End post {Sh(e.Length)} long: {Sh(e.TaperLength)} taper then {Sh(e.Length - e.TaperLength)} straight; {Sh(e.HeightRailEnd)} to {Sh(e.HeightAtEnd)} high", mesh, "Class C concrete");
            if (e.CushionLength is { } cl)
            {
                var cm = new Mesh();
                MeshBuilder.AddLoft(cm, sec, new[] { Ring(e.Length, e.EndWidth, e.HeightAtEnd), Ring(e.Length + cl, e.EndWidth, e.HeightAtEnd) });
                Add(MiscGroup.CrashCushion, MiscKind.CushionPlaceholder, asm, asm + " crash cushion (by others)", "Permanent severe use crash cushion (Materials I.M. 455)", cm, "By others", true,
                    "Plans give no geometry for the cushion unit; envelope = end width x end height x the given length", e.Support);
            }
            else Warn($"{asm}: crash cushion unit (I.M. 455) has no geometry in the plans: envelope not built.");
        }
    }

    // ---------------------------------------------------------------- lighting
    /// <summary>Traffic-face distance from the barrier back face at a height above the base (barrier profile polygon).</summary>
    private double TrafficFaceAt(double h)
    {
        var poly = BarrierProfiles.For(_deck.Barrier);
        if (poly == null) return _deck.Barrier.BaseWidth;
        // right edge: the polygon's boundary points with x > 0 ordered by height
        double best = 0;
        for (int i = 0; i < poly.Length; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % poly.Length];
            if ((a.Y - h) * (b.Y - h) <= 0 && Math.Abs(b.Y - a.Y) > 1e-9)
            {
                double x = a.X + (b.X - a.X) * (h - a.Y) / (b.Y - a.Y);
                best = Math.Max(best, x);
            }
            else if (Math.Abs(a.Y - h) < 1e-9) best = Math.Max(best, a.X);
        }
        return best;
    }

    private void BuildLighting()
    {
        var lt = _def.Misc?.Lighting;
        if (lt == null) return;
        foreach (var pb in lt.PoleBases) BuildPoleBase(pb);
        foreach (var jb in lt.JunctionBoxes) BuildJunctionBox(jb);
        foreach (var c in lt.Conduits) BuildConduit(c);
        if (lt.PoleBases.Count > 0) Info("Lighting: poles, luminaires, ITS devices and conductors are not part of the contract: not modeled.");
    }

    private (double sta, double edge, Vec2 T, Vec2 outDir) BarrierFrame(string support, double along, LightingSide side, out bool ok, string who)
    {
        ok = false;
        int j = SupportIndex(support);
        if (j < 0) { Warn($"{who}: support '{support}' not found."); return default; }
        double sta = _f.SupportLine(j).Station + along;
        bool left = side == LightingSide.Left;
        if (left ? !_deck.Barrier.Left : !_deck.Barrier.Right) { Warn($"{who}: no barrier on the {(left ? "left" : "right")} side."); return default; }
        var T = Vec2.FromAngle(_a.AngleAt(sta));
        ok = true;
        return (sta, left ? _deck.LeftAt(sta) : _deck.RightAt(sta), T, left ? T.Left : T.Right);
    }

    private void BuildPoleBase(PoleBaseDefinition pb)
    {
        string asm = pb.Name;
        Asm(MiscGroup.Lighting, asm, $"Pole base at {pb.Support}: {Sh(pb.BlockAlong)} x {Sh(pb.BlockOut)} block outboard of the barrier, {pb.BoltCount} anchor bolts on a {Sh(pb.BoltCircleDiameter)} circle", "pole base");
        AddMarks(asm, "pole base", pb.Bars);
        if (pb.AlongFromSupport is not { } along)
        {
            Warn($"{asm}: position is UNRESOLVED (plans: 10'-0 max from the end of the superstructure): not placed; bar list scheduled.");
            return;
        }
        var (sta, edge, T, outDir) = BarrierFrame(pb.Support, along, pb.Side, out bool ok, asm);
        if (!ok) return;
        var pe = Pt(sta, edge);
        double zl = Ztop(sta, edge);
        double zt = zl + pb.TopAboveLedge;
        // block section in (out, z): top flat, soffit an arc of radius R between the printed end heights
        double Y = pb.BlockOut, h1 = pb.HeightAtBarrier, h2 = pb.HeightAtOuterEdge;
        var A = new Vec2(0, -h1); var B = new Vec2(Y, -h2);
        var chord = B - A; double c = chord.Length;
        var mid = Vec2.Lerp(A, B, 0.5);
        var n = chord.Left.Normalized(); if (n.Y < 0) n = -n;     // center above the chord: arc bulges down
        double sag = Math.Sqrt(Math.Max(pb.SoffitRadius * pb.SoffitRadius - c * c / 4, 0));
        var center = mid + n * sag;
        var poly = new List<Vec2> { new(0, 0), new(Y, 0), B };
        // sample from B back to A along the shorter arc below the chord
        double s0 = Math.Atan2(B.Y - center.Y, B.X - center.X), s1 = Math.Atan2(A.Y - center.Y, A.X - center.X);
        double d = s1 - s0; while (d > Math.PI) d -= 2 * Math.PI; while (d < -Math.PI) d += 2 * Math.PI;
        for (int i = 1; i < 8; i++) { double ang = s0 + d * i / 8; poly.Add(center + Vec2.FromAngle(ang) * pb.SoffitRadius); }
        poly.Add(A);
        var pts = poly.Select(q => new Vec2(q.X, zt + q.Y)).ToList();
        pts[0] = new Vec2(0, zt); pts[1] = new Vec2(Y, zt);
        var block = MiscMesh.VerticalSection(pe, outDir, pts, pb.BlockAlong / 2);
        Add(MiscGroup.Lighting, MiscKind.PoleBaseConcrete, asm, asm + " block", $"Pole base block {Sh(pb.BlockAlong)} x {Sh(pb.BlockOut)}, {Sh(h1)} high at the barrier face, {Sh(h2)} at the outer edge (soffit R {Sh(pb.SoffitRadius)}), top {Sh(pb.TopAboveLedge)} above the deck at the barrier", block, "Class C concrete");
        var bolts = new Mesh();
        var cp = pe + outDir * (pb.BlockAlong / 2);
        double r = pb.BoltCircleDiameter / 2;
        for (int i = 0; i < pb.BoltCount; i++)
        {
            double ang = (45 + 360.0 * i / pb.BoltCount) * Math.PI / 180;
            var q = cp + T * (r * Math.Cos(ang)) + outDir * (r * Math.Sin(ang));
            var f = new Frame3(q.WithZ(0), new Vec3(1, 0, 0), new Vec3(0, 1, 0), new Vec3(0, 0, 1));
            var g = new BoltGeometry(pb.BoltDiameter);
            MeshBuilder.AddPrism(bolts, f, pb.BoltDiameter / 2, 12, zt + In(5) - pb.BoltLength, zt + In(5));
            MeshBuilder.AddPrism(bolts, f, g.NutAcrossFlats / Math.Sqrt(3), 6, zt + In(5) - pb.BoltLength, zt + In(5) - pb.BoltLength + g.NutHeight);
        }
        Add(MiscGroup.Lighting, MiscKind.PoleAnchorBolt, asm, asm + " anchor bolts", $"{pb.BoltCount} anchor bolts {Sh(pb.BoltDiameter)} x {Sh(pb.BoltLength)} galvanized on a {Sh(pb.BoltCircleDiameter)} circle, 5\" thread above the top; anchor plate, nuts above the top not modeled", bolts, "Galvanized steel");
        Warn($"{asm}: placement conventions to confirm - bolt circle centred on the block along the barrier and {Sh(pb.BlockAlong / 2)} outboard of the barrier, pattern at 45 deg to the barrier, block top above the deck-edge top; the pole and luminaire are not in the contract.");
    }

    private void BuildJunctionBox(JunctionBoxDefinition jb)
    {
        string asm = jb.Name;
        var (sta, edge, T, outDir) = BarrierFrame(jb.Support, jb.AlongFromSupport, jb.Side, out bool ok, asm);
        if (!ok) return;
        double hc = Ft(1);
        double xf = TrafficFaceAt(hc);
        double inb = jb.Side == LightingSide.Left ? 1 : -1;
        var pFace = Pt(sta, edge + inb * xf);
        double zb = Ztop(sta, edge + inb * xf);
        var inDir = -outDir;
        var m = new Mesh();
        // recessed into the traffic face: occupies [face - depth, face] in the barrier
        MeshBuilder.AddBox(m, MiscMesh.Plan(pFace - outDir * 0, T), -jb.Width / 2, jb.Width / 2, jb.Side == LightingSide.Left ? -jb.Depth : 0, jb.Side == LightingSide.Left ? 0 : jb.Depth, zb + hc - jb.Height / 2, zb + hc + jb.Height / 2);
        string kind = jb.Kind == JunctionBoxKind.Unresolved ? "power or ITS (plans do not say which side)" : jb.Kind == JunctionBoxKind.Power ? "power supply" : "ITS";
        Asm(MiscGroup.Lighting, asm, $"Type 1 junction box ({kind}) in the barrier", "junction box");
        Add(MiscGroup.Lighting, MiscKind.JunctionBox, asm, asm, $"Iowa LI-104 Type 1 flush cast iron junction box, {kind}, {Sh(jb.Depth)} deep, {Sh(jb.Height)} high", m, "Cast iron", true,
            "Plans: depth 5\" and height 6\"; plan size is in standard road plan LI-104 (not in the set); width, height of the centre above the barrier base (1'-0) and stand-in values flagged",
            $"{jb.Support} {(jb.AlongFromSupport >= 0 ? "+" : "-")}{Sh(Math.Abs(jb.AlongFromSupport))}");
        Warn($"{asm}: junction box size is not in the plan set (LI-104): placeholder {Sh(jb.Width)} x {Sh(jb.Height)} x {Sh(jb.Depth)}; centre 1'-0 above the barrier base (\"within the textured lower portion\"; exact height not printed).");
    }

    private void BuildConduit(ConduitRunDefinition c)
    {
        string asm = c.Name;
        bool left = c.Side == LightingSide.Left;
        if (left ? !_deck.Barrier.Left : !_deck.Barrier.Right) { Warn($"{asm}: no barrier on that side."); return; }
        double s0 = Math.Max(c.FromStation, _f.Supports[0].Station), s1 = Math.Min(c.ToStation, _f.Supports[^1].Station);
        if (s1 <= s0) return;
        var (od, wall) = MiscMesh.PipeSch40(c.NominalDiameter);
        double xf = TrafficFaceAt(c.HeightAboveBase) - c.FromTrafficFace;
        double inb = left ? 1 : -1;
        var pts = new List<Vec3>();
        int n = Math.Max(1, (int)Math.Ceiling((s1 - s0) / 3.0));
        for (int i = 0; i <= n; i++)
        {
            double sta = s0 + (s1 - s0) * i / n;
            double edge = left ? _deck.LeftAt(sta) : _deck.RightAt(sta);
            double off = edge + inb * xf;
            pts.Add(Pt(sta, off).WithZ(Ztop(sta, off) + c.HeightAboveBase));
        }
        var m = new Mesh();
        for (int i = 1; i < pts.Count; i++) m.Append(MiscMesh.Tube(pts[i - 1], pts[i], od / 2, od / 2 - wall, 12));
        Asm(MiscGroup.Lighting, asm, $"{Sh(c.NominalDiameter)} rigid steel conduit in the {(left ? "left" : "right")} barrier, {Sh(c.HeightAboveBase)} above the barrier base", "conduit");
        Add(MiscGroup.Lighting, MiscKind.Conduit, asm, asm, $"{Sh(c.NominalDiameter)} rigid steel conduit ({c.Kind}), centre {Sh(c.HeightAboveBase)} above the base and {Sh(c.FromTrafficFace)} from the traffic face; straight chords of the barrier line, STA {_u.FormatStation(s0)} to {_u.FormatStation(s1)}", m, "Galvanized rigid steel conduit");
    }
}
