namespace helengine {
    /// <summary>
    /// Verifies persistent pair lookup, contact matching, and lifecycle behavior for fixed-capacity contact manifolds.
    /// </summary>
    public sealed class HelPhysicsManifoldCache3DTests {
        /// <summary>
        /// Verifies indexed cache inspection copies only occupied entries and validates the fixed probe range.
        /// </summary>
        [Fact]
        public void TryGetEntry_WithOccupiedAndEmptySlots_CopiesOnlyRetainedValues() {
            HelPhysicsManifoldCache3D cache = new HelPhysicsManifoldCache3D(4);
            HelPhysicsPairKey3D expectedPair = new HelPhysicsPairKey3D(2, 5);
            HelPhysicsContactManifold3D expectedManifold = CreateManifold(CreateContact(91u, 3f, 0f, 0f, 0f));
            cache.Update(expectedPair, ref expectedManifold, 7);
            bool found = false;

            for (int entryIndex = 0; entryIndex < cache.Capacity; entryIndex++) {
                if (!cache.TryGetEntry(
                    entryIndex,
                    out HelPhysicsPairKey3D actualPair,
                    out HelPhysicsContactManifold3D actualManifold)) {
                    continue;
                }

                Assert.False(found);
                Assert.Equal(expectedPair, actualPair);
                Assert.Equal((uint)91, actualManifold.GetContact(0).Feature.Value);
                found = true;
            }

            Assert.True(found);
            Assert.Throws<ArgumentOutOfRangeException>(() => cache.TryGetEntry(-1, out _, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => cache.TryGetEntry(cache.Capacity, out _, out _));
        }

        /// <summary>
        /// Verifies body removal tombstones every affected pair while preserving unrelated probe entries and exact count.
        /// </summary>
        [Fact]
        public void RemoveBody_WithSeveralRetainedPairs_RemovesOnlyAffectedEntries() {
            HelPhysicsManifoldCache3D cache = new HelPhysicsManifoldCache3D(8);
            HelPhysicsPairKey3D firstAffectedPair = new HelPhysicsPairKey3D(1, 2);
            HelPhysicsPairKey3D secondAffectedPair = new HelPhysicsPairKey3D(1, 3);
            HelPhysicsPairKey3D retainedPair = new HelPhysicsPairKey3D(2, 3);
            HelPhysicsContactManifold3D manifold = CreateManifold(CreateContact(92u, 0f, 0f, 0f, 0f));
            cache.Update(firstAffectedPair, ref manifold, 1);
            cache.Update(secondAffectedPair, ref manifold, 1);
            cache.Update(retainedPair, ref manifold, 1);

            cache.RemoveBody(1);

            Assert.Equal(1, cache.Count);
            Assert.False(cache.TryGet(firstAffectedPair, out _));
            Assert.False(cache.TryGet(secondAffectedPair, out _));
            Assert.True(cache.TryGet(retainedPair, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => cache.RemoveBody(-1));
        }

        /// <summary>
        /// Verifies that a contact with the same geometric feature receives the prior solver impulses without receiving stale geometry.
        /// </summary>
        [Fact]
        public void Update_WithMatchingFeature_CopiesImpulsesAndAdvancesLifetime() {
            HelPhysicsManifoldCache3D cache = new HelPhysicsManifoldCache3D(4);
            HelPhysicsPairKey3D pair = new HelPhysicsPairKey3D(9, 4);
            HelPhysicsContactManifold3D previousManifold = CreateManifold(CreateContact(1u, 0f, 0f, 0f, 0f));
            HelPhysicsContactPoint3D previousContact = previousManifold.GetContact(0);
            previousContact.AccumulatedNormalImpulse = PhysicsScalar.FromFloat(3f);
            previousContact.AccumulatedTangentImpulse0 = PhysicsScalar.FromFloat(2f);
            previousContact.AccumulatedTangentImpulse1 = PhysicsScalar.FromFloat(-1f);
            previousContact.PreviousStepLifetime = 7;
            previousManifold.SetContact(0, in previousContact);
            cache.Update(pair, ref previousManifold, 10);

            HelPhysicsContactManifold3D currentManifold = CreateManifold(CreateContact(1u, 9f, 0.2f, 0.3f, 0.4f));
            cache.Update(pair, ref currentManifold, 11);

            HelPhysicsContactPoint3D currentContact = currentManifold.GetContact(0);
            Assert.Equal(PhysicsScalar.FromFloat(3f), currentContact.AccumulatedNormalImpulse);
            Assert.Equal(PhysicsScalar.FromFloat(2f), currentContact.AccumulatedTangentImpulse0);
            Assert.Equal(PhysicsScalar.FromFloat(-1f), currentContact.AccumulatedTangentImpulse1);
            Assert.Equal(8, currentContact.PreviousStepLifetime);
            Assert.Equal(PhysicsScalar.FromFloat(9f), currentContact.Position.X);
            Assert.Equal(PhysicsScalar.FromFloat(0.2f), currentContact.LocalAnchorA.X);
            Assert.Equal(PhysicsScalar.FromFloat(0.4f), currentContact.LocalAnchorB.X);
        }

        /// <summary>
        /// Verifies that a changed feature matches the closest unused prior contact when both local anchors remain within the persistence threshold.
        /// </summary>
        [Fact]
        public void Update_WithNearbyLocalAnchors_CopiesImpulsesWhenFeatureChanges() {
            HelPhysicsManifoldCache3D cache = new HelPhysicsManifoldCache3D(4);
            HelPhysicsPairKey3D pair = new HelPhysicsPairKey3D(1, 2);
            HelPhysicsContactManifold3D previousManifold = CreateManifold(CreateContact(11u, 0f, 0f, 0f, 0f));
            HelPhysicsContactPoint3D previousContact = previousManifold.GetContact(0);
            previousContact.AccumulatedNormalImpulse = PhysicsScalar.FromFloat(4f);
            previousContact.AccumulatedTangentImpulse0 = PhysicsScalar.FromFloat(-3f);
            previousContact.AccumulatedTangentImpulse1 = PhysicsScalar.FromFloat(2f);
            previousContact.PreviousStepLifetime = 2;
            previousManifold.SetContact(0, in previousContact);
            cache.Update(pair, ref previousManifold, 1);

            HelPhysicsContactManifold3D currentManifold = CreateManifold(CreateContact(12u, 1f, 0.01f, 0f, 0.01f));
            cache.Update(pair, ref currentManifold, 2);

            HelPhysicsContactPoint3D currentContact = currentManifold.GetContact(0);
            Assert.Equal(PhysicsScalar.FromFloat(4f), currentContact.AccumulatedNormalImpulse);
            Assert.Equal(PhysicsScalar.FromFloat(-3f), currentContact.AccumulatedTangentImpulse0);
            Assert.Equal(PhysicsScalar.FromFloat(2f), currentContact.AccumulatedTangentImpulse1);
            Assert.Equal(3, currentContact.PreviousStepLifetime);
        }

        /// <summary>
        /// Verifies that a contact outside the local-anchor persistence threshold keeps its newly generated zero solver state.
        /// </summary>
        [Fact]
        public void Update_WithGenuinelyNewContact_RetainsZeroImpulsesAndLifetime() {
            HelPhysicsManifoldCache3D cache = new HelPhysicsManifoldCache3D(4);
            HelPhysicsPairKey3D pair = new HelPhysicsPairKey3D(1, 3);
            HelPhysicsContactManifold3D previousManifold = CreateManifold(CreateContact(21u, 0f, 0f, 0f, 0f));
            HelPhysicsContactPoint3D previousContact = previousManifold.GetContact(0);
            previousContact.AccumulatedNormalImpulse = PhysicsScalar.FromFloat(4f);
            previousContact.AccumulatedTangentImpulse0 = PhysicsScalar.FromFloat(3f);
            previousContact.AccumulatedTangentImpulse1 = PhysicsScalar.FromFloat(2f);
            previousContact.PreviousStepLifetime = 5;
            previousManifold.SetContact(0, in previousContact);
            cache.Update(pair, ref previousManifold, 1);

            HelPhysicsContactManifold3D currentManifold = CreateManifold(CreateContact(22u, 1f, 0.1f, 0f, 0.1f));
            cache.Update(pair, ref currentManifold, 2);

            HelPhysicsContactPoint3D currentContact = currentManifold.GetContact(0);
            Assert.Equal(PhysicsScalar.Zero, currentContact.AccumulatedNormalImpulse);
            Assert.Equal(PhysicsScalar.Zero, currentContact.AccumulatedTangentImpulse0);
            Assert.Equal(PhysicsScalar.Zero, currentContact.AccumulatedTangentImpulse1);
            Assert.Equal(0, currentContact.PreviousStepLifetime);
        }

        /// <summary>
        /// Verifies that a reused current manifold slot loses stale solver state when no retained feature or local-anchor match exists.
        /// </summary>
        [Fact]
        public void Update_WithReusedUnmatchedContact_ResetsImpulsesAndLifetime() {
            HelPhysicsManifoldCache3D cache = new HelPhysicsManifoldCache3D(4);
            HelPhysicsPairKey3D pair = new HelPhysicsPairKey3D(1, 4);
            HelPhysicsContactManifold3D previousManifold = CreateManifold(CreateContact(23u, 0f, 0f, 0f, 0f));
            cache.Update(pair, ref previousManifold, 1);

            HelPhysicsContactManifold3D currentManifold = CreateManifold(CreateContact(24u, 1f, 0.1f, 0f, 0.1f));
            HelPhysicsContactPoint3D currentContact = currentManifold.GetContact(0);
            currentContact.AccumulatedNormalImpulse = PhysicsScalar.FromFloat(8f);
            currentContact.AccumulatedTangentImpulse0 = PhysicsScalar.FromFloat(7f);
            currentContact.AccumulatedTangentImpulse1 = PhysicsScalar.FromFloat(6f);
            currentContact.PreviousStepLifetime = 5;
            currentManifold.SetContact(0, in currentContact);

            cache.Update(pair, ref currentManifold, 2);

            currentContact = currentManifold.GetContact(0);
            Assert.Equal(PhysicsScalar.Zero, currentContact.AccumulatedNormalImpulse);
            Assert.Equal(PhysicsScalar.Zero, currentContact.AccumulatedTangentImpulse0);
            Assert.Equal(PhysicsScalar.Zero, currentContact.AccumulatedTangentImpulse1);
            Assert.Equal(0, currentContact.PreviousStepLifetime);
        }

        /// <summary>
        /// Verifies that matching a retained maximum lifetime preserves the maximum instead of overflowing it negative.
        /// </summary>
        [Fact]
        public void Update_WithMaximumLifetime_SaturatesAtMaximumInteger() {
            HelPhysicsManifoldCache3D cache = new HelPhysicsManifoldCache3D(4);
            HelPhysicsPairKey3D pair = new HelPhysicsPairKey3D(1, 5);
            HelPhysicsContactManifold3D previousManifold = CreateManifold(CreateContact(25u, 0f, 0f, 0f, 0f));
            HelPhysicsContactPoint3D previousContact = previousManifold.GetContact(0);
            previousContact.PreviousStepLifetime = int.MaxValue;
            previousManifold.SetContact(0, in previousContact);
            cache.Update(pair, ref previousManifold, 1);

            HelPhysicsContactManifold3D currentManifold = CreateManifold(CreateContact(25u, 1f, 0f, 0f, 0f));
            cache.Update(pair, ref currentManifold, 2);

            Assert.Equal(int.MaxValue, currentManifold.GetContact(0).PreviousStepLifetime);
        }

        /// <summary>
        /// Verifies that a duplicate update for the same step rewrites current geometry and warm-start impulses without advancing lifetime again.
        /// </summary>
        [Fact]
        public void Update_RepeatedForSameStep_DoesNotAdvanceLifetimeAgain() {
            HelPhysicsManifoldCache3D cache = new HelPhysicsManifoldCache3D(4);
            HelPhysicsPairKey3D pair = new HelPhysicsPairKey3D(1, 6);
            HelPhysicsContactManifold3D previousManifold = CreateManifold(CreateContact(26u, 0f, 0f, 0f, 0f));
            HelPhysicsContactPoint3D previousContact = previousManifold.GetContact(0);
            previousContact.AccumulatedNormalImpulse = PhysicsScalar.FromFloat(9f);
            previousContact.PreviousStepLifetime = 5;
            previousManifold.SetContact(0, in previousContact);
            cache.Update(pair, ref previousManifold, 1);

            HelPhysicsContactManifold3D firstCurrentManifold = CreateManifold(CreateContact(26u, 1f, 0f, 0f, 0f));
            cache.Update(pair, ref firstCurrentManifold, 2);
            HelPhysicsContactManifold3D duplicateCurrentManifold = CreateManifold(CreateContact(26u, 2f, 0f, 0f, 0f));
            cache.Update(pair, ref duplicateCurrentManifold, 2);

            HelPhysicsContactPoint3D duplicateContact = duplicateCurrentManifold.GetContact(0);
            Assert.Equal(PhysicsScalar.FromFloat(9f), duplicateContact.AccumulatedNormalImpulse);
            Assert.Equal(6, duplicateContact.PreviousStepLifetime);
            Assert.True(cache.TryGet(pair, out HelPhysicsContactManifold3D retainedManifold));
            Assert.Equal(PhysicsScalar.FromFloat(2f), retainedManifold.GetContact(0).Position.X);
            Assert.Equal(6, retainedManifold.GetContact(0).PreviousStepLifetime);
        }

        /// <summary>
        /// Verifies that stale removal tombstones an occupied slot without breaking lookup through its collision probe chain.
        /// </summary>
        [Fact]
        public void RemoveUntouched_WithCollisionProbeChain_RemovesOnlyStalePair() {
            HelPhysicsManifoldCache3D cache = new HelPhysicsManifoldCache3D(4);
            HelPhysicsPairKey3D stalePair = new HelPhysicsPairKey3D(1, 2);
            HelPhysicsPairKey3D retainedPair = new HelPhysicsPairKey3D(3, 4);
            HelPhysicsContactManifold3D staleManifold = CreateManifold(CreateContact(31u, 0f, 0f, 0f, 0f));
            HelPhysicsContactManifold3D retainedManifold = CreateManifold(CreateContact(32u, 1f, 0f, 0f, 0f));

            Assert.Equal(stalePair.GetHashCode() & 3, retainedPair.GetHashCode() & 3);
            cache.Update(stalePair, ref staleManifold, 1);
            cache.Update(retainedPair, ref retainedManifold, 1);
            cache.Touch(retainedPair, 2);
            cache.RemoveUntouched(2);

            Assert.Equal(1, cache.Count);
            Assert.False(cache.TryGet(stalePair, out HelPhysicsContactManifold3D ignoredManifold));
            Assert.True(cache.TryGet(retainedPair, out HelPhysicsContactManifold3D foundManifold));
            Assert.Equal((uint)32, foundManifold.GetContact(0).Feature.Value);
        }

        /// <summary>
        /// Verifies that touching an existing quiescent pair retains it for the current step and advances its cached contact lifetime.
        /// </summary>
        [Fact]
        public void Touch_WithExistingSleepingPair_RetainsPairAndAdvancesContactLifetime() {
            HelPhysicsManifoldCache3D cache = new HelPhysicsManifoldCache3D(4);
            HelPhysicsPairKey3D pair = new HelPhysicsPairKey3D(6, 7);
            HelPhysicsContactManifold3D manifold = CreateManifold(CreateContact(41u, 0f, 0f, 0f, 0f));
            HelPhysicsContactPoint3D contact = manifold.GetContact(0);
            contact.PreviousStepLifetime = 9;
            manifold.SetContact(0, in contact);
            cache.Update(pair, ref manifold, 1);

            cache.Touch(pair, 2);
            cache.RemoveUntouched(2);

            Assert.True(cache.TryGet(pair, out HelPhysicsContactManifold3D retainedManifold));
            Assert.Equal(10, retainedManifold.GetContact(0).PreviousStepLifetime);
            Assert.Equal(1, cache.Count);
        }

        /// <summary>
        /// Verifies that touching a quiescent pair more than once in one simulation step advances its lifetime only once.
        /// </summary>
        [Fact]
        public void Touch_RepeatedForSameStep_DoesNotAdvanceLifetimeAgain() {
            HelPhysicsManifoldCache3D cache = new HelPhysicsManifoldCache3D(4);
            HelPhysicsPairKey3D pair = new HelPhysicsPairKey3D(1, 7);
            HelPhysicsContactManifold3D manifold = CreateManifold(CreateContact(42u, 0f, 0f, 0f, 0f));
            HelPhysicsContactPoint3D contact = manifold.GetContact(0);
            contact.PreviousStepLifetime = 5;
            manifold.SetContact(0, in contact);
            cache.Update(pair, ref manifold, 1);

            cache.Touch(pair, 2);
            cache.Touch(pair, 2);

            Assert.True(cache.TryGet(pair, out HelPhysicsContactManifold3D retainedManifold));
            Assert.Equal(6, retainedManifold.GetContact(0).PreviousStepLifetime);
        }

        /// <summary>
        /// Verifies that colliding hashes retain distinct canonical pairs and that reversed construction finds the same retained manifold.
        /// </summary>
        [Fact]
        public void Update_WithHashCollision_RetainsDistinctCanonicalPairsDeterministically() {
            HelPhysicsManifoldCache3D cache = new HelPhysicsManifoldCache3D(4);
            HelPhysicsPairKey3D firstPair = new HelPhysicsPairKey3D(1, 2);
            HelPhysicsPairKey3D secondPair = new HelPhysicsPairKey3D(3, 4);
            HelPhysicsContactManifold3D firstManifold = CreateManifold(CreateContact(51u, 0f, 0f, 0f, 0f));
            HelPhysicsContactManifold3D secondManifold = CreateManifold(CreateContact(52u, 1f, 0f, 0f, 0f));

            Assert.Equal(firstPair.GetHashCode() & 3, secondPair.GetHashCode() & 3);
            cache.Update(firstPair, ref firstManifold, 1);
            cache.Update(secondPair, ref secondManifold, 1);

            Assert.True(cache.TryGet(new HelPhysicsPairKey3D(2, 1), out HelPhysicsContactManifold3D firstFoundManifold));
            Assert.True(cache.TryGet(new HelPhysicsPairKey3D(4, 3), out HelPhysicsContactManifold3D secondFoundManifold));
            Assert.Equal((uint)51, firstFoundManifold.GetContact(0).Feature.Value);
            Assert.Equal((uint)52, secondFoundManifold.GetContact(0).Feature.Value);
            Assert.Equal(2, cache.Count);
        }

        /// <summary>
        /// Verifies that a full cache throws the exact fixed-capacity exception without dropping the manifold that already owns its only slot.
        /// </summary>
        [Fact]
        public void Update_WhenCapacityIsExhausted_ThrowsExactManifoldCapacityException() {
            HelPhysicsManifoldCache3D cache = new HelPhysicsManifoldCache3D(1);
            HelPhysicsPairKey3D retainedPair = new HelPhysicsPairKey3D(1, 2);
            HelPhysicsPairKey3D rejectedPair = new HelPhysicsPairKey3D(3, 4);
            HelPhysicsContactManifold3D retainedManifold = CreateManifold(CreateContact(61u, 0f, 0f, 0f, 0f));
            HelPhysicsContactManifold3D rejectedManifold = CreateManifold(CreateContact(62u, 1f, 0f, 0f, 0f));
            cache.Update(retainedPair, ref retainedManifold, 1);

            HelPhysicsCapacityExceededException exception = null;
            try {
                cache.Update(rejectedPair, ref rejectedManifold, 1);
            } catch (HelPhysicsCapacityExceededException caughtException) {
                exception = caughtException;
            }

            Assert.NotNull(exception);
            Assert.Equal("manifold", exception.PoolName);
            Assert.Equal(1, exception.Capacity);
            Assert.True(cache.TryGet(retainedPair, out HelPhysicsContactManifold3D foundManifold));
            Assert.Equal((uint)61, foundManifold.GetContact(0).Feature.Value);
            Assert.Equal(1, cache.Count);
        }

        /// <summary>
        /// Verifies that solved writeback replaces only impulses while retaining the cache's current geometry and contact lifetime.
        /// </summary>
        [Fact]
        public void StoreSolved_WithMatchingSameStepManifold_ReplacesOnlySolvedImpulses() {
            HelPhysicsManifoldCache3D cache = new HelPhysicsManifoldCache3D(4);
            HelPhysicsPairKey3D pair = new HelPhysicsPairKey3D(2, 5);
            HelPhysicsContactManifold3D cachedManifold = CreateManifold(CreateContact(81u, 1f, 0.1f, 0.2f, 0.3f));
            HelPhysicsContactPoint3D cachedContact = cachedManifold.GetContact(0);
            cachedContact.PreviousStepLifetime = 7;
            cachedManifold.SetContact(0, in cachedContact);
            cache.Update(pair, ref cachedManifold, 12);
            HelPhysicsContactManifold3D solvedManifold = CreateManifold(CreateContact(81u, 9f, 0.4f, 0.5f, 0.6f));
            HelPhysicsContactPoint3D solvedContact = solvedManifold.GetContact(0);
            solvedContact.AccumulatedNormalImpulse = PhysicsScalar.FromFloat(6f);
            solvedContact.AccumulatedTangentImpulse0 = PhysicsScalar.FromFloat(-2f);
            solvedContact.AccumulatedTangentImpulse1 = PhysicsScalar.FromFloat(3f);
            solvedContact.PreviousStepLifetime = 99;
            solvedManifold.SetContact(0, in solvedContact);

            cache.StoreSolved(pair, ref solvedManifold, 12);

            Assert.True(cache.TryGet(pair, out HelPhysicsContactManifold3D retainedManifold));
            HelPhysicsContactPoint3D retainedContact = retainedManifold.GetContact(0);
            Assert.Equal(PhysicsScalar.FromFloat(6f), retainedContact.AccumulatedNormalImpulse);
            Assert.Equal(PhysicsScalar.FromFloat(-2f), retainedContact.AccumulatedTangentImpulse0);
            Assert.Equal(PhysicsScalar.FromFloat(3f), retainedContact.AccumulatedTangentImpulse1);
            Assert.Equal(PhysicsScalar.FromFloat(1f), retainedContact.Position.X);
            Assert.Equal(PhysicsScalar.FromFloat(0.1f), retainedContact.LocalAnchorA.X);
            Assert.Equal(PhysicsScalar.FromFloat(0.3f), retainedContact.LocalAnchorB.X);
            Assert.Equal(7, retainedContact.PreviousStepLifetime);
        }

        /// <summary>
        /// Verifies that solved writeback cannot create a cache entry or update a pair retained by a different simulation step.
        /// </summary>
        [Fact]
        public void StoreSolved_WithoutExistingSameStepPair_ThrowsRequiredStateException() {
            HelPhysicsManifoldCache3D cache = new HelPhysicsManifoldCache3D(4);
            HelPhysicsPairKey3D retainedPair = new HelPhysicsPairKey3D(1, 2);
            HelPhysicsPairKey3D missingPair = new HelPhysicsPairKey3D(3, 4);
            HelPhysicsContactManifold3D manifold = CreateManifold(CreateContact(82u, 0f, 0f, 0f, 0f));
            cache.Update(retainedPair, ref manifold, 4);

            Assert.Throws<KeyNotFoundException>(() => cache.StoreSolved(missingPair, ref manifold, 4));
            Assert.Throws<InvalidOperationException>(() => cache.StoreSolved(retainedPair, ref manifold, 5));
        }

        /// <summary>
        /// Verifies that solved writeback rejects a changed contact count before any cached impulse can be replaced.
        /// </summary>
        [Fact]
        public void StoreSolved_WithChangedContactCount_ThrowsInvalidOperationException() {
            HelPhysicsManifoldCache3D cache = new HelPhysicsManifoldCache3D(4);
            HelPhysicsPairKey3D pair = new HelPhysicsPairKey3D(1, 2);
            HelPhysicsContactManifold3D cachedManifold = CreateManifold(CreateContact(83u, 0f, 0f, 0f, 0f));
            cache.Update(pair, ref cachedManifold, 6);
            HelPhysicsContactManifold3D solvedManifold = default;

            Assert.Throws<InvalidOperationException>(() => cache.StoreSolved(pair, ref solvedManifold, 6));
        }

        /// <summary>
        /// Verifies that solved writeback rejects changed feature provenance before any cached impulse can be replaced.
        /// </summary>
        [Fact]
        public void StoreSolved_WithChangedContactFeature_ThrowsInvalidOperationException() {
            HelPhysicsManifoldCache3D cache = new HelPhysicsManifoldCache3D(4);
            HelPhysicsPairKey3D pair = new HelPhysicsPairKey3D(1, 2);
            HelPhysicsContactManifold3D cachedManifold = CreateManifold(CreateContact(84u, 0f, 0f, 0f, 0f));
            cache.Update(pair, ref cachedManifold, 7);
            HelPhysicsContactManifold3D solvedManifold = CreateManifold(CreateContact(85u, 0f, 0f, 0f, 0f));

            Assert.Throws<InvalidOperationException>(() => cache.StoreSolved(pair, ref solvedManifold, 7));
        }

        /// <summary>
        /// Verifies that successful cache hot-path operations reuse the constructor-owned table without managed allocations.
        /// </summary>
        [Fact]
        public void Cache_AfterWarmup_AllocatesNoManagedMemory() {
            HelPhysicsManifoldCache3D cache = new HelPhysicsManifoldCache3D(4);
            HelPhysicsPairKey3D pair = new HelPhysicsPairKey3D(1, 2);
            HelPhysicsContactManifold3D manifold = CreateManifold(CreateContact(71u, 0f, 0f, 0f, 0f));
            cache.Update(pair, ref manifold, 1);
            cache.StoreSolved(pair, ref manifold, 1);
            cache.TryGet(pair, out HelPhysicsContactManifold3D warmedManifold);
            cache.Touch(pair, 1);
            cache.RemoveUntouched(1);

            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int stepId = 2; stepId < 1026; stepId++) {
                cache.Update(pair, ref manifold, stepId);
                cache.StoreSolved(pair, ref manifold, stepId);
                cache.TryGet(pair, out HelPhysicsContactManifold3D foundManifold);
                cache.Touch(pair, stepId);
                cache.RemoveUntouched(stepId);
            }
            long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();

            Assert.Equal(allocatedBefore, allocatedAfter);
        }

        /// <summary>
        /// Creates a one-contact manifold that mirrors narrow-phase output before the solver has accumulated impulses.
        /// </summary>
        /// <param name="contact">Fresh contact geometry to store in the first inline manifold slot.</param>
        /// <returns>A manifold containing exactly the supplied contact.</returns>
        static HelPhysicsContactManifold3D CreateManifold(HelPhysicsContactPoint3D contact) {
            HelPhysicsContactManifold3D manifold = default;
            manifold.ContactCount = 1;
            manifold.SetContact(0, in contact);
            return manifold;
        }

        /// <summary>
        /// Creates distinct deterministic geometry for matching tests while initializing all solver state to the contact constructor defaults.
        /// </summary>
        /// <param name="featureValue">Packed geometric provenance identifier.</param>
        /// <param name="positionX">World-space x coordinate that distinguishes current geometry from retained geometry.</param>
        /// <param name="localAnchorAX">Body-A local-anchor x coordinate.</param>
        /// <param name="localAnchorAY">Body-A local-anchor y coordinate.</param>
        /// <param name="localAnchorBX">Body-B local-anchor x coordinate.</param>
        /// <returns>A newly generated contact point for a single-contact manifold.</returns>
        static HelPhysicsContactPoint3D CreateContact(uint featureValue, float positionX, float localAnchorAX, float localAnchorAY, float localAnchorBX) {
            return new HelPhysicsContactPoint3D(
                new PhysicsVector3(PhysicsScalar.FromFloat(positionX), PhysicsScalar.Zero, PhysicsScalar.Zero),
                PhysicsVector3.UnitY,
                new PhysicsVector3(PhysicsScalar.FromFloat(localAnchorAX), PhysicsScalar.FromFloat(localAnchorAY), PhysicsScalar.Zero),
                new PhysicsVector3(PhysicsScalar.FromFloat(localAnchorBX), PhysicsScalar.Zero, PhysicsScalar.Zero),
                PhysicsScalar.FromFloat(0.5f),
                new HelPhysicsContactFeature3D(featureValue));
        }
    }
}
