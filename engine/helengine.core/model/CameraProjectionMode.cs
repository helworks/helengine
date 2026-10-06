namespace helengine {
    /// <summary>
    /// Identifies how a camera maps view-space positions into clip space.
    /// </summary>
    public enum CameraProjectionMode {
        /// <summary>
        /// Uses a perspective projection controlled by the camera's field of view.
        /// </summary>
        Perspective = 0,

        /// <summary>
        /// Uses a parallel projection controlled by an orthographic vertical span.
        /// </summary>
        Orthographic = 1
    }
}
