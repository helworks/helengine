namespace helengine {
    /// <summary>
    /// Verifies stable allocation-free contact manifold generation for overlapping oriented boxes.
    /// </summary>
    public sealed class HelPhysicsBoxBoxCollision3DTests {
        /// <summary>
        /// Verifies that two aligned cubes overlapping by one tenth produce the four corners of their shared face patch.
        /// </summary>
        [Fact]
        public void TryBuildManifold_WithAlignedFaceOverlap_ReturnsFourSurfaceAnchoredContacts() {
            HelPhysicsBoxShape3D shape = new HelPhysicsBoxShape3D(new PhysicsVector3(1f, 1f, 1f));
            HelPhysicsBodyState3D bodyA = CreateBodyState(PhysicsVector3.Zero, PhysicsQuaternion.Identity);
            HelPhysicsBodyState3D bodyB = CreateBodyState(new PhysicsVector3(1.9f, 0f, 0f), PhysicsQuaternion.Identity);
            HelPhysicsBoxCollisionScratch3D scratch = new HelPhysicsBoxCollisionScratch3D();
            HelPhysicsContactManifold3D manifold = default;

            bool overlaps = HelPhysicsBoxBoxCollision3D.TryBuildManifold(
                in shape,
                in bodyA,
                in shape,
                in bodyB,
                scratch,
                ref manifold);

            Assert.True(overlaps);
            Assert.Equal(4, manifold.ContactCount);
            for (int contactIndex = 0; contactIndex < manifold.ContactCount; contactIndex++) {
                HelPhysicsContactPoint3D contact = manifold.GetContact(contactIndex);
                PhysicsVector3 anchorA = TransformLocalAnchor(contact.LocalAnchorA, in bodyA);
                PhysicsVector3 anchorB = TransformLocalAnchor(contact.LocalAnchorB, in bodyB);

                AssertVectorClose(new PhysicsVector3(1f, 0f, 0f), contact.Normal, 0.0001f);
                AssertScalarClose(0.1f, contact.PenetrationDepth, 0.0001f);
                AssertScalarClose(0.95f, contact.Position.X, 0.0001f);
                AssertScalarClose(1f, anchorA.X, 0.0001f);
                AssertScalarClose(0.9f, anchorB.X, 0.0001f);
                AssertScalarClose(anchorA.Y.ToFloat(), anchorB.Y, 0.0001f);
                AssertScalarClose(anchorA.Z.ToFloat(), anchorB.Z, 0.0001f);
                AssertScalarClose((anchorA.X.ToFloat() + anchorB.X.ToFloat()) * 0.5f, contact.Position.X, 0.0001f);
                Assert.Equal(PhysicsScalar.Zero, contact.AccumulatedNormalImpulse);
                Assert.Equal(PhysicsScalar.Zero, contact.AccumulatedTangentImpulse0);
                Assert.Equal(PhysicsScalar.Zero, contact.AccumulatedTangentImpulse1);
                Assert.Equal(0, contact.PreviousStepLifetime);

                for (int otherContactIndex = contactIndex + 1; otherContactIndex < manifold.ContactCount; otherContactIndex++) {
                    Assert.NotEqual(contact.Feature, manifold.GetContact(otherContactIndex).Feature);
                }
            }
        }

        /// <summary>
        /// Verifies that a tilted half-unit cube with one incident corner behind a broad reference face retains only that corner.
        /// </summary>
        [Fact]
        public void TryBuildManifold_WithSingleTiltedCornerPenetration_ReturnsOneContact() {
            HelPhysicsBoxShape3D shapeA = new HelPhysicsBoxShape3D(new PhysicsVector3(1f, 3f, 3f));
            HelPhysicsBoxShape3D shapeB = new HelPhysicsBoxShape3D(new PhysicsVector3(0.5f, 0.5f, 0.5f));
            PhysicsQuaternion rotationY = PhysicsQuaternion.CreateFromAxisAngle(
                PhysicsVector3.UnitY,
                PhysicsScalar.FromFloat((float)(Math.PI * 0.25d)));
            PhysicsQuaternion rotationZ = PhysicsQuaternion.CreateFromAxisAngle(
                PhysicsVector3.UnitZ,
                PhysicsScalar.FromFloat((float)(Math.PI * 0.25d)));
            HelPhysicsBodyState3D bodyA = CreateBodyState(PhysicsVector3.Zero, PhysicsQuaternion.Identity);
            HelPhysicsBodyState3D bodyB = CreateBodyState(new PhysicsVector3(1.8f, 0f, 0f), rotationZ * rotationY);
            HelPhysicsBoxCollisionScratch3D scratch = new HelPhysicsBoxCollisionScratch3D();
            HelPhysicsContactManifold3D manifold = default;

            bool overlaps = HelPhysicsBoxBoxCollision3D.TryBuildManifold(
                in shapeA,
                in bodyA,
                in shapeB,
                in bodyB,
                scratch,
                ref manifold);

            Assert.True(overlaps);
            Assert.Equal(1, manifold.ContactCount);

            HelPhysicsContactPoint3D contact = manifold.GetContact(0);
            PhysicsVector3 anchorA = TransformLocalAnchor(contact.LocalAnchorA, in bodyA);
            PhysicsVector3 anchorB = TransformLocalAnchor(contact.LocalAnchorB, in bodyB);
            AssertVectorClose(new PhysicsVector3(1f, 0f, 0f), contact.Normal, 0.0001f);
            AssertScalarClose(0.0535534f, contact.PenetrationDepth, 0.0002f);
            AssertVectorClose(new PhysicsVector3(1f, -0.1464466f, 0f), anchorA, 0.0002f);
            AssertVectorClose(new PhysicsVector3(0.9464466f, -0.1464466f, 0f), anchorB, 0.0002f);
            AssertVectorClose(new PhysicsVector3(0.9732233f, -0.1464466f, 0f), contact.Position, 0.0002f);
        }

        /// <summary>
        /// Verifies that the winning support edges of two skew slender boxes produce their scalar closest-point midpoint.
        /// </summary>
        [Fact]
        public void TryBuildManifold_WithSkewEdgeOverlap_ReturnsOneClosestEdgeContact() {
            HelPhysicsBoxShape3D shape = new HelPhysicsBoxShape3D(new PhysicsVector3(2f, 0.2f, 0.2f));
            PhysicsQuaternion rotationY = PhysicsQuaternion.CreateFromAxisAngle(
                PhysicsVector3.UnitY,
                PhysicsScalar.FromFloat((float)(Math.PI * 0.25d)));
            PhysicsQuaternion rotationZ = PhysicsQuaternion.CreateFromAxisAngle(
                PhysicsVector3.UnitZ,
                PhysicsScalar.FromFloat((float)(Math.PI * 0.25d)));
            HelPhysicsBodyState3D bodyA = CreateBodyState(PhysicsVector3.Zero, PhysicsQuaternion.Identity);
            HelPhysicsBodyState3D bodyB = CreateBodyState(
                new PhysicsVector3(0f, 0.36742346f, 0.25980762f),
                rotationZ * rotationY);
            HelPhysicsBoxCollisionScratch3D scratch = new HelPhysicsBoxCollisionScratch3D();
            HelPhysicsContactManifold3D manifold = default;

            bool overlaps = HelPhysicsBoxBoxCollision3D.TryBuildManifold(
                in shape,
                in bodyA,
                in shape,
                in bodyB,
                scratch,
                ref manifold);

            Assert.True(overlaps);
            Assert.Equal(1, manifold.ContactCount);

            HelPhysicsContactPoint3D contact = manifold.GetContact(0);
            PhysicsVector3 anchorA = TransformLocalAnchor(contact.LocalAnchorA, in bodyA);
            PhysicsVector3 anchorB = TransformLocalAnchor(contact.LocalAnchorB, in bodyB);
            AssertVectorClose(new PhysicsVector3(0f, 0.8164966f, 0.5773503f), contact.Normal, 0.0002f);
            AssertScalarClose(0.10754f, contact.PenetrationDepth, 0.0002f);
            AssertVectorClose(new PhysicsVector3(0.0276142f, 0.2f, 0.2f), anchorA, 0.0003f);
            AssertVectorClose(new PhysicsVector3(0.0276142f, 0.112195f, 0.137912f), anchorB, 0.0003f);
            AssertVectorClose(new PhysicsVector3(0.0276142f, 0.1560975f, 0.168956f), contact.Position, 0.0003f);
        }

        /// <summary>
        /// Verifies that a separating gap rejects a pair and clears contacts left by an earlier successful query.
        /// </summary>
        [Fact]
        public void TryBuildManifold_WithSeparatedBoxes_ReturnsFalseAndClearsManifold() {
            HelPhysicsBoxShape3D shape = new HelPhysicsBoxShape3D(new PhysicsVector3(1f, 1f, 1f));
            HelPhysicsBodyState3D bodyA = CreateBodyState(PhysicsVector3.Zero, PhysicsQuaternion.Identity);
            HelPhysicsBodyState3D overlappingBodyB = CreateBodyState(new PhysicsVector3(1.9f, 0f, 0f), PhysicsQuaternion.Identity);
            HelPhysicsBodyState3D separatedBodyB = CreateBodyState(new PhysicsVector3(2.1f, 0f, 0f), PhysicsQuaternion.Identity);
            HelPhysicsBoxCollisionScratch3D scratch = new HelPhysicsBoxCollisionScratch3D();
            HelPhysicsContactManifold3D manifold = default;

            bool firstOverlaps = HelPhysicsBoxBoxCollision3D.TryBuildManifold(
                in shape,
                in bodyA,
                in shape,
                in overlappingBodyB,
                scratch,
                ref manifold);
            bool secondOverlaps = HelPhysicsBoxBoxCollision3D.TryBuildManifold(
                in shape,
                in bodyA,
                in shape,
                in separatedBodyB,
                scratch,
                ref manifold);

            Assert.True(firstOverlaps);
            Assert.False(secondOverlaps);
            Assert.Equal(0, manifold.ContactCount);
        }

        /// <summary>
        /// Verifies that reversing query order reverses every normal and exchanges the surface anchors of the aligned patch.
        /// </summary>
        [Fact]
        public void TryBuildManifold_WithSwappedBodies_ReversesNormalAndPreservesSurfaceAnchors() {
            HelPhysicsBoxShape3D shape = new HelPhysicsBoxShape3D(new PhysicsVector3(1f, 1f, 1f));
            HelPhysicsBodyState3D bodyA = CreateBodyState(PhysicsVector3.Zero, PhysicsQuaternion.Identity);
            HelPhysicsBodyState3D bodyB = CreateBodyState(new PhysicsVector3(1.9f, 0f, 0f), PhysicsQuaternion.Identity);
            HelPhysicsBoxCollisionScratch3D scratch = new HelPhysicsBoxCollisionScratch3D();
            HelPhysicsContactManifold3D manifold = default;

            bool overlaps = HelPhysicsBoxBoxCollision3D.TryBuildManifold(
                in shape,
                in bodyB,
                in shape,
                in bodyA,
                scratch,
                ref manifold);

            Assert.True(overlaps);
            Assert.Equal(4, manifold.ContactCount);
            for (int contactIndex = 0; contactIndex < manifold.ContactCount; contactIndex++) {
                HelPhysicsContactPoint3D contact = manifold.GetContact(contactIndex);
                PhysicsVector3 anchorA = TransformLocalAnchor(contact.LocalAnchorA, in bodyB);
                PhysicsVector3 anchorB = TransformLocalAnchor(contact.LocalAnchorB, in bodyA);

                AssertVectorClose(new PhysicsVector3(-1f, 0f, 0f), contact.Normal, 0.0001f);
                AssertScalarClose(0.9f, anchorA.X, 0.0001f);
                AssertScalarClose(1f, anchorB.X, 0.0001f);
                AssertScalarClose(0.95f, contact.Position.X, 0.0001f);
            }
        }

        /// <summary>
        /// Verifies that repeated identical clipping queries preserve contact order, geometry, and provenance identifiers exactly.
        /// </summary>
        [Fact]
        public void TryBuildManifold_WithRepeatedIdenticalQuery_PreservesOrderAndFeatures() {
            HelPhysicsBoxShape3D shape = new HelPhysicsBoxShape3D(new PhysicsVector3(1f, 1f, 1f));
            PhysicsQuaternion orientationB = PhysicsQuaternion.CreateFromAxisAngle(
                PhysicsVector3.UnitX,
                PhysicsScalar.FromFloat((float)(Math.PI / 18d)));
            HelPhysicsBodyState3D bodyA = CreateBodyState(PhysicsVector3.Zero, PhysicsQuaternion.Identity);
            HelPhysicsBodyState3D bodyB = CreateBodyState(new PhysicsVector3(1.9f, 0.15f, -0.1f), orientationB);
            HelPhysicsBoxCollisionScratch3D scratch = new HelPhysicsBoxCollisionScratch3D();
            HelPhysicsContactManifold3D first = default;
            HelPhysicsContactManifold3D second = default;

            bool firstOverlaps = HelPhysicsBoxBoxCollision3D.TryBuildManifold(
                in shape,
                in bodyA,
                in shape,
                in bodyB,
                scratch,
                ref first);
            bool secondOverlaps = HelPhysicsBoxBoxCollision3D.TryBuildManifold(
                in shape,
                in bodyA,
                in shape,
                in bodyB,
                scratch,
                ref second);

            Assert.True(firstOverlaps);
            Assert.True(secondOverlaps);
            Assert.Equal(first.ContactCount, second.ContactCount);
            for (int contactIndex = 0; contactIndex < first.ContactCount; contactIndex++) {
                HelPhysicsContactPoint3D firstContact = first.GetContact(contactIndex);
                HelPhysicsContactPoint3D secondContact = second.GetContact(contactIndex);

                Assert.Equal(firstContact.Feature, secondContact.Feature);
                Assert.Equal(firstContact.PenetrationDepth, secondContact.PenetrationDepth);
                AssertVectorClose(firstContact.Position, secondContact.Position, 0f);
                AssertVectorClose(firstContact.LocalAnchorA, secondContact.LocalAnchorA, 0f);
                AssertVectorClose(firstContact.LocalAnchorB, secondContact.LocalAnchorB, 0f);
            }
        }

        /// <summary>
        /// Verifies that contact storage directly addresses all four inline slots and rejects indices outside that capacity.
        /// </summary>
        [Fact]
        public void ContactManifold_GetAndSetContact_UsesCheckedInlineSlots() {
            HelPhysicsContactManifold3D manifold = default;
            for (int contactIndex = 0; contactIndex < 4; contactIndex++) {
                HelPhysicsContactPoint3D contact = new HelPhysicsContactPoint3D(
                    new PhysicsVector3((float)contactIndex, 0f, 0f),
                    PhysicsVector3.UnitY,
                    PhysicsVector3.Zero,
                    PhysicsVector3.Zero,
                    PhysicsScalar.FromFloat(0.25f),
                    new HelPhysicsContactFeature3D((uint)(contactIndex + 1)));
                manifold.SetContact(contactIndex, in contact);
            }

            for (int contactIndex = 0; contactIndex < 4; contactIndex++) {
                Assert.Equal((uint)(contactIndex + 1), manifold.GetContact(contactIndex).Feature.Value);
            }

            bool getThrew = false;
            bool setThrew = false;
            HelPhysicsContactPoint3D replacement = default;
            try {
                manifold.GetContact(-1);
            } catch (ArgumentOutOfRangeException) {
                getThrew = true;
            }
            try {
                manifold.SetContact(4, in replacement);
            } catch (ArgumentOutOfRangeException) {
                setThrew = true;
            }

            Assert.True(getThrew);
            Assert.True(setThrew);
        }

        /// <summary>
        /// Verifies that feature identifiers provide value equality suitable for exact persistent-contact matching.
        /// </summary>
        [Fact]
        public void ContactFeature_ValueEquality_ComparesPackedIdentifiers() {
            HelPhysicsContactFeature3D first = new HelPhysicsContactFeature3D(42u);
            HelPhysicsContactFeature3D equal = new HelPhysicsContactFeature3D(42u);
            HelPhysicsContactFeature3D different = new HelPhysicsContactFeature3D(43u);

            Assert.True(first.Equals(equal));
            Assert.True(first.Equals((object)equal));
            Assert.Equal(first.GetHashCode(), equal.GetHashCode());
            Assert.True(first == equal);
            Assert.False(first != equal);
            Assert.False(first == different);
            Assert.True(first != different);
        }

        /// <summary>
        /// Verifies that warmed manifold queries reuse constructor-owned clipping buffers without managed allocations.
        /// </summary>
        [Fact]
        public void TryBuildManifold_AfterWarmup_AllocatesNoManagedMemory() {
            HelPhysicsBoxShape3D shape = new HelPhysicsBoxShape3D(new PhysicsVector3(1f, 1f, 1f));
            HelPhysicsBodyState3D bodyA = CreateBodyState(PhysicsVector3.Zero, PhysicsQuaternion.Identity);
            HelPhysicsBodyState3D bodyB = CreateBodyState(new PhysicsVector3(1.9f, 0f, 0f), PhysicsQuaternion.Identity);
            HelPhysicsBoxCollisionScratch3D scratch = new HelPhysicsBoxCollisionScratch3D();
            HelPhysicsContactManifold3D manifold = default;

            HelPhysicsBoxBoxCollision3D.TryBuildManifold(in shape, in bodyA, in shape, in bodyB, scratch, ref manifold);
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int queryIndex = 0; queryIndex < 1024; queryIndex++) {
                HelPhysicsBoxBoxCollision3D.TryBuildManifold(in shape, in bodyA, in shape, in bodyB, scratch, ref manifold);
            }
            long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

            Assert.Equal(allocatedBefore, allocatedAfter);
        }

        /// <summary>
        /// Creates the body pose required by a collision query while leaving unrelated solver state at inert defaults.
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

        /// <summary>
        /// Transforms one stored body-local contact anchor back into world space for surface validation.
        /// </summary>
        /// <param name="localAnchor">Anchor expressed relative to the body's center and orientation.</param>
        /// <param name="body">Body pose that owns the anchor.</param>
        /// <returns>The anchor transformed into world coordinates.</returns>
        static PhysicsVector3 TransformLocalAnchor(PhysicsVector3 localAnchor, in HelPhysicsBodyState3D body) {
            return body.Position + body.Orientation.Rotate(localAnchor);
        }

        /// <summary>
        /// Asserts that one physics scalar lies within an explicit absolute tolerance of a hand-derived float value.
        /// </summary>
        /// <param name="expected">Hand-derived expected float value.</param>
        /// <param name="actual">Physics scalar produced by the collision query.</param>
        /// <param name="tolerance">Maximum accepted absolute difference.</param>
        static void AssertScalarClose(float expected, PhysicsScalar actual, float tolerance) {
            Assert.InRange(actual.ToFloat(), expected - tolerance, expected + tolerance);
        }

        /// <summary>
        /// Asserts each component of a physics vector against fixed expected geometry within one absolute tolerance.
        /// </summary>
        /// <param name="expected">Hand-derived or previously captured deterministic expected vector.</param>
        /// <param name="actual">Physics vector produced by the collision query.</param>
        /// <param name="tolerance">Maximum accepted absolute component difference.</param>
        static void AssertVectorClose(PhysicsVector3 expected, PhysicsVector3 actual, float tolerance) {
            AssertScalarClose(expected.X.ToFloat(), actual.X, tolerance);
            AssertScalarClose(expected.Y.ToFloat(), actual.Y, tolerance);
            AssertScalarClose(expected.Z.ToFloat(), actual.Z, tolerance);
        }
    }
}
