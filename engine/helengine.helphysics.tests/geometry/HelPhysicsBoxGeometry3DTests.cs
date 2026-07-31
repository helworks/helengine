namespace helengine {
    /// <summary>
    /// Verifies box transform helpers, conservative world bounds, and local inertia calculations.
    /// </summary>
    public sealed class HelPhysicsBoxGeometry3DTests {
        /// <summary>
        /// Verifies that rotating a box around Z maps its local X and Y half extents onto the opposite world axes.
        /// </summary>
        [Fact]
        public void ComputeWorldAabb_WithNinetyDegreeZRotation_SwapsXYExtents() {
            HelPhysicsBoxShape3D box = new HelPhysicsBoxShape3D(new PhysicsVector3(1f, 2f, 3f));
            PhysicsQuaternion orientation = PhysicsQuaternion.CreateFromAxisAngle(
                PhysicsVector3.UnitZ,
                PhysicsScalar.FromFloat((float)(Math.PI * 0.5d)));

            HelPhysicsAabb3D aabb = HelPhysicsBoxGeometry3D.ComputeWorldAabb(
                box,
                PhysicsVector3.Zero,
                orientation,
                PhysicsScalar.Zero);

            Assert.InRange(aabb.Maximum.X.ToFloat(), 1.9999f, 2.0001f);
            Assert.InRange(aabb.Maximum.Y.ToFloat(), 0.9999f, 1.0001f);
            Assert.InRange(aabb.Maximum.Z.ToFloat(), 2.9999f, 3.0001f);
        }

        /// <summary>
        /// Verifies that the reciprocal inertia of a unit-mass box with unit full dimensions is six on every diagonal.
        /// </summary>
        [Fact]
        public void ComputeLocalInverseInertia_WithUnitDynamicCube_ReturnsDiagonalSix() {
            HelPhysicsBoxShape3D box = new HelPhysicsBoxShape3D(new PhysicsVector3(0.5f, 0.5f, 0.5f));

            PhysicsMatrix3x3 inverseInertia = HelPhysicsBoxGeometry3D.ComputeLocalInverseInertia(
                box,
                BodyKind3D.Dynamic,
                PhysicsScalar.One);

            Assert.Equal(6f, inverseInertia.Row0.X.ToFloat());
            Assert.Equal(6f, inverseInertia.Row1.Y.ToFloat());
            Assert.Equal(6f, inverseInertia.Row2.Z.ToFloat());
            Assert.Equal(PhysicsScalar.Zero, inverseInertia.Row0.Y);
            Assert.Equal(PhysicsScalar.Zero, inverseInertia.Row0.Z);
            Assert.Equal(PhysicsScalar.Zero, inverseInertia.Row1.X);
            Assert.Equal(PhysicsScalar.Zero, inverseInertia.Row1.Z);
            Assert.Equal(PhysicsScalar.Zero, inverseInertia.Row2.X);
            Assert.Equal(PhysicsScalar.Zero, inverseInertia.Row2.Y);
        }

        /// <summary>
        /// Verifies that static and kinematic bodies receive no rotational response regardless of the supplied box dimensions.
        /// </summary>
        [Fact]
        public void ComputeLocalInverseInertia_WithNonDynamicBody_ReturnsZeroMatrix() {
            HelPhysicsBoxShape3D box = new HelPhysicsBoxShape3D(new PhysicsVector3(1f, 2f, 3f));

            PhysicsMatrix3x3 staticInverseInertia = HelPhysicsBoxGeometry3D.ComputeLocalInverseInertia(
                box,
                BodyKind3D.Static,
                PhysicsScalar.One);
            PhysicsMatrix3x3 kinematicInverseInertia = HelPhysicsBoxGeometry3D.ComputeLocalInverseInertia(
                box,
                BodyKind3D.Kinematic,
                PhysicsScalar.One);

            Assert.Equal(PhysicsScalar.Zero, staticInverseInertia.Row0.X);
            Assert.Equal(PhysicsScalar.Zero, staticInverseInertia.Row1.Y);
            Assert.Equal(PhysicsScalar.Zero, staticInverseInertia.Row2.Z);
            Assert.Equal(PhysicsScalar.Zero, kinematicInverseInertia.Row0.X);
            Assert.Equal(PhysicsScalar.Zero, kinematicInverseInertia.Row1.Y);
            Assert.Equal(PhysicsScalar.Zero, kinematicInverseInertia.Row2.Z);
        }

        /// <summary>
        /// Verifies that boxes reject zero and negative half extents instead of producing degenerate geometry.
        /// </summary>
        [Fact]
        public void Constructor_WithNonPositiveHalfExtent_ThrowsArgumentOutOfRangeException() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelPhysicsBoxShape3D(new PhysicsVector3(0f, 1f, 1f)));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelPhysicsBoxShape3D(new PhysicsVector3(1f, -1f, 1f)));
        }

        /// <summary>
        /// Verifies that world axes and vertices apply the supplied orientation and translation with the expected signed local corner.
        /// </summary>
        [Fact]
        public void GetWorldAxisAndVertex_WithNinetyDegreeZRotation_TransformsExpectedDirections() {
            HelPhysicsBoxShape3D box = new HelPhysicsBoxShape3D(new PhysicsVector3(1f, 2f, 3f));
            PhysicsQuaternion orientation = PhysicsQuaternion.CreateFromAxisAngle(
                PhysicsVector3.UnitZ,
                PhysicsScalar.FromFloat((float)(Math.PI * 0.5d)));
            PhysicsVector3 position = new PhysicsVector3(10f, 20f, 30f);

            PhysicsVector3 axis = HelPhysicsBoxGeometry3D.GetWorldAxis(orientation, 0);
            PhysicsVector3 vertex = HelPhysicsBoxGeometry3D.GetWorldVertex(box, position, orientation, 7);

            Assert.InRange(axis.X.ToFloat(), -0.0001f, 0.0001f);
            Assert.InRange(axis.Y.ToFloat(), 0.9999f, 1.0001f);
            Assert.InRange(axis.Z.ToFloat(), -0.0001f, 0.0001f);
            Assert.InRange(vertex.X.ToFloat(), 7.9999f, 8.0001f);
            Assert.InRange(vertex.Y.ToFloat(), 20.9999f, 21.0001f);
            Assert.InRange(vertex.Z.ToFloat(), 32.9999f, 33.0001f);
        }

        /// <summary>
        /// Verifies that AABBs include face contact as overlap and reject disjoint bounds.
        /// </summary>
        [Fact]
        public void Overlaps_WithTouchingAndDisjointBounds_ReturnsInclusiveExpectedResult() {
            HelPhysicsAabb3D first = new HelPhysicsAabb3D(new PhysicsVector3(-1f, -1f, -1f), new PhysicsVector3(1f, 1f, 1f));
            HelPhysicsAabb3D touching = new HelPhysicsAabb3D(new PhysicsVector3(1f, -1f, -1f), new PhysicsVector3(2f, 1f, 1f));
            HelPhysicsAabb3D disjoint = new HelPhysicsAabb3D(new PhysicsVector3(1.01f, -1f, -1f), new PhysicsVector3(2f, 1f, 1f));

            Assert.True(first.Overlaps(touching));
            Assert.False(first.Overlaps(disjoint));
        }
    }
}
