using helengine.baseplatform.Definitions;
using helengine.baseplatform.Profiles;
using Xunit;

namespace helengine.baseplatform.tests.Definitions;

/// <summary>
/// Verifies the typed platform definition model preserves builder metadata.
/// </summary>
public class PlatformDefinitionTests {
    /// <summary>
    /// Verifies a platform definition retains build, graphics, asset, material, and profile metadata.
    /// </summary>
    [Fact]
    public void PlatformDefinition_preserves_build_graphics_asset_and_material_metadata() {
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
            ],
            [
                new PlatformMaterialSchemaDefinition(
                    "standard-shader",
                    "Standard Shader",
                    ["directx11"],
                    [
                        new PlatformMaterialFieldDefinition(
                            "shader-asset-id",
                            "Shader Asset",
                            PlatformMaterialFieldKind.AssetReference,
                            string.Empty,
                            true,
                            [])
                    ])
            ],
            [
                new PlatformComponentSupportRule(
                    "helengine.FPSComponent",
                    PlatformComponentSupportKind.PassThrough,
                    "FPS overlay is canonical on this platform.",
                    string.Empty)
            ],
            [
                new PlatformCodegenProfileDefinition(
                    "default",
                    "Default",
                    "Default codegen profile",
                    PlatformCodegenLanguage.Cpp,
                    PlatformSerializationEndianness.LittleEndian,
                    [])
            ],
            [
                new PlatformStorageProfileDefinition(
                    "install-tree",
                    "Install Tree",
                    PlatformStorageProfileKind.LooseFiles,
                    "windows-loose-files",
                    false)
            ],
            [
                new PlatformMediaProfileDefinition(
                    "install-tree",
                    "Install Tree",
                    PlatformMediaLayoutKind.InstallTree,
                    false,
                    false)
            ]);

        Assert.Equal("windows", definition.PlatformId);
        Assert.Equal("Windows DirectX", definition.DisplayName);
        Assert.Equal("debug", definition.BuildProfiles[0].ProfileId);
        Assert.Equal("directx11", definition.GraphicsProfiles[0].ProfileId);
        Assert.Equal("texture", definition.AssetRequirements[0].RequirementId);
        Assert.Equal("standard-shader", definition.MaterialSchemas[0].SchemaId);
        Assert.Equal(PlatformMaterialFieldKind.AssetReference, definition.MaterialSchemas[0].Fields[0].FieldKind);
        Assert.Equal("helengine.FPSComponent", definition.ComponentSupportRules[0].ComponentTypeId);
        Assert.Equal("default", definition.CodegenProfiles[0].ProfileId);
        Assert.Equal("install-tree", definition.StorageProfiles[0].ProfileId);
        Assert.Equal("install-tree", definition.MediaProfiles[0].ProfileId);
        Assert.Equal(RuntimeMaterialResolutionMode.RawShaderBacked, definition.RuntimeGenerationContract.MaterialResolutionMode);
        Assert.True(definition.RuntimeGenerationContract.SupportsRenderManager2DTextureReleaseFlush);
        Assert.Equal(PackagedPathPolicy.ContentRelativeOnly, definition.RuntimeGenerationContract.PackagedPathPolicy);
    }
}

