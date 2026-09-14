using SharpDX;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// In-memory picking backend used to test shared picker coordination without a graphics device.
    /// </summary>
    public sealed class TestEditorPickingBackend : IEditorPickingBackend {
        /// <summary>
        /// Gets the number of render requests received by this backend.
        /// </summary>
        public int RenderCount { get; private set; }

        /// <summary>
        /// Gets the color returned by the next readback request.
        /// </summary>
        public byte4 ReadbackColor { get; set; }

        /// <summary>
        /// Gets whether readback requests should report a ready pixel.
        /// </summary>
        public bool IsReadbackReady { get; set; }

        /// <summary>
        /// Gets whether disposal has been requested.
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <summary>
        /// Records a shared picker render request.
        /// </summary>
        /// <param name="camera">Camera that would be rendered.</param>
        /// <param name="colors">Drawable color table supplied by the picker.</param>
        public void Render(CameraComponent camera, IReadOnlyDictionary<IDrawable3D, byte4> colors) {
            if (camera == null) {
                throw new ArgumentNullException(nameof(camera));
            }
            if (colors == null) {
                throw new ArgumentNullException(nameof(colors));
            }

            RenderCount++;
        }

        /// <summary>
        /// Returns the configured fake readback color when the test marks it ready.
        /// </summary>
        /// <param name="pixel">Requested target pixel.</param>
        /// <param name="color">Configured fake color when ready.</param>
        /// <returns>True when <see cref="IsReadbackReady"/> is true.</returns>
        public bool TryReadPixel(int2 pixel, out byte4 color) {
            color = ReadbackColor;
            return IsReadbackReady;
        }

        /// <summary>
        /// Marks this fake backend as disposed.
        /// </summary>
        public void Dispose() {
            IsDisposed = true;
        }
    }
}