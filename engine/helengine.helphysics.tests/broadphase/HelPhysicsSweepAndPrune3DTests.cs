namespace helengine {
    /// <summary>
    /// Verifies deterministic, fixed-capacity candidate generation for the HelPhysics sweep-and-prune broadphase.
    /// </summary>
    public sealed class HelPhysicsSweepAndPrune3DTests {
        /// <summary>
        /// Verifies that one awake dynamic proxy and one overlapping static proxy produce their lower-index-first candidate.
        /// </summary>
        [Fact]
        public void BuildCandidatePairs_WithOverlappingDynamicAndStaticProxy_EmitsOneOrderedPair() {
            HelPhysicsSweepAndPrune3D broadphase = new HelPhysicsSweepAndPrune3D(4, 4);
            broadphase.UpdateProxy(2, BodyKind3D.Dynamic, true, 1, ushort.MaxValue, CreateAabb(-1f, 1f));
            broadphase.UpdateProxy(1, BodyKind3D.Static, true, 1, ushort.MaxValue, CreateAabb(-2f, 2f));
            HelPhysicsCandidatePair3D[] pairs = new HelPhysicsCandidatePair3D[4];

            int count = broadphase.BuildCandidatePairs(pairs);

            Assert.Equal(1, count);
            AssertPair(pairs[0], 1, 2);
        }

        /// <summary>
        /// Verifies that overlapping static proxies do not create a candidate even when both update calls mark them active.
        /// </summary>
        [Fact]
        public void BuildCandidatePairs_WithOverlappingStaticProxies_RejectsStaticStaticPair() {
            HelPhysicsSweepAndPrune3D broadphase = new HelPhysicsSweepAndPrune3D(2, 2);
            broadphase.UpdateProxy(1, BodyKind3D.Static, true, 1, ushort.MaxValue, CreateAabb(-1f, 1f));
            broadphase.UpdateProxy(2, BodyKind3D.Static, true, 1, ushort.MaxValue, CreateAabb(-1f, 1f));
            HelPhysicsCandidatePair3D[] pairs = new HelPhysicsCandidatePair3D[2];

            int count = broadphase.BuildCandidatePairs(pairs);

            Assert.Equal(0, count);
        }

        /// <summary>
        /// Verifies that X-axis sweep overlap alone is insufficient when bounds are separated on the Y axis.
        /// </summary>
        [Fact]
        public void BuildCandidatePairs_WithYSeparatedProxies_RejectsNonOverlappingPair() {
            HelPhysicsSweepAndPrune3D broadphase = new HelPhysicsSweepAndPrune3D(2, 2);
            broadphase.UpdateProxy(1, BodyKind3D.Dynamic, true, 1, ushort.MaxValue, CreateAabb(-1f, 1f, -1f, 1f, -1f, 1f));
            broadphase.UpdateProxy(2, BodyKind3D.Static, true, 1, ushort.MaxValue, CreateAabb(-1f, 1f, 2f, 4f, -1f, 1f));
            HelPhysicsCandidatePair3D[] pairs = new HelPhysicsCandidatePair3D[2];

            int count = broadphase.BuildCandidatePairs(pairs);

            Assert.Equal(0, count);
        }

        /// <summary>
        /// Verifies that collision masks must permit interaction in both layer-to-mask directions.
        /// </summary>
        [Fact]
        public void BuildCandidatePairs_WithOneWayMaskRejection_RejectsPair() {
            HelPhysicsSweepAndPrune3D broadphase = new HelPhysicsSweepAndPrune3D(2, 2);
            broadphase.UpdateProxy(1, BodyKind3D.Dynamic, true, 1, 2, CreateAabb(-1f, 1f));
            broadphase.UpdateProxy(2, BodyKind3D.Static, true, 2, 0, CreateAabb(-1f, 1f));
            HelPhysicsCandidatePair3D[] pairs = new HelPhysicsCandidatePair3D[2];

            int count = broadphase.BuildCandidatePairs(pairs);

            Assert.Equal(0, count);
        }

        /// <summary>
        /// Verifies that endpoint ordering remains deterministic after a proxy crosses earlier X endpoints.
        /// </summary>
        [Fact]
        public void BuildCandidatePairs_AfterProxyMotion_EmitsDeterministicEndpointOrder() {
            HelPhysicsSweepAndPrune3D broadphase = new HelPhysicsSweepAndPrune3D(3, 4);
            broadphase.UpdateProxy(1, BodyKind3D.Static, true, 1, ushort.MaxValue, CreateAabb(-2f, 2f));
            broadphase.UpdateProxy(2, BodyKind3D.Dynamic, true, 1, ushort.MaxValue, CreateAabb(-1f, 5f));
            broadphase.UpdateProxy(3, BodyKind3D.Dynamic, true, 1, ushort.MaxValue, CreateAabb(1f, 3f));
            HelPhysicsCandidatePair3D[] pairs = new HelPhysicsCandidatePair3D[4];

            broadphase.BuildCandidatePairs(pairs);
            broadphase.UpdateProxy(3, BodyKind3D.Dynamic, true, 1, ushort.MaxValue, CreateAabb(-3f, 1f));
            int firstCount = broadphase.BuildCandidatePairs(pairs);
            HelPhysicsCandidatePair3D first = pairs[0];
            HelPhysicsCandidatePair3D second = pairs[1];
            HelPhysicsCandidatePair3D third = pairs[2];
            int secondCount = broadphase.BuildCandidatePairs(pairs);

            Assert.Equal(3, firstCount);
            Assert.Equal(firstCount, secondCount);
            AssertPair(first, 1, 3);
            AssertPair(second, 2, 3);
            AssertPair(third, 1, 2);
            AssertPair(pairs[0], 1, 3);
            AssertPair(pairs[1], 2, 3);
            AssertPair(pairs[2], 1, 2);
        }

        /// <summary>
        /// Verifies that a sleeping dynamic proxy does not wake a static interaction by itself.
        /// </summary>
        [Fact]
        public void BuildCandidatePairs_WithSleepingDynamicAndStaticProxy_RejectsQuiescentPair() {
            HelPhysicsSweepAndPrune3D broadphase = new HelPhysicsSweepAndPrune3D(2, 2);
            broadphase.UpdateProxy(1, BodyKind3D.Dynamic, false, 1, ushort.MaxValue, CreateAabb(-1f, 1f));
            broadphase.UpdateProxy(2, BodyKind3D.Static, true, 1, ushort.MaxValue, CreateAabb(-1f, 1f));
            HelPhysicsCandidatePair3D[] pairs = new HelPhysicsCandidatePair3D[2];

            int count = broadphase.BuildCandidatePairs(pairs);

            Assert.Equal(0, count);
        }

        /// <summary>
        /// Verifies that two sleeping dynamic proxies remain quiescent while their bounds overlap.
        /// </summary>
        [Fact]
        public void BuildCandidatePairs_WithSleepingDynamicProxies_RejectsQuiescentPair() {
            HelPhysicsSweepAndPrune3D broadphase = new HelPhysicsSweepAndPrune3D(2, 2);
            broadphase.UpdateProxy(1, BodyKind3D.Dynamic, false, 1, ushort.MaxValue, CreateAabb(-1f, 1f));
            broadphase.UpdateProxy(2, BodyKind3D.Dynamic, false, 1, ushort.MaxValue, CreateAabb(-1f, 1f));
            HelPhysicsCandidatePair3D[] pairs = new HelPhysicsCandidatePair3D[2];

            int count = broadphase.BuildCandidatePairs(pairs);

            Assert.Equal(0, count);
        }

        /// <summary>
        /// Verifies that a moved kinematic proxy makes an overlapping static interaction eligible for narrow phase.
        /// </summary>
        [Fact]
        public void BuildCandidatePairs_WithMovedKinematicAndStaticProxy_EmitsPair() {
            HelPhysicsSweepAndPrune3D broadphase = new HelPhysicsSweepAndPrune3D(2, 2);
            broadphase.UpdateProxy(1, BodyKind3D.Kinematic, true, 1, ushort.MaxValue, CreateAabb(-1f, 1f));
            broadphase.UpdateProxy(2, BodyKind3D.Static, true, 1, ushort.MaxValue, CreateAabb(-1f, 1f));
            HelPhysicsCandidatePair3D[] pairs = new HelPhysicsCandidatePair3D[2];

            int count = broadphase.BuildCandidatePairs(pairs);

            Assert.Equal(1, count);
            AssertPair(pairs[0], 1, 2);
        }

        /// <summary>
        /// Verifies that candidate generation diagnoses the configured constructor pair capacity instead of truncating output.
        /// </summary>
        [Fact]
        public void BuildCandidatePairs_WhenConfiguredCandidateCapacityIsExhausted_ThrowsExactCapacityError() {
            HelPhysicsSweepAndPrune3D broadphase = CreateThreeOverlappingDynamicProxyBroadphase(1);
            HelPhysicsCandidatePair3D[] pairs = new HelPhysicsCandidatePair3D[3];

            try {
                broadphase.BuildCandidatePairs(pairs);
                Assert.Fail("Expected candidate-pair capacity exhaustion.");
            } catch (HelPhysicsCapacityExceededException exception) {
                Assert.Equal("candidate pair", exception.PoolName);
                Assert.Equal(1, exception.Capacity);
                Assert.Equal("The candidate pair pool capacity of 1 has been exceeded.", exception.Message);
            }
        }

        /// <summary>
        /// Verifies that destination array capacity is independently enforced with the same candidate-pair diagnostic.
        /// </summary>
        [Fact]
        public void BuildCandidatePairs_WhenDestinationCapacityIsExhausted_ThrowsExactCapacityError() {
            HelPhysicsSweepAndPrune3D broadphase = CreateThreeOverlappingDynamicProxyBroadphase(3);
            HelPhysicsCandidatePair3D[] pairs = new HelPhysicsCandidatePair3D[1];

            try {
                broadphase.BuildCandidatePairs(pairs);
                Assert.Fail("Expected destination candidate-pair capacity exhaustion.");
            } catch (HelPhysicsCapacityExceededException exception) {
                Assert.Equal("candidate pair", exception.PoolName);
                Assert.Equal(1, exception.Capacity);
                Assert.Equal("The candidate pair pool capacity of 1 has been exceeded.", exception.Message);
            }
        }

        /// <summary>
        /// Verifies that removing a proxy removes all of its endpoint participation before the next candidate build.
        /// </summary>
        [Fact]
        public void RemoveProxy_RemovesExistingProxyFromCandidatePairs() {
            HelPhysicsSweepAndPrune3D broadphase = new HelPhysicsSweepAndPrune3D(2, 2);
            broadphase.UpdateProxy(1, BodyKind3D.Dynamic, true, 1, ushort.MaxValue, CreateAabb(-1f, 1f));
            broadphase.UpdateProxy(2, BodyKind3D.Static, true, 1, ushort.MaxValue, CreateAabb(-1f, 1f));
            HelPhysicsCandidatePair3D[] pairs = new HelPhysicsCandidatePair3D[2];

            broadphase.RemoveProxy(1);
            int count = broadphase.BuildCandidatePairs(pairs);

            Assert.Equal(0, count);
        }

        /// <summary>
        /// Verifies that candidate builds reuse constructor-allocated endpoint and active-set memory without allocating.
        /// </summary>
        [Fact]
        public void BuildCandidatePairs_AfterWarmup_AllocatesNoMemory() {
            HelPhysicsSweepAndPrune3D broadphase = CreateThreeOverlappingDynamicProxyBroadphase(3);
            HelPhysicsCandidatePair3D[] pairs = new HelPhysicsCandidatePair3D[3];
            broadphase.BuildCandidatePairs(pairs);

            long bytesBefore = GC.GetAllocatedBytesForCurrentThread();
            int count = broadphase.BuildCandidatePairs(pairs);
            long bytesAfter = GC.GetAllocatedBytesForCurrentThread();

            Assert.Equal(3, count);
            Assert.Equal(bytesBefore, bytesAfter);
        }

        /// <summary>
        /// Creates three fully overlapping awake dynamic proxies that produce three distinct candidate pairs.
        /// </summary>
        /// <param name="candidateCapacity">Fixed candidate capacity configured for the broadphase.</param>
        /// <returns>A broadphase populated with three mutually overlapping dynamic proxies.</returns>
        static HelPhysicsSweepAndPrune3D CreateThreeOverlappingDynamicProxyBroadphase(int candidateCapacity) {
            HelPhysicsSweepAndPrune3D broadphase = new HelPhysicsSweepAndPrune3D(3, candidateCapacity);
            broadphase.UpdateProxy(1, BodyKind3D.Dynamic, true, 1, ushort.MaxValue, CreateAabb(-1f, 1f));
            broadphase.UpdateProxy(2, BodyKind3D.Dynamic, true, 1, ushort.MaxValue, CreateAabb(-1f, 1f));
            broadphase.UpdateProxy(3, BodyKind3D.Dynamic, true, 1, ushort.MaxValue, CreateAabb(-1f, 1f));
            return broadphase;
        }

        /// <summary>
        /// Creates cubic bounds using a shared interval on all three world axes.
        /// </summary>
        /// <param name="minimum">Inclusive lower coordinate used on all axes.</param>
        /// <param name="maximum">Inclusive upper coordinate used on all axes.</param>
        /// <returns>Inclusive cubic bounds spanning the supplied interval.</returns>
        static HelPhysicsAabb3D CreateAabb(float minimum, float maximum) {
            return CreateAabb(minimum, maximum, minimum, maximum, minimum, maximum);
        }

        /// <summary>
        /// Creates axis-aligned bounds from explicit inclusive intervals on every world axis.
        /// </summary>
        /// <param name="minimumX">Inclusive lower X coordinate.</param>
        /// <param name="maximumX">Inclusive upper X coordinate.</param>
        /// <param name="minimumY">Inclusive lower Y coordinate.</param>
        /// <param name="maximumY">Inclusive upper Y coordinate.</param>
        /// <param name="minimumZ">Inclusive lower Z coordinate.</param>
        /// <param name="maximumZ">Inclusive upper Z coordinate.</param>
        /// <returns>Inclusive bounds covering every supplied axis interval.</returns>
        static HelPhysicsAabb3D CreateAabb(float minimumX, float maximumX, float minimumY, float maximumY, float minimumZ, float maximumZ) {
            return new HelPhysicsAabb3D(
                new PhysicsVector3(minimumX, minimumY, minimumZ),
                new PhysicsVector3(maximumX, maximumY, maximumZ));
        }

        /// <summary>
        /// Verifies that one candidate stores the supplied body indices in canonical ascending order.
        /// </summary>
        /// <param name="pair">Candidate pair to inspect.</param>
        /// <param name="firstBodyIndex">Expected lower body index.</param>
        /// <param name="secondBodyIndex">Expected higher body index.</param>
        static void AssertPair(HelPhysicsCandidatePair3D pair, int firstBodyIndex, int secondBodyIndex) {
            Assert.Equal(firstBodyIndex, pair.FirstBodyIndex);
            Assert.Equal(secondBodyIndex, pair.SecondBodyIndex);
        }
    }
}
