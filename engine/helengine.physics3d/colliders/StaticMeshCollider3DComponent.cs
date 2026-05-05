namespace helengine {
    /// <summary>
    /// Defines one authored cooked static mesh collider consumed by the 3D physics runtime.
    /// </summary>
    public sealed class StaticMeshCollider3DComponent : Collider3DComponent {
        /// <summary>
        /// Backing field for the cooked collision data blob.
        /// </summary>
        StaticMeshCollisionData3D CollisionDataValue;

        /// <summary>
        /// Gets or sets the cooked static collision data queried by the runtime.
        /// </summary>
        public StaticMeshCollisionData3D CollisionData {
            get { return CollisionDataValue; }
            set { CollisionDataValue = value ?? throw new ArgumentNullException(nameof(value)); }
        }
    }
}
