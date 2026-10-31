using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SBM.Core.Geometry;

namespace SBM.App;

/// <summary>
/// North arrow overlay in the model viewport (top right, below the navigation buttons). It points to GRID north = project +Y, the
/// direction the plan coordinates (LandXML / state plane / UTM) are given in and the compass views use. It lies on the ground plane, so
/// it turns with the plan rotation and is foreshortened in 3D like the ground. True north (grid convergence) is not drawn: the project
/// georeference has no convergence angle. Without a georeference the label says the +Y axis is a local grid's.
/// </summary>
public partial class MainWindow
{
    private bool _northArrowOn = true;
    private Polygon? _naLeft, _naRight;
    private Polyline? _naRing;
    private TextBlock? _naLabel, _naNote;

    private void InitNorthArrow()
    {
        var ink = new SolidColorBrush(Color.FromRgb(235, 235, 235));
        var dark = new SolidColorBrush(Color.FromRgb(70, 70, 70));
        _naRing = new Polyline { Stroke = new SolidColorBrush(Color.FromArgb(150, 235, 235, 235)), StrokeThickness = 1 };
        _naLeft = new Polygon { Fill = ink, Stroke = ink, StrokeThickness = 1, StrokeLineJoin = PenLineJoin.Round };
        _naRight = new Polygon { Fill = dark, Stroke = ink, StrokeThickness = 1, StrokeLineJoin = PenLineJoin.Round };
        _naLabel = new TextBlock { Text = "N", Foreground = ink, FontWeight = FontWeights.Bold, FontSize = 15 };
        _naNote = new TextBlock { Foreground = new SolidColorBrush(Color.FromArgb(190, 235, 235, 235)), FontSize = 10, Width = 84, TextAlignment = TextAlignment.Center };
        foreach (UIElement c in new UIElement[] { _naRing, _naRight, _naLeft, _naLabel, _naNote }) NorthArrowCanvas.Children.Add(c);
        View.CameraChanged += (_, _) => UpdateNorthArrow();
        UpdateNorthArrow();
    }

    /// <summary>Redraws the arrow from the current camera (cheap: a few points).</summary>
    private void UpdateNorthArrow()
    {
        if (_naLeft == null) return;
        bool show = _northArrowOn && !SheetsMode && CurrentView() is not null;
        NorthArrowOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (!show) return;
        var ((nx, ny), (ex, ey)) = ViewNav.NorthArrow(CurrentView()!.Value);
        const double cx = 42, cy = 46, R = 24;
        // a plan point (east, north) in arrow units -> overlay pixels
        Point P(double e, double n) => new(cx + R * (e * ex + n * nx), cy + R * (e * ey + n * ny));
        var ring = new PointCollection();
        for (int i = 0; i <= 48; i++) { double a = i * Math.PI / 24; ring.Add(P(0.62 * Math.Cos(a), 0.62 * Math.Sin(a))); }
        _naRing!.Points = ring;
        // two-tone arrow: left half filled light, right half dark (reads the same on dark terrain and light sky)
        _naLeft.Points = new PointCollection { P(0, 1), P(-0.36, -0.62), P(0, -0.32) };
        _naRight!.Points = new PointCollection { P(0, 1), P(0.36, -0.62), P(0, -0.32) };
        // "N" upright just beyond the tip along the arrow's screen direction (centred when looking level to the north)
        double len = Math.Sqrt(nx * nx + ny * ny), ux = len > 1e-6 ? nx / len : 0, uy = len > 1e-6 ? ny / len : -1;
        var tip = P(0, 1);
        Canvas.SetLeft(_naLabel!, tip.X + ux * 11 - 5);
        Canvas.SetTop(_naLabel!, tip.Y + uy * 11 - 10);
        _naNote!.Text = _vm.Project.Georeference != null ? "grid N" : "local grid +Y";
        Canvas.SetLeft(_naNote, 0);
        Canvas.SetTop(_naNote, 86);
    }
}
