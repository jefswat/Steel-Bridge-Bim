using SBM.Core.Bridge;

namespace SBM.Core.Steel;

public enum TubPanelKind { CrossFrame, TopStrut }

/// <summary>What the tub generator built and measured (for checks / reports; lengths in meters, slopes rise/run).</summary>
public sealed class TubGirderReport
{
    public sealed record PanelPoint(int Girder, int Span, int Point, double S, TubPanelKind Kind, double Station);
    public sealed record PlateRun(int Girder, GirderPlate Plate, double S0, double S1, double Width, double Thickness);
    public sealed record BearingRow(string Support, int Girder, string Mark, double SeatElevation, double TopOfSlab, double H, double PlanH,
        double TransverseSlope, double LongitudinalSlope, double PlanTransverse, double PlanLongitudinal, double HorizontalOffset, double PlanHorizontalOffset);
    public sealed record ZoneRow(int Girder, double S0, double S1, TubFlangeZone Flanges);

    public double WebDepth { get; set; }
    public double WebSlopeLength { get; set; }
    public List<double> GirderLengths { get; } = new();
    /// <summary>Per girder: CL bearing path distances (support order) and splice path distances by plan number.</summary>
    public List<List<double>> BearingS { get; } = new();
    public List<Dictionary<int, double>> SpliceS { get; } = new();
    public List<PanelPoint> PanelPoints { get; } = new();
    public List<PlateRun> PlateRuns { get; } = new();
    public List<BearingRow> Bearings { get; } = new();
    public List<ZoneRow> TensionZones { get; } = new();
    public int InternalCrossFrames { get; set; }
    public int TopStruts { get; set; }
    public int LateralMembers { get; set; }
    public int ExternalDiaphragms { get; set; }
    public int SupportDiaphragms { get; set; }
    public int Stiffeners { get; set; }
    public int Studs { get; set; }
    public int Hatches { get; set; }
    public int SpliceBolts { get; set; }
    public List<string> Notes { get; } = new();
}
