using helengine;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.gizmo {
    /// <summary>
    /// Verifies the dedicated material factory used by translation gizmo plane handles.
    /// </summary>
    public class TransformGizmoPlaneMaterialFactoryTests {
        /// <summary>
        /// Ensures normal and highlighted plane materials use alpha-blended overlay render state.
        /// </summary>
        [Fact]
        public void Create_UsesAlphaBlendedOverlayRenderState() {
            TestRenderManager3D render3D = new TestRenderManager3D();

            TransformGizmoPlaneMaterialFactory.CreateNormal(render3D);
            TransformGizmoPlaneMaterialFactory.CreateHighlight(render3D);

            Assert.Equal(2, render3D.BuiltMaterialAssets.Count);
            AssertAlphaBlendedOverlayState(render3D.BuiltMaterialAssets[0].RenderState);
            AssertAlphaBlendedOverlayState(render3D.BuiltMaterialAssets[1].RenderState);
        }

        /// <summary>
        /// Validates that one material render state matches the expected gizmo plane overlay configuration.
        /// </summary>
        /// <param name="renderState">Render state to inspect.</param>
        void AssertAlphaBlendedOverlayState(MaterialRenderState renderState) {
            Assert.NotNull(renderState);
            Assert.Equal(MaterialBlendMode.AlphaBlend, renderState.BlendMode);
            Assert.Equal(MaterialCullMode.None, renderState.CullMode);
            Assert.True(renderState.DepthTestEnabled);
            Assert.False(renderState.DepthWriteEnabled);
        }
    }
}
