namespace helengine {
    /// <summary>
    /// Describes one byte-range chunk inside an encoded audio payload.
    /// </summary>
    public class AudioChunkDescriptor {
        /// <summary>
        /// Gets or sets the byte offset of the chunk within the encoded payload.
        /// </summary>
        public int ByteOffset { get; set; }

        /// <summary>
        /// Gets or sets the byte length of the chunk within the encoded payload.
        /// </summary>
        public int ByteLength { get; set; }
    }
}
