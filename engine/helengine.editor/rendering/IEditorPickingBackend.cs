namespace helengine.editor {
    /// <summary>
    /// Executes one editor picking pass and exposes its pixel readback without exposing native graphics resources.
    /// </summary>
    public interface IEditorPickingBackend : IDisposable {
        /// <summary>
        /// Renders the supplied camera queue into the backend-owned picking target.
        /// </summary>
        /// <param name="camera">Camera whose queue and viewport should be rendered.</param>
        /// <param name="colors">Per-drawable colors encoded into the picking target.</param>
        void Render(CameraComponent camera, IReadOnlyDictionary<IDrawable3D, byte4> colors);

        /// <summary>
        /// Attempts to read one pixel from the most recently rendered picking target.
        /// </summary>
        /// <param name="pixel">Pixel coordinate in target space.</param>
        /// <param name="color">Color read from the target when readback is ready.</param>
        /// <returns>True when a color was read; otherwise false when readback is not ready.</returns>
        bool TryReadPixel(int2 pixel, out byte4 color);
    }
}
