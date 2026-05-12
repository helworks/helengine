using helengine.baseplatform.Definitions;
using helengine.baseplatform.Manifest;
using Xunit;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies first-stage physical layout planning for cooked artifacts.
/// </summary>
public sealed class EditorPlatformLayoutPlanServiceTests {
    [Fact]
    public void Layout_loose_files_creates_one_placement_per_selected_variant() {
        EditorPlatformLayoutPlanService service = new();

        PlatformBuildManifest manifest = service.Plan(
            new PlatformBuildManifest(
                2,
                "project",
                "1.0.0",
                "1.0.0-engine",
                "windows",
                "2026.05.12",
                "Scenes/MainMenu.helen",
                [
                    new PlatformBuildScene(
                        "Scenes/MainMenu.helen",
                        "MainMenu",
                        "Scenes/MainMenu.helen",
                        [
                            new PlatformBuildPayloadReference("cooked/scenes/MainMenu.hasset", "cooked/scenes/MainMenu.hasset")
                        ],
                        [])
                ],
                [],
                [
                    new PlatformBuildArtifact("cooked/scenes/MainMenu.hasset", "scene:main-menu", "sha256:scene", "scene", "shared")
                ],
                Array.Empty<PlatformBuildCodeModule>(),
                Array.Empty<PlatformArtifactPlacement>(),
                new PlatformContainerWritePlan(string.Empty, Array.Empty<PlatformContainerArtifact>())),
            new PlatformStorageProfileDefinition(
                "loose-files",
                "Loose Files",
                PlatformStorageProfileKind.LooseFiles,
                "windows-loose-files",
                false),
            new PlatformMediaProfileDefinition(
                "windows-install-tree",
                "Windows Install Tree",
                PlatformMediaLayoutKind.InstallTree,
                false,
                false));

        Assert.Equal("Scenes/MainMenu.helen", manifest.StartupSceneId);
        Assert.Equal("windows", manifest.PlatformName);
        Assert.Equal("2026.05.12", manifest.PlatformVersion);
        Assert.Single(manifest.ArtifactPlacements);
        Assert.Equal("windows-loose-files", manifest.ContainerWritePlan.RuntimeSpecializationId);
        Assert.Equal("container-0", manifest.ContainerWritePlan.ContainerArtifacts[0].ContainerId);
    }

    [Fact]
    public void Layout_prioritizes_startup_scene_before_other_scene_artifacts() {
        EditorPlatformLayoutPlanService service = new();

        PlatformBuildManifest manifest = service.Plan(
            new PlatformBuildManifest(
                2,
                "project",
                "1.0.0",
                "1.0.0-engine",
                "windows",
                "2026.05.12",
                "Scenes/Menu.helen",
                [
                    new PlatformBuildScene(
                        "Scenes/Menu.helen",
                        "Menu",
                        "Scenes/Menu.helen",
                        [
                            new PlatformBuildPayloadReference("cooked/scenes/Menu.hasset", "cooked/scenes/Menu.hasset")
                        ],
                        [
                            new KeyValuePair<string, string>("build-order-index", "0"),
                            new KeyValuePair<string, string>("cooked-relative-path", "cooked/scenes/Menu.hasset")
                        ]),
                    new PlatformBuildScene(
                        "Scenes/LevelOne.helen",
                        "Level One",
                        "Scenes/LevelOne.helen",
                        [
                            new PlatformBuildPayloadReference("cooked/scenes/LevelOne.hasset", "cooked/scenes/LevelOne.hasset")
                        ],
                        [
                            new KeyValuePair<string, string>("build-order-index", "1"),
                            new KeyValuePair<string, string>("cooked-relative-path", "cooked/scenes/LevelOne.hasset")
                        ])
                ],
                [],
                [
                    new PlatformBuildArtifact("cooked/scenes/LevelOne.hasset", "scene:level-one", "sha256:level", "scene", "shared"),
                    new PlatformBuildArtifact("cooked/scenes/Menu.hasset", "scene:menu", "sha256:menu", "scene", "shared")
                ],
                Array.Empty<PlatformBuildCodeModule>(),
                Array.Empty<PlatformArtifactPlacement>(),
                new PlatformContainerWritePlan(string.Empty, Array.Empty<PlatformContainerArtifact>())),
            new PlatformStorageProfileDefinition(
                "loose-files",
                "Loose Files",
                PlatformStorageProfileKind.LooseFiles,
                "windows-loose-files",
                false),
            new PlatformMediaProfileDefinition(
                "windows-install-tree",
                "Windows Install Tree",
                PlatformMediaLayoutKind.InstallTree,
                false,
                false));

        Assert.Collection(
            manifest.ArtifactPlacements,
            placement => Assert.Equal("scene:menu", placement.LogicalArtifactId),
            placement => Assert.Equal("scene:level-one", placement.LogicalArtifactId));
        Assert.True(manifest.ArtifactPlacements[0].PlacementPriority < manifest.ArtifactPlacements[1].PlacementPriority);
    }
}
