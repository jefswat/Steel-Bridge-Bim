using SBM.Api;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Substructure;
using Xunit;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Tests;

public class SubstructureTests
{
    private static SubstructureModel Build(Action<SubstructureDefinition>? pier = null, Action<SubstructureDefinition>? abut = null)
    {
        var doc = SbmDocument.Sample();
        foreach (var s in doc.Bridge().Supports)
        {
            var sd = SubstructureDefinition.Default(s.Type);
            if (s.Type == SupportType.Pier) pier?.Invoke(sd); else abut?.Invoke(sd);
            s.Substructure = sd;
        }
        return doc.Substructure();
    }

    [Fact]
    public void EverySupport_HasCapPedestalsAndFoundations()
    {
        var m = Build();
        Assert.Equal(3, m.Units.Count);
        var pier = m.Units.Single(u => u.Type == SupportType.Pier);
        Assert.Equal(3, pier.Columns.Count);
        Assert.Equal(3, pier.Foundations.Count);
        Assert.Equal(5, pier.Pedestals.Count);
        Assert.All(m.Units, u => Assert.Contains(u.Solids, s => s.Kind == SubElementKind.Cap));
        Assert.All(m.Units.Where(u => u.Type == SupportType.Abutment), u => Assert.NotNull(u.Backwall));
        Assert.All(m.Units.SelectMany(u => u.Solids), s => Assert.True(Math.Abs(s.Mesh.Volume()) > 0, s.Name));
    }

    [Fact]
    public void ShortestPedestal_IsTheMinimum_AndSeatsAreMet()
    {
        var m = Build(p => p.MinPedestalHeight = In(2), a => a.MinPedestalHeight = In(2));
        foreach (var u in m.Units)
        {
            Assert.Equal(In(2), u.Pedestals.Min(p => p.Height), 6);
            Assert.All(u.Pedestals, p => Assert.Equal(p.SeatZ, u.CapTopAt(p.S) + p.Height, 9));
        }
    }

    [Fact]
    public void FollowSeats_GivesUniformPedestals_LevelGivesVariable()
    {
        var follow = Build(p => { p.TopShape = CapTopShape.FollowSeats; p.MinPedestalHeight = In(3); }).Units.Single(u => u.Type == SupportType.Pier);
        Assert.All(follow.Pedestals, p => Assert.Equal(In(3), p.Height, 3));
        var level = Build(p => p.TopShape = CapTopShape.Level).Units.Single(u => u.Type == SupportType.Pier);
        // Superelevated deck: seats differ by feet across the bent, so level-cap pedestals vary.
        Assert.True(level.Pedestals.Max(p => p.Height) - level.Pedestals.Min(p => p.Height) > In(6));
        Assert.Equal(level.CapTop[0].Y, level.CapTop[^1].Y, 9);
    }

    [Fact]
    public void SlopedCap_FitsSeats_AndBottomIsParallel()
    {
        var u = Build(p => p.TopShape = CapTopShape.Sloped).Units.Single(x => x.Type == SupportType.Pier);
        double slope = (u.CapTop[^1].Y - u.CapTop[0].Y) / (u.CapTop[^1].X - u.CapTop[0].X);
        Assert.True(Math.Abs(slope) > 0.01, $"slope {slope}");
        Assert.True(u.Pedestals.Max(p => p.Height) - u.Pedestals.Min(p => p.Height) < In(3));
        Assert.All(u.CapBottom.Zip(u.CapTop), p => Assert.Equal(Ft(4), p.Second.Y - p.First.Y, 9));
    }

    [Fact]
    public void OctagonalColumn_BarsFollowTheChamfers_WithPlanCover()
    {
        // I-80 type column: 8'-0 x 4'-6 with 2'-4 x 1'-3 chamfers, 6 / 4 / 3 bars per S / N / chamfer face, paired hoops
        // overlapping 3'-4, crossties; flared base 9'-0 x 5'-6 with its own cage.
        var m = Build(p =>
        {
            p.ColumnShape = ColumnShape.Rectangular; p.ColumnWidth = Ft(8); p.ColumnDepth = Ft(4.5);
            p.ColumnChamferS = Ft(2 + 4 / 12.0); p.ColumnChamferN = Ft(1.25);
            p.ColumnFlareElevation = Ft(512); p.ColumnFlareWidth = Ft(9); p.ColumnFlareDepth = Ft(5.5);
            p.ColumnFlareChamferS = Ft(2 + 8.5 / 12); p.ColumnFlareChamferN = Ft(1 + 5.625 / 12);
            p.FoundationTopElevation = Ft(500); p.Foundation = FoundationType.PileCap;
            var r = p.Reinforcement;
            r.ColumnBarSize = "#11"; r.ColumnCover = In(2); r.ColumnSpiralSize = "#6"; r.ColumnSpiralPitch = In(12);
            r.ColumnFaceBarsS = 6; r.ColumnFaceSpacingS = In(7.5); r.ColumnFaceBarsN = 4; r.ColumnFaceSpacingN = In(6.5);
            r.ColumnChamferBarPositions = new() { In(8), In(15.25), In(22.5) };
            r.ColumnHoopOverlap = In(40); r.ColumnCrosstieSize = "#5"; r.ColumnCrosstieBar = 2;
            r.FlareBarSize = "#7"; r.FlareFaceBarsS = 5; r.FlareFaceBarsN = 4; r.FlareChamferBars = 4; r.FlareTieSpacing = In(6);
        });
        var u = m.Units.Single(x => x.Type == SupportType.Pier);
        var c = u.Columns[0];
        var marks = m.Marks.Where(mk => mk.Unit == u.Name).ToDictionary(mk => mk.Mark);
        // Column 1's bars: column bar marks are shared between columns, so pick them by position.
        var mine = m.Bars.Where(b => b.Unit == u.Name && marks[b.Mark].Location.Contains(" column ")
            && Math.Abs(b.Points.Average(p => Vec2.Dot(p.XY - u.Origin, u.D)) - c.S) < Ft(6)).ToList();
        int Count(string what) => mine.Count(b => marks[b.Mark].Location.EndsWith(what));
        Assert.Equal(2 * 6 + 2 * 4 + 4 * 3, Count("verticals"));
        Assert.Equal(2 * 5 + 2 * 4 + 4 * 4 - 8, Count("flare bars"));
        Assert.Equal(Count("crossties"), Count("hoops"));                         // two hoops and two crossties per level
        // Clear cover to the octagon (column bars) / flare section, in the section plane.
        double a = c.SizeS / 2, bb = c.SizeN / 2;
        double Clear(Vec2[] sec, Vec2 q) => Enumerable.Range(0, sec.Length).Min(k => Vec2.Cross((sec[(k + 1) % sec.Length] - sec[k]).Normalized(), q - sec[k]));
        Vec2[] Oct(double w, double d, double cs, double cn) => new[] { new Vec2(-w / 2 + cs, -d / 2), new Vec2(w / 2 - cs, -d / 2), new Vec2(w / 2, -d / 2 + cn),
            new Vec2(w / 2, d / 2 - cn), new Vec2(w / 2 - cs, d / 2), new Vec2(-w / 2 + cs, d / 2), new Vec2(-w / 2, d / 2 - cn), new Vec2(-w / 2, -d / 2 + cn) };
        var col = Oct(c.SizeS, c.SizeN, Ft(2 + 4 / 12.0), Ft(1.25));
        var fl = Oct(Ft(9), Ft(5.5), Ft(2 + 8.5 / 12), Ft(1 + 5.625 / 12));
        foreach (var bar in mine)
        {
            var mk = marks[bar.Mark];
            bool flare = mk.Location.Contains("flare");
            foreach (var p in bar.Points.Where(p => p.Z >= c.BottomZ - 1e-9))
            {
                var q = new Vec2(Vec2.Dot(p.XY - u.Origin, u.D) - c.S, Vec2.Dot(p.XY - u.Origin, u.N) - c.N);
                double clear = Clear(flare ? fl : col, q) - mk.Size.Diameter / 2;
                Assert.True(clear >= (flare ? In(3.5) : In(2)) - In(0.01), $"{mk.Location}: {clear / In(1):F2} in clear");
            }
        }
    }

    [Fact]
    public void PileCap_HasPileGroups_AndRebarMarks()
    {
        var m = Build(p => { p.Foundation = FoundationType.PileCap; p.FoundationTopElevation = Ft(500); });
        var pier = m.Units.Single(u => u.Type == SupportType.Pier);
        Assert.All(pier.Foundations, f => Assert.Equal(9, f.Piles.Count));
        Assert.All(pier.Columns, c => Assert.Equal(Ft(500), c.BottomZ, 9));
        Assert.Contains(m.Marks, mk => mk.Unit == "Pier 1" && mk.Mark.StartsWith("F"));
        Assert.Contains(m.Marks, mk => mk.Unit == "Pier 1" && mk.Shape.Code == "SPIRAL");
        Assert.Contains(m.Marks, mk => mk.Unit == "Pier 1" && mk.Shape.Code == "TIE");
        Assert.All(m.Marks, mk => Assert.True(mk.Length <= Ft(60) + In(1) || mk.Shape.Code == "SPIRAL", $"{mk.Unit} {mk.Mark}"));
    }

    [Fact]
    public void Substructure_RoundTripsThroughProjectFile()
    {
        var doc = SbmDocument.Sample();
        doc.Bridge().Supports[1].Substructure = new SubstructureDefinition { ColumnCount = 2, ColumnSpacings = { Ft(20) }, TopShape = CapTopShape.Crowned };
        var json = doc.Project.ToJson();
        var back = SBM.Core.Project.BridgeProject.FromJson(json);
        var sd = back.Bridges[0].Supports[1].Substructure!;
        Assert.Equal(CapTopShape.Crowned, sd.TopShape);
        Assert.Equal(2, sd.ColumnCount);
        Assert.Null(back.Bridges[0].Supports[0].Substructure);
    }

    private static SubstructureModel BuildBattered(double ratio, Func<List<FootingPoint>> piles, bool sides = false, bool diag = false)
    {
        return Build(p =>
        {
            p.Foundation = FoundationType.PileCap; p.FoundationTopElevation = Ft(500); p.PileLength = Ft(60); p.PileEmbedment = Ft(1);
            var fd = new FootingDefinition { Piles = piles() };
            fd.BatterPerimeter(ratio, sides, diag);
            p.Footings = Enumerable.Range(1, 3).Select(k => { var c = fd.Clone(); c.Column = k; return c; }).ToList();
        });
    }

    [Fact]
    public void BatteredPile_TipMovesOutward_LengthAlongAxis_HeadHorizontal()
    {
        // 3 x 3 grid at 5 ft, 1:4 on the outer N rows, outward in N.
        var m = BuildBattered(4, () => (from s in new[] { -5.0, 0, 5 } from n in new[] { -5.0, 0, 5 } select new FootingPoint(Ft(s), Ft(n))).ToList());
        var pier = m.Units.Single(u => u.Type == SupportType.Pier);
        var f = pier.Foundations[0];
        Assert.Equal(9, f.Piles.Count);
        Assert.Equal(6, f.Piles.Count(p => p.IsBattered));
        var pile = f.Piles.First(p => p.N - f.N > Ft(4));                 // outer +N row
        double len = Ft(60), drop = len / Math.Sqrt(1 + 1 / 16.0);
        Assert.Equal(len, pile.Length, 6);
        Assert.Equal(drop, pile.TopZ - pile.TipZ, 6);
        Assert.Equal(drop / 4, pile.TipN - pile.N, 6);              // tip moves +N by drop/4
        Assert.Equal(pile.S, pile.TipS, 9);                         // S unchanged for an N-edge pile
        // Plan point at the footing bottom plane is the dimensioned position: head is inside by embedment/4.
        Assert.Equal(Ft(5) - Ft(1) / 4, pile.N - f.N, 6);
        // Plumb piles stay plumb.
        Assert.All(f.Piles.Where(p => !p.IsBattered), p => Assert.Equal(p.Length, p.TopZ - p.TipZ, 9));
        // Solid: top and bottom are horizontal, the mesh spans the head..tip in plan along N.
        var solid = pier.Solids.First(x => x.Kind == SubElementKind.Pile && x.Description.Contains("battered"));
        var b = solid.Mesh.Bounds();
        Assert.Equal(pile.TopZ - pile.TipZ, b.Max.Z - b.Min.Z, 6);
        Assert.True(solid.Mesh.Volume() > 0);
        Assert.True(solid.Mesh.HasExactSolids);                       // oblique extrusion recipe: IFC writes an IfcExtrudedAreaSolid
        double plumbVol = pier.Solids.First(x => x.Kind == SubElementKind.Pile && !x.Description.Contains("battered")).Mesh.Volume();
        // Same cross-section, axis length 60 ft vs vertical 60 ft: equal volume.
        Assert.Equal(plumbVol, solid.Mesh.Volume(), 4);
    }

    [Fact]
    public void BatteredCornerPiles_Diagonal45_And_FileRoundTrip()
    {
        var m = BuildBattered(4, () => (from s in new[] { -6.0, 0, 6 } from n in new[] { -6.0, 0, 6 } select new FootingPoint(Ft(s), Ft(n))).ToList(), sides: true, diag: true);
        var f = m.Units.Single(u => u.Type == SupportType.Pier).Foundations[0];
        Assert.Equal(8, f.Piles.Count(p => p.IsBattered));          // perimeter ring
        var corner = f.Piles.First(p => p.S - f.S > Ft(5) && p.N - f.N > Ft(5));
        Assert.Equal(corner.TipS - corner.S, corner.TipN - corner.N, 9);   // 45 deg
        double drop = corner.TopZ - corner.TipZ;
        Assert.Equal(drop / 4, Math.Sqrt(Math.Pow(corner.TipS - corner.S, 2) + Math.Pow(corner.TipN - corner.N, 2)), 6);   // 1:4 along the diagonal

        var doc = SbmDocument.Sample();
        var sd = SubstructureDefinition.Default(SupportType.Pier);
        var fd = new FootingDefinition { Column = 1, Piles = { new FootingPoint(0, Ft(5)), new FootingPoint(0, 0) } };
        fd.BatterPerimeter(6, false, false);
        sd.Footings = new() { fd };
        doc.Bridge().Supports[1].Substructure = sd;
        var back = SBM.Core.Project.BridgeProject.FromJson(doc.Project.ToJson()).Bridges[0].Supports[1].Substructure!.Footings[0];
        Assert.Equal(6, back.BatterRatio);
        Assert.Equal(1, back.Piles[0].BatterDirN, 9);
        Assert.False(back.Piles[1].IsBattered);
    }

    [Fact]
    public void BatteredPiles_RunningIntoNeighbours_AreReported()
    {
        // Two piles 1 ft apart, one battered toward the other.
        var m = Build(p =>
        {
            p.Foundation = FoundationType.PileCap; p.FoundationTopElevation = Ft(500); p.PileLength = Ft(60);
            var fd = new FootingDefinition { Column = 1, BatterRatio = 2, Piles = { new FootingPoint(0, 0, 0, 1), new FootingPoint(0, Ft(1.5)) } };
            p.Footings = new() { fd };
        });
        Assert.Contains(m.Diagnostics, d => d.Message.Contains("battered") && d.Message.Contains("closer than a pile width"));
    }

    private static (double s, double n) MeshExtent(SubstructureUnit u, SBM.Core.Solids.Mesh m, double top)
    {
        // Plan extent (S, N) of the mesh slice near the top (head) of a pile.
        var v = m.Positions.Where(q => q.Z > top - 1e-6).Select(q => new Vec2(Vec2.Dot(q.XY - u.Origin, u.D), Vec2.Dot(q.XY - u.Origin, u.N))).ToList();
        return (v.Max(q => q.X) - v.Min(q => q.X), v.Max(q => q.Y) - v.Min(q => q.Y));
    }

    [Fact]
    public void PileWebOrientation_Default_Explicit_AndAlongBatter()
    {
        // HP14x73: depth 13.6 in, flange width 14.6 in. Default: flanges parallel to the cap (S extent = flange width).
        var m = Build(p =>
        {
            p.Foundation = FoundationType.PileCap; p.FoundationTopElevation = Ft(500); p.PileLength = Ft(60); p.PileEmbedment = Ft(1);
            var fd = new FootingDefinition { Column = 1, PileWeb = PileWebMode.AlongBatter, Piles =
            {
                new FootingPoint(0, 0),                                  // plumb, default web along N
                new FootingPoint(Ft(5), 0) { WebDirS = 1, WebDirN = 0 }, // plumb, explicit web along S
                new FootingPoint(0, Ft(5), 0, 1),                        // battered in N: web along N
                new FootingPoint(-Ft(5), 0, -1, 0),                      // battered in S: web along S
                new FootingPoint(Ft(5), Ft(5), Math.Sqrt(0.5), Math.Sqrt(0.5)),   // diagonal
            } };
            p.Footings = new() { fd };
        });
        var u = m.Units.Single(x => x.Type == SupportType.Pier);
        var f = u.Foundations[0];
        Assert.Equal((0, 1), (f.Piles[0].WebS, f.Piles[0].WebN));
        Assert.Equal((1, 0), (f.Piles[1].WebS, f.Piles[1].WebN));
        Assert.Equal((0, 1), (f.Piles[2].WebS, f.Piles[2].WebN));
        Assert.Equal(-1, f.Piles[3].WebS, 9);
        Assert.Equal(Math.Sqrt(0.5), f.Piles[4].WebS, 9);
        var piles = u.Solids.Where(x => x.Kind == SubElementKind.Pile).ToList();
        var e0 = MeshExtent(u, piles[0].Mesh, f.Piles[0].TopZ);
        Assert.Equal(In(14.6), e0.s, 3); Assert.Equal(In(13.6), e0.n, 3);          // flanges along S, web across
        var e1 = MeshExtent(u, piles[1].Mesh, f.Piles[1].TopZ);
        Assert.Equal(In(13.6), e1.s, 3); Assert.Equal(In(14.6), e1.n, 3);          // turned 90 degrees
        // Battered along S, web along the batter: the section is turned like pile 1 (head slice is oblique, so only check the volume).
        Assert.Equal(piles[0].Mesh.Volume(), piles[3].Mesh.Volume(), 3);
        Assert.Equal(piles[0].Mesh.Volume(), piles[4].Mesh.Volume(), 3);
    }

    [Fact]
    public void ColumnSplices_DowelsAndCouplers_MatchPlanConventions()
    {
        // 8'-0 x 4'-6 chamfered column, #18, two coupler levels: 16 bars per level, d3 dowels long / d4 short.
        double top = Ft(500);
        var m = Build(p =>
        {
            p.ColumnShape = ColumnShape.Rectangular; p.ColumnWidth = Ft(8); p.ColumnDepth = Ft(4.5);
            p.ColumnChamferS = Ft(2 + 4 / 12.0); p.ColumnChamferN = Ft(1.25);
            p.FoundationTopElevation = top; p.Foundation = FoundationType.PileCap; p.FootingThickness = Ft(6);
            var r = p.Reinforcement;
            r.ColumnBarSize = "#18"; r.ColumnCover = In(2); r.ColumnSpiralSize = "#6"; r.ColumnSpiralPitch = In(12);
            r.ColumnFaceBarsS = 6; r.ColumnFaceSpacingS = In(7.5); r.ColumnFaceBarsN = 4; r.ColumnFaceSpacingN = In(6);
            r.ColumnChamferBarPositions = new() { In(6.5), In(15.5), In(21.25) };
            r.ColumnSplice = ColumnSpliceType.Coupler; r.CouplerLow = Ft(4) + In(11); r.CouplerHigh = Ft(9) + In(11);
            r.DowelBottomAboveFooting = Ft(1) + In(3); r.ColumnBarEmbedInCap = Ft(5) + In(11);
            r.ColumnSplicePattern = "HLHLHLHLLHLHLHLHHLHLHLHLLHLHLHLH";
        });
        var u = m.Units.Single(x => x.Type == SupportType.Pier);
        var marks = m.Marks.Where(k => k.Unit == u.Name).ToList();
        var dow = marks.Where(k => k.Shape.Code == "L").ToList();
        Assert.Equal(3 * 32, dow.Sum(k => k.Count));
        Assert.All(dow, k => Assert.Equal(Ft(3) + In(5), k.Shape.Dimensions[1].Value, 6));          // #18 hook 3'-5
        Assert.Equal(2, dow.Count);                                                                 // long and short dowels
        double dLong = dow.Max(k => k.Length), dShort = dow.Min(k => k.Length);
        Assert.Equal(Ft(5), dLong - dShort, 6);                                                     // levels 5'-0 apart
        // Dowel A = coupler level + footing thickness - hook height; length = A + B.
        Assert.Equal(Ft(9) + In(11) + Ft(6) - (Ft(1) + In(3)) + Ft(3) + In(5), dLong, 6);
        // Couplers: one per column bar; column bars start at the coupler.
        var cp = u.Solids.Where(x => x.Kind == SubElementKind.Coupler).ToList();
        Assert.Equal(3 * 32, cp.Count);
        Assert.All(cp, c => Assert.True(c.Mesh.HasExactSolids && !c.IsConcrete));
        var vMarks = marks.Where(k => k.Shape.Code == "STR" && k.Location.Contains("verticals")).Select(k => k.Mark).ToHashSet();
        var bottoms = m.Bars.Where(b => b.Unit == u.Name && vMarks.Contains(b.Mark)).Select(b => Math.Round(b.Points.Min(q => q.Z), 4)).Distinct().OrderBy(z => z).ToList();
        Assert.Equal(2, bottoms.Count);                                                             // two splice levels
        Assert.Equal(Ft(5), bottoms[1] - bottoms[0], 3);
        Assert.Equal(top + Ft(4) + In(11), bottoms[0], 3);
        // Dowel centerline starts at the hook (on the bottom mat) and ends at the coupler level.
        var dwPl = m.Bars.Where(b => b.Unit == u.Name && dow.Any(k => k.Mark == b.Mark)).ToList();
        Assert.All(dwPl, b => Assert.True(b.Points.Min(q => q.Z) >= top - Ft(6) + Ft(1) + In(3) - 1e-9));
    }

    [Fact]
    public void ColumnSplices_LapPier_DowelProjectsLapLength_ColumnBarsStartAtFootingTop()
    {
        double top = Ft(500);
        var m = Build(p =>
        {
            p.ColumnShape = ColumnShape.Rectangular; p.ColumnWidth = Ft(8); p.ColumnDepth = Ft(4.5);
            p.ColumnChamferS = Ft(2 + 4 / 12.0); p.ColumnChamferN = Ft(1.25);
            p.FoundationTopElevation = top; p.Foundation = FoundationType.PileCap; p.FootingThickness = Ft(5.5);
            var r = p.Reinforcement;
            r.ColumnBarSize = "#11"; r.ColumnCover = In(2); r.ColumnSpiralSize = "#6"; r.ColumnSpiralPitch = In(12);
            r.ColumnFaceBarsS = 6; r.ColumnFaceSpacingS = In(7.5); r.ColumnFaceBarsN = 4; r.ColumnFaceSpacingN = In(6.5);
            r.ColumnChamferBarPositions = new() { In(8), In(15.25), In(22.5) };
            r.ColumnSplice = ColumnSpliceType.Lap; r.ColumnLapLength = Ft(8) + In(4); r.DowelBottomAboveFooting = Ft(1) + In(4);
        });
        var u = m.Units.Single(x => x.Type == SupportType.Pier);
        var marks = m.Marks.Where(k => k.Unit == u.Name).ToList();
        var dow = marks.Single(k => k.Shape.Code == "L");
        Assert.Equal(3 * 32, dow.Count);
        Assert.Equal(Ft(8) + In(4) + Ft(5.5) - (Ft(1) + In(4)) + Ft(2), dow.Length, 6);             // 14'-6 style: A + 2'-0 hook
        Assert.Equal(top, m.Bars.Where(b => marks.Any(k => k.Mark == b.Mark && k.Shape.Code == "STR" && k.Location.Contains("verticals")))
            .Min(b => b.Points.Min(q => q.Z)), 6);                                                  // bars start at the footing top
        Assert.DoesNotContain(u.Solids, s => s.Kind == SubElementKind.Coupler);
    }

    [Fact]
    public void PlanBars_Footing_MatsSideBarsAndUpliftAnchors_FollowTheBarList()
    {
        double top = Ft(500);
        var m = Build(p =>
        {
            p.Foundation = FoundationType.PileCap; p.FoundationTopElevation = top; p.FootingThickness = Ft(5.5); p.PileEmbedment = Ft(1);
            p.FootingLength = Ft(18); p.FootingWidth = Ft(16);
            var fd = new FootingDefinition { Column = 1, UpliftBarsPerPile = 3 };
            for (int i = 0; i < 4; i++) fd.Piles.Add(new FootingPoint(Ft(-6 + 4 * i), 0));
            fd.UpliftPiles = new() { 0, 3 };
            fd.Bars = new()
            {
                new PlanBarGroup { Mark = "11g1", Kind = PlanBarKind.FootingMat, Size = "#11", Count = 18, Length = Ft(19) + In(6), AlongN = true, HookExtra = Ft(1) + In(11), Order = 1 },
                new PlanBarGroup { Mark = "7f1", Kind = PlanBarKind.FootingMat, Size = "#7", Count = 18, Length = Ft(15) + In(8), Top = true },
                new PlanBarGroup { Mark = "7f3", Kind = PlanBarKind.FootingSideVertical, Size = "#7", Count = 64, Length = Ft(5) + In(1) },
                new PlanBarGroup { Mark = "8f6", Kind = PlanBarKind.UpliftAnchor, Size = "#8", Count = 6, Length = Ft(5) },
            };
            p.Footings = Enumerable.Range(1, 3).Select(k => { var c = fd.Clone(); c.Column = k; return c; }).ToList();
        });
        var u = m.Units.Single(x => x.Type == SupportType.Pier);
        var marks = m.Marks.Where(k => k.Unit == u.Name).ToList();
        var g1 = marks.Single(k => k.Location.Contains("11g1"));
        Assert.Equal(3 * 18, g1.Count);                                                    // three footings
        Assert.Equal(Ft(19) + In(6), g1.Length, 6);                                        // listed length, hooks included
        Assert.Equal("BENT", g1.Shape.Code);
        Assert.Equal(3 * 64, marks.Single(k => k.Location.Contains("7f3")).Count);
        Assert.Equal(3 * 6, marks.Single(k => k.Location.Contains("8f6")).Count);
        Assert.Equal(Ft(5), marks.Single(k => k.Location.Contains("8f6")).Length, 6);
        // Bottom bars sit 1" clear above the pile heads (12" embedment): lowest point of the bar centreline.
        var f = u.Foundations[0];
        var bar = m.Bars.First(b => b.Mark == g1.Mark);
        Assert.Equal(f.BottomZ + Ft(1) + In(1) + g1.Size.Diameter / 2, bar.Points.Min(q => q.Z), 6);
        // Anchors pass through the pile web hole 6" above the footing bottom.
        var anc = m.Bars.First(b => b.Mark == marks.Single(k => k.Location.Contains("8f6")).Mark);
        Assert.Equal(f.BottomZ + In(6), anc.Points.Min(q => q.Z), 3);
    }

    [Fact]
    public void PlanBars_Cap_GroupsAreCountedAndStayInsideTheCap()
    {
        var m = Build(p =>
        {
            p.CapWidth = Ft(6); p.CapDepth = Ft(5);
            p.Reinforcement.CapBars = new()
            {
                new PlanBarGroup { Mark = "11a1", Kind = PlanBarKind.CapTop, Size = "#11", Count = 20, Length = Ft(30) },
                new PlanBarGroup { Mark = "11a2", Kind = PlanBarKind.CapTop, Size = "#11", Count = 20, Length = Ft(28) },
                new PlanBarGroup { Mark = "11b1", Kind = PlanBarKind.CapBottom, Size = "#11", Count = 30, Length = Ft(30) },
                new PlanBarGroup { Mark = "6c1", Kind = PlanBarKind.CapHoop, Size = "#6", Count = 40, Length = Ft(15) },
                new PlanBarGroup { Mark = "5c5", Kind = PlanBarKind.CapHairpin, Size = "#5", Count = 10, Length = Ft(8) },
                new PlanBarGroup { Mark = "4c7", Kind = PlanBarKind.CapTransverse, Size = "#4", Count = 12, Length = Ft(5) },
            };
        });
        var u = m.Units.Single(x => x.Type == SupportType.Pier);
        var marks = m.Marks.Where(k => k.Unit == u.Name && k.Location.Contains(" cap")).ToList();
        int N(string mk) => marks.Where(k => k.Location.Contains(mk)).Sum(k => k.Count);
        Assert.Equal(40, N("a1") + N("a2"));
        Assert.Equal(30, N("11b1"));
        Assert.Equal(40, N("6c1"));
        Assert.Equal(10, N("5c5"));
        Assert.Equal(12, N("4c7"));
        Assert.Equal(Ft(30), marks.First(k => k.Location.Contains("11a1")).Length, 3);
        var hoop = marks.First(k => k.Location.Contains("6c1"));
        Assert.Equal(Ft(15), hoop.Shape.DevelopedLength, 2);                                // height solved from the listed length
        // Every bar point lies inside the cap's plan width.
        foreach (var b in m.Bars.Where(b => b.Unit == u.Name && marks.Any(k => k.Mark == b.Mark)))
            foreach (var q in b.Points)
            {
                double n = Vec2.Dot(q.XY - u.Origin, u.N);
                Assert.InRange(n, u.CapN0 - 1e-6, u.CapN1 + 1e-6);
            }
    }

    [Fact]
    public void SegmentDistance_Works()
    {
        Assert.Equal(1, SubstructureGenerator.SegmentDistance(new Vec3(0, 0, 0), new Vec3(0, 0, 10), new Vec3(1, 0, 0), new Vec3(1, 0, 10)), 9);
        Assert.Equal(0, SubstructureGenerator.SegmentDistance(new Vec3(-1, 0, 0), new Vec3(1, 0, 0), new Vec3(0, -1, 0), new Vec3(0, 1, 0)), 9);
    }
}
