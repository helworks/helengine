using helengine.baseplatform.Definitions;

namespace helengine.baseplatform.tests.Definitions;

public class PlatformStorageProfileDefinitionTests {
    [Fact]
    public void PlatformStorageProfileDefinition_preserves_storage_profile_metadata() {
        PlatformStorageProfileDefinition definition = new(
            "loose-files",
            "Loose Files",
            PlatformStorageProfileKind.LooseFiles,
            "windows-loose-files",
            false);

        Assert.Equal("loose-files", definition.ProfileId);
        Assert.Equal("Loose Files", definition.DisplayName);
        Assert.Equal(PlatformStorageProfileKind.LooseFiles, definition.StorageKind);
        Assert.Equal("windows-loose-files", definition.RuntimeSpecializationId);
        Assert.False(definition.AllowContainerSegmentation);
    }
}
