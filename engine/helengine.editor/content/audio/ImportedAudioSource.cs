namespace helengine.editor {
    /// <summary>
    /// Stores decoded audio metadata and PCM payload returned by one source importer.
    /// </summary>
    public sealed class ImportedAudioSource {
        /// <summary>
        /// Gets or sets the decoded channel count.
        /// </summary>
        public ushort Channels { get; set; }

        /// <summary>
        /// Gets or sets the decoded sample rate.
        /// </summary>
        public int SampleRate { get; set; }

        /// <summary>
        /// Gets or sets the authored duration in seconds.
        /// </summary>
        public float DurationSeconds { get; set; }

        /// <summary>
        /// Gets or sets the decoded PCM16 payload bytes.
        /// </summary>
        public byte[] Pcm16Bytes { get; set; } = Array.Empty<byte>();
    }
}
