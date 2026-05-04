using helengine.baseplatform.Definitions;

namespace helengine.baseplatform.tests.Definitions;

/// <summary>
/// Verifies the typed media profile metadata model preserves layout and duplication flags.
/// </summary>
public sealed class PlatformMediaProfileDefinitionTests {
    /// <summary>
    /// Verifies a media profile retains layout kind and duplication preference metadata.
    /// </summary>
    [Fact]
    public void PlatformMediaProfileDefinition_preserves_metadata() {
        PlatformMediaProfileDefinition definition = new(
            "install-tree",
            "Install Tree",
            PlatformMediaLayoutKind.InstallTree,
            true,
            false);

        Assert.Equal("install-tree", definition.ProfileId);
        Assert.Equal("Install Tree", definition.DisplayName);
        Assert.Equal(PlatformMediaLayoutKind.InstallTree, definition.LayoutKind);
        Assert.True(definition.AllowPhysicalDuplication);
        Assert.False(definition.PreferLocalityOverDeduplication);
    }
}
