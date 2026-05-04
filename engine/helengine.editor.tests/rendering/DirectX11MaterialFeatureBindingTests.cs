using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies compact Windows-forward material feature binding for the DirectX11 material build path.
    /// </summary>
    public class DirectX11MaterialFeatureBindingTests {
        /// <summary>
        /// Ensures building one material from raw authored data sets the compact PBR feature flags.
        /// </summary>
        [Fact]
        public void BuildMaterialFromRaw_WhenNormalAndEmissiveInputsExist_SetsCompactPbrFeatureFlags() {
            MaterialAsset materialAsset = new MaterialAsset {
                Id = "materials/test",
                ShaderAssetId = "shader/test",
                VertexProgram = "VS",
                PixelProgram = "PS",
                Variant = "default",
                NormalTextureAssetId = "textures/normal",
                EmissiveTextureAssetId = "textures/emissive"
            };
            ShaderAsset shaderAsset = new ShaderAsset {
                Id = "shader/test"
            };
            TestDirectX11RenderManager3D renderer = TestDirectX11RenderManager3D.Create();

            RuntimeMaterial material = renderer.BuildMaterialFromRaw(materialAsset, shaderAsset);

            Assert.Equal(RuntimeMaterialLightingModel.MetalRoughPbr, material.LightingModel);
            Assert.True(material.SupportsNormalMapping);
            Assert.True(material.SupportsEmissive);
        }
    }
}
