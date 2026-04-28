using System.Linq;
using helengine.editor.launcher.Models;
using helengine.editor.launcher.Services;
using Xunit;

namespace helengine.editor.launcher.tests;

/// <summary>
/// Verifies the mocked platform catalog exposes stable engine and platform dependency data for launcher planning.
/// </summary>
public sealed class MockEnginePlatformCatalogTests {
    /// <summary>
    /// Ensures the mocked catalog exposes known engine versions with installable platform requirements.
    /// </summary>
    [Fact]
    public void GetAvailableEngines_WhenCalled_ReturnsKnownEngineVersionsAndPlatforms() {
        MockEnginePlatformCatalog catalog = new MockEnginePlatformCatalog();

        EngineCatalogEntry engine = Assert.Single(catalog.GetAvailableEngines().Where(item => item.EngineVersion == "1.2.3"));
        Assert.Contains(engine.PlatformRequirements, item => item.PlatformId == "android");
        Assert.Contains(engine.PlatformRequirements, item => item.PlatformId == "windows");
    }

    /// <summary>
    /// Ensures the mocked catalog surfaces exact reusable artifact identities for each platform requirement.
    /// </summary>
    [Fact]
    public void GetAvailableEngines_WhenKnownPlatformExists_ExposesExactArtifactIdentities() {
        MockEnginePlatformCatalog catalog = new MockEnginePlatformCatalog();

        EngineCatalogEntry engine = Assert.Single(catalog.GetAvailableEngines().Where(item => item.EngineVersion == "1.2.3"));
        EnginePlatformRequirement platform = Assert.Single(engine.PlatformRequirements.Where(item => item.PlatformId == "android"));
        Assert.Equal("android-sdk", platform.Sdk.Identity.Id);
        Assert.Equal("34.0", platform.Sdk.Identity.Version);
        Assert.Equal(PlatformArtifactKind.PlatformBuilder, platform.PlatformBuilder.Identity.Kind);
        Assert.Equal(PlatformArtifactKind.PlatformFiles, platform.PlatformFiles.Identity.Kind);
    }
}
