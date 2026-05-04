using helengine.baseplatform.Manifest;
using helengine.baseplatform.Profiles;
using helengine.baseplatform.Requests;
using helengine.baseplatform.Targets;

namespace helengine.baseplatform.tests.Builders;

/// <summary>
/// Verifies build requests preserve codegen selections.
/// </summary>
public class PlatformBuildRequestCodegenTests {
    /// <summary>
    /// Verifies a build request stores explicit codegen profile and option values.
    /// </summary>
    [Fact]
    public void PlatformBuildRequest_preserves_codegen_selection() {
        PlatformBuildRequest request = new(
            new PlatformBuildManifest(
                1,
                "project",
                "1.0.0",
                "1.0.0",
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
                ]),
            [
                new PlatformBuildTargetVariant("target", "windows", "dx11", "shared")
            ],
            [
                new PlatformCookProfile(
                    "shared",
                    "Shared",
                    new PlatformCookProfileCapabilities(
                        "dx",
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
            "generated-core");

        Assert.Equal("debug", request.SelectedBuildProfileId);
        Assert.Equal("directx11", request.SelectedGraphicsProfileId);
        Assert.Equal("default", request.SelectedCodegenProfileId);
        Assert.Equal("true", request.SelectedBuildOptionValues["emit-pdb"]);
        Assert.Equal("false", request.SelectedGraphicsOptionValues["vsync"]);
        Assert.Equal("true", request.SelectedCodegenOptionValues["emit-symbols"]);
        Assert.Equal("generated-core", request.GeneratedCoreCppRootPath);
    }
}
