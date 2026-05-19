namespace helengine {
    /// <summary>
    /// Describes one viewport-owning component that can route a subtree to a specific camera.
    /// </summary>
    public interface ICameraBoundViewportOwner {
        /// <summary>
        /// Gets the binding mode currently used by the viewport owner.
        /// </summary>
        byte BindingMode { get; }

        /// <summary>
        /// Gets the resolved viewport rectangle in pixel-space coordinates.
        /// </summary>
        float4 ResolvedViewportBounds { get; }

        /// <summary>
        /// Gets the resolved viewport size in pixels.
        /// </summary>
        int2 ResolvedViewportSize { get; }

        /// <summary>
        /// Resolves the camera currently targeted by the viewport owner, or null when no camera is bound.
        /// </summary>
        /// <returns>Bound camera for rendering decisions, or null when no camera is currently targeted.</returns>
        CameraComponent GetBoundCameraComponent();
    }
}
