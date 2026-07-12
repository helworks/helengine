namespace helengine {
    /// <summary>
    /// Represents one authored audio asset and its encoded runtime payload.
    /// </summary>
    public class AudioAsset : Asset {
        /// <summary>
        /// Gets or sets how this asset should be consumed at runtime.
        /// </summary>
        public AudioPlaybackMode PlaybackMode { get; set; }

        /// <summary>
        /// Gets or sets whether playback should loop by default.
        /// </summary>
        public bool DefaultLoop { get; set; }

        /// <summary>
        /// Gets or sets the default mixer bus identifier.
        /// </summary>
        public string DefaultBusId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the encoded channel count.
        /// </summary>
        public int Channels { get; set; }

        /// <summary>
        /// Gets or sets the encoded sample rate.
        /// </summary>
        public int SampleRate { get; set; }

        /// <summary>
        /// Gets or sets the authored duration in seconds.
        /// </summary>
        public float DurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the encoding family identifier for the payload.
        /// </summary>
        public string EncodingFamilyId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the encoded audio payload.
        /// </summary>
        public byte[] EncodedBytes { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Gets or sets the chunk table for streamed payloads.
        /// </summary>
        public AudioChunkDescriptor[] Chunks { get; set; } = Array.Empty<AudioChunkDescriptor>();

        /// <summary>
        /// Gets or sets platform-authored encoded variants for this asset.
        /// </summary>
        public AudioAssetPlatformOverrideAsset[] PlatformOverrides { get; set; } = Array.Empty<AudioAssetPlatformOverrideAsset>();
    }
}
