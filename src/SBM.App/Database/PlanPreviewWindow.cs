using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SBM.App.Database;

/// <summary>Shows the rendered preview of one plan page (scroll + Ctrl-wheel zoom) with a button to open the single-page PDF.</summary>
public sealed class PlanPreviewWindow : Window
{
    private readonly Image _image = new() { Stretch = Stretch.None };
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly ScrollViewer _scroll;
    private readonly TextBlock _title = new() { FontWeight = FontWeights.SemiBold, Margin = new Thickness(8, 6, 8, 6), TextTrimming = TextTrimming.CharacterEllipsis };
    private string _pdf = "";

    private static PlanPreviewWindow? _instance;

    /// <summary>Shows (reusing one window) the preview image of a sheet.</summary>
    public static void ShowSheet(Window owner, string title, string previewFile, string pdfFile)
    {
        if (_instance == null || !_instance.IsLoaded) _instance = new PlanPreviewWindow { Owner = owner };
        _instance.Load(title, previewFile, pdfFile);
        _instance.Show();
        _instance.Activate();
    }

    private PlanPreviewWindow()
    {
        Title = "Plan sheet";
        Width = 1100; Height = 760;
        Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x1F));
        _image.LayoutTransform = _scale;
        _scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _image };
        var open = new Button { Content = "Open PDF page", Margin = new Thickness(8, 4, 8, 4), Padding = new Thickness(10, 2, 10, 2) };
        open.Click += (_, _) => { if (_pdf.Length > 0 && File.Exists(_pdf)) Process.Start(new ProcessStartInfo(_pdf) { UseShellExecute = true }); };
        var fit = new Button { Content = "Fit", Margin = new Thickness(0, 4, 8, 4), Padding = new Thickness(10, 2, 10, 2) };
        fit.Click += (_, _) => Fit();
        var bar = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(open, Dock.Right); DockPanel.SetDock(fit, Dock.Right);
        bar.Children.Add(open); bar.Children.Add(fit); bar.Children.Add(_title);
        var root = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        root.Children.Add(bar); root.Children.Add(_scroll);
        Content = root;
        PreviewMouseWheel += (_, e) =>
        {
            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            double f = e.Delta > 0 ? 1.15 : 1 / 1.15;
            double s = Math.Clamp(_scale.ScaleX * f, 0.1, 6);
            _scale.ScaleX = _scale.ScaleY = s;
            e.Handled = true;
        };
        SizeChanged += (_, _) => { if (_fitted) Fit(); };
    }

    private bool _fitted = true;

    private void Fit()
    {
        _fitted = true;
        if (_image.Source is not BitmapSource bs) return;
        double s = Math.Min((_scroll.ActualWidth - 20) / bs.PixelWidth, (_scroll.ActualHeight - 20) / bs.PixelHeight);
        if (s > 0) _scale.ScaleX = _scale.ScaleY = s;
    }

    private void Load(string title, string previewFile, string pdfFile)
    {
        _title.Text = title;
        _pdf = pdfFile;
        Title = title;
        _image.Source = null;
        if (File.Exists(previewFile))
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(previewFile);
            bmp.EndInit();
            _image.Source = bmp;
            Dispatcher.BeginInvoke(Fit, System.Windows.Threading.DispatcherPriority.Loaded);
        }
        else _title.Text = title + "  (no preview image: run 'sbm db ... --pages')";
    }
}
