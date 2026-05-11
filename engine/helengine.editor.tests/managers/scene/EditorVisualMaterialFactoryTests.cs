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
            CoreValue = new Core();
            CoreValue.Initialize(TestDirectX11RenderManager3D.Create(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Releases generated material cache state after each test.
        /// </summary>
        public void Dispose() {
            EngineGeneratedMaterialCache.ResetForTests();
            CoreValue.Dispose();
        }

        /// <summary>
        /// Ensures editor-only scene visuals render as overlay geometry instead of competing with authored scene depth.
        /// </summary>
        [Fact]
        public void CreateNonShadowCastingStandardMaterial_DisablesDepthTestingAndDepthWrites() {
            RuntimeMaterial material = EditorVisualMaterialFactory.CreateNonShadowCastingStandardMaterial();

            Assert.False(material.RenderState.DepthTestEnabled);
            Assert.False(material.RenderState.DepthWriteEnabled);
            Assert.Equal(MaterialBlendMode.AlphaBlend, material.RenderState.BlendMode);
            Assert.False(material.CastsShadows);
        }
    }
}
