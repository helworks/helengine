namespace helengine {
    /// <summary>
    /// Applies solved transform state back to core entities using one explicit synchronization policy.
    /// </summary>
    public interface IPhysicsTransformSyncPolicy {
        /// <summary>
        /// Applies one solved world transform to the supplied entity.
        /// </summary>
        /// <param name="entity">Entity receiving the solved transform.</param>
        /// <param name="position">Solved world position.</param>
        /// <param name="orientation">Solved world orientation.</param>
        void ApplyTransform(Entity entity, float3 position, float4 orientation);
    }
}
