// PTC / NJTA DELAWARE RIVER BRIDGE (1954-57, AS-BUILT) - DECK AND EVERYTHING ON IT, on all seven truss-generator bridges of the PTC model:
// slab 6 3/4 + 1" thickening over every stringer (p.107), curb + parapet wall (p.107 Section A), aluminum parapet railing (p.107 / p.108),
// expansion dams (p.46 sliding plate, p.54 / p.68 / p.91 / p.92 finger dams), service walkways (approach truss spans p.101, main span piers 18 / 19 p.103).
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

// ---------------------------------------------------------------- expansion dams (p.46 / p.54 / p.68 / p.91 / p.92): one plate band per dam, top flush with the finished grade
void Dam(string bridge, string name, double sta, double widthIn, double thkIn, string kind, string pages, string why)
{
    var t = Doc.Project.Bridges.First(b => b.Name == bridge).Truss!;
    t.LineItems!.Add(new TrussLineItemDefinition
    {
        Name = name, Kind = TrussLineItemKind.Steel, Category = PartCategory.JointArmor, Transverse = true, Station = sta, From = -Ft(CurbOff), To = Ft(CurbOff),
        Profile = new() { UV(-widthIn / 2, -thkIn), UV(widthIn / 2, -thkIn), UV(widthIn / 2, 0), UV(-widthIn / 2, 0) }, Material = "C", Assembly = "DAM-" + name.Replace(" ", "-").ToUpperInvariant(),
        AssemblyDescription = $"{kind} expansion dam {name} ({pages})", Source = pages,
        Unresolved = U(pages, $"{kind} dam at {name}: teeth, edge channels, stringer supports, anchor bars and the curb / parapet ends", why,
            $"one steel band {widthIn:0.##} in wide (total dam width at 68 F) x {thkIn:0.###} in, top flush with the grade, 38'-3 each side of the CL (curb to curb)."),
    });
}
string slidingWhy = "p.46 Section A gives the total dam width 8 1/4 in at 68 F (low confidence) and 3/8 in sliding plates; the serpentine seal, bulkhead plates and supports are drawn, not all dimensioned";
var girderPiers = new[] { ("Pier 1", "647+44.630"), ("Pier 2", "648+69.046"), ("Pier 3", "649+93.463"), ("Pier 4", "651+17.880"), ("Pier 5", "652+42.297"), ("Pier 6", "653+66.713"), ("Pier 7", "654+91.130"), ("Pier 8", "656+15.547"), ("Pier 9", "657+39.963") };
foreach (var (p, s) in girderPiers) Dam("PTC Girder spans 1-10", $"dam {p}", Sta(s), 8.25, 0.375, "sliding plate", "p.46", slidingWhy);
foreach (var (p, s) in new[] { ("Pier 28", "708+20.797"), ("Pier 29", "709+45.213"), ("Pier 30", "710+69.630") }) Dam("PTC Girder spans 28-31", $"dam {p}", Sta(s), 8.25, 0.375, "sliding plate", "p.46", slidingWhy);
string fingerWhy = "the finger plates are drawn with their teeth (61 + 47 teeth @ 4 1/4 in across the half roadway) and the movement tables; the tooth outline, the plate anchorage and the dam stringers are not all dimensioned";
Dam("PTC Girder spans 1-10", "dam Pier 10", Sta("658+64.568"), 16.5, 3, "finger", "p.54, p.1", fingerWhy + "; the dam station 658+64.568 is p.1 (3-9-54), not re-printed after the 10-26-54 girder shoe shift");
Dam("PTC 3-span truss 25-27", "dam Pier 27", Sta("706+96.192"), 16.5, 3, "finger", "p.54, p.1", fingerWhy + "; station 706+96.192 is p.1 only");
Dam("PTC 3-span truss 11-13", "dam Pier 13", Sta("665+14.880"), 26, 3, "finger", "p.68", fingerWhy);
Dam("PTC 4-span truss 21-24", "dam Pier 24", Sta("700+45.880"), 26, 3, "finger", "p.68", fingerWhy);
Dam("PTC 4-span truss 14-17", "dam Pier 17", Sta("675+96.380"), 26, 3, "finger", "p.68", fingerWhy);
Dam("PTC Arch truss 18-20", "dam Pier 20", Sta("689+64.380"), 44.5, 3.5, "finger", "p.91", fingerWhy + "; steel parapet corners at the dam (3/8 in plates) not built");
Dam("PTC Arch truss 18-20", "dam W22", Sta("682+80.380"), 20, 3, "finger", "p.92", fingerWhy + "; the middle values of the W22 opening table are blurred (p.92)");

// ---------------------------------------------------------------- service walkways (p.101 approach spans, p.103 main span)
// grating 3/4 x 3/16 bearing bars (Irving X-Bar type AA), 2'-0 wide; nominal weight not printed -> 7 psf flagged
double gratingDensity = 7.0 * 4.88242764 / In(0.75);   // 7 lb/sq ft over a 3/4 in plate (kg/m3)
string gratingFlag = U("p.101", "grating weight", "the sheet names the grating (3/4 x 3/16 bearing bars, Irving X-Bar type AA) but prints no weight", "7 lb/sq ft (catalogue value for the type, not from the plans).");
void Walk(string bridge, string name, bool transverse, double reference, double from, double to, double vBottom, bool fromGrade, string pages, string unresolved)
{
    var t = Doc.Project.Bridges.First(b => b.Name == bridge).Truss!;
    t.LineItems!.Add(new TrussLineItemDefinition
    {
        Name = name, Kind = TrussLineItemKind.Steel, Category = PartCategory.Grating, Transverse = transverse, Station = transverse ? reference : 0, ReferenceOffset = transverse ? 0 : reference,
        From = from, To = to, Side = 1, FromGrade = fromGrade, Density = gratingDensity, Material = "Galvanized steel grating", Assembly = "WALKWAYS", AssemblyDescription = $"{bridge} - service walkways ({pages})",
        Profile = new() { new TrussUV { U = -Ft(1), V = vBottom }, new TrussUV { U = Ft(1), V = vBottom }, new TrussUV { U = Ft(1), V = vBottom + In(0.75) }, new TrussUV { U = -Ft(1), V = vBottom + In(0.75) } },
        Source = pages, Unresolved = unresolved + " " + gratingFlag,
    });
}
var walkSummary = new List<string>();
foreach (var (bridge, pierJoints, endJoint) in new[] { ("PTC 3-span truss 11-13", new[] { "8", "8'" }, "0"), ("PTC 3-span truss 25-27", new[] { "8", "8'" }, "0"), ("PTC 4-span truss 14-17", new[] { "10", "10'" }, ""), ("PTC 4-span truss 21-24", new[] { "10", "10'" }, "") })
{
    var br = Doc.Project.Bridges.First(b => b.Name == bridge);
    var al = Doc.Project.Alignments.First(a => a.Name == br.AlignmentName);
    var dk = br.Truss!.Floor!.Deck!;
    // floor beam bottom (cover plate underside) relative to the grade on the CL, read at the first pier floor beam (the floor beams are parallel to the grade)
    var fb = PartBox(bridge, p => p.Mark == "FB" + pierJoints[0]);
    if (fb == null) { Print($"{bridge}: floor beam FB{pierJoints[0]} not found - walkways not built"); continue; }
    double vFb = fb.Value.zMin - al.ElevationAt(fb.Value.sta, 0);
    double vBot = vFb + In(1);    // grating on the bottom flange angles (1/2 cover + 1/2 angle leg)
    string wflag = U("p.101", "walkway level, lateral position and the openings in the floor beams", "the walkway runs at the bottom flange of the floor beams through the manholes of the floor beam webs (drawn, not dimensioned); its line is drawn near the CL bridge",
        "grating bottom 1 in above the floor beam bottom (on the bottom flange angles), longitudinal walkway centred on the CL bridge; the floor beam webs are not cut for the manholes, walk angles / hangers (L 4x4x3/8) / hand railing / ladders not built.");
    Walk(bridge, "walkway CL", false, 0, dk.BeginStation, dk.EndStation, vBot, true, "p.101", wflag);
    // cross walkways along the pier floor beams, truss to truss (53'-2 to the ladder at the south truss, p.101 plan of walkway at pier 10); omitted at piers 13 / 15 / 17 (p.101 note)
    var cross = pierJoints.Select(j => (j, PartBox(bridge, p => p.Mark == "FB" + j))).ToList();
    if (endJoint.Length > 0) cross.Add((endJoint, PartBox(bridge, p => p.Mark == "FB" + endJoint)));
    foreach (var (j, box) in cross)
    {
        if (box == null) continue;
        double s = box.Value.sta + In(9.5) + Ft(1) + In(2);    // beside the floor beam: web side + 1 ft half walkway + clearance (position UNRESOLVED)
        if (s > dk.EndStation) s = box.Value.sta - In(9.5) - Ft(1) - In(2);
        double vb = box.Value.zMin + In(1);
        Walk(bridge, $"walkway at FB{j}", true, s, -FI(26, 7), FI(26, 7), vb, false, "p.101",
            U("p.101", "cross walkway beside the pier floor beam", "the key plan shows a cross walkway at the pier floor beams 53'-2 long between the walkway ends at the trusses; its side of the floor beam and its level are drawn only", "2'-0 grating beside the floor beam (centre 1'-2 from the web face), level with the floor beam bottom flange; supports from the top chord not built."));
    }
    walkSummary.Add($"{bridge}: CL walkway {(dk.EndStation - dk.BeginStation) / 0.3048:0} ft + {cross.Count(c => c.Item2 != null)} cross walkways");
}
// main span (p.103): walkway at the floor beam at L11 (pier 18) / L11' (pier 19) and on top of the L11 bottom strut (22'-0 long, 2'-0 wide)
{
    string bridge = "PTC Arch truss 18-20";
    foreach (var j in new[] { "L11", "L11'" })
    {
        var strut = PartBox(bridge, p => p.Mark == "LS-" + j);
        if (strut != null)
            Walk(bridge, $"walkway on strut {j}", true, strut.Value.sta, -Ft(11), Ft(11), strut.Value.zMax, false, "p.103",
                U("p.103", $"walkway on the bottom strut {j}", "the key plan gives 22'-0 x 2'-0 on the strut; its fastening and the platform / ladder to the pier top are drawn without all dimensions", "2'-0 grating centred on the strut top, 22'-0 long; ladders, cages and the top-of-pier platform (El. 87.701) not built."));
        string fbj = "FB" + j.Substring(1);
        var fb = PartBox(bridge, p => p.Mark == fbj);
        if (fb != null)
            Walk(bridge, $"walkway at {fbj}", true, fb.Value.sta + In(12) + Ft(1), -FI(26, 7), FI(26, 7), fb.Value.zMin + In(1), false, "p.103",
                U("p.103", $"walkway at the floor beam {fbj} (U11-L11)", "the key plan shows the walkway along the floor beam with a 1'-0 stub; the walkway elevations printed (141.67 / 141.60 / 151.06) cannot be assigned", "2'-0 grating beside the floor beam at its bottom flange level, 53'-2 long."));
        walkSummary.Add($"{bridge}: {j}: strut walkway {(strut != null ? "built" : "strut LS-" + j + " not found")}, floor beam walkway {(fb != null ? "built" : fbj + " not found")}");
    }
}
// girder spans: no walkway sheet (p.44 only mentions manholes omitted at the end floor beams) - flag on the girder bridges
foreach (var n in new[] { "PTC Girder spans 1-10", "PTC Girder spans 28-31" })
    Doc.Project.Bridges.First(b => b.Name == n).Truss!.Unresolved.Add(U("p.44, p.42", "service walkway in the girder spans", "no walkway sheet for the girder spans was found, but p.42 item 14 lists 2,510 / 1,010 sq ft of grating for them and p.44 mentions floor beam manholes", "not built."));

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
foreach (var w in walkSummary) Print("  " + w);
Doc.Save(outPath);
Print($"Saved {outPath}");
