using System.Globalization;
using SBM.Api;
using SBM.IO.Database;

namespace SBM.Cli;

/// <summary><c>sbm asset</c>: asset management records in the companion database (G9) - photos, documents, inspections, findings, calcs.</summary>
public static partial class Program
{
    private const string AssetUsage = """
        sbm asset <file.sbm> [--db <file.sqlite>] <command> ...   (the database must exist: sbm db <file.sbm>)
          status [--verify]                       counts, assets folder, orphaned records, missing / changed files (--verify = SHA-256)
          photo add <file>... [--element REF]... [--caption T] [--taken DATE] [--author A] [--inspection ID] [--finding ID]
          photo list [--element REF]              doc add / doc list: the same for documents (reports, drawings, letters)
          link <fileId> [--element REF]... [--inspection ID] [--finding ID] [--unlink]
          inspection add --date D [--kind routine] [--structure NO] [--inspector N] [--agency A] [--note T]
          inspection list
          inspection import <file.csv> [--kind routine]     element-level (CS1-CS4 + defects) or NBI/SNBI ratings CSV (layout in docs/API.md)
          inspection rate <inspectionId> <component> <0-9|N> [--element REF] [--note T]
          inspection export <out.csv>             history: out.csv (element-level), out_ratings.csv, out_findings.csv
          finding add --element REF --desc T [--defect T] [--severity low|medium|high|critical] [--location T] [--point E,N,Z]
                      [--station STA --offset LEN] [--action T] [--status open] [--inspection ID] [--date D] [--due D] [--photo FILE]...
          finding list [--element REF] [--open]
          finding set <id> [--status open|monitor|scheduled|repaired|closed] [--note T] [--action T] [--due D]
          calc add <file.pdf> --summary 1-2 [--title T] [--rev R] [--date D] [--engineer E] [--element REF]...
          calc list | calc show <id>
          calc values <calcId> <values.json>      extracted values (schema sbm-calc-values/1, docs/API.md); replaces the previous extraction
          calc diff [<calcId>] [--json] [--csv out.csv]     calc values vs the model (reads only)
          calc apply <calcId> --ids 3,4 | --all   set the DIFFERING input values, log each change (who, when, old -> new, calc page), save the .sbm
          calc log [<calcId>]                     changes applied from calcs
        REF = GlobalId, mark or name of a model element. Files are copied to <model>_assets\ (photos, documents, calcs, thumbnails) beside
        the database; the database keeps relative references + SHA-256. Common option: --by NAME (default: the Windows user).
        """;

    private static int AssetCommand(string[] args)
    {
        // args: asset <file.sbm> [--db x] <command> [sub] ...
        if (args.Length < 3 || args[2] is "help" or "--help") { Console.WriteLine(AssetUsage); return args.Length < 3 ? 1 : 0; }
        string model = args[1];
        var rest = args.Skip(2).ToList();
        string? dbOpt = null;
        int di = rest.IndexOf("--db");
        if (di >= 0 && di + 1 < rest.Count) { dbOpt = rest[di + 1]; rest.RemoveRange(di, 2); }
        string dbFile = dbOpt ?? CompanionDatabase.DefaultPath(model);
        if (!File.Exists(dbFile)) { Console.Error.WriteLine($"{dbFile} does not exist: build it first (sbm db {model})."); return 1; }

        string? Opt(string name) { int i = rest.IndexOf(name); return i >= 0 && i + 1 < rest.Count ? rest[i + 1] : null; }
        List<string> Opts(string name) { var l = new List<string>(); for (int i = 0; i < rest.Count - 1; i++) if (rest[i] == name) l.Add(rest[i + 1]); return l; }
        bool Flag(string name) => rest.Contains(name);
        var valued = new HashSet<string> { "--element", "--caption", "--taken", "--author", "--inspection", "--finding", "--date", "--kind", "--structure", "--inspector",
            "--agency", "--note", "--desc", "--defect", "--severity", "--location", "--point", "--station", "--offset", "--action", "--status", "--due", "--photo",
            "--summary", "--title", "--rev", "--engineer", "--ids", "--csv", "--by" };
        List<string> Pos()
        {
            var l = new List<string>();
            for (int i = 0; i < rest.Count; i++)
            {
                if (rest[i].StartsWith("--")) { if (valued.Contains(rest[i])) i++; continue; }
                l.Add(rest[i]);
            }
            return l;
        }
        var pos = Pos();
        string cmd = pos[0].ToLowerInvariant(), sub = pos.Count > 1 ? pos[1].ToLowerInvariant() : "";
        string? by = Opt("--by");
        using var db = new ModelDb(dbFile);
        SbmDocument? doc = null;
        SbmDocument Doc() => doc ??= SbmDocument.Open(model);
        List<string> Elements() => Opts("--element").Select(db.FindElement).ToList();
        long? LongOpt(string n) => Opt(n) is { } s ? long.Parse(s, CultureInfo.InvariantCulture) : null;
        string Lbl(string gid) => gid.Length == 0 ? "(structure)" : db.Get(gid) is { } e ? (e.Name.Length > 0 ? e.Name : e.Category) : gid + " (not in the model)";

        void PrintFiles(List<AssetFile> files)
        {
            foreach (var f in files)
            {
                var loc = db.ResolveAsset(f);
                var st = AssetStore.Check(loc, f.SizeBytes, f.Sha256, false);
                Console.WriteLine($"#{f.Id} {f.Kind} {f.FileName} ({(f.SizeBytes ?? 0) / 1024.0:N0} KB{(f.TakenAt.Length > 0 ? ", taken " + f.TakenAt : "")}) {(f.Caption.Length > 0 ? "\"" + f.Caption + "\" " : "")}"
                                  + $"-> {f.Location}{(st is AssetFileState.Ok or AssetFileState.Url ? "" : " [" + st.ToString().ToUpperInvariant() + "]")}");
                foreach (var l in f.Links) Console.WriteLine("    linked to " + (l.StartsWith("element:") ? "element " + Lbl(l[8..]) : l));
            }
            if (files.Count == 0) Console.WriteLine("(none)");
        }

        switch (cmd)
        {
            case "status":
            {
                Console.WriteLine($"Database {db.Path} (asset schema {db.Meta("asset_schema_version")}), assets folder {db.AssetsFolder}");
                foreach (var (k, n) in db.AssetCounts()) Console.WriteLine($"  {k,-18} {n,8:N0}");
                var orph = db.Orphans();
                Console.WriteLine(orph.Count == 0 ? "  orphaned records: none" : $"  orphaned records (element no longer in the model; kept): {orph.Count}");
                foreach (var o in orph.Take(50)) Console.WriteLine($"    {o.Source} {o.Record}: {o.GlobalId}");
                var probs = db.CheckFiles(Flag("--verify"));
                Console.WriteLine(probs.Count == 0 ? $"  files: all present{(Flag("--verify") ? " and unchanged (SHA-256)" : " (size checked; --verify checks SHA-256)")}" : $"  files missing / changed: {probs.Count}");
                foreach (var p in probs) Console.WriteLine($"    #{p.File.Id} {p.File.FileName}: {p.State} ({p.Path})");
                return 0;
            }
            case "photo":
            case "doc":
            case "document":
            {
                string kind = cmd == "photo" ? "photo" : "document";
                if (sub == "list")
                {
                    PrintFiles(Opt("--element") is { } er ? db.FilesForElement(db.FindElement(er), kind) : db.Files(kind));
                    return 0;
                }
                if (sub != "add" || pos.Count < 3) throw new ArgumentException($"usage: sbm asset <model> {cmd} add <file>... [--element REF]");
                var els = Elements();
                long? insp = LongOpt("--inspection"), fnd = LongOpt("--finding");
                if (insp is { } iid && db.GetInspection(iid) == null) throw new ArgumentException($"No inspection {iid}.");
                if (fnd is { } fid && db.GetFinding(fid) == null) throw new ArgumentException($"No finding {fid}.");
                foreach (var file in pos.Skip(2))
                {
                    var f = db.AddFile(kind, file, new ModelDb.AddFileOptions
                    {
                        Caption = Opt("--caption"), Author = Opt("--author"), TakenAt = Opt("--taken") is { } t ? ModelDb.NormDate(t) : null, By = by,
                        Log = m => Console.WriteLine("  " + m),
                    });
                    foreach (var g in els) db.Link(f.Id, "element", g);
                    if (insp != null) db.Link(f.Id, "inspection", insp.Value.ToString(CultureInfo.InvariantCulture));
                    if (fnd != null) db.Link(f.Id, "finding", fnd.Value.ToString(CultureInfo.InvariantCulture));
                    Console.WriteLine($"Added {kind} #{f.Id} {f.FileName} -> {f.Location}{(f.ThumbLocation.Length > 0 ? ", thumbnail " + f.ThumbLocation : "")}"
                                      + (els.Count > 0 ? ", linked to " + string.Join(", ", els.Select(Lbl)) : ""));
                }
                return 0;
            }
            case "link":
            {
                if (pos.Count < 2) throw new ArgumentException("usage: sbm asset <model> link <fileId> --element REF");
                long id = long.Parse(pos[1], CultureInfo.InvariantCulture);
                if (db.GetFile(id) == null) throw new ArgumentException($"No file #{id}.");
                var targets = Elements().Select(g => ("element", g)).ToList();
                if (Opt("--inspection") is { } i) targets.Add(("inspection", i));
                if (Opt("--finding") is { } f) targets.Add(("finding", f));
                foreach (var (k, t) in targets)
                {
                    if (Flag("--unlink")) db.Unlink(id, k, t); else db.Link(id, k, t);
                    Console.WriteLine($"{(Flag("--unlink") ? "Unlinked" : "Linked")} #{id} {(k == "element" ? "element " + Lbl(t) : k + " " + t)}");
                }
                return 0;
            }
            case "inspection":
            {
                switch (sub)
                {
                    case "add":
                    {
                        var ins = db.AddInspection(Opt("--date") ?? throw new ArgumentException("--date is required"), Opt("--kind") ?? "routine", Opt("--structure") ?? "",
                            Opt("--inspector"), null, Opt("--agency"), null, Opt("--note"), by);
                        Console.WriteLine($"Inspection #{ins.Id} {ins.Label}");
                        return 0;
                    }
                    case "list":
                    {
                        foreach (var i in db.Inspections())
                        {
                            int rows = db.Condition(i.Id).Count, rt = db.Ratings().Count(r => r.InspectionId == i.Id);
                            Console.WriteLine($"#{i.Id} {i.Label}{(i.Inspector.Length > 0 ? ", " + i.Inspector : "")}: {rows} element-level rows, {rt} ratings");
                        }
                        return 0;
                    }
                    case "import":
                    {
                        if (pos.Count < 3) throw new ArgumentException("usage: sbm asset <model> inspection import <file.csv>");
                        var r = InspectionCsv.Import(db, pos[2], Opt("--kind"), by);
                        Console.WriteLine($"Imported {r.Rows} {r.Layout} row(s) into {r.Inspections.Count} inspection(s): {string.Join("; ", r.Inspections.Select(i => $"#{i.Id} {i.Label}"))}"
                                          + (r.Unmapped > 0 ? $"; {r.Unmapped} row(s) not mapped to a model element (kept)" : ""));
                        foreach (var w in r.Warnings) Console.WriteLine("  warning: " + w);
                        return 0;
                    }
                    case "rate":
                    {
                        if (pos.Count < 5) throw new ArgumentException("usage: sbm asset <model> inspection rate <inspectionId> <component> <0-9|N>");
                        long id = long.Parse(pos[2], CultureInfo.InvariantCulture);
                        if (db.GetInspection(id) == null) throw new ArgumentException($"No inspection {id}.");
                        string g = Opt("--element") is { } er ? db.FindElement(er) : "";
                        db.SetRating(id, pos[3], pos[4], g, Opt("--note"));
                        Console.WriteLine($"Inspection #{id}: {pos[3]} = {pos[4].ToUpperInvariant()}{(g.Length > 0 ? " (" + Lbl(g) + ")" : "")}");
                        return 0;
                    }
                    case "export":
                    {
                        if (pos.Count < 3) throw new ArgumentException("usage: sbm asset <model> inspection export <out.csv>");
                        foreach (var f in InspectionCsv.ExportHistory(db, pos[2])) Console.WriteLine("  -> " + f);
                        return 0;
                    }
                }
                break;
            }
            case "finding":
            {
                switch (sub)
                {
                    case "add":
                    {
                        string g = db.FindElement(Opt("--element") ?? throw new ArgumentException("--element is required"));
                        double? x = null, y = null, z = null, sta = null, off = null;
                        if (Opt("--point") is { } pt)
                        {
                            var p = pt.Split(',').Select(s => double.Parse(s, CultureInfo.InvariantCulture)).ToArray();
                            if (p.Length != 3) throw new ArgumentException("--point E,N,Z (meters, project coordinates)");
                            (x, y, z) = (p[0], p[1], p[2]);
                        }
                        if (Opt("--station") is { } st) sta = Doc().Station(st);
                        if (Opt("--offset") is { } of) off = Doc().Length(of);
                        var f = db.AddFinding(new Finding
                        {
                            GlobalId = g, InspectionId = LongOpt("--inspection"), FoundAt = Opt("--date") ?? "", Description = Opt("--desc") ?? "",
                            DefectType = Opt("--defect") ?? "", Severity = (Opt("--severity") ?? "medium").ToLowerInvariant(), LocationText = Opt("--location") ?? "",
                            X = x, Y = y, Z = z, Station = sta, Offset = off, RecommendedAction = Opt("--action") ?? "", Status = (Opt("--status") ?? "open").ToLowerInvariant(),
                            DueDate = Opt("--due") is { } due ? ModelDb.NormDate(due) : "", Note = Opt("--note") ?? "",
                        }, by);
                        foreach (var ph in Opts("--photo"))
                        {
                            var pf = db.AddFile("photo", ph, new ModelDb.AddFileOptions { By = by, Caption = f.Description, Log = m => Console.WriteLine("  " + m) });
                            db.Link(pf.Id, "finding", f.Id.ToString(CultureInfo.InvariantCulture));
                            db.Link(pf.Id, "element", g);
                        }
                        Console.WriteLine($"Finding #{f.Id} on {Lbl(g)}: {f.Description} [{f.Severity}, {f.Status}]");
                        return 0;
                    }
                    case "list":
                    {
                        string? g = Opt("--element") is { } er ? db.FindElement(er) : null;
                        var list = db.Findings(g, Flag("--open"), withDescendants: g != null);
                        foreach (var f in list)
                            Console.WriteLine($"#{f.Id} {f.FoundAt} {Lbl(f.GlobalId)}: {f.Description} [{f.Severity}, {f.Status}]"
                                              + (f.DefectType.Length > 0 ? $" defect {f.DefectType}" : "") + (f.LocationText.Length > 0 ? $" at {f.LocationText}" : "")
                                              + (f.RecommendedAction.Length > 0 ? $"; action: {f.RecommendedAction}" : "") + (f.DueDate.Length > 0 ? $" (due {f.DueDate})" : ""));
                        if (list.Count == 0) Console.WriteLine("(none)");
                        return 0;
                    }
                    case "set":
                    {
                        if (pos.Count < 3) throw new ArgumentException("usage: sbm asset <model> finding set <id> --status S");
                        long id = long.Parse(pos[2], CultureInfo.InvariantCulture);
                        db.UpdateFinding(id, Opt("--status")?.ToLowerInvariant(), Opt("--note"), Opt("--action"), Opt("--due") is { } due ? ModelDb.NormDate(due) : null, by);
                        var f = db.GetFinding(id)!;
                        Console.WriteLine($"Finding #{id}: {f.Status}{(f.ClosedAt.Length > 0 ? " (closed " + f.ClosedAt + ")" : "")}");
                        return 0;
                    }
                }
                break;
            }
            case "calc":
                return CalcCommand(db, model, sub, pos, Opt, Flag, Elements, by, Doc);
        }
        Console.Error.WriteLine($"Unknown asset command '{string.Join(" ", pos.Take(2))}'.\n");
        Console.WriteLine(AssetUsage);
        return 1;
    }

    private static int CalcCommand(ModelDb db, string model, string sub, List<string> pos, Func<string, string?> Opt, Func<string, bool> Flag,
        Func<List<string>> Elements, string? by, Func<SbmDocument> Doc)
    {
        long Id(int i) => pos.Count > i ? long.Parse(pos[i], CultureInfo.InvariantCulture) : throw new ArgumentException("calc id missing (sbm asset <model> calc list).");
        switch (sub)
        {
            case "add":
            {
                if (pos.Count < 3) throw new ArgumentException("usage: sbm asset <model> calc add <file.pdf> --summary 1-2");
                var c = db.AddCalc(pos[2], Opt("--summary") ?? throw new ArgumentException("--summary <pages> is required (e.g. 1-2): the calc's summary pages."),
                    Opt("--title"), Opt("--rev"), Opt("--date"), Opt("--engineer"), Elements(), by, m => Console.WriteLine("  " + m));
                Console.WriteLine($"Calc #{c.Id} \"{c.Title}\": {c.PageCount?.ToString(CultureInfo.InvariantCulture) ?? "?"} pages, summary pages {c.SummaryPages}, "
                                  + $"{c.SummaryText.Length:N0} characters of summary text{(c.SummaryText.Length == 0 ? " (python + PyMuPDF needed to read it)" : "")}");
                Console.WriteLine($"  Next: extract the values of the summary pages to a JSON file (schema {CalcSync.Schema}, docs/API.md) and run: sbm asset \"{model}\" calc values {c.Id} values.json");
                return 0;
            }
            case "list":
            {
                foreach (var c in db.Calcs())
                {
                    var f = db.GetFile(c.FileId);
                    Console.WriteLine($"#{c.Id} \"{c.Title}\"{(c.Revision.Length > 0 ? " rev " + c.Revision : "")}{(c.CalcDate.Length > 0 ? " " + c.CalcDate : "")}: {f?.FileName} "
                                      + $"({c.PageCount} pages, summary {c.SummaryPages}), {db.CalcValues(c.Id).Count} values, {db.AppliedLog(c.Id).Count} applied");
                }
                return 0;
            }
            case "show":
            {
                var c = db.GetCalc(Id(2)) ?? throw new ArgumentException("No such calc.");
                var f = db.GetFile(c.FileId);
                Console.WriteLine($"Calc #{c.Id} \"{c.Title}\" {f?.FileName} -> {f?.Location}; summary pages {c.SummaryPages}");
                Console.WriteLine(c.SummaryText.Length > 0 ? c.SummaryText : "(no summary text)");
                foreach (var v in db.CalcValues(c.Id))
                    Console.WriteLine($"  [{v.Id}] {v.Name}: {v.ValueText} -> {v.TargetText} (p.{v.Page}) {(v.Quote.Length > 0 ? "\"" + v.Quote + "\"" : "")}");
                return 0;
            }
            case "values":
            {
                if (pos.Count < 4) throw new ArgumentException("usage: sbm asset <model> calc values <calcId> <values.json>");
                int n = CalcSync.ImportValues(db, Id(2), pos[3]);
                Console.WriteLine($"Calc #{Id(2)}: {n} value(s) stored. Compare with the model: sbm asset \"{model}\" calc diff {Id(2)}");
                return 0;
            }
            case "diff":
            {
                long? id = pos.Count > 2 ? Id(2) : null;
                var rows = CalcSync.Diff(Doc(), db, id);
                if (Flag("--json"))
                {
                    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(rows.Select(r => new
                    {
                        id = r.Value.Id, calc = r.Value.CalcId, calcTitle = r.CalcTitle, name = r.Value.Name, target = r.Target, kind = r.Kind, status = r.Status,
                        model = r.ModelText, calcValue = r.CalcText, page = r.Value.Page, quote = r.Value.Quote, message = r.Message, applicable = r.Applicable,
                    }), new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
                    return 0;
                }
                foreach (var r in rows)
                    Console.WriteLine($"[{r.Value.Id}] {r.Status.ToUpperInvariant(),-8} {r.Value.Name}: model {(r.ModelText.Length > 0 ? r.ModelText : "-")} | calc {r.CalcText}  ({r.Kind} {r.Target}, calc #{r.Value.CalcId} p.{r.Value.Page})"
                                      + (r.Message.Length > 0 ? " - " + r.Message : ""));
                int d = rows.Count(r => r.Status == "differs"), ap = rows.Count(r => r.Applicable);
                Console.WriteLine($"{rows.Count} value(s): {rows.Count(r => r.Status == "match")} match, {d} differ ({ap} applicable input(s)), {rows.Count(r => r.Status is "missing" or "error")} missing / unreadable."
                                  + (ap > 0 ? $" Apply with: sbm asset \"{model}\" calc apply {(id?.ToString(CultureInfo.InvariantCulture) ?? "<calcId>")} --ids {string.Join(",", rows.Where(r => r.Applicable).Select(r => r.Value.Id))}" : ""));
                if (Opt("--csv") is { } csv)
                {
                    var lines = new List<string> { InspectionCsv.Line(new[] { "id", "calc_id", "calc_title", "name", "kind", "target", "status", "model", "calc", "page", "quote", "message" }) };
                    lines.AddRange(rows.Select(r => InspectionCsv.Line(new[] { r.Value.Id.ToString(CultureInfo.InvariantCulture), r.Value.CalcId.ToString(CultureInfo.InvariantCulture), r.CalcTitle,
                        r.Value.Name, r.Kind, r.Target, r.Status, r.ModelText, r.CalcText, r.Value.Page?.ToString(CultureInfo.InvariantCulture), r.Value.Quote, r.Message })));
                    File.WriteAllLines(csv, lines, new System.Text.UTF8Encoding(true));
                    Console.WriteLine("  -> " + csv);
                }
                return 0;
            }
            case "apply":
            {
                long cid = Id(2);
                if (db.GetCalc(cid) == null) throw new ArgumentException($"No calc {cid}.");
                var doc = Doc();
                List<long> ids;
                if (Opt("--ids") is { } s) ids = CalcSync.ParseIds(s);
                else if (Flag("--all")) ids = CalcSync.Diff(doc, db, cid).Where(r => r.Applicable).Select(r => r.Value.Id).ToList();
                else throw new ArgumentException("Name the values to apply: --ids 3,4 (see calc diff), or --all for every differing input of the calc.");
                var own = db.CalcValues(cid).Select(v => v.Id).ToHashSet();
                foreach (var i in ids.Where(i => !own.Contains(i))) throw new ArgumentException($"Value {i} does not belong to calc {cid}.");
                var res = CalcSync.Apply(doc, db, ids, by);
                foreach (var (row, o, n) in res.Applied) Console.WriteLine($"  applied [{row.Value.Id}] {row.Value.PropertyPath}: {o} -> {n} (calc #{cid} p.{row.Value.Page})");
                foreach (var (v, why) in res.Skipped) Console.WriteLine($"  skipped [{v.Id}] {v.Name}: {why}");
                Console.WriteLine(res.Applied.Count == 0 ? "Nothing applied." : $"{res.Applied.Count} change(s) applied and logged; saved {res.SavedTo}. Rebuild the database to refresh results (sbm db).");
                return 0;
            }
            case "log":
            {
                long? id = pos.Count > 2 ? Id(2) : null;
                var log = db.AppliedLog(id);
                foreach (var a in log) Console.WriteLine($"#{a.Id} {a.AppliedAt} {a.AppliedBy}: {a.PropertyPath} {a.OldValue} -> {a.NewValue} (calc #{a.CalcId} \"{a.CalcTitle}\" p.{a.Page})");
                if (log.Count == 0) Console.WriteLine("(no changes applied from calcs)");
                return 0;
            }
        }
        Console.Error.WriteLine($"Unknown calc command '{sub}'.\n");
        Console.WriteLine(AssetUsage);
        return 1;
    }
}
