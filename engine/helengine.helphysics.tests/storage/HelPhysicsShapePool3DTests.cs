namespace helengine {
    /// <summary>
    /// Verifies fixed-capacity allocation and generational validity for box shapes.
    /// </summary>
    public sealed class HelPhysicsShapePool3DTests {
        /// <summary>
        /// Verifies that a released shape slot is reused with an incremented generation and invalidates the previous handle.
        /// </summary>
        [Fact]
        public void ReleaseAndAllocate_ReusesIndexWithNewGeneration() {
            HelPhysicsShapePool3D pool = new HelPhysicsShapePool3D(1);
            HelPhysicsShapeHandle3D first = pool.Allocate(CreateBox());

            pool.Release(first);
            HelPhysicsShapeHandle3D second = pool.Allocate(CreateBox());

            Assert.Equal(first.Index, second.Index);
            Assert.NotEqual(first.Generation, second.Generation);
            Assert.Throws<InvalidOperationException>(() => pool.GetRequiredBox(first));
        }

        /// <summary>
        /// Verifies that generation exhaustion is detected before the live slot state changes or an old generation can be reissued.
        /// </summary>
        [Fact]
        public void Release_WhenGenerationWouldOverflow_ThrowsWithoutMutatingLiveSlot() {
            HelPhysicsShapePool3D pool = new HelPhysicsShapePool3D(1);
            HelPhysicsShapeHandle3D handle = pool.Allocate(CreateBox());

            for (int releaseCount = 0; releaseCount < ushort.MaxValue; releaseCount++) {
                pool.Release(handle);
                handle = pool.Allocate(CreateBox());
            }

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => pool.Release(handle));
            ref HelPhysicsBoxShape3D box = ref pool.GetRequiredBox(handle);

            Assert.Contains("generation", exception.Message);
            Assert.Contains("exhausted", exception.Message);
            Assert.Equal(1, pool.ActiveCount);
            Assert.Equal(1f, box.HalfExtents.X.ToFloat());
        }

        /// <summary>
        /// Verifies that allocation reports the shape pool name and configured capacity when no shape slot remains.
        /// </summary>
        [Fact]
        public void Allocate_WhenCapacityIsExhausted_ThrowsExactCapacityError() {
            HelPhysicsShapePool3D pool = new HelPhysicsShapePool3D(1);
            pool.Allocate(CreateBox());

            HelPhysicsCapacityExceededException exception = Assert.Throws<HelPhysicsCapacityExceededException>(() => pool.Allocate(CreateBox()));

            Assert.Equal("shape", exception.PoolName);
            Assert.Equal(1, exception.Capacity);
        }

        /// <summary>
        /// Verifies that newly created pools allocate their permanent slots in ascending index order.
        /// </summary>
        [Fact]
        public void Allocate_WhenPoolIsNew_ReturnsHandlesInAscendingIndexOrder() {
            HelPhysicsShapePool3D pool = new HelPhysicsShapePool3D(3);

            HelPhysicsShapeHandle3D first = pool.Allocate(CreateBox());
            HelPhysicsShapeHandle3D second = pool.Allocate(CreateBox());
            HelPhysicsShapeHandle3D third = pool.Allocate(CreateBox());

            Assert.Equal((ushort)0, first.Index);
            Assert.Equal((ushort)1, second.Index);
            Assert.Equal((ushort)2, third.Index);
        }

        /// <summary>
        /// Verifies that a pool preserves the complete box value supplied during allocation.
        /// </summary>
        [Fact]
        public void Allocate_StoresProvidedBox() {
            HelPhysicsShapePool3D pool = new HelPhysicsShapePool3D(1);
            HelPhysicsBoxShape3D expected = new HelPhysicsBoxShape3D(new PhysicsVector3(1f, 2f, 3f));

            HelPhysicsShapeHandle3D handle = pool.Allocate(expected);
            ref HelPhysicsBoxShape3D stored = ref pool.GetRequiredBox(handle);

            Assert.Equal(expected.HalfExtents.X, stored.HalfExtents.X);
            Assert.Equal(expected.HalfExtents.Y, stored.HalfExtents.Y);
            Assert.Equal(expected.HalfExtents.Z, stored.HalfExtents.Z);
            Assert.Equal(1, pool.ActiveCount);
        }

        /// <summary>
        /// Verifies that pools reject capacities outside the handle-addressable fixed-slot range.
        /// </summary>
        [Fact]
        public void Constructor_WithUnaddressableCapacity_ThrowsArgumentOutOfRangeException() {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelPhysicsShapePool3D(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HelPhysicsShapePool3D(65535));
        }

        /// <summary>
        /// Creates a non-uniform box so storage tests observe each independently stored half extent.
        /// </summary>
        /// <returns>A box with positive half extents along all local axes.</returns>
        static HelPhysicsBoxShape3D CreateBox() {
            return new HelPhysicsBoxShape3D(new PhysicsVector3(1f, 2f, 3f));
        }
    }
}
