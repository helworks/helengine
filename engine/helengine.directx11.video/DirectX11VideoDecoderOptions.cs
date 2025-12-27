using SharpDX.Direct3D11;

namespace helengine.directx11.video {
    /// <summary>
    /// Provides configuration for opening a DirectX 11 backed video decoder.
    /// </summary>
    public sealed class DirectX11VideoDecoderOptions {
        /// <summary>
        /// Initializes a new set of decoder options.
        /// </summary>
        /// <param name="device">Direct3D device used for hardware surfaces.</param>
        /// <param name="sourcePath">Path to the video file to decode.</param>
        /// <param name="hardwareMode">Hardware usage preference for the decoder.</param>
        public DirectX11VideoDecoderOptions(Device device, string sourcePath, VideoDecoderHardwareMode hardwareMode) {
            if (device == null) {
                throw new ArgumentNullException(nameof(device));
            }

            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            if (!Enum.IsDefined(typeof(VideoDecoderHardwareMode), hardwareMode)) {
                throw new ArgumentOutOfRangeException(nameof(hardwareMode), "Hardware mode is not supported.");
            }

            Device = device;
            SourcePath = sourcePath;
            HardwareMode = hardwareMode;
        }

        /// <summary>
        /// Gets the Direct3D device used to allocate decoder surfaces.
        /// </summary>
        public Device Device { get; }

        /// <summary>
        /// Gets the source file path provided by the caller.
        /// </summary>
        public string SourcePath { get; }

        /// <summary>
        /// Gets the requested hardware usage mode.
        /// </summary>
        public VideoDecoderHardwareMode HardwareMode { get; }
    }
}
