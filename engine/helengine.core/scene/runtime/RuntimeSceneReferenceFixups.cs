namespace helengine {
    /// <summary>
    /// Temporarily collects authored entity references and resolves them against one scene load.
    /// </summary>
    public sealed class RuntimeSceneReferenceFixups {
        /// <summary>
        /// Stores only references requested by components in the active scene load.
        /// </summary>
        [NativeOwnedMember]
        List<Request> Requests;

        /// <summary>
        /// Adds one nonzero scene reference to this load's sparse binding requests.
        /// </summary>
        /// <param name="reference">Reference decoded from a component payload.</param>
        /// <param name="componentTypeId">Stable component type id that owns the reference.</param>
        public void Track(SceneEntityReference reference, string componentTypeId) {
            if (reference == null || reference.EntityId == 0u) {
                return;
            }

            if (Requests == null) {
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

        struct Request {
            public readonly SceneEntityReference Reference;
            public readonly string ComponentTypeId;
            public readonly uint EntityId;
            public bool Matched;

            public Request(SceneEntityReference reference, string componentTypeId, uint entityId) {
                Reference = reference;
                ComponentTypeId = componentTypeId;
                EntityId = entityId;
            }
        }
    }
}
