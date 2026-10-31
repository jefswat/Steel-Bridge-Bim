using System.IO;
using System.Windows;
using HelixToolkit;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Microsoft.Win32;
using SBM.App.Viewport;
using SBM.Core.Geometry;
using SBM.IO.Database;
using SBM.IO.Ifc;

using MeshGeometry3D = HelixToolkit.SharpDX.MeshGeometry3D;

namespace SBM.App;

/// <summary>
/// Companion database in the app: the Properties side shows the database record of the selected element (GlobalId, hierarchy,
/// quantities, linked plan sheets with PDF / preview, QC status). The database file is <c>&lt;model&gt;.sqlite</c> beside the model;
/// it is built on demand (Create / Rebuild), shows as out of date after the model regenerates, and keeps the QC records on rebuild
/// (GlobalIds are the IFC ones, so they are stable). Elements are matched by object reference through a <see cref="TagIndex"/>
/// rebuilt (cheaply, no database writes) whenever the scene is rebuilt.
/// </summary>
public partial class MainWindow
{
    private ModelDb? _db;
    private TagIndex? _dbTags;
    private Task<TagIndex>? _dbTagTask;
    private int _dbScenes;
    private bool _dbBusy;
    private bool _dbStale;
    private string? _dbShown;

    private string? DbPath => _vm.FilePath is { Length: > 0 } f ? CompanionDatabase.DefaultPath(f) : null;

    private void InitDatabase()
    {
        _vm.SceneReady += (_, _) => OnDbSceneReady();
        DbPanel.CreateRequested += () => _ = BuildDatabaseAsync(false);
        DbPanel.ImportPagesRequested += () => _ = BuildDatabaseAsync(true);
        DbPanel.NavigateRequested += gid => { ShowDbElement(gid); HighlightDbGroup(gid); };
        DbPanel.OpenSheetRequested += (gid, s) => { if (_db != null) Database.PlanSheetWindow.ShowElement(this, _db, gid, DbLabel(gid), s); };
        // G9 asset management: calc values vs the live model, the picked 3D point for findings, the Calcs window
        DbPanel.CalcDiff = (v, title) =>
            v.ResultPath.Length > 0 && !_vm.Project.Bridges.All(_vm.Framings.ContainsKey)
                ? new SBM.Api.CalcDiffRow { Value = v, CalcTitle = title, Status = "error", CalcText = v.ValueText, Message = "results not generated (regenerate the model)" } // never regenerate here
                : SBM.Api.CalcSync.DiffOne(CalcDoc(), _db, v, title);
        DbPanel.PickedPoint = () => _dbPickWorld is { } w ? (w.X, w.Y, w.Z) : null;
        DbPanel.OpenCalcsRequested += id => ShowCalcsWindow(id);
        RefreshDbStatus();
    }

    private Vec3? _dbPickWorld;

    /// <summary>The live model with its current results (no regeneration) for calc comparisons.</summary>
    private SBM.Api.SbmDocument CalcDoc()
    {
        var models = _vm.Project.Bridges.Select(b => _vm.Framings.TryGetValue(b, out var f)
            ? new SBM.Core.Project.BridgeModel(b, f, _vm.SteelModels.GetValueOrDefault(b), true, _vm.ConcreteModels.GetValueOrDefault(b),
                _vm.SubstructureModels.GetValueOrDefault(b), _vm.PrestressedModels.GetValueOrDefault(b))
            : null).ToList();
        return SBM.Api.SbmDocument.Attach(_vm.Project, _vm.FilePath, models);
    }

    private void OnShowCalcs(object sender, RoutedEventArgs e) => ShowCalcsWindow(0);

    private void ShowCalcsWindow(long calcId)
    {
        if (_db == null)
        {
            MessageBox.Show(this, DbPath == null ? "Save the model first: calcs live in its companion database." : "Create the database first (Properties panel, Database).", "Calcs",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Database.CalcsWindow.ShowCalcs(this, new Database.CalcsWindow.Host { Db = () => _db, Doc = CalcDoc, ApplyAsync = ApplyCalcValuesAsync }, calcId);
    }

    /// <summary>Applies confirmed calc values to the live model: undo checkpoint, PropertyPath set + log per value, regenerate, save.</summary>
    private async Task<SBM.Api.CalcApplyResult> ApplyCalcValuesAsync(IReadOnlyList<long> ids)
    {
        if (_db == null) throw new InvalidOperationException("No database.");
        _vm.Checkpoint();
        var res = SBM.Api.CalcSync.Apply(SBM.Api.SbmDocument.Attach(_vm.Project, _vm.FilePath), _db, ids, null, save: false);
        foreach (var (row, o, n) in res.Applied) ViewModels.MainViewModel.Log($"CALC APPLY: {row.Value.PropertyPath}: {o} -> {n} (calc #{row.Value.CalcId} p.{row.Value.Page})");
        if (res.Applied.Count > 0)
        {
            await _vm.RegenerateAsync(false);
            if (_vm.FilePath != null) { _vm.SaveToPath(null); res.SavedTo = _vm.FilePath; }
        }
        if (_dbShown != null && _db != null) ShowDbElement(_dbShown);
        return res;
    }

    private List<IfcBridgeData> DbSnapshotData() =>
        _vm.Framings.Keys.Select(b => new IfcBridgeData(_vm.Framings[b], _vm.SteelModels.GetValueOrDefault(b), _vm.ConcreteModels.GetValueOrDefault(b),
            _vm.SubstructureModels.GetValueOrDefault(b), _vm.PrestressedModels.GetValueOrDefault(b))).ToList();

    private void OnDbSceneReady()
    {
        _dbScenes++;
        _dbTags = null;
        _dbTagTask = null;
        if (_dbBusy) return;
        OpenDb();
        // Index the model objects → GlobalIds in the background (hashing only, no database writes). Always: the Properties panel shows
        // the picked element's GlobalId (= its IFC GlobalId) with or without a database.
        {
            var data = DbSnapshotData();
            var project = _vm.Project;
            var roads = _vm.RoadModels;
            var walls = _vm.WallModels;
            var gradings = _vm.GradingModels;
            var utilities = _vm.UtilityModels;
            _dbTagTask = Task.Run(() => ModelDatabase.BuildTagIndex(project, data, roads, walls, gradings, utilities));
            if (_db != null && _dbScenes > 1) _dbStale = true;
        }
        RefreshDbStatus();
        _ = RelinkIfNeededAsync();
    }

    private string? _dbRelinked;

    /// <summary>
    /// Plan sheet links by default: when the database has no links, or the model's <c>sheet-links*.json</c> changed (or is a different file)
    /// since the links were made, re-applies the sheet index + link rules to the existing database in the background (no element rebuild;
    /// QC is kept). Once per database and rules file version per session.
    /// </summary>
    private async Task RelinkIfNeededAsync()
    {
        if (_db == null || _dbBusy || DbPath is not { } path || CompanionDatabase.FindLinksFile(_vm.FilePath, _vm.Project.Bridges.Select(b => b.Name)) is not { } links) return;
        string key = path + "|" + links + "|" + File.GetLastWriteTimeUtc(links).Ticks;
        if (_dbRelinked == key) return;
        bool applied = DateTime.TryParse(_db.Meta("links_applied_at"), null, System.Globalization.DateTimeStyles.RoundtripKind, out var at);
        bool current = _db.LinkCount > 0 && applied && File.GetLastWriteTimeUtc(links) <= at.ToUniversalTime()
                       && string.Equals(_db.Meta("links_file"), Path.GetFullPath(links), StringComparison.OrdinalIgnoreCase);
        _dbRelinked = key;
        if (current) return;
        _dbBusy = true;
        _db.Dispose();
        _db = null;
        RefreshDbStatus();
        string name = _vm.Project.Name;
        string? model = _vm.FilePath;
        var opts = Opts(model, links);
        try
        {
            var res = await Task.Run(() => CompanionDatabase.Relink(path, name, opts));
            ViewModels.MainViewModel.Log($"DB: plan sheet links from {Path.GetFileName(links)}: {res.Sheets} sheets, {res.Links?.Links ?? 0} links in {res.Elapsed.TotalSeconds:F1} s");
            if (res.Coverage.Length > 0) ViewModels.MainViewModel.Log("DB: " + res.Coverage);
        }
        catch (Exception ex) { ViewModels.MainViewModel.Log("DB relink failed: " + ex.Message); }
        _dbBusy = false;
        OpenDb();
        RefreshDbStatus();
        if (_dbShown != null) ShowDbElement(_dbShown);
    }

    private DatabaseOptions Opts(string? model, string? links)
    {
        var o = new DatabaseOptions { ModelFile = model, LinksFile = links, Log = m => ViewModels.MainViewModel.Log("DB: " + m) };
        o.BridgeNames.AddRange(_vm.Project.Bridges.Select(b => b.Name));
        return o;
    }

    private void OpenDb()
    {
        _db?.Dispose();
        _db = null;
        if (DbPath is { } p && File.Exists(p))
        {
            try
            {
                _db = new ModelDb(p);
                if (DateTime.TryParse(_db.Meta("built_at"), null, System.Globalization.DateTimeStyles.RoundtripKind, out var built)
                    && _vm.FilePath is { } mf && File.Exists(mf) && File.GetLastWriteTimeUtc(mf) > built.ToUniversalTime() && _dbScenes <= 1)
                    _dbStale = true;
            }
            catch (Exception ex) { ViewModels.MainViewModel.Log("DB open failed: " + ex.Message); }
        }
    }

    private void RefreshDbStatus()
    {
        if (DbPath == null) { DbPanel.SetStatus("Save the model first: the database file is <model>.sqlite beside it.", false, true, false); return; }
        if (_dbBusy) { DbPanel.SetStatus("Building the database…", false, false, false); return; }
        if (_db == null) { DbPanel.SetStatus($"No database yet ({Path.GetFileName(DbPath)}). Create it to see the sheets each element is based on.", true, true, false); return; }
        string built = _db.Meta("built_at");
        double mb = new FileInfo(_db.Path).Length / 1048576.0;
        DbPanel.SetStatus($"{Path.GetFileName(_db.Path)}: {_db.ElementCount:N0} elements, {_db.SheetCount} plan sheets, {_db.LinkCount:N0} sheet links"
            + (_db.Meta("links_file") is { Length: > 0 } lf ? $" ({Path.GetFileName(lf)})" : _db.SheetCount == 0 ? " (no sheet-links*.json for this model)" : "") + $", built {built}, {mb:F0} MB." + (mb > ModelDatabase.SizeNoteMb ? $" (Note: larger than {ModelDatabase.SizeNoteMb:F0} MB; informational only.)" : "")
            + (_dbStale ? " The model changed since: rebuild to update (QC is kept)." : ""), true, false, true);
    }

    private async Task BuildDatabaseAsync(bool pages)
    {
        if (_dbBusy || DbPath is not { } path) return;
        string? folder = null;
        string? links = CompanionDatabase.FindLinksFile(_vm.FilePath, _vm.Project.Bridges.Select(b => b.Name));
        if (pages)
        {
            if (links == null) { MessageBox.Show(this, "No sheet-links*.json (plan sets and link rules) naming this model beside it.", "Plan pages", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            var dlg = new OpenFolderDialog { Title = "Folder with the plan set PDFs" };
            if (dlg.ShowDialog(this) != true) return;
            folder = dlg.FolderName;
        }
        _dbBusy = true;
        _db?.Dispose();
        _db = null;
        RefreshDbStatus();
        var data = DbSnapshotData();
        var project = _vm.Project;
        var roads = _vm.RoadModels;
        var walls = _vm.WallModels;
        var gradings = _vm.GradingModels;
        var utilities = _vm.UtilityModels;
        string? model = _vm.FilePath;
        try
        {
            var res = await Task.Run(() =>
            {
                var o = new DatabaseOptions { ModelFile = model, LinksFile = links, Pages = pages, Log = m => ViewModels.MainViewModel.Log("DB: " + m),
                    Roads = roads.Count > 0 ? roads : null, Walls = walls.Count > 0 ? walls : null, Gradings = gradings.Count > 0 ? gradings : null,
                    Utilities = utilities.Count > 0 ? utilities : null };
                if (folder != null) o.PlanFolders.Add(folder);
                return CompanionDatabase.Build(path, project, data, o);
            });
            _dbTags = res.Build.Tags;
            _dbTagTask = Task.FromResult(res.Build.Tags);
            _dbStale = false;
            ViewModels.MainViewModel.Log($"DB: built {res.Build.Elements:N0} elements in {res.Elapsed.TotalSeconds:F1} s, {res.Sheets} sheets, {res.Links?.Links ?? 0} links"
                + (res.LinksFile != null ? $" ({Path.GetFileName(res.LinksFile)})" : " (no sheet-links*.json for this model)"));
            if (res.Coverage.Length > 0) ViewModels.MainViewModel.Log("DB: " + res.Coverage);
            if (links != null) _dbRelinked = _vm.FilePath is { } fp ? CompanionDatabase.DefaultPath(fp) + "|" + links + "|" + File.GetLastWriteTimeUtc(links).Ticks : null;
            StatusText($"Database built: {res.Build.Elements:N0} elements, {res.Sheets} sheets in {res.Elapsed.TotalSeconds:F1} s");
            ViewModels.MainViewModel.Log("DB: " + ModelDatabase.SizeText(path));
        }
        catch (Exception ex)
        {
            ViewModels.MainViewModel.Log("DB build failed: " + ex);
            MessageBox.Show(this, ex.Message, "Database", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        _dbBusy = false;
        OpenDb();
        RefreshDbStatus();
        if (_dbShown != null) ShowDbElement(_dbShown);
    }

    // ------------------------------------------------------------------ selection hooks (called from Pick)

    private static object? RawModelObject(object tag) => tag switch
    {
        RebarPick rp => rp.Bar,
        SubPick sp => sp.Solid,
        PsBeamPick pb => pb.Beam,
        PsStrandPick ps => ps.Strand,
        PsPadPick pp => pp.Pad,
        NamedPick np => np.Model ?? np.Source,
        _ => tag,
    };

    private void DbOnPicked(object tag, System.Numerics.Vector3 hitLocal, string? parentLevel = null)
    {
        _dbPickWorld = _scene != null ? new Vec3(hitLocal.X, hitLocal.Y, hitLocal.Z) + _scene.Origin : null;
        ShowGlobalIdRow(tag);
        if (_db == null) { _dbShown = null; DbPanel.ShowMessage(DbPath == null ? "Save the model to use the database." : "No database for this model yet: Create database."); return; }
        var task = _dbTagTask;
        if (task == null) { DbPanel.ShowMessage("Indexing the model…"); return; }
        void Resolve()
        {
            try
            {
                _dbTags = task.Result;
                string? gid = _dbTags.Lookup(RawModelObject(tag));
                if (gid == null && tag is NamedPick np && _scene != null)
                {
                    var w = new Vec3(hitLocal.X, hitLocal.Y, hitLocal.Z) + _scene.Origin;
                    string[] cats = np.Kind switch
                    {
                        "Deck" => new[] { "deck slab" },
                        "Barrier" => new[] { "barrier" },
                        _ => new[] { "joint blockout", "expansion joint device" },
                    };
                    string name = np.Kind == "Deck" ? "Deck slab" : np.Name;
                    foreach (var c in cats) { gid = _db!.FindByNameNear(c, name, w.X, w.Y, w.Z); if (gid != null) break; }
                }
                if (gid == null) { _dbShown = null; DbPanel.ShowMessage("This object is not in the database (rebuild it after model changes)."); return; }
                // member pick level: show the parent row (level "member") instead of the piece
                if (parentLevel != null && _db!.Chain(gid) is { Count: >= 2 } ch && ch[^2].Level == parentLevel) gid = ch[^2].GlobalId;
                ShowDbElement(gid);
                // G1: a click in the 3D view opens the element's plan sheets in the app (unless the user closed the sheet window).
                if (!Database.PlanSheetWindow.IsOpen && !Database.PlanSheetWindow.ClosedByUser && _db!.LinkCount > 0
                    && !Environment.GetCommandLineArgs().Contains("--snapshot") || Environment.GetCommandLineArgs().Contains("--sheetwindow"))
                    Database.PlanSheetWindow.ShowElement(this, _db!, gid, DbLabel(gid));
            }
            catch (Exception ex) { DbPanel.ShowMessage("Database error: " + ex.Message); }
        }
        if (task.IsCompleted) Resolve();
        else if (Environment.GetCommandLineArgs().Contains("--snapshot")) Resolve();
        else
        {
            DbPanel.ShowMessage("Indexing the model…");
            task.ContinueWith(_ => Dispatcher.BeginInvoke(Resolve), TaskScheduler.Default);
        }
    }

    /// <summary>
    /// "GlobalId" row in the Properties panel (second row, under "Selected") for the picked element = the IFC GlobalId the export writes
    /// (deterministic, `IfcKeys`), from the background tag index - no database needed. Filled in when the index finishes if it is still building.
    /// </summary>
    private void ShowGlobalIdRow(object tag)
    {
        var rows = _vm.SelectedProperties;
        void Put(string v)
        {
            int at = rows.ToList().FindIndex(r => r.Name == "GlobalId");
            var row = new ViewModels.PropertyRow("GlobalId", v);
            if (at >= 0) rows[at] = row; else rows.Insert(Math.Min(1, rows.Count), row);
        }
        string Text(TagIndex t) => t.Lookup(RawModelObject(tag)) ?? "not indexed for this object (see the Database section below)";
        var task = _dbTagTask;
        if (task == null) { Put("indexing…"); return; }
        if (task.IsCompleted) { Put(task.IsFaulted ? "index failed" : Text(task.Result)); return; }
        if (Environment.GetCommandLineArgs().Contains("--snapshot")) { Put(Text(task.Result)); return; }
        Put("indexing…");
        task.ContinueWith(t => Dispatcher.BeginInvoke(() =>
        {
            if (!ReferenceEquals(_selectedTag, tag)) return; // selection changed meanwhile
            Put(t.IsFaulted ? "index failed" : Text(t.Result));
        }), TaskScheduler.Default);
    }

    private string DbLabel(string gid) => _db?.Get(gid) is { } e ? (e.Name.Length > 0 ? e.Name : e.Category) : gid;

    private void DbOnCleared()
    {
        _dbShown = null;
        DbPanel.Clear();
    }

    private void ShowDbElement(string gid)
    {
        if (_db == null) return;
        _dbShown = gid;
        DbPanel.ShowElement(_db, gid);
        ViewModels.MainViewModel.Log("DB: " + DbPanel.LastSummary);
        Database.PlanSheetWindow.Follow(_db, gid, DbLabel(gid));
    }

    /// <summary>Highlights, in the viewport, the mesh objects (plates, caps, columns, ...) below a hierarchy item. Bolts, studs and bars are instanced and not drawn in the highlight.</summary>
    private void HighlightDbGroup(string gid)
    {
        if (_db == null || _dbTags == null) return;
        var ids = new HashSet<string>(_db.Descendants(gid, 60000));
        ids.Add(gid);
        var tags = new List<object>();
        var pos = new Vector3Collection();
        var nrm = new Vector3Collection();
        var idx = new IntCollection();
        int tris = 0;
        foreach (var kv in SceneBuilder.Picks)
        {
            var full = kv.Key;
            var table = kv.Value;
            for (int i = 0; i < table.Tags.Count && tris < 400000; i++)
            {
                var raw = RawModelObject(table.Tags[i]);
                if (raw == null || _dbTags.Lookup(raw) is not { } g || !ids.Contains(g)) continue;
                tags.Add(table.Tags[i]);
                int t0 = table.TriStart[i], t1 = i + 1 < table.TriStart.Count ? table.TriStart[i + 1] : full.Indices!.Count / 3;
                for (int t = t0; t < t1; t++)
                    for (int k = 0; k < 3; k++)
                    {
                        int v = full.Indices![3 * t + k];
                        idx.Add(pos.Count);
                        pos.Add(full.Positions![v]);
                        nrm.Add(full.Normals![v]);
                    }
                tris += t1 - t0;
            }
        }
        // Instanced objects (bolts, studs, bars) are not drawn in the highlight but belong to the group (isolate).
        foreach (var kv in SceneBuilder.InstanceTags)
            foreach (var t in kv.Value)
                if (RawModelObject(t) is { } raw && _dbTags.Lookup(raw) is { } g && ids.Contains(g)) tags.Add(t);
        ClearHighlight();
        _selectedTag = null;
        _selectedModel = null;
        _groupSel = null;
        if (tris == 0 && tags.Count == 0) return;
        var geo = new MeshGeometry3D { Positions = pos, Normals = nrm, Indices = idx };
        geo.UpdateBounds();
        if (tris > 0) ShowHighlight(geo);
        _groupSel = (_db.Get(gid) is { } info ? (info.Name.Length > 0 ? info.Name : info.Category) : gid, tags.Distinct().ToList(), geo);
    }
}
