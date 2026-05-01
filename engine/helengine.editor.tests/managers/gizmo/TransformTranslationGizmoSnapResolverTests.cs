using helengine;
using helengine.editor;
using Xunit;

namespace helengine.editor.tests.managers.gizmo {
    /// <summary>
    /// Verifies snapped translation offsets used by translation gizmo dragging.
    /// </summary>
    public class TransformTranslationGizmoSnapResolverTests {
        /// <summary>
        /// Ensures axis movement snaps to the nearest configured step along the handle axis.
        /// </summary>
        [Fact]
        public void ResolveAxisOffset_SnapsToNearestAxisIncrement() {
            float3 resolvedOffset = TransformTranslationGizmoSnapResolver.ResolveAxisOffset(
                new float3(1f, 0f, 0f),
                0.37,
                0.25);

            Assert.Equal(0.25f, resolvedOffset.X);
            Assert.Equal(0f, resolvedOffset.Y);
            Assert.Equal(0f, resolvedOffset.Z);
        }

        /// <summary>
        /// Ensures axis movement preserves sign when snapping along a negative world direction.
        /// </summary>
        [Fact]
        public void ResolveAxisOffset_PreservesSignedMovementOnNegativeAxis() {
            float3 resolvedOffset = TransformTranslationGizmoSnapResolver.ResolveAxisOffset(
                new float3(0f, 0f, -1f),
                0.62,
                0.25);

            Assert.Equal(0f, resolvedOffset.X);
            Assert.Equal(0f, resolvedOffset.Y);
            Assert.Equal(-0.5f, resolvedOffset.Z);
        }

        /// <summary>
        /// Ensures axis movement snaps the final position to the absolute grid instead of preserving the start offset.
        /// </summary>
        [Fact]
        public void ResolveAxisOffset_SnapsTheFinalAxisPositionToTheFixedGrid() {
            float3 resolvedOffset = TransformTranslationGizmoSnapResolver.ResolveAxisOffset(
                new float3(0.25f, 0f, 0f),
                new float3(1f, 0f, 0f),
                0.0,
                0.5);

            Assert.Equal(0.25f, resolvedOffset.X);
            Assert.Equal(0f, resolvedOffset.Y);
            Assert.Equal(0f, resolvedOffset.Z);
        }

        /// <summary>
        /// Ensures plane movement snaps independently along each plane basis axis.
        /// </summary>
        [Fact]
        public void ResolvePlaneOffset_SnapsBothPlaneBasisDirections() {
            float3 resolvedOffset = TransformTranslationGizmoSnapResolver.ResolvePlaneOffset(
                new float3(1f, 0f, 0f),
                new float3(0f, 1f, 0f),
                new float3(0.38f, 0.62f, 0.1f),
                0.25);

            Assert.Equal(0.5f, resolvedOffset.X);
            Assert.Equal(0.5f, resolvedOffset.Y);
            Assert.Equal(0f, resolvedOffset.Z);
        }

        /// <summary>
        /// Ensures plane snapping respects snapped gizmo directions that point toward negative world axes.
        /// </summary>
        [Fact]
        public void ResolvePlaneOffset_UsesSignedComponentsForNegativeDirections() {
            float3 resolvedOffset = TransformTranslationGizmoSnapResolver.ResolvePlaneOffset(
                new float3(-1f, 0f, 0f),
                new float3(0f, 0f, -1f),
                new float3(-0.63f, 0f, -0.12f),
                0.25);

            Assert.Equal(-0.75f, resolvedOffset.X);
            Assert.Equal(0f, resolvedOffset.Y);
            Assert.Equal(0f, resolvedOffset.Z);
        }

        /// <summary>
        /// Ensures plane movement snaps the final position to the absolute grid instead of preserving the start offset.
        /// </summary>
        [Fact]
        public void ResolvePlaneOffset_SnapsTheFinalPlanePositionToTheFixedGrid() {
            float3 resolvedOffset = TransformTranslationGizmoSnapResolver.ResolvePlaneOffset(
                new float3(0.25f, 0.75f, 0f),
                new float3(1f, 0f, 0f),
                new float3(0f, 1f, 0f),
                new float3(0f, 0f, 0f),
                0.5);

            Assert.Equal(0.25f, resolvedOffset.X);
            Assert.Equal(0.25f, resolvedOffset.Y);
            Assert.Equal(0f, resolvedOffset.Z);
        }
    }
}
