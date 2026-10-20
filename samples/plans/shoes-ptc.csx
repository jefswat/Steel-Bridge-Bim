// PTC / NJTA DELAWARE RIVER BRIDGE (1954-57, AS-BUILT) - SHOES PIECE BY PIECE (replaces the trapezoidal body envelopes of the truss-generator shoes):
//   girder spans 1-10 / 28-31 (p.44: fixed = sole, stem, 2 wings, 2 stiffeners; expansion = sole + rocker 17 5/8 x 9 x 3'-0, R 1'-6),
//   approach trusses: fixed L8 / L20 (p.52 / p.67: sole, 2 stems, 3 wings, 4 end stiffeners), unit-end expansion L0 / L0' (p.59), intermediate rockers
//   pier 12 / 25 (p.49) and piers 14 / 16 / 21 / 23 (p.63) (sole, stem, 2 wings, 2 stiffeners, rocker base on a bearing plate),
//   main spans: L0 / L0' rocker shoes (p.73), L11 fixed cast shoe (pier 18) and L11' expansion roller nest (pier 19) (p.96: cast steel base / walls / ribs,
//   steel forging top, five segmental forged rollers).
// Generic input: TrussShoeDefinition.Pieces (TrussShoePiece: profile in the shoe frame swept across / along / up, copies, mirrors).
// Re-runnable; run AFTER truss-ptc-arch, truss-ptc-approach, girders-ptc, deck-ptc, connections-ptc (they rebuild Truss):
//   sbm run samples\plans\shoes-ptc.csx "samples\plans\PTC Delaware River Bridge.sbm"
// Shoe frame: origin = pin centre (girder shoes: the W.P. on the sole plate top), u along the bridge, n across (right +), z up; all values below in INCHES.
// Data: docs/plan-data/ptc-delaware-river/connections/shoes.csv, deck/bearings-extra.csv (+ the drawings of the same pages; positions read from the
// drawings are flagged as dimension-only where the sheet does not dimension them). Differences are reported, never adjusted.
using SBM.Core;
using SBM.Core.Bridge;
using SBM.Core.Steel;

string outPath = Args.Length > 0 ? Args[0] : (Doc.FilePath ?? @"samples\plans\PTC Delaware River Bridge.sbm");
string U(string pages, string item, string why, string then = "") => UnresolvedFlag.Text(pages, item, why, then);
double FI(double ft, double inch) => Ft(ft) + In(inch);

List<TrussUV> Poly(params double[] xy) { var l = new List<TrussUV>(); for (int i = 0; i + 1 < xy.Length; i += 2) l.Add(new TrussUV(In(xy[i]), In(xy[i + 1]))); return l; }
List<TrussUV> Rect(double x0, double x1, double y0, double y1) => Poly(x0, y0, x1, y0, x1, y1, x0, y1);
// arc points (inches) on a circle of radius r about (0, 0) from angle a0 to a1 (degrees), n steps, endpoints included
IEnumerable<double> Arc(double r, double a0, double a1, int n = 12)
{
    for (int i = 0; i <= n; i++) { double a = (a0 + (a1 - a0) * i / n) * Math.PI / 180; yield return r * Math.Cos(a); yield return r * Math.Sin(a); }
}
// polygon whose top edge at zTop (below / near the pin centre) is notched by the pin seat of radius r (pin through the plate / casting)
List<TrussUV> SeatTop(double[] lower, double halfTop, double zTop, double r)
{
    // lower = polygon points (x, z) from the top-left going down / around to the top-right corner (counter-clockwise), the top edge is added with the seat
    var pts = new List<double>(lower);
    pts.AddRange(new[] { halfTop, zTop });
    double a0 = Math.Asin(Math.Clamp(zTop / r, -1, 1)) * 180 / Math.PI;
    pts.AddRange(Arc(r, a0, -180 - a0));
    pts.AddRange(new[] { -halfTop, zTop });
    return Poly(pts.ToArray());
}
// rocker / roller base: flat top at zTop between +-half, circular bottom of radius R about the pin centre
List<TrussUV> RockerBase(double half, double zTop, double R)
{
    double a = Math.Asin(half / R) * 180 / Math.PI;     // angle from the vertical
    var pts = new List<double> { -half, zTop, half, zTop };
    pts.AddRange(Arc(R, -90 + a, -90 - a, 16));
    return Poly(pts.ToArray());
}
TrussShoePiece P(string name, TrussShoePieceKind kind, TrussShoePieceAxis axis, List<TrussUV> prof, double from, double to, string mat, string desc,
    bool mirrorN = false, bool mirrorU = false, double[]? shiftsX = null, string flag = "") => new TrussShoePiece
{
    Name = name, Kind = kind, Axis = axis, Profile = prof, From = In(from), To = In(to), Material = mat, Description = desc, MirrorN = mirrorN, MirrorU = mirrorU,
    ShiftsProfileX = shiftsX?.Select(In).ToList() ?? new(), Unresolved = flag,
};
const TrussShoePieceAxis Across = TrussShoePieceAxis.Across, Along = TrussShoePieceAxis.Along;
const TrussShoePieceKind Body = TrussShoePieceKind.Body, Plate = TrussShoePieceKind.Plate, Sole = TrussShoePieceKind.Sole, Rocker = TrussShoePieceKind.Rocker, Roller = TrussShoePieceKind.Roller;

string bevel(string pg) => U(pg, "sole plate bevel", "the sole plate is beveled to the grade, only its thickness on the CL is listed", "level plate of the listed CL thickness.");
var report = new List<string>();
int shoes = 0;

// ================================================================ girder spans (p.44), reference = W.P. (top of the sole plate on the girder CL)
foreach (var bn in new[] { "PTC Girder spans 1-10", "PTC Girder spans 28-31" })
{
    var t = Doc.Project.Bridges.First(b => b.Name == bn).Truss!;
    foreach (var s in t.Shoes)
    {
        var pc = new List<TrussShoePiece> { P("sole plate", Sole, Across, Rect(-5.5, 5.5, -3, 0), -9, 9, "C", "PL 11 x 4 3/4 x 1'-6 beveled (3 in. on the CL)", flag: bevel("p.44")) };
        if (s.Type == TrussShoeType.Expansion)
        {
            // rocker 17 5/8 x 9 x 3'-0: top 1 3/4 R nose on the pin centre (3 in. under the W.P.), bottom R 1'-6 about the pin centre, 1/2 in. chamfers at the top corners
            var rock = new List<double> { -4.5, -5, -3, -3, 3, -3, 4.5, -5 };
            double a = Math.Asin(4.5 / 18) * 180 / Math.PI;
            foreach (var (x, z) in Arc(18, -90 + a, -90 - a, 12).Chunk(2).Select(c => (c[0], c[1] - 3))) { rock.Add(x); rock.Add(z); }
            pc.Add(P("rocker", Rocker, Across, Poly(rock.ToArray()), -18, 18, "C", "17 5/8 x 9 x 3'-0, R 1'-6"));
            s.Unresolved = U("p.44", "expansion shoe key and dowels", "the 3 x 1 x 5 1/2 key and the two 2 in. stainless dowels are listed; the rocker nose groove (1 3/4 R) is drawn", "not built; rocker top flat under the sole plate.");
        }
        else
        {
            pc.Add(P("stem plate", Body, Across, Rect(-4.5, 4.5, -20.5, -3), -9, 9, "C", "PL 18 x 9 x varies"));
            pc.Add(P("wing plate", Plate, Across, Poly(-9, -20.5, 9, -20.5, 4.5, -5.5, -4.5, -5.5), 9, 10, "C", "PL 15 x 1 x 1'-10", mirrorN: true,
                flag: U("p.44", "wing plate outline", "the wing plates 15 x 1 x 1'-10 are drawn as trapezoids without the top width", "15 in. high, 1'-6 at the masonry plate tapering to the 9 in. stem.")));
            pc.Add(P("stiffener plate", Plate, Along, Poly(10, -20.5, 17, -20.5, 17, -17.5, 10, -8), -0.5, 0.5, "C", "PL 7 x 1", mirrorN: true,
                flag: U("p.44", "stiffener plate outline", "the two 7 x 1 stiffeners are listed without a length and drawn as sloped triangles on the CL bearing", "outside each wing plate on the CL bearing, 12 1/2 in. high, sloped edge.")));
            s.Unresolved = U("p.44", "fixed shoe key", "the 3 x 1 x 5 1/2 key is listed", "not built.");
        }
        s.Pieces = pc;
        shoes++;
    }
}

// ================================================================ approach trusses
foreach (var bn in new[] { "PTC 3-span truss 11-13", "PTC 4-span truss 14-17", "PTC 4-span truss 21-24", "PTC 3-span truss 25-27" })
{
    var t = Doc.Project.Bridges.First(b => b.Name == bn).Truss!;
    bool four = bn.Contains("4-span");
    foreach (var s in t.Shoes)
    {
        var pc = new List<TrussShoePiece>();
        double total = s.PinBelowPoint + s.Height;   // bottom chord W.P. to the bottom of the shoe (from the model / printed seat)
        if (s.Type == TrussShoeType.Fixed)
        {
            // p.52 / p.67: pin 3'-0 1/4 above the bottom, masonry 6 in. (top 2'-6 1/4 below the pin), 3 wing plates 4 in. (centre + 2 outer at 1'-1 clear), 2 stems 13 x 9
            string pg = four ? "p.67" : "p.52";
            // CORRECTION rows (shoes.csv, third pass 2026-10-20): p.67 is identical to p.52 - sole 10 3/16 (not 10 7/16), wings 29 7/8 (not 29 3/4), stems 2'-5 7/8 (not 5 5/8 / 5 3/8)
            double soleT = 10.1875, wingH = 29.875;
            s.MasonryLength = FI(5, 6); s.MasonryWidth = In(60);    // 'Masonry Pl. 60 x 6 x 5'-6': 5'-6 along the truss (2'-9 + 2'-9 about the CL brg), 60 in. across (2'-6 + 2'-6)
            pc.Add(P("sole plate", Sole, Across, Rect(-11, 11, 0.1875, 0.1875 + soleT), -19, 19, "S", "PL 22 x 10 3/16 (on CL) x 3'-2", flag: bevel(pg)));
            pc.Add(P("stem plate", Body, Across, SeatTop(new double[] { -4.5, -30.25, 4.5, -30.25 }, 4.5, -30.25 + 29.875, 2.75), 2, 15, "C", "PL 13 x 9 x 2'-5 7/8", mirrorN: true));
            var wing = SeatTop(new double[] { -31.75, -30.25, 31.75, -30.25 }, 5.5, -30.25 + wingH, 2.75);
            string wingFlag = U(pg, "wing plate top width", "the wing plates are drawn as triangles reaching the pin (29 7/8 x 4 x 5'-3 1/2 listed on p.52 and p.67)", "5'-3 1/2 at the masonry plate tapering to 11 in. at the pin seat.");
            pc.Add(P("wing plate (centre)", Plate, Across, wing, -2, 2, "C", "PL 29 7/8 x 4 x 5'-3 1/2", flag: wingFlag));
            pc.Add(P("wing plate (outer)", Plate, Across, wing, 15, 19, "C", "PL 29 7/8 x 4 x 5'-3 1/2", mirrorN: true, flag: wingFlag));
            pc.Add(P("end stiffener plate", Plate, Along, Poly(19, -30.25, 29, -30.25, 19, -6.75), 4.5, 6.5, "C", "PL 10 x 2 x 1'-11 1/2", mirrorN: true, mirrorU: true,
                flag: U(pg, "end stiffener position", "four end stiffeners 10 x 2 x 1'-11 1/2 are listed and drawn sloped outside the outer wing plates; their position along the truss is not dimensioned", "against the stem plate faces (u = 4 1/2 to 6 1/2 in.), triangular.")));
            s.Unresolved = U(pg, "fixed shoe keys, pin nuts / rings and the anchor bolts", "listed / drawn (8 holes for 3 in. anchor bolts, 2 - 2 1/2 keys) but not part of the shoe solid", "not built.");
        }
        else
        {
            bool endShoe = s.Source.Contains("p.59");
            // rocker shoes: rocker base of radius R about the pin on a bearing plate projecting 1 1/2 in. above the masonry plate (1 in. let in)
            double R, half, stemHalf, stemTop, wingH, wingT, across, stiffW, masonryT, bearHalf, soleT, soleHalfN, pinBelow;
            string pg, stemTxt, wingTxt, stiffTxt, rockTxt, soleTxt, bearTxt, bearMat;
            if (endShoe)
            {
                pg = "p.59"; R = 29.5; half = 12; stemHalf = 5.5; stemTop = 0.125; wingH = 16.5; wingT = 1.5; across = 34; stiffW = 6; masonryT = 6.5; bearHalf = 7.5; bearMat = "S";
                soleT = s.Point.EndsWith("'") ? 7.5625 : 9; soleHalfN = 17; pinBelow = double.NaN;
                stemTxt = "PL 23 5/8 x 11 x 2'-10"; wingTxt = "PL 16 1/2 x 1 1/2 x 2'-0"; stiffTxt = "PL 6 x 1 1/2 x 1'-4 1/2"; rockTxt = "base PL 24 x 6 (on CL) x 3'-8, R 2'-5 1/2";
                soleTxt = $"PL 22 x {(s.Point.EndsWith("'") ? "7 9/16" : "9")} (on CL) x 2'-10"; bearTxt = "bearing PL 15 x 2 1/2 x 3'-8";
            }
            else if (four)
            {
                pg = "p.63"; R = 30.25; half = 15; stemHalf = 7; stemTop = -0.375; wingH = 15.5; wingT = 2; across = 35.5; stiffW = 8; masonryT = 9.5; bearHalf = 15; bearMat = "Stainless steel";
                soleT = 10.4375; soleHalfN = 17.75; pinBelow = 19;     // p.63: W.P. to pin 8 3/4 + 10 1/4, pin to bottom 3'-5 1/4 (= R 2'-6 1/4 + 1 1/2 + 9 1/2), total 5'-0 1/4
                stemTxt = "PL 23 1/8 x 14 x 2'-11 1/2"; wingTxt = "PL 15 1/2 x 2 x 2'-6"; stiffTxt = "PL 15 1/2 x 2 x 0'-8"; rockTxt = "rocker 30 x 6 3/4 (on CL) x 4'-0, R 2'-6 1/4";
                soleTxt = "PL 22 x 10 7/16 (on CL) x 2'-11 1/2"; bearTxt = "bearing PL 30 x 2 1/2 x 4'-0 stainless";
            }
            else
            {
                pg = "p.49"; R = 33; half = 12; stemHalf = 6; stemTop = -0.375; wingH = 19.75; wingT = 2; across = 34.5; stiffW = 6; masonryT = 8; bearHalf = 12; bearMat = "Stainless steel";
                soleT = 9.1875; soleHalfN = 17.25; pinBelow = 17.75;   // p.49: W.P. to pin 8 3/4 + 9, pin to bottom 2'-9 (R) + 1 1/2 + 8 = 3'-6 1/2, total 5'-0 1/4
                stemTxt = "PL 26 5/8 x 12 x 2'-10 1/2"; wingTxt = "PL 19 3/4 x 2 x 2'-0"; stiffTxt = "PL 6 x 2 x 1'-7 3/8"; rockTxt = "rocker 24 x 6 (on CL) x 3'-5, R 2'-9";
                soleTxt = "PL 22 x 9 3/16 (on CL) x 2'-10 1/2"; bearTxt = "bearing PL 24 x 2 1/2 x 3'-5 stainless";
            }
            double rockerT = endShoe ? 6 : four ? 6.75 : 6;
            double baseTop = -R + rockerT, rockAcross = endShoe ? 22 : four ? 24 : 20.5;
            if (!double.IsNaN(pinBelow))
            {
                double h = R + 1.5 + masonryT;
                report.Add($"{bn} {s.Name}: W.P. to bottom of shoe {total / In(1):0.###} in. (model) vs {pinBelow + h:0.###} in. printed chain ({pg}); pin {pinBelow} in. below the W.P., {h} in. above the bottom (was {s.PinBelowPoint / In(1):0.###} / {s.Height / In(1):0.###})");
                s.PinBelowPoint = In(pinBelow); s.Height = total - In(pinBelow);
            }
            pc.Add(P("sole plate", Sole, Across, Rect(-11, 11, 0.375, 0.375 + soleT), -soleHalfN, soleHalfN, "S", soleTxt, flag: bevel(pg)));
            pc.Add(P("stem plate", Body, Across, SeatTop(new double[] { -stemHalf, baseTop, stemHalf, baseTop }, stemHalf, stemTop, s.PinDiameter / In(1) / 2), -across / 2, across / 2, "C", stemTxt));
            pc.Add(P("wing plate", Plate, Across, Poly(-half, baseTop, half, baseTop, stemHalf, baseTop + wingH, -stemHalf, baseTop + wingH), across / 2, across / 2 + wingT, "C", wingTxt, mirrorN: true,
                flag: U(pg, "wing plate outline", "the wing plates are drawn as trapezoids from the rocker base to the stem; only height x thickness x length are listed", $"trapezoid {2 * half} in. at the base tapering to the stem width {2 * stemHalf} in.")));
            pc.Add(P("stiffener plate", Plate, Across, Poly(stemHalf, baseTop, stemHalf + stiffW, baseTop, stemHalf, baseTop + wingH), -wingT / 2, wingT / 2, "C", stiffTxt, mirrorU: true));
            pc.Add(P("rocker", Rocker, Across, RockerBase(half, baseTop, R), -rockAcross, rockAcross, "C", rockTxt,
                flag: U(pg, "rocker inlay plate", "the rocker carries an inlay bearing plate (silicon / stainless) let into its curved bottom, drawn as a lens, not dimensioned", "one rocker solid to the listed radius; inlay, dowels, countersunk cap screws not built.")));
            double mt = -R - 1.5;   // masonry plate top (pin centre = 0)
            pc.Add(P("bearing plate", Plate, Across, Rect(-bearHalf, bearHalf, mt - 1, mt + 1.5), -rockAcross, rockAcross, bearMat, bearTxt,
                flag: U(pg, "bearing plate seat", "the bearing plate is let into the masonry plate; the drawing shows it 1 1/2 in. proud with the 1 in. recess", "plate 2 1/2 in. thick, 1 in. into the masonry plate (the overlap is counted twice in the weight).")));
            s.Unresolved = U(pg, "rocker shoe shims, dowels, pin nuts / rings and anchor bolts", "listed / drawn but not part of the shoe solids", "not built.");
        }
        s.Pieces = pc;
        shoes++;
    }
}

// ================================================================ main spans (p.73 L0 rocker shoes, p.96 L11 cast shoes)
{
    var t = Doc.Project.Bridges.First(b => b.Name == "PTC Arch truss 18-20").Truss!;
    foreach (var s in t.Shoes)
    {
        var pc = new List<TrussShoePiece>();
        if (s.Source == "p.73")
        {
            // pin 3 in., 3'-5 above the bottom: masonry 5 in., rocker R 3'-0 about the pin, base 32 x 6 3/4 (on CL) x 3'-0, stem 9 in. x 29 1/4 under the pin
            // CORRECTION rows (shoes.csv, third pass): wing plates and stiffeners 1 1/2 thick (not 1/2), rocker base 6 3/4 (not 6 1/2); the stem keeps its listed 29 1/4 and sits on the base top
            string stackFlag = U("p.73", "rocker shoe stack", "with the corrected 6 3/4 rocker base (R 3'-0 about the pin) and the 29 1/4 stem the stack is 1/4 in. taller than the pin height drawn (3'-5 over a 5 in. masonry plate)", "stem, wings and stiffeners from the base top (-29 1/4 in. below the pin) up; 1/4 in. stem overlap into the pin seat accepted.");
            pc.Add(P("sole plate", Sole, Across, Rect(-9.5, 9.5, 0.375, 6.125), -13.75, 13.75, "C", "PL 19 x 5 3/4 (on CL) x 2'-3 1/2", flag: bevel("p.73")));
            pc.Add(P("stem plate", Body, Across, SeatTop(new double[] { -4.5, -29.25, 4.5, -29.25 }, 4.5, -0.25, 1.5), -13.5, 13.5, "C", "PL 29 1/4 x 9 x 2'-3", flag: stackFlag));
            pc.Add(P("wing plate", Plate, Across, Poly(-16, -29.25, 16, -29.25, 9.5, -5, -9.5, -5), 13.5, 15, "C", "PL 24 1/2 x 1 1/2 x 2'-8", mirrorN: true));
            pc.Add(P("stiffener plate", Plate, Across, Poly(4.5, -29.25, 16, -29.25, 9.5, -5, 4.5, -5), -0.75, 0.75, "C", "PL 24 1/2 x 1 1/2 x 0'-11 1/2", mirrorU: true,
                flag: U("p.73", "stiffener plate position", "the two stiffeners 24 1/2 x 1 1/2 x 11 1/2 are listed; the elevation shows them on the stem faces, their position across is not dimensioned", "on the truss CL, one each side of the stem.")));
            pc.Add(P("rocker base plate", Rocker, Across, RockerBase(16, -29.25, 36), -18, 18, "C", "PL 32 x 6 3/4 x 3'-0, R 3'-0"));
            s.Unresolved = U("p.73", "rocker shoe dowels, hold-down / shear lock (Section E / H) and the anchor bolts", "8 stainless dowels 2 x 4, the shear lock tongue plate in its pedestal pocket and 5 in. anchor bolts 13'-4 are drawn / listed with the substructure contract", "not built.");
        }
        else
        {
            bool fixedShoe = s.Type == TrussShoeType.Fixed;
            // p.96: 10 in. pin 6'-7 above the bottom of the shoe; forging top 4'-1 x 4'-4, 1'-7 above the pin, chamfered to the pin; steel casting below
            var forging = new List<double> { -24.5, 19, 24.5, 19, 24.5, 10, 10, 0, 5, 0 };
            forging.AddRange(Arc(5, 0, 180));
            forging.AddRange(new double[] { -10, 0, -24.5, 10 });
            pc.Add(P("steel forging (top)", Body, Across, Poly(forging.ToArray()), -26, 26, "Forged steel (ASTM A237-52T class B)", "forging 4'-1 x 1'-7 x 4'-4",
                flag: U("p.96", "forging outline", "the forging is drawn 4'-1 x 4'-4 fin. with its 1'-6 / 1 in. heights; the chamfer to the pin and the key / keyway (2 3/4 / 2 13/16) are drawn only", "vertical sides 9 in. then chamfered to 10 in. each side of the pin; keys not built.")));
            double baseTop = fixedShoe ? -73 : -43, half = fixedShoe ? 55 : 53;
            var wall = new List<double> { -half, baseTop, half, baseTop, 10, 0, 5, 0 };
            wall.AddRange(Arc(5, 0, -180));
            wall.AddRange(new double[] { -10, 0 });
            string castMat = "Cast steel (ASTM A27-52T 65-35)";
            string castFlag = U("p.96", "steel casting walls and ribs", "the casting is drawn in elevation / side view with wall and rib thicknesses (4 in., 4 3/4 / 3 1/2 in. at pier 18) and rib positions at 1'-9; the cradle under the pin (10 in. R), fillets (2 in.) and the internal ribs are not fully dimensioned",
                "three longitudinal walls from the base to the 10 in. cradle at the pin, an 8 in. diaphragm under the pin between the walls, outer triangular ribs at 1'-9; fillets, drain holes, bosses and the inspection rail not built.");
            if (fixedShoe)
            {
                s.MasonryThickness = 0;     // pier 18: the casting has its own 6 in. base (9'-2 x 8'-8) on the pier, no separate masonry plate
                pc.Add(P("casting base", Body, Across, Rect(-55, 55, -79, -73), -52, 52, castMat, "base 9'-2 x 8'-8 x 6", flag: castFlag));
                pc.Add(P("casting outer wall", Body, Across, Poly(wall.ToArray()), 20.25, 25, castMat, "wall 4 3/4", mirrorN: true, flag: castFlag));
                pc.Add(P("casting centre wall", Body, Across, Poly(wall.ToArray()), -1.75, 1.75, castMat, "wall 3 1/2 (2 halves with butt splice)", flag: castFlag));
                pc.Add(P("casting diaphragm under the pin", Body, Along, Rect(1.75, 20.25, -73, -5), -4, 4, castMat, "diaphragm 8", mirrorN: true, flag: castFlag));
                // outer ribs: 2'-3 out to the base edge, 3'-9 high at the walls (side view), cut by the wall slope in elevation
                pc.Add(P("casting outer rib", Body, Along, Poly(25, -73, 52, -73, 25, -34), 19, 23, castMat, "rib 4", mirrorN: true, mirrorU: true, flag: castFlag));
                pc.Add(P("casting outer rib (end)", Body, Along, Poly(25, -73, 52, -73, 25, -51.9), 40, 44, castMat, "rib 4", mirrorN: true, mirrorU: true, flag: castFlag));
                s.Unresolved = U("p.96", "anchor bolts and the pin cap / ring", "24 holes for 3 in. anchor bolts and the pin caps are drawn", "not built.");
            }
            else
            {
                // pier 19: masonry plate 6 in. (side view; listed 110 x 6 3/4 x 9'-2), 5 forged rollers 2'-0 dia. 1'-3 wide at 1'-9 (two 3'-6 pieces 4 in. apart), casting base on the rollers
                pc.Add(P("forged roller", Roller, Across, new List<TrussUV>(), 2, 44, "Forged steel (ASTM A237-52T class B)", "segmental roller 2'-0 dia. x 1'-3 x 3'-6", mirrorN: true,
                    shiftsX: new double[] { -42, -21, 0, 21, 42 }));
                pc[^1].CircleDiameter = In(24); pc[^1].CircleY = In(-61); pc[^1].ClipHalfWidth = In(7.5);
                pc.Add(P("casting base", Body, Across, Rect(-53, 53, -49, -43), -44, 44, castMat, "base 8'-10 x 7'-4 x 6", flag: castFlag));
                pc.Add(P("casting outer wall", Body, Across, Poly(wall.ToArray()), 21, 25, castMat, "wall 4", mirrorN: true, flag: castFlag));
                pc.Add(P("casting centre wall", Body, Across, Poly(wall.ToArray()), -2, 2, castMat, "wall 4", flag: castFlag));
                pc.Add(P("casting diaphragm under the pin", Body, Along, Rect(2, 21, -43, -5), -4, 4, castMat, "diaphragm 8", mirrorN: true, flag: castFlag));
                pc.Add(P("casting outer rib", Body, Along, Poly(25, -43, 44, -43, 25, -31), 19, 23, castMat, "rib 4", mirrorN: true, mirrorU: true, flag: castFlag));
                s.Unresolved = U("p.96", "roller nest link bars, inlay plate 84 x 2 3/4 x 8'-10 (press fit), dowels, tap bolts, anchor bolts", "drawn / listed; the inlay is let into the masonry plate and the 6 3/4 listed masonry thickness differs from the 6 in. side view (CONFLICT, 6 in. used so the 6'-7 chain closes)", "not built.");
            }
        }
        s.Pieces = pc;
        shoes++;
    }
}

var rep = Doc.Regenerate();
foreach (var r in report) Print(r);
double lb = 2.20462262;
foreach (var b in Doc.Project.Bridges.Where(b => b.Truss != null && b.Name.StartsWith("PTC")))
{
    var st = Doc.Model(b.Name).Steel;
    if (st == null) continue;
    var parts = st.Parts.Where(p => p.Assembly.StartsWith("SHOE-")).ToList();
    var by = parts.GroupBy(p => (p.Material ?? "").ToLowerInvariant().Contains("cast") ? "casting" : (p.Material ?? "").ToLowerInvariant().Contains("forged") || p.Category == PartCategory.TrussPin ? "forging" : "plate steel");
    Print($"{b.Name}: {b.Truss!.Shoes.Count} shoes, {parts.Count} shoe parts; " + string.Join(", ", by.Select(g => $"{g.Key} {g.Sum(p => p.Mass) * lb:N0} lb")));
    foreach (var e in rep.Errors.Where(e => e.StartsWith(b.Name))) Print("  ERROR " + e);
}
Print($"{shoes} shoe definitions given pieces.");
Doc.Save(outPath);
Print($"Saved {outPath}");
