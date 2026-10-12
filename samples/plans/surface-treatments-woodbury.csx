// Iowa DOT Design 817 (Woodbury, I-29 NB over Floyd Blvd) - ARCHITECTURAL SURFACE TREATMENTS (the user's "811" form liner item):
//   1. brick-pattern FORM LINER texture (DS 42 = p.44 "Textured concrete notes", p.15 / p.16 note): barrier back face recesses (parallel to grade),
//      abutment footing (pile cap), abutment diaphragm and wing extension faces (set level);
//   2. INTEGRAL THIN VENEER BRICK (DS 11-12 = p.13-14, bid item 2,124 SF, p.3): pier columns and caps, barrier bump-outs (p.38 D-D / H-H).
// Called by build-woodbury.csx after barrier-woodbury.csx and substructure-woodbury.csx (re-runnable: replaces BridgeDefinition.SurfaceTreatments).
//   sbm run samples\plans\surface-treatments-woodbury.csx "samples\plans\Woodbury I-29 NB over Floyd.sbm"
// The texture itself is not modeled: each region gets a thin relief layer (texture depth / brick projection) and an area; the treatment data
// (pattern, products, orientation, plan quantity) go to the IFC covering's SBM_SurfaceTreatment property set.
using SBM.Core.Bridge;
using SBM.Core.Geometry;

var br = Doc.Project.Bridges.First(b => b.Structure == "I-29 NB over Floyd Blvd (Design 817)");
Doc.Regenerate(includeSteel: false);                     // framing for the positions below
var fr = Doc.Framing(br.Name);
var al = Doc.Alignment(br.AlignmentName);
double ft = Ft(1);
Vec2 D(int k) => fr.Supports[k].Direction;
double SOffset(int k, double e)
{
    double s = e;
    for (int i = 0; i < 40; i++)
    {
        var p = fr.Supports[k].AlignmentPoint + D(k) * s;
        double f = al.Inverse(p).Offset - e;
        if (Math.Abs(f) < 1e-10) break;
        double h = 1e-3, df = (al.Inverse(p + D(k) * h).Offset - al.Inverse(p - D(k) * h).Offset) / (2 * h);
        s -= f / df;
    }
    return s;
}
double SBeam(int k, int j, int g)
{
    var c = fr.Girders[g].Spans[j];
    Numerics.IntersectLines(fr.Supports[k].AlignmentPoint, D(k), c.Start, (c.End - c.Start).Normalized(), out double t, out _);
    return t;
}
double gutter = Ft(63.00), outer = Ft(63.00) + Len("1'-7\"");

var list = new List<SurfaceTreatmentDefinition>();

// ================================================================ 1. FORM LINER (DS 42 / p.44)
const string LinerPattern = "realistic smooth-faced brick wall, brick 2 1/4 x 7 5/8, running bond, horizontal joints horizontal in the final work";
const string LinerProducts = "Fitzgerald Formliners (pattern 16941); Customrock International (12016); Architectural Polymers (101); others by sample to the Iowa DOT Office of Bridges and Structures";
const string LinerItem = "included in 'Structural Concrete (High Performance)' / barrier rail (p.44 notes) - no separate quantity";
// Barrier: the back-face recesses between the rustication strips (p.38 C-C / F-F / H-H 'CONCRETE TEXTURE, SEE DS 42'; p.44 elevations: 'brick
// pattern form liner set parallel to grade'); the strips and the traffic face are plain. Faces = the "liner"-tagged section edges (barrier-woodbury.csx).
list.Add(new SurfaceTreatmentDefinition
{
    Name = "Form liner (barrier)", Kind = SurfaceTreatmentKind.FormLiner, Pattern = LinerPattern, Products = LinerProducts,
    Relief = In(0.25),                                   // "approximate maximum texture depth shall be 1/4 inch" (p.44)
    Orientation = "parallel to grade", QuantityItem = LinerItem, Source = "p.44 / DS 42 textured concrete notes; p.38 / DS 36",
    Regions = { new SurfaceRegion { Name = "barrier back face recesses", Host = SurfaceHost.Barrier, Tag = "liner", Source = "p.38 C-C, F-F, H-H; p.44 elevations" } },
});
// Abutments (p.15 / p.16 note: "abutment footing, abutment diaphragm and wing extension include concrete texture"; p.44 'South abutment, east barrier'
// (north similar) and 'Typical section at abutment'): brick pattern form liner SET LEVEL on (a) the east face (wing outer face = pile cap / diaphragm end,
// 1'-7 outside the gutter line) and (b) the front face from the face of the beam H web to the wing outer face; top = the bottom of the 8 3/4 deck edge
// (p.44 '8 3/4'); bottom = 1'-0 (MIN.) below the top of berm (p.44). Top of berm at the wall = 'B' point + 3 (p.40 section A-A: 3'-0 at 1" per foot
// up to the wall), B from the p.6 berm slope location table, linear between B2 (27.00' RT) and B3 (67.58' RT).
var abut = new SurfaceTreatmentDefinition
{
    Name = "Form liner (abutments)", Kind = SurfaceTreatmentKind.FormLiner, Pattern = LinerPattern, Products = LinerProducts, Relief = In(0.25),
    Orientation = "level", QuantityItem = LinerItem, Source = "p.44 / DS 42; p.15 / p.16 note; p.6 berm table; p.40 section A-A",
};
foreach (var (k, nm, b2, b3, sgn) in new[] { (0, "S. Abut.", 1115.38, 1116.43, 1), (3, "N. Abut.", 1115.70, 1117.75, -1) })
{
    double Berm(double off) => b2 + (b3 - b2) * (off / ft - 27.00) / (67.58 - 27.00);   // ft, at the B points
    double LowZ(double off) => Ft(Berm(off) + 0.25 - 1.0);
    int span = k == 0 ? 0 : 2;
    double sH = SBeam(k, span, 4) + In(3.25);            // outside face of the beam H web (BTD web 6 1/2)
    var lowFront = new SurfaceLevel { Ref = SurfaceLevelRef.Elevation, Points = { new(SOffset(k, Ft(27)), LowZ(Ft(27))), new(SOffset(k, Ft(67.58)), LowZ(Ft(67.58))) } };
    var lowEast = SurfaceLevel.Level(LowZ(outer));
    string why = "1'-0 MIN. below the top of berm (p.44): taken at the 1'-0 minimum";
    abut.Regions.Add(new SurfaceRegion { Name = $"{nm} front face, pile cap", Host = SurfaceHost.Substructure, Support = nm, Elements = { "Footing" }, NameContains = "pile cap",
        FacingN = sgn, SMin = sH, Lower = lowFront, Upper = SurfaceLevel.BelowDeck(In(8.75)), Source = $"p.44 typical section at abutment; {why}" });
    abut.Regions.Add(new SurfaceRegion { Name = $"{nm} front face, diaphragm", Host = SurfaceHost.Diaphragm, Support = nm,
        FacingN = sgn, SMin = sH, Lower = lowFront, Upper = SurfaceLevel.BelowDeck(In(8.75)), Source = "p.44 typical section at abutment (from the face of the beam H web)" });
    abut.Regions.Add(new SurfaceRegion { Name = $"{nm} east face, wing extension + pile cap end", Host = SurfaceHost.Substructure, Support = nm, Elements = { "Wingwall", "WingFooting", "Footing" },
        FacingOffsetSide = 1, Lower = lowEast, Upper = SurfaceLevel.BelowDeck(In(8.75)), Source = $"p.44 south abutment, east barrier (north similar); {why}" });
    abut.Regions.Add(new SurfaceRegion { Name = $"{nm} east face, diaphragm end", Host = SurfaceHost.Diaphragm, Support = nm,
        FacingOffsetSide = 1, Lower = lowEast, Upper = SurfaceLevel.BelowDeck(In(8.75)), Source = "p.44 south abutment, east barrier" });
    Print($"  {nm}: form liner bottom {LowZ(Ft(27)) / ft:F2} (27' RT) .. {LowZ(outer) / ft:F2} (wing outer face), beam H web face S {sH / ft:F3} ft");
}
abut.Unresolved.Add("(p.44) the liner on the wing END faces and behind the MSE wall is not dimensioned (elevation only) - not modeled.");
list.Add(abut);

// ================================================================ 2. INTEGRAL THIN VENEER BRICK (DS 11-12 / p.13-14; bid item 2,124 SF, p.3)
var brick = new SurfaceTreatmentDefinition
{
    Name = "Integral thin veneer brick", Kind = SurfaceTreatmentKind.ThinBrick,
    Pattern = "modular size brick, running bond, 2 1/4 x 7 5/8, mixture of dark brown tones (up to two color lots), velour texture",
    Products = "per the special provisions for integral thin veneer brick for structural concrete (p.13 notes)",
    Relief = In(0.375),                                  // the 3/8 (TYP.) offset between the brick face and the plain feature bands (p.13 Details A / B)
    Orientation = "level on the columns, parallel to the cap slope on the cap faces, parallel to grade on the barrier",
    PlanQuantity = 2124 * Math.Pow(ft, 2), QuantityItem = "2599-9999014 Integral thin veneer brick for structural concrete, 2,124 SF (p.3)",
    Source = "p.13 / DS 11, p.14 / DS 12; p.38 / DS 36 D-D, H-H",
};
var pierData = new[]
{
    (k: 1, J: 1117.25, K: 1118.76, L: 1112.36, M: 1113.01, N: 1113.65, mask: Len("4'-11\"")),
    (k: 2, J: 1117.42, K: 1119.54, L: 1112.57, M: 1113.48, N: 1114.39, mask: Len("5'-0\"")),
};
double colEnd = Len("2'-11 13/16\""), colSp = Len("18'-1\""), capLen = Len("42'-1 5/8\""), colHalf = (Len("5'-3 5/8\"") + 2 * In(4)) / 2;
double b716 = In(8 + 7.0 / 16);                          // plain bands at the cap top / bottom (p.13 Detail A: 8 7/16 + 3'-7 1/8 + 8 7/16 = 5'-0)
foreach (var pd in pierData)
{
    var sd = br.Supports[pd.k].Substructure!;
    double sW = sd.CapProfile[0].S, sE = sW + capLen, sL = sd.FirstColumnOffset, sM = sL + colSp, sN = sL + 2 * colSp;
    string P = $"Pier {pd.k}";
    double J = Ft(pd.J), K = Ft(pd.K);
    double Top(double s) => J + (K - J) * (s - sW) / (sE - sW);
    double Bot(double s) => s <= sM ? Ft(pd.L) + (Ft(pd.M) - Ft(pd.L)) * (s - sL) / (sM - sL) : Ft(pd.M) + (Ft(pd.N) - Ft(pd.M)) * (s - sM) / (sN - sM);
    // Columns (p.13 elevation, Details A / B, View A-A, Section B-B): brick on all sides, LEVEL courses, from ELEV. 1101.00 up to 2" (MIN.) below the cap
    // soffit (4 1/2 MAX.); plain feature bands 2 1/4 high: one 5 3/4 below the top course (Detail A), two near the bottom with 5 3/4 of brick under each
    // (Detail B). The top course is taken 2" below the LOWEST soffit point over the column (the 2" minimum governs); the max gap is checked below.
    for (int c = 0; c < 3; c++)
    {
        double sc = sL + c * colSp;
        double soffLo = Math.Min(Bot(sc - colHalf), Bot(sc + colHalf)), soffHi = Math.Max(Bot(sc - colHalf), Bot(sc + colHalf));
        double T = soffLo - In(2), B0 = Ft(1101.00);
        double gapMax = soffHi - T;
        Print($"  {P} column {c + 2}: brick {B0 / ft:F2} .. {T / ft:F3} (2\" below the soffit low point); gap to the soffit 2\" .. {gapMax / In(1):F2}\"{(gapMax > In(4.5) + In(1.0 / 16) ? " > 4 1/2 MAX - CONFLICT (p.13 Detail A): level courses on this sloped soffit cannot keep the gap within 2 .. 4 1/2" : "")}");
        brick.Regions.Add(new SurfaceRegion
        {
            Name = $"{P} column {c + 2}", Host = SurfaceHost.Substructure, Support = P, Elements = { "Column" }, SMin = sc - Ft(4), SMax = sc + Ft(4),
            Lower = SurfaceLevel.Level(B0), Upper = SurfaceLevel.Level(T),
            Exclude =
            {
                new SurfaceBand { Lower = SurfaceLevel.Level(B0 + In(5.75)), Upper = SurfaceLevel.Level(B0 + In(8)), Source = "p.13 Detail B" },
                new SurfaceBand { Lower = SurfaceLevel.Level(B0 + In(13.75)), Upper = SurfaceLevel.Level(B0 + In(16)), Source = "p.13 Detail B" },
                new SurfaceBand { Lower = SurfaceLevel.Level(T - In(8)), Upper = SurfaceLevel.Level(T - In(5.75)), Source = "p.13 Detail A" },
            },
            Source = "p.13 elevation, Details A / B, Section B-B (wraps all sides)",
            Unresolved = gapMax > In(4.5) + In(1.0 / 16) ? $"p.13 Detail A: the gap below the cap soffit (2 MIN. / 4 1/2 MAX.) cannot hold with level courses here (soffit rise {(soffHi - soffLo) / In(1):F2}\" over the column)" : "",
            Basis = "top course 2\" below the soffit low point (the minimum governs)",
        });
    }
    // Cap long faces (p.13 elevation 'thin veneer brick to match slope of pier cap', Detail A): between 8 7/16 plain bands at the top and the bottom, both
    // faces; not on the stage-joint end (p.14 Detail C: the pattern continues through the joint into stage 2).
    brick.Regions.Add(new SurfaceRegion
    {
        Name = $"{P} cap faces", Host = SurfaceHost.Substructure, Support = P, Elements = { "Cap" }, NameContains = "", FacingN = 1, MaxAngleDeg = 30,
        SMin = sW + In(0.1), Lower = new SurfaceLevel { Points = { new(sW, Bot(sW)), new(sL, Bot(sL)), new(sM, Bot(sM)), new(sN, Bot(sN)), new(sE, Bot(sE)) }, Offset = b716 },
        Upper = new SurfaceLevel { Points = { new(sW, Top(sW)), new(sE, Top(sE)) }, Offset = -b716 },
        Source = "p.13 elevation, Detail A (8 7/16 + 3'-7 1/8 + 8 7/16)",
    });
    brick.Regions.Add(new SurfaceRegion
    {
        Name = $"{P} cap back face", Host = SurfaceHost.Substructure, Support = P, Elements = { "Cap" }, FacingN = -1, MaxAngleDeg = 30,
        SMin = sW + In(0.1), Lower = new SurfaceLevel { Points = { new(sW, Bot(sW)), new(sL, Bot(sL)), new(sM, Bot(sM)), new(sN, Bot(sN)), new(sE, Bot(sE)) }, Offset = b716 },
        Upper = new SurfaceLevel { Points = { new(sW, Top(sW)), new(sE, Top(sE)) }, Offset = -b716 },
        Source = "p.13 Detail A note: brick and feature bands wrap all sides of the columns and cap",
    });
    // East end (p.13 View A-A, p.14 Detail E): the end reveal + maskwall end face; brick from the cap bottom band up to 3" (MIN.) below the maskwall top
    // (5 MAX.), Detail E feature bands 2 1/4 at 5 3/4 above K and 5 3/4 below the top course.
    double maskLow = K + pd.mask - In(1.5), topCourse = maskLow - In(3);
    brick.Regions.Add(new SurfaceRegion
    {
        Name = $"{P} east end (cap end, reveal, maskwall)", Host = SurfaceHost.Substructure, Support = P, Elements = { "Cap", "Maskwall" }, FacingS = 1, MaxAngleDeg = 30,
        Lower = SurfaceLevel.Level(Bot(sE) + b716), Upper = SurfaceLevel.Level(topCourse),
        Exclude =
        {
            new SurfaceBand { Lower = SurfaceLevel.Level(K + In(5.75)), Upper = SurfaceLevel.Level(K + In(8)), Source = "p.14 Detail E" },
            new SurfaceBand { Lower = SurfaceLevel.Level(topCourse - In(8)), Upper = SurfaceLevel.Level(topCourse - In(5.75)), Source = "p.14 Detail E" },
        },
        Source = "p.13 View A-A, p.14 Detail E",
        Unresolved = "p.13 View A-A / p.14 Detail E: the band layout on the cap end below K is drawn without a full dimension chain",
        Basis = "brick from the cap bottom band (8 7/16) to 3\" below the low edge of the maskwall top, Detail E bands only",
    });
}
brick.Regions.Add(new SurfaceRegion { Name = "barrier bump-outs (piers 1 and 2)", Host = SurfaceHost.Barrier, Tag = "brick", Source = "p.38 Part Sections D-D / H-H (faces and 3 13/16 returns)" });
brick.Unresolved.Add("(p.13-14) maskwall long-side faces and the column / cap stage-joint end (Detail C, brick continued into stage 2) are not modeled as brick.");
list.Add(brick);

br.SurfaceTreatments = list;
Print($"Woodbury surface treatments set: {string.Join(", ", list.Select(t => $"{t.Name} ({t.Regions.Count} regions)"))}.");
