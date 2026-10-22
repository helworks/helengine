using helengine;

namespace helengine.core.tests {
    /// <summary>Protects shared runtime rotation geometry and the drag math consumed by both editor and player.</summary>
    public class RotationGizmoTests {
        /// <summary>Verifies the editor's hollow double-sided ring retains its vertex layout and hole.</summary>
        [Fact]
        public void TubeRing_PreservesClosedDoubleSidedGeometry() {
            ModelAsset ring = RotationGizmoGeometry.CreateTubeRing(0.92f, 1.02f, 0.08f, 48);
            Assert.Equal(384, ring.Positions.Length);
            Assert.Equal(2304, ring.Indices16.Length);
            Assert.Equal(ring.Positions.Length, ring.Normals.Length);
            Assert.Equal(ring.Positions.Length, ring.TexCoords.Length);
            for (int index = 0; index < ring.Indices16.Length; index += 6) {
                Assert.Equal(ring.Indices16[index], ring.Indices16[index + 3]);
                Assert.Equal(ring.Indices16[index + 1], ring.Indices16[index + 5]);
                Assert.Equal(ring.Indices16[index + 2], ring.Indices16[index + 4]);
            }
            Assert.All(ring.Positions, position => Assert.InRange(Math.Sqrt((double)(position.X * position.X + position.Z * position.Z)), 0.91999, 1.02001));
        }

        /// <summary>Verifies each oriented local-Y ring uses the same axis mapping as the editor.</summary>
        [Theory]
        [InlineData(0, 1, 0, 0)]
        [InlineData(1, 0, 1, 0)]
        [InlineData(2, 0, 0, 1)]
        public void AxisOrientation_MapsLocalRingNormal(int axis, double x, double y, double z) {
            float3 normal = float4.RotateVector(new float3(0, 1, 0), RotationGizmoGeometry.GetAxisOrientation(axis));
            Assert.Equal(x, (double)normal.X, 6);
            Assert.Equal(y, (double)normal.Y, 6);
            Assert.Equal(z, (double)normal.Z, 6);
        }

        /// <summary>Verifies nearest triangle picking selects all three axes including edge-on tube faces.</summary>
        [Theory]
        [InlineData(0, 0, 0.7, 0.7, -1, 0, 0)]
        [InlineData(1, 0.7, 0, 0.7, 0, -1, 0)]
        [InlineData(2, 0.7, 0.7, 0, 0, 0, -1)]
        public void Picking_SelectsCorrectAxis(int expectedAxis, double x, double y, double z, double dx, double dy, double dz) {
            ModelAsset ring = RotationGizmoGeometry.CreateTubeRing(0.92f, 1.02f, 0.08f, 48);
            float3 point = new float3(x, y, z);
            float3 direction = new float3(dx, dy, dz);
            bool hit = RotationGizmoMath.TryPickAxis(ring, point - direction * 3, direction, float3.Zero, float4.Identity, 1, out int axis, out RealEvaluation distance);
            Assert.True(hit);
            Assert.Equal(expectedAxis, axis);
            Assert.InRange((double)distance, 2.95, 3.05);
        }

        /// <summary>Edge-on ring sidewalls remain pickable and the closest surface determines ownership at ring overlaps.</summary>
        [Fact]
        public void Picking_EdgeOnTubeSelectsNearestSurface() {
            ModelAsset ring = RotationGizmoGeometry.CreateTubeRing(0.92f, 1.02f, 0.08f, 48);
            Assert.True(RotationGizmoMath.TryPickAxis(ring, new float3(0.7, 0, 3), new float3(0, 0, -1), float3.Zero, float4.Identity, 1, out int axis, out RealEvaluation distance));
            Assert.Equal(1, axis);
            Assert.InRange((double)distance, 2.24, 2.28);
        }

        /// <summary>Picking uses the rendered pivot, orientation and uniform scale while reporting distance in world units.</summary>
        [Fact]
        public void Picking_TransformedGizmoMatchesRenderedRing() {
            ModelAsset ring = RotationGizmoGeometry.CreateTubeRing(0.92f, 1.02f, 0.08f, 48);
            float4 orientation = RotationGizmoMath.Rotate(float4.Identity, new float3(1, 2, 3), 0.8);
            float3 center = new float3(5, -3, 2);
            float3 direction = float4.RotateVector(new float3(0, -1, 0), orientation);
            float3 origin = center + float4.RotateVector(new float3(1.4, 6, 1.4), orientation);
            Assert.True(RotationGizmoMath.TryPickAxis(ring, origin, direction * 4, center, orientation, 2, out int axis, out RealEvaluation distance));
            Assert.Equal(1, axis);
            Assert.InRange((double)distance, 5.91, 5.93);
            Assert.False(RotationGizmoMath.TryPickAxis(ring, origin, float3.Zero, center, orientation, 2, out _, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => RotationGizmoMath.TryPickAxis(ring, origin, direction, center, orientation, 0, out _, out _));
        }

        /// <summary>Ensures the empty center of the rings never behaves as a filled pick disc.</summary>
        [Fact]
        public void Picking_ThroughHoleOrAwayFromRings_ReturnsFalse() {
            ModelAsset ring = RotationGizmoGeometry.CreateTubeRing(0.92f, 1.02f, 0.08f, 48);
            Assert.False(RotationGizmoMath.TryPickAxis(ring, new float3(0, 0, 3), new float3(0, 0, 1), float3.Zero, float4.Identity, 1, out int axis, out _));
            Assert.Equal(-1, axis);
            Assert.False(RotationGizmoMath.TryPickAxis(ring, new float3(0.2, 0.2, 3), new float3(0, 0, -1), float3.Zero, float4.Identity, 1, out _, out _));
            Assert.False(RotationGizmoMath.TryPickAxis(ring, new float3(0, 0, 3), new float3(0, 0, -1), new float3(5, 5, 0), float4.Identity, 1, out _, out _));
        }

        /// <summary>Rejects intersections behind the ray and degenerate or parallel rotation planes.</summary>
        [Fact]
        public void PlaneIntersection_RejectsParallelBehindAndDegenerateInputs() {
            Assert.False(RotationGizmoMath.TryIntersectPlane(float3.Zero, new float3(1, 0, 0), new float3(0, 0, -3), new float3(0, 0, 1), out _));
            Assert.False(RotationGizmoMath.TryIntersectPlane(float3.Zero, new float3(0, 0, 1), new float3(0, 0, -3), new float3(0, 0, 1), out _));
            Assert.False(RotationGizmoMath.TryIntersectPlane(float3.Zero, float3.Zero, float3.Zero, new float3(0, 1, 0), out _));
            Assert.False(RotationGizmoMath.TryIntersectPlane(float3.Zero, new float3(0, 0, -1), float3.Zero, float3.Zero, out _));
            Assert.True(RotationGizmoMath.TryIntersectPlane(new float3(1, 2, 3), new float3(0, 0, -1), float3.Zero, new float3(0, 0, 1), out float3 point));
            Assert.Equal(new float3(1, 2, 0), point);
        }

        /// <summary>Signed drag angles reverse with pointer direction and reject an absent rotation axis.</summary>
        [Fact]
        public void SignedAngle_PreservesDirection() {
            Assert.Equal(Math.PI / 2, (double)RotationGizmoMath.ComputeSignedAngle(new float3(1, 0, 0), new float3(0, 1, 0), new float3(0, 0, 1)), 6);
            Assert.Equal(-Math.PI / 2, (double)RotationGizmoMath.ComputeSignedAngle(new float3(0, 1, 0), new float3(1, 0, 0), new float3(0, 0, 1)), 6);
            Assert.Throws<ArgumentOutOfRangeException>(() => RotationGizmoMath.ComputeSignedAngle(float3.Zero, new float3(1, 0, 0), new float3(0, 0, 1)));
        }

        /// <summary>World-axis drag composition retains a nonidentity start orientation and normalizes the result.</summary>
        [Fact]
        public void Rotate_ConcatenatesWorldDeltaAndNormalizes() {
            float3 xAxis = new float3(1, 0, 0);
            float4.CreateFromAxisAngle(ref xAxis, Math.PI / 2, out float4 start);
            float4 result = RotationGizmoMath.Rotate(start * 2, new float3(0, 0, 3), Math.PI / 2);
            float3 rotated = float4.RotateVector(new float3(0, 1, 0), result);
            Assert.Equal(0, (double)rotated.X, 6);
            Assert.Equal(0, (double)rotated.Y, 6);
            Assert.Equal(1, (double)rotated.Z, 6);
            Assert.Equal(1, (double)(result.X * result.X + result.Y * result.Y + result.Z * result.Z + result.W * result.W), 6);
            Assert.Throws<ArgumentOutOfRangeException>(() => RotationGizmoMath.Rotate(float4.Zero, xAxis, 1));
        }

        /// <summary>Twist extraction uses the requested normalized axis rather than raw quaternion components.</summary>
        [Fact]
        public void AxisTwist_ReportsSignedRadians() {
            float4 orientation = RotationGizmoMath.Rotate(float4.Identity, new float3(0, 1, 0), -Math.PI / 3);
            Assert.Equal(-Math.PI / 3, (double)RotationGizmoMath.ResolveAxisTwistAngle(orientation, new float3(0, 4, 0)), 6);
        }

        /// <summary>Ring input validation rejects nonfinite dimensions before they enter geometry streams.</summary>
        [Fact]
        public void Geometry_RejectsNonfiniteDimensions() {
            Assert.Throws<ArgumentOutOfRangeException>(() => RotationGizmoGeometry.CreateTubeRing(float.NaN, 1, 0.1f, 48));
            Assert.Throws<ArgumentOutOfRangeException>(() => RotationGizmoGeometry.CreateTubeRing(0.9f, float.PositiveInfinity, 0.1f, 48));
            Assert.Throws<ArgumentOutOfRangeException>(() => RotationGizmoGeometry.GetAxisOrientation(3));
        }
    }
}
