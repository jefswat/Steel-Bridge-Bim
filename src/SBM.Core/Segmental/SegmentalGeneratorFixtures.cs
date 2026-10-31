using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Misc;
using SBM.Core.Solids;
using SBM.Core.Steel;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Segmental;

// OPTIONAL fixtures attached to the segmental box (SegmentalMiscDefinition.Fixtures, Bridge/SegmentalMisc.cs): platforms, luminaires, outlet / junction
// boxes, panels, screens ... repeated at -L- stations, each built from pieces (boxes or straight members) in a LEVEL local frame (A along the horizontal
// tangent, N across, Z up) anchored at the deck edge or at the inside face of a web. One MiscAssembly per station; items by others are placeholders.
public sealed partial class SegmentalGenerator
{
    private readonly record struct FixtureFrame(Vec3 Origin, Vec3 A, Vec3 N, Vec3 Z)
    {
        public Vec3 At(double a, double n, double z) => Origin + A * a + N * n + Z * z;
    }

    private void BuildFixtures(SegmentalFixtureDefinition fd, MiscModel misc)
    {
        string src = fd.Source.Length > 0 ? fd.Source : Pages;
        if (fd.Unresolved.Length > 0) Warn(fd.Unresolved);
        foreach (var u in fd.Pieces.Select(p => p.Unresolved).Where(u => u.Length > 0).Distinct()) Warn(u);
        var b = _d.Box;
        if (fd.Side == 0 && fd.Reference is SegmentalFixtureReference.DeckEdge or SegmentalFixtureReference.WebInside)
        {
            Warn(UnresolvedFlag.Text(src, $"{fd.Name} side", "the side of the box is not given", "Not built."));
            return;
        }
        int side = fd.Side >= 0 ? 1 : -1;
        int built = 0, skipped = 0;
        var notFound = new HashSet<string>();
        for (int k = 0; k < fd.Stations.Count; k++)
        {
            double sta = fd.Stations[k];
            if (_segs.Count == 0 || sta < _segs.Min(x => x.r.Station0) - 1e-6 || sta > _segs.Max(x => x.r.Station1) + 1e-6) { skipped++; continue; }
            string asm = k < fd.Labels.Count && fd.Labels[k].Length > 0 ? fd.Labels[k] : $"{fd.Name} {k + 1}";
            var fr = FrameOnChord(sta);
            var (h, t, hollow) = SectionAt(sta);
            var th = new Vec3(fr.Tangent.X, fr.Tangent.Y, 0).Normalized();
            var lat = new Vec3(fr.Lat.X, fr.Lat.Y, 0).Normalized();          // horizontal, toward the right of -L-
            var up = new Vec3(0, 0, 1);
            FixtureFrame frame;
            Func<double, double> nShift = _ => 0;                            // N of the reference face at a piece height (web: inclined)
            if (fd.Reference == SegmentalFixtureReference.DeckEdge)
                frame = new FixtureFrame(fr.At(new Vec2(side * b.TopWidth / 2, 0)), th, lat * side, up);
            else if (fd.Reference == SegmentalFixtureReference.DeckSection)
                frame = new FixtureFrame(fr.At(new Vec2(0, 0)), fr.Tangent.Normalized(), fr.Lat.Normalized(), fr.Up.Normalized());
            else if (fd.Reference == SegmentalFixtureReference.Floor)
                frame = new FixtureFrame(fr.At(new Vec2(0, h - t)), th, lat * side, up);
            else
            {
                if (!hollow) { skipped++; continue; }
                double yFloor = h - t;
                double XFace(double y) => b.WebRootX - b.WebHorizontal - (y - b.WebTopDepth) / b.WebSlope;   // inner web face (section x, right half)
                var floorAtFace = fr.At(new Vec2(side * XFace(yFloor), yFloor));
                frame = new FixtureFrame(floorAtFace, th, -lat * side, up);
                // face position at height z above the floor, as a horizontal distance from the face at the floor (into the cell = +N)
                nShift = z => XFace(yFloor) - XFace(yFloor - z);
            }
            misc.Assemblies.Add(new MiscAssembly(fd.Group, asm, $"{fd.Name} at {sta / Ft(1):0.00} ({(side > 0 ? "right" : "left")} side) ({src})", fd.Category.Length > 0 ? fd.Category : fd.Name));
            foreach (var pc in fd.Pieces)
            {
                Mesh mesh;
                double zm = (pc.Z0 + pc.Z1) / 2, dn = nShift(zm);
                if (pc.Section.Length > 0)
                {
                    var sec = FixtureSection(pc.Section);
                    if (sec == null) { notFound.Add(pc.Section); continue; }
                    var p0 = frame.At(pc.A0, pc.N0 + nShift(pc.Z0), pc.Z0);
                    var p1 = frame.At(pc.A1, pc.N1 + nShift(pc.Z1), pc.Z1);
                    var ax = p1 - p0;
                    if (ax.Length < 1e-6) continue;
                    var d = ax.Normalized();
                    var sUp = Math.Abs(Vec3.Dot(d, up)) < 0.9 ? up : frame.A;
                    var v = Vec3.Cross(sUp, d).Normalized();                // section v (across), m = up
                    var m = Vec3.Cross(d, v).Normalized();
                    double rr = pc.Roll * Math.PI / 180;
                    var mr = m * Math.Cos(rr) + v * Math.Sin(rr);
                    var vr = v * Math.Cos(rr) - m * Math.Sin(rr);
                    mesh = new Mesh();
                    MeshBuilder.AddLoft(mesh, sec, new[] { sec.Select(q => p0 + mr * q.X + vr * q.Y).ToArray(), sec.Select(q => p1 + mr * q.X + vr * q.Y).ToArray() });
                }
                else
                {
                    var rect = new[] { new Vec2(pc.N0 + dn, pc.Z0), new Vec2(pc.N1 + dn, pc.Z0), new Vec2(pc.N1 + dn, pc.Z1), new Vec2(pc.N0 + dn, pc.Z1) };
                    if (MeshBuilder.SignedArea(rect) < 0) Array.Reverse(rect);
                    mesh = new Mesh();
                    MeshBuilder.AddLoft(mesh, rect, new[] { Math.Min(pc.A0, pc.A1), Math.Max(pc.A0, pc.A1) }
                        .Select(a => rect.Select(q => frame.At(a, q.X, q.Y)).ToArray()).ToList());
                }
                string why = string.Join(" ", new[] { fd.Unresolved, pc.Unresolved }.Where(x => x.Length > 0));
                var flag = why.Length > 0 ? Assumption.UnresolvedDimension(why, pc.Placeholder ? "Envelope of an item by others." : "Built as listed.") : null;
                string name = pc.Name.Length > 0 ? $"{asm} {pc.Name}" : asm;
                misc.Elements.Add(new MiscElement(fd.Group, pc.Kind, asm, name, (pc.Description.Length > 0 ? pc.Description : pc.Section) + $" ({src})", mesh, pc.Material,
                    Placeholder: pc.Placeholder, PlaceholderBasis: pc.PlaceholderBasis, Location: asm, Assumption: flag));
            }
            built++;
        }
        if (notFound.Count > 0) Warn($"Segmental {fd.Name}: section(s) {string.Join(", ", notFound)} not found - those pieces not built.");
        if (skipped > 0) Warn($"Segmental {fd.Name}: {skipped} station(s) outside the unit or in a solid section - not built.");
        Info($"Segmental {fd.Name}: {built} built ({fd.Pieces.Count} pieces each, {(side > 0 ? "right" : "left")} side).");
        _m.Checks.Add(new SegmentalCheck($"{fd.Name}: count", built, fd.Stations.Count, 0, $"{fd.Reference} on the {(side > 0 ? "right" : "left")}"));
    }

    /// <summary>Section polygon (m = up, v = across) of a fixture member: AISC W / L, "PL t x w", "ROD d" (inches).</summary>
    private static IReadOnlyList<Vec2>? FixtureSection(string name)
    {
        string n = name.Trim().ToUpperInvariant();
        if (n.StartsWith("ROD"))
        {
            if (!TryInches(n[3..], out double d)) return null;
            const int k = 32;                                               // equal-area 32-gon (volume = the round bar)
            double r = d / 2 * Math.Sqrt(2 * Math.PI / (k * Math.Sin(2 * Math.PI / k)));
            return Enumerable.Range(0, k).Select(i => new Vec2(r * Math.Cos(2 * Math.PI * i / k), r * Math.Sin(2 * Math.PI * i / k))).ToList();
        }
        if (n.StartsWith("PL"))
        {
            var parts = n[2..].Split('X', StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !TryInches(parts[0], out double t) || !TryInches(parts[1], out double w)) return null;
            return new[] { new Vec2(-w / 2, -t / 2), new Vec2(w / 2, -t / 2), new Vec2(w / 2, t / 2), new Vec2(-w / 2, t / 2) };
        }
        if (n.StartsWith("W") && SteelCatalog.FindWideFlange(name) is { } wf)
            return SteelCatalog.WideFlangePolygon(wf, -wf.WebThickness / 2);
        if (n.StartsWith("L") && SteelCatalog.FindAngle(name) is { } an)
            return SteelCatalog.AnglePolygon(an, 0, 1, 0, 1);
        return null;
    }

    private static bool TryInches(string s, out double meters)
    {
        meters = 0;
        s = s.Trim().Trim('"');
        double v = 0;
        foreach (var part in s.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Contains('/'))
            {
                var f = part.Split('/');
                if (f.Length != 2 || !double.TryParse(f[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double a)
                    || !double.TryParse(f[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double c) || c == 0) return false;
                v += a / c;
            }
            else if (double.TryParse(part, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double a)) v += a;
            else return false;
        }
        meters = In(v);
        return v > 0;
    }
}
