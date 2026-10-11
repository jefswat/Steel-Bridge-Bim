using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;

namespace SBM.Core.Steel;

/// <summary>
/// Bolted field splices (NSBA-style): each flange gets an outside splice plate the full width of the (narrower)
/// flange and two inside plates clear of the web and flange-to-web welds; fillers make up flange thickness differences
/// on the outside. The web gets two splice plates. Bolts are on a regular grid (pitch × gage) with the configured edge
/// distances; holes are cut in the girder plates, splice plates and fillers.
/// </summary>
public sealed partial class SteelModelGenerator
{
    private void BuildFieldSplice(FieldSpliceResult sp)
    {
        var g = _f.Girders[sp.Girder];
        var path = g.Path;
        var fsd = _def.FieldSplices[sp.SpliceIndex];
        var sd = fsd.Detail ?? _def.SpliceDetail;
        int number = fsd.Number > 0 ? fsd.Number : sp.SpliceIndex + 1;
        double sj = sp.GirderDistance, gap = _def.FieldSpliceGap;
        var bg = sd.BoltDiameter > 0 ? new BoltGeometry(sd.BoltDiameter) : _model.Bolt;
        double e = _def.Bolts.EdgeDistance, p = _def.Bolts.Pitch, hd = bg.HoleDiameter, d = Sec.WebDepth;
        double first = sd.FirstBoltFromJoint > 0 ? sd.FirstBoltFromJoint : gap / 2 + e;
        double endE = sd.PlateEndDistance > 0 ? sd.PlateEndDistance : e;
        double twL = _web[g.Index].Thickness(sj - gap / 2 - 1e-4), twR = _web[g.Index].Thickness(sj + gap / 2 + 1e-4);
        double tw = Math.Max(twL, twR);
        if (Math.Abs(twL - twR) > 1e-4) Warn($"{g.Name}: web thickness changes at field splice {number}; web fillers are not modeled.");
        string mark = $"FS{number}-{g.Name}";
        _assemblies.Add(new SteelAssembly { Mark = mark, Kind = AssemblyKind.FieldSplice, Girder = g.Index,
            Description = $"Bolted field splice {number}, {g.Name}" });

        Vec3 Map(double s, double r, double z) => GirderPoint(path, s, r, z);
        Vec3 WebMap(double s, double z, double r) => GirderPoint(path, s, r, z);
        var tangentAt = new Func<double, Vec3>(s => { var t = path.At(s).tangent; return new Vec3(t.X, t.Y, 0); });
        var holes = _holes[g.Index];
        int pn = 1;
        void Part(Mesh m, PartCategory cat, string desc, double len) =>
            _parts.Add(new SteelPart { Mark = $"{mark}-P{pn++}", Category = cat, Assembly = mark, Mesh = m, Length = len, Description = desc });

        foreach (bool isTop in new[] { true, false })
        {
            var prof = isTop ? _top[g.Index] : _bot[g.Index];
            double wl = prof.Width(sj - gap / 2), wr = prof.Width(sj + gap / 2);
            double b = Math.Min(wl, wr);
            double tL = prof.Thickness(sj - gap / 2), tR = prof.Thickness(sj + gap / 2);
            double tmax = Math.Max(tL, tR), tmin = Math.Min(tL, tR);
            var pat = isTop ? sd.Top : sd.Bottom;
            double to = pat?.OuterThickness ?? sd.FlangeOuterPlateThickness, ti = pat?.InnerThickness ?? sd.FlangeInnerPlateThickness;
            double ls;
            var bolts = new List<Vec2>();
            if (pat != null)
            {
                // As scheduled: rows at first + k*spacing, lines at the listed offsets; staggered = outer lines on even rows.
                ls = first + pat.Spaces * pat.Spacing + endE;
                var offs = pat.LineOffsets.OrderBy(x => x).ToList();
                double outerOff = offs.Count > 0 ? offs.Max() : 0;
                foreach (int sgn in new[] { -1, 1 })
                    for (int k = 0; k <= pat.Spaces; k++)
                        foreach (double o in offs)
                        {
                            if (pat.Staggered && (Math.Abs(o - outerOff) < 1e-6) != (k % 2 == 0)) continue;
                            foreach (int side in new[] { -1, 1 }) bolts.Add(new Vec2(sj + sgn * (first + k * pat.Spacing), side * o));
                        }
                if (pat.OuterWidth > 0) b = pat.OuterWidth;
                if (outerOff + endE > b / 2 + 1e-6)
                    Warn($"{mark}: {(isTop ? "top" : "bottom")} flange bolt lines need {In(2 * (outerOff + endE))} of plate width (have {In(b)}).");
                if (Math.Min(wl, wr) < b - 1e-4)
                    Warn($"{mark}: {(isTop ? "top" : "bottom")} flange is {In(Math.Min(wl, wr))} wide; the schedule's minimum is {In(b)}.");
            }
            else
            {
                int rows = Math.Max(1, isTop ? sd.TopFlangeBoltRows : sd.BottomFlangeBoltRows);
                int lines = Math.Max(1, sd.FlangeBoltLinesPerSide);
                ls = gap / 2 + e + (rows - 1) * p + e;
                var rs = new List<double>();
                for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i < lines; i++) rs.Add(side * (tw / 2 + sd.FirstLineFromWeb + i * sd.FlangeGage));
                if (rs.Max() + e > b / 2 + 1e-6)
                    Warn($"{mark}: {(isTop ? "top" : "bottom")} flange bolt lines need {In(2 * (rs.Max() + e))} of flange width (have {In(b)}).");
                foreach (int sgn in new[] { -1, 1 })
                    for (int m = 0; m < rows; m++)
                        foreach (double r in rs) bolts.Add(new Vec2(sj + sgn * (gap / 2 + e + m * p), r));
            }
            var hs = bolts.Select(q => new Hole(q, hd)).ToList();
            foreach (var h in hs) (isTop ? holes.Top : holes.Bottom).Add(h);

            // z: relative to top of web. Top flange inner face = 0 (up); bottom flange inner face = −D (down).
            double inner = isTop ? 0 : -d, dir = isTop ? 1 : -1;
            (double, double) Z(double a, double c) => (Math.Min(inner + dir * a, inner + dir * c), Math.Max(inner + dir * a, inner + dir * c));
            var (zo0, zo1) = Z(tmax, tmax + to);
            var (zi0, zi1) = Z(0, -ti);
            string fl = isTop ? "top" : "bottom";

            var outer = new Mesh();
            LongPlate(outer, path, sj - ls, sj + ls, path.S, Array.Empty<double>(), _ => -b / 2, _ => b / 2, _ => zo0, _ => zo1, hs, Map, false, "splice plate");
            Part(outer, PartCategory.SplicePlate, $"Outside {fl} flange splice {Pl(b, to)} x {In(2 * ls)}", 2 * ls);

            // Inside plates: the scheduled width from the flange edge, else clear of the web weld.
            double rin = pat is { InnerWidth: > 0 } ? b / 2 - pat.InnerWidth : tw / 2 + _def.Stiffeners.FlangeToWebWeld + sd.InnerPlateWebClearance;
            foreach (int side in new[] { -1, 1 })
            {
                var m = new Mesh();
                double r0 = side < 0 ? -b / 2 : rin, r1 = side < 0 ? -rin : b / 2;
                LongPlate(m, path, sj - ls, sj + ls, path.S, Array.Empty<double>(), _ => r0, _ => r1, _ => zi0, _ => zi1,
                    hs.Where(h => h.Center.Y > r0 && h.Center.Y < r1).ToList(), Map, false, "splice plate");
                Part(m, PartCategory.SplicePlate, $"Inside {fl} flange splice {Pl(r1 - r0, ti)} x {In(2 * ls)}", 2 * ls);
            }

            if (tmax - tmin > 1e-4)
            {
                // Filler on the thinner flange's outside face, out to the end of the splice plate.
                int thin = tL < tR ? -1 : 1;
                double a0 = thin < 0 ? sj - ls : sj + gap / 2, a1 = thin < 0 ? sj - gap / 2 : sj + ls;
                var (zf0, zf1) = Z(tmin, tmax);
                var fm = new Mesh();
                LongPlate(fm, path, a0, a1, path.S, Array.Empty<double>(), _ => -b / 2, _ => b / 2, _ => zf0, _ => zf1,
                    hs.Where(h => h.Center.X > a0 && h.Center.X < a1).ToList(), Map, false, "filler plate");
                Part(fm, PartCategory.FillerPlate, $"Filler {Pl(b, tmax - tmin)} x {In(a1 - a0)} ({fl} flange)", a1 - a0);
                if (tmax - tmin >= Inch(0.25))
                    Warn($"{mark}: {fl} flange filler is {In(tmax - tmin)} thick; check filler development (AASHTO 6.13.6.1.5).");
            }

            double grip = to + tmax + ti;
            double len = bg.LengthForGrip(grip);
            foreach (var q in bolts)
            {
                var origin = Map(q.X, q.Y, isTop ? zo1 : zo0);
                _bolts.Add(new BoltInstance { Frame = Frame3.FromXZ(origin, tangentAt(q.X), Up * -dir), Grip = grip, Length = len, Assembly = mark,
                    Diameter = bg.Diameter });
            }
            if (isTop) _studExclusions[g.Index].Add((sj - ls, sj + ls));
        }

        // Web splice: two plates, bolt columns each side of the joint, rows filling the plate depth.
        {
            var wp = sd.Web;
            double twp = wp?.PlateThickness ?? sd.WebPlateThickness, clr = sd.WebPlateFlangeClearance + _def.Stiffeners.FlangeToWebWeld;
            double h, lw, zTopPlate, zBotPlate;
            var bolts = new List<Vec2>();
            if (wp != null)
            {
                // As scheduled: top row at Dim. "S" below the top of web, rows down at the spacing; columns from the joint.
                double zTopRow = -wp.TopRowBelowWebTop, zBotRow = zTopRow - wp.RowSpaces * wp.RowSpacing;
                zTopPlate = zTopRow + endE; zBotPlate = zBotRow - endE;
                h = zTopPlate - zBotPlate;
                lw = first + wp.ColumnSpaces * wp.ColumnSpacing + endE;
                foreach (int sgn in new[] { -1, 1 })
                    for (int c = 0; c <= wp.ColumnSpaces; c++)
                        for (int k = 0; k <= wp.RowSpaces; k++)
                            bolts.Add(new Vec2(sj + sgn * (first + c * wp.ColumnSpacing), zTopRow - k * wp.RowSpacing));
                if (zBotPlate < -d + 1e-4 || zTopPlate > -1e-4)
                    Warn($"{mark}: web splice plate ({In(h)}) does not fit the {In(d)} web.");
            }
            else
            {
                int cols = Math.Max(1, sd.WebBoltColumns);
                h = d - 2 * clr;
                zTopPlate = -clr; zBotPlate = -d + clr;
                int nRows = Math.Max(2, (int)Math.Floor((h - 2 * e) / p + 1e-9) + 1);
                lw = gap / 2 + e + (cols - 1) * p + e;
                foreach (int sgn in new[] { -1, 1 })
                    for (int c = 0; c < cols; c++)
                        for (int k = 0; k < nRows; k++)
                            bolts.Add(new Vec2(sj + sgn * (gap / 2 + e + c * p), -d / 2 + (k - (nRows - 1) / 2.0) * p));
            }
            var hs = bolts.Select(q => new Hole(q, hd)).ToList();
            foreach (var hh in hs) holes.Web.Add(hh);
            foreach (int side in new[] { -1, 1 })
            {
                var m = new Mesh();
                double r0 = side < 0 ? -tw / 2 - twp : tw / 2, r1 = side < 0 ? -tw / 2 : tw / 2 + twp;
                LongPlate(m, path, sj - lw, sj + lw, path.S, Array.Empty<double>(), _ => zBotPlate, _ => zTopPlate, _ => r0, _ => r1, hs, WebMap, true, "splice plate");
                Part(m, PartCategory.SplicePlate, $"Web splice {Pl(h, twp)} x {In(2 * lw)}", 2 * lw);
            }
            double grip = 2 * twp + tw;
            double len = bg.LengthForGrip(grip);
            foreach (var q in bolts)
            {
                var (pt, t, _) = path.At(q.X);
                var right = new Vec3(t.Right.X, t.Right.Y, 0);
                _bolts.Add(new BoltInstance { Frame = Frame3.FromXZ(Map(q.X, -tw / 2 - twp, q.Y), Up, right), Grip = grip, Length = len,
                    Assembly = mark, Diameter = bg.Diameter });
            }
        }
    }
}
