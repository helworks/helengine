using Xunit;

namespace helengine.editor.tests.managers.viewport {
    /// <summary>
    /// Verifies visible navigation-cube faces, edges, corners, and the shared render projection.
    /// </summary>
    public sealed class EditorViewportNavigationCubeGeometryTests {
        /// <summary>
        /// Ensures each signed cube direction wins the center region when its face, edge, or corner faces the camera.
        /// </summary>
        /// <param name="x">Signed X direction of the expected target.</param>
        /// <param name="y">Signed Y direction of the expected target.</param>
        /// <param name="z">Signed Z direction of the expected target.</param>
        [Theory]
        [InlineData(-1, -1, -1)]
        [InlineData(-1, -1, 0)]
        [InlineData(-1, -1, 1)]
        [InlineData(-1, 0, -1)]
        [InlineData(-1, 0, 0)]
        [InlineData(-1, 0, 1)]
        [InlineData(-1, 1, -1)]
        [InlineData(-1, 1, 0)]
        [InlineData(-1, 1, 1)]
        [InlineData(0, -1, -1)]
        [InlineData(0, -1, 0)]
        [InlineData(0, -1, 1)]
        [InlineData(0, 0, -1)]
        [InlineData(0, 0, 1)]
        [InlineData(0, 1, -1)]
        [InlineData(0, 1, 0)]
        [InlineData(0, 1, 1)]
        [InlineData(1, -1, -1)]
        [InlineData(1, -1, 0)]
        [InlineData(1, -1, 1)]
        [InlineData(1, 0, -1)]
        [InlineData(1, 0, 0)]
        [InlineData(1, 0, 1)]
        [InlineData(1, 1, -1)]
        [InlineData(1, 1, 0)]
        [InlineData(1, 1, 1)]
        public void TryHit_CenterOfVisibleFaceEdgeOrCorner_ResolvesAllTwentySixTargets(int x, int y, int z) {
            EditorViewportNavigationCubeGeometry geometry = new EditorViewportNavigationCubeGeometry();
            EditorViewportNavigationTarget expectedTarget = new EditorViewportNavigationTarget(x, y, z);
            float4 cameraOrientation = CreateCameraOrientationForTarget(expectedTarget);
            float3 cameraBackward = float4.RotateVector(new float3(0f, 0f, 1f), cameraOrientation);
            float targetLength = (float)Math.Sqrt((x * x) + (y * y) + (z * z));
            AssertVectorApproximately(new float3(x / targetLength, y / targetLength, z / targetLength), cameraBackward, 0.0001f);

            bool hasHit = geometry.TryHit(new float2(48f, 48f), cameraOrientation, 96f, out EditorViewportNavigationCubeHit hit);

            Assert.True(hasHit);
            Assert.Equal(expectedTarget, hit.Target);
            Assert.True(float.IsFinite(hit.VisibleFaceDepth));
        }

        /// <summary>
        /// Ensures an aligned front-facing view never resolves points to the occluded rear face.
        /// </summary>
        [Fact]
        public void TryHit_AlignedFrontView_NeverSelectsOccludedRearFace() {
            EditorViewportNavigationCubeGeometry geometry = new EditorViewportNavigationCubeGeometry();
            EditorViewportNavigationTarget rearFace = new EditorViewportNavigationTarget(0, 0, -1);

            for (int y = 0; y <= 96; y += 3) {
                for (int x = 0; x <= 96; x += 3) {
                    if (geometry.TryHit(new float2(x, y), float4.Identity, 96f, out EditorViewportNavigationCubeHit hit)) {
                        Assert.NotEqual(rearFace, hit.Target);
                    }
                }
            }
        }

        /// <summary>
        /// Ensures drawing vertices and hit-tested faces remain co-located after orientation and UI scaling changes.
        /// </summary>
        /// <param name="x">Signed X direction being brought to the cube center.</param>
        /// <param name="y">Signed Y direction being brought to the cube center.</param>
        /// <param name="z">Signed Z direction being brought to the cube center.</param>
        [Theory]
        [InlineData(-1, 0, 0)]
        [InlineData(0, 1, 0)]
        [InlineData(0, 0, -1)]
        [InlineData(-1, 1, 0)]
        [InlineData(1, 0, -1)]
        [InlineData(1, 1, 1)]
        public void GetProjectedVerticesAndTryHit_RotatedAtTwoSizes_UseSameCenteredGeometry(int x, int y, int z) {
            EditorViewportNavigationCubeGeometry geometry = new EditorViewportNavigationCubeGeometry();
            EditorViewportNavigationTarget expectedTarget = new EditorViewportNavigationTarget(x, y, z);
            float4 cameraOrientation = CreateCameraOrientationForTarget(expectedTarget);

            for (int size = 96; size <= 192; size += 96) {
                IReadOnlyList<float2> vertices = geometry.GetProjectedVertices(cameraOrientation, size);
                Assert.Equal(8, vertices.Count);
                for (int vertexIndex = 0; vertexIndex < vertices.Count; vertexIndex++) {
                    Assert.InRange(vertices[vertexIndex].X, 0f, size);
                    Assert.InRange(vertices[vertexIndex].Y, 0f, size);
                }

                Assert.True(geometry.TryHit(new float2(size * 0.5f, size * 0.5f), cameraOrientation, size, out EditorViewportNavigationCubeHit hit));
                Assert.Equal(expectedTarget, hit.Target);
            }
        }

        /// <summary>
        /// Ensures invalid cube bounds are rejected instead of producing a non-finite projected model.
        /// </summary>
        [Fact]
        public void GetProjectedVertices_ZeroSize_ThrowsArgumentOutOfRange() {
            EditorViewportNavigationCubeGeometry geometry = new EditorViewportNavigationCubeGeometry();

            Assert.Throws<ArgumentOutOfRangeException>(() => geometry.GetProjectedVertices(float4.Identity, 0f));
        }

        /// <summary>
        /// Constructs the camera orientation whose backward axis faces the selected signed cube direction.
        /// </summary>
        /// <param name="target">Cube direction to bring to the center of the view.</param>
        /// <returns>Normalized camera orientation.</returns>
        /// <summary>
        /// Checks two direction vectors within a per-component tolerance.
        /// </summary>
        /// <param name="expected">Expected direction.</param>
        /// <param name="actual">Actual direction.</param>
        /// <param name="tolerance">Maximum accepted component difference.</param>
        static void AssertVectorApproximately(float3 expected, float3 actual, float tolerance) {
            Assert.InRange(Math.Abs(expected.X - actual.X), 0f, tolerance);
            Assert.InRange(Math.Abs(expected.Y - actual.Y), 0f, tolerance);
            Assert.InRange(Math.Abs(expected.Z - actual.Z), 0f, tolerance);
        }
        static float4 CreateCameraOrientationForTarget(EditorViewportNavigationTarget target) {
            float targetLength = (float)Math.Sqrt((target.X * target.X) + (target.Y * target.Y) + (target.Z * target.Z));
            float3 forward = new float3(-target.X / targetLength, -target.Y / targetLength, -target.Z / targetLength);
            double yaw = Math.Atan2(-forward.X, -forward.Z);
            double pitch = Math.Asin(Math.Clamp(forward.Y, -1f, 1f));
            float4 orientation;
            float4.CreateFromYawPitchRoll((float)yaw, (float)pitch, 0f, out orientation);
            orientation.Normalize();
            return orientation;
        }
    }
}
