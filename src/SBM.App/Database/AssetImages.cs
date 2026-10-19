using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using SBM.IO.Database;

namespace SBM.App.Database;

/// <summary>Thumbnails and opening of asset files in the app (photos decoded by WPF, so python is not needed for photo thumbnails).</summary>
public static class AssetImages
{
    /// <summary>Loads an image file without locking it, decoded to <paramref name="width"/> px (0 = full size). Null when it cannot be read.</summary>
    public static BitmapImage? Load(string file, int width = 0)
    {
        try
        {
            if (!File.Exists(file)) return null;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            if (width > 0) bmp.DecodePixelWidth = width;
            bmp.UriSource = new Uri(file);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch (Exception) { return null; }
    }

    /// <summary>The thumbnail of a photo: the stored one, else one made now with WPF (written to thumbnails/ and recorded). Null for non-images
    /// or missing files.</summary>
    public static BitmapSource? Thumbnail(ModelDb db, AssetFile f, int width = 160)
    {
        if (db.ThumbnailPath(f) is { } t && Load(t, width) is { } bt) return bt;
        if (!f.IsImage) return null;
        var loc = db.ResolveAsset(f);
        if (!loc.Exists) return null;
        var img = Load(loc.LocalPath!, 320);
        if (img == null) return null;
        try
        {
            string rel = $"thumbnails/{(f.Sha256.Length >= 16 ? f.Sha256[..16] : "f" + f.Id)}.png";
            string dest = Path.Combine(db.AssetsFolder, "thumbnails", Path.GetFileName(rel));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(img));
            using (var fs = File.Create(dest)) enc.Save(fs);
            db.SetThumbnail(f.Id, rel);
        }
        catch (Exception ex) { ViewModels.MainViewModel.Log("DB: thumbnail failed: " + ex.Message); }
        return img;
    }

    /// <summary>Opens an asset: images in the app's preview window, PDFs in the plan sheet style viewer (external reader), anything else with
    /// the program Windows associates with it; URLs in the browser.</summary>
    public static void Open(Window owner, ModelDb db, AssetFile f, int page = 1)
    {
        var loc = db.ResolveAsset(f);
        if (loc.IsFile && !loc.Exists)
        {
            MessageBox.Show(owner, $"The file is missing: {loc.LocalPath}\n(The record is kept; restore the file to the assets folder.)", f.FileName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            if (f.IsImage && loc.IsFile)
                PlanPreviewWindow.ShowSheet(owner, (f.Caption.Length > 0 ? f.Caption + " · " : "") + f.FileName + (f.TakenAt.Length > 0 ? " · " + f.TakenAt : ""), loc.LocalPath!, "", 1);
            else if (f.MediaType == "application/pdf" && loc.IsFile) PdfOpener.Open(loc.LocalPath!, page);
            else Process.Start(new ProcessStartInfo(loc.Target) { UseShellExecute = true });
        }
        catch (Exception ex) { MessageBox.Show(owner, ex.Message, f.FileName, MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}
