using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace SBM.IO.Database;

/// <summary>Where an asset file is: a local file in the assets folder, or (later) a URL / permalink.</summary>
public sealed record AssetLocation(string Kind, string Location, string? LocalPath, Uri? Url)
{
    public bool IsFile => Kind == "file";
    /// <summary>The local file exists (always false for a URL: not checked here).</summary>
    public bool Exists => LocalPath != null && File.Exists(LocalPath);
    /// <summary>What a viewer opens: the local path or the URL text.</summary>
    public string Target => LocalPath ?? Url?.AbsoluteUri ?? Location;
}

/// <summary>State of a stored file vs its metadata (<see cref="AssetStore.Check"/>).</summary>
public enum AssetFileState { Ok, Missing, SizeChanged, HashChanged, Url }

/// <summary>
/// The assets folder of a companion database: photos, documents and calc PDFs are COPIED into
/// <c>&lt;database name&gt;_assets\{photos,documents,calcs,thumbnails}</c> (beside the database; for the default database that is
/// <c>&lt;model&gt;_assets</c> beside the .sbm) and named by content hash (first 16 hex digits of the SHA-256 + the original extension), so a
/// rename of the original never breaks a link and the same file added twice is stored once. The database keeps the location RELATIVE to the
/// assets folder (forward slashes), so the model, database and assets folder move together. The folder can be redirected with
/// <c>meta.assets_folder</c> (relative to the database folder, or absolute).
/// </summary>
public static class AssetStore
{
    public static readonly string[] Subfolders = { "photos", "documents", "calcs", "thumbnails" };

    /// <summary>Default assets folder of a database file: <c>&lt;db folder&gt;\&lt;db name&gt;_assets</c>.</summary>
    public static string DefaultFolder(string dbPath) =>
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dbPath))!, Path.GetFileNameWithoutExtension(dbPath) + "_assets");

    /// <summary>The ONE function that turns a stored reference into something to open: 'file' = path relative to the assets folder, 'url' = a
    /// permalink (http / https / file URI) used as is.</summary>
    public static AssetLocation Resolve(string assetsFolder, string location, string locationKind)
    {
        if (locationKind == "url")
            return new AssetLocation("url", location, null, Uri.TryCreate(location, UriKind.Absolute, out var u) ? u : null);
        string rel = location.Replace('/', Path.DirectorySeparatorChar);
        string full = Path.GetFullPath(Path.IsPathRooted(rel) ? rel : Path.Combine(assetsFolder, rel));
        return new AssetLocation("file", location, full, null);
    }

    public static string Sha256(string file)
    {
        using var fs = File.OpenRead(file);
        return Convert.ToHexStringLower(SHA256.HashData(fs));
    }

    /// <summary>Copies <paramref name="source"/> into the assets folder (subfolder by kind), named by content hash. Returns the relative
    /// location, sha256 and size. An identical file already there is reused.</summary>
    public static (string location, string sha256, long size) Import(string assetsFolder, string kind, string source)
    {
        if (!File.Exists(source)) throw new FileNotFoundException($"File not found: {source}");
        string sub = kind switch { "photo" => "photos", "document" => "documents", "calc" => "calcs", _ => throw new ArgumentException($"Unknown asset kind '{kind}'.") };
        string sha = Sha256(source);
        string ext = Path.GetExtension(source).ToLowerInvariant();
        string rel = $"{sub}/{sha[..16]}{ext}";
        string dest = Path.Combine(assetsFolder, sub, sha[..16] + ext);
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        if (!File.Exists(dest) || new FileInfo(dest).Length != new FileInfo(source).Length || Sha256(dest) != sha)
            File.Copy(source, dest, overwrite: true);
        return (rel, sha, new FileInfo(source).Length);
    }

    /// <summary>Checks a stored file against its metadata: size always, the SHA-256 only when <paramref name="hash"/> (slower).</summary>
    public static AssetFileState Check(AssetLocation loc, long? size, string? sha, bool hash)
    {
        if (!loc.IsFile) return AssetFileState.Url;
        if (!loc.Exists) return AssetFileState.Missing;
        if (size is { } s && new FileInfo(loc.LocalPath!).Length != s) return AssetFileState.SizeChanged;
        if (hash && !string.IsNullOrEmpty(sha) && Sha256(loc.LocalPath!) != sha) return AssetFileState.HashChanged;
        return AssetFileState.Ok;
    }

    public static string MediaType(string file) => Path.GetExtension(file).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        ".tif" or ".tiff" => "image/tiff",
        ".webp" => "image/webp",
        ".heic" => "image/heic",
        ".pdf" => "application/pdf",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".csv" => "text/csv",
        ".txt" => "text/plain",
        ".mp4" => "video/mp4",
        ".mov" => "video/quicktime",
        ".dwg" => "application/acad",
        ".dxf" => "image/vnd.dxf",
        ".ifc" => "application/x-step",
        _ => "application/octet-stream",
    };

    public static bool IsImage(string mediaType) => mediaType.StartsWith("image/", StringComparison.Ordinal);

    /// <summary>EXIF DateTimeOriginal (else DateTime) of a JPEG as "yyyy-MM-ddTHH:mm:ss"; null when there is none.</summary>
    public static string? ExifDate(string file)
    {
        try
        {
            using var fs = File.OpenRead(file);
            var br = new BinaryReader(fs);
            if (br.ReadByte() != 0xFF || br.ReadByte() != 0xD8) return null;
            while (fs.Position < fs.Length - 4)
            {
                if (br.ReadByte() != 0xFF) return null;
                byte marker = br.ReadByte();
                int len = (br.ReadByte() << 8) | br.ReadByte();
                if (marker == 0xE1)
                {
                    var seg = br.ReadBytes(len - 2);
                    if (seg.Length < 14 || seg[0] != 'E' || seg[1] != 'x' || seg[2] != 'i' || seg[3] != 'f') continue;
                    return TiffDate(seg.AsSpan(6).ToArray());
                }
                if (marker is 0xDA or 0xD9) return null;
                fs.Seek(len - 2, SeekOrigin.Current);
            }
        }
        catch (Exception) { }
        return null;
    }

    private static string? TiffDate(byte[] t)
    {
        bool le = t[0] == 'I';
        int U16(int o) => le ? t[o] | t[o + 1] << 8 : t[o] << 8 | t[o + 1];
        int U32(int o) => le ? t[o] | t[o + 1] << 8 | t[o + 2] << 16 | t[o + 3] << 24 : t[o] << 24 | t[o + 1] << 16 | t[o + 2] << 8 | t[o + 3];
        string? Find(int ifd, int tag, out int exifPtr)
        {
            exifPtr = 0;
            string? found = null;
            if (ifd <= 0 || ifd + 2 > t.Length) return null;
            int n = U16(ifd);
            for (int i = 0; i < n; i++)
            {
                int e = ifd + 2 + 12 * i;
                if (e + 12 > t.Length) break;
                int tg = U16(e);
                if (tg == 0x8769) exifPtr = U32(e + 8);
                if (tg == tag)
                {
                    int cnt = U32(e + 4), off = U32(e + 8);
                    if (off > 0 && off + cnt <= t.Length) found = System.Text.Encoding.ASCII.GetString(t, off, Math.Max(0, cnt - 1));
                }
            }
            return found;
        }
        int ifd0 = U32(4);
        string? dt = Find(ifd0, 0x0132, out int exif);
        string? orig = exif > 0 ? Find(exif, 0x9003, out _) : null;
        string? s = orig ?? dt;
        return s != null && DateTime.TryParseExact(s.Trim(), "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d.ToString("s", CultureInfo.InvariantCulture) : null;
    }

    // ------------------------------------------------------------------ PyMuPDF helper (tools/asset_tool.py)

    /// <summary>Runs <c>python -I tools/asset_tool.py</c> (PyMuPDF) and returns stdout; null when python / the tool / PyMuPDF is not available.</summary>
    public static string? RunTool(Action<string>? log, params string[] args)
    {
        string? script = CompanionDatabase.FindScript("asset_tool.py");
        if (script == null) { log?.Invoke("tools/asset_tool.py not found (thumbnails / PDF text need it)."); return null; }
        try
        {
            var psi = new ProcessStartInfo("python") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = System.Text.Encoding.UTF8 };
            psi.ArgumentList.Add("-I"); psi.ArgumentList.Add(script);
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            if (p == null) return null;
            var errTask = p.StandardError.ReadToEndAsync();
            string o = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            string e = errTask.Result;
            if (p.ExitCode != 0) { log?.Invoke($"asset_tool.py {args.FirstOrDefault()} failed: {e.Trim()}"); return null; }
            return o;
        }
        catch (Exception ex) { log?.Invoke("python not available (" + ex.Message + "): no thumbnail / PDF text."); return null; }
    }

    /// <summary>Writes a PNG thumbnail (longest side <paramref name="size"/> px) of an image or of a PDF page into thumbnails/; returns its
    /// relative location or null (no python / unsupported type).</summary>
    public static string? MakeThumbnail(string assetsFolder, string sourceFile, string sha256, int page = 1, int size = 320, Action<string>? log = null)
    {
        string rel = $"thumbnails/{sha256[..16]}{(page > 1 ? "_p" + page : "")}.png";
        string dest = Path.Combine(assetsFolder, "thumbnails", Path.GetFileName(rel));
        if (File.Exists(dest)) return rel;
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        return RunTool(log, "thumb", sourceFile, dest, "--size", size.ToString(CultureInfo.InvariantCulture), "--page", page.ToString(CultureInfo.InvariantCulture)) != null && File.Exists(dest) ? rel : null;
    }

    /// <summary>Page count and the text of the given pages of a PDF (PyMuPDF). Null when python / PyMuPDF is not available.</summary>
    public static (int pages, string text)? PdfText(string pdf, IReadOnlyList<int> pages, Action<string>? log = null)
    {
        string? o = RunTool(log, "pdftext", pdf, "--pages", string.Join(",", pages));
        if (o == null) return null;
        using var j = JsonDocument.Parse(o);
        return (j.RootElement.GetProperty("page_count").GetInt32(), j.RootElement.GetProperty("text").GetString() ?? "");
    }

    /// <summary>Parses "1-2", "1,3,5-6" into page numbers (1-based).</summary>
    public static List<int> ParsePages(string text)
    {
        var list = new List<int>();
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var ab = part.Split('-', StringSplitOptions.TrimEntries);
            if (!int.TryParse(ab[0], out int a) || a < 1) throw new ArgumentException($"Bad page range '{text}' (e.g. 1-2 or 1,3).");
            int b = ab.Length > 1 && int.TryParse(ab[1], out int bb) ? bb : a;
            if (b < a) throw new ArgumentException($"Bad page range '{text}'.");
            for (int i = a; i <= b; i++) if (!list.Contains(i)) list.Add(i);
        }
        if (list.Count == 0) throw new ArgumentException("No pages given (e.g. --summary 1-2).");
        return list;
    }
}
