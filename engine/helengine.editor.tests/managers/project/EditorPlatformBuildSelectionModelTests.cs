using helengine.baseplatform.Definitions;
using helengine.editor;

namespace helengine.editor.tests.managers.project;

public class EditorPlatformBuildSelectionModelTests {
    [Fact]
    public void From_copies_storage_and_media_profiles() {
        PlatformDefinition definition = new(
            "windows",
            "Windows",
            [],
            [],
            [],
            [],
            [],
            [
                new PlatformStorageProfileDefinition(
                    "loose-files",
                    "Loose Files",
                    PlatformStorageProfileKind.LooseFiles,
                    "windows-loose-files",
                    false)
            ],
            [
                new PlatformMediaProfileDefinition(
                    "windows-install-tree",
                    "Windows Install Tree",
                    PlatformMediaLayoutKind.InstallTree,
                    true,
                    false)
            ]);

        EditorPlatformBuildSelectionModel selectionModel = EditorPlatformBuildSelectionModel.From(definition);

        Assert.Single(selectionModel.StorageProfiles);
        Assert.Equal("loose-files", selectionModel.ResolveStorageProfile(string.Empty)?.ProfileId);
        Assert.Equal("windows-install-tree", selectionModel.ResolveMediaProfile(string.Empty)?.ProfileId);
    }
}
