namespace helengine.render.validation {
    /// <summary>
    /// Defines command-line backend selection for validation runs.
    /// </summary>
    public enum RenderBackendSelection {
        /// <summary>
        /// Run only DirectX 11 validation.
        /// </summary>
        DirectX11,
        /// <summary>
        /// Run only Vulkan validation.
        /// </summary>
        Vulkan,
        /// <summary>
        /// Run both DirectX 11 and Vulkan validations.
        /// </summary>
        Both
    }
}
