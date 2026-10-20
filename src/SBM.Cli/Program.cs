using SBM.Core.Project;
using System.Diagnostics;
using SBM.Api;

namespace SBM.Cli;

/// <summary>
/// sbm — command-line access to Steel Bridge Modeler projects (for batch work, Python/Excel automation, CI).
/// Exit code 0 = success, 1 = usage/input error, 2 = model has errors.
/// </summary>
public static partial class Program
{
    private const string Usage = """
        sbm — Steel Bridge Modeler command line

        Usage:
          sbm new <file.sbm> [--sample] [--metric]          Create a project (optionally the demo project)
          sbm info <file.sbm>                               Summary of alignments and bridges
          sbm get <file.sbm> [path]                         Show a value or list members at a path
          sbm set <file.sbm> <path>=<value> [...]           Set values (units allowed) and save
          sbm add <file.sbm> <listPath> <json|value>        Append to a list and save
          sbm remove <file.sbm> <path[index]>               Remove a list item and save
          sbm import-landxml <file.sbm> <alignments.xml>    Add alignments from LandXML and save
          sbm import-pgsuper <file.sbm> <model.pgs> [alignment] [begin station]
                                                            Add a PGSuper bridge (prestressed I-girders) on an alignment
          sbm app [--pid N] <command> [args...]            Drive the RUNNING app (live model): status, get, set, regen,
                                                            open, save, import-pgsuper, run, snapshot, view, section,
                                                            messages, undo, close. "sbm app help" lists them; "sbm app list"
                                                            shows running copies. (All other commands work on .sbm files.)
          sbm regen <file.sbm>                              Regenerate; print counts, warnings, timing
          sbm export <file.sbm> [--ifc a.ifc|a.ifczip] [--geometry parametric|abv] [--chord-tol mm] [--dxf b.dxf] [--landxml c.xml]
                                                            [--for-check [--keep-designations]]  IFC for an independent checker: originator notes stripped
          sbm db <file.sbm> [out.sqlite] [--pages] [--plans <folder>[,<folder>]] [--links rules.json] [--no-previews] [--no-vacuum]
                                                            [--for-check]  registry only: no sheet links, props text or model path
                                                            [--unused report.md]
          sbm db <file.sbm> --sheets-for <text> | --find-sheet <text> [--db <file.sqlite>] [--json]
                                                            Read-only queries on an existing database (no rebuild): sheets for an element
                                                            (mark / name / category match) or sheets by title / sheet no. (^regex ok)
                                                            Companion SQLite database: every element (IFC GlobalIds + hierarchy),
                                                            plan sheets (--pages splits the plan PDFs), element -> sheet links, QC
          sbm asset <file.sbm> <command> ...                Asset management in the companion database: photos, documents,
                                                            inspections (element-level CS1-CS4, NBI/SNBI ratings, CSV import /
                                                            export), findings, calcs (summary pages, values, diff, apply).
                                                            "sbm asset <file.sbm> help" lists the commands
          sbm run <script.csx> [file.sbm] [-- args...]      Run a C# script (globals: Doc, Print, Ft, In, Sta, Len)
          sbm bench <file.sbm> [n]                          Time cold/warm regeneration
          sbm nurbs <file.sbm> [--bridge NAME] [--chord LEN] [--tol LEN] [--csv out.csv]
                                                            Exact curved geometry (NURBS) report: regenerates with the project
                                                            setting Geometry.ExactCurvedGeometry off and on, prints timings, what
                                                            became NURBS (fallbacks), fit deviation and exact vs mesh volumes
          sbm clash <file.sbm> [--bridge NAME] [--csv out.csv] [--tol LEN] [--bolts] [--welds] [--no-misc] [--no-concrete]
                                                            Steel solid-vs-solid interference (part meshes; contact within the
                                                            tolerance, default 1/16", is not a clash): counts by category pair +
                                                            each pair with station / offset / nearest girder. --bridge = name filter;
                                                            drains / misc items are also tested against the pier concrete
          sbm elevations <file.sbm> [bridge]              Step-by-step elevations, grade to bearing seat, at every bearing
          sbm blocking <file.sbm> [bridge] [--all]          Camber / blocking per girder (key points; --all = every plan point)
          sbm drawings <file.sbm> <folder|file.zip> [bridge] [--zip]
                                                            2D DXF models: layout plan + elevation, plan, sections, bents, bar bending
                                                            (a .zip target or --zip = one zip of the DXFs)
          sbm sheets <file.sbm> <out.pdf> [bridge] [--dxf <folder|file.zip>] [--models]
                                                            11x17 plan set as a vector PDF; --dxf also writes every sheet as a
                                                            paper-space DXF (inches, border + title block) to a folder or one zip;
                                                            --models adds the full-size 2D model DXFs (subfolder "models")
          sbm compare-sheets <file.sbm> [bridge] [--plans <folder>] [--db <file.sqlite>] [--out compare.pdf]
                                                            Generated sheets side by side with the matching plan sheets + checklist/QC strip (PyMuPDF;
                                                            plans = folder with the plan PDFs or split pages; default <model folder>\plans)
          sbm qc-sheet <db.sqlite> "<bridge>:<S-n>" <plan set>:<pdf page> <ok|issue|unchecked> [note]
                                                            Store the check result of a generated sheet vs a plan page in the companion database (qc table)

        Paths:   Bridges[0].Supports[1].SkewDegrees   Bridges["Sample Bridge"].Girders.Section.WebDepth
                 Alignments[0].StartStation           Bridges[0].FieldSplices   Units.Kind
        Values:  lengths 12'-6 1/2"  54"  1.2 m  300 mm   stations 12+34.56   angles 15°30'00"   slopes -2%
                 plain numbers use the field's display unit (ft or in / m or mm); JSON sets whole objects (SI units)
        """;

    private static int DbQuery(string[] args, string? dbOpt, string? sheetsFor, string? findSheet)
    {
        string dbFile = dbOpt ?? SBM.IO.Database.CompanionDatabase.DefaultPath(args[1]);
        if (!File.Exists(dbFile)) { Console.Error.WriteLine($"{dbFile} does not exist: build it first (sbm db {args[1]})."); return 1; }
        bool json = args.Contains("--json");
        var jo = new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        using var mdb = new SBM.IO.Database.ModelDb(dbFile);
        if (sheetsFor != null)
        {
            var res = mdb.SheetsFor(sheetsFor);
            if (json)
            {
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
                {
                    query = res.Query, totalMatches = res.TotalMatches, shown = res.Elements.Count,
                    elements = res.Elements.Select(e => new
                    {
                        globalId = e.GlobalId, mark = e.Mark, name = e.Name, category = e.Category, path = e.Path,
                        sheets = e.Sheets.Select(s => new { planSet = s.PlanSet, pdfPage = s.PdfPage, sheetNo = s.DesignSheet, title = s.Title, role = s.Role, inherited = s.Inherited, linkedOn = s.LinkRootName, pdfPath = s.PdfPath }),
                    }),
                    pagesToRead = res.Pages.ToDictionary(k => k.Key, k => string.Join(",", k.Value)),
                }, jo));
                return 0;
            }
            Console.WriteLine($"{res.TotalMatches} element(s) match \"{res.Query}\"" + (res.TotalMatches > res.Elements.Count ? $" (showing {res.Elements.Count}, {res.TotalMatches - res.Elements.Count} more: refine the text)" : ""));
            foreach (var e in res.Elements)
            {
                Console.WriteLine($"- {(e.Mark.Length > 0 ? e.Mark + " " : "")}[{e.Category}] {e.Name}\n    {e.Path}");
                if (e.Sheets.Count == 0) Console.WriteLine("    (no linked sheets)");
                foreach (var s in e.Sheets)
                    Console.WriteLine($"    {s.PlanSet} p.{s.PdfPage} sheet {(s.DesignSheet.Length > 0 ? s.DesignSheet : "-")} \"{s.Title}\" {s.Role}{(s.Inherited ? $" (inherited from {s.LinkRootName})" : "")} {s.PdfPath}");
            }
            if (res.Pages.Count > 0)
            {
                Console.WriteLine("Pages to read:");
                foreach (var (pdf, pages) in res.Pages) Console.WriteLine($"{pdf}: {string.Join(",", pages)}");
            }
            return 0;
        }
        var sheets = mdb.FindSheets(findSheet!);
        if (json)
        {
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
            {
                query = findSheet, count = sheets.Count,
                sheets = sheets.Select(s => new { planSet = s.PlanSet, pdfPage = s.PdfPage, sheetNo = s.DesignSheet, title = s.Title, pdfPath = s.PdfPath, linkedElements = s.LinkedElements }),
            }, jo));
            return 0;
        }
        Console.WriteLine($"{sheets.Count} sheet(s) match \"{findSheet}\"");
        foreach (var s in sheets)
            Console.WriteLine($"- {s.PlanSet} p.{s.PdfPage} sheet {(s.DesignSheet.Length > 0 ? s.DesignSheet : "-")} \"{s.Title}\" {s.PdfPath} ({s.LinkedElements:N0} linked element(s))");
        return 0;
    }

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            Console.WriteLine(Usage);
            return args.Length == 0 ? 1 : 0;
        }
        try
        {
            switch (args[0].ToLowerInvariant())
            {
                case "new":
                {
                    Need(args, 2);
                    var doc = args.Contains("--sample") ? SbmDocument.Sample() : SbmDocument.New(Path.GetFileNameWithoutExtension(args[1]), args.Contains("--metric"));
                    doc.Save(args[1]);
                    Console.WriteLine($"Created {args[1]}");
                    return 0;
                }
                case "info":
                {
                    Need(args, 2);
                    var doc = SbmDocument.Open(args[1]);
                    var p = doc.Project;
                    Console.WriteLine($"{p.Name}  ({(doc.Units.IsUS ? "US" : "SI")} units)");
                    foreach (var a in p.Alignments)
                        Console.WriteLine($"  Alignment '{a.Name}': {doc.Units.FormatStation(a.StartStation)} to {doc.Units.FormatStation(a.EndStation)}, {a.Elements.Count} elements, {a.Profile.Vpis.Count} VPIs");
                    foreach (var b in p.Bridges)
                        Console.WriteLine($"  Bridge '{b.Name}' on '{b.AlignmentName}': {b.Supports.Count - 1} spans, {b.Girders.Count} girders, bearings {b.Bearings.Type}");
                    return 0;
                }
                case "get":
                {
                    Need(args, 2);
                    var doc = SbmDocument.Open(args[1]);
                    string path = args.Length > 2 ? args[2] : "";
                    var v = path.Length > 0 ? doc.Get(path) : doc.Project;
                    if (v is double or string or bool or int or Enum) Console.WriteLine(doc.GetText(path));
                    else foreach (var (name, type, value) in doc.Describe(path)) Console.WriteLine($"  {name,-34} {type,-22} {value}");
                    return 0;
                }
                case "set":
                {
                    Need(args, 3);
                    var doc = SbmDocument.Open(args[1]);
                    foreach (var kv in args.Skip(2))
                    {
                        int eq = kv.IndexOf('=');
                        if (eq <= 0) throw new ArgumentException($"Expected path=value, got '{kv}'.");
                        string shown = doc.Set(kv[..eq], kv[(eq + 1)..]);
                        Console.WriteLine($"  {kv[..eq]} = {shown}");
                    }
                    doc.Save();
                    return 0;
                }
                case "add":
                {
                    Need(args, 4);
                    var doc = SbmDocument.Open(args[1]);
                    int i = doc.Add(args[2], args[3]);
                    doc.Save();
                    Console.WriteLine($"  added {args[2]}[{i}]");
                    return 0;
                }
                case "remove":
                {
                    Need(args, 3);
                    var doc = SbmDocument.Open(args[1]);
                    doc.Remove(args[2]);
                    doc.Save();
                    Console.WriteLine($"  removed {args[2]}");
                    return 0;
                }
                case "import-landxml":
                {
                    Need(args, 3);
                    var doc = SbmDocument.Open(args[1]);
                    foreach (var m in doc.ImportLandXml(args[2])) Console.WriteLine("  " + m);
                    doc.Save();
                    return 0;
                }
                case "app":
                {
                    var rest = args.Skip(1).ToList();
                    int? pid = null;
                    int pi = rest.IndexOf("--pid");
                    if (pi >= 0 && pi + 1 < rest.Count) { pid = int.Parse(rest[pi + 1]); rest.RemoveRange(pi, 2); }
                    if (rest.Count == 0 || rest[0] == "list")
                    {
                        var all = SbmApp.RunningInstances();
                        Console.WriteLine(all.Count == 0 ? "No running Steel Bridge Modeler." : "Running (process ids): " + string.Join(", ", all));
                        return 0;
                    }
                    var app = SbmApp.Connect(pid);
                    var a = rest.Skip(1).ToArray();
                    // File arguments are resolved here: the app's working folder differs.
                    if (rest[0] is "open" or "save" or "import-pgsuper" or "run" or "snapshot" && a.Length > 0 && !a[0].StartsWith("--"))
                        a[0] = Path.GetFullPath(a[0]);
                    Console.WriteLine(app.Send(rest[0], a));
                    return 0;
                }
                case "import-pgsuper":
                {
                    Need(args, 3);
                    var doc = SbmDocument.Open(args[1]);
                    double sta = args.Length > 4 ? doc.Station(args[4]) : double.NaN;
                    var res = doc.ImportPgSuper(args[2], args.Length > 3 ? args[3] : null, sta);
                    Console.WriteLine($"Imported '{res.Bridge.Name}' on '{res.Bridge.AlignmentName}':");
                    foreach (var m in res.Notes) Console.WriteLine("  " + m);
                    foreach (var w in res.Warnings) Console.WriteLine("  warning: " + w);
                    doc.Save();
                    return 0;
                }
                case "regen":
                {
                    Need(args, 2);
                    var doc = SbmDocument.Open(args[1]);
                    var r = doc.Regenerate();
                    Print(r, doc);
                    return r.Errors.Count > 0 ? 2 : 0;
                }
                case "export":
                {
                    Need(args, 2);
                    var doc = SbmDocument.Open(args[1]);
                    string? Opt(string name) { int i = Array.IndexOf(args, name); return i > 0 && i + 1 < args.Length ? args[i + 1] : null; }
                    var geometry = Opt("--geometry")?.ToLowerInvariant() switch
                    {
                        null or "parametric" => SBM.IO.Ifc.IfcGeometryMode.Parametric,
                        "abv" or "referenceview" => SBM.IO.Ifc.IfcGeometryMode.ReferenceView,
                        var g => throw new ArgumentException($"--geometry {g}: use parametric or abv"),
                    };
                    // --chord-tol <mm>: ReferenceView polygon tolerance for hole circles / profile arcs (default 1/32 in = 0.79 mm).
                    double chord = Opt("--chord-tol") is { } ct ? double.Parse(ct, System.Globalization.CultureInfo.InvariantCulture) / 1000 : 0;
                    if (Opt("--ifc") is { } ifc)
                    {
                        doc.Regenerate();
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        var res = doc.ExportIfc(ifc, geometry, chord, args.Contains("--for-check"), args.Contains("--keep-designations"));
                        Console.WriteLine($"  IFC 4.3 ({geometry}{(args.Contains("--for-check") ? ", FOR CHECK" : "")}) → {ifc}  ({sw.Elapsed.TotalSeconds:0.0} s, {new FileInfo(ifc).Length / 1048576.0:0.0} MB, {res.EntityCount:N0} entities" +
                                          (geometry == SBM.IO.Ifc.IfcGeometryMode.ReferenceView ? $", {res.PolygonalSolids:N0} polygonal face sets, chord tol {res.ChordTolerance * 1000:0.###} mm)" : ")"));
                    }
                    if (Opt("--dxf") is { } dxf) { doc.ExportDxf(dxf); Console.WriteLine($"  DXF → {dxf}"); }
                    if (Opt("--landxml") is { } lx) { doc.ExportLandXml(lx); Console.WriteLine($"  LandXML → {lx}"); }
                    return 0;
                }
                case "db":
                {
                    Need(args, 2);
                    string? Opt(string name) { int i = Array.IndexOf(args, name); return i > 0 && i + 1 < args.Length ? args[i + 1] : null; }
                    if (Opt("--sheets-for") is { } sf) return DbQuery(args, Opt("--db"), sf, null);
                    if (Opt("--find-sheet") is { } fs) return DbQuery(args, Opt("--db"), null, fs);
                    var doc = SbmDocument.Open(args[1]);
                    var o = new SBM.IO.Database.DatabaseOptions
                    {
                        Pages = args.Contains("--pages"), Vacuum = !args.Contains("--no-vacuum"), Previews = !args.Contains("--no-previews"), LinksFile = Opt("--links"),
                        UnusedReport = Opt("--unused"), ForCheck = args.Contains("--for-check"), Log = m => Console.WriteLine("  " + m),
                    };
                    if (Opt("--plans") is { } pl) o.PlanFolders.AddRange(pl.Split(','));
                    o.BridgeNames.AddRange(doc.Project.Bridges.Select(b => b.Name));
                    string? outPath = args.Length > 2 && !args[2].StartsWith("--") ? args[2] : null;
                    if (args.Contains("--relink"))
                    {
                        // Sheets + links only, on the existing database (no regeneration, element registry untouched).
                        o.ModelFile = Path.GetFullPath(args[1]);
                        string dbFile = outPath ?? SBM.IO.Database.CompanionDatabase.DefaultPath(args[1]);
                        if (!File.Exists(dbFile)) { Console.Error.WriteLine($"{dbFile} does not exist: build it first (sbm db {args[1]})."); return 1; }
                        var rl = SBM.IO.Database.CompanionDatabase.Relink(dbFile, doc.Project.Name, o);
                        Console.WriteLine(rl.LinksFile != null ? $"  link rules {rl.LinksFile}" : "  link rules: none found");
                        if (rl.Links is { } ll) Console.WriteLine($"  sheets {ll.Sheets}, links {ll.Links:N0}, sheets linked {ll.SheetsLinked}, unlinked {ll.SheetsUnused} ({rl.Elapsed.TotalSeconds:F1} s)");
                        if (rl.Coverage.Length > 0) Console.WriteLine(rl.Coverage);
                        if (rl.Links != null) foreach (var w in rl.Links.Warnings) Console.WriteLine("  warning: " + w);
                        return 0;
                    }
                    var r = doc.ExportDatabase(outPath, o);
                    Console.WriteLine($"Database {r.Build.Path}: {r.Build.Elements:N0} elements in {r.Elapsed.TotalSeconds:F1} s (registry {r.Build.Elapsed.TotalSeconds:F1} s)");
                    foreach (var (lv, n) in r.Build.ByLevel.OrderByDescending(x => x.Value)) Console.WriteLine($"  {lv,-10} {n,9:N0}");
                    Console.WriteLine(o.ForCheck ? "  checking database (--for-check): no sheet links, plan sheets, QC or property text" : r.LinksFile != null ? $"  link rules {r.LinksFile}" : "  link rules: none found (no sheet-links*.json naming this model beside it): no plan sheet links");
                    if (r.Links is { } l)
                        Console.WriteLine($"  sheets {l.Sheets}, links {l.Links:N0}, sheets linked {l.SheetsLinked}, unlinked {l.SheetsUnused}");
                    else if (r.LinksFile != null) Console.WriteLine($"  sheets {r.Sheets}: no links (no sheet index in the rules and no imported pages: run with --pages)");
                    Console.WriteLine("  " + SBM.IO.Database.ModelDatabase.SizeText(r.Build.Path));
                    if (r.Coverage.Length > 0) Console.WriteLine(r.Coverage);
                    if (r.Links != null) foreach (var w in r.Links.Warnings) Console.WriteLine("  warning: " + w);
                    return 0;
                }
                case "asset":
                case "assets":
                    Need(args, 2);
                    return AssetCommand(args);
                case "run":
                {
                    Need(args, 2);
                    int dd = Array.IndexOf(args, "--");
                    var scriptArgs = dd > 0 ? args[(dd + 1)..] : Array.Empty<string>();
                    string? file = args.Length > 2 && args[2] != "--" ? args[2] : null;
                    var doc = file != null && File.Exists(file) ? SbmDocument.Open(file) : SbmDocument.New();
                    var res = await ScriptHost.RunFileAsync(args[1], doc, scriptArgs);
                    Console.Write(res.Output);
                    if (res.ReturnValue != null) Console.WriteLine(res.ReturnValue);
                    if (!res.Success) { Console.Error.WriteLine(res.Error); return 1; }
                    return 0;
                }
                case "elevations":
                {
                    Need(args, 2);
                    var doc = SbmDocument.Open(args[1]);
                    var chains = doc.BearingElevations(args.Length > 2 ? args[2] : null);
                    Console.Write(SBM.Core.Checks.ElevationCheck.ToText(chains, doc.Project.Units));
                    return chains.Any(c => c.Mismatch) ? 2 : 0;
                }
                case "blocking":
                {
                    Need(args, 2);
                    var doc = SbmDocument.Open(args[1]);
                    string? bn = args.Skip(2).FirstOrDefault(a => !a.StartsWith("--"));
                    var names = bn != null ? new[] { bn } : doc.Project.Bridges.Select(b => b.Name).ToArray();
                    foreach (var n in names)
                        if (doc.Framing(n).Camber is { } cr)
                            Console.Write(SBM.Core.Framing.CamberBlocking.ToText(n, cr, doc.Project.Units, args.Contains("--all")));
                    return 0;
                }
                case "drawings":
                {
                    Need(args, 3);
                    var pos = Positional(args, "--zip");
                    var doc = SbmDocument.Open(args[1]);
                    var sw = Stopwatch.StartNew();
                    string target = args[2];
                    if (args.Contains("--zip") && !SBM.IO.Dxf.SheetExport.IsZip(target)) target = target.TrimEnd('\\', '/') + ".zip";
                    var files = doc.ExportDrawings(target, pos.Count > 3 ? pos[3] : null);
                    foreach (var f in files) Console.WriteLine($"  {f}");
                    Console.WriteLine($"{files.Count} views{(SBM.IO.Dxf.SheetExport.IsZip(target) ? " → " + target : "")} in {sw.ElapsedMilliseconds} ms");
                    return 0;
                }
                case "sheets":
                {
                    Need(args, 3);
                    string? Opt(string name) { int i = Array.IndexOf(args, name); return i > 0 && i + 1 < args.Length ? args[i + 1] : null; }
                    var pos = Positional(args, "--models");
                    string? bridge = pos.Count > 3 ? pos[3] : null;
                    var doc = SbmDocument.Open(args[1]);
                    doc.Regenerate();
                    var sw = Stopwatch.StartNew();
                    var sheets = doc.Sheets(bridge);
                    string bname = doc.Bridge(bridge).Name;
                    SBM.IO.Pdf.PdfWriter.Write(args[2], sheets, bname);
                    Console.WriteLine($"{sheets.Count} sheets → {args[2]} in {sw.ElapsedMilliseconds} ms");
                    if (Opt("--dxf") is { } dxf)
                    {
                        sw.Restart();
                        var files = SBM.IO.Dxf.SheetExport.WriteSheets(dxf, bname, sheets, doc.Project.Units, args.Contains("--models") ? doc.Drawings(bridge) : null);
                        Console.WriteLine($"{files.Count} DXF files → {dxf} in {sw.ElapsedMilliseconds} ms");
                    }
                    return 0;
                }
                case "compare-sheets":
                {
                    Need(args, 2);
                    string? Opt(string name) { int i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
                    var optNames = new HashSet<string> { "--plans", "--db", "--out" };
                    var pos = args.Skip(2).Where((a, i) => !a.StartsWith("--") && !(i > 0 && optNames.Contains(args[i + 1]))).ToList();
                    var doc = SbmDocument.Open(args[1]);
                    doc.Regenerate();
                    string modelDir = Path.GetDirectoryName(Path.GetFullPath(args[1]))!;
                    string plans = Opt("--plans") ?? Path.Combine(modelDir, "plans");
                    string? db = Opt("--db");
                    string outPath = Opt("--out") ?? Path.Combine(modelDir, "compare.pdf");
                    string? script = SBM.IO.Database.CompanionDatabase.FindScript("compare_sheets.py") ?? throw new FileNotFoundException("tools/compare_sheets.py not found.");
                    var bridges = pos.Count > 0 ? new[] { pos[0] } : doc.Project.Bridges.Select(b => b.Name).ToArray();
                    foreach (var bn in bridges)
                    {
                        string tmp = Path.Combine(Path.GetTempPath(), "sbm-compare-" + Guid.NewGuid().ToString("N"));
                        Directory.CreateDirectory(tmp);
                        int n = doc.ExportSheetsWithMeta(Path.Combine(tmp, "sheets.pdf"), Path.Combine(tmp, "meta.json"), bn);
                        string o = bridges.Length > 1 ? Path.Combine(Path.GetDirectoryName(outPath)!, Path.GetFileNameWithoutExtension(outPath) + " - " + bn + ".pdf") : outPath;
                        var psi = new System.Diagnostics.ProcessStartInfo("python") { UseShellExecute = false };
                        foreach (var a in new[] { "-I", script, "compare", "--gen", Path.Combine(tmp, "sheets.pdf"), "--meta", Path.Combine(tmp, "meta.json"), "--plans", plans, "--out", o }) psi.ArgumentList.Add(a);
                        if (db != null) { psi.ArgumentList.Add("--db"); psi.ArgumentList.Add(db); }
                        Console.WriteLine($"{bn}: {n} sheets");
                        using var p = System.Diagnostics.Process.Start(psi)!;
                        p.WaitForExit();
                        try { Directory.Delete(tmp, true); } catch { }
                        if (p.ExitCode != 0) return p.ExitCode;
                    }
                    return 0;
                }
                case "qc-sheet":
                {
                    Need(args, 5);
                    string? script = SBM.IO.Database.CompanionDatabase.FindScript("compare_sheets.py") ?? throw new FileNotFoundException("tools/compare_sheets.py not found.");
                    var psi = new System.Diagnostics.ProcessStartInfo("python") { UseShellExecute = false };
                    foreach (var a in new[] { "-I", script, "qc", "--db", args[1], "--sheet", args[2], "--plan", args[3], "--status", args[4] }) psi.ArgumentList.Add(a);
                    if (args.Length > 5) { psi.ArgumentList.Add("--note"); psi.ArgumentList.Add(string.Join(" ", args.Skip(5))); }
                    using var p = System.Diagnostics.Process.Start(psi)!;
                    p.WaitForExit();
                    return p.ExitCode;
                }
                case "nurbs":
                    return NurbsCommand(args);
                case "clash":
                {
                    Need(args, 2);
                    string? Opt(string name) { int i = Array.IndexOf(args, name); return i > 0 && i + 1 < args.Length ? args[i + 1] : null; }
                    var doc = SbmDocument.Open(args[1]);
                    var o = new SBM.Core.Checks.ClashOptions { Bolts = args.Contains("--bolts"), Welds = args.Contains("--welds"), Misc = !args.Contains("--no-misc"), Concrete = !args.Contains("--no-concrete") };
                    if (Opt("--tol") is { } tl) o.Tolerance = doc.Units.TryParseLength(tl, SBM.Core.Units.LengthKind.Short, out double tv) ? tv : throw new ArgumentException($"Can't read length '{tl}'.");
                    var names = doc.Project.Bridges.Select(b => b.Name)
                        .Where(n => Opt("--bridge") is not { } bf || n.Contains(bf, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (names.Count == 0) throw new ArgumentException($"No bridge matches '{Opt("--bridge")}'.");
                    var sw = Stopwatch.StartNew();
                    doc.Regenerate();
                    var reps = names.Select(n => doc.Clashes(n, o)).SelectMany(r => r).ToList();
                    Console.Write(SBM.Core.Checks.SteelClashCheck.ToText(reps, doc.Units));
                    Console.WriteLine($"{reps.Sum(r => r.Clashes.Count)} clashes in {reps.Count} bridge(s), tolerance {doc.Units.FormatLength(o.Tolerance, SBM.Core.Units.LengthKind.Short)}, {sw.Elapsed.TotalSeconds:F0} s");
                    if (Opt("--csv") is { } csv) { SBM.Core.Checks.SteelClashCheck.WriteCsv(csv, reps, doc.Units); Console.WriteLine($"  -> {csv}"); }
                    return reps.Any(r => r.Clashes.Count > 0) ? 2 : 0;
                }
                case "bench":
                {
                    Need(args, 2);
                    int n = args.Length > 2 ? int.Parse(args[2]) : 5;
                    var proj = SbmDocument.Open(args[1]);
                    var sw = Stopwatch.StartNew();
                    var r = proj.Regenerate();
                    Console.WriteLine($"cold: {sw.ElapsedMilliseconds} ms ({r.Parts} parts, {r.Bolts} bolts, {r.Studs} studs)");
                    var times = new List<double>();
                    for (int i = 0; i < n; i++)
                    {
                        var fresh = SbmDocument.Open(args[1]); // no cache: full regeneration
                        sw.Restart();
                        fresh.Regenerate();
                        times.Add(sw.Elapsed.TotalMilliseconds);
                    }
                    Console.WriteLine($"warm full regeneration: min {times.Min():F0} ms, avg {times.Average():F0} ms");
                    sw.Restart();
                    proj.Regenerate();
                    Console.WriteLine($"no-change regeneration (cache): {sw.Elapsed.TotalMilliseconds:F1} ms");
                    return 0;
                }
                default:
                    Console.Error.WriteLine($"Unknown command '{args[0]}'.\n");
                    Console.WriteLine(Usage);
                    return 1;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or InvalidOperationException or FormatException or System.Text.Json.JsonException or SbmAppException or TimeoutException or Microsoft.Data.Sqlite.SqliteException)
        {
            Console.Error.WriteLine("Error: " + ex.Message);
            return 1;
        }
    }

    private static int NurbsCommand(string[] args)
    {
        Need(args, 2);
        string? Opt(string name) { int i = Array.IndexOf(args, name); return i > 0 && i + 1 < args.Length ? args[i + 1] : null; }
        SbmDocument Load(bool on)
        {
            var d = SbmDocument.Open(args[1]);
            if (Opt("--bridge") is { } b) d.Project.Bridges.RemoveAll(x => !string.Equals(x.Name, b, StringComparison.OrdinalIgnoreCase));
            d.Project.Geometry.ExactCurvedGeometry = on;
            if (Opt("--chord") is { } c) d.Project.Geometry.ChordTolerance = d.Length(c);
            if (Opt("--tol") is { } tl) d.Project.Geometry.FitTolerance = d.Length(tl);
            return d;
        }
        var offVol = new Dictionary<(string, string), double>();
        var times = new Dictionary<bool, List<double>> { [false] = new(), [true] = new() };
        SbmDocument? doc = null;
        foreach (bool on in new[] { false, true, false, true })
        {
            doc = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            doc = Load(on);
            var r = doc.Regenerate();
            times[on].Add(r.Elapsed.TotalMilliseconds);
            Console.WriteLine($"  regeneration, exact geometry {(on ? "ON " : "OFF")}: {r.Elapsed.TotalMilliseconds:F0} ms ({r.Parts} parts), " +
                $"{System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / 1048576} MB working set");
            if (!on)
                foreach (var b in doc.Project.Bridges)
                {
                    var m = doc.Model(b.Name);
                    if (m.Steel != null) foreach (var p in m.Steel.Parts) offVol[(b.Name, p.Mark)] = p.Volume;
                    if (m.Concrete is { } c)
                    {
                        if (c.Deck != null) offVol[(b.Name, "Deck slab")] = Math.Abs(c.Deck.Volume());
                        if (c.Haunches != null) offVol[(b.Name, "Haunches")] = Math.Abs(c.Haunches.Volume());
                        foreach (var (n, mm) in c.Barriers) offVol[(b.Name, n)] = Math.Abs(mm.Volume());
                    }
                }
        }
        Console.WriteLine($"Regeneration (second runs): OFF {times[false][^1]:F0} ms, ON {times[true][^1]:F0} ms");
        var rows = new List<NurbsQuantityRow>();
        foreach (var b in doc!.Project.Bridges)
        {
            var m = doc.Model(b.Name);
            Console.WriteLine($"{b.Name}:");
            if (m.Steel?.Nurbs is { } sn) Console.WriteLine("  steel: " + sn.Summary());
            if (m.Concrete?.Nurbs is { } cn) Console.WriteLine("  concrete: " + cn.Summary());
            rows.AddRange(NurbsQuantities.Rows(m));
        }
        double In3(double v) => doc.Units.IsUS ? v / Math.Pow(0.3048, 3) : v;
        string vu = doc.Units.IsUS ? "ft3" : "m3";
        Console.WriteLine($"{"kind",-28} {"n",5} {"faces",7} {"holes",6} {"exact " + vu,14} {"mesh-ON vs exact",17} {"mesh-OFF vs exact",18} {"max fit dev",12}");
        foreach (var g in rows.GroupBy(r => r.Kind).OrderBy(g => g.Key))
        {
            double ex = g.Sum(r => r.ExactVolume), mon = g.Sum(r => r.MeshVolume);
            double moff = g.Sum(r => offVol.GetValueOrDefault((r.Bridge, r.Element), double.NaN));
            double maxOff = g.Max(r => Math.Abs(offVol.GetValueOrDefault((r.Bridge, r.Element), double.NaN) / r.ExactVolume - 1) * 100);
            double maxOn = g.Max(r => Math.Abs(r.VolumeDiffPercent));
            Console.WriteLine($"{g.Key,-28} {g.Count(),5} {g.Sum(r => r.Faces),7} {g.Sum(r => r.Holes),6} {In3(ex),14:F3} {(mon / ex - 1) * 100,8:+0.0000;-0.0000;0.0000}% max {maxOn:0.000}% " +
                $"{(moff / ex - 1) * 100,8:+0.0000;-0.0000;0.0000}% max {maxOff:0.000}% {g.Max(r => r.MaxDeviation) * 1000,9:0.0000} mm");
        }
        if (Opt("--csv") is { } csv)
        {
            using var w = new StreamWriter(csv);
            w.WriteLine("bridge,element,kind,solids,faces,holes,exact_m3,mesh_on_m3,mesh_off_m3,exact_area_m2,mesh_area_m2,max_dev_m");
            foreach (var r in rows)
                w.WriteLine(string.Join(",", $"\"{r.Bridge}\"", $"\"{r.Element}\"", r.Kind, r.Solids, r.Faces, r.Holes, r.ExactVolume.ToString("R"), r.MeshVolume.ToString("R"),
                    offVol.GetValueOrDefault((r.Bridge, r.Element), double.NaN).ToString("R"), r.ExactArea.ToString("R"), r.MeshArea.ToString("R"), r.MaxDeviation.ToString("R")));
            Console.WriteLine($"  per element -> {csv}");
        }
        return 0;
    }

    /// <summary>Positional arguments: everything except "--" options; options in <paramref name="flags"/> take no value, any other
    /// "--name" takes the next argument as its value.</summary>
    private static List<string> Positional(string[] args, params string[] flags)
    {
        var res = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--")) { res.Add(args[i]); continue; }
            if (!flags.Contains(args[i])) i++;
        }
        return res;
    }

    private static void Need(string[] args, int n)
    {
        if (args.Length < n) throw new ArgumentException($"'{args[0]}' needs more arguments. Run 'sbm help'.");
    }

    private static void Print(RegenerationReport r, SbmDocument doc)
    {
        double tons = r.SteelMassKg / (doc.Units.IsUS ? 907.18474 : 1000);
        Console.WriteLine($"Regenerated {r.Regenerated} of {r.Bridges} bridge(s) ({r.Reused} reused) in {r.Elapsed.TotalMilliseconds:F0} ms");
        Console.WriteLine($"  {r.CrossFrames} cross frames, {r.Parts} parts, {r.Bolts} bolts, {r.Studs} studs, {tons:F1} {(doc.Units.IsUS ? "tons" : "t")} steel");
        foreach (var b in doc.Project.Bridges.Where(b => b.Girders.IsPrestressed))
            if (doc.Model(b.Name).Prestressed is { } ps)
                Console.WriteLine($"  {b.Name}: {ps.Beams.Count} {ps.Shape.Name} prestressed beams, {ps.Beams.Sum(x => x.Strands.Count)} strands, " +
                    $"{ps.TotalMass / (doc.Units.IsUS ? 907.18474 : 1000):F1} {(doc.Units.IsUS ? "tons" : "t")} precast");
        foreach (var w in r.Warnings) Console.WriteLine("  warning: " + w);
        foreach (var e in r.Errors) Console.WriteLine("  ERROR: " + e);
    }
}
