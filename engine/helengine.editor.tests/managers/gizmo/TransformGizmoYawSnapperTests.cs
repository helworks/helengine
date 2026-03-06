using helengine;
using helengine.editor;
using Xunit;

namespace helengine.editor.tests.managers.gizmo {
    /// <summary>
    /// Verifies snapped transform-gizmo yaw decisions derived from camera position.
    /// </summary>
    public class TransformGizmoYawSnapperTests {
        /// <summary>
        /// Radius used when constructing camera positions around the gizmo.
        /// </summary>
        const double CameraRadius = 10.0;
        /// <summary>
        /// Multiplier used to convert degrees into radians.
        /// </summary>
        const double DegreeToRadian = Math.PI / 180.0;

        /// <summary>
        /// Ensures zero horizontal distance falls back to identity quarter-turn selection.
        /// </summary>
        [Fact]
        public void ComputeSnappedQuarterTurns_ReturnsZero_WhenHorizontalOffsetIsZero() {
            float3 gizmoPosition = new float3(5f, 2f, -3f);
            float3 cameraPosition = new float3(5f, 12f, -3f);

            int snappedQuarterTurns = TransformGizmoYawSnapper.ComputeSnappedQuarterTurns(gizmoPosition, cameraPosition);

            Assert.Equal(0, snappedQuarterTurns);
        }

        /// <summary>
        /// Ensures positive yaw angles remain on the forward quarter turn until the delayed right-side threshold.
        /// </summary>
        [Theory]
        [InlineData(10.0)]
        [InlineData(30.0)]
        [InlineData(49.9)]
        public void ComputeSnappedQuarterTurns_StaysAtZero_BeforePositiveFiftyDegrees(double cameraYawDegrees) {
            float3 gizmoPosition = float3.Zero;
            float3 cameraPosition = CreateCameraPositionFromYawDegrees(cameraYawDegrees);

            int snappedQuarterTurns = TransformGizmoYawSnapper.ComputeSnappedQuarterTurns(gizmoPosition, cameraPosition);

            Assert.Equal(0, snappedQuarterTurns);
        }

        /// <summary>
        /// Ensures positive yaw angles cross into the next quarter turn after the delayed right-side threshold.
        /// </summary>
        [Theory]
        [InlineData(50.1, 1)]
        [InlineData(90.0, 1)]
        [InlineData(134.9, 1)]
        [InlineData(140.1, 2)]
        [InlineData(179.0, 2)]
        public void ComputeSnappedQuarterTurns_SnapsPositiveAngles_AfterDelayedThreshold(double cameraYawDegrees, int expectedQuarterTurns) {
            float3 gizmoPosition = float3.Zero;
            float3 cameraPosition = CreateCameraPositionFromYawDegrees(cameraYawDegrees);

            int snappedQuarterTurns = TransformGizmoYawSnapper.ComputeSnappedQuarterTurns(gizmoPosition, cameraPosition);

            Assert.Equal(expectedQuarterTurns, snappedQuarterTurns);
        }

        /// <summary>
        /// Ensures negative yaw angles remain on the forward quarter turn until the advanced left-side threshold.
        /// </summary>
        [Theory]
        [InlineData(-10.0)]
        [InlineData(-30.0)]
        [InlineData(-39.9)]
        public void ComputeSnappedQuarterTurns_StaysAtZero_BeforeNegativeFortyDegrees(double cameraYawDegrees) {
            float3 gizmoPosition = float3.Zero;
            float3 cameraPosition = CreateCameraPositionFromYawDegrees(cameraYawDegrees);

            int snappedQuarterTurns = TransformGizmoYawSnapper.ComputeSnappedQuarterTurns(gizmoPosition, cameraPosition);

            Assert.Equal(0, snappedQuarterTurns);
        }

        /// <summary>
        /// Ensures negative yaw angles cross into the next quarter turn after the advanced left-side threshold.
        /// </summary>
        [Theory]
        [InlineData(-40.1, -1)]
        [InlineData(-90.0, -1)]
        [InlineData(-129.9, -1)]
        [InlineData(-130.1, -2)]
        [InlineData(-135.1, -2)]
        [InlineData(-179.0, -2)]
        public void ComputeSnappedQuarterTurns_SnapsNegativeAngles_AfterAdvancedThreshold(double cameraYawDegrees, int expectedQuarterTurns) {
            float3 gizmoPosition = float3.Zero;
            float3 cameraPosition = CreateCameraPositionFromYawDegrees(cameraYawDegrees);

            int snappedQuarterTurns = TransformGizmoYawSnapper.ComputeSnappedQuarterTurns(gizmoPosition, cameraPosition);

            Assert.Equal(expectedQuarterTurns, snappedQuarterTurns);
        }

        /// <summary>
        /// Ensures key cardinal directions map to expected non-mirrored quarter-turn values.
        /// </summary>
        [Theory]
        [InlineData(0.0, 0)]
        [InlineData(90.0, 1)]
        [InlineData(180.0, 2)]
        [InlineData(-90.0, -1)]
        public void ComputeSnappedQuarterTurns_MapsCardinalDirections(double cameraYawDegrees, int expectedQuarterTurns) {
            float3 gizmoPosition = float3.Zero;
            float3 cameraPosition = CreateCameraPositionFromYawDegrees(cameraYawDegrees);

            int snappedQuarterTurns = TransformGizmoYawSnapper.ComputeSnappedQuarterTurns(gizmoPosition, cameraPosition);

            Assert.Equal(expectedQuarterTurns, snappedQuarterTurns);
        }

        /// <summary>
        /// Creates a camera world position from a yaw angle around the world Y axis.
        /// </summary>
        /// <param name="yawDegrees">Yaw angle in degrees where zero points to positive Z.</param>
        /// <returns>World-space camera position on a horizontal orbit around the gizmo origin.</returns>
        static float3 CreateCameraPositionFromYawDegrees(double yawDegrees) {
            double radians = yawDegrees * DegreeToRadian;
            double x = Math.Sin(radians) * CameraRadius;
            double z = Math.Cos(radians) * CameraRadius;
            return new float3((float)x, 0f, (float)z);
        }
    }
}
