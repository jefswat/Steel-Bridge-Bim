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
        Assert.Equal(2, cs.Legs.Count);
        Assert.Equal(28, cs.Stays.Count);
        Assert.Equal(28, m.Steel.Parts.Count(p => p.Category == PartCategory.StayCable));
        Assert.Equal(2, m.Steel.Parts.Count(p => p.Category == PartCategory.Pylon && p.Mark.StartsWith("PY-L")));
        Assert.All(m.Steel.Parts, p => Assert.True(p.Mesh.Volume() > 0, p.Mark));
        // leg tips where the definition puts them: 180 ft above the base, swept back 50 ft (main span ahead = +X), +-40 ft across
        var f = cs.PylonFrame;
        foreach (var (leg, sy) in cs.Legs.Zip(new[] { -1.0, 1.0 }))
        {
            var tip = leg.Points[^1] - f.Origin;
            Assert.Equal(Ft(180), tip.Z, 6);
            Assert.Equal(-Ft(50), Vec3.Dot(tip, f.X), 6);
            Assert.Equal(sy * Ft(40), Vec3.Dot(tip, f.Y), 6);
        }
        // every stay's upper end lies on its leg's centerline, the lower end at the deck anchor
        foreach (var s in cs.Stays)
            Assert.True(cs.Legs[s.Leg].Points.Min(p => (p - s.Upper).Length) < 0.2, s.Name);
        // exact NURBS for the curved legs and deck spans
        Assert.All(m.Steel.Parts.Where(p => p.Mark.StartsWith("PY-L") || p.Category == PartCategory.CsDeck), p => Assert.True(p.Mesh.HasExactNurbs, p.Mark));
        // piers: columns at P1 / P2, seats at the abutments, pylon footing + strut footing
        var units = m.Substructure!.Units;
        Assert.Contains(units, u => u.Name == "P1" && u.Solids.Any(s => s.Kind == SBM.Core.Substructure.SubElementKind.Column));
        Assert.Contains(units, u => u.Name == "A1" && u.Solids.Any(s => s.Kind == SBM.Core.Substructure.SubElementKind.Cap));
        Assert.Equal(2, units.Single(u => u.Name == "PY").Solids.Count(s => s.Kind == SBM.Core.Substructure.SubElementKind.Footing));
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
        Assert.True(r.BaseMomentLongDead > 0);                            // the 10 front stays out-pull 4 back stays + the lean
        // more back stays (same front stays) reduce the tipping toward the main span
        var more = Doc(c => { foreach (var g in c.Stays.Where(g => g.Name.StartsWith("back"))) { g.Count = 8; g.LastStation = Ft(1000 - 190); } });
        Assert.True(more.Model("CS").Steel!.CableStayed!.Forces.BaseMomentLongDead < r.BaseMomentLongDead);
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
}
