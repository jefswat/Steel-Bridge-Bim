// PTC / NJTA DELAWARE RIVER BRIDGE (1954-57, AS-BUILT) - DECK AND EVERYTHING ON IT, on all seven truss-generator bridges of the PTC model:
// slab 6 3/4 + 1" thickening over every stringer (p.107), curb + parapet wall (p.107 Section A), aluminum parapet railing (p.107 / p.108),
// expansion dams (p.46 / p.54 / p.68 / p.91 / p.92 finger dams). Service walkways / ladders / platforms: walkways-ptc.csx (p.101-106).
// Generic inputs used: TrussDeckDefinition.Haunch*, TrussDefinition.LineItems (concrete curb / parapet, steel dam plates / walkway gratings),
// PedestrianRailingDefinition (posts + rail on the parapet). Re-runnable (replaces its own items), run AFTER truss-ptc-arch.csx, truss-ptc-approach.csx and
// girders-ptc.csx (they rebuild Truss):
//   sbm run samples\plans\deck-ptc.csx "samples\plans\PTC Delaware River Bridge.sbm"
// Data: docs/plan-data/ptc-delaware-river/deck/{deck,parapets,walkways,expansion-dams}.csv, connections/dams.csv (every row cites its page).
// User rules: as-built set only; nothing invented: every value the sheets do not give is an UNRESOLVED flag with the page and a plain reason;
// differences reported, never adjusted. No reinforcement (user: "I don't want rebar").
using SBM.Core;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Steel;

string outPath = Args.Length > 0 ? Args[0] : (Doc.FilePath ?? @"samples\plans\PTC Delaware River Bridge.sbm");
string U(string pages, string item, string why, string then = "") => UnresolvedFlag.Text(pages, item, why, then);
double FI(double ft, double inch) => Ft(ft) + In(inch);
TrussUV UV(double uIn, double vIn) => new TrussUV { U = In(uIn), V = In(vIn) };

var names = new[] { "PTC Girder spans 1-10", "PTC 3-span truss 11-13", "PTC 4-span truss 14-17", "PTC Arch truss 18-20", "PTC 4-span truss 21-24", "PTC 3-span truss 25-27", "PTC Girder spans 28-31" };
var bridges = names.Select(n => Doc.Project.Bridges.FirstOrDefault(b => b.Name == n)).ToList();
if (bridges.Any(b => b?.Truss?.Floor?.Deck == null)) throw new InvalidOperationException("Run the truss / girder scripts first: every PTC bridge needs Truss.Floor.Deck.");

const double CurbOff = 38.25;              // p.107: 38'-3 CL roadway to curb (gutter line), 76'-6 curb to curb
double parapetOut = In(34);                // p.107 Section A: 2'-10 = 2'-0 curb + 10 in wall, gutter line to the outside face (82'-2 overall, p.11 / p.38)

// ---------------------------------------------------------------- floor beam levels read from the generated model (walkways hang at them)
var model0 = names.ToDictionary(n => n, n => Doc.Model(n));
(double zMin, double zMax, double sta)? PartBox(string bridge, Func<SteelPart, bool> pick)
{
    var p = model0[bridge].Steel?.Parts.FirstOrDefault(pick);
    if (p == null || p.Mesh.IsEmpty) return null;
    var bb = p.Mesh.Bounds();
    var c = new Vec2((bb.Min.X + bb.Max.X) / 2, (bb.Min.Y + bb.Max.Y) / 2);
    return (bb.Min.Z, bb.Max.Z, Doc.Project.Alignments.First(a => a.Name == Doc.Project.Bridges.First(b => b.Name == bridge).AlignmentName).Inverse(c).Station);
}

foreach (var br in bridges)
{
    var t = br!.Truss!;
    var al = Doc.Project.Alignments.First(a => a.Name == br.AlignmentName);
    var dk = t.Floor!.Deck!;
    var items = new List<TrussLineItemDefinition>();
    var flags = new List<string>();

    // ---- slab (p.107): 6 3/4 between the outside faces of the parapets, 1 in thickening 1'-5 wide at the bottom with 12:12 sides over every stringer
    dk.Thickness = In(6.75);
    dk.LeftOffset = -(Ft(CurbOff) + parapetOut); dk.RightOffset = Ft(CurbOff) + parapetOut;
    dk.HaunchDepth = In(1); dk.HaunchBottomWidth = FI(1, 5); dk.HaunchSideSlope = 1;
    dk.Source = "p.107 (half cross section, Section A)";
    dk.Unresolved = U("p.107, p.55", "slab edge under the parapet and the haunch depth", "Section A prints 7 3/4 / 7 7/8 at the curb and the 1 in thickening (7 3/4 - 6 3/4) is derived, not printed; the slab edge under the wall is drawn with a 4 1/4 in drop not dimensioned",
        "6 3/4 slab (crown 1/8 in per ft) from outside face to outside face of the parapets (82'-2), 1 in x 1'-5 thickening with 12:12 sides under every stringer line; CL key joint / pipe sockets / drip grooves not built.");

    // ---- curb + parapet (p.107 Section A): U outward from the gutter line, V above the finished grade at the gutter line
    // curb face 9 in (W.P. at its top), curb top 2'-0 rising 1/8 in per ft, wall 10 in x 1'-9 above the curb top; the bottom follows the slab top (crown 1/8 in per ft)
    var prof = new List<TrussUV> { UV(0, 0), UV(0, 9), UV(24, 9.25), UV(24, 30.25), UV(34, 30.25), UV(34, -34.0 / 96) };
    foreach (int side in new[] { -1, 1 })
        items.Add(new TrussLineItemDefinition
        {
            Name = side < 0 ? "Left curb + parapet" : "Right curb + parapet", Kind = TrussLineItemKind.Concrete, ReferenceOffset = side * Ft(CurbOff), Side = side,
            From = dk.BeginStation, To = dk.EndStation, Profile = prof, Material = "Class A concrete", Source = "p.107 Section A / B",
            Unresolved = U("p.107, p.108", "curb + parapet details", "the 3/4 in radius at the curb face, the chamfers, the drainage openings in the curb (2'-2 / 2'-0 alternating, size not read), scoring and the openings at the expansion dams are drawn without all dimensions",
                "one solid section (curb 2'-0 x 9 in, curb top +1/4 in, wall 10 in x 1'-9 above the curb top) continuous over the deck length."),
        });
    br.Deck.Barrier.Type = BarrierType.Custom;
    br.Deck.Barrier.ProfileName = "PTC curb + parapet wall (p.107)";

    // ---- aluminum parapet railing (p.107 / p.108): oval rail 8 x 5 5/8 (3/16 wall) on cast posts 10 x 7 at the base, rail top 1'-6 above the parapet top
    br.Railings = new List<PedestrianRailingDefinition>();
    foreach (int side in new[] { -1, 1 })
        br.Railings.Add(new PedestrianRailingDefinition
        {
            Name = side < 0 ? "Left parapet railing" : "Right parapet railing", TypeName = "PTC aluminum parapet railing (p.107 / p.108)", Kind = RailingKind.Guardrail,
            Offset = side * (Ft(CurbOff) + In(29)), BeginStation = dk.BeginStation, EndStation = dk.EndStation, BaseProbe = 0, Material = "Aluminum (rail ASTM B235 GS10A-T6, posts B108 356-T6)", Density = 2700,
            Source = "p.107 / p.108", FlagsDimensionOnly = true,
            Post = new RailPostDefinition
            {
                Profile = new() { new(-In(5), 0), new(In(5), 0), new(In(3), In(15.1875)), new(-In(3), In(15.1875)) }, Thickness = In(7), Spacing = FI(5, 8.75), EndDistance = FI(1, 2.125),
                Description = "cast aluminum post 10 x 7 at the base, 6 in at the top",
                Unresolved = U("p.107, p.108", "post layout and shape", "the posts are laid out per railing panel (31'-0 / 30'-5 3/4 panels: 5 spaces with 1'-2 1/8 to the joints, special panels on p.108) and the cast post is hollow with a 3/8 web",
                    "solid tapered post 10 in -> 6 in x 7 in, equally spaced at most 5'-8 3/4 over each bridge run, 1'-2 1/8 from its ends; anchor bolts and pads not built."),
            },
            Members = new()
            {
                new RailMemberDefinition { Name = "oval rail", Kind = RailMemberKind.Rail, Width = In(8), Height = In(5.625), Center = new RailPoint(0, In(18 - 5.625 / 2)), Description = "oval aluminum tube 8 x 5 5/8 x 3/16",
                    MassPerLength = 7.0, Unresolved = U("p.108", "rail section", "the oval tube is 8 in +- x 5 5/8 in +- with a 3/16 wall", "rectangular envelope 8 x 5 5/8, mass of the tube (about 4.7 lb/ft)") },
            },
        });

    br.Truss!.LineItems = items;
}

// ---------------------------------------------------------------- expansion dams (p.46 / p.54 / p.68 / p.91 / p.92): two interlocking finger plates per dam
// Section A of each sheet gives (normal @ 68 F) the tooth-root to tooth-root distance = gap + overlap + gap and the plate from the root back to its edge; the plan
// gives 108 spaces @ 4 1/4 from the curb to the CL bridge (per plate) and the root / tip radii (1 1/8 R slot, 3/4 R tooth corners) -> tooth 2 in., slot 2 1/4 in.
// Teeth are TrussLineItemDefinition.RepeatPitch pieces across the roadway; the plates follow the grade (top flush with it).
string toothFlag = U("p.54, p.46", "finger tooth width and plan shape", "the plan prints 108 spaces @ 4 1/4 in. per half roadway with 1 1/8 in. R slot ends and 3/4 in. R tooth corners, no tooth width",
    "teeth 2 in. wide (slot 2 1/4 in. = the 1 1/8 R) with square tips, the opposite plate shifted half a space; the 12 in. R / 2 in. tip bevel (enlarged detail) and the bead welding pattern not built.");
void Dam(string bridge, string name, double sta, double rootHalfIn, double toothIn, double solidIn, double thkIn, string pages, string why, bool supports)
{
    var t = Doc.Project.Bridges.First(b => b.Name == bridge).Truss!;
    string asm = "DAM-" + name.Replace(" ", "-").ToUpperInvariant();
    string desc = $"finger expansion dam {name} ({pages})";
    TrussLineItemDefinition Item(string part, List<TrussUV> prof, string flag, double pitch = 0, double start = 0) => new TrussLineItemDefinition
    {
        Name = $"{name} {part}", Kind = TrussLineItemKind.Steel, Category = PartCategory.JointArmor, Transverse = true, Station = sta, From = -Ft(CurbOff), To = Ft(CurbOff),
        Profile = prof, Material = "C", Assembly = asm, AssemblyDescription = desc, Source = pages, Unresolved = flag,
        RepeatPitch = pitch > 0 ? In(pitch) : 0, RepeatWidth = pitch > 0 ? In(2) : 0, RepeatStart = In(start),
    };
    string plateFlag = U(pages, $"finger plates at {name}: plate edges at the curbs / parapet ends, bulkhead / diaphragm plates, anchor bars", why,
        $"two plates {thkIn:0.###} in. thick, top flush with the grade, curb to curb: {solidIn:0.###} in. solid behind the tooth roots {2 * rootHalfIn:0.###} in. apart at 68 F, teeth {toothIn:0.###} in. long.");
    foreach (int side in new[] { -1, 1 })
    {
        string sd = side < 0 ? "back" : "ahead";
        double r0 = side * rootHalfIn, r1 = side * (rootHalfIn + solidIn), tip = side * (rootHalfIn - toothIn);
        t.LineItems!.Add(Item($"plate {sd}", new() { UV(Math.Min(r0, r1), -thkIn), UV(Math.Max(r0, r1), -thkIn), UV(Math.Max(r0, r1), 0), UV(Math.Min(r0, r1), 0) }, plateFlag));
        t.LineItems.Add(Item($"teeth {sd}", new() { UV(Math.Min(r0, tip), -thkIn), UV(Math.Max(r0, tip), -thkIn), UV(Math.Max(r0, tip), 0), UV(Math.Min(r0, tip), 0) }, toothFlag,
            4.25, side < 0 ? 0.125 : 0.125 + 2.125));
        if (!supports) continue;
        // dam support members under each plate (Section A): 18 I 70 on the back side, 18 [ 58 on the ahead side, top flange under the plate, web 6 in. behind the root (scaled)
        double w = side * (rootHalfIn + 6), top = -thkIn;
        var prof = side < 0
            ? new List<TrussUV> { UV(w - 3.125, top - 18), UV(w + 3.125, top - 18), UV(w + 3.125, top - 17.309), UV(w + 0.3555, top - 17.309), UV(w + 0.3555, top - 0.691), UV(w + 3.125, top - 0.691),
                UV(w + 3.125, top), UV(w - 3.125, top), UV(w - 3.125, top - 0.691), UV(w - 0.3555, top - 0.691), UV(w - 0.3555, top - 17.309), UV(w - 3.125, top - 17.309) }
            : new List<TrussUV> { UV(w - 0.35, top - 18), UV(w + 3.85, top - 18), UV(w + 3.85, top - 17.375), UV(w + 0.35, top - 17.375), UV(w + 0.35, top - 0.625), UV(w + 3.85, top - 0.625),
                UV(w + 3.85, top), UV(w - 0.35, top) };
        t.LineItems.Add(Item(side < 0 ? "support 18 I 70" : "support 18 [ 58", prof,
            U(pages, $"dam support members at {name}", "Section A draws the 18 I 70 / 18 [ 58 under the plates with their connection angles to the floor beams / stringers but does not dimension their position; the plan shows them in pieces between the stringer lines",
                "continuous curb to curb, top flange under the plate, web 6 in. behind the tooth root (scaled); connection angles, bulkheads and anchor bars not built.")));
    }
}
string slidingWhy = "p.46 Section A gives the roots 8 1/4 in. apart (1 1/2 + 5 1/4 + 1 1/2) and each plate 1'-3 from the CL dam; the plate thickness is read 1 1/2 (low confidence, '1 3/4' possible) and the supports (bracket plates on the floor beams) are drawn, not dimensioned (no dam beams modeled)";
var girderPiers = new[] { ("Pier 1", "647+44.630"), ("Pier 2", "648+69.046"), ("Pier 3", "649+93.463"), ("Pier 4", "651+17.880"), ("Pier 5", "652+42.297"), ("Pier 6", "653+66.713"), ("Pier 7", "654+91.130"), ("Pier 8", "656+15.547"), ("Pier 9", "657+39.963") };
foreach (var (p, s) in girderPiers) Dam("PTC Girder spans 1-10", $"dam {p}", Sta(s), 4.125, 6.75, 10.875, 1.5, "p.46", slidingWhy, false);
foreach (var (p, s) in new[] { ("Pier 28", "708+20.797"), ("Pier 29", "709+45.213"), ("Pier 30", "710+69.630") }) Dam("PTC Girder spans 28-31", $"dam {p}", Sta(s), 4.125, 6.75, 10.875, 1.5, "p.46", slidingWhy, false);
string fingerWhy = "Section A gives the 68 F tooth gaps / overlap and the plate behind the roots (1'-3 + 3 1/2); the curb ends, the parapet openings and the plate splice at the CL bridge are drawn, not all dimensioned";
Dam("PTC Girder spans 1-10", "dam Pier 10", Sta("658+64.568"), 8.25, 12.5, 18.5, 3, "p.54, p.1", fingerWhy + "; the dam station 658+64.568 is p.1 (3-9-54), not re-printed after the 10-26-54 girder shoe shift", true);
Dam("PTC 3-span truss 25-27", "dam Pier 27", Sta("706+96.192"), 8.25, 12.5, 18.5, 3, "p.54, p.1", fingerWhy + "; station 706+96.192 is p.1 only", true);
Dam("PTC 3-span truss 11-13", "dam Pier 13", Sta("665+14.880"), 13, 19.25, 18.5, 3.5, "p.68", fingerWhy, true);
Dam("PTC 4-span truss 21-24", "dam Pier 24", Sta("700+45.880"), 13, 19.25, 18.5, 3.5, "p.68", fingerWhy, true);
Dam("PTC 4-span truss 14-17", "dam Pier 17", Sta("675+96.380"), 13, 19.25, 18.5, 3.5, "p.68", fingerWhy, true);
Dam("PTC Arch truss 18-20", "dam Pier 20", Sta("689+64.380"), 22.25, 31.5, 18.5, 3.5, "p.91", fingerWhy + "; the 18 1/2 in. plate behind the roots is taken from p.54 (p.91 prints 2'-0 + 1'-3 1/2 not decoded); steel parapet corners at the dam (3/8 in plates) not built", true);
Dam("PTC Arch truss 18-20", "dam W22", Sta("682+80.380"), 10, 15, 18.5, 3, "p.92", fingerWhy + "; the middle values of the W22 opening table are blurred (p.92); 18 1/2 in. plate behind the roots taken from p.54", true);

// ---------------------------------------------------------------- service walkways, ladders, platforms: walkways-ptc.csx (run after this script)

// ---------------------------------------------------------------- regenerate + save
var rep = Doc.Regenerate();
foreach (var n in names)
{
    var m = Doc.Model(n);
    var c = m.Concrete;
    var st = m.Steel;
    int dams = st?.Parts.Count(p => p.Category == PartCategory.JointArmor) ?? 0;
    double grating = st?.Parts.Where(p => p.Category == PartCategory.Grating).Sum(p => p.Length) ?? 0;
    Print($"{n}: slab {c?.DeckVolume / 0.764554858:0} CY + thickening {c?.HaunchVolume / 0.764554858:0} CY, curb + parapet {c?.BarrierVolume / 0.764554858:0} CY, railing runs {st?.Railings?.Runs.Count ?? 0} ({st?.Railings?.Runs.Sum(r => r.Length) / 0.3048:0} ft), dams {dams}, grating {grating / 0.3048:0} ft x 2 ft");
    foreach (var e in rep.Errors.Where(e => e.StartsWith(n))) Print("  ERROR " + e);
}
Doc.Save(outPath);
Print($"Saved {outPath}");
