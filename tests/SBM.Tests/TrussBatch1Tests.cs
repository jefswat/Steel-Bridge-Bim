using Microsoft.Data.Sqlite;
using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Steel;
using SBM.Core.Truss;
using SBM.IO.Database;
using SBM.IO.Ifc;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

/// <summary>PTC main span fix batch 1 (2026-10-27): joint rivet grips from the steel (1a), built-up sections turned 90 degrees (1b), continuous stringers (1c).</summary>
public class TrussBatch1Tests
{
    private static BridgeModel Regen(BridgeProject p) => Assert.Single(new RegenerationCache().Regenerate(p))!;

    private static (BridgeProject p, BridgeDefinition b) RivetModel(bool fromSteel)
    {
        var (p, b) = TrussTests.Model();
        var t = b.Truss!;
        // the L2-L3 box chord has its web ply only in the middle 2 m: at the L2 joint the section-rule grip (gusset + ply) runs past the steel
        var box = t.Sections.First(s => s.Name == "BOX");
        var part = new TrussSectionDefinition
        {
            Name = "BOXP", Kind = TrussSectionKind.Box, Label = "box chord, partial ply", WebDepth = box.WebDepth, WebPlies = { new TrussPly(0, In(0.5)) { Length = 2.0 } },
            WebsOutToOut = box.WebsOutToOut, Angle = box.Angle, TopPlates = { new TrussPlate(In(18), In(0.5)) }, BottomPlates = { new TrussPlate(In(18), In(0.5)) },
        };
        t.Sections.Add(part);
        t.Members.First(m => m.Name == "L2-L3").Section = "BOXP";
        var jd = t.Joints.First(j => j.Point == "L2" && j.Plane == "N");
        jd.Rivets.Clear();
        jd.Rivets.Add(new TrussRivetGroup { Member = "L2-L3", Count = 20, Diameter = In(0.875), Distributed = true });
        jd.Rivets.Add(new TrussRivetGroup { Member = "U1-L2", Count = 12, Diameter = In(0.875), Distributed = true });
        // two rows 12 in. apart on the H diagonal (flange ~6 1/2 in. wide): the section rule gives gusset + 3 in. stand-off + angle leg, the steel only the gusset
        jd.Rivets.Add(new TrussRivetGroup { Member = "U1-L2", Count = 4, Rows = 2, Pitch = In(3), Gage = In(12), FirstFromEnd = In(1.5), Diameter = In(0.875) });
        t.JointRivetGripsFromSteel = fromSteel;
        return (p, b);
    }

    [Fact]
    public void JointRivetGrips_FollowTheSteelAlongTheShank()
    {
        var (p0, _) = RivetModel(false);
        Assert.DoesNotContain("JointRivetGripsFromSteel", p0.ToJson());
        var (p1, _) = RivetModel(true);
        Assert.Contains("\"JointRivetGripsFromSteel\": true", p1.ToJson());
        Assert.True(BridgeProject.FromJson(p1.ToJson()).Bridges[0].Truss!.JointRivetGripsFromSteel);
        var s0 = Regen(p0).Steel!; var s1 = Regen(p1).Steel!;
        static List<BoltInstance> R(SteelModel s) => s.Bolts.Where(x => x.Kind == BoltKind.Rivet).OrderBy(x => x.Frame.Origin.X).ThenBy(x => x.Frame.Origin.Z).ThenBy(x => x.Frame.Origin.Y).ToList();
        var r0 = R(s0); var r1 = R(s1);
        Assert.Equal(r0.Count, r1.Count);
        // the pitch rows off the H diagonal: gusset + angle (1 1/8) by the section rule, the 3/4 gusset by the steel
        Assert.Equal(new[] { 1.125, 1.75, 4.125 }, r0.Select(x => Math.Round(x.Grip / 0.0254 * 16) / 16).Distinct().OrderBy(x => x).ToArray());
        Assert.Equal(new[] { 0.75, 1.75, 4.125 }, r1.Select(x => Math.Round(x.Grip / 0.0254 * 16) / 16).Distinct().OrderBy(x => x).ToArray());
        Assert.Equal(2 * 4, r1.Count(x => Math.Abs(x.Grip - In(0.75)) < 1e-6));
        int shorter = 0;
        for (int i = 0; i < r0.Count; i++)
        {
            Assert.True(r1[i].Grip <= r0[i].Grip + In(1.5) + 1e-9);
            if (r1[i].Grip < r0[i].Grip - 1e-6) shorter++;
            // the head stays on the gusset face (no other steel over the stack here)
            Assert.True((r1[i].Frame.Origin - r0[i].Frame.Origin).Length < 1e-6, "head moved");
        }
        Assert.True(shorter > 0);
        Assert.Contains(s1.Diagnostics, d => d.Message.StartsWith("Truss: joint rivet grips from the steel along the shank"));
        // the H diagonal U1-L2 stands 3 in. off the gusset (no fill): its rivets keep the air in the grip and it is reported
        Assert.Contains(s1.Diagnostics, d => d.Message.Contains("have air inside their grip"));
        Assert.DoesNotContain(s0.Diagnostics, d => d.Message.Contains("joint rivet grips from the steel"));
    }

    [Fact]
    public void LacedChannels_AndBox_TurnedNinetyDegrees()
    {
        var ch = new TrussSectionDefinition { Name = "CH", Kind = TrussSectionKind.LacedChannels, Channel = "C12x20.7", BackToBack = In(16), TopPlates = { new TrussPlate(In(14), In(0.375)) }, BottomPlates = { new TrussPlate(In(14), In(0.375)) } };
        var a = TrussSections.Build(ch);
        ch.Rotated = true;
        var b = TrussSections.Build(ch);
        Assert.Equal(a.XMax - a.XMin, b.YMax - b.YMin, 9);
        Assert.Equal(a.YMax - a.YMin, b.XMax - b.XMin, 9);
        Assert.Equal(In(16), b.YMax - b.YMin, 9);   // back to back = the depth now
        Assert.Equal(a.Area, b.Area, 12);
        Assert.Equal(In(0.375), b.OuterPly, 12);    // the cover plates are the plies at the +/- x faces
        Assert.Contains(b.Notes, n => n.Contains("turned 90 degrees"));
        // a turned box: the web plies lie top and bottom
        var box = new TrussSectionDefinition { Name = "B", Kind = TrussSectionKind.Box, WebDepth = In(24), WebPlies = { new TrussPly(0, In(0.5)) }, WebsOutToOut = In(20), Angle = "L4x4x1/2", Rotated = true };
        var bb = TrussSections.Build(box);
        Assert.Equal(In(20), bb.YMax - bb.YMin, 9);
        Assert.Equal(0, bb.WebOuterX);
        // in a model: the turned member's section depth across the bracing plane, area / mass unchanged
        var (p0, b0) = TrussTests.Model();
        var (p1, b1) = TrussTests.Model();
        b0.Truss!.Sections.First(s => s.Name == "CH").Lacing = null;   // lacing of a turned section is not built: compare without
        b1.Truss!.Sections.First(s => s.Name == "CH").Lacing = null;
        b1.Truss!.Sections.First(s => s.Name == "CH").Rotated = true;
        var m0 = Regen(p0).Steel!.Parts.First(x => x.Mark == "N-U3-L3");
        var m1 = Regen(p1).Steel!.Parts.First(x => x.Mark == "N-U3-L3");
        Assert.Equal(m0.Volume, m1.Volume, 6);
        var e0 = m0.Mesh.Bounds(); var e1 = m1.Mesh.Bounds();
        Assert.NotEqual(e0.Max.X - e0.Min.X, e1.Max.X - e1.Min.X, 3);
    }

    [Fact]
    public void ContinuousStringers_OnePartPerMember_MassKept()
    {
        var (p0, _) = TrussTests.Model();
        var (p1, b1) = TrussTests.Model();
        var fl = b1.Truss!.Floor!;
        fl.Stringers[0].ContinuousBays = new() { 2, 4 };
        fl.Stringers[0].RunTypes = new() { "S13", "S9" };
        fl.Stringers[1].ContinuousBays = new() { 4 };                // 2 bays left over: single pieces + a warning
        var json = p1.ToJson();
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(json, "\"ContinuousBays\"").Count);
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(json, "\"RunTypes\"").Count);
        Assert.DoesNotContain("ContinuousBays", p0.ToJson());
        var bm0 = Regen(p0); var bm1 = Regen(p1);
        var st0 = bm0.Steel!.Parts.Where(x => x.Category == PartCategory.TrussStringer).ToList();
        var st1 = bm1.Steel!.Parts.Where(x => x.Category == PartCategory.TrussStringer).ToList();
        Assert.Equal(18, st0.Count);                                 // 3 lines x 6 bays
        Assert.Equal(2 + 3 + 6, st1.Count);                          // A: 2 members, B: 1 + 2 single, C: 6
        Assert.Equal(st0.Sum(x => x.Mass), st1.Sum(x => x.Mass), 6);
        Assert.Equal(st0.Sum(x => x.Volume), st1.Sum(x => x.Volume), 9);
        var a2 = st1.Single(x => x.Mark == "ST-A-2");
        Assert.Equal(st0.Where(x => new[] { "ST-A-3", "ST-A-4", "ST-A-5", "ST-A-6" }.Contains(x.Mark)).Sum(x => x.Length), a2.Length, 9);
        Assert.Contains("S9 4-span continuous stringer", a2.Description);
        Assert.True(a2.Mesh.HasExactSolids);
        Assert.Equal("Bays 3-6", bm1.Steel!.Truss!.PanelOf["ST-A-2"]);
        Assert.Equal("Bay 6", bm1.Steel!.Truss!.PanelOf["ST-B-3"]);
        Assert.Contains(bm1.Steel!.Truss!.Members, m => m.Mark == "ST-A-1" && m.Role == TrussMemberRole.Stringer);
        Assert.DoesNotContain(bm1.Steel!.Truss!.Members, m => m.Mark.Contains("bay"));
        Assert.Contains(bm1.Steel!.Diagnostics, d => d.Message.Contains("stringer line B - the continuous members list 4 bays, the line has 6"));
        // other lines unchanged
        Assert.Equal(st0.Where(x => x.Mark.StartsWith("ST-C-")).Select(x => x.Mark), st1.Where(x => x.Mark.StartsWith("ST-C-")).Select(x => x.Mark));
    }

    [Fact]
    public void PartPerPiece_EveryPlyAngleCoverItsOwnPart_MassVolumeKept()
    {
        var (p0, _) = TrussTests.Model();
        var (p1, b1) = TrussTests.Model();
        b1.Truss!.PartPerPiece = true;
        Assert.DoesNotContain("PartPerPiece", p0.ToJson());
        Assert.Contains("\"PartPerPiece\": true", p1.ToJson());
        var s0 = Regen(p0).Steel!; var s1 = Regen(p1).Steel!;
        Assert.Equal(s0.SteelMass, s1.SteelMass, 6);
        Assert.Equal(s0.Parts.Sum(x => x.Volume), s1.Parts.Sum(x => x.Volume), 9);
        // a box chord: 2 web plies + 4 angles + top + bottom cover = 8 pieces, marks <member>-W1.. / -A1.. / -C1..
        var ch = s1.Parts.Where(x => x.Member == "N-L2-L3").ToList();
        Assert.Equal(8, ch.Count);
        Assert.Equal(new[] { "A", "C", "W" }, ch.Select(x => new string(x.Mark["N-L2-L3-".Length..].TakeWhile(char.IsLetter).ToArray())).Distinct().OrderBy(x => x).ToArray());
        Assert.Equal(s0.Parts.Single(x => x.Mark == "N-L2-L3").Volume, ch.Sum(x => x.Volume), 9);
        Assert.All(ch, x => { Assert.Equal(PartCategory.TrussChord, x.Category); Assert.True(x.Mesh.HasExactSolids); Assert.NotNull(x.Piece); });
        Assert.DoesNotContain(s1.Parts, x => x.Mark == "N-L2-L3");
        // laced channel vertical: 2 channels + 2 lacing faces
        Assert.Equal(new[] { "channel", "channel", "lacing", "lacing" }, s1.Parts.Where(x => x.Member == "N-U3-L3").Select(x => x.Piece!).OrderBy(x => x).ToArray());
        // rolled floor beam: one piece carrying the AISC weight
        var fb = s1.Parts.Single(x => x.Member == "FB2");
        Assert.Equal(s0.Parts.Single(x => x.Mark == "FB2").Mass, fb.Mass, 6);
        // gussets: their joint stack
        Assert.Equal("N-JL2", s1.Parts.Single(x => x.Mark == "N-GL2-L").Member);
        Assert.Equal("gusset", s1.Parts.Single(x => x.Mark == "N-GL2-R").Piece);
        // member results keep the member mass
        var tm = s1.Truss!;
        Assert.Equal(tm.Members.Single(m => m.Mark == "N-L2-L3").Mass, ch.Sum(x => x.Mass), 6);
        // rivet grips unchanged (same solids)
        Assert.Equal(s0.Bolts.Select(x => x.Grip).OrderBy(x => x), s1.Bolts.Select(x => x.Grip).OrderBy(x => x));
    }

    [Fact]
    public void PartPerPiece_Ifc_MemberAssemblies_Database_MemberLevel()
    {
        var (p, b) = TrussTests.Model();
        b.Truss!.PartPerPiece = true;
        b.Truss.Floor!.Stringers[0].ContinuousBays = new() { 6 };
        var bm = Regen(p);
        var steel = bm.Steel!;
        // continuous stringer made of pieces: one rolled piece over the 6 bays
        var st = steel.Parts.Single(x => x.Member == "ST-A-1");
        Assert.Equal("ST-A-1-R1", st.Mark);
        var sw = new StringWriter();
        IfcExporter.Export(sw, "truss.ifc", p, new[] { new IfcBridgeData(bm.Framing, steel, bm.Concrete) });
        string ifc = sw.ToString();
        int members = steel.Parts.Where(x => x.Member != null).Select(x => x.Member).Distinct().Count();
        int stacks = steel.Parts.Where(x => x.Member != null && x.Piece is "gusset" or "gusset layer").Select(x => x.Member).Distinct().Count();
        Assert.Equal(members - stacks, System.Text.RegularExpressions.Regex.Matches(ifc, "'BUILT_UP_MEMBER'").Count);
        Assert.Equal(stacks, System.Text.RegularExpressions.Regex.Matches(ifc, "'GUSSET_STACK'").Count);
        Assert.Contains(".COVER_PLATE.", ifc);
        Assert.Contains(".WEB_PLATE.", ifc);
        Assert.Contains("'ANGLE'", ifc);
        // database: member rows (= the IFC member assemblies) between the panel / joint group and the pieces
        string dir = Path.Combine(Path.GetTempPath(), "sbmb1_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var doc = SbmDocument.Attach(p);
            doc.Regenerate();
            string db = Path.Combine(dir, "t.sqlite");
            doc.ExportDatabase(db);
            using var cn = ModelDatabase.Open(db);
            Assert.Equal(members, ModelDatabase.Scalar<long>(cn, "SELECT COUNT(*) FROM elements WHERE level = 'member'"));
            Assert.Equal(8, ModelDatabase.Scalar<long>(cn, "SELECT COUNT(*) FROM elements c JOIN elements m ON c.parent_global_id = m.global_id WHERE m.level = 'member' AND m.mark = 'N-L2-L3'"));
            Assert.Equal("truss panel", ModelDatabase.Scalar<string>(cn, "SELECT g.category FROM elements m JOIN elements g ON m.parent_global_id = g.global_id WHERE m.level = 'member' AND m.mark = 'N-L2-L3'"));
            Assert.Equal("gusset stack", ModelDatabase.Scalar<string>(cn, "SELECT category FROM elements WHERE level = 'member' AND mark = 'N-JL2'"));
            // the member row GlobalId is the IFC one
            string gid = ModelDatabase.Scalar<string>(cn, "SELECT global_id FROM elements WHERE level = 'member' AND mark = 'N-L2-L3'");
            Assert.Contains($"'{gid}'", ifc);
        }
        finally { SqliteConnection.ClearAllPools(); try { Directory.Delete(dir, true); } catch (IOException) { } }
    }

    [Fact]
    public void SubElements_PickLevel_Listing_Tints()
    {
        Assert.Equal(PickLevel.Member, SubElements.Effective(PickLevel.Member, false));
        Assert.Equal(PickLevel.Part, SubElements.Effective(PickLevel.Member, true));
        Assert.Equal(PickLevel.Member, SubElements.Effective(PickLevel.Part, true));
        var (p, b) = TrussTests.Model();
        b.Truss!.PartPerPiece = true;
        var steel = Regen(p).Steel!;
        var ply = steel.Parts.First(x => x.Member == "N-L2-L3");
        Assert.Equal("N-L2-L3", SubElements.Resolve(ply, PickLevel.Member));
        Assert.Null(SubElements.Resolve(ply, PickLevel.Part));
        Assert.Null(SubElements.Resolve("not a part", PickLevel.Member));
        var rows = SubElements.List(steel.Parts, "N-L2-L3");
        Assert.Equal(8, rows.Count);
        Assert.Equal(new[] { "-A1", "-A2", "-A3", "-A4", "-C1", "-C2", "-W1", "-W2" }, rows.Select(r => r.Mark["N-L2-L3".Length..]).ToArray());
        Assert.Equal(steel.Parts.Where(x => x.Member == "N-L2-L3").Sum(x => x.Mass), rows.Sum(r => r.MassKg), 9);
        // tags in pick tables (any object): only the member's own
        var tags = steel.Parts.Cast<object>().Append("other").ToList();
        Assert.Equal(8, SubElements.TagsOf(tags, "N-L2-L3", t => t).Count);
        var tints = SubElements.Tints(steel.Parts.Where(x => x.Member == "N-L2-L3"));
        Assert.Equal(8, tints.Count);
        for (int i = 1; i < rows.Count; i++) Assert.NotEqual(tints[rows[i - 1].Part], tints[rows[i].Part]);
    }

}
