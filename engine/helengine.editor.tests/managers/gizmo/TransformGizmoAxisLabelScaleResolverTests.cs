using helengine.editor;
using Xunit;

namespace helengine.editor.tests.managers.gizmo {
    /// <summary>
    /// Verifies drag-aware scale selection for transform-gizmo axis labels.
    /// </summary>
    public class TransformGizmoAxisLabelScaleResolverTests {
        /// <summary>
        /// Ensures normal camera movement keeps the live computed gizmo scale.
        /// </summary>
        [Fact]
        public void Resolve_ReturnsComputedScale_WhenNotDragging() {
            double resolvedScale = TransformGizmoAxisLabelScaleResolver.Resolve(false, 2.5, 9.0);

            Assert.Equal(2.5, resolvedScale);
        }

        /// <summary>
        /// Ensures drag-time labels keep the frozen gizmo scale instead of resizing from camera motion.
        /// </summary>
        [Fact]
        public void Resolve_ReturnsFrozenScale_WhenDraggingAndFrozenScaleIsPositive() {
            double resolvedScale = TransformGizmoAxisLabelScaleResolver.Resolve(true, 2.5, 9.0);

            Assert.Equal(9.0, resolvedScale);
        }

        /// <summary>
        /// Ensures drag-time scale resolution falls back to the computed size when no frozen gizmo scale is available.
        /// </summary>
        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        public void Resolve_FallsBackToComputedScale_WhenFrozenScaleIsNotPositive(double frozenScale) {
            double resolvedScale = TransformGizmoAxisLabelScaleResolver.Resolve(true, 2.5, frozenScale);

            Assert.Equal(2.5, resolvedScale);
        }

        /// <summary>
        /// Ensures invalid computed scales fail fast instead of hiding a broken gizmo-size calculation.
        /// </summary>
        [Theory]
        [InlineData(0.0)]
        [InlineData(-0.5)]
        public void Resolve_Throws_WhenComputedScaleIsNotPositive(double computedScale) {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TransformGizmoAxisLabelScaleResolver.Resolve(true, computedScale, 4.0));
        }
    }
}
