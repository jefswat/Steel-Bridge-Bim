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
    private void FlagPier(string asm)
    {
        Flag(asm + " neoprene trough", "U-section width and sag of the trough are not dimensioned (Ramp G PDF 154 sections A-A / C-C, PDF 156 section B-B).", "strip as wide as the joint blockout, invert at the deck underside at the high end");
        Flag(asm + " trough collection box", "Only the distance of the box from the roadway edge is printed (PDF 154); its orientation and vertical position are not.", "2'-2 side along the pier line, top at the trough invert at the box");
        Flag(asm + " pier collection box", "Position of the pier collection box across the cap (N) and its top 'below the pier top 1\" min / 1/4\" max' are not dimensioned (PDF 154 section A-A, PDF 155 / 157).", "centred on the column in N, top 1/4\" below the cap top");
        Flag(asm + " pipe 1", "The longitudinal run of the 1:12 pipe from the trough box to the pier collection box is not dimensioned (PDF 154 sections A-A / C-C).", "straight in plan between the box centres at 1:12 from the box low end");
        Flag(asm + " pier pipe", "Route of the pipe along the cap soffit and the face / standoff of the vertical pipe on the column are not dimensioned (PDF 157 drain pipe elevations).", "pipe tangent under the cap soffit at the box, exactly the minimum slope to the column, vertical tangent to the back face of the column");
        Flag(asm + " pipe supports", "Support positions follow the pipe route, which is not dimensioned (PDF 157; spacing 6'-0 / 4'-0 max is printed).", "first clamp 1'-0 below the soffit run end, then at the maximum spacing");
        Flag(asm + " outlet leg", "Direction of the 45 deg outlet leg is not printed (PDF 157 outlet detail).", "away from the column on the back side");
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
        Vec2 u = pd.CollectRight ? -dir : dir;                         // toward the high end of the trough
        double Soffit(Vec2 p) { var so = _a.Inverse(p); return Ztop(so.Station, so.Offset) - _deck.Thickness; }
        double zHi = Soffit(pFar);
        double run = Math.Abs(tBox - tFar);
        double drop = pd.TroughSlope * run;
        double zBox = zHi - drop;
        if (pd.PrintedTroughDrop is { } pdrop)
        {
            if (Math.Abs(drop - pdrop) > In(2)) Warn($"{asm}: trough drop {Sh(drop)} (slope {pd.TroughSlope * 100:F1}% x run {L(run)}) differs from the printed {Sh(pdrop)}.");
            else Info($"{asm}: trough drop {Sh(drop)} from the deck underside over {L(run)} at {pd.TroughSlope * 100:F1}% (printed {Sh(pdrop)}).");
        }
        Info($"{asm}: roadway between gutter lines along the pier line {L(span)}; trough box {Sh(pd.BoxFromGutter)} inboard of the {(pd.CollectRight ? "right" : "left")} gutter line.");

        // trough: sloped strip along the joint, invert at the deck underside at the high end (hung from the deck by inserts), width = blockout width (not dimensioned)
        double tw = ej.BlockoutWidth > 0 ? ej.BlockoutWidth : Ft(1);
        var trough = MiscMesh.Strip(pFar.WithZ(zHi), pBox.WithZ(zBox), new Vec3(ahead.X, ahead.Y, 0), tw, In(0.25));
        Add(MiscGroup.Drainage, MiscKind.DrainTrough, asm, asm + " neoprene trough", $"1/4\" reinforced neoprene trough under the joint, slope {pd.TroughSlope * 100:F1}%, {L(run)} long, dropping {Sh(drop)} to the collection box",
            trough, "Reinforced neoprene", true, $"Plans give slope and bolting only: the U section (width, sag) is not dimensioned; width modeled = joint blockout {Sh(tw)}; invert modeled at the deck underside at the high end");

        // trough collection box
        double H = pd.BoxHighEndHeight, sl3 = pd.BoxBottomSlope;
        var boxMesh = MiscMesh.VerticalSection(pBox, u, new[] { new Vec2(-pd.BoxLength / 2, zBox), new Vec2(pd.BoxLength / 2, zBox), new Vec2(pd.BoxLength / 2, zBox - H), new Vec2(-pd.BoxLength / 2, zBox - H - sl3) }, pd.BoxWidth / 2);
        Add(MiscGroup.Drainage, MiscKind.DrainBox, asm, asm + " trough collection box", $"Trough collection box, 3/8\" plate, {Sh(pd.BoxLength)} x {Sh(pd.BoxWidth)}, {Sh(H)} high at the high end, bottom sloping {Sh(sl3)}; solid envelope; top at the trough invert (modeling convention)",
            boxMesh, "ASTM A36 3/8 plate, galvanized");

        // cap collection box (S from the column CL; on the column's N)
        var col = unit.Columns.OrderBy(c => Math.Abs(c.S)).FirstOrDefault();
        if (col == null) { Warn($"{asm}: the pier has no column; pier collection box and pipes not built."); return; }
        if (unit.Columns.Count != 1) Warn($"{asm}: {unit.Columns.Count} columns; the 'CL pier / CL column' distance is taken from the column nearest the support line.");
        double sCb = col.S + pd.CapBoxFromColumn, nCb = col.N;
        double capTop = unit.CapTopAt(sCb, nCb);
        if (double.IsNaN(capTop)) { Warn($"{asm}: no cap top at S = {L(sCb)}."); return; }
        if (sCb < unit.CapS0 || sCb > unit.CapS1) Warn($"{asm}: the cap collection box (S = {L(sCb)}) is off the cap (S {L(unit.CapS0)} to {L(unit.CapS1)}).");
        if (pd.CapBoxBelowTop == null) Warn($"{asm}: box top below the cap top is printed as 1\" min / 1/4\" max (UNRESOLVED): 1/4\" modeled.");
        double boxTop = capTop - (pd.CapBoxBelowTop ?? In(0.25));
        var pCap = unit.Plan(sCb, nCb);
        var capBox = new Mesh();
        MeshBuilder.AddBox(capBox, MiscMesh.Plan(pCap, unit.D), -pd.CapBoxWidthS / 2, pd.CapBoxWidthS / 2, -pd.CapBoxLengthN / 2, pd.CapBoxLengthN / 2, boxTop - pd.CapBoxDepth, boxTop);
        Add(MiscGroup.Drainage, MiscKind.DrainBox, asm, asm + " pier collection box", $"Pier collection box on the cap, 3/8\" plate, {Sh(pd.CapBoxLengthN)} x {Sh(pd.CapBoxWidthS)} x {Sh(pd.CapBoxDepth)} deep, {Sh(pd.CapBoxFromColumn < 0 ? -pd.CapBoxFromColumn : pd.CapBoxFromColumn)} from CL column; N position = CL column (not dimensioned); solid envelope (tapered bottom not modeled)",
            capBox, "ASTM A36 3/8 plate, galvanized");

        // pipe 1: trough box low end, slope 1:12 to above the cap box, then vertical
        var (od1, w1) = MiscMesh.PipeSch40(pd.PipeNominalDiameter);
        var p0 = pBox;
        double z0 = zBox - H - sl3 + In(1.5);
        double runP = (pCap - p0).Length;
        var dSN = pCap - p0;
        Info($"{asm}: pipe 1 plan run {L(runP)} (along the pier line {L(Vec2.Dot(dSN, dir))}, along the bridge {L(Vec2.Dot(dSN, ahead))}) from the trough box to the pier collection box.");
        if (runP < In(6)) Warn($"{asm}: the pier collection box is almost directly under the trough box (run {Sh(runP)}): the plan's 1:12 pipe needs a longitudinal run (section A-A) whose length is not dimensioned (UNRESOLVED N position of the cap box); modeled as computed.");
        else Warn($"{asm}: N position of the pier collection box on the cap is not dimensioned (section A-A shows the pipe running back-station about 6-7 ft by scale): placed on the CL column.");
        double z1 = z0 - runP * pd.Pipe1Slope;
        double zFl = boxTop + In(4);
        var pipe1 = new Mesh();
        pipe1.Append(MiscMesh.Tube(p0.WithZ(z0), pCap.WithZ(z1), od1 / 2, od1 / 2 - w1));
        if (z1 > zFl) pipe1.Append(MiscMesh.Tube(pCap.WithZ(z1), pCap.WithZ(zFl), od1 / 2, od1 / 2 - w1));
        else Warn($"{asm}: pipe 1 reaches the cap box at {L(z1)}, below the box flange level {L(zFl)} (run {L(runP)} at 1:{1 / pd.Pipe1Slope:F0}): the plan's vertical relationships do not close with the modeled trough level.");
        Add(MiscGroup.Drainage, MiscKind.DrainPipe, asm, asm + " pipe 1", $"{Sh(pd.PipeNominalDiameter)} Sch 40 pipe from the trough collection box, 1:{1 / pd.Pipe1Slope:F0} to the pier collection box ({L(runP)}), then vertical", pipe1, "ASTM A53 Gr B, galvanized");
        Record(asm, "pipe 1", p0.WithZ(z0), pCap.WithZ(z1), od1 / 2, sl.Station);
        if (z1 > zFl) Record(asm, "pipe 1 vertical leg", pCap.WithZ(z1), pCap.WithZ(zFl), od1 / 2, sl.Station);

        // collector end (Pier 19G): horizontal to the box, then down
        var (odc, wc) = MiscMesh.PipeSch40(pd.CapPipeNominalDiameter);
        if (pd.Collector != "" && _collectors.TryGetValue(pd.Collector, out var coll))
        {
            var e = coll.Pts[^1];
            double dd = (pCap - e.XY).Length;
            if (dd > In(12)) Warn($"{asm}: the span collector turns down at its end ({L(e.Z)}), {Sh(dd)} (plan) from the pier collection box centre; the elbow is modeled straight down at the pipe end (the plan gives no plan routing to the box, so a clearance conflict of this elbow is a consequence of that assumption).");
            var em = new Mesh();
            double zEl = Math.Max(zFl, 0);
            if (e.Z > zFl) em.Append(MiscMesh.Tube(e, e.XY.WithZ(zFl), coll.Od / 2, coll.Od / 2 - coll.Wall));
            Add(MiscGroup.Drainage, MiscKind.DrainPipe, asm, asm + " collector end", $"{coll.Name} elbow down toward the pier collection box", em, "ASTM A53 Gr B, galvanized");
            if (e.Z > zFl) Record(asm, "collector end", e, e.XY.WithZ(zFl), coll.Od / 2, sl.Station);
            string clash = "";
            if (e.Z > zFl)
            {
                var (cc, ag, _) = MiscMesh.Clearance(_steelIndex, e, e.XY.WithZ(zFl), coll.Od / 2);
                clash = double.IsNaN(cc) ? "" : $" Modeled drop clearance to steel: {Sh(cc)} to {ag}.";
            }
            Flag(asm + " collector end", $"The plans (Ramp G PDF 154 partial plan, PDF 157) do not give the routing of the span 19G collector from its end at the bearing line to the pier collection box ({Sh(dd)} apart in plan).{clash} The straight drop also overlaps the Pier 19G masonry plate where modeled (see the clearance records).",
                "elbow straight down at the pipe end to the box flange level");
        }

        // cap pipe: box bottom -> soffit -> 8% toward the column -> vertical on the back face
        double capBot = unit.CapBottomAt(sCb);
        if (double.IsNaN(capBot)) { Warn($"{asm}: no cap soffit at the box: pipe along the cap and down the column not built."); return; }
        double rc = odc / 2;
        double zBoxBot = boxTop - pd.CapBoxDepth;
        double zB = capBot - rc;
        if (zBoxBot < zB) Warn($"{asm}: the pier collection box bottom ({L(zBoxBot)}) is below the cap soffit under it ({L(capBot)}): box deeper than the cap is thick.");
        var pCol = unit.Plan(col.S, col.N - col.SizeN / 2 - rc);
        double runC = (pCol - pCap).Length;
        double zC = Math.Min(zBoxBot, zB) - runC * pd.CapPipeSlope;
        double? pav = pd.PavementElevation;
        double? gz = pav is { } pv ? pv : _ground?.Invoke(pCol);
        var pts = new List<Vec3> { pCap.WithZ(zBoxBot), pCap.WithZ(Math.Min(zBoxBot, zB)), pCol.WithZ(zC) };
        double zD = double.NaN;
        Vec2 pE = pCol;
        double zE = double.NaN;
        if (gz is not { } g0) Warn($"{asm}: no terrain or pavement elevation at the pier: the vertical pipe is not extended to the ground.");
        else if (pd.Outlet == PierDrainOutlet.Pavement) zD = g0 + In(1);
        else zD = g0 + pd.OutletAboveMattress;
        if (!double.IsNaN(zD))
        {
            if (zD >= zC) Warn($"{asm}: ground / pavement at the pipe ({L(zD)}) is above the pipe where it reaches the column ({L(zC)}).");
            else pts.Add(pCol.WithZ(zD));
        }
        var cp = new Mesh();
        for (int i = 1; i < pts.Count; i++) cp.Append(MiscMesh.Tube(pts[i - 1], pts[i], rc, rc - wc));
        Add(MiscGroup.Drainage, MiscKind.DrainPipe, asm, asm + " pier pipe", $"{Sh(pd.CapPipeNominalDiameter)} Sch 40 pipe from the pier collection box through the cap, {pd.CapPipeSlope * 100:F0}% along the soffit toward the CL column, then vertical on the back face of the column (face and stand-off not dimensioned: tangent to the back face, 8% exactly)",
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
            var e2 = -unit.N;
            pE = pCol + e2 * (pd.OutletLegLength * Math.Cos(Math.PI / 4));
            zE = zD - pd.OutletLegLength * Math.Sin(Math.PI / 4);
            Add(MiscGroup.Drainage, MiscKind.DrainPipe, asm, asm + " outlet leg", $"45 deg outlet leg {Sh(pd.OutletLegLength)} long (direction away from the column: not printed), outlet {Sh(zE - gg)} above the mattress",
                MiscMesh.Tube(pCol.WithZ(zD), pE.WithZ(zE), rc, rc - wc), "ASTM A53 Gr B, galvanized");
            var mat = new Mesh();
            MeshBuilder.AddBox(mat, MiscMesh.Plan(pE, unit.D), -pd.MattressSize / 2, pd.MattressSize / 2, -pd.MattressSize / 2, pd.MattressSize / 2, gg - pd.MattressThickness, gg);
            Add(MiscGroup.Drainage, MiscKind.ErosionMattress, asm, asm + " erosion stone mattress", $"Erosion stone mattress {Sh(pd.MattressSize)} x {Sh(pd.MattressSize)} x {Sh(pd.MattressThickness)} on engineering fabric, centred under the outlet", mat, "Erosion stone on engineering fabric");
        }
        FlagPier(asm);
        Warn($"{asm}: placement conventions to confirm - CL pier = CL column (S), N of the pier collection box = CL column, vertical pipe on the back (down-station) face, trough invert at the deck underside at its high end, cleanouts / flanges / hanger rods not modeled.");
    }
}
