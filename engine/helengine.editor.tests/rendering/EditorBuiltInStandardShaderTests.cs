using helengine.editor;
using helengine.editor.tests.testing;
using helengine.directx11;
using Xunit;

namespace helengine.editor.tests.rendering {
    /// <summary>
    /// Verifies the built-in forward standard shader compiles after forward-light and shadow-buffer changes.
    /// </summary>
    public class EditorBuiltInStandardShaderTests : IDisposable {
        readonly Core CoreValue;
        readonly TestGeneratedAssetGraph GeneratedAssetGraph;
        /// <summary>
        /// Configures the shared built-in shader backend registry for the shader-compilation tests.
        /// </summary>
        public EditorBuiltInStandardShaderTests() {
            CoreValue = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
            CoreValue.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
            GeneratedAssetGraph = new TestGeneratedAssetGraph(CoreValue);
        }

        public void Dispose() {
            GeneratedAssetGraph.Dispose();
            CoreValue.Dispose();
        }

        /// <summary>
        /// Ensures the built-in forward standard shader compiles for the DirectX11 target.
        /// </summary>
        [Fact]
        public void LoadShaderAsset_WhenUsingBuiltInStandardShader_CompilesForDirectX11() {
            ShaderAsset shaderAsset = GeneratedAssetGraph.LoadShaderAsset(ShaderCompileTarget.DirectX11, "ForwardStandardShader.hlsl");

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
            ShaderAsset shaderAsset = GeneratedAssetGraph.LoadShaderAsset(ShaderCompileTarget.DirectX11, "EditorShadowDepth.hlsl");

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
            ShaderAsset shaderAsset = GeneratedAssetGraph.LoadShaderAsset(ShaderCompileTarget.DirectX11, "EditorPointShadowDepth.hlsl");

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
            ShaderAsset shaderAsset = GeneratedAssetGraph.LoadShaderAsset(ShaderCompileTarget.DirectX11, "ForwardStandardShader.hlsl");
            ShaderMaterialAsset materialAsset = new ShaderMaterialAsset {
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
