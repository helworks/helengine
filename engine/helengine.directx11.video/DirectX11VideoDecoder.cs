using SharpDX.Direct3D11;

namespace helengine.directx11.video {
    /// <summary>
    /// Decodes video frames using a DirectX 11 device and a native FFmpeg backend.
    /// </summary>
    /// <remarks>
    /// All frames must be disposed before disposing the decoder to avoid invalid native handles.
    /// </remarks>
    public sealed class DirectX11VideoDecoder : IDisposable {
        /// <summary>
        /// Stores the native decoder handle owned by this instance.
        /// </summary>
        IntPtr decoderHandle;

        /// <summary>
        /// Tracks whether the decoder has been disposed.
        /// </summary>
        bool disposed;

        /// <summary>
        /// Initializes a new DirectX 11 video decoder.
        /// </summary>
        /// <param name="options">Decoder configuration options.</param>
        public DirectX11VideoDecoder(DirectX11VideoDecoderOptions options) {
            if (options == null) {
                throw new ArgumentNullException(nameof(options));
            }

            Device = options.Device ?? throw new ArgumentNullException(nameof(options.Device));

            if (string.IsNullOrWhiteSpace(options.SourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(options.SourcePath));
            }

            if (!Enum.IsDefined(typeof(VideoDecoderHardwareMode), options.HardwareMode)) {
                throw new ArgumentOutOfRangeException(nameof(options.HardwareMode), "Hardware mode is not supported.");
            }

            FfmpegNativeVideoStreamInfo nativeInfo;
            try {
                decoderHandle = FfmpegNativeApi.he_video_decoder_create(Device.NativePointer, options.SourcePath, options.HardwareMode, out nativeInfo);
            } catch (DllNotFoundException ex) {
                throw new InvalidOperationException("Native decoder library was not found. Ensure helengine.video.ffmpeg is available.", ex);
            } catch (EntryPointNotFoundException ex) {
                throw new InvalidOperationException("Native decoder entry points are missing. Ensure the native library matches the managed bindings.", ex);
            }

            if (decoderHandle == IntPtr.Zero) {
                throw new InvalidOperationException("Native decoder failed to initialize for the supplied source.");
            }

            StreamInfo = CreateStreamInfo(nativeInfo);
        }

        /// <summary>
        /// Gets the Direct3D device used by the decoder.
        /// </summary>
        public Device Device { get; }

        /// <summary>
        /// Gets metadata describing the opened video stream.
        /// </summary>
        public VideoStreamInfo StreamInfo { get; }

        /// <summary>
        /// Attempts to decode the next available frame.
        /// </summary>
        /// <param name="frame">Decoded frame when available.</param>
        /// <returns>True when a frame is returned.</returns>
        public bool TryGetNextFrame(out VideoFrame frame) {
            EnsureNotDisposed();

            FfmpegNativeVideoFrame nativeFrame;
            int result = FfmpegNativeApi.he_video_decoder_try_get_frame(decoderHandle, out nativeFrame);
            if (result == 0) {
                frame = null;
                return false;
            }

            frame = new VideoFrame(this, nativeFrame);
            return true;
        }

        /// <summary>
        /// Seeks to the requested timestamp within the stream.
        /// </summary>
        /// <param name="timestamp">Target position in the stream timeline.</param>
        public void Seek(TimeSpan timestamp) {
            EnsureNotDisposed();

            int result = FfmpegNativeApi.he_video_decoder_seek(decoderHandle, timestamp.Ticks);
            if (result == 0) {
                throw new InvalidOperationException("Native decoder failed to seek to the requested timestamp.");
            }
        }

        /// <summary>
        /// Flushes any buffered decode state after a seek operation.
        /// </summary>
        public void Flush() {
            EnsureNotDisposed();
            FfmpegNativeApi.he_video_decoder_flush(decoderHandle);
        }

        /// <summary>
        /// Finalizes the decoder if it was not disposed.
        /// </summary>
        ~DirectX11VideoDecoder() {
            Dispose(false);
        }

        /// <summary>
        /// Releases the decoder and its native resources.
        /// </summary>
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases a native frame back to the decoder.
        /// </summary>
        /// <param name="nativeFrame">Native frame metadata to return.</param>
        internal void ReleaseFrame(ref FfmpegNativeVideoFrame nativeFrame) {
            if (nativeFrame.Texture == IntPtr.Zero) {
                return;
            }

            EnsureNotDisposed();
            FfmpegNativeApi.he_video_decoder_release_frame(decoderHandle, ref nativeFrame);
        }

        /// <summary>
        /// Releases unmanaged resources held by the decoder.
        /// </summary>
        /// <param name="disposing">True when called from Dispose.</param>
        void Dispose(bool disposing) {
            if (disposed) {
                return;
            }

            if (decoderHandle != IntPtr.Zero) {
                FfmpegNativeApi.he_video_decoder_destroy(decoderHandle);
                decoderHandle = IntPtr.Zero;
            }

            disposed = true;
        }

        /// <summary>
        /// Validates that the decoder has not been disposed.
        /// </summary>
        void EnsureNotDisposed() {
            if (disposed) {
                throw new ObjectDisposedException(nameof(DirectX11VideoDecoder));
            }
        }

        /// <summary>
        /// Builds a managed stream description from native metadata.
        /// </summary>
        /// <param name="nativeInfo">Native stream information payload.</param>
        /// <returns>Managed stream description.</returns>
        VideoStreamInfo CreateStreamInfo(FfmpegNativeVideoStreamInfo nativeInfo) {
            if (nativeInfo.FrameFormat == VideoFrameFormat.Unknown) {
                throw new InvalidOperationException("Native decoder did not report a frame format.");
            }

            if (nativeInfo.Width <= 0 || nativeInfo.Height <= 0) {
                throw new InvalidOperationException("Native decoder returned invalid frame dimensions.");
            }

            if (nativeInfo.FrameRate <= 0.0) {
                throw new InvalidOperationException("Native decoder returned an invalid frame rate.");
            }

            if (nativeInfo.DurationTicks < 0) {
                throw new InvalidOperationException("Native decoder returned an invalid duration.");
            }

            TimeSpan duration = TimeSpan.FromTicks(nativeInfo.DurationTicks);
            bool isHardware = nativeInfo.IsHardwareAccelerated != 0;

            return new VideoStreamInfo(nativeInfo.Width, nativeInfo.Height, nativeInfo.FrameRate, duration, nativeInfo.FrameFormat, isHardware);
        }
    }
}
