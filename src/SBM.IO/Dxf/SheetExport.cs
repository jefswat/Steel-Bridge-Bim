using SBM.Core.Drawing.Views;
using SBM.Core.Units;

namespace SBM.IO.Dxf;

/// <summary>Writes 2D views as DXF 2D models (one file per view) in the project's units.</summary>
public static class SheetExport
{
    /// <summary>Model-space views are written in feet (US) or meters (SI); paper-space sheets in inches (US) or mm (SI).</summary>
    public static double UnitFor(SheetView v, UnitSystem u) => v.PaperSpace ? (u.IsUS ? 0.0254 : 0.001) : u.LongUnit;

    public static string Write(string folder, string bridgeName, SheetView v, UnitSystem u)
    {
        Directory.CreateDirectory(folder);
        string safe = string.Concat($"{bridgeName} - {v.FileStem}".Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        string path = Path.Combine(folder, safe + ".dxf");
        double mpu = UnitFor(v, u);
        // Linetype patterns are ~2 units long: scale them to about 3/4" on paper.
        double paperIn = 0.75 * 0.0254 * v.Scale;
        DxfWriter.Write(path, v.Drawing, mpu, flat: true, ltScale: paperIn / mpu / 2.0);
        return path;
    }
}
