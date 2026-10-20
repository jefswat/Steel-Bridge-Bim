namespace SBM.Core.Project;

/// <summary>
/// Georeference of the project coordinates (OPTIONAL, <see cref="BridgeProject.Georeference"/>): the map CRS the project X / Y belong to and the
/// grid scale = map distance / project distance (TxDOT surface coordinates: 1 / surface adjustment factor). IFC: IfcProjectedCRS (name, datum,
/// projection, zone, map unit) + IfcMapConversion Scale and the base point converted to grid coordinates. The model itself is never moved.
/// </summary>
public sealed class GeoreferenceDefinition
{
    /// <summary>CRS name, e.g. "EPSG:2276".</summary>
    public string CrsName { get; set; } = "";
    public string Description { get; set; } = "";
    public string GeodeticDatum { get; set; } = "";
    public string MapProjection { get; set; } = "";
    public string MapZone { get; set; } = "";
    /// <summary>Map unit = US survey foot (else the project length unit).</summary>
    public bool MapUnitUsSurveyFoot { get; set; }
    /// <summary>Map (grid) distance / project distance (1 = none).</summary>
    public double GridScale { get; set; } = 1;
    /// <summary>OPTIONAL point (project X / Y, meters) the grid scale is applied about (map = centre + (project - centre) x GridScale): NCDOT "localized" coordinates
    /// keep the control point's state plane values and scale about it. null = about the origin (TxDOT surface coordinates).</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? ScaleCenterX { get; set; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public double? ScaleCenterY { get; set; }
    /// <summary>Evidence / source of the georeference (report).</summary>
    public string Basis { get; set; } = "";
}
