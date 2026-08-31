namespace helengine {
    /// <summary>
    /// Associates one engine entity and its exact authored component pair with the generation-safe body identity reserved in a HelPhysics world.
    /// </summary>
    public sealed class HelPhysicsEntityBinding3D {
        /// <summary>
        /// Stores the world that owns the generation-safe body handle.
        /// </summary>
        readonly HelPhysicsWorld3D WorldValue;

        /// <summary>
        /// Initializes one valid association after its body reservation has succeeded.
        /// </summary>
        /// <param name="world">World that owns the reserved body.</param>
        /// <param name="entity">Engine entity represented by the body.</param>
        /// <param name="rigidBody">Authored rigid-body component synchronized by the binding.</param>
        /// <param name="boxCollider">Authored box-collider component whose identity must remain attached.</param>
        /// <param name="bodyHandle">Generation-safe world-owned body identity.</param>
        /// <param name="bindingId">Positive binder-local identity retained by the body description.</param>
        /// <param name="description">Validated immutable creation data translated from the entity.</param>
        /// <param name="lifecycle">Entity lifecycle observer that invalidates this binding during disposal.</param>
        /// <exception cref="ArgumentNullException">Thrown when a required reference is null.</exception>
        internal HelPhysicsEntityBinding3D(
            HelPhysicsWorld3D world,
            Entity entity,
            RigidBody3DComponent rigidBody,
            BoxCollider3DComponent boxCollider,
            HelPhysicsBodyHandle3D bodyHandle,
            int bindingId,
            [NativeTakesOwnership] HelPhysicsBodyDescription3D description,
            [NativeRetainsBorrow] HelPhysicsEntityBindingLifecycle3D lifecycle) {
            WorldValue = world ?? throw new ArgumentNullException(nameof(world));
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            RigidBody = rigidBody ?? throw new ArgumentNullException(nameof(rigidBody));
            BoxCollider = boxCollider ?? throw new ArgumentNullException(nameof(boxCollider));
            BodyHandle = bodyHandle;
            BindingId = bindingId;
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
            IsValid = true;
        }

        /// <summary>
        /// Gets the engine entity represented by this binding.
        /// </summary>
        public Entity Entity { get; }

        /// <summary>
        /// Gets the authored rigid-body component synchronized by this binding.
        /// </summary>
        public RigidBody3DComponent RigidBody { get; }

        /// <summary>
        /// Gets the exact authored box collider whose continued attachment is required by this binding.
        /// </summary>
        public BoxCollider3DComponent BoxCollider { get; }

        /// <summary>
        /// Gets the generation-safe body identity issued by the owning world.
        /// </summary>
        public HelPhysicsBodyHandle3D BodyHandle { get; }

        /// <summary>
        /// Gets the positive identity retained in the body's engine-binding metadata.
        /// </summary>
        public int BindingId { get; }

        /// <summary>
        /// Gets the complete immutable creation data translated from the authored entity for inspection and diagnostics.
        /// </summary>
        public HelPhysicsBodyDescription3D Description { get; }

        /// <summary>
        /// Gets the entity lifecycle observer owned exclusively by this binding.
        /// </summary>
        internal HelPhysicsEntityBindingLifecycle3D Lifecycle { get; }

        /// <summary>
        /// Gets whether this association still owns its original body generation.
        /// </summary>
        public bool IsValid { get; private set; }

        /// <summary>
        /// Copies the current observable state of the generation-safe body represented by this binding.
        /// </summary>
        /// <returns>An immutable snapshot of current simulation and lifecycle state.</returns>
        /// <exception cref="InvalidOperationException">Thrown after the binding has been invalidated.</exception>
        public HelPhysicsBodySnapshot3D GetBodySnapshot() {
            if (!IsValid) {
                throw new InvalidOperationException("Invalidated HelPhysics entity bindings cannot access a body.");
            }

            return WorldValue.GetBodySnapshot(BodyHandle);
        }

        /// <summary>
        /// Permanently severs this association after its original body generation has been queued for removal.
        /// </summary>
        internal void Invalidate() {
            if (!IsValid) {
                throw new InvalidOperationException("HelPhysics entity bindings cannot be invalidated more than once.");
            }

            IsValid = false;
        }
    }
}
