using static SBM.Core.Units.UnitSystem;

namespace SBM.Core.Bridge;

/// <summary>
/// OPTIONAL dapped steel girder end at an expansion joint (<c>SupportDefinition.GirderEndDap</c>; null = plain square girder ends, the default).
/// Applies to every girder END that sits on the support line (a girder's start on the first support / its auxiliary beginning support, or its
/// end on the last support; girders terminated on a cross girder are not affected). Plan basis (Iowa DOT Designs 116 / 316, girder end details):
/// <list type="bullet">
/// <item>Top flange bent DOWN by <see cref="FlangeDrop"/> (bent plate, two bends of <see cref="BendRadius"/>) so the thickened slab / joint blockout clears it:
/// lowered flat from the girder end to <see cref="LowFlatFromBearing"/> past the CL bearing (theoretical vertex), then a straight slope over
/// <see cref="DropRun"/> back to the normal level. The top of the web follows the flange.</item>
/// <item>Optional web end cope (<see cref="CopeInset"/> &gt; 0): the web end is recessed by CopeInset between two end tabs; each tab is
/// <see cref="CopeEdge"/> deep from the flange (web edge), then the edge runs to the recess over <see cref="CopeRun"/> (vertical), corners rounded
/// to <see cref="CopeRadius"/>.</item>
/// </list>
/// Lengths in meters. Nothing is built for a girder end that the dap does not fit (warning).
/// </summary>
public sealed class GirderEndDapDefinition
{
    /// <summary>Vertical drop of the top flange at the girder end (plans: 7 1/2").</summary>
    public double FlangeDrop { get; set; } = In(7.5);
    /// <summary>Horizontal run of the sloped part between the theoretical vertices (plans: 7 1/2", i.e. 45 degrees).</summary>
    public double DropRun { get; set; } = In(7.5);
    /// <summary>CL bearing to the lower theoretical vertex, measured along the girder toward the span (plans: 1'-2).</summary>
    public double LowFlatFromBearing { get; set; } = Ft(1) + In(2);
    /// <summary>Bend radius of both flange bends (plans: R = 6"), applied to the underside of the flange (top of web).</summary>
    public double BendRadius { get; set; } = In(6);

    /// <summary>Web end recess depth along the girder; 0 = no cope (plans: 6" on Detail B / Ramp G Detail A, none on I-80 Detail A).</summary>
    public double CopeInset { get; set; }
    /// <summary>Depth of each end tab measured from the web edge at the flange (plans: 10").</summary>
    public double CopeEdge { get; set; } = In(10);
    /// <summary>Vertical extent of the sloped transition from the tab to the recess (plans: 6").</summary>
    public double CopeRun { get; set; } = In(6);
    /// <summary>Radius of the two cope corners (plans: R = 6").</summary>
    public double CopeRadius { get; set; } = In(6);

    /// <summary>Girder indices the dap applies to; null = every girder end on the support line.</summary>
    public List<int>? Girders { get; set; }

    /// <summary>Plan reference (sheet / PDF page) printed in notes and warnings.</summary>
    public string Source { get; set; } = "";

    public GirderEndDapDefinition Clone()
    {
        var c = (GirderEndDapDefinition)MemberwiseClone();
        c.Girders = Girders?.ToList();
        return c;
    }
}
