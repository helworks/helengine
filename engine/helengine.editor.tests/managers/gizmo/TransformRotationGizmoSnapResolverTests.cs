using helengine.editor;
using Xunit;

namespace helengine.editor.tests.managers.gizmo {
    /// <summary>
    /// Verifies snapped rotation deltas used by rotation gizmo dragging.
    /// </summary>
    public class TransformRotationGizmoSnapResolverTests {
        /// <summary>
        /// Converts degrees into radians for test inputs and expectations.
        /// </summary>
        const double DegreesToRadians = Math.PI / 180.0;
        /// <summary>
        /// Tolerance used when comparing floating-point snapped angles.
        /// </summary>
        const double AngleTolerance = 0.000000001;

        /// <summary>
        /// Ensures positive drag angles snap to the nearest configured increment.
        /// </summary>
        [Fact]
        public void ResolveSnappedDeltaAngle_SnapsPositiveAngleToNearestIncrement() {
            double resolvedAngle = TransformRotationGizmoSnapResolver.ResolveSnappedDeltaAngle(12.0 * DegreesToRadians, 5.0);

            Assert.Equal(10.0 * DegreesToRadians, resolvedAngle, AngleTolerance);
        }

        /// <summary>
        /// Ensures negative drag angles preserve sign when snapping to the configured increment.
        /// </summary>
        [Fact]
        public void ResolveSnappedDeltaAngle_PreservesNegativeAngleSign() {
            double resolvedAngle = TransformRotationGizmoSnapResolver.ResolveSnappedDeltaAngle(-13.0 * DegreesToRadians, 15.0);

            Assert.Equal(-15.0 * DegreesToRadians, resolvedAngle, AngleTolerance);
        }

        /// <summary>
        /// Ensures snapped rotation angles land on the fixed snap grid instead of preserving intermediate offsets.
        /// </summary>
        [Fact]
        public void ResolveSnappedAngle_SnapsToTheNearestFixedIncrement() {
            double resolvedAngle = TransformRotationGizmoSnapResolver.ResolveSnappedAngle(2.75 * DegreesToRadians, 2.5);

            Assert.Equal(2.5 * DegreesToRadians, resolvedAngle, AngleTolerance);
        }
    }
}
