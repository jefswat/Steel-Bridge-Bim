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
        double tipAboveDeck = 54.864, bool mainSpanAhead = true, OspreyFoundation? foundation = null)
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
        var fo = foundation;
        double colFoot = fo != null ? sgn * fo.LegFootX : sgn * Ft(41), footY = fo != null ? fo.LegFootY : Ft(30);
        double propFoot = -sgn * Ft(9), propTop = sgn * Ft(18);
        double legLean = Math.Atan(colFoot * sgn / knee) * 180 / Math.PI;
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
        // in side view they lean back from the feet to the waist. With a foundation layout: straight chord from the given feet to the knees.
        double footSplay = Math.Atan((footY - colY) / knee) * 180 / Math.PI;
        foreach (double sy in new[] { -1.0, 1.0 })
            py.Legs.Add(new CsPylonLeg
            {
                Name = sy < 0 ? "Left lower leg" : "Right lower leg", Start = new CsXyz(colFoot, sy * footY, 0), Tip = new CsXyz(0, sy * colY, knee),
                StartLeanDeg = -sgn * legLean, StartSplayDeg = fo != null ? -sy * footSplay : -sy * 40, TipLeanDeg = -sgn * legLean,
                TipSplayDeg = fo != null ? -sy * footSplay : 0, StartHandle = 0.35, TipHandle = 0.45,
                // blade: deep in side view at the foot (~9 ft), tapering to ~3.5 ft at the waist (user markup of the aerial rendering)
                Section = new CsSection(Ft(4), fo?.LegFootDepth ?? Ft(9), Ft(3), Ft(3.5), In(1.25)), Segments = 24,
            });
        if (fo != null)
        {
            py.FootingLength = fo.LegFootingLength; py.FootingWidth = 2 * footY + fo.LegFootingLength; py.FootingThickness = Ft(6); py.FootingX = colFoot;
            // props from the knees, leaning forward over the main span to their own footings
            foreach (double sy in new[] { -1.0, 1.0 })
                d.Struts.Add(new CsStrut { Name = (sy < 0 ? "left" : "right") + " prop", From = new CsXyz(0, sy * colY, knee), To = new CsXyz(sgn * fo.PropFootX, sy * fo.PropFootY, sy < 0 ? fo.PropFootZLeft : fo.PropFootZRight),
                    Section = new CsSection(fo.PropWidth, fo.PropDepth) { Wall = In(1) }, FootingLength = Ft(10), FootingWidth = Ft(10), FootingThickness = Ft(4) });
        }
        else
        {
            py.FootingLength = Ft(16); py.FootingWidth = Ft(2 * 30 + 16); py.FootingThickness = Ft(6); py.FootingX = colFoot;
            foreach (double y in new[] { -colY, colY })
                d.Struts.Add(new CsStrut { Name = (y < 0 ? "left" : "right") + " prop", From = new CsXyz(propTop, y, deckTop - Ft(3)), To = new CsXyz(propFoot, y, 0),
                    Section = new CsSection(Ft(2.5), Ft(3)) { Wall = In(1) }, FootingLength = Ft(10), FootingWidth = Ft(10), FootingThickness = Ft(4) });
        }
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

    /// <summary>
    /// Conventional single-pylon cable-stayed footbridge: an H-pylon (two plumb legs straddling the deck, a deck beam the deck bears on, one or two
    /// cross beams below the anchorage zone) and two planes of semi-harp stays to the deck edges, over the main span and the back span. Starting
    /// proportions (all inputs): pylon above the walkway ~ 0.45 x the main-span stay reach (single pylon: flattest stay ~ 24 deg), back-span reach ~ 0.45 x the main span, anchorage zone on
    /// the top 40 % of the legs.
    /// </summary>
    /// <param name="pylonSupport">Support line (name) of the pylon.</param>
    /// <param name="pylonStation">Its station.</param>
    /// <param name="baseElevation">Top of the pylon footing.</param>
    /// <param name="deckTopAboveBase">Finished grade at the pylon minus the base elevation.</param>
    /// <param name="mainReach">Last main-span deck anchor from the pylon.</param>
    /// <param name="backReach">Last back-span deck anchor from the pylon.</param>
    /// <param name="towerAboveDeck">Leg tops above the walkway (0 = 0.45 x mainReach).</param>
    /// <param name="legOffset">Legs at +- this from the CL (0 = deck half width + 2'-6").</param>
    public static CableStayedDefinition Conventional(string pylonSupport, double pylonStation, double baseElevation, double deckTopAboveBase,
        double mainReach, double backReach, double towerAboveDeck = 0, double legOffset = 0, int mainStays = 10, int backStays = 10, bool mainSpanAhead = true)
    {
        double sgn = mainSpanAhead ? 1 : -1, deckTop = deckTopAboveBase;
        var d = new CableStayedDefinition
        {
            Concept = "Conventional single-pylon cable-stayed footbridge (H-pylon, semi-harp stays in two planes)",
            Source = "starting proportions (no design): pylon height ~0.45 main span (single pylon), back span ~0.45 main span, anchorage zone on the top 40 % of the legs",
            Unresolved = { "UNRESOLVED (concept): every dimension is a starting proportion for the shape study, not a design." },
            Deck = new CsDeckDefinition
            {
                TopWidth = Ft(16), BottomWidth = Ft(9), Depth = Ft(3), EdgeDepth = Ft(1), Material = CsMaterial.Steel, WallThickness = In(0.75),
                RingSpacing = Ft(5), BarrierHeight = Ft(4.5), BarrierThickness = In(1), BarrierInset = Ft(0.5), BarrierMaterial = "glass", BarrierDensity = 2500,
            },
        };
        double edge = Ft(8.5), legY = legOffset > 0 ? legOffset : Ft(8 + 2.5);
        double top = deckTop + (towerAboveDeck > 0 ? towerAboveDeck : 0.45 * mainReach);
        HPylon(d.Pylon, pylonSupport, baseElevation, deckTop - Ft(3) - Ft(2), top, legY, "", new CsSection(Ft(5), Ft(6), Ft(4), Ft(5), In(1.5)),
            new CsSection(Ft(4), Ft(4)) { Wall = In(1) }, Ft(14), Ft(6));
        for (int side = 0; side < 2; side++)
        {
            double off = side == 0 ? -edge : edge;
            string n = side == 0 ? "left" : "right";
            d.Stays.Add(new CsStayGroup
            {
                Name = n + " main span", Leg = side, Count = mainStays, FirstStation = pylonStation + sgn * Ft(40), LastStation = pylonStation + sgn * mainReach,
                DeckOffset = off, DeckAnchorHeight = -Ft(1), PylonFrom = 0.6, PylonTo = 0.97, Diameter = In(3),
            });
            d.Stays.Add(new CsStayGroup
            {
                Name = n + " back span", Leg = side, Count = backStays, FirstStation = pylonStation - sgn * Ft(40), LastStation = pylonStation - sgn * backReach,
                DeckOffset = off, DeckAnchorHeight = -Ft(1), PylonFrom = 0.6, PylonTo = 0.97, Diameter = In(3),
            });
        }
        d.Piers = new CsPierDefinition
        {
            ColumnWidth = Ft(4), FootingLength = Ft(12), FootingWidth = Ft(12), FootingThickness = Ft(4),
            AbutmentLength = Ft(6), AbutmentExtraWidth = Ft(1.5), AbutmentDepth = Ft(6),
        };
        d.Analysis.SuperimposedDeadLoad = 0.25 * 4448.22 / 0.3048;
        return d;
    }
    /// <summary>H-pylon: two plumb straight legs at +- <paramref name="legY"/> from base to <paramref name="top"/> (above the base), a deck beam at
    /// <paramref name="deckBeam"/>, a cross beam at 0.55 x top (below the anchorage zone) and a top beam; one footing under both legs.</summary>
    private static void HPylon(CsPylonDefinition py, string support, double baseElevation, double deckBeam, double top, double legY, string tag,
        CsSection leg, CsSection beam, double footingLength, double footingThickness)
    {
        py.Support = support; py.BaseElevation = baseElevation; py.TrunkHeight = 0;
        foreach (double sy in new[] { -1.0, 1.0 })
            py.Legs.Add(new CsPylonLeg
            {
                Name = tag.Length > 0 ? $"{tag} {(sy < 0 ? "left" : "right")} leg" : sy < 0 ? "Left leg" : "Right leg",
                Start = new CsXyz(0, sy * legY, 0), Tip = new CsXyz(0, sy * legY, top), Straight = true, Section = leg.Clone(), Segments = 8,
            });
        py.CrossBeams.Add(new CsCrossBeam { Name = "deck beam", LegA = 0, LegB = 1, Height = deckBeam, Section = beam.Clone() });
        py.CrossBeams.Add(new CsCrossBeam { Name = "cross beam", LegA = 0, LegB = 1, Height = 0.55 * top, Section = beam.Clone() });
        py.CrossBeams.Add(new CsCrossBeam { Name = "top beam", LegA = 0, LegB = 1, Height = top - beam.Depth / 2, Section = beam.Clone() });
        py.FootingLength = footingLength; py.FootingWidth = 2 * legY + footingLength; py.FootingThickness = footingThickness;
    }

    /// <summary>
    /// TWO-TOWER cable-stayed bridge with a steel EDGE GIRDER deck (starting proportions for a shape study, every value flagged):
    /// <list type="bullet">
    /// <item>TUNING FORK towers: two plumb legs per tower standing outside the deck edges, joined only by a strut BELOW the deck (no cross brace above
    /// the deck); stays anchor on the inner face of each leg (semi-harp, top 40 % of the leg).</item>
    /// <item>Deck (<see cref="CsEdgeGirderDeck"/>): two welded plate edge girders under the stay planes, floorbeams every 15 ft measured from each tower,
    /// a longitudinal support on the CL, precast deck panels with closure pours over every member; field splices <paramref name="spliceBeyondAnchor"/> (1 ft) past the far end of every stay anchorage.</item>
    /// <item>Stays every <paramref name="staySpacing"/> along each girder (anchorages at floorbeams), from <paramref name="staySpacing"/> off each tower
    /// to midspan less half a spacing and over each side span to <paramref name="backReachA"/> / <paramref name="backReachB"/>; anchored in "rocket
    /// launcher" pipes hung off the outside of the girder webs.</item>
    /// </list>
    /// Tower A is the up-station tower; leg numbers: A left 0, A right 1, B left 2, B right 3. The edge girder unit runs between the stations
    /// <paramref name="unitFrom"/> / <paramref name="unitTo"/> (NaN = the whole deck).
    /// </summary>
    public static CableStayedDefinition TwoTower(string pylonA, double staA, double baseA, double deckTopAboveBaseA,
        string pylonB, double staB, double baseB, double deckTopAboveBaseB, double backReachA, double backReachB,
        double unitFrom = double.NaN, double unitTo = double.NaN, CsEdgeGirderDeck? girders = null, double towerAboveDeck = 0,
        double staySpacing = 0, double spliceBeyondAnchor = 0, double stayDiameter = 0)
    {
        double main = staB - staA;
        // Member sizes: the New NY (Tappan Zee) Bridge main span as published (Scollard et al., TAC: edge girder web 5'-6", top flange 28 x 1 1/4, bottom
        // flange 36 x 1 3/4 - 4 (2 1/2 used), web 13/16 - 1 1/2 (1 used); floorbeams 4'-8" deep, 5'-6" at the stay anchorages; 10 3/4" precast deck, CIP infill
        // joints; anchor pipes 16 - 22 in OD) - a reference for the proportions, not a design for this span. Not published (concept values): floorbeam
        // flanges, the longitudinal support, panel seats, anchorage plate sizes.
        var eg = girders ?? new CsEdgeGirderDeck
        {
            GirderOffset = Ft(53.5), WebDepth = In(66), WebThickness = In(1), TopFlangeWidth = In(28), TopFlangeThickness = In(1.25),
            BottomFlangeWidth = In(36), BottomFlangeThickness = In(2.5), SlabThickness = In(10.75), SlabLeft = Ft(55), SlabRight = Ft(55),
            FloorbeamSpacing = Ft(15), FloorbeamWebDepth = In(56), FloorbeamAtAnchorWebDepth = In(66), FloorbeamWebThickness = In(0.625), FloorbeamTopFlangeWidth = In(18),
            FloorbeamTopFlangeThickness = In(1.25), FloorbeamBottomFlangeWidth = In(24), FloorbeamBottomFlangeThickness = In(2),
            StringerOffsets = { 0 }, StringerWebDepth = In(30), StringerWebThickness = In(0.5), StringerFlangeWidth = In(16), StringerFlangeThickness = In(1),
            Precast = new CsPrecastDeck { PanelSeat = In(2) },
            SpliceLength = Ft(4), SpliceWebPlateThickness = In(0.75), SpliceFlangePlateThickness = In(1),
            Anchorage = new CsStayAnchorage
            {
                AxisOutboard = In(36 / 2.0 + 8 + 6), PipeAboveTopOfSteel = Ft(5), PipeBelowBottomFlange = Ft(2), PipeDiameter = In(16), PipeWall = In(0.75),
                BearingPlateSize = In(30), BearingPlateThickness = In(4), StemThickness = In(2), MountingPlateWidth = In(24), MountingPlateThickness = In(1.5),
                FlangeLength = In(24), FlangeThickness = In(1.5),
            },
        };
        eg.FromStation = unitFrom; eg.ToStation = unitTo;
        eg.SpliceBeyondAnchor = spliceBeyondAnchor > 0 ? spliceBeyondAnchor : Ft(1);   // clear past the far end of the anchorage (bearing plate)
        double spacing = staySpacing > 0 ? staySpacing : Ft(45);
        var d = new CableStayedDefinition
        {
            Concept = "Two-tower cable-stayed bridge (tuning fork towers, steel edge girder deck, rocket launcher anchorages)",
            Source = "starting proportions (no design): towers ~0.22 x main span above the deck, side spans ~0.4 x main span, stays every 45 ft at the floorbeams, anchorage zone on the top 40 % of the legs",
            Unresolved = { "UNRESOLVED (concept): every dimension is a starting proportion for the shape study, not a design." },
            Deck = new CsDeckDefinition
            {
                TopWidth = eg.SlabLeft + eg.SlabRight, BottomWidth = 2 * eg.GirderOffset, Depth = eg.Depth, EdgeDepth = eg.Depth, Material = CsMaterial.Steel,
                RingSpacing = Ft(10), BarrierHeight = In(42), BarrierThickness = In(15), BarrierInset = In(6), BarrierMaterial = "concrete", BarrierDensity = 2400,
                EdgeGirders = eg,
            },
        };
        // tuning fork: hollow concrete legs (2 ft walls), inner faces 2 ft clear of the slab edges, 14 ft across x 20 ft along at the base tapering to
        // 10 x 16 ft at the top (the New NY towers: 25 x 26 ft -> 15 x 17 ft for a 409 ft tower); steel anchorage trays cast in the upper legs
        var leg = new CsSection(Ft(14), Ft(20), Ft(10), Ft(16), Ft(2)) { Material = CsMaterial.Concrete };
        double legY = Math.Max(eg.SlabLeft, eg.SlabRight) + Ft(2) + leg.Width / 2;
        double above = towerAboveDeck > 0 ? towerAboveDeck : 0.22 * main;
        double inset = 0;   // the trays place the tower anchors
        var strut = new CsSection(Ft(12), Ft(10)) { Material = CsMaterial.Concrete };
        // anchorage trays (New NY Fig. 8: trays about 6 ft apart vertically; plate sizes are concept values)
        var tray = new CsTowerAnchorage
        {
            TrayWidth = Ft(3.5), TrayHeight = Ft(5.5), WebThickness = In(1.5), FloorThickness = In(1.25), EndPlateThickness = In(2.5),
            BearingPlateSize = In(30), BearingPlateThickness = In(4), BearingFromEnd = Ft(2.5), RibThickness = In(1.5),
            GuidePipeDiameter = In(14), GuidePipeWall = In(0.5), CableOffset = Ft(0.8),
        };
        double level = Ft(6), topGap = Ft(10);   // tray levels 6 ft apart from 10 ft below the leg top
        var pb = new CsPylonDefinition();
        d.MorePylons = new() { pb };
        foreach (var (py, sup, bse, top, tag) in new[] { (d.Pylon, pylonA, baseA, deckTopAboveBaseA, "Tower A"), (pb, pylonB, baseB, deckTopAboveBaseB, "Tower B") })
        {
            py.Support = sup; py.BaseElevation = bse; py.TrunkHeight = 0; py.Anchorage = tray.Clone();
            foreach (double sy in new[] { -1.0, 1.0 })
                py.Legs.Add(new CsPylonLeg
                {
                    Name = $"{tag} {(sy < 0 ? "left" : "right")} leg", Start = new CsXyz(0, sy * legY, 0), Tip = new CsXyz(0, sy * legY, top + above), Straight = true,
                    Section = leg.Clone(), Segments = 8,
                });
            // the fork's only cross member: a strut under the deck (the girders bear on it), 3 ft below the girder bottom
            py.CrossBeams.Add(new CsCrossBeam { Name = "strut below the deck", LegA = 0, LegB = 1, Height = top - eg.Depth - Ft(3) - strut.Depth / 2, Section = strut });
            py.FootingLength = Ft(40); py.FootingWidth = 2 * legY + Ft(40); py.FootingThickness = Ft(12);
        }
        double wpUp = -eg.TopOfSteelBelowGrade;   // stay working point: on the pipe axis at the top of steel
        double plane = eg.GirderOffset + (eg.Anchorage?.AxisOutboard ?? 0);
        int nMain = (int)Math.Floor((main / 2 - spacing / 2 + 1e-6) / spacing);
        foreach (var (tower, sta, l0, back, toMain, legLen) in new[] { ("A", staA, 0, backReachA, 1.0, deckTopAboveBaseA + above), ("B", staB, 2, backReachB, -1.0, deckTopAboveBaseB + above) })
        {
            int nSide = (int)Math.Floor((back + 1e-6) / spacing);
            // main and side stays share the tray levels (opposing pairs), the farthest stays at the top
            double Fr(int k) => (legLen - topGap - k * level) / legLen;
            for (int side = 0; side < 2; side++)
            {
                double off = side == 0 ? -plane : plane;
                string n = $"{tower} {(side == 0 ? "left" : "right")}";
                foreach (var (what, count, dir) in new[] { ("main", nMain, toMain), ("side", nSide, -toMain) })
                    d.Stays.Add(new CsStayGroup
                    {
                        Name = $"{n} {what}", Leg = l0 + side, Count = count, FirstStation = sta + dir * spacing, LastStation = sta + dir * spacing * count,
                        DeckOffset = off, DeckAnchorHeight = wpUp, PylonFrom = Fr(count - 1), PylonTo = Fr(0), PylonAnchorInset = inset,
                        Diameter = stayDiameter > 0 ? stayDiameter : In(7),
                    });
            }
        }
        d.Piers = new CsPierDefinition
        {
            ColumnWidth = Ft(40), ColumnDepth = Ft(8), FootingLength = Ft(20), FootingWidth = Ft(50), FootingThickness = Ft(6),
            AbutmentLength = Ft(10), AbutmentExtraWidth = Ft(2), AbutmentDepth = Ft(10),
        };
        d.Analysis.PedestrianLoad = 0;   // highway live load is not in the first-cut estimate (analysis comes later)
        d.Analysis.SuperimposedDeadLoad = 20 * 4.44822 / (0.3048 * 0.3048) * (eg.SlabLeft + eg.SlabRight);   // future wearing surface 20 psf (barriers are modeled)
        return d;
    }
}


/// <summary>Foundation layout for <see cref="CableStayedConcepts.Osprey"/> (pylon frame: x along the bridge toward the main span, y across, metres from
/// the pylon point). Lower-leg feet at (LegFootX, +-LegFootY), one footing under both; props run from the knees to their feet at (PropFootX, +-PropFootY).</summary>
public sealed class OspreyFoundation
{
    public double LegFootX { get; set; }
    public double LegFootY { get; set; } = 15.5 * 0.3048;
    /// <summary>Lower-leg section depth along the bridge at the foot.</summary>
    public double LegFootDepth { get; set; } = 4 * 0.3048;
    /// <summary>Pylon footing length along the bridge (its width = 2 LegFootY + this).</summary>
    public double LegFootingLength { get; set; } = 10 * 0.3048;
    public double PropFootX { get; set; } = 65 * 0.3048;
    public double PropFootY { get; set; } = 25 * 0.3048;
    /// <summary>Prop foot heights above the pylon base (the ground where each prop lands), left / right of the alignment.</summary>
    public double PropFootZLeft { get; set; }
    public double PropFootZRight { get; set; }
    public double PropWidth { get; set; } = 3 * 0.3048;
    public double PropDepth { get; set; } = 6 * 0.3048;
}
