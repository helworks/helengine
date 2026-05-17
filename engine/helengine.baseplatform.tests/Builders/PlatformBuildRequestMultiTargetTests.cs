using helengine.baseplatform.Manifest;
using helengine.baseplatform.Profiles;
using helengine.baseplatform.Requests;
using helengine.baseplatform.Targets;

namespace helengine.baseplatform.tests.Builders;

/// <summary>
/// Verifies build requests preserve media-profile selection and multi-target metadata.
/// </summary>
public class PlatformBuildRequestMultiTargetTests {
    [Fact]
    public void PlatformBuildRequest_preserves_media_profile_selection() {
        PlatformBuildRequest request = new(
            new PlatformBuildManifest(
                2,
                "project",
                "1.0.0",
                "1.0.0",
                "scene",
                [
                    new PlatformBuildScene(
                        "scene",
                        "Scene",
                        "scene.source",
                        [
                            new PlatformBuildPayloadReference("payload", "payload.source")
                        ],
                        [])
                ],
                [
                    new PlatformBuildAsset(
                        "asset",
                        "Asset",
                        "asset.source",
                        new PlatformBuildPayloadReference("asset-payload", "asset-payload.source"),
                        [])
                ],
                [
                    new PlatformBuildArtifact("scene.hasset", "scene:scene", "sha256:scene", "scene", "shared")
                ],
                [
                    new PlatformBuildCodeModule("gameplay", "gameplay", "windows-loose-files", ["always-loaded"], [])
                ],
                [],
                new PlatformContainerWritePlan(string.Empty, [])),
            [
                new PlatformBuildTargetVariant("windows-target", "windows", "dx11", "shared"),
                new PlatformBuildTargetVariant("ps2-target", "ps2", "gskit", "shared")
            ],
            [
                new PlatformCookProfile(
                    "shared",
                    "Shared",
                    new PlatformCookProfileCapabilities(
                        "shared-runtime",
                        "bc",
                        "ogg",
                        "scene",
                        PlatformSerializationEndianness.LittleEndian))
            ],
            "out",
            "work",
            "debug",
            "directx11",
            "default",
            new Dictionary<string, string> { ["emit-pdb"] = "true" },
            new Dictionary<string, string> { ["vsync"] = "false" },
            new Dictionary<string, string> { ["emit-symbols"] = "true" },
            "generated-core",
            "install-tree",
            "loose-files");

        Assert.Equal(2, request.TargetVariants.Length);
        Assert.Equal("install-tree", request.SelectedMediaProfileId);
        Assert.Equal("loose-files", request.SelectedStorageProfileId);
        Assert.Equal("scene", request.Manifest.StartupSceneId);
        Assert.Single(request.Manifest.CookedArtifacts);
        Assert.Equal("windows-loose-files", request.Manifest.CodeModules[0].RuntimeSpecializationId);
        Assert.Empty(request.Manifest.PlatformCookWorkItems);
    }
}
