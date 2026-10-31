using SBM.Core.Bridge;
using SBM.Core.Concrete;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Arch;

public static partial class ArchGenerator
{
    private sealed partial class Gen
    {
        /// <summary>Rib-box concrete fills built by <see cref="RibBoxFill"/> (WORLD meshes), appended to the knuckle-zone concrete by BuildDeck.</summary>
        private readonly List<(string Corner, Mesh Mesh)> _ribFills = new();

        /// <summary>
        /// Concrete fill of the rib box at a knuckle corner (<see cref="ArchDeckDefinition.KnuckleRibBoxFill"/>): the space between the inner faces of the
        /// outside and inside arch flange plates (lateral +-<paramref name="hi"/> about the rib CL plane), from the end web inside face to the plates'
        /// intrados edge, on the end flange top, up to the finished grade (the pocket is open at the top, below the deck surface). Coordinates as the
        /// plates: P(u, v, t) = T + cIn u + up v + bn t (local); the corners are solved per face t so the end follows the skewed end web, the bottom the
        /// end flange top plane, the top the grade and the far side the intrados edge line {(u, v) . n2 = -hw}.
        /// </summary>
        private void RibBoxFill(string corner, Vec3 T, Vec3 cIn, Vec3 up, Vec3 bn, Vec2 t2, Vec2 n2, double hw, double hi,
            (Vec2 a, Vec2 b, Vec2 t, Vec2 n, Vec2 ctr) el, double dIn, double zef)
        {
            Vec3 Pt(double u, double v, double t) => T + bn * t + cIn * u + up * v;
            double Dn(Vec3 p) => Vec2.Dot(p.XY - el.a, el.n);
            double du = Vec2.Dot(cIn.XY, el.n);
            if (Math.Abs(du) < 0.1 || Math.Abs(up.Z) < 0.5) { Warn($"Arch: knuckle rib box fill at {corner} not built (geometry)."); return; }
            (double u, double v) Line(double s) => (t2.X * s - n2.X * hw, t2.Y * s - n2.Y * hw);
            double Grade(Vec3 p) => GradeZ(p.X, p.Y);
            // 0 = end web x bottom, 1 = intrados x bottom, 2 = intrados x grade, 3 = end web x grade
            (double u, double v) Corner(int kind, double t)
            {
                if (kind == 1 || kind == 2)
                {
                    double s = 0;
                    for (int i = 0; i < 12; i++)
                    {
                        var (u0, v0) = Line(s); var (u1, v1) = Line(s + 0.01);
                        var p0 = Pt(u0, v0, t); var p1 = Pt(u1, v1, t);
                        double target = kind == 1 ? zef : Grade(p0);
                        double dz = (p1.Z - p0.Z) / 0.01;
                        if (Math.Abs(dz) < 1e-6) break;
                        s += (target - p0.Z) / dz;
                    }
                    return Line(s);
                }
                double uu = 0, vv = 0;
                for (int i = 0; i < 12; i++)
                {
                    uu = (dIn - Dn(Pt(0, vv, t))) / du;
                    var q = Pt(uu, 0, t);
                    double target = kind == 0 ? zef : Grade(Pt(uu, vv, t));
                    vv = (target - q.Z) / up.Z;
                }
                return (uu, vv);
            }
            var nom = Enumerable.Range(0, 4).Select(k => Corner(k, 0)).ToArray();
            if (nom[1].u <= nom[0].u + In(0.5) || nom[2].u <= nom[3].u + In(0.5) || nom[3].v <= nom[0].v + In(0.5))
            { Warn($"Arch: knuckle rib box fill at {corner} not built (the intrados edge does not pass the end web inside the slab depth)."); return; }
            var poly = new List<Vec2> { new(nom[0].u, nom[0].v), new(nom[1].u, nom[1].v), new(nom[2].u, nom[2].v), new(nom[3].u, nom[3].v) };
            bool ccw = MeshBuilder.SignedArea(poly) >= 0;
            if (!ccw) poly.Reverse();
            int Kind(double u, double v)
            {
                for (int k = 0; k < 4; k++) if (Math.Abs(u - nom[k].u) < 1e-12 && Math.Abs(v - nom[k].v) < 1e-12) return k;
                return -1;
            }
            var m = new Mesh();
            PlateBuilder.AddPrism(m, poly, Array.Empty<Hole>(), (u, v, t) =>
            {
                int k = Kind(u, v);
                var (uu, vv) = k >= 0 ? Corner(k, t) : (u, v);
                return W(Pt(uu, vv, t));
            }, _ => -hi, _ => hi);
            _ribFills.Add((corner, m));
        }

        /// <summary>Appends the rib box fills to the knuckle-zone concrete and reports their volume and the steel inside them (not subtracted).</summary>
        private void FinishRibBoxFills(Mesh ends, ArchDeckDefinition dk)
        {
            if (_ribFills.Count == 0) return;
            double vol = 0, steelIn = 0;
            var rows = new List<string>();
            foreach (var (corner, m) in _ribFills)
            {
                AppendExact(ends, m);
                double v = Math.Abs(m.Volume());
                vol += v;
                // Steel plates inside the fill (stub web, knuckle tie web, stiffeners, studs are not cut out of the concrete): sampled on a 1/4" grid.
                var bb = m.Bounds();
                var parts = Model.Steel.Parts.Where(p => p.Mesh != null && !p.Mesh.IsEmpty && BoxesOverlap(p.Mesh.Bounds(), bb)).Select(p => p.Mesh!).ToList();
                double sIn = 0;
                if (parts.Count > 0)
                {
                    var fill = new RebarCover(new[] { m }, 0.01);
                    var steel = new RebarCover(parts, 0.01);
                    double h = In(0.25);
                    int nIn = 0;
                    for (double x = bb.Min.X + h / 2; x < bb.Max.X; x += h)
                        for (double y = bb.Min.Y + h / 2; y < bb.Max.Y; y += h)
                            for (double z = bb.Min.Z + h / 2; z < bb.Max.Z; z += h)
                            {
                                var p = new Vec3(x, y, z);
                                if (fill.Contains(p) && steel.Contains(p)) nIn++;
                            }
                    sIn = nIn * h * h * h;
                }
                steelIn += sIn;
                rows.Add($"{corner} {v / 0.0283168:F2} CF (steel inside {sIn / 0.0283168:F2} CF)");
            }
            if (dk.KnuckleRibBoxFillFlag.Length > 0) Warn(dk.KnuckleRibBoxFillFlag);
            Info($"Arch knuckle rib box fill (concrete between the arch flange plates, end web to the intrados edge, end flange top to the finished grade): {_ribFills.Count} corners, " +
                 $"{vol / 0.764555:F3} CY gross; steel plates inside (stub web, tie web, stiffeners, not subtracted) {steelIn / 0.764555:F4} CY - {string.Join("; ", rows)}.");
            Model.RibBoxFillVolume = vol;
            Model.RibBoxFillSteelVolume = steelIn;
        }

        /// <summary>
        /// Reports (never moves) the bottom flange connection plates of the floorbeams at the knuckles that overlap the knuckle fill plates under the tie
        /// flange: p.146 Detail 1 (typical connection) puts a plate under the floorbeam / tie flanges, p.124 / 125 put the 1 1/8" fill (splice outline) under
        /// the tie flange past the joint - at FB1 / FB33 both occupy that space; the end floorbeam detail sheet (p.124 note 1) is not in the set.
        /// </summary>
        private void FloorbeamFillConflicts()
        {
            if (!_d.Knuckles.Any(k => k.Detail is { FloorbeamsStopAtEndFlange: true })) return;
            var plates = Model.Steel.Parts.Where(p => p.Category == PartCategory.ArchConnectionPlate && System.Text.RegularExpressions.Regex.IsMatch(p.Mark, @"-FB[AB]$")).ToList();
            var fills = Model.Steel.Parts.Where(p => p.Category == PartCategory.KnucklePlate && p.Mark.EndsWith("-FI")).ToList();
            foreach (var pl in plates)
            {
                var bp = pl.Mesh.Bounds();
                foreach (var fi in fills)
                {
                    var bf = fi.Mesh.Bounds();
                    if (!BoxesOverlap(bp, bf)) continue;
                    var a = new RebarCover(new[] { pl.Mesh }, 0.01); var b = new RebarCover(new[] { fi.Mesh }, 0.01);
                    double h = In(1.0 / 16);
                    var lo = new Vec3(Math.Max(bp.Min.X, bf.Min.X), Math.Max(bp.Min.Y, bf.Min.Y), Math.Max(bp.Min.Z, bf.Min.Z));
                    var hi = new Vec3(Math.Min(bp.Max.X, bf.Max.X), Math.Min(bp.Max.Y, bf.Max.Y), Math.Min(bp.Max.Z, bf.Max.Z));
                    int n = 0;
                    for (double x = lo.X + h / 2; x < hi.X; x += h)
                        for (double y = lo.Y + h / 2; y < hi.Y; y += h)
                            for (double z = lo.Z + h / 2; z < hi.Z; z += h)
                            {
                                var q = new Vec3(x, y, z);
                                if (a.Contains(q) && b.Contains(q)) n++;
                            }
                    if (n == 0) continue;
                    double vol = n * h * h * h / Math.Pow(0.0254, 3);
                    string item = $"floorbeam {pl.Assembly} bottom flange connection plates {pl.Mark} vs the knuckle fill {fi.Mark} under the tie flange";
                    Model.Checks.Add(new ArchCheck("CONFLICT (p.146 vs p.124 / 125): " + item, "one plate under the tie flange",
                        $"both: overlap ~{vol:F0} cu in", vol, false,
                        "p.146 Detail 1 (typical floorbeam connection; bottom plates = top plates mirrored, already UNRESOLVED) vs the 1 1/8\" fill under the tie flange past the joint (p.124 / 125 plan + A-A); the end floorbeam detail (p.124 note 1) is not in the set - plates kept, not moved"));
                    Warn($"Arch CONFLICT (p.146 vs p.124 / 125): {item} overlap ~{vol:F0} cu in (sampled at 1/16\") - the typical bottom connection plate and the knuckle fill both sit under the tie flange at the end floorbeam; the end floorbeam detail sheet (p.124 note 1) is not in the set. Both kept as drawn.");
                }
            }
        }

        private static bool BoxesOverlap(Box3 a, Box3 b) => a.Min.X <= b.Max.X && b.Min.X <= a.Max.X && a.Min.Y <= b.Max.Y && b.Min.Y <= a.Max.Y && a.Min.Z <= b.Max.Z && b.Min.Z <= a.Max.Z;
    }
}
