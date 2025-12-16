namespace helengine {
    /// <summary>
    /// Describes a camera used for rendering scenes.
    /// </summary>
    public interface ICamera {
        /// <summary>
        /// Gets the parent entity owning the camera.
        /// </summary>
        Entity Parent { get; }

        /// <summary>
        /// Gets or sets the layer mask that the camera renders.
        /// </summary>
        ushort LayerMask { get; set; }

        /// <summary>
        /// Gets or sets the draw order bucket for the camera.
        /// </summary>
        byte CameraDrawOrder { get; set; }
    }
}
