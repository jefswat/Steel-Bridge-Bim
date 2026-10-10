using System.Text;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using SBM.Core.Units;

namespace SBM.Api;

/// <summary>Members available to C# scripts (.csx) without qualification.</summary>
public sealed class ScriptGlobals
{
    private readonly StringBuilder _out;
    internal ScriptGlobals(SbmDocument doc, StringBuilder output, string[] args)
    {
        Doc = doc;
        _out = output;
        Args = args;
    }

    /// <summary>The open project.</summary>
    public SbmDocument Doc { get; }

    private SbmApp? _app;
    /// <summary>The running desktop app (connects on first use) — for scripts run from "sbm run" that drive the live model.</summary>
    public SbmApp App => _app ??= SbmApp.Connect();
    public string[] Args { get; }
    public UnitSystem Units => Doc.Units;

    public static double Ft(double feet) => UnitSystem.Ft(feet);
    public static double In(double inches) => UnitSystem.In(inches);
    public static double M(double meters) => meters;
    public double Sta(string station) => Doc.Station(station);
    public double Len(string length) => Doc.Length(length);

    public void Print(object? value) => _out.AppendLine(value?.ToString() ?? "null");
}

/// <summary>Runs C# scripts against an <see cref="SbmDocument"/> (Roslyn scripting).</summary>
public static class ScriptHost
{
    private static readonly ScriptOptions Options = ScriptOptions.Default
        .AddReferences(typeof(SbmDocument).Assembly, typeof(SBM.Core.Bridge.BridgeDefinition).Assembly, typeof(SBM.IO.Ifc.IfcExporter).Assembly)
        .AddImports("System", "System.Linq", "System.Collections.Generic", "SBM.Api", "SBM.Core.Alignments", "SBM.Core.Bridge",
            "SBM.Core.Framing", "SBM.Core.Steel", "SBM.Core.Project", "SBM.Core.Units", "SBM.Core.Geometry");

    public sealed record Result(bool Success, string Output, string? Error, object? ReturnValue);

    public static async Task<Result> RunAsync(string code, SbmDocument doc, string[]? args = null, CancellationToken ct = default)
    {
        var sb = new StringBuilder();
        var globals = new ScriptGlobals(doc, sb, args ?? Array.Empty<string>());
        try
        {
            var state = await CSharpScript.RunAsync(code, Options, globals, typeof(ScriptGlobals), ct);
            return new Result(true, sb.ToString(), null, state.ReturnValue);
        }
        catch (CompilationErrorException ex)
        {
            return new Result(false, sb.ToString(), string.Join(Environment.NewLine, ex.Diagnostics), null);
        }
        catch (Exception ex)
        {
            return new Result(false, sb.ToString(), ex.GetType().Name + ": " + ex.Message, null);
        }
    }

    public static Task<Result> RunFileAsync(string path, SbmDocument doc, string[]? args = null) => RunAsync(File.ReadAllText(path), doc, args);
}
