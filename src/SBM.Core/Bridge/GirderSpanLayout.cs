namespace SBM.Core.Bridge;

/// <summary>How the girder offsets of a <see cref="GirderSpanEnd"/> are measured.</summary>
public enum SpanOffsetMeasure
{
    /// <summary>Normal offset from the alignment (right positive): the girder point is where the end's line crosses that offset curve.</summary>
    NormalToAlignment,
    /// <summary>Distance ALONG the end's line (support line or bearing line) from its reference point (the line crossing the
    /// <see cref="GirderSpanEnd.ReferenceOffset"/> curve), right positive - the plans' "measured along CL bearing" dimensions.</summary>
    AlongLine,
}

/// <summary>
/// One end (begin or end support) of a <see cref="GirderSpanLayout"/>: the line on which the girder positions are given and the
/// positions on it. The line is the support line (CL bearing / CL pier) moved parallel to itself by <see cref="LineShift"/>,
/// measured along the <see cref="ReferenceOffset"/> curve (e.g. "CL bearing 1'-0 from CL pier along CL roadway"); 0 = the
/// support line itself.
/// </summary>
public sealed class GirderSpanEnd
{
    /// <summary>Distance from the support line to the line the offsets are given on, along the reference offset curve
    /// (up-station positive). At a pier the back span's end is usually negative and the ahead span's begin positive.</summary>
    public double LineShift { get; set; }
    public SpanOffsetMeasure Measure { get; set; } = SpanOffsetMeasure.NormalToAlignment;
    /// <summary>Normal offset of the reference curve from the alignment (right positive): the curve <see cref="LineShift"/> is
    /// measured along and, for <see cref="SpanOffsetMeasure.AlongLine"/>, the zero point on the line (the plans' W.P.).</summary>
    public double ReferenceOffset { get; set; }
    /// <summary>Girder positions left to right, one per girder (or only the first when <see cref="Spacings"/> or
    /// <see cref="GirderSpanLayout.PerpendicularSpacings"/> give the rest).</summary>
    public List<double> Offsets { get; set; } = new();
    /// <summary>Optional: girders after the first at these spacings (measured like the offsets: along the line or as normal
    /// offset differences); null = <see cref="Offsets"/> lists every girder.</summary>
    public List<double>? Spacings { get; set; }
    /// <summary>Optional, prestressed beams: haunch (top of beam to bottom of deck) per girder at this end's girder point (the plans'
    /// deck thickness at the beam minus the slab); null = <see cref="DeckDefinition.Haunch"/>.</summary>
    public List<double>? Haunches { get; set; }

    /// <summary>The girder positions resolved from <see cref="Offsets"/> + <see cref="Spacings"/> (count = available values).</summary>
    public List<double> Resolved()
    {
        if (Spacings is not { } sp || Offsets.Count == 0) return Offsets.ToList();
        var l = new List<double> { Offsets[0] };
        foreach (var s in sp) l.Add(l[^1] + s);
        return l;
    }

    public GirderSpanEnd Clone() => new()
    {
        LineShift = LineShift, Measure = Measure, ReferenceOffset = ReferenceOffset, Offsets = Offsets.ToList(),
        Spacings = Spacings?.ToList(), Haunches = Haunches?.ToList(),
    };
}

/// <summary>
/// OPTIONAL as-designed girder layout of ONE span (<see cref="GirderLayoutDefinition.SpanLayouts"/>): every girder is a straight
/// chord between its point on the begin line and its point on the end line, so each span can have its own spacing and the
/// girders may have different offsets on the back and ahead side of a pier (beam ends that do not line up across the pier -
/// simple-span prestressed beams). Spans without a layout keep the default offset curves. Bearings (support-line points),
/// span curves, cross frames, the haunch and the elevation check follow the chords. Steel girders are continuous: their back
/// and ahead points at an interior support must coincide (an error otherwise).
/// </summary>
public sealed class GirderSpanLayout
{
    /// <summary>Span index (0 = first span).</summary>
    public int Span { get; set; }
    /// <summary>Number of girders in this span; must equal <see cref="GirderLayoutDefinition.Count"/> (a different number of
    /// girders per span is not supported yet and is reported as an error).</summary>
    public int Count { get; set; }
    public GirderSpanEnd Begin { get; set; } = new();
    public GirderSpanEnd End { get; set; } = new();
    /// <summary>Optional: girders after the first are PARALLEL to the first girder's chord at these spacings measured perpendicular
    /// to it (the plans' "4 SPA. @ 8'-0 5/16 measured perpendicular to CL beam"); then only the first offset of each end is used.</summary>
    public List<double>? PerpendicularSpacings { get; set; }

    public GirderSpanLayout Clone() => new()
    {
        Span = Span, Count = Count, Begin = Begin.Clone(), End = End.Clone(), PerpendicularSpacings = PerpendicularSpacings?.ToList(),
    };

    /// <summary>Input errors (for <see cref="BridgeDefinition.Validate"/>).</summary>
    public IEnumerable<string> Validate(int girderCount, int spanCount, bool prestressed)
    {
        string n = $"Span {Span + 1} girder layout";
        if (Span < 0 || Span >= spanCount) yield return $"{n}: span index out of range (1..{spanCount}).";
        if (Count != girderCount)
            yield return $"{n}: {Count} girders given but the bridge has {girderCount}; a different number of girders per span is not supported.";
        foreach (var (e, name) in new[] { (Begin, "begin"), (End, "end") })
        {
            var offs = e.Resolved();
            int need = PerpendicularSpacings != null ? 1 : girderCount;
            if (offs.Count < need) yield return $"{n} {name}: {need} girder offset(s) required, {offs.Count} given.";
            else if (PerpendicularSpacings == null && offs.Take(girderCount).Zip(offs.Skip(1).Take(girderCount - 1)).Any(p => p.Second <= p.First))
                yield return $"{n} {name}: girder offsets must increase from left to right.";
            if (e.Haunches is { } h)
            {
                if (!prestressed) yield return $"{n} {name}: haunches per girder apply to prestressed beams only.";
                else if (h.Count != girderCount) yield return $"{n} {name}: {girderCount} haunches required, {h.Count} given.";
            }
        }
        if (PerpendicularSpacings is { } ps)
        {
            if (ps.Count != girderCount - 1) yield return $"{n}: {girderCount - 1} perpendicular spacings required, {ps.Count} given.";
            if (ps.Any(s => s <= 0)) yield return $"{n}: perpendicular spacings must be positive.";
        }
    }
}
