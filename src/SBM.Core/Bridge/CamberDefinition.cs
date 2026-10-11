namespace SBM.Core.Bridge;

/// <summary>
/// One key point of a girder's camber table - a point the plans define by its location: a CL bearing (<see cref="Support"/>,
/// framing index) or a field splice (<see cref="Splice"/>, the plan FS number). The points between consecutive key points are
/// equally spaced along the girder (plans: "numbered points are equally spaced along each line between the field splice and
/// pier locations"; camber sheet: equal spaces inside each segment).
/// </summary>
public sealed class CamberKeyPoint
{
    /// <summary>Plan point number.</summary>
    public int Number { get; set; }
    /// <summary>Support (framing index, auxiliary supports after the supports) when the key point is a CL bearing; -1 = none.</summary>
    public int Support { get; set; } = -1;
    /// <summary>Plan field splice number when the key point is a field splice; 0 = none.</summary>
    public int Splice { get; set; }
    /// <summary>The key point is the end of a discontinuous girder: its termination (<see cref="GirderTermination"/>, CL bearing of the
    /// termination support + the termination distance along the girder).</summary>
    public bool GirderEnd { get; set; }
    public CamberKeyPoint Clone() => (CamberKeyPoint)MemberwiseClone();
}

/// <summary>Plan values at one numbered point of one girder (all in meters).</summary>
public sealed class CamberPoint
{
    /// <summary>Plan point number.</summary>
    public int Number { get; set; }
    /// <summary>Camber ordinate, measured from the chord line between the field splices; UP is positive (plan note 3).</summary>
    public double Camber { get; set; }
    /// <summary>Deflection due to the weight of the structural steel, DOWN positive.</summary>
    public double SteelDeflection { get; set; }
    /// <summary>Deflection due to the weight of the slab and barriers, DOWN positive (the plans' "anticipated deflection due to slab and barrier").</summary>
    public double SlabDeflection { get; set; }
    public CamberPoint Clone() => (CamberPoint)MemberwiseClone();
}

/// <summary>
/// As-designed camber data of one girder line (plans' CAMBER &amp; BLOCKING / deflection ordinate tables). The model geometry stays
/// the final (loaded) shape; camber and deflections are inputs from which <see cref="Framing.CamberResult"/> computes the
/// blocking (no-load top of web relative to the datum point).
/// </summary>
public sealed class GirderCamberDefinition
{
    /// <summary>Girder index (0 = G1).</summary>
    public int Girder { get; set; }
    /// <summary>Point number of the BLOCKING datum: the girder end whose blocking the plans print as 0.00 (the "horizontal line" of the
    /// plan's camber and blocking diagram runs through the no-load top of web there). 0 = none given (no blocking is computed).</summary>
    public int DatumPoint { get; set; }
    /// <summary>Key points in order along the girder (at least two to locate any point).</summary>
    public List<CamberKeyPoint> Keys { get; set; } = new();
    /// <summary>Plan values per point number.</summary>
    public List<CamberPoint> Points { get; set; } = new();

    public GirderCamberDefinition Clone() => new()
    {
        Girder = Girder, DatumPoint = DatumPoint,
        Keys = Keys.Select(k => k.Clone()).ToList(),
        Points = Points.Select(p => p.Clone()).ToList(),
    };
}
