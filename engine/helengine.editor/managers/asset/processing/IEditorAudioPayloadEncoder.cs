namespace helengine.editor {
    /// <summary>
    /// Encodes processed signed PCM16 samples into one runtime audio payload family.
    /// </summary>
    public interface IEditorAudioPayloadEncoder {
        /// <summary>
        /// Gets the stable encoding-family identifier selected by audio processor settings.
        /// </summary>
        string EncodingFamilyId { get; }

        /// <summary>
        /// Encodes a complete sample buffer without changing the sample order.
        /// </summary>
        /// <param name="samples">Interleaved signed PCM16 samples to encode.</param>
        /// <returns>Runtime payload bytes.</returns>
        byte[] Encode(short[] samples);
    }
}
