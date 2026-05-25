using helengine.baseplatform.Definitions;
using helengine.baseplatform.Paths;
using Xunit;

namespace helengine.baseplatform.tests.Paths;

/// <summary>
/// Verifies shared packaged asset paths are emitted in the runtime form required by each platform contract.
/// </summary>
public class PlatformPackagedAssetPathResolverTests {
    /// <summary>
    /// Ensures content-relative platforms preserve normalized cooked paths.
    /// </summary>
    [Fact]
    public void ResolveRuntimeReferencePath_WhenPlatformUsesContentRelativePolicy_ReturnsNormalizedRelativePath() {
        RuntimeGenerationContract contract = new RuntimeGenerationContract(
            RuntimeMaterialResolutionMode.RawShaderBacked,
            true,
            PackagedPathPolicy.ContentRelativeOnly);

        string resolvedPath = PlatformPackagedAssetPathResolver.ResolveRuntimeReferencePath(
            "windows",
            contract,
            "cooked\\fonts\\default.hefont");

        Assert.Equal("cooked/fonts/default.hefont", resolvedPath);
    }

    /// <summary>
    /// Ensures PS2-rooted platforms emit deterministic disc runtime paths instead of logical cooked-relative paths.
    /// </summary>
    [Fact]
    public void ResolveRuntimeReferencePath_WhenPs2PlatformUsesRootedPolicy_ReturnsPs2RuntimePath() {
        RuntimeGenerationContract contract = new RuntimeGenerationContract(
            RuntimeMaterialResolutionMode.CookedPlatformOwned,
            true,
            PackagedPathPolicy.RootedOrContentRelative);

        string resolvedPath = PlatformPackagedAssetPathResolver.ResolveRuntimeReferencePath(
            "ps2",
            contract,
            "cooked/fonts/default.hefont");

        Assert.Equal("cdrom0:\\COOKED\\FONTS\\DEFAULT.HEF;1", resolvedPath);
    }
}
