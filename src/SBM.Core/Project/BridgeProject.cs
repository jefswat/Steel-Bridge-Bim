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
    /// <summary>The plan-set PDFs this model is built from (OPTIONAL; absolute, or relative to the model file; saved only when not empty).
    /// `sbm pipeline` builds the sheet index / skeleton link rules from them when no sheet-links*.json names the model.</summary>
    public List<string> PlanPdfs { get; set; } = new();

    /// <summary>Resolves a reference path (relative paths are relative to the project file's folder). An absolute path that does not exist
    /// (a model saved on another machine or in the sandbox, e.g. C:\SBM\dist\terrain\...) is looked up again under the project file's folders:
    /// for each ancestor folder of the project file, the trailing parts of the stored path (shortest first: "x.flt", "ptc\x.flt", ...,
    /// "dist\terrain\ptc\x.flt") are tried; the first file that exists wins. Otherwise the stored path is returned unchanged.</summary>
    public static string ResolvePath(string path, string? projectFile)
    {
        if (string.IsNullOrWhiteSpace(path) || projectFile == null) return path;
        string dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(projectFile))!;
        if (!System.IO.Path.IsPathRooted(path)) return System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, path));
        if (System.IO.File.Exists(path)) return path;
        var parts = path.Split('\\', '/').Where(p => p.Length > 0).ToArray();
        for (var d = new System.IO.DirectoryInfo(dir); d != null; d = d.Parent)
            for (int k = parts.Length - 1; k >= 1; k--)   // k = first kept part; parts[0] is the drive / root
            {
                string candidate = System.IO.Path.Combine(d.FullName, System.IO.Path.Combine(parts[k..]));
                if (System.IO.File.Exists(candidate)) return candidate;
            }
        return path;
    }

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
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? PlanPdfs { get; set; }

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
            PlanPdfs = p.PlanPdfs.Count == 0 ? null : p.PlanPdfs,
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
            PlanPdfs = PlanPdfs ?? new(),
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
