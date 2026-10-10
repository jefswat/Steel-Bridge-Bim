using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace SBM.Api;

/// <summary>Error reported by the running app.</summary>
public sealed class SbmAppException(string message) : Exception(message);

/// <summary>
/// Drives a RUNNING Steel Bridge Modeler (the desktop app) — the counterpart of <see cref="SbmDocument"/>, which works on
/// project files headlessly. Like COM automation's GetActiveObject, but over a local named pipe
/// (<c>\\.\pipe\SteelBridgeModeler.&lt;process id&gt;</c>, restricted to the current Windows user): no registration needed,
/// usable from C#, Python or PowerShell. Protocol: one UTF-8 JSON line per request <c>{"cmd":"set","args":["path","value"]}</c>,
/// one JSON line back <c>{"ok":true,"result":"…"}</c>.
/// </summary>
/// <example>
/// <code>
/// using var app = SbmApp.Connect();
/// app.Set("Bridges[0].Girders.Count", "6");      // edits the live model, regenerates, undoable in the app
/// Console.WriteLine(app.Get("Bridges[0].Girders.Spacings"));
/// app.Save();
/// </code>
/// </example>
public sealed class SbmApp : IDisposable
{
    public const string PipePrefix = "SteelBridgeModeler.";
    public int ProcessId { get; }
    public int TimeoutMs { get; set; } = 120_000;

    private SbmApp(int pid) => ProcessId = pid;

    /// <summary>Process ids of the running app instances that accept commands.</summary>
    public static List<int> RunningInstances()
    {
        var list = new List<int>();
        try
        {
            foreach (var p in Directory.GetFiles(@"\\.\pipe\"))
            {
                var name = Path.GetFileName(p);
                if (name.StartsWith(PipePrefix) && int.TryParse(name[PipePrefix.Length..], out int pid)) list.Add(pid);
            }
        }
        catch (IOException) { }
        return list.Distinct().ToList();
    }

    /// <summary>Connects to the running app (the only one, or the given process id).</summary>
    public static SbmApp Connect(int? processId = null)
    {
        var all = RunningInstances();
        if (processId is { } id)
        {
            if (!all.Contains(id)) throw new SbmAppException($"No running Steel Bridge Modeler with process id {id}.");
            return new SbmApp(id);
        }
        return all.Count switch
        {
            0 => throw new SbmAppException("Steel Bridge Modeler is not running (start the app, then try again)."),
            1 => new SbmApp(all[0]),
            _ => throw new SbmAppException($"{all.Count} copies are running (process ids {string.Join(", ", all)}); pass one with --pid / Connect(pid)."),
        };
    }

    /// <summary>Sends one command; returns the app's result text or throws <see cref="SbmAppException"/>.</summary>
    public string Send(string command, params string[] args)
    {
        using var pipe = new NamedPipeClientStream(".", PipePrefix + ProcessId, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
        pipe.Connect(5000);
        var req = JsonSerializer.Serialize(new Request(command, args)) + "\n";
        var bytes = Encoding.UTF8.GetBytes(req);
        pipe.Write(bytes, 0, bytes.Length);
        pipe.Flush();
        using var reader = new StreamReader(pipe, Encoding.UTF8);
        var task = reader.ReadLineAsync();
        if (!task.Wait(TimeoutMs)) throw new SbmAppException($"The app did not answer '{command}' within {TimeoutMs / 1000} s.");
        var line = task.Result ?? throw new SbmAppException("The app closed the connection.");
        var resp = JsonSerializer.Deserialize<Response>(line) ?? throw new SbmAppException("Bad response.");
        if (!resp.ok) throw new SbmAppException(resp.result);
        return resp.result;
    }

    public sealed record Request(string cmd, string[] args);
    public sealed record Response(bool ok, string result);

    // ---- convenience wrappers (each maps to one command; see "sbm app help")
    public string Status() => Send("status");
    public string Get(string path) => Send("get", path);
    public string Set(string path, string value) => Send("set", path, value);
    public string Describe(string path = "") => Send("describe", path);
    public string Regenerate() => Send("regen");
    public string Open(string file, bool discardChanges = false) => Send("open", discardChanges ? new[] { Path.GetFullPath(file), "--force" } : new[] { Path.GetFullPath(file) });
    public string Save(string? file = null) => file == null ? Send("save") : Send("save", Path.GetFullPath(file));
    public string ImportPgSuper(string file, string? alignment = null, string? station = null, string? name = null) =>
        Send("import-pgsuper", new[] { Path.GetFullPath(file), alignment ?? "", station ?? "", name ?? "" });
    public string RunScript(string file, params string[] args) => Send("run", new[] { Path.GetFullPath(file) }.Concat(args).ToArray());
    public string Snapshot(string png) => Send("snapshot", Path.GetFullPath(png));
    public string View(string mode) => Send("view", mode);
    /// <summary>
    /// Distance (two points) or angle (three points, vertex in the middle) between points given as "E,N[,Z]" in the project's coordinate
    /// units (ft for US projects, m for SI; unit suffixes accepted). Returns the formatted record (3D / horizontal / slope distance, station and
    /// offset of each point). <paramref name="alignment"/> names the alignment for stations (default: nearest).
    /// </summary>
    public string Measure(string pointA, string pointB, string? pointC = null, string? alignment = null) =>
        Send("measure", new[] { pointA, pointB }.Concat(pointC != null ? new[] { pointC } : Array.Empty<string>())
            .Concat(alignment != null ? new[] { "alignment=" + alignment } : Array.Empty<string>()).ToArray());
    /// <summary>Station, offset and elevations (profile, ground, cross-slope grade) at a point "E,N[,Z]".</summary>
    public string StationOffset(string point, string? alignment = null) =>
        Send("station-offset", alignment != null ? new[] { point, "alignment=" + alignment } : new[] { point });
    public string Messages(int count = 20) => Send("messages", count.ToString());
    /// <summary>Closes the app. Unsaved changes: error unless <paramref name="save"/> or <paramref name="discard"/>.</summary>
    public string Close(bool save = false, bool discard = false) => Send("close", save ? "--save" : discard ? "--discard" : "");

    public void Dispose() { }
}
