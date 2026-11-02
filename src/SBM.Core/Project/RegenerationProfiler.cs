using System.Collections.Concurrent;
using System.Diagnostics;

namespace SBM.Core.Project;

/// <summary>
/// Timing recorder for regeneration (and anything else that wants to report steps): <c>using (RegenerationProfiler.Step(bridge, "steel")) ...</c>;
/// nested steps are named with a '/' ("steel/connections").
/// Off (no recorder) = one null check per step. <see cref="Start"/> installs a recorder for the calling flow and everything it starts (async /
/// parallel work included, via <see cref="AsyncLocal{T}"/>); <see cref="Stop"/> returns the samples. Step times are wall time of that step on its
/// thread: steps of different bridges (and steel / concrete of one bridge) run in parallel, so their sum exceeds the elapsed time.
/// </summary>
public static class RegenerationProfiler
{
    private static readonly AsyncLocal<Recorder?> _current = new();

    public sealed record Sample(string Bridge, string Step, double Ms, int Depth);

    public sealed class Recorder
    {
        internal readonly ConcurrentQueue<Sample> Samples = new();
        internal readonly Stopwatch Clock = Stopwatch.StartNew();
    }

    /// <summary>Starts recording for this flow (replaces any recorder already running in it).</summary>
    public static Recorder Start() => _current.Value = new Recorder();

    /// <summary>Stops recording and returns the samples and the elapsed time since <see cref="Start"/>.</summary>
    public static (IReadOnlyList<Sample> samples, double elapsedMs) Stop(Recorder r)
    {
        if (_current.Value == r) _current.Value = null;
        return (r.Samples.ToList(), r.Clock.Elapsed.TotalMilliseconds);
    }

    public static bool Active => _current.Value != null;

    /// <summary>Times one step (dispose to end it). No recorder = no-op.</summary>
    public static Scope Step(string bridge, string step) => _current.Value is { } r ? new Scope(r, bridge, step) : default;

    public readonly struct Scope : IDisposable
    {
        private readonly Recorder? _r;
        private readonly string _bridge, _step;
        private readonly long _t0;
        internal Scope(Recorder r, string bridge, string step) { _r = r; _bridge = bridge; _step = step; _t0 = Stopwatch.GetTimestamp(); }
        /// <summary>Depth = number of '/' in the step name ("steel/connections" is inside "steel").</summary>
        public void Dispose()
        {
            if (_r == null) return;
            _r.Samples.Enqueue(new Sample(_bridge, _step, Stopwatch.GetElapsedTime(_t0).TotalMilliseconds, _step.Count(c => c == '/')));
        }
    }

    /// <summary>Plain-text report: elapsed time, then per step the summed time over bridges (top level and nested), then per bridge.</summary>
    public static string Report(IReadOnlyList<Sample> samples, double elapsedMs, int top = 40)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"elapsed {elapsedMs / 1000:0.00} s (steps of different bridges run in parallel: sums can exceed it)");
        sb.AppendLine("by step (summed over bridges)                     total s   count   max s");
        foreach (var g in samples.GroupBy(s => (s.Depth, s.Step)).OrderBy(g => g.Key.Step.Split('/')[0] is var root ? -samples.Where(x => x.Step == root).Sum(x => x.Ms) : 0)
                     .ThenBy(g => g.Key.Step.Split('/')[0]).ThenBy(g => g.Key.Depth).ThenByDescending(g => g.Sum(s => s.Ms)).Take(top))
            sb.AppendLine($"  {new string(' ', 2 * g.Key.Depth)}{g.Key.Step,-46}{g.Sum(s => s.Ms) / 1000,8:0.00}{g.Count(),8}{g.Max(s => s.Ms) / 1000,8:0.00}");
        sb.AppendLine("by bridge (its top-level steps)");
        foreach (var g in samples.Where(s => s.Depth == 0).GroupBy(s => s.Bridge).OrderByDescending(g => g.Sum(s => s.Ms)))
            sb.AppendLine($"  {g.Key,-40}{g.Sum(s => s.Ms) / 1000,8:0.00} s  " + string.Join(", ", g.OrderByDescending(s => s.Ms).Take(4).Select(s => $"{s.Step} {s.Ms / 1000:0.00}")));
        return sb.ToString();
    }

    /// <summary>CSV: bridge, step, depth, ms.</summary>
    public static string Csv(IReadOnlyList<Sample> samples)
    {
        var sb = new System.Text.StringBuilder("bridge,step,depth,ms\n");
        foreach (var s in samples) sb.Append('"').Append(s.Bridge.Replace("\"", "\"\"")).Append("\",\"").Append(s.Step).Append("\",").Append(s.Depth).Append(',')
            .Append(s.Ms.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
        return sb.ToString();
    }
}
