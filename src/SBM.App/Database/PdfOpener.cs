using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace SBM.App.Database;

/// <summary>
/// Opens a PDF at a page. Windows has no shell verb for "open at page", so this uses a viewer that takes a page argument:
/// Adobe Acrobat / Reader (<c>/A "page=N"</c>), SumatraPDF (<c>-page N</c>) or Microsoft Edge (<c>file:///...#page=N</c>), found through
/// the App Paths registry key or the usual install folders; otherwise the file opens in the default viewer at page 1.
/// </summary>
public static class PdfOpener
{
    /// <summary>Opens <paramref name="pdf"/> at <paramref name="page"/> (1-based). Returns a short description of how it was opened.</summary>
    public static string Open(string pdf, int page)
    {
        if (page <= 1)
        {
            Process.Start(new ProcessStartInfo(pdf) { UseShellExecute = true });
            return "default viewer";
        }
        foreach (var exe in new[] { "Acrobat.exe", "AcroRd32.exe" })
            if (AppPath(exe) is { } a)
            {
                var psi = new ProcessStartInfo(a) { UseShellExecute = false };
                psi.ArgumentList.Add("/A"); psi.ArgumentList.Add($"page={page}"); psi.ArgumentList.Add(pdf);
                Process.Start(psi);
                return Path.GetFileNameWithoutExtension(a) + $" at page {page}";
            }
        if (AppPath("SumatraPDF.exe") is { } su)
        {
            var psi = new ProcessStartInfo(su) { UseShellExecute = false };
            psi.ArgumentList.Add("-page"); psi.ArgumentList.Add(page.ToString(System.Globalization.CultureInfo.InvariantCulture)); psi.ArgumentList.Add(pdf);
            Process.Start(psi);
            return $"SumatraPDF at page {page}";
        }
        if (AppPath("msedge.exe") is { } edge)
        {
            var psi = new ProcessStartInfo(edge) { UseShellExecute = false };
            psi.ArgumentList.Add(new Uri(Path.GetFullPath(pdf)).AbsoluteUri + "#page=" + page);
            Process.Start(psi);
            return $"Edge at page {page}";
        }
        Process.Start(new ProcessStartInfo(pdf) { UseShellExecute = true });
        return $"default viewer (go to page {page})";
    }

    private static string? AppPath(string exe)
    {
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            try
            {
                using var k = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + exe);
                if (k?.GetValue(null) is string p && File.Exists(p.Trim('"'))) return p.Trim('"');
            }
            catch (Exception) { }
        }
        string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string[] guesses = exe switch
        {
            "msedge.exe" => new[] { Path.Combine(pf86, @"Microsoft\Edge\Application\msedge.exe"), Path.Combine(pf, @"Microsoft\Edge\Application\msedge.exe") },
            "SumatraPDF.exe" => new[] { Path.Combine(local, @"SumatraPDF\SumatraPDF.exe"), Path.Combine(pf, @"SumatraPDF\SumatraPDF.exe") },
            "Acrobat.exe" => new[] { Path.Combine(pf, @"Adobe\Acrobat DC\Acrobat\Acrobat.exe") },
            _ => Array.Empty<string>(),
        };
        return guesses.FirstOrDefault(File.Exists);
    }
}
