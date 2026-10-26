using SBM.Api;

namespace SBM.Cli;

/// <summary>`sbm pipeline` / `sbm build-all`: the default finishing pipeline of a model (SBM.Api.ModelPipeline, docs/MODEL-PIPELINE.md), and the
/// automatic run after `sbm run` saved a model.</summary>
public static partial class Program
{
        private static PipelineOptions PipelineOptionsFrom(string[] args)
    {
        string? Opt(string name) { int i = Array.IndexOf(args, name); return i > 0 && i + 1 < args.Length ? args[i + 1] : null; }
        var o = new PipelineOptions
        {
            Regenerate = !args.Contains("--no-regen"), Database = !args.Contains("--no-db"), SheetIndex = !args.Contains("--no-index"),
            Terrain = !args.Contains("--no-terrain"), Reindex = args.Contains("--reindex"), ForceDatabase = args.Contains("--force-db"),
            TerrainFolder = Opt("--terrain-dir"), Log = m => Console.WriteLine("  " + m),
        };
        if (args.Contains("--offline")) o.Offline = true;
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == "--pdf") o.Pdfs.Add(Path.GetFullPath(args[i + 1]));
        if (Opt("--plans") is { } pl) o.PlanFolders.AddRange(pl.Split(','));
        return o;
    }

    private static int PipelineCommand(string[] args)
    {
        Need(args, 2);
        var doc = SbmDocument.Open(args[1]);
        var o = PipelineOptionsFrom(args);
        if (Array.IndexOf(args, "--margin") is int mi and > 0 && mi + 1 < args.Length)
            o.TerrainMargin = doc.Units.TryParseLength(args[mi + 1], SBM.Core.Units.LengthKind.Long, out double mg) ? mg : throw new ArgumentException($"--margin {args[mi + 1]}: not a length");
        if (args.Contains("--status"))
        {
            Console.WriteLine($"Model pipeline status of {Path.GetFileName(args[1])} (nothing changed):");
            Console.WriteLine(ModelPipeline.Status(doc, o).ToText());
            return 0;
        }
        Console.WriteLine($"Model pipeline: {Path.GetFileName(args[1])}");
        var rep = ModelPipeline.Run(doc, o);
        Console.WriteLine(rep.ToText());
        return rep.Failed ? 1 : 0;
    }

    /// <summary>After `sbm run`: when the script saved a model, run the pipeline on it (opt out: `--no-pipeline` or SBM_PIPELINE=0).</summary>
    private static void PipelineAfterRun(SbmDocument doc, string[] args, string? file, DateTime? before)
    {
        int dd = Array.IndexOf(args, "--");
        var own = dd > 0 ? args[..dd] : args;
        string? path = doc.FilePath ?? (file != null ? Path.GetFullPath(file) : null);
        bool saved = path != null && File.Exists(path) && (before == null || File.GetLastWriteTimeUtc(path) > before.Value);
        if (!saved) return;   // check / report scripts do not save: no pipeline
        if (own.Contains("--no-pipeline") || Environment.GetEnvironmentVariable("SBM_PIPELINE") is "0" or "false")
        {
            Console.WriteLine($"Model pipeline NOT run (--no-pipeline / SBM_PIPELINE=0). Required before the model is finished: sbm pipeline \"{path}\"");
            return;
        }
        Console.WriteLine($"Model pipeline (the script saved {Path.GetFileName(path)}; opt out with --no-pipeline):");
        var model = doc.FilePath == path ? doc : SbmDocument.Open(path!);
        var rep = ModelPipeline.Run(model, PipelineOptionsFrom(own));
        Console.WriteLine(rep.ToText());
    }
}
