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
    private void FlagPier(string asm, string standoff, bool noTrough = false, bool jog = false)
    {
        if (!noTrough)
        {
        Flag(asm + " neoprene trough", "U-section width and sag of the trough are not dimensioned (Ramp G PDF 154 sections A-A / C-C, PDF 156 section B-B).", "strip as wide as the joint blockout, from the deck underside at the high end to the box top");
        Flag(asm + " trough collection box", "Position along the pier is printed to the box (3'-6 / 4'-0 from the roadway edge, PDF 154) without saying to which point of the box; the L8x8x1/2 hanger to the cross frame top chord (PDF 155 sections E-E / H-H) is not modeled.", "centre of the box at the printed distance");
        Flag(asm + " pier collection box", "The box top is printed as a range, 1/4\" min / 1\" max below the top of cap (PDF 157 box elevation; PDF 155 section A-A shows 1\" at CL box), not one value. Face, 9\" inlet and 1 1/2\" outlet clearance are printed (PDF 155 / 157).", "top 1/4\" below the cap top (the minimum)");
        }
        if (!noTrough) Flag(asm + " pipe 1", "Only the 1:12 minimum slope and the turn-down 9\" (at 50 F) off the cap face are printed (PDF 155 sections A-A / C-C); the plan route from the trough box to the turn-down is not dimensioned (PDF 154 partial plan at 19G: 'orientation to clear inspection walkway and cross frame').", "straight in plan from the trough box to the turn-down, exactly 1:12 from the box low side");
        Flag(asm + " pier pipe", "The drain pipe elevations (Ramp G PDF 157, I-80 PDF 216-217) show the pipe leaving the box bottom, a cleanout bend to 8% min along the cap face toward the CL column, and a cleanout bend down the column; the level of the first bend, the stand-off of the vertical run from the column face" + (jog ? "" : " (no offset is drawn)") + " and the direction of the 45 deg outlet leg are not dimensioned.",
            $"bend directly under the box (pipe crown at the box bottom), exactly 8%, the run in the vertical plane of the box outlet 1 1/2\" clear of the cap face" + (jog ? ", then the Section C-C jog toward the column" : "") + $", so the vertical run is {standoff}; outlet leg away from the pier");
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
        bool noTrough = pd.NoTrough == true;
        var unit = _sub.Units.FirstOrDefault(u => u.SupportIndex == j);
        if (unit == null) { Warn($"{asm}: the substructure unit of this support is not generated here (shared pier owned by another bridge?); not built."); return; }
        Vec2 q = default, dir = default, ahead = default, pBox = default, lowDir = default;
        double zBox = 0, H = 0, sl3 = 0;
        if (noTrough)
            Asm(MiscGroup.Drainage, asm, $"Pier drain at {pd.Support}: pier collection box and pier pipe fed by {pd.Collector}", "pier drain");
        else
        {
            var ej = sd.ExpansionJoint;
            bool atEnd = j == _def.Supports.Count - 1, atBegin = j == 0;
            if (ej == null || _f.Deck == null || (!atEnd && !atBegin)) { Warn($"{asm}: needs an expansion joint at the first / last support of this bridge; not built."); return; }

            q = atEnd ? _f.Deck.EndLinePoint : _f.Deck.BeginLinePoint;
            dir = (atEnd ? _f.Deck.EndLineDirection : _f.Deck.BeginLineDirection).Normalized();
            var tan = Vec2.FromAngle(_a.AngleAt(sl.Station));
            ahead = dir.Left; if (Vec2.Dot(ahead, tan) < 0) ahead = -ahead;
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
            Vec2 pFar = q + dir * tFar; pBox = q + dir * tBox;
            double Soffit(Vec2 p) { var so = _a.Inverse(p); return Ztop(so.Station, so.Offset) - _deck.Thickness; }
            double zHi = Soffit(pFar);
            double run = Math.Abs(tBox - tFar);
            double soffitBox = Soffit(pBox);
            // trough collection box top: the printed 1'-8 is measured from the deck underside to the top of the box at the collection end (PDF 154 elevations)
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
            lowDir = pd.CapBoxFace == CapFaceSide.Back ? -ahead : ahead;
            H = pd.BoxHighEndHeight; sl3 = pd.BoxBottomSlope;
            var boxMesh = MiscMesh.VerticalSection(pBox, lowDir, new[] { new Vec2(-pd.BoxLength / 2, zBox), new Vec2(pd.BoxLength / 2, zBox), new Vec2(pd.BoxLength / 2, zBox - H - sl3), new Vec2(-pd.BoxLength / 2, zBox - H) }, pd.BoxWidth / 2);
            Add(MiscGroup.Drainage, MiscKind.DrainBox, asm, asm + " trough collection box", $"Trough collection box, 3/8\" plate, {Sh(pd.BoxLength)} across the joint x {Sh(pd.BoxWidth)} along it, {Sh(H)} deep at the high end, {Sh(H + sl3)} at the low end; top {Sh(soffitBox - zBox)} below the deck underside; solid envelope",
                boxMesh, "ASTM A36 3/8 plate, galvanized");
        }

        // pier collection box: fastened to the cap FACE (PDF 155 sections A-A / C-C, PDF 157 box details), S from the column CL
        var cols = unit.Columns.OrderBy(c => c.S).ToList();
        var col = pd.ColumnNumber is int cn ? (cn >= 1 && cn <= cols.Count ? cols[cn - 1] : null) : unit.Columns.OrderBy(c => Math.Abs(c.S)).FirstOrDefault();
        if (col == null) { Warn($"{asm}: the pier has no column{(pd.ColumnNumber != null ? $" no. {pd.ColumnNumber}" : "")}; pier collection box and pipes not built."); return; }
        if (unit.Columns.Count != 1 && pd.ColumnNumber == null) Warn($"{asm}: {unit.Columns.Count} columns; the 'CL pier / CL column' distance is taken from the column nearest the support line.");
        double side = pd.CapBoxFace == CapFaceSide.Back ? -1 : 1;
        double nFace = side < 0 ? unit.CapN0 : unit.CapN1;
        Vec2 outw = unit.N * side;                                       // away from the cap, normal to the face
        string faceName = side < 0 ? "back (down-station)" : "ahead (up-station)";
        double sCb = col.S + pd.CapBoxFromColumn;
        double capTop = unit.CapTopAt(sCb, nFace);
        if (double.IsNaN(capTop)) { Warn($"{asm}: no cap top at S = {L(sCb)}."); return; }
        if (sCb < unit.CapS0 || sCb > unit.CapS1) Warn($"{asm}: the cap collection box (S = {L(sCb)}) is off the cap (S {L(unit.CapS0)} to {L(unit.CapS1)}).");
        if (pd.CapBoxBelowTop == null) Warn($"{asm}: " + UnresolvedFlag.Text("pier collection box details (Ramp G PDF 157 / I-80 PDF 216)", "pier collection box top below the cap top", "it is printed as a range '1/4\" min / 1\" max', not one value", "1/4\" (the minimum) modeled."));
        double boxTop = capTop - (pd.CapBoxBelowTop ?? In(0.25));
        double zBoxBot = boxTop - pd.CapBoxDepth;
        var pFace = unit.Plan(sCb, nFace);
        var pIn = pFace + outw * pd.CapBoxInletFromFace;                 // CL of the pipe dropping into the box
        var (odc, wc) = MiscMesh.PipeSch40(pd.CapPipeNominalDiameter);
        double rc = odc / 2;
        var pOut = pFace + outw * (pd.CapBoxOutletClear + rc);           // CL of the outlet pipe at the box bottom
        double hTaper = Math.Min(pd.CapBoxTaperHeight ?? Ft(1), pd.CapBoxDepth);
        var capBoxMesh = MiscMesh.VerticalSection(pFace, outw, new[] { new Vec2(0, boxTop), new Vec2(pd.CapBoxOutTop, boxTop), new Vec2(pd.CapBoxOutTop, zBoxBot + hTaper), new Vec2(pd.CapBoxOutBottom, zBoxBot), new Vec2(0, zBoxBot) }, pd.CapBoxAlongFace / 2);
        Add(MiscGroup.Drainage, MiscKind.DrainBox, asm, asm + " pier collection box", $"Pier collection box on the {faceName} face of the cap, 3/8\" plate, {Sh(pd.CapBoxAlongFace)} along the face x {Sh(pd.CapBoxOutTop)} ({Sh(pd.CapBoxOutBottom)} at the bottom) out from the face x {Sh(pd.CapBoxDepth)} deep, {Sh(Math.Abs(pd.CapBoxFromColumn))} from CL column{(pd.ColumnNumber != null ? $" no. {pd.ColumnNumber}" : "")}, strapped to the face with 1/2\" stud anchors; solid envelope",
            capBoxMesh, "ASTM A36 3/8 plate, galvanized");
        Info($"{asm}: pier collection box on the {faceName} cap face at S = {L(sCb)} (cap face N = {L(nFace)}), top {L(boxTop)}; inlet CL {Sh(pd.CapBoxInletFromFace)} and outlet CL {Sh(pd.CapBoxOutletClear + rc)} off the face.");

        // collector end(s): each span collector turns down into the box (Ramp G PDF 155 section C-C; I-80 PDF 219 / 220 section C-C)
        bool collectorFeeds = false;
        double zJoin = double.NaN;                                       // CL of the (highest) span collector at its end
        double zIn = boxTop - pd.CapBoxInletEmbed;
        foreach (var cname in pd.Collector.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!_collectors.TryGetValue(cname, out var coll)) { Warn($"{asm}: collector '{cname}' not found."); continue; }
            collectorFeeds = true;
            var e = coll.Pts[^1];
            zJoin = double.IsNaN(zJoin) ? e.Z : Math.Max(zJoin, e.Z);
            double dd = (pIn - e.XY).Length;
            if (dd > In(1)) Warn($"{asm}: CHECK - {coll.Name} ends {Sh(dd)} (plan) from the box inlet axis {Sh(pd.CapBoxInletFromFace)} off the cap face: the printed box position ({Sh(Math.Abs(pd.CapBoxFromColumn))} from CL column) and the collector line do not meet; the drop is modeled from the collector end down and over into the box inlet (mitred).");
            else Info($"{asm}: {coll.Name} ends over the box inlet ({Sh(dd)} in plan).");
            if (e.Z > zIn)
            {
                var drop = new List<Vec3> { e };
                if (dd > In(1)) { double zm = Math.Max(zIn + In(18), e.Z - In(24)); drop.Add(e.XY.WithZ(zm)); drop.Add(pIn.WithZ(Math.Max(zIn + In(6), zm - dd))); }
                drop.Add(pIn.WithZ(zIn));
                Add(MiscGroup.Drainage, MiscKind.DrainPipe, asm, asm + $" {coll.Name} drop", $"{coll.Name}: drop into the pier collection box (mitred bends)", MiscMesh.Pipe(drop, coll.Od / 2, coll.Od / 2 - coll.Wall), "ASTM A53 Gr B, galvanized");
                for (int i = 1; i < drop.Count; i++) Record(asm, coll.Name + " drop", drop[i - 1], drop[i], coll.Od / 2, sl.Station);
            }
        }

        if (!noTrough)
        {
            // pipe 1: trough box low side, 1:12 to the turn-down 9" off the cap face, then vertical into the box (or into the collector drop at 19G)
            var (od1, w1) = MiscMesh.PipeSch40(pd.PipeNominalDiameter);
            var p0 = pBox + lowDir * (pd.BoxLength / 2);                    // low end wall of the trough box
            double z0 = zBox - H - sl3 + In(1.5);
            double runP = (pIn - p0).Length;
            var dSN = pIn - p0;
            Info($"{asm}: pipe 1 plan run {L(runP)} (along the pier line {L(Vec2.Dot(dSN, dir))}, along the bridge {L(Vec2.Dot(dSN, ahead))}) from the trough box to the turn-down {Sh(pd.CapBoxInletFromFace)} off the cap face.");
            double z1 = z0 - runP * pd.Pipe1Slope;
            double zLegEnd = collectorFeeds ? zJoin : zIn;                   // into the box, or down to the collector elbow it joins (PDF 155 section C-C)
            bool leg = z1 > zLegEnd;
            var p1pts = new List<Vec3> { p0.WithZ(z0), pIn.WithZ(z1) };
            if (leg) p1pts.Add(pIn.WithZ(zLegEnd));
            else Warn($"{asm}: pipe 1 reaches the turn-down at {L(z1)}, below the {(collectorFeeds ? "span collector" : "box inlet")} level {L(zLegEnd)} (run {L(runP)} at 1:{1 / pd.Pipe1Slope:F0}): the plan's vertical relationships do not close with the modeled trough level.");
            Add(MiscGroup.Drainage, MiscKind.DrainPipe, asm, asm + " pipe 1", collectorFeeds
                    ? $"{Sh(pd.PipeNominalDiameter)} Sch 40 pipe from the trough collection box, 1:{1 / pd.Pipe1Slope:F0} ({L(runP)}), then down into the end elbow of the span collector over the pier collection box (mitred bend)"
                    : $"{Sh(pd.PipeNominalDiameter)} Sch 40 pipe from the trough collection box, 1:{1 / pd.Pipe1Slope:F0} ({L(runP)}), turning down {Sh(pd.CapBoxInletFromFace)} off the cap face into the pier collection box (mitred bend)",
                MiscMesh.Pipe(p1pts, od1 / 2, od1 / 2 - w1), "ASTM A53 Gr B, galvanized");
            for (int i = 1; i < p1pts.Count; i++) Record(asm, i == 1 ? "pipe 1" : "pipe 1 vertical leg", p1pts[i - 1], p1pts[i], od1 / 2, sl.Station);
        }

        // pier pipe (one pipe, mitred bends): box bottom -> bend under the box -> 8% along the cap face toward the CL column -> bend -> vertical
        // down the column [-> OPTIONAL jog toward the column below the cap (I-80 PDF 217 Section C-C) -> vertical] -> outlet
        double nPipe = nFace + side * (pd.CapBoxOutletClear + rc);
        double zBend = zBoxBot - rc;
        var pCol = unit.Plan(col.S, nPipe);
        double runC = (pCol - pOut).Length;
        double zC = zBend - runC * pd.CapPipeSlope;
        double colFace = col.N + side * col.SizeN / 2;
        double capBot = unit.CapBottomAt(col.S);
        bool jog = pd.JogOffset is double && pd.JogRise is double && pd.JogBelowCap is double && !double.IsNaN(capBot);
        double nVert = jog ? nPipe - side * pd.JogOffset!.Value : nPipe;
        var pVert = unit.Plan(col.S, nVert);
        double zJ0 = double.NaN, zJ1 = double.NaN;
        var pts = new List<Vec3> { pOut.WithZ(zBoxBot), pOut.WithZ(zBend), pCol.WithZ(zC) };
        if (jog)
        {
            zJ0 = capBot - pd.JogBelowCap!.Value; zJ1 = zJ0 - pd.JogRise!.Value;
            if (zJ0 >= zC) { Warn($"{asm}: the jog start ({L(zJ0)}) is above the bend at the CL column ({L(zC)}); jog not modeled."); jog = false; pVert = pCol; nVert = nPipe; }
            else { pts.Add(pCol.WithZ(zJ0)); pts.Add(pVert.WithZ(zJ1)); }
        }
        double gap = side * (nVert - colFace) - rc;                       // clear from the column face to the (lower) vertical pipe
        string standoff = gap >= 0 ? $"{Sh(gap)} clear of the {faceName} column face" : $"{Sh(-gap)} INTO the column";
        if (gap < 0) Warn($"{asm}: CONFLICT - the vertical pier pipe overlaps the column by {Sh(-gap)} (column face N = {L(colFace)}).");
        else Info($"{asm}: pier pipe runs {Sh(pd.CapBoxOutletClear)} clear of the cap face; the vertical run is {standoff}{(jog ? $" after the {Sh(pd.JogOffset!.Value)} jog toward the column (Section C-C)" : "")} (cap face {Sh(side * (nFace - colFace))} proud of the column face).");
        double? pav = pd.PavementElevation;
        double? gz = pav is { } pv ? pv : _ground?.Invoke(pVert);
        double zD = double.NaN;
        double zE = double.NaN;
        if (gz is not { } g0) Warn($"{asm}: no terrain or pavement elevation at the pier: the vertical pipe is not extended to the ground.");
        else if (pd.Outlet == PierDrainOutlet.Pavement) zD = g0 + In(1);
        else zD = g0 + pd.OutletAboveMattress;
        double zVertTop = jog ? zJ1 : zC;
        if (!double.IsNaN(zD))
        {
            if (zD >= zVertTop) Warn($"{asm}: ground / pavement at the pipe ({L(zD)}) is above the pipe where it turns down ({L(zVertTop)}).");
            else pts.Add(pVert.WithZ(zD));
        }
        Vec2 pE = pVert;
        bool mattress = !double.IsNaN(zD) && zD < zVertTop && pd.Outlet == PierDrainOutlet.ErosionMattress && gz is not null;
        if (mattress)
        {
            pE = pVert + outw * (pd.OutletLegLength * Math.Cos(Math.PI / 4));
            zE = zD - pd.OutletLegLength * Math.Sin(Math.PI / 4);
            pts.Add(pE.WithZ(zE));                                      // 45 deg outlet leg, mitred onto the vertical (PDF 157 outlet detail)
        }
        Add(MiscGroup.Drainage, MiscKind.DrainPipe, asm, asm + " pier pipe", $"{Sh(pd.CapPipeNominalDiameter)} Sch 40 pipe from the bottom of the pier collection box, {pd.CapPipeSlope * 100:F0}% along the {faceName} cap face toward the CL column{(pd.ColumnNumber != null ? $" no. {pd.ColumnNumber}" : "")} ({L(runC)}), then vertical down the column ({standoff}){(jog ? $", jog {Sh(pd.JogOffset!.Value)} over {Sh(pd.JogRise!.Value)} below the cap" : "")}{(mattress ? $", 45 deg outlet leg {Sh(pd.OutletLegLength)}" : "")}; one pipe with mitred bends; outside the concrete, {Sh(pd.CapBoxOutletClear)} clear of the cap face",
            MiscMesh.Pipe(pts, rc, rc - wc), "ASTM A53 Gr B, galvanized");
        for (int i = 1; i < pts.Count; i++) Record(asm, "pier pipe", pts[i - 1], pts[i], rc, sl.Station);
        if (mattress && gz is { } gg)
        {
            var mat = new Mesh();
            MeshBuilder.AddBox(mat, MiscMesh.Plan(pE, unit.D), -pd.MattressSize / 2, pd.MattressSize / 2, -pd.MattressSize / 2, pd.MattressSize / 2, gg - pd.MattressThickness, gg);
            Add(MiscGroup.Drainage, MiscKind.ErosionMattress, asm, asm + " erosion stone mattress", $"Erosion stone mattress {Sh(pd.MattressSize)} x {Sh(pd.MattressSize)} x {Sh(pd.MattressThickness)} on engineering fabric, centred under the outlet", mat, "Erosion stone on engineering fabric");
        }

        // clamps (and, as designed, the 3/4" rods to the concrete): supports on the vertical run(s)
        if (!double.IsNaN(zD) && zD < zVertTop)
        {
            var cm = new Mesh();
            var rods = new Mesh();
            int n = 0;
            void Clamp(Vec2 at, double z, double nAt, double faceN)
            {
                cm.Append(MiscMesh.Tube(at.WithZ(z - In(1)), at.WithZ(z + In(1)), rc + In(0.5), rc)); n++;
                double len = side * (nAt - faceN) - (rc + In(0.5));
                if (pd.Fittings == true && len > In(0.5))
                {
                    var a0 = at - outw * (rc + In(0.5)); var a1 = at - outw * (rc + In(0.5) + len);
                    rods.Append(MiscMesh.Cylinder(a0.WithZ(z), a1.WithZ(z), In(0.375), 8));
                }
            }
            if (jog)
            {
                double zUp = capBot + (pd.JogSupportAbove ?? In(12));
                if (zUp < zC - In(3)) Clamp(pCol, zUp, nPipe, nFace);
                for (double z = zJ1 - (pd.JogSupportBelow ?? In(8.5)); z > zD; z -= pd.SupportSpacing) Clamp(pVert, z, nVert, colFace);
            }
            else
                for (double z = zC - In(12); z > zD; z -= pd.SupportSpacing) Clamp(pVert, z, nVert, colFace);
            Add(MiscGroup.Drainage, MiscKind.PipeSupport, asm, asm + " pipe supports", $"{n} pipe supports (steel clamps on 3/4\" rods into concrete inserts), {Sh(pd.SupportSpacing)} max{(jog ? $", 1'-0 above the cap bottom and {Sh(pd.JogSupportBelow ?? In(8.5))} below the jog (Section C-C)" : "")}; {(pd.Fittings == true ? "rods to the concrete face modeled, inserts not" : "rods and inserts not modeled")}", cm, "Galvanized steel");
            if (pd.Fittings == true && !rods.IsEmpty) Add(MiscGroup.Drainage, MiscKind.PipeSupport, asm, asm + " support rods", "3/4\" threaded rods, clamp to the concrete face (length VARIES on the plans)", rods, "Galvanized steel (ASTM A307)");
        }
        if (pd.Fittings == true)
        {
            // 4" raised brass cleanouts at the two bends on the cap (elevations "CLEANOUT"): stub on the extension of the incoming leg
            var co = new Mesh();
            foreach (int bi in new[] { 1, 2 })
            {
                var dIn = (pts[bi] - pts[bi - 1]).Normalized();
                co.Append(MiscMesh.Cylinder(pts[bi] + dIn * rc, pts[bi] + dIn * (rc + In(4)), In(2), 12));
            }
            Add(MiscGroup.Drainage, MiscKind.PipeFitting, asm, asm + " cleanouts", "2 cleanouts, 4\" raised hollow-square-head brass in a tapped hole at the bends on the cap", co, "Brass");
            Flag(asm + " cleanouts", "The cleanouts are shown at the two bends on the cap face (elevations) without their orientation or the tapped position on the fitting (Ramp G PDF 157 / 158, I-80 PDF 216-218).", "4\" stub on the extension of the incoming leg at each bend");
        }
        if (pd.FlangeDiameter is double fd)
        {
            // drain pipe flange (TYP.) at the top of the vertical run below the cap (elevations); 2 x 1/2" plates + 1/2" gasket
            double zf = (jog ? zJ1 : zC) - rc - In(6);
            if (!double.IsNaN(zD) && zf > zD + In(12))
            {
                Add(MiscGroup.Drainage, MiscKind.PipeFitting, asm, asm + " drain pipe flange", $"Drain pipe flange {Sh(fd)} OD, 2 x 1/2\" plates + 1/2\" neoprene gasket, 4 - 7/8\" bolts (flange table)",
                    MiscMesh.Tube(pVert.WithZ(zf - In(0.75)), pVert.WithZ(zf + In(0.75)), fd / 2, rc), "Galvanized steel / neoprene");
                Flag(asm + " drain pipe flange", "The flange is drawn at the top of the vertical run below the cap without a dimension (elevations, Ramp G PDF 157 / I-80 PDF 216-217).", "flange 6\" below the bend / jog");
            }
        }
        FlagPier(asm, standoff, noTrough, jog);
        Warn($"{asm}: placement conventions to confirm - CL pier = CL column (S){(noTrough ? "" : ", trough invert at the deck underside at its high end")}{(pd.Fittings == true ? "; concrete inserts not modeled" : ", cleanouts / flanges / hanger rods not modeled")}.");
    }
}
