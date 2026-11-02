p = 'src/SBM.Core/Bridge/TrussDefinition.cs'; s = open(p, encoding='utf-8').read()
old = '''    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<TrussGussetEdgeAngle>? EdgeAngles { get; set; }
}'''
assert s.count(old) == 1
s = s.replace(old, old[:-1] + '''    /// <summary>OPTIONAL (0 = one plate per face, omitted from the .sbm): the inner gusset is TWO plates per face, split along the joint's v axis
    /// through the point (u = 0, e.g. the CL of a closure joint where each half truss brings its own gusset) with this gap between them (meters).
    /// Marks get "A" (lower station side) / "B"; layers, rivets and edge angles still use the whole outline (a splice layer bridges the gap).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double GussetSplitGap { get; set; }
}''')
open(p, 'w', encoding='utf-8').write(s)
p = 'src/SBM.Core/Truss/TrussGenerator.cs'; s = open(p, encoding='utf-8').read()
old = '''            double area = Math.Abs(MeshBuilder.SignedArea(outline));
            foreach (int side in new[] { -1, 1 })
            {
                var mesh = new Mesh();
                var fr = new Frame3(P + n * (side * X), u, v, n);
                if (side > 0) MeshBuilder.AddExtrusion(mesh, outline, fr, 0, tg); else MeshBuilder.AddExtrusion(mesh, outline, fr, -tg, 0);
                string mark = $"{pl}-G{pt}-{(side < 0 ? "L" : "R")}";
                Model.Steel.Parts.Add(new SteelPart
                {
                    Mark = mark, Category = PartCategory.TrussGusset, Assembly = asmMark, Description = $"gusset PL {TrussSections.FormatIn(tg)} at {pt} ({area / 0.09290304:0.0} SF)",
                    Mesh = mesh, Material = Material(chords.FirstOrDefault()?.Sec.Definition.Material ?? ""), Length = outline.Max(q => q.X) - outline.Min(q => q.X), Assumption = asu,
                    Member = _d.PartPerPiece ? JointStack(pl, pt) : null, Piece = _d.PartPerPiece ? "gusset" : null,
                });
                Model.PanelOf[mark] = $"Joint {pt}";
                Model.Gussets++;
            }'''
assert s.count(old) == 1
new = '''            double area = Math.Abs(MeshBuilder.SignedArea(outline));
            // split gusset (GussetSplitGap): two plates per face, cut along v through the point, gap between them
            var pieces = new List<(List<Vec2> poly, string suffix)>();
            if (jdef is { GussetSplitGap: > 0 } sj)
            {
                foreach (var (sg, sfx) in new[] { (-1, "A"), (1, "B") })
                {
                    var half = ClipHalf(outline, new Vec2(sg, 0), sj.GussetSplitGap / 2);
                    if (half.Count >= 3 && Math.Abs(MeshBuilder.SignedArea(half)) > 1e-6) pieces.Add((half, sfx));
                }
                Info($"Truss: joint {pt} ({pl}): inner gusset split at the joint's v axis, {L(sj.GussetSplitGap)} gap ({pieces.Count} plates per face){Src}.");
            }
            else pieces.Add((outline, ""));
            foreach (int side in new[] { -1, 1 })
                foreach (var (poly, sfx) in pieces)
                {
                    var mesh = new Mesh();
                    var fr = new Frame3(P + n * (side * X), u, v, n);
                    if (side > 0) MeshBuilder.AddExtrusion(mesh, poly, fr, 0, tg); else MeshBuilder.AddExtrusion(mesh, poly, fr, -tg, 0);
                    string mark = $"{pl}-G{pt}-{(side < 0 ? "L" : "R")}{sfx}";
                    double ap = Math.Abs(MeshBuilder.SignedArea(poly));
                    Model.Steel.Parts.Add(new SteelPart
                    {
                        Mark = mark, Category = PartCategory.TrussGusset, Assembly = asmMark,
                        Description = $"gusset PL {TrussSections.FormatIn(tg)} at {pt}{(sfx.Length > 0 ? $", {(sfx == "A" ? "lower" : "higher")} station half" : "")} ({ap / 0.09290304:0.0} SF)",
                        Mesh = mesh, Material = Material(chords.FirstOrDefault()?.Sec.Definition.Material ?? ""), Length = poly.Max(q => q.X) - poly.Min(q => q.X), Assumption = asu,
                        Member = _d.PartPerPiece ? JointStack(pl, pt) : null, Piece = _d.PartPerPiece ? "gusset" : null,
                    });
                    Model.PanelOf[mark] = $"Joint {pt}";
                    Model.Gussets++;
                }'''
s = s.replace(old, new)
open(p, 'w', encoding='utf-8').write(s)
print('ok')
