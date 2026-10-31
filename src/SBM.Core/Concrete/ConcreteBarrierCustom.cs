using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Solids;
using SBM.Core.Units;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Concrete;

/// <summary>A face of a concrete element carrying a surface tag (form liner, thin brick, ...): planar-ish polygon + outward unit normal.
/// Consumed by <c>Surface.SurfaceTreatmentGenerator</c>.</summary>
public sealed record TaggedSurface(string Tag, string Owner, Vec3[] Polygon, Vec3 Outward);

/// <summary>
/// Custom barrier (<see cref="BarrierType.Custom"/>): the section from <see cref="BarrierDefinition.Profile"/> (fillets / chamfers expanded), optionally
/// perpendicular to the top of deck, square ends extended beyond the deck ends (<see cref="BarrierDefinition.BeginExtension"/>), zones with their own
/// sections (bump-outs, <see cref="BarrierZone"/>) and deck widenings under them. Tagged section edges become <see cref="TaggedSurface"/>s, and so do the
/// exposed end faces ("returns") of a zone where its tagged face stands outboard of the neighbouring section.
/// </summary>
public sealed partial class ConcreteGenerator
{
    private void BuildCustomBarrier(int side, Func<double, double> edgeAt)
    {
        var b = Deck.Barrier;
        if (b.Profile is not { Count: >= 3 }) { Warn("Custom barrier: no Profile (at least 3 points) - not built."); return; }
        string name = (side < 0 ? "Left " : "Right ") + (b.IsCurb ? "curb" : "barrier");
        string label = b.ProfileName ?? "custom barrier";
        var baseX = BarrierProfiles.Expand(b.Profile);
        bool perp = b.PerpendicularToDeck;

        // section point (r inward from the deck edge, h above the deck) at alignment distance d -> world
        Vec3 Map(double d, double r, double h)
        {
            double oE = edgeAt(d);
            if (!perp)
            {
                double o = oE - side * r;
                var p = _a.PointAtDistance(d, o);
                return p.WithZ(Math.Abs(h) < 1e-9 ? Top(d, o) : Top(d, oE) + h);   // H = 0: on the deck top; H < 0 (deck widening below the edge) from the edge, as H > 0
            }
            double zE = Top(d, oE), dl = 0.3;
            double sIn = (Top(d, oE - side * dl) - zE) / dl;            // rise per unit inward
            double c = 1 / Math.Sqrt(1 + sIn * sIn);
            double inward = r * c - h * sIn * c, z = zE + r * sIn * c + h * c;
            return _a.PointAtDistance(d, oE - side * inward).WithZ(z);
        }
        double toeR = baseX.Points.Where(q => Math.Abs(q.Y) < 1e-9).Max(q => q.X);
        Func<double, double> OffAt(double r) => dd => edgeAt(dd) - side * r;
        (double b, double e) VEnds(double r) => Deck.VariableWidth ? EndsVar(OffAt(r)) : Ends(edgeAt(0) - side * r);
        var toe = VEnds(toeR);
        double dStart = b.BeginExtension is double xb ? AlongOffset(toe.b, OffAt(toeR), -xb) : double.NaN;
        double dEnd = b.EndExtension is double xe ? AlongOffset(toe.e, OffAt(toeR), xe) : double.NaN;
        double d0 = double.IsNaN(dStart) ? baseX.Points.Min(q => VEnds(q.X).b) : dStart;
        double d1 = double.IsNaN(dEnd) ? baseX.Points.Max(q => VEnds(q.X).e) : dEnd;

        // alignment distance where a support CL (or a station's radial line) crosses the deck edge
        double Crossing(string support, double station)
        {
            if (support != "")
            {
                int j = _def.Supports.FindIndex(s => s.Name.Equals(support, StringComparison.OrdinalIgnoreCase));
                if (j < 0) throw new ArgumentException($"support '{support}' not found");
                var sl = _f.SupportLine(j);
                double guess = _a.Inverse(sl.AlignmentPoint).Distance;
                if (!_a.IntersectOffsetCurveWithLine(edgeAt, sl.AlignmentPoint, sl.Direction, guess - 40, guess + 40, guess, out double dx))
                    throw new ArgumentException($"CL {support} does not cross the deck edge");
                return dx;
            }
            if (!double.IsNaN(station)) return _a.StationToDistance(station);
            throw new ArgumentException("neither Support nor Station given");
        }
        // zones
        var zones = new List<(BarrierZone z, double a, double e, BarrierProfiles.Expanded x)>();
        foreach (var z in b.Zones ?? new())
        {
            try
            {
                double dc = Crossing(z.Support, z.Station);
                if (z.Profile.Count < 3) throw new ArgumentException("no section");
                var zx = BarrierProfiles.Expand(z.Profile);
                double za = AlongOffset(dc, edgeAt, -z.Length / 2), ze = AlongOffset(dc, edgeAt, z.Length / 2);
                // a zone ending AT a barrier end (end post at the deck end) may reach past it by up to 1 in. (station vs edge length on curves): clamped there
                const double endTol = 0.0254;
                bool atA = za < d0 + 1e-4 && za >= d0 - endTol, atE = ze > d1 - 1e-4 && ze <= d1 + endTol;
                if ((za < d0 + 1e-4 && !atA) || (ze > d1 - 1e-4 && !atE)) throw new ArgumentException("it reaches past the barrier end");
                if (atA) za = d0;
                if (atE) ze = d1;
                zones.Add((z, za, ze, zx));
            }
            catch (ArgumentException ex) { Warn($"{name}: zone '{z.Name}' not built - {ex.Message}."); }
        }

        // open joints (gaps across the section) along the deck edge
        var gaps = new List<(double a, double e)>();
        foreach (var jt in b.Joints ?? new())
        {
            try
            {
                if (jt.Width <= 0) throw new ArgumentException("no width");
                double dj = AlongOffset(Crossing(jt.Support, jt.Station), edgeAt, jt.Along);
                double ga = AlongOffset(dj, edgeAt, -jt.Width / 2), ge = AlongOffset(dj, edgeAt, jt.Width / 2);
                if (ga <= d0 + 1e-4 || ge >= d1 - 1e-4) throw new ArgumentException("it is not inside the barrier");
                gaps.Add((ga, ge));
            }
            catch (ArgumentException ex) { Warn($"{name}: joint at {(jt.Support != "" ? jt.Support : $"station {jt.Station:0.###}")} + {_u.FormatLength(jt.Along, LengthKind.Short)} not built - {ex.Message}."); }
        }

        // intervals between the zone ends; the shortest zone covering an interval governs
        var cuts = new List<double> { d0, d1 };
        foreach (var z in zones) { cuts.Add(z.a); cuts.Add(z.e); }
        foreach (var g in gaps) { cuts.Add(g.a); cuts.Add(g.e); }
        cuts = cuts.Distinct().OrderBy(x => x).ToList();
        var pieces = new List<Mesh>();
        var merged = new Mesh();
        var intervals = new List<(double a, double e, int zone)>();
        for (int i = 0; i + 1 < cuts.Count; i++)
        {
            double a = cuts[i], e = cuts[i + 1], m = (a + e) / 2;
            if (e - a < 1e-5) continue;
            if (gaps.Any(g => g.a <= m && m <= g.e)) continue;           // open joint
            int zi = -1;
            for (int k = 0; k < zones.Count; k++)
                if (zones[k].a <= m && m <= zones[k].e && (zi < 0 || zones[k].z.Length < zones[zi].z.Length)) zi = k;
            intervals.Add((a, e, zi));
        }
        var surf = new List<TaggedSurface>();
        for (int ii = 0; ii < intervals.Count; ii++)
        {
            var (a, e, zi) = intervals[ii];
            var x = zi < 0 ? baseX : zones[zi].x;
            var prof = x.Points;
            bool first = ii == 0 && a <= d0 + 1e-9, last = ii == intervals.Count - 1 && e >= d1 - 1e-9;
            var dA = prof.Select(q => first && double.IsNaN(dStart) ? VEnds(q.X).b : a).ToArray();
            var dE = prof.Select(q => last && double.IsNaN(dEnd) ? VEnds(q.X).e : e).ToArray();
            int n = AlongSamples(Math.Max(dE.Max() - dA.Min(), 0.01)) + 1;
            {
                // on curves the straight chords between sections cut inside the true curb / barrier line: sections close enough that the chord sag on the
                // tightest line of the section stays within 1/32 in (agent review NHT curbs C01: 0.68 in on the R ~ 53 ft trail curves at 1.5 m)
                double lo = dA.Min(), hi = dE.Max(), kMax = 0;
                for (int q = 0; q <= 20; q++) kMax = Math.Max(kMax, Math.Abs(_a.CurvatureAtDistance(Math.Clamp(lo + (hi - lo) * q / 20, 0, _a.Length))));
                if (kMax > 1e-6)
                {
                    double R = 1 / kMax, oMax = Math.Abs(edgeAt((lo + hi) / 2)) + prof.Max(q => Math.Abs(q.X));
                    double Rl = Math.Max(R - oMax, 0.5);                                   // the tighter side of the alignment
                    double dMax = Math.Sqrt(8 * Rl * 0.0254 / 32) * R / Rl;               // alignment distance per chord
                    n = Math.Max(n, Math.Min(4000, (int)Math.Ceiling((hi - lo) / dMax)) + 1);
                }
            }
            var rings = new List<Vec3[]>(n);
            for (int r = 0; r < n; r++)
                rings.Add(prof.Select((q, k) => Map(dA[k] + (dE[k] - dA[k]) * r / (n - 1), q.X, q.Y)).ToArray());
            var mesh = new Mesh();
            MeshBuilder.AddLoft(mesh, prof, rings);
            // exact geometry ON: the piece's exact sweep (corner k = its own section point between its own end distances); the mesh is unchanged
            if (_geo != null)
            {
                var dAk = dA; var dEk = dE;
                bool common = dAk.All(x => x == dAk[0]) && dEk.All(x => x == dEk[0]);
                RecordSweep(mesh, 0, prof.Length, u => prof.Select((q, k) => Map(dAk[k] + (dEk[k] - dAk[k]) * u, q.X, q.Y)).ToArray(),
                    common ? GradeBreaks(dAk[0], dEk[0]) : Array.Empty<double>(), b.IsCurb ? "curb" : "barrier");
            }
            pieces.Add(mesh);
            merged.Append(mesh);
            string owner = zi < 0 ? name : $"{name} {zones[zi].z.Name}";
            for (int k = 0; k < prof.Length; k++)
            {
                string tag = x.EdgeTags[k];
                if (tag == "") continue;
                int k1 = (k + 1) % prof.Length;
                var ed = prof[k1] - prof[k];
                var nrm = new Vec2(ed.Y, -ed.X).Normalized();               // outward in (r, h) for a CCW section
                for (int r = 0; r + 1 < n; r++)
                {
                    var poly = new[] { rings[r][k], rings[r][k1], rings[r + 1][k1], rings[r + 1][k] };
                    double dm = (dA[k] + dE[k]) / 2;
                    var mid = prof[k] + ed * 0.5;
                    var outw = (Map(dm, mid.X + nrm.X * 0.01, mid.Y + nrm.Y * 0.01) - Map(dm, mid.X, mid.Y)).Normalized();
                    surf.Add(new TaggedSurface(tag, owner, poly, outw));
                }
            }
        }
        // zone returns: a tagged plumb face of one section standing outboard of the neighbour's face at the same heights
        for (int ii = 0; ii + 1 < intervals.Count; ii++)
        {
            double d = intervals[ii].e;
            if (intervals[ii + 1].a > d + 1e-6) continue;                  // an open joint between them: no return
            var A = intervals[ii].zone < 0 ? baseX : zones[intervals[ii].zone].x;
            var B = intervals[ii + 1].zone < 0 ? baseX : zones[intervals[ii + 1].zone].x;
            foreach (var (P, Q, dir, zi) in new[] { (A, B, 1, intervals[ii].zone), (B, A, -1, intervals[ii + 1].zone) })
            {
                string owner = zi < 0 ? name : $"{name} {zones[zi].z.Name}";
                for (int k = 0; k < P.Points.Length; k++)
                {
                    string tag = P.EdgeTags[k];
                    var p0 = P.Points[k]; var p1 = P.Points[(k + 1) % P.Points.Length];
                    if (tag == "" || Math.Abs(p1.X - p0.X) > 1e-9 || p1.Y >= p0.Y) continue;   // tagged back-face edge (runs down in a CCW section)
                    double rA = p0.X, h0 = Math.Min(p0.Y, p1.Y), h1 = Math.Max(p0.Y, p1.Y);
                    for (int m = 0; m < Q.Points.Length; m++)
                    {
                        var q0 = Q.Points[m]; var q1 = Q.Points[(m + 1) % Q.Points.Length];
                        if (Math.Abs(q1.X - q0.X) > 1e-9 || q1.Y >= q0.Y || q0.X <= rA + 1e-6) continue;   // the neighbour's back face, further inward
                        double lo = Math.Max(h0, Math.Min(q0.Y, q1.Y)), hi = Math.Min(h1, Math.Max(q0.Y, q1.Y));
                        if (hi - lo < 1e-6) continue;
                        var poly = new[] { Map(d, rA, lo), Map(d, q0.X, lo), Map(d, q0.X, hi), Map(d, rA, hi) };
                        var outw = (Map(d + dir * 0.01, rA, lo) - Map(d, rA, lo)).Normalized();
                        surf.Add(new TaggedSurface(tag, owner + " return", poly, outw));
                    }
                }
            }
        }
        lock (_m.Barriers)
        {
            _m.Barriers.Add((name, merged));
            _m.BarrierPieces[name] = pieces;
            _m.TaggedSurfaces.AddRange(surf);
        }

        // deck widening under zones
        foreach (var (z, _, _, _) in zones.Where(t => t.z.DeckWidening > 0))
        {
            double dc = (zones.First(t => t.z == z).a + zones.First(t => t.z == z).e) / 2;
            double wa = AlongOffset(dc, edgeAt, -z.DeckWideningLength / 2), we = AlongOffset(dc, edgeAt, z.DeckWideningLength / 2);
            var sec = new[] { new Vec2(-z.DeckWidening, -Deck.OverhangThickness), new Vec2(0, -Deck.OverhangThickness), new Vec2(0, 0), new Vec2(-z.DeckWidening, 0) };
            var rings = Enumerable.Range(0, 3).Select(r => sec.Select(q => Map(wa + (we - wa) * r / 2.0, q.X, q.Y)).ToArray()).ToList();
            var mesh = new Mesh();
            MeshBuilder.AddLoft(mesh, sec, rings);
            if (_geo != null)
                RecordSweep(mesh, 0, sec.Length, u => sec.Select(q => Map(wa + (we - wa) * u, q.X, q.Y)).ToArray(), GradeBreaks(wa, we), "deck widening");
            lock (_m.Barriers) _m.DeckBumps.Add(($"Deck widening {z.Name}", mesh));
        }

        // report: length along the toe (gutter) line, section areas
        double toeLen = LengthAlong(d0, d1, OffAt(toeR)), slabLen = LengthAlong(toe.b, toe.e, OffAt(toeR));
        double vol = Math.Abs(merged.Volume());
        string L(double m) => _u.FormatLength(m, LengthKind.Long);
        string msg = $"{name}: {label}, {L(toeLen)} along the toe (gutter) line{(b.PlanLength is double pl ? $" (plan {L(pl)}, diff {_u.FormatLength(toeLen - pl, LengthKind.Short)})" : "")}, " +
                     $"deck end to deck end along it {L(slabLen)}, section {Math.Abs(MeshBuilder.SignedArea(baseX.Points)) / Math.Pow(Ft(1), 2):0.000} SF, {zones.Count} zone(s){(gaps.Count > 0 ? $", {gaps.Count} open joint(s)" : "")}, concrete {vol / Math.Pow(Ft(1), 3) / 27:0.00} CY" +
                     $"{(perp ? ", perpendicular to the top of deck" : "")}.";
        lock (_m.Notes) _m.Notes.Add(msg);
        _diag.Enqueue(new Diagnostic(DiagnosticSeverity.Info, msg));
        if (b.PlanLength is double plen && Math.Abs(toeLen - plen) > In(0.5))
            Warn($"{name}: length along the toe line {L(toeLen)} differs from the plan {L(plen)} by {_u.FormatLength(toeLen - plen, LengthKind.Short)}.");
    }

    /// <summary>Alignment distance reached by moving <paramref name="length"/> (signed) along the offset curve from d0.</summary>
    private double AlongOffset(double d0, Func<double, double> offAt, double length)
    {
        if (Math.Abs(length) < 1e-12) return d0;
        double dir = Math.Sign(length), rem = Math.Abs(length), d = d0, h = 0.05;
        var p = _a.PointAtDistance(d, offAt(d));
        for (int it = 0; it < 200000; it++)
        {
            var q = _a.PointAtDistance(d + dir * h, offAt(d + dir * h));
            double l = (q - p).Length;
            if (l >= rem) return d + dir * h * rem / l;
            rem -= l; d += dir * h; p = q;
        }
        return d;
    }

    private double LengthAlong(double d0, double d1, Func<double, double> offAt)
    {
        int n = Math.Max(8, (int)Math.Ceiling(Math.Abs(d1 - d0) / 0.05));
        double len = 0;
        var p = _a.PointAtDistance(d0, offAt(d0));
        for (int i = 1; i <= n; i++) { double d = d0 + (d1 - d0) * i / n; var q = _a.PointAtDistance(d, offAt(d)); len += (q - p).Length; p = q; }
        return len;
    }
}
