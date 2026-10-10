using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SBM.App.Editors;
using SBM.App.Viewport;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Drawing;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Project;
using SBM.Core.References;
using SBM.IO.References;
using SBM.Core.Units;
using SBM.Core.Steel;
using SBM.IO.Dxf;
using SBM.IO.Ifc;
using SBM.IO.LandXml;

namespace SBM.App.ViewModels;

public sealed partial class LayerItem : ObservableObject
{
    public required string Name { get; init; }
    public required Brush Swatch { get; init; }
    public int Segments { get; init; }
    [ObservableProperty] private bool _isVisible = true;
    public event Action<LayerItem>? VisibilityChanged;
    partial void OnIsVisibleChanged(bool value) => VisibilityChanged?.Invoke(this);
}

public sealed record MessageItem(string Severity, string Text, string Icon, Brush Color, bool IsDiagnostic);

public sealed record PropertyRow(string Name, string Value);

public sealed record BearingRow(string Support, string Girder, string Station, string Offset, string Northing, string Easting,
    string TopOfSteel, string BottomOfSteel, string Deck, string Seat, string SolePlate, string Ground);

public sealed record MaterialRow(string Bridge, string Item, string Size, int Quantity, string TotalLength, string Weight);

public sealed record CrossFrameRow(string Mark, string Span, string Bay, string Type, string Station, string PlanLength, string LeftDist, string RightDist);

public sealed partial class ExplorerNode : ObservableObject
{
    public required string Header { get; init; }
    public string Icon { get; init; } = "";
    public ObservableCollection<ExplorerNode> Children { get; } = new();
    public object? Tag { get; init; }
    public Func<IEnumerable<PropertyRow>>? Properties { get; init; }
    public Action? Open { get; init; }
    [ObservableProperty] private bool _isExpanded = true;
}

public sealed partial class MainViewModel : ObservableObject
{
    private BridgeProject _project = new();
    private readonly Stack<string> _undo = new();
    private readonly Stack<string> _redo = new();
    private int _regenVersion;
    private readonly RegenerationCache _regenCache = new();

    public BridgeProject Project => _project;
    public UnitSystem Units => _project.Units;

    public Dictionary<BridgeDefinition, BridgeFraming> Framings { get; } = new();
    public Dictionary<BridgeDefinition, SteelModel> SteelModels { get; } = new();
    public Dictionary<BridgeDefinition, SBM.Core.Concrete.ConcreteModel> ConcreteModels { get; } = new();
    public Dictionary<BridgeDefinition, SBM.Core.Substructure.SubstructureModel> SubstructureModels { get; } = new();
    public Dictionary<BridgeDefinition, SBM.Core.Prestressed.PrestressedModel> PrestressedModels { get; } = new();
    public ObservableCollection<MaterialRow> MaterialRows { get; } = new();

    public ObservableCollection<ExplorerNode> Explorer { get; } = new();
    public ObservableCollection<LayerItem> LayerItems { get; } = new();
    public ObservableCollection<MessageItem> Messages { get; } = new();
    public ObservableCollection<PropertyRow> SelectedProperties { get; } = new();
    public ObservableCollection<BearingRow> BearingRows { get; } = new();
    public ObservableCollection<CrossFrameRow> CrossFrameRows { get; } = new();

    [ObservableProperty] private string? _filePath;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private string _cursorText = "";
    [ObservableProperty] private string _unitsText = "US (ft)";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private ExplorerNode? _selectedNode;
    [ObservableProperty] private string _windowTitle = "Steel Bridge Modeler";

    /// <summary>Raised on the UI thread when new GPU geometry is ready.</summary>
    public event Action<SceneData, bool>? SceneReady;
    /// <summary>Raised when the view should zoom to extents.</summary>
    public event Action? ZoomExtentsRequested;
    public event Action<bool>? ViewModeRequested;

    public SceneData? Scene { get; private set; }

    public MainViewModel()
    {
        UpdateTitle();
        // Automatic foundation tops follow the attached terrain.
        _regenCache.Ground = p => GroundElevation(p);
    }

    partial void OnFilePathChanged(string? value) => UpdateTitle();
    partial void OnIsDirtyChanged(bool value) => UpdateTitle();

    partial void OnSelectedNodeChanged(ExplorerNode? value)
    {
        SelectedProperties.Clear();
        if (value?.Properties == null) return;
        foreach (var p in value.Properties()) SelectedProperties.Add(p);
    }

    /// <summary>Fills the Properties panel for an object picked in the 3D view; returns a short title.</summary>
    public string ShowPicked(object tag)
    {
        var u = Units;
        string L(double m) => u.FormatLength(m);
        string S(double m) => u.FormatLength(m, LengthKind.Short);
        string E(double z) => u.FormatLength(z, LengthKind.Coordinate);
        string W(double kg) => u.IsUS ? $"{kg * 2.20462:N0} lb" : $"{kg:N0} kg";
        string CY(double m3) => u.IsUS ? $"{m3 / 0.764554857984:N2} CY" : $"{m3:N2} m³";
        var rows = new List<PropertyRow>();
        void R(string n, string v) => rows.Add(new PropertyRow(n, v));
        string title;
        switch (tag)
        {
            case SteelPart p:
            {
                title = $"{p.Mark} — {p.Description}";
                R("Mark", p.Mark);
                R("Type", ChoiceField.Humanize(p.Category.ToString()) + (p.Role != default ? $" ({ChoiceField.Humanize(p.Role.ToString())})" : ""));
                R("Size", p.Description);
                R("Assembly", p.Assembly);
                var asm = SteelModels.Values.SelectMany(m => m.Assemblies).FirstOrDefault(a => a.Mark == p.Assembly);
                if (asm != null) R("Assembly description", asm.Description);
                R("Material", p.Material);
                if (p.Length > 0) R("Length", L(p.Length));
                if (p.Category != PartCategory.Weld) R("Weight", W(p.Mass));
                var b = p.Mesh.Bounds();
                R("Top elevation", E(b.Max.Z));
                R("Bottom elevation", E(b.Min.Z));
                break;
            }
            case BoltInstance bolt:
            {
                bool anchor = bolt.Kind == BoltKind.Anchor;
                var sm = SteelModels.Values.FirstOrDefault();
                title = anchor ? $"Anchor bolt — {bolt.Assembly}" : $"Bolt — {bolt.Assembly}";
                R("Type", anchor ? "Anchor bolt with plate washer and nuts" : $"High-strength bolt {sm?.BoltGrade}");
                R("Diameter", S(anchor ? bolt.Diameter : sm?.Bolt.Diameter ?? bolt.Diameter));
                R("Length", S(bolt.Length));
                R(anchor ? "Plate thickness" : "Grip", S(bolt.Grip));
                if (anchor) R("Embedment", S(bolt.Embedment));
                R("Assembly", bolt.Assembly);
                var o = bolt.Frame.Origin;
                var a = _project.Alignments.FirstOrDefault();
                if (a != null) { var so = a.Inverse(o.XY); R("Station / offset", $"{u.FormatStation(so.Station)} / {L(so.Offset)}"); }
                R("Elevation (head side)", E(o.Z));
                break;
            }
            case StudInstance stud:
                title = "Shear stud";
                R("Diameter", S(SteelModels.Values.FirstOrDefault()?.StudDiameter ?? 0));
                R("Length", S(stud.Length));
                R("Girder field section", stud.Assembly);
                R("Elevation (top of flange)", E(stud.Frame.Origin.Z));
                break;
            case RebarPick rp:
            {
                var m = rp.Mark;
                double len = 0;
                for (int i = 1; i < rp.Bar.Points.Length; i++) len += (rp.Bar.Points[i] - rp.Bar.Points[i - 1]).Length;
                title = $"Bar {(string.IsNullOrEmpty(m.Unit) ? "" : m.Unit + " ")}{m.Mark} ({m.Size.Name})";
                R("Mark", m.Mark);
                R("Unit", string.IsNullOrEmpty(m.Unit) ? "Superstructure" : m.Unit);
                R("Size", m.Size.Name);
                R("Shape", m.Shape.Description);
                R("Length (this bar)", L(len));
                R("Mark length", m.Varies ? $"{L(m.MinLength)} to {L(m.Length)} (varies)" : L(m.Length));
                foreach (var d in m.Shape.Dimensions) R($"Dimension {d.Letter}", S(d.Value));
                R("Number of bars in mark", m.Count.ToString());
                R("Location", m.Location);
                R("Coating", m.Epoxy ? "Epoxy" : "Uncoated");
                R("Weight (mark total)", W(m.TotalMass));
                break;
            }
            case SubPick sp:
            {
                var s = sp.Solid;
                title = s.Name;
                R("Element", ChoiceField.Humanize(s.Kind.ToString()));
                R("Description", s.Description);
                R("Support", sp.Unit.Name);
                var b = s.Mesh.Bounds();
                R("Top elevation", E(b.Max.Z));
                R("Bottom elevation", E(b.Min.Z));
                R(s.IsConcrete ? "Concrete" : "Steel weight", s.IsConcrete ? CY(Math.Abs(s.Mesh.Volume())) : W(Math.Abs(s.Mesh.Volume()) * 7850));
                if (s.Kind == SBM.Core.Substructure.SubElementKind.Cap) R("Cap top", sp.Unit.CapTopDescription);
                if (s.Kind == SBM.Core.Substructure.SubElementKind.Pedestal)
                    foreach (var p in sp.Unit.Pedestals.Where(p => (s.Name + " ").Contains($" G{p.Girder + 1} ")))
                    {
                        R("Bearing seat", E(p.SeatZ));
                        R("Cap top at bearing", E(p.CapTopZ));
                        R("Pedestal height", S(p.Height));
                    }
                break;
            }
            case PsBeamPick pb:
            {
                var bm = pb.Beam;
                var pd = pb.Model.Definition;
                title = $"{bm.Mark} — {bm.Shape.Name} prestressed girder";
                R("Mark", bm.Mark);
                R("Girder / span", $"G{bm.Girder + 1}, span {bm.Span + 1}");
                R("Type", $"TxDOT {bm.Shape.Name} (IGD)");
                R("Depth", S(bm.Shape.Depth));
                R("Length (end to end)", L(bm.Length));
                R("CL bearing to CL bearing", L(bm.BearingSpan));
                R("Skew at ends", $"{bm.SkewStart:0.##}° / {bm.SkewEnd:0.##}°");
                R("Grade (top of beam)", $"{bm.Slope * 100:0.###}%");
                int dep = bm.Strands.Count(s => s.Depressed);
                R("Strands", $"{bm.Strands.Count} – {SBM.Core.Prestressed.TxGirderCatalog.StrandSize(pd.StrandDiameter, u)} dia, {pd.StrandStrength:0} ksi low-relaxation");
                R("Depressed strands", dep == 0 ? "None" : $"{dep}, hold-downs {L(bm.HoldDownFromEnd)} from each end");
                double cgMid = bm.Strands.Average(s => s.Section.Y), cgEnd = bm.Strands.Average(s => s.EndHeight);
                R("e at CL / at ends", $"{S(bm.Shape.Yb * 0.0254 - cgMid)} / {S(bm.Shape.Yb * 0.0254 - cgEnd)}");
                R("f'c / f'ci", $"{pd.ConcreteStrength:0.0#} / {pd.ReleaseStrength:0.0#} ksi");
                R("Area / Ix (IGD)", $"{bm.Shape.Area:N0} in² / {bm.Shape.Ix:N0} in⁴");
                R("Weight (155 pcf)", W(bm.Mass));
                R("Concrete", CY(bm.Volume));
                R("Top of beam at bearings", $"{E(bm.BearingStart.Z)} / {E(bm.BearingEnd.Z)}");
                R("Haunch (min / max)", $"{S(bm.MinHaunch)} / {S(bm.MaxHaunch)}");
                break;
            }
            case PsStrandPick st:
            {
                title = $"{(st.Strand.Depressed ? "Depressed strand" : "Strand")} — {st.Beam.Mark}";
                R("Girder", st.Beam.Mark);
                R("Diameter", SBM.Core.Prestressed.TxGirderCatalog.StrandSize(st.Beam.StrandDiameter, u));
                R("Position at CL (x, y)", $"{S(st.Strand.Section.X)}, {S(st.Strand.Section.Y)} above bottom");
                if (st.Strand.Depressed) R("Height at girder ends", S(st.Strand.EndHeight));
                R("Length", L(SBM.Core.Prestressed.PrestressedModel.PolyLength(st.Strand.Points)));
                break;
            }
            case PsPadPick pp:
            {
                var p = pp.Pad;
                title = $"Bearing pad {p.Mark}";
                R("Support", p.Support);
                R("Girder", $"G{p.Girder + 1}, span {p.Span + 1}");
                R("Size (L x W x T)", $"{S(p.Length)} x {S(p.Width)} x {S(p.Thickness)}");
                R("Top / bottom elevation", $"{E(p.Center.Z + p.Thickness / 2)} / {E(p.Center.Z - p.Thickness / 2)}");
                break;
            }
            case NamedPick np:
                title = np.Name;
                R("Element", np.Kind);
                foreach (var (n, v) in np.Properties) R(n, v);
                break;
            default:
                title = tag.ToString() ?? "";
                break;
        }
        SelectedProperties.Clear();
        SelectedProperties.Add(new PropertyRow("Selected", title));
        foreach (var r in rows) SelectedProperties.Add(r);
        StatusText = $"Selected {title}";
        return title;
    }

    private void UpdateTitle()
    {
        string name = FilePath != null ? Path.GetFileName(FilePath) : _project.Name;
        WindowTitle = $"{name}{(IsDirty ? " *" : "")} — Steel Bridge Modeler";
    }

    // ------------------------------------------------------------------ project lifecycle

    private void SetProject(BridgeProject p, string? path, bool zoom)
    {
        _project = p;
        FilePath = path;
        IsDirty = false;
        _undo.Clear();
        _redo.Clear();
        UnitsText = Units.IsUS ? "US (ft)" : "SI (m)";
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        UpdateTitle();
        _origin = null;
        _refCache.Clear();
        _regenCache.Clear();
        _ = LoadAfterRegen(zoom);
    }

    private async Task LoadAfterRegen(bool zoom)
    {
        await RegenerateAsync(zoom);
        await LoadReferencesAsync();
    }

    /// <summary>Snapshot before a change so it can be undone.</summary>
    public void Checkpoint()
    {
        _undo.Push(_project.ToJson());
        _redo.Clear();
        IsDirty = true;
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private bool ConfirmDiscard()
    {
        if (!IsDirty) return true;
        var r = MessageBox.Show("Save changes to the current project?", "Steel Bridge Modeler", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        if (r == MessageBoxResult.Cancel) return false;
        if (r == MessageBoxResult.Yes) return SaveInternal(false);
        return true;
    }

    public bool CanClose() => ConfirmDiscard();

    [RelayCommand]
    private void New()
    {
        if (!ConfirmDiscard()) return;
        SetProject(new BridgeProject(), null, true);
        Info("New project created. Import a LandXML alignment or create one with Alignment ▸ New.");
    }

    /// <summary>Opens a project file given on the command line (a *.sbm argument or --open path).</summary>
    public void OpenPath(string path)
    {
        try
        {
            SetProject(BridgeProject.Load(path), Path.GetFullPath(path), true);
            Info($"Opened {path}");
        }
        catch (Exception ex)
        {
            Error($"Could not open project: {ex.Message}");
        }
    }

    [RelayCommand]
    private void LoadSample()
    {
        if (!ConfirmDiscard()) return;
        var p = SampleProject.Create();
        try
        {
            // The sample's existing-ground TIN is generated once into the local app folder and referenced like any terrain.
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SteelBridgeModeler", "samples");
            Directory.CreateDirectory(dir);
            string tin = Path.Combine(dir, "Sample Existing Ground.xml");
            if (!File.Exists(tin))
                LandXmlWriter.WriteSurface(tin, "Existing Ground", SampleProject.CreateExistingGround(p.Alignments[0]), p.Units);
            p.References.Add(new ReferenceModelDefinition { Name = "Existing Ground", Path = tin, Kind = ReferenceKind.Terrain, R = 128, G = 108, B = 76, Opacity = 0.85 });
        }
        catch (IOException ex) { Warn($"Sample terrain not created: {ex.Message}"); }
        SetProject(p, null, true);
        Info("Loaded sample: spiral-curve alignment with 6% superelevation, a 2-span skewed girder bridge, and an existing-ground TIN.");
    }

    [RelayCommand]
    private void Open()
    {
        if (!ConfirmDiscard()) return;
        var dlg = new OpenFileDialog { Filter = "Steel Bridge Modeler project (*.sbm)|*.sbm|All files|*.*" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            SetProject(BridgeProject.Load(dlg.FileName), dlg.FileName, true);
            Info($"Opened {dlg.FileName}");
        }
        catch (Exception ex)
        {
            Error($"Could not open project: {ex.Message}");
        }
    }

    /// <summary>Remote control: saves to the given path (or the current file). Returns a message; throws when there is no path.</summary>
    public string SaveToPath(string? path)
    {
        path ??= FilePath ?? throw new InvalidOperationException("The project has never been saved; give a file name (save <file.sbm>).");
        _project.Save(path);
        FilePath = path;
        IsDirty = false;
        Info($"Saved {path}");
        return $"Saved {path}";
    }

    [RelayCommand]
    private void Save() => SaveInternal(false);

    [RelayCommand]
    private void SaveAs() => SaveInternal(true);

    private bool SaveInternal(bool saveAs)
    {
        string? path = FilePath;
        if (saveAs || path == null)
        {
            var dlg = new SaveFileDialog
            {
                Filter = "Steel Bridge Modeler project (*.sbm)|*.sbm",
                FileName = (path != null ? Path.GetFileNameWithoutExtension(path) : _project.Name) + BridgeProject.FileExtension,
            };
            if (dlg.ShowDialog() != true) return false;
            path = dlg.FileName;
        }
        try
        {
            _project.Save(path);
            FilePath = path;
            IsDirty = false;
            Info($"Saved {path}");
            return true;
        }
        catch (Exception ex)
        {
            Error($"Save failed: {ex.Message}");
            return false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        _redo.Push(_project.ToJson());
        RestoreSnapshot(_undo.Pop());
    }

    private bool CanUndo() => _undo.Count > 0;

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        _undo.Push(_project.ToJson());
        RestoreSnapshot(_redo.Pop());
    }

    private bool CanRedo() => _redo.Count > 0;

    private void RestoreSnapshot(string json)
    {
        _project = BridgeProject.FromJson(json);
        IsDirty = true;
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        _ = LoadAfterRegen(false);
    }

    [RelayCommand]
    private void ToggleUnits()
    {
        Checkpoint();
        Units.Kind = Units.IsUS ? UnitSystemKind.SI : UnitSystemKind.US;
        UnitsText = Units.IsUS ? "US (ft)" : "SI (m)";
        _ = RegenerateAsync(false);
    }

    // ------------------------------------------------------------------ alignments

    [RelayCommand]
    private void ImportLandXml()
    {
        var dlg = new OpenFileDialog { Filter = "LandXML (*.xml)|*.xml|All files|*.*", Title = "Import LandXML alignments" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var res = LandXmlReader.Read(dlg.FileName);
            foreach (var m in res.Messages) Warn(m);
            if (res.Alignments.Count == 0) return;
            Checkpoint();
            if (_project.Alignments.Count == 0 && _project.Bridges.Count == 0)
            {
                Units.Kind = res.Imperial ? UnitSystemKind.US : UnitSystemKind.SI;
                Units.Foot = res.UsSurveyFeet ? FootDefinition.USSurvey : FootDefinition.International;
                UnitsText = Units.IsUS ? "US (ft)" : "SI (m)";
            }
            foreach (var a in res.Alignments)
            {
                a.Name = UniqueAlignmentName(a.Name);
                _project.Alignments.Add(a);
                Info($"Imported alignment '{a.Name}': {a.Elements.Count} elements, length {Units.FormatLength(a.Length)}, " +
                     $"STA {Units.FormatStation(a.StartStation)} to {Units.FormatStation(a.EndStation)}" +
                     (a.Profile.IsEmpty ? ", no profile." : $", {a.Profile.Vpis.Count} VPIs."));
            }
            _ = RegenerateAsync(true);
        }
        catch (Exception ex)
        {
            Error($"LandXML import failed: {ex.Message}");
        }
    }

    /// <summary>Remote control: imports a PGSuper bridge without dialogs (empty alignment = the first; empty station = default).</summary>
    public async Task<string> ImportPgSuperFile(string path, string alignmentName, string station, string bridgeName)
    {
        var pg = SBM.IO.PgSuper.PgSuperReader.Read(path);
        var a = string.IsNullOrEmpty(alignmentName) ? _project.Alignments.FirstOrDefault() : _project.FindAlignment(alignmentName);
        if (a == null) throw new InvalidOperationException(string.IsNullOrEmpty(alignmentName) ? "The project has no alignment." : $"No alignment named '{alignmentName}'.");
        double s0 = pg.Piers[0].Station;
        double sta = s0 >= a.StartStation && s0 <= a.EndStation ? s0 : a.StartStation;
        if (!string.IsNullOrEmpty(station) && !Units.TryParseStation(station, out sta)) throw new ArgumentException($"Can't read the station '{station}'.");
        string name = !string.IsNullOrEmpty(bridgeName) ? bridgeName : string.IsNullOrWhiteSpace(pg.BridgeName) ? Path.GetFileNameWithoutExtension(path) : pg.BridgeName;
        var res = SBM.IO.PgSuper.PgSuperImport.Apply(pg, a, sta, name, Units);
        Checkpoint();
        while (_project.Bridges.Any(b => b.Name.Equals(res.Bridge.Name, StringComparison.OrdinalIgnoreCase))) res.Bridge.Name += " (2)";
        _project.Bridges.Add(res.Bridge);
        Info($"Imported PGSuper bridge '{res.Bridge.Name}' from {Path.GetFileName(path)} on '{a.Name}'.");
        foreach (var n in res.Notes) Info("  " + n);
        foreach (var w in res.Warnings) Warn("  " + w);
        await RegenerateAsync(true);
        return string.Join("\n", new[] { $"Imported '{res.Bridge.Name}' on '{a.Name}' at STA {Units.FormatStation(sta)}" }
            .Concat(res.Notes).Concat(res.Warnings.Select(w => "warning: " + w)));
    }

    [RelayCommand]
    private void ImportPgSuper()
    {
        if (_project.Alignments.Count == 0) { Warn("Import or create an alignment first; the PGSuper bridge is placed on it."); return; }
        var dlg = new OpenFileDialog
        {
            Filter = "PGSuper project (*.pgs)|*.pgs|PGSuper export (*_export.xml)|*.xml|All files|*.*",
            Title = "Import PGSuper bridge",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var pg = SBM.IO.PgSuper.PgSuperReader.Read(dlg.FileName);
            string name = string.IsNullOrWhiteSpace(pg.BridgeName) ? Path.GetFileNameWithoutExtension(dlg.FileName) : pg.BridgeName;
            var win = new Editors.PgSuperImportWindow(pg, _project.Alignments, Units, name) { Owner = Application.Current.MainWindow };
            if (win.ShowDialog() != true || win.Alignment == null) return;
            var res = SBM.IO.PgSuper.PgSuperImport.Apply(pg, win.Alignment, win.BeginStation, string.IsNullOrEmpty(win.BridgeName) ? name : win.BridgeName, Units);
            Checkpoint();
            while (_project.Bridges.Any(b => b.Name.Equals(res.Bridge.Name, StringComparison.OrdinalIgnoreCase))) res.Bridge.Name += " (2)";
            _project.Bridges.Add(res.Bridge);
            Info($"Imported PGSuper bridge '{res.Bridge.Name}' from {Path.GetFileName(dlg.FileName)} on '{win.Alignment.Name}':");
            foreach (var n in res.Notes) Info("  " + n);
            foreach (var w in res.Warnings) Warn("  " + w);
            _ = RegenerateAsync(true);
        }
        catch (Exception ex)
        {
            Log(ex.ToString());
            Error($"PGSuper import failed: {ex.Message}");
        }
    }

    private string UniqueAlignmentName(string name)
    {
        string n = name;
        int k = 2;
        while (_project.Alignments.Any(a => a.Name.Equals(n, StringComparison.OrdinalIgnoreCase))) n = $"{name} ({k++})";
        return n;
    }

    [RelayCommand]
    private void ExportLandXml()
    {
        if (_project.Alignments.Count == 0) { Warn("There are no alignments to export."); return; }
        var dlg = new SaveFileDialog { Filter = "LandXML (*.xml)|*.xml", FileName = _project.Name + " Alignments.xml" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            LandXmlWriter.Write(dlg.FileName, _project.Alignments, Units);
            Info($"Exported {_project.Alignments.Count} alignment(s) to {dlg.FileName}");
        }
        catch (Exception ex) { Error($"LandXML export failed: {ex.Message}"); }
    }

    [RelayCommand]
    private void NewAlignment()
    {
        double ft = Units.LongUnit;
        var a = new Alignment
        {
            Name = UniqueAlignmentName("Alignment"),
            StartStation = 1000 * ft,
            PiDefinition = new PiDefinition
            {
                Pis = { new PiRow(10000 * ft, 10000 * ft), new PiRow(10000 * ft, 11000 * ft) },
            },
            Profile = new VerticalProfile { Vpis = { new Vpi(1000 * ft, 500 * ft), new Vpi(2000 * ft, 500 * ft) } },
        };
        a.SetElements(a.PiDefinition.Build(out _));
        Alignment? added = null;
        AlignmentEditorWindow.Edit(a, Units, isNew: true, apply: edited =>
        {
            Checkpoint();
            // Apply may be pressed several times: add once, then replace.
            int i = added != null ? _project.Alignments.IndexOf(added) : -1;
            if (i >= 0) _project.Alignments[i] = edited; else _project.Alignments.Add(edited);
            added = edited;
            _ = RegenerateAsync(i < 0);
        });
        if (added != null) Info($"Created alignment '{added.Name}'.");
    }

    [RelayCommand]
    private void EditAlignment(Alignment? alignment)
    {
        alignment ??= SelectedNode?.Tag as Alignment ?? _project.Alignments.FirstOrDefault();
        if (alignment == null) { Warn("Create or import an alignment first."); return; }
        int index = _project.Alignments.IndexOf(alignment);
        string oldName = alignment.Name;
        AlignmentEditorWindow.Edit(alignment, Units, isNew: false, apply: edited =>
        {
            Checkpoint();
            _project.Alignments[index] = edited;
            foreach (var b in _project.Bridges.Where(b => b.AlignmentName == oldName)) b.AlignmentName = edited.Name;
            oldName = edited.Name;
            _ = RegenerateAsync(false);
        });
    }

    // ------------------------------------------------------------------ bridges

    [RelayCommand]
    private void NewBridge()
    {
        var align = SelectedNode?.Tag as Alignment ?? _project.Alignments.FirstOrDefault();
        if (align == null) { Warn("Create or import an alignment before adding a bridge."); return; }
        double begin = align.StartStation + Math.Min(align.Length * 0.25, 300 * Units.LongUnit);
        begin = Math.Round(begin / Units.LongUnit) * Units.LongUnit;
        var b = BridgeDefinition.CreateDefault(begin, align.Name);
        b.Name = $"Bridge {_project.Bridges.Count + 1}";
        BridgeDefinition? added = null;
        BridgeEditorWindow.Edit(b, _project, Units, 0, isNew: true, apply: edited =>
        {
            Checkpoint();
            int i = added != null ? _project.Bridges.IndexOf(added) : -1;
            if (i >= 0) _project.Bridges[i] = edited; else _project.Bridges.Add(edited);
            added = edited;
            _ = RegenerateAsync(i < 0);
        });
    }

    [RelayCommand]
    private void EditBridge(string? tab)
    {
        var bridge = SelectedNode?.Tag as BridgeDefinition ?? _project.Bridges.FirstOrDefault();
        if (bridge == null) { NewBridge(); return; }
        int index = _project.Bridges.IndexOf(bridge);
        int tabIndex = int.TryParse(tab, out int t) ? t : 0;
        BridgeEditorWindow.Edit(bridge, _project, Units, tabIndex, isNew: false, apply: edited =>
        {
            Checkpoint();
            _project.Bridges[index] = edited;
            _ = RegenerateAsync(false);
        });
    }

    [RelayCommand]
    private void Regenerate() => _ = RegenerateAsync(false);

    // ------------------------------------------------------------------ view

    [RelayCommand] private void ZoomExtents() => ZoomExtentsRequested?.Invoke();
    [RelayCommand] private void ViewPlan() => ViewModeRequested?.Invoke(true);
    [RelayCommand] private void View3D() => ViewModeRequested?.Invoke(false);

    [RelayCommand]
    private void ExportDxf()
    {
        if (Scene == null || _lastDrawing == null) { Warn("Nothing to export."); return; }
        var dlg = new SaveFileDialog { Filter = "DXF (*.dxf)|*.dxf", FileName = _project.Name + " Framing Plan.dxf" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            DxfWriter.Write(dlg.FileName, _lastDrawing, Units.LongUnit);
            Info($"Exported framing plan to {dlg.FileName} (units: {Units.LongUnitName}, true elevations on 3D polylines).");
        }
        catch (Exception ex) { Error($"DXF export failed: {ex.Message}"); }
    }

    /// <summary>Runs a C# script (.csx) against the open project, then regenerates. Undo restores the pre-script state.</summary>
    [RelayCommand]
    private async Task RunScript()
    {
        var dlg = new OpenFileDialog { Filter = "C# script (*.csx)|*.csx|All files|*.*", Title = "Run script" };
        if (dlg.ShowDialog() != true) return;
        Checkpoint();
        StatusText = $"Running {Path.GetFileName(dlg.FileName)}…";
        var doc = SBM.Api.SbmDocument.Attach(_project, FilePath);
        var sw = Stopwatch.StartNew();
        var res = await Task.Run(() => SBM.Api.ScriptHost.RunFileAsync(dlg.FileName, doc));
        foreach (var line in res.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries)) Info("script: " + line.TrimEnd());
        if (res.ReturnValue != null) Info("script returned: " + res.ReturnValue);
        if (res.Success) Info($"Script {Path.GetFileName(dlg.FileName)} finished in {sw.ElapsedMilliseconds} ms.");
        else Error($"Script failed: {res.Error}");
        await RegenerateAsync(false);
    }

    /// <summary>Remote control: runs a script against the open project (undoable), regenerates, returns its output.</summary>
    public async Task<string> RunScriptFile(string path, string[] args)
    {
        Checkpoint();
        var doc = SBM.Api.SbmDocument.Attach(_project, FilePath);
        var res = await Task.Run(() => SBM.Api.ScriptHost.RunFileAsync(path, doc, args));
        foreach (var line in res.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries)) Info("script: " + line.TrimEnd());
        await RegenerateAsync(false);
        if (!res.Success) throw new InvalidOperationException($"Script failed: {res.Error}\n{res.Output}");
        return res.Output + (res.ReturnValue != null ? $"returned: {res.ReturnValue}" : "");
    }

    // ------------------------------------------------------------------ terrain & reference models

    private Vec3? _origin;
    private readonly Dictionary<string, (DateTime mtime, string key, ReferenceGeometry geo, TerrainModel? terrain)> _refCache = new();
    private List<TerrainModel> _terrains = new();
    private List<string> _refLayers = new();
    public event Action<List<ReferenceRender>>? ReferencesReady;
    public IReadOnlyList<(string name, bool planOnly, bool threeDOnly)> ReferenceLayerModes { get; private set; } = Array.Empty<(string, bool, bool)>();

    [RelayCommand]
    private async Task ImportTerrain()
    {
        var dlg = new OpenFileDialog { Filter = "Terrain (LandXML TIN, DXF 3DFACE, OBJ, STL)|*.xml;*.dxf;*.obj;*.stl|All files|*.*", Title = "Import terrain model" };
        if (dlg.ShowDialog() != true) return;
        await AddReference(dlg.FileName, ReferenceKind.Terrain);
    }

    [RelayCommand]
    private async Task AttachReference()
    {
        var dlg = new OpenFileDialog { Filter = "Reference models (DXF, OBJ, STL, LandXML surface)|*.dxf;*.obj;*.stl;*.xml|All files|*.*", Title = "Attach reference model" };
        if (dlg.ShowDialog() != true) return;
        await AddReference(dlg.FileName, ReferenceKind.Reference);
    }

    private async Task AddReference(string path, ReferenceKind kind)
    {
        Checkpoint();
        string stored = path;
        if (FilePath != null)
        {
            var rel = Path.GetRelativePath(Path.GetDirectoryName(FilePath)!, path);
            if (!rel.StartsWith("..")) stored = rel;
        }
        string name = Path.GetFileNameWithoutExtension(path);
        int k = 2;
        while (_project.References.Any(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) name = $"{Path.GetFileNameWithoutExtension(path)} ({k++})";
        _project.References.Add(kind == ReferenceKind.Terrain
            ? new ReferenceModelDefinition { Name = name, Path = stored, Kind = kind, R = 128, G = 108, B = 76, Opacity = 0.85 }
            : new ReferenceModelDefinition { Name = name, Path = stored, Kind = kind });
        await LoadReferencesAsync();
    }

    [RelayCommand]
    private async Task RemoveReference()
    {
        var def = SelectedNode?.Tag as ReferenceModelDefinition;
        if (def == null) { Warn("Select a terrain or reference model in the Project Explorer first."); return; }
        Checkpoint();
        _project.References.Remove(def);
        await LoadReferencesAsync();
    }

    /// <summary>Loads (or reuses) all reference models in parallel and builds their GPU buffers off the UI thread.</summary>
    public async Task LoadReferencesAsync()
    {
        var defs = _project.References.ToList();
        var units = Units;
        string? projectFile = FilePath;
        var sw = Stopwatch.StartNew();
        if (defs.Count > 0) StatusText = $"Loading {defs.Count} reference model(s)…";
        var origin = _origin ?? Vec3.Zero;
        var loaded = await Task.Run(() => defs.AsParallel().AsOrdered().Select(def =>
        {
            string full = BridgeProject.ResolvePath(def.Path, projectFile);
            try
            {
                if (!File.Exists(full)) return (def, (ReferenceRender?)null, (TerrainModel?)null, $"{def.Name}: file not found ({full}).");
                var mtime = File.GetLastWriteTimeUtc(full);
                string key = $"{full}|{def.Format}|{def.UnitScale}|{def.OffsetX}|{def.OffsetY}|{def.OffsetZ}|{def.Kind}|{units.LongUnit}";
                ReferenceGeometry geo;
                TerrainModel? terrain;
                string msg = "";
                lock (_refCache)
                {
                    if (_refCache.TryGetValue(def.Name, out var c) && c.mtime == mtime && c.key == key) { geo = c.geo; terrain = c.terrain; goto build; }
                }
                geo = ReferenceReaders.Load(def, full, units.LongUnit);
                terrain = def.Kind == ReferenceKind.Terrain && !geo.Mesh.IsEmpty ? (geo.Grid is { } grd ? new TerrainModel(grd, geo.Mesh) : new TerrainModel(geo.Mesh)) : null;
                msg = $"{def.Name}: " + string.Join(" ", geo.Messages);
                lock (_refCache) _refCache[def.Name] = (mtime, key, geo, terrain);
            build:
                double ci = def.ContourInterval > 0 ? def.ContourInterval : (units.IsUS ? UnitSystem.Ft(2) : 0.5);
                var render = SceneBuilder.BuildReference(def, geo, terrain, origin, ci);
                return (def, (ReferenceRender?)render, terrain, msg);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or FormatException or System.Xml.XmlException or UnauthorizedAccessException)
            {
                return (def, null, null, $"{def.Name}: could not load — {ex.Message}");
            }
        }).ToList());

        var newTerrains = loaded.Where(x => x.Item3 != null && x.def.Visible).Select(x => x.Item3!).ToList();
        bool groundChanged = newTerrains.Count != _terrains.Count || newTerrains.Zip(_terrains).Any(p => !ReferenceEquals(p.First, p.Second));
        _terrains = newTerrains;
        if (groundChanged)
        {
            _regenCache.GroundVersion++;
            // Foundation tops set from the ground need regeneration.
            if (_project.Bridges.Any(b => b.Drains.Any(d => d.SplashBasin || d.Outlet == DrainOutlet.PipedToPier)
                    || b.Supports.Any(s => s.ResolvedSubstructure is { Enabled: true } sd && double.IsNaN(sd.FoundationTopElevation))))
                _ = RegenerateAsync(false);
        }
        var renders = loaded.Where(x => x.Item2 != null).Select(x => x.Item2!).ToList();
        foreach (var (_, _, _, msg) in loaded) if (!string.IsNullOrEmpty(msg)) (msg.Contains("could not") || msg.Contains("not found") ? (Action<string>)Warn : Info)(msg);
        var modes = new List<(string, bool, bool)>();
        foreach (var r in renders)
        {
            bool terrain = r.Definition.Kind == ReferenceKind.Terrain;
            modes.Add((r.LayerName, false, terrain));       // terrain surface: 3D only
            if (terrain) modes.Add((r.ContourLayerName, true, false)); // contours: plan only
        }
        ReferenceLayerModes = modes;
        _refLayers = modes.Select(m => m.Item1).ToList();
        if (Scene != null) RebuildLayers(Scene);
        RebuildExplorer();
        RebuildReports();
        ReferencesReady?.Invoke(renders);
        if (defs.Count > 0) StatusText = $"Loaded {renders.Count} reference model(s), {renders.Sum(r => r.Triangles):N0} triangles in {sw.ElapsedMilliseconds} ms";
    }

    public double? GroundElevation(Vec2 p)
    {
        foreach (var t in _terrains)
            if (t.TryElevation(p, out double z)) return z;
        return null;
    }

    [RelayCommand]
    private void ShowBarSchedule()
    {
        var bridge = SelectedNode?.Tag as BridgeDefinition ?? _project.Bridges.FirstOrDefault();
        if (bridge == null) { Warn("No reinforcement to schedule. Create a bridge first."); return; }
        ConcreteModels.TryGetValue(bridge, out var cm);
        SubstructureModels.TryGetValue(bridge, out var sm);
        if ((cm?.Marks.Count ?? 0) + (sm?.Marks.Count ?? 0) == 0) { Warn($"{bridge.Name} has no reinforcement (enable deck, rail or substructure reinforcing)."); return; }
        new BarScheduleWindow(bridge.Name, cm, sm, Units) { Owner = Application.Current.MainWindow }.Show();
    }

    [RelayCommand]
    private void ShowElevationCheck()
    {
        var bridge = SelectedNode?.Tag as BridgeDefinition ?? _project.Bridges.FirstOrDefault();
        if (bridge == null || !Framings.TryGetValue(bridge, out var f)) { Warn("No bridge to check. Create a bridge first."); return; }
        SteelModels.TryGetValue(bridge, out var sm);
        new ElevationCheckWindow(f, sm, Units, SubstructureModels.GetValueOrDefault(bridge), PrestressedModels.GetValueOrDefault(bridge)) { Owner = Application.Current.MainWindow }.Show();
    }

    /// <summary>Builds the 2D views (plan, elevation, sections, bents, bar bending) for the selected bridge.</summary>
    /// <summary>Everything the 2D views and sheets read, for the selected (or first) bridge.</summary>
    public SBM.Core.Drawing.Views.ViewContext? BuildDrawingContext()
    {
        var bridge = SelectedNode?.Tag as BridgeDefinition ?? _project.Bridges.FirstOrDefault();
        if (bridge == null || !Framings.TryGetValue(bridge, out var f) || f.HasErrors) return null;
        var terrains = _terrains;
        return new SBM.Core.Drawing.Views.ViewContext(f, SteelModels.GetValueOrDefault(bridge), ConcreteModels.GetValueOrDefault(bridge),
            SubstructureModels.GetValueOrDefault(bridge), terrains.Count > 0 ? p => GroundElevation(p) : null, Units, SBM.Core.Drawing.Views.DrawingOptions.For(Units),
            PrestressedModels.GetValueOrDefault(bridge));
    }

    /// <summary>The full-size 2D views (models) of the selected bridge.</summary>
    public (BridgeDefinition bridge, List<SBM.Core.Drawing.Views.SheetView> views)? BuildDrawings() =>
        BuildDrawingContext() is { } ctx ? (ctx.Framing.Definition, SBM.Core.Drawing.Views.DrawingSet.Build(ctx)) : null;

    /// <summary>Raised to switch the main window to the Sheets view.</summary>
    public event Action? SheetsRequested;

    [RelayCommand]
    private void ShowDrawings() => SheetsRequested?.Invoke();

    [RelayCommand]
    private void ExportIfc()
    {
        if (SteelModels.Count + PrestressedModels.Count == 0) { Warn("There is no 3D model to export. Create a bridge first."); return; }
        var dlg = new SaveFileDialog { Filter = "IFC 4.3 (*.ifc)|*.ifc|IFC 4.3 compressed (*.ifczip)|*.ifczip", FileName = _project.Name + ".ifc" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var data = Framings.Keys.Where(b => SteelModels.ContainsKey(b) || PrestressedModels.ContainsKey(b))
                .Select(b => new IfcBridgeData(Framings[b], SteelModels.GetValueOrDefault(b), ConcreteModels.GetValueOrDefault(b), SubstructureModels.GetValueOrDefault(b),
                    PrestressedModels.GetValueOrDefault(b))).ToList();
            var sw = Stopwatch.StartNew();
            var r = IfcExporter.Export(dlg.FileName, _project, data);
            Info($"Exported IFC 4.3 to {dlg.FileName}: {r.ElementCount:N0} elements, {r.EntityCount:N0} entities in {sw.ElapsedMilliseconds} ms. " +
                 $"Base point E {ValueFormat.Format(Units, ValueKind.Coordinate, r.BasePoint.X)}, N {ValueFormat.Format(Units, ValueKind.Coordinate, r.BasePoint.Y)} (IfcMapConversion).");
        }
        catch (Exception ex) { Error($"IFC export failed: {ex.Message}"); }
    }

    // ------------------------------------------------------------------ regeneration

    private PlanDrawing? _lastDrawing;

    public async Task RegenerateAsync(bool zoom)
    {
        int version = Interlocked.Increment(ref _regenVersion);
        IsBusy = true;
        StatusText = "Regenerating…";
        var project = _project;
        var origin = _origin;
        var sw = Stopwatch.StartNew();
        try
        {
            var (framings, steels, drawing, scene) = await Task.Run(() =>
            {
                // Incremental: unchanged bridges come from the cache; changed ones regenerate in parallel
                // (framing and steel generators also parallelize internally).
                var bridges = project.Bridges.ToArray();
                var models = _regenCache.Regenerate(project);
                var results = models.Select(m => m?.Framing).ToArray();
                var steel = models.Select(m => m?.Steel).ToArray();
                var concrete = models.Select(m => m?.Concrete).OfType<SBM.Core.Concrete.ConcreteModel>().ToList();
                var subs = models.Select(m => m?.Substructure).OfType<SBM.Core.Substructure.SubstructureModel>().ToList();
                var merged = MergeDrawings(project, bridges, results);
                foreach (var sub in subs) SubstructurePlanBuilder.Add(merged, sub);
                // A stable origin keeps reference models (built separately) aligned with the bridge.
                var sc = SceneBuilder.Build(merged, origin);
                SceneBuilder.AddSolids(sc, steel.OfType<SteelModel>().ToList());
                SceneBuilder.AddConcrete(sc, concrete);
                SceneBuilder.AddSubstructure(sc, subs);
                SceneBuilder.AddPrestressed(sc, models.Select(m => m?.Prestressed).OfType<SBM.Core.Prestressed.PrestressedModel>().ToList());
                return (results, models, merged, sc);
            });
            if (version != _regenVersion) return; // superseded

            Framings.Clear();
            SteelModels.Clear();
            ConcreteModels.Clear();
            SubstructureModels.Clear();
            PrestressedModels.Clear();
            // Replace the previous model diagnostics; keep the session log (imports, saves, ...).
            foreach (var old in Messages.Where(m => m.IsDiagnostic).ToList()) Messages.Remove(old);
            for (int i = 0; i < project.Bridges.Count; i++)
            {
                var f = framings[i];
                if (f == null)
                {
                    Add("Warning", $"{project.Bridges[i].Name}: alignment '{project.Bridges[i].AlignmentName}' not found.", true);
                    continue;
                }
                Framings[project.Bridges[i]] = f;
                foreach (var d in f.Diagnostics)
                    Add(d.Severity.ToString(), $"{project.Bridges[i].Name}: {d.Message}", true);
                if (steels[i]?.Steel is { } sm)
                {
                    SteelModels[project.Bridges[i]] = sm;
                    foreach (var d in sm.Diagnostics)
                        Add(d.Severity.ToString(), $"{project.Bridges[i].Name}: {d.Message}", true);
                }
                if (steels[i]?.Prestressed is { } psm)
                {
                    PrestressedModels[project.Bridges[i]] = psm;
                    foreach (var d in psm.Diagnostics)
                        Add(d.Severity.ToString(), $"{project.Bridges[i].Name}: {d.Message}", true);
                }
                if (steels[i]?.Concrete is { } cm)
                {
                    ConcreteModels[project.Bridges[i]] = cm;
                    foreach (var d in cm.Diagnostics)
                        Add(d.Severity.ToString(), $"{project.Bridges[i].Name}: {d.Message}", true);
                    foreach (var n in cm.Notes) Add("Info", $"{project.Bridges[i].Name}: {n}", true);
                }
                if (steels[i]?.Substructure is { } sub)
                {
                    SubstructureModels[project.Bridges[i]] = sub;
                    foreach (var d in sub.Diagnostics)
                        Add(d.Severity.ToString(), $"{project.Bridges[i].Name}: {d.Message}", true);
                    foreach (var n in sub.Notes) Add("Info", $"{project.Bridges[i].Name}: {n}", true);
                }
            }

            _lastDrawing = drawing;
            Scene = scene;
            _origin ??= scene.Origin;
            RebuildLayers(scene);
            RebuildExplorer();
            RebuildReports();
            SceneReady?.Invoke(scene, zoom);
            int frames = Framings.Values.Sum(f => f.CrossFrames.Count);
            int stiff = Framings.Values.Sum(f => f.Stiffeners.Count);
            int parts = SteelModels.Values.Sum(m => m.Parts.Count), bolts = SteelModels.Values.Sum(m => m.Bolts.Count);
            double tons = SteelModels.Values.Sum(m => m.SteelMass) / (Units.IsUS ? 907.18474 : 1000);
            int psBeams = PrestressedModels.Values.Sum(m => m.Beams.Count);
            StatusText = psBeams > 0 && SteelModels.Count == 0
                ? $"Regenerated in {sw.ElapsedMilliseconds} ms — {psBeams} prestressed girders, {PrestressedModels.Values.Sum(m => m.Beams.Sum(b => b.Strands.Count)):N0} strands, " +
                  $"{scene.Solids.Sum(s => s.Triangles):N0} triangles"
                : $"Regenerated in {sw.ElapsedMilliseconds} ms — {frames} cross frames, {stiff} stiffeners, {parts:N0} parts, {bolts:N0} bolts, " +
                  $"{tons:N1} {(Units.IsUS ? "tons" : "t")} steel, {scene.Solids.Sum(s => s.Triangles):N0} triangles";
        }
        catch (Exception ex)
        {
            Log(ex.ToString());
            Error($"Regeneration failed: {ex.Message}");
            StatusText = "Regeneration failed";
        }
        finally
        {
            if (version == _regenVersion) IsBusy = false;
        }
    }

    private static PlanDrawing MergeDrawings(BridgeProject project, BridgeDefinition[] bridges, BridgeFraming?[] framings)
    {
        // Alignments (all), then each bridge's framing, all into one layered drawing.
        var dwg = new PlanDrawing();
        foreach (var a in project.Alignments)
            Append(dwg, PlanDrawingBuilder.Build(a, null, project.Units));
        foreach (var f in framings)
            if (f != null && !f.HasErrors) Append(dwg, PlanDrawingBuilder.Build(null, f, project.Units));
        return dwg;
    }

    private static void Append(PlanDrawing target, PlanDrawing src)
    {
        foreach (var l in src.Layers)
        {
            var t = target.Layer(l.Style);
            t.Polylines.AddRange(l.Polylines);
            t.Texts.AddRange(l.Texts);
        }
    }

    private void RebuildLayers(SceneData scene)
    {
        var vis = LayerItems.ToDictionary(l => l.Name, l => l.IsVisible);
        LayerItems.Clear();
        foreach (var l in scene.Layers.OrderBy(l => l.Style.Name))
        {
            var item = new LayerItem
            {
                Name = l.Style.Name,
                Swatch = new SolidColorBrush(Color.FromRgb(l.Style.R, l.Style.G, l.Style.B)),
                Segments = l.SegmentCount,
                IsVisible = !vis.TryGetValue(l.Style.Name, out bool v) || v,
            };
            item.VisibilityChanged += i => LayerVisibilityChanged?.Invoke(i.Name, i.IsVisible);
            LayerItems.Add(item);
        }
        foreach (var name in _refLayers)
        {
            var def = _project.References.FirstOrDefault(r => name.StartsWith("REF-" + r.Name.ToUpperInvariant()));
            bool contours = name.EndsWith("-CONTOURS");
            var item = new LayerItem
            {
                Name = name,
                Swatch = new SolidColorBrush(contours ? Color.FromRgb(150, 120, 80) : Color.FromRgb(def?.R ?? 160, def?.G ?? 160, def?.B ?? 160)),
                IsVisible = !vis.TryGetValue(name, out bool v) || v,
            };
            item.VisibilityChanged += i => LayerVisibilityChanged?.Invoke(i.Name, i.IsVisible);
            LayerItems.Add(item);
        }
        foreach (var s in scene.Solids.OrderBy(s => s.Name))
        {
            var item = new LayerItem
            {
                Name = s.Name,
                Swatch = new SolidColorBrush(Color.FromRgb(s.R, s.G, s.B)),
                Segments = s.Triangles,
                IsVisible = !vis.TryGetValue(s.Name, out bool v) || v,
            };
            item.VisibilityChanged += i => LayerVisibilityChanged?.Invoke(i.Name, i.IsVisible);
            LayerItems.Add(item);
        }
    }

    public event Action<string, bool>? LayerVisibilityChanged;

    public bool IsLayerVisible(string name) => LayerItems.FirstOrDefault(l => l.Name == name)?.IsVisible ?? true;

    // ------------------------------------------------------------------ explorer & reports

    private void RebuildExplorer()
    {
        var u = Units;
        Explorer.Clear();
        var root = new ExplorerNode
        {
            Header = _project.Name, Icon = "",
            Properties = () => new[]
            {
                new PropertyRow("Units", u.IsUS ? $"US ({(u.Foot == FootDefinition.USSurvey ? "US survey ft" : "international ft")})" : "SI (m)"),
                new PropertyRow("Alignments", _project.Alignments.Count.ToString()),
                new PropertyRow("Bridges", _project.Bridges.Count.ToString()),
                new PropertyRow("File", FilePath ?? "(not saved)"),
            },
        };
        Explorer.Add(root);

        var alNode = new ExplorerNode { Header = "Alignments", Icon = "" };
        root.Children.Add(alNode);
        foreach (var a in _project.Alignments)
        {
            var al = a;
            alNode.Children.Add(new ExplorerNode
            {
                Header = a.Name, Icon = "", Tag = a, Open = () => EditAlignment(al),
                Properties = () => AlignmentProperties(al),
            });
        }

        if (_project.References.Count > 0)
        {
            var refNode = new ExplorerNode { Header = "References", Icon = "" };
            root.Children.Add(refNode);
            foreach (var r in _project.References)
            {
                var rr = r;
                refNode.Children.Add(new ExplorerNode
                {
                    Header = r.Name, Icon = r.Kind == ReferenceKind.Terrain ? "" : "", Tag = r,
                    Properties = () =>
                    {
                        string full = BridgeProject.ResolvePath(rr.Path, FilePath);
                        var rows = new List<PropertyRow>
                        {
                            new("Kind", rr.Kind.ToString()), new("File", full), new("Exists", File.Exists(full) ? "Yes" : "NO — file missing"),
                        };
                        if (_refCache.TryGetValue(rr.Name, out var c))
                        {
                            rows.Add(new("Triangles", c.geo.Mesh.TriangleCount.ToString("N0")));
                            rows.Add(new("Lines", c.geo.Polylines.Count.ToString("N0")));
                            rows.Add(new("File units", c.geo.SourceUnit));
                            var b = c.geo.Bounds();
                            if (!b.IsEmpty) rows.Add(new("Elevation range", $"{u.FormatLength(b.Min.Z, LengthKind.Coordinate)} to {u.FormatLength(b.Max.Z, LengthKind.Coordinate)}"));
                        }
                        return rows;
                    },
                });
            }
        }

        var brNode = new ExplorerNode { Header = "Bridges", Icon = "" };
        root.Children.Add(brNode);
        foreach (var b in _project.Bridges)
        {
            var bridge = b;
            Framings.TryGetValue(b, out var f);
            var bn = new ExplorerNode
            {
                Header = b.Name, Icon = "", Tag = b, Open = () => EditBridge("0"),
                Properties = () => BridgeProperties(bridge, f),
            };
            brNode.Children.Add(bn);
            var sup = new ExplorerNode { Header = $"Supports ({b.Supports.Count})", Icon = "", Tag = b, Open = () => EditBridge("0"), IsExpanded = false };
            bn.Children.Add(sup);
            if (f != null)
            {
                foreach (var s in f.Supports)
                {
                    var ss = s;
                    sup.Children.Add(new ExplorerNode
                    {
                        Header = s.Name, Icon = "", Tag = b, Open = () => EditBridge("0"),
                        Properties = () => SupportProperties(ss),
                    });
                }
                var gn = new ExplorerNode { Header = $"Girders ({f.Girders.Count})", Icon = "", Tag = b, Open = () => EditBridge("1"), IsExpanded = false };
                bn.Children.Add(gn);
                foreach (var g in f.Girders)
                {
                    var gg = g;
                    gn.Children.Add(new ExplorerNode
                    {
                        Header = $"{g.Name}  ({u.FormatLength(g.Length)})", Icon = "", Tag = b, Open = () => EditBridge("1"),
                        Properties = () => GirderProperties(bridge, f, gg),
                    });
                }
                bn.Children.Add(new ExplorerNode { Header = "Deck & Barriers", Icon = "", Tag = b, Open = () => EditBridge("2"),
                    Properties = () => DeckProperties(bridge, f) });
                bn.Children.Add(new ExplorerNode { Header = $"Cross Frames ({f.CrossFrames.Count})", Icon = "", Tag = b, Open = () => EditBridge("3"),
                    Properties = () => f.CrossFrames.GroupBy(c => c.Type).Select(gr => new PropertyRow(ChoiceField.Humanize(gr.Key.ToString()), gr.Count().ToString())) });
                bn.Children.Add(new ExplorerNode { Header = $"Stiffeners ({f.Stiffeners.Count})", Icon = "", Tag = b, Open = () => EditBridge("4"),
                    Properties = () => f.Stiffeners.GroupBy(c => c.Kind).Select(gr => new PropertyRow(ChoiceField.Humanize(gr.Key.ToString()), gr.Count().ToString())) });
                bn.Children.Add(new ExplorerNode { Header = $"Field Splices ({b.FieldSplices.Count})", Icon = "", Tag = b, Open = () => EditBridge("5"),
                    Properties = () => b.FieldSplices.Select((s, i) => new PropertyRow($"FS{i + 1}", u.FormatStation(s.Station))) });
            }
        }
    }

    private IEnumerable<PropertyRow> AlignmentProperties(Alignment a)
    {
        var u = Units;
        yield return new("Name", a.Name);
        yield return new("Start station", u.FormatStation(a.StartStation));
        yield return new("End station", u.FormatStation(a.EndStation));
        yield return new("Length", u.FormatLength(a.Length));
        yield return new("Elements", a.Elements.Count.ToString());
        yield return new("Lines / Arcs / Spirals",
            $"{a.Elements.Count(e => e.Kind == HorizontalElementKind.Line)} / {a.Elements.Count(e => e.Kind == HorizontalElementKind.Arc)} / {a.Elements.Count(e => e.Kind == HorizontalElementKind.Spiral)}");
        yield return new("Start bearing", UnitSystem.FormatBearing(a.Elements.FirstOrDefault()?.StartAngle ?? 0));
        yield return new("VPIs", a.Profile.Vpis.Count.ToString());
        yield return new("Cross-slope rows", a.CrossSlope.Rows.Count.ToString());
        yield return new("PI-editable", a.PiDefinition != null ? "Yes" : "No (imported geometry)");
        foreach (var e in a.Elements.Where(e => e.Kind != HorizontalElementKind.Line))
            yield return new($"{e.Kind} @ {u.FormatStation(a.DistanceToStation(e.StartDistance))}",
                $"R = {(double.IsInfinity(e.Radius) ? "∞" : u.FormatLength(e.Radius))}, L = {u.FormatLength(e.Length)}");
    }

    private IEnumerable<PropertyRow> BridgeProperties(BridgeDefinition b, BridgeFraming? f)
    {
        var u = Units;
        yield return new("Name", b.Name);
        yield return new("Alignment", b.AlignmentName);
        yield return new("Spans", string.Join(" – ", b.Supports.Zip(b.Supports.Skip(1), (x, y) => u.FormatLength(y.Station - x.Station))));
        yield return new("Begin / end", $"{u.FormatStation(b.Supports.First().Station)} / {u.FormatStation(b.Supports.Last().Station)}");
        yield return new("Girders", $"{b.Girders.Count} ({ChoiceField.Humanize(b.Girders.PlanLayout.ToString())})");
        yield return new("Steel depth", u.FormatLength(b.Girders.Section.TotalDepth, LengthKind.Short));
        yield return new("Deck width", u.FormatLength(b.Deck.RightEdgeOffset - b.Deck.LeftEdgeOffset));
        if (f != null)
        {
            if (f.Deck != null) yield return new("Deck area", $"{f.Deck.Area / (u.LongUnit * u.LongUnit):N0} {u.LongUnitName}²");
            yield return new("Cross frames", f.CrossFrames.Count.ToString());
            yield return new("Stiffeners", f.Stiffeners.Count.ToString());
            yield return new("Generation time", $"{f.GenerationTime.TotalMilliseconds:F0} ms");
        }
    }

    private IEnumerable<PropertyRow> SupportProperties(SupportLineResult s)
    {
        var u = Units;
        yield return new("Type", s.Type.ToString());
        yield return new("Station", u.FormatStation(s.Station));
        yield return new("Skew", UnitSystem.FormatAngleDms(Numerics.DegToRad(s.SkewDegrees)));
        yield return new("Support line bearing", UnitSystem.FormatBearing(s.Direction.Angle));
        foreach (var b in s.Bearings.OfType<BearingResult>())
            yield return new($"G{b.Girder + 1} bearing", $"STA {u.FormatStation(b.Station)}, TOS {u.FormatLength(b.TopOfSteel.Z, LengthKind.Coordinate)}");
    }

    private IEnumerable<PropertyRow> GirderProperties(BridgeDefinition b, BridgeFraming f, GirderResult g)
    {
        var u = Units;
        yield return new("Length (end to end)", u.FormatLength(g.Length));
        for (int j = 0; j < g.Spans.Count; j++)
            yield return new($"Span {j + 1} (CL brg to CL brg)", u.FormatLength(g.Spans[j].Length));
        if (b.Girders.IsPrestressed)
        {
            var ps = b.Girders.Prestressed;
            var shp = SBM.Core.Prestressed.TxGirderCatalog.Get(ps);
            yield return new("Girder", $"TxDOT {shp.Name} prestressed (simple spans)");
            yield return new("Depth / top flange", $"{u.FormatLength(shp.Depth, LengthKind.Short)} / {u.FormatLength(shp.TopWidth, LengthKind.Short)}");
            yield return new("Strands", $"{ps.StrandCount} – {SBM.Core.Prestressed.TxGirderCatalog.StrandSize(ps.StrandDiameter, u)} ({ps.DepressedStrands} depressed)");
            if (PrestressedModels.TryGetValue(b, out var pm))
                foreach (var bm in pm.Beams.Where(x => x.Girder == g.Index))
                    yield return new($"Beam {bm.Mark}", $"{u.FormatLength(bm.Length)} long, haunch {u.FormatLength(bm.MinHaunch, LengthKind.Short)} to {u.FormatLength(bm.MaxHaunch, LengthKind.Short)}");
            yield break;
        }
        var sec = b.Girders.Section;
        yield return new("Top flange", $"{u.FormatLength(sec.TopFlange.Width, LengthKind.Short)} × {u.FormatLength(sec.TopFlange.Thickness, LengthKind.Short)}");
        yield return new("Web", $"{u.FormatLength(sec.WebDepth, LengthKind.Short)} × {u.FormatLength(sec.WebThickness, LengthKind.Short)}");
        yield return new("Bottom flange", $"{u.FormatLength(sec.BottomFlange.Width, LengthKind.Short)} × {u.FormatLength(sec.BottomFlange.Thickness, LengthKind.Short)}");
        yield return new("Stiffeners", f.Stiffeners.Count(s => s.Girder == g.Index).ToString());
        foreach (var sp in f.Splices.Where(s => s.Girder == g.Index))
            yield return new($"Field splice {sp.Number}", $"{u.FormatLength(sp.GirderDistance)} from girder end");
    }

    private IEnumerable<PropertyRow> DeckProperties(BridgeDefinition b, BridgeFraming f)
    {
        var u = Units;
        var d = b.Deck;
        yield return new("Left edge offset", u.FormatLength(d.LeftEdgeOffset));
        yield return new("Right edge offset", u.FormatLength(d.RightEdgeOffset));
        yield return new("Thickness", u.FormatLength(d.Thickness, LengthKind.Short));
        yield return new("Haunch", u.FormatLength(d.Haunch, LengthKind.Short));
        yield return new("SIP forms", d.StayInPlaceForms ? "Yes" : "No");
        yield return new("Barrier", $"{d.Barrier.Type}, {u.FormatLength(d.Barrier.Height, LengthKind.Short)} high");
        if (f.Deck != null) yield return new("Deck area", $"{f.Deck.Area / (u.LongUnit * u.LongUnit):N0} {u.LongUnitName}²");
    }

    private void RebuildReports()
    {
        var u = Units;
        BearingRows.Clear();
        CrossFrameRows.Clear();
        MaterialRows.Clear();
        foreach (var (b, f) in Framings)
        {
            SteelModels.TryGetValue(b, out var sm);
            PrestressedModels.TryGetValue(b, out var psm);
            foreach (var s in f.Supports)
                foreach (var br in s.Bearings.OfType<BearingResult>())
                {
                    var seat = sm?.BearingSeats.FirstOrDefault(x => x.Girder == br.Girder && Math.Abs(x.Station - br.Station) < 1e-6)
                               ?? psm?.BearingSeats.Where(x => x.Girder == br.Girder && x.Support == s.Name).MinBy(x => x.SeatElevation);
                    BearingRows.Add(new BearingRow(s.Name, $"G{br.Girder + 1}", u.FormatStation(br.Station), u.FormatLength(br.Offset),
                        ValueFormat.Format(u, ValueKind.Coordinate, br.TopOfSteel.Y), ValueFormat.Format(u, ValueKind.Coordinate, br.TopOfSteel.X),
                        u.FormatLength(br.TopOfSteel.Z, LengthKind.Coordinate), u.FormatLength(br.BottomOfSteelElevation, LengthKind.Coordinate),
                        u.FormatLength(br.DeckElevation, LengthKind.Coordinate),
                        seat != null ? u.FormatLength(seat.SeatElevation, LengthKind.Coordinate) : "",
                        seat != null ? $"{seat.Type} ({seat.Fixity}), sole PL {u.FormatLength(seat.SolePlateMaxThickness, LengthKind.Short)} max" : "",
                        GroundElevation(br.TopOfSteel.XY) is { } gz ? u.FormatLength(gz, LengthKind.Coordinate) : ""));
                }
            if (sm != null)
            {
                double massUnit = u.IsUS ? 0.45359237 : 1; // lb or kg
                string mu = u.IsUS ? "lb" : "kg";
                foreach (var grp in sm.Parts.Where(p => p.Category != PartCategory.Weld)
                             .GroupBy(p => (p.Category, p.Description)).OrderBy(g => g.Key.Category).ThenBy(g => g.Key.Description))
                    MaterialRows.Add(new MaterialRow(b.Name, ChoiceField.Humanize(grp.Key.Category.ToString()), grp.Key.Description, grp.Count(),
                        u.FormatLength(grp.Sum(p => p.Length)),
                        grp.Key.Category == PartCategory.BearingPad ? "—" : $"{grp.Sum(p => p.Mass) / massUnit:N0} {mu}"));
                foreach (var grp in sm.Parts.Where(p => p.Category == PartCategory.Weld).GroupBy(p => p.Description).OrderBy(g => g.Key))
                    MaterialRows.Add(new MaterialRow(b.Name, "Weld", grp.Key, grp.Count(), u.FormatLength(grp.Sum(p => p.Length)), ""));
                foreach (var grp in sm.Bolts.Where(x => x.Kind == BoltKind.Structural).GroupBy(x => Math.Round(x.Length, 5)).OrderBy(g => g.Key))
                    MaterialRows.Add(new MaterialRow(b.Name, "Bolt",
                        $"{u.FormatLength(sm.Bolt.Diameter, LengthKind.Short)} {sm.BoltGrade} x {u.FormatLength(grp.Key, LengthKind.Short)} w/ nut & washer",
                        grp.Count(), "", ""));
                foreach (var grp in sm.Bolts.Where(x => x.Kind == BoltKind.Anchor).GroupBy(x => (Math.Round(x.Diameter, 5), Math.Round(x.Embedment, 5))))
                    MaterialRows.Add(new MaterialRow(b.Name, "Anchor bolt",
                        $"{u.FormatLength(grp.Key.Item1, LengthKind.Short)} {b.Bearings.AnchorBoltGrade}, embed {u.FormatLength(grp.Key.Item2, LengthKind.Short)}, w/ plate washer & nuts",
                        grp.Count(), "", ""));
                foreach (var grp in sm.Studs.GroupBy(x => Math.Round(x.Length, 5)).OrderBy(g => g.Key))
                    MaterialRows.Add(new MaterialRow(b.Name, "Shear stud",
                        $"{u.FormatLength(sm.StudDiameter, LengthKind.Short)} dia x {u.FormatLength(grp.Key, LengthKind.Short)} headed stud",
                        grp.Count(), "", ""));
                MaterialRows.Add(new MaterialRow(b.Name, "TOTAL STRUCTURAL STEEL", "", sm.Parts.Count(p => p.Category is not (PartCategory.Weld or PartCategory.BearingPad)),
                    "", $"{sm.SteelMass / massUnit:N0} {mu}"));
            }
            if (psm != null)
            {
                double massUnit = u.IsUS ? 0.45359237 : 1;
                string mu = u.IsUS ? "lb" : "kg";
                string su = SBM.Core.Prestressed.TxGirderCatalog.StrandSize(psm.Definition.StrandDiameter, u);
                foreach (var grp in psm.Beams.GroupBy(x => x.Shape.Name))
                    MaterialRows.Add(new MaterialRow(b.Name, "Prestressed girder", $"TxDOT {grp.Key}, f'c {psm.Definition.ConcreteStrength:0.0#} ksi", grp.Count(),
                        u.FormatLength(grp.Sum(x => x.Length)), $"{grp.Sum(x => x.Mass) / massUnit:N0} {mu}"));
                MaterialRows.Add(new MaterialRow(b.Name, "Prestressing strand", $"{su} dia, {psm.Definition.StrandStrength:0} ksi low-relaxation",
                    psm.Beams.Sum(x => x.Strands.Count), u.FormatLength(psm.StrandLength), ""));
                foreach (var grp in psm.Pads.GroupBy(x => (x.Length, x.Width, x.Thickness)))
                    MaterialRows.Add(new MaterialRow(b.Name, "Bearing pad",
                        $"Elastomeric {u.FormatLength(grp.Key.Length, LengthKind.Short)} x {u.FormatLength(grp.Key.Width, LengthKind.Short)} x {u.FormatLength(grp.Key.Thickness, LengthKind.Short)}",
                        grp.Count(), "", "—"));
            }
            if (ConcreteModels.TryGetValue(b, out var cmod))
            {
                double massUnit = u.IsUS ? 0.45359237 : 1;
                string mu = u.IsUS ? "lb" : "kg";
                double cy = u.IsUS ? 0.764554857984 : 1;
                string vu = u.IsUS ? "CY" : "m³";
                MaterialRows.Add(new MaterialRow(b.Name, "Concrete", "Deck slab", 1, "", $"{cmod.DeckVolume / cy:N1} {vu}"));
                MaterialRows.Add(new MaterialRow(b.Name, "Concrete", "Haunches", _project.Bridges.Count, "", $"{cmod.HaunchVolume / cy:N1} {vu}"));
                if (cmod.Barriers.Count > 0)
                    MaterialRows.Add(new MaterialRow(b.Name, "Concrete", $"Barrier ({b.Deck.Barrier.Type})", cmod.Barriers.Count, "", $"{cmod.BarrierVolume / cy:N1} {vu}"));
                foreach (var jr in cmod.Joints)
                {
                    MaterialRows.Add(new MaterialRow(b.Name, "Concrete", $"Joint blockout, {jr.Support}", 1, "", $"{jr.BlockoutVolume / cy:N2} {vu}"));
                    if (jr.EndBlock != null)
                        MaterialRows.Add(new MaterialRow(b.Name, "Concrete", $"Deck end block (end dam diaphragm), {jr.Support}", 1, "", $"{jr.EndBlock.Volume / cy:N2} {vu}"));
                    if (jr.Body != null)
                        MaterialRows.Add(new MaterialRow(b.Name, "Expansion joint", $"{jr.Definition.TypeName} at {jr.Support} (generic, manufacturer-designed)", 1,
                            u.FormatLength(jr.Length), "by manufacturer"));
                }
                foreach (var g in cmod.Marks.GroupBy(m => m.Size.Name))
                    MaterialRows.Add(new MaterialRow(b.Name, "Reinforcing", $"{g.Key} ({string.Join(", ", g.Select(m => m.Mark))})", g.Sum(m => m.Count),
                        u.FormatLength(g.Sum(m => m.TotalLength)), $"{g.Sum(m => m.TotalMass) / massUnit:N0} {mu}"));
                if (cmod.Marks.Count > 0)
                    MaterialRows.Add(new MaterialRow(b.Name, "TOTAL REINFORCING", cmod.Marks.Any(m => m.Epoxy) ? "Epoxy coated" : "", cmod.Marks.Sum(m => m.Count),
                        "", $"{cmod.RebarMass / massUnit:N0} {mu}"));
            }
            if (SubstructureModels.TryGetValue(b, out var smod))
            {
                double massUnit = u.IsUS ? 0.45359237 : 1;
                string mu = u.IsUS ? "lb" : "kg";
                double cy = u.IsUS ? 0.764554857984 : 1;
                string vu = u.IsUS ? "CY" : "m³";
                foreach (var g in smod.Units.SelectMany(x => x.Solids).Where(x => x.IsConcrete).GroupBy(x => x.Kind))
                    MaterialRows.Add(new MaterialRow(b.Name, "Concrete", "Substructure " + ChoiceField.Humanize(g.Key.ToString()).ToLowerInvariant() + "s", g.Count(), "",
                        $"{g.Sum(x => Math.Abs(x.Mesh.Volume())) / cy:N1} {vu}"));
                foreach (var g in smod.Units.SelectMany(x => x.Foundations).SelectMany(x => x.Piles).GroupBy(_ => 1))
                    MaterialRows.Add(new MaterialRow(b.Name, "Piling", "Piles", g.Count(), u.FormatLength(g.Sum(p => p.Length)), ""));
                foreach (var g in smod.Marks.GroupBy(m => m.Size.Name))
                    MaterialRows.Add(new MaterialRow(b.Name, "Reinforcing (substructure)", g.Key, g.Sum(m => m.Count),
                        u.FormatLength(g.Sum(m => m.TotalLength)), $"{g.Sum(m => m.TotalMass) / massUnit:N0} {mu}"));
                if (smod.Marks.Count > 0)
                    MaterialRows.Add(new MaterialRow(b.Name, "TOTAL SUBSTRUCTURE REINFORCING", "", smod.Marks.Sum(m => m.Count), "", $"{smod.RebarMass / massUnit:N0} {mu}"));
            }
            foreach (var c in f.CrossFrames)
                CrossFrameRows.Add(new CrossFrameRow(c.Mark, (c.Span + 1).ToString(), $"G{c.Bay + 1}–G{c.RightGirder + 1}",
                    (c.AtSupport ? "Support " : "") + ChoiceField.Humanize(c.Type.ToString()), u.FormatStation(c.Station), u.FormatLength(c.PlanLength),
                    u.FormatLength(c.LeftGirderDistance), u.FormatLength(c.RightGirderDistance)));
        }
    }

    // ------------------------------------------------------------------ cursor readout

    public void UpdateCursor(Vec2 world)
    {
        var u = Units;
        string text = $"E {ValueFormat.Format(u, ValueKind.Coordinate, world.X)}   N {ValueFormat.Format(u, ValueKind.Coordinate, world.Y)}";
        var align = (SelectedNode?.Tag as Alignment) ?? (SelectedNode?.Tag is BridgeDefinition bd ? _project.FindAlignment(bd.AlignmentName) : null)
                    ?? _project.Alignments.FirstOrDefault();
        if (align != null && align.Elements.Count > 0)
        {
            var so = align.Inverse(world);
            double elev = align.ElevationAt(so.Station, so.Offset);
            text += $"   |   {align.Name}: STA {u.FormatStation(so.Station)}  OFF {u.FormatLength(so.Offset)} {(so.Offset < 0 ? "LT" : "RT")}" +
                    $"   Grade elev {u.FormatLength(elev, LengthKind.Coordinate)}";
            if (GroundElevation(world) is { } g)
                text += $"   Ground {u.FormatLength(g, LengthKind.Coordinate)}  (grade − ground {u.FormatLength(elev - g)})";
        }
        else if (GroundElevation(world) is { } g2) text += $"   Ground {u.FormatLength(g2, LengthKind.Coordinate)}";
        CursorText = text;
    }

    // ------------------------------------------------------------------ messages

    private static readonly Brush InfoBrush = Freeze(new SolidColorBrush(Color.FromRgb(140, 196, 255)));
    private static readonly Brush WarnBrush = Freeze(new SolidColorBrush(Color.FromRgb(255, 200, 80)));
    private static readonly Brush ErrBrush = Freeze(new SolidColorBrush(Color.FromRgb(255, 107, 107)));
    private static Brush Freeze(Brush b) { b.Freeze(); return b; }

    public void Info(string m) => Add("Info", m, false);
    public void Warn(string m) => Add("Warning", m, false);
    public void Error(string m) => Add("Error", m, false);

    private void Add(string severity, string text, bool diagnostic)
    {
        var (icon, brush) = severity switch
        {
            "Error" => ("", ErrBrush),
            "Warning" => ("", WarnBrush),
            _ => ("", InfoBrush),
        };
        Messages.Add(new MessageItem(severity, text, icon, brush, diagnostic));
        Log($"{severity}: {text}");
    }

    public static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SteelBridgeModeler", "session.log");

    /// <summary>Appends to the session log (%LOCALAPPDATA%\SteelBridgeModeler\session.log) for troubleshooting.</summary>
    public static void Log(string line)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}  {line}{Environment.NewLine}");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
