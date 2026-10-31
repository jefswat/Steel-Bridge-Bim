using System.Text.Json;
using SBM.Api;

namespace SBM.Cli;

/// <summary><c>sbm review</c>: agent review of a model built from plan PDFs (see <see cref="AgentReview"/>, docs/AGENT-REVIEW.md).</summary>
public static class ReviewCommand
{
    public const string Help = """
        sbm review <model.sbm> [--all] [--no-regen] [--no-db] [--no-record] [--accept-pdf FILE]... [--json flags.json]
        sbm review <model.sbm> --confirm PATH [--confirm PATH]... [--note TEXT]
          Agent review of a model built from plan PDFs. Flags:
            plan PDF        no PDF attached, PDF missing, PDF changed since its fingerprint was recorded (first review records it)
            sheet links     no / wrong sheet-links file, plan sets not attached, attached PDFs not indexed, sheets no element uses,
                            elements with no sheet they were modeled from, database older than the model
            program default every input still at the program's starting value (non-zero = OPEN, zero / off / empty = minor),
                            with the sheets linked to that support / item ("read on")
            generator flag  UNRESOLVED / CONFLICT warnings and errors of the regeneration (other warnings = minor)
          Writes <model>.agent-review.json (PDF fingerprints, confirmed values; commit it) and the table agent_review in the
          companion database (view v_agent_review_open). --confirm records the current starting values at or under PATH
          (e.g. "Bridges[Ramp B].Supports[Pier 1].DeckEndOffset" or a whole object "Bridges[Ramp B].Studs") as confirmed;
          they stay confirmed while the value is unchanged. Exit code 3 when flags are open.
        """;

    public static int Run(string[] args)
    {
        if (args.Length < 2) { Console.WriteLine(Help); return 0; }
        var doc = SbmDocument.Open(args[1]);
        List<string> Many(string name) { var l = new List<string>(); for (int i = 2; i < args.Length - 1; i++) if (args[i] == name) l.Add(args[i + 1]); return l; }
        string? Opt(string name) => Many(name).LastOrDefault();
        bool Has(string name) => args.Contains(name);

        var confirm = Many("--confirm");
        if (confirm.Count > 0)
        {
            int n = AgentReview.Confirm(doc, confirm, Opt("--note") ?? "");
            Console.WriteLine(n == 0 ? "Nothing confirmed: no starting value at or under those paths (paths as `sbm review --all` prints them)."
                                     : $"{n} starting value(s) recorded as confirmed in {Path.GetFileName(AgentReviewRecord.PathFor(doc.FilePath!))}.");
            return n == 0 ? 1 : 0;
        }

        var o = new AgentReviewOptions
        {
            Regenerate = !Has("--no-regen"), UseDatabase = !Has("--no-db"), Record = !Has("--no-record"), AcceptPdfs = Many("--accept-pdf"),
        };
        var rep = AgentReview.Run(doc, o);
        Console.WriteLine(rep.ToText(Has("--all")));
        if (Opt("--json") is { } json)
            File.WriteAllText(json, JsonSerializer.Serialize(new { model = rep.Model, open = rep.Open, minor = rep.Minor, confirmed = rep.Confirmed, notes = rep.Notes,
                flags = rep.Flags.Select(f => new { kind = f.KindText, level = f.Level.ToString().ToLowerInvariant(), f.Bridge, f.Subject, f.Path, f.Value, f.Reason, f.Next }) },
                new JsonSerializerOptions { WriteIndented = true }));
        return rep.Open > 0 ? 3 : 0;
    }
}
