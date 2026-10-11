using System.Diagnostics;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Drawing;
using SBM.Core.Framing;
using SBM.Core.Project;
using SBM.Core.Steel;
using SBM.Core.Units;
using SBM.IO.Dxf;
using SBM.IO.Ifc;
using SBM.IO.LandXml;
using SBM.IO.References;
using SBM.Core.Drawing.Views;
using SBM.Core.Geometry;
using SBM.Core.References;

namespace SBM.Api;

/// <summary>Summary of a regeneration run.</summary>
public sealed record RegenerationReport(int Bridges, int Regenerated, int Reused, int CrossFrames, int Parts, int Bolts, int Studs,
    double SteelMassKg, IReadOnlyList<string> Warnings, IReadOnlyList<string> Errors, TimeSpan Elapsed);

/// <summary>
/// Programmatic access to a Steel Bridge Modeler project: open/create, edit (typed objects or text property paths with
/// units), regenerate (incremental, parallel), query results, save and export. This is the same engine the desktop
/// app uses. Not thread-safe for concurrent edits; regeneration itself runs in parallel internally.
/// </summary>
/// <example>
/// <code>
/// var doc = SbmDocument.Open("Bridge.sbm");
/// doc.Set("Bridges[0].Supports[1].SkewDegrees", "20");
/// doc.Set("Bridges[0].Girders.Section.WebDepth", "60\"");
/// var r = doc.Regenerate();
/// doc.ExportIfc("Bridge.ifc");
/// doc.Save();
/// </code>
/// </example>
public sealed class SbmDocument
{
    private readonly RegenerationCache _cache = new();
    private List<BridgeModel?> _models = new();

    public BridgeProject Project { get; private set; }
    public string? FilePath { get; private set; }
    public UnitSystem Units => Project.Units;

    private SbmDocument(BridgeProject project, string? path)
    {
        Project = project;
        FilePath = path;
        _cache.Ground = GroundAt;
    }

    private List<TerrainModel>? _terrains;
    private string _terrainKey = "";

    /// <summary>Existing ground elevation (attached terrain references) at a plan point, or null outside the terrain.</summary>
    public double? GroundAt(Vec2 p)
    {
        foreach (var t in _terrains ?? new List<TerrainModel>())
            if (t.TryElevation(p, out double z)) return z;
        return null;
    }

    /// <summary>Loads the project's visible terrain references (used for automatic foundation tops and ground lines).</summary>
    private void EnsureTerrain()
    {
        var defs = Project.References.Where(r => r.Kind == ReferenceKind.Terrain && r.Visible).ToList();
        string key = string.Join("|", defs.Select(d => d.Path + d.UnitScale + d.OffsetX + d.OffsetY + d.OffsetZ));
        if (_terrains != null && key == _terrainKey) return;
        var list = new List<TerrainModel>();
        foreach (var def in defs)
        {
            string full = BridgeProject.ResolvePath(def.Path, FilePath);
            if (!File.Exists(full)) continue;
            try
            {
                var geo = ReferenceReaders.Load(def, full, Units.LongUnit);
                if (!geo.Mesh.IsEmpty) list.Add(geo.Grid is { } grd ? new TerrainModel(grd, geo.Mesh) : new TerrainModel(geo.Mesh));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException or System.Xml.XmlException) { }
        }
        _terrains = list;
        _terrainKey = key;
        _cache.GroundVersion++;
    }

    // ------------------------------------------------------------------ lifecycle

    public static SbmDocument New(string name = "Untitled", bool metric = false) =>
        new(new BridgeProject { Name = name, Units = new UnitSystem { Kind = metric ? UnitSystemKind.SI : UnitSystemKind.US } }, null);

    public static SbmDocument Sample() => new(SampleProject.Create(), null);

    public static SbmDocument Open(string path) => new(BridgeProject.Load(path), Path.GetFullPath(path));

    /// <summary>Wraps an existing in-memory project (used by the desktop app's script runner).</summary>
    public static SbmDocument Attach(BridgeProject project, string? path = null) => new(project, path);

    public void Save(string? path = null)
    {
        path ??= FilePath ?? throw new InvalidOperationException("No file path; pass one to Save.");
        Project.Save(path);
        FilePath = Path.GetFullPath(path);
    }

    // ------------------------------------------------------------------ text property access

    public object? Get(string path) => PropertyPath.GetValue(Project, path);
    public string GetText(string path) => PropertyPath.Format(Get(path), path, Units);
    public string Set(string path, string value) => PropertyPath.SetValue(Project, path, value, Units);
    public int Add(string listPath, string value) => PropertyPath.Add(Project, listPath, value, Units);
    public void Remove(string itemPath) => PropertyPath.Remove(Project, itemPath);
    public IEnumerable<(string name, string type, string value)> Describe(string path = "") => PropertyPath.Describe(Project, path, Units);

    // ------------------------------------------------------------------ typed helpers

    public Alignment Alignment(string? name = null) =>
        name == null ? Project.Alignments.FirstOrDefault() ?? throw new InvalidOperationException("The project has no alignments.")
            : Project.Alignments.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
              ?? throw new ArgumentException($"No alignment named '{name}'.");

    public BridgeDefinition Bridge(string? name = null) =>
        name == null ? Project.Bridges.FirstOrDefault() ?? throw new InvalidOperationException("The project has no bridges.")
            : Project.Bridges.FirstOrDefault(b => b.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
              ?? throw new ArgumentException($"No bridge named '{name}'.");

    /// <summary>Parses a length with units (or display units).</summary>
    public double Length(string text) => Units.TryParseLength(text, LengthKind.Long, out double v) ? v : throw new ArgumentException($"Can't read length '{text}'.");
    public double Station(string text) => Units.TryParseStation(text, out double v) ? v : throw new ArgumentException($"Can't read station '{text}'.");

    /// <summary>Creates an alignment from a PI table: (northing, easting, radius, spiral in, spiral out) in meters.</summary>
    public Alignment AddAlignment(string name, double startStation, IEnumerable<PiRow> pis, IEnumerable<Vpi>? vpis = null, CrossSlopeTable? crossSlope = null)
    {
        var def = new PiDefinition { Pis = pis.ToList() };
        var a = new Alignment(def.Build(out _)) { Name = name, StartStation = startStation, PiDefinition = def };
        if (vpis != null) a.Profile = new VerticalProfile { Vpis = vpis.ToList() };
        if (crossSlope != null) a.CrossSlope = crossSlope;
        Project.Alignments.Add(a);
        return a;
    }

    public IReadOnlyList<string> ImportLandXml(string path)
    {
        var res = LandXmlReader.Read(path);
        Project.Alignments.AddRange(res.Alignments);
        return res.Messages;
    }

    /// <summary>
    /// Imports a PGSuper / BridgeLink project (*.pgs, or a ReportExportAgent *_export.xml) as a prestressed girder bridge on
    /// one of this project's alignments. <paramref name="beginStation"/> = station of the first PGSuper pier line (NaN =
    /// PGSuper's own station when it lies on the alignment, else the alignment start). Returns the result with notes.
    /// </summary>
    public SBM.IO.PgSuper.PgSuperImportResult ImportPgSuper(string path, string? alignmentName = null, double beginStation = double.NaN, string? bridgeName = null)
    {
        var pg = SBM.IO.PgSuper.PgSuperReader.Read(path);
        var a = Alignment(alignmentName);
        if (double.IsNaN(beginStation))
        {
            double s0 = pg.Piers[0].Station;
            beginStation = s0 >= a.StartStation && s0 <= a.EndStation ? s0 : a.StartStation;
        }
        string name = bridgeName ?? (string.IsNullOrWhiteSpace(pg.BridgeName) ? Path.GetFileNameWithoutExtension(path) : pg.BridgeName);
        var res = SBM.IO.PgSuper.PgSuperImport.Apply(pg, a, beginStation, name, Units);
        Project.Bridges.Add(res.Bridge);
        return res;
    }

    /// <summary>Adds a bridge with default framing on an alignment: span lengths in meters from the begin station.</summary>
    public BridgeDefinition AddBridge(string name, string alignmentName, double beginStation, params double[] spans)
    {
        var b = BridgeDefinition.CreateDefault(beginStation, alignmentName);
        b.Name = name;
        if (spans.Length > 0)
        {
            b.Supports.Clear();
            double sta = beginStation;
            for (int i = 0; i <= spans.Length; i++)
            {
                bool end = i == 0 || i == spans.Length;
                b.Supports.Add(new SupportDefinition
                {
                    Name = end ? (i == 0 ? "Abut 1" : "Abut 2") : $"Pier {i}",
                    Type = end ? SupportType.Abutment : SupportType.Pier,
                    Station = sta,
                });
                if (i < spans.Length) sta += spans[i];
            }
            b.FieldSplices.Clear();
        }
        Project.Bridges.Add(b);
        return b;
    }

    // ------------------------------------------------------------------ regeneration & results

    /// <summary>Regenerates changed bridges (framing + 3D steel); unchanged bridges are reused from cache.</summary>
    public RegenerationReport Regenerate(bool includeSteel = true)
    {
        var sw = Stopwatch.StartNew();
        EnsureTerrain();
        _models = _cache.Regenerate(Project, includeSteel);
        var warn = new List<string>();
        var err = new List<string>();
        foreach (var m in _models.OfType<BridgeModel>())
        {
            foreach (var d in m.Framing.Diagnostics.Concat(m.Steel?.Diagnostics ?? Enumerable.Empty<Diagnostic>())
                         .Concat(m.Prestressed?.Diagnostics ?? Enumerable.Empty<Diagnostic>())
                         .Concat((m.Prestressed?.ConcreteDiaphragms?.Diagnostics ?? Enumerable.Empty<Diagnostic>()).Where(x => x.Severity != DiagnosticSeverity.Info)))
                (d.Severity == DiagnosticSeverity.Error ? err : warn).Add($"{m.Definition.Name}: {d.Message}");
        }
        var ok = _models.OfType<BridgeModel>().ToList();
        return new RegenerationReport(Project.Bridges.Count, ok.Count(m => !m.FromCache), ok.Count(m => m.FromCache),
            ok.Sum(m => m.Framing.CrossFrames.Count), ok.Sum(m => m.Steel?.Parts.Count ?? 0), ok.Sum(m => m.Steel?.Bolts.Count ?? 0),
            ok.Sum(m => m.Steel?.Studs.Count ?? 0), ok.Sum(m => m.Steel?.SteelMass ?? 0), warn, err, sw.Elapsed);
    }

    /// <summary>Exact quantities of the elements that carry NURBS (project setting Geometry.ExactCurvedGeometry): exact volume / area from
    /// the NURBS vs the tessellated mesh, fit deviation. Empty when the setting is off.</summary>
    public List<NurbsQuantityRow> ExactQuantities(string? bridgeName = null) =>
        (bridgeName == null ? Project.Bridges.Select(b => b.Name) : new[] { bridgeName }).SelectMany(n => NurbsQuantities.Rows(Model(n))).ToList();

    /// <summary>Results for a bridge (regenerates if needed).</summary>
    public BridgeModel Model(string? bridgeName = null)
    {
        var b = Bridge(bridgeName);
        if (_models.Count != Project.Bridges.Count || _models.All(m => m?.Definition != b)) Regenerate();
        int i = Project.Bridges.IndexOf(b);
        return _models[i] ?? throw new InvalidOperationException($"Bridge '{b.Name}' could not be generated (missing alignment?).");
    }

    public BridgeFraming Framing(string? bridgeName = null) => Model(bridgeName).Framing;
    public SteelModel Steel(string? bridgeName = null) => Model(bridgeName).Steel ?? throw new InvalidOperationException("3D steel was not generated (framing errors).");
    public SBM.Core.Substructure.SubstructureModel Substructure(string? bridgeName = null) => Model(bridgeName).Substructure ?? throw new InvalidOperationException("Substructure was not generated (framing errors).");
    /// <summary>TxDOT prestressed girders (bridges whose girder kind is TxDotPrestressed).</summary>
    public SBM.Core.Prestressed.PrestressedModel Prestressed(string? bridgeName = null) => Model(bridgeName).Prestressed ?? throw new InvalidOperationException("This bridge has no prestressed girders (or framing errors).");
    /// <summary>Concrete end / pier diaphragms of a prestressed bridge (SupportDefinition.Diaphragm), with their diagnostics (incl. Info rows).</summary>
    public SBM.Core.Diaphragms.ConcreteDiaphragmModel Diaphragms(string? bridgeName = null) =>
        Prestressed(bridgeName).ConcreteDiaphragms ?? throw new InvalidOperationException("This bridge has no concrete diaphragms (no SupportDefinition.Diaphragm).");
    public SBM.Core.Concrete.ConcreteModel Concrete(string? bridgeName = null) => Model(bridgeName).Concrete ?? throw new InvalidOperationException("Concrete was not generated (framing errors).");

    /// <summary>Hand-checkable elevation chains (profile grade → deck → haunch → web → flange → sole plate → bearing seat) for every bearing.</summary>
    public List<SBM.Core.Checks.ElevationChain> BearingElevations(string? bridgeName = null)
    {
        var m = Model(bridgeName);
        // Other bridges' substructures: a shared pier is built by its owner bridge.
        var others = _models.Where(x => x != null && !ReferenceEquals(x, m)).Select(x => x!.Substructure).OfType<SBM.Core.Substructure.SubstructureModel>().ToList();
        return SBM.Core.Checks.ElevationCheck.AllBearings(m.Framing, m.Steel, Project.Units, m.Substructure, m.Prestressed, others);
    }

    /// <summary>Elevation chain at a girder web centerline at a station (finished grade down to bottom of steel).</summary>
    public SBM.Core.Checks.ElevationChain GirderElevations(int girder, double station, string? bridgeName = null) =>
        SBM.Core.Checks.ElevationCheck.GirderAt(Framing(bridgeName), girder, station, Project.Units);

    /// <summary>2D views (layout plan + elevation, true plan, typical section, bents, bar bending) for a bridge.</summary>
    public List<SheetView> Drawings(string? bridgeName = null, DrawingOptions? options = null)
    {
        var m = Model(bridgeName);
        var ctx = new ViewContext(m.Framing, m.Steel, m.Concrete, m.Substructure, _terrains is { Count: > 0 } ? GroundAt : null, Units,
            options ?? DrawingOptions.For(Units), m.Prestressed);
        return DrawingSet.Build(ctx);
    }

    /// <summary>Writes every 2D view as a DXF 2D model into a folder; returns the file paths.</summary>
    public List<string> ExportDrawings(string folder, string? bridgeName = null, DrawingOptions? options = null)
    {
        var b = Bridge(bridgeName);
        return Drawings(bridgeName, options).Select(v => SheetExport.Write(folder, b.Name, v, Units)).ToList();
    }

    /// <summary>The plan set on 11x17 (or another format): layout, typical section, girder elevations, steel and bearing
    /// details, bents, bar lists — each view at the largest standard scale that fits.</summary>
    public List<SBM.Core.Drawing.Sheets.Sheet> Sheets(string? bridgeName = null, double widthIn = 17, double heightIn = 11)
    {
        var m = Model(bridgeName);
        var ctx = new ViewContext(m.Framing, m.Steel, m.Concrete, m.Substructure, _terrains is { Count: > 0 } ? GroundAt : null, Units, DrawingOptions.For(Units), m.Prestressed);
        return SBM.Core.Drawing.Sheets.SheetSet.Build(ctx, new SBM.Core.Drawing.Sheets.SheetFormat(widthIn, heightIn, Project.Name, m.Definition.Name, DateTime.Now.ToString("MM/dd/yyyy")));
    }

    /// <summary>Writes the plan set as one vector PDF; returns the number of sheets.</summary>
    public int ExportPdf(string path, string? bridgeName = null)
    {
        var sheets = Sheets(bridgeName);
        SBM.IO.Pdf.PdfWriter.Write(path, sheets, Bridge(bridgeName).Name);
        return sheets.Count;
    }

    /// <summary>Writes the plan set PDF and a JSON list of its sheets (number, title, subtitle, view names, page) for the side-by-side comparison with the plans.</summary>
    public int ExportSheetsWithMeta(string pdfPath, string metaPath, string? bridgeName = null)
    {
        var sheets = Sheets(bridgeName);
        string bridge = Bridge(bridgeName).Name;
        SBM.IO.Pdf.PdfWriter.Write(pdfPath, sheets, bridge);
        var list = sheets.Select((s, i) => new { number = s.Number, title = s.Title, subtitle = s.Subtitle, bridge, page = i + 1, views = s.ScaleNotes.Select(n => n.Split(':')[0]).Distinct().ToList() });
        System.IO.File.WriteAllText(metaPath, System.Text.Json.JsonSerializer.Serialize(list));
        return sheets.Count;
    }

    // ------------------------------------------------------------------ export

    /// <param name="chordTolerance">ReferenceView only: max sagitta (m) of hole / profile-arc polygons; 0 = 1/32 in.</param>
    public IfcExportResult ExportIfc(string path, IfcGeometryMode geometry = IfcGeometryMode.Parametric, double chordTolerance = 0)
    {
        Regenerate();
        var data = _models.OfType<BridgeModel>().Where(m => m.Steel != null || m.Prestressed != null).Select(m => new IfcBridgeData(m.Framing, m.Steel, m.Concrete, m.Substructure, m.Prestressed)).ToList();
        return IfcExporter.Export(path, Project, data, new IfcExportOptions(geometry, chordTolerance));
    }

    /// <summary>Writes the companion SQLite database (element registry with IFC GlobalIds and hierarchy, plan sheets, sheet links, QC).</summary>
    public SBM.IO.Database.CompanionResult ExportDatabase(string? path = null, SBM.IO.Database.DatabaseOptions? options = null)
    {
        Regenerate();
        options ??= new SBM.IO.Database.DatabaseOptions();
        options.ModelFile ??= FilePath;
        path ??= FilePath != null ? SBM.IO.Database.CompanionDatabase.DefaultPath(FilePath) : throw new InvalidOperationException("Pass a database path.");
        var data = _models.OfType<BridgeModel>().Select(m => new IfcBridgeData(m.Framing, m.Steel, m.Concrete, m.Substructure, m.Prestressed)).ToList();
        return SBM.IO.Database.CompanionDatabase.Build(path, Project, data, options);
    }

    public void ExportDxf(string path)
    {
        Regenerate(includeSteel: false);
        var dwg = new PlanDrawing();
        foreach (var a in Project.Alignments) Merge(dwg, PlanDrawingBuilder.Build(a, null, Units));
        foreach (var m in _models.OfType<BridgeModel>()) Merge(dwg, PlanDrawingBuilder.Build(null, m.Framing, Units));
        DxfWriter.Write(path, dwg, Units.LongUnit);
    }

    public void ExportLandXml(string path) => LandXmlWriter.Write(path, Project.Alignments, Units);

    private static void Merge(PlanDrawing target, PlanDrawing src)
    {
        foreach (var l in src.Layers)
        {
            var t = target.Layer(l.Style);
            t.Polylines.AddRange(l.Polylines);
            t.Texts.AddRange(l.Texts);
        }
    }
}
