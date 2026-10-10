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
//      on the S / N faces with 4 spaces on the chamfers. Flare hoops 6e3 come in lapped pairs at the column tie
//      spacing; modeled as single closed hoops at HALF that spacing (same count).
// Not modeled: footing dowels (11d2 / 18d3-d4) lapped or coupled to the column bars - the column bars run continuous from
// the footing; 11G / 12G inner 6e4 ties.
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
void Flare(SubstructureDefinition sd, int nS, double sS, int nN, double sN, int nC, double hoopSpacing)
{
    var r = sd.Reinforcement;
    if (double.IsNaN(sd.ColumnFlareElevation)) { r.FlareBarSize = ""; return; }
    r.FlareBarSize = "#7"; r.FlareCover = I(3.5);
    r.FlareFaceBarsS = nS; r.FlareFaceSpacingS = I(sS); r.FlareFaceBarsN = nN; r.FlareFaceSpacingN = I(sN); r.FlareChamferBars = nC;
    r.FlareTieSize = "#6"; r.FlareTieSpacing = I(hoopSpacing);
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
            if (n is "Pier 11G" or "Pier 12G") Flare(sd, 7, 6, 6, 6, 5, 5);
            else Flare(sd, 5, 0, 5, 0, 5, rampG ? 5 : 6);
        }
        else if (n is "Pier 16G" or "Pier 17G")            // Ramp G two-column bents, 8'-0 x 4'-6 (section C-C, PDF 65 / 69)
        {
            // #14: 6 @ 7 1/2 on the 8'-0 faces, 4 @ 6 1/4 on the 4'-6 faces, 2 per chamfer at 10 3/8 and 20 1/8 along the
            // outside chamfer face (10 3/8 | 9 3/4 | 11 5/8); 6e1 paired hoops + 5e2 crossties around the chamfer bar
            // nearest the 8'-0 face, 29-30 spaces @ about 1'-0.
            Apply(sd, "#14", 6, 7.5, 4, 6.25, 0, 0, Pos(10.375, 20.125), 12, 40, 1);
            Flare(sd, 0, 0, 0, 0, 0, 0);
        }
        else                                               // I-80 8'-0 x 4'-6 (incl. Pier 11WB & 19G)
        {
            if (n is "Pier 3WB" or "Pier 13WB") Apply(sd, "#18", 6, 7.5, 4, 6, 0, 0, Pos(6.5, 15.5, 21.25), 12, 40, 2);
            else if (n == "Pier 7WB") Apply(sd, "#18", 7, 6.25, 4, 6, 3, 5.75, null, 12, 40, 2);
            else Apply(sd, "#11", 6, 7.5, 4, 6.5, 0, 0, Pos(8, 15.25, 22.5), 12, 40, 2);
            Flare(sd, 5, 0, 4, 0, 4, 6);
        }
        done++;
        Print($"  {b.Name} {n}: {sd.Reinforcement.ColumnBarSize} column bars{(sd.Reinforcement.FlareBarSize != "" ? ", flare cage" : "")}");
    }
var res = Doc.Regenerate();
Print($"{done} piers; regenerated {res.Bridges} bridges in {res.Elapsed.TotalSeconds:F1} s, {res.Errors.Count} errors.");
foreach (var e in res.Errors) Print("  ERROR " + e);
foreach (var b in Doc.Project.Bridges)
    foreach (var d in Doc.Model(b.Name).Substructure?.Diagnostics ?? new())
        if (d.Message.Contains("column")) Print("  WARNING " + d.Message);
Doc.Save();
