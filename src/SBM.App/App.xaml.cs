using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Interop;
using System.Windows.Threading;

namespace SBM.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // Engineering input is always invariant (decimal point), regardless of Windows locale.
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        DispatcherUnhandledException += OnUnhandled;
        // Dark title bars on every window so the caption text is never dark-on-dark (Windows in light mode).
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler((s, _) => DarkTitleBar((Window)s)));
        base.OnStartup(e);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private static void DarkTitleBar(Window w)
    {
        // Light default text for anything inside that doesn't set its own color (custom templates otherwise fall back to black).
        if (w.ReadLocalValue(Control.ForegroundProperty) == DependencyProperty.UnsetValue)
            w.Foreground = new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xE6));
        var hwnd = new WindowInteropHelper(w).Handle;
        if (hwnd == IntPtr.Zero) return;
        int on = 1;
        DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE
    }

    private static void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ViewModels.MainViewModel.Log("UNHANDLED: " + e.Exception);
        MessageBox.Show(e.Exception.ToString(), "Unexpected error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
