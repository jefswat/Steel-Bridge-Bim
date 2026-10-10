using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using SBM.Api;

namespace SBM.App;

/// <summary>
/// Remote control: the running app accepts commands from <see cref="SbmApp"/> / "sbm app …" over the named pipe
/// \\.\pipe\SteelBridgeModeler.&lt;pid&gt; (current Windows user only). Each request is handled on the UI thread exactly
/// as the matching ribbon command would be (edits are undoable and regenerate the model).
/// </summary>
public partial class MainWindow
{
    private CancellationTokenSource? _remoteCts;

    private void StartRemote()
    {
        _remoteCts = new CancellationTokenSource();
        var ct = _remoteCts.Token;
        string name = SbmApp.PipePrefix + Environment.ProcessId;
        Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await server.WaitForConnectionAsync(ct);
                    using var reader = new StreamReader(server, Encoding.UTF8, leaveOpen: true);
                    string? line = await reader.ReadLineAsync(ct);
                    SbmApp.Response resp;
                    try
                    {
                        var req = JsonSerializer.Deserialize<SbmApp.Request>(line ?? "") ?? throw new SbmAppException("Empty request.");
                        string result = await Dispatcher.InvokeAsync(() => HandleRemote(req.cmd, req.args ?? Array.Empty<string>())).Task.Unwrap();
                        resp = new SbmApp.Response(true, result);
                    }
                    catch (Exception ex)
                    {
                        resp = new SbmApp.Response(false, ex is SbmAppException or ArgumentException or InvalidOperationException or IOException ? ex.Message : ex.ToString());
                    }
                    var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(resp) + "\n");
                    await server.WriteAsync(bytes, ct);
                    await server.FlushAsync(ct);
                    if (_closeAfterReply) await Dispatcher.InvokeAsync(Close);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { ViewModels.MainViewModel.Log("remote: " + ex.Message); }
            }
        }, ct);
    }

    private bool _closeAfterReply;

    /// <summary>Waits for a running regeneration and the deferred zoom / render passes.</summary>
    private async Task Settle()
    {
        for (int i = 0; i < 600 && _vm.IsBusy; i++) await Task.Delay(100);
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        await Task.Delay(400);
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private async Task<string> HandleRemote(string cmd, string[] a)
    {
        string Arg(int i) => i < a.Length ? a[i] : "";
        var doc = SbmDocument.Attach(_vm.Project, _vm.FilePath);
        switch (cmd.ToLowerInvariant())
        {
            case "status":
            case "ping":
                return $"Steel Bridge Modeler (process {Environment.ProcessId})\n" +
                       $"project: {_vm.Project.Name}\nfile: {_vm.FilePath ?? "(unsaved)"}\nunsaved changes: {(_vm.IsDirty ? "yes" : "no")}\n" +
                       $"alignments: {string.Join(", ", _vm.Project.Alignments.Select(x => x.Name))}\n" +
                       $"bridges: {string.Join(", ", _vm.Project.Bridges.Select(x => x.Name))}\nstatus: {_vm.StatusText}";
            case "get":
                return doc.GetText(Arg(0));
            case "describe":
                return string.Join("\n", doc.Describe(Arg(0)).Select(d => $"{d.name} ({d.type}) = {d.value}"));
            case "set":
            {
                if (a.Length < 2) throw new ArgumentException("set needs a path and a value.");
                _vm.Checkpoint();
                string v = doc.Set(a[0], a[1]);
                await _vm.RegenerateAsync(false);
                return $"{a[0]} = {v}";
            }
            case "regen":
            case "regenerate":
                await _vm.RegenerateAsync(false);
                return _vm.StatusText;
            case "open":
            {
                if (_vm.IsDirty && Arg(1) != "--force") throw new SbmAppException("The open project has unsaved changes (save first, or pass --force to discard them).");
                _vm.OpenPath(Arg(0));
                return $"Opened {Arg(0)}";
            }
            case "save":
                return _vm.SaveToPath(string.IsNullOrEmpty(Arg(0)) ? null : Arg(0));
            case "import-pgsuper":
                return await _vm.ImportPgSuperFile(Arg(0), Arg(1), Arg(2), Arg(3));
            case "run":
                return await _vm.RunScriptFile(Arg(0), a.Skip(1).ToArray());
            case "snapshot":
                await Settle();
                SaveSnapshot(Arg(0));
                return $"Wrote {Arg(0)} and {Path.ChangeExtension(Arg(0), null)}_view.png";
            case "view":
                if (Arg(0).Equals("plan", StringComparison.OrdinalIgnoreCase)) SetPlanMode(true);
                else if (Arg(0).Equals("3d", StringComparison.OrdinalIgnoreCase)) SetPlanMode(false);
                else if (Arg(0).Equals("zoom", StringComparison.OrdinalIgnoreCase)) ZoomExtents();
                else if (Arg(0).Equals("terrain", StringComparison.OrdinalIgnoreCase)) SetTerrainVisible(!Arg(1).Equals("off", StringComparison.OrdinalIgnoreCase));
                else if (Compass.FirstOrDefault(c => c.name.Equals(Arg(0), StringComparison.OrdinalIgnoreCase)) is { name: not null } cv)
                {
                    if (double.TryParse(Arg(1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double ang)) _compassAngle = ang;
                    SetCompassView(cv.az);
                }
                else throw new ArgumentException("view plan | 3d | zoom | n|ne|e|se|s|sw|w|nw [angle] | terrain on|off");
                await Settle();
                return "ok";
            case "section":
            {
                if (Arg(0).Equals("off", StringComparison.OrdinalIgnoreCase)) { if (SectionPanel.Visibility == System.Windows.Visibility.Visible) OnSectionClose(this, new System.Windows.RoutedEventArgs()); return "Section off"; }
                if (!_vm.Units.TryParseStation(Arg(0), out double sta)) throw new ArgumentException($"Can't read the station '{Arg(0)}'.");
                SectionAtStation(sta, double.TryParse(Arg(1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double sk) ? sk : 0);
                return $"Section at STA {_vm.Units.FormatStation(sta)}";
            }
            case "measure":
            case "station-offset":
                return MeasureRemote(cmd, a);
            case "fps":
            {
                // Orbit the 3D camera for ~3 s and report frame times (render benchmark).
                if (_planMode) SetPlanMode(false);
                if (View.Camera is not HelixToolkit.Wpf.SharpDX.ProjectionCamera cam) return "no camera";
                var target = cam.Position + cam.LookDirection;
                var times = new List<double>();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                double last = 0; int frame = 0;
                var done = new TaskCompletionSource();
                EventHandler h = null!;
                h = (_, _) =>
                {
                    double now = sw.Elapsed.TotalMilliseconds;
                    if (frame++ > 0) times.Add(now - last);
                    last = now;
                    var rot = new System.Windows.Media.Media3D.RotateTransform3D(new System.Windows.Media.Media3D.AxisAngleRotation3D(new System.Windows.Media.Media3D.Vector3D(0, 0, 1), 2));
                    var lk = rot.Transform(cam.LookDirection);
                    cam.LookDirection = lk; cam.Position = target - lk;
                    if (now > 3000) { System.Windows.Media.CompositionTarget.Rendering -= h; done.TrySetResult(); }
                };
                System.Windows.Media.CompositionTarget.Rendering += h;
                await done.Task;
                times.Sort();
                return times.Count == 0 ? "no frames" : $"{times.Count} frames, average {times.Average():F1} ms ({1000 / times.Average():F0} fps), median {times[times.Count / 2]:F1} ms, worst {times[^1]:F1} ms";
            }
            case "messages":
            {
                int n = int.TryParse(Arg(0), out int k) ? k : 20;
                return string.Join("\n", _vm.Messages.TakeLast(n).Select(m => $"{m.Severity}: {m.Text}"));
            }
            case "undo":
                _vm.UndoCommand.Execute(null);
                return "ok";
            case "close":
            {
                if (_vm.IsDirty)
                {
                    if (Arg(0) == "--save") _vm.SaveToPath(null);
                    else if (Arg(0) == "--discard") _vm.IsDirty = false;
                    else throw new SbmAppException("Unsaved changes: use 'close --save' or 'close --discard'.");
                }
                _closeAfterReply = true;
                return "Closing";
            }
            case "help":
                return "status | get <path> | set <path> <value> | describe [path] | regen | open <file> [--force] | save [file] | " +
                       "import-pgsuper <file> [alignment] [station] [name] | run <script.csx> [args] | snapshot <png> | view plan|3d|zoom | " +
                       "section <station> [skew] | section off | measure <E,N,Z> <E,N,Z> [<E,N,Z>] | station-offset <E,N[,Z]> [alignment=NAME] | messages [n] | undo | close [--save|--discard]";
            default:
                throw new SbmAppException($"Unknown command '{cmd}'. Try 'help'.");
        }
    }
}
