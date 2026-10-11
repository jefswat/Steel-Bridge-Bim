using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Substructure;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Misc;

public sealed partial class MiscItemsGenerator
{
    private void BuildPierDrains()
    {
        foreach (var pd in _def.Misc?.PierDrains ?? new())
        {
            try { BuildPierDrain(pd); }
            catch (Exception ex) { Warn($"Pier drain {pd.Support}: could not be generated ({ex.Message})."); }
        }
    }

    /// <summary>Plan parameter t along the line (q + dir t, dir left to right) at which the line meets the gutter line of the given side.</summary>
    private double GutterCrossing(Vec2 q, Vec2 dir, bool right, double gfe)
    {
        double F(double t)
        {
            var so = _a.Inverse(q + dir * t);
            double target = right ? _deck.RightAt(so.Station) - gfe : _deck.LeftAt(so.Station) + gfe;
            return so.Offset - target;
        }
        double lo = -150, hi = 150;
        double flo = F(lo), fhi = F(hi);
        if (flo * fhi > 0) return double.NaN;
        for (int i = 0; i < 60; i++)
        {
            double mid = (lo + hi) / 2, fm = F(mid);
            if ((fm > 0) == (fhi > 0)) { hi = mid; fhi = fm; } else { lo = mid; flo = fm; }
        }
        return (lo + hi) / 2;
    }

    /// <summary>Flags the pier-system parts whose position / size the plans do not dimension (Ramp G PDF 154-158).</summary>
    private void FlagPier(string asm, string standoff)
    {
        Flag(asm + " neoprene trough", "U-section width and sag of the trough are not dimensioned (Ramp G PDF 154 sections A-A / C-C, PDF 156 section B-B).", "strip as wide as the joint blockout, from the deck underside at the high end to the box top");
        Flag(asm + " trough collection box", "Position along the pier is printed to the box (3'-6 / 4'-0 from the roadway edge, PDF 154) without saying to which point of the box; the L8x8x1/2 hanger to the cross frame top chord (PDF 155 sections E-E / H-H) is not modeled.", "centre of the box at the printed distance");
        Flag(asm + " pier collection box", "The box top is printed as a range, 1/4\" min / 1\" max below the top of cap (PDF 157 box elevation; PDF 155 section A-A shows 1\" at CL box), not one value. Face, 9\" inlet and 1 1/2\" outlet clearance are printed (PDF 155 / 157).", "top 1/4\" below the cap top (the minimum)");
        Flag(asm + " pipe 1", "Only the 1:12 minimum slope and the turn-down 9\" (at 50 F) off the cap face are printed (PDF 155 sections A-A / C-C); the plan route from the trough box to the turn-down is not dimensioned (PDF 154 partial plan at 19G: 'orientation to clear inspection walkway and cross frame').", "straight in plan from the trough box to the turn-down, exactly 1:12 from the box low side");
        Flag(asm + " pier pipe", "PDF 157 drain pipe elevations show the pipe leaving the box bottom, a cleanout bend to 8% min along the cap face toward the CL pier, and a cleanout bend down the column; the level of the first bend and the stand-off of the vertical run from the column face are not dimensioned.",
            $"bend directly under the box (pipe crown at the box bottom), exactly 8%, the whole run in the vertical plane of the box outlet 1 1/2\" clear of the cap face (only the two printed bends, no offset), so the vertical run is {standoff}");
        Flag(asm + " pipe supports", "Support positions follow the pipe route, which is not dimensioned (PDF 157; spacing 6'-0 / 4'-0 max is printed).", "first clamp 1'-0 below the bend, then at the maximum spacing");
        Flag(asm + " outlet leg", "Direction of the 45 deg outlet leg is not printed (PDF 157 outlet detail).", "away from the pier on the box side");
        Flag(asm + " erosion stone mattress", "Plan position of the mattress under the outlet follows the unresolved outlet leg direction (PDF 157).", "centred under the outlet end");
    }

    private void BuildPierDrain(PierDrainDefinition pd)
    {
        string asm = $"Pier drain {pd.Support}";
        int j = SupportIndex(pd.Support);
        if (j < 0) { Warn($"{asm}: support not found in this bridge."); return; }
        var sd = _def.SupportAt(j);
        var sl = _f.SupportLine(j);
        var ej = sd.ExpansionJoint;
        bool atEnd = j == _def.Supports.Count - 1, atBegin = j == 0;
        if (ej == null || _f.Deck == null || (!atEnd && !atBegin)) { Warn($"{asm}: needs an expansion joint at the first / last support of this bridge; not built."); return; }
        var unit = _sub.Units.FirstOrDefault(u => u.SupportIndex == j);
        if (unit == null) { Warn($"{asm}: the substructure unit of this support is not generated here (shared pier owned by another bridge?); not built."); return; }

        Vec2 q = atEnd ? _f.Deck.EndLinePoint : _f.Deck.BeginLinePoint;
        Vec2 dir = (atEnd ? _f.Deck.EndLineDirection : _f.Deck.BeginLineDirection).Normalized();
        var tan = Vec2.FromAngle(_a.AngleAt(sl.Station));
        var ahead = dir.Left; if (Vec2.Dot(ahead, tan) < 0) ahead = -ahead;
        q += ahead * (ej.GapAt50F / 2) * (atEnd ? 1 : -1);          // CL joint = CL pier line of the plans
        double? gfeN = _deck.EdgeStrip?.Width;
        if (gfeN == null) { Warn($"{asm}: no deck edge strip: the gutter line is not defined; not built."); return; }
        double gfe = gfeN.Value;
        double tR = GutterCrossing(q, dir, true, gfe), tL = GutterCrossing(q, dir, false, gfe);
        if (double.IsNaN(tR) || double.IsNaN(tL)) { Warn($"{asm}: the joint line does not cross both gutter lines."); return; }
        Asm(MiscGroup.Drainage, asm, $"Deck joint collection system at {pd.Support}: trough, trough collection box, pipes, pier collection box", "pier drain");
        double span = Math.Abs(tR - tL);
        double tFar = pd.CollectRight ? tL : tR;
        double tBox = pd.CollectRight ? tR - pd.BoxFromGutter : tL + pd.BoxFromGutter;
        Vec2 pFar = q + dir * tFar, pBox = q + dir * tBox;
        double Soffit(Vec2 p) { var so = _a.Inverse(p); return Ztop(so.Station, so.Offset) - _deck.Thickness; }
        double zHi = Soffit(pFar);
        double run = Math.Abs(tBox - tFar);
        double soffitBox = Soffit(pBox);
        // trough collection box top: the printed 1'-8 is measured from the deck underside to the top of the box at the collection end (PDF 154 elevations)
        double zBox;
        if (pd.PrintedTroughDrop is { } pdrop)
        {
            zBox = soffitBox - pdrop;
            double slope = (zHi - zBox) / Math.Max(run, 1e-9);
            if (slope + 1e-4 < pd.TroughSlope) Warn($"{asm}: the trough from the deck underside at its high end to the box top ({Sh(pdrop)} below the deck underside at the box) falls only {slope * 100:F1}%, less than the printed {pd.TroughSlope * 100:F1}% minimum.");
            else Info($"{asm}: trough collection box top {Sh(pdrop)} below the deck underside (PDF 154); the trough falls {Sh(zHi - zBox)} over {L(run)} = {slope * 100:F1}% (printed {pd.TroughSlope * 100:F1}% min; the deck underside itself falls {Sh(zHi - soffitBox)}).");
        }
        else
        {
            zBox = zHi - pd.TroughSlope * run;
            Warn($"{asm}: no printed box depth below the deck: trough modeled at exactly {pd.TroughSlope * 100:F1}% from the deck underside at the high end.");
        }
        double drop = zHi - zBox;
        Info($"{asm}: roadway between gutter lines along the pier line {L(span)}; trough box {Sh(pd.BoxFromGutter)} inboard of the {(pd.CollectRight ? "right" : "left")} gutter line.");

        // trough: sloped strip along the joint, from the deck underside at the high end to the box top (hung from the deck by inserts), width = blockout width (not dimensioned)
        double tw = ej.BlockoutWidth > 0 ? ej.BlockoutWidth : Ft(1);
        var trough = MiscMesh.Strip(pFar.WithZ(zHi), pBox.WithZ(zBox), new Vec3(ahead.X, ahead.Y, 0), tw, In(0.25));
        Add(MiscGroup.Drainage, MiscKind.DrainTrough, asm, asm + " neoprene trough", $"1/4\" reinforced neoprene trough under the joint, {L(run)} long, falling {Sh(drop)} ({drop / Math.Max(run, 1e-9) * 100:F1}%, {pd.TroughSlope * 100:F1}% min printed) to the collection box",
            trough, "Reinforced neoprene", true, $"Plans give slope and bolting only: the U section (width, sag) is not dimensioned; width modeled = joint blockout {Sh(tw)}; modeled from the deck underside at the high end to the box top");

        // trough collection box (PDF 156): the 2'-2 faces carry the R 1'-1 1/2 cut for the trough, so the 2'-2 side runs across the joint (along the bridge,
        // PDF 155 section A-A shows it centred on CL pier); 1'-4 deep at the high end, bottom falling 3" to the low end, which faces the pier collection box and carries the 8" pipe
        Vec2 lowDir = pd.CapBoxFace == CapFaceSide.Back ? -ahead : ahead;
        double H = pd.BoxHighEndHeight, sl3 = pd.BoxBottomSlope;
        var boxMesh = MiscMesh.VerticalSection(pBox, lowDir, new[] { new Vec2(-pd.BoxLength / 2, zBox), new Vec2(pd.BoxLength / 2, zBox), new Vec2(pd.BoxLength / 2, zBox - H - sl3), new Vec2(-pd.BoxLength / 2, zBox - H) }, pd.BoxWidth / 2);
        Add(MiscGroup.Drainage, MiscKind.DrainBox, asm, asm + " trough collection box", $"Trough collection box, 3/8\" plate, {Sh(pd.BoxLength)} across the joint x {Sh(pd.BoxWidth)} along it, {Sh(H)} deep at the high end, {Sh(H + sl3)} at the low end; top {Sh(soffitBox - zBox)} below the deck underside; solid envelope",
            boxMesh, "ASTM A36 3/8 plate, galvanized");

        // pier collection box: fastened to the cap FACE (PDF 155 sections A-A / C-C, PDF 157 box details), S from the column CL
        var col = unit.Columns.OrderBy(c => Math.Abs(c.S)).FirstOrDefault();
        if (col == null) { Warn($"{asm}: the pier has no column; pier collection box and pipes not built."); return; }
        if (unit.Columns.Count != 1) Warn($"{asm}: {unit.Columns.Count} columns; the 'CL pier / CL column' distance is taken from the column nearest the support line.");
        double side = pd.CapBoxFace == CapFaceSide.Back ? -1 : 1;
        double nFace = side < 0 ? unit.CapN0 : unit.CapN1;
        Vec2 outw = unit.N * side;                                       // away from the cap, normal to the face
        string faceName = side < 0 ? "back (down-station)" : "ahead (up-station)";
        double sCb = col.S + pd.CapBoxFromColumn;
        double capTop = unit.CapTopAt(sCb, nFace);
        if (double.IsNaN(capTop)) { Warn($"{asm}: no cap top at S = {L(sCb)}."); return; }
        if (sCb < unit.CapS0 || sCb > unit.CapS1) Warn($"{asm}: the cap collection box (S = {L(sCb)}) is off the cap (S {L(unit.CapS0)} to {L(unit.CapS1)}).");
        if (pd.CapBoxBelowTop == null) Warn($"{asm}: " + UnresolvedFlag.Text("Ramp G PDF 157", "pier collection box top below the cap top", "it is printed as a range '1/4\" min / 1\" max', not one value", "1/4\" (the minimum) modeled."));
        double boxTop = capTop - (pd.CapBoxBelowTop ?? In(0.25));
        double zBoxBot = boxTop - pd.CapBoxDepth;
        var pFace = unit.Plan(sCb, nFace);
        var pIn = pFace + outw * pd.CapBoxInletFromFace;                 // CL of the pipe dropping into the box
        var (odc, wc) = MiscMesh.PipeSch40(pd.CapPipeNominalDiameter);
        double rc = odc / 2;
        var pOut = pFace + outw * (pd.CapBoxOutletClear + rc);           // CL of the outlet pipe at the box bottom
        double hTaper = Math.Min(Ft(1), pd.CapBoxDepth);
        var capBoxMesh = MiscMesh.VerticalSection(pFace, outw, new[] { new Vec2(0, boxTop), new Vec2(pd.CapBoxOutTop, boxTop), new Vec2(pd.CapBoxOutTop, zBoxBot + hTaper), new Vec2(pd.CapBoxOutBottom, zBoxBot), new Vec2(0, zBoxBot) }, pd.CapBoxAlongFace / 2);
        Add(MiscGroup.Drainage, MiscKind.DrainBox, asm, asm + " pier collection box", $"Pier collection box on the {faceName} face of the cap, 3/8\" plate, {Sh(pd.CapBoxAlongFace)} along the face x {Sh(pd.CapBoxOutTop)} ({Sh(pd.CapBoxOutBottom)} at the bottom) out from the face x {Sh(pd.CapBoxDepth)} deep, {Sh(Math.Abs(pd.CapBoxFromColumn))} from CL column, strapped to the face with 1/2\" stud anchors; solid envelope",
            capBoxMesh, "ASTM A36 3/8 plate, galvanized");
        Info($"{asm}: pier collection box on the {faceName} cap face at S = {L(sCb)} (cap face N = {L(nFace)}), top {L(boxTop)}; inlet CL {Sh(pd.CapBoxInletFromFace)} and outlet CL {Sh(pd.CapBoxOutletClear + rc)} off the face (PDF 155 / 157).");

        // collector end (Pier 19G): the span collector turns down into the box (PDF 155 section C-C)
        bool collectorFeeds = false;
        double zJoin = double.NaN;                                       // CL of the span collector at its end (19G)
        double zIn = boxTop - pd.CapBoxInletEmbed;
        if (pd.Collector != "" && _collectors.TryGetValue(pd.Collector, out var coll))
        {
            collectorFeeds = true;
            var e = coll.Pts[^1];
            zJoin = e.Z;
            double dd = (pIn - e.XY).Length;
            if (dd > In(1)) Warn($"{asm}: the span collector ends {Sh(dd)} (plan) from the box inlet axis {Sh(pd.CapBoxInletFromFace)} off the cap face: the printed box position ({Sh(Math.Abs(pd.CapBoxFromColumn))} from CL column) and the collector line ({coll.Name}) do not meet; the elbow is modeled straight down at the pipe end (PDF 154 partial plan shows the collector ending over the box).");
            else Info($"{asm}: the span collector ends over the box inlet ({Sh(dd)} in plan).");
            var em = new Mesh();
            if (e.Z > zIn) em.Append(MiscMesh.Tube(e, e.XY.WithZ(zIn), coll.Od / 2, coll.Od / 2 - coll.Wall));
            Add(MiscGroup.Drainage, MiscKind.DrainPipe, asm, asm + " collector end", $"{coll.Name} elbow down into the pier collection box", em, "ASTM A53 Gr B, galvanized");
            if (e.Z > zIn) Record(asm, "collector end", e, e.XY.WithZ(zIn), coll.Od / 2, sl.Station);
        }

        // pipe 1: trough box low side, 1:12 to the turn-down 9" off the cap face, then vertical into the box (or into the collector drop at 19G)
        var (od1, w1) = MiscMesh.PipeSch40(pd.PipeNominalDiameter);
        var p0 = pBox + lowDir * (pd.BoxLength / 2);                    // low end wall of the trough box
        double z0 = zBox - H - sl3 + In(1.5);
        double runP = (pIn - p0).Length;
        var dSN = pIn - p0;
        Info($"{asm}: pipe 1 plan run {L(runP)} (along the pier line {L(Vec2.Dot(dSN, dir))}, along the bridge {L(Vec2.Dot(dSN, ahead))}) from the trough box to the turn-down {Sh(pd.CapBoxInletFromFace)} off the cap face.");
        double z1 = z0 - runP * pd.Pipe1Slope;
        var pipe1 = new Mesh();
        pipe1.Append(MiscMesh.Tube(p0.WithZ(z0), pIn.WithZ(z1), od1 / 2, od1 / 2 - w1));
        double zLegEnd = collectorFeeds ? zJoin : zIn;                   // into the box, or down to the collector elbow it joins (PDF 155 section C-C)
        bool leg = z1 > zLegEnd;
        if (leg) pipe1.Append(MiscMesh.Tube(pIn.WithZ(z1), pIn.WithZ(zLegEnd), od1 / 2, od1 / 2 - w1));
        else Warn($"{asm}: pipe 1 reaches the turn-down at {L(z1)}, below the {(collectorFeeds ? "span collector" : "box inlet")} level {L(zLegEnd)} (run {L(runP)} at 1:{1 / pd.Pipe1Slope:F0}): the plan's vertical relationships do not close with the modeled trough level.");
        Add(MiscGroup.Drainage, MiscKind.DrainPipe, asm, asm + " pipe 1", collectorFeeds
                ? $"{Sh(pd.PipeNominalDiameter)} Sch 40 pipe from the trough collection box, 1:{1 / pd.Pipe1Slope:F0} ({L(runP)}), then down into the end elbow of the span collector over the pier collection box"
                : $"{Sh(pd.PipeNominalDiameter)} Sch 40 pipe from the trough collection box, 1:{1 / pd.Pipe1Slope:F0} ({L(runP)}), turning down {Sh(pd.CapBoxInletFromFace)} off the cap face into the pier collection box",
            pipe1, "ASTM A53 Gr B, galvanized");
        Record(asm, "pipe 1", p0.WithZ(z0), pIn.WithZ(z1), od1 / 2, sl.Station);
        if (leg) Record(asm, "pipe 1 vertical leg", pIn.WithZ(z1), pIn.WithZ(zLegEnd), od1 / 2, sl.Station);

        // pier pipe: box bottom -> bend under the box -> 8% along the cap face toward the CL pier -> vertical down the column, all in the plane of the box outlet
        double nPipe = nFace + side * (pd.CapBoxOutletClear + rc);
        double zBend = zBoxBot - rc;
        var pCol = unit.Plan(col.S, nPipe);
        double runC = (pCol - pOut).Length;
        double zC = zBend - runC * pd.CapPipeSlope;
        double colFace = col.N + side * col.SizeN / 2;
        double gap = side * (nPipe - colFace) - rc;                     // clear from the column face to the pipe
        string standoff = gap >= 0 ? $"{Sh(gap)} clear of the {faceName} column face" : $"{Sh(-gap)} INTO the column";
        if (gap < 0) Warn($"{asm}: CONFLICT - the vertical pier pipe in the plane of the box outlet overlaps the column by {Sh(-gap)} (column face N = {L(colFace)}).");
        else Info($"{asm}: pier pipe runs {Sh(pd.CapBoxOutletClear)} clear of the cap face; the vertical run is {standoff} (cap face {Sh(side * (nFace - colFace))} proud of the column face).");
        double? pav = pd.PavementElevation;
        double? gz = pav is { } pv ? pv : _ground?.Invoke(pCol);
        var pts = new List<Vec3> { pOut.WithZ(zBoxBot), pOut.WithZ(zBend), pCol.WithZ(zC) };
        double zD = double.NaN;
        Vec2 pE = pCol;
        double zE = double.NaN;
        if (gz is not { } g0) Warn($"{asm}: no terrain or pavement elevation at the pier: the vertical pipe is not extended to the ground.");
        else if (pd.Outlet == PierDrainOutlet.Pavement) zD = g0 + In(1);
        else zD = g0 + pd.OutletAboveMattress;
        if (!double.IsNaN(zD))
        {
            if (zD >= zC) Warn($"{asm}: ground / pavement at the pipe ({L(zD)}) is above the pipe where it reaches the CL pier ({L(zC)}).");
            else pts.Add(pCol.WithZ(zD));
        }
        var cp = new Mesh();
        for (int i = 1; i < pts.Count; i++) cp.Append(MiscMesh.Tube(pts[i - 1], pts[i], rc, rc - wc));
        Add(MiscGroup.Drainage, MiscKind.DrainPipe, asm, asm + " pier pipe", $"{Sh(pd.CapPipeNominalDiameter)} Sch 40 pipe from the bottom of the pier collection box, {pd.CapPipeSlope * 100:F0}% along the {faceName} cap face toward the CL column ({L(runC)}), then vertical down the column ({standoff}); outside the concrete, {Sh(pd.CapBoxOutletClear)} clear of the cap face",
            cp, "ASTM A53 Gr B, galvanized");
        for (int i = 1; i < pts.Count; i++) Record(asm, i == 3 ? "pier pipe vertical" : "pier pipe", pts[i - 1], pts[i], rc, sl.Station);
        // clamps on the vertical run
        if (pts.Count == 4)
        {
            var cm = new Mesh();
            int n = 0;
            for (double z = zC - In(12); z > zD; z -= pd.SupportSpacing)
            {
                cm.Append(MiscMesh.Tube(pCol.WithZ(z - In(1)), pCol.WithZ(z + In(1)), rc + In(0.5), rc)); n++;
            }
            Add(MiscGroup.Drainage, MiscKind.PipeSupport, asm, asm + " pipe supports", $"{n} pipe supports (steel clamps on 3/4\" rods into concrete inserts), {Sh(pd.SupportSpacing)} max; rods and inserts not modeled", cm, "Galvanized steel");
        }
        // outlet: 45 deg elbow + 1'-0 leg and erosion stone mattress (pier 5G/9G/14G); 19G ends 1" above the pavement
        if (!double.IsNaN(zD) && pd.Outlet == PierDrainOutlet.ErosionMattress && gz is { } gg)
        {
            pE = pCol + outw * (pd.OutletLegLength * Math.Cos(Math.PI / 4));
            zE = zD - pd.OutletLegLength * Math.Sin(Math.PI / 4);
            Add(MiscGroup.Drainage, MiscKind.DrainPipe, asm, asm + " outlet leg", $"45 deg outlet leg {Sh(pd.OutletLegLength)} long (direction away from the pier: not printed), outlet {Sh(zE - gg)} above the mattress",
                MiscMesh.Tube(pCol.WithZ(zD), pE.WithZ(zE), rc, rc - wc), "ASTM A53 Gr B, galvanized");
            var mat = new Mesh();
            MeshBuilder.AddBox(mat, MiscMesh.Plan(pE, unit.D), -pd.MattressSize / 2, pd.MattressSize / 2, -pd.MattressSize / 2, pd.MattressSize / 2, gg - pd.MattressThickness, gg);
            Add(MiscGroup.Drainage, MiscKind.ErosionMattress, asm, asm + " erosion stone mattress", $"Erosion stone mattress {Sh(pd.MattressSize)} x {Sh(pd.MattressSize)} x {Sh(pd.MattressThickness)} on engineering fabric, centred under the outlet", mat, "Erosion stone on engineering fabric");
        }
        FlagPier(asm, standoff);
        Warn($"{asm}: placement conventions to confirm - CL pier = CL column (S), trough invert at the deck underside at its high end, cleanouts / flanges / hanger rods not modeled.");
    }
}
