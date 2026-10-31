using SBM.Core.Project;
using System.Diagnostics;
using SBM.Api;

namespace SBM.Cli;

/// <summary>
/// sbm â€” command-line access to Steel Bridge Modeler projects (for batch work, Python/Excel automation, CI).
/// Exit code 0 = success, 1 = usage/input error, 2 = model has errors.
/// </summary>
public static partial class Program
{
    private const string Usage = """
        sbm â€” Steel Bridge Modeler command line

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
                                                            [--3dm a.3dm [--bridges A,B] [--no-detail] [--mesh-only] [--terrain] [--bar-solids] [--3dm-version 7|8] [--3dm-layers REGEX]]
                                                            Rhino 3DM: exact extrusions / B-reps, fastener blocks, bar and alignment
                                                            curves, layers per bridge, IFC GlobalIds as user text (IFC base point)
          sbm 3dm-info <file.3dm> [--blocks] [--objects [layer regex]]
                                                            Read a 3DM back: units, objects by layer / type, blocks, notes;
                                                            --objects: one row per object (type, closed, valid, volume, bounds)
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
          sbm <command> ... --lod 300[,Fasteners=200,...]   (any command: level of detail 100/200/300/350/400 per group Steel, Connections,
                                                            Fasteners, Welds, Reinforcing, Prestressing; in memory only, never saved)
          sbm run <script.csx> [file.sbm] [--no-pipeline] [-- args...]
                                                            Run a C# script (globals: Doc, Print, Ft, In, Sta, Len); when the script
                                                            SAVES a model, `sbm pipeline` runs on it afterwards (--no-pipeline skips;
                                                            --no-db / --no-terrain / --no-index / --offline are passed on)
          sbm pipeline <file.sbm> [--status] [--no-regen] [--no-db] [--no-index] [--no-terrain] [--offline] [--reindex] [--force-db]
                       [--pdf <plan set.pdf>]... [--plans <folder>[,<folder>]] [--terrain-dir <folder>] [--margin LEN]
                                                            REQUIRED final step of every model (alias build-all; also runs by itself
                                                            after `sbm run` saves a model): regenerate -> georeference check ->
                                                            terrain (USGS 3DEP for a georeferenced project, cached) -> plan sheet index
                                                            (automatic from the PDF text layer / OCR; skeleton sheet-links file for a new
                                                            plan set; curated rules stay authoritative) -> companion database + sheet
                                                            links. Reports DONE / UP TO DATE / SKIPPED / UNRESOLVED / FAILED per step.
                                                            --status = report only, changes nothing
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
          sbm project <file.sbm> [--view top|front|left|x,y,z] [--up x,y,z] [--origin x,y,z] [--bridge NAME] [--layers L1,L2,-L3]
                     [--kinds K1,K2] [--girder N] [--near LEN] [--far LEN] [--developed alignment[:NAME]|girder:G] [--from-left]
                     [--hidden show|dashed|none|off] [--method exact|zbuffer] [--window x0,y0,x1,y1] [--bolts] [--welds] [--rebar]
                     [--no-holes] [--no-recipes] [--sharp DEG] [--transparent L1,L2] [--dxf out.dxf] [--svg out.svg] [--pdf out.pdf] [--title TEXT]
                                                            3D -> 2D projection of the model with hidden-line removal: visible
                                                            edges on the 3D layer names, hidden ones on <layer>-HIDDEN (dashed),
                                                            every line tagged with its element. Lengths in model units (12 m, 40');
                                                            depth (near / far) from the origin (default: center of the selection)
                                                            along the view direction; developed: x = station (alignment) or distance
                                                            along the girder, y = elevation, depth = offset (viewer on the right)
          sbm elevations <file.sbm> [bridge]              Step-by-step elevations, grade to bearing seat, at every bearing
          sbm roads <file.sbm>                              Roadway corridors: course volumes, top areas, earthwork, clearances, diagnostics
          sbm walls <file.sbm>                              Retaining walls: stem / footing volumes per panel, face area, diagnostics
          sbm grading <file.sbm> [--at x,y]                 Grading: graded surface, cut / fill, protection; --at = finished ground at a point
          sbm finished-ground <file.sbm> <out.xml> [--spacing L] [--margin L]
                                                            Merged finished ground (roads > grading > existing ground) as one LandXML TIN on a
                                                            grid (default 1 m spacing) over the civil items + margin (default 15 m)
          sbm import-corridor <file.sbm> <landxml> [--alignment A] [--name ROAD] [--lines a,b,c*] [--courses Name:t:Material,...] [--template]
                                                            Civil 3D corridor feature lines (PlanFeatures) -> a road built between the lines;
                                                            --template = parametric Left / Right components when the offsets are constant
          sbm utilities <file.sbm> [--profile <folder>] [--ve N] [--no-clash]
                                                            Utility networks: structures (rim, sump, size, cone / frame), pipes (inverts,
                                                            slope, cover), duct banks, hangers, LF by size, checks, clashes / clearances
                                                            against the bridges / walls / roads / other networks; --profile = one DXF pipe
                                                            profile per run (vertical exaggeration --ve, default 10). Import: sbm import-landxml
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
          sbm measure <file.sbm> --part|--assembly|--member|--distance|--point|--station|--bar|--cover|--spacing|--laps ... [--json] [--svg f]
          sbm section <file.sbm> --plane x,y,z,nx,ny,nz | --station STA [--normal-to ...] [--svg/--dxf/--csv f] [--dims]
                                                            Read-only QC measurements (flattened plates, assembly planes, true sections,
                                                            distances, rebar); "sbm measure" alone lists the options. Never saves the model.
          sbm centerlines <file.sbm> [--bridge NAME] [--kind sub,part] [--category Column] [--csv f] [--json f] [--dxf f]
                                                            Geometric centerline + section centroid line of every element; "sbm centerlines" alone = help.
          sbm crossframes <file.sbm> [--bridge NAME] [--csv f] [--twist] [--top L --depth L ...]
                                                            Standard cross frame depth report, hole-shift table and twist (read-only); "sbm crossframes" alone = help.
          sbm review <file.sbm> [--all] [--no-regen] [--confirm PATH] [--accept-pdf FILE]   agent review: PDFs attached / unchanged,
                                                            sheets vs elements, inputs still at program starting values, generator flags
          sbm cable-stayed <file.sbm> [--json f] [--nodes f --members f] [--stays f]
                                                            Cable-stayed report (stays, first-cut forces, pylon balance, clearances) + analysis wireframe.

        Paths:   Bridges[0].Supports[1].SkewDegrees   Bridges["Sample Bridge"].Girders.Section.WebDepth
                 Alignments[0].StartStation           Bridges[0].FieldSplices   Units.Kind
        Values:  lengths 12'-6 1/2"  54"  1.2 m  300 mm   stations 12+34.56   angles 15Â°30'00"   slopes -2%
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
        // global option: --lod <level[,Group=level...]> = level of detail of every model opened by this command, in memory only (never saved)
        int li = Array.IndexOf(args, "--lod");
        if (li >= 0 && li + 1 < args.Length)
        {
            SbmDocument.LodOverride = SbmDocument.ParseLod(args[li + 1]);
            args = args.Take(li).Concat(args.Skip(li + 2)).ToArray();
        }
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
                    if (!args.Contains("--sample"))
                        Console.WriteLine("  New plan-set model: follow the checklist in CLAUDE.md / docs/MODEL-PIPELINE.md (georeference, plan PDFs in "
                            + "Doc.Project.PlanPdfs); `sbm run <build.csx> <file.sbm>` runs `sbm pipeline` after the script saves.");
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
                case "3dm-info":
                {
                    Need(args, 2);
                    var s = SBM.IO.Rhino3dm.Rhino3dmInfo.Read(args[1]);
                    Console.WriteLine($"{args[1]}: units {s.Units}, {s.Objects:N0} objects, {s.Layers} layers, {s.BlockDefinitions} block definitions, " +
                                      $"{s.ObjectsWithGlobalId:N0} with GlobalId, B-reps {s.ClosedBreps:N0} closed / {s.OpenBreps:N0} open");
                    Console.WriteLine("  by type: " + string.Join(", ", s.ByType.Select(kv => $"{kv.Key} {kv.Value:N0}")));
                    foreach (var (layer, types) in s.ByLayer) Console.WriteLine($"  {layer}: " + string.Join(", ", types.Select(kv => $"{kv.Key} {kv.Value:N0}")));
                    if (args.Contains("--blocks")) foreach (var (b, n) in s.BlockUse) Console.WriteLine($"  block '{b}': {n:N0}");
                    if (Array.IndexOf(args, "--objects") is int oi && oi > 0)
                    {
                        string? rx = oi + 1 < args.Length && !args[oi + 1].StartsWith("--") ? args[oi + 1] : null;
                        Console.WriteLine("  layer\tname\tkind\ttype\tclosed\tvalid\tfaces\tvolume\tminX\tminY\tminZ\tmaxX\tmaxY\tmaxZ\tmodel_m3\telement\tnaked");
                        foreach (var ob in SBM.IO.Rhino3dm.Rhino3dmInfo.Objects(args[1], rx))
                            Console.WriteLine(FormattableString.Invariant($"  {ob.Layer}\t{ob.Name}\t{ob.Kind}\t{ob.Type}\t{ob.Closed}\t{ob.Valid}\t{ob.Faces}\t{ob.Volume:G9}\t{ob.MinX:F4}\t{ob.MinY:F4}\t{ob.MinZ:F4}\t{ob.MaxX:F4}\t{ob.MaxY:F4}\t{ob.MaxZ:F4}\t{ob.ModelVolumeM3}\t{ob.Element}\t{ob.Naked}"));
                    }
                    foreach (var (k, v) in s.DocumentStrings) Console.WriteLine($"  {k} = {v}");
                    return 0;
                }
                case "regen":
                {
                    Need(args, 2);
                    var doc = SbmDocument.Open(args[1]);
                    var r = doc.Regenerate();
                    Print(r, doc);
                    foreach (var tp in doc.TerrainProblems) Console.WriteLine("  warning: " + tp);
                    var st = ModelPipeline.Status(doc);
                    if (st.Open > 0) Console.WriteLine($"Model pipeline: {st.Open} item(s) open - run `sbm pipeline \"{args[1]}\"` (`--status` lists them)");
                    return r.Errors.Count > 0 ? 2 : 0;
                }
                case "pipeline":
                case "build-all":
                    return PipelineCommand(args);
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
                        Console.WriteLine($"  IFC 4.3 ({geometry}{(args.Contains("--for-check") ? ", FOR CHECK" : "")}) â†’ {ifc}  ({sw.Elapsed.TotalSeconds:0.0} s, {new FileInfo(ifc).Length / 1048576.0:0.0} MB, {res.EntityCount:N0} entities" +
                                          (geometry == SBM.IO.Ifc.IfcGeometryMode.ReferenceView ? $", {res.PolygonalSolids:N0} polygonal face sets, chord tol {res.ChordTolerance * 1000:0.###} mm)" : ")"));
                    }
                    if (Opt("--3dm") is { } r3)
                    {
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        var bl = Opt("--bridges")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        var rr = doc.Export3dm(r3, bl, detail: !args.Contains("--no-detail"), meshOnly: args.Contains("--mesh-only"),
                            terrain: args.Contains("--terrain"), barSolids: args.Contains("--bar-solids"), fileVersion: int.Parse(Opt("--3dm-version") ?? "8"), centerlines: args.Contains("--centerlines"), exactCurved: !args.Contains("--no-exact"), layers: Opt("--3dm-layers"));
                        Console.WriteLine($"  Rhino 3DM ({rr.Units}) â†’ {r3}  ({rr.Elapsed.TotalSeconds:0.0} s export, {sw.Elapsed.TotalSeconds:0.0} s with the regeneration check, {new FileInfo(r3).Length / 1048576.0:0.0} MB, {rr.Objects:N0} objects: " +
                                          $"{rr.Extrusions:N0} extrusions, {rr.MiteredExtrusions:N0} mitered extrusions, {rr.ObliqueBreps:N0} oblique B-reps, {rr.NurbsBreps:N0} NURBS B-reps ({rr.NurbsBrepsClosed:N0} closed), {rr.PlanarBreps:N0} planar B-reps, {rr.ExactBodies:N0} beam bodies, {rr.Meshes:N0} meshes, " +
                                          $"{rr.Curves:N0} curves ({rr.CurveSegments:N0} pieces), {rr.BlockInstances:N0} block instances of {rr.BlockDefinitions:N0} blocks; {rr.Layers} layers, {rr.Fallbacks} fallbacks to mesh, {rr.DroppedSlivers:N0} sliver triangles left out)");
                        Console.WriteLine($"  base point (Rhino origin) = grid {rr.BasePoint.X:0.###}, {rr.BasePoint.Y:0.###}, {rr.BasePoint.Z:0.###} m (as the IFC export)");
                        foreach (var n in rr.Notes.Take(10)) Console.WriteLine("  " + n);
                        foreach (var n in rr.FallbackItems.Take(5)) Console.WriteLine("    e.g. " + n);
                        if (rr.MeshReasons.Count > 0)
                            Console.WriteLine("  still meshes: " + string.Join("; ", rr.MeshReasons.OrderByDescending(kv => kv.Value).Take(6).Select(kv => $"{kv.Value:N0} {kv.Key}")));
                        Console.WriteLine("  timings: " + string.Join(", ", rr.Timings.Select(kv => $"{kv.Key} {kv.Value:0.0} s")));
                    }
                    if (Opt("--dxf") is { } dxf) { doc.ExportDxf(dxf); Console.WriteLine($"  DXF â†’ {dxf}"); }
                    if (Opt("--landxml") is { } lx) { doc.ExportLandXml(lx); Console.WriteLine($"  LandXML â†’ {lx}"); }
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
                    if (file is "--no-pipeline" or "--no-db" or "--no-terrain" or "--no-index" or "--offline") file = null;
                    var doc = file != null && File.Exists(file) ? SbmDocument.Open(file) : SbmDocument.New();
                    DateTime? before = file != null && File.Exists(file) ? File.GetLastWriteTimeUtc(file) : null;
                    var res = await ScriptHost.RunFileAsync(args[1], doc, scriptArgs);
                    Console.Write(res.Output);
                    if (res.ReturnValue != null) Console.WriteLine(res.ReturnValue);
                    if (!res.Success) { Console.Error.WriteLine(res.Error); return 1; }
                    PipelineAfterRun(doc, args, file, before);
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
                case "roads":
                {
                    Need(args, 2);
                    var doc = SbmDocument.Open(args[1]);
                    var u = doc.Project.Units;
                    double vol = u.IsUS ? 1 / 0.764554857984 : 1, area = u.IsUS ? 1 / 0.83612736 : 1;
                    string vu = u.IsUS ? "CY" : "m3", au = u.IsUS ? "SY" : "m2";
                    foreach (var rm in doc.Roads())
                    {
                        Console.WriteLine($"Road {rm.Definition.Name} ({rm.Definition.AlignmentName} {u.FormatStation(rm.Definition.BeginStation)} - {u.FormatStation(rm.Definition.EndStation)})");
                        foreach (var g in rm.Parts.GroupBy(p => (p.Side, p.Component)))
                            foreach (var p in g)
                                Console.WriteLine($"  {g.Key.Side,-5} {g.Key.Component,-22} {(p.Course == "" ? "(section)" : p.Course),-18} {p.Material,-28} {p.Volume * vol,12:N2} {vu}");
                        foreach (var (k, a) in rm.TopArea) Console.WriteLine($"  top area {k,-30} {a * area,12:N2} {au}");
                        // section levels at the first, middle and last section: depth below the finished surface at the inner edge of each side
                        foreach (var lv in rm.Levels.Where(l => l.Name != "finished grade"))
                        {
                            var fg = rm.Levels.FirstOrDefault(l => l.Side == lv.Side && l.Name == "finished grade");
                            var secs = lv.Sections;
                            if (secs.Count == 0) continue;
                            var picks = new[] { secs[0], secs[secs.Count / 2], secs[^1] }.Distinct();
                            Console.WriteLine($"  level {lv.Side,-5} {lv.Name,-30}" + string.Concat(picks.Select(x =>
                            {
                                // depth below the road's own finished surface at that offset (none beyond the template)
                                var top = fg?.Sections.FirstOrDefault(f => Math.Abs(f.Station - x.Station) < 1e-9).Line;
                                double? Fin(double o)
                                {
                                    if (top == null) return null;
                                    for (int k = 0; k + 1 < top.Count; k++)
                                    {
                                        var (a0, z0) = top[k]; var (a1, z1) = top[k + 1];
                                        double lo = Math.Min(a0, a1), hi = Math.Max(a0, a1);
                                        if (hi - lo > 1e-9 && o >= lo - 1e-6 && o <= hi + 1e-6) return z0 + (z1 - z0) * (o - a0) / (a1 - a0);
                                    }
                                    return null;
                                }
                                string At((double o, double z) q) => $"EL {u.FormatLength(q.z, SBM.Core.Units.LengthKind.Coordinate)} at {(Math.Abs(q.o) < 1e-6 ? "CL" : u.FormatLength(Math.Abs(q.o)) + (q.o < 0 ? " LT" : " RT"))}"
                                    + (Fin(q.o) is double f ? $" ({u.FormatLength(f - q.z)} below the finished surface)" : " (beyond the template)");
                                return $"\n      {u.FormatStation(x.Station)}: {At(x.Line[0])} to {At(x.Line[^1])}";
                            })));
                        }
                        if (rm.CutVolume is double cv && rm.FillVolume is double fv)
                            Console.WriteLine($"  earthwork (to subgrade / placed subbase, average end areas): cut {cv * vol,12:N2} {vu}   fill {fv * vol,12:N2} {vu}");
                        foreach (var c in rm.Clearances)
                            Console.WriteLine($"  {c.Kind} clearance to {c.Bridge,-28} {u.FormatLength(c.Measured)} (required {(double.IsNaN(c.Required) ? "not given - UNRESOLVED" : u.FormatLength(c.Required))}) " +
                                $"{(c.Ok switch { true => "OK", false => "CONFLICT", null => "" })} at {c.Element}");
                        foreach (var dg in rm.Diagnostics) Console.WriteLine($"  {dg.Severity}: {dg.Message}");
                    }
                    if (doc.Project.Roads.Count == 0) Console.WriteLine("No roads in this model (BridgeProject.Roads).");
                    return 0;
                }
                case "finished-ground":
                {
                    Need(args, 3);
                    var doc = SbmDocument.Open(args[1]);
                    string? FO(string n) { int i = Array.IndexOf(args, n); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
                    double M(string? v, double dflt) => v == null ? dflt : doc.Project.Units.TryParseLength(v, SBM.Core.Units.LengthKind.Long, out double x) ? x : throw new ArgumentException("bad length " + v);
                    double spacing = M(FO("--spacing"), 1.0), margin = M(FO("--margin"), 15.0);
                    foreach (var m in doc.ExportFinishedGround(args[2], spacing, margin)) Console.WriteLine(m);
                    return 0;
                }
                case "import-corridor":
                {
                    Need(args, 3);
                    var doc = SbmDocument.Open(args[1]);
                    string? CO(string n) { int i = Array.IndexOf(args, n); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
                    string al = CO("--alignment") ?? doc.Project.Alignments.FirstOrDefault()?.Name ?? throw new ArgumentException("The model has no alignment.");
                    var lines = CO("--lines")?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    // --courses "Surface:0.05:HMA,Base:0.25:Aggregate" (thicknesses in the display length unit)
                    var courses = CO("--courses")?.Split(',').Select(c => c.Split(':')).Select(p => new SBM.Core.Road.RoadCourse
                    {
                        Name = p[0], Thickness = doc.Project.Units.TryParseLength(p.Length > 1 ? p[1] : "", SBM.Core.Units.LengthKind.Short, out double tk) ? tk : throw new ArgumentException($"--courses: bad thickness in '{string.Join(":", p)}'."), Material = p.Length > 2 ? p[2] : "",
                    }).ToList();
                    foreach (var m in doc.ImportCorridor(args[2], al, CO("--name"), lines, courses, args.Contains("--template"))) Console.WriteLine(m);
                    doc.Save();
                    return 0;
                }
                case "utilities":
                {
                    Need(args, 2);
                    var doc = SbmDocument.Open(args[1]);
                    var u = doc.Project.Units;
                    string L(double m) => u.FormatLength(m);
                    string E(double m) => double.IsNaN(m) ? "-" : u.FormatLength(m, SBM.Core.Units.LengthKind.Coordinate);
                    string? UOpt(string n) { int i = Array.IndexOf(args, n); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
                    string? profDir = UOpt("--profile");
                    double ve = UOpt("--ve") is string ves ? double.Parse(ves, System.Globalization.CultureInfo.InvariantCulture) : 10;
                    var nets = doc.Utilities(finish: !args.Contains("--no-clash"));
                    var fgp = profDir != null ? doc.FinishedGround() : null;
                    foreach (var um in nets)
                    {
                        var nd = um.Definition;
                        Console.WriteLine($"Network {nd.Name} ({nd.System}{(nd.Owner.Length > 0 ? ", " + nd.Owner : "")}){(nd.Source.Length > 0 ? " - " + nd.Source : "")}");
                        foreach (var s in um.Structures)
                        {
                            var sd = s.Definition;
                            string size = sd.Shape == SBM.Core.Utility.UtilityStructureShape.Round ? $"{(double.IsNaN(sd.InnerDiameter) ? "?" : L(sd.InnerDiameter))} dia" : $"{(double.IsNaN(sd.InnerLength) ? "?" : L(sd.InnerLength))} x {(double.IsNaN(sd.InnerWidth) ? "?" : L(sd.InnerWidth))}";
                            Console.WriteLine($"  structure {sd.Name,-14} {sd.Kind,-11} {size,-20} rim {E(s.Rim)} ({(s.RimSource.Length > 0 ? s.RimSource : "-")})  sump {E(s.Sump)}  depth {(double.IsNaN(s.Rim) || double.IsNaN(s.Sump) ? "-" : L(s.Rim - s.Sump))}{(s.IsBuilt ? "" : "  NOT BUILT")}");
                        }
                        foreach (var p in um.Pipes)
                        {
                            var pd = p.Definition;
                            string size = pd.Shape == SBM.Core.Utility.UtilityPipeShape.Circular ? L(pd.InnerDiameter) + " dia" : $"{L(pd.InnerRise)} x {L(pd.InnerSpan)} {pd.Shape}";
                            Console.WriteLine($"  pipe      {pd.Name,-14} {pd.From,-10} -> {pd.To,-10} {size,-18} {pd.Material,-8} L {L(p.Length)}  inv {E(p.Start.Z)} / {E(p.End.Z)}  slope {p.Slope * 100:0.###} %  cover {(p.MinCover is double c ? L(c) : "-")}");
                        }
                        foreach (var g in um.Pipes.GroupBy(p => (p.Definition.Shape, p.Definition.InnerDiameter, p.Definition.InnerRise, p.Definition.InnerSpan, p.Definition.Material)))
                        {
                            var f = g.First().Definition;
                            string size = f.Shape == SBM.Core.Utility.UtilityPipeShape.Circular ? L(f.InnerDiameter) : $"{L(f.InnerRise)} x {L(f.InnerSpan)}";
                            double len = g.Sum(p => p.Length);
                            Console.WriteLine($"  quantity  {f.Shape} {size} {f.Material}: {(u.IsUS ? len / 0.3048 : len):N1} {(u.IsUS ? "LF" : "m")}");
                        }
                        foreach (var b in um.DuctBanks)
                            Console.WriteLine($"  duct bank {b.Definition.Name,-14} {b.Definition.From,-10} -> {b.Definition.To,-10} {b.Definition.Rows} x {b.Definition.Columns} @ {L(b.Definition.ConduitDiameter)}  encasement {L(b.Definition.Width)} x {L(b.Definition.Height)}  L {L(b.Length)}  bottom {E(b.Start.Z)} / {E(b.End.Z)}  concrete {b.ConcreteVolume:0.###} m3");
                        foreach (var hg in um.Hangers.GroupBy(h => h.Pipe))
                            Console.WriteLine($"  hangers   {hg.Key.Definition.Name,-14} {hg.Count()} from {hg.Key.Definition.Hangers!.Bridge}, rods {L(hg.Min(h => h.Length))} - {L(hg.Max(h => h.Length))}{(hg.Any(h => h.Mesh.IsEmpty) ? " (rods not built: no diameter)" : "")}");
                        foreach (var g in um.Structures.Where(s => s.IsBuilt).GroupBy(s => s.Definition.Kind))
                            Console.WriteLine($"  quantity  {g.Key}: {g.Count()} EA");
                        foreach (var g in um.Structures.SelectMany(s => s.Parts).Where(p => p.Kind != "cone").GroupBy(p => p.Kind))
                            Console.WriteLine($"  quantity  {g.Key}: {g.Count()} EA");
                        if (um.DuctBanks.Count > 0) Console.WriteLine($"  quantity  conduit: {(u.IsUS ? um.DuctBanks.Sum(b => b.ConduitLength) / 0.3048 : um.DuctBanks.Sum(b => b.ConduitLength)):N1} {(u.IsUS ? "LF" : "m")}, encasement {um.DuctBanks.Sum(b => b.ConcreteVolume) * (u.IsUS ? 1.30795 : 1):N2} {(u.IsUS ? "CY" : "m3")}");
                        foreach (var c in um.Clashes)
                            Console.WriteLine($"  {c.Kind,-9} {c.Element} x {c.OtherKind} {c.Other}: {(c.Overlap ? "overlap " : "clear ")}{L(c.Value)}{(c.Overlap ? "" : ", required " + L(c.Required))}");
                        if (profDir != null)
                        {
                            Directory.CreateDirectory(profDir);
                            foreach (var run in SBM.Core.Drawing.Views.UtilityProfileViews.Runs(um))
                            {
                                var dwg = SBM.Core.Drawing.Views.UtilityProfileViews.Profile(run, p => fgp!.Query(p), u, ve);
                                string fn = Path.Combine(profDir, string.Concat(run.Name.Split(':')[0].Select(ch => Path.GetInvalidFileNameChars().Contains(ch) || ch == ' ' ? '_' : ch)) + ".dxf");
                                SBM.IO.Dxf.DxfWriter.Write(fn, dwg, 1, flat: true);
                                Console.WriteLine($"  profile   {run.Name} -> {fn}");
                            }
                        }
                        foreach (var dg in um.Diagnostics) Console.WriteLine($"  {dg.Severity}: {dg.Message}");
                    }
                    if (doc.Project.Utilities.Count == 0) Console.WriteLine("No utility networks in this model (BridgeProject.Utilities).");
                    return 0;
                }
                case "grading":
                {
                    Need(args, 2);
                    var doc = SbmDocument.Open(args[1]);
                    var u = doc.Project.Units;
                    double vol = u.IsUS ? 1 / 0.764554857984 : 1, area = u.IsUS ? 1 / 0.09290304 : 1;
                    string vu = u.IsUS ? "CY" : "m3", au = u.IsUS ? "SF" : "m2";
                    foreach (var gm in doc.Gradings())
                    {
                        var gd = gm.Definition;
                        Console.WriteLine($"Grading {gd.Name} ({(gd.Closed ? "closed" : "open")}, {gd.Points.Count} points{(gd.AlignmentName.Length > 0 ? ", on " + gd.AlignmentName : "")})");
                        if (gm.IsBuilt)
                            Console.WriteLine($"  surface {gm.SurfaceArea * area:N0} {au}   cut {(gm.CutVolume is double c ? (c * vol).ToString("N1") : "-")} {vu}   fill {(gm.FillVolume is double f ? (f * vol).ToString("N1") : "-")} {vu}");
                        foreach (var p in gm.Protection)
                            Console.WriteLine($"  {p.Name}: {p.Area * area:N0} {au}, {u.FormatLength(p.Definition.Thickness)} thick, {p.Volume * vol:N1} {vu}");
                        foreach (var dg in gm.Diagnostics) Console.WriteLine($"  {dg.Severity}: {dg.Message}");
                    }
                    if (doc.Project.Gradings.Count == 0) Console.WriteLine("No grading in this model (BridgeProject.Gradings).");
                    // OPTIONAL: --at x,y (project meters) = finished ground there and the layer it came from
                    int at = Array.IndexOf(args, "--at");
                    if (at > 0 && at + 1 < args.Length)
                    {
                        var xy = args[at + 1].Split(',').Select(s => double.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                        var (z, src) = doc.FinishedGround().Query(new SBM.Core.Geometry.Vec2(xy[0], xy[1]));
                        Console.WriteLine(z is double zz ? $"Finished ground at {xy[0]:0.###},{xy[1]:0.###}: {u.FormatLength(zz)} ({src})" : "No surface covers that point.");
                    }
                    return 0;
                }
                case "walls":
                {
                    Need(args, 2);
                    var doc = SbmDocument.Open(args[1]);
                    var u = doc.Project.Units;
                    double vol = u.IsUS ? 1 / 0.764554857984 : 1, area = u.IsUS ? 1 / 0.09290304 : 1;
                    string vu = u.IsUS ? "CY" : "m3", au = u.IsUS ? "SF" : "m2";
                    foreach (var wm in doc.Walls())
                    {
                        var wd = wm.Definition;
                        Console.WriteLine($"Wall {wd.Name} ({wd.AlignmentName} {u.FormatStation(wd.BeginStation)} - {u.FormatStation(wd.EndStation)}, offset {u.FormatLength(wd.Offset)})");
                        if (wm.IsMse)
                        {
                            foreach (var g in wm.Elements.GroupBy(e => e.Role))
                                Console.WriteLine($"  {g.Key,-15} {g.Count(),5} element(s)   {g.Sum(e => e.Volume) * vol,10:N2} {vu}" +
                                    (g.Any(e => !double.IsNaN(e.Length)) ? $"   lengths {string.Join(", ", g.Where(e => !double.IsNaN(e.Length)).Select(e => u.FormatLength(e.Length)).Distinct().Take(8))}" : ""));
                            Console.WriteLine($"  exposed face {wm.FaceArea * area:N0} {au}");
                        }
                        else
                        foreach (var p in wm.Panels)
                            Console.WriteLine($"  panel {p.Index,-3} {u.FormatStation(p.FromStation)} - {u.FormatStation(p.ToStation)}   stem {p.StemVolume * vol,10:N2} {vu}   footing {p.FootingVolume * vol,10:N2} {vu}");
                        if (!wm.IsMse && wm.Panels.Count > 0)
                            Console.WriteLine($"  total stem {wm.StemVolume * vol:N2} {vu}, footing {wm.FootingVolume * vol:N2} {vu}, exposed face above the footing top {wm.FaceArea * area:N0} {au}");
                        foreach (var dg in wm.Diagnostics) Console.WriteLine($"  {dg.Severity}: {dg.Message}");
                    }
                    if (doc.Project.Walls.Count == 0) Console.WriteLine("No retaining walls in this model (BridgeProject.Walls).");
                    return 0;
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
                    Console.WriteLine($"{files.Count} views{(SBM.IO.Dxf.SheetExport.IsZip(target) ? " â†’ " + target : "")} in {sw.ElapsedMilliseconds} ms");
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
                    Console.WriteLine($"{sheets.Count} sheets â†’ {args[2]} in {sw.ElapsedMilliseconds} ms");
                    if (Opt("--dxf") is { } dxf)
                    {
                        sw.Restart();
                        var files = SBM.IO.Dxf.SheetExport.WriteSheets(dxf, bname, sheets, doc.Project.Units, args.Contains("--models") ? doc.Drawings(bridge) : null);
                        Console.WriteLine($"{files.Count} DXF files â†’ {dxf} in {sw.ElapsedMilliseconds} ms");
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
                case "project":
                    return ProjectCommand(args);
                case "measure":
                    if (args.Length < 3) { Console.WriteLine(MeasureCommand.Help); return args.Length < 2 ? 1 : 0; }
                    return MeasureCommand.Measure(args);
                case "centerlines":
                    return CenterlinesCommand.Run(args);
                case "crossframes":
                    return CrossFramesCommand.Run(args);
                case "cable-stayed":
                    return CableStayedCommand.Run(args);
                case "review":
                    return ReviewCommand.Run(args);
                case "section":
                    if (args.Length < 3) { Console.WriteLine(MeasureCommand.Help); return args.Length < 2 ? 1 : 0; }
                    return MeasureCommand.Section(args);
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

    private static int ProjectCommand(string[] args)
    {
        Need(args, 2);
        string? Opt(string name) { int i = Array.IndexOf(args, name); return i > 0 && i + 1 < args.Length ? args[i + 1] : null; }
        var doc = SbmDocument.Open(args[1]);
        var u = doc.Units;
        double Len(string t) => u.TryParseLength(t, SBM.Core.Units.LengthKind.Long, out double v) ? v : throw new ArgumentException($"Can't read length '{t}'.");
        SBM.Core.Geometry.Vec3 V3(string t, bool lengths)
        {
            var p = t.Split(',');
            if (p.Length != 3) throw new ArgumentException($"Expected x,y,z: '{t}'.");
            double C(string c) => lengths ? Len(c.Trim()) : double.Parse(c, System.Globalization.CultureInfo.InvariantCulture);
            return new SBM.Core.Geometry.Vec3(C(p[0]), C(p[1]), C(p[2]));
        }
        List<string>? List(string name) => Opt(name)?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var src = new SBM.Core.Drawing.Projection.ProjectionSourceOptions
        {
            Bolts = args.Contains("--bolts"), Welds = args.Contains("--welds"), Rebar = args.Contains("--rebar"), Layers = List("--layers"), Kinds = List("--kinds"), Transparent = List("--transparent"),
            Girder = Opt("--girder") is { } gs ? int.Parse(gs) - 1 : null,
        };
        var o = new SBM.Core.Drawing.Projection.ProjectionOptions
        {
            Hidden = (Opt("--hidden") ?? "show").ToLowerInvariant() switch
            {
                "show" or "dashed" => SBM.Core.Drawing.Projection.HiddenLines.Show,
                "none" => SBM.Core.Drawing.Projection.HiddenLines.None,
                "off" => SBM.Core.Drawing.Projection.HiddenLines.Off,
                var h => throw new ArgumentException($"--hidden {h}: use show, dashed, none or off."),
            },
            Method = (Opt("--method") ?? "exact").StartsWith("z", StringComparison.OrdinalIgnoreCase) ? SBM.Core.Drawing.Projection.HlrMethod.DepthBuffer : SBM.Core.Drawing.Projection.HlrMethod.Exact,
            Holes = !args.Contains("--no-holes"),
            UseRecipes = !args.Contains("--no-recipes"),
        };
        if (Opt("--sharp") is { } sh) o.SharpAngleDegrees = double.Parse(sh, System.Globalization.CultureInfo.InvariantCulture);
        if (Opt("--window") is { } win)
        {
            var w = win.Split(',').Select(x => Len(x.Trim())).ToArray();
            if (w.Length != 4) throw new ArgumentException("--window x0,y0,x1,y1");
            o.Window = new SBM.Core.Geometry.Box2 { MinX = w[0], MinY = w[1], MaxX = w[2], MaxY = w[3] };
        }
        string? bridge = Opt("--bridge");
        SBM.Core.Bridge.BridgeDefinition FirstBridge() => bridge == null ? doc.Project.Bridges[0]
            : doc.Project.Bridges.FirstOrDefault(x => x.Name.Contains(bridge, StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException($"No bridge matches '{bridge}'.");
        var sw = Stopwatch.StartNew();
        doc.Regenerate();
        Console.WriteLine($"Regenerated in {sw.Elapsed.TotalSeconds:F1} s");
        double? near = Opt("--near") is { } ns ? Len(ns) : null, far = Opt("--far") is { } fs ? Len(fs) : null;
        SBM.Core.Drawing.Projection.IViewMap view;
        string title;
        if (Opt("--developed") is { } dev)
        {
            bool left = args.Contains("--from-left");
            if (dev.StartsWith("girder:", StringComparison.OrdinalIgnoreCase))
            {
                string gname = dev[7..];
                var b = FirstBridge();
                var f = doc.Framing(b.Name);
                int gi = Enumerable.Range(0, f.Girders.Count).FirstOrDefault(i => f.Girders[i].Name.Equals(gname, StringComparison.OrdinalIgnoreCase)
                    || (b.Girders.PlanNames?.ElementAtOrDefault(i)?.Equals(gname, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (int.TryParse(gname, out int k) && k - 1 == i), -1);
                if (gi < 0) throw new ArgumentException($"Girder '{gname}' not found in '{b.Name}' (use G1.., a plan name or a number).");
                var dv = SBM.Core.Drawing.Projection.DevelopedView.AlongGirder(f.Girders[gi]);
                double half = b.Girders.Section.Segments(SBM.Core.Bridge.GirderPlate.TopFlange).Concat(b.Girders.Section.Segments(SBM.Core.Bridge.GirderPlate.BottomFlange))
                    .Select(x => x.Width).DefaultIfEmpty(0.6).Max() / 2;
                view = dv.WithSlab(near ?? -(half + 0.15), far, left, $"girder {gname} of {b.Name}");
                bridge = b.Name;
                title = $"{b.Name} girder {gname} developed elevation";
            }
            else
            {
                string? an = dev.Contains(':') ? dev[(dev.IndexOf(':') + 1)..] : null;
                var al = doc.Alignment(an ?? FirstBridge().AlignmentName);
                var stas = doc.ProjectionItems(src, bridge).Where(i => i.Mesh != null).SelectMany(i => { var bb = i.Mesh!.Bounds(); return new[] { bb.Min, bb.Max }; })
                    .Select(p => al.Inverse(p.XY).Station).ToList();
                if (stas.Count == 0) throw new ArgumentException("Nothing to project.");
                view = SBM.Core.Drawing.Projection.DevelopedView.Along(al, stas.Min(), stas.Max()).WithSlab(near, far, left, al.Name);
                title = $"Developed elevation along {al.Name}";
            }
        }
        else
        {
            string vname = (Opt("--view") ?? "top").ToLowerInvariant();
            var dir = vname switch
            {
                "top" or "plan" => new SBM.Core.Geometry.Vec3(0, 0, -1),
                "front" => new SBM.Core.Geometry.Vec3(0, 1, 0),
                "left" => new SBM.Core.Geometry.Vec3(1, 0, 0),
                _ => V3(vname, false),
            };
            var up = Opt("--up") is { } us ? V3(us, false) : Math.Abs(dir.Normalized().Z) > 0.99 ? new SBM.Core.Geometry.Vec3(0, 1, 0) : new SBM.Core.Geometry.Vec3(0, 0, 1);
            SBM.Core.Geometry.Vec3 origin;
            if (Opt("--origin") is { } os) origin = V3(os, true);
            else
            {
                var bb = SBM.Core.Solids.Box3.Empty;
                foreach (var it in doc.ProjectionItems(src, bridge)) if (it.Mesh != null) { var b = it.Mesh.Bounds(); bb.Add(b.Min); bb.Add(b.Max); }
                origin = bb.IsEmpty ? SBM.Core.Geometry.Vec3.Zero : (bb.Min + bb.Max) * 0.5;
            }
            view = new SBM.Core.Drawing.Projection.OrthographicView(origin, dir, up) { Near = near, Far = far };
            title = $"{bridge ?? doc.Project.Name} - {vname} view";
        }
        title = Opt("--title") ?? title;
        sw.Restart();
        var r = doc.ProjectView(view, o, src, bridge);
        Console.WriteLine($"Projected ({view.Describe}): {r.Stats}");
        Console.WriteLine($"  visible {u.FormatLength(r.Stats.VisibleLength)}, hidden {u.FormatLength(r.Stats.HiddenLength)} of lines; {r.Elements.Count} elements; layers: " +
            string.Join(", ", r.Drawing.Layers.Where(l => l.Polylines.Count > 0).Select(l => $"{l.Name} ({l.Polylines.Count})")));
        if (Opt("--dxf") is { } dxf) { SBM.IO.Dxf.DxfWriter.Write(dxf, r.Drawing, u.LongUnit, flat: true); Console.WriteLine($"  -> {dxf}"); }
        if (Opt("--svg") is { } svg) { SBM.IO.Svg.SvgWriter.Write(svg, r.Drawing); Console.WriteLine($"  -> {svg}"); }
        if (Opt("--pdf") is { } pdf)
        {
            var fmt = new SBM.Core.Drawing.Sheets.SheetFormat(17, 11, doc.Project.Name, bridge ?? doc.Project.Name, DateTime.Now.ToString("MM/dd/yyyy"));
            var (ax0, ay0, ax1, ay1) = fmt.Area;
            string t2 = title;
            var spec = new SBM.Core.Drawing.Sheets.SheetSpec(title, view.Describe, new[] { new SBM.Core.Drawing.Sheets.Panel(new SBM.Core.Drawing.Sheets.ViewSpec(title,
                sc => SBM.Core.Drawing.Views.ProjectedViews.View(r, t2, t2.ToUpperInvariant(), "Projected from the 3D model, hidden lines dashed", sc, u),
                o.Window != null ? SBM.Core.Drawing.Sheets.ScaleFamily.Architectural : SBM.Core.Drawing.Sheets.ScaleFamily.Engineering), 0, 0, ax1 - ax0, ay1 - ay0) });
            var sheets = SBM.Core.Drawing.Sheets.SheetComposer.Number(new List<SBM.Core.Drawing.Sheets.Sheet> { SBM.Core.Drawing.Sheets.SheetComposer.Compose(spec, fmt, u) });
            SBM.IO.Pdf.PdfWriter.Write(pdf, sheets, title);
            Console.WriteLine($"  -> {pdf} ({string.Join("; ", sheets[0].ScaleNotes)})");
        }
        return 0;
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
