using SBM.Core.Geometry;
using SBM.Core.Units;
using SBM.Core.Utility;

namespace SBM.Core.Drawing.Views;

/// <summary>One profile run of a network: structures in flow order and the pipes between them.</summary>
public sealed record UtilityRun(string Name, List<UtilityStructure> Structures, List<UtilityPipe> Pipes);

/// <summary>
/// Pipe profile views of utility networks (2D, Z = 0): x = distance along the run (structure centres), y = elevation x the vertical exaggeration.
/// A run starts at a structure with no pipe coming in (or, in a loop, the first structure) and follows the outlet pipes; every pipe is drawn in exactly
/// one run (branches start their own run at the junction). Drawn: finished ground (sampled every <= 1 m), structures (outside from the floor bottom to the
/// rim), pipes (inside bottom / crown and the outside crown), labels (rim, sump, inverts in / out, size, slope, length), elevation grid.
/// </summary>
public static class UtilityProfileViews
{
    public static readonly LayerStyle Structure = new("P-UTIL-STRUCTURE", 230, 230, 230, 7, LineStyle.Continuous, 1.5);
    public static readonly LayerStyle Pipe = new("P-UTIL-PIPE", 40, 170, 60, 3, LineStyle.Continuous, 1.5);
    public static readonly LayerStyle PipeOutside = new("P-UTIL-PIPE-OUTSIDE", 40, 170, 60, 3, LineStyle.Hidden);

    /// <summary>The runs of a network (flow order; each pipe once).</summary>
    public static List<UtilityRun> Runs(UtilityModel m)
    {
        var pipes = m.Pipes.Where(p => p.IsBuilt).ToList();
        var used = new HashSet<UtilityPipe>();
        var runs = new List<UtilityRun>();
        var starts = m.Structures.Where(s => !pipes.Any(p => p.ToStructure == s) && pipes.Any(p => p.FromStructure == s)).ToList();
        // free-start pipes first, then runs from the head structures, then branches / loops left over
        void Follow(UtilityStructure? s, UtilityPipe? first)
        {
            var st = new List<UtilityStructure>(); var pl = new List<UtilityPipe>();
            if (s != null) st.Add(s);
            var p = first ?? pipes.FirstOrDefault(x => x.FromStructure == s && !used.Contains(x));
            while (p != null && used.Add(p))
            {
                pl.Add(p);
                if (p.ToStructure == null) break;
                st.Add(p.ToStructure);
                var next = p.ToStructure;
                p = pipes.FirstOrDefault(x => x.FromStructure == next && !used.Contains(x));
            }
            if (pl.Count > 0) runs.Add(new UtilityRun($"{m.Definition.Name} run {runs.Count + 1}: {(st.Count > 0 ? st[0].Definition.Name : pl[0].Definition.Name)} to {(pl[^1].ToStructure?.Definition.Name ?? "free end")}", st, pl));
        }
        foreach (var p in pipes.Where(p => p.FromStructure == null)) Follow(null, p);
        foreach (var s in starts) while (pipes.Any(x => x.FromStructure == s && !used.Contains(x))) Follow(s, null);
        foreach (var p in pipes.Where(p => !used.Contains(p)).ToList()) if (!used.Contains(p)) Follow(p.FromStructure, p);
        return runs;
    }

    /// <summary>Profile drawing of one run. <paramref name="exaggeration"/> = vertical exaggeration (display only; 1 = true scale).</summary>
    public static PlanDrawing Profile(UtilityRun run, Func<Vec2, (double? z, string source)>? ground, UnitSystem u, double exaggeration = 10, double scale = 100)
    {
        var dwg = new PlanDrawing();
        var d = new Drafter(dwg, u, scale);
        double ve = exaggeration > 0 ? exaggeration : 1;
        // stations: structure centres along the run (pipes' plan direction), free ends at the pipe ends
        var xs = new List<(double x, Vec2 plan)>();
        double x = 0;
        Vec2 prev = run.Pipes[0].FromStructure?.Plan ?? run.Pipes[0].Start.XY;
        xs.Add((0, prev));
        double zMin = double.MaxValue, zMax = double.MinValue;
        void Z(double z) { if (double.IsNaN(z)) return; zMin = Math.Min(zMin, z); zMax = Math.Max(zMax, z); }
        var pipeX = new List<(UtilityPipe p, double x0, double x1)>();
        foreach (var p in run.Pipes)
        {
            var a = p.FromStructure?.Plan ?? p.Start.XY;
            var b = p.ToStructure?.Plan ?? p.End.XY;
            double x0 = x + Vec2.Distance(a, p.Start.XY), x1 = x0 + p.Length;
            pipeX.Add((p, x0, x1));
            x += Vec2.Distance(a, b);
            xs.Add((x, b));
            Z(p.Start.Z); Z(p.End.Z); Z(p.Start.Z + p.OutsideRise); Z(p.End.Z + p.OutsideRise);
        }
        foreach (var s in run.Structures) { Z(s.Rim); Z(s.Sump - (double.IsNaN(s.Definition.FloorThickness) ? 0 : s.Definition.FloorThickness)); }
        Vec2 P(double xx, double z) => new(xx, (z - zMin) * ve);
        // finished ground along the run
        var gl = new List<Vec2>();
        if (ground != null)
            for (int i = 0; i + 1 < xs.Count; i++)
            {
                var (xa, pa) = xs[i]; var (xb, pb) = xs[i + 1];
                int n = Math.Max(1, (int)Math.Ceiling((xb - xa) / 1.0));
                for (int k = (i == 0 ? 0 : 1); k <= n; k++)
                {
                    double t = (double)k / n;
                    if (ground(pa + (pb - pa) * t).z is double g) { gl.Add(new Vec2(xa + (xb - xa) * t, g)); Z(g); }
                }
            }
        double top = zMax + 1;
        zMin = Math.Floor(zMin) - 1;
        if (gl.Count > 1) d.Poly(SheetLayers.Ground, gl.Select(q => P(q.X, q.Y)));
        // elevation grid every 1 m (2 ft US)
        double step = u.IsUS ? 0.6096 * (ve >= 5 ? 1 : 5) : (ve >= 5 ? 1 : 5);
        for (double z = Math.Ceiling(zMin / step) * step; z <= top + 1e-9; z += step)
        {
            d.Line(SheetLayers.Grid, P(-2, z), P(x + 2, z));
            d.Text(u.FormatLength(z, LengthKind.Coordinate), P(-2.5, z), d.SmallTextH, 0, TextAlign.Right, SheetLayers.Grid);
        }
        // structures
        foreach (var (s, k) in run.Structures.Select((s, k) => (s, k)))
        {
            double xc = xs[Math.Min(k + (run.Pipes[0].FromStructure == null ? 1 : 0), xs.Count - 1)].x;
            var sd = s.Definition;
            double half = sd.Shape == UtilityStructureShape.Round ? sd.InnerDiameter / 2 : sd.InnerLength / 2;
            double wt = double.IsNaN(sd.WallThickness) ? 0 : sd.WallThickness, ft = double.IsNaN(sd.FloorThickness) ? 0 : sd.FloorThickness;
            if (!double.IsNaN(half) && s.IsBuilt)
            {
                d.Poly(Structure, new[] { P(xc - half - wt, s.Sump - ft), P(xc + half + wt, s.Sump - ft), P(xc + half + wt, s.Rim), P(xc - half - wt, s.Rim) }, true);
                if (!sd.IsEnvelope) d.Poly(Structure, new[] { P(xc - half, s.Rim), P(xc - half, s.Sump), P(xc + half, s.Sump), P(xc + half, s.Rim) });
            }
            var ins = run.Pipes.Where(p => p.ToStructure == s).Select(p => p.End.Z).ToList();
            var outs = run.Pipes.Where(p => p.FromStructure == s).Select(p => p.Start.Z).ToList();
            string lbl = $"{sd.Name} ({sd.Kind})\nRIM {u.FormatLength(s.Rim, LengthKind.Coordinate)}"
                + string.Concat(ins.Select(z => $"\nINV IN {u.FormatLength(z, LengthKind.Coordinate)}"))
                + string.Concat(outs.Select(z => $"\nINV OUT {u.FormatLength(z, LengthKind.Coordinate)}"))
                + (double.IsNaN(s.Sump) ? "" : $"\nSUMP {u.FormatLength(s.Sump, LengthKind.Coordinate)}");
            d.Line(SheetLayers.Dims, P(xc, s.Rim), P(xc, top + 0.3));
            d.Text(lbl, P(xc, top + 0.5) + new Vec2(0, d.TextH * (lbl.Count(c => c == '\n') + 1) * 1.6), d.TextH, Math.PI / 2, TextAlign.Left, SheetLayers.Text);
        }
        // pipes
        foreach (var (p, x0, x1) in pipeX)
        {
            var pd = p.Definition;
            double rise = pd.Shape == UtilityPipeShape.Circular ? pd.InnerDiameter : pd.InnerRise;
            d.Line(Pipe, P(x0, p.Start.Z), P(x1, p.End.Z));
            d.Line(Pipe, P(x0, p.Start.Z + rise), P(x1, p.End.Z + rise));
            d.Line(PipeOutside, P(x0, p.Start.Z + p.OutsideRise - pd.WallThickness), P(x1, p.End.Z + p.OutsideRise - pd.WallThickness));
            d.Line(PipeOutside, P(x0, p.Start.Z - pd.WallThickness), P(x1, p.End.Z - pd.WallThickness));
            string size = pd.Shape == UtilityPipeShape.Circular ? u.FormatLength(pd.InnerDiameter) + " DIA" : $"{u.FormatLength(pd.InnerRise)} x {u.FormatLength(pd.InnerSpan)} {pd.Shape.ToString().ToUpperInvariant()}";
            string txt = $"{pd.Name}: {size} {pd.Material}\n{u.FormatLength(p.Length)} @ {p.Slope * 100:0.00}%" + (p.MinCover is double c ? $"\nMIN COVER {u.FormatLength(c)}" : "");
            var mid = P((x0 + x1) / 2, (p.Start.Z + p.End.Z) / 2 - pd.WallThickness);
            d.Text(txt, mid - new Vec2(0, d.TextH * 1.5), d.TextH, 0, TextAlign.Center, SheetLayers.Text);
        }
        d.Title(run.Name.ToUpperInvariant() + " - PROFILE", $"VERTICAL EXAGGERATION {ve:0.#}:1", new Vec2(x / 2, -d.TitleH * 4));
        return dwg;
    }
}
