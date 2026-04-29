namespace helengine {
    /// <summary>
    /// Stores raw bytes loaded directly from disk.
    /// </summary>
    public class RawByteContent {
        /// <summary>
        /// Gets or sets the raw file bytes.
        /// </summary>
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
    }
}
