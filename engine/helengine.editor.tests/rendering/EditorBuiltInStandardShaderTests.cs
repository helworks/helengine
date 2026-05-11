using helengine.editor;
using helengine.directx11;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies the built-in forward standard shader compiles after forward-light and shadow-buffer changes.
    /// </summary>
    public class EditorBuiltInStandardShaderTests {
        /// <summary>
        /// Ensures the built-in forward standard shader compiles for the DirectX11 target.
        /// </summary>
        [Fact]
        public void LoadShaderAsset_WhenUsingBuiltInStandardShader_CompilesForDirectX11() {
            ShaderAsset shaderAsset = EditorBuiltInShaderAssetLibrary.LoadShaderAsset(ShaderCompileTarget.DirectX11, "ForwardStandardShader.hlsl");

            Assert.NotNull(shaderAsset);
            Assert.Equal("ForwardStandardShader", shaderAsset.Id);
            Assert.NotNull(shaderAsset.Binaries);
            Assert.NotEmpty(shaderAsset.Binaries);
        }

        /// <summary>
        /// Ensures the built-in shadow-depth shader compiles for the DirectX11 target.
        /// </summary>
        [Fact]
        public void LoadShaderAsset_WhenUsingBuiltInShadowDepthShader_CompilesForDirectX11() {
            ShaderAsset shaderAsset = EditorBuiltInShaderAssetLibrary.LoadShaderAsset(ShaderCompileTarget.DirectX11, "EditorShadowDepth.hlsl");

            Assert.NotNull(shaderAsset);
            Assert.Equal("EditorShadowDepth", shaderAsset.Id);
            Assert.NotNull(shaderAsset.Binaries);
            Assert.NotEmpty(shaderAsset.Binaries);
        }

        /// <summary>
        /// Ensures the built-in point-shadow depth shader compiles for the DirectX11 target.
        /// </summary>
        [Fact]
        public void LoadShaderAsset_WhenUsingBuiltInPointShadowDepthShader_CompilesForDirectX11() {
            ShaderAsset shaderAsset = EditorBuiltInShaderAssetLibrary.LoadShaderAsset(ShaderCompileTarget.DirectX11, "EditorPointShadowDepth.hlsl");

            Assert.NotNull(shaderAsset);
            Assert.Equal("EditorPointShadowDepth", shaderAsset.Id);
            Assert.NotNull(shaderAsset.Binaries);
            Assert.NotEmpty(shaderAsset.Binaries);
        }

        /// <summary>
        /// Ensures mesh-authored built-in standard materials can resolve through the real DirectX11 material-build path.
        /// </summary>
        [Fact]
        public void BuildMaterialFromRaw_WhenUsingBuiltInStandardShaderMeshVariant_CompilesForDirectX11() {
            using DirectX11Renderer3D renderer = new DirectX11Renderer3D();
            ShaderAsset shaderAsset = EditorBuiltInShaderAssetLibrary.LoadShaderAsset(ShaderCompileTarget.DirectX11, "ForwardStandardShader.hlsl");
            MaterialAsset materialAsset = new MaterialAsset {
                Id = "ForwardStandardShader.mesh.material",
                ShaderAssetId = shaderAsset.Id,
                VertexProgram = "ForwardStandardShader.vs",
                PixelProgram = "ForwardStandardShader.ps",
                Variant = "Mesh",
                RenderState = new MaterialRenderState()
            };

            RuntimeMaterial material = renderer.BuildMaterialFromRaw(materialAsset, shaderAsset);

            Assert.NotNull(material);
        }
    }
}
