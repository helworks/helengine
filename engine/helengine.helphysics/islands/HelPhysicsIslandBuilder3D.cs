namespace helengine {
    /// <summary>
    /// Builds deterministic dynamic-body contact islands into transactionally published fixed-capacity indexed arrays.
    /// </summary>
    sealed class HelPhysicsIslandBuilder3D {
        /// <summary>
        /// Stores the minimum supported fixed body or island capacity.
        /// </summary>
        const int MinimumCapacity = 1;

        /// <summary>
        /// Stores the largest body capacity representable by the fixed body-handle index contract.
        /// </summary>
        const int MaximumBodyCapacity = 65534;

        /// <summary>
        /// Stores currently published island ranges in deterministic minimum-member order.
        /// </summary>
        HelPhysicsIsland3D[] Islands;

        /// <summary>
        /// Stores inactive island ranges until a complete successful build publishes them atomically.
        /// </summary>
        HelPhysicsIsland3D[] StagingIslands;

        /// <summary>
        /// Stores currently published ascending dynamic body indices grouped by contiguous island range.
        /// </summary>
        int[] BodyIndices;

        /// <summary>
        /// Stores inactive grouped body indices until successful publication.
        /// </summary>
        int[] StagingBodyIndices;

        /// <summary>
        /// Stores the full generational body identity parallel to every currently published member index.
        /// </summary>
        HelPhysicsBodyHandle3D[] BodyHandles;

        /// <summary>
        /// Stores inactive member identities until their complete ranges and lookups publish successfully.
        /// </summary>
        HelPhysicsBodyHandle3D[] StagingBodyHandles;

        /// <summary>
        /// Maps each fixed body slot to its currently published island index, or negative one when it is not a dynamic member.
        /// </summary>
        int[] BodyToIslandIndices;

        /// <summary>
        /// Stores inactive body-to-island lookup results until successful publication.
        /// </summary>
        int[] StagingBodyToIslandIndices;

        /// <summary>
        /// Stores constructor-owned union-find parents for occupied dynamic body slots and negative one for all other slots.
        /// </summary>
        readonly int[] Parents;

        /// <summary>
        /// Stores constructor-owned union-by-rank depth estimates parallel to <see cref="Parents"/>.
        /// </summary>
        readonly byte[] Ranks;

        /// <summary>
        /// Maps each current union root to its deterministic staging island index during construction.
        /// </summary>
        readonly int[] RootToIslandIndices;

        /// <summary>
        /// Stores the staged member count accumulated for each deterministic island before ranges are assigned.
        /// </summary>
        readonly int[] StagingIslandMemberCounts;

        /// <summary>
        /// Stores the next flat member destination for each staging island during ascending body traversal.
        /// </summary>
        readonly int[] StagingIslandWriteIndices;

        /// <summary>
        /// Stores how many leading published island ranges are current.
        /// </summary>
        int IslandCountValue;

        /// <summary>
        /// Stores how many leading published flat member entries are current.
        /// </summary>
        int BodyCountValue;

        /// <summary>
        /// Initializes every active, staging, union-find, range, and lookup array needed for later allocation-free builds.
        /// </summary>
        /// <param name="bodyCapacity">Fixed body-pool capacity this builder addresses exactly.</param>
        /// <param name="islandCapacity">Positive maximum number of simultaneous dynamic islands.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when capacities are invalid or island capacity exceeds body capacity.</exception>
        public HelPhysicsIslandBuilder3D(int bodyCapacity, int islandCapacity) {
            if (bodyCapacity < MinimumCapacity || bodyCapacity > MaximumBodyCapacity) {
                throw new ArgumentOutOfRangeException(nameof(bodyCapacity), "Island body capacity must be between 1 and 65,534 inclusive.");
            }

            if (islandCapacity < MinimumCapacity || islandCapacity > bodyCapacity) {
                throw new ArgumentOutOfRangeException(nameof(islandCapacity), "Island capacity must be positive and cannot exceed body capacity.");
            }

            Islands = new HelPhysicsIsland3D[islandCapacity];
            StagingIslands = new HelPhysicsIsland3D[islandCapacity];
            BodyIndices = new int[bodyCapacity];
            StagingBodyIndices = new int[bodyCapacity];
            BodyHandles = new HelPhysicsBodyHandle3D[bodyCapacity];
            StagingBodyHandles = new HelPhysicsBodyHandle3D[bodyCapacity];
            BodyToIslandIndices = new int[bodyCapacity];
            StagingBodyToIslandIndices = new int[bodyCapacity];
            Parents = new int[bodyCapacity];
            Ranks = new byte[bodyCapacity];
            RootToIslandIndices = new int[bodyCapacity];
            StagingIslandMemberCounts = new int[islandCapacity];
            StagingIslandWriteIndices = new int[islandCapacity];

            for (int bodyIndex = 0; bodyIndex < bodyCapacity; bodyIndex++) {
                BodyToIslandIndices[bodyIndex] = -1;
                StagingBodyToIslandIndices[bodyIndex] = -1;
            }
        }

        /// <summary>
        /// Gets the fixed body-slot count addressed by member and lookup arrays.
        /// </summary>
        public int BodyCapacity => BodyIndices.Length;

        /// <summary>
        /// Gets the fixed number of island ranges available to each transactional publication buffer.
        /// </summary>
        public int IslandCapacity => Islands.Length;

        /// <summary>
        /// Gets the number of islands in the most recent successful publication.
        /// </summary>
        public int IslandCount => IslandCountValue;

        /// <summary>
        /// Gets the number of dynamic members in the most recent successful publication.
        /// </summary>
        public int BodyCount => BodyCountValue;

        /// <summary>
        /// Validates all active manifold inputs, unions only dynamic-dynamic contacts, and atomically publishes sorted indexed islands.
        /// </summary>
        /// <param name="bodies">Fixed body pool whose capacity must exactly match this builder.</param>
        /// <param name="pairs">Canonical distinct active body pairs parallel to <paramref name="manifolds"/>.</param>
        /// <param name="manifolds">Active contact manifolds parallel to <paramref name="pairs"/>.</param>
        /// <param name="manifoldCount">Number of leading pair and manifold entries active this step.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required pool or array is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when pool capacity or parallel array lengths do not match fixed requirements.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the active count or a pair index is invalid.</exception>
        /// <exception cref="InvalidOperationException">Thrown when body occupancy, body mode, pair uniqueness, or manifold activity is invalid.</exception>
        /// <exception cref="HelPhysicsCapacityExceededException">Thrown when disconnected dynamic groups exceed fixed island capacity.</exception>
        public void Build(
            HelPhysicsBodyPool3D bodies,
            HelPhysicsPairKey3D[] pairs,
            HelPhysicsContactManifold3D[] manifolds,
            int manifoldCount) {
            ValidateBuildInputs(bodies, pairs, manifolds, manifoldCount);
            ValidateBodyPool(bodies);
            ValidateActiveManifolds(bodies, pairs, manifolds, manifoldCount);
            InitializeUnionFind(bodies);

            for (int manifoldIndex = 0; manifoldIndex < manifoldCount; manifoldIndex++) {
                HelPhysicsPairKey3D pair = pairs[manifoldIndex];
                ref HelPhysicsBodyColdState3D coldStateA = ref bodies.GetRequiredColdStateByIndex(pair.FirstBodyIndex);
                ref HelPhysicsBodyColdState3D coldStateB = ref bodies.GetRequiredColdStateByIndex(pair.SecondBodyIndex);
                if (coldStateA.BodyKind == BodyKind3D.Dynamic && coldStateB.BodyKind == BodyKind3D.Dynamic) {
                    Union(pair.FirstBodyIndex, pair.SecondBodyIndex);
                }
            }

            int stagingIslandCount = StageIslandRanges(bodies);
            int stagingBodyCount = StageIslandMembers(bodies);
            Publish(stagingIslandCount, stagingBodyCount);
        }

        /// <summary>
        /// Returns one published island range by deterministic island index.
        /// </summary>
        /// <param name="islandIndex">Published island index to inspect.</param>
        /// <returns>The selected contiguous member range.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside the current publication.</exception>
        public HelPhysicsIsland3D GetIsland(int islandIndex) {
            if (islandIndex < 0 || islandIndex >= IslandCountValue) {
                throw new ArgumentOutOfRangeException(nameof(islandIndex), "The island index is outside the current publication.");
            }

            return Islands[islandIndex];
        }

        /// <summary>
        /// Returns one body index from the published flat member array.
        /// </summary>
        /// <param name="memberIndex">Flat member index contained by one published island range.</param>
        /// <returns>The occupied dynamic body slot stored at that index.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside current published members.</exception>
        public int GetBodyIndex(int memberIndex) {
            if (memberIndex < 0 || memberIndex >= BodyCountValue) {
                throw new ArgumentOutOfRangeException(nameof(memberIndex), "The island member index is outside the current publication.");
            }

            return BodyIndices[memberIndex];
        }

        /// <summary>
        /// Returns the full body identity captured for one published flat island member.
        /// </summary>
        /// <param name="memberIndex">Flat member index contained by one published island range.</param>
        /// <returns>The occupied dynamic body handle captured when the publication succeeded.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is outside current published members.</exception>
        public HelPhysicsBodyHandle3D GetBodyHandle(int memberIndex) {
            if (memberIndex < 0 || memberIndex >= BodyCountValue) {
                throw new ArgumentOutOfRangeException(nameof(memberIndex), "The island member index is outside the current publication.");
            }

            return BodyHandles[memberIndex];
        }

        /// <summary>
        /// Returns the published island containing one fixed body slot, or negative one for a non-dynamic or absent slot.
        /// </summary>
        /// <param name="bodyIndex">Fixed body slot to locate.</param>
        /// <returns>Current island index, or negative one when the slot is not a published dynamic member.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the body index lies outside fixed lookup storage.</exception>
        public int GetIslandIndexForBody(int bodyIndex) {
            if (bodyIndex < 0 || bodyIndex >= BodyToIslandIndices.Length) {
                throw new ArgumentOutOfRangeException(nameof(bodyIndex), "The body index lies outside island lookup capacity.");
            }

            return BodyToIslandIndices[bodyIndex];
        }

        /// <summary>
        /// Validates complete pool, array, body, pair, and manifold input without touching active or staging publication storage.
        /// </summary>
        /// <param name="bodies">Fixed body pool to validate.</param>
        /// <param name="pairs">Pair array parallel to manifolds.</param>
        /// <param name="manifolds">Manifold array parallel to pairs.</param>
        /// <param name="manifoldCount">Number of leading active entries.</param>
        static void ValidateBuildInputs(
            HelPhysicsBodyPool3D bodies,
            HelPhysicsPairKey3D[] pairs,
            HelPhysicsContactManifold3D[] manifolds,
            int manifoldCount) {
            if (bodies == null) {
                throw new ArgumentNullException(nameof(bodies));
            } else if (pairs == null) {
                throw new ArgumentNullException(nameof(pairs));
            } else if (manifolds == null) {
                throw new ArgumentNullException(nameof(manifolds));
            }

            if (pairs.Length != manifolds.Length) {
                throw new ArgumentException("Pair and manifold arrays must have equal lengths.", nameof(pairs));
            }

            if (manifoldCount < 0 || manifoldCount > pairs.Length) {
                throw new ArgumentOutOfRangeException(nameof(manifoldCount), "Active manifold count must fit the parallel input arrays.");
            }
        }

        /// <summary>
        /// Validates instance-specific pool capacity and all occupied body modes before union-find scratch is initialized.
        /// </summary>
        /// <param name="bodies">Body pool that must match constructor-owned body storage.</param>
        /// <exception cref="ArgumentException">Thrown when body capacity differs.</exception>
        /// <exception cref="InvalidOperationException">Thrown when an occupied body has an unsupported mode.</exception>
        void ValidateBodyPool(HelPhysicsBodyPool3D bodies) {
            if (bodies.Capacity != BodyIndices.Length) {
                throw new ArgumentException("Body pool capacity must match island builder body capacity.", nameof(bodies));
            }

            for (int bodyIndex = 0; bodyIndex < bodies.Capacity; bodyIndex++) {
                if (!bodies.IsOccupied(bodyIndex)) {
                    continue;
                }

                BodyKind3D bodyKind = bodies.GetRequiredColdStateByIndex(bodyIndex).BodyKind;
                if (bodyKind != BodyKind3D.Static &&
                    bodyKind != BodyKind3D.Kinematic &&
                    bodyKind != BodyKind3D.Dynamic) {
                    throw new InvalidOperationException("Occupied bodies must use a supported simulation mode before island construction.");
                }
            }
        }

        /// <summary>
        /// Validates every leading active pair and manifold, including pair uniqueness and required contact activity.
        /// </summary>
        /// <param name="bodies">Body pool addressed by active pairs.</param>
        /// <param name="pairs">Canonical active pairs to validate.</param>
        /// <param name="manifolds">Active manifolds parallel to pairs.</param>
        /// <param name="manifoldCount">Number of leading active entries.</param>
        static void ValidateActiveManifolds(
            HelPhysicsBodyPool3D bodies,
            HelPhysicsPairKey3D[] pairs,
            HelPhysicsContactManifold3D[] manifolds,
            int manifoldCount) {
            for (int manifoldIndex = 0; manifoldIndex < manifoldCount; manifoldIndex++) {
                HelPhysicsPairKey3D pair = pairs[manifoldIndex];
                if (pair.FirstBodyIndex < 0 || pair.SecondBodyIndex <= pair.FirstBodyIndex) {
                    throw new ArgumentOutOfRangeException(nameof(pairs), "Active island pairs must contain ascending distinct body indices.");
                }

                if (pair.SecondBodyIndex >= bodies.Capacity) {
                    throw new ArgumentOutOfRangeException(nameof(pairs), "Active island pairs must address body indices within pool capacity.");
                }

                if (!bodies.IsOccupied(pair.FirstBodyIndex) || !bodies.IsOccupied(pair.SecondBodyIndex)) {
                    throw new InvalidOperationException("Active island pairs must address two occupied body slots.");
                }

                for (int previousIndex = 0; previousIndex < manifoldIndex; previousIndex++) {
                    if (pairs[previousIndex] == pair) {
                        throw new InvalidOperationException("Each active body pair may contribute only one island manifold.");
                    }
                }

                int contactCount = manifolds[manifoldIndex].ContactCount;
                if (contactCount <= 0 || contactCount > 4) {
                    throw new InvalidOperationException("Active island manifolds must contain between one and four contacts.");
                }
            }
        }

        /// <summary>
        /// Initializes union-find and staging scratch from occupied dynamic body slots after complete input validation.
        /// </summary>
        /// <param name="bodies">Validated fixed body pool.</param>
        void InitializeUnionFind(HelPhysicsBodyPool3D bodies) {
            for (int bodyIndex = 0; bodyIndex < Parents.Length; bodyIndex++) {
                Parents[bodyIndex] = -1;
                Ranks[bodyIndex] = 0;
                RootToIslandIndices[bodyIndex] = -1;
                StagingBodyToIslandIndices[bodyIndex] = -1;
                if (bodies.IsOccupied(bodyIndex) &&
                    bodies.GetRequiredColdStateByIndex(bodyIndex).BodyKind == BodyKind3D.Dynamic) {
                    Parents[bodyIndex] = bodyIndex;
                }
            }

            for (int islandIndex = 0; islandIndex < StagingIslandMemberCounts.Length; islandIndex++) {
                StagingIslandMemberCounts[islandIndex] = 0;
                StagingIslandWriteIndices[islandIndex] = 0;
            }
        }

        /// <summary>
        /// Groups ascending dynamic slots by union root and assigns island indices in ascending minimum-member order.
        /// </summary>
        /// <param name="bodies">Validated body pool whose occupied dynamics are grouped.</param>
        /// <returns>Number of deterministic staging islands.</returns>
        /// <exception cref="HelPhysicsCapacityExceededException">Thrown when disconnected groups exceed fixed island storage.</exception>
        int StageIslandRanges(HelPhysicsBodyPool3D bodies) {
            int stagingIslandCount = 0;
            for (int bodyIndex = 0; bodyIndex < bodies.Capacity; bodyIndex++) {
                if (Parents[bodyIndex] < 0) {
                    continue;
                }

                int rootBodyIndex = FindRoot(bodyIndex);
                int islandIndex = RootToIslandIndices[rootBodyIndex];
                if (islandIndex < 0) {
                    if (stagingIslandCount == StagingIslands.Length) {
                        throw new HelPhysicsCapacityExceededException("island", StagingIslands.Length);
                    }

                    islandIndex = stagingIslandCount++;
                    RootToIslandIndices[rootBodyIndex] = islandIndex;
                }

                StagingIslandMemberCounts[islandIndex]++;
            }

            int bodyStartIndex = 0;
            for (int islandIndex = 0; islandIndex < stagingIslandCount; islandIndex++) {
                int bodyCount = StagingIslandMemberCounts[islandIndex];
                StagingIslands[islandIndex] = new HelPhysicsIsland3D(bodyStartIndex, bodyCount);
                StagingIslandWriteIndices[islandIndex] = bodyStartIndex;
                bodyStartIndex += bodyCount;
            }

            return stagingIslandCount;
        }

        /// <summary>
        /// Fills each staged island range by ascending body index and builds the inverse body-to-island lookup.
        /// </summary>
        /// <param name="bodies">Validated body pool whose dynamic members are written.</param>
        /// <returns>Total number of staged dynamic members.</returns>
        int StageIslandMembers(HelPhysicsBodyPool3D bodies) {
            int stagingBodyCount = 0;
            for (int bodyIndex = 0; bodyIndex < bodies.Capacity; bodyIndex++) {
                if (Parents[bodyIndex] < 0) {
                    continue;
                }

                int rootBodyIndex = FindRoot(bodyIndex);
                int islandIndex = RootToIslandIndices[rootBodyIndex];
                int destinationIndex = StagingIslandWriteIndices[islandIndex]++;
                StagingBodyIndices[destinationIndex] = bodyIndex;
                StagingBodyHandles[destinationIndex] = bodies.GetRequiredHandleByIndex(bodyIndex);
                StagingBodyToIslandIndices[bodyIndex] = islandIndex;
                stagingBodyCount++;
            }

            return stagingBodyCount;
        }

        /// <summary>
        /// Swaps complete staging arrays into active use only after every range, member, and lookup entry succeeded.
        /// </summary>
        /// <param name="stagingIslandCount">Number of valid leading staging ranges.</param>
        /// <param name="stagingBodyCount">Number of valid leading staging members.</param>
        void Publish(int stagingIslandCount, int stagingBodyCount) {
            HelPhysicsIsland3D[] previousIslands = Islands;
            Islands = StagingIslands;
            StagingIslands = previousIslands;

            int[] previousBodyIndices = BodyIndices;
            BodyIndices = StagingBodyIndices;
            StagingBodyIndices = previousBodyIndices;

            HelPhysicsBodyHandle3D[] previousBodyHandles = BodyHandles;
            BodyHandles = StagingBodyHandles;
            StagingBodyHandles = previousBodyHandles;

            int[] previousBodyToIslandIndices = BodyToIslandIndices;
            BodyToIslandIndices = StagingBodyToIslandIndices;
            StagingBodyToIslandIndices = previousBodyToIslandIndices;

            IslandCountValue = stagingIslandCount;
            BodyCountValue = stagingBodyCount;
        }

        /// <summary>
        /// Merges two dynamic union-find sets using deterministic rank updates.
        /// </summary>
        /// <param name="firstBodyIndex">First occupied dynamic body index.</param>
        /// <param name="secondBodyIndex">Second occupied dynamic body index.</param>
        void Union(int firstBodyIndex, int secondBodyIndex) {
            int firstRoot = FindRoot(firstBodyIndex);
            int secondRoot = FindRoot(secondBodyIndex);
            if (firstRoot == secondRoot) {
                return;
            }

            if (Ranks[firstRoot] < Ranks[secondRoot]) {
                Parents[firstRoot] = secondRoot;
            } else if (Ranks[firstRoot] > Ranks[secondRoot]) {
                Parents[secondRoot] = firstRoot;
            } else if (firstRoot < secondRoot) {
                Parents[secondRoot] = firstRoot;
                Ranks[firstRoot]++;
            } else {
                Parents[firstRoot] = secondRoot;
                Ranks[secondRoot]++;
            }
        }

        /// <summary>
        /// Finds one dynamic body's union root and compresses its traversed parent path in place.
        /// </summary>
        /// <param name="bodyIndex">Dynamic body index whose root is required.</param>
        /// <returns>Root body index for the current connected dynamic set.</returns>
        int FindRoot(int bodyIndex) {
            int rootBodyIndex = bodyIndex;
            while (Parents[rootBodyIndex] != rootBodyIndex) {
                rootBodyIndex = Parents[rootBodyIndex];
            }

            while (Parents[bodyIndex] != bodyIndex) {
                int parentBodyIndex = Parents[bodyIndex];
                Parents[bodyIndex] = rootBodyIndex;
                bodyIndex = parentBodyIndex;
            }

            return rootBodyIndex;
        }
    }
}
