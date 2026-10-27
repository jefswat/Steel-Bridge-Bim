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
    /// "Osprey" footbridge (Donald MacDonald Architects concept for the Richmond Greenway, renderings Dec 2022 / Grandview Independent 2026): one pier,
    /// a trunk splitting below the deck into two curved legs that straddle the deck, splay apart and sweep back away from the main span (wings), a portal
    /// cross beam above the walkway, a fan / semi-harp of stays from each leg to its deck edge, a few back stays, a prop strut, thin steel deck with glass
    /// barriers. Heights are above the pylon base (the ground at the pier); stations of the stay anchors are distances from the pylon station along
    /// the main span direction (<paramref name="mainSpanAhead"/> = main span up-station).
    /// </summary>
    /// <param name="pylonSupport">Support line name of the pylon.</param>
    /// <param name="pylonStation">Its station (stay anchors are placed from it).</param>
    /// <param name="baseElevation">Pylon base (top of footing, ground at the pier).</param>
    /// <param name="deckTopAboveBase">Finished grade at the pylon minus the base elevation (sets the junction below the deck and the cross beam above it).</param>
    /// <param name="height">Pylon tip above the base (the renderings' "180 feet at its tallest point").</param>
    public static CableStayedDefinition Osprey(string pylonSupport, double pylonStation, double baseElevation, double deckTopAboveBase,
        double height = 54.864, bool mainSpanAhead = true)
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
        var py = d.Pylon;
        py.Support = pylonSupport;
        py.BaseElevation = baseElevation;
        py.TrunkHeight = deckTop - Ft(20);
        py.TrunkSection = new CsSection(Ft(14), Ft(8)) { Wall = In(1.25) };
        py.FootingLength = Ft(30); py.FootingWidth = Ft(36); py.FootingThickness = Ft(6);
        double lean = -sgn;   // the legs sweep back, away from the main span
        var leg = new CsPylonLeg
        {
            Name = "Left wing",
            Start = new CsXyz(0, -Ft(5), py.TrunkHeight),
            Tip = new CsXyz(lean * Ft(50), -Ft(40), height),
            StartLeanDeg = lean * 3, StartSplayDeg = -25, TipLeanDeg = lean * 20, TipSplayDeg = -12,
            StartHandle = 0.35, TipHandle = 0.3,
            Section = new CsSection(Ft(4), Ft(6), Ft(2), Ft(2.5), In(1.25)),
            Segments = 40,
        };
        py.Legs.Add(leg);
        py.Legs.Add(leg.Mirrored("Right wing"));
        py.CrossBeams.Add(new CsCrossBeam { Name = "portal", LegA = 0, LegB = 1, Height = deckTop + Ft(13), Section = new CsSection(Ft(3), Ft(3)) { Wall = In(1) } });
        d.Struts.Add(new CsStrut { Name = "prop", From = new CsXyz(0, Ft(2), py.TrunkHeight - Ft(4)), To = new CsXyz(sgn * Ft(2), Ft(32), 0),
            Section = new CsSection(Ft(3), Ft(2.5), Ft(2), Ft(2)) { Wall = In(1) }, FootingLength = Ft(10), FootingWidth = Ft(10), FootingThickness = Ft(4) });

        double edge = Ft(8.5);
        for (int side = 0; side < 2; side++)
        {
            double off = side == 0 ? -edge : edge;
            d.Stays.Add(new CsStayGroup
            {
                Name = side == 0 ? "front left" : "front right", Leg = side, Count = 10,
                FirstStation = pylonStation + sgn * Ft(50), LastStation = pylonStation + sgn * Ft(350), DeckOffset = off, DeckAnchorHeight = -Ft(1),
                PylonFrom = 0.45, PylonTo = 0.97, Diameter = In(3),
            });
            d.Stays.Add(new CsStayGroup
            {
                Name = side == 0 ? "back left" : "back right", Leg = side, Count = 4,
                FirstStation = pylonStation - sgn * Ft(40), LastStation = pylonStation - sgn * Ft(130), DeckOffset = off, DeckAnchorHeight = -Ft(1),
                PylonFrom = 0.55, PylonTo = 0.92, Diameter = In(3),
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
