namespace helengine {
    /// <summary>
    /// Generates deterministic collision candidates by insertion-sorting persistent X-axis endpoints and sweeping a fixed active set.
    /// </summary>
    public sealed class HelPhysicsSweepAndPrune3D {
        /// <summary>
        /// Stores the minimum number of proxy and candidate slots accepted by this broadphase.
        /// </summary>
        const int MinimumCapacity = 1;

        /// <summary>
        /// Stores body metadata and bounds in permanently allocated proxy slots.
        /// </summary>
        readonly HelPhysicsBroadphaseProxy3D[] Proxies;

        /// <summary>
        /// Stores two persistent X-axis endpoints for every occupied proxy slot.
        /// </summary>
        readonly HelPhysicsSweepEndpoint3D[] Endpoints;

        /// <summary>
        /// Stores body indices whose X intervals currently overlap the sweep position.
        /// </summary>
        readonly int[] ActiveBodyIndices;

        /// <summary>
        /// Stores the maximum number of candidates this broadphase may emit in one build.
        /// </summary>
        readonly int CandidateCapacity;

        /// <summary>
        /// Stores the number of proxy slots currently occupied by body metadata.
        /// </summary>
        int ProxyCount;

        /// <summary>
        /// Stores the number of active entries in the persistent endpoint array.
        /// </summary>
        int EndpointCount;

        /// <summary>
        /// Stores the number of body indices presently in the fixed active sweep set.
        /// </summary>
        int ActiveBodyCount;

        /// <summary>
        /// Initializes fixed proxy, endpoint, active-set, and candidate-bound storage for all future broadphase builds.
        /// </summary>
        /// <param name="proxyCapacity">Maximum number of body proxies that may be registered simultaneously.</param>
        /// <param name="candidateCapacity">Maximum candidates that one build may emit before diagnosing exhaustion.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when either capacity is less than one or endpoint storage would overflow.</exception>
        public HelPhysicsSweepAndPrune3D(int proxyCapacity, int candidateCapacity) {
            ValidateConstructorCapacities(proxyCapacity, candidateCapacity);

            Proxies = new HelPhysicsBroadphaseProxy3D[proxyCapacity];
            Endpoints = new HelPhysicsSweepEndpoint3D[proxyCapacity * 2];
            ActiveBodyIndices = new int[proxyCapacity];
            CandidateCapacity = candidateCapacity;
        }

        /// <summary>
        /// Adds or updates one body's persistent broadphase proxy and its two X-axis endpoints.
        /// </summary>
        /// <param name="bodyIndex">Stable non-negative index of the body represented by the proxy.</param>
        /// <param name="bodyKind">Simulation participation mode for the represented body.</param>
        /// <param name="isActive">Awake state for dynamics or moved state for kinematics.</param>
        /// <param name="collisionLayer">Layer emitted by this proxy for other collision masks.</param>
        /// <param name="collisionMask">Layers this proxy permits for candidate generation.</param>
        /// <param name="aabb">Current inclusive world-space bounds for this body.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the body index or body mode is invalid.</exception>
        /// <exception cref="HelPhysicsCapacityExceededException">Thrown when no fixed proxy slot remains for a new body.</exception>
        public void UpdateProxy(int bodyIndex, BodyKind3D bodyKind, bool isActive, ushort collisionLayer, ushort collisionMask, HelPhysicsAabb3D aabb) {
            ValidateBodyIndex(bodyIndex);
            ValidateBodyKind(bodyKind);

            int proxyIndex = FindProxyIndex(bodyIndex);
            if (proxyIndex >= 0) {
                Proxies[proxyIndex].BodyKind = bodyKind;
                Proxies[proxyIndex].IsActive = isActive;
                Proxies[proxyIndex].CollisionLayer = collisionLayer;
                Proxies[proxyIndex].CollisionMask = collisionMask;
                Proxies[proxyIndex].Aabb = aabb;
                UpdateEndpoints(bodyIndex, aabb);
                return;
            }

            if (ProxyCount == Proxies.Length) {
                throw new HelPhysicsCapacityExceededException("broadphase proxy", Proxies.Length);
            }

            int freeProxyIndex = FindFreeProxyIndex();
            Proxies[freeProxyIndex].IsOccupied = true;
            Proxies[freeProxyIndex].BodyIndex = bodyIndex;
            Proxies[freeProxyIndex].BodyKind = bodyKind;
            Proxies[freeProxyIndex].IsActive = isActive;
            Proxies[freeProxyIndex].CollisionLayer = collisionLayer;
            Proxies[freeProxyIndex].CollisionMask = collisionMask;
            Proxies[freeProxyIndex].Aabb = aabb;
            AddEndpoints(bodyIndex, aabb);
            ProxyCount++;
        }

        /// <summary>
        /// Removes one body's proxy and both endpoints so it cannot participate in subsequent candidate builds.
        /// </summary>
        /// <param name="bodyIndex">Stable non-negative index of the body to remove.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="bodyIndex"/> is negative.</exception>
        public void RemoveProxy(int bodyIndex) {
            ValidateBodyIndex(bodyIndex);

            int proxyIndex = FindProxyIndex(bodyIndex);
            if (proxyIndex < 0) {
                return;
            }

            RemoveEndpoints(bodyIndex);
            Proxies[proxyIndex].IsOccupied = false;
            ProxyCount--;
        }

        /// <summary>
        /// Sorts persistent endpoints and writes all eligible overlapping body pairs into the caller-provided fixed destination.
        /// </summary>
        /// <param name="destination">Fixed candidate storage whose length is independently enforced as a hard capacity bound.</param>
        /// <returns>The number of candidate pairs written to <paramref name="destination"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="destination"/> is null.</exception>
        /// <exception cref="HelPhysicsCapacityExceededException">Thrown when configured or destination candidate capacity is exhausted.</exception>
        public int BuildCandidatePairs(HelPhysicsCandidatePair3D[] destination) {
            if (destination == null) {
                throw new ArgumentNullException(nameof(destination));
            }

            SortEndpoints();
            ActiveBodyCount = 0;
            int candidateCount = 0;

            for (int endpointIndex = 0; endpointIndex < EndpointCount; endpointIndex++) {
                HelPhysicsSweepEndpoint3D endpoint = Endpoints[endpointIndex];
                if (endpoint.IsMinimum) {
                    AppendPairsForMinimumEndpoint(endpoint.BodyIndex, destination, ref candidateCount);
                    ActiveBodyIndices[ActiveBodyCount++] = endpoint.BodyIndex;
                } else {
                    RemoveActiveBodyIndex(endpoint.BodyIndex);
                }
            }

            return candidateCount;
        }

        /// <summary>
        /// Validates that constructor capacities can allocate all fixed storage without overflow.
        /// </summary>
        /// <param name="proxyCapacity">Requested simultaneous proxy count.</param>
        /// <param name="candidateCapacity">Requested per-build candidate count.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when either capacity is invalid.</exception>
        static void ValidateConstructorCapacities(int proxyCapacity, int candidateCapacity) {
            if (proxyCapacity < MinimumCapacity || proxyCapacity > int.MaxValue / 2) {
                throw new ArgumentOutOfRangeException(nameof(proxyCapacity), "Broadphase proxy capacity must be positive and leave room for two endpoints per proxy.");
            }

            if (candidateCapacity < MinimumCapacity) {
                throw new ArgumentOutOfRangeException(nameof(candidateCapacity), "Broadphase candidate capacity must be positive.");
            }
        }

        /// <summary>
        /// Validates that a body index can identify a broadphase proxy.
        /// </summary>
        /// <param name="bodyIndex">Body index to validate.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="bodyIndex"/> is negative.</exception>
        static void ValidateBodyIndex(int bodyIndex) {
            if (bodyIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(bodyIndex), "Broadphase body indices cannot be negative.");
            }
        }

        /// <summary>
        /// Validates that a body mode is one of the supported simulation participation modes.
        /// </summary>
        /// <param name="bodyKind">Body mode to validate.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="bodyKind"/> is not supported.</exception>
        static void ValidateBodyKind(BodyKind3D bodyKind) {
            if (bodyKind != BodyKind3D.Static && bodyKind != BodyKind3D.Kinematic && bodyKind != BodyKind3D.Dynamic) {
                throw new ArgumentOutOfRangeException(nameof(bodyKind), "Broadphase proxies require a supported body mode.");
            }
        }

        /// <summary>
        /// Finds the occupied proxy slot for one body index.
        /// </summary>
        /// <param name="bodyIndex">Body index whose proxy slot is required.</param>
        /// <returns>The occupied proxy slot, or negative one when the body has no proxy.</returns>
        int FindProxyIndex(int bodyIndex) {
            for (int proxyIndex = 0; proxyIndex < Proxies.Length; proxyIndex++) {
                if (Proxies[proxyIndex].IsOccupied && Proxies[proxyIndex].BodyIndex == bodyIndex) {
                    return proxyIndex;
                }
            }

            return -1;
        }

        /// <summary>
        /// Finds one unoccupied fixed proxy slot for a new body.
        /// </summary>
        /// <returns>The first free proxy slot in deterministic storage order.</returns>
        /// <exception cref="InvalidOperationException">Thrown when proxy occupancy contradicts the tracked fixed capacity.</exception>
        int FindFreeProxyIndex() {
            for (int proxyIndex = 0; proxyIndex < Proxies.Length; proxyIndex++) {
                if (!Proxies[proxyIndex].IsOccupied) {
                    return proxyIndex;
                }
            }

            throw new InvalidOperationException("Broadphase proxy occupancy does not contain the expected free slot.");
        }

        /// <summary>
        /// Appends both persistent X-axis endpoints for a newly registered proxy.
        /// </summary>
        /// <param name="bodyIndex">Body index that owns the new endpoints.</param>
        /// <param name="aabb">Inclusive bounds supplying minimum and maximum X coordinates.</param>
        void AddEndpoints(int bodyIndex, HelPhysicsAabb3D aabb) {
            Endpoints[EndpointCount] = new HelPhysicsSweepEndpoint3D {
                Value = aabb.Minimum.X,
                BodyIndex = bodyIndex,
                IsMinimum = true
            };
            EndpointCount++;
            Endpoints[EndpointCount] = new HelPhysicsSweepEndpoint3D {
                Value = aabb.Maximum.X,
                BodyIndex = bodyIndex,
                IsMinimum = false
            };
            EndpointCount++;
        }

        /// <summary>
        /// Updates the two persistent endpoint values that belong to an existing proxy.
        /// </summary>
        /// <param name="bodyIndex">Body index whose endpoints should receive new values.</param>
        /// <param name="aabb">Inclusive bounds supplying minimum and maximum X coordinates.</param>
        void UpdateEndpoints(int bodyIndex, HelPhysicsAabb3D aabb) {
            for (int endpointIndex = 0; endpointIndex < EndpointCount; endpointIndex++) {
                if (Endpoints[endpointIndex].BodyIndex == bodyIndex) {
                    Endpoints[endpointIndex].Value = Endpoints[endpointIndex].IsMinimum ? aabb.Minimum.X : aabb.Maximum.X;
                }
            }
        }

        /// <summary>
        /// Removes both endpoints belonging to a deleted proxy while preserving the sorted prefix's reusable storage.
        /// </summary>
        /// <param name="bodyIndex">Body index whose endpoint entries should be removed.</param>
        void RemoveEndpoints(int bodyIndex) {
            int endpointIndex = 0;
            while (endpointIndex < EndpointCount) {
                if (Endpoints[endpointIndex].BodyIndex != bodyIndex) {
                    endpointIndex++;
                    continue;
                }

                for (int shiftedEndpointIndex = endpointIndex + 1; shiftedEndpointIndex < EndpointCount; shiftedEndpointIndex++) {
                    Endpoints[shiftedEndpointIndex - 1] = Endpoints[shiftedEndpointIndex];
                }

                EndpointCount--;
            }
        }

        /// <summary>
        /// Insertion-sorts endpoints using value, minimum-before-maximum, and body-index tie order for coherent deterministic frames.
        /// </summary>
        void SortEndpoints() {
            for (int endpointIndex = 1; endpointIndex < EndpointCount; endpointIndex++) {
                HelPhysicsSweepEndpoint3D endpoint = Endpoints[endpointIndex];
                int insertionIndex = endpointIndex - 1;

                while (insertionIndex >= 0 && IsEndpointBefore(endpoint, Endpoints[insertionIndex])) {
                    Endpoints[insertionIndex + 1] = Endpoints[insertionIndex];
                    insertionIndex--;
                }

                Endpoints[insertionIndex + 1] = endpoint;
            }
        }

        /// <summary>
        /// Determines whether one endpoint precedes another in the deterministic sweep order.
        /// </summary>
        /// <param name="left">Endpoint that may precede <paramref name="right"/>.</param>
        /// <param name="right">Endpoint that may follow <paramref name="left"/>.</param>
        /// <returns>True when <paramref name="left"/> belongs earlier in the sorted endpoint sequence.</returns>
        static bool IsEndpointBefore(HelPhysicsSweepEndpoint3D left, HelPhysicsSweepEndpoint3D right) {
            if (left.Value != right.Value) {
                return left.Value < right.Value;
            }

            if (left.IsMinimum != right.IsMinimum) {
                return left.IsMinimum;
            }

            return left.BodyIndex < right.BodyIndex;
        }

        /// <summary>
        /// Evaluates the current active set against a newly encountered interval minimum and appends every eligible candidate.
        /// </summary>
        /// <param name="bodyIndex">Body whose minimum endpoint was reached by the sweep.</param>
        /// <param name="destination">Fixed candidate destination to receive eligible pairs.</param>
        /// <param name="candidateCount">Current number of pairs already written to <paramref name="destination"/>.</param>
        void AppendPairsForMinimumEndpoint(int bodyIndex, HelPhysicsCandidatePair3D[] destination, ref int candidateCount) {
            int proxyIndex = FindProxyIndex(bodyIndex);
            if (proxyIndex < 0) {
                throw new InvalidOperationException("A sweep endpoint does not reference an occupied broadphase proxy.");
            }

            HelPhysicsBroadphaseProxy3D proxy = Proxies[proxyIndex];
            for (int activeIndex = 0; activeIndex < ActiveBodyCount; activeIndex++) {
                int otherBodyIndex = ActiveBodyIndices[activeIndex];
                int otherProxyIndex = FindProxyIndex(otherBodyIndex);
                if (otherProxyIndex < 0) {
                    throw new InvalidOperationException("The active sweep set does not reference an occupied broadphase proxy.");
                }

                HelPhysicsBroadphaseProxy3D otherProxy = Proxies[otherProxyIndex];
                if (!CanEmitCandidate(proxy, otherProxy)) {
                    continue;
                }

                EnsureCandidateCapacity(destination, candidateCount);
                if (bodyIndex < otherBodyIndex) {
                    destination[candidateCount] = new HelPhysicsCandidatePair3D(bodyIndex, otherBodyIndex);
                } else {
                    destination[candidateCount] = new HelPhysicsCandidatePair3D(otherBodyIndex, bodyIndex);
                }

                candidateCount++;
            }
        }

        /// <summary>
        /// Determines whether two X-overlapping proxies should enter narrow phase after full bounds, filter, mode, and activity checks.
        /// </summary>
        /// <param name="first">First active-set proxy to inspect.</param>
        /// <param name="second">Second proxy whose minimum endpoint is currently processed.</param>
        /// <returns>True when the pair is an eligible narrow-phase candidate.</returns>
        static bool CanEmitCandidate(HelPhysicsBroadphaseProxy3D first, HelPhysicsBroadphaseProxy3D second) {
            if (!first.Aabb.Overlaps(second.Aabb)) {
                return false;
            }

            if (!AreCollisionFiltersCompatible(first, second)) {
                return false;
            }

            if (first.BodyKind == BodyKind3D.Static && second.BodyKind == BodyKind3D.Static) {
                return false;
            }

            return IsPairActive(first) || IsPairActive(second);
        }

        /// <summary>
        /// Determines whether both proxies' layer and mask settings permit their interaction.
        /// </summary>
        /// <param name="first">First proxy whose emitted layer is checked against the second mask.</param>
        /// <param name="second">Second proxy whose emitted layer is checked against the first mask.</param>
        /// <returns>True when both collision-filter directions permit interaction.</returns>
        static bool AreCollisionFiltersCompatible(HelPhysicsBroadphaseProxy3D first, HelPhysicsBroadphaseProxy3D second) {
            return (first.CollisionLayer & second.CollisionMask) != 0
                && (second.CollisionLayer & first.CollisionMask) != 0;
        }

        /// <summary>
        /// Determines whether a proxy independently makes one otherwise valid pair eligible for narrow phase.
        /// </summary>
        /// <param name="proxy">Proxy whose body mode and activity flag should be interpreted.</param>
        /// <returns>True for awake dynamics and moved kinematics, but never for statics.</returns>
        static bool IsPairActive(HelPhysicsBroadphaseProxy3D proxy) {
            if (proxy.BodyKind == BodyKind3D.Dynamic) {
                return proxy.IsActive;
            }

            if (proxy.BodyKind == BodyKind3D.Kinematic) {
                return proxy.IsActive;
            }

            return false;
        }

        /// <summary>
        /// Throws the exact candidate-pair capacity diagnostic before a pair can be silently truncated.
        /// </summary>
        /// <param name="destination">Caller-provided fixed pair storage.</param>
        /// <param name="candidateCount">Number of pairs already written during this build.</param>
        /// <exception cref="HelPhysicsCapacityExceededException">Thrown when either fixed candidate capacity has no remaining slot.</exception>
        void EnsureCandidateCapacity(HelPhysicsCandidatePair3D[] destination, int candidateCount) {
            if (candidateCount == CandidateCapacity) {
                throw new HelPhysicsCapacityExceededException("candidate pair", CandidateCapacity);
            }

            if (candidateCount == destination.Length) {
                throw new HelPhysicsCapacityExceededException("candidate pair", destination.Length);
            }
        }

        /// <summary>
        /// Removes one completed interval from the fixed active set while preserving deterministic remaining order.
        /// </summary>
        /// <param name="bodyIndex">Body index whose maximum endpoint was reached by the sweep.</param>
        /// <exception cref="InvalidOperationException">Thrown when endpoint ordering does not correspond to an active interval.</exception>
        void RemoveActiveBodyIndex(int bodyIndex) {
            for (int activeIndex = 0; activeIndex < ActiveBodyCount; activeIndex++) {
                if (ActiveBodyIndices[activeIndex] != bodyIndex) {
                    continue;
                }

                for (int shiftedActiveIndex = activeIndex + 1; shiftedActiveIndex < ActiveBodyCount; shiftedActiveIndex++) {
                    ActiveBodyIndices[shiftedActiveIndex - 1] = ActiveBodyIndices[shiftedActiveIndex];
                }

                ActiveBodyCount--;
                return;
            }

            throw new InvalidOperationException("A sweep maximum endpoint does not match an active interval.");
        }
    }
}
