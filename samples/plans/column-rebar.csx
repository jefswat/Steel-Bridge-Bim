// Column (and flared column base) reinforcing of the as-designed piers, per the pier detail sheets (sections C-C / D-D
// and the column elevations):
//  * I-80 columns 8'-0 x 4'-6 (2'-4 x 1'-3 chamfers), 2" clear to the hoops:
//      #11 piers: 6 @ 7 1/2 on each S face, 4 @ 6 1/2 on each N face, 3 per chamfer at 8, 15 1/4, 22 1/2 from the S face;
//      3WB / 13WB (fixed): #18, 6 @ 7 1/2, 4 @ 6, chamfer bars at 6 1/2, 15 1/2, 21 1/4;  7WB (fixed): #18, 7 @ 6 1/4,
//      4 @ 6, 3 @ 5 3/4 centred on the chamfers. Ties: #6 "hoops" 6e1 = two closed hoops per level, each around one end,
//      overlapping the middle 3'-4 (legs at +-1'-8), plus #5 crossties 5e2 around the 2nd chamfer bar on each side,
//      about 1'-0 apart over the full column height.
//  * Pier 6A and Ramp G columns 10'-0 x 6'-6 (3'-4 x 1'-9 chamfers): 7 @ 6 on the S faces, 6 @ 6 on the N faces,
//      5 per chamfer @ 7 starting 7 3/8 from the S face; #11, or #14 (8G, 10G), or #18 (2G, 7G, 11G, 12G, 18G). Same hoops /
//      crossties; Ramp G tie levels about 10" apart.
//  * Flared bases (3 1/2" clear): #7 bars (7d12 / 7d5) at equal spaces with shared corners - I-80 9'-0 x 5'-6: S faces
//      4 spaces, N faces 3, chamfers 3; Ramp G 11'-0 x 7'-6: 4 spaces on every face, except 11G / 12G: 7 @ 6 and 6 @ 6
//      on the S / N faces with 4 spaces on the chamfers. Plinth ties per level = a LAPPED PAIR at the column tie spacing
//      ("N-6e1, N-6e3 & N-5e2", "n SPA. @ ABT. 10 / 1'-0"), from 3" above the footing to 3" below the plinth top:
//      6e3 PLINTH HAIRPINS = open U around each S end (bending diagrams: I-80 legs 3'-1 / chamfer 2'-10 / end 2'-3, 14'-1;
//      Ramp G 3'-1 / 3'-11 / 3'-3, 17'-3), the legs lapping 2'-9 out-to-out along the long faces (U 5'-7 / 6'-7 along S,
//      twice that minus the 8'-5 / 10'-5 between covers); 11G / 12G: 6e4 PLINTH HOOPS = two closed end hoops (3'-6 / 3'-11 /
//      3'-3, 7'-0 along S, 27'-0) overlapping 3'-7 out-to-out. Space counts from the elevation callouts (= bar list / 2 /
//      columns - 1): the plans' "about" spacing is not a fixed rounding of the zone length.
//  * 18d9 (7WB) / 18d7 (2G, 7G, 11G, 12G) / 18d5 (18G) "BAR TERMINATES AT TOP OF COLUMN": the half-filled symbol in the
//      legend is a TAG whose leader points at the circled perimeter bar wrapped by the crosstie (34 / 46 perimeter bars =
//      the dowel count); those bars are the 'T' letters below, cut to the listed length from their coupler.
//  * FOOTING DOWELS / COLUMN SPLICES (docs/plan-data/piles-capstep-dowels.md, section 3): every column bar has an L-shaped
//    footing dowel (hook on the bottom mat: 2'-0 #11 / 2'-7 #14 / 3'-5 #18 hook, hook underside 1'-4 [Ramp G] / 1'-3 [I-80]
//    above the footing bottom, DERIVED from the printed laps) and the column bar either laps it (printed "8'-4 / 12'-6 MIN. LAP":
//    column bars start at the footing top, dowels project by the lap) or meets it at a mechanical coupler (two splice levels
//    5'-0 apart; short bar d1 + long dowel d3 at the HIGH level, long bar d2 + short dowel d4 at the LOW level; coupler
//    elevations DERIVED from the bar lists, the coupler itself is generic). Which bar is d1 / d2 / terminating comes from the
//    plan sections C-C (filled bar symbol = d1, open = d2, circled middle chamfer bar = the bar that stops at the column
//    top, d7/d5/d9), read from the PDF vector symbols and written as SplicePattern strings: one letter per bar, counter-clockwise
//    from +S (plan view, S right, N up-station; the mirror is not shown on the plans - UNCERTAIN, only swaps which bar is d1/d2).
//    Bar tops = soffit at the column CL + the embedment derived from the bar lists. 9G: second lap at mid-height (27'-1).
// Not modeled: plinth dowels (7d6 / 7d13) and flare bars starting at the footing top.
// Re-runnable; run after piers-i80.csx, pier-6a.csx and piers-ramp-g.csx.
using SBM.Core.Bridge;

double I(double inch) => In(inch);
List<double> Pos(params double[] inches) => inches.Select(I).ToList();

void Apply(SubstructureDefinition sd, string size, int nS, double sS, int nN, double sN, int nC, double sC, List<double>? cPos,
    double tieSpacing, double overlap, int crossBar)
{
    var r = sd.Reinforcement;
    r.ColumnBarSize = size; r.ColumnCover = I(2);
    r.ColumnFaceBarsS = nS; r.ColumnFaceSpacingS = I(sS);
    r.ColumnFaceBarsN = nN; r.ColumnFaceSpacingN = I(sN);
    r.ColumnChamferBars = nC; r.ColumnChamferSpacing = I(sC); r.ColumnChamferBarPositions = cPos;
    r.ColumnSpiralSize = "#6"; r.ColumnSpiralPitch = I(tieSpacing); r.ColumnHoopOverlap = I(overlap);
    r.ColumnCrosstieSize = "#5"; r.ColumnCrosstieBar = crossBar;
}
void Flare(SubstructureDefinition sd, int nS, double sS, int nN, double sN, int nC, double tieSpacing, FlareTieType style, double overlap, int spaces = 0)
{
    var r = sd.Reinforcement;
    if (double.IsNaN(sd.ColumnFlareElevation)) { r.FlareBarSize = ""; return; }
    r.FlareBarSize = "#7"; r.FlareCover = I(3.5);
    r.FlareFaceBarsS = nS; r.FlareFaceSpacingS = I(sS); r.FlareFaceBarsN = nN; r.FlareFaceSpacingN = I(sN); r.FlareChamferBars = nC;
    r.FlareTieSize = "#6"; r.FlareTieSpacing = I(tieSpacing); r.FlareTieStyle = style; r.FlareTieOverlap = I(overlap); r.FlareTieSpaces = spaces;
}

// Plinth tie spaces per column (pier elevations "n SPA. @ ABT. s"; check: plinth hairpins / hoops in the bar list = 2 x (n + 1) x columns).
var plinthSpaces = new Dictionary<string, int>
{
    ["Pier 2G"] = 14, ["Pier 3G"] = 16, ["Pier 4G"] = 20, ["Pier 5G"] = 19, ["Pier 6G"] = 21, ["Pier 7G"] = 19, ["Pier 8G"] = 25,
    ["Pier 9G"] = 25, ["Pier 10G"] = 24, ["Pier 11G"] = 24, ["Pier 12G"] = 27, ["Pier 13G"] = 20, ["Pier 14G"] = 19, ["Pier 15G"] = 16,
    ["Pier 4WB"] = 12, ["Pier 5WB"] = 13, ["Pier 6WB"] = 19, ["Pier 7WB"] = 12, ["Pier 8WB"] = 12, ["Pier 9WB"] = 11, ["Pier 10WB"] = 12,
    ["Pier 11WB"] = 13, ["Pier 12WB"] = 14, ["Pier 13WB"] = 12, ["Pier 14WB"] = 12,
};
// One letter per column bar, counter-clockwise from +S: H = splices at the upper coupler, L = lower, T = lower and stops
// short of the cap steel. 10'-0 x 6'-6 columns (46 bars), 8'-0 x 4'-6 columns (32 / 34 bars) and the Ramp G two-column bents (28).
const string P46 = "HLHLHLHLHLHLHLHLHLHLHLHLLHLHLHLHLHLHLHLHLHLHLH", P46T = "HLHLHTHLHLHLHLHLHTHLHLHLLHLHTHLHLHLHLHLHTHLHLH";
const string P32 = "HLHLHLHLLHLHLHLHHLHLHLHLLHLHLHLH", P34T = "HLHTHHLHLHLHHTHLHHLHTHHLHLHLHHTHLH", P28 = "LHLHLHLHLHLHLHLHLHLHLHLHLHLH";
// pier name -> (lap or low coupler, high coupler [0 = lap pier], bar top above soffit, length of the T bars, pattern, mid-height lap start)
var splices = new Dictionary<string, (double lapOrLow, double high, double embed, double term, string pat, double mid)>
{
    ["Pier 1G"] = (Ft(8) + In(4), 0, Ft(7) + In(10), 0, "", 0), ["Pier 2G"] = (Ft(10), Ft(15), Ft(7) + In(11), Ft(21) + In(9), P46T, 0),
    ["Pier 3G"] = (Ft(8) + In(4), 0, Ft(7) + In(8), 0, "", 0), ["Pier 4G"] = (Ft(8) + In(4), 0, Ft(7) + In(8), 0, "", 0),
    ["Pier 5G"] = (Ft(12) + In(6), 0, Ft(7) + In(8), 0, "", 0), ["Pier 6G"] = (Ft(8) + In(4), 0, Ft(7) + In(8), 0, "", 0),
    ["Pier 7G"] = (Ft(10), Ft(15), Ft(7) + In(8), Ft(34) + In(6), P46T, 0), ["Pier 8G"] = (Ft(10), Ft(15), Ft(7) + In(8), 0, P46, 0),
    ["Pier 9G"] = (Ft(12) + In(6), 0, Ft(7) + In(8), 0, "", Ft(27) + In(1)), ["Pier 10G"] = (Ft(10), Ft(15), Ft(7) + In(8), 0, P46, 0),
    ["Pier 11G"] = (Ft(10), Ft(15), Ft(7) + In(8), Ft(44) + In(6), P46T, 0), ["Pier 12G"] = (Ft(10), Ft(15), Ft(7) + In(8), Ft(47) + In(6), P46T, 0),
    ["Pier 13G"] = (Ft(8) + In(4), 0, Ft(7) + In(8), 0, "", 0), ["Pier 14G"] = (Ft(12) + In(6), 0, Ft(7) + In(8), 0, "", 0),
    ["Pier 15G"] = (Ft(8) + In(4), 0, Ft(7) + In(9), 0, "", 0), ["Pier 16G"] = (Ft(5) + In(1), Ft(10) + In(1), Ft(6) + In(1), 0, P28, 0),
    ["Pier 17G"] = (Ft(5) + In(1), Ft(10) + In(1), Ft(5) + In(11), 0, P28, 0),
    ["Pier 18G"] = (Ft(9) + In(11), Ft(14) + In(11), Ft(7) + In(7), Ft(11) + In(6), P46T, 0), ["Pier 19G"] = (Ft(12) + In(6), 0, Ft(7) + In(9), 0, "", 0),
    ["Pier 1WB"] = (Ft(12) + In(6), 0, Ft(6), 0, "", 0), ["Pier 2WB"] = (Ft(8) + In(4), 0, Ft(6), 0, "", 0),
    ["Pier 3WB"] = (Ft(4) + In(11), Ft(9) + In(11), Ft(5) + In(11), 0, P32, 0), ["Pier 4WB"] = (Ft(8) + In(4), 0, Ft(6), 0, "", 0),
    ["Pier 5WB"] = (Ft(12) + In(6), 0, Ft(6), 0, "", 0), ["Pier 6A"] = (Ft(12) + In(6), 0, Ft(6) + In(11), 0, "", 0),
    ["Pier 6WB"] = (Ft(8) + In(4), 0, Ft(6), 0, "", 0), ["Pier 7WB"] = (Ft(5), Ft(10), Ft(5) + In(11), Ft(25) + In(8), P34T, 0),
    ["Pier 8WB"] = (Ft(8) + In(4), 0, Ft(5) + In(11), 0, "", 0), ["Pier 9WB"] = (Ft(8) + In(4), 0, Ft(5) + In(11), 0, "", 0),
    ["Pier 10WB"] = (Ft(8) + In(4), 0, Ft(5) + In(11), 0, "", 0), ["Pier 11WB"] = (Ft(12) + In(6), 0, Ft(5) + In(10), 0, "", 0),
    ["Pier 12WB"] = (Ft(8) + In(4), 0, Ft(5) + In(10), 0, "", 0), ["Pier 13WB"] = (Ft(4) + In(11), Ft(9) + In(11), Ft(5) + In(9), 0, P32, 0),
    ["Pier 14WB"] = (Ft(8) + In(4), 0, Ft(5) + In(10), 0, "", 0), ["Pier 15WB"] = (Ft(8) + In(4), 0, Ft(5) + In(10), 0, "", 0),
};
void Splice(SubstructureDefinition sd, string name, bool rampG)
{
    var r = sd.Reinforcement;
    if (!splices.TryGetValue(name, out var s)) { r.ColumnSplice = ColumnSpliceType.Continuous; return; }
    r.DowelBottomAboveFooting = rampG ? Ft(1) + In(4) : Ft(1) + In(3);
    r.DowelHookLength = 0; r.DowelPin = 0;                      // by bar size: #11 2'-0, #14 2'-7, #18 3'-5
    r.ColumnBarEmbedInCap = s.embed; r.ColumnSplicePattern = s.pat; r.ColumnTerminatingBarLength = s.term; r.ColumnMidLapStart = s.mid;
    if (s.high > 0) { r.ColumnSplice = ColumnSpliceType.Coupler; r.CouplerLow = s.lapOrLow; r.CouplerHigh = s.high; r.ColumnLapLength = 0; }
    else { r.ColumnSplice = ColumnSpliceType.Lap; r.ColumnLapLength = s.lapOrLow; r.CouplerLow = 0; r.CouplerHigh = 0; }
}

int done = 0;
foreach (var b in Doc.Project.Bridges.Where(x => x.Name.StartsWith("I-80") || x.Name.StartsWith("Ramp G")))
    foreach (var sup in b.Supports.Concat(b.AuxiliarySupports))
    {
        var sd = sup.Substructure;
        if (sd == null || sd.CapProfile == null || sd.CapProfile.Count < 3 || sd.ColumnShape != ColumnShape.Rectangular) continue;
        string n = sup.Name;
        bool rampG = b.Name.StartsWith("Ramp G") && !n.Contains("19G");
        if (sd.ColumnWidth > Ft(9))                        // 10'-0 x 6'-6: Pier 6A and Ramp G
        {
            string size = n is "Pier 8G" or "Pier 10G" ? "#14" : n is "Pier 2G" or "Pier 7G" or "Pier 11G" or "Pier 12G" or "Pier 18G" ? "#18" : "#11";
            Apply(sd, size, 7, 6, 6, 6, 0, 0, Pos(7.375, 14.375, 21.375, 28.375, 35.375), rampG ? 10 : 12, 40, 2);
            if (n is "Pier 11G" or "Pier 12G") Flare(sd, 7, 6, 6, 6, 5, 10, FlareTieType.HoopPair, 43, plinthSpaces[n]);
            else Flare(sd, 5, 0, 5, 0, 5, 10, FlareTieType.HairpinPair, 33, plinthSpaces.GetValueOrDefault(n));
        }
        else if (n is "Pier 16G" or "Pier 17G")            // Ramp G two-column bents, 8'-0 x 4'-6 (section C-C, PDF 65 / 69)
        {
            // #14: 6 @ 7 1/2 on the 8'-0 faces, 4 @ 6 1/4 on the 4'-6 faces, 2 per chamfer at 10 3/8 and 20 1/8 along the
            // outside chamfer face (10 3/8 | 9 3/4 | 11 5/8); 6e1 paired hoops + 5e2 crossties around the chamfer bar
            // nearest the 8'-0 face, 29-30 spaces @ about 1'-0.
            Apply(sd, "#14", 6, 7.5, 4, 6.25, 0, 0, Pos(10.375, 20.125), 12, 40, 1);
            Flare(sd, 0, 0, 0, 0, 0, 0, FlareTieType.ClosedTie, 0);
        }
        else                                               // I-80 8'-0 x 4'-6 (incl. Pier 11WB & 19G)
        {
            if (n is "Pier 3WB" or "Pier 13WB") Apply(sd, "#18", 6, 7.5, 4, 6, 0, 0, Pos(6.5, 15.5, 21.25), 12, 40, 2);
            else if (n == "Pier 7WB") Apply(sd, "#18", 7, 6.25, 4, 6, 3, 5.75, null, 12, 40, 2);
            else Apply(sd, "#11", 6, 7.5, 4, 6.5, 0, 0, Pos(8, 15.25, 22.5), 12, 40, 2);
            Flare(sd, 5, 0, 4, 0, 4, 12, FlareTieType.HairpinPair, 33, plinthSpaces.GetValueOrDefault(n));
        }
        Splice(sd, n, b.Name.StartsWith("Ramp G"));
        done++;
        Print($"  {b.Name} {n}: {sd.Reinforcement.ColumnBarSize} column bars{(sd.Reinforcement.FlareBarSize != "" ? ", flare cage" : "")}, {sd.Reinforcement.ColumnSplice}");
    }
var res = Doc.Regenerate();
Print($"{done} piers; regenerated {res.Bridges} bridges in {res.Elapsed.TotalSeconds:F1} s, {res.Errors.Count} errors.");
foreach (var e in res.Errors) Print("  ERROR " + e);
foreach (var b in Doc.Project.Bridges)
    foreach (var d in Doc.Model(b.Name).Substructure?.Diagnostics ?? new())
        if (d.Message.Contains("column")) Print("  WARNING " + d.Message);
Doc.Save();
