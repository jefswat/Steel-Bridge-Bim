// Agent review of the NHT approach curbs: read-only measurement (no Doc.Save).
using SBM.Core;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Solids;

double inch = 0.0254;
string F(double m) => (m / inch).ToString("0.000");
Doc.Regenerate();
var units = new[] { "NHT Unit 1", "NHT Unit 2", "NHT Unit 3", "NHT Unit 4", "NHT Unit 5" };
double totVol = 0;
var curbEnds = new List<(string unit, string side, double sBeg, double sEnd)>();
foreach (var un in units)
{
    var br = Doc.Project.Bridges.First(b => b.Name == un);
    var m = Doc.Model(un);
    var al = Doc.Project.FindAlignment(br.AlignmentName)!;
    var deck = br.Deck;
    var dr = m.Framing.Deck!;
    var c = m.Concrete!;
    var bar = deck.Barrier;
    Print($"==== {un}: alignment {br.AlignmentName}; deck edges L {deck.LeftEdgeOffset / 0.3048:0.0000} ft R {deck.RightEdgeOffset / 0.3048:0.0000} ft, t {F(deck.Thickness)} in; barrier type {bar.Type} IsCurb {bar.IsCurb} L {bar.Left} R {bar.Right} perp {bar.PerpendicularToDeck} reinf {bar.Reinforcement}");
    Print($"   profile: " + string.Join(" | ", bar.Profile!.Select(p => $"R {F(p.R)} H {F(p.H)} rad {F(p.Radius)} ch {F(p.Chamfer)} tag '{p.Tag}'")));
    Print($"   supports: " + string.Join(", ", br.Supports.Select(s => $"{s.Name} sta {s.Station / 0.3048:0.000} skew {s.SkewDegrees:0.000} deckEndOff {F(s.DeckEndOffset)}\"")));
    foreach (var (name, mesh) in c.Barriers)
    {
        double vol = Math.Abs(mesh.Volume()); totVol += vol;
        int side = name.StartsWith("Left") ? -1 : 1;
        double edge = side < 0 ? deck.LeftEdgeOffset : deck.RightEdgeOffset;
        // classify vertices: offset inward from the edge, height above the deck finished elevation at the edge / at the point
        var rows = new List<(double sta, double off, double rin, double hEdge, double hHere)>();
        foreach (var p in mesh.Positions)
        {
            var iv = al.Inverse(new Vec2(p.X, p.Y));
            double rin = -side * (iv.Offset - edge);
            double zEdge = deck.FinishedElevation(al, iv.Station, edge);
            double zHere = deck.FinishedElevation(al, iv.Station, iv.Offset);
            rows.Add((iv.Station, iv.Offset, rin, p.Z - zEdge, p.Z - zHere));
        }
        var sect = rows.Select(r => (R: Math.Round(r.rin / inch * 16) / 16, H: Math.Round(r.hEdge / inch * 100) / 100, Hh: Math.Round(r.hHere / inch * 100) / 100))
            .GroupBy(x => (x.R, x.H)).Select(g => $"({g.Key.R:0.###},{g.Key.H:0.##}/{g.First().Hh:0.##})x{g.Count()}").Take(40);
        double s0 = rows.Min(r => r.sta), s1 = rows.Max(r => r.sta);
        // ends per section point: station at min / max
        var toe = rows.Where(r => Math.Abs(r.rin / inch - 10) < 0.05 && Math.Abs(r.hHere) < 0.002).ToList();
        var outer = rows.Where(r => Math.Abs(r.rin) < 0.002).ToList();
        Print($"   {name}: vol {vol:0.0000} m3 = {vol * 1.30795:0.000} CY; tris {mesh.TriangleCount}; sta {s0 / 0.3048:0.000}..{s1 / 0.3048:0.000} ft; pieces {(c.BarrierPieces.TryGetValue(name, out var pc) ? pc.Count : 0)}");
        Print($"      section clusters (R in from edge, H above deck@edge / above deck@point): {string.Join(" ", sect)}");
        double bT0 = toe.Min(r => r.sta), bT1 = toe.Max(r => r.sta), bO0 = outer.Min(r => r.sta), bO1 = outer.Max(r => r.sta);
        Print($"      toe sta {bT0 / 0.3048:0.0000}..{bT1 / 0.3048:0.0000}; outer face sta {bO0 / 0.3048:0.0000}..{bO1 / 0.3048:0.0000}");
        // distance of the curb ends from the deck end lines
        var pT0 = al.PointAt(bT0, edge - side * 10 * inch); var pT1 = al.PointAt(bT1, edge - side * 10 * inch);
        var pO0 = al.PointAt(bO0, edge); var pO1 = al.PointAt(bO1, edge);
        Print($"      FromBegin toe {F(dr.FromBegin(pT0))}\" outer {F(dr.FromBegin(pO0))}\"; FromEnd toe {F(dr.FromEnd(pT1))}\" outer {F(dr.FromEnd(pO1))}\"");
        curbEnds.Add((un, side < 0 ? "L" : "R", bO0, bO1));
        // curb top height above the trail along the curb line at sample stations (max z of vertices near the inner chamfer top)
        var topIn = rows.Where(r => Math.Abs(r.rin / inch - 9.25) < 0.05).ToList();
        if (topIn.Count > 0) Print($"      inner top chamfer point (R 9.25): H above deck@point min {topIn.Min(r => r.hHere) / inch:0.000} max {topIn.Max(r => r.hHere) / inch:0.000}");
        var topOut = rows.Where(r => Math.Abs(r.rin / inch - 0.75) < 0.05).ToList();
        if (topOut.Count > 0) Print($"      outer top chamfer point (R 0.75): H above deck@point min {topOut.Min(r => r.hHere) / inch:0.000} max {topOut.Max(r => r.hHere) / inch:0.000}");
        // cross slope at the curb: deck rise from edge to curb line
        var slopes = Enumerable.Range(0, 11).Select(k => s0 + (s1 - s0) * k / 10).Select(s => (deck.FinishedElevation(al, s, edge - side * 10 * inch) - deck.FinishedElevation(al, s, edge)) / (10 * inch)).ToList();
        Print($"      deck slope rising inward under the curb: {string.Join(" ", slopes.Select(x => (x * 100).ToString("0.000")))} %");
    }
    // deck end lines
    Print($"   deck begin line pt sta {al.Inverse(dr.BeginLinePoint).Station / 0.3048:0.000}, end {al.Inverse(dr.EndLinePoint).Station / 0.3048:0.000}; joints: {string.Join("; ", c.Joints.Select(j => $"{j.Support} begin={j.AtBegin} blockoutW {F(j.Definition.BlockoutWidth)} body {(j.Body != null)} hw {j.Hardware.Count}"))}");
    // curb bars
    foreach (var mk in new[] { "C", "CS", "D", "T" })
    {
        var bs = c.Bars.Where(b => b.Mark == mk).ToList();
        var mark = c.Marks.FirstOrDefault(x => x.Mark == mk);
        Print($"   bars {mk}: {bs.Count} placements; mark count {mark?.Count} len {(mark != null ? mark.Length / 0.3048 : 0):0.00} ft mass {(mark != null ? mark.TotalMass * 2.20462 : 0):0.0} lb");
    }
    var cv = c.CoverViolations.Where(v => v.Mark == "C" || v.Mark == "CS" || v.Mark == "D" || v.Mark == "T").ToList();
    Print($"   cover violations C/CS/D/T: {cv.Count}; all marks: {c.CoverViolations.Count} " + string.Join(", ", c.CoverViolations.GroupBy(v => v.Mark).Select(g => $"{g.Key}:{g.Count()} max {g.Max(v => v.Excess) / inch:0.000}\" {g.First().Face}")));
    // C bar clear to curb top / to curb faces at sample points
    foreach (var mk in new[] { "C", "CS" })
    {
        foreach (var bp in c.Bars.Where(b => b.Mark == mk).Take(mk == "C" ? 8 : 4))
        {
            var p = bp.Points[bp.Points.Length / 2];
            var iv = al.Inverse(new Vec2(p.X, p.Y));
            int side = iv.Offset < 0 ? -1 : 1; double edge = side < 0 ? deck.LeftEdgeOffset : deck.RightEdgeOffset;
            double rin = -side * (iv.Offset - edge);
            double top = deck.FinishedElevation(al, iv.Station, iv.Offset) + 4 * inch;
            double db = 0.5 * inch;
            Print($"      {mk} mid pt: R in {rin / inch:0.000}\" z below curb top (deck@pt+4\") {(top - p.Z) / inch:0.000}\" clear {(top - p.Z - db / 2) / inch:0.000}\" db {db / inch:0.000}");
        }
    }
    // CS: max z, min/max R in at a bar
    var cs = c.Bars.Where(b => b.Mark == "CS").ToList();
    if (cs.Count > 0)
    {
        var b0 = cs[cs.Count / 3];
        var iv0 = al.Inverse(new Vec2(b0.Points[0].X, b0.Points[0].Y));
        int side = iv0.Offset < 0 ? -1 : 1; double edge = side < 0 ? deck.LeftEdgeOffset : deck.RightEdgeOffset;
        var rs = b0.Points.Select(p => { var iv = al.Inverse(new Vec2(p.X, p.Y)); return (r: -side * (iv.Offset - edge), z: p.Z - deck.FinishedElevation(al, iv.Station, iv.Offset)); }).ToList();
        Print($"      CS sample: R in {rs.Min(x => x.r) / inch:0.000}..{rs.Max(x => x.r) / inch:0.000}\", z rel deck@pt {rs.Min(x => x.z) / inch:0.000}..{rs.Max(x => x.z) / inch:0.000}\" (db 0.500); spacing first two {(cs.Count > 1 ? (new Vec2(cs[0].Points[1].X, cs[0].Points[1].Y) - new Vec2(cs[1].Points[1].X, cs[1].Points[1].Y)).Length / inch : 0):0.000}\"");
    }
    // railings
    if (m.Steel?.Railings is { } rm)
        foreach (var run in rm.Runs)
        {
            var dz = run.Line.Select(p => { var iv = al.Inverse(new Vec2(p.X, p.Y)); return (p.Z - deck.FinishedElevation(al, iv.Station, iv.Offset)) / inch; }).ToList();
            Print($"   rail {run.Name}: off {run.Offset / 0.3048:0.000} ft sta {run.BeginStation / 0.3048:0.000}..{run.EndStation / 0.3048:0.000} L {run.Length / 0.3048:0.000} ft; base above deck@ref line min {dz.Min():0.000} max {dz.Max():0.000} in");
        }
    foreach (var d in c.Diagnostics.Where(d => d.Message.Contains("curb", StringComparison.OrdinalIgnoreCase) || d.Message.Contains("barrier", StringComparison.OrdinalIgnoreCase)).Take(10)) Print($"   DIAG {d.Severity}: {d.Message}");
}
Print($"TOTAL curb volume {totVol:0.0000} m3 = {totVol * 1.30795:0.000} CY");
Print("curb ends by unit (outer face): " + string.Join("; ", curbEnds.Select(e => $"{e.unit} {e.side} {e.sBeg / 0.3048:0.000}..{e.sEnd / 0.3048:0.000}")));
