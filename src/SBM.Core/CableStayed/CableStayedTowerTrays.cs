using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Steel;

namespace SBM.Core.CableStayed;

/// <summary>Hollow concrete tower legs and the steel stay anchorage trays cast into them (<see cref="CsTowerAnchorage"/>).</summary>
public sealed partial class CableStayedGenerator
{
    /// <summary>One wall of a hollow leg at parameter t: a rectangle in (across, along) given from the half width / half depth there
    /// (across = the horizontal normal of the leg tangent, along = tangent x across; as <see cref="LegRing"/>).</summary>
    private Vec3[] LegRingBox(CsPylonLeg leg, double t, Func<double, double, (double a0, double a1, double b0, double b1)> box)
    {
        var (p, d) = Bezier(leg, t);
        var up = new Vec3(0, 0, 1);
        var w = Vec3.Cross(up, d);
        w = w.Length < 1e-6 ? new Vec3(0, 1, 0) : w.Normalized();
        var dd = Vec3.Cross(d, w).Normalized();
        var (a0, a1, b0, b1) = box(leg.Section.WidthAt(t) / 2, leg.Section.DepthAt(t) / 2);
        return new[] { P(p + w * a0 + dd * b0), P(p + w * a1 + dd * b0), P(p + w * a1 + dd * b1), P(p + w * a0 + dd * b1) };
    }

    private sealed record TrayCable(string Stay, Vec3 Upper, Vec3 Dir, double Dxs, double Y);
    private sealed class Tray
    {
        public required int Pylon { get; init; }
        public required int Leg { get; init; }
        public required string LegName { get; init; }
        public required Frame3 Frame { get; init; }      // origin = leg CL at the tray level, X along the bridge, Y right, Z up
        public required double Side { get; init; }       // sign of the leg's frame y (inboard = -Side)
        public required double HalfWidth { get; init; }
        public required double HalfDepth { get; init; }
        public required double Wall { get; init; }
        public List<TrayCable> Cables { get; } = new();
    }
    private readonly Dictionary<(int leg, long z), Tray> _trays = new();

    /// <summary>The tower end of one stay with an anchorage tray: the cable passes the tray at its level (leg CL point at the stay's fraction), side by side
    /// with the opposing cable (+-CableOffset), enters through the transverse wall facing its span and ends on the bearing plate at the far end.
    /// Returns the stay's upper end (bearing plate face).</summary>
    private Vec3 TowerTray(string stayName, int legIndex, CsLegCurve leg, double fr, Vec3 lower)
    {
        var an = _d.AllPylons[leg.Pylon].Anchorage!;
        var sec = leg.Definition.Section;
        var pf = _m.PylonFrames[leg.Pylon];
        var c = leg.At(fr);
        double side = Math.Sign(Vec3.Dot(c - pf.Origin, pf.Y));
        if (side == 0) side = 1;
        double hw = sec.WidthAt(fr) / 2, hd = sec.DepthAt(fr) / 2, tw = sec.Wall;
        var key = (legIndex, (long)Math.Round(c.Z * 100));
        if (!_trays.TryGetValue(key, out var tray))
            _trays[key] = tray = new Tray { Pylon = leg.Pylon, Leg = legIndex, LegName = leg.Name, Frame = pf with { Origin = c }, Side = side, HalfWidth = hw, HalfDepth = hd, Wall = tw };
        double yc = -side * (hw - tw - an.TrayWidth / 2);                    // tray centre across, from the leg CL (primary web on the inboard wall)
        double dxs = Math.Sign(Vec3.Dot(lower - c, pf.X));                   // the stay's span side
        double y = yc + dxs * an.CableOffset;                                 // each cable in its own half of the tray
        double L2 = hd - tw, h2 = an.TrayHeight / 2, hb = an.BearingPlateSize / 2;
        // the cable enters through the end plate facing its span, low in the tray, rises across it and anchors where it reaches the top of the tray or
        // the far end (bearing plate BearingFromEnd inside the far end plate), whichever comes first
        var entry = c + pf.X * (dxs * (L2 - an.EndPlateThickness)) + pf.Y * y + new Vec3(0, 0, -h2 + an.FloorThickness + hb);
        var dir = (entry - lower).Normalized();
        double dx = Vec3.Dot(dir, pf.X);
        double kFar = Math.Abs(dx) < 1e-6 ? double.MaxValue : (-dxs * (L2 - an.EndPlateThickness - an.BearingFromEnd) - Vec3.Dot(entry - c, pf.X)) / dx;
        double kTop = dir.Z < 1e-6 ? double.MaxValue : (c.Z + h2 - an.FloorThickness - hb - entry.Z) / dir.Z;
        var upper = entry + dir * Math.Max(0, Math.Min(kFar, kTop));
        tray.Cables.Add(new TrayCable(stayName, upper, dir, dxs, y));
        return upper;
    }

    private void BuildTowerTrays()
    {
        if (_trays.Count == 0) return;
        int n = 0;
        foreach (var g in _trays.Values.GroupBy(t => t.Leg))
        {
            int k = 0;
            foreach (var tr in g.OrderBy(t => t.Frame.Origin.Z))
            {
                k++; n++;
                var an = _d.AllPylons[tr.Pylon].Anchorage!;
                string pm = tr.Pylon == 0 ? "PY" : $"PY{tr.Pylon + 1}";
                string asm = $"{pm}-L{tr.Leg - _d.FirstLegOf(tr.Pylon) + 1}-AT{k}";
                _m.Steel.Assemblies.Add(new SteelAssembly { Mark = asm, Kind = AssemblyKind.PylonAnchorTray,
                    Description = $"Stay anchorage tray {k} in {tr.LegName}, el. {L(tr.Frame.Origin.Z)}: {string.Join(" / ", tr.Cables.Select(c => c.Stay))}" });
                var f = tr.Frame;
                double s = tr.Side, L2 = tr.HalfDepth - tr.Wall, h2 = an.TrayHeight / 2, yIn = -s * (tr.HalfWidth - tr.Wall);
                double Y(double fromInboard) => yIn + s * fromInboard;
                void Box(string mk, PartCategory cat, double x0, double x1, double ya, double yb, double z0, double z1, string desc)
                {
                    var m = new Mesh();
                    MeshBuilder.AddBox(m, f, Math.Min(x0, x1), Math.Max(x0, x1), Math.Min(ya, yb), Math.Max(ya, yb), z0, z1);
                    _m.Steel.Parts.Add(new SteelPart { Mark = $"{asm}-{mk}", Category = cat, Assembly = asm, Mesh = m, Length = Math.Abs(x1 - x0), Description = desc, Assumption = _flag });
                }
                double tw = an.WebThickness, tf = an.FloorThickness, te = an.EndPlateThickness, W = an.TrayWidth;
                Box("PW", PartCategory.PylonAnchorPlate, -L2, L2, Y(0), Y(tw), -h2, h2, $"Primary web plate {L(2 * L2)} x {L(an.TrayHeight)} x {L(tw)} (on the inboard wall; studs not modeled)");
                Box("SW", PartCategory.PylonAnchorPlate, -L2, L2, Y(W - tw), Y(W), -h2, h2, $"Secondary web plate {L(2 * L2)} x {L(an.TrayHeight)} x {L(tw)}");
                Box("FL", PartCategory.PylonAnchorPlate, -L2, L2, Y(tw), Y(W - tw), -h2, -h2 + tf, $"Floor plate {L(W - 2 * tw)} x {L(tf)}");
                Box("TP", PartCategory.PylonAnchorPlate, -L2, L2, Y(tw), Y(W - tw), h2 - tf, h2, $"Top plate {L(W - 2 * tw)} x {L(tf)}");
                foreach (var (mk, sx) in new[] { ("E1", -1.0), ("E2", 1.0) })
                    Box(mk, PartCategory.PylonAnchorPlate, sx * L2, sx * (L2 - te), Y(tw), Y(W - tw), -h2 + tf, h2 - tf,
                        $"End plate {L(W - 2 * tw)} x {L(an.TrayHeight - 2 * tf)} x {L(te)} (embedded in the transverse wall; studs and the guide pipe opening not modeled)");
                int ci = 0;
                foreach (var c in tr.Cables)
                {
                    ci++;
                    // bearing plate normal to the cable at its upper end (the socket bears on its face), rib plates from it to the far end plate
                    var e1 = Vec3.Cross(Math.Abs(c.Dir.Z) > 0.9 ? new Vec3(1, 0, 0) : new Vec3(0, 0, 1), c.Dir).Normalized();
                    var e2 = Vec3.Cross(c.Dir, e1);
                    var bp = new Mesh();
                    // e1 is horizontal (across the tray): the plate is the half-cell wide across, BearingPlateSize high
                    double bw = Math.Min(an.BearingPlateSize, an.TrayWidth / 2 - tw - 0.0125);
                    MeshBuilder.AddBox(bp, new Frame3(c.Upper, e1, e2, c.Dir), -bw / 2, bw / 2, -an.BearingPlateSize / 2, an.BearingPlateSize / 2, 0, an.BearingPlateThickness);
                    _m.Steel.Parts.Add(new SteelPart { Mark = $"{asm}-BP{ci}", Category = PartCategory.PylonAnchorPlate, Assembly = asm, Mesh = bp, Length = an.BearingPlateSize,
                        Description = $"Bearing plate {L(bw)} x {L(an.BearingPlateSize)} x {L(an.BearingPlateThickness)}, stay {c.Stay}", Assumption = _flag });
                    double xu = Vec3.Dot(c.Upper - f.Origin, f.X), xEnd = -c.Dxs * (L2 - te);
                    double zu = c.Upper.Z - f.Origin.Z, hb = an.BearingPlateSize / 2;
                    double xBack = xu - c.Dxs * an.BearingPlateThickness * Math.Abs(Vec3.Dot(c.Dir, f.X));   // behind the bearing plate
                    double rh = bw / 2;
                    foreach (var (rk, ry) in new[] { ("R1", c.Y - rh), ("R2", c.Y + rh - an.RibThickness) })
                        Box($"{rk}{ci}", PartCategory.PylonAnchorPlate, xBack, xEnd, ry, ry + an.RibThickness, Math.Max(-h2 + tf, zu - hb), Math.Min(h2 - tf, zu + hb),
                            $"Rib plate (bearing plate to the end plate) x {L(an.RibThickness)}, stay {c.Stay}");
                    // guide pipe: from the leg face toward the cable's span to the inner face of that end plate, on the cable axis
                    double xFace = c.Dxs * tr.HalfDepth, xIn = c.Dxs * (L2 - te);
                    double dx = Vec3.Dot(c.Dir, f.X);
                    if (Math.Abs(dx) > 1e-6)
                    {
                        double x0u = Vec3.Dot(c.Upper - f.Origin, f.X);
                        var pOut = c.Upper + c.Dir * ((xFace - x0u) / dx);
                        var pIn = c.Upper + c.Dir * ((xIn - x0u) / dx);
                        var gm = new Mesh();
                        double r = an.GuidePipeDiameter / 2, len = (pIn - pOut).Length;
                        MeshBuilder.AddPrism(gm, new Frame3(pOut, e1, e2, c.Dir), r, 24, 0, len);
                        double full = 0.5 * 24 * r * r * Math.Sin(2 * Math.PI / 24), wall = Math.PI * (r * r - Math.Pow(Math.Max(0, r - an.GuidePipeWall), 2));
                        _m.Steel.Parts.Add(new SteelPart { Mark = $"{asm}-GP{ci}", Category = PartCategory.PylonGuidePipe, Assembly = asm, Mesh = gm, Length = len,
                            Description = $"Guide pipe {L(an.GuidePipeDiameter)} OD x {L(an.GuidePipeWall)} through the transverse wall, stay {c.Stay}", Assumption = _flag,
                            Density = _d.Analysis.SteelDensity * wall / full });
                    }
                }
            }
        }
        foreach (var t in _trays.Values.Where(t => t.Cables.Count > 2))
            Warn($"Cable-stayed tower anchorage: tray at el. {L(t.Frame.Origin.Z)} in {t.LegName} carries {t.Cables.Count} stays (a tray holds one opposing pair).");
        Info($"Cable-stayed tower anchorages: {n} trays ({_trays.Values.Count(t => t.Cables.Count == 2)} with an opposing pair, {_trays.Values.Count(t => t.Cables.Count == 1)} with one stay).");
    }
}
