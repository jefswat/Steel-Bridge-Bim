using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Bridge;

// Precast post-tensioned SEGMENTAL concrete box girder (balanced cantilever), OPTIONAL on BridgeDefinition.Segmental
// (null = none, omitted from the .sbm). A segmental bridge has NO girders (Girders.Count = 0): FramingGenerator builds the support
// lines only; SBM.Core.Segmental.SegmentalGenerator builds segments, tendons, bearings and the bents (design note:
// docs/plan-data/bonner-b2500/segmental-unit5/SEGMENTAL-DESIGN.md). All lengths in meters. Nothing is defaulted from a guess:
// values the plans do not give stay NaN / empty and the generator raises an UNRESOLVED warning (page + plain reason).

public enum SegmentType { BentHalf, Typical, Closure, End }

public enum TendonGroup { Cantilever, Bottom, Closure, Transverse }

/// <summary>Where a longitudinal tendon runs (the plans give anchor segments / lengths, not profiles; every rule except Cantilever is flagged).</summary>
public enum TendonLayout
{
    /// <summary>Straight between the far (tip-side) faces of the anchor segments Dk / Uk of the bent's cantilever.</summary>
    Cantilever,
    /// <summary>Straight, plan length, centred on the closure joint of the span.</summary>
    CentredOnClosure,
    /// <summary>Straight, plan length, from the anchor face of the span's end segment into the span.</summary>
    FromEndSegment,
}

/// <summary>Box cross section (S3-20 p268): symmetric about the girder CL; y measured DOWN from the top surface, normal to it.</summary>
public sealed class SegmentalBoxSection
{
    /// <summary>Out-to-out top width (42'-7").</summary>
    public double TopWidth { get; set; } = Ft(42) + In(7);
    /// <summary>Top slab thickness at the CL (9") and flat width from the CL (5'-1").</summary>
    public double TopSlab { get; set; } = In(9);
    public double TopSlabFlat { get; set; } = Ft(5) + In(1);
    /// <summary>Top haunch run from the end of the flat part to the inner web corner (5'-6"); the corner is <see cref="WebTopDepth"/> below the top.</summary>
    public double TopHaunchRun { get; set; } = Ft(5) + In(6);
    /// <summary>Depth of the wing root and of the inner web corner (2'-2").</summary>
    public double WebTopDepth { get; set; } = Ft(2) + In(2);
    /// <summary>Wing: tip thickness (10"), flat underside from the tip (4'-1"), tip to the outer web face at the wing root (9'-4").</summary>
    public double WingTip { get; set; } = In(10);
    public double WingFlat { get; set; } = Ft(4) + In(1);
    public double TipToWeb { get; set; } = Ft(9) + In(4);
    /// <summary>Web slope vertical : horizontal (4 : 1) and thickness normal to the web (1'-4").</summary>
    public double WebSlope { get; set; } = 4;
    public double WebThickness { get; set; } = Ft(1) + In(4);
    /// <summary>Bottom slab haunch at the webs: rise 6" over 2'-5 1/2".</summary>
    public double BottomHaunchRise { get; set; } = In(6);
    public double BottomHaunchRun { get; set; } = Ft(2) + In(5.5);
    /// <summary>Fillet at the inner web / top haunch corner (R = 1'-0"); 0 = sharp.</summary>
    public double FilletRadius { get; set; } = Ft(1);
    /// <summary>x of the outer web face at the wing root (TopWidth/2 - TipToWeb).</summary>
    public double WebRootX => TopWidth / 2 - TipToWeb;
    /// <summary>Horizontal web thickness (normal thickness x sqrt(1 + 1/slope^2)).</summary>
    public double WebHorizontal => WebThickness * Math.Sqrt(1 + 1 / (WebSlope * WebSlope));
    /// <summary>Half soffit width W/2 at depth H from the web slope rule.</summary>
    public double HalfSoffit(double h) => WebRootX - (h - WebTopDepth) / WebSlope;
    public SegmentalBoxSection Clone() => (SegmentalBoxSection)MemberwiseClone();
}

/// <summary>One printed section of the variable depth table (x from the bent CL).</summary>
public sealed class SegmentalDepthSection
{
    public int Number { get; set; }
    public double X { get; set; }
    public double Depth { get; set; }
    /// <summary>Printed half soffit width (check only; the geometry uses the web slope rule).</summary>
    public double HalfSoffitPrinted { get; set; } = double.NaN;
    public double BottomSlab { get; set; }
    public SegmentalDepthSection Clone() => (SegmentalDepthSection)MemberwiseClone();
}

/// <summary>A segment / joint of a span: x from the span's START bent along the alignment.</summary>
public sealed class SegmentDefinition
{
    public string Name { get; set; } = "";
    public SegmentType Type { get; set; }
    public double X0 { get; set; }
    public double X1 { get; set; }
    /// <summary>Cantilever bent (support name) the segment belongs to; "" for closure joints and end segments.</summary>
    public string Bent { get; set; } = "";
    /// <summary>Segment number k in the cantilever (XX-Uk / XX-Dk; 0 = bent half).</summary>
    public int Index { get; set; }
    public SegmentDefinition Clone() => (SegmentDefinition)MemberwiseClone();
}

public sealed class SegmentalSpan
{
    /// <summary>Span index in the bridge (0 = between Supports[0] and Supports[1]).</summary>
    public int Span { get; set; }
    /// <summary>Plan span name (e.g. "Span 19").</summary>
    public string Name { get; set; } = "";
    public List<SegmentDefinition> Segments { get; set; } = new();
    public SegmentalSpan Clone() => new() { Span = Span, Name = Name, Segments = Segments.Select(s => s.Clone()).ToList() };
}

/// <summary>A longitudinal tendon designation (one schedule row: Count tendons, mirrored about the CL when Count = 2).</summary>
public sealed class TendonDefinition
{
    public string Name { get; set; } = "";
    public TendonGroup Group { get; set; }
    public TendonLayout Layout { get; set; }
    /// <summary>Schedule location: support name (cantilever) or span name.</summary>
    public string Location { get; set; } = "";
    public int Count { get; set; } = 2;
    public int Strands { get; set; }
    public double PlanLength { get; set; }
    /// <summary>Plan weight of one tendon (lb).</summary>
    public double PlanWeightEachLb { get; set; }
    public double JackingForceKip { get; set; }
    public string StressingEnd { get; set; } = "";
    /// <summary>Cantilever: anchor segment number on the down-station / up-station side.</summary>
    public int AnchorDown { get; set; }
    public int AnchorUp { get; set; }
    /// <summary>Duct position in the section: lateral offset of the right-hand tendon from the CL (left one mirrored) and depth below the
    /// top surface (Row A / B) or height above the soffit (<see cref="AboveSoffit"/>, Row C).</summary>
    public double DuctOffset { get; set; } = double.NaN;
    public double DuctDepth { get; set; } = double.NaN;
    public bool AboveSoffit { get; set; }
    /// <summary>Plan text this layout / duct position is assumed from (raised as UNRESOLVED on the tendon).</summary>
    public string Unresolved { get; set; } = "";
    public TendonDefinition Clone() => (TendonDefinition)MemberwiseClone();
}

/// <summary>Transverse tendons of a segment type (S3-24 / S3-30 / S3-34).</summary>
public sealed class TransverseTendonDefinition
{
    public SegmentType SegmentType { get; set; }
    /// <summary>Closure joints only: applies to closures of this nominal length (0 = any).</summary>
    public double SegmentLength { get; set; }
    public string Name { get; set; } = "";
    public int PerSegment { get; set; }
    public int Strands { get; set; } = 4;
    public double PlanLength { get; set; }
    public double PlanWeightPerSegmentLb { get; set; }
    public double JackingForceKip { get; set; }
    /// <summary>Depth of the duct below the top surface at the CL.</summary>
    public double Depth { get; set; } = double.NaN;
    public string Unresolved { get; set; } = "";
    public TransverseTendonDefinition Clone() => (TransverseTendonDefinition)MemberwiseClone();
}

/// <summary>A disc bearing under the box at a bent.</summary>
public sealed class SegmentalBearing
{
    public string Name { get; set; } = "";
    /// <summary>Plan type I (pinned) / II (guided) / III (free).</summary>
    public string Type { get; set; } = "";
    /// <summary>Lateral offset of the CL bearing from the bent's alignment point, along the bent line (right +).</summary>
    public double Offset { get; set; }
    /// <summary>Distance of the CL bearing from the bent control line along the alignment (up-station +).</summary>
    public double Along { get; set; }
    /// <summary>Assumed bearing height (plan bearing table).</summary>
    public double Height { get; set; } = double.NaN;
    /// <summary>Plan size of the bearing body (masonry plate max), along the bent line / along the alignment; NaN = not given (no solid).</summary>
    public double SizeAcross { get; set; } = double.NaN;
    public double SizeAlong { get; set; } = double.NaN;
    /// <summary>Top of the CIP bearing pedestal (plan elevation table); NaN = not given.</summary>
    public double PedestalTop { get; set; } = double.NaN;
    public double PedestalAcross { get; set; } = double.NaN;
    public double PedestalAlong { get; set; } = double.NaN;
    public string Unresolved { get; set; } = "";
    public SegmentalBearing Clone() => (SegmentalBearing)MemberwiseClone();
}

/// <summary>Bent (pier) below the box: footing, CIP column base + precast column, precast cap. Plan coordinates (S along the bent line
/// right +, N along the alignment up-station +) from the bent's alignment point (the WP; column vertical through it).</summary>
public sealed class SegmentalPier
{
    public double CapTop { get; set; } = double.NaN;
    public double CapHeight { get; set; } = double.NaN;
    /// <summary>Cap plan outline (S, N); empty = not resolved (no cap solid).</summary>
    public List<PlanPoint> CapOutline { get; set; } = new();
    /// <summary>Tapered lower part of the cap: inset of the bottom outline per side and height of the taper (0 = prismatic).</summary>
    public double CapTaperInset { get; set; }
    public double CapTaperHeight { get; set; }
    /// <summary>Column envelope along S / N (NaN = not resolved, no column solid) and the top of the CIP column base.</summary>
    public double ColumnS { get; set; } = double.NaN;
    public double ColumnN { get; set; } = double.NaN;
    public double ColumnBaseTop { get; set; } = double.NaN;
    /// <summary>Footing outline (S, N) and elevations.</summary>
    public List<PlanPoint> FootingOutline { get; set; } = new();
    public double FootingTop { get; set; } = double.NaN;
    public double FootingBottom { get; set; } = double.NaN;
    public int PileCount { get; set; }
    /// <summary>Items modeled as an envelope / not modeled (one UNRESOLVED warning each, also on the element).</summary>
    public List<string> Unresolved { get; set; } = new();
    public SegmentalPier Clone() => new()
    {
        CapTop = CapTop, CapHeight = CapHeight, CapOutline = CapOutline.Select(p => new PlanPoint(p.S, p.N)).ToList(), CapTaperInset = CapTaperInset, CapTaperHeight = CapTaperHeight,
        ColumnS = ColumnS, ColumnN = ColumnN, ColumnBaseTop = ColumnBaseTop, FootingOutline = FootingOutline.Select(p => new PlanPoint(p.S, p.N)).ToList(), FootingTop = FootingTop,
        FootingBottom = FootingBottom, PileCount = PileCount, Unresolved = Unresolved.ToList(),
    };
}

/// <summary>Plan point in a bent frame (S along the bent line right +, N along the alignment up-station +).</summary>
public sealed class PlanPoint
{
    public double S { get; set; }
    public double N { get; set; }
    public PlanPoint() { }
    public PlanPoint(double s, double n) { S = s; N = n; }
    public Geometry.Vec2 ToVec2() => new(S, N);
}

public sealed class SegmentalBent
{
    /// <summary>Support name (= the bridge's support).</summary>
    public string Name { get; set; } = "";
    /// <summary>Plan box depth at the bent CL (19'-3" at cantilever bents; NaN at end bents).</summary>
    public double DepthAtCl { get; set; } = double.NaN;
    public List<SegmentalBearing> Bearings { get; set; } = new();
    public SegmentalPier? Pier { get; set; }
    public SegmentalBent Clone() => new() { Name = Name, DepthAtCl = DepthAtCl, Bearings = Bearings.Select(b => b.Clone()).ToList(), Pier = Pier?.Clone() };
}

public sealed class SegmentalDefinition
{
    /// <summary>Plan set / sheets the data comes from (pages for generic flags).</summary>
    public string Source { get; set; } = "";
    public SegmentalBoxSection Box { get; set; } = new();
    /// <summary>Printed depth table (x from the bent CL); between sections: <see cref="ParabolaVertexX"/> curve + linear residuals.</summary>
    public List<SegmentalDepthSection> DepthTable { get; set; } = new();
    /// <summary>Depth curve RECONSTRUCTION (not printed): H(x) = VertexDepth + Coefficient (VertexX - x)^2 for x below VertexX (1/m), H = VertexDepth beyond.
    /// At the table sections the printed values govern (residuals interpolated linearly). NaN = linear interpolation of the table only.</summary>
    public double ParabolaVertexX { get; set; } = double.NaN;
    public double ParabolaVertexDepth { get; set; } = double.NaN;
    public double ParabolaCoefficient { get; set; } = double.NaN;
    /// <summary>Bent half segments: depth at the bent CL comes from <see cref="SegmentalBent.DepthAtCl"/>; the soffit steps down from the
    /// joint depth over <see cref="BentSoffitTransition"/> from the bulkhead face; the pier diaphragm (void closed) is <see cref="BentDiaphragm"/>
    /// long, measured from the bent CL.</summary>
    public double BentSoffitTransition { get; set; } = double.NaN;
    public double BentDiaphragm { get; set; } = double.NaN;
    /// <summary>Depth of the closure joints / end segments / constant zone (9'-0") and the bottom slab there (10").</summary>
    public double ConstantDepth { get; set; } = double.NaN;
    public double ConstantBottomSlab { get; set; } = double.NaN;
    public List<SegmentalBent> Bents { get; set; } = new();
    public List<SegmentalSpan> Spans { get; set; } = new();
    public List<TendonDefinition> Tendons { get; set; } = new();
    public List<TransverseTendonDefinition> Transverse { get; set; } = new();
    /// <summary>Strand data: 0.6" dia, area 0.217 in2, 0.74 lb/ft (plan weight basis).</summary>
    public double StrandDiameter { get; set; } = In(0.6);
    public double StrandArea { get; set; } = In(1) * In(1) * 0.217;
    public double StrandWeightLbPerFt { get; set; } = 0.74;
    public double ConcreteStrengthKsi { get; set; } = 6.0;
    public string StrandMaterial { get; set; } = "ASTM A416 Gr. 270 low-relaxation 0.6\" strand";
    /// <summary>Plan items not modeled / not dimensioned (raised as warnings, text built with UnresolvedFlag).</summary>
    public List<string> Unresolved { get; set; } = new();
    /// <summary>Plan-vs-plan differences (Info).</summary>
    public List<string> Reported { get; set; } = new();

    public SegmentalBent? Bent(string name) => Bents.FirstOrDefault(b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public SegmentalDefinition Clone() => new()
    {
        Source = Source, Box = Box.Clone(), DepthTable = DepthTable.Select(d => d.Clone()).ToList(),
        ParabolaVertexX = ParabolaVertexX, ParabolaVertexDepth = ParabolaVertexDepth, ParabolaCoefficient = ParabolaCoefficient,
        BentSoffitTransition = BentSoffitTransition, BentDiaphragm = BentDiaphragm, ConstantDepth = ConstantDepth, ConstantBottomSlab = ConstantBottomSlab,
        Bents = Bents.Select(b => b.Clone()).ToList(), Spans = Spans.Select(s => s.Clone()).ToList(), Tendons = Tendons.Select(t => t.Clone()).ToList(),
        Transverse = Transverse.Select(t => t.Clone()).ToList(), StrandDiameter = StrandDiameter, StrandArea = StrandArea,
        StrandWeightLbPerFt = StrandWeightLbPerFt, ConcreteStrengthKsi = ConcreteStrengthKsi, StrandMaterial = StrandMaterial,
        Unresolved = Unresolved.ToList(), Reported = Reported.ToList(),
    };

    public List<string> Validate(int spanCount)
    {
        var e = new List<string>();
        if (Spans.Count == 0) e.Add("Segmental: no spans.");
        foreach (var s in Spans)
        {
            if (s.Span < 0 || s.Span >= spanCount) e.Add($"Segmental {s.Name}: span index {s.Span} is outside the bridge's spans.");
            foreach (var g in s.Segments) if (g.X1 <= g.X0) e.Add($"Segmental {s.Name} {g.Name}: segment end must be beyond its start.");
        }
        if (DepthTable.Count < 2) e.Add("Segmental: the depth table needs at least two sections.");
        return e;
    }
}
