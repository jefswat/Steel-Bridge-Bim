using SBM.Core.Geometry;

namespace SBM.Core.Bridge;

/// <summary>
/// Starting shapes for the cable-stayed generator: every value is a plain input the user changes (the generator has no built-in rule for any of them).
/// The concept values are READINGS of renderings / sketches and are flagged as such on every part (<see cref="CableStayedDefinition.Unresolved"/>).
/// </summary>
public static class CableStayedConcepts
{
    private static double Ft(double x) => x * 0.3048;
    private static double In(double x) => x * 0.0254;

    /// <summary>
    /// "Osprey" footbridge (Donald MacDonald Architects concept for the Richmond Greenway, renderings Dec 2022 / Grandview Independent 2026 / Pogo Park
    /// Bridge Week flyer; form per the user's elevation sketch): transversely symmetric; plumb columns straddling the deck (side view: leaning back from
    /// their footings to a knee above the walkway), a cross beam at the knees, wings leaning forward over the main span and splaying outward, props from
    /// the ground behind crossing the columns to the deck edges, stay fans from both wings to the main span deck edges, two back stays per wing. Heights are above the pylon base (the ground at the pier); stations of the stay anchors are distances from the pylon station along
    /// the main span direction (<paramref name="mainSpanAhead"/> = main span up-station).
    /// </summary>
    /// <param name="pylonSupport">Support line name of the pylon.</param>
    /// <param name="pylonStation">Its station (stay anchors are placed from it).</param>
    /// <param name="baseElevation">Pylon base (top of footing, ground at the pier).</param>
    /// <param name="deckTopAboveBase">Finished grade at the pylon minus the base elevation (sets the junction below the deck and the cross beam above it).</param>
    /// <param name="tipAboveDeck">Wing tips above the walkway at the pylon. The article's "rise 180 feet at its tallest point" read as above the walkway:
    /// the two-view fit (aerial + path view) gives the same fit quality for this reading and for a 180 ft wing length (the fitted wing is ~177 ft long),
    /// and a worse one for 180 ft above the road in the cut.</param>
    public static CableStayedDefinition Osprey(string pylonSupport, double pylonStation, double baseElevation, double deckTopAboveBase,
        double tipAboveDeck = 54.864, bool mainSpanAhead = true)
    {
        double sgn = mainSpanAhead ? 1 : -1;
        double deckTop = deckTopAboveBase;
        var d = new CableStayedDefinition
        {
            Concept = "Osprey cable-stayed footbridge concept (starting shape fitted to renderings)",
            Source = "Donald MacDonald Architects concept renderings (Richmond Greenway Bridge, Dec 2022)",
            Unresolved =
            {
                "UNRESOLVED (renderings): every dimension (pylon legs, sections, stays, deck, piers) is read from concept renderings - no drawings exist; values are the starting point for the shape study.",
                "UNRESOLVED (renderings): stay arrangement and count, back stays and the prop strut are a reading of the renderings, not a design.",
            },
            Deck = new CsDeckDefinition
            {
                TopWidth = Ft(16), BottomWidth = Ft(9), Depth = Ft(3), EdgeDepth = Ft(1), Material = CsMaterial.Steel, WallThickness = In(0.75),
                RingSpacing = Ft(5), BarrierHeight = Ft(4.5), BarrierThickness = In(1), BarrierInset = Ft(0.5), BarrierMaterial = "glass", BarrierDensity = 2500,
            },
        };
        // Form per the user's elevation sketch (2026-10-26; the renderings are perspective views): TRANSVERSELY SYMMETRIC.
        //  transverse elevation: two plumb columns just outside the deck edges, a cross beam ~26 ft above the walkway (the knee), wings splaying outward from
        //  the knees;
        //  side elevation: each column leans back from its footing on the main-span side up to the knee; from the knee the wing leans strongly forward over
        //  the main span; a prop from the ground behind the knee crosses the column and supports the deck edge (the "X" below the walkway).
        // Stays: a fan from each wing to its own deck edge over the main span, two back stays per wing.
        var py = d.Pylon;
        py.Support = pylonSupport;
        py.BaseElevation = baseElevation;
        py.TrunkHeight = 0;
        // Wings fitted to two renderings (aerial + view back along the path, cameras solved with the shape; 12 px RMS) with the tips held 180 ft above the
        // walkway (the reported "180 feet at its tallest point"; a three-view fit at rendering scale gave ~420 ft, judged far too tall by the user):
        // knee 37 ft above the walkway, upper cross beam 22 ft above it, tips 81 ft forward / 64 ft out from the CL; side view: lean 25 deg from vertical at
        // the knee increasing to 41 deg at the tip; transverse: nearly straight, splay 16 deg at the knee, 14 deg at the tip. The tip reach scales with
        // the rise when tipAboveDeck changes.
        double edge = Ft(8.5), colY = Ft(9.75), knee = deckTop + Ft(37);
        double colFoot = sgn * Ft(41), footY = Ft(30), propFoot = -sgn * Ft(9), propTop = sgn * Ft(18);
        double legLean = Math.Atan(Ft(41) / knee) * 180 / Math.PI;
        double tipZ = deckTop + tipAboveDeck, sc = (tipZ - knee) / Ft(182 - 37);
        foreach (double sy in new[] { -1.0, 1.0 })
            py.Legs.Add(new CsPylonLeg
            {
                Name = sy < 0 ? "Left wing" : "Right wing", Start = new CsXyz(0, sy * colY, knee),
                Tip = new CsXyz(sgn * Ft(80.8) * sc, sy * (colY + (Ft(63.9) - colY) * sc), tipZ),
                StartLeanDeg = sgn * 24.6, StartSplayDeg = sy * 16.4, TipLeanDeg = sgn * 40.6, TipSplayDeg = sy * 14.3, StartHandle = 0.16, TipHandle = 0.65,
                Section = new CsSection(Ft(4), Ft(4), Ft(1.75), Ft(2), In(1.25)), Segments = 40,
            });
        // lower legs (legs 2 / 3): from the feet, flared out transversely, curving in to the waist (plumb there) just outside the deck edge;
        // in side view they lean back from the feet to the waist
        foreach (double sy in new[] { -1.0, 1.0 })
            py.Legs.Add(new CsPylonLeg
            {
                Name = sy < 0 ? "Left lower leg" : "Right lower leg", Start = new CsXyz(colFoot, sy * footY, 0), Tip = new CsXyz(0, sy * colY, knee),
                StartLeanDeg = -sgn * legLean, StartSplayDeg = -sy * 40, TipLeanDeg = -sgn * legLean, TipSplayDeg = 0, StartHandle = 0.35, TipHandle = 0.45,
                // blade: deep in side view at the foot (~9 ft), tapering to ~3.5 ft at the waist (user markup of the aerial rendering)
                Section = new CsSection(Ft(4), Ft(9), Ft(3), Ft(3.5), In(1.25)), Segments = 24,
            });
        py.FootingLength = Ft(16); py.FootingWidth = Ft(2 * 30 + 16); py.FootingThickness = Ft(6); py.FootingX = colFoot;
        foreach (double y in new[] { -colY, colY })
            d.Struts.Add(new CsStrut { Name = (y < 0 ? "left" : "right") + " prop", From = new CsXyz(propTop, y, deckTop - Ft(3)), To = new CsXyz(propFoot, y, 0),
                Section = new CsSection(Ft(2.5), Ft(3)) { Wall = In(1) }, FootingLength = Ft(10), FootingWidth = Ft(10), FootingThickness = Ft(4) });
        py.CrossBeams.Add(new CsCrossBeam { Name = "strut", LegA = 2, LegB = 3, Height = deckTop + Ft(22), Section = new CsSection(Ft(3), Ft(3)) { Wall = In(1) } });
        // deck beam under the walkway between the lower legs (the deck bears on it; user markup of the top view rendering)
        py.CrossBeams.Add(new CsCrossBeam { Name = "deck beam", LegA = 2, LegB = 3, Height = deckTop - Ft(3) - Ft(1.5), Section = new CsSection(Ft(4), Ft(3)) { Wall = In(1) } });

        for (int side = 0; side < 2; side++)
        {
            double off = side == 0 ? -edge : edge;
            string n = side == 0 ? "left" : "right";
            d.Stays.Add(new CsStayGroup
            {
                Name = n + " fan", Leg = side, Count = 12, FirstStation = pylonStation + sgn * Ft(40), LastStation = pylonStation + sgn * Ft(420),
                DeckOffset = off, DeckAnchorHeight = -Ft(1), PylonFrom = 0.3, PylonTo = 1.0, Diameter = In(3),
            });
            d.Stays.Add(new CsStayGroup
            {
                Name = n + " back stays", Leg = side, Count = 2, FirstStation = pylonStation - sgn * Ft(100), LastStation = pylonStation - sgn * Ft(200),
                DeckOffset = off, DeckAnchorHeight = -Ft(1), PylonFrom = 0.9, PylonTo = 1.0, Diameter = In(3),
            });
        }
        d.Piers = new CsPierDefinition
        {
            ColumnWidth = Ft(4), FootingLength = Ft(12), FootingWidth = Ft(12), FootingThickness = Ft(4),
            AbutmentLength = Ft(6), AbutmentExtraWidth = Ft(1.5), AbutmentDepth = Ft(6),
        };
        d.Analysis.SuperimposedDeadLoad = 0.25 * 4448.22 / 0.3048;   // 0.25 kip/ft: surfacing, lighting, handrails beyond the glass panels
        return d;
    }
}
