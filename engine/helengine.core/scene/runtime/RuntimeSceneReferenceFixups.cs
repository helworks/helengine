namespace helengine {
    /// <summary>
    /// Temporarily collects authored entity references and resolves them against one scene load.
    /// </summary>
    public sealed class RuntimeSceneReferenceFixups : IDisposable {
        /// <summary>
        /// Stores only references requested by components in the active scene load.
        /// </summary>
        [NativeOwnedMember]
        List<Request> Requests;

        /// <summary>
        /// Adds one nonzero scene reference to this load's sparse binding requests.
        /// </summary>
        /// <param name="reference">Reference decoded from a component payload. This collector borrows it until <see cref="Bind(IReadOnlyList{Entity})"/> or <see cref="Clear"/>; ownership remains with the caller.</param>
        /// <param name="componentTypeId">Stable component type id that owns the reference.</param>
        public void Track([NativeRetainsBorrow] SceneEntityReference reference, string componentTypeId) {
            if (reference == null || reference.EntityId == 0u) {
                return;
            }

            if (Requests == null) {
                NativeOwnership.Release(ref Requests);
                Requests = new List<Request>();
            }

            Requests.Add(new Request(reference, componentTypeId, reference.EntityId));
        }

        /// <summary>
        /// Resolves every tracked reference by walking the supplied scene roots once.
        /// </summary>
        /// <param name="roots">Top-level entities in the scene being loaded.</param>
        public void Bind(IReadOnlyList<Entity> roots) {
            try {
                if (roots == null) {
                    throw new ArgumentNullException(nameof(roots));
                }

                if (Requests == null) {
                    return;
                }

                for (int rootIndex = 0; rootIndex < roots.Count; rootIndex++) {
                    BindEntity(roots[rootIndex]);
                }

                for (int requestIndex = 0; requestIndex < Requests.Count; requestIndex++) {
                    Request request = Requests[requestIndex];
                    if (!request.Matched) {
                        throw new InvalidOperationException($"Component '{request.ComponentTypeId}' references missing scene entity id {request.EntityId}.");
                    }
                }
            } finally {
                Clear();
            }
        }

        /// <summary>
        /// Releases all temporary reference requests collected for this load.
        /// </summary>
        public void Clear() {
            NativeOwnership.Release(ref Requests);
        }

        /// <summary>Releases any requests left by an incomplete scene load.</summary>
        public void Dispose() {
            NativeOwnership.Release(ref Requests);
        }

        void BindEntity(Entity entity) {
            if (entity == null) {
                return;
            }

            uint entityId = entity.SceneEntityRuntimeId;
            if (entityId != 0u) {
                for (int requestIndex = 0; requestIndex < Requests.Count; requestIndex++) {
                    Request request = Requests[requestIndex];
                    if (request.EntityId != entityId) {
                        continue;
                    }

                    if (request.Matched) {
                        throw new InvalidOperationException($"Component '{request.ComponentTypeId}' references ambiguous scene entity id {request.EntityId}; more than one entity has that id.");
                    }

                    request.Matched = true;
                    Requests[requestIndex] = request;
                    request.Reference.ResolvedEntity = entity;
                }
            }

            for (int childIndex = 0; childIndex < entity.Children.Count; childIndex++) {
                BindEntity(entity.Children[childIndex]);
            }
        }

        /// <summary>
        /// Holds one temporary entity-binding request while retaining only a borrow of its caller-owned reference.
        /// </summary>
        struct Request {
            /// <summary>
            /// Stores the caller-owned scene reference as a borrow until the enclosing fixups collection binds or clears its requests.
            /// </summary>
            public readonly SceneEntityReference Reference;

            public readonly string ComponentTypeId;
            public readonly uint EntityId;
            public bool Matched;

            /// <summary>
            /// Initializes one temporary binding request that borrows the supplied scene reference.
            /// </summary>
            /// <param name="reference">Caller-owned reference borrowed until the containing fixups collection binds or clears its requests.</param>
            /// <param name="componentTypeId">Stable component type id that produced the reference.</param>
            /// <param name="entityId">Serialized id used to locate the referenced entity.</param>
            public Request([NativeRetainsBorrow] SceneEntityReference reference, string componentTypeId, uint entityId) {
                Reference = reference;
                ComponentTypeId = componentTypeId;
                EntityId = entityId;
            }
        }
    }
}
