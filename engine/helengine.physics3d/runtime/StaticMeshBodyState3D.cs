namespace helengine {
    /// <summary>
    /// Stores the dense runtime state tracked for one static cooked mesh collider.
    /// </summary>
    public sealed class StaticMeshBodyState3D {
        /// <summary>
        /// Initializes a new runtime static-mesh state for one entity-backed rigid body.
        /// </summary>
        /// <param name="entity">Entity whose transform is synchronized by the mesh state.</param>
        /// <param name="rigidBody">Authored rigid body component that owns the body settings.</param>
        /// <param name="meshCollider">Authored static mesh collider component that owns the cooked collision data.</param>
        public StaticMeshBodyState3D(Entity entity, RigidBody3DComponent rigidBody, StaticMeshCollider3DComponent meshCollider) {
            Entity = entity ?? throw new ArgumentNullException(nameof(entity));
            RigidBody = rigidBody ?? throw new ArgumentNullException(nameof(rigidBody));
            MeshCollider = meshCollider ?? throw new ArgumentNullException(nameof(meshCollider));
            if (meshCollider.CollisionData == null) {
                throw new ArgumentNullException(nameof(meshCollider), "Static mesh collider requires cooked collision data.");
            }

            WorldVertices = new float3[meshCollider.CollisionData.Vertices.Length];
            SynchronizeFromEntity();
        }

        /// <summary>
        /// Gets the entity synchronized by the runtime mesh state.
        /// </summary>
        public Entity Entity { get; }

        /// <summary>
        /// Gets the authored rigid body component consumed by the runtime mesh state.
        /// </summary>
        public RigidBody3DComponent RigidBody { get; }

        /// <summary>
        /// Gets the authored static mesh collider component consumed by the runtime mesh state.
        /// </summary>
        public StaticMeshCollider3DComponent MeshCollider { get; }

        /// <summary>
        /// Gets the transformed world-space vertices for the cooked mesh blob.
        /// </summary>
        public float3[] WorldVertices { get; }

        /// <summary>
        /// Rebuilds the transformed triangle vertices from the current entity transform.
        /// </summary>
        public void SynchronizeFromEntity() {
            float3[] localVertices = MeshCollider.CollisionData.Vertices;
            for (int index = 0; index < localVertices.Length; index++) {
                WorldVertices[index] = TransformLocalVertex(localVertices[index], Entity.LocalScale, Entity.LocalOrientation, Entity.LocalPosition);
            }
        }

        /// <summary>
        /// Transforms one local-space collision vertex into world space.
        /// </summary>
        /// <param name="localVertex">Local-space vertex.</param>
        /// <param name="scale">Current entity scale.</param>
        /// <param name="orientation">Current entity orientation.</param>
        /// <param name="position">Current entity position.</param>
        /// <returns>Transformed world-space vertex.</returns>
        static float3 TransformLocalVertex(float3 localVertex, float3 scale, float4 orientation, float3 position) {
            float3 scaledVertex = new float3(
                localVertex.X * scale.X,
                localVertex.Y * scale.Y,
                localVertex.Z * scale.Z);
            float3 rotatedVertex = float4.RotateVector(scaledVertex, orientation);
            return rotatedVertex + position;
        }
    }
}
