namespace helengine {
    /// <summary>
    /// Optional projection settings implemented by cameras that can render orthographically.
    /// </summary>
    public interface ICameraProjectionSettings {
        /// <summary>
        /// Gets or sets the projection mode selected for this camera.
        /// </summary>
        CameraProjectionMode ProjectionMode { get; set; }

        /// <summary>
        /// Gets or sets the full vertical world-space extent used by orthographic projection.
        /// </summary>
        float OrthographicVerticalSpan { get; set; }
    }
}
