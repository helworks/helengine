namespace helengine {
    /// <summary>
    /// Verifies fixed-capacity allocation, generational validity, and hot/cold state access for physics bodies.
    /// </summary>
    public sealed class HelPhysicsBodyPool3DTests {
        /// <summary>
        /// Verifies that releasing a body makes its slot reusable while invalidating the earlier handle generation.
        /// </summary>
        [Fact]
        public void ReleaseAndAllocate_ReusesIndexWithNewGeneration() {
            HelPhysicsBodyPool3D pool = new HelPhysicsBodyPool3D(1);
            HelPhysicsBodyHandle3D first = pool.Allocate(CreateDynamicState(), CreateDynamicColdState());

            pool.Release(first);
            HelPhysicsBodyHandle3D second = pool.Allocate(CreateDynamicState(), CreateDynamicColdState());

            Assert.Equal(first.Index, second.Index);
            Assert.NotEqual(first.Generation, second.Generation);
            Assert.Throws<InvalidOperationException>(() => pool.GetRequiredState(first));
        }

        /// <summary>
        /// Verifies that releasing the final representable generation diagnoses exhaustion before the slot can wrap and reissue an ancient handle generation.
        /// </summary>
        [Fact]
        public void Release_WhenGenerationWouldOverflow_ThrowsGenerationExhaustionError() {
            HelPhysicsBodyPool3D pool = new HelPhysicsBodyPool3D(1);
            HelPhysicsBodyHandle3D handle = pool.Allocate(CreateDynamicState(), CreateDynamicColdState());

            for (int releaseCount = 0; releaseCount < ushort.MaxValue; releaseCount++) {
                pool.Release(handle);
                handle = pool.Allocate(CreateDynamicState(), CreateDynamicColdState());
            }

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => pool.Release(handle));

            Assert.Contains("generation", exception.Message);
            Assert.Contains("exhausted", exception.Message);
            Assert.Equal(1, pool.ActiveCount);
        }

        /// <summary>
        /// Verifies that allocation reports the body pool name and fixed capacity when no free slot remains.
        /// </summary>
        [Fact]
        public void Allocate_WhenCapacityIsExhausted_ThrowsExactCapacityError() {
            HelPhysicsBodyPool3D pool = new HelPhysicsBodyPool3D(1);
            pool.Allocate(CreateDynamicState(), CreateDynamicColdState());

            HelPhysicsCapacityExceededException exception = Assert.Throws<HelPhysicsCapacityExceededException>(
                () => pool.Allocate(CreateDynamicState(), CreateDynamicColdState()));

            Assert.Equal("body", exception.PoolName);
            Assert.Equal(1, exception.Capacity);
        }

        /// <summary>
        /// Verifies that a newly created pool exposes its fixed slots in deterministic ascending index order.
        /// </summary>
        [Fact]
        public void Allocate_WhenPoolIsNew_ReturnsHandlesInAscendingIndexOrder() {
            HelPhysicsBodyPool3D pool = new HelPhysicsBodyPool3D(3);

            HelPhysicsBodyHandle3D first = pool.Allocate(CreateDynamicState(), CreateDynamicColdState());
            HelPhysicsBodyHandle3D second = pool.Allocate(CreateDynamicState(), CreateDynamicColdState());
            HelPhysicsBodyHandle3D third = pool.Allocate(CreateDynamicState(), CreateDynamicColdState());

            Assert.Equal((ushort)0, first.Index);
            Assert.Equal((ushort)1, second.Index);
            Assert.Equal((ushort)2, third.Index);
        }

        /// <summary>
        /// Verifies that allocated hot and cold state remain accessible by their currently valid handle.
        /// </summary>
        [Fact]
        public void Allocate_StoresProvidedHotAndColdState() {
            HelPhysicsBodyPool3D pool = new HelPhysicsBodyPool3D(1);
            HelPhysicsBodyState3D expectedState = CreateDynamicState();
            HelPhysicsBodyColdState3D expectedColdState = CreateDynamicColdState();

            HelPhysicsBodyHandle3D handle = pool.Allocate(expectedState, expectedColdState);
            ref HelPhysicsBodyState3D storedState = ref pool.GetRequiredState(handle);
            ref HelPhysicsBodyColdState3D storedColdState = ref pool.GetRequiredColdState(handle);

            Assert.Equal(expectedState.Position.X, storedState.Position.X);
            Assert.Equal(expectedState.Position.Y, storedState.Position.Y);
            Assert.Equal(expectedState.Position.Z, storedState.Position.Z);
            Assert.Equal(expectedState.Orientation.W, storedState.Orientation.W);
            Assert.Equal(expectedState.InverseMass, storedState.InverseMass);
            Assert.True(storedState.IsOccupied);
            Assert.Equal(expectedColdState.ShapeHandle.Index, storedColdState.ShapeHandle.Index);
            Assert.Equal(expectedColdState.ShapeHandle.Generation, storedColdState.ShapeHandle.Generation);
            Assert.Equal(expectedColdState.BodyKind, storedColdState.BodyKind);
            Assert.Equal(expectedColdState.MaterialIndex, storedColdState.MaterialIndex);
            Assert.Equal(expectedColdState.CollisionLayer, storedColdState.CollisionLayer);
            Assert.Equal(expectedColdState.CollisionMask, storedColdState.CollisionMask);
            Assert.Equal(expectedColdState.EntityBindingId, storedColdState.EntityBindingId);
            Assert.Equal(1, pool.ActiveCount);
        }

        /// <summary>
        /// Verifies that releasing a body twice rejects the stale handle instead of adding its slot to the free list twice.
        /// </summary>
        [Fact]
        public void Release_WhenHandleWasAlreadyReleased_ThrowsInvalidOperationException() {
            HelPhysicsBodyPool3D pool = new HelPhysicsBodyPool3D(1);
            HelPhysicsBodyHandle3D handle = pool.Allocate(CreateDynamicState(), CreateDynamicColdState());

            pool.Release(handle);

            Assert.Throws<InvalidOperationException>(() => pool.Release(handle));
        }

        /// <summary>
        /// Verifies that a pool cannot be created without at least one addressable body slot.
        /// </summary>
        [Fact]
        public void Constructor_WhenCapacityIsZero_ThrowsArgumentOutOfRangeException() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelPhysicsBodyPool3D(0));
        }

        /// <summary>
        /// Verifies that a pool reserves the invalid handle index and therefore rejects capacities above 65,534.
        /// </summary>
        [Fact]
        public void Constructor_WhenCapacityExceedsMaximumAddressableSlots_ThrowsArgumentOutOfRangeException() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelPhysicsBodyPool3D(65535));
        }

        /// <summary>
        /// Verifies that world capacity configuration preserves separately fixed body and shape slot counts.
        /// </summary>
        [Fact]
        public void WorldCapacity_StoresBodyAndShapeCapacities() {
            HelPhysicsWorldCapacity3D capacity = new HelPhysicsWorldCapacity3D(29, 31);

            Assert.Equal(29, capacity.BodyCapacity);
            Assert.Equal(31, capacity.ShapeCapacity);
        }

        /// <summary>
        /// Verifies that world capacity rejects an unaddressable body-slot count.
        /// </summary>
        [Fact]
        public void WorldCapacity_WhenBodyCapacityExceedsMaximumAddressableSlots_ThrowsArgumentOutOfRangeException() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelPhysicsWorldCapacity3D(65535, 1));
        }

        /// <summary>
        /// Verifies that world capacity rejects a shape-slot count below the minimum fixed allocation.
        /// </summary>
        [Fact]
        public void WorldCapacity_WhenShapeCapacityIsZero_ThrowsArgumentOutOfRangeException() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelPhysicsWorldCapacity3D(1, 0));
        }

        /// <summary>
        /// Creates populated dynamic state so tests observe stored values instead of default-value coincidences.
        /// </summary>
        /// <returns>Hot state for one awake dynamic body.</returns>
        static HelPhysicsBodyState3D CreateDynamicState() {
            return new HelPhysicsBodyState3D {
                Position = new PhysicsVector3(1f, 2f, 3f),
                Orientation = PhysicsQuaternion.Identity,
                LinearVelocity = new PhysicsVector3(4f, 5f, 6f),
                AngularVelocity = new PhysicsVector3(7f, 8f, 9f),
                InverseMass = PhysicsScalar.FromFloat(0.5f),
                LocalInverseInertia = PhysicsMatrix3x3.Identity,
                GravityScale = PhysicsScalar.One,
                LinearDamping = PhysicsScalar.FromFloat(0.1f),
                AngularDamping = PhysicsScalar.FromFloat(0.2f),
                LowMotionStepCount = 3,
                IsAwake = true
            };
        }

        /// <summary>
        /// Creates populated cold state so tests prove the pool preserves all Task 2 metadata fields.
        /// </summary>
        /// <returns>Cold body metadata associated with the dynamic test body.</returns>
        static HelPhysicsBodyColdState3D CreateDynamicColdState() {
            return new HelPhysicsBodyColdState3D {
                ShapeHandle = new HelPhysicsShapeHandle3D(7, 11),
                BodyKind = BodyKind3D.Dynamic,
                MaterialIndex = 13,
                CollisionLayer = 17,
                CollisionMask = 19,
                EntityBindingId = 23
            };
        }
    }
}
