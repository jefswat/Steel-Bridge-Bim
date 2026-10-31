// 1d size estimate: pieces per truss member (read-only, no save)
using SBM.Core.Truss;
var sw = System.Diagnostics.Stopwatch.StartNew();
Doc.Regenerate();
int totM = 0, totP = 0, totParts = 0; long tris = 0;
foreach (var br in Doc.Project.Bridges)
{
    if (br.Truss == null) continue;
    var m = Doc.Model(br.Name); var tm = m.Steel?.Truss; if (tm == null) continue;
    var secs = br.Truss.Sections.ToDictionary(s => s.Name, s => TrussSections.Build(s));
    int nm = 0, np = 0;
    foreach (var mr in tm.Members) { if (!secs.TryGetValue(mr.Section, out var bs)) continue; nm++; np += bs.Pieces.Count + bs.Lacing.Count; }
    var memberMarks = tm.Members.Select(x => x.Mark).ToHashSet();
    tris += m.Steel!.Parts.Where(p => memberMarks.Contains(p.Mark)).Sum(p => (long)p.Mesh.TriangleCount);
    Print($"{br.Name}: {nm} members -> {np} pieces; parts now {m.Steel!.Parts.Count}, gussets {tm.Gussets}");
    totM += nm; totP += np; totParts += m.Steel!.Parts.Count;
}
Print($"TOTAL {totM} members -> {totP} pieces (+{totP - totM} parts), steel parts now {totParts}, member triangles {tris:N0}; {sw.Elapsed.TotalSeconds:0}s");
