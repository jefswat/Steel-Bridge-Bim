// FINISH (required, see CLAUDE.md "New plan-set model checklist" / docs/MODEL-PIPELINE.md): `sbm run <this script> <model.sbm>` runs `sbm pipeline` after the
// script saves = regenerate, terrain (georeferenced projects), plan sheet index, companion database + sheet links; UNRESOLVED items are printed.
// Iowa DOT Design 316 (B00169): Ramp G — 3675'-0 x 32'-0 continuous welded girder bridge, four units, joining the
// I-80 WB viaduct at the shared Pier 11WB & 19G. Run after build-i80-wb.csx (same project).
//   sbm run "samples\plans\build-ramp-g.csx" "samples\plans\I-80 WB and Ramp G.sbm"
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Geometry;

Func<string, double> S = Sta;
var wb = Doc.Alignment("I-80 WB");

// ---------------------------------------------------------------- Ramp G alignment (design sheet 12)
// Curve 1: PC 67470+46.89, PI 67502+87.93, D = 147°19'35.72" RT, R = 950', T = 3241.04', PT 67494+89.66
// Curve 2: PC 67497+34.48, PI 67500+51.56, D = 23°07'20.45" LT, R = 1550', T = 317.07', PT 67503+60.00
double d1 = (147 + 19 / 60.0 + 35.72 / 3600) * Math.PI / 180, d2 = (23 + 7 / 60.0 + 20.45 / 3600) * Math.PI / 180;
double R1 = Ft(950), R2 = Ft(1550), T1 = R1 * Math.Tan(d1 / 2), T2 = R2 * Math.Tan(d2 / 2);
// Built backwards from PT 2 in a local frame (ahead tangent of curve 2 = +x), then moved into the I-80 WB frame.
var aheadDir = new Vec2(1, 0);
var pt2 = new Vec2(0, 0);
var pi2 = pt2 - aheadDir * T2;
var back2 = aheadDir.Rotate(-d2);                 // left curve: back heading = ahead - delta
var pc2 = pi2 - back2 * T2;
var pt1 = pc2 - back2 * (S("67497+34.48") - S("67494+89.66"));
var pi1 = pt1 - back2 * T1;
var back1 = back2.Rotate(d1);                     // right curve: back heading = ahead + delta
var pc1 = pi1 - back1 * T1;
double startSta = S("67465+00");
var start = pc1 - back1 * (S("67470+46.89") - startSta);
var end = pt2 + aheadDir * Ft(300);
var local = new[] { (start, 0.0), (pi1, R1), (pi2, R2), (end, 0.0) };
var tmp = new Alignment(new PiDefinition { Pis = local.Select(p => new PiRow(p.Item1.Y, p.Item1.X, p.Item2)).ToList() }.Build(out _)) { StartStation = startSta };

// Ties to I-80 (Ramp G situation plans, design sheets 9-11): best-fit rigid placement of the ramp onto
//   67485+45.28 = CL I-80 STA 7585+96.21 (crossing, angle 79°28'50"), 67502+97.70 = 7602+94.96, 109.63' LT,
//   67503+60.00 = 7603+60.00, 105.08' LT. I-80 offsets are from CL I-80 (WB CL = 41.08' LT).
double pc80 = S("7597+73.11"), Rwb = Ft(2400) - Ft(41.08);
double WbSta(string i80) { double v = S(i80); return v <= pc80 ? v : pc80 + (v - pc80) * Rwb / Ft(2400); }
Vec2 OnI80(string i80, double ltFt)   // point ltFt left of CL I-80 (WB CL is 41.08' left)
{
    double ws = WbSta(i80);
    // on the curve, CL I-80 offsets scale about the curve center; the WB CL is concentric, so offset relative to it:
    return wb.PointAt(ws, -Ft(ltFt - 41.08));
}
var ties = new[]
{
    (S("67485+45.28"), OnI80("7585+96.21", 0)),
    (S("67502+97.70"), OnI80("7602+94.96", 109.63)),
    (S("67503+60.00"), OnI80("7603+60.00", 105.08)),
};
var src = ties.Select(t => tmp.PointAt(t.Item1, 0)).ToArray();
var dst = ties.Select(t => t.Item2).ToArray();
var sc = new Vec2(src.Average(p => p.X), src.Average(p => p.Y));
var dc = new Vec2(dst.Average(p => p.X), dst.Average(p => p.Y));
double num = 0, den = 0;
for (int k = 0; k < src.Length; k++)
{
    var u = src[k] - sc; var v = dst[k] - dc;
    num += u.X * v.Y - u.Y * v.X; den += u.X * v.X + u.Y * v.Y;
}
double rot = Math.Atan2(num, den);
Vec2 Move(Vec2 p) => dc + (p - sc).Rotate(rot);
for (int k = 0; k < src.Length; k++)
    Print($"Ramp G tie {Doc.Units.FormatStation(ties[k].Item1)}: misfit {Doc.Units.FormatLength((Move(src[k]) - dst[k]).Length)}");
Doc.Project.Alignments.RemoveAll(a => a.Name == "Ramp G");
var al = Doc.AddAlignment("Ramp G", startSta, local.Select(p => { var q = Move(p.Item1); return new PiRow(q.Y, q.X, p.Item2); }));
al.Description = "CL Ramp G (Iowa DOT Design 316)";
{
    double cross = S("67485+45.28");
    double ang = (al.AngleAt(cross) - wb.AngleAt(WbSta("7585+96.21"))) * 180 / Math.PI;
    Print($"Crossing angle with I-80: {Math.Abs(Math.IEEERemainder(ang, 180)):F3}° (plans 79.481°)");
}
al.Profile = new VerticalProfile
{
    Vpis =
    {
        new Vpi(S("67461+10"), Ft(983.71), Ft(500)),
        new Vpi(S("67478+75"), Ft(1054.31), Ft(650)),
        new Vpi(S("67488+00"), Ft(1049.68), Ft(300)),
        new Vpi(S("67500+50"), Ft(1020.29), Ft(350)),
        new Vpi(S("67503+60"), Ft(1019.82)),
    },
};
// Superelevation diagram (positive = rise away from the P.G.L. on CL Ramp G).
al.CrossSlope = new CrossSlopeTable
{
    PivotOffset = 0,
    Rows =
    {
        new CrossSlopeRow(S("67468+74.69"), -0.02, 0.02),
        new CrossSlopeRow(S("67470+96.69"), 0.059, -0.059),
        new CrossSlopeRow(S("67494+39.86"), 0.059, -0.059),
        new CrossSlopeRow(S("67497+89.60"), -0.059, 0.059),
    },
};

// ---------------------------------------------------------------- units
const string Structure = "Ramp G (Design 316)";
Doc.Project.Bridges.RemoveAll(b => b.Structure == Structure);
SupportDefinition Sup(string name, SupportType type, double sta, double skew = 0) => new()
{
    Name = name, Type = type, Station = sta, SkewDegrees = skew, GirderEndExtension = In(12), DeckEndOffset = Ft(1.5),
    Fixity = type == SupportType.Abutment ? BearingFixity.Expansion : BearingFixity.Auto,
};
SubstructureDefinition Pier()
{
    var sd = SubstructureDefinition.Default(SupportType.Pier);
    sd.CapWidth = Ft(8); sd.CapDepth = Ft(9); sd.CapEndOverhang = Ft(1.5);
    sd.TopShape = CapTopShape.FollowSeats; sd.BottomShape = CapBottomShape.Level;
    sd.ColumnCount = 1; sd.ColumnSpacings = new();
    sd.ColumnShape = ColumnShape.Rectangular; sd.ColumnWidth = Ft(10); sd.ColumnDepth = Ft(7);
    sd.Foundation = FoundationType.PileCap; sd.FootingLength = Ft(29); sd.FootingWidth = Ft(16); sd.FootingThickness = Ft(5.5);
    sd.PileSection = "HP14x73"; sd.PileRows = 4; sd.PileColumns = 7; sd.PileSpacing = Ft(4.5); sd.PileLength = Ft(100);
    sd.FoundationTopElevation = Ft(967.0);
    sd.MaxPedestalHeight = Ft(3);
    return sd;
}
// Support line through the alignment at station sta with skew (deg, from radial, + = right end up-station), shifted
// parallel by off (normal to it, + = down-station): returns the station where it crosses the alignment and its skew there.
(double sta, double skew) ParallelLine(double sta, double skewDeg, double off)
{
    double k = skewDeg * Math.PI / 180;
    var t0 = Vec2.FromAngle(al.AngleAt(sta));
    var d = al.RightNormalAt(sta) * Math.Cos(k) + t0 * Math.Sin(k);    // pier line direction, left -> right
    var nrm = d.Left;
    if (Vec2.Dot(nrm, t0) < 0) nrm = -nrm;                               // normal pointing up-station
    var q = al.PointAt(sta, 0) - nrm * off;                              // a point on the bearing line
    double s = sta - off / Math.Cos(k);
    for (int it = 0; it < 8; it++)                                       // Newton: (P(s) - q) . nrm = 0
        s -= Vec2.Dot(al.PointAt(s, 0) - q, nrm) / Vec2.Dot(Vec2.FromAngle(al.AngleAt(s)), nrm);
    var ts = Vec2.FromAngle(al.AngleAt(s));
    return (s, Math.Atan2(Vec2.Dot(d, ts), Vec2.Dot(d, al.RightNormalAt(s))) * 180 / Math.PI);
}
BridgeDefinition Unit(string name, double webIn, double haunchIn, (string name, double sta, double skew, string shared, double off)[] sups,
    (int i, double tfW, double tfT, double tfPk, double bfW, double bfT, double bfPk, double peak, double region)[] piers,
    double posTfW, double posTfT, double posBfW, double posBfT)
{
    var b = new BridgeDefinition { Name = name, AlignmentName = al.Name, Structure = Structure };
    b.FieldSplices.Clear();
    b.Bearings.Type = BearingType.Disc;
    b.Deck.Thickness = In(8); b.Deck.Haunch = In(haunchIn);
    // Typical section (sheet 83 / PDF 85): girder A 4'-6 left of CL Ramp G (= P.G.L.), 5'-2 to girder B, 3'-1 overhangs.
    // Confirmed by the girder data radii (A 954'-6 vs CL 950'-0) and spans (Units 2-4 within 0.5"); was 3'-0 (wrong).
    b.Deck.LeftEdgeOffset = -Ft(7 + 7 / 12.0); b.Deck.RightEdgeOffset = Ft(27 + 7 / 12.0);
    b.Deck.Barrier.Type = BarrierType.FShape; b.Deck.Barrier.Height = In(44);
    b.Girders.Count = 4;
    b.Girders.PlanNames = new() { "A", "B", "C", "D" };   // plan girder letters (sheets only)
    b.Girders.Spacings = Enumerable.Repeat(Ft(9 + 8 / 12.0), 3).ToList();
    b.Girders.FirstGirderOffset = -Ft(4.5);
    b.Girders.Section.WebDepth = In(webIn); b.Girders.Section.WebThickness = In(0.625);
    b.Girders.Section.TopFlange = new PlateSize { Width = In(posTfW), Thickness = In(posTfT) };
    b.Girders.Section.BottomFlange = new PlateSize { Width = In(posBfW), Thickness = In(posBfT) };
    for (int k = 0; k < sups.Length; k++)
    {
        var (n, sta, skew, shared, off) = sups[k];
        bool end = k == 0 || k == sups.Length - 1;
        // off = CL bearing to CL pier normal to the pier line, as the framing plans dimension it: the bearing line is the
        // CL pier line shifted PARALLEL by off (not a radial/skewed line at a station off back; on the 950' curve that
        // made the bearing-to-pier distance shrink toward the inside girders, up to 0.47" at D). Find where the shifted
        // line crosses the alignment and the skew it has there.
        var (bSta, bSkew) = off == 0 ? (sta, skew) : ParallelLine(sta, skew, off);
        var sp = Sup(n, n.Contains("Abut") ? SupportType.Abutment : SupportType.Pier, bSta, bSkew);
        if (shared != "") { sp.SharedPier = shared; sp.PierCenterOffset = off; sp.Fixity = BearingFixity.Expansion; }
        sp.Substructure = sp.Type == SupportType.Pier ? Pier() : null;
        b.Supports.Add(sp);
    }
    var plates = new List<PlateSegment>();
    foreach (var plate in new[] { GirderPlate.TopFlange, GirderPlate.BottomFlange })
    {
        bool top = plate == GirderPlate.TopFlange;
        foreach (var p in piers)
        {
            double s = b.Supports[p.i].Station;
            plates.Add(new PlateSegment { Plate = plate, EndStation = s - p.region, Width = In(top ? posTfW : posBfW), Thickness = In(top ? posTfT : posBfT) });
            plates.Add(new PlateSegment { Plate = plate, EndStation = s - p.peak, Width = In(top ? p.tfW : p.bfW), Thickness = In(top ? p.tfT : p.bfT) });
            plates.Add(new PlateSegment { Plate = plate, EndStation = s + p.peak, Width = In(top ? p.tfW : p.bfW), Thickness = In(top ? p.tfPk : p.bfPk) });
            plates.Add(new PlateSegment { Plate = plate, EndStation = s + p.region, Width = In(top ? p.tfW : p.bfW), Thickness = In(top ? p.tfT : p.bfT) });
            if (plate == GirderPlate.TopFlange)
            {
                b.FieldSplices.Add(new FieldSpliceDefinition { Station = s - p.region });
                b.FieldSplices.Add(new FieldSpliceDefinition { Station = s + p.region });
            }
        }
        plates.Add(new PlateSegment { Plate = plate, Width = In(top ? posTfW : posBfW), Thickness = In(top ? posTfT : posBfT) });
    }
    b.Girders.Section.PlateChanges = plates;
    Doc.Project.Bridges.Add(b);
    return b;
}
double Dms(int d, int m, double s) => d + m / 60.0 + s / 3600;
double J = Ft(1.5); // CL bearing to CL pier at the radial expansion piers 5G/9G/14G ("1'-6 (at pier)", framing plans)
Unit("Ramp G Unit 1", 82, 3.5, new[]
{
    ("South Abut", S("67466+22.70"), 0.0, "", 0.0), ("Pier 1G", S("67467+67.70"), 0, "", 0), ("Pier 2G", S("67469+57.70"), 0, "", 0),
    ("Pier 3G", S("67471+47.70"), 0, "", 0), ("Pier 4G", S("67473+37.70"), 0, "", 0), ("Pier 5G", S("67474+87.70"), 0, "Pier 5G", J),
}, new[]
{
    (1, 20.0, 1.0, 1.5, 20.0, 1.25, 1.5, Ft(19), Ft(50)), (2, 20.0, 1.25, 2.0, 20.0, 1.25, 2.0, Ft(19), Ft(50)),
    (3, 20.0, 1.0, 2.0, 20.0, 1.25, 2.0, Ft(17), Ft(50)), (4, 20.0, 1.0, 2.0, 20.0, 1.25, 2.0, Ft(17), Ft(50)),
}, 18, 0.875, 18, 0.875);
Unit("Ramp G Unit 2", 82, 3.5, new[]
{
    ("Pier 5G", S("67474+87.70"), 0.0, "Pier 5G", -J), ("Pier 6G", S("67476+62.70"), 0, "", 0), ("Pier 7G", S("67478+75.70"), 0, "", 0),
    ("Pier 8G", S("67480+77.70"), 0, "", 0), ("Pier 9G", S("67482+37.70"), 0, "Pier 9G", J),
}, new[]
{
    (1, 26.0, 1.25, 1.75, 26.0, 1.25, 1.75, Ft(17), Ft(50)), (2, 24.0, 1.0, 1.25, 24.0, 1.0, 1.25, Ft(17), Ft(50)),
    (3, 22.0, 1.0, 1.25, 22.0, 1.0, 1.25, Ft(17), Ft(50)),
}, 18, 0.875, 20, 1.0);
Unit("Ramp G Unit 3", 92, 4.0, new[]
{
    ("Pier 9G", S("67482+37.70"), 0.0, "Pier 9G", -J), ("Pier 10G", S("67484+37.70"), 0, "", 0), ("Pier 11G", S("67486+92.70"), 0, "", 0),
    ("Pier 12G", S("67489+12.70"), 0, "", 0), ("Pier 13G", S("67491+37.70"), 0, "", 0), ("Pier 14G", S("67493+17.70"), 0, "Pier 14G", J),
}, new[]
{
    (1, 30.0, 1.375, 2.0, 30.0, 1.375, 2.0, Ft(19.5), Ft(65)), (2, 26.0, 1.25, 2.0, 26.0, 1.25, 2.0, Ft(19.5), Ft(65)),
    (3, 24.0, 1.25, 1.625, 24.0, 1.25, 1.625, Ft(19.5), Ft(50)), (4, 24.0, 1.25, 2.0, 24.0, 1.25, 2.0, Ft(19.5), Ft(50)),
}, 20, 0.875, 22, 1.125);
var u4 = Unit("Ramp G Unit 4", 92, 3.5, new[]
{
    ("Pier 14G", S("67493+17.70"), 0.0, "Pier 14G", -J), ("Pier 15G", S("67494+84.70"), 0, "", 0),
    ("Pier 16G", S("67496+82.70"), Dms(56, 32, 38), "", 0), ("Pier 17G", S("67499+40.70"), Dms(49, 0, 0), "", 0),
    // 19G: CL BRG to CL pier 1'-8 normal to the pier (framing plan, sheet 99 / PDF 101); span 19G was 6" long with 1'-6.
    ("Pier 18G", S("67501+45.70"), 0, "", 0), ("Pier 19G", S("67502+97.70"), Dms(32, 55, 29), "Pier 11WB & 19G", Ft(1 + 8 / 12.0)),
}, new[]
{
    (1, 24.0, 1.0, 1.25, 24.0, 1.0, 1.25, Ft(18), Ft(60)), (2, 32.0, 1.375, 2.0, 32.0, 1.375, 2.0, Ft(19), Ft(70)),
    (3, 30.0, 1.375, 2.25, 30.0, 1.375, 2.25, Ft(19), Ft(60)), (4, 24.0, 1.0, 1.25, 24.0, 1.0, 1.25, Ft(19), Ft(60)),
}, 18, 0.875, 22, 1.125);
// Shared piers are built by the first unit that has them; the later unit's copy is off.
foreach (var b in Doc.Project.Bridges.Where(b => b.Structure == Structure).Skip(1))
    b.Supports[0].Substructure = new SubstructureDefinition { Enabled = false };
u4.Supports[^1].Substructure = new SubstructureDefinition { Enabled = false };   // Pier 11WB & 19G is built by I-80 WB Unit 2
foreach (var b in Doc.Project.Bridges.Where(b => b.Structure == Structure))
    if (b.Supports[0].Type == SupportType.Abutment)
    {
        var sd = SubstructureDefinition.Default(SupportType.Abutment);
        sd.Foundation = FoundationType.PileCap; sd.PileSection = "HP14x73"; sd.PileLength = Ft(100);
        b.Supports[0].Substructure = sd;
    }

var r = Doc.Regenerate();
Print($"Regenerated {r.Bridges} bridges in {r.Elapsed.TotalSeconds:F1} s: {r.Parts:N0} parts, {r.Bolts:N0} bolts, {r.SteelMassKg / 907.18474:N0} tons steel.");
foreach (var e in r.Errors) Print("ERROR " + e);
foreach (var w in r.Warnings.Where(w => !w.Contains("cross-frame spacing")).Take(30)) Print("warning " + w);
Print($"({r.Warnings.Count(w => w.Contains("cross-frame spacing"))} cross-frame spacing warnings not listed)");
Doc.Save();
