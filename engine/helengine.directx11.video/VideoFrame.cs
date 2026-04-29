using SharpDX.Direct3D11;

namespace helengine.directx11.video {
    /// <summary>
    /// Represents a decoded frame backed by a Direct3D 11 texture.
    /// </summary>
    public sealed class VideoFrame : IDisposable {
        /// <summary>
        /// Stores the decoder that owns the native frame resources.
        /// </summary>
        readonly DirectX11VideoDecoder owner;

        /// <summary>
        /// Stores the native frame metadata required for release.
        /// </summary>
        FfmpegNativeVideoFrame nativeFrame;

        /// <summary>
        /// Stores the native texture handle for this frame.
        /// </summary>
        IntPtr textureHandle;

        /// <summary>
        /// Tracks whether the frame has been disposed.
        /// </summary>
        bool disposed;

        /// <summary>
        /// Initializes a new decoded frame instance.
        /// </summary>
        /// <param name="owner">Owning decoder instance.</param>
        /// <param name="nativeFrame">Native frame metadata.</param>
        internal VideoFrame(DirectX11VideoDecoder owner, FfmpegNativeVideoFrame nativeFrame) {
            if (owner == null) {
                throw new ArgumentNullException(nameof(owner));
            }

            if (nativeFrame.Texture == IntPtr.Zero) {
                throw new InvalidOperationException("Native frame did not provide a texture handle.");
            }

            if (nativeFrame.Width <= 0 || nativeFrame.Height <= 0) {
                throw new InvalidOperationException("Native frame dimensions are invalid.");
            }

            if (nativeFrame.FrameFormat == VideoFrameFormat.Unknown) {
                throw new InvalidOperationException("Native frame format is not specified.");
            }

            if (nativeFrame.DurationTicks < 0) {
                throw new InvalidOperationException("Native frame duration is invalid.");
            }

            this.owner = owner;
            this.nativeFrame = nativeFrame;
            textureHandle = nativeFrame.Texture;
            SubresourceIndex = nativeFrame.SubresourceIndex;
            Width = nativeFrame.Width;
            Height = nativeFrame.Height;
            Format = nativeFrame.FrameFormat;
            Timestamp = TimeSpan.FromTicks(nativeFrame.TimestampTicks);
            Duration = TimeSpan.FromTicks(nativeFrame.DurationTicks);
        }

        /// <summary>
        /// Gets the native ID3D11Texture2D handle for this frame.
        /// </summary>
        public IntPtr TextureHandle => textureHandle;

        /// <summary>
        /// Gets the subresource index for planar texture access.
        /// </summary>
        public int SubresourceIndex { get; }

        /// <summary>
        /// Gets the frame width in pixels.
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// Gets the frame height in pixels.
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// Gets the pixel format for the decoded frame.
        /// </summary>
        public VideoFrameFormat Format { get; }

        /// <summary>
        /// Gets the presentation timestamp for this frame.
        /// </summary>
        public TimeSpan Timestamp { get; }

        /// <summary>
        /// Gets the duration represented by this frame.
        /// </summary>
        public TimeSpan Duration { get; }

        /// <summary>
        /// Creates a SharpDX texture wrapper for the native handle.
        /// </summary>
        /// <remarks>
        /// The returned wrapper must be disposed by the caller and does not own the underlying texture.
        /// </remarks>
        /// <returns>SharpDX texture wrapper for the native handle.</returns>
        public Texture2D CreateTextureReference() {
            if (disposed) {
                throw new ObjectDisposedException(nameof(VideoFrame));
            }

            if (textureHandle == IntPtr.Zero) {
                throw new InvalidOperationException("Frame texture handle is no longer valid.");
            }

            return new Texture2D(textureHandle);
        }

        /// <summary>
        /// Releases the native frame back to the decoder.
        /// </summary>
        public void Dispose() {
            if (disposed) {
                return;
            }

            owner.ReleaseFrame(ref nativeFrame);
            nativeFrame = new FfmpegNativeVideoFrame();
            textureHandle = IntPtr.Zero;
            disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
