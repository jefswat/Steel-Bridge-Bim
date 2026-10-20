using SBM.Core.Project;
using Xunit;

namespace SBM.Tests;

public class ReferencePathTests
{
    [Fact]
    public void MissingAbsolutePath_IsFoundUnderTheProjectFolders()
    {
        string root = Path.Combine(Path.GetTempPath(), "sbm-refpath-" + Guid.NewGuid().ToString("N"));
        try
        {
            string model = Path.Combine(root, "samples", "plans", "M.sbm");
            string dem = Path.Combine(root, "dist", "terrain", "ptc", "dem.flt");
            Directory.CreateDirectory(Path.GetDirectoryName(model)!);
            Directory.CreateDirectory(Path.GetDirectoryName(dem)!);
            File.WriteAllText(model, "{}");
            File.WriteAllText(dem, "x");

            // stored on another machine (sandbox): C:\SBM\dist\terrain\ptc\dem.flt
            string stored = @"Z:\NoSuchRoot\dist\terrain\ptc\dem.flt";
            Assert.Equal(Path.GetFullPath(dem), Path.GetFullPath(BridgeProject.ResolvePath(stored, model)));

            // existing absolute path and relative path keep their old behaviour; nothing found = unchanged
            Assert.Equal(dem, BridgeProject.ResolvePath(dem, model));
            Assert.Equal(Path.GetFullPath(dem), BridgeProject.ResolvePath(@"..\..\dist\terrain\ptc\dem.flt", model));
            Assert.Equal(@"Z:\NoSuchRoot\other.flt", BridgeProject.ResolvePath(@"Z:\NoSuchRoot\other.flt", model));
        }
        finally { try { Directory.Delete(root, true); } catch { } }
    }
}
