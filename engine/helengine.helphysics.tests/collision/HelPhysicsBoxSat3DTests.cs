namespace helengine {
    /// <summary>
    /// Verifies allocation-free oriented-box separating-axis queries and their deterministic minimum-axis metadata.
    /// </summary>
    public sealed class HelPhysicsBoxSat3DTests {
        /// <summary>
        /// Verifies that a positive gap on one face axis rejects otherwise aligned boxes.
        /// </summary>
        [Fact]
        public void TryFindMinimumPenetration_WithSeparatedBoxes_ReturnsFalse() {
            HelPhysicsBoxShape3D shape = new HelPhysicsBoxShape3D(new PhysicsVector3(1f, 1f, 1f));
            HelPhysicsBodyState3D bodyA = CreateBodyState(PhysicsVector3.Zero, PhysicsQuaternion.Identity);
            HelPhysicsBodyState3D bodyB = CreateBodyState(new PhysicsVector3(2.1f, 0f, 0f), PhysicsQuaternion.Identity);

            bool overlaps = HelPhysicsBoxSat3D.TryFindMinimumPenetration(
                in shape,
                in bodyA,
                in shape,
                in bodyB,
                out HelPhysicsBoxSatResult3D result);

            Assert.False(overlaps);
        }

        /// <summary>
        /// Verifies a half-unit aligned overlap, its A-to-B normal, and the first A-face tie winner.
        /// </summary>
        [Fact]
        public void TryFindMinimumPenetration_WithHalfUnitOverlap_ReturnsPositiveXFaceAndDepth() {
            HelPhysicsBoxShape3D shape = new HelPhysicsBoxShape3D(new PhysicsVector3(1f, 1f, 1f));
            HelPhysicsBodyState3D bodyA = CreateBodyState(PhysicsVector3.Zero, PhysicsQuaternion.Identity);
            HelPhysicsBodyState3D bodyB = CreateBodyState(new PhysicsVector3(1.5f, 0f, 0f), PhysicsQuaternion.Identity);

            bool overlaps = HelPhysicsBoxSat3D.TryFindMinimumPenetration(
                in shape,
                in bodyA,
                in shape,
                in bodyB,
                out HelPhysicsBoxSatResult3D result);

            Assert.True(overlaps);
            Assert.Equal(1f, result.Normal.X.ToFloat());
            Assert.Equal(0f, result.Normal.Y.ToFloat());
            Assert.Equal(0f, result.Normal.Z.ToFloat());
            Assert.Equal(0.5f, result.PenetrationDepth.ToFloat());
            Assert.Equal(HelPhysicsBoxSatAxisKind3D.FaceA, result.AxisKind);
            Assert.Equal(0, result.AxisAIndex);
            Assert.Equal(-1, result.AxisBIndex);
        }

        /// <summary>
        /// Verifies that equally rotated boxes use their shared local face direction and preserve the analytical overlap depth.
        /// </summary>
        [Fact]
        public void TryFindMinimumPenetration_WithRotatedFaceToFaceOverlap_ReturnsRotatedNormal() {
            HelPhysicsBoxShape3D shape = new HelPhysicsBoxShape3D(new PhysicsVector3(1f, 2f, 3f));
            PhysicsQuaternion orientation = PhysicsQuaternion.CreateFromAxisAngle(
                PhysicsVector3.UnitZ,
                PhysicsScalar.FromFloat((float)(Math.PI * 0.25d)));
            HelPhysicsBodyState3D bodyA = CreateBodyState(PhysicsVector3.Zero, orientation);
            HelPhysicsBodyState3D bodyB = CreateBodyState(new PhysicsVector3(1.0606601f, 1.0606601f, 0f), orientation);

            bool overlaps = HelPhysicsBoxSat3D.TryFindMinimumPenetration(
                in shape,
                in bodyA,
                in shape,
                in bodyB,
                out HelPhysicsBoxSatResult3D result);

            Assert.True(overlaps);
            Assert.InRange(result.Normal.X.ToFloat(), 0.7070f, 0.7072f);
            Assert.InRange(result.Normal.Y.ToFloat(), 0.7070f, 0.7072f);
            Assert.InRange(result.Normal.Z.ToFloat(), -0.0001f, 0.0001f);
            Assert.InRange(result.PenetrationDepth.ToFloat(), 0.4999f, 0.5001f);
            Assert.Equal(HelPhysicsBoxSatAxisKind3D.FaceA, result.AxisKind);
            Assert.Equal(0, result.AxisAIndex);
            Assert.Equal(-1, result.AxisBIndex);
        }

        /// <summary>
        /// Verifies that a rotated B face can uniquely provide the minimum penetration axis and its B-axis index.
        /// </summary>
        [Fact]
        public void TryFindMinimumPenetration_WithRotatedBFaceMinimum_ReturnsBFaceMetadata() {
            HelPhysicsBoxShape3D shape = new HelPhysicsBoxShape3D(new PhysicsVector3(1f, 1f, 1f));
            PhysicsQuaternion orientationB = PhysicsQuaternion.CreateFromAxisAngle(
                PhysicsVector3.UnitZ,
                PhysicsScalar.FromFloat((float)(Math.PI * 0.25d)));
            HelPhysicsBodyState3D bodyA = CreateBodyState(PhysicsVector3.Zero, PhysicsQuaternion.Identity);
            HelPhysicsBodyState3D bodyB = CreateBodyState(new PhysicsVector3(1.4142135f, 1.4142135f, 0f), orientationB);

            bool overlaps = HelPhysicsBoxSat3D.TryFindMinimumPenetration(
                in shape,
                in bodyA,
                in shape,
                in bodyB,
                out HelPhysicsBoxSatResult3D result);

            Assert.True(overlaps);
            Assert.InRange(result.Normal.X.ToFloat(), 0.7070f, 0.7072f);
            Assert.InRange(result.Normal.Y.ToFloat(), 0.7070f, 0.7072f);
            Assert.InRange(result.Normal.Z.ToFloat(), -0.0001f, 0.0001f);
            Assert.InRange(result.PenetrationDepth.ToFloat(), 0.4141f, 0.4143f);
            Assert.Equal(HelPhysicsBoxSatAxisKind3D.FaceB, result.AxisKind);
            Assert.Equal(-1, result.AxisAIndex);
            Assert.Equal(0, result.AxisBIndex);
        }

        /// <summary>
        /// Verifies that two skew slender boxes can select the normalized cross product of their first edge directions.
        /// </summary>
        [Fact]
        public void TryFindMinimumPenetration_WithEdgeToEdgeOverlap_ReturnsEdgePairMetadata() {
            HelPhysicsBoxShape3D shape = new HelPhysicsBoxShape3D(new PhysicsVector3(2f, 0.2f, 0.2f));
            PhysicsQuaternion rotationY = PhysicsQuaternion.CreateFromAxisAngle(
                PhysicsVector3.UnitY,
                PhysicsScalar.FromFloat((float)(Math.PI * 0.25d)));
            PhysicsQuaternion rotationZ = PhysicsQuaternion.CreateFromAxisAngle(
                PhysicsVector3.UnitZ,
                PhysicsScalar.FromFloat((float)(Math.PI * 0.25d)));
            PhysicsQuaternion orientationB = rotationZ * rotationY;
            HelPhysicsBodyState3D bodyA = CreateBodyState(PhysicsVector3.Zero, PhysicsQuaternion.Identity);
            HelPhysicsBodyState3D bodyB = CreateBodyState(new PhysicsVector3(0f, 0.36742346f, 0.25980762f), orientationB);

            bool overlaps = HelPhysicsBoxSat3D.TryFindMinimumPenetration(
                in shape,
                in bodyA,
                in shape,
                in bodyB,
                out HelPhysicsBoxSatResult3D result);

            Assert.True(overlaps);
            Assert.InRange(result.Normal.X.ToFloat(), -0.0001f, 0.0001f);
            Assert.InRange(result.Normal.Y.ToFloat(), 0.8163f, 0.8167f);
            Assert.InRange(result.Normal.Z.ToFloat(), 0.5772f, 0.5775f);
            Assert.InRange(result.PenetrationDepth.ToFloat(), 0.1074f, 0.1077f);
            Assert.Equal(HelPhysicsBoxSatAxisKind3D.EdgePair, result.AxisKind);
            Assert.Equal(0, result.AxisAIndex);
            Assert.Equal(0, result.AxisBIndex);
        }

        /// <summary>
        /// Verifies that near-parallel edge pairs below the cross-axis threshold are skipped without losing face contact.
        /// </summary>
        [Fact]
        public void TryFindMinimumPenetration_WithNearlyParallelAxes_ReturnsStableFaceResult() {
            HelPhysicsBoxShape3D shape = new HelPhysicsBoxShape3D(new PhysicsVector3(1f, 1f, 1f));
            PhysicsQuaternion orientationB = PhysicsQuaternion.CreateFromAxisAngle(
                PhysicsVector3.UnitZ,
                PhysicsScalar.FromFloat(0.00001f));
            HelPhysicsBodyState3D bodyA = CreateBodyState(PhysicsVector3.Zero, PhysicsQuaternion.Identity);
            HelPhysicsBodyState3D bodyB = CreateBodyState(new PhysicsVector3(1.5f, 0f, 0f), orientationB);

            bool overlaps = HelPhysicsBoxSat3D.TryFindMinimumPenetration(
                in shape,
                in bodyA,
                in shape,
                in bodyB,
                out HelPhysicsBoxSatResult3D result);

            Assert.True(overlaps);
            Assert.InRange(result.Normal.X.ToFloat(), 0.9999f, 1.0001f);
            Assert.InRange(result.Normal.Y.ToFloat(), -0.0001f, 0.0001f);
            Assert.InRange(result.Normal.Z.ToFloat(), -0.0001f, 0.0001f);
            Assert.InRange(result.PenetrationDepth.ToFloat(), 0.5f, 0.5001f);
            Assert.Equal(HelPhysicsBoxSatAxisKind3D.FaceA, result.AxisKind);
            Assert.Equal(0, result.AxisAIndex);
            Assert.Equal(-1, result.AxisBIndex);
        }

        /// <summary>
        /// Verifies that zero penetration at exact face touching remains an inclusive contact.
        /// </summary>
        [Fact]
        public void TryFindMinimumPenetration_WithExactTouching_ReturnsZeroDepthContact() {
            HelPhysicsBoxShape3D shape = new HelPhysicsBoxShape3D(new PhysicsVector3(1f, 1f, 1f));
            HelPhysicsBodyState3D bodyA = CreateBodyState(PhysicsVector3.Zero, PhysicsQuaternion.Identity);
            HelPhysicsBodyState3D bodyB = CreateBodyState(new PhysicsVector3(2f, 0f, 0f), PhysicsQuaternion.Identity);

            bool overlaps = HelPhysicsBoxSat3D.TryFindMinimumPenetration(
                in shape,
                in bodyA,
                in shape,
                in bodyB,
                out HelPhysicsBoxSatResult3D result);

            Assert.True(overlaps);
            Assert.Equal(PhysicsScalar.Zero, result.PenetrationDepth);
            Assert.Equal(1f, result.Normal.X.ToFloat());
            Assert.Equal(HelPhysicsBoxSatAxisKind3D.FaceA, result.AxisKind);
            Assert.Equal(0, result.AxisAIndex);
            Assert.Equal(-1, result.AxisBIndex);
        }

        /// <summary>
        /// Verifies that swapping query order reverses the normal while retaining the same penetration magnitude.
        /// </summary>
        [Fact]
        public void TryFindMinimumPenetration_WithSwappedBodyOrder_ReturnsOppositeNormal() {
            HelPhysicsBoxShape3D shape = new HelPhysicsBoxShape3D(new PhysicsVector3(1f, 1f, 1f));
            HelPhysicsBodyState3D bodyA = CreateBodyState(PhysicsVector3.Zero, PhysicsQuaternion.Identity);
            HelPhysicsBodyState3D bodyB = CreateBodyState(new PhysicsVector3(1.5f, 0f, 0f), PhysicsQuaternion.Identity);

            bool overlaps = HelPhysicsBoxSat3D.TryFindMinimumPenetration(
                in shape,
                in bodyB,
                in shape,
                in bodyA,
                out HelPhysicsBoxSatResult3D result);

            Assert.True(overlaps);
            Assert.Equal(-1f, result.Normal.X.ToFloat());
            Assert.Equal(0f, result.Normal.Y.ToFloat());
            Assert.Equal(0f, result.Normal.Z.ToFloat());
            Assert.Equal(0.5f, result.PenetrationDepth.ToFloat());
            Assert.Equal(HelPhysicsBoxSatAxisKind3D.FaceA, result.AxisKind);
            Assert.Equal(0, result.AxisAIndex);
            Assert.Equal(-1, result.AxisBIndex);
        }

        /// <summary>
        /// Verifies that complete penetration ties preserve A-face axis zero ahead of every later SAT candidate.
        /// </summary>
        [Fact]
        public void TryFindMinimumPenetration_WithCoincidentCubes_PreservesFirstAxisTieOrder() {
            HelPhysicsBoxShape3D shape = new HelPhysicsBoxShape3D(new PhysicsVector3(1f, 1f, 1f));
            HelPhysicsBodyState3D bodyA = CreateBodyState(PhysicsVector3.Zero, PhysicsQuaternion.Identity);
            HelPhysicsBodyState3D bodyB = CreateBodyState(PhysicsVector3.Zero, PhysicsQuaternion.Identity);

            bool overlaps = HelPhysicsBoxSat3D.TryFindMinimumPenetration(
                in shape,
                in bodyA,
                in shape,
                in bodyB,
                out HelPhysicsBoxSatResult3D result);

            Assert.True(overlaps);
            Assert.Equal(2f, result.PenetrationDepth.ToFloat());
            Assert.Equal(1f, result.Normal.X.ToFloat());
            Assert.Equal(0f, result.Normal.Y.ToFloat());
            Assert.Equal(0f, result.Normal.Z.ToFloat());
            Assert.Equal(HelPhysicsBoxSatAxisKind3D.FaceA, result.AxisKind);
            Assert.Equal(0, result.AxisAIndex);
            Assert.Equal(-1, result.AxisBIndex);
        }

        /// <summary>
        /// Creates the body pose required by a SAT query while leaving unrelated solver state at its inert defaults.
        /// </summary>
        /// <param name="position">World-space center of the test box.</param>
        /// <param name="orientation">World-space orientation of the test box.</param>
        /// <returns>A body state containing the supplied collision pose.</returns>
        static HelPhysicsBodyState3D CreateBodyState(PhysicsVector3 position, PhysicsQuaternion orientation) {
            return new HelPhysicsBodyState3D {
                Position = position,
                Orientation = orientation
            };
        }
    }
}
