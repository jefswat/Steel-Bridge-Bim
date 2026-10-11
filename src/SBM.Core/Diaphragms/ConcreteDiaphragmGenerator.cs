using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Prestressed;
using SBM.Core.Solids;
using SBM.Core.Units;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Diaphragms;

/// <summary>
/// Concrete end / pier diaphragms of a prestressed bridge (concrete-bridge design feature C, docs/plan-data/concrete-bridge-design.md
/// section 4). Runs after the beams and the substructure (RegenerationCache phase 2) and reads only: the framing (support lines, bearing
/// points), the prestressed beams (outline, end points, slope, pads) and the deck surface. Each support with a
/// <see cref="SupportDefinition.Diaphragm"/> gets one diaphragm made of parts.
/// <para>Geometry (exact, no booleans): a part is cut into SLABS between planes parallel to the support line (x = const) at every section
/// vertex and beam end. In a slab each slice x = c is a simple polygon in (S along the support line, z): the bottom edge (stepped bottom /
/// reference line), the two end faces, and the top edge with one NOTCH per beam running into the slab - the beam outline (stretched and
/// shifted by the beam's angle to the support line) plus its haunch up to the soffit. Every polygon vertex moves linearly with c (straight
/// beam lines, planar references sampled at fixed parameters), so the slab is a loft of the two end slices. Beam ends are taken plumb at
/// the end-face position at the top CL (pier beams square to the axis in elevation lean by slope x depth; the deviation is reported).</para>
/// </summary>
public static class ConcreteDiaphragmGenerator
{
    private const double Big = 1000;           // top of the haunch extension of a beam void (m above the beam)
    private static readonly double Sample = Ft(1);

    public static ConcreteDiaphragmModel? Generate(BridgeFraming f, PrestressedModel? ps, UnitSystem u)
    {
        var def = f.Definition;
        if (!def.Supports.Any(s => s.Diaphragm != null)) return null;
        var model = new ConcreteDiaphragmModel();
        if (ps == null)
        {
            model.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, "Concrete diaphragms need prestressed beams (girder kind prestressed) - not built."));
            return model;
        }
        for (int j = 0; j < def.Supports.Count && j < f.Supports.Count; j++)
        {
            var dd = def.Supports[j].Diaphragm;
            if (dd == null) continue;
            try { BuildSupport(f, ps, u, j, dd, model); }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                model.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, $"Diaphragm {def.Supports[j].Name}: not built - {ex.Message}"));
            }
        }
        return model;
    }

    // ------------------------------------------------------------------------------------------------ support frame

    /// <summary>Support frame: S along the support line from the alignment point (+ right), x normal to it in the definition's sense
    /// (abutments: + toward the span; piers: + ahead station).</summary>
    internal sealed class Frame
    {
        public required Alignment Al;
        public required DeckDefinition Deck;
        public required Vec2 O, DS, N;
        public required double Xs;
        public List<(double S, double Z)> Steps = new();   // per girder line, sorted by S
        public List<double> Risers = new();
        public Vec2 W(double s, double x) => O + DS * s + N * (Xs * x);
        public double X(Vec2 p) => Xs * Vec2.Dot(p - O, N);
        public double S(Vec2 p) => Vec2.Dot(p - O, DS);
        public double DeckTop(double s, double x)
        {
            var so = Al.Inverse(W(s, x));
            return Deck.FinishedElevation(Al, so.Station, so.Offset);
        }
        public double OffsetAt(double s, double x) => Al.Inverse(W(s, x)).Offset;
        public double BottomAt(double s)
        {
            if (Steps.Count == 0) throw new InvalidOperationException("a section point uses the Bottom reference but no BottomSteps are given");
            int k = 0;
            while (k < Risers.Count && s > Risers[k]) k++;
            return Steps[Math.Min(k, Steps.Count - 1)].Z;
        }
        public double Z(DiaphragmZRef r, double dz, double s, double x) => r switch
        {
            DiaphragmZRef.DeckTop => DeckTop(s, x) + dz,
            DiaphragmZRef.Soffit => DeckTop(s, x) - Deck.Thickness + dz,
            DiaphragmZRef.Bottom => BottomAt(s) + dz,
            _ => dz,
        };
        /// <summary>S on the slice x where the alignment offset equals <paramref name="o"/>.</summary>
        public double SAtOffset(double o, double x)
        {
            double s = o, h = 0.01;
            for (int it = 0; it < 40; it++)
            {
                double g = OffsetAt(s, x) - o;
                if (Math.Abs(g) < 1e-9) break;
                double dg = (OffsetAt(s + h, x) - OffsetAt(s - h, x)) / (2 * h);
                if (Math.Abs(dg) < 1e-6) break;
                s -= g / dg;
            }
            return s;
        }
        /// <summary>Offset lines where the deck top has a kink (cross-slope pivot, edge-strip gutter lines).</summary>
        public List<double> KinkOffsets(double station)
        {
            var k = new List<double> { Al.CrossSlope.PivotOffsetAt(station) };
            if (Deck.EdgeStrip is { Width: > 0 } es) { k.Add(Deck.LeftAt(station) + es.Width); k.Add(Deck.RightAt(station) - es.Width); }
            return k;
        }
    }

    /// <summary>A beam end at a support: maps beam section points to (S, z) on a slice x = c.</summary>
    internal sealed class BeamEnd
    {
        public required PrestressedBeam Beam;
        public required bool AtStart;
        public required Vec3 Top;        // top of beam at the CL at this end face
        public required Vec2 T, Right;   // beam axis (start -> end), right of the CL
        public required double Xd;       // end face x (definition sense) at the top CL
        public required int Sx;          // +1: the beam occupies x > Xd
        public required Vec2[] Void;     // section coords, CCW: outline + haunch extension up to Big
        public double D => Beam.Shape.Depth;
        public double Hw => Beam.Shape.TopWidth / 2;
        public double Hb => Beam.Shape.BottomWidth / 2;
        public bool PresentAt(double c) => (c - Xd) * Sx > 1e-9;
        public double SAlong(Frame k, double c, double xb)
        {
            double tn = k.Xs * Vec2.Dot(T, k.N);
            return (c - k.Xs * Vec2.Dot(Top.XY + Right * xb - k.O, k.N)) / tn;
        }
        public (double S, double Z) Map(Frame k, double c, double xb, double y)
        {
            double s = SAlong(k, c, xb);
            var q = Top.XY + T * s + Right * xb;
            return (k.S(q), Top.Z + Beam.Slope * s + y - D);
        }
        /// <summary>Plan point and elevation of section point (xb, y) at distance s (plan) along the beam from the CL top end point.</summary>
        public Vec3 At(double s, double xb, double y) { var q = Top.XY + T * s + Right * xb; return q.WithZ(Top.Z + Beam.Slope * s + y - D); }
        /// <summary>Plan direction from this end into the beam.</summary>
        public Vec2 Into => AtStart ? T : T * -1;
    }

    private static Vec2[] VoidOutline(TxGirderShape shape)
    {
        var o = shape.Outline();
        double d = shape.Depth, hw = shape.TopWidth / 2, eps = 1e-7;
        int n = o.Length;
        bool IsTop(Vec2 p) => p.Y >= d - eps;
        int start = -1;
        for (int i = 0; i < n; i++) if (IsTop(o[i]) && !IsTop(o[(i + 1) % n])) { start = (i + 1) % n; break; }
        if (start < 0) throw new InvalidOperationException($"beam outline {shape.Name} has no top edge");
        var v = new List<Vec2>();
        for (int k = 0; k < n; k++) { var p = o[(start + k) % n]; if (!IsTop(p)) v.Add(p); }
        // left side down, bottom, right side up; then the haunch (top flange width) up to Big.
        v.Add(new Vec2(hw, d)); v.Add(new Vec2(hw, d + Big)); v.Add(new Vec2(-hw, d + Big)); v.Add(new Vec2(-hw, d));
        if (MeshBuilder.SignedArea(v) < 0) v.Reverse();
        return v.ToArray();
    }

    // ------------------------------------------------------------------------------------------------ one support

    private static void BuildSupport(BridgeFraming f, PrestressedModel ps, UnitSystem u, int j, ConcreteDiaphragmDefinition dd, ConcreteDiaphragmModel model)
    {
        var def = f.Definition;
        var sup = def.Supports[j];
        var sl = f.Supports[j];
        var al = f.Alignment;
        string name = dd.Name != "" ? dd.Name : $"{sup.Name} diaphragm";
        string L(double m) => u.FormatLength(m, LengthKind.Short);
        void Warn(string m) => model.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, $"Diaphragm {sup.Name}: {m}"));
        void Info(string m) => model.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info, $"Diaphragm {sup.Name}: {m}"));

        // Frame: N = support normal pointing up-station; abutments x + toward the span.
        var tan = Vec2.FromAngle(al.AngleAt(sl.Station));
        var nrm = sl.Direction.Left;
        if (Vec2.Dot(nrm, tan) < 0) nrm = nrm * -1;
        double xs = sup.Type == SupportType.Abutment && j == def.Supports.Count - 1 ? -1 : 1;
        if (sup.Type == SupportType.Abutment && j > 0 && j < def.Supports.Count - 1) Warn("abutment diaphragm at an interior support: x is taken + ahead station.");
        var k = new Frame
        {
            Al = al, Deck = def.Deck, O = sl.AlignmentPoint, DS = sl.Direction, N = nrm, Xs = xs,
        };

        // Bottom steps: S of each girder line on the CL bearing / CL pier line (average of the back / ahead bearing points).
        foreach (var st in dd.BottomSteps.OrderBy(s => s.Girder))
        {
            int g = st.Girder - 1;
            var pts = g >= 0 && g < sl.Bearings.Length
                ? new[] { sl.BearingFor(g, false), sl.BearingFor(g, true) }.Where(b => b != null).ToList()
                : new List<BearingResult?>();
            if (pts.Count == 0) { Warn($"bottom step for girder {st.Girder}: no bearing of that girder on this support - step ignored."); continue; }
            k.Steps.Add((pts.Average(b => k.S(b!.TopOfSteel.XY)), st.Elevation));
        }
        k.Steps.Sort((a, b) => a.S.CompareTo(b.S));
        Assumption? assumption = null;
        if (k.Steps.Count > 1)
        {
            switch (dd.Risers)
            {
                case DiaphragmRiserRule.Explicit:
                    k.Risers = dd.RiserS.OrderBy(x => x).ToList();
                    if (k.Risers.Count != k.Steps.Count - 1) Warn($"{k.Risers.Count} explicit risers for {k.Steps.Count} steps (need {k.Steps.Count - 1}).");
                    break;
                default:
                    k.Risers = Enumerable.Range(0, k.Steps.Count - 1).Select(i => (k.Steps[i].S + k.Steps[i + 1].S) / 2).ToList();
                    if (dd.Risers == DiaphragmRiserRule.Unresolved)
                    {
                        string why = dd.RiserSource != "" ? dd.RiserSource : "riser position between the bottom steps not dimensioned on the plans";
                        assumption = Assumption.Unresolved(why, "risers drawn at mid-bay between the beam CLs on the CL bearing / pier line (display only; volumes depend on it)");
                    }
                    break;
            }
        }

        // Beam ends at this support: start ends of the span ahead, end ends of the span behind.
        var ends = new List<BeamEnd>();
        foreach (var b in ps.Beams)
        {
            bool atStart = b.Span == j, atEnd = b.Span == j - 1;
            if (!atStart && !atEnd) continue;
            var top = atStart ? b.TopStart : b.TopEnd;
            var t = (b.TopEnd.XY - b.TopStart.XY).Normalized();
            var into = atStart ? t : t * -1;
            var be = new BeamEnd
            {
                Beam = b, AtStart = atStart, Top = top, T = t, Right = t.Right, Xd = k.X(top.XY),
                Sx = Math.Sign(xs * Vec2.Dot(into, nrm)), Void = VoidOutline(b.Shape),
            };
            if (b.Definition?.InteriorEnds == InteriorGirderEnds.Square && sup.Type != SupportType.Abutment)
                Warn($"{b.Mark}: beam end is square to the beam in plan - the diaphragm treats it as parallel to the support at its CL (not exact).");
            ends.Add(be);
        }
        double maxLean = ends.Count == 0 ? 0 : ends.Max(e => sup.Type == SupportType.Abutment ? 0 : Math.Abs(e.Beam.Slope) * e.D * Math.Abs(Vec2.Dot(e.T, nrm)));
        if (maxLean > In(1.0 / 32))
            Info($"beam end faces lean up to {L(maxLean)} over the beam depth (square to the axis in elevation); the beam voids end plumb at the top CL end point.");

        var dia = new ConcreteDiaphragm { Name = name, Support = sup.Name, SupportIndex = j, Definition = dd, Assumption = assumption };
        foreach (var part in dd.Parts)
        {
            string pn = part.Name != "" ? part.Name : $"part {dd.Parts.IndexOf(part) + 1}";
            if (part.Unresolved != "")
            {
                dia.UnbuiltParts.Add(pn);
                Warn($"UNRESOLVED: {pn} not built - {part.Unresolved}");
                continue;
            }
            var le = part.Left ?? dd.Left; var re = part.Right ?? dd.Right;
            if (le == null || re == null) { Warn($"{pn}: no left / right end given - not built."); dia.UnbuiltParts.Add(pn); continue; }
            try
            {
                var mesh = new Mesh();
                var used = BuildPrism(k, part.Section, le, re, ends, mesh, cutBeams: true, sup.Type == SupportType.Abutment);
                double vol = Math.Abs(mesh.Volume());
                var xsec = part.Section.Select(p => p.X).ToList();
                dia.Parts.Add(new ConcreteDiaphragmPart(pn, mesh, vol, xsec.Min(), xsec.Max()));
                dia.Mesh.Append(mesh);
                if (used.Count > 0) Info($"{pn}: beams embedded {string.Join(", ", used.Select(e => e.Beam.Mark))}.");
            }
            catch (InvalidOperationException ex) { Warn($"{pn} not built - {ex.Message}"); dia.UnbuiltParts.Add(pn); }
        }
        if (dia.Parts.Count == 0 && dia.UnbuiltParts.Count == 0) Warn("no parts defined.");

        // Length at x = 0.
        if (dd.Left != null && dd.Right != null)
        {
            try
            {
                var anyEnds = ends;
                dia.Length = EndS(k, dd.Right, 0, anyEnds, right: true) - EndS(k, dd.Left, 0, anyEnds, right: false);
            }
            catch (InvalidOperationException) { }
        }

        // ---- checks: embedment, bearing gap, deck end vs back face
        var built = dia.Parts;
        foreach (var e in ends)
        {
            bool inside = built.Any(p => e.Xd > p.XMin - 1e-6 && e.Xd < p.XMax + 1e-6);
            if (!inside && built.Count > 0)
                Warn($"{e.Beam.Mark}: beam end (x {L(e.Xd)}) is not inside a built part of the diaphragm" + (dia.UnbuiltParts.Count > 0 ? $" (unbuilt: {string.Join(", ", dia.UnbuiltParts)})." : "."));
            if (!double.IsNaN(dd.PlanBearingGap) && k.Steps.Count > 0)
            {
                var brg = e.AtStart ? e.Beam.BearingStart : e.Beam.BearingEnd;
                double gap = brg.Z - e.D - k.BottomAt(k.S(brg.XY));
                if (Math.Abs(gap - dd.PlanBearingGap) > In(1.0 / 16))
                    Warn($"{e.Beam.Mark}: beam bottom at the CL bearing is {L(gap)} above the diaphragm bottom step (plan bearing {L(dd.PlanBearingGap)}) - model seat / beam elevation differs from the plan step.");
            }
        }
        if (sup.Type == SupportType.Abutment && built.Count > 0)
        {
            double back = built.Min(p => p.XMin);
            Info($"deck end {L(sup.DeckEndOffset)} from CL brg (model input, normal to the support) vs diaphragm back-most face {L(-back)} behind CL brg.");
        }

        // ---- cast holes through the beam webs (plan table) + strand clearance
        foreach (var e in ends)
            foreach (var h in dd.BeamHoles)
            {
                double s = h.FromEnd;                    // along the CL beam (plan length ~ along the beam; slope < 3%)
                var c3 = e.AtStart ? e.At(s, 0, h.AboveBottom) : e.At(-s, 0, h.AboveBottom);
                var axis = new Vec3(e.Right.X, e.Right.Y, 0);
                bool inPart = built.Any(p => { double x = k.X(c3.XY); return x > p.XMin && x < p.XMax; });
                double clr = double.PositiveInfinity;
                foreach (var st in e.Beam.Strands)
                    clr = Math.Min(clr, Vec2.Distance(st.Section, new Vec2(0, h.AboveBottom)) - h.Diameter / 2 - e.Beam.StrandDiameter / 2);
                dia.Holes.Add(new DiaphragmHoleResult(e.Beam.Mark, c3, axis, h.Diameter, inPart, clr));
            }
        if (dia.Holes.Count > 0)
        {
            int outside = dia.Holes.Count(h => !h.InsideBuiltPart);
            Info($"{dia.Holes.Count} cast web holes ({string.Join(", ", dd.BeamHoles.Select(h => $"{L(h.Diameter)} dia {L(h.FromEnd)} from end, {L(h.AboveBottom)} above bottom"))}); " +
                 $"min clearance to strands {L(dia.Holes.Min(h => h.StrandClearance))}" + (outside > 0 ? $"; {outside} of them are outside the BUILT parts (the part holding them is unbuilt)." : "."));
        }

        foreach (var r in dd.Unresolved) Warn("UNRESOLVED: " + r);
        if (assumption != null) Warn($"UNRESOLVED: {assumption.Reason} - {assumption.Basis}.");

        dia.Description = $"{(dd.Kind == ConcreteDiaphragmKind.PierContinuity ? "Pier continuity" : dd.Kind == ConcreteDiaphragmKind.IntegralEnd ? "Integral abutment" : "Semi-integral end")} diaphragm, " +
                          $"{dia.Parts.Count} part(s), {ends.Count} beam ends embedded" + (dd.Source != "" ? $" ({dd.Source})" : "");
        if (dia.Parts.Count > 0) model.Diaphragms.Add(dia);
        if (dia.Parts.Count > 0)
            Info($"{dia.Parts.Count} part(s) built, concrete {dia.Volume / Math.Pow(Ft(1), 3) / 27:0.00} CY ({string.Join(", ", dia.Parts.Select(p => $"{p.Name} {p.Volume / Math.Pow(Ft(1), 3) / 27:0.00}"))}), length {u.FormatLength(dia.Length)} at the CL.");

        // ---- fillers and keyways (thin solids)
        foreach (var fd in dd.Fillers)
        {
            try
            {
                var mesh = BuildFiller(k, dd, fd, ends, ps, sup, built);
                if (mesh.IsEmpty) { Warn($"filler {fd.Name}: nothing to build."); continue; }
                model.Accessories.Add(new DiaphragmAccessory
                {
                    Name = $"{sup.Name} {fd.Name}", Support = sup.Name, Kind = DiaphragmAccessoryKind.Filler, Material = fd.Material, Mesh = mesh,
                    Description = $"{fd.Material}{(fd.Thickness > 0 ? ", " + L(fd.Thickness) : "")}{(fd.Source != "" ? " (" + fd.Source + ")" : "")}",
                    Assumption = assumption != null && fd.Kind != DiaphragmFillerKind.EndFace ? assumption : null,
                });
            }
            catch (InvalidOperationException ex) { Warn($"filler {fd.Name} not built - {ex.Message}"); }
        }
        foreach (var kd in dd.Keyways)
        {
            try
            {
                var mesh = BuildKeyway(k, dd, kd, ends);
                if (mesh.IsEmpty) { Warn($"keyway {kd.Name}: nothing to build."); continue; }
                var ka = kd.Unresolved != "" ? Assumption.Unresolved(kd.Unresolved, kd.Basis) : assumption;
                if (kd.Unresolved != "") Warn($"UNRESOLVED: keyway {kd.Name}: {kd.Unresolved} - {kd.Basis}.");
                model.Accessories.Add(new DiaphragmAccessory
                {
                    Name = $"{sup.Name} {kd.Name}", Support = sup.Name, Kind = DiaphragmAccessoryKind.Keyway, Material = dd.Material, Mesh = mesh, Assumption = ka,
                    Description = $"Keyway {L(kd.Width)} x {L(kd.Depth)} in the substructure top, filled by the diaphragm pour{(kd.Source != "" ? " (" + kd.Source + ")" : "")}",
                });
            }
            catch (InvalidOperationException ex) { Warn($"keyway {kd.Name} not built - {ex.Message}"); }
        }
    }

    // ------------------------------------------------------------------------------------------------ prism builder

    /// <summary>S of an end at slice x.</summary>
    private static double EndS(Frame k, DiaphragmEnd e, double x, IReadOnlyList<BeamEnd> beams, bool right)
    {
        switch (e.Kind)
        {
            case DiaphragmEndKind.Offset: return k.SAtOffset(e.Offset, x);
            case DiaphragmEndKind.AlongLineFromOffset:
            {
                double s0 = k.SAtOffset(e.Offset, 0) + (right ? 1 : -1) * e.Distance;
                double o = k.OffsetAt(s0, 0);
                return k.SAtOffset(o, x);
            }
            default:
            {
                if (beams.Count == 0) throw new InvalidOperationException("an end flush with the fascia beam needs beams in the part");
                var vals = beams.SelectMany(b => new[] { b.Map(k, x, b.Hw, b.D).S, b.Map(k, x, -b.Hw, b.D).S });
                return right ? vals.Max() : vals.Min();
            }
        }
    }

    private sealed record Edge(DiaphragmZRef Ref, double X0, double Dz0, double X1, double Dz1)
    {
        public double DzAt(double x) => Math.Abs(X1 - X0) < 1e-12 ? Dz0 : Dz0 + (Dz1 - Dz0) * (x - X0) / (X1 - X0);
    }

    /// <summary>Builds a section swept between two ends, beams cut out; returns the beam ends that were cut.</summary>
    private static List<BeamEnd> BuildPrism(Frame k, IReadOnlyList<DiaphragmSectionPoint> section, DiaphragmEnd left, DiaphragmEnd right,
        IReadOnlyList<BeamEnd> allEnds, Mesh mesh, bool cutBeams, bool abutment)
    {
        if (section.Count < 3) throw new InvalidOperationException("section needs at least 3 points");
        var pts = section.ToList();
        // Orientation from approximate elevations (references at S = 0).
        var approx = pts.Select(p => new Vec2(p.X, k.Z(p.Ref, p.Dz, 0, p.X))).ToList();
        if (MeshBuilder.SignedArea(approx) < 0) pts.Reverse();
        int n = pts.Count;
        var lower = new List<Edge>();
        var upper = new List<Edge>();
        for (int i = 0; i < n; i++)
        {
            var a = pts[i]; var b = pts[(i + 1) % n];
            if (Math.Abs(b.X - a.X) < 1e-9) continue;
            if (a.Ref != b.Ref) throw new InvalidOperationException($"section edge x {a.X:0.###}..{b.X:0.###} m mixes references {a.Ref}/{b.Ref} (only vertical edges may)");
            var e = b.X > a.X ? new Edge(a.Ref, a.X, a.Dz, b.X, b.Dz) : new Edge(b.Ref, b.X, b.Dz, a.X, a.Dz);
            (b.X > a.X ? lower : upper).Add(e);
        }
        double xmin = pts.Min(p => p.X), xmax = pts.Max(p => p.X);
        var cuts = pts.Select(p => p.X).ToList();
        var beams = cutBeams ? allEnds.Where(e => e.Sx > 0 ? xmax > e.Xd + 1e-6 : xmin < e.Xd - 1e-6).ToList() : new List<BeamEnd>();
        foreach (var b in beams) if (b.Xd > xmin + 1e-6 && b.Xd < xmax - 1e-6) cuts.Add(b.Xd);
        cuts = cuts.Distinct().OrderBy(x => x).ToList();
        var merged = new List<double>();
        foreach (var c in cuts) if (merged.Count == 0 || c - merged[^1] > 1e-6) merged.Add(c);
        var usedBeams = new HashSet<BeamEnd>();
        var fasciaBeams = beams;   // the part's beams (present anywhere in it) define a fascia end
        for (int i = 0; i + 1 < merged.Count; i++)
        {
            double xa = merged[i], xb = merged[i + 1], xm = (xa + xb) / 2;
            var lo = lower.FirstOrDefault(e => e.X0 <= xm && e.X1 >= xm) ?? throw new InvalidOperationException($"no lower edge at x {xm:0.###} m (section not vertically convex?)");
            var up = upper.FirstOrDefault(e => e.X0 <= xm && e.X1 >= xm) ?? throw new InvalidOperationException($"no upper edge at x {xm:0.###} m");
            var present = beams.Where(b => b.PresentAt(xm)).ToList();
            foreach (var b in present) usedBeams.Add(b);
            var plan = SlicePlan(k, xm, lo, up, left, right, present, fasciaBeams);
            var r0 = Slice(k, xa, lo, up, left, right, present, fasciaBeams, plan);
            var r1 = Slice(k, xb, lo, up, left, right, present, fasciaBeams, plan);
            if (r0.Count != r1.Count) throw new InvalidOperationException($"slice topology changed in x {xa:0.###}..{xb:0.###} m");
            var sec = r0.Select(p => new Vec2(p.S, p.Z)).ToList();
            var ring0 = r0.Select(p => k.W(p.S, xa).WithZ(p.Z)).ToArray();
            var ring1 = r1.Select(p => k.W(p.S, xb).WithZ(p.Z)).ToArray();
            MeshBuilder.AddLoft(mesh, sec, new[] { ring0, ring1 });
        }
        return usedBeams.ToList();
    }

    /// <summary>Fixed parametrization of a slab's slices (sample counts, kink events) so both end slices share one topology.</summary>
    private sealed record Plan(int[] TopSamples, int[] BottomSamples, List<double> TopKinks, List<double> BottomKinks, List<int> Order);

    private static double RefZ(Frame k, Edge e, double s, double x) => k.Z(e.Ref, e.DzAt(x), s, x);

    private static Plan SlicePlan(Frame k, double x, Edge lo, Edge up, DiaphragmEnd left, DiaphragmEnd right, List<BeamEnd> present, List<BeamEnd> fascia)
    {
        double sl = EndS(k, left, x, fascia, false), sr = EndS(k, right, x, fascia, true);
        if (sr - sl < 1e-3) throw new InvalidOperationException($"ends cross at x {x:0.###} m");
        // voids sorted by S descending (top edge is walked right to left)
        var order = Enumerable.Range(0, present.Count).OrderByDescending(i => present[i].Map(k, x, 0, present[i].D).S).ToList();
        double sta = k.Al.Inverse(k.W((sl + sr) / 2, x)).Station;
        var kinkS = k.KinkOffsets(sta).Select(o => k.SAtOffset(o, x)).Where(s => s > sl + 1e-4 && s < sr - 1e-4).ToList();
        bool Smooth(DiaphragmZRef r) => r is DiaphragmZRef.DeckTop or DiaphragmZRef.Soffit;
        var topK = Smooth(up.Ref) ? kinkS.ToList() : new List<double>();
        var botK = Smooth(lo.Ref) ? kinkS.ToList() : new List<double>();
        // top segments between events (right end, voids, left end) and kinks
        var ev = TopEvents(k, x, sl, sr, present, order, topK);
        var topN = new int[ev.Count - 1];
        for (int i = 0; i + 1 < ev.Count; i++) topN[i] = Smooth(up.Ref) && ev[i].kind != 1 ? Math.Max(0, (int)Math.Ceiling(Math.Abs(ev[i].s - ev[i + 1].s) / Sample) - 1) : 0;
        var bev = BottomEvents(k, sl, sr, lo, botK);
        var botN = new int[bev.Count - 1];
        for (int i = 0; i + 1 < bev.Count; i++) botN[i] = Smooth(lo.Ref) ? Math.Max(0, (int)Math.Ceiling(Math.Abs(bev[i + 1] - bev[i]) / Sample) - 1) : 0;
        return new Plan(topN, botN, topK.Select(s => k.OffsetAt(s, x)).ToList(), botK.Select(s => k.OffsetAt(s, x)).ToList(), order);
    }

    /// <summary>Top-edge break points from right to left: (S, kind) with kind 0 = end/kink point, 1 = void start (right side), 2 = void end.</summary>
    private static List<(double s, int kind, int beam)> TopEvents(Frame k, double x, double sl, double sr, List<BeamEnd> present, List<int> order, List<double> kinks)
    {
        var ev = new List<(double s, int kind, int beam)> { (sr, 0, -1) };
        foreach (int i in order)
        {
            var b = present[i];
            double a = b.Map(k, x, b.Hw, b.D).S, c = b.Map(k, x, -b.Hw, b.D).S;
            ev.Add((Math.Max(a, c), 1, i));
            ev.Add((Math.Min(a, c), 2, i));
        }
        foreach (var s in kinks) ev.Add((s, 3, -1));
        ev.Add((sl, 0, -1));
        // keep the void pairs together; kinks inside a void are dropped
        var res = new List<(double s, int kind, int beam)>();
        var sorted = ev.OrderByDescending(e => e.s).ThenBy(e => e.kind == 2 ? 0 : 1).ToList();
        bool inVoid = false;
        foreach (var e in sorted)
        {
            if (e.kind == 1) inVoid = true;
            if (e.kind == 3 && inVoid) continue;
            if (e.kind == 2) inVoid = false;
            res.Add(e);
        }
        return res;
    }

    private static List<double> BottomEvents(Frame k, double sl, double sr, Edge lo, List<double> kinks)
    {
        var ev = new List<double> { sl };
        if (lo.Ref == DiaphragmZRef.Bottom) ev.AddRange(k.Risers.Where(r => r > sl + 1e-6 && r < sr - 1e-6));
        ev.AddRange(kinks);
        ev.Add(sr);
        return ev.OrderBy(s => s).ToList();
    }

    /// <summary>The slice polygon at x (CCW in S-z): bottom left→right, right end up, top right→left with beam notches, left end down.</summary>
    private static List<(double S, double Z)> Slice(Frame k, double x, Edge lo, Edge up, DiaphragmEnd left, DiaphragmEnd right,
        List<BeamEnd> present, List<BeamEnd> fascia, Plan plan)
    {
        double sl = EndS(k, left, x, fascia, false), sr = EndS(k, right, x, fascia, true);
        var poly = new List<(double S, double Z)>();
        double Top(double s) => RefZ(k, up, s, x);
        double Low(double s) => RefZ(k, lo, s, x);

        // ---- bottom (left to right), risers as vertical jumps
        var bk = plan.BottomKinks.Select(o => Math.Clamp(k.SAtOffset(o, x), sl, sr)).ToList();
        var bev = BottomEvents(k, sl, sr, lo, bk);
        if (bev.Count - 1 != plan.BottomSamples.Length) throw new InvalidOperationException("bottom topology changed (a riser / kink crosses an end inside a slab)");
        for (int i = 0; i + 1 < bev.Count; i++)
        {
            double a = bev[i], b = bev[i + 1];
            double zA = lo.Ref == DiaphragmZRef.Bottom ? k.Z(lo.Ref, lo.DzAt(x), (a + b) / 2, x) : Low(a);
            poly.Add((a, zA));
            int m = plan.BottomSamples[i];
            for (int q = 1; q <= m; q++) { double s = a + (b - a) * q / (m + 1); poly.Add((s, Low(s))); }
            if (lo.Ref == DiaphragmZRef.Bottom) poly.Add((b, zA));   // level step to the riser / end
        }
        if (lo.Ref != DiaphragmZRef.Bottom) poly.Add((sr, Low(sr)));

        // ---- top (right to left)
        var tk = plan.TopKinks.Select(o => k.SAtOffset(o, x)).ToList();
        var ev = TopEvents(k, x, sl, sr, present, plan.Order, tk);
        if (ev.Count - 1 != plan.TopSamples.Length) throw new InvalidOperationException("top topology changed (a kink crosses an end or a beam inside a slab)");
        for (int i = 0; i < ev.Count; i++)
        {
            var e = ev[i];
            if (e.kind == 1)
            {
                // notch: walk the beam void clockwise from its top-right to its top-left, clipped to below the top line
                poly.AddRange(Notch(k, x, present[e.beam], Top, Low));
            }
            else if (e.kind != 2) poly.Add((e.s, Top(e.s)));
            if (i + 1 < ev.Count && e.kind != 1)
            {
                int m = plan.TopSamples[i];
                double a = e.s, b = ev[i + 1].s;
                for (int q = 1; q <= m; q++) { double s = a + (b - a) * q / (m + 1); poly.Add((s, Top(s))); }
            }
            else if (i + 1 < ev.Count && e.kind == 1 && plan.TopSamples[i] != 0) throw new InvalidOperationException("sample inside a beam void");
        }
        // drop consecutive duplicates
        var clean = new List<(double S, double Z)>();
        foreach (var p in poly)
            if (clean.Count == 0 || Math.Abs(p.S - clean[^1].S) > 1e-9 || Math.Abs(p.Z - clean[^1].Z) > 1e-9) clean.Add(p);
        if (clean.Count > 1 && Math.Abs(clean[0].S - clean[^1].S) < 1e-9 && Math.Abs(clean[0].Z - clean[^1].Z) < 1e-9) clean.RemoveAt(clean.Count - 1);
        return clean;
    }

    private static List<(double S, double Z)> Notch(Frame k, double x, BeamEnd b, Func<double, double> top, Func<double, double> low)
    {
        var v = b.Void.Select(p => b.Map(k, x, p.X, p.Y)).ToList();
        double area = 0;
        for (int i = 0; i < v.Count; i++) { var p = v[i]; var q = v[(i + 1) % v.Count]; area += p.S * q.Z - q.S * p.Z; }
        if (area < 0) v.Reverse();
        // the two extension vertices (z ~ Big): top-right = larger S; CCW goes top-right -> top-left
        int n = v.Count;
        var ext = Enumerable.Range(0, n).Where(i => v[i].Z > b.Top.Z + Big / 2).ToList();
        if (ext.Count != 2) throw new InvalidOperationException($"{b.Beam.Mark}: beam void has no haunch extension");
        int r = v[ext[0]].S > v[ext[1]].S ? ext[0] : ext[1];
        // clockwise chain from r back to l
        var chain = new List<(double S, double Z)>();
        for (int q = 0; q < n; q++)
        {
            var p = v[(r - q + n) % n];
            chain.Add(p);
            if (q > 0 && p.Z > b.Top.Z + Big / 2) break;
        }
        // clip to below the top line
        double F((double S, double Z) p) => p.Z - top(p.S);
        (double S, double Z) Cross((double S, double Z) a, (double S, double Z) c)
        {
            double t0 = 0, t1 = 1;
            for (int it = 0; it < 60; it++)
            {
                double tm = (t0 + t1) / 2;
                var pm = (a.S + (c.S - a.S) * tm, a.Z + (c.Z - a.Z) * tm);
                if ((F(pm) > 0) == (F(a) > 0)) t0 = tm; else t1 = tm;
            }
            double t = (t0 + t1) / 2;
            double s = a.S + (c.S - a.S) * t;
            return (s, top(s));
        }
        int first = -1, last = -1;
        for (int i = 0; i + 1 < chain.Count; i++) if (F(chain[i]) > 0 && F(chain[i + 1]) <= 0) { first = i; break; }
        for (int i = chain.Count - 2; i >= 0; i--) if (F(chain[i]) <= 0 && F(chain[i + 1]) > 0) { last = i; break; }
        if (first < 0 || last < 0 || last < first) throw new InvalidOperationException($"{b.Beam.Mark}: beam does not reach below the top of the part");
        var res = new List<(double S, double Z)> { Cross(chain[first], chain[first + 1]) };
        for (int i = first + 1; i <= last; i++)
        {
            if (chain[i].Z < low(chain[i].S) + 1e-6)
                throw new InvalidOperationException($"{b.Beam.Mark}: beam bottom is at or below the part bottom (cannot embed it; check the bottom steps / beam seat)");
            res.Add(chain[i]);
        }
        res.Add(Cross(chain[last], chain[last + 1]));
        return res;
    }

    // ------------------------------------------------------------------------------------------------ fillers and keyways

    private static Mesh BuildFiller(Frame k, ConcreteDiaphragmDefinition dd, DiaphragmFillerDefinition fd, List<BeamEnd> ends, PrestressedModel ps,
        SupportDefinition sup, List<ConcreteDiaphragmPart> built)
    {
        var mesh = new Mesh();
        switch (fd.Kind)
        {
            case DiaphragmFillerKind.Section:
                if (dd.Left == null || dd.Right == null) throw new InvalidOperationException("needs the diaphragm ends");
                BuildPrism(k, fd.Section, dd.Left, dd.Right, ends, mesh, cutBeams: true, sup.Type == SupportType.Abutment);
                break;
            case DiaphragmFillerKind.EndFace:
            {
                if (fd.Side == 0 || fd.Thickness <= 0) throw new InvalidOperationException("EndFace filler needs Side -1 / +1 and a thickness");
                foreach (var part in dd.Parts.Where(p => p.Unresolved == "" && built.Any(b => b.Name == (p.Name != "" ? p.Name : b.Name))))
                {
                    var end = fd.Side < 0 ? part.Left ?? dd.Left : part.Right ?? dd.Right;
                    if (end == null) continue;
                    if (end.Kind == DiaphragmEndKind.FasciaOutside) throw new InvalidOperationException("an end-face filler needs an offset end (not a fascia end)");
                    double o = end.Kind == DiaphragmEndKind.Offset ? end.Offset : k.OffsetAt(EndS(k, end, 0, ends, fd.Side > 0), 0);
                    // thickness normal to the end face (an offset line): offsets are normal distances
                    var inner = DiaphragmEnd.AtOffset(o);
                    var outer = DiaphragmEnd.AtOffset(o + fd.Side * fd.Thickness);
                    BuildPrism(k, part.Section, fd.Side < 0 ? outer : inner, fd.Side < 0 ? inner : outer, ends, mesh, cutBeams: false, sup.Type == SupportType.Abutment);
                }
                break;
            }
            case DiaphragmFillerKind.AroundBearings:
                foreach (var e in ends) AroundBearing(k, e, ps, sup, built, mesh);
                break;
        }
        return mesh;
    }

    /// <summary>Gap under a beam end inside the diaphragm (bottom flange footprint between the end face and the diaphragm face) minus the pad.</summary>
    private static void AroundBearing(Frame k, BeamEnd e, PrestressedModel ps, SupportDefinition sup, List<ConcreteDiaphragmPart> built, Mesh mesh)
    {
        var part = built.FirstOrDefault(p => e.Xd > p.XMin - 1e-6 && e.Xd < p.XMax + 1e-6);
        if (part == null) return;
        double xFace = e.Sx > 0 ? part.XMax : part.XMin;
        // plan coordinates in the beam frame: u from the CL end point into the beam, v right of the CL (beam "right")
        var into = e.Into;
        Vec2 P(double uu, double vv) => e.Top.XY + into * uu + into.Right * vv;
        double xRate = k.Xs * Vec2.Dot(into, k.N);    // dx/du
        double vRate = k.Xs * Vec2.Dot(into.Right, k.N);  // dx/dv
        double UAtX(double x, double vv) => (x - e.Xd - vRate * vv) / xRate;
        double hb = e.Hb;
        var pad = ps.Pads.FirstOrDefault(p => p.Girder == e.Beam.Girder && p.Span == e.Beam.Span && p.Support == sup.Name);
        var pieces = new List<List<Vec2>>();
        var foot = new List<Vec2> { new(UAtX(e.Xd, -hb), -hb), new(UAtX(xFace, -hb), -hb), new(UAtX(xFace, hb), hb), new(UAtX(e.Xd, hb), hb) };
        if (pad == null) pieces.Add(foot);
        else
        {
            double uc = Vec2.Dot(pad.Center.XY - e.Top.XY, into), vc = Vec2.Dot(pad.Center.XY - e.Top.XY, into.Right);
            double u0 = uc - pad.Length / 2, u1 = uc + pad.Length / 2, v0 = vc - pad.Width / 2, v1 = vc + pad.Width / 2;
            // footprint minus the pad rectangle = up to 4 pieces (before, after, beside left / right)
            pieces.Add(ClipU(foot, double.NegativeInfinity, u0));
            pieces.Add(ClipU(foot, u1, double.PositiveInfinity));
            var mid = ClipU(foot, u0, u1);
            pieces.Add(ClipV(mid, double.NegativeInfinity, v0));
            pieces.Add(ClipV(mid, v1, double.PositiveInfinity));
        }
        double zBot = k.BottomAt(k.S(e.Top.XY));
        foreach (var pc in pieces)
        {
            if (pc.Count < 3 || Math.Abs(MeshBuilder.SignedArea(pc)) < 1e-8) continue;
            var poly = MeshBuilder.SignedArea(pc) < 0 ? Enumerable.Reverse(pc).ToList() : pc;
            var bot = poly.Select(q => P(q.X, q.Y).WithZ(zBot)).ToArray();
            var top = poly.Select(q =>
            {
                double s = Vec2.Dot(P(q.X, q.Y) - e.Top.XY, e.T);           // along the axis from the CL end point (start->end sense)
                return P(q.X, q.Y).WithZ(e.Top.Z + e.Beam.Slope * s - e.D);
            }).ToArray();
            if (top.Zip(bot).Any(t => t.First.Z - t.Second.Z < 1e-5)) continue;
            MeshBuilder.AddLoft(mesh, poly, new[] { bot, top });
        }
    }

    private static List<Vec2> ClipU(List<Vec2> poly, double lo, double hi) => ClipHalf(ClipHalf(poly, p => p.X - lo), p => hi - p.X);
    private static List<Vec2> ClipV(List<Vec2> poly, double lo, double hi) => ClipHalf(ClipHalf(poly, p => p.Y - lo), p => hi - p.Y);

    /// <summary>Sutherland-Hodgman: keep f >= 0.</summary>
    private static List<Vec2> ClipHalf(List<Vec2> poly, Func<Vec2, double> f)
    {
        var res = new List<Vec2>();
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % poly.Count];
            double fa = f(a), fb = f(b);
            if (double.IsInfinity(fa) || double.IsNaN(fa)) fa = 1;
            if (double.IsInfinity(fb) || double.IsNaN(fb)) fb = 1;
            if (fa >= 0) res.Add(a);
            if ((fa >= 0) != (fb >= 0)) res.Add(a + (b - a) * (fa / (fa - fb)));
        }
        return res;
    }

    private static Mesh BuildKeyway(Frame k, ConcreteDiaphragmDefinition dd, DiaphragmKeywayDefinition kd, List<BeamEnd> ends)
    {
        if (kd.Width <= 0 || kd.Depth <= 0) throw new InvalidOperationException("keyway width / depth not given");
        if (k.Steps.Count == 0) throw new InvalidOperationException("keyway needs the BottomSteps (it is cut into the substructure top)");
        var mesh = new Mesh();
        double x0 = kd.X - kd.Width / 2, x1 = kd.X + kd.Width / 2;
        // S intervals at each x edge: between beams (bottom flange edges of the beam lines, extended) or the diaphragm ends
        List<(double a, double b)> Intervals(double x)
        {
            var list = new List<(double a, double b)>();
            if (kd.BetweenBeams)
            {
                // one line per girder: the beam end nearest this x (back or ahead) gives the line
                var lines = ends.GroupBy(e => e.Beam.Girder).Select(g => g.OrderBy(e => Math.Abs(e.Xd - x)).First())
                    .Select(e => { double a = e.Map(k, x, e.Hb, 0).S, c = e.Map(k, x, -e.Hb, 0).S; return (lo: Math.Min(a, c), hi: Math.Max(a, c)); })
                    .OrderBy(t => t.lo).ToList();
                for (int i = 0; i + 1 < lines.Count; i++) list.Add((lines[i].hi, lines[i + 1].lo));
            }
            else
            {
                if (dd.Left == null || dd.Right == null) throw new InvalidOperationException("needs the diaphragm ends");
                list.Add((EndS(k, dd.Left, x, ends, false), EndS(k, dd.Right, x, ends, true)));
            }
            return list;
        }
        var i0 = Intervals(x0); var i1 = Intervals(x1);
        if (i0.Count != i1.Count) throw new InvalidOperationException("keyway bays differ across its width");
        for (int b = 0; b < i0.Count; b++)
        {
            // split at risers (level pieces)
            double aMid = (i0[b].a + i1[b].a) / 2, bMid = (i0[b].b + i1[b].b) / 2;
            var cuts = new List<double> { 0 };
            foreach (var r in k.Risers.Where(r => r > aMid && r < bMid)) cuts.Add((r - aMid) / (bMid - aMid));
            cuts.Add(1);
            for (int c = 0; c + 1 < cuts.Count; c++)
            {
                double Sx(double x, double t) { var iv = x == x0 ? i0[b] : i1[b]; return iv.a + (iv.b - iv.a) * t; }
                // risers are at constant S: piece edges at a riser use the riser S on both sides
                double SA(double x) => c == 0 ? Sx(x, 0) : k.Risers.Where(r => r > aMid && r < bMid).ElementAt(c - 1);
                double SB(double x) => c + 2 == cuts.Count ? Sx(x, 1) : k.Risers.Where(r => r > aMid && r < bMid).ElementAt(c);
                double z = k.BottomAt((SA(x0) + SB(x0)) / 2);
                var quad = new List<Vec2> { new(SA(x0), x0), new(SB(x0), x0), new(SB(x1), x1), new(SA(x1), x1) };
                if (MeshBuilder.SignedArea(quad) < 0) quad.Reverse();
                if (Math.Abs(MeshBuilder.SignedArea(quad)) < 1e-8) continue;
                var bot = quad.Select(q => k.W(q.X, q.Y).WithZ(z - kd.Depth)).ToArray();
                var top = quad.Select(q => k.W(q.X, q.Y).WithZ(z)).ToArray();
                MeshBuilder.AddLoft(mesh, quad, new[] { bot, top });
            }
        }
        return mesh;
    }
}
