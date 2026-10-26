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

    /// <summary>Wraps an in-memory project together with its already generated results (one per bridge, in project order), so result
    /// queries (<see cref="Model"/>) do not regenerate. Used by the app's calc comparison.</summary>
    public static SbmDocument Attach(BridgeProject project, string? path, IReadOnlyList<BridgeModel?> models) => new(project, path) { _models = models.ToList() };

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
                         .Concat((m.Prestressed?.ConcreteDiaphragms?.Diagnostics ?? Enumerable.Empty<Diagnostic>()).Where(x => x.Severity != DiagnosticSeverity.Info))
                         .Concat((m.Substructure?.SurfaceTreatments?.Diagnostics ?? Enumerable.Empty<Diagnostic>()).Where(x => x.Severity != DiagnosticSeverity.Info))
                         .Concat((m.Concrete?.Segmental?.Diagnostics ?? Enumerable.Empty<Diagnostic>()).Where(x => x.Severity != DiagnosticSeverity.Info)))
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

    /// <summary>Steel solid-vs-solid interference per bridge (<see cref="SBM.Core.Checks.SteelClashCheck"/>); all bridges when no name is given.</summary>
    public List<SBM.Core.Checks.ClashReport> Clashes(string? bridgeName = null, SBM.Core.Checks.ClashOptions? options = null)
    {
        var o = options ?? new SBM.Core.Checks.ClashOptions();
        var names = bridgeName != null ? new[] { Bridge(bridgeName).Name } : Project.Bridges.Select(b => b.Name).ToArray();
        return names.Select(n => SBM.Core.Checks.SteelClashCheck.Run(Model(n), o, Units)).ToList();
    }

    /// <summary>
    /// Projects model solids onto a view (<see cref="SBM.Core.Drawing.Projection.ModelProjector"/>: orthographic or developed, depth slab,
    /// hidden-line removal). <paramref name="bridgeFilter"/> = bridges whose name contains it (null = all bridges).
    /// </summary>
    public SBM.Core.Drawing.Projection.ProjectionResult ProjectView(SBM.Core.Drawing.Projection.IViewMap view, SBM.Core.Drawing.Projection.ProjectionOptions? options = null,
        SBM.Core.Drawing.Projection.ProjectionSourceOptions? source = null, string? bridgeFilter = null) =>
        SBM.Core.Drawing.Projection.ModelProjector.Project(ProjectionItems(source, bridgeFilter), view, options);

    /// <summary>The projectable elements (steel parts, concrete, substructure ...) of the bridges whose name contains <paramref name="bridgeFilter"/>.</summary>
    public List<SBM.Core.Drawing.Projection.ProjectionItem> ProjectionItems(SBM.Core.Drawing.Projection.ProjectionSourceOptions? source = null, string? bridgeFilter = null)
    {
        var names = Project.Bridges.Select(b => b.Name).Where(n => bridgeFilter == null || n.Contains(bridgeFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        if (names.Count == 0) throw new ArgumentException($"No bridge matches '{bridgeFilter}'.");
        return names.SelectMany(n => SBM.Core.Drawing.Projection.ModelProjectionSource.Items(Model(n), source)).ToList();
    }

    public BridgeFraming Framing(string? bridgeName = null) => Model(bridgeName).Framing;
    public SteelModel Steel(string? bridgeName = null) => Model(bridgeName).Steel ?? throw new InvalidOperationException("3D steel was not generated (framing errors).");
    public SBM.Core.Substructure.SubstructureModel Substructure(string? bridgeName = null) => Model(bridgeName).Substructure ?? throw new InvalidOperationException("Substructure was not generated (framing errors).");
    /// <summary>TxDOT prestressed girders (bridges whose girder kind is TxDotPrestressed).</summary>
    public SBM.Core.Prestressed.PrestressedModel Prestressed(string? bridgeName = null) => Model(bridgeName).Prestressed ?? throw new InvalidOperationException("This bridge has no prestressed girders (or framing errors).");
    /// <summary>Concrete end / pier diaphragms of a prestressed bridge (SupportDefinition.Diaphragm), with their diagnostics (incl. Info rows).</summary>
    public SBM.Core.Diaphragms.ConcreteDiaphragmModel Diaphragms(string? bridgeName = null) =>
        Prestressed(bridgeName).ConcreteDiaphragms ?? throw new InvalidOperationException("This bridge has no concrete diaphragms (no SupportDefinition.Diaphragm).");
    /// <summary>Precast segmental box of a bridge with <c>BridgeDefinition.Segmental</c> (segments, tendons, bearings, bents, checks, diagnostics incl. Info).</summary>
    public SBM.Core.Segmental.SegmentalModel Segmental(string? bridgeName = null) =>
        Model(bridgeName).Concrete?.Segmental ?? throw new InvalidOperationException("This bridge has no segmental box (or framing errors).");
    public SBM.Core.Concrete.ConcreteModel Concrete(string? bridgeName = null) => Model(bridgeName).Concrete ?? throw new InvalidOperationException("Concrete was not generated (framing errors).");

    /// <summary>Hand-checkable elevation chains (profile grade â†’ deck â†’ haunch â†’ web â†’ flange â†’ sole plate â†’ bearing seat) for every bearing.</summary>
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

    /// <summary>Writes every 2D view as a DXF 2D model into a folder, or into one zip when <paramref name="folder"/> ends in ".zip";
    /// returns the file paths (folder) or zip entry names.</summary>
    public List<string> ExportDrawings(string folder, string? bridgeName = null, DrawingOptions? options = null)
    {
        var b = Bridge(bridgeName);
        return SheetExport.WriteAll(folder, Drawings(bridgeName, options).Select(v => ("", b.Name, v)), Units);
    }

    /// <summary>Writes every sheet of the plan set as a paper-space DXF (inches / mm, border and title block included) into a folder,
    /// or into one zip when <paramref name="target"/> ends in ".zip"; <paramref name="includeModels"/> adds the full-size 2D model
    /// DXFs (<see cref="Drawings"/>) in a "models" subfolder. Returns the file paths / entry names.</summary>
    public List<string> ExportSheetsDxf(string target, string? bridgeName = null, bool includeModels = false)
    {
        var b = Bridge(bridgeName);
        return SheetExport.WriteSheets(target, b.Name, Sheets(bridgeName), Units, includeModels ? Drawings(bridgeName) : null);
    }

    /// <summary>The plan set on 11x17 (or another format): layout, typical section, girder elevations, steel and bearing
    /// details, bents, bar lists â€” each view at the largest standard scale that fits.</summary>
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

    // ------------------------------------------------------------------ measure (read-only QC tools)

    private SBM.Core.Checks.Measure.MeasureModel? _measure;
    private List<BridgeModel?>? _measureFor;

    /// <summary>Read-only measure view of the regenerated model (parts, assemblies, bars, every solid) with IFC GlobalIds attached, for
    /// <c>sbm measure</c> / <c>sbm section</c> (<see cref="SBM.Core.Checks.Measure"/>). Regenerates when needed; cached until the next regeneration.</summary>
    public SBM.Core.Checks.Measure.MeasureModel MeasureModel()
    {
        Regenerate();
        if (_measure != null && ReferenceEquals(_measureFor, _models)) return _measure;
        string key = Project.Name;
        string? Gid(SBM.Core.Checks.Measure.ElementRef r)
        {
            string bk = IfcKeys.Bridge(key, r.Bridge);
            string? k = r.Kind switch
            {
                "part" => IfcKeys.Part(bk, r.Mark),
                "assembly" => IfcKeys.Assembly(bk, r.Mark),
                "deck" when r.Mark == "Deck slab" => IfcKeys.DeckSlab(bk),
                "haunch" => IfcKeys.DeckSlab(bk),           // the haunches are an item of the deck slab in the IFC
                "barrier" => IfcKeys.Barrier(bk, r.Mark),
                "caphaunch" when r.Assembly != null => IfcKeys.CapHaunch(bk, r.Assembly),
                "sub" when r.Unit != null && r.Index is { } i => IfcKeys.SubSolid(IfcKeys.Unit(bk, r.Unit), i),
                "psbeam" => IfcKeys.PsBeam(bk, r.Mark),
                "deckbar" when r.Index is { } i => IfcKeys.DeckBar(bk, i),
                "subbar" when r.Unit != null && r.Index is { } i => IfcKeys.SubBar(IfcKeys.Unit(bk, r.Unit), i),
                "psbar" when r.Assembly != null && r.Index is { } i => IfcKeys.PsBar(bk, r.Assembly, i),
                _ => null,
            };
            return k == null ? null : IfcKeys.GlobalId(k);
        }
        _measureFor = _models;
        return _measure = new SBM.Core.Checks.Measure.MeasureModel(_models.OfType<BridgeModel>(), Units) { GlobalIds = Gid };
    }

    // ------------------------------------------------------------------ export

    /// <param name="chordTolerance">ReferenceView only: max sagitta (m) of hole / profile-arc polygons; 0 = 1/32 in.</param>
    /// <param name="forCheck">Checking export: the originator's interpretation is stripped (see IfcCheckFilter, docs/API.md); geometry is identical.</param>
    public IfcExportResult ExportIfc(string path, IfcGeometryMode geometry = IfcGeometryMode.Parametric, double chordTolerance = 0, bool forCheck = false, bool keepDesignations = false)
    {
        Regenerate();
        var data = _models.OfType<BridgeModel>().Where(m => m.Steel != null || m.Prestressed != null || m.Concrete?.Segmental != null || m.Framing.Definition.SupportLinesOnly).Select(m => new IfcBridgeData(m.Framing, m.Steel, m.Concrete, m.Substructure, m.Prestressed)).ToList();
        return IfcExporter.Export(path, Project, data, new IfcExportOptions(geometry, chordTolerance, forCheck, keepDesignations));
    }

    /// <summary>Writes a Rhino 3DM file (rhino3dm): exact extrusions / B-reps where the model records them, meshes elsewhere, fastener blocks,
    /// bar / strand / alignment curves, layers per bridge = the viewport layers, user text with the IFC GlobalIds. Coordinates are relative to the
    /// IFC export base point (same local frame as <see cref="ExportIfc"/>), in the project's display length unit. See docs/API.md.</summary>
    /// <param name="bridges">Bridge names to write (null = all).</param>
    /// <param name="detail">Bolts, rivets, studs, anchor bolts, welds, bars, strands, tendons.</param>
    /// <param name="meshOnly">Every solid as a mesh.</param>
    /// <param name="terrain">Include the visible terrain references (display meshes).</param>
    /// <param name="barSolids">Also write bars / strands / tendons as tube meshes.</param>
    public SBM.IO.Rhino3dm.Rhino3dmResult Export3dm(string path, IReadOnlyList<string>? bridges = null, bool detail = true, bool meshOnly = false,
        bool terrain = false, bool barSolids = false)
    {
        Regenerate();
        var all = _models.OfType<BridgeModel>().ToList();
        // same base point as the IFC export (computed from the bridges the IFC writes, whatever subset goes to the 3DM)
        var ifcBridges = all.Where(m => m.Steel != null || m.Prestressed != null || m.Concrete?.Segmental != null || m.Framing.Definition.SupportLinesOnly).Select(m => m.Framing);
        var bp = IfcExporter.BasePoint(Project, ifcBridges);
        List<(string, SBM.Core.Solids.Mesh)>? terr = null;
        if (terrain)
        {
            EnsureTerrain();
            var defs = Project.References.Where(r => r.Kind == ReferenceKind.Terrain && r.Visible).ToList();
            terr = _terrains!.Select((t, i) => (defs.Count == _terrains!.Count ? Path.GetFileNameWithoutExtension(defs[i].Path) : $"Terrain {i + 1}", t.Mesh)).ToList();
        }
        var o = new SBM.IO.Rhino3dm.Rhino3dmOptions
        {
            Bridges = bridges, Detail = detail, MeshOnly = meshOnly, BarSolids = barSolids, Terrain = terr, BasePoint = bp,
            Source = FilePath != null ? Path.GetFileName(FilePath) : null,
        };
        return SBM.IO.Rhino3dm.Rhino3dmExporter.Export(path, Project, all, o);
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
