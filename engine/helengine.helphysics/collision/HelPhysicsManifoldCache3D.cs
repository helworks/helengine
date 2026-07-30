namespace helengine {
    /// <summary>
    /// Persists fixed-size contact manifolds by canonical body pair so sequential simulation steps can warm-start solver impulses.
    /// </summary>
    sealed class HelPhysicsManifoldCache3D {
        /// <summary>
        /// Represents an unused table slot that terminates an unsuccessful linear probe.
        /// </summary>
        const byte EmptyState = 0;

        /// <summary>
        /// Represents a slot that currently owns a valid pair and persisted manifold.
        /// </summary>
        const byte OccupiedState = 1;

        /// <summary>
        /// Represents a removed slot that must remain visible to probes that began before it.
        /// </summary>
        const byte TombstoneState = 2;

        /// <summary>
        /// Stores the combined local-anchor squared-distance limit for geometric fallback matching.
        /// </summary>
        static readonly PhysicsScalar AnchorMatchDistanceSquared = PhysicsScalar.FromFloat(0.0004f);

        /// <summary>
        /// Stores the constructor-owned power-of-two slot array used for bounded linear probing.
        /// </summary>
        readonly HelPhysicsManifoldCacheEntry3D[] Entries;

        /// <summary>
        /// Stores the bit mask that wraps each deterministic hash-table probe index.
        /// </summary>
        readonly int TableMask;

        /// <summary>
        /// Stores how many table slots currently contain an occupied pair.
        /// </summary>
        int CountValue;

        /// <summary>
        /// Initializes an allocation-free manifold table whose fixed capacity must be a positive power of two.
        /// </summary>
        /// <param name="capacity">Number of pair slots to allocate for the lifetime of the cache.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="capacity"/> is not a positive power of two.</exception>
        public HelPhysicsManifoldCache3D(int capacity) {
            if (!IsPositivePowerOfTwo(capacity)) {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Manifold cache capacity must be a positive power of two.");
            }

            Entries = new HelPhysicsManifoldCacheEntry3D[capacity];
            TableMask = capacity - 1;
        }

        /// <summary>
        /// Gets the number of body pairs currently retained by the fixed-capacity table.
        /// </summary>
        public int Count {
            get {
                return CountValue;
            }
        }

        /// <summary>
        /// Gets the fixed number of probe slots allocated for the lifetime of this cache.
        /// </summary>
        public int Capacity => Entries.Length;

        /// <summary>
        /// Warms a newly generated manifold from a retained pair, then persists its current geometry and solver state for the supplied step.
        /// </summary>
        /// <param name="pair">Canonicalizable unordered body pair that owns <paramref name="manifold"/>.</param>
        /// <param name="manifold">New narrow-phase manifold to warm and retain.</param>
        /// <param name="stepId">Simulation step in which the pair was observed.</param>
        /// <exception cref="HelPhysicsCapacityExceededException">Thrown when no free or tombstoned slot remains for a new pair.</exception>
        public void Update(HelPhysicsPairKey3D pair, ref HelPhysicsContactManifold3D manifold, int stepId) {
            int existingEntryIndex = FindExistingEntryIndex(pair);
            if (existingEntryIndex >= 0) {
                ref HelPhysicsManifoldCacheEntry3D existingEntry = ref Entries[existingEntryIndex];
                bool advancesLifetime = existingEntry.StepId != stepId;
                WarmStartManifold(ref manifold, in existingEntry.Manifold, advancesLifetime);
                existingEntry.Manifold = manifold;
                existingEntry.StepId = stepId;
                return;
            }

            int insertionEntryIndex = FindInsertionEntryIndex(pair);
            if (insertionEntryIndex < 0) {
                throw new HelPhysicsCapacityExceededException("manifold", Entries.Length);
            }

            ref HelPhysicsManifoldCacheEntry3D insertionEntry = ref Entries[insertionEntryIndex];
            insertionEntry.Pair = pair;
            insertionEntry.Manifold = manifold;
            insertionEntry.StepId = stepId;
            insertionEntry.State = OccupiedState;
            CountValue++;
        }

        /// <summary>
        /// Replaces solved impulses on an existing same-step manifold while preserving its retained geometry and lifecycle state.
        /// </summary>
        /// <param name="pair">Canonicalizable unordered body pair whose retained manifold was solved.</param>
        /// <param name="manifold">Current manifold containing final normal and tangent impulses in matching contact order.</param>
        /// <param name="stepId">Simulation step that previously updated the retained pair.</param>
        /// <exception cref="KeyNotFoundException">Thrown when <paramref name="pair"/> does not own an occupied cache slot.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the retained step, contact count, or ordered contact features do not match.</exception>
        public void StoreSolved(HelPhysicsPairKey3D pair, ref HelPhysicsContactManifold3D manifold, int stepId) {
            int entryIndex = FindExistingEntryIndex(pair);
            if (entryIndex < 0) {
                throw new KeyNotFoundException("Only a retained manifold pair can store solved impulses.");
            }

            ref HelPhysicsManifoldCacheEntry3D entry = ref Entries[entryIndex];
            if (entry.StepId != stepId) {
                throw new InvalidOperationException("Solved impulses must be stored in the same step that updated the retained manifold.");
            }

            if (entry.Manifold.ContactCount != manifold.ContactCount) {
                throw new InvalidOperationException("Solved impulse writeback requires the retained contact count to remain unchanged.");
            }

            for (int contactIndex = 0; contactIndex < manifold.ContactCount; contactIndex++) {
                HelPhysicsContactPoint3D retainedContact = entry.Manifold.GetContact(contactIndex);
                HelPhysicsContactPoint3D solvedContact = manifold.GetContact(contactIndex);
                if (retainedContact.Feature != solvedContact.Feature) {
                    throw new InvalidOperationException("Solved impulse writeback requires every retained contact feature to remain in matching order.");
                }
            }

            for (int contactIndex = 0; contactIndex < manifold.ContactCount; contactIndex++) {
                HelPhysicsContactPoint3D retainedContact = entry.Manifold.GetContact(contactIndex);
                HelPhysicsContactPoint3D solvedContact = manifold.GetContact(contactIndex);
                retainedContact.AccumulatedNormalImpulse = solvedContact.AccumulatedNormalImpulse;
                retainedContact.AccumulatedTangentImpulse0 = solvedContact.AccumulatedTangentImpulse0;
                retainedContact.AccumulatedTangentImpulse1 = solvedContact.AccumulatedTangentImpulse1;
                entry.Manifold.SetContact(contactIndex, in retainedContact);
            }
        }

        /// <summary>
        /// Marks an already retained, still-overlapping sleeping pair as observed and advances every persisted contact lifetime.
        /// </summary>
        /// <param name="pair">Canonicalizable unordered body pair that must already be retained.</param>
        /// <param name="stepId">Simulation step in which the quiescent pair remained overlapping.</param>
        /// <exception cref="KeyNotFoundException">Thrown when <paramref name="pair"/> does not already own an occupied cache slot.</exception>
        public void Touch(HelPhysicsPairKey3D pair, int stepId) {
            int entryIndex = FindExistingEntryIndex(pair);
            if (entryIndex < 0) {
                throw new KeyNotFoundException("Only a retained manifold pair can be touched.");
            }

            ref HelPhysicsManifoldCacheEntry3D entry = ref Entries[entryIndex];
            if (entry.StepId != stepId) {
                AdvanceContactLifetimes(ref entry.Manifold);
            }
            entry.StepId = stepId;
        }

        /// <summary>
        /// Retrieves the retained manifold for one pair without modifying its lifecycle state.
        /// </summary>
        /// <param name="pair">Canonicalizable unordered body pair to locate.</param>
        /// <param name="manifold">Retained manifold when the pair exists; otherwise the default manifold value.</param>
        /// <returns><see langword="true"/> when the pair exists; otherwise <see langword="false"/>.</returns>
        public bool TryGet(HelPhysicsPairKey3D pair, out HelPhysicsContactManifold3D manifold) {
            int entryIndex = FindExistingEntryIndex(pair);
            if (entryIndex < 0) {
                manifold = default;
                return false;
            }

            manifold = Entries[entryIndex].Manifold;
            return true;
        }

        /// <summary>
        /// Copies one occupied table entry by fixed probe index for allocation-free world composition.
        /// </summary>
        /// <param name="entryIndex">Probe-table index to inspect.</param>
        /// <param name="pair">Receives the retained canonical pair when the slot is occupied.</param>
        /// <param name="manifold">Receives the retained manifold when the slot is occupied.</param>
        /// <returns><see langword="true"/> when the selected slot is occupied; otherwise <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="entryIndex"/> lies outside fixed table storage.</exception>
        public bool TryGetEntry(
            int entryIndex,
            out HelPhysicsPairKey3D pair,
            out HelPhysicsContactManifold3D manifold) {
            if (entryIndex < 0 || entryIndex >= Entries.Length) {
                throw new ArgumentOutOfRangeException(nameof(entryIndex), "The manifold cache entry index lies outside fixed table storage.");
            }

            HelPhysicsManifoldCacheEntry3D entry = Entries[entryIndex];
            if (entry.State != OccupiedState) {
                pair = default;
                manifold = default;
                return false;
            }

            pair = entry.Pair;
            manifold = entry.Manifold;
            return true;
        }

        /// <summary>
        /// Removes every retained pair containing one released body index before that fixed slot may be reused by a new generation.
        /// </summary>
        /// <param name="bodyIndex">Non-negative fixed body slot being released.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="bodyIndex"/> is negative.</exception>
        public void RemoveBody(int bodyIndex) {
            if (bodyIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(bodyIndex), "Removed manifold body indices cannot be negative.");
            }

            for (int entryIndex = 0; entryIndex < Entries.Length; entryIndex++) {
                ref HelPhysicsManifoldCacheEntry3D entry = ref Entries[entryIndex];
                if (entry.State != OccupiedState ||
                    (entry.Pair.FirstBodyIndex != bodyIndex && entry.Pair.SecondBodyIndex != bodyIndex)) {
                    continue;
                }

                entry.Pair = default;
                entry.Manifold = default;
                entry.StepId = default;
                entry.State = TombstoneState;
                CountValue--;
            }
        }

        /// <summary>
        /// Removes pairs not observed in the supplied step while retaining tombstones so colliding probes remain valid.
        /// </summary>
        /// <param name="stepId">Simulation step whose updated or touched pairs must remain retained.</param>
        public void RemoveUntouched(int stepId) {
            for (int entryIndex = 0; entryIndex < Entries.Length; entryIndex++) {
                ref HelPhysicsManifoldCacheEntry3D entry = ref Entries[entryIndex];
                if (entry.State == OccupiedState && entry.StepId != stepId) {
                    entry.Pair = default;
                    entry.Manifold = default;
                    entry.StepId = default;
                    entry.State = TombstoneState;
                    CountValue--;
                }
            }
        }

        /// <summary>
        /// Finds an occupied entry by probing from the deterministic hash index until an empty slot ends the chain.
        /// </summary>
        /// <param name="pair">Canonicalizable unordered body pair to locate.</param>
        /// <returns>Occupied entry index when found; otherwise negative one.</returns>
        int FindExistingEntryIndex(HelPhysicsPairKey3D pair) {
            int entryIndex = pair.GetHashCode() & TableMask;
            for (int probeCount = 0; probeCount < Entries.Length; probeCount++) {
                HelPhysicsManifoldCacheEntry3D entry = Entries[entryIndex];
                if (entry.State == EmptyState) {
                    return -1;
                }

                if (entry.State == OccupiedState && entry.Pair == pair) {
                    return entryIndex;
                }

                entryIndex = (entryIndex + 1) & TableMask;
            }

            return -1;
        }

        /// <summary>
        /// Finds the first reusable tombstone or empty slot reached by the bounded probe for a pair that is not yet retained.
        /// </summary>
        /// <param name="pair">Canonicalizable unordered body pair that requires a slot.</param>
        /// <returns>Reusable table index when available; otherwise negative one.</returns>
        int FindInsertionEntryIndex(HelPhysicsPairKey3D pair) {
            int entryIndex = pair.GetHashCode() & TableMask;
            int firstTombstoneEntryIndex = -1;
            for (int probeCount = 0; probeCount < Entries.Length; probeCount++) {
                HelPhysicsManifoldCacheEntry3D entry = Entries[entryIndex];
                if (entry.State == EmptyState) {
                    if (firstTombstoneEntryIndex >= 0) {
                        return firstTombstoneEntryIndex;
                    }

                    return entryIndex;
                }

                if (entry.State == TombstoneState && firstTombstoneEntryIndex < 0) {
                    firstTombstoneEntryIndex = entryIndex;
                }

                entryIndex = (entryIndex + 1) & TableMask;
            }

            return firstTombstoneEntryIndex;
        }

        /// <summary>
        /// Matches each new contact at most once against the retained manifold and resets unmatched solver state before retaining current geometry.
        /// </summary>
        /// <param name="currentManifold">New narrow-phase manifold whose contacts receive warm-start state.</param>
        /// <param name="previousManifold">Retained manifold that supplies prior contact state.</param>
        /// <param name="advancesLifetime">Whether this update represents a later simulation step than the retained manifold.</param>
        static void WarmStartManifold(
            ref HelPhysicsContactManifold3D currentManifold,
            in HelPhysicsContactManifold3D previousManifold,
            bool advancesLifetime) {
            int usedPreviousContactMask = 0;
            for (int currentContactIndex = 0; currentContactIndex < currentManifold.ContactCount; currentContactIndex++) {
                HelPhysicsContactPoint3D currentContact = currentManifold.GetContact(currentContactIndex);
                ResetSolverState(ref currentContact);
                int previousContactIndex = FindExactFeatureMatchIndex(in currentContact, in previousManifold, usedPreviousContactMask);
                if (previousContactIndex < 0) {
                    previousContactIndex = FindNearestAnchorMatchIndex(in currentContact, in previousManifold, usedPreviousContactMask);
                }

                if (previousContactIndex >= 0) {
                    HelPhysicsContactPoint3D previousContact = previousManifold.GetContact(previousContactIndex);
                    CopyMatchedImpulseState(ref currentContact, in previousContact, advancesLifetime);
                    usedPreviousContactMask |= 1 << previousContactIndex;
                }

                currentManifold.SetContact(currentContactIndex, in currentContact);
            }
        }

        /// <summary>
        /// Finds the lowest-index unused retained contact with exactly equal geometric provenance.
        /// </summary>
        /// <param name="currentContact">New contact whose feature is being matched.</param>
        /// <param name="previousManifold">Retained contacts to inspect.</param>
        /// <param name="usedPreviousContactMask">Bit mask of retained contacts already consumed by earlier new contacts.</param>
        /// <returns>Lowest unused exact-match index, or negative one when no exact feature matches.</returns>
        static int FindExactFeatureMatchIndex(
            in HelPhysicsContactPoint3D currentContact,
            in HelPhysicsContactManifold3D previousManifold,
            int usedPreviousContactMask) {
            for (int previousContactIndex = 0; previousContactIndex < previousManifold.ContactCount; previousContactIndex++) {
                if ((usedPreviousContactMask & (1 << previousContactIndex)) != 0) {
                    continue;
                }

                HelPhysicsContactPoint3D previousContact = previousManifold.GetContact(previousContactIndex);
                if (currentContact.Feature == previousContact.Feature) {
                    return previousContactIndex;
                }
            }

            return -1;
        }

        /// <summary>
        /// Finds the nearest unused retained contact by combined local-anchor distance when it is strictly within the persistence limit.
        /// </summary>
        /// <param name="currentContact">New contact whose local anchors are being matched.</param>
        /// <param name="previousManifold">Retained contacts to inspect.</param>
        /// <param name="usedPreviousContactMask">Bit mask of retained contacts already consumed by earlier new contacts.</param>
        /// <returns>Nearest unused in-range contact index, or negative one when none qualifies.</returns>
        static int FindNearestAnchorMatchIndex(
            in HelPhysicsContactPoint3D currentContact,
            in HelPhysicsContactManifold3D previousManifold,
            int usedPreviousContactMask) {
            int nearestPreviousContactIndex = -1;
            PhysicsScalar nearestDistanceSquared = default;
            for (int previousContactIndex = 0; previousContactIndex < previousManifold.ContactCount; previousContactIndex++) {
                if ((usedPreviousContactMask & (1 << previousContactIndex)) != 0) {
                    continue;
                }

                HelPhysicsContactPoint3D previousContact = previousManifold.GetContact(previousContactIndex);
                PhysicsVector3 anchorASeparation = currentContact.LocalAnchorA - previousContact.LocalAnchorA;
                PhysicsVector3 anchorBSeparation = currentContact.LocalAnchorB - previousContact.LocalAnchorB;
                PhysicsScalar distanceSquared = anchorASeparation.LengthSquared() + anchorBSeparation.LengthSquared();
                if (distanceSquared < AnchorMatchDistanceSquared &&
                    (nearestPreviousContactIndex < 0 || distanceSquared < nearestDistanceSquared)) {
                    nearestPreviousContactIndex = previousContactIndex;
                    nearestDistanceSquared = distanceSquared;
                }
            }

            return nearestPreviousContactIndex;
        }

        /// <summary>
        /// Clears solver impulses and persistence age so newly generated contact geometry cannot retain state from a reused manifold slot.
        /// </summary>
        /// <param name="contact">Current contact whose solver state is reset before persistence matching.</param>
        static void ResetSolverState(ref HelPhysicsContactPoint3D contact) {
            contact.AccumulatedNormalImpulse = PhysicsScalar.Zero;
            contact.AccumulatedTangentImpulse0 = PhysicsScalar.Zero;
            contact.AccumulatedTangentImpulse1 = PhysicsScalar.Zero;
            contact.PreviousStepLifetime = 0;
        }

        /// <summary>
        /// Copies only accumulated solver impulses and persistence age from one matched retained contact to new contact geometry.
        /// </summary>
        /// <param name="currentContact">New contact that receives matched solver state.</param>
        /// <param name="previousContact">Retained contact that supplies solver state without supplying geometry.</param>
        /// <param name="advancesLifetime">Whether the current update represents a later simulation step.</param>
        static void CopyMatchedImpulseState(
            ref HelPhysicsContactPoint3D currentContact,
            in HelPhysicsContactPoint3D previousContact,
            bool advancesLifetime) {
            currentContact.AccumulatedNormalImpulse = previousContact.AccumulatedNormalImpulse;
            currentContact.AccumulatedTangentImpulse0 = previousContact.AccumulatedTangentImpulse0;
            currentContact.AccumulatedTangentImpulse1 = previousContact.AccumulatedTangentImpulse1;
            if (advancesLifetime) {
                currentContact.PreviousStepLifetime = AdvanceLifetime(previousContact.PreviousStepLifetime);
            } else {
                currentContact.PreviousStepLifetime = previousContact.PreviousStepLifetime;
            }
        }

        /// <summary>
        /// Advances the retained lifetime of every active contact for a sleeping pair that remains overlapping.
        /// </summary>
        /// <param name="manifold">Retained manifold whose active contact ages advance by one simulation step.</param>
        static void AdvanceContactLifetimes(ref HelPhysicsContactManifold3D manifold) {
            for (int contactIndex = 0; contactIndex < manifold.ContactCount; contactIndex++) {
                HelPhysicsContactPoint3D contact = manifold.GetContact(contactIndex);
                contact.PreviousStepLifetime = AdvanceLifetime(contact.PreviousStepLifetime);
                manifold.SetContact(contactIndex, in contact);
            }
        }

        /// <summary>
        /// Advances one retained contact lifetime without allowing its non-negative count to overflow.
        /// </summary>
        /// <param name="previousLifetime">Lifetime accumulated through prior simulation steps.</param>
        /// <returns>The next lifetime, saturated at <see cref="int.MaxValue"/>.</returns>
        static int AdvanceLifetime(int previousLifetime) {
            if (previousLifetime == int.MaxValue) {
                return int.MaxValue;
            }

            return previousLifetime + 1;
        }

        /// <summary>
        /// Determines whether a requested table size can use bit-mask wrapping without zero or non-power-of-two capacity.
        /// </summary>
        /// <param name="value">Requested number of fixed cache slots.</param>
        /// <returns><see langword="true"/> when <paramref name="value"/> is a positive power of two; otherwise <see langword="false"/>.</returns>
        static bool IsPositivePowerOfTwo(int value) {
            return value > 0 && (value & (value - 1)) == 0;
        }
    }
}
