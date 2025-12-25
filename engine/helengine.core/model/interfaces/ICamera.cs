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
        /// Gets or sets the draw order for the camera.
        /// </summary>
        byte CameraDrawOrder { get; set; }

        /// <summary>
        /// Gets or sets the viewport rectangle for the camera.
        /// </summary>
        float4 Viewport { get; set; }

        /// <summary>
        /// Gets the 2D render buckets registered for this camera.
        /// </summary>
        RenderBucket2D[] RenderBuckets2D { get; }

        /// <summary>
        /// Gets the 3D render buckets registered for this camera.
        /// </summary>
        RenderBucket3D[][][] RenderBuckets3D { get; }
    }
}
