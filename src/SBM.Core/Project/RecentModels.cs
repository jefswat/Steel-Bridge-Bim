using System.Text.Json;

namespace SBM.Core.Project;

/// <summary>One entry of the recent-models list: full path and when it was last opened or saved (UTC).</summary>
public sealed class RecentModel
{
    public string Path { get; set; } = "";
    public DateTime LastOpenedUtc { get; set; }

    public string FileName => System.IO.Path.GetFileName(Path);
    public string Folder => System.IO.Path.GetDirectoryName(Path) ?? "";
}

/// <summary>
/// Most recently opened / saved model files (app setting, not saved in the project): newest first, at most <see cref="Max"/>,
/// one entry per file (full path, case-insensitive). Missing files stay in the list (shown greyed) until removed.
/// Stored as JSON in <c>%LOCALAPPDATA%\SteelBridgeModeler\recent-models.json</c> by the app.
/// </summary>
public sealed class RecentModelList
{
    public const int Max = 10;

    public List<RecentModel> Items { get; set; } = new();

    /// <summary>Records that <paramref name="path"/> was opened or saved at <paramref name="utc"/>: moves it to the top (one entry per file).</summary>
    public void Touch(string path, DateTime utc)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        string full = Full(path);
        Items.RemoveAll(i => Same(i.Path, full));
        Items.Insert(0, new RecentModel { Path = full, LastOpenedUtc = utc });
        Normalize();
    }

    public bool Remove(string path)
    {
        string full = Full(path);
        return Items.RemoveAll(i => Same(i.Path, full)) > 0;
    }

    public void Clear() => Items.Clear();

    /// <summary>Removes every entry whose file does not exist.</summary>
    public int RemoveMissing(Func<string, bool> exists) => Items.RemoveAll(i => !exists(i.Path));

    /// <summary>Entries newest first with whether the file exists (missing files are greyed in the UI).</summary>
    public IReadOnlyList<(RecentModel item, bool exists)> WithStatus(Func<string, bool> exists) =>
        Items.Select(i => (i, exists(i.Path))).ToList();

    /// <summary>Sorts newest first (stable), drops empty paths and duplicates (keeping the newest), trims to <see cref="Max"/>.</summary>
    public void Normalize()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Items = Items.Where(i => !string.IsNullOrWhiteSpace(i.Path))
            .Select((i, k) => (i, k)).OrderByDescending(x => x.i.LastOpenedUtc).ThenBy(x => x.k).Select(x => x.i)
            .Where(i => seen.Add(Full(i.Path)))
            .Take(Max).ToList();
    }

    public static RecentModelList Load(string file)
    {
        try
        {
            if (File.Exists(file))
            {
                var list = JsonSerializer.Deserialize<RecentModelList>(File.ReadAllText(file)) ?? new();
                list.Items ??= new();
                list.Normalize();
                return list;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        return new RecentModelList();
    }

    public void Save(string file)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
        File.WriteAllText(file, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>"Today 14:05", "Yesterday 09:12", "Mon 3 Oct 16:40" (this year) or "3 Oct 2025" (local time).</summary>
    public static string Describe(DateTime utc, DateTime nowUtc)
    {
        var t = utc.ToLocalTime();
        var now = nowUtc.ToLocalTime();
        var ci = System.Globalization.CultureInfo.CurrentCulture;
        if (t.Date == now.Date) return "Today " + t.ToString("HH:mm", ci);
        if (t.Date == now.Date.AddDays(-1)) return "Yesterday " + t.ToString("HH:mm", ci);
        if (t.Year == now.Year) return t.ToString("ddd d MMM HH:mm", ci);
        return t.ToString("d MMM yyyy", ci);
    }

    private static string Full(string path)
    {
        try { return System.IO.Path.GetFullPath(path); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return path; }
    }

    private static bool Same(string a, string b) => string.Equals(Full(a), b, StringComparison.OrdinalIgnoreCase);
}
