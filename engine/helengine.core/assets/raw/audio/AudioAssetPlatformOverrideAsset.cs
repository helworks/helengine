namespace helengine {
    /// <summary>
    /// Stores one platform-authored encoded variant for an audio asset.
    /// </summary>
    public class AudioAssetPlatformOverrideAsset {
        /// <summary>
        /// Gets or sets the platform identifier that owns this override payload.
        /// </summary>
        public string PlatformId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets how this asset should be played back on the target platform.
        /// </summary>
        public AudioPlaybackMode PlaybackMode { get; set; }

        /// <summary>
        /// Gets or sets whether playback should loop by default on the target platform.
        /// </summary>
        public bool DefaultLoop { get; set; }

        /// <summary>
        /// Gets or sets the default bus identifier for the target platform.
        /// </summary>
        public string DefaultBusId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the encoded channel count for the target platform.
        /// </summary>
        public int Channels { get; set; }

        /// <summary>
        /// Gets or sets the encoded sample rate for the target platform.
        /// </summary>
        public int SampleRate { get; set; }

        /// <summary>
        /// Gets or sets the authored duration in seconds for the target platform payload.
        /// </summary>
        public float DurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the encoding family identifier used by the target platform payload.
        /// </summary>
        public string EncodingFamilyId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the encoded audio payload for the target platform.
        /// </summary>
        public byte[] EncodedBytes { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Gets or sets the chunk table for the target platform payload.
        /// </summary>
        public AudioChunkDescriptor[] Chunks { get; set; } = Array.Empty<AudioChunkDescriptor>();
    }
}
