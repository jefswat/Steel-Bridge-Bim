// Iowa DOT Design 817 (Woodbury, I-29 NB over Floyd Blvd) - Iowa AESTHETIC BARRIER RAIL, east side only (closes U10).
// Called by build-woodbury.csx before regeneration (re-runnable: replaces Deck.Barrier).
//   sbm run samples\plans\barrier-woodbury.csx "samples\plans\Woodbury I-29 NB over Floyd.sbm"
// Plan data: p.38 / DS 36 (Barrier Rail Details: Part Sections C-C / F-F standard, D-D bump-out, G-G / H-H bump-out plans, Elevation lengths),
// p.22 / DS 20 Detail C (deck bump-out at the piers), p.19 / DS 17 Barrier Rail Orientation Detail, p.44 / DS 42 (texture), p.13 / DS 11 (thin brick).
// Section coordinates: R = inches INWARD from the deck edge (= the back reference line of C-C; the gutter line is R = 19 = 1'-7), H = inches above
// the top of deck. Read on 300 dpi rasters (fractions RASTER). Surface tags: "liner" = brick-pattern form liner (DS 42), "brick" = integral thin
// veneer brick (DS 11) - used by surface-treatments-woodbury.csx.
using SBM.Core.Bridge;

var br = Doc.Project.Bridges.First(b => b.Structure == "I-29 NB over Floyd Blvd (Design 817)");
BarrierProfilePoint P(double r, double h, double radius = 0, double chamfer = 0, string? tag = null) => new(In(r), In(h), In(radius), In(chamfer), tag);
const double c38 = 0.375;   // 3/8 chamfers (TYP.) and the 3/8 strip drafts
// Front (traffic) face, all sections (C-C): toe 3 (* max) vertical at the gutter line (R 19), 1st point of intersection 5 in / 7 up (R = 1 1/2),
// 2nd 3 1/2 in / 2'-10 up to the top (R = 10); 7 + 3 = 10, 2'-10 + 10 = 3'-8 at the gutter; top 3'-8 at the front corner (R 10 1/2 = 2 + 8 1/2).
List<BarrierProfilePoint> Front(double backR, double backTop) => new()
{
    P(19, 0), P(19, 3, radius: 1.5), P(14, 10, radius: 10), P(10.5, 44, chamfer: c38), P(backR, backTop, chamfer: c38),
};
// Back face rustication: three strips, 3/8 drafted top and bottom at their outer face, recesses between. Strip / recess levels from the deck (C-C chain
// 6 1/8 + 5 1/2 + 6 1/2 + 5 1/2 + 6 1/2 + 1'-1 1/2 = 3'-7 5/8; D-D 5 3/4 + ... = 3'-7 1/4 - same levels): recess 0..13 1/2, strip 13 1/2..20, recess
// 20..25 1/2, strip 25 1/2..32, recess 32..37 1/2, top strip 37 1/2..top.
// stripR = strip outer face, recessR = recess plane, inner = R where the strip's 3/8 draft starts (= recessR, or the bump-out brick plane for the
// strip returns), tag = recess surface.
List<BarrierProfilePoint> Back(double stripR, double recessR, double inner, string tag)
{
    var l = new List<BarrierProfilePoint>();
    foreach (var (top, bot) in new[] { (37.5, 32.0), (25.5, 20.0), (13.5, 0.0) })
    {
        // strip bottom (drafted) -> recess down
        l.Add(P(stripR, top + c38));
        if (inner != recessR) l.Add(P(inner, top));
        l.Add(P(recessR, top, tag: tag));
        if (bot > 0)
        {
            l.Add(P(recessR, bot));
            if (inner != recessR) l.Add(P(inner, bot));
            l.Add(P(stripR, bot - c38));
        }
        else l.Add(P(recessR, 0));
    }
    return l;
}
// Standard section C-C / F-F: recess 2 from the deck edge (1'-5 from the gutter), strips 1/2 from the edge (1 1/2 projection), top 3'-7 5/8 at the back.
var std = Front(0.5, 43.625); std.AddRange(Back(0.5, 2, 2, "liner"));
// Bump-out D-D (CL pier): thin veneer brick plane 2'-1 from the gutter (R -6), strips 4 further (2'-5, R -10), top 3'-7 1/4 at the back.
var dd = Front(-10, 43.25); dd.AddRange(Back(-10, -6, -6, "brick"));
// Strip returns (G-G 5'-10 long at the strips vs H-H 4'-7 5/8 at the brick): the bump-out strips (R -10, drafted over their outer 4" as in D-D) on the
// standard recess (R 2, form liner).
var ret = Front(-10, 43.25); ret.AddRange(Back(-10, 2, -6, "liner"));
// Each list runs (19, 0) toe -> front face -> top -> back face -> (recess R, 0): counter-clockwise in (R, H), the base edge closes it.

var b = br.Deck.Barrier;
b.Type = BarrierType.Custom;
b.ProfileName = "Iowa aesthetic barrier rail (Design 817 DS 36)";
b.Left = false; b.Right = true;                      // east side only (west edge = stage-2 construction joint, p.18)
b.Reinforcement = false;                              // barrier bars 5c1 / 5c2 / 5c8 / 6d1 / 4c15 / 4d2 / 4d3 (p.39 bar list): not placed in 3D
b.Profile = std;
b.PerpendicularToDeck = true;                         // p.19 / DS 17 note: the deck slopes DOWN AWAY from the rail (superelevation rises to the east) ->
                                                      // the rail is placed perpendicular to the top of deck (Barrier Rail Orientation Detail)
b.BeginExtension = Len("4'-11 3/8\"");               // p.38 Elevation: ABUT. DIAPH. EXT. (S), dimensions along the gutter line
b.EndExtension = Len("4'-11 3/4\"");                 // (N); 4'-11 3/8 + 226'-10 1/4 (end to end of slab) + 4'-11 3/4 = 236'-9 3/8
b.PlanLength = Len("236'-9 3/8\"");                  // END TO END OF BARRIER RAIL (BID LENGTH) = END TO END OF STANDARD SECTION; bid 236.8 LF (p.3)
b.Source = "p.38 / DS 36 Part Sections C-C, F-F, D-D, G-G, H-H, Elevation; p.19 / DS 17 orientation; p.22 / DS 20 Detail C";
b.Zones = new();
foreach (var pier in new[] { "Pier 1", "Pier 2" })
{
    // CL barrier bump-out = where the CL pier crosses the edge of deck, square to the edge (p.22 Detail C: 90 deg 00'00").
    b.Zones.Add(new BarrierZone
    {
        Name = $"bump-out {pier} strip returns", Support = pier, Length = Len("5'-10\""), Profile = ret,
        Source = "p.38 Part Section G-G: strips 2'-5 from the gutter (10 1/2 deeper than 1'-6 1/2), 2'-11 + 2'-11 = 5'-10 centred on CL bump-out",
    });
    b.Zones.Add(new BarrierZone
    {
        Name = $"bump-out {pier}", Support = pier, Length = Len("4'-7 5/8\""), Profile = dd,
        DeckWidening = Len("1'-3\""), DeckWideningLength = Len("5'-2\""),
        Source = "p.38 Part Sections D-D / H-H: brick face 2'-1 from the gutter (8 deeper than 1'-5), 2'-3 13/16 + 2'-3 13/16 = 4'-7 5/8; p.22 Detail C deck 1'-3 x 5'-2 (2'-7 + 2'-7)",
    });
}
Print($"Woodbury barrier set: {b.ProfileName}, east side, {b.Zones.Count} zones (bump-outs at Pier 1 / Pier 2), extensions 4'-11 3/8 / 4'-11 3/4.");
