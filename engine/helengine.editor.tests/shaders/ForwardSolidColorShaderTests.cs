using helengine.editor;
using helengine.editor.tests;
using Xunit;

namespace helengine.editor.tests.shaders {
    /// <summary>
    /// Verifies the built-in forward solid-color shader compiles through the shared editor shader pipeline for desktop targets.
    /// </summary>
    public class ForwardSolidColorShaderTests {
        /// <summary>
        /// Ensures the built-in forward solid-color shader compiles for the requested desktop backend and publishes shader binaries.
        /// </summary>
        /// <param name="target">Shader backend that should receive the compiled solid-color shader.</param>
        [Theory]
        [InlineData(ShaderCompileTarget.DirectX11)]
        [InlineData(ShaderCompileTarget.Vulkan)]
        public void LoadShaderAsset_whenForwardSolidColorShaderIsRequested_compilesForDesktopTargets(ShaderCompileTarget target) {
            DesktopShaderBackendRegistryFactory.ConfigureEditorBuiltInShaderBackends();
            ShaderAsset shaderAsset = EditorBuiltInShaderAssetLibrary.LoadShaderAsset(target, "ForwardSolidColorShader.hlsl");

            Assert.Equal("ForwardSolidColorShader", shaderAsset.Id);
            Assert.Equal(ShaderTargetNames.GetTargetName(target), shaderAsset.TargetName);
            Assert.NotNull(shaderAsset.Binaries);
            Assert.Equal(4, shaderAsset.Binaries.Length);
            Assert.Contains(shaderAsset.Binaries, binary => binary.Stage == ShaderStage.Vertex && binary.ProgramName == "ForwardSolidColorShader.vs" && binary.Variant == "default");
            Assert.Contains(shaderAsset.Binaries, binary => binary.Stage == ShaderStage.Pixel && binary.ProgramName == "ForwardSolidColorShader.ps" && binary.Variant == "default");
            Assert.Contains(shaderAsset.Binaries, binary => binary.Stage == ShaderStage.Vertex && binary.ProgramName == "ForwardSolidColorShader.vs" && binary.Variant == "Mesh");
            Assert.Contains(shaderAsset.Binaries, binary => binary.Stage == ShaderStage.Pixel && binary.ProgramName == "ForwardSolidColorShader.ps" && binary.Variant == "Mesh");
        }
    }
}
