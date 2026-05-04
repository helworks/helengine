namespace helengine.directx11 {
    /// <summary>
    /// Publishes the default DirectX11 renderer capability profile used by shared planning services.
    /// </summary>
    public static class DirectX11RenderCapabilityProfile {
        /// <summary>
        /// Creates the default DirectX11 renderer capability profile.
        /// </summary>
        /// <returns>Capability profile for the current DirectX11 backend.</returns>
        public static RendererBackendCapabilityProfile CreateDefault() {
            return new RendererBackendCapabilityProfile(
                true,
                false,
                true,
                true,
                32,
                4);
        }
    }
}
