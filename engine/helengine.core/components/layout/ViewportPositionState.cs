namespace helengine {
    /// <summary>Retains one original UI position without adding cache fields to Entity.</summary>
    internal struct ViewportPositionState {
        /// <summary>Borrowed transform participant.</summary>
        public Entity Entity;
        /// <summary>Original local position before the owner applied layout.</summary>
        public float3 LocalPosition;
        /// <summary>Captures the original UI transform.</summary>
        public ViewportPositionState(Entity entity) {
            Entity = entity;
            LocalPosition = entity.LocalPosition;
        }
        /// <summary>Restores the unscaled transform.</summary>
        public void Restore() { Entity.LocalPosition = LocalPosition; }
        /// <summary>Maps intentional editor changes back through the last preview factors.</summary>
        public void RestoreEdited(ViewportPositionState applied, ViewportLayoutMetrics metrics, float2 origin) {
            float3 live = Entity.LocalPosition;
            Entity.LocalPosition = new float3(
                AuthoredLayoutScaleUtils.UnscalePosition(live.X, applied.LocalPosition.X, LocalPosition.X, origin.X, metrics.ScaleX),
                AuthoredLayoutScaleUtils.UnscalePosition(live.Y, applied.LocalPosition.Y, LocalPosition.Y, origin.Y, metrics.ScaleY),
                live.Z == applied.LocalPosition.Z ? LocalPosition.Z : live.Z);
        }
    }
}
