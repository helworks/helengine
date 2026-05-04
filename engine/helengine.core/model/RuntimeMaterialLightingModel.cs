namespace helengine {
    /// <summary>
    /// Describes the lighting model expected by one runtime material.
    /// </summary>
    public enum RuntimeMaterialLightingModel {
        /// <summary>
        /// Uses no dynamic lighting contribution.
        /// </summary>
        Unlit,
        /// <summary>
        /// Uses one compact metallic-roughness PBR lighting model.
        /// </summary>
        MetalRoughPbr
    }
}
