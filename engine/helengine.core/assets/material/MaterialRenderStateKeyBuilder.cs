namespace helengine {
    /// <summary>
    /// Builds compact integer keys for material render states so backends can cache API-specific state objects.
    /// </summary>
    public static class MaterialRenderStateKeyBuilder {
        /// <summary>
        /// Builds a compact key that uniquely represents one material render-state configuration.
        /// </summary>
        /// <param name="renderState">Render-state values to encode.</param>
        /// <returns>Compact integer key for cache lookups.</returns>
        public static int Build(MaterialRenderState renderState) {
            if (renderState == null) {
                throw new ArgumentNullException(nameof(renderState));
            }

            int key = (int)renderState.BlendMode & 0xFF;
            key |= ((int)renderState.CullMode & 0xFF) << 8;
            if (renderState.DepthTestEnabled) {
                key |= 1 << 16;
            }

            if (renderState.DepthWriteEnabled) {
                key |= 1 << 17;
            }

            return key;
        }
    }
}
