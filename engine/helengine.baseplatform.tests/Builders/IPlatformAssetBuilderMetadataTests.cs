using helengine;
using helengine.baseplatform.Builders;
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
        Assert.Equal("helengine.FPSComponent", builder.Definition.ComponentCompatibilities[0].ComponentTypeId);
        Assert.Equal("default", builder.Definition.CodegenProfiles[0].ProfileId);
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
                ["shader-asset-id"] = "ForwardStandardShader",
                ["vertex-program"] = "ForwardStandardShader.vs",
                ["pixel-program"] = "ForwardStandardShader.ps",
                ["variant"] = "default"
            }));

        MaterialAsset materialAsset = Assert.IsType<MaterialAsset>(AssetSerializer.DeserializeFromBytes(result.CookedMaterialBytes));
        Assert.Equal("ForwardStandardShader", materialAsset.ShaderAssetId);
        Assert.Equal(new[] { "ForwardStandardShader" }, result.ReferencedShaderAssetIds);
    }
}
