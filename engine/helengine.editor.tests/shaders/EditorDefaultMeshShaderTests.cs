using helengine.editor;
using Xunit;

namespace helengine.editor.tests.shaders {
    /// <summary>
    /// Verifies the built-in default mesh shader compiles for both renderer backends and keeps the material layout engine-managed.
    /// </summary>
    public class EditorDefaultMeshShaderTests {
        /// <summary>
        /// Ensures the built-in default mesh shader compiles for DirectX11 and exposes no user-authored material bindings.
        /// </summary>
        [Fact]
        public void LoadShaderAsset_WhenCompilingForDirectX11_ProducesAnEmptyMaterialLayout() {
            AssertShaderAssetLayout(ShaderCompileTarget.DirectX11);
        }

        /// <summary>
        /// Ensures the built-in default mesh shader compiles for Vulkan and exposes no user-authored material bindings.
        /// </summary>
        [Fact]
        public void LoadShaderAsset_WhenCompilingForVulkan_ProducesAnEmptyMaterialLayout() {
            AssertShaderAssetLayout(ShaderCompileTarget.Vulkan);
        }

        /// <summary>
        /// Compiles the built-in default mesh shader for one backend and verifies the resolved material layout.
        /// </summary>
        /// <param name="target">Shader backend that should receive the compiled built-in shader.</param>
        static void AssertShaderAssetLayout(ShaderCompileTarget target) {
            ShaderAsset shaderAsset = EditorBuiltInShaderAssetLibrary.LoadShaderAsset(target, "EditorDefaultMesh.hlsl");

            Assert.Equal("EditorDefaultMesh", shaderAsset.Id);
            Assert.Equal(ShaderTargetNames.GetTargetName(target), shaderAsset.TargetName);
            Assert.Equal(2, shaderAsset.Binaries.Length);

            MaterialLayout layout = MaterialLayoutBuilder.Build(CreateMaterialAsset(shaderAsset.Id), shaderAsset);

            Assert.Empty(layout.TextureBindings);
            Assert.Empty(layout.ConstantBufferBindings);
            Assert.Empty(layout.SamplerBindings);
        }

        /// <summary>
        /// Creates the minimal material asset required to resolve the built-in default mesh shader layout.
        /// </summary>
        /// <param name="shaderAssetId">Shader asset identifier selected by the material.</param>
        /// <returns>Material asset configured for the built-in default mesh shader.</returns>
        static MaterialAsset CreateMaterialAsset(string shaderAssetId) {
            if (string.IsNullOrWhiteSpace(shaderAssetId)) {
                throw new ArgumentException("Shader asset id must be provided.", nameof(shaderAssetId));
            }

            return new MaterialAsset {
                Id = "EditorDefaultMesh.material",
                ShaderAssetId = shaderAssetId,
                VertexProgram = "EditorDefaultMesh.vs",
                PixelProgram = "EditorDefaultMesh.ps",
                Variant = "default",
                RenderState = new MaterialRenderState()
            };
        }
    }
}
