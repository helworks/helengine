using helengine;
using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Requests;
using helengine.baseplatform.Results;
using Xunit;

namespace helengine.baseplatform.tests.Builders;

/// <summary>
/// Verifies that the shared builder contract exposes typed platform metadata.
/// </summary>
public class IPlatformAssetBuilderMetadataTests {
    /// <summary>
    /// Verifies a builder implementation can expose a typed platform definition.
    /// </summary>
    [Fact]
    public void Builder_contract_exposes_platform_definition() {
        IPlatformAssetBuilder builder = new TestPlatformAssetBuilder();

        Assert.Equal("windows", builder.Definition.PlatformId);
        Assert.Equal("debug", builder.Definition.BuildProfiles[0].ProfileId);
        Assert.Equal("standard-shader", builder.Definition.MaterialSchemas[0].SchemaId);
        Assert.Equal("helengine.FPSComponent", builder.Definition.ComponentSupportRules[0].ComponentTypeId);
        Assert.Equal("default", builder.Definition.CodegenProfiles[0].ProfileId);
        Assert.Empty(builder.Definition.AssetCookCapabilities);
    }

    /// <summary>
    /// Verifies one platform definition preserves explicit builder-owned asset cook capabilities.
    /// </summary>
    [Fact]
    public void PlatformDefinition_preserves_asset_cook_capabilities() {
        PlatformDefinition definition = new(
            "gamecube",
            "Nintendo GameCube",
            Array.Empty<PlatformBuildProfileDefinition>(),
            Array.Empty<PlatformGraphicsProfileDefinition>(),
            Array.Empty<PlatformAssetRequirementDefinition>(),
            Array.Empty<PlatformMaterialSchemaDefinition>(),
            Array.Empty<PlatformComponentSupportRule>(),
            Array.Empty<PlatformCodegenProfileDefinition>(),
            Array.Empty<PlatformStorageProfileDefinition>(),
            Array.Empty<PlatformMediaProfileDefinition>(),
            assetCookCapabilities: [
                new PlatformAssetCookCapabilityDefinition(
                    "texture",
                    "runtime-texture",
                    PlatformAssetCookOwnershipKind.BuilderOwned,
                    "gamecube-texture",
                    textureFormatCapabilities: new PlatformTextureFormatCapabilityDefinition(
                        [TextureAssetColorFormat.Rgba4444.ToString(), TextureAssetColorFormat.Indexed8.ToString()],
                        [TextureAssetAlphaPrecision.A4, TextureAssetAlphaPrecision.A8],
                        [
                            new PlatformTextureFormatCombinationDefinition(TextureAssetColorFormat.Rgba4444.ToString(), TextureAssetAlphaPrecision.A4),
                            new PlatformTextureFormatCombinationDefinition(TextureAssetColorFormat.Indexed8.ToString(), TextureAssetAlphaPrecision.A8)
                        ]))
            ]);

        PlatformAssetCookCapabilityDefinition capability = Assert.Single(definition.AssetCookCapabilities);
        Assert.Equal("texture", capability.SourceAssetKind);
        Assert.Equal("runtime-texture", capability.TargetArtifactKind);
        Assert.Equal(PlatformAssetCookOwnershipKind.BuilderOwned, capability.OwnershipKind);
        Assert.Equal("gamecube-texture", capability.SettingsContractId);
        Assert.NotNull(capability.TextureFormatCapabilities);
        Assert.Equal(
            [TextureAssetColorFormat.Rgba4444.ToString(), TextureAssetColorFormat.Indexed8.ToString()],
            capability.TextureFormatCapabilities.SupportedColorFormatIds);
        Assert.Equal(
            [TextureAssetAlphaPrecision.A4, TextureAssetAlphaPrecision.A8],
            capability.TextureFormatCapabilities.SupportedAlphaPrecisions);
        Assert.Collection(
            capability.TextureFormatCapabilities.SupportedCombinations,
            combination => {
                Assert.Equal(TextureAssetColorFormat.Rgba4444.ToString(), combination.ColorFormatId);
                Assert.Equal(TextureAssetAlphaPrecision.A4, combination.AlphaPrecision);
            },
            combination => {
                Assert.Equal(TextureAssetColorFormat.Indexed8.ToString(), combination.ColorFormatId);
                Assert.Equal(TextureAssetAlphaPrecision.A8, combination.AlphaPrecision);
            });
    }

    /// <summary>
    /// Verifies a builder implementation can cook one schema-driven material payload.
    /// </summary>
    [Fact]
    public void Builder_contract_cooks_material_from_schema_data() {
        IPlatformAssetBuilder builder = new TestPlatformAssetBuilder();

        PlatformMaterialCookResult result = builder.CookMaterial(new PlatformMaterialCookRequest(
            "Materials/Test.helmat",
            "Materials/Test.helmat",
            "windows",
            "debug",
            "directx11",
            "standard-shader",
            new Dictionary<string, string> {
                ["use-custom-shader"] = "false",
                ["shader-asset-id"] = "ForwardStandardShader",
                ["vertex-program"] = "ForwardStandardShader.vs",
                ["pixel-program"] = "ForwardStandardShader.ps",
                ["variant"] = "Mesh",
                ["base-color"] = "#336699"
            }));

        ShaderMaterialAsset materialAsset = Assert.IsType<ShaderMaterialAsset>(AssetSerializer.DeserializeFromBytes(result.CookedMaterialBytes));
        Assert.Equal("ForwardStandardShader", materialAsset.ShaderAssetId);
        Assert.Single(materialAsset.ConstantBuffers);
        Assert.Equal("BaseColorBuffer", materialAsset.ConstantBuffers[0].Name);
        Assert.Equal(16, materialAsset.ConstantBuffers[0].Data.Length);
        Assert.Equal(new[] { "ForwardStandardShader" }, result.ReferencedShaderAssetIds);
    }
}

