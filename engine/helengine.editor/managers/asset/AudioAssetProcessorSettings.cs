namespace helengine.editor {
    /// <summary>
    /// Stores one platform-specific audio processor configuration record for a source asset.
    /// </summary>
    public sealed class AudioAssetProcessorSettings {
        /// <summary>
        /// Gets or sets the target encoding family identifier emitted for this platform.
        /// </summary>
        public string EncodingFamilyId { get; set; } = "pcm-streamed";

        /// <summary>
        /// Gets or sets the default playback mode for the processed audio asset.
        /// </summary>
        public AudioPlaybackMode PlaybackMode { get; set; } = AudioPlaybackMode.Streamed;

        /// <summary>
        /// Gets or sets the requested target channel count, or zero to preserve the imported source channel count.
        /// </summary>
        public ushort TargetChannels { get; set; }

        /// <summary>
        /// Gets or sets the requested target sample rate, or zero to preserve the imported source sample rate.
        /// </summary>
        public int TargetSampleRate { get; set; }

        /// <summary>
        /// Gets or sets the stream chunk size in bytes used when the processed asset is streamed.
        /// </summary>
        public int StreamChunkByteSize { get; set; } = 16384;

        /// <summary>
        /// Gets or sets whether the processed asset should loop by default.
        /// </summary>
        public bool DefaultLoop { get; set; }

        /// <summary>
        /// Gets or sets the default mixer bus identifier for the processed asset.
        /// </summary>
        public string DefaultBusId { get; set; } = "master";
    }
}
