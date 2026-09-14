using Xunit;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies byte-for-byte payload encoding for supported editor audio families.
    /// </summary>
    public sealed class EditorAudioPayloadEncoderTests {
        /// <summary>
        /// Ensures PCM encoding preserves signed sample bytes in little-endian order.
        /// </summary>
        [Fact]
        public void Pcm16Encoder_WhenEncodingExtremes_PreservesLittleEndianBytes() {
            Pcm16AudioPayloadEncoder encoder = new Pcm16AudioPayloadEncoder();

            byte[] bytes = encoder.Encode([short.MinValue, -1, 0, 1, short.MaxValue]);

            Assert.Equal([0, 128, 255, 255, 0, 0, 1, 0, 255, 127], bytes);
        }

        /// <summary>
        /// Ensures Nintendo DS framing preserves the predictor header and nibble order.
        /// </summary>
        [Fact]
        public void NintendoDsEncoder_WhenEncodingKnownSamples_EmitsGoldenBytes() {
            NintendoDsImaAdpcmAudioPayloadEncoder encoder = new NintendoDsImaAdpcmAudioPayloadEncoder();

            byte[] bytes = encoder.Encode([1000, 2000]);

            Assert.Equal([232, 3, 0, 0, 7], bytes);
        }

        /// <summary>
        /// Ensures empty sample buffers remain empty for every payload encoder.
        /// </summary>
        [Fact]
        public void Encoders_WhenEncodingEmptySamples_ReturnEmptyPayloads() {
            Assert.Empty(new Pcm16AudioPayloadEncoder().Encode(Array.Empty<short>()));
            Assert.Empty(new NintendoDsImaAdpcmAudioPayloadEncoder().Encode(Array.Empty<short>()));
        }
    }
}