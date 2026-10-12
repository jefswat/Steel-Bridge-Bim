using SBM.Core.Concrete;
using SBM.Core.Framing;
using SBM.Core.Geometry;
using SBM.Core.Steel;
using SBM.Core.Substructure;
using SBM.Core.Units;

namespace SBM.Core.Drawing.Views;

/// <summary>Scales and choices for the 2D views. Scales are model length per paper length (e.g. 240 = 1" : 20'-0").</summary>
public sealed class DrawingOptions
{
    public double LayoutScale { get; set; } = 240;
    public double SectionScale { get; set; } = 24;
    public double BentScale { get; set; } = 48;
    /// <summary>Station of the typical section; NaN = middle of span 1.</summary>
    public double TypicalSectionStation { get; set; } = double.NaN;
    public bool IncludeRebar { get; set; } = true;

    public static DrawingOptions For(UnitSystem u) => u.IsUS
        ? new DrawingOptions()
        : new DrawingOptions { LayoutScale = 200, SectionScale = 25, BentScale = 50 };

    public static string ScaleText(double scale, UnitSystem u)
    {
        if (!u.IsUS) return $"1:{scale:0}";
        double inchPerFoot = 12 / scale;
        if (inchPerFoot >= 0.124)
        {
            string frac = inchPerFoot switch
            {
                >= 2.99 => "3\"", >= 1.49 => "1 1/2\"", >= 0.99 => "1\"", >= 0.74 => "3/4\"", >= 0.49 => "1/2\"", >= 0.37 => "3/8\"",
                >= 0.24 => "1/4\"", >= 0.18 => "3/16\"", _ => "1/8\"",
            };
            return $"{frac} = 1'-0\"";
        }
        if (Math.Abs(inchPerFoot - 0.0625) < 1e-9) return "1/16\" = 1'-0\"";
        return $"1\" = {scale / 12:0.##}'-0\"";
    }
}

/// <summary>Everything the view builders read: the generated bridge and the terrain.</summary>
public sealed record ViewContext(
    BridgeFraming Framing,
    SteelModel? Steel,
    ConcreteModel? Concrete,
    SubstructureModel? Substructure,
    Func<Vec2, double?>? Ground,
    UnitSystem Units,
    DrawingOptions Options,
    Prestressed.PrestressedModel? Prestressed = null)
{
    /// <summary>Sheet name of a girder: its plan name (<see cref="Bridge.GirderLayoutDefinition.PlanNames"/>) when the model has one, else "G1"...</summary>
    public string GirderName(int girder)
    {
        var names = Framing.Definition.Girders.PlanNames;
        return names != null && girder >= 0 && girder < names.Count && !string.IsNullOrWhiteSpace(names[girder]) ? names[girder] : $"G{girder + 1}";
    }

    /// <summary>"GIRDER A" with plan names, else "G1" (titles).</summary>
    public string GirderTitle(int girder) => Framing.Definition.Girders.PlanNames != null ? $"GIRDER {GirderName(girder)}" : GirderName(girder);
}

/// <summary>One 2D model (view) ready for DXF: drawing in meters, its scale, and whether it is in paper units.</summary>
public sealed record SheetView(string Name, string FileStem, PlanDrawing Drawing, double Scale, string Description, bool PaperSpace = false);
