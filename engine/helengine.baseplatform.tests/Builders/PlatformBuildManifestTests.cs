using helengine.baseplatform.Manifest;

namespace helengine.baseplatform.tests.Builders;

public class PlatformBuildManifestTests {
    [Fact]
    public void PlatformBuildManifest_preserves_platform_cook_work_items() {
        PlatformCookWorkItem workItem = new(
            "gc-texture:logo",
            "Images/Menu/helengine-logo.png",
            "texture",
            "gamecube",
            "runtime-texture",
            "cooked/imported/helengine-logo.gc.hasset",
            "texture:helengine-logo",
            "sha256:source",
            "sha256:settings",
            "{\"colorFormat\":\"GxRgb5A3\",\"maxResolution\":256}",
            [new PlatformCookWorkItemMetadata("source-asset-id", "Images/Menu/helengine-logo.png")]);

        PlatformBuildManifest manifest = new(
            1,
            "city",
            "1.0.0",
            "1.0.0-engine",
            "gamecube",
            "1.0.0",
            "Scenes/DemoDiscMainMenu.helen",
            Array.Empty<PlatformBuildScene>(),
            Array.Empty<PlatformBuildAsset>(),
            Array.Empty<PlatformBuildArtifact>(),
            Array.Empty<PlatformBuildCodeModule>(),
            Array.Empty<PlatformArtifactPlacement>(),
            new PlatformContainerWritePlan(string.Empty, Array.Empty<PlatformContainerArtifact>()),
            [workItem]);

        Assert.Single(manifest.PlatformCookWorkItems);
        Assert.Equal("gc-texture:logo", manifest.PlatformCookWorkItems[0].WorkItemId);
        Assert.Equal("texture", manifest.PlatformCookWorkItems[0].SourceAssetKind);
        Assert.Equal("runtime-texture", manifest.PlatformCookWorkItems[0].TargetArtifactKind);
    }

    [Fact]
    public void PlatformBuildManifest_preserves_artifact_placements_and_container_plan() {
        PlatformBuildManifest manifest = new(
            3,
            "game",
            "1.0.0",
            "1.0.0-engine",
            "main-menu",
            [
                new PlatformBuildScene(
                    "main-menu",
                    "Main Menu",
                    "scenes/main-menu",
                    [
                        new PlatformBuildPayloadReference("scenes/main-menu", "scenes/main-menu")
                    ],
                    [
                        new KeyValuePair<string, string>("build-order-index", "0")
                    ])
            ],
            [],
            [
                new PlatformBuildArtifact("fonts/default.hasset", "font:default", "sha256:abc", "font", "shared")
            ],
            [
                new PlatformBuildCodeModule("gameplay", "gameplay", "windows-loose-files", ["always-loaded"], ["core"])
            ],
            [
                new PlatformArtifactPlacement("font:default", "shared", "container-0", 0, 4096, 0, 0)
            ],
            new PlatformContainerWritePlan(
                "windows-loose-files",
                [
                    new PlatformContainerArtifact("container-0", "install-tree", 0)
                ]));

        Assert.Equal("main-menu", manifest.StartupSceneId);
        Assert.Single(manifest.CookedArtifacts);
        Assert.Single(manifest.CodeModules);
        Assert.Single(manifest.ArtifactPlacements);
        Assert.NotNull(manifest.ContainerWritePlan);
        Assert.Equal("windows-loose-files", manifest.ContainerWritePlan.RuntimeSpecializationId);
        Assert.Equal("font:default", manifest.CookedArtifacts[0].LogicalArtifactId);
        Assert.Equal("fonts/default.hasset", manifest.CookedArtifacts[0].RelativePath);
        Assert.Equal("gameplay", manifest.CodeModules[0].ModuleId);
        Assert.Equal("windows-loose-files", manifest.CodeModules[0].RuntimeSpecializationId);
        Assert.Equal("container-0", manifest.ArtifactPlacements[0].ContainerId);
        Assert.Equal("container-0", manifest.ContainerWritePlan.ContainerArtifacts[0].ContainerId);
        Assert.Empty(manifest.PlatformCookWorkItems);
    }
}
