namespace helengine.editor {
    /// <summary>
    /// Encodes samples as little-endian PCM16 bytes used by the generic streamed audio profiles.
    /// </summary>
    public sealed class Pcm16AudioPayloadEncoder : IEditorAudioPayloadEncoder {
        /// <summary>
        /// Stable base identifier for PCM payloads.
        /// </summary>
        public string EncodingFamilyId => "pcm";

        /// <summary>
        /// Copies signed PCM16 samples into their serialized byte representation.
        /// </summary>
        /// <param name="samples">Signed PCM16 samples to encode.</param>
        /// <returns>Little-endian PCM16 bytes.</returns>
        public byte[] Encode(short[] samples) {
            if (samples == null) {
                throw new ArgumentNullException(nameof(samples));
            }

            if (samples.Length == 0) {
                return Array.Empty<byte>();
            }

            byte[] encodedBytes = new byte[samples.Length * sizeof(short)];
            Buffer.BlockCopy(samples, 0, encodedBytes, 0, encodedBytes.Length);
            return encodedBytes;
        }
    }
}
