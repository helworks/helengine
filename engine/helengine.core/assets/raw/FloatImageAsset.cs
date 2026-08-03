namespace helengine {
    /// <summary>
    /// Represents raw floating-point (HDR/linear) image data stored in memory, RGBA interleaved,
    /// row-major with the top row first.
    /// </summary>
    public class FloatImageAsset : Asset, IDisposable {
        bool IsDisposedValue;

        /// <summary>
        /// Raw color data for the image in RGBA float order.
        /// </summary>
        [NativeOwnedMember]
        public float[] Pixels;

        /// <summary>
        /// Width of the image in pixels.
        /// </summary>
        public ushort Width;

        /// <summary>
        /// Height of the image in pixels.
        /// </summary>
        public ushort Height;

        /// <summary>
        /// Releases the pixel buffer owned by this raw image asset.
        /// </summary>
        public void Dispose() {
            NativeOwnership.Release(ref Pixels);
            IsDisposedValue = true;
        }
    }
}
