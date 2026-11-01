using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SBM.Core.Alignments;
using SBM.Core.Bridge;
using SBM.Core.Project;
using SBM.IO.Database;

namespace SBM.Api;

/// <summary>What an agent review flag is about.</summary>
public enum ReviewFlagKind { PlanPdf, SheetLinks, ProgramDefault, GeneratorFlag, ElementReview }

/// <summary>Open = must be resolved (or recorded as the user's decision) before the model is finished; Minor = listed with <c>--all</c>
/// (zero / off / empty starting values, generator warnings); Note = information (first record of a PDF, confirmed defaults).</summary>
public enum ReviewLevel { Open, Minor, Note }

public sealed record ReviewFlag(ReviewFlagKind Kind, ReviewLevel Level, string Bridge, string Subject, string Path, string Value, string Reason, string Next = "", int Places = 1)
{
    public string KindText => Kind switch
    {
        ReviewFlagKind.PlanPdf => "plan PDF", ReviewFlagKind.SheetLinks => "sheet links", ReviewFlagKind.ProgramDefault => "program default", ReviewFlagKind.ElementReview => "element review", _ => "generator flag",
    };
}

public sealed class AgentReviewOptions
{
    /// <summary>Regenerate the model and carry its UNRESOLVED / CONFLICT warnings and errors as flags.</summary>
    public bool Regenerate { get; set; } = true;
    /// <summary>Read the companion database (unused sheets, elements without sheets, sheets per flagged input).</summary>
    public bool UseDatabase { get; set; } = true;
    /// <summary>Write the record file (PDF fingerprints on first sight) and the database table <c>agent_review</c>.</summary>
    public bool Record { get; set; } = true;
    /// <summary>Attached PDFs (file names or paths) whose CHANGED fingerprint is accepted as the new version.</summary>
    public List<string> AcceptPdfs { get; set; } = new();
}

/// <summary>Plan PDF fingerprint and user-confirmed starting values of a model: <c>&lt;model&gt;.agent-review.json</c> beside the .sbm (source, commit it).</summary>
public sealed class AgentReviewRecord
{
    public List<ReviewPdfRecord> Pdfs { get; set; } = new();
    public List<ReviewConfirmation> Confirmed { get; set; } = new();
    /// <summary>Element agent reviews (an agent compared the element with its plan sheets): one per element, the latest wins.</summary>
    public List<ElementReviewRecord> Elements { get; set; } = new();

    public static string PathFor(string modelFile) => System.IO.Path.ChangeExtension(modelFile, ".agent-review.json");

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static AgentReviewRecord Load(string path) =>
        File.Exists(path) ? JsonSerializer.Deserialize<AgentReviewRecord>(File.ReadAllText(path), Json) ?? new() : new();

    public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, Json));
}

public sealed class ReviewPdfRecord
{
    public string File { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public long Bytes { get; set; }
    public string RecordedAt { get; set; } = "";
}

/// <summary>A starting value the user confirmed is correct for this model. It stays confirmed while the value is unchanged.</summary>
public sealed class ReviewConfirmation
{
    public string Path { get; set; } = "";
    public string Value { get; set; } = "";
    public string Note { get; set; } = "";
    public string At { get; set; } = "";
}

/// <summary>
/// The result of an agent comparing ONE element (or a container and everything in it) with the plan sheets it was modeled from. It holds while the
/// element's <see cref="Fingerprint"/> and the fingerprints of its <see cref="Neighbours"/> (elements in contact, from the database boxes) are unchanged:
/// a change of the element makes it stale, a change of a neighbour flags the interface for a new review. AI-only: there is no human sign-off state.
/// </summary>
public sealed class ElementReviewRecord
{
    public string GlobalId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Level { get; set; } = "";
    public string Category { get; set; } = "";
    public string Bridge { get; set; } = "";
    /// <summary>"agrees" = the element matches its sheets (flagged items aside, they stay generator flags); "differs" = a difference was found and
    /// reported (<see cref="Note"/>).</summary>
    public string Result { get; set; } = "agrees";
    public List<string> Sheets { get; set; } = new();
    public string Note { get; set; } = "";
    /// <summary>Path of the agent's write-up (repo-relative), e.g. qc/bonner-unit5/segments/end-segment.md.</summary>
    public string Report { get; set; } = "";
    public string By { get; set; } = "agent";
    public string At { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    /// <summary>Hash of the neighbours' GlobalIds + fingerprints (<see cref="ModelDb.NeighbourHash"/>) and their count per category when recorded.</summary>
    public string NeighbourHash { get; set; } = "";
    public Dictionary<string, int> Neighbours { get; set; } = new();
}

/// <summary>A request to record element agent reviews (<c>sbm review --record-elements FILE</c>): which elements, the result, sheets and write-up.</summary>
public sealed class ElementReviewRequest
{
    public List<ElementReviewRequestItem> Reviews { get; set; } = new();
}

public sealed class ElementReviewRequestItem
{
    /// <summary>Explicit GlobalIds; or a selector (all given parts must match): Bridge (exact), Level (exact), Category (exact), Name (regex).</summary>
    public List<string> Ids { get; set; } = new();
    public string? Bridge { get; set; }
    public string? Level { get; set; }
    public string? Category { get; set; }
    public string? Name { get; set; }
    public string Result { get; set; } = "agrees";
    public List<string> Sheets { get; set; } = new();
    public string Note { get; set; } = "";
    public string Report { get; set; } = "";
}

public sealed class AgentReviewReport
{
    public string Model { get; init; } = "";
    public List<ReviewFlag> Flags { get; } = new();
    public List<string> Notes { get; } = new();
    public int Open => Flags.Count(f => f.Level == ReviewLevel.Open);
    public int Minor => Flags.Count(f => f.Level == ReviewLevel.Minor);
    public int Confirmed { get; set; }

    public string Summary =>
        $"{Open} open flag(s) ({string.Join(", ", Flags.Where(f => f.Level == ReviewLevel.Open).GroupBy(f => f.KindText).Select(g => $"{g.Count()} {g.Key}"))})" +
        $", {Minor} minor, {Confirmed} confirmed default(s)";

    public string ToText(bool all = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Agent review: {Model}");
        foreach (var n in Notes) sb.AppendLine("  note: " + n);
        foreach (var kind in Enum.GetValues<ReviewFlagKind>())
        {
            var fl = Flags.Where(f => f.Kind == kind && (all ? f.Level != ReviewLevel.Note : f.Level == ReviewLevel.Open)).ToList();
            if (fl.Count == 0) continue;
            sb.AppendLine();
            sb.AppendLine($"== {fl[0].KindText} ({fl.Count})");
            if (kind == ReviewFlagKind.ProgramDefault)
            {
                sb.AppendLine("   Still the program's starting value: confirm it from the plans and set it (or record it with --confirm PATH).");
                foreach (var g in fl.GroupBy(f => f.Subject))
                {
                    sb.AppendLine($"  {g.Key}");
                    var next = g.Select(f => f.Next).FirstOrDefault(n => n.Length > 0);
                    if (next != null) sb.AppendLine($"      read on: {next}");
                    foreach (var f in g) sb.AppendLine($"    {(f.Level == ReviewLevel.Minor ? "minor " : "OPEN  ")}{f.Path.Substring(Math.Min(f.Path.Length, g.Key.Length)).TrimStart('.')} = {f.Value}{(f.Places > 1 ? $"  (in {f.Places:N0} places)" : "")}");
                }
            }
            else
                foreach (var g in fl.GroupBy(f => (f.Reason, f.Next)))
                {
                    foreach (var f in g)
                        sb.AppendLine($"  {(f.Level == ReviewLevel.Minor ? "minor " : "OPEN  ")}{(f.Bridge.Length > 0 ? f.Bridge + ": " : "")}{f.Subject}" + (f.Value.Length > 0 ? $" [{f.Value}]" : ""));
                    if (g.Key.Reason.Length > 0) sb.AppendLine("        " + g.Key.Reason);
                    if (g.Key.Next.Length > 0) sb.AppendLine("        next: " + g.Key.Next);
                }
        }
        sb.AppendLine();
        sb.Append("Agent review: " + Summary + (all || Minor == 0 ? "." : " (--all lists the minor ones)."));
        return sb.ToString();
    }
}

/// <summary>
/// AGENT REVIEW of a model built from plan PDFs (docs/AGENT-REVIEW.md): (1) the right PDFs are attached and unchanged since they were recorded, every
/// sheet is used by an element and every element has a sheet it was modeled from; (2) every input still at the program's starting value is flagged as
/// not reviewed yet (non-zero starting values = open, zero / off / empty = minor) unless the user confirmed it; (3) the generators' UNRESOLVED /
/// CONFLICT warnings. Deterministic; the per-element comparison of the PDF with the model geometry is done by an agent on top of this.
/// </summary>
public static class AgentReview
{
    private static readonly string[] ElementLevels = { "group", "assembly", "element", "part", "bar" };

    public static AgentReviewReport Run(SbmDocument doc, AgentReviewOptions? o = null)
    {
        o ??= new AgentReviewOptions();
        string? model = doc.FilePath;
        var rep = new AgentReviewReport { Model = model ?? doc.Project.Name };
        string? recordPath = model == null ? null : AgentReviewRecord.PathFor(model);
        var record = recordPath == null ? new AgentReviewRecord() : AgentReviewRecord.Load(recordPath);
        bool recordChanged = false;

        string? db = model == null ? null : CompanionDatabase.DefaultPath(model);
        ModelDb? mdb = null;
        try
        {
            if (o.UseDatabase && db != null && File.Exists(db)) mdb = new ModelDb(db);
            recordChanged |= Pdfs(doc, model, record, o, rep, mdb);
            SheetLinks(doc, model, db, mdb, rep);
            Defaults(doc, record, mdb, rep);
            if (o.Regenerate) Generator(doc, rep);
            if (mdb != null) ElementReviews(record, mdb, rep, o.Record);
            if (o.Record && mdb != null)
                mdb.WriteAgentReview(rep.Flags.Select(f => new AgentReviewRow(f.KindText, f.Level.ToString().ToLowerInvariant(), f.Bridge, f.Subject, f.Path, f.Value, f.Reason, f.Next)),
                    DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
        }
        finally { mdb?.Dispose(); }

        if (o.Record && recordChanged && recordPath != null) record.Save(recordPath);
        return rep;
    }

    // ------------------------------------------------------------------ (4) element agent reviews

    /// <summary>Levels counted for the coverage flags (an element is covered when it or a container above it, below the bridge, has a review).</summary>
    private static readonly HashSet<string> CoveredLevels = new(StringComparer.Ordinal) { "element", "part", "bar" };
    private static readonly HashSet<string> AboveReview = new(StringComparer.Ordinal) { "project", "site", "alignment", "bridge group", "unit" };

    /// <summary>State of every recorded element review vs the database (current / changed / interface / missing), rows of
    /// <c>element_agent_review</c>, and one coverage flag per bridge / level / category with elements no review covers.</summary>
    private static void ElementReviews(AgentReviewRecord record, ModelDb mdb, AgentReviewReport rep, bool write)
    {
        var all = mdb.ReviewElements();
        var rows = new List<ElementReviewRow>();
        var neighboursNow = ModelDb.Neighbours(all, record.Elements.Select(r => r.GlobalId));
        int current = 0;
        foreach (var r in record.Elements)
        {
            string state, changed = "";
            if (!all.TryGetValue(r.GlobalId, out var e)) { state = "missing"; }
            else if (e.Fingerprint != r.Fingerprint) state = "changed";
            else
            {
                var now = neighboursNow.GetValueOrDefault(r.GlobalId) ?? new List<string>();
                state = ModelDb.NeighbourHash(all, now) == r.NeighbourHash ? "current" : "interface";
                if (state == "interface")
                {
                    var cats = ModelDb.NeighbourCounts(all, now);
                    changed = string.Join("; ", cats.Keys.Union(r.Neighbours.Keys).OrderBy(k => k)
                        .Select(k => (k, a: r.Neighbours.GetValueOrDefault(k), b: cats.GetValueOrDefault(k))).Select(x => x.a == x.b ? $"{x.k} {x.b}" : $"{x.k} {x.a} -> {x.b}"));
                    changed = "a neighbour changed; neighbours by category (recorded -> now): " + changed;
                }
            }
            if (state == "current") current++;
            rows.Add(new ElementReviewRow(r.GlobalId, r.Name, r.Level, r.Category, r.Bridge, r.Result, state, r.At, string.Join(", ", r.Sheets), r.Note, r.Report, changed));
        }
        foreach (var g in rows.Where(x => x.State != "current").GroupBy(x => (x.Bridge, x.Category, x.State)))
        {
            string why = g.Key.State switch
            {
                "changed" => "The element changed since its agent review (its fingerprint differs): review it again against its sheets.",
                "interface" => "An element in contact with it changed since the review: review the interface again.",
                _ => "The reviewed element is no longer in the model.",
            };
            rep.Flags.Add(new(ReviewFlagKind.ElementReview, ReviewLevel.Open, g.Key.Bridge, $"{g.Count()} {g.Key.Category} review(s) {g.Key.State}", "",
                string.Join(", ", g.Take(3).Select(x => x.Name)) + (g.Count() > 3 ? ", ..." : ""), why, "re-review and record with `sbm review <model> --record-elements FILE`"));
        }
        foreach (var g in rows.Where(x => x.State == "current" && x.Result == "differs").GroupBy(x => (x.Bridge, x.Category)))
            rep.Flags.Add(new(ReviewFlagKind.ElementReview, ReviewLevel.Minor, g.Key.Bridge, $"{g.Count()} {g.Key.Category} reviewed with a reported difference", "",
                string.Join(", ", g.Take(3).Select(x => x.Name)), "The agent found a difference and reported it (note / write-up); it is a generator flag or a user decision."));

        // coverage: an element is covered by a current or stale review of itself or of a container above it (below the bridge level)
        var byId = all.Values.ToDictionary(x => x.Id);
        var reviewed = new HashSet<long>(record.Elements.Where(r => all.ContainsKey(r.GlobalId)).Select(r => all[r.GlobalId].Id));
        var memo = new Dictionary<long, bool>();
        bool Covered(long id)
        {
            var chain = new List<long>();
            bool res = false;
            for (long? cur = id; cur is long k && byId.TryGetValue(k, out var x); cur = x.ParentId)
            {
                if (memo.TryGetValue(k, out var m)) { res = m; break; }
                chain.Add(k);
                if (AboveReview.Contains(x.Level)) break;
                if (reviewed.Contains(k)) { res = true; break; }
            }
            foreach (var k in chain) memo[k] = res;
            return res;
        }
        int total = 0, covered = 0;
        foreach (var g in all.Values.Where(x => CoveredLevels.Contains(x.Level)).GroupBy(x => (x.Bridge, x.Level, x.Category)).OrderBy(g => g.Key))
        {
            int n = g.Count(), c = g.Count(x => Covered(x.Id));
            total += n; covered += c;
            if (c < n)
                rep.Flags.Add(new(ReviewFlagKind.ElementReview, ReviewLevel.Open, g.Key.Bridge, $"{n - c:N0} of {n:N0} {g.Key.Level}s '{g.Key.Category}' without an agent review", "",
                    string.Join(", ", g.Where(x => !Covered(x.Id)).Take(3).Select(x => x.Name)),
                    "No agent has compared these elements with their plan sheets yet.", "agent review against `sbm db <model> --sheets-for <name>`, then `sbm review <model> --record-elements FILE`"));
        }
        rep.Notes.Add($"element agent reviews: {record.Elements.Count:N0} recorded, {current:N0} current; {covered:N0} of {total:N0} elements / parts / bars covered");
        if (write) mdb.WriteElementReview(rows, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
    }

    /// <summary>Records element agent reviews from a request file: resolves the selectors in the companion database (it must be newer than the
    /// model: run <c>sbm pipeline</c> first), stores each element's fingerprint and its neighbours' fingerprints. Returns the number recorded.</summary>
    public static int RecordElements(SbmDocument doc, string requestFile, out string message)
    {
        string model = doc.FilePath ?? throw new InvalidOperationException("The model has no file.");
        string db = CompanionDatabase.DefaultPath(model);
        if (!File.Exists(db) || (ModelDatabase.BuiltAt(db) ?? File.GetLastWriteTimeUtc(db)) < File.GetLastWriteTimeUtc(model))
        { message = "The companion database is missing or older than the model: run `sbm pipeline <model.sbm>` first."; return 0; }
        var req = JsonSerializer.Deserialize<ElementReviewRequest>(File.ReadAllText(requestFile), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new ElementReviewRequest();
        string recordPath = AgentReviewRecord.PathFor(model);
        var record = AgentReviewRecord.Load(recordPath);
        using var mdb = new ModelDb(db);
        var all = mdb.ReviewElements();
        var at = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        var picked = new List<(string gid, ElementReviewRequestItem it)>();
        var empty = new List<string>();
        foreach (var it in req.Reviews)
        {
            var ids = it.Ids.Count > 0 ? it.Ids.Where(all.ContainsKey).ToList() : ModelDb.Select(all, it.Bridge, it.Level, it.Category, it.Name);
            if (ids.Count == 0) empty.Add(it.Ids.Count > 0 ? string.Join(",", it.Ids.Take(3)) : $"{it.Bridge}/{it.Level}/{it.Category}/{it.Name}");
            picked.AddRange(ids.Select(g => (g, it)));
        }
        var nb = ModelDb.Neighbours(all, picked.Select(p => p.gid));
        var byGid = record.Elements.ToDictionary(r => r.GlobalId, StringComparer.Ordinal);
        foreach (var (gid, it) in picked)
        {
            var e = all[gid];
            byGid[gid] = new ElementReviewRecord
            {
                GlobalId = gid, Name = e.Name, Level = e.Level, Category = e.Category, Bridge = e.Bridge, Result = it.Result, Sheets = it.Sheets, Note = it.Note,
                Report = it.Report, At = at, Fingerprint = e.Fingerprint,
                NeighbourHash = ModelDb.NeighbourHash(all, nb.GetValueOrDefault(gid) ?? new List<string>()),
                Neighbours = ModelDb.NeighbourCounts(all, nb.GetValueOrDefault(gid) ?? new List<string>()),
            };
        }
        record.Elements = byGid.Values.OrderBy(r => r.Bridge).ThenBy(r => all.TryGetValue(r.GlobalId, out var x) ? x.Id : long.MaxValue).ToList();
        record.Save(recordPath);
        message = $"{picked.Select(p => p.gid).Distinct().Count():N0} element review(s) recorded in {System.IO.Path.GetFileName(recordPath)}"
            + (empty.Count > 0 ? $"; no element matched: {string.Join(" | ", empty)}" : "") + ".";
        return picked.Count;
    }

    // ------------------------------------------------------------------ (1) plan PDFs

    private static bool Pdfs(SbmDocument doc, string? model, AgentReviewRecord record, AgentReviewOptions o, AgentReviewReport rep, ModelDb? mdb)
    {
        bool changed = false;
        var p = doc.Project;
        var indexed = new List<string>();
        try { if (mdb != null) indexed = mdb.PdfPaths(); } catch (Exception) { }
        if (p.PlanPdfs.Count == 0)
            rep.Flags.Add(new(ReviewFlagKind.PlanPdf, ReviewLevel.Open, "", "No plan PDF attached to the model (Doc.Project.PlanPdfs is empty)", "PlanPdfs",
                string.Join("; ", indexed.Select(Path.GetFileName)),
                indexed.Count == 0 ? "The model names no plan set, so nothing can be traced to a sheet."
                                   : "The sheet index found the plan set through the sheet-links file, but the model itself does not name it (checklist step 1).",
                indexed.Count == 0 ? "add Doc.Project.PlanPdfs.Add(@\"C:\\SBMData\\<plan set>.pdf\") to the build script"
                                   : string.Join(" ", indexed.Select(x => $"Doc.Project.PlanPdfs.Add(@\"{x}\");")) + " in the build script"));
        var files = p.PlanPdfs.Select(f => Path.GetFullPath(BridgeProject.ResolvePath(f, model))).ToList();
        foreach (var x in indexed)
            if (!files.Any(f => string.Equals(Path.GetFileName(f), Path.GetFileName(x), StringComparison.OrdinalIgnoreCase))) files.Add(x);
        foreach (var full in files)
        {
            string f = Path.GetFileName(full);
            if (!File.Exists(full))
            {
                rep.Flags.Add(new(ReviewFlagKind.PlanPdf, ReviewLevel.Open, "", $"Attached PDF not found: {f}", "PlanPdfs", full,
                    "The plan set named by the model is not on this computer, so its sheets cannot be read or indexed.", "copy the PDF there or correct the path in the build script"));
                continue;
            }
            var info = new FileInfo(full);
            string hash;
            using (var s = File.OpenRead(full)) hash = Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant();
            string name = Path.GetFileName(full);
            var rec = record.Pdfs.FirstOrDefault(r => string.Equals(r.File, name, StringComparison.OrdinalIgnoreCase));
            string now = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (rec == null)
            {
                record.Pdfs.Add(new ReviewPdfRecord { File = name, Sha256 = hash, Bytes = info.Length, RecordedAt = now });
                changed = true;
                rep.Notes.Add($"{name}: fingerprint recorded ({info.Length:N0} bytes) - later reviews flag any change to this PDF");
            }
            else if (rec.Sha256 != hash)
            {
                bool accept = o.AcceptPdfs.Any(a => string.Equals(Path.GetFileName(a), name, StringComparison.OrdinalIgnoreCase));
                if (accept)
                {
                    rec.Sha256 = hash; rec.Bytes = info.Length; rec.RecordedAt = now; changed = true;
                    rep.Notes.Add($"{name}: new version accepted and recorded");
                }
                else
                    rep.Flags.Add(new(ReviewFlagKind.PlanPdf, ReviewLevel.Open, "", $"{name} changed since it was recorded ({rec.RecordedAt})", "PlanPdfs", $"{rec.Bytes:N0} -> {info.Length:N0} bytes",
                        "A different PDF (revised or replaced set) is attached now: every element modeled from it needs a new review against the new sheets.",
                        $"re-index (`sbm pipeline <model.sbm> --reindex`), review the elements, then `sbm review <model.sbm> --accept-pdf \"{name}\"`"));
            }
        }
        return changed;
    }

    // ------------------------------------------------------------------ (1) sheet links + database

    private static void SheetLinks(SbmDocument doc, string? model, string? db, ModelDb? mdb, AgentReviewReport rep)
    {
        if (model == null) { rep.Notes.Add("model not saved: sheet links and the database are not reviewed"); return; }
        var p = doc.Project;
        var attached = p.PlanPdfs.Select(f => Path.GetFileName(f)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        string? links = CompanionDatabase.FindLinksFile(model, p.Bridges.Select(b => b.Name));
        if (links == null)
            rep.Flags.Add(new(ReviewFlagKind.SheetLinks, ReviewLevel.Open, "", "No sheet-links file for this model", "", "",
                "No plan sheet index and no element -> sheet links, so no element can be traced to the sheet it was modeled from.", "run `sbm pipeline <model.sbm>`"));
        else
        {
            var rules = LinkRules.Load(links);
            if (!rules.IsFor(model))
                rep.Flags.Add(new(ReviewFlagKind.SheetLinks, ReviewLevel.Open, "", $"{Path.GetFileName(links)} does not name this model", "", "",
                    rules.Models.Count == 0 ? "It is a generic rules file (no \"models\" list): it may belong to another plan set."
                                            : $"It was found by a bridge / plan set name; its \"models\" list is {string.Join(", ", rules.Models)}.",
                    $"add \"{Path.GetFileName(model)}\" to \"models\" in {Path.GetFileName(links)} if this is the right plan set"));
            foreach (var set in rules.PlanSets)
                if (set.Pdf.Length > 0 && attached.Count > 0 && !attached.Any(x => Like(x, Path.GetFileName(set.Pdf))))
                    rep.Flags.Add(new(ReviewFlagKind.SheetLinks, ReviewLevel.Open, "", $"Plan set '{set.Id}' ({Path.GetFileName(set.Pdf)}) is not attached to the model", "", "",
                        "The links file indexes a PDF the model does not name: either the wrong rules file or a missing Doc.Project.PlanPdfs entry.", "add the PDF to Doc.Project.PlanPdfs in the build script"));
            foreach (var a in attached)
                if (!rules.PlanSets.Any(s => Like(a, Path.GetFileName(s.Pdf))))
                    rep.Flags.Add(new(ReviewFlagKind.SheetLinks, ReviewLevel.Open, "", $"Attached PDF {a} has no plan set in {Path.GetFileName(links)}", "", "",
                        "Its sheets are not indexed or linked to elements.", "run `sbm pipeline <model.sbm> --pdf \"" + a + "\"`"));
        }
        if (db == null || !File.Exists(db))
        {
            rep.Flags.Add(new(ReviewFlagKind.SheetLinks, ReviewLevel.Open, "", "No companion database", "", "",
                "Sheets per element, unused sheets and elements without a sheet cannot be reviewed.", "run `sbm pipeline <model.sbm>`"));
            return;
        }
        if ((ModelDatabase.BuiltAt(db) ?? File.GetLastWriteTimeUtc(db)) < File.GetLastWriteTimeUtc(model))
            rep.Flags.Add(new(ReviewFlagKind.SheetLinks, ReviewLevel.Open, "", "Companion database is older than the model", "", "",
                "The sheet findings below come from the previous build and may be out of date.", "run `sbm pipeline <model.sbm>`"));
        if (mdb == null) return;
        foreach (var s in mdb.UnusedSheets())
            rep.Flags.Add(new(ReviewFlagKind.SheetLinks, ReviewLevel.Open, "", $"Sheet p.{s.Page}{(s.Sheet.Length > 0 ? $" ({s.Sheet})" : "")} {s.Title}", "", s.PlanSet,
                "No element links to this sheet: either what it shows is not modeled yet, or the link rules miss it.",
                "model it, or add a link rule, or list it under \"administrative\" in the sheet-links file if it carries nothing to model"));
        foreach (var u in mdb.Unlinked(ElementLevels))
            rep.Flags.Add(new(ReviewFlagKind.SheetLinks, ReviewLevel.Open, u.Bridge, $"{u.Count} {u.Level}(s) '{u.Category}' have no sheet", "", string.Join("; ", u.Examples),
                "Nothing records which sheet these were modeled from: they have no link of their own or on a parent below the bridge (the bridge's general sheets do not count).",
                "add a link rule for them in the sheet-links file"));
    }

    /// <summary>File name matches a plan-set pattern (* and ? wildcards, case-insensitive).</summary>
    private static bool Like(string name, string pattern) =>
        System.Text.RegularExpressions.Regex.IsMatch(name, "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    // ------------------------------------------------------------------ (3) generator flags

    private static void Generator(SbmDocument doc, AgentReviewReport rep)
    {
        var r = doc.Regenerate();
        var all = r.Errors.Select(e => Gen(e, ReviewLevel.Open)).Concat(r.Warnings.Select(w =>
            Gen(w, w.Contains("UNRESOLVED", StringComparison.Ordinal) || w.Contains("CONFLICT", StringComparison.Ordinal) ? ReviewLevel.Open : ReviewLevel.Minor)));
        // the same flag raised for many items (joint by joint, member by member) is reported once with the number of places
        foreach (var g in all.GroupBy(f => (f.Bridge, f.Level, Key: System.Text.RegularExpressions.Regex.Replace(f.Subject, @"[0-9][0-9.'/ -]*", "#"))))
        {
            var first = g.First();
            rep.Flags.Add(g.Count() < RepeatThreshold ? first : first with { Subject = first.Subject + $"  (and {g.Count() - 1:N0} more like it)", Places = g.Count() });
            if (g.Count() < RepeatThreshold) rep.Flags.AddRange(g.Skip(1));
        }
    }

    private static ReviewFlag Gen(string message, ReviewLevel level)
    {
        int i = message.IndexOf(": ", StringComparison.Ordinal);
        string bridge = i > 0 ? message[..i] : "", text = i > 0 ? message[(i + 2)..] : message;
        return new(ReviewFlagKind.GeneratorFlag, level, bridge, text, "", "", "");
    }

    // ------------------------------------------------------------------ (2) program starting values

    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() },
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    /// <summary>Top-level bridge inputs the generators do not read for this kind of bridge (never flagged).</summary>
    private static HashSet<string> Unused(BridgeDefinition b)
    {
        bool girders = b.Girders.Count > 0 && b.Arch == null && b.Segmental == null && b.Truss == null && b.CableStayed == null && !b.SupportLinesOnly;
        var skip = new HashSet<string> { nameof(BridgeDefinition.Name), nameof(BridgeDefinition.AlignmentName), nameof(BridgeDefinition.Structure) };
        string[] steelOnly = { "CrossFrames", "Stiffeners", "FieldSplices", "FieldSpliceGap", "SpliceDetail", "Studs", "LateralBracing", "Bolts" };
        if (!girders) skip.UnionWith(steelOnly.Concat(new[] { "Girders", "Deck", "Bearings", "Drains" }));
        else if (b.Girders.Kind != GirderKind.SteelPlate) skip.UnionWith(steelOnly);
        return skip;
    }

    private static readonly Dictionary<string, string[]> SheetKeywords = new()
    {
        ["Girders"] = new[] { "girder", "beam" }, ["Deck"] = new[] { "deck", "slab", "barrier" }, ["CrossFrames"] = new[] { "cross frame", "diaphragm" },
        ["Stiffeners"] = new[] { "girder" }, ["FieldSplices"] = new[] { "splice" }, ["SpliceDetail"] = new[] { "splice" }, ["Studs"] = new[] { "girder" },
        ["Bearings"] = new[] { "bearing" }, ["LateralBracing"] = new[] { "lateral" }, ["Bolts"] = new[] { "splice", "cross frame" },
        ["Truss"] = new[] { "truss" }, ["Arch"] = new[] { "arch", "tie", "hanger" }, ["CableStayed"] = new[] { "pylon", "stay", "cable", "deck" },
        ["Segmental"] = new[] { "segment" }, ["Railings"] = new[] { "railing" }, ["InspectionAccess"] = new[] { "inspection" },
    };

    private static void Defaults(SbmDocument doc, AgentReviewRecord record, ModelDb? mdb, AgentReviewReport rep)
    {
        var u = doc.Units;
        var confirmed = record.Confirmed.ToDictionary(c => c.Path, c => c.Value, StringComparer.OrdinalIgnoreCase);
        var sheetCache = new Dictionary<string, string>();
        string Sheets(string bridge, string? support, string? top)
        {
            if (mdb == null) return "";
            string key = bridge + "|" + support + "|" + top;
            if (sheetCache.TryGetValue(key, out var s)) return s;
            var kw = top != null && SheetKeywords.TryGetValue(top, out var k) ? k : null;
            List<string> list;
            try { list = mdb.SheetsFor(bridge, support, kw); } catch (Exception) { list = new(); }
            return sheetCache[key] = string.Join("; ", list);
        }

        void Add(string bridge, string path, string subject, object? value, bool minor, string next)
        {
            string shown = PropertyPath.Format(value, path, u);
            if ((confirmed.TryGetValue(path, out var cv) || confirmed.TryGetValue(Pattern(path), out cv)) && cv == shown) { rep.Confirmed++; return; }
            rep.Flags.Add(new(ReviewFlagKind.ProgramDefault, minor ? ReviewLevel.Minor : ReviewLevel.Open, bridge, subject, path, shown,
                "Still the program's starting value: not set from the plans yet.", next));
        }

        // alignments: a missing profile or the program's normal crown
        foreach (var al in doc.Project.Alignments)
        {
            string ap = $"Alignments[{al.Name}]";
            if (al.Profile.Vpis.Count == 0) Add("", ap + ".Profile", ap, "no vertical profile", false, "");
            if (Same(al.CrossSlope, CrossSlopeTable.NormalCrown())) Add("", ap + ".CrossSlope", ap, "program normal crown", false, "");
        }

        foreach (var b in doc.Project.Bridges)
        {
            string bp = $"Bridges[{b.Name}]";
            var skip = Unused(b);
            var skipUnder = new List<string>();
            if (b.Girders.Kind == GirderKind.SteelPlate) skipUnder.Add(bp + ".Girders.Prestressed");
            else skipUnder.AddRange(new[] { bp + ".Girders.Section", bp + ".Girders.Tub" });
            if (b.Girders.Terminations.Count == 0) skipUnder.Add(bp + ".Girders.CrossGirder");
            bool girders = !skip.Contains("Girders");
            var fresh = new BridgeDefinition();
            foreach (var prop in Props(typeof(BridgeDefinition)))
            {
                if (skip.Contains(prop.Name)) continue;
                string path = bp + "." + prop.Name;
                if (prop.Name is nameof(BridgeDefinition.Supports) or nameof(BridgeDefinition.AuxiliarySupports))
                {
                    foreach (var (s, i) in b.GetType().GetProperty(prop.Name)!.GetValue(b) is List<SupportDefinition> l ? l.Select((s, i) => (s, i)) : Enumerable.Empty<(SupportDefinition, int)>())
                    {
                        string sp = $"{path}[{Key(s.Name, i)}]";
                        string next = Sheets(b.Name, s.Name, null);
                        var sskip = girders ? new HashSet<string> { "Name", "SharedPier" }
                                            : new HashSet<string> { "Name", "SharedPier", "DeckEndOffset", "GirderEndExtension", "Fixity", "Bearings", "BearingStiffener", "BearingStiffenerFits" };
                        if (girders && s.Substructure == null)
                            Add(b.Name, sp + ".Substructure", sp, "not defined: program defaults by support type", false, next);
                        Walk(s, new SupportDefinition(), sp, sskip, 0, (p2, v, minor) => Add(b.Name, p2, Owner(p2), v, minor, next), s.Type);
                    }
                    continue;
                }
                if (prop.Name == nameof(BridgeDefinition.Bearings) && b.Supports.Concat(b.AuxiliarySupports).All(x => x.Bearings != null)) continue;
                object? v = prop.GetValue(b), dv = prop.GetValue(fresh);
                string nx = Sheets(b.Name, null, prop.Name);
                Compare(v, dv, prop.PropertyType, path, 0, (p2, val, minor) =>
                {
                    if (!skipUnder.Any(x => p2.StartsWith(x + ".", StringComparison.Ordinal) || p2 == x)) Add(b.Name, p2, Owner(p2), val, minor, nx);
                });
            }
        }

        // project-level civil items (roads, walls, grading, utility networks): every input still at its starting value, same rules as the bridges
        void Items<T>(string list, IEnumerable<T> items, Func<T, string> name) where T : class, new()
        {
            int i = 0;
            foreach (var it in items)
            {
                string n = name(it);
                string ip = $"{list}[{Key(n, i++)}]";
                string next = Sheets(n, null, list);
                Walk(it, new T(), ip, null, 0, (p2, v, minor) => Add(n, p2, Owner(p2), v, minor, next));
            }
        }
        Items("Roads", doc.Project.Roads, r => r.Name);
        Items("Walls", doc.Project.Walls, w => w.Name);
        Items("Gradings", doc.Project.Gradings, g => g.Name);
        Items("Utilities", doc.Project.Utilities, u2 => u2.Name);

        // one flag per input pattern and value when it repeats (items of a list built by a script: thousands of truss items, every pier)
        var defaults = rep.Flags.Where(f => f.Kind == ReviewFlagKind.ProgramDefault).ToList();
        rep.Flags.RemoveAll(f => f.Kind == ReviewFlagKind.ProgramDefault);
        foreach (var g in defaults.GroupBy(f => (f.Bridge, P: Pattern(f.Path), f.Value, f.Level)))
        {
            if (g.Count() < RepeatThreshold) { rep.Flags.AddRange(g); continue; }
            var first = g.First();
            var sheets = g.Select(f => f.Next).Where(n => n.Length > 0).SelectMany(n => n.Split("; ")).Distinct().Take(6);
            rep.Flags.Add(first with { Path = g.Key.P, Subject = Owner(g.Key.P), Next = string.Join("; ", sheets), Places = g.Count() });
        }
    }

    /// <summary>A starting value that repeats this often under one input pattern is reported (and confirmed) once.</summary>
    public const int RepeatThreshold = 4;

    /// <summary>The input pattern of a path: every list index after the bridge as [*] ("Bridges[A].Supports[*].Substructure.MinPedestalHeight").</summary>
    public static string Pattern(string path)
    {
        int first = path.IndexOf(']');
        if (first < 0) return path;
        return path[..(first + 1)] + System.Text.RegularExpressions.Regex.Replace(path[(first + 1)..], @"\[[^\]]*\]", "[*]");
    }

    /// <summary>Records the current starting values at (or under) each path as confirmed by the user; returns how many were recorded.</summary>
    public static int Confirm(SbmDocument doc, IEnumerable<string> paths, string note)
    {
        if (doc.FilePath == null) throw new InvalidOperationException("Save the model first.");
        string recordPath = AgentReviewRecord.PathFor(doc.FilePath);
        var record = AgentReviewRecord.Load(recordPath);
        var rep = new AgentReviewReport();
        Defaults(doc, new AgentReviewRecord(), null, rep);
        int n = 0;
        string at = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        foreach (var raw in paths)
        {
            string pre = raw.Trim().Replace("\"", "").Replace("'", "");
            foreach (var f in rep.Flags.Where(f => f.Path.Equals(pre, StringComparison.OrdinalIgnoreCase)
                         || f.Path.StartsWith(pre + ".", StringComparison.OrdinalIgnoreCase) || f.Path.StartsWith(pre + "[", StringComparison.OrdinalIgnoreCase)))
            {
                record.Confirmed.RemoveAll(c => c.Path.Equals(f.Path, StringComparison.OrdinalIgnoreCase));
                record.Confirmed.Add(new ReviewConfirmation { Path = f.Path, Value = f.Value, Note = note, At = at });
                n++;
            }
        }
        record.Save(recordPath);
        return n;
    }

    private static string Key(string name, int i) => string.IsNullOrWhiteSpace(name) || name.Contains(']') ? i.ToString(CultureInfo.InvariantCulture) : name;

    /// <summary>The object path a leaf belongs to (the subject flags are grouped by).</summary>
    private static string Owner(string path)
    {
        int dot = path.LastIndexOf('.'), br = path.LastIndexOf('[');
        return dot > br ? path[..dot] : path;
    }

    private static IEnumerable<PropertyInfo> Props(Type t) =>
        t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
         .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0 && p.SetMethod!.IsPublic
                     && p.GetCustomAttribute<JsonIgnoreAttribute>() is not { Condition: JsonIgnoreCondition.Always });

    private static bool IsLeaf(Type t)
    {
        t = Nullable.GetUnderlyingType(t) ?? t;
        return t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) || t.IsValueType || t.GetConstructor(Type.EmptyTypes) == null;
    }

    private static bool Same(object? a, object? b)
    {
        if (a == null || b == null) return a == null && b == null;
        try { return JsonSerializer.Serialize(a, a.GetType(), Json) == JsonSerializer.Serialize(b, b.GetType(), Json); }
        catch (Exception) { return false; }
    }

    /// <summary>Zero, negative counts / indices (-1 = all / none), on-off switches, kind / type choices (enums: the generated element shows them), empty,
    /// NaN (= automatic), +-999 m and beyond (= unbounded) or null: a starting value that usually means "off / not used / automatic".</summary>
    private static bool Minor(object? v) => v switch
    {
        null => true, double d => d == 0 || double.IsNaN(d) || Math.Abs(d) >= 999, float f => f == 0, int i => i <= 0, long l => l <= 0, bool => true, string s => s.Length == 0,
        Enum => true, ICollection c => c.Count == 0, _ => false,
    };

    private static void Compare(object? v, object? dv, Type declared, string path, int depth, Action<string, object?, bool> flag, SupportType? st = null)
    {
        if (depth > 14) return;
        var t = v?.GetType() ?? declared;
        if (IsLeaf(t))
        {
            if (v != null && Same(v, dv)) flag(path, v, Minor(v));
            return;
        }
        if (v == null) return;                                              // optional input not used
        if (v is IDictionary dict)
        {
            foreach (DictionaryEntry e in dict)
                if (e.Value != null && !IsLeaf(e.Value.GetType())) Compare(e.Value, Fresh(e.Value.GetType()), e.Value.GetType(), $"{path}[{e.Key}]", depth + 1, flag, st);
            return;
        }
        if (v is IList list)
        {
            var et = t.IsGenericType ? t.GetGenericArguments()[0] : typeof(object);
            if (IsLeaf(et)) { if (list.Count > 0 && Same(v, dv)) flag(path, v, false); return; }
            var names = list.Cast<object?>().Select(x => x?.GetType().GetProperty("Name")?.GetValue(x) as string ?? "").ToList();
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item == null) continue;
                string n = names[i];
                string key = n.Length > 0 && names.Count(x => x == n) == 1 ? Key(n, i) : i.ToString(CultureInfo.InvariantCulture);
                Compare(item, Fresh(item.GetType()), item.GetType(), $"{path}[{key}]", depth + 1, flag, st);
            }
            return;
        }
        Walk(v, dv ?? Fresh(t), path, null, depth + 1, flag, st);
    }

    private static void Walk(object v, object? dv, string path, HashSet<string>? skip, int depth, Action<string, object?, bool> flag, SupportType? st = null)
    {
        if (v.GetType().GetProperty("Enabled") is { PropertyType: var et } ep && et == typeof(bool) && ep.GetValue(v) is false) return;   // switched off
        if (dv != null && dv.GetType() != v.GetType()) dv = Fresh(v.GetType());
        foreach (var p in Props(v.GetType()))
        {
            if (skip != null && skip.Contains(p.Name)) continue;
            if (NotRead(v, p.Name, st)) continue;
            if (p.Name is "Name" or "Source" or "Note" or "Notes" or "Unresolved" or "Basis" or "Reported" or "Assembly" or "Mark" or "Label" or "Tag" or "Description" or "Title"
                || ModelingParameter(p.Name)) continue;
            object? val, dval;
            try { val = p.GetValue(v); dval = dv == null ? null : p.GetValue(dv); } catch (Exception) { continue; }
            Compare(val, dval, p.PropertyType, path + "." + p.Name, depth, flag, st);
        }
    }

    /// <summary>Program accuracy / search settings (not plan values): sampling steps, probes, tolerances.</summary>
    private static bool ModelingParameter(string name) =>
        name.StartsWith("Sample", StringComparison.Ordinal) || name.EndsWith("Step", StringComparison.Ordinal) || name.EndsWith("Probe", StringComparison.Ordinal) || name.Contains("Tolerance", StringComparison.Ordinal)
        || name.EndsWith("TolDeg", StringComparison.Ordinal) || name.EndsWith("Samples", StringComparison.Ordinal);

    private static bool Starts(string name, params string[] prefixes) => prefixes.Any(x => name.StartsWith(x, StringComparison.Ordinal));

    /// <summary>Inputs the generators do not read in this configuration (bearing type, support type, foundation type, column shape, as-designed
    /// inputs that replace the parametric ones). Approximate: when in doubt an input stays in the review.</summary>
    private static bool NotRead(object owner, string name, SupportType? st)
    {
        switch (owner)
        {
            // plan fit records (stiffener / cap ends): each carries its plan Source - entered from the plans, never a program starting value
            case StiffenerEndFit:
                return true;
            // explicit frame of a truss plane item: placement computed by the plan-data script from plan positions, not a program starting value
            case TrussItemFrame:
                return true;
            // segmental details: a block station uses only the clip limits it sets (NaN = this limit is not part of the shape); a recorded-only
            // block type has no shape; schedule-only bars have no placement; slab openings use a size OR a diameter, diaphragm openings an outline
            case SegmentalBlockStation bs:
                return name != nameof(SegmentalBlockStation.Along) && bs.GetType().GetProperty(name)?.GetValue(bs) is double dv && double.IsNaN(dv);
            case SegmentalBlockType bt when bt.NotBuilt.Length > 0:
                return name is nameof(SegmentalBlockType.Stations) or nameof(SegmentalBlockType.Start) or nameof(SegmentalBlockType.From);
            case SegmentalBarDefinition sb:
                return sb.Placement == SegmentalBarPlacement.ScheduleOnly
                    ? name is nameof(SegmentalBarDefinition.Count) or nameof(SegmentalBarDefinition.Spacing) or nameof(SegmentalBarDefinition.FaceDistance)
                        or nameof(SegmentalBarDefinition.Cover) or nameof(SegmentalBarDefinition.CoverTop) or nameof(SegmentalBarDefinition.CoverBottom)
                    : name is nameof(SegmentalBarDefinition.CountPerSegment) or nameof(SegmentalBarDefinition.PrintedDims) or nameof(SegmentalBarDefinition.ShapeCode)
                        || (name is nameof(SegmentalBarDefinition.CoverTop) or nameof(SegmentalBarDefinition.CoverBottom) && double.IsNaN(name == nameof(SegmentalBarDefinition.CoverTop) ? sb.CoverTop : sb.CoverBottom));
            // segment interiors: the segment regex is a selector (default = every segment of the type), not a plan value
            case SegmentalInteriorDefinition:
                return name == nameof(SegmentalInteriorDefinition.Segments);
            case SegmentalOpeningDefinition so:
                if (so.Kind == SegmentalOpeningKind.BentDiaphragm) return name is nameof(SegmentalOpeningDefinition.Width) or nameof(SegmentalOpeningDefinition.Length)
                    or nameof(SegmentalOpeningDefinition.Diameter) or nameof(SegmentalOpeningDefinition.Segments) or nameof(SegmentalOpeningDefinition.CenterOffset);
                if (name is nameof(SegmentalOpeningDefinition.Outline) or nameof(SegmentalOpeningDefinition.OutlineFromTop)) return true;
                if (name == nameof(SegmentalOpeningDefinition.Segments) && so.Bents.Count > 0) return true;
                if (name == nameof(SegmentalOpeningDefinition.Bents) && so.Segments.Count > 0) return true;
                return double.IsNaN(so.Diameter) ? name == nameof(SegmentalOpeningDefinition.Diameter) : name is nameof(SegmentalOpeningDefinition.Width) or nameof(SegmentalOpeningDefinition.Length);
            // segmental joint keys: web rows vs slab key positions; parapet joint runs: printed values are check rows only
            case SegmentalKeyDefinition kd:
                return kd.Kind == SegmentalKeyKind.WebShear
                    ? name is nameof(SegmentalKeyDefinition.Offsets) or nameof(SegmentalKeyDefinition.CenterDepth) or nameof(SegmentalKeyDefinition.FromSection) or nameof(SegmentalKeyDefinition.ToSection)
                    : name is nameof(SegmentalKeyDefinition.WebRows) or nameof(SegmentalKeyDefinition.Spacing) or nameof(SegmentalKeyDefinition.BottomAlongWeb);
            case SegmentalParapetJointRun jr when name.StartsWith("Printed"):
                return jr.GetType().GetProperty(name)?.GetValue(jr) is double pv && double.IsNaN(pv);
            // program switches, not plan values: the twist report and the legacy plate placement (reproduces pre-2026-10-27 output)
            case CrossFrameDefinition when name is nameof(CrossFrameDefinition.ReportTwist) or nameof(CrossFrameDefinition.PlatesAtEachTopOfWeb):
                return true;
            // OPTIONAL standard cross frame given (top WP + depth) for EVERY frame: the per-girder work point offsets are not read
            case CrossFrameDefinition cfd when cfd.Standard is { Scope: StandardFrameScope.All, Styles.Count: 0, Supports.Count: 0 } sd
                                              && !double.IsNaN(sd.TopWorkPoint) && !double.IsNaN(sd.Depth):
                return name is nameof(CrossFrameDefinition.TopWorkPointOffset) or nameof(CrossFrameDefinition.BottomWorkPointOffset);
            case CrossFrameStyle cs when cs.Standard is { Scope: StandardFrameScope.All, Supports.Count: 0 } ss && !double.IsNaN(ss.TopWorkPoint) && !double.IsNaN(ss.Depth):
                return name is nameof(CrossFrameStyle.TopWorkPointOffset) or nameof(CrossFrameStyle.BottomWorkPointOffset);            // roads: the stations are not read where the end is tied to a bridge
            case SBM.Core.Road.RoadDefinition rd:
                return (name == nameof(SBM.Core.Road.RoadDefinition.BeginStation) && rd.BeginAtBridge != null)
                    || (name == nameof(SBM.Core.Road.RoadDefinition.EndStation) && rd.EndAtBridge != null);
            // utilities: location by X / Y or by station / offset; size fields of the other shape; walls / floor of an envelope; cover where both inverts are given
            case SBM.Core.Utility.UtilityStructureDefinition us:
            {
                bool xy = !double.IsNaN(us.X) && !double.IsNaN(us.Y);
                if (xy ? name is "AlignmentName" or "Station" or "Offset" : name is "X" or "Y") return true;
                if (us.Shape == SBM.Core.Utility.UtilityStructureShape.Round ? name is "InnerLength" or "InnerWidth" or "RotationDegrees" : name is "InnerDiameter" or "Cone") return true;
                if (us.IsEnvelope && name is "WallThickness" or "FloorThickness" or "Cone" or "Frame") return true;
                return false;
            }
            case SBM.Core.Utility.UtilityPipeDefinition up:
                if (up.Shape == SBM.Core.Utility.UtilityPipeShape.Circular ? name is "InnerRise" or "InnerSpan" : name == "InnerDiameter") return true;
                if (up.From.Length > 0 && name is "StartX" or "StartY") return true;
                if (up.To.Length > 0 && name is "EndX" or "EndY") return true;
                if (up.From.Length == 0 && name == "From") return true;
                if (up.To.Length == 0 && name == "To") return true;
                return name == "Cover" && !double.IsNaN(up.InvertStart) && !double.IsNaN(up.InvertEnd);
            case SBM.Core.Utility.UtilityDuctBankDefinition ub:
                if (ub.From.Length > 0 && name is "StartX" or "StartY") return true;
                if (ub.To.Length > 0 && name is "EndX" or "EndY") return true;
                if (ub.Columns <= 1 && name == "HorizontalSpacing") return true;
                if (ub.Rows <= 1 && name == "VerticalSpacing") return true;
                return name is "From" or "To" && (name == "From" ? ub.From : ub.To).Length == 0;
            case SBM.Core.Utility.UtilityFrameDefinition uf:
                return name is "OuterLength" or "OuterWidth" or "OpeningLength" or "OpeningWidth" or "OuterDiameter" or "OpeningDiameter"
                    && (uf.OuterDiameter > 0 || uf.OpeningDiameter > 0 ? name is "OuterLength" or "OuterWidth" or "OpeningLength" or "OpeningWidth" : name is "OuterDiameter" or "OpeningDiameter");
            case BearingDefinition b:
                return b.Type switch
                {
                    BearingType.Elastomeric => !Starts(name, "Type", "Pad", "SolePlate"),
                    BearingType.Disc => Starts(name, "Pad", "Generic"),
                    _ => Starts(name, "Pad", "Disc", "UpperPlate", "Ptfe", "ShearPin", "ExpansionPlate"),
                };
            case SubstructureDefinition d:
            {
                if (d.Plan != null) return name != nameof(SubstructureDefinition.Plan);
                string[] abut = { "BearingToCapFront", "Backwall", "AbutmentFoundation" };
                string[] cap = { "CapDepth", "CapEndOverhang", "CapOffset", "TopShape", "CapSlope", "CrownOffset", "BottomShape" };
                if (d.Abutment != null && Starts(name, abut.Concat(cap).Concat(new[] { "Column", "FirstColumnOffset", "Shaft", "Footing", "Foundation", "GroundCover" }).ToArray())) return true;
                if (st == SupportType.Abutment && Starts(name, "Column", "FirstColumnOffset", "Footings", "PlanColumnHeights")) return true;
                if (st == SupportType.Pier && Starts(name, abut)) return true;
                if ((d.NoCap || d.CapProfile.Count > 0) && Starts(name, cap)) return true;
                if (d.NoCap && Starts(name, "CapWidth", "Pedestal", "MinPedestal", "MaxPedestal")) return true;
                if (d.ColumnShape == ColumnShape.Round && Starts(name, "ColumnWidth", "ColumnDepth", "ColumnChamfer", "ColumnProjection")) return true;
                if (d.ColumnShape != ColumnShape.Round && name == nameof(SubstructureDefinition.ColumnDiameter)) return true;
                if (d.Footings.Count > 0 && Starts(name, "FootingLength", "FootingWidth", "PileRows", "PileColumns", "PileSpacing")) return true;
                return d.Foundation switch
                {
                    FoundationType.DrilledShaft => Starts(name, "Footing", "Pile"),
                    FoundationType.SpreadFooting => Starts(name, "Shaft", "Pile"),
                    FoundationType.PileCap => Starts(name, "Shaft"),
                    _ => Starts(name, "Shaft", "Footing", "Pile"),
                };
            }
        }
        return false;
    }

    private static object? Fresh(Type t)
    {
        try { return t.GetConstructor(Type.EmptyTypes) != null ? Activator.CreateInstance(t) : null; }
        catch (Exception) { return null; }
    }
}
