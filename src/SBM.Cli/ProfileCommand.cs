using SBM.Api;
using SBM.Core.Project;

namespace SBM.Cli;

/// <summary><c>sbm profile</c>: where the time goes when a model is opened and regenerated (read-only, the model is never saved).</summary>
public static class ProfileCommand
{
    public const string Help = """
        sbm profile <model.sbm> [--runs N] [--top N] [--csv f.csv]
          Opens the model and regenerates it from scratch N times (default 1), timing every step (RegenerationProfiler): terrain load, framing,
          steel (layouts, cross frames, splices, girder plates, studs, stiffeners, bearings ...), concrete (deck slab, haunches, barriers, joints,
          rebar, bar marks), prestressed beams, arch / truss / cable-stayed / segmental generators (segments, tendons, piers, details, misc,
          section bars), rebar bends, deck checks, railings, level of detail, substructure, misc items, concrete diaphragms.
          Step times are wall time on their own thread; bridges (and steel / concrete of one bridge) run in parallel, so sums exceed the elapsed
          time. A last run with the in-memory cache shows what a re-regeneration of an unchanged model costs. --csv writes every sample.
        """;

    public static int Run(string[] args)
    {
        if (args.Length < 2) { Console.WriteLine(Help); return 0; }
        string? Opt(string name) { int i = Array.IndexOf(args, name); return i > 0 && i + 1 < args.Length ? args[i + 1] : null; }
        int runs = int.Parse(Opt("--runs") ?? "1", System.Globalization.CultureInfo.InvariantCulture);
        int top = int.Parse(Opt("--top") ?? "60", System.Globalization.CultureInfo.InvariantCulture);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var doc = SbmDocument.Open(args[1]);
        Console.WriteLine($"{Path.GetFileName(args[1])}: {doc.Project.Bridges.Count} bridge(s); open (read + parse the .sbm) {sw.Elapsed.TotalSeconds:0.00} s");
        var all = new List<RegenerationProfiler.Sample>();
        for (int run = 1; run <= runs; run++)
        {
            if (run > 1) doc = SbmDocument.Open(args[1]);   // a fresh document = a cold cache
            var rec = RegenerationProfiler.Start();
            var rep = doc.Regenerate();
            var (samples, ms) = RegenerationProfiler.Stop(rec);
            Console.WriteLine($"\n=== run {run}: regenerated {rep.Bridges} bridge(s) in {rep.Elapsed.TotalSeconds:0.00} s ({rep.Errors.Count} errors)");
            Console.Write(RegenerationProfiler.Report(samples, ms, top));
            all.AddRange(samples);
        }
        var recW = RegenerationProfiler.Start();
        var repW = doc.Regenerate();
        RegenerationProfiler.Stop(recW);
        Console.WriteLine($"\n=== unchanged model again (in-memory cache): {repW.Elapsed.TotalSeconds:0.00} s ({repW.Reused} of {repW.Bridges} bridge(s) from the cache)");
        if (Opt("--csv") is { } csv) { File.WriteAllText(csv, RegenerationProfiler.Csv(all)); Console.WriteLine($"Samples -> {csv}"); }
        return 0;
    }
}
