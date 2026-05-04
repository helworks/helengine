using helengine.baseplatform.Manifest;

namespace helengine.baseplatform.tests.Builders;

public class PlatformBuildManifestTests {
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
    }
}
