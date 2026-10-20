using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.Solids;
using SBM.Core.Steel;

namespace SBM.Core.Checks.Measure;

/// <summary>Cut planes from the model's own directions: normal to the alignment (incl. profile grade), to a girder (tangent incl. grade) or to a member's axis.</summary>
public static class SectionPlanes
{
    /// <summary>Plane at an alignment station / offset. <paramref name="normalTo"/>: "alignment" (default), "girder:G" (girder tangent at that station; origin on its
    /// top of web when no offset is given), "member:MARK" (the member's axis), or "plumb" (horizontal alignment tangent, no grade).</summary>
    public static SectionPlane AtStation(MeasureModel mm, BridgeModel m, double station, double? offset, string? normalTo, double skewDeg, List<string> notes)
    {
        var a = m.Framing.Alignment;
        var u = mm.Units;
        Vec3 origin = a.Point3At(station, offset ?? 0);
        origin = new Vec3(origin.X, origin.Y, m.Definition.Deck.FinishedElevation(a, station, offset ?? 0));
        Vec3 ahead;
        string desc;
        string nt = normalTo ?? "alignment";
        if (nt.StartsWith("girder:", StringComparison.OrdinalIgnoreCase))
        {
            var g = MeasureModel.Girder(m, nt[7..]) ?? throw new ArgumentException($"No girder '{nt[7..]}' in {m.Definition.Name}.");
            double s = PathAtStation(g.Path, station);
            ahead = MeasureModel.GirderTangent(g, s);
            if (offset == null) origin = g.Path.At(s).point;
            desc = $"at Sta {u.FormatStation(station, 3)} normal to girder {MeasureModel.GirderName(m, g)} (tangent incl. grade {g.Path.GradeAt(s) * 100:0.###}%)";
        }
        else if (nt.StartsWith("member:", StringComparison.OrdinalIgnoreCase))
        {
            var (axis, _, _) = MemberAxis(mm, m, nt[7..], notes);
            ahead = axis;
            desc = $"at Sta {u.FormatStation(station, 3)} normal to member {nt[7..]}";
        }
        else if (nt.Equals("plumb", StringComparison.OrdinalIgnoreCase))
        {
            double ang = a.AngleAt(station);
            ahead = new Vec3(Math.Cos(ang), Math.Sin(ang), 0);
            desc = $"at Sta {u.FormatStation(station, 3)}, plumb plane radial to {a.Name}";
        }
        else
        {
            ahead = MeasureModel.AlignmentTangent(a, station);
            double g = ahead.Z / Math.Sqrt(ahead.X * ahead.X + ahead.Y * ahead.Y);
            desc = $"at Sta {u.FormatStation(station, 3)}{(offset is { } o ? $", offset {u.FormatLength(o)}" : "")} normal to {a.Name} (tangent incl. grade {g * 100:0.###}%)";
        }
        var pl = new SectionPlane(origin, ahead) { Description = desc };
        return Math.Abs(skewDeg) > 1e-12 ? pl.Skewed(skewDeg) : pl;
    }

    /// <summary>Path distance where the girder path reaches a station (linear between samples).</summary>
    public static double PathAtStation(GirderPath p, double station)
    {
        for (int i = 0; i + 1 < p.Count; i++)
        {
            double s0 = p.Station[i], s1 = p.Station[i + 1];
            if ((station - s0) * (station - s1) <= 0 && Math.Abs(s1 - s0) > 1e-12)
                return p.S[i] + (p.S[i + 1] - p.S[i]) * (station - s0) / (s1 - s0);
        }
        return station < p.Station[0] ? p.S[0] - (p.Station[0] - station) : p.S[^1] + (station - p.Station[^1]);
    }

    /// <summary>Axis (unit, pointing from start to end), start point and length of a member: a girder ("G1" / plan name) or girder field section assembly
    /// follows its girder path (returned axis = tangent at mid); a part uses its exact sweep when it is a long extrusion, else the principal axis of its vertices.</summary>
    public static (Vec3 Axis, Vec3 Start, double Length) MemberAxis(MeasureModel mm, BridgeModel m, string mark, List<string> notes)
    {
        if (MeasureModel.Girder(m, mark) is { } g && !(m.Steel?.Parts.Any(p => p.Mark.Equals(mark, StringComparison.OrdinalIgnoreCase)) ?? false))
        {
            var mid = g.Path.Length / 2;
            return (MeasureModel.GirderTangent(g, mid), g.Path.At(0).point, g.Path.Length);
        }
        var part = m.Steel?.Parts.FirstOrDefault(p => p.Mark.Equals(mark, StringComparison.OrdinalIgnoreCase));
        if (part == null) throw new ArgumentException($"No member '{mark}' (a part mark or a girder name) in {m.Definition.Name}.");
        var mesh = part.Mesh;
        if (mesh.Solids is { Count: 1 } ss && ss[0] is var s0)
        {
            double len = s0.Extrusion.Length;
            double prof = s0.Outline.Max(p => p.Length) * 2;
            if (len > 2 * prof)
            {
                var ax = s0.Extrusion * (1 / len);
                var c = s0.Frame.Origin + s0.Frame.X * s0.Outline.Average(p => p.X) + s0.Frame.Y * s0.Outline.Average(p => p.Y);
                return (ax, c, len);
            }
        }
        var pts = mesh.Positions;
        var o = pts[0];
        var cen = Vec3.Zero; foreach (var p in pts) cen += p - o; cen = cen * (1.0 / pts.Count) + o;
        double xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
        foreach (var p in pts) { var d = p - cen; xx += d.X * d.X; xy += d.X * d.Y; xz += d.X * d.Z; yy += d.Y * d.Y; yz += d.Y * d.Z; zz += d.Z * d.Z; }
        var v = new Vec3(1, 0.3, 0.1).Normalized();
        for (int it = 0; it < 100; it++)
        {
            var w = new Vec3(xx * v.X + xy * v.Y + xz * v.Z, xy * v.X + yy * v.Y + yz * v.Z, xz * v.X + yz * v.Y + zz * v.Z);
            if (w.Length < 1e-15) break;
            v = w.Normalized();
        }
        if (Math.Abs(v.X) + Math.Abs(v.Y) > 1e-6 && v.X * 1 + v.Y * 0.001 < 0) v = -v;
        double lo = pts.Min(p => Vec3.Dot(p - cen, v)), hi = pts.Max(p => Vec3.Dot(p - cen, v));
        notes.Add($"member axis of {mark} = principal axis of its vertices (no single long extrusion recipe)");
        return (v, cen + v * lo, hi - lo);
    }

    /// <summary>Plane normal to a member at a distance from its start (or at a station). Girders / field sections use the girder path tangent incl. grade.</summary>
    public static (SectionPlane Plane, Box2? Window) ForMember(MeasureModel mm, string mark, string? bridge, double? distance, double? station, List<string> notes)
    {
        var u = mm.Units;
        foreach (var m in mm.Filter(bridge))
        {
            GirderResult? g = MeasureModel.Girder(m, mark);
            var asm = m.Steel?.Assemblies.FirstOrDefault(a => a.Mark.Equals(mark, StringComparison.OrdinalIgnoreCase));
            bool isPart = m.Steel?.Parts.Any(p => p.Mark.Equals(mark, StringComparison.OrdinalIgnoreCase)) ?? false;
            if (asm is { Kind: AssemblyKind.GirderFieldSection } && asm.Girder >= 0) g = m.Framing.Girders[asm.Girder];
            else if (asm != null && !isPart && g == null)
            {
                // other assemblies: nearest girder to the assembly
                var b = Box3.Empty;
                foreach (var p in m.Steel!.Parts.Where(p => p.Assembly == asm.Mark)) { var bb = p.Mesh.Bounds(); b.Add(bb.Min); b.Add(bb.Max); }
                g = MeasureModel.NearestGirder(m, (b.Min + b.Max) * 0.5).G;
            }
            if (g != null && (!isPart || asm != null))
            {
                double s0 = 0;
                if (asm != null)
                {
                    // field section start: smallest path distance of its parts
                    double lo = double.MaxValue;
                    foreach (var p in m.Steel!.Parts.Where(p => p.Assembly == asm.Mark))
                        for (int i = 0; i < p.Mesh.Positions.Count; i += Math.Max(1, p.Mesh.Positions.Count / 200))
                            lo = Math.Min(lo, MeasureModel.NearestGirderS(g, p.Mesh.Positions[i]));
                    if (lo < double.MaxValue) s0 = lo;
                }
                double s = station is { } st ? PathAtStation(g.Path, st) : s0 + (distance ?? 0);
                var (pt, _, sta) = g.Path.At(s);
                var pl = new SectionPlane(pt, MeasureModel.GirderTangent(g, s))
                {
                    Description = $"normal to girder {MeasureModel.GirderName(m, g)} (tangent incl. grade {g.Path.GradeAt(s) * 100:0.###}%) at Sta {u.FormatStation(sta, 3)}" +
                                  (asm != null && station == null ? $", {Fmt.FtIn(distance ?? 0)} from the start of {asm.Mark}" : "") + "; origin = top of web at the web CL",
                };
                return (pl, null);
            }
            if (isPart)
            {
                var (axis, start, len) = MemberAxis(mm, m, mark, notes);
                double d = distance ?? len / 2;
                if (station is { } st2)
                {
                    var a = m.Framing.Alignment;
                    double lo = 0, hi = len;
                    double F(double t) => a.Inverse((start + axis * t).XY).Station - st2;
                    if (F(lo) * F(hi) > 0) throw new ArgumentException($"Station {u.FormatStation(st2)} is not within member {mark}.");
                    for (int it = 0; it < 60; it++) { double md = (lo + hi) / 2; if (F(lo) * F(md) <= 0) hi = md; else lo = md; }
                    d = (lo + hi) / 2;
                }
                var origin = start + axis * d;
                var pl = new SectionPlane(origin, axis) { Description = $"normal to the axis of {mark} at {Fmt.FtIn(d)} from its start (length {Fmt.FtIn(len)})" };
                return (pl, null);
            }
        }
        throw new ArgumentException($"No member '{mark}' (part, girder field section, assembly or girder name).");
    }
}
