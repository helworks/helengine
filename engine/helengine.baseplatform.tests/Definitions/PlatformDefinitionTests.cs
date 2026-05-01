using helengine.baseplatform.Definitions;

namespace helengine.baseplatform.tests.Definitions;

/// <summary>
/// Verifies the typed platform definition model preserves builder metadata.
/// </summary>
public class PlatformDefinitionTests {
    /// <summary>
    /// Verifies a platform definition retains build, graphics, and asset metadata.
    /// </summary>
    [Fact]
    public void PlatformDefinition_preserves_build_and_graphics_metadata() {
        PlatformDefinition definition = new(
            "windows",
            "Windows DirectX",
            [
                new PlatformBuildProfileDefinition(
                    "debug",
                    "Debug",
                    "Debug player build",
                    "directx11",
                    [
                        new PlatformSettingDefinition(
                            "emit-pdb",
                            "Emit PDB",
                            PlatformSettingKind.Boolean,
                            "true",
                            false,
                            [])
                    ])
            ],
            [
                new PlatformGraphicsProfileDefinition(
                    "directx11",
                    "DirectX 11",
                    "Default Windows renderer",
                    [])
            ],
            [
                new PlatformAssetRequirementDefinition(
                    "texture",
                    "Texture",
                    true,
                    ["png", "tga"])
            ]);

        Assert.Equal("windows", definition.PlatformId);
        Assert.Equal("Windows DirectX", definition.DisplayName);
        Assert.Equal("debug", definition.BuildProfiles[0].ProfileId);
        Assert.Equal("directx11", definition.GraphicsProfiles[0].ProfileId);
        Assert.Equal("texture", definition.AssetRequirements[0].RequirementId);
    }
}
