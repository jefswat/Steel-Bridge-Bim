// MnDOT Br. 02069 - DECK SLAB reinforcing S401M .. S404M (p18 deck plan, p19 transverse section + Detail A, bill p21) as the program's plan deck
// reinforcing (DeckReinforcementDefinition.Plan). Called by build-br02069.csx (re-runnable).
//   sbm run samples\plans\rebar-deck-br02069.csx "samples\plans\Br02069 TH65.sbm"
// Read from the sheets:
//  * Transverse (p18): "136 SPACES @ 9" = 102'-0", 137-S403M TOP TRANSVERSE" / "137-S404M BOTTOM TRANSVERSE", 5 1/2" from each deck end (5 1/2" +
//    102'-0" + 5 1/2" = 102'-11"); radial = normal to the tangent (skew 0). S403M 40'-8" = 41'-0" - 2 x 2" (Detail A "2" CLR. TOP"); S404M 40'-3"
//    (Detail A "4" CLR. BOTTOM" at the W edge; the bill length centres it: 4 1/2" each side).
//  * Cover (p19 transverse section): top 3" clr ("3" CLR."), bottom 1" clr ("1" CLR."); longitudinal bars inside the transverse ones.
//  * Longitudinal (p19): "29 LINES OF S401M & S402M TOP (STAGGERED)" at 3 1/2" + 8 1/2" + 26 sps @ 1'-6" + 8 1/2" + 3 1/2"; "55 LINES OF 1-S401M &
//    1-S402M BOTTOM (STAGGERED)" at 7" + 5" + 52 sps @ 9" + 5" + 7"; each line one S401M 60'-0" + one S402M 45'-2", "2'-7" MIN. LAP" (p18):
//    60'-0" + 45'-2" - 2'-7" = 102'-7" = 102'-11" - 2 x 2" end clear. Staggered: the W line starts with S401M at the S end (p18 plan labels),
//    the next line with S402M (DeckLongZone.AlternateEnds).
using SBM.Core.Bridge;

var br = Doc.Project.Bridges.First(b => b.Name == "Br02069 TH65 SB");
double I(double x) => In(x);
var r = br.Deck.Reinforcement;
r.Enabled = true;
r.EpoxyCoated = true;                  // chromium-epoxy (4% Cr) "M" bars (bill p21)
r.TopCover = I(3);
r.BottomCover = I(1);
r.SideCover = I(2);
var plan = new DeckPlanReinforcement { LongitudinalEndCover = I(2), LongitudinalBeginCover = I(2), WeightTolerance = 0.005 };
DeckTransverseZone Tz(DeckBarLayer layer, string mark, double side) => new()
{
    Name = mark, Layer = layer, Size = "#4", Reference = DeckBarSide.Left, StartDistance = I(5.5), Spacing = I(9), Count = 137, FirstFrom = DeckBarSide.Left,
    Pieces = { new DeckLinePiece { Mark = mark, Length = 0 } }, SideClear = side,
};
plan.Transverse.Add(Tz(DeckBarLayer.Top, "S403M", 0));
plan.Transverse.Add(Tz(DeckBarLayer.Bottom, "S404M", I(4.5)));
var top = new List<double> { 3.5, 12 }; for (int k = 1; k <= 26; k++) top.Add(12 + 18 * k); top.Add(top[^1] + 8.5);
var bot = new List<double> { 7 }; for (int k = 0; k <= 52; k++) bot.Add(12 + 9 * k); bot.Add(bot[^1] + 5);
DeckLongZone Lz(string name, List<double> pos, DeckBarLayer layer) => new()
{
    Name = name, Left = DeckLongRef.Edge(DeckBarSide.Left), Right = DeckLongRef.Edge(DeckBarSide.Right), Positions = pos.Select(I).ToList(),
    PositionLayers = pos.Select(_ => layer).ToList(), FirstLayer = layer, Size = "#4", Mark = "S401M", ClosureMark = "S402M", StockLength = Ft(60), Lap = Ft(2) + I(7),
    AlternateEnds = true,
};
plan.Longitudinal.Add(Lz("top (29 lines)", top, DeckBarLayer.Top));
plan.Longitudinal.Add(Lz("bottom (55 lines)", bot, DeckBarLayer.Bottom));
foreach (var (m, n, len, lb, loc) in new[] { ("S401M", 84, 60.0, 3366.7, "DECK - TOP & BOT. LONGIT."), ("S402M", 84, 45 + 2.0 / 12, 2534.4, "DECK - TOP & BOT. LONGIT."),
    ("S403M", 137, 40 + 8.0 / 12, 3721.7, "DECK - TOP TRANSV"), ("S404M", 137, 40.25, 3683.5, "DECK - BOT. TRANSV") })
    plan.PlanBarList.Add(new DeckPlanBarLine { Mark = m, Size = "#4", Count = n, Length = Ft(len), WeightLb = lb, Location = loc });
r.Plan = plan;
Print($"Br02069 deck plan bars: {top.Count} top + {bot.Count} bottom longitudinal lines, 2 x 137 transverse");
