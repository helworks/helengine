namespace helengine {
    /// <summary>
    /// Verifies deterministic fixed-capacity dynamic-body island construction and transactional publication.
    /// </summary>
    public sealed class HelPhysicsIslandBuilder3DTests {
        /// <summary>
        /// Verifies that independent dynamic bodies touching one shared static floor remain separate ordered islands.
        /// </summary>
        [Fact]
        public void Build_WithIndependentBodiesOnSharedStaticFloor_KeepsSeparateOrderedIslands() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(3);
            bodies.Allocate(CreateBodyState(false), CreateColdState(BodyKind3D.Static));
            bodies.Allocate(CreateBodyState(true), CreateColdState(BodyKind3D.Dynamic));
            bodies.Allocate(CreateBodyState(true), CreateColdState(BodyKind3D.Dynamic));
            HelPhysicsPairKey3D[] pairs = new HelPhysicsPairKey3D[] {
                new HelPhysicsPairKey3D(0, 2),
                new HelPhysicsPairKey3D(0, 1)
            };
            HelPhysicsContactManifold3D[] manifolds = CreateActiveManifolds(2);
            HelPhysicsIslandBuilder3D builder = new HelPhysicsIslandBuilder3D(3, 3);

            builder.Build(bodies, pairs, manifolds, 2);

            Assert.Equal(2, builder.IslandCount);
            AssertIsland(builder, 0, 1);
            AssertIsland(builder, 1, 2);
            Assert.Equal(-1, builder.GetIslandIndexForBody(0));
            Assert.Equal(0, builder.GetIslandIndexForBody(1));
            Assert.Equal(1, builder.GetIslandIndexForBody(2));
        }

        /// <summary>
        /// Verifies that active dynamic contacts join transitively while members and islands use ascending body-index order.
        /// </summary>
        [Fact]
        public void Build_WithOutOfOrderDynamicContacts_JoinsAndSortsMembersDeterministically() {
            HelPhysicsBodyPool3D bodies = CreateDynamicBodies(6);
            HelPhysicsPairKey3D[] pairs = new HelPhysicsPairKey3D[] {
                new HelPhysicsPairKey3D(4, 5),
                new HelPhysicsPairKey3D(1, 3),
                new HelPhysicsPairKey3D(2, 3)
            };
            HelPhysicsContactManifold3D[] manifolds = CreateActiveManifolds(3);
            HelPhysicsIslandBuilder3D builder = new HelPhysicsIslandBuilder3D(6, 6);

            builder.Build(bodies, pairs, manifolds, 3);

            Assert.Equal(3, builder.IslandCount);
            AssertIsland(builder, 0, 0);
            AssertIsland(builder, 1, 1, 2, 3);
            AssertIsland(builder, 2, 4, 5);
        }

        /// <summary>
        /// Verifies that one kinematic body constrains multiple dynamics without connecting their dynamic islands.
        /// </summary>
        [Fact]
        public void Build_WithDynamicsTouchingSharedKinematicBody_DoesNotBridgeTheirIslands() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(3);
            bodies.Allocate(CreateBodyState(true), CreateColdState(BodyKind3D.Dynamic));
            bodies.Allocate(CreateBodyState(false), CreateColdState(BodyKind3D.Kinematic));
            bodies.Allocate(CreateBodyState(true), CreateColdState(BodyKind3D.Dynamic));
            HelPhysicsPairKey3D[] pairs = new HelPhysicsPairKey3D[] {
                new HelPhysicsPairKey3D(0, 1),
                new HelPhysicsPairKey3D(1, 2)
            };
            HelPhysicsIslandBuilder3D builder = new HelPhysicsIslandBuilder3D(3, 3);

            builder.Build(bodies, pairs, CreateActiveManifolds(2), 2);

            Assert.Equal(2, builder.IslandCount);
            AssertIsland(builder, 0, 0);
            AssertIsland(builder, 1, 2);
            Assert.Equal(-1, builder.GetIslandIndexForBody(1));
        }

        /// <summary>
        /// Verifies that every occupied isolated dynamic body receives an island while empty, static, and kinematic slots do not.
        /// </summary>
        [Fact]
        public void Build_WithIsolatedAndUnoccupiedSlots_PublishesOccupiedDynamicsOnly() {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(5);
            bodies.Allocate(CreateBodyState(false), CreateColdState(BodyKind3D.Static));
            bodies.Allocate(CreateBodyState(true), CreateColdState(BodyKind3D.Dynamic));
            HelPhysicsBodyHandle3D released = bodies.Allocate(
                CreateBodyState(true),
                CreateColdState(BodyKind3D.Dynamic));
            bodies.Allocate(CreateBodyState(false), CreateColdState(BodyKind3D.Kinematic));
            bodies.Release(released);
            HelPhysicsIslandBuilder3D builder = new HelPhysicsIslandBuilder3D(5, 5);

            builder.Build(
                bodies,
                Array.Empty<HelPhysicsPairKey3D>(),
                Array.Empty<HelPhysicsContactManifold3D>(),
                0);

            Assert.Equal(1, builder.IslandCount);
            AssertIsland(builder, 0, 1);
            Assert.Equal(-1, builder.GetIslandIndexForBody(0));
            Assert.Equal(-1, builder.GetIslandIndexForBody(2));
            Assert.Equal(-1, builder.GetIslandIndexForBody(3));
            Assert.Equal(-1, builder.GetIslandIndexForBody(4));
        }

        /// <summary>
        /// Verifies that invalid constructor capacities cannot create unusable or over-provisioned fixed island storage.
        /// </summary>
        [Fact]
        public void Constructor_WithInvalidCapacity_ThrowsArgumentOutOfRangeException() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelPhysicsIslandBuilder3D(0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelPhysicsIslandBuilder3D(1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelPhysicsIslandBuilder3D(1, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelPhysicsIslandBuilder3D(65535, 1));
        }

        /// <summary>
        /// Verifies that a build requires a body pool matching the constructor-owned body-index storage.
        /// </summary>
        [Fact]
        public void Build_WithMismatchedBodyCapacity_ThrowsArgumentException() {
            HelPhysicsIslandBuilder3D builder = new HelPhysicsIslandBuilder3D(2, 2);
            HelPhysicsBodyPool3D bodies = CreateDynamicBodies(1);

            Assert.Throws<ArgumentException>(() => builder.Build(
                bodies,
                Array.Empty<HelPhysicsPairKey3D>(),
                Array.Empty<HelPhysicsContactManifold3D>(),
                0));
        }

        /// <summary>
        /// Verifies that active pairs and manifolds must occupy exactly parallel arrays with a valid leading count.
        /// </summary>
        [Fact]
        public void Build_WithInvalidParallelInputs_ThrowsBeforePublication() {
            HelPhysicsBodyPool3D bodies = CreateDynamicBodies(2);
            HelPhysicsIslandBuilder3D builder = new HelPhysicsIslandBuilder3D(2, 2);
            HelPhysicsPairKey3D[] pairs = new HelPhysicsPairKey3D[] {
                new HelPhysicsPairKey3D(0, 1)
            };

            Assert.Throws<ArgumentException>(() => builder.Build(
                bodies,
                pairs,
                new HelPhysicsContactManifold3D[2],
                1));
            Assert.Throws<ArgumentOutOfRangeException>(() => builder.Build(
                bodies,
                pairs,
                new HelPhysicsContactManifold3D[1],
                2));
        }

        /// <summary>
        /// Verifies that default, duplicate, out-of-range, and unoccupied active pair entries are rejected.
        /// </summary>
        [Fact]
        public void Build_WithInvalidActivePair_ThrowsBeforePublication() {
            HelPhysicsBodyPool3D bodies = CreateDynamicBodies(2);
            HelPhysicsIslandBuilder3D builder = new HelPhysicsIslandBuilder3D(2, 2);
            HelPhysicsContactManifold3D activeManifold = CreateActiveManifold();
            HelPhysicsPairKey3D pair = new HelPhysicsPairKey3D(0, 1);

            Assert.Throws<ArgumentOutOfRangeException>(() => builder.Build(
                bodies,
                new HelPhysicsPairKey3D[] { default },
                new HelPhysicsContactManifold3D[] { activeManifold },
                1));
            Assert.Throws<InvalidOperationException>(() => builder.Build(
                bodies,
                new HelPhysicsPairKey3D[] { pair, pair },
                new HelPhysicsContactManifold3D[] { activeManifold, activeManifold },
                2));
            Assert.Throws<ArgumentOutOfRangeException>(() => builder.Build(
                bodies,
                new HelPhysicsPairKey3D[] { new HelPhysicsPairKey3D(0, 2) },
                new HelPhysicsContactManifold3D[] { activeManifold },
                1));

            HelPhysicsBodyPool3D sparseBodies = new HelPhysicsBodyPool3D(2);
            sparseBodies.Allocate(CreateBodyState(true), CreateColdState(BodyKind3D.Dynamic));
            Assert.Throws<InvalidOperationException>(() => builder.Build(
                sparseBodies,
                new HelPhysicsPairKey3D[] { pair },
                new HelPhysicsContactManifold3D[] { activeManifold },
                1));
        }

        /// <summary>
        /// Verifies that every leading manifold supplied as active contains one through four contacts.
        /// </summary>
        [Fact]
        public void Build_WithInactiveOrOversizedManifold_ThrowsInvalidOperationException() {
            HelPhysicsBodyPool3D bodies = CreateDynamicBodies(2);
            HelPhysicsIslandBuilder3D builder = new HelPhysicsIslandBuilder3D(2, 2);
            HelPhysicsPairKey3D[] pairs = new HelPhysicsPairKey3D[] {
                new HelPhysicsPairKey3D(0, 1)
            };
            HelPhysicsContactManifold3D oversized = default;
            oversized.ContactCount = 5;

            Assert.Throws<InvalidOperationException>(() => builder.Build(
                bodies,
                pairs,
                new HelPhysicsContactManifold3D[1],
                1));
            Assert.Throws<InvalidOperationException>(() => builder.Build(
                bodies,
                pairs,
                new HelPhysicsContactManifold3D[] { oversized },
                1));
        }

        /// <summary>
        /// Verifies that an island-capacity failure leaves the preceding successful indexed publication unchanged.
        /// </summary>
        [Fact]
        public void Build_WhenIslandCapacityIsExceeded_PreservesPriorPublishedState() {
            HelPhysicsBodyPool3D bodies = CreateDynamicBodies(2);
            HelPhysicsIslandBuilder3D builder = new HelPhysicsIslandBuilder3D(2, 1);
            HelPhysicsPairKey3D[] joinedPairs = new HelPhysicsPairKey3D[] {
                new HelPhysicsPairKey3D(0, 1)
            };
            HelPhysicsContactManifold3D[] joinedManifolds = CreateActiveManifolds(1);
            builder.Build(bodies, joinedPairs, joinedManifolds, 1);

            HelPhysicsCapacityExceededException exception = Assert.Throws<HelPhysicsCapacityExceededException>(
                () => builder.Build(
                    bodies,
                    Array.Empty<HelPhysicsPairKey3D>(),
                    Array.Empty<HelPhysicsContactManifold3D>(),
                    0));

            Assert.Equal("island", exception.PoolName);
            Assert.Equal(1, exception.Capacity);
            Assert.Equal(1, builder.IslandCount);
            AssertIsland(builder, 0, 0, 1);
            Assert.Equal(0, builder.GetIslandIndexForBody(0));
            Assert.Equal(0, builder.GetIslandIndexForBody(1));
        }

        /// <summary>
        /// Verifies that a later invalid pair leaves the preceding successful indexed publication unchanged.
        /// </summary>
        [Fact]
        public void Build_WithInvalidLaterPair_PreservesPriorPublishedState() {
            HelPhysicsBodyPool3D bodies = CreateDynamicBodies(3);
            HelPhysicsIslandBuilder3D builder = new HelPhysicsIslandBuilder3D(3, 3);
            builder.Build(
                bodies,
                new HelPhysicsPairKey3D[] { new HelPhysicsPairKey3D(0, 1) },
                CreateActiveManifolds(1),
                1);
            HelPhysicsContactManifold3D activeManifold = CreateActiveManifold();

            Assert.Throws<ArgumentOutOfRangeException>(() => builder.Build(
                bodies,
                new HelPhysicsPairKey3D[] {
                    new HelPhysicsPairKey3D(1, 2),
                    default
                },
                new HelPhysicsContactManifold3D[] { activeManifold, activeManifold },
                2));

            Assert.Equal(2, builder.IslandCount);
            AssertIsland(builder, 0, 0, 1);
            AssertIsland(builder, 1, 2);
        }

        /// <summary>
        /// Verifies that warmed successful builds reuse all constructor-owned union-find and publication arrays.
        /// </summary>
        [Fact]
        public void Build_AfterWarmup_AllocatesNoManagedMemory() {
            HelPhysicsBodyPool3D bodies = CreateDynamicBodies(4);
            HelPhysicsPairKey3D[] pairs = new HelPhysicsPairKey3D[] {
                new HelPhysicsPairKey3D(0, 1),
                new HelPhysicsPairKey3D(2, 3)
            };
            HelPhysicsContactManifold3D[] manifolds = CreateActiveManifolds(2);
            HelPhysicsIslandBuilder3D builder = new HelPhysicsIslandBuilder3D(4, 4);
            builder.Build(bodies, pairs, manifolds, 2);

            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int iteration = 0; iteration < 1024; iteration++) {
                builder.Build(bodies, pairs, manifolds, 2);
            }
            long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

            Assert.Equal(allocatedBefore, allocatedAfter);
        }

        /// <summary>
        /// Creates a pool containing the requested number of occupied awake dynamic bodies.
        /// </summary>
        /// <param name="bodyCount">Positive number of fixed body slots to allocate and occupy.</param>
        /// <returns>A fully occupied dynamic body pool.</returns>
        static HelPhysicsBodyPool3D CreateDynamicBodies(int bodyCount) {
            HelPhysicsBodyPool3D bodies = new HelPhysicsBodyPool3D(bodyCount);
            for (int bodyIndex = 0; bodyIndex < bodyCount; bodyIndex++) {
                bodies.Allocate(CreateBodyState(true), CreateColdState(BodyKind3D.Dynamic));
            }

            return bodies;
        }

        /// <summary>
        /// Creates finite hot state suitable for dynamic, static, or kinematic island participants.
        /// </summary>
        /// <param name="isAwake">Whether the body begins awake.</param>
        /// <returns>Finite body state with identity orientation and inertia.</returns>
        static HelPhysicsBodyState3D CreateBodyState(bool isAwake) {
            return new HelPhysicsBodyState3D {
                Orientation = PhysicsQuaternion.Identity,
                InverseMass = PhysicsScalar.One,
                LocalInverseInertia = PhysicsMatrix3x3.Identity,
                GravityScale = PhysicsScalar.One,
                IsAwake = isAwake
            };
        }

        /// <summary>
        /// Creates explicit cold metadata with valid sleep settings for one body kind.
        /// </summary>
        /// <param name="bodyKind">Simulation participation mode to store.</param>
        /// <returns>Cold state with neutral material and one-tick sleep configuration.</returns>
        static HelPhysicsBodyColdState3D CreateColdState(BodyKind3D bodyKind) {
            return new HelPhysicsBodyColdState3D(
                default,
                bodyKind,
                new HelPhysicsMaterial3D(PhysicsScalar.Zero, PhysicsScalar.Zero, PhysicsScalar.Zero),
                1,
                ushort.MaxValue,
                0,
                PhysicsScalar.FromFloat(0.01f),
                PhysicsScalar.FromFloat(0.01f),
                1);
        }

        /// <summary>
        /// Creates a fixed array whose every entry represents one active contact manifold.
        /// </summary>
        /// <param name="count">Number of active manifold entries to create.</param>
        /// <returns>An array containing <paramref name="count"/> one-contact manifolds.</returns>
        static HelPhysicsContactManifold3D[] CreateActiveManifolds(int count) {
            HelPhysicsContactManifold3D[] manifolds = new HelPhysicsContactManifold3D[count];
            for (int manifoldIndex = 0; manifoldIndex < count; manifoldIndex++) {
                manifolds[manifoldIndex] = CreateActiveManifold();
            }

            return manifolds;
        }

        /// <summary>
        /// Creates one manifold marked active by a single leading contact.
        /// </summary>
        /// <returns>A manifold with an active contact count of one.</returns>
        static HelPhysicsContactManifold3D CreateActiveManifold() {
            HelPhysicsContactManifold3D manifold = default;
            manifold.ContactCount = 1;
            return manifold;
        }

        /// <summary>
        /// Verifies one published island range against explicit ascending body-index expectations.
        /// </summary>
        /// <param name="builder">Builder containing the published island and flat member arrays.</param>
        /// <param name="islandIndex">Published island index to inspect.</param>
        /// <param name="expectedBodyIndices">Literal ascending member indices expected in the island range.</param>
        static void AssertIsland(
            HelPhysicsIslandBuilder3D builder,
            int islandIndex,
            params int[] expectedBodyIndices) {
            HelPhysicsIsland3D island = builder.GetIsland(islandIndex);
            Assert.Equal(expectedBodyIndices.Length, island.BodyCount);
            for (int memberOffset = 0; memberOffset < expectedBodyIndices.Length; memberOffset++) {
                Assert.Equal(
                    expectedBodyIndices[memberOffset],
                    builder.GetBodyIndex(island.BodyStartIndex + memberOffset));
            }
        }
    }
}
