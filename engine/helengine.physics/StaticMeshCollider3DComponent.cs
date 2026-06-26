namespace helengine {
    /// <summary>
    /// Defines one authored cooked static mesh collider consumed by 3D physics runtimes.
    /// </summary>
    public sealed class StaticMeshCollider3DComponent : Collider3DComponent {
        /// <summary>
        /// Backing field for the cooked collision data blob.
        /// </summary>
        StaticMeshCollisionData3D CollisionDataValue;

        /// <summary>
        /// Backing field for the optional cooked runtime payload.
        /// </summary>
        StaticMeshCollisionRuntimeData3D CookedRuntimeDataValue;

        /// <summary>
        /// Gets or sets the cooked static collision data queried by the runtime.
        /// </summary>
        public StaticMeshCollisionData3D CollisionData {
            get { return CollisionDataValue; }
            set { CollisionDataValue = value ?? throw new ArgumentNullException(nameof(value)); }
        }

        /// <summary>
        /// Gets or sets the optional cooked runtime payload generated for the active physics backend.
        /// </summary>
        public StaticMeshCollisionRuntimeData3D CookedRuntimeData {
            get { return CookedRuntimeDataValue; }
            set { CookedRuntimeDataValue = value; }
        }
    }
}
