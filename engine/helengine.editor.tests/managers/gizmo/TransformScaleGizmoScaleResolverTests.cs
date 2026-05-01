using helengine;
using helengine.editor;
using Xunit;

namespace helengine.editor.tests.managers.gizmo {
    /// <summary>
    /// Verifies scale-vector resolution used by the scale gizmo drag controller.
    /// </summary>
    public class TransformScaleGizmoScaleResolverTests {
        /// <summary>
        /// Ensures axis scaling updates only the dominant component for the active handle direction.
        /// </summary>
        [Fact]
        public void ResolveAxisScale_UpdatesOnlyDominantAxisComponent() {
            float3 resolvedScale = TransformScaleGizmoScaleResolver.ResolveAxisScale(
                new float3(1f, 2f, 3f),
                new float3(0f, 0f, -1f),
                0.5,
                0.0001f);

            Assert.Equal(1f, resolvedScale.X);
            Assert.Equal(2f, resolvedScale.Y);
            Assert.Equal(3.5f, resolvedScale.Z);
        }

        /// <summary>
        /// Ensures axis scaling clamps the resolved component to the configured minimum.
        /// </summary>
        [Fact]
        public void ResolveAxisScale_ClampsToMinimumScaleComponent() {
            float3 resolvedScale = TransformScaleGizmoScaleResolver.ResolveAxisScale(
                new float3(1f, 2f, 3f),
                new float3(1f, 0f, 0f),
                -5.0,
                0.25f);

            Assert.Equal(0.25f, resolvedScale.X);
            Assert.Equal(2f, resolvedScale.Y);
            Assert.Equal(3f, resolvedScale.Z);
        }

        /// <summary>
        /// Ensures axis scaling snaps the drag delta to the configured increment before updating the target component.
        /// </summary>
        [Fact]
        public void ResolveSnappedAxisScale_UsesConfiguredIncrement() {
            float3 resolvedScale = TransformScaleGizmoScaleResolver.ResolveSnappedAxisScale(
                new float3(1f, 2f, 3f),
                new float3(1f, 0f, 0f),
                0.16,
                0.1,
                0.0001f);

            Assert.Equal(1.2f, resolvedScale.X);
            Assert.Equal(2f, resolvedScale.Y);
            Assert.Equal(3f, resolvedScale.Z);
        }

        /// <summary>
        /// Ensures snapped axis scaling resolves to the fixed scale grid even when the starting value is offset.
        /// </summary>
        [Fact]
        public void ResolveSnappedAxisScale_SnapsTheFinalAxisComponentToTheFixedGrid() {
            float3 resolvedScale = TransformScaleGizmoScaleResolver.ResolveSnappedAxisScale(
                new float3(1.06f, 2f, 3f),
                new float3(1f, 0f, 0f),
                0.0,
                0.1,
                0.0001f);

            Assert.Equal(1.1f, resolvedScale.X);
            Assert.Equal(2f, resolvedScale.Y);
            Assert.Equal(3f, resolvedScale.Z);
        }

        /// <summary>
        /// Ensures plane scaling updates both mapped components using the world-plane delta.
        /// </summary>
        [Fact]
        public void ResolvePlaneScale_UpdatesBothPlaneComponents() {
            float3 resolvedScale = TransformScaleGizmoScaleResolver.ResolvePlaneScale(
                new float3(1f, 2f, 3f),
                new float3(0f, 1f, 0f),
                new float3(1f, 0f, 0f),
                new float3(0.25f, 0.75f, 0f),
                0.0001f);

            Assert.Equal(1.25f, resolvedScale.X);
            Assert.Equal(2.75f, resolvedScale.Y);
            Assert.Equal(3f, resolvedScale.Z);
        }

        /// <summary>
        /// Ensures plane scaling respects snapped world directions that point toward negative world axes.
        /// </summary>
        [Fact]
        public void ResolvePlaneScale_UsesSignedDeltaAlongSnappedDirections() {
            float3 resolvedScale = TransformScaleGizmoScaleResolver.ResolvePlaneScale(
                new float3(1f, 2f, 3f),
                new float3(-1f, 0f, 0f),
                new float3(0f, 0f, -1f),
                new float3(-0.5f, 0f, -0.25f),
                0.0001f);

            Assert.Equal(1.5f, resolvedScale.X);
            Assert.Equal(2f, resolvedScale.Y);
            Assert.Equal(3.25f, resolvedScale.Z);
        }

        /// <summary>
        /// Ensures plane scaling snaps both basis directions independently before applying the scale update.
        /// </summary>
        [Fact]
        public void ResolveSnappedPlaneScale_SnapsBothPlaneComponents() {
            float3 resolvedScale = TransformScaleGizmoScaleResolver.ResolveSnappedPlaneScale(
                new float3(1f, 2f, 3f),
                new float3(1f, 0f, 0f),
                new float3(0f, 1f, 0f),
                new float3(0.13f, 0.36f, 0f),
                0.25,
                0.0001f);

            Assert.Equal(1.25f, resolvedScale.X);
            Assert.Equal(2.25f, resolvedScale.Y);
            Assert.Equal(3f, resolvedScale.Z);
        }

        /// <summary>
        /// Ensures snapped plane scaling resolves both components to the fixed scale grid even when the starting values are offset.
        /// </summary>
        [Fact]
        public void ResolveSnappedPlaneScale_SnapsTheFinalPlaneComponentsToTheFixedGrid() {
            float3 resolvedScale = TransformScaleGizmoScaleResolver.ResolveSnappedPlaneScale(
                new float3(1.06f, 2.16f, 3f),
                new float3(1f, 0f, 0f),
                new float3(0f, 1f, 0f),
                new float3(0f, 0f, 0f),
                0.1,
                0.0001f);

            Assert.Equal(1.1f, resolvedScale.X);
            Assert.Equal(2.2f, resolvedScale.Y);
            Assert.Equal(3f, resolvedScale.Z);
        }
    }
}
