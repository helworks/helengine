namespace helengine {
    /// <summary>
    /// Provides the engine-wide authored light-direction convention shared by render extraction and backend execution.
    /// </summary>
    public static class LightDirectionUtility {
        /// <summary>
        /// Gets the authored local forward axis used by directional and spot lights before entity orientation is applied.
        /// </summary>
        public static float3 AuthoredForwardAxis => new float3(0f, 0f, -1f);

        /// <summary>
        /// Resolves the world-space forward direction produced by one entity orientation.
        /// </summary>
        /// <param name="entity">Entity whose orientation defines the authored forward direction.</param>
        /// <returns>World-space forward direction derived from the engine light convention.</returns>
        public static float3 GetEntityForwardDirection(Entity entity) {
            if (entity == null) {
                throw new ArgumentNullException(nameof(entity));
            }

            return float4.RotateVector(AuthoredForwardAxis, entity.Orientation);
        }

        /// <summary>
        /// Resolves the world-space direction used by one authored directional or spot light.
        /// </summary>
        /// <param name="lightComponent">Light whose owning entity orientation defines the world-space light direction.</param>
        /// <returns>World-space light direction derived from the owning entity forward axis.</returns>
        public static float3 GetLightDirection(LightComponent lightComponent) {
            if (lightComponent == null) {
                throw new ArgumentNullException(nameof(lightComponent));
            } else if (lightComponent.Parent == null) {
                throw new InvalidOperationException("Light directions require the light component to be attached to an entity.");
            }

            return GetEntityForwardDirection(lightComponent.Parent);
        }
    }
}
