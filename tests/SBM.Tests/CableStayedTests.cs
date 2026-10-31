using SBM.Api;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.CableStayed;
using SBM.Core.Geometry;
using SBM.Core.Steel;
using Xunit;

namespace SBM.Tests;

/// <summary>Cable-stayed footbridge generator (2026-10-26): geometry, exact NURBS sweeps, analysis wireframe and first-cut forces, file round trip.</summary>
public class CableStayedTests
{
    private static double Ft(double x) => x * 0.3048;

    /// <summary>Straight east-heading alignment with a crest profile and the osprey concept on it (pylon at 10+00, main span ahead).</summary>
    private static SbmDocument Doc(Action<CableStayedDefinition>? tweak = null, bool concrete = false)
    {
        var doc = SbmDocument.New("cs test");
        var al = doc.AddAlignment("CL", Ft(400), new[] { new PiRow(1000, 1000), new PiRow(1000, 1000 + Ft(1400)) });
        al.Profile = new VerticalProfile { Vpis = { new Vpi(Ft(400), Ft(40)), new Vpi(Ft(1000), Ft(70), Ft(200)), new Vpi(Ft(1800), Ft(30)) } };
        var br = new BridgeDefinition { Name = "CS", AlignmentName = "CL" };
        br.FieldSplices.Clear(); br.Girders.Count = 0; br.LateralBracing.Enabled = false;
        foreach (var (n, t, sta) in new[] { ("A1", SupportType.Abutment, 600.0), ("P1", SupportType.Pier, 800.0), ("PY", SupportType.Pier, 1000.0),
                                              ("P2", SupportType.Pier, 1420.0), ("A2", SupportType.Abutment, 1600.0) })
            br.Supports.Add(new SupportDefinition { Name = n, Type = t, Station = Ft(sta), Substructure = new SubstructureDefinition { Enabled = false } });
        double top = al.Profile.ElevationAt(Ft(1000));
        var cs = CableStayedConcepts.Osprey("PY", Ft(1000), Ft(26), top - Ft(26), Ft(180));
        cs.Piers.BaseElevation = Ft(30);
        if (concrete) cs.Deck.Material = CsMaterial.Concrete;
        tweak?.Invoke(cs);
        br.CableStayed = cs;
        doc.Project.Bridges.Add(br);
        doc.Regenerate();
        return doc;
    }

    [Fact]
    public void Osprey_BuildsPylonStaysDeckPiers_AllClosed()
    {
        var doc = Doc();
        var m = doc.Model("CS");
        var cs = m.Steel!.CableStayed!;
        Assert.DoesNotContain(cs.Diagnostics, d => d.Severity == SBM.Core.Framing.DiagnosticSeverity.Error);
        Assert.Equal(4, cs.Legs.Count);                                         // two wings + two lower legs
        Assert.Equal(28, cs.Stays.Count);
        Assert.Equal(28, m.Steel.Parts.Count(p => p.Category == PartCategory.StayCable));
        Assert.Equal(4, m.Steel.Parts.Count(p => p.Category == PartCategory.Pylon && p.Mark.StartsWith("PY-L")));
        Assert.All(m.Steel.Parts, p => Assert.True(p.Mesh.Volume() > 0, p.Mark));
        // leg ends where the definition puts them (pylon frame -> world), wings mirrored
        var f = cs.PylonFrame;
        var def = doc.Project.Bridges.Single(b => b.Name == "CS").CableStayed!.Pylon.Legs;
        for (int i = 0; i < cs.Legs.Count; i++)
        {
            var tip = cs.Legs[i].Points[^1] - f.Origin;
            Assert.Equal(def[i].Tip.Z, tip.Z, 6);
            Assert.Equal(def[i].Tip.X, Vec3.Dot(tip, f.X), 6);
            Assert.Equal(def[i].Tip.Y, Vec3.Dot(tip, f.Y), 6);
        }
        Assert.Equal(-def[0].Tip.Y, def[1].Tip.Y, 9);
        Assert.Equal(Ft(180), def[0].Tip.Z - (doc.Alignment("CL").Profile.ElevationAt(Ft(1000)) - Ft(26)), 6);   // 180 ft above the walkway
        // every stay's upper end lies on its leg's centerline, the lower end at the deck anchor
        foreach (var s in cs.Stays)
            Assert.True(cs.Legs[s.Leg].Points.Min(p => (p - s.Upper).Length) < 0.2, s.Name);
        // exact NURBS for the curved legs and deck spans
        Assert.All(m.Steel.Parts.Where(p => p.Mark.StartsWith("PY-L") || p.Category == PartCategory.CsDeck), p => Assert.True(p.Mesh.HasExactNurbs, p.Mark));
        // piers: columns at P1 / P2, seats at the abutments, pylon footing + strut footing
        var units = m.Substructure!.Units;
        Assert.Contains(units, u => u.Name == "P1" && u.Solids.Any(s => s.Kind == SBM.Core.Substructure.SubElementKind.Column));
        Assert.Contains(units, u => u.Name == "A1" && u.Solids.Any(s => s.Kind == SBM.Core.Substructure.SubElementKind.Cap));
        Assert.Equal(3, units.Single(u => u.Name == "PY").Solids.Count(s => s.Kind == SBM.Core.Substructure.SubElementKind.Footing));   // legs + two props
    }

    [Fact]
    public void Forces_StayEqualsTributaryOverSine_SymmetricPylonHasNoTransverseMoment()
    {
        var doc = Doc();
        var cs = doc.Model("CS").Steel!.CableStayed!;
        var r = cs.Forces;
        double w = r.DeckSelfWeight + r.BarrierWeight + r.SuperimposedDead;
        foreach (var sf in r.Stays)
        {
            var s = cs.Stays.Single(x => x.Name == sf.Stay);
            int atStation = cs.Stays.Count(x => Math.Abs(x.Station - s.Station) < 1e-6);
            Assert.Equal(w * sf.Tributary / atStation / Math.Sin(s.AngleDeg * Math.PI / 180), sf.Dead, 3);
            Assert.True(sf.Total > sf.Dead);
        }
        Assert.Equal(0, r.BaseMomentTransDead, 0);                       // two mirrored legs
        // the deck weight is the hollow section: thin wall x perimeter
        var o = cs.DeckSection;
        double per = Enumerable.Range(0, o.Count).Sum(i => (o[(i + 1) % o.Count] - o[i]).Length);
        Assert.Equal(per * 0.75 * 0.0254, cs.DeckProps.A, 9);
    }

    [Fact]
    public void AnalysisWireframe_ConnectsEveryMember_AndHasSupports()
    {
        var cs = Doc().Model("CS").Steel!.CableStayed!;
        var an = cs.Analysis;
        var ids = an.Nodes.Select(n => n.Id).ToHashSet();
        Assert.All(an.Members, m => { Assert.Contains(m.N1, ids); Assert.Contains(m.N2, ids); Assert.True(m.Section.A > 0, m.Name); });
        Assert.Equal(28, an.Members.Count(m => m.Kind == "stay" && m.Cable));
        Assert.Contains(an.Supports, s => s.Name == "pylon base" && s.Fixed.All(x => x));
        // every node is reached from the pylon base through members (one connected structure)
        var adj = an.Members.SelectMany(m => new[] { (m.N1, m.N2), (m.N2, m.N1) }).ToLookup(x => x.Item1, x => x.Item2);
        var seen = new HashSet<int> { an.Supports.First(s => s.Name == "pylon base").Node };
        var q = new Queue<int>(seen);
        while (q.Count > 0) foreach (var n in adj[q.Dequeue()]) if (seen.Add(n)) q.Enqueue(n);
        Assert.Equal(an.Nodes.Count, seen.Count);
    }

    [Fact]
    public void SectionProperties_RectangleAndBox()
    {
        var rect = CableStayedAnalysis.PolygonProps(new[] { new Vec2(-1, -0.25), new Vec2(1, -0.25), new Vec2(1, 0.25), new Vec2(-1, 0.25) });
        Assert.Equal(1.0, rect.A, 12);
        Assert.Equal(2 * 0.125 / 12, rect.Iy, 12);
        Assert.Equal(0.5 * 8 / 12, rect.Iz, 12);
        var box = CableStayedAnalysis.RectProps(new CsSection(1.0, 2.0) { Wall = 0.05 }, 0);
        Assert.Equal(2 - 0.9 * 1.9, box.A, 12);
        Assert.Equal((1 * 8 - 0.9 * Math.Pow(1.9, 3)) / 12, box.Iy, 12);
    }

    [Fact]
    public void Definition_SavesAndLoads()
    {
        var doc = Doc();
        string path = Path.Combine(Path.GetTempPath(), $"sbm-cs-{Guid.NewGuid():N}.sbm");
        try
        {
            doc.Save(path);
            var back = SbmDocument.Open(path);
            var a = doc.Project.Bridges.Single(b => b.Name == "CS").CableStayed!;
            var b = back.Project.Bridges.Single(b => b.Name == "CS").CableStayed!;
            Assert.Equal(a.Pylon.Legs[1].Tip, b.Pylon.Legs[1].Tip);
            Assert.Equal(a.Stays.Count, b.Stays.Count);
            Assert.Equal(a.Deck.WallThickness, b.Deck.WallThickness);
            back.Regenerate();
            Assert.Equal(doc.Model("CS").Steel!.Parts.Count, back.Model("CS").Steel!.Parts.Count);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ConcreteDeck_And_ClearanceCheck()
    {
        var doc = Doc(c => c.Analysis.Clearances.Add(new CsClearance("road", Ft(1100), Ft(1200), Ft(40), Ft(17))), concrete: true);
        var m = doc.Model("CS");
        var cs = m.Steel!.CableStayed!;
        Assert.NotNull(m.Concrete!.Deck);
        Assert.DoesNotContain(m.Steel.Parts, p => p.Category == PartCategory.CsDeck);
        var c = Assert.Single(cs.Clearances);
        Assert.Equal(c.Provided >= c.Required, c.Ok);
        // the soffit over the range: grade + lowest section point - surface
        double soffit = cs.DeckSection.Min(p => p.Y);
        Assert.True(c.Provided <= doc.Alignment("CL").Profile.ElevationAt(Ft(1100)) + soffit - Ft(40) + 1e-6);
    }

    [Fact]
    public void TwoTower_TuningForkEdgeGirdersRocketLaunchers_SymmetricBasesAndRoundTrip()
    {
        var doc = SbmDocument.New("cs2 test");
        var al = doc.AddAlignment("CL", 0, new[] { new PiRow(1000, 1000), new PiRow(1000, 1000 + Ft(3000)) });
        al.Profile = new VerticalProfile { Vpis = { new Vpi(0, Ft(100)), new Vpi(Ft(3000), Ft(100)) } };
        var br = new BridgeDefinition { Name = "CS2", AlignmentName = "CL" };
        br.FieldSplices.Clear(); br.Girders.Count = 0; br.LateralBracing.Enabled = false;
        foreach (var (n, t, sta) in new[] { ("A1", SupportType.Abutment, 500.0), ("PA", SupportType.Pier, 800.0), ("TA", SupportType.Pier, 1100.0),
                                              ("TB", SupportType.Pier, 1820.0), ("PB", SupportType.Pier, 2120.0), ("A2", SupportType.Abutment, 2400.0) })
            br.Supports.Add(new SupportDefinition { Name = n, Type = t, Station = Ft(sta), Substructure = new SubstructureDefinition { Enabled = false } });
        var cs = CableStayedConcepts.TwoTower("TA", Ft(1100), 0, Ft(100), "TB", Ft(1820), 0, Ft(100), Ft(290), Ft(290), unitFrom: Ft(800), unitTo: Ft(2120));
        cs.Piers.BaseElevation = 0;
        br.CableStayed = cs;
        doc.Project.Bridges.Add(br);
        doc.Regenerate();
        var m = doc.Model("CS2").Steel!;
        var csm = m.CableStayed!;
        Assert.Equal(2, csm.PylonFrames.Count);
        Assert.Equal(4, csm.Legs.Count);
        Assert.Equal(new[] { 0, 0, 1, 1 }, csm.Legs.Select(l => l.Pylon));
        Assert.Equal(52, csm.Stays.Count);                                       // 2 towers x 2 planes x (7 main + 6 side) at 45 ft
        // tuning fork: hollow concrete legs (4 walls + top each) and one concrete strut below the deck per tower, no steel tower parts
        var subUnits = doc.Model("CS2").Substructure!.Units;
        foreach (var tn in new[] { "TA", "TB" })
        {
            var u = subUnits.Single(x => x.Name == tn);
            Assert.Equal(2 * 5, u.Solids.Count(x => x.Name.Contains(" leg ")));
            Assert.Single(u.Solids, x => x.Name.Contains("strut below the deck"));
            Assert.All(u.Solids, x => Assert.True(x.Mesh.Volume() > 0, x.Name));
        }
        Assert.DoesNotContain(m.Parts, p => p.Category is PartCategory.Pylon or PartCategory.PylonCrossBeam);
        // anchorage trays: one per stay level per leg (7 levels: 6 opposing pairs + the lowest main-span stay), each stay ends on a bearing plate inside its tray
        var trays = m.Assemblies.Where(a => a.Kind == AssemblyKind.PylonAnchorTray).ToList();
        Assert.Equal(4 * 7, trays.Count);
        Assert.Equal(4 * 6, trays.Count(a => a.Description.Contains(" / ")));
        Assert.Equal(52, m.Parts.Count(p => p.Category == PartCategory.PylonGuidePipe));
        foreach (var st in csm.Stays)
        {
            var leg = csm.Legs[st.Leg];
            var c = leg.Points.OrderBy(p => Math.Abs(p.Z - st.Upper.Z)).First();
            var pfr = csm.PylonFrames[leg.Pylon];
            var sec = leg.Definition.Section;
            double frac = (st.Upper.Z - leg.Points[0].Z) / (leg.Points[^1].Z - leg.Points[0].Z);
            Assert.True(Math.Abs(Vec3.Dot(st.Upper - c, pfr.X)) < sec.DepthAt(frac) / 2 - sec.Wall + 1e-6, st.Name);   // inside the leg's void along the bridge
            Assert.True(Math.Abs(Vec3.Dot(st.Upper - c, pfr.Y)) < sec.WidthAt(frac) / 2 - sec.Wall + 1e-6, st.Name);   // and across
        }
        var eg = cs.Deck.EdgeGirders!;
        double topOfDeck = Ft(100), strutTop = csm.Legs[0].Points[0].Z + cs.Pylon.CrossBeams[0].Height + cs.Pylon.CrossBeams[0].Section.Depth / 2;
        Assert.True(strutTop < topOfDeck - eg.Depth, "the only cross member is below the deck");
        Assert.Equal(CsMaterial.Concrete, cs.Pylon.CrossBeams[0].Section.Material);
        // floorbeams every 15 ft from each tower (main span 720 = 48 x 15) + the unit ends; anchorages at floorbeams every 45 ft
        var fbSta = m.Assemblies.Where(a => a.Kind == AssemblyKind.CsFloorbeam).Select(a => a.Description).ToList();
        Assert.Equal(89, fbSta.Count);                                           // 800 .. 2120 ft at 15 ft, both ends on the grid
        foreach (var st in csm.Stays)
        {
            double fromTower = Math.Min(Math.Abs(st.Station - Ft(1100)), Math.Abs(st.Station - Ft(1820)));
            Assert.Equal(0, fromTower / Ft(45) - Math.Round(fromTower / Ft(45)), 6);
        }
        // one rocket launcher per stay; the stay ends at the bearing plate below the deck point
        Assert.Equal(52, m.Assemblies.Count(a => a.Kind == AssemblyKind.CsAnchorage));
        Assert.Equal(52, m.Parts.Count(p => p.Category == PartCategory.StayAnchorPipe));
        Assert.All(csm.Stays, st => Assert.True(st.Lower.Z < topOfDeck - eg.SlabThickness - 1, $"{st.Name} {st.Lower} {st.Upper} {st.Station}"));
        // field splices: one line per stay anchor station and side = 26 lines x 2 sides; 27 girder field sections per side
        int lines = m.Assemblies.Count(a => a.Kind == AssemblyKind.CsFieldSplice) / 2;
        Assert.InRange(lines, 20, 26);                                            // one per anchorage, the outermost ones may fall outside the unit
        Assert.Equal(2 * (lines + 1), m.Assemblies.Count(a => a.Kind == AssemblyKind.CsEdgeGirder));
        Assert.DoesNotContain(csm.Diagnostics, d => d.Message.Contains("overlaps the floorbeam"));
        Assert.All(m.Parts.Where(p => p.Category is PartCategory.FlangePlate or PartCategory.WebPlate or PartCategory.SplicePlate or PartCategory.CsFloorbeamPlate or PartCategory.CsStringerPlate
            or PartCategory.StayAnchorPipe or PartCategory.StayAnchorPlate), p => Assert.True(p.Mesh.Volume() > 0, p.Mark));
        // precast deck: panels between the members, closure pours over both edge girders + the CL longitudinal support (through) and every floorbeam
        var conc = doc.Model("CS2").Concrete!;
        Assert.Null(conc.Deck);
        Assert.Equal(88 * 2, conc.DeckParts.Count(p => p.IsPanel));            // 88 bays x 2 panels across (one longitudinal closure on the CL)
        Assert.Equal(3 + 89 * 2, conc.DeckParts.Count(p => !p.IsPanel));       // 3 longitudinal closures + 89 floorbeams x 2
        Assert.All(conc.DeckParts, p => Assert.True(p.Mesh.Volume() > 0, p.Name));
        double slabVol = (eg.SlabLeft + eg.SlabRight) * eg.SlabThickness * Ft(2120 - 800);
        Assert.Equal(slabVol, conc.DeckPartVolume, slabVol * 0.002);             // panels + closures fill the slab exactly
        Assert.Equal(88, m.Assemblies.Count(a => a.Kind == AssemblyKind.CsStringer));
        // rocket launchers span the girder: pipe + bearing plate + stem + mounting plate + anchor top / bottom flanges; the pipe runs past both flanges
        foreach (var asm in m.Assemblies.Where(a => a.Kind == AssemblyKind.CsAnchorage))
        {
            var ps = m.Parts.Where(p => p.Assembly == asm.Mark).ToList();
            Assert.Equal(6, ps.Count);
            var pipe = ps.Single(p => p.Category == PartCategory.StayAnchorPipe).Mesh.Bounds();
            double tosZ = topOfDeck - eg.TopOfSteelBelowGrade, botZ = tosZ - eg.TopFlangeThickness - eg.WebDepth - eg.BottomFlangeThickness;
            Assert.True(pipe.Max.Z > tosZ + 1 && pipe.Min.Z < botZ - 0.3, asm.Mark);
            var stem = ps.Single(p => p.Mark.EndsWith("-ST")).Mesh.Bounds();
            Assert.True(stem.Max.Z - stem.Min.Z > 0.8 * eg.WebDepth, asm.Mark);
        }
        Assert.True(csm.DeckWeightPerLength > 0);
        // every stay hangs from its own tower: nearest to its pylon base in plan
        foreach (var s in csm.Stays)
        {
            int own = csm.Legs[s.Leg].Pylon;
            Assert.True((s.Upper.XY - csm.PylonFrames[own].Origin.XY).Length < (s.Upper.XY - csm.PylonFrames[1 - own].Origin.XY).Length, s.Name);
        }
        // symmetric bridge: equal and opposite longitudinal base moments, both pylons have footings
        var f = csm.Forces;
        Assert.Equal(2, f.Pylons.Count);
        Assert.Equal(f.Pylons[0].MomentLongDead, -f.Pylons[1].MomentLongDead, 3);
        Assert.Equal(f.BaseMomentLongDead, f.Pylons[0].MomentLongDead, 9);
        var units = doc.Model("CS2").Substructure!.Units;
        Assert.Contains(units, u => u.Name == "TA" && u.Solids.Any(s => s.Kind == SBM.Core.Substructure.SubElementKind.Footing));
        Assert.Contains(units, u => u.Name == "TB" && u.Solids.Any(s => s.Kind == SBM.Core.Substructure.SubElementKind.Footing));
        // the second pylon survives the file round trip; a single-pylon definition writes no MorePylons
        var path = Path.Combine(Path.GetTempPath(), $"cs2-{Guid.NewGuid():N}.sbm");
        try
        {
            doc.Save(path);
            var back = SbmDocument.Open(path).Project.Bridges.Single(b => b.Name == "CS2").CableStayed!;
            Assert.Single(back.MorePylons!);
            Assert.Equal("TB", back.MorePylons![0].Support);
            Assert.DoesNotContain("MorePylons", System.Text.Json.JsonSerializer.Serialize(CableStayedConcepts.Conventional("P", 0, 0, 30, 100, 50), new System.Text.Json.JsonSerializerOptions { NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals }));
        }
        finally { File.Delete(path); }
    }
}
