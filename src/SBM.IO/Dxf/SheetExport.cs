using System.IO.Compression;
using System.Text;
using SBM.Core.Drawing.Sheets;
using SBM.Core.Drawing.Views;
using SBM.Core.Units;

namespace SBM.IO.Dxf;

/// <summary>Writes 2D views as DXF 2D models (one file per view) in the project's units, to a folder or into one .zip.</summary>
public static class SheetExport
{
    /// <summary>Model-space views are written in feet (US) or meters (SI); paper-space sheets in inches (US) or mm (SI).</summary>
    public static double UnitFor(SheetView v, UnitSystem u) => v.PaperSpace ? (u.IsUS ? 0.0254 : 0.001) : u.LongUnit;

    /// <summary>A composed sheet as a paper-space view (1:1, border and title block included).</summary>
    public static SheetView ForSheet(Sheet s) => new(s.Title, $"{s.Number} {s.Title}", s.Drawing, 1, "", PaperSpace: true);

    /// <summary>File name (no folder) of a view: "&lt;bridge&gt; - &lt;view&gt;.dxf" with invalid characters replaced.</summary>
    public static string FileName(string bridgeName, SheetView v) =>
        string.Concat($"{bridgeName} - {v.FileStem}".Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)) + ".dxf";

    public static string Write(string folder, string bridgeName, SheetView v, UnitSystem u)
    {
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, FileName(bridgeName, v));
        using var w = new StreamWriter(path, false, new UTF8Encoding(false));
        Write(w, v, u);
        return path;
    }

    /// <summary>Writes one view as DXF text.</summary>
    public static void Write(TextWriter w, SheetView v, UnitSystem u)
    {
        double mpu = UnitFor(v, u);
        // Linetype patterns are ~2 units long: scale them to about 3/4" on paper.
        double paperIn = 0.75 * 0.0254 * v.Scale;
        DxfWriter.Write(w, v.Drawing, mpu, flat: true, ltScale: paperIn / mpu / 2.0);
    }

    /// <summary>True when <paramref name="target"/> names a .zip file (else it is a folder).</summary>
    public static bool IsZip(string target) => target.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Writes views to a folder, or to one .zip when <paramref name="target"/> ends in ".zip" (entries "&lt;subfolder&gt;/&lt;file&gt;.dxf";
    /// subfolder "" = the folder / zip root). Returns the written file paths (folder) or entry names (zip). An existing zip is replaced.
    /// </summary>
    public static List<string> WriteAll(string target, IEnumerable<(string subfolder, string bridge, SheetView view)> views, UnitSystem u)
    {
        var written = new List<string>();
        if (!IsZip(target))
        {
            foreach (var (sub, bridge, v) in views)
                written.Add(Write(sub == "" ? target : Path.Combine(target, sub), bridge, v, u));
            return written;
        }
        string full = Path.GetFullPath(target);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        string tmp = full + ".tmp";
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (sub, bridge, v) in views)
            {
                string prefix = sub == "" ? "" : sub.Trim('/') + "/";
                string file = FileName(bridge, v), name = prefix + file;
                // Two views with the same stem: number the duplicates instead of overwriting.
                for (int k = 2; !names.Add(name); k++) name = prefix + Path.GetFileNameWithoutExtension(file) + $" ({k}).dxf";
                var e = zip.CreateEntry(name, CompressionLevel.Optimal);
                using var w = new StreamWriter(e.Open(), new UTF8Encoding(false));
                Write(w, v, u);
                written.Add(name);
            }
        }
        File.Move(tmp, full, overwrite: true);
        return written;
    }

    /// <summary>Sheets as paper-space DXFs (and optionally the full-size 2D model views in a "models" subfolder) to a folder or a .zip.</summary>
    public static List<string> WriteSheets(string target, string bridgeName, IEnumerable<Sheet> sheets, UnitSystem u, IEnumerable<SheetView>? models = null) =>
        WriteAll(target, sheets.Select(s => ("", bridgeName, ForSheet(s)))
            .Concat((models ?? Enumerable.Empty<SheetView>()).Select(v => ("models", bridgeName, v))), u);
}
