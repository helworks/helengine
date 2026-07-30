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
            Assert.Equal(expectedState.AccumulatedForce.X, storedState.AccumulatedForce.X);
            Assert.Equal(expectedState.AccumulatedForce.Y, storedState.AccumulatedForce.Y);
            Assert.Equal(expectedState.AccumulatedForce.Z, storedState.AccumulatedForce.Z);
            Assert.Equal(expectedState.AccumulatedTorque.X, storedState.AccumulatedTorque.X);
            Assert.Equal(expectedState.AccumulatedTorque.Y, storedState.AccumulatedTorque.Y);
            Assert.Equal(expectedState.AccumulatedTorque.Z, storedState.AccumulatedTorque.Z);
            Assert.True(storedState.IsOccupied);
            Assert.Equal(expectedColdState.ShapeHandle.Index, storedColdState.ShapeHandle.Index);
            Assert.Equal(expectedColdState.ShapeHandle.Generation, storedColdState.ShapeHandle.Generation);
            Assert.Equal(expectedColdState.BodyKind, storedColdState.BodyKind);
            Assert.Equal(expectedColdState.Material.StaticFriction, storedColdState.Material.StaticFriction);
            Assert.Equal(expectedColdState.Material.DynamicFriction, storedColdState.Material.DynamicFriction);
            Assert.Equal(expectedColdState.Material.Restitution, storedColdState.Material.Restitution);
            Assert.Equal(expectedColdState.CollisionLayer, storedColdState.CollisionLayer);
            Assert.Equal(expectedColdState.CollisionMask, storedColdState.CollisionMask);
            Assert.Equal(expectedColdState.EntityBindingId, storedColdState.EntityBindingId);
            Assert.Equal(expectedColdState.LinearSleepThresholdSquared, storedColdState.LinearSleepThresholdSquared);
            Assert.Equal(expectedColdState.AngularSleepThresholdSquared, storedColdState.AngularSleepThresholdSquared);
            Assert.Equal(expectedColdState.SleepTicks, storedColdState.SleepTicks);
            Assert.Equal(1, pool.ActiveCount);
        }

        /// <summary>
        /// Verifies that cold body construction preserves validated squared sleep thresholds and a positive quiet duration.
        /// </summary>
        [Fact]
        public void ColdState_WithValidSleepSettings_StoresEverySetting() {
            HelPhysicsBodyColdState3D coldState = CreateDynamicColdStateWithSleepSettings(
                PhysicsScalar.FromFloat(0.04f),
                PhysicsScalar.FromFloat(0.09f),
                13);

            Assert.Equal(PhysicsScalar.FromFloat(0.04f), coldState.LinearSleepThresholdSquared);
            Assert.Equal(PhysicsScalar.FromFloat(0.09f), coldState.AngularSleepThresholdSquared);
            Assert.Equal((ushort)13, coldState.SleepTicks);
        }

        /// <summary>
        /// Verifies that cold body construction rejects negative squared thresholds and a zero quiet duration.
        /// </summary>
        [Fact]
        public void ColdState_WithInvalidSleepSettings_ThrowsArgumentOutOfRangeException() {
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateDynamicColdStateWithSleepSettings(
                PhysicsScalar.FromFloat(-0.01f),
                PhysicsScalar.Zero,
                1));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateDynamicColdStateWithSleepSettings(
                PhysicsScalar.Zero,
                PhysicsScalar.FromFloat(-0.01f),
                1));
            Assert.Throws<ArgumentOutOfRangeException>(() => CreateDynamicColdStateWithSleepSettings(
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                0));
            Assert.Throws<ArgumentOutOfRangeException>(() => PhysicsScalar.FromFloat(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => PhysicsScalar.FromFloat(float.PositiveInfinity));
        }

        /// <summary>
        /// Verifies that fixed-index access exposes pool capacity and the live occupant without synthesizing a generational handle.
        /// </summary>
        [Fact]
        public void FixedIndexAccess_WithLiveBody_ReturnsStoredHotAndColdState() {
            HelPhysicsBodyPool3D pool = new HelPhysicsBodyPool3D(3);
            HelPhysicsBodyState3D expectedState = CreateDynamicState();
            HelPhysicsBodyColdState3D expectedColdState = CreateDynamicColdState();
            HelPhysicsBodyHandle3D handle = pool.Allocate(expectedState, expectedColdState);

            ref HelPhysicsBodyState3D storedState = ref pool.GetRequiredStateByIndex(handle.Index);
            ref HelPhysicsBodyColdState3D storedColdState = ref pool.GetRequiredColdStateByIndex(handle.Index);

            Assert.Equal(3, pool.Capacity);
            Assert.True(pool.IsOccupied(handle.Index));
            Assert.Equal(PhysicsScalar.FromFloat(0.5f), storedState.InverseMass);
            Assert.Equal(PhysicsScalar.FromFloat(0.7f), storedColdState.Material.StaticFriction);
        }

        /// <summary>
        /// Verifies that fixed-index handle access identifies the live generation, rejects vacancy, and observes a reused slot's replacement generation.
        /// </summary>
        [Fact]
        public void GetRequiredHandleByIndex_AcrossReleaseAndReuse_ReturnsCurrentOccupantIdentity() {
            HelPhysicsBodyPool3D pool = new HelPhysicsBodyPool3D(1);
            HelPhysicsBodyHandle3D first = pool.Allocate(CreateDynamicState(), CreateDynamicColdState());

            HelPhysicsBodyHandle3D publishedFirst = pool.GetRequiredHandleByIndex(first.Index);
            pool.Release(first);

            Assert.Equal(first.Index, publishedFirst.Index);
            Assert.Equal(first.Generation, publishedFirst.Generation);
            Assert.Throws<InvalidOperationException>(() => pool.GetRequiredHandleByIndex(first.Index));

            HelPhysicsBodyHandle3D replacement = pool.Allocate(CreateDynamicState(), CreateDynamicColdState());
            HelPhysicsBodyHandle3D publishedReplacement = pool.GetRequiredHandleByIndex(replacement.Index);

            Assert.Equal(replacement.Index, publishedReplacement.Index);
            Assert.Equal(replacement.Generation, publishedReplacement.Generation);
            Assert.NotEqual(publishedFirst.Generation, publishedReplacement.Generation);
        }

        /// <summary>
        /// Verifies that fixed-index occupancy reports a released in-range slot without treating it as a live body.
        /// </summary>
        [Fact]
        public void IsOccupied_AfterRelease_ReturnsFalse() {
            HelPhysicsBodyPool3D pool = new HelPhysicsBodyPool3D(1);
            HelPhysicsBodyHandle3D handle = pool.Allocate(CreateDynamicState(), CreateDynamicColdState());

            pool.Release(handle);

            Assert.False(pool.IsOccupied(handle.Index));
            Assert.Throws<InvalidOperationException>(() => pool.GetRequiredStateByIndex(handle.Index));
            Assert.Throws<InvalidOperationException>(() => pool.GetRequiredColdStateByIndex(handle.Index));
        }

        /// <summary>
        /// Verifies that every fixed-index body-pool API rejects indices outside its constructor-owned storage.
        /// </summary>
        [Fact]
        public void FixedIndexAccess_WhenIndexIsOutsideCapacity_ThrowsArgumentOutOfRangeException() {
            HelPhysicsBodyPool3D pool = new HelPhysicsBodyPool3D(1);

            Assert.Throws<ArgumentOutOfRangeException>(() => pool.IsOccupied(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => pool.IsOccupied(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => pool.GetRequiredHandleByIndex(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => pool.GetRequiredHandleByIndex(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => pool.GetRequiredStateByIndex(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => pool.GetRequiredColdStateByIndex(1));
        }

        /// <summary>
        /// Verifies that a material preserves independently authored static friction, dynamic friction, and restitution coefficients.
        /// </summary>
        [Fact]
        public void Material_WithValidCoefficients_StoresEveryCoefficient() {
            HelPhysicsMaterial3D material = new HelPhysicsMaterial3D(
                PhysicsScalar.FromFloat(0.7f),
                PhysicsScalar.FromFloat(0.4f),
                PhysicsScalar.FromFloat(0.25f));

            Assert.Equal(PhysicsScalar.FromFloat(0.7f), material.StaticFriction);
            Assert.Equal(PhysicsScalar.FromFloat(0.4f), material.DynamicFriction);
            Assert.Equal(PhysicsScalar.FromFloat(0.25f), material.Restitution);
        }

        /// <summary>
        /// Verifies that material construction rejects negative friction and restitution outside its physical coefficient range.
        /// </summary>
        [Fact]
        public void Material_WithInvalidCoefficient_ThrowsArgumentOutOfRangeException() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelPhysicsMaterial3D(
                PhysicsScalar.FromFloat(-0.1f),
                PhysicsScalar.Zero,
                PhysicsScalar.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelPhysicsMaterial3D(
                PhysicsScalar.Zero,
                PhysicsScalar.FromFloat(-0.1f),
                PhysicsScalar.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelPhysicsMaterial3D(
                PhysicsScalar.Zero,
                PhysicsScalar.Zero,
                PhysicsScalar.FromFloat(1.1f)));
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
                AccumulatedForce = new PhysicsVector3(10f, 11f, 12f),
                AccumulatedTorque = new PhysicsVector3(13f, 14f, 15f),
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
        /// Creates populated cold state so tests prove the pool preserves all metadata and sleep fields.
        /// </summary>
        /// <returns>Cold body metadata associated with the dynamic test body.</returns>
        static HelPhysicsBodyColdState3D CreateDynamicColdState() {
            return CreateDynamicColdStateWithSleepSettings(
                PhysicsScalar.FromFloat(0.04f),
                PhysicsScalar.FromFloat(0.09f),
                13);
        }

        /// <summary>
        /// Creates complete dynamic cold state through the validated sleep-setting constructor.
        /// </summary>
        /// <param name="linearSleepThresholdSquared">Squared linear speed threshold to validate and store.</param>
        /// <param name="angularSleepThresholdSquared">Squared angular speed threshold to validate and store.</param>
        /// <param name="sleepTicks">Positive quiet duration to validate and store.</param>
        /// <returns>Cold body metadata containing the requested sleep configuration.</returns>
        static HelPhysicsBodyColdState3D CreateDynamicColdStateWithSleepSettings(
            PhysicsScalar linearSleepThresholdSquared,
            PhysicsScalar angularSleepThresholdSquared,
            ushort sleepTicks) {
            return new HelPhysicsBodyColdState3D(
                new HelPhysicsShapeHandle3D(7, 11),
                BodyKind3D.Dynamic,
                new HelPhysicsMaterial3D(
                    PhysicsScalar.FromFloat(0.7f),
                    PhysicsScalar.FromFloat(0.4f),
                    PhysicsScalar.FromFloat(0.25f)),
                17,
                19,
                23,
                linearSleepThresholdSquared,
                angularSleepThresholdSquared,
                sleepTicks);
        }
    }
}
