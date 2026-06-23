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
    /// Ensures rooted packaged-path policies emit one rooted canonical runtime path for platforms that allow absolute packaged references.
    /// </summary>
    [Fact]
    public void ResolveRuntimeReferencePath_WhenPlatformUsesRootedPolicy_ReturnsRootedCanonicalPath() {
        RuntimeGenerationContract contract = new RuntimeGenerationContract(
            RuntimeMaterialResolutionMode.CookedPlatformOwned,
            true,
            PackagedPathPolicy.RootedOrContentRelative);

        string resolvedPath = PlatformPackagedAssetPathResolver.ResolveRuntimeReferencePath(
            "ps2",
            contract,
            "cooked/fonts/default.hefont");

        Assert.Equal("/cooked/fonts/default.hefont", resolvedPath);
    }

    /// <summary>
    /// Ensures mixed-case content-relative logical paths fail immediately instead of normalizing implicitly.
    /// </summary>
    [Fact]
    public void ResolveRuntimeReferencePath_WhenContentRelativePathUsesUppercase_ThrowsInvalidOperationException() {
        RuntimeGenerationContract contract = new RuntimeGenerationContract(
            RuntimeMaterialResolutionMode.RawShaderBacked,
            true,
            PackagedPathPolicy.ContentRelativeOnly);

        Assert.Throws<InvalidOperationException>(() => PlatformPackagedAssetPathResolver.ResolveRuntimeReferencePath(
            "windows",
            contract,
            "cooked/Fonts/default.hefont"));
    }
}
