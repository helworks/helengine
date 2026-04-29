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
        /// Gets or sets the render target that receives this camera's output; null renders to the main back buffer.
        /// </summary>
        RenderTarget RenderTarget { get; set; }

        /// <summary>
        /// Gets or sets the clear settings applied before this camera renders.
        /// </summary>
        CameraClearSettings ClearSettings { get; set; }

        /// <summary>
        /// Gets the 2D render queue registered for this camera.
        /// </summary>
        IRenderQueue2D RenderQueue2D { get; }

        /// <summary>
        /// Gets the 3D render queue registered for this camera.
        /// </summary>
        IRenderQueue3D RenderQueue3D { get; }
    }
}
