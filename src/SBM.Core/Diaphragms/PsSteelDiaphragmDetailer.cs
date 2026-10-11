using SBM.Core.Bridge;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Prestressed;
using SBM.Core.Solids;
using SBM.Core.Steel;
using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Diaphragms;

/// <summary>
/// Geometry of one prestressed beam as the steel diaphragm detailer needs it (pure input; no dependency on how the beam
/// was generated). <see cref="BottomStart"/> = bottom of the beam on the CL web at the beam end at the LOWER station (the
/// origin of the plan's hole chain), <see cref="BottomEnd"/> = the same at the other end. The web is plumb.
/// </summary>
public sealed record PsDiaphragmBeamLine(int Span, int Girder, Vec3 BottomStart, Vec3 BottomEnd, double WebThickness)
{
    /// <summary>From a generated beam (0-based Span/Girder in the model, 1-based here). TopStart is the end at the
    /// lower station; the bottom is the top minus the shape depth (plumb web).</summary>
    public static PsDiaphragmBeamLine FromPrestressed(PrestressedBeam b, double depth, double webThickness) =>
        new(b.Span + 1, b.Girder + 1, b.TopStart - new Vec3(0, 0, depth), b.TopEnd - new Vec3(0, 0, depth), webThickness);

    public Vec3 Axis => (BottomEnd - BottomStart).Normalized();
    public double Length => (BottomEnd - BottomStart).Length;
}

/// <summary>One cast hole location (two 1 1/2" holes, lower and upper) in a beam web.</summary>
public sealed class PsDiaphragmCastHole
{
    public required int Span { get; init; }
    public required int Girder { get; init; }
    /// <summary>1-based index in the beam's chain.</summary>
    public required int Index { get; init; }
    public required double FromBeamEnd { get; init; }
    /// <summary>Hole centres on the CL web.</summary>
    public required Vec3 Lower { get; init; }
    public required Vec3 Upper { get; init; }
    /// <summary>Hole axis (horizontal web normal) and length (web thickness).</summary>
    public required Vec3 Axis { get; init; }
    public required double Diameter { get; init; }
    public required double Length { get; init; }
}

/// <summary>Bolt fit at one end of a diaphragm: offsets of the bolts from the slot centres (m).</summary>
public sealed class PsDiaphragmEndFit
{
    /// <summary>Largest offset along the horizontal 1" x 2" slots of the 9" leg, and its take-up (±(2 - 1)/2).</summary>
    public required double PlateSlotOffset { get; init; }
    public required double PlateSlotCapacity { get; init; }
    /// <summary>Largest offset along the vertical 1" x 1 1/2" member slots, and its take-up (±(1 1/2 - 1)/2).</summary>
    public required double MemberSlotOffset { get; init; }
    public required double MemberSlotCapacity { get; init; }
    /// <summary>Gap between the 9" leg face and the member web (plates square to the beam, member hole to hole).</summary>
    public required double OutOfPlane { get; init; }
    public bool Fits => PlateSlotOffset <= PlateSlotCapacity + 1e-9 && MemberSlotOffset <= MemberSlotCapacity + 1e-9;
}

/// <summary>Result for one bay (diaphragm).</summary>
public sealed class PsDiaphragmBayResult
{
    public required string Mark { get; init; }
    public required PsDiaphragmBay Bay { get; init; }
    /// <summary>Member length used (plan length when given) and from the geometry (slot centres + 2 x 1 5/8).</summary>
    public double Length { get; init; }
    public double GeometricLength { get; init; }
    /// <summary>Right minus left plate bolt-group centre elevation.</summary>
    public double HeightDifference { get; init; }
    /// <summary>Member slope (rise / run) as set (0 when level).</summary>
    public double MemberSlope { get; init; }
    /// <summary>Plan angle of the member off square to the left beam (rad).</summary>
    public double OffSquare { get; init; }
    public PsDiaphragmEndFit? LeftFit { get; init; }
    public PsDiaphragmEndFit? RightFit { get; init; }
    public bool Fits => LeftFit is { Fits: true } && RightFit is { Fits: true };
    /// <summary>Main member weight from the AISC nominal weight x length (lb), as the plan table.</summary>
    public double MemberWeightLb { get; init; }
    public double LowerWeightLb { get; init; }
    /// <summary>True when steel parts were produced (false: unresolved input or error).</summary>
    public bool Detailed { get; init; }
}

/// <summary>Detailed steel intermediate diaphragms (parts, bolts, cast holes, checks). Hooked into the bridge by T6.</summary>
public sealed class PsSteelDiaphragmModel
{
    public List<SteelAssembly> Assemblies { get; } = new();
    public List<SteelPart> Parts { get; } = new();
    /// <summary>Bolts; <see cref="BoltInstance.Diameter"/> is set on every bolt (7/8").</summary>
    public List<BoltInstance> Bolts { get; } = new();
    public List<PsDiaphragmCastHole> Holes { get; } = new();
    public List<PsDiaphragmBayResult> Bays { get; } = new();
    public List<Diagnostic> Diagnostics { get; } = new();

    public const double KgPerLb = 0.45359237;
    public double SteelWeightLb => Parts.Sum(p => p.Mass) / KgPerLb;
}

/// <summary>
/// Iowa 1036-BTD steel intermediate diaphragms between prestressed beams (feature D / T4 of
/// docs/plan-data/concrete-bridge-design.md). Pure function: beam geometry + definition in, steel out. Per bay: bent plate
/// (9" leg toward the bay, 6" leg on the web face) and backing plate on the far web face, 2 web bolts through the cast
/// holes, member (C15x33.9 or W21x50 web against the 9" legs, bolt group centred on the member depth: p.32 "a"
/// dimensions equal), 4 bolts per end through the 1" x 2" leg slots and the 1" x 1 1/2" member slots; optional lower W
/// hung flange to flange under the W main member (18 bolts, end plates, shims).
/// </summary>
public static class PsSteelDiaphragmDetailer
{
    private static readonly Vec3 Up = new(0, 0, 1);

    public static PsSteelDiaphragmModel Detail(PsSteelDiaphragmDefinition def, IReadOnlyList<PsDiaphragmBeamLine> beams)
    {
        var m = new PsSteelDiaphragmModel();
        var c = def.Connection;
        void Warn(string s) => m.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, s));
        void Error(string s) => m.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, s));
        void Info(string s) => m.Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info, s));
        string Line(int g) => g - 1 < def.BeamLineNames.Count && g >= 1 ? def.BeamLineNames[g - 1] : g.ToString();

        if (Math.Abs(c.PlateLength - (c.HolePitch + 2 * c.PlateEndDistance)) > In(1.0 / 64))
            Warn($"Steel diaphragm: plate length {c.PlateLength / In(1):0.###}\" differs from hole pitch + 2 x end distance.");

        // ---- Cast holes (fully defined by the plans) -------------------------------------------------------------
        var beamOf = new Dictionary<(int, int), PsDiaphragmBeamLine>();
        foreach (var b in beams) beamOf[(b.Span, b.Girder)] = b;
        var chainOf = new Dictionary<(int, int), PsDiaphragmHoleChain>();
        foreach (var ch in def.Holes)
        {
            chainOf[(ch.Span, ch.Girder)] = ch;
            if (!beamOf.TryGetValue((ch.Span, ch.Girder), out var b))
            {
                Error($"Steel diaphragm holes: no beam for span {ch.Span} beam line {Line(ch.Girder)}.");
                continue;
            }
            if (Math.Abs(ch.Total - b.Length) > In(0.125))
                Warn($"Steel diaphragm holes: span {ch.Span} beam line {Line(ch.Girder)} chain sums to {FtIn(ch.Total)} but the beam is {FtIn(b.Length)} end to end (p.31: the chain sums to the beam length).");
            var hs = ch.Holes();
            var n = WebNormal(b);
            for (int i = 0; i < hs.Count; i++)
            {
                var p = b.BottomStart + b.Axis * hs[i];
                m.Holes.Add(new PsDiaphragmCastHole
                {
                    Span = ch.Span, Girder = ch.Girder, Index = i + 1, FromBeamEnd = hs[i],
                    Lower = p + Up * c.LowerHoleAboveBottom, Upper = p + Up * (c.LowerHoleAboveBottom + c.HolePitch),
                    Axis = n, Diameter = c.CastHoleDiameter, Length = b.WebThickness,
                });
            }
        }

        if (def.Bays.Count == 0) return m;
        bool sideKnown = def.MemberSide != PsDiaphragmMemberSide.Unresolved;
        if (!sideKnown)
            Warn("UNRESOLVED WD-3 (p.32 PART SECTION A-A, p.21 beam layout): side of the web holes on which the bent-plate 9\" leg and the diaphragm member lie (toward increasing or decreasing station) is not given - the standard's Section A-A has no station arrow and shows the opposite stagger hand. Set PsSteelDiaphragmDefinition.MemberSide; steel parts are not detailed until then (cast holes are).");
        double sigma = def.MemberSide == PsDiaphragmMemberSide.TowardDecreasingStation ? -1 : 1;
        bool anyLower = false;

        foreach (var bay in def.Bays)
        {
            int gL = bay.LeftGirder, gR = bay.LeftGirder + 1;
            string mark = $"ID{bay.Span}-{Line(gL)}{Line(gR)}";
            string where = $"span {bay.Span} bay {Line(gL)}-{Line(gR)}";
            if (!beamOf.TryGetValue((bay.Span, gL), out var bl) || !beamOf.TryGetValue((bay.Span, gR), out var br))
            {
                Error($"Steel diaphragm {where}: beam not found."); continue;
            }
            if (!chainOf.TryGetValue((bay.Span, gL), out var cl) || !chainOf.TryGetValue((bay.Span, gR), out var cr))
            {
                Error($"Steel diaphragm {where}: no hole chain for one of the beams."); continue;
            }
            var hl = cl.Holes(); var hr = cr.Holes();
            if (bay.LeftHole < 1 || bay.LeftHole > hl.Count || bay.RightHole < 1 || bay.RightHole > hr.Count)
            {
                Error($"Steel diaphragm {where}: hole index out of the chain ({bay.LeftHole} of {hl.Count}, {bay.RightHole} of {hr.Count})."); continue;
            }
            ChannelSize? chan = null; WideFlangeSize? wf = null;
            if (bay.Member.TrimStart().StartsWith("C", StringComparison.OrdinalIgnoreCase)) chan = SteelCatalog.FindChannel(bay.Member);
            else wf = SteelCatalog.FindWideFlange(bay.Member);
            if (chan == null && wf == null) { Error($"Steel diaphragm {where}: member '{bay.Member}' is not in the AISC catalog."); continue; }
            WideFlangeSize? lower = null;
            if (bay.Lower != null)
            {
                anyLower = true;
                lower = SteelCatalog.FindWideFlange(bay.Lower);
                if (lower == null) { Error($"Steel diaphragm {where}: lower member '{bay.Lower}' is not in the AISC catalog."); continue; }
                if (wf == null) { Error($"Steel diaphragm {where}: lower member needs a W main member (flange to flange)."); continue; }
            }
            double depth = chan?.Depth ?? wf!.Depth, tw = chan?.WebThickness ?? wf!.WebThickness;
            double kdes = chan?.Kdes ?? wf!.Kdes, wPerFt = chan?.WeightPerFootLb ?? wf!.WeightPerFootLb;

            // ---- Plate geometry at each end -----------------------------------------------------------------------
            var holeL = bl.BottomStart + bl.Axis * hl[bay.LeftHole - 1] + Up * c.LowerHoleAboveBottom;
            var holeR = br.BottomStart + br.Axis * hr[bay.RightHole - 1] + Up * c.LowerHoleAboveBottom;
            var nL = Toward(WebNormal(bl), holeR - holeL);
            var nR = Toward(WebNormal(br), holeL - holeR);
            var eL = Horizontal(bl.Axis) * sigma;
            var eR = Horizontal(br.Axis) * sigma;
            Vec3 SlotCentre(Vec3 hole, Vec3 n, Vec3 e, double web, int i) =>
                hole + n * (web / 2 + c.BentLongLeg - c.LegSlotFromFreeEdge) + e * c.HoleToLongLegFace + Up * (c.SlotHeights[i] - c.PlateEndDistance);
            var pl = Enumerable.Range(0, c.SlotHeights.Count).Select(i => SlotCentre(holeL, nL, eL, bl.WebThickness, i)).ToArray();
            var pr = Enumerable.Range(0, c.SlotHeights.Count).Select(i => SlotCentre(holeR, nR, eR, br.WebThickness, i)).ToArray();
            double groupMid = (c.SlotHeights.Min() + c.SlotHeights.Max()) / 2 - c.PlateEndDistance;
            var gcL = holeL + nL * (bl.WebThickness / 2 + c.BentLongLeg - c.LegSlotFromFreeEdge) + eL * c.HoleToLongLegFace + Up * groupMid;
            var gcR = holeR + nR * (br.WebThickness / 2 + c.BentLongLeg - c.LegSlotFromFreeEdge) + eR * c.HoleToLongLegFace + Up * groupMid;

            // ---- Member placement (Q4 / WD-1) -------------------------------------------------------------------
            var mid = (gcL + gcR) * 0.5;
            Vec3 xm, zm;
            if (def.Elevation == PsDiaphragmMemberElevation.Level) { xm = Horizontal(gcR - gcL); zm = Up; }
            else
            {
                xm = (gcR - gcL).Normalized();
                zm = (Up - xm * Vec3.Dot(Up, xm)).Normalized();
            }
            var ym = Vec3.Cross(zm, xm).Normalized();
            if (Vec3.Dot(ym, eL) < 0) ym = -ym;               // member flanges / web thickness away from the 9" leg
            double geoLen = Vec3.Dot(gcR - gcL, xm) + 2 * c.MemberSlotFromEnd;
            double len = double.IsNaN(bay.PlanLength) ? geoLen : bay.PlanLength;
            if (!double.IsNaN(bay.PlanLength) && Math.Abs(geoLen - bay.PlanLength) > In(0.25))
                Warn($"Steel diaphragm {where}: plan length {FtIn(bay.PlanLength)} vs {FtIn(geoLen)} from the hole positions (p.31 table).");
            var origin = mid - xm * (len / 2);                 // member left end, web back face, depth centre

            // Member slots (bolt group centred on the member depth: "a" dimensions equal, p.32).
            var offs = c.SlotHeights.Select(h => h - c.PlateEndDistance - groupMid).ToArray();
            var msl = offs.Select(o => origin + xm * c.MemberSlotFromEnd + zm * o).ToArray();
            var msr = offs.Select(o => origin + xm * (len - c.MemberSlotFromEnd) + zm * o).ToArray();
            double plateCap = (c.LegSlotLength - c.LegSlotWidth) / 2, memberCap = (c.MemberSlotLength - c.MemberSlotWidth) / 2;
            var boltsL = new Vec3[pl.Length]; var boltsR = new Vec3[pr.Length];
            PsDiaphragmEndFit Fit(Vec3[] p, Vec3[] ms, Vec3 n, Vec3[] bolts)
            {
                double tMax = 0, sMax = 0, wMax = 0;
                for (int i = 0; i < p.Length; i++)
                {
                    // bolt = P + t n (plate slot) = M + s zm (member slot) + w ym (gap)
                    var (t, s, w) = Solve(n, -zm, -ym, ms[i] - p[i]);
                    bolts[i] = p[i] + n * t;
                    tMax = Math.Max(tMax, Math.Abs(t)); sMax = Math.Max(sMax, Math.Abs(s)); wMax = Math.Max(wMax, Math.Abs(w));
                }
                return new PsDiaphragmEndFit { PlateSlotOffset = tMax, PlateSlotCapacity = plateCap, MemberSlotOffset = sMax, MemberSlotCapacity = memberCap, OutOfPlane = wMax };
            }
            var fitL = Fit(pl, msl, nL, boltsL);
            var fitR = Fit(pr, msr, nR, boltsR);
            double dz = gcR.Z - gcL.Z;
            double offSquare = Math.Asin(Math.Min(1, Math.Abs(Vec3.Dot(Horizontal(gcR - gcL), Horizontal(bl.Axis)))));
            if (!fitL.Fits || !fitR.Fits)
            {
                string mode = def.Elevation == PsDiaphragmMemberElevation.Level ? "level member (p.32 \"(LEVEL)\")" : "member along the hole line";
                Warn($"Steel diaphragm {where} ({bay.Member}): slot tolerance exceeded with a {mode}: hole groups differ by {dz / In(1):0.00}\" in height; " +
                     $"member slot offset {Math.Max(fitL.MemberSlotOffset, fitR.MemberSlotOffset) / In(1):0.00}\" (take-up ±{memberCap / In(1):0.00}\", 1\" x 1 1/2\" slots), " +
                     $"leg slot offset {Math.Max(fitL.PlateSlotOffset, fitR.PlateSlotOffset) / In(1):0.00}\" (take-up ±{plateCap / In(1):0.00}\", 1\" x 2\" slots). Open question Q4 / WD-1 (p.32).");
            }
            double edgeReach = offs.Max(Math.Abs) + c.MemberSlotLength / 2;
            if (edgeReach > depth / 2 - kdes)
                Warn($"Steel diaphragm {where}: member slots reach {edgeReach / In(1):0.00}\" from the depth centre, into the {bay.Member} fillets (k = {kdes / In(1):0.00}\").");

            double lowerLen = bay.LowerPlanLength;
            bool doLower = lower != null && !double.IsNaN(lowerLen);
            if (lower != null && !doLower)
                Warn(UnresolvedFlag.Text("p.31 diaphragm table", $"steel diaphragm {where}: length of the lower member {bay.Lower}", "the table lists no length for this bay's lower member", "Not detailed."));

            var res = new PsDiaphragmBayResult
            {
                Mark = mark, Bay = bay, Length = len, GeometricLength = geoLen, HeightDifference = dz,
                MemberSlope = def.Elevation == PsDiaphragmMemberElevation.Level ? 0 : dz / Math.Max(1e-9, Vec3.Dot(gcR - gcL, Horizontal(gcR - gcL))),
                OffSquare = offSquare, LeftFit = fitL, RightFit = fitR,
                MemberWeightLb = wPerFt * len / Ft(1), LowerWeightLb = doLower ? lower!.WeightPerFootLb * lowerLen / Ft(1) : 0,
                Detailed = sideKnown,
            };
            m.Bays.Add(res);
            if (!sideKnown) continue;

            // ---- Parts -----------------------------------------------------------------------------------------------
            m.Assemblies.Add(new SteelAssembly { Mark = mark, Kind = AssemblyKind.CrossFrame, Description = $"Intermediate diaphragm {bay.Member}{(bay.Lower != null ? " + " + bay.Lower : "")} (Iowa 1036-BTD, detail {bay.Connection}), {where}" });
            AddConnection(m, def, mark, "L", holeL, nL, eL, bl.WebThickness, boltsL, ym, tw);
            AddConnection(m, def, mark, "R", holeR, nR, eR, br.WebThickness, boltsR, ym, tw);

            var mesh = new Mesh();
            var poly = chan != null ? SteelCatalog.ChannelPolygon(chan, 0, 1) : SteelCatalog.WideFlangePolygon(wf!, 0);
            RolledBuilder.AddExtrusion(mesh, poly, origin, zm, ym, xm, 0, len);
            m.Parts.Add(new SteelPart
            {
                Mark = $"{mark}-M", Category = PartCategory.Channel, Role = MemberRole.Diaphragm, Assembly = mark, Mesh = mesh,
                Description = chan?.Name ?? wf!.Name, Length = len, NominalWeightLbPerFt = wPerFt, Material = def.Material,
            });

            if (doLower) AddLower(m, def, mark, wf!, lower!, origin, xm, ym, zm, len, lowerLen, Warn);
        }

        if (sideKnown && anyLower)
            Warn("UNRESOLVED WD-5 (p.31 list, p.32 Section B-B): the 4 - 7/8\" x 2\" bolts \"for shim PL connection as required\" are not dimensioned - not placed; the W14x38 end plates and 1/8\" shims are set at the member ends and fitted to the bottom bulbs in the field (\"as required\").");
        if (sideKnown && m.Bays.Count > 0)
        {
            Info("Steel diaphragm: member bolt group centred on the member depth (p.32 note \"a\" dimensions must be equal; applied to C15x33.9 as drawn in Detail F).");
            if (anyLower) Info("Steel diaphragm: lower-member flange bolts at the AISC workable gage (gage not dimensioned on p.32 Section B-B).");
        }
        if (!double.IsNaN(def.PlanSteelWeightLb) && sideKnown)
            Info($"Steel diaphragm: modelled steel {m.SteelWeightLb:0} lb (members {m.Bays.Sum(b => b.MemberWeightLb + b.LowerWeightLb):0} lb) vs plan total {def.PlanSteelWeightLb:0} lb incl. bolts, nuts and washers (p.31).");
        return m;
    }

    /// <summary>Bent plate, backing plate and 2 web bolts at one end; 4 member bolts.</summary>
    private static void AddConnection(PsSteelDiaphragmModel m, PsSteelDiaphragmDefinition def, string mark, string end,
        Vec3 hole, Vec3 n, Vec3 e, double web, Vec3[] memberBolts, Vec3 ym, double memberTw)
    {
        var c = def.Connection;
        double ed = c.PlateEndDistance, L = c.PlateLength, t = c.BentThickness, face = c.HoleToLongLegFace;
        var webHoles = new[] { new Hole(new Vec2(0, 0), c.PlateHoleDiameter), new Hole(new Vec2(0, c.HolePitch), c.PlateHoleDiameter) };

        // 6" leg on the web face: (u along e, w up), thickness along n.
        var bent = new Mesh();
        var fShort = new Frame3(hole + n * (web / 2), e, Up, n);
        PlateBuilder.AddPlanar(bent, Rect(face - c.BentShortLeg, -ed, face, L - ed), webHoles, fShort, 0, t);
        // 9" leg toward the bay: (u along n from the web face, w up), thickness along e, outer face at `face`.
        var fLong = new Frame3(hole + n * (web / 2) + e * (face - t), n, Up, e);
        double us = c.BentLongLeg - c.LegSlotFromFreeEdge;
        var slots = c.SlotHeights.Select(h => PlateBuilder.RoundedRectangle(new Vec2(us, h - ed), c.LegSlotLength, c.LegSlotWidth, c.LegSlotWidth / 2 * 0.999)).ToArray();
        PlateBuilder.AddPlanarWithOpenings(bent, Rect(t, -ed, c.BentLongLeg, L - ed), Array.Empty<Hole>(), slots, fLong, 0, t);
        m.Parts.Add(new SteelPart
        {
            Mark = $"{mark}-BP{end}", Category = PartCategory.Gusset, Assembly = mark, Mesh = bent, Material = def.Material,
            Description = $"Bent PL {c.BentLongLeg / In(1):0.###} x {c.BentShortLeg / In(1):0.###} x {Frac(c.BentThickness)} x {FtIn(L)}", Length = L,
        });

        var back = new Mesh();
        var fBack = new Frame3(hole - n * (web / 2), e, Up, -n);
        PlateBuilder.AddPlanar(back, Rect(-c.BackingWidth / 2, -ed, c.BackingWidth / 2, L - ed), webHoles, fBack, 0, c.BackingThickness);
        m.Parts.Add(new SteelPart
        {
            Mark = $"{mark}-BK{end}", Category = PartCategory.FillerPlate, Assembly = mark, Mesh = back, Material = def.Material,
            Description = $"PL {c.BackingWidth / In(1):0.###} x {Frac(c.BackingThickness)} x {FtIn(L)}", Length = L,
        });

        // Web bolts: head on the backing plate, +Z through the web toward the bay.
        double gripWeb = c.BackingThickness + web + t;
        foreach (var dz in new[] { 0.0, c.HolePitch })
        {
            var o = hole + Up * dz - n * (web / 2 + c.BackingThickness);
            m.Bolts.Add(new BoltInstance { Frame = new Frame3(o, e, Vec3.Cross(n, e), n), Grip = gripWeb, Length = c.WebBoltLength, Assembly = mark, Diameter = c.BoltDiameter });
        }
        // Member bolts: head on the inner face of the 9" leg, +Z through the leg and the member web.
        foreach (var p in memberBolts)
        {
            var z = ym;
            var x = Vec3.Cross(Up, z).Normalized();
            m.Bolts.Add(new BoltInstance { Frame = new Frame3(p - z * t, x, Vec3.Cross(z, x), z), Grip = t + memberTw, Length = c.MemberBoltLength, Assembly = mark, Diameter = c.BoltDiameter });
        }
    }

    /// <summary>Lower W hung flange to flange under the main W (webs in one plane), 2 bolts per row, end plates, shims.</summary>
    private static void AddLower(PsSteelDiaphragmModel m, PsSteelDiaphragmDefinition def, string mark, WideFlangeSize main, WideFlangeSize w,
        Vec3 origin, Vec3 xm, Vec3 ym, Vec3 zm, double len, double lowLen, Action<string> warn)
    {
        var c = def.Connection;
        double vWeb = main.WebThickness / 2 - w.WebThickness / 2;
        var o = origin + xm * ((len - lowLen) / 2) - zm * (main.Depth / 2 + w.Depth / 2);
        var mesh = new Mesh();
        RolledBuilder.AddExtrusion(mesh, SteelCatalog.WideFlangePolygon(w, vWeb), o, zm, ym, xm, 0, lowLen);
        m.Parts.Add(new SteelPart
        {
            Mark = $"{mark}-LM", Category = PartCategory.Channel, Role = MemberRole.Diaphragm, Assembly = mark, Mesh = mesh,
            Description = w.Name, Length = lowLen, NominalWeightLbPerFt = w.WeightPerFootLb, Material = def.Material,
        });
        double webC = main.WebThickness / 2;
        double gage = w.WGi > 0 ? w.WGi : main.WGi;
        if (main.WGi > 0 && w.WGi > 0 && Math.Abs(main.WGi - w.WGi) > 1e-6)
            warn($"Steel diaphragm {mark}: workable gages differ ({main.Name} {main.WGi / In(1):0.##}\", {w.Name} {w.WGi / In(1):0.##}\").");
        int rows = c.LowerBoltRows;
        double sp = rows > 1 ? (lowLen - 2 * c.LowerBoltEndDistance) / (rows - 1) : 0;
        double grip = w.FlangeThickness + main.FlangeThickness;
        for (int k = 0; k < rows; k++)
            foreach (var sgn in new[] { -1.0, 1.0 })
            {
                var p = o + xm * (c.LowerBoltEndDistance + k * sp) + ym * (webC + sgn * gage / 2) + zm * (w.Depth / 2 - w.FlangeThickness);
                m.Bolts.Add(new BoltInstance { Frame = new Frame3(p, xm, Vec3.Cross(zm, xm), zm), Grip = grip, Length = c.LowerBoltLength, Assembly = mark, Diameter = c.BoltDiameter });
            }
        // End plates (welded to the ends) and shims outside them.
        for (int end = 0; end < 2; end++)
        {
            var dir = end == 0 ? -xm : xm;
            var face = o + xm * (end == 0 ? 0 : lowLen);
            var f = new Frame3(face, ym, zm, dir);
            var rect = Rect(webC - c.EndPlateWidth / 2, -c.EndPlateLength / 2, webC + c.EndPlateWidth / 2, c.EndPlateLength / 2);
            var ep = new Mesh();
            PlateBuilder.AddPlanar(ep, rect, Array.Empty<Hole>(), f, 0, c.EndPlateThickness);
            m.Parts.Add(new SteelPart
            {
                Mark = $"{mark}-EP{end + 1}", Category = PartCategory.Gusset, Assembly = mark, Mesh = ep, Material = def.Material,
                Description = $"PL {c.EndPlateWidth / In(1):0.###} x {Frac(c.EndPlateThickness)} x {FtIn(c.EndPlateLength)}", Length = c.EndPlateLength,
            });
            for (int s = 0; s < c.ShimsPerEnd; s++)
            {
                var sm = new Mesh();
                double z0 = c.EndPlateThickness + s * c.ShimThickness;
                PlateBuilder.AddPlanar(sm, rect, Array.Empty<Hole>(), f, z0, z0 + c.ShimThickness);
                m.Parts.Add(new SteelPart
                {
                    Mark = $"{mark}-SH{end * c.ShimsPerEnd + s + 1}", Category = PartCategory.FillerPlate, Assembly = mark, Mesh = sm, Material = def.Material,
                    Description = $"Shim PL {c.EndPlateWidth / In(1):0.###} x {Frac(c.ShimThickness)} x {FtIn(c.EndPlateLength)}", Length = c.EndPlateLength,
                });
            }
        }
    }

    // ---- helpers -----------------------------------------------------------------------------------------------------

    private static Vec3 Horizontal(Vec3 v) => new Vec3(v.X, v.Y, 0).Normalized();
    private static Vec3 WebNormal(PsDiaphragmBeamLine b) => Vec3.Cross(Up, Horizontal(b.Axis)).Normalized();
    private static Vec3 Toward(Vec3 n, Vec3 d) => Vec3.Dot(n, d) >= 0 ? n : -n;
    private static Vec2[] Rect(double u0, double w0, double u1, double w1) => new[] { new Vec2(u0, w0), new Vec2(u1, w0), new Vec2(u1, w1), new Vec2(u0, w1) };

    /// <summary>Solves a t + b s + c w = r (Cramer).</summary>
    private static (double, double, double) Solve(Vec3 a, Vec3 b, Vec3 c, Vec3 r)
    {
        double det = Vec3.Dot(a, Vec3.Cross(b, c));
        if (Math.Abs(det) < 1e-12) return (0, 0, 0);
        return (Vec3.Dot(r, Vec3.Cross(b, c)) / det, Vec3.Dot(a, Vec3.Cross(r, c)) / det, Vec3.Dot(a, Vec3.Cross(b, r)) / det);
    }

    internal static string Frac(double m)
    {
        double x = m / In(1);
        int s = (int)Math.Round(x * 16);
        int whole = s / 16, num = s % 16, den = 16;
        while (num > 0 && num % 2 == 0) { num /= 2; den /= 2; }
        return num == 0 ? $"{whole}" : whole == 0 ? $"{num}/{den}" : $"{whole} {num}/{den}";
    }

    internal static string FtIn(double m)
    {
        double x = m / In(1);
        int s = (int)Math.Round(x * 16);
        int ft = s / (12 * 16);
        return $"{ft}'-{Frac(In((s - ft * 12 * 16) / 16.0))}";
    }
}
