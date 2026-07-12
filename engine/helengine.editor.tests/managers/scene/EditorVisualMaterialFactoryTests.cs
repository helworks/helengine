using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.scene {
    /// <summary>
    /// Verifies the runtime material contract used by editor-only camera and light visuals.
    /// </summary>
    public sealed class EditorVisualMaterialFactoryTests : IDisposable {
        /// <summary>
        /// Core instance that provides the generated material cache dependencies required by the factory.
        /// </summary>
        readonly Core CoreValue;

        /// <summary>
        /// Initializes the engine core with a DirectX11-shaped renderer so editor visual materials can be created.
        /// </summary>
        public EditorVisualMaterialFactoryTests() {
            CoreValue = new Core(new CoreInitializationOptions {
                ContentStreamSource = new HostFileSystemContentStreamSource(AppContext.BaseDirectory)
            });
            CoreValue.Initialize(TestDirectX11RenderManager3D.Create(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Releases generated material cache state after each test.
        /// </summary>
        public void Dispose() {
            EngineGeneratedMaterialCache.ResetForTests();
            CoreValue.Dispose();
        }

        /// <summary>
        /// Ensures the shared non-shadow-casting editor visual material still participates in normal scene depth testing.
        /// </summary>
        [Fact]
        public void CreateNonShadowCastingStandardMaterial_PreservesOpaqueDepthTesting() {
            RuntimeMaterial material = EditorVisualMaterialFactory.CreateNonShadowCastingStandardMaterial();

            Assert.True(material.RenderState.DepthTestEnabled);
            Assert.True(material.RenderState.DepthWriteEnabled);
            Assert.Equal(MaterialBlendMode.Opaque, material.RenderState.BlendMode);
            Assert.False(material.CastsShadows);
        }

        /// <summary>
        /// Ensures the overlay editor visual material stays visible on top of authored scene depth when explicitly requested.
        /// </summary>
        [Fact]
        public void CreateOverlayStandardMaterial_DisablesDepthTestingAndDepthWrites() {
            RuntimeMaterial material = EditorVisualMaterialFactory.CreateOverlayStandardMaterial();

            Assert.False(material.RenderState.DepthTestEnabled);
            Assert.False(material.RenderState.DepthWriteEnabled);
            Assert.Equal(MaterialBlendMode.AlphaBlend, material.RenderState.BlendMode);
            Assert.False(material.CastsShadows);
        }
    }
}
