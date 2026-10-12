using System.Text.Json;
using System.Text.Json.Serialization;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Geometry;
using SBM.Core.Units;

namespace SBM.Core.Project;

/// <summary>The parametric project: everything needed to regenerate the model. Saved as JSON (*.sbm).</summary>
public sealed class BridgeProject
{
    public const int CurrentVersion = 1;
    public const string FileExtension = ".sbm";

    public string Name { get; set; } = "Untitled";
    public UnitSystem Units { get; set; } = new();
    public List<Alignment> Alignments { get; set; } = new();
    public List<BridgeDefinition> Bridges { get; set; } = new();
    public List<References.ReferenceModelDefinition> References { get; set; } = new();
    /// <summary>Geometry options (exact NURBS for curved parts; default off).</summary>
    public GeometrySettings Geometry { get; set; } = new();
    /// <summary>Georeference of the project X / Y (OPTIONAL, null = local grid, IFC map conversion scale 1): map CRS, grid scale (map = local x scale,
    /// e.g. 1 / surface adjustment factor for TxDOT surface coordinates).</summary>
    public GeoreferenceDefinition? Georeference { get; set; }

    /// <summary>Resolves a reference path (relative paths are relative to the project file's folder).</summary>
    public static string ResolvePath(string path, string? projectFile) =>
        System.IO.Path.IsPathRooted(path) || projectFile == null ? path
            : System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(projectFile)!, path));

    public Alignment? FindAlignment(string name) =>
        Alignments.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase)) ?? Alignments.FirstOrDefault();

    // ------------------------------------------------------------------ serialization

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    public string ToJson() => JsonSerializer.Serialize(ProjectDto.From(this), Options);

    public static BridgeProject FromJson(string json)
    {
        var dto = JsonSerializer.Deserialize<ProjectDto>(json, Options) ?? throw new InvalidDataException("Empty project file.");
        if (dto.Version > CurrentVersion)
            throw new InvalidDataException($"Project file version {dto.Version} is newer than this program supports ({CurrentVersion}).");
        return dto.ToProject();
    }

    public void Save(string path) => File.WriteAllText(path, ToJson());

    public static BridgeProject Load(string path) => FromJson(File.ReadAllText(path));

    public BridgeProject DeepClone() => FromJson(ToJson());

    // DTOs keep the file format stable and independent of runtime-only state.
    private sealed class ProjectDto
    {
        public int Version { get; set; } = CurrentVersion;
        public string Name { get; set; } = "";
        public UnitSystemKind UnitSystem { get; set; }
        public FootDefinition Foot { get; set; }
        public List<AlignmentDto> Alignments { get; set; } = new();
        public List<BridgeDefinition> Bridges { get; set; } = new();
        public List<References.ReferenceModelDefinition>? References { get; set; } = new();
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public GeometrySettings? Geometry { get; set; }
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public GeoreferenceDefinition? Georeference { get; set; }

        public static ProjectDto From(BridgeProject p) => new()
        {
            Name = p.Name,
            UnitSystem = p.Units.Kind,
            Foot = p.Units.Foot,
            Alignments = p.Alignments.Select(AlignmentDto.From).ToList(),
            Bridges = p.Bridges,
            References = p.References,
            Geometry = p.Geometry.IsDefault ? null : p.Geometry,
            Georeference = p.Georeference,
        };

        public BridgeProject ToProject() => new()
        {
            Name = Name,
            Units = new UnitSystem { Kind = UnitSystem, Foot = Foot },
            Alignments = Alignments.Select(a => a.ToAlignment()).ToList(),
            Bridges = Bridges,
            References = References ?? new(),
            Geometry = Geometry ?? new(),
            Georeference = Georeference,
        };
    }

    private sealed class ElementDto
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Angle { get; set; }
        public double Length { get; set; }
        public double K0 { get; set; }
        public double K1 { get; set; }
    }

    private sealed class AlignmentDto
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public double StartStation { get; set; }
        public List<StationEquation> StationEquations { get; set; } = new();
        public List<ElementDto> Elements { get; set; } = new();
        public PiDefinition? PiDefinition { get; set; }
        public VerticalProfile Profile { get; set; } = new();
        public CrossSlopeTable CrossSlope { get; set; } = new();

        public static AlignmentDto From(Alignment a) => new()
        {
            Name = a.Name,
            Description = a.Description,
            StartStation = a.StartStation,
            StationEquations = a.StationEquations,
            Elements = a.Elements.Select(e => new ElementDto
            {
                X = e.Start.X, Y = e.Start.Y, Angle = e.StartAngle, Length = e.Length, K0 = e.K0, K1 = e.K1,
            }).ToList(),
            PiDefinition = a.PiDefinition,
            Profile = a.Profile,
            CrossSlope = a.CrossSlope,
        };

        public Alignment ToAlignment()
        {
            var a = new Alignment
            {
                Name = Name, Description = Description, StartStation = StartStation, StationEquations = StationEquations,
                PiDefinition = PiDefinition, Profile = Profile, CrossSlope = CrossSlope,
            };
            a.SetElements(Elements.Select(e => new HorizontalElement(new Vec2(e.X, e.Y), e.Angle, e.Length, e.K0, e.K1)));
            return a;
        }
    }
}
