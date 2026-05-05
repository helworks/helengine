using helengine.editor;
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
    }
}
