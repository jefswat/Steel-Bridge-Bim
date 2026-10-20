using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Steel;

/// <summary>
/// Support (pier / end) diaphragms of the steel tubs with their connection plates (Bridge O p.283-288): an interior diaphragm in every tub (web with access
/// opening, top flange, bearing stiffeners, corner clips, strut plates), an exterior diaphragm between the tubs (plate girder bolted to the tub webs by
/// angles / bent plates) and, when <see cref="TubSupportDiaphragmDefinition.TopConnection"/> / <see cref="TubSupportDiaphragmDefinition.BottomConnection"/>
/// are given, the TOP CONNECTION PLs over the girder top flanges and the BOTT. CONNECTION PLs beneath the bottom flanges, bolted to the girder flange and
/// the diaphragm flange(s) (holes registered in all of them). With connection plates the diaphragm flanges lie FLUSH with the girder flange face the plate
/// lies on and end 1/2" short of the girder flange edge (p.284 / 288 elevations), the exterior web being notched around the girder flanges.
/// </summary>
public sealed partial class SteelModelGenerator
{
    /// <summary>Exterior support diaphragm fits the girder flanges (connection plates): gap between the girder flange edge and the diaphragm flange end.</summary>
    private sealed record TubSupportFit(double Gap);

    /// <summary>Holes (world points on the plate seat) and plate ends the exterior diaphragm flanges receive from the connection plates.</summary>
    private sealed class TubDiaphragmExtras
    {
        public readonly List<Vec3> TopHoles = new(), BottomHoles = new();
        public readonly List<(Vec3 a, Vec3 b)> TopPlates = new();
    }

    /// <summary>Footprints (s, section lateral x) of the top connection plates on the girder top flanges: no studs under them.</summary>
    private readonly List<(int gi, int side, Vec2[] poly)> _tubTopPlates = new();
    private readonly List<string> _tubStudsOmitted = new();

    /// <summary>Plan point at section lateral <paramref name="x"/> / height <paramref name="y"/> on the line parallel to the girder (constant lateral position),
    /// <paramref name="n"/> measured perpendicular to the support line (+ ahead), for the support line direction <paramref name="dir"/>.</summary>
    private static Vec2 TubSupportPlan(TubFrame f, Vec2 dir, double x, double y, double n)
    {
        double h = x * f.Cos - y * f.Sin;
        double dr = Vec2.Dot(dir, f.PlanRight);
        double k = Math.Abs(dr) < 1e-9 ? 0 : Vec2.Dot(dir, f.PlanAhead) / dr;
        return f.Origin.XY + f.PlanRight * h + f.PlanAhead * (h * k + n * Math.Sqrt(1 + k * k));
    }

    /// <summary>Girder coordinates (path distance s, section lateral x) of a plan point at section height y.</summary>
    private (double s, double x) TubPlanToGirder(int gi, Vec2 q, double y, double s0)
    {
        double s = s0;
        for (int it = 0; it < 6; it++) { var tf = TF(gi, s); s += Vec2.Dot(q - tf.Origin.XY, tf.PlanAhead); }
        var f = TF(gi, s);
        double h = Vec2.Dot(q - f.Origin.XY, f.PlanRight);
        return (s, (h + y * f.Sin) / f.Cos);
    }

    private static bool InPoly(Vec2[] poly, Vec2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y) && p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X) inside = !inside;
        }
        return inside;
    }

    private void TubSupportDiaphragm(TubSupportDiaphragmDefinition sd)
    {
        var t = TubDef;
        int j = sd.Support;
        if (j < 0 || j >= _f.Supports.Count) { Warn($"Support diaphragm: support {j + 1} does not exist."); return; }
        var line = _f.SupportLine(j);
        var dir = line.Direction;
        double D = Sec.WebDepth, T = t.TopWebSpacing / 2;
        int nG = _f.Girders.Count;
        foreach (var u in sd.Unresolved) Warn($"{line.Name} diaphragm: " + (u.StartsWith("UNRESOLVED") ? u : "UNRESOLVED " + u));
        var tc = sd.TopConnection; var bc = sd.BottomConnection;
        if ((tc == null) != (bc == null)) Warn($"{line.Name} diaphragm: top and bottom connection plates are built together (the diaphragm flanges are then flush with the girder flanges); neither built.");
        if ((tc == null) != (bc == null)) { tc = null; bc = null; }
        var fit = tc != null && bc != null ? new TubSupportFit(sd.FlangeEndGap) : null;
        var pairExtras = Enumerable.Range(0, Math.Max(0, nG - 1)).Select(_ => new TubDiaphragmExtras()).ToArray();
        double clipL = sd.CornerClipLength, clipH = sd.CornerClipHeight;
        bool extPlateFlagged = false;

        foreach (var g in _f.Girders)
        {
            int gi = g.Index;
            double sb = BrgS(gi, j);
            var f = TF(gi, sb);
            double tw = TubPlate(gi, GirderPlate.Web, sb).T;
            var (fw, tfT) = TubPlate(gi, GirderPlate.TopFlange, sb);
            var (wbW, bfT) = TubPlate(gi, GirderPlate.BottomFlange, sb);
            var nrm3 = new Vec3(-dir.Y, dir.X, 0);    // horizontal normal of the support plane
            var nrm2 = new Vec2(-dir.Y, dir.X);
            // (x, y) on the support plane, n horizontally normal to it; the girder rises / falls with its grade over the horizontal shift (the plates sit on its flanges)
            // the plates keep to the girder flange / bottom flange SURFACES: over the shift n (normal to the support plane, which at a skewed support also moves
            // the point across the girder) the surface rises with the girder grade along the girder and with the deck cross slope across it
            double tanTh = f.Sin / f.Cos;
            Vec3 Map(double x, double y, double n) => f.AtInPlane(x, y, dir) + nrm3 * n
                + new Vec3(0, 0, (f.Grade * Vec2.Dot(nrm2, f.PlanAhead) + tanTh * Vec2.Dot(nrm2, f.PlanRight)) * n);
            // lateral shift per unit n of a point of the support plane (0 at a square support): plate ENDS against a girder web / flange edge are cut parallel to it
            double qS = Vec2.Dot(nrm2, f.PlanRight) / f.Cos;
            string asm = $"{g.Name}-SD{j + 1}";
            _assemblies.Add(new SteelAssembly { Mark = asm, Kind = AssemblyKind.CrossFrame, Girder = gi, Description = $"{g.Name} internal support diaphragm at {line.Name}" });
            double XI(int side, double y) => TubSection.WebFaceX(t, side, y, tw, false);
            bool hasTop = sd.InteriorTopFlange.Width > 0;
            double thkI = sd.InteriorTopFlange.Thickness;
            // fit: the interior top flange is flush with the top of the girder top flange, its web notched around the girder flange inner edges
            double yWT = fit != null && hasTop ? tfT - thkI : 0;
            double xf = T - fw / 2 - (fit != null ? fit.Gap : 0);
            bool notch = fit != null && hasTop && yWT > 1e-6;

            // ---- interior web (clips at the bottom corners, notch at the top, access opening)
            var outline = new List<Vec2>();
            double cL = clipL, cH = clipH;
            outline.Add(new Vec2(XI(-1, -D) + cL, -D));
            outline.Add(new Vec2(XI(1, -D) - cL, -D));
            if (cH > 0) outline.Add(new Vec2(XI(1, -D + cH), -D + cH));
            outline.Add(new Vec2(XI(1, 0), 0));
            if (notch) { outline.Add(new Vec2(xf, 0)); outline.Add(new Vec2(xf, yWT)); outline.Add(new Vec2(-xf, yWT)); outline.Add(new Vec2(-xf, 0)); }
            outline.Add(new Vec2(XI(-1, 0), 0));
            if (cH > 0) outline.Add(new Vec2(XI(-1, -D + cH), -D + cH));
            var openings = new List<Vec2[]>();
            if (sd.AccessOpeningWidth > 0)
                openings.Add(PlateBuilder.RoundedRectangle(new Vec2(0, -D + sd.AccessOpeningBottom + sd.AccessOpeningHeight / 2), sd.AccessOpeningWidth, sd.AccessOpeningHeight, sd.AccessOpeningRadius));
            var web = new Mesh();
            // the web's ends lie on the tub web faces: at a skewed support the end faces follow the web instead of staying square to the plate
            Vec3 MapEnds(double x, double y, double n) => Map(Math.Abs(Math.Abs(x) - Math.Abs(XI(x < 0 ? -1 : 1, y))) < 1e-7 ? x - n * qS : x, y, n);
            PlateBuilder.AddMappedWithOpenings(web, outline, Array.Empty<Hole>(), openings, MapEnds, -sd.InteriorWebThickness / 2, sd.InteriorWebThickness / 2);
            _parts.Add(new SteelPart { Mark = $"{asm}-W", Category = PartCategory.WebPlate, Assembly = asm, Mesh = web, Length = 2 * T,
                Description = $"Interior support diaphragm web PL {In(sd.InteriorWebThickness)}" + (openings.Count > 0 ? $", access opening {In(sd.AccessOpeningWidth)} x {In(sd.AccessOpeningHeight)} R {In(sd.AccessOpeningRadius)}" : "")
                    + (cH > 0 ? $", corner clips {In(cL)} x {In(cH)}" : "") });

            // ---- connection plates over the top flanges (collect the holes of the interior diaphragm flange first)
            var intFlangeHoles = new List<Hole>();
            if (tc != null)
                for (int side = -1; side <= 1; side += 2)
                    TubTopConnectionPlate(g, j, line.Name, sd, tc, dir, f, side, fw, tfT, asm, Map, nrm2, pairExtras, intFlangeHoles, ref extPlateFlagged);

            // ---- interior top flange
            if (hasTop)
            {
                double xa = -xf, xb = xf;
                double y0 = fit != null ? yWT : 0, y1 = y0 + thkI;
                var m = new Mesh();
                double hwI = sd.InteriorTopFlange.Width / 2;
                // ends parallel to the girder flange edge (square ends would cross it at a skewed support), the 1/2" gap kept across the girder
                PlateBuilder.AddPrism(m, new[] { new Vec2(xa + hwI * qS, -hwI), new Vec2(xb + hwI * qS, -hwI), new Vec2(xb - hwI * qS, hwI), new Vec2(xa - hwI * qS, hwI) },
                    intFlangeHoles, (x, n, y) => Map(x, y, n), _ => y0, _ => y1);
                _parts.Add(new SteelPart { Mark = $"{asm}-TF", Category = PartCategory.FlangePlate, Assembly = asm, Mesh = m, Length = xb - xa,
                    Description = $"Interior support diaphragm top flange {Pl(sd.InteriorTopFlange.Width, thkI)}" });
            }
            // ---- bearing stiffeners, near and far side, normal to the bottom flange, tight fit at the top flange
            double yBS = notch ? yWT : 0;
            int no = 1;
            foreach (double xc in new[] { -sd.BearingStiffenerSpacing / 2, sd.BearingStiffenerSpacing / 2 })
                foreach (int face in new[] { -1, 1 })
                {
                    var m = new Mesh();
                    double n0 = face > 0 ? sd.InteriorWebThickness / 2 : -sd.InteriorWebThickness / 2 - sd.BearingStiffener.Width, n1 = n0 + sd.BearingStiffener.Width;
                    PlateBuilder.AddPrism(m, new[] { new Vec2(n0, -D), new Vec2(n1, -D), new Vec2(n1, yBS), new Vec2(n0, yBS) }, Array.Empty<Hole>(),
                        (n, y, x) => Map(xc + x, y, n), _ => -sd.BearingStiffener.Thickness / 2, _ => sd.BearingStiffener.Thickness / 2);
                    _parts.Add(new SteelPart { Mark = $"{asm}-BS{no++}", Category = PartCategory.BearingStiffener, Assembly = asm, Mesh = m, Length = D - yBS,
                        Description = $"Bearing stiffener {Pl(sd.BearingStiffener.Width, sd.BearingStiffener.Thickness)}" });
                }
            _tr.Stiffeners += 4;
            // ---- STRUT PL under the access opening (Section A-A p.287): one on each web face between the bearing stiffeners, 1 x 1 clips at the web corners
            if (sd.StrutPlate is { Thickness: > 0, Width: > 0 } sp && sd.AccessOpeningWidth > 0)
            {
                double y0 = -D + sd.AccessOpeningBottom;
                double xe = sd.BearingStiffenerSpacing / 2 - sd.BearingStiffener.Thickness / 2, c = sp.Clip;
                int k = 1;
                foreach (int face in new[] { -1, 1 })
                {
                    double nw = face * sd.InteriorWebThickness / 2, nf = face * (sd.InteriorWebThickness / 2 + sp.Width);
                    var poly = new[] { new Vec2(-xe + c, nw), new Vec2(xe - c, nw), new Vec2(xe, nw + face * c), new Vec2(xe, nf), new Vec2(-xe, nf), new Vec2(-xe, nw + face * c) };
                    var m = new Mesh();
                    PlateBuilder.AddPrism(m, poly, Array.Empty<Hole>(), (x, n, y) => Map(x, y, n), _ => y0 - sp.Thickness, _ => y0);
                    if (sp.Flag.Length > 0 && _tubBottomFlagged.Add("strut" + j)) Warn(sp.Flag);
                    Assumption? asu = sp.Flag.Length > 0 ? Assumption.UnresolvedDimension(sp.Flag, $"PL {In(sp.Thickness)} x {In(sp.Width)} on each web face between the bearing stiffeners, {In(c)} x {In(c)} clips") : null;
                    _parts.Add(new SteelPart { Mark = $"{asm}-ST{k++}", Category = PartCategory.DiaphragmPlate, Assembly = asm, Mesh = m, Length = 2 * xe, Assumption = asu,
                        Description = $"Strut PL {In(sp.Thickness)} x {In(sp.Width)} x {In(2 * xe)}, {In(c)} x {In(c)} clips" });
                }
            }
        }

        // ---- bottom connection plates (beneath the tub bottom flange and the exterior diaphragm bottom flange)
        if (bc != null)
            foreach (var g in _f.Girders)
                for (int side = -1; side <= 1; side += 2)
                {
                    bool hasNeighbour = side == 1 ? g.Index + 1 < nG : g.Index > 0;
                    if (hasNeighbour) TubBottomConnectionPlate(g, j, line.Name, sd, bc, dir, side, pairExtras[side == 1 ? g.Index : g.Index - 1]);
                }

        // ---- exterior diaphragms
        for (int gl = 0; gl + 1 < nG; gl++)
        {
            string asm = $"XSD{j + 1}-{gl + 1}";
            var fl = sd.ExteriorFlange;
            double sL = BrgS(gl, j), sR = BrgS(gl + 1, j);
            double yTopWeb = -fl.Thickness, webDepth = D - 2 * fl.Thickness;
            if (fit != null)
            {
                double tfL = TubPlate(gl, GirderPlate.TopFlange, sL).T, tfR = TubPlate(gl + 1, GirderPlate.TopFlange, sR).T;
                double bfL = TubPlate(gl, GirderPlate.BottomFlange, sL).T, bfR = TubPlate(gl + 1, GirderPlate.BottomFlange, sR).T;
                if (Math.Abs(tfL - tfR) > Inch(1.0 / 16) || Math.Abs(bfL - bfR) > Inch(1.0 / 16))
                    Warn($"{asm}: the girder flanges differ at the support (top {In(tfL)} / {In(tfR)}, bottom {In(bfL)} / {In(bfR)}); the diaphragm flanges are flush with girder {gl + 1}.");
                yTopWeb = tfL - fl.Thickness;
                webDepth = yTopWeb - (-D - bfL + fl.Thickness);
            }
            TubDiaphragmBetween(asm, gl, sL, gl + 1, sR, dir, yTopWeb, webDepth, sd.ExteriorWebThickness, fl, true,
                (sd.Connection == TubDiaphragmConnection.BentPlates ? "BENT " : "") + sd.ConnectionShape, sd.ConnectionLength, sd.GirderLegBolts, sd.DiaphragmLegBolts,
                sd.BoltPitch, Inch(0.5), true, $"Exterior support diaphragm at {line.Name}, G{gl + 1} - G{gl + 2}", fit, pairExtras[gl]);
        }
        _tr.SupportDiaphragms++;
    }

    /// <summary>TOP CONNECTION PL on the top flange of girder <paramref name="g"/> side <paramref name="side"/> at support <paramref name="j"/>: bolted to the girder
    /// top flange (holes registered) and the diaphragm top flange(s) next to it (interior: hole list in the interior flange's (x, n); exterior: world points).</summary>
    private void TubTopConnectionPlate(GirderResult g, int j, string supportName, TubSupportDiaphragmDefinition sd, TubTopConnectionDefinition tc, Vec2 dir, TubFrame f, int side,
        double fw, double tfT, string asm, Func<double, double, double, Vec3> map, Vec2 nrm2, TubDiaphragmExtras[] pairExtras, List<Hole> intFlangeHoles, ref bool flagged)
    {
        var t = TubDef;
        int gi = g.Index, nG = _f.Girders.Count;
        double T = t.TopWebSpacing / 2, sb = BrgS(gi, j), tp = tc.Thickness;
        bool interior = side == 1 ? gi + 1 < nG : gi > 0;      // flange faces the neighbour tub: plate over both diaphragm flanges
        double a0 = interior ? tc.InteriorStart : tc.ExteriorStart, a1 = interior ? tc.InteriorEnd : tc.ExteriorEnd;
        var offs = interior ? tc.InteriorHoles : tc.ExteriorHoles;
        double xin = side * (T - fw / 2);
        Vec2 Plan(double off, double n) => TubSupportPlan(f, dir, xin + side * off, tfT, n);
        (double s, double x) Conv(Vec2 q, double s0) => TubPlanToGirder(gi, q, tfT, s0);
        int pair = side == 1 ? gi : gi - 1;                    // exterior diaphragm gap index (valid when interior)
        double halfW = tc.Width / 2;

        var poly = new List<Vec2>();
        var footprint = new List<Vec2>();
        foreach (var (off, n) in new[] { (a0, -halfW), (a1, -halfW), (a1, halfW), (a0, halfW) })
        {
            var (s, x) = Conv(Plan(off, n), sb);
            poly.Add(new Vec2(s, x - side * T));
            footprint.Add(new Vec2(s, x));
        }
        var plateHoles = new List<Hole>();
        int count = 0;
        foreach (double off in offs)
            foreach (double n in new[] { -tc.RowGage / 2, tc.RowGage / 2 })
            {
                var q = Plan(off, n);
                var (s, x) = Conv(q, sb);
                double w = x - side * T;
                var ff = TF(gi, s);
                plateHoles.Add(new Hole(new Vec2(s, w), HoleDia));
                double grip;
                if (off >= 0 && off <= fw)
                {
                    TubHoles(gi, TopKey(side)).Add(new Hole(new Vec2(s, w), HoleDia));
                    grip = tp + tfT;
                }
                else if (off < 0)
                {
                    // interior diaphragm flange: its (x, n) plane coordinates
                    double dr = Vec2.Dot(dir, f.PlanRight);
                    double h = dr * Vec2.Dot(q - f.Origin.XY, dir);
                    intFlangeHoles.Add(new Hole(new Vec2((h + tfT * f.Sin) / f.Cos, Vec2.Dot(q - f.Origin.XY, nrm2)), HoleDia));
                    grip = tp + sd.InteriorTopFlange.Thickness;
                }
                else
                {
                    pairExtras[pair].TopHoles.Add(ff.At(x, tfT));
                    grip = tp + sd.ExteriorFlange.Thickness;
                }
                AddBolt(ff.At(x, tfT + tp), -ff.Up, grip, asm);
                count++;
            }
        var mesh = new Mesh();
        PlateBuilder.AddPrism(mesh, poly, plateHoles, (s, w, tt) => TF(gi, s).At(side * T + w, tfT + tt), _ => 0, _ => tp);
        double len = Math.Abs(a1 - a0);
        Assumption? asu = null;
        if (!interior)
        {
            // the plan dimensions the plate and its holes from the end at the interior diaphragm; where the girder flange edge falls along the plate is not dimensioned
            asu = Assumption.UnresolvedDimension(
                UnresolvedFlag.Text("p.284, p.286, p.288", "top connection PL at the exterior flange: position of the girder top flange edge along the 1'-5 1/2\" plate",
                    "the plan view dimensions the holes (2\" / 3 1/2\" / 2 1/2\" / 2\" / 5 1/2\") but not where the flange edge crosses the plate", "Flange edge taken 8\" from the plate end (2 + 3 1/2 + 2 1/2), plate 9 1/2\" over the girder flange."),
                "plate laid out from the printed hole dimensions");
            if (!flagged) { Warn(asu.Reason); flagged = true; }
        }
        _parts.Add(new SteelPart { Mark = $"{asm}-TCP{(side < 0 ? "L" : "R")}", Category = PartCategory.ConnectorPlate, Assembly = asm, Mesh = mesh, Length = len, Assumption = asu,
            Description = $"Top connection PL {In(tp)} x {In(tc.Width)} x {In(len)} at the {(interior ? "interior" : "exterior")} flange, {count} holes" });
        _tubTopPlates.Add((gi, side, footprint.ToArray()));
        if (interior)
        {
            var pa = Conv(Plan(fw, 0), sb); var pb = Conv(Plan(a1, 0), sb);
            var fa = TF(gi, pa.s); var fb2 = TF(gi, pb.s);
            pairExtras[pair].TopPlates.Add((fa.At(pa.x, tfT), fb2.At(pb.x, tfT)));
        }
    }

    /// <summary>BOTT. CONNECTION PL beneath the bottom flange of girder <paramref name="g"/> on <paramref name="side"/> (toward the exterior diaphragm).</summary>
    private void TubBottomConnectionPlate(GirderResult g, int j, string supportName, TubSupportDiaphragmDefinition sd, TubBottomConnectionDefinition bc, Vec2 dir, int side, TubDiaphragmExtras extras)
    {
        var t = TubDef;
        int gi = g.Index;
        double D = Sec.WebDepth, sb = BrgS(gi, j), tp = bc.Thickness;
        var f = TF(gi, sb);
        var (wbW, bfT) = TubPlate(gi, GirderPlate.BottomFlange, sb);
        double yU = -D - bfT;                                    // underside of the tub bottom flange
        double xe = side * wbW / 2;                              // bottom flange edge
        Vec2 Plan(double u, double n) => TubSupportPlan(f, dir, xe + side * (u - bc.InsetFromFlangeEdge), yU, n);
        string asm = $"{g.Name}-SD{j + 1}";
        double gw = bc.GirderEndWidth / 2, dw = bc.DiaphragmEndWidth / 2;
        var poly = new List<Vec2>();
        foreach (var (u, n) in new[] { (0.0, -gw), (bc.TaperStart, -gw), (bc.Length, -dw), (bc.Length, dw), (bc.TaperStart, gw), (0.0, gw) })
        {
            var (s, x) = TubPlanToGirder(gi, Plan(u, n), yU, sb);
            poly.Add(new Vec2(s, x));
        }
        var holes = new List<Hole>();
        int pair = side == 1 ? gi : gi - 1, count = 0;
        void AddHoles(IEnumerable<double> cols, IEnumerable<double> rows, bool girderFlange)
        {
            foreach (double u in cols)
                foreach (double n in rows)
                {
                    var (s, x) = TubPlanToGirder(gi, Plan(u, n), yU, sb);
                    var ff = TF(gi, s);
                    holes.Add(new Hole(new Vec2(s, x), HoleDia));
                    double grip;
                    if (girderFlange) { TubHoles(gi, "BF").Add(new Hole(new Vec2(s, x), HoleDia)); grip = tp + bfT; }
                    else { extras.BottomHoles.Add(ff.At(x, yU)); grip = tp + sd.ExteriorFlange.Thickness; }
                    AddBolt(ff.At(x, yU - tp), ff.Up, grip, asm);
                    count++;
                }
        }
        AddHoles(bc.GirderHoleColumns, bc.GirderHoleRows, true);
        AddHoles(bc.DiaphragmHoleColumns, bc.DiaphragmHoleRows, false);
        var mesh = new Mesh();
        PlateBuilder.AddPrism(mesh, poly, holes, (s, w, tt) => TF(gi, s).At(w, yU - tp + tt), _ => 0, _ => tp);
        Assumption? asu = bc.TaperFlag.Length > 0
            ? Assumption.UnresolvedDimension(bc.TaperFlag, $"width {In(bc.GirderEndWidth)} over the first {In(bc.TaperStart)}, then tapered to {In(bc.DiaphragmEndWidth)} at the diaphragm end")
            : null;
        _parts.Add(new SteelPart { Mark = $"{asm}-BCP{(side < 0 ? "L" : "R")}", Category = PartCategory.ConnectorPlate, Assembly = asm, Mesh = mesh, Length = bc.Length, Assumption = asu,
            Description = $"Bottom connection PL {In(tp)} x {In(bc.GirderEndWidth)} / {In(bc.DiaphragmEndWidth)} x {In(bc.Length)}, {count} holes" });
        if (asu != null && !_tubBottomFlagged.Contains(supportName)) { _tubBottomFlagged.Add(supportName); Warn(asu.Reason); }
    }

    private readonly HashSet<string> _tubBottomFlagged = new();
}
