namespace helengine {
    /// <summary>
    /// Stores the dense runtime state tracked for one simulated 3D character controller.
    /// </summary>
    public sealed class CharacterControllerState3D {
        /// <summary>
        /// Initializes a new runtime character-controller state for one entity-backed controller.
        /// </summary>
        /// <param name="entity">Entity whose transform is synchronized by the controller state.</param>
        /// <param name="controller">Authored character-controller component that owns the locomotion settings.</param>
        /// <param name="boxCollider">Authored box collider that defines the controller bounds.</param>
        public CharacterControllerState3D(Entity entity, CharacterController3DComponent controller, BoxCollider3DComponent boxCollider) {
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            Controller = controller ?? throw new ArgumentNullException(nameof(controller));
            BoxCollider = boxCollider ?? throw new ArgumentNullException(nameof(boxCollider));
            Position = entity.LocalPosition;
            Orientation = entity.LocalOrientation;
            HalfExtents = CreateHalfExtents(boxCollider.Size, entity.LocalScale);
            VerticalVelocity = 0f;
        }

        /// <summary>
        /// Gets the entity synchronized by the runtime controller state.
        /// </summary>
        public Entity Entity { get; }

        /// <summary>
        /// Gets the authored controller component consumed by the runtime controller state.
        /// </summary>
        public CharacterController3DComponent Controller { get; }

        /// <summary>
        /// Gets the authored box collider consumed by the runtime controller state.
        /// </summary>
        public BoxCollider3DComponent BoxCollider { get; }

        /// <summary>
        /// Gets or sets the current runtime position.
        /// </summary>
        public float3 Position { get; set; }

        /// <summary>
        /// Gets or sets the current runtime orientation.
        /// </summary>
        public float4 Orientation { get; set; }

        /// <summary>
        /// Gets or sets the current axis-aligned half extents used by the controller.
        /// </summary>
        public float3 HalfExtents { get; set; }

        /// <summary>
        /// Gets or sets the current runtime vertical velocity in world units per second.
        /// </summary>
        public float VerticalVelocity { get; set; }

        /// <summary>
        /// Rebuilds the runtime state from the current authored entity and component values.
        /// </summary>
        public void SynchronizeFromEntity() {
            Position = Entity.LocalPosition;
            Orientation = Entity.LocalOrientation;
            HalfExtents = CreateHalfExtents(BoxCollider.Size, Entity.LocalScale);
        }

        /// <summary>
        /// Pushes the current runtime state back into the authored entity.
        /// </summary>
        public void SynchronizeToEntity() {
            Entity.LocalPosition = Position;
            Entity.LocalOrientation = Orientation;
        }

        /// <summary>
        /// Builds half extents from one authored box size and entity scale.
        /// </summary>
        /// <param name="size">Full authored box size.</param>
        /// <param name="scale">Current entity scale.</param>
        /// <returns>Axis-aligned half extents used by the controller.</returns>
        static float3 CreateHalfExtents(float3 size, float3 scale) {
            return new float3(
                Math.Abs(size.X * scale.X) * 0.5f,
                Math.Abs(size.Y * scale.Y) * 0.5f,
                Math.Abs(size.Z * scale.Z) * 0.5f);
        }
    }
}
