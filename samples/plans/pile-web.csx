// H-pile web orientation of the as-designed footings, read from the pile symbols on every pile layout sheet (Ramp G PDF 20-76, I-80 PDF 21-81):
//  * plumb piles are drawn as an "I": web along N (across the pier line), flanges parallel to the cap line = the default;
//  * battered piles show the H section at the head with the WEB ALONG THE BATTER DIRECTION (flanges across it): web along N on the N-edge
//    rows, along S on the S-edge piles (Ramp G 1G-3G, 8G-15G, 18G), along the 45 deg diagonal on the corner piles.
//  * abutment piles (abutments.md): flanges parallel to the bearing line, web normal to it = the default.
// Sets FootingDefinition.PileWeb = AlongBatter on every footing (identical to the default where no pile is battered).
// Re-runnable; equivalent to the PileWeb lines in piers-i80.csx / pier-6a.csx / piers-ramp-g.csx (use this on an already built model).
using SBM.Core.Bridge;
int n = 0;
foreach (var b in Doc.Project.Bridges)
    foreach (var sup in b.Supports.Concat(b.AuxiliarySupports))
        foreach (var fd in sup.Substructure?.Footings ?? new())
            if (fd.Piles.Any(p => p.IsBattered)) { fd.PileWeb = PileWebMode.AlongBatter; n++; }
Print($"{n} footings with battered piles: web along the batter direction.");
var res = Doc.Regenerate();
Print($"regenerated {res.Bridges} bridges, {res.Errors.Count} errors.");
Doc.Save();
