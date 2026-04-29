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
        /// Quarter-turn angle in radians used by snapped gizmo yaw states.
        /// </summary>
        const double QuarterTurnRadians = Math.PI * 0.5;
        /// <summary>
        /// Tolerance used when comparing floating-point visibility scores.
        /// </summary>
        const double ScoreTolerance = 0.000000001;
        /// <summary>
        /// Candidate snapped quarter-turn values that cover one full horizontal orbit.
        /// </summary>
        static readonly int[] CandidateQuarterTurns = new[] { -1, 0, 1, 2 };
        /// <summary>
        /// World-up axis used when constructing snapped yaw orientations.
        /// </summary>
        static readonly float3 WorldUpAxis = new float3(0f, 1f, 0f);

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
        /// Ensures snapped quarter turns switch at the cardinal orbit boundaries where one horizontal arrow would otherwise collapse.
        /// </summary>
        /// <param name="cameraYawDegrees">Horizontal camera yaw around the gizmo.</param>
        /// <param name="expectedQuarterTurns">Expected snapped quarter-turn value.</param>
        [Theory]
        [InlineData(-179.9, -1)]
        [InlineData(-135.0, -1)]
        [InlineData(-90.1, -1)]
        [InlineData(-90.0, 0)]
        [InlineData(-89.9, 0)]
        [InlineData(-45.0, 0)]
        [InlineData(-0.1, 0)]
        [InlineData(0.0, 1)]
        [InlineData(0.1, 1)]
        [InlineData(45.0, 1)]
        [InlineData(89.9, 1)]
        [InlineData(90.0, 2)]
        [InlineData(90.1, 2)]
        [InlineData(135.0, 2)]
        [InlineData(179.9, 2)]
        [InlineData(180.0, -1)]
        public void ComputeSnappedQuarterTurns_UsesCardinalSectorBoundaries(double cameraYawDegrees, int expectedQuarterTurns) {
            float3 gizmoPosition = float3.Zero;
            float3 cameraPosition = CreateCameraPositionFromYawDegrees(cameraYawDegrees);

            int snappedQuarterTurns = TransformGizmoYawSnapper.ComputeSnappedQuarterTurns(gizmoPosition, cameraPosition);

            Assert.Equal(expectedQuarterTurns, snappedQuarterTurns);
        }

        /// <summary>
        /// Ensures every off-cardinal integer orbit angle keeps the horizontal arrows split across opposite viewport sides.
        /// </summary>
        [Fact]
        public void ComputeSnappedYawFacingOrientation_SplitsHorizontalArrowsAcrossViewport_ForOrbitSweep() {
            for (int cameraYawDegrees = -179; cameraYawDegrees <= 179; cameraYawDegrees++) {
                if (cameraYawDegrees == -90 || cameraYawDegrees == 0 || cameraYawDegrees == 90) {
                    continue;
                }

                float3 gizmoPosition = float3.Zero;
                float3 cameraPosition = CreateCameraPositionFromYawDegrees(cameraYawDegrees);
                float4 snappedOrientation = TransformGizmoYawSnapper.ComputeSnappedYawFacingOrientation(gizmoPosition, cameraPosition);
                ComputeHorizontalArrowVisibility(snappedOrientation, cameraPosition, out bool arrowsSplitAcrossViewport, out double minimumHorizontalOffset, out double totalHorizontalOffset);

                Assert.True(arrowsSplitAcrossViewport, $"Yaw {cameraYawDegrees} should keep the horizontal arrows on opposite sides of the viewport.");
                Assert.True(minimumHorizontalOffset > 0.0, $"Yaw {cameraYawDegrees} should keep both horizontal arrows horizontally separated from the gizmo origin.");
                Assert.True(totalHorizontalOffset > minimumHorizontalOffset, $"Yaw {cameraYawDegrees} should keep both horizontal arrows visible.");
            }
        }

        /// <summary>
        /// Ensures the snapped yaw orientation keeps the horizontal positive-axis arrows on opposite sides of the viewport away from exact cardinal views.
        /// </summary>
        /// <param name="cameraYawDegrees">Horizontal camera yaw around the gizmo.</param>
        [Theory]
        [InlineData(-170.0)]
        [InlineData(-140.0)]
        [InlineData(-110.0)]
        [InlineData(-80.0)]
        [InlineData(-50.0)]
        [InlineData(-20.0)]
        [InlineData(-10.0)]
        [InlineData(10.0)]
        [InlineData(20.0)]
        [InlineData(50.0)]
        [InlineData(80.0)]
        [InlineData(110.0)]
        [InlineData(140.0)]
        [InlineData(170.0)]
        public void ComputeSnappedYawFacingOrientation_SplitsHorizontalArrowsAcrossViewport(double cameraYawDegrees) {
            float3 gizmoPosition = float3.Zero;
            float3 cameraPosition = CreateCameraPositionFromYawDegrees(cameraYawDegrees);
            float4 snappedOrientation = TransformGizmoYawSnapper.ComputeSnappedYawFacingOrientation(gizmoPosition, cameraPosition);

            ComputeHorizontalArrowVisibility(snappedOrientation, cameraPosition, out bool arrowsSplitAcrossViewport, out double minimumHorizontalOffset, out double totalHorizontalOffset);

            Assert.True(arrowsSplitAcrossViewport);
            Assert.True(minimumHorizontalOffset > 0.0);
            Assert.True(totalHorizontalOffset > minimumHorizontalOffset);
        }

        /// <summary>
        /// Ensures every off-cardinal integer orbit angle chooses the strongest horizontal-arrow visibility layout among the four snapped candidates.
        /// </summary>
        [Fact]
        public void ComputeSnappedYawFacingOrientation_SelectsBestHorizontalArrowVisibility_ForOrbitSweep() {
            for (int cameraYawDegrees = -179; cameraYawDegrees <= 179; cameraYawDegrees++) {
                if (cameraYawDegrees == -90 || cameraYawDegrees == 0 || cameraYawDegrees == 90) {
                    continue;
                }

                float3 gizmoPosition = float3.Zero;
                float3 cameraPosition = CreateCameraPositionFromYawDegrees(cameraYawDegrees);
                float4 actualOrientation = TransformGizmoYawSnapper.ComputeSnappedYawFacingOrientation(gizmoPosition, cameraPosition);
                ComputeHorizontalArrowVisibility(actualOrientation, cameraPosition, out bool actualSplit, out double actualMinimumOffset, out double actualTotalOffset);
                FindBestQuarterTurnsForHorizontalArrowVisibility(cameraPosition, out int bestQuarterTurns, out bool bestSplit, out double bestMinimumOffset, out double bestTotalOffset);

                Assert.True(bestSplit, $"Yaw {cameraYawDegrees} should have a snapped layout that places the horizontal arrows on opposite viewport sides.");
                Assert.Equal(bestQuarterTurns, TransformGizmoYawSnapper.ComputeSnappedQuarterTurns(gizmoPosition, cameraPosition));
                Assert.False(
                    IsCandidateScoreBetter(bestSplit, bestMinimumOffset, bestTotalOffset, actualSplit, actualMinimumOffset, actualTotalOffset),
                    $"Yaw {cameraYawDegrees} should not have a snapped candidate with better horizontal-arrow visibility than the chosen orientation.");
                Assert.False(
                    IsCandidateScoreBetter(actualSplit, actualMinimumOffset, actualTotalOffset, bestSplit, bestMinimumOffset, bestTotalOffset),
                    $"Yaw {cameraYawDegrees} should match the best horizontal-arrow visibility score.");
            }
        }

        /// <summary>
        /// Ensures the snapped yaw orientation matches the most usable horizontal-arrow layout among the four 90-degree candidates.
        /// </summary>
        /// <param name="cameraYawDegrees">Horizontal camera yaw around the gizmo.</param>
        [Theory]
        [InlineData(-170.0)]
        [InlineData(-140.0)]
        [InlineData(-110.0)]
        [InlineData(-80.0)]
        [InlineData(-50.0)]
        [InlineData(-20.0)]
        [InlineData(-10.0)]
        [InlineData(10.0)]
        [InlineData(20.0)]
        [InlineData(50.0)]
        [InlineData(80.0)]
        [InlineData(110.0)]
        [InlineData(140.0)]
        [InlineData(170.0)]
        public void ComputeSnappedYawFacingOrientation_SelectsBestHorizontalArrowVisibility(double cameraYawDegrees) {
            float3 gizmoPosition = float3.Zero;
            float3 cameraPosition = CreateCameraPositionFromYawDegrees(cameraYawDegrees);
            float4 actualOrientation = TransformGizmoYawSnapper.ComputeSnappedYawFacingOrientation(gizmoPosition, cameraPosition);

            ComputeHorizontalArrowVisibility(actualOrientation, cameraPosition, out bool actualSplit, out double actualMinimumOffset, out double actualTotalOffset);
            FindBestQuarterTurnsForHorizontalArrowVisibility(cameraPosition, out int bestQuarterTurns, out bool bestSplit, out double bestMinimumOffset, out double bestTotalOffset);

            Assert.True(bestSplit);
            Assert.Equal(bestQuarterTurns, TransformGizmoYawSnapper.ComputeSnappedQuarterTurns(gizmoPosition, cameraPosition));
            Assert.False(IsCandidateScoreBetter(bestSplit, bestMinimumOffset, bestTotalOffset, actualSplit, actualMinimumOffset, actualTotalOffset));
            Assert.False(IsCandidateScoreBetter(actualSplit, actualMinimumOffset, actualTotalOffset, bestSplit, bestMinimumOffset, bestTotalOffset));
        }

        /// <summary>
        /// Finds the best snapped quarter-turn layout for horizontal-arrow visibility from the current camera position.
        /// </summary>
        /// <param name="cameraPosition">Camera world position on the horizontal orbit.</param>
        /// <param name="bestQuarterTurns">Best snapped quarter-turn value.</param>
        /// <param name="bestSplit">True when the best layout places the arrows on opposite viewport sides.</param>
        /// <param name="bestMinimumOffset">Minimum absolute horizontal screen offset among the two horizontal arrows.</param>
        /// <param name="bestTotalOffset">Total absolute horizontal screen offset across both horizontal arrows.</param>
        static void FindBestQuarterTurnsForHorizontalArrowVisibility(
            float3 cameraPosition,
            out int bestQuarterTurns,
            out bool bestSplit,
            out double bestMinimumOffset,
            out double bestTotalOffset) {
            bestQuarterTurns = CandidateQuarterTurns[0];
            float4 bestOrientation = CreateOrientationFromQuarterTurns(bestQuarterTurns);
            ComputeHorizontalArrowVisibility(bestOrientation, cameraPosition, out bestSplit, out bestMinimumOffset, out bestTotalOffset);

            for (int candidateIndex = 1; candidateIndex < CandidateQuarterTurns.Length; candidateIndex++) {
                int candidateQuarterTurns = CandidateQuarterTurns[candidateIndex];
                float4 candidateOrientation = CreateOrientationFromQuarterTurns(candidateQuarterTurns);
                ComputeHorizontalArrowVisibility(candidateOrientation, cameraPosition, out bool candidateSplit, out double candidateMinimumOffset, out double candidateTotalOffset);
                if (!IsCandidateScoreBetter(candidateSplit, candidateMinimumOffset, candidateTotalOffset, bestSplit, bestMinimumOffset, bestTotalOffset)) {
                    continue;
                }

                bestQuarterTurns = candidateQuarterTurns;
                bestSplit = candidateSplit;
                bestMinimumOffset = candidateMinimumOffset;
                bestTotalOffset = candidateTotalOffset;
            }
        }

        /// <summary>
        /// Determines whether one horizontal-arrow visibility score is better than another.
        /// </summary>
        /// <param name="candidateSplit">True when the candidate places arrows on opposite viewport sides.</param>
        /// <param name="candidateMinimumOffset">Candidate minimum absolute horizontal offset.</param>
        /// <param name="candidateTotalOffset">Candidate total absolute horizontal offset.</param>
        /// <param name="currentSplit">True when the current best score places arrows on opposite viewport sides.</param>
        /// <param name="currentMinimumOffset">Current minimum absolute horizontal offset.</param>
        /// <param name="currentTotalOffset">Current total absolute horizontal offset.</param>
        /// <returns>True when the candidate score should replace the current best score.</returns>
        static bool IsCandidateScoreBetter(
            bool candidateSplit,
            double candidateMinimumOffset,
            double candidateTotalOffset,
            bool currentSplit,
            double currentMinimumOffset,
            double currentTotalOffset) {
            if (candidateSplit != currentSplit) {
                return candidateSplit && !currentSplit;
            }

            if (candidateMinimumOffset > currentMinimumOffset + ScoreTolerance) {
                return true;
            }

            if (candidateMinimumOffset < currentMinimumOffset - ScoreTolerance) {
                return false;
            }

            return candidateTotalOffset > currentTotalOffset + ScoreTolerance;
        }

        /// <summary>
        /// Computes the horizontal visibility score for the positive X and positive Z gizmo arrows.
        /// </summary>
        /// <param name="orientation">Snapped gizmo orientation to evaluate.</param>
        /// <param name="cameraPosition">Camera world position on the horizontal orbit.</param>
        /// <param name="arrowsSplitAcrossViewport">True when the arrows project onto opposite viewport sides.</param>
        /// <param name="minimumHorizontalOffset">Minimum absolute horizontal screen offset among both arrows.</param>
        /// <param name="totalHorizontalOffset">Total absolute horizontal screen offset among both arrows.</param>
        static void ComputeHorizontalArrowVisibility(
            float4 orientation,
            float3 cameraPosition,
            out bool arrowsSplitAcrossViewport,
            out double minimumHorizontalOffset,
            out double totalHorizontalOffset) {
            float3 xTip = float4.RotateVector(new float3(TransformTranslationGizmoFactory.AxisLength, 0f, 0f), orientation);
            float3 zTip = float4.RotateVector(new float3(0f, 0f, TransformTranslationGizmoFactory.AxisLength), orientation);
            double xOffset = ProjectHorizontalScreenOffset(cameraPosition, xTip);
            double zOffset = ProjectHorizontalScreenOffset(cameraPosition, zTip);

            arrowsSplitAcrossViewport =
                (xOffset < 0.0 && zOffset > 0.0) ||
                (xOffset > 0.0 && zOffset < 0.0);
            minimumHorizontalOffset = Math.Min(Math.Abs(xOffset), Math.Abs(zOffset));
            totalHorizontalOffset = Math.Abs(xOffset) + Math.Abs(zOffset);
        }

        /// <summary>
        /// Projects a world-space point onto the camera's horizontal screen axis while the camera looks back toward the gizmo origin.
        /// </summary>
        /// <param name="cameraPosition">Camera world position on the horizontal orbit.</param>
        /// <param name="worldPoint">World-space point to project.</param>
        /// <returns>Signed horizontal screen offset relative to the gizmo center projection.</returns>
        static double ProjectHorizontalScreenOffset(float3 cameraPosition, float3 worldPoint) {
            float3 forward = NormalizeHorizontal(new float3(-cameraPosition.X, 0f, -cameraPosition.Z));
            float3 right = NormalizeHorizontal(float3.Cross(forward, WorldUpAxis));
            float3 toPoint = worldPoint - cameraPosition;
            double depth = float3.Dot(toPoint, forward);
            if (depth <= 0.0) {
                throw new InvalidOperationException("Projected gizmo arrow tip must remain in front of the validation camera.");
            }

            return float3.Dot(toPoint, right) / depth;
        }

        /// <summary>
        /// Creates a snapped yaw orientation from a snapped quarter-turn value.
        /// </summary>
        /// <param name="snappedQuarterTurns">Snapped quarter-turn value returned by the yaw snapper.</param>
        /// <returns>Quaternion representing the snapped world-space yaw.</returns>
        static float4 CreateOrientationFromQuarterTurns(int snappedQuarterTurns) {
            double snappedYawRadians = (snappedQuarterTurns * QuarterTurnRadians) - QuarterTurnRadians;
            float3 axis = WorldUpAxis;
            float4 orientation;
            float4.CreateFromAxisAngle(ref axis, (float)snappedYawRadians, out orientation);
            return orientation;
        }

        /// <summary>
        /// Normalizes a horizontal vector.
        /// </summary>
        /// <param name="value">Horizontal vector to normalize.</param>
        /// <returns>Normalized horizontal vector.</returns>
        static float3 NormalizeHorizontal(float3 value) {
            double lengthSquared =
                (value.X * value.X) +
                (value.Z * value.Z);
            if (lengthSquared <= 0.0) {
                throw new InvalidOperationException("Horizontal vector must be non-zero.");
            }

            double inverseLength = 1.0 / Math.Sqrt(lengthSquared);
            return new float3(
                (float)(value.X * inverseLength),
                0f,
                (float)(value.Z * inverseLength));
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
