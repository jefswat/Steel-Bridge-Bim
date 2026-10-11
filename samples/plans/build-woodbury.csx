// Iowa DOT Design 817 (File 30377): I-29 NB over Floyd Blvd, Sioux City (Woodbury County) - STAGE 1.
// 223'-10 7/8 x 36'-0 pretensioned prestressed concrete beam bridge, 3 spans, BTD bulb tees, integral abutments.
// SUPERSTRUCTURE GEOMETRY ONLY: alignment (local coordinates, no datum - U1), profile on the PGL 5.00' RT of CL I-29,
// superelevation, supports (all parallel, 10 deg 09'36" to the extended tangent), deck, per-span beam layout + haunches.
// Plan data: docs/plan-data/woodbury-i29-floyd/SUMMARY.md (sections 1-3) and its CSVs.
//   sbm run samples\plans\build-woodbury.csx -- "samples\plans\Woodbury I-29 NB over Floyd.sbm"
//   sbm run samples\plans\check-woodbury.csx "samples\plans\Woodbury I-29 NB over Floyd.sbm"
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Geometry;

string outPath = Args.Length > 0 ? Args[0] : (Doc.FilePath ?? @"samples\plans\Woodbury I-29 NB over Floyd.sbm");
Func<string, double> S = Sta;
double Deg(int d, int m, double s) => (d + m / 60.0 + s / 3600.0) * Math.PI / 180;
var unresolved = new List<string>();

// ---------------------------------------------------------------- CL I-29 (situation plan, PDF p.6 / DS 4)
// PI STA 469+93.34, Delta = 29 deg 19'58.71" LT, R = 2320.00' (T 607.19, L 1187.74, PC 463+86.15, PT 475+73.89), no spirals,
// no station equations. Local frame (U1: no coordinates in the set): PI at (N 10000, E 10000), back tangent heading north.
double R = Ft(2320.00), delta = Deg(29, 19, 58.71), T = R * Math.Tan(delta / 2);
double piSta = S("469+93.34"), startSta = S("458+00"), endPast = Ft(300);
var back = new Vec2(0, 1);                       // heading (x = east, y = north)
var ahead = back.Rotate(delta);                  // left curve
var pi = new Vec2(Ft(10000), Ft(10000));
var pStart = pi - back * (piSta - startSta);
var pEnd = pi + ahead * (T + endPast);
Doc.Project.Alignments.RemoveAll(a => a.Name == "CL I-29");
var al = Doc.AddAlignment("CL I-29", startSta, new[]
{
    new PiRow(pStart.Y, pStart.X), new PiRow(pi.Y, pi.X, R), new PiRow(pEnd.Y, pEnd.X),
});
al.Description = "CL I-29 (Iowa DOT Design 817, local coordinates - no datum in the plan set)";
Print($"PC {Doc.Units.FormatStation(piSta - T)} (plan 463+86.15), T {T / 0.3048:F2}' (plan 607.19), L {R * delta / 0.3048:F2}' (plan 1187.74)");
unresolved.Add("U1: horizontal coordinates / datum of CL I-29 not in the plan set (PDF p.6) - model in local coordinates (PI at N 10000', E 10000', back tangent north).");

// Profile on the P.G.L. I-29 N.B. (5.00' RT of CL I-29): PVI 462+50.00 elev 1124.11, VC 700', G1 +1.9791 %, G2 -0.6143 %.
double pviSta = S("462+50.00"), pviZ = Ft(1124.11), g1 = 0.019791, g2 = -0.006143;
double endSta = piSta + T + endPast + (R * delta - 2 * T);   // approximate end station (beyond the bridge)
al.Profile = new VerticalProfile
{
    Vpis =
    {
        new Vpi(startSta, pviZ - g1 * (pviSta - startSta)),
        new Vpi(pviSta, pviZ, Ft(700)),
        new Vpi(endSta, pviZ + g2 * (endSta - pviSta)),
    },
};
// Superelevation (p.6): 2 % at 462+89.05, linear to 5 % at 464+83.05, 5 % beyond; one plane rising to the right (east)
// across the stage-1 deck; pivot = PGL NB 5.00' RT of CL I-29 (typical section p.18: CL I-29 -5'-0- PGL NB -22'-0- CL NB).
al.CrossSlope = new CrossSlopeTable
{
    PivotOffset = Ft(5.00),
    Rows =
    {
        new CrossSlopeRow(S("462+89.05"), -0.02, 0.02),
        new CrossSlopeRow(S("464+83.05"), -0.05, 0.05),
    },
};
unresolved.Add("U2: the cross-slope pivot / profile grade line = PGL NB 5.00' RT of CL I-29 is DERIVED (typical section dimensions p.18, reproduces p.6 / p.33 to 0.01'); not stated as text.");

// ---------------------------------------------------------------- support lines (staking diagram p.7 / DS 5)
// All support lines parallel, 10 deg 09'36" to the normal of the EXTENDED back tangent (L.A.); the right (east) end is
// back-station. Staking points on the extended tangent of CL NB roadway (27.00' RT): CL S abut brg STA 463+39.99, then
// the spans along the extended tangent 55'-11 7/8, 106'-4 5/8, 60'-1 13/16 (p.7).
double skew0 = Deg(10, 9, 36);
var supDir = back.Right.Rotate(-skew0);             // left -> right
var p0 = al.PointAt(S("463+39.99"), Ft(27.00));
double[] tangentSpans = { Len("55'-11 7/8\""), Len("106'-4 5/8\""), Len("60'-1 13/16\"") };
var stakes = new List<Vec2> { p0 };
foreach (var s in tangentSpans) stakes.Add(stakes[^1] + back * s);

// The support line through a staking point: station where it crosses CL I-29 and its skew from the local radial there.
(double sta, double skew) Line(Vec2 q)
{
    var n = supDir.Left;
    double s = al.Inverse(q).Station;
    for (int it = 0; it < 30; it++)
    {
        double f = Vec2.Dot(al.PointAt(s, 0) - q, n);
        s -= f / Vec2.Dot(Vec2.FromAngle(al.AngleAt(s)), n);
        if (Math.Abs(f) < 1e-11) break;
    }
    var r = al.RightNormalAt(s);
    var t = Vec2.FromAngle(al.AngleAt(s));
    return (s, Math.Atan2(Vec2.Dot(supDir, t), Vec2.Dot(supDir, r)) * 180 / Math.PI);
}
// Point where a support line crosses the CL NB roadway (offset curve 27.00' RT), as an alignment station.
double OnClNb(Vec2 q)
{
    var n = supDir.Left;
    double s = al.Inverse(q).Station;
    for (int it = 0; it < 30; it++)
    {
        double f = Vec2.Dot(al.PointAt(s, Ft(27)) - q, n);
        var h = 1e-4;
        double df = (Vec2.Dot(al.PointAt(s + h, Ft(27)) - q, n) - Vec2.Dot(al.PointAt(s - h, Ft(27)) - q, n)) / (2 * h);
        s -= f / df;
        if (Math.Abs(f) < 1e-11) break;
    }
    return s;
}
// Distance along CL NB roadway between the deck end and the CL abutment bearing (p.6/p.22) -> normal to the support line.
double DeckEnd(Vec2 q, double alongClNb)
{
    double s = OnClNb(q);
    var tNb = Vec2.FromAngle(al.AngleAt(s));
    return alongClNb * Math.Abs(Vec2.Dot(tNb, supDir.Left));
}

const string Structure = "I-29 NB over Floyd Blvd (Design 817)";
Doc.Project.Bridges.RemoveAll(b => b.Structure == Structure);
var br = new BridgeDefinition { Name = "I-29 NB over Floyd Blvd - Stage 1", AlignmentName = al.Name, Structure = Structure };
br.FieldSplices.Clear();
string[] names = { "S. Abut.", "Pier 1", "Pier 2", "N. Abut." };
for (int k = 0; k < 4; k++)
{
    var (sta, skew) = Line(stakes[k]);
    bool abut = k == 0 || k == 3;
    var sub = SubstructureDefinition.Default(abut ? SupportType.Abutment : SupportType.Pier);
    sub.Enabled = false;      // superstructure geometry only (substructure: later step)
    br.Supports.Add(new SupportDefinition
    {
        Name = names[k], Type = abut ? SupportType.Abutment : SupportType.Pier, Station = sta, SkewDegrees = skew, Substructure = sub,
        DeckEndOffset = k == 0 ? DeckEnd(stakes[k], Len("1'-6 1/4\"")) : k == 3 ? DeckEnd(stakes[k], Len("1'-6 5/8\"")) : Ft(1.5),
    });
    Print($"{names[k]}: CL I-29 STA {Doc.Units.FormatStation(sta)}, skew {skew:F5} deg from the local radial (right end back-station)");
}

// ---------------------------------------------------------------- deck (p.18, p.22, general notes p.5)
// West edge = CL NB roadway (curved, R 2347.00' = 27.00' RT of CL I-29) = stage-2 construction joint; 36'-0 roadway to the
// east gutter line + 1'-7 barrier = 37'-7 out to out. Structural deck 8" (incl. 1/2" integral wearing surface); nominal
// thickness at the beams 9" = 8" + 1" haunch (p.24). Deck edge 8 3/4" thick (p.18).
br.Deck.LeftEdgeOffset = Ft(27.00);
br.Deck.RightEdgeOffset = Ft(27.00) + Len("37'-7\"");
br.Deck.Thickness = In(8);
br.Deck.Haunch = In(1);
br.Deck.OverhangThickness = In(8.75);
br.Deck.StayInPlaceForms = false;
br.Deck.Reinforcement.Enabled = false;   // deck bars: later step (bar lists in bar-lists.csv, bend dims U9)
br.Deck.Barrier.Type = BarrierType.None; // Iowa aesthetic barrier east side only: profile not transcribed (U10)
br.Deck.Barrier.Left = false;
unresolved.Add("U10: Iowa aesthetic barrier (east side, 3'-8 x 1'-7) profile not fully transcribed (PDF p.38) - no barrier modeled.");

// ---------------------------------------------------------------- beams (beam layout p.21 / DS 19)
// 5 beam lines D, E, F, G, H (west to east; A-C are stage 2). Iowa BTD beams: depth 4'-6. PLACEHOLDER shape = Tx54
// (54" deep, same depth) until the Iowa BTx catalog lands (prestressed queue item 1); strands / standard lengths there too.
br.Girders.Count = 5;
br.Girders.Kind = GirderKind.TxDotPrestressed;
br.Girders.Prestressed.Type = TxGirderType.Tx54;
br.Girders.Prestressed.StrandCount = 14;
br.Girders.Prestressed.StrandDiameter = In(0.6);
br.Girders.Prestressed.BearingToEnd = In(8);    // end of beam to CL bearing 8" (p.28-30)
br.Girders.Prestressed.EndGapAtBent = In(4);    // beam ends 8" apart over the piers (Detail A p.20), centred on CL pier
br.Girders.Prestressed.PadLength = In(9);       // pier pads 9 x 1 x 2'-4 1/2 plain neoprene (p.20); abutments: 3x3 bar (not modeled)
br.Girders.Prestressed.PadWidth = Len("2'-4 1/2\"");
br.Girders.Prestressed.PadThickness = In(1);
// Default offsets (unused: every span has a span layout) - only the starting guesses of the framing.
br.Girders.FirstGirderOffset = Ft(30);
br.Girders.Spacings = Enumerable.Repeat(Ft(8), 4).ToList();
br.Girders.PlanLayout = GirderPlanLayout.Chorded;

// Per-span layout. Beam D "measured along CL bearing" from CL NB roadway (W.P. = CL pier x CL NB roadway); the pier
// bearing lines are 1'-0 each side of CL pier measured along CL NB roadway; the other beams parallel to beam D at the span's
// spacing "measured perpendicular to CL beam". Haunch at each bearing = deck thickness at beams T (p.24) - 8" slab.
GirderSpanEnd End(double shift, string beamD, double[] tIn) => new()
{
    LineShift = shift, Measure = SpanOffsetMeasure.AlongLine, ReferenceOffset = Ft(27.00),
    Offsets = { Len(beamD) }, Haunches = tIn.Select(t => In(t - 8)).ToList(),
};
double F(int whole, int num = 0, int den = 1) => whole + (double)num / den;
// T at the bearings per beam D..H (p.24 / DS 22): S abut, pier 1 back, pier 1 ahead, pier 2 back, pier 2 ahead, N abut.
double[][] tBrg =
{
    new[] { F(9), F(9, 7, 8), F(10), F(10), F(9, 7, 8), F(9, 1, 8) },              // D
    new[] { F(9), F(9, 3, 4), F(9, 7, 8), F(9, 7, 8), F(9, 3, 4), F(9, 1, 8) },    // E
    new[] { F(9), F(9, 5, 8), F(9, 3, 4), F(9, 3, 4), F(9, 3, 4), F(9, 1, 8) },    // F
    new[] { F(9, 1, 8), F(9, 1, 2), F(9, 3, 4), F(9, 3, 4), F(9, 3, 4), F(9, 1, 8) }, // G
    new[] { F(9, 1, 8), F(9, 5, 8), F(9, 7, 8), F(9, 7, 8), F(9, 7, 8), F(9, 1, 8) }, // H
};
double[] Col(int c) => tBrg.Select(r => r[c]).ToArray();
double one = Ft(1);
br.Girders.SpanLayouts = new()
{
    new GirderSpanLayout
    {
        Span = 0, Count = 5, Begin = End(0, "3'-1 11/16\"", Col(0)), End = End(-one, "3'-3 1/2\"", Col(1)),
        PerpendicularSpacings = Enumerable.Repeat(Len("8'-0 5/16\""), 4).ToList(),
    },
    new GirderSpanLayout
    {
        Span = 1, Count = 5, Begin = End(one, "2'-10 15/16\"", Col(2)), End = End(-one, "3'-0 11/16\"", Col(3)),
        PerpendicularSpacings = Enumerable.Repeat(Len("7'-11 13/16\""), 4).ToList(),
    },
    new GirderSpanLayout
    {
        Span = 2, Count = 5, Begin = End(one, "2'-7 3/8\"", Col(4)), End = End(0, "2'-8 1/16\"", Col(5)),
        PerpendicularSpacings = Enumerable.Repeat(Len("7'-11\""), 4).ToList(),
    },
};
br.CrossFrames.MaxSpacing = Ft(25);
Doc.Project.Bridges.Add(br);

unresolved.Add("U3 (closed by the user): beams are standard BTD55 / BTD105 / BTD60 at the STANDARD lengths - the beam generator change belongs to the prestressed work; this model's beams follow the layout chords (placeholder Tx54 shape).");
unresolved.Add("U4: plan skews to the local tangent at Pier 2 (13 deg 00'40\") and N. Abut. (14 deg 29'57\") do not reproduce from the parallel 10 deg 09'36\" (PDF p.21 vs p.7); the parallel lines through the staking points govern.");
unresolved.Add("Beam ends at the piers (square or parallel to the pier) and abutment end details (integral, beams embedded) - prestressed/abutment work (PDF p.20, p.28-30).");

// ---------------------------------------------------------------- concrete end / pier diaphragms + fillers / keyways (diaphragms-woodbury.csx, run from the repo root)
{
    var dr = await SBM.Api.ScriptHost.RunFileAsync(@"samples\plans\diaphragms-woodbury.csx", Doc, Args);
    Print(dr.Output.TrimEnd());
    if (!dr.Success) Print("ERROR in diaphragms-woodbury.csx: " + dr.Error);
}

var rep = Doc.Regenerate(includeSteel: false);
foreach (var e in rep.Errors) Print("ERROR " + e);
foreach (var w in rep.Warnings.Take(30)) Print("warning " + w);
foreach (var u in unresolved) Print("UNRESOLVED " + u);
Doc.Save(outPath);
Print($"Saved {outPath}");
