using helengine.editor;
using helengine.editor.tests.testing;
using helengine.directx11;
using helengine.vulkan;
using Xunit;

namespace helengine.editor.tests.shaders {
    /// <summary>
    /// Verifies the built-in forward standard shader compiles for both renderer backends and exposes the expected standard-material contract.
    /// </summary>
    public class ForwardStandardShaderTests : IDisposable {
        readonly Core CoreValue;
        readonly TestGeneratedAssetGraph GeneratedAssetGraph;
        /// <summary>
        /// Configures the shared built-in shader backend registry for the shader layout tests.
        /// </summary>
        public ForwardStandardShaderTests() {
            CoreValue = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            CoreValue.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
            GeneratedAssetGraph = new TestGeneratedAssetGraph(CoreValue);
        }

        public void Dispose() {
            GeneratedAssetGraph.Dispose();
            CoreValue.Dispose();
        }

        /// <summary>
        /// Ensures the built-in forward standard shader compiles for DirectX11 and exposes the expected standard-material bindings.
        /// </summary>
        [Fact]
        public void LoadShaderAsset_WhenCompilingForDirectX11_ExposesExpectedStandardMaterialBindings() {
            AssertShaderAssetLayout(ShaderCompileTarget.DirectX11);
        }

        /// <summary>
        /// Ensures the built-in forward standard shader compiles for Vulkan and exposes the expected standard-material bindings.
        /// </summary>
        [Fact]
    public void LoadShaderAsset_WhenCompilingForVulkan_ExposesExpectedStandardMaterialBindings() {
        AssertShaderAssetLayout(ShaderCompileTarget.Vulkan);
    }

    /// <summary>
    /// Ensures every shader-capable Windows backend compiles the complete shared Standard Shader variant contract.
    /// </summary>
    /// <param name="target">Backend target that should compile the shared variant catalog.</param>
    [Theory]
    [InlineData(ShaderCompileTarget.DirectX11)]
    [InlineData(ShaderCompileTarget.Vulkan)]
    public void LoadShaderAsset_WhenStandardShaderIsLoaded_CompilesEverySharedVariant(ShaderCompileTarget target) {
        ShaderAsset shaderAsset = GeneratedAssetGraph.LoadShaderAsset(target, "ForwardStandardShader.hlsl");

        Assert.Equal(10, shaderAsset.Binaries.Length);
        Assert.Contains(shaderAsset.Binaries, binary => binary.Stage == ShaderStage.Pixel && binary.Variant == "ForwardStandardShadowed");
        Assert.Contains(shaderAsset.Binaries, binary => binary.Stage == ShaderStage.Pixel && binary.Variant == "ShadowDepth" && binary.ProgramName == "ForwardStandardShader.ps");
    }

        /// <summary>
        /// Compiles the built-in forward standard shader for one backend and verifies the resolved material layout.
        /// </summary>
        /// <param name="target">Shader backend that should receive the compiled built-in shader.</param>
        void AssertShaderAssetLayout(ShaderCompileTarget target) {
            ShaderAsset shaderAsset = GeneratedAssetGraph.LoadShaderAsset(target, "ForwardStandardShader.hlsl");

            Assert.Equal("ForwardStandardShader", shaderAsset.Id);
            Assert.Equal(ShaderTargetNames.GetTargetName(target), shaderAsset.TargetName);
            Assert.Equal(10, shaderAsset.Binaries.Length);
            Assert.Contains(shaderAsset.Binaries, binary => binary.Stage == ShaderStage.Vertex && binary.ProgramName == "ForwardStandardShader.vs" && binary.Variant == "ForwardStandard");
            Assert.Contains(shaderAsset.Binaries, binary => binary.Stage == ShaderStage.Pixel && binary.ProgramName == "ForwardStandardShader.ps" && binary.Variant == "ForwardStandard");
            Assert.Contains(shaderAsset.Binaries, binary => binary.Stage == ShaderStage.Vertex && binary.ProgramName == "ForwardStandardShader.vs" && binary.Variant == "ForwardStandardShadowed");
            Assert.Contains(shaderAsset.Binaries, binary => binary.Stage == ShaderStage.Pixel && binary.ProgramName == "ForwardStandardShader.ps" && binary.Variant == "ForwardStandardShadowed");
            Assert.Contains(shaderAsset.Binaries, binary => binary.Stage == ShaderStage.Vertex && binary.ProgramName == "ForwardStandardShader.vs" && binary.Variant == "ShadowDepth");
            Assert.Contains(shaderAsset.Binaries, binary => binary.Stage == ShaderStage.Pixel && binary.ProgramName == "ForwardStandardShader.ps" && binary.Variant == "ShadowDepth");
            Assert.Contains(shaderAsset.Binaries, binary => binary.Stage == ShaderStage.Vertex && binary.ProgramName == "ForwardStandardShader.vs" && binary.Variant == "default");
            Assert.Contains(shaderAsset.Binaries, binary => binary.Stage == ShaderStage.Pixel && binary.ProgramName == "ForwardStandardShader.ps" && binary.Variant == "Mesh");

            MaterialLayout layout = MaterialLayoutBuilder.Build(CreateMaterialAsset(shaderAsset.Id), shaderAsset);

            Assert.True(layout.FindTextureBindingIndex(StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName) >= 0);
            Assert.True(layout.FindTextureBindingIndex(StandardMaterialTextureBindingDefaults.RoughnessTextureBindingName) >= 0);
            Assert.True(layout.FindTextureBindingIndex(StandardMaterialTextureBindingDefaults.EmissiveTextureBindingName) >= 0);
            Assert.True(layout.FindConstantBufferBindingIndex("BaseColorBuffer") >= 0);
            Assert.True(layout.FindConstantBufferBindingIndex(StandardMaterialRoughnessDefaults.RoughnessBufferName) >= 0);
            Assert.True(layout.FindConstantBufferBindingIndex(StandardMaterialMetallicDefaults.MetallicBufferName) >= 0);
            Assert.True(layout.FindConstantBufferBindingIndex(StandardMaterialSpecularDefaults.SpecularBufferName) >= 0);
            Assert.True(layout.FindConstantBufferBindingIndex(StandardMaterialEmissiveColorDefaults.EmissiveColorBufferName) >= 0);
            Assert.True(layout.FindConstantBufferBindingIndex("ForwardLightBuffer") >= 0);
            Assert.True(layout.FindConstantBufferBindingIndex("ShadowBuffer") >= 0);
            Assert.True(layout.FindSamplerBindingIndex(StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName + "Sampler") >= 0);
            Assert.True(layout.FindSamplerBindingIndex(StandardMaterialTextureBindingDefaults.RoughnessTextureBindingName + "Sampler") >= 0);
            Assert.True(layout.FindSamplerBindingIndex(StandardMaterialTextureBindingDefaults.EmissiveTextureBindingName + "Sampler") >= 0);
        }

        /// <summary>
        /// Creates the minimal material asset required to resolve the built-in forward standard shader layout.
        /// </summary>
        /// <param name="shaderAssetId">Shader asset identifier selected by the material.</param>
        /// <returns>Material asset configured for the built-in forward standard shader.</returns>
        static ShaderMaterialAsset CreateMaterialAsset(string shaderAssetId) {
            if (string.IsNullOrWhiteSpace(shaderAssetId)) {
                throw new ArgumentException("Shader asset id must be provided.", nameof(shaderAssetId));
            }

            return new ShaderMaterialAsset {
                Id = "ForwardStandardShader.material",
                ShaderAssetId = shaderAssetId,
                VertexProgram = "ForwardStandardShader.vs",
                PixelProgram = "ForwardStandardShader.ps",
                Variant = "ForwardStandard",
                RenderState = new MaterialRenderState()
            };
        }
    }
}
