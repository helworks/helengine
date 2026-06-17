using helengine.editor;
using helengine.directx11;
using helengine.vulkan;
using Xunit;

namespace helengine.editor.tests.shaders {
    /// <summary>
    /// Verifies the built-in forward standard shader compiles for both renderer backends and exposes the expected standard-material contract.
    /// </summary>
    public class ForwardStandardShaderTests {
        /// <summary>
        /// Configures the shared built-in shader backend registry for the shader layout tests.
        /// </summary>
        public ForwardStandardShaderTests() {
            ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
            shaderBackendRegistry.Register(new DirectX11ShaderBackend());
            shaderBackendRegistry.Register(new VulkanShaderBackend());
            EditorBuiltInShaderAssetLibrary.ConfigureShaderBackends(shaderBackendRegistry);
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
        /// Ensures the built-in forward standard shader reads ambient lighting from the shared forward-light buffer instead of a hardcoded fallback color.
        /// </summary>
        [Fact]
        public void ForwardStandardShaderSource_WhenInspected_UsesSharedAmbientLightBuffer() {
            string repositoryRootPath = new EditorSourceBuildWorkspaceLocator().ResolveHelEngineRootPath();
            string shaderPath = Path.Combine(repositoryRootPath, "engine", "helengine.editor", "shaders", "builtin", "ForwardStandardShader.hlsl");
            string shaderSource = File.ReadAllText(shaderPath);

            Assert.Contains("float4 ambientLightColor;", shaderSource, StringComparison.Ordinal);
            Assert.DoesNotContain("float3 ambientColor = float3(0.12f, 0.13f, 0.15f);", shaderSource, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the built-in forward standard shader does not replace point-light shading with legacy debug axis colors.
        /// </summary>
        [Fact]
        public void ForwardStandardShaderSource_WhenInspected_DoesNotContainPointLightDebugTintOverride() {
            string repositoryRootPath = new EditorSourceBuildWorkspaceLocator().ResolveHelEngineRootPath();
            string shaderPath = Path.Combine(repositoryRootPath, "engine", "helengine.editor", "shaders", "builtin", "ForwardStandardShader.hlsl");
            string shaderSource = File.ReadAllText(shaderPath);

            Assert.DoesNotContain("diffuseColor = float3(1.0f, 0.0f, 0.0f) * diffuse * attenuation;", shaderSource, StringComparison.Ordinal);
            Assert.DoesNotContain("diffuseColor = float3(0.0f, 1.0f, 0.0f) * diffuse * attenuation;", shaderSource, StringComparison.Ordinal);
            Assert.DoesNotContain("diffuseColor = float3(0.0f, 0.0f, 1.0f) * diffuse * attenuation;", shaderSource, StringComparison.Ordinal);
            Assert.DoesNotContain("diffuseColor = float3(1.0f, 1.0f, 0.0f) * diffuse * attenuation;", shaderSource, StringComparison.Ordinal);
            Assert.DoesNotContain("diffuseColor = float3(1.0f, 0.0f, 1.0f) * diffuse * attenuation;", shaderSource, StringComparison.Ordinal);
            Assert.DoesNotContain("diffuseColor = float3(0.0f, 1.0f, 1.0f) * diffuse * attenuation;", shaderSource, StringComparison.Ordinal);
        }

        /// <summary>
        /// Compiles the built-in forward standard shader for one backend and verifies the resolved material layout.
        /// </summary>
        /// <param name="target">Shader backend that should receive the compiled built-in shader.</param>
        static void AssertShaderAssetLayout(ShaderCompileTarget target) {
            ShaderAsset shaderAsset = EditorBuiltInShaderAssetLibrary.LoadShaderAsset(target, "ForwardStandardShader.hlsl");

            Assert.Equal("ForwardStandardShader", shaderAsset.Id);
            Assert.Equal(ShaderTargetNames.GetTargetName(target), shaderAsset.TargetName);
            Assert.Equal(4, shaderAsset.Binaries.Length);
            Assert.Contains(shaderAsset.Binaries, binary => binary.Stage == ShaderStage.Vertex && binary.ProgramName == "ForwardStandardShader.vs" && binary.Variant == "default");
            Assert.Contains(shaderAsset.Binaries, binary => binary.Stage == ShaderStage.Pixel && binary.ProgramName == "ForwardStandardShader.ps" && binary.Variant == "default");
            Assert.Contains(shaderAsset.Binaries, binary => binary.Stage == ShaderStage.Vertex && binary.ProgramName == "ForwardStandardShader.vs" && binary.Variant == "Mesh");
            Assert.Contains(shaderAsset.Binaries, binary => binary.Stage == ShaderStage.Pixel && binary.ProgramName == "ForwardStandardShader.ps" && binary.Variant == "Mesh");

            MaterialLayout layout = MaterialLayoutBuilder.Build(CreateMaterialAsset(shaderAsset.Id), shaderAsset);

            Assert.Contains(layout.TextureBindings, binding => binding.Name == StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName);
            Assert.Contains(layout.ConstantBufferBindings, binding => binding.Name == "BaseColorBuffer");
            Assert.Contains(layout.ConstantBufferBindings, binding => binding.Name == "ForwardLightBuffer");
            Assert.Contains(layout.ConstantBufferBindings, binding => binding.Name == "ShadowBuffer");
            Assert.Contains(layout.SamplerBindings, binding => binding.Name == StandardMaterialTextureBindingDefaults.DiffuseTextureBindingName + "Sampler");
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
                Variant = "default",
                RenderState = new MaterialRenderState()
            };
        }
    }
}
