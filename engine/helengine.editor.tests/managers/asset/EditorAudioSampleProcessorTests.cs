using Xunit;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies PCM validation and target-independent audio transforms.
    /// </summary>
    public sealed class EditorAudioSampleProcessorTests {
        /// <summary>
        /// Ensures PCM16 bytes decode into signed samples in source order.
        /// </summary>
        [Fact]
        public void DecodePcm16Samples_WhenPayloadIsLittleEndian_ReturnsSignedSamples() {
            EditorAudioSampleProcessor processor = new EditorAudioSampleProcessor();

            short[] samples = processor.DecodePcm16Samples([232, 3, 24, 252], 1);

            Assert.Equal([1000, -1000], samples);
        }

        /// <summary>
        /// Ensures stereo frames are averaged when a mono output is requested.
        /// </summary>
        [Fact]
        public void ConvertAudioChannels_WhenDownmixingStereo_AveragesEachFrame() {
            EditorAudioSampleProcessor processor = new EditorAudioSampleProcessor();

            short[] samples = processor.ConvertAudioChannels([1000, -1000, 2000, 4000], 2, 1);

            Assert.Equal([0, 3000], samples);
        }

        /// <summary>
        /// Ensures linear resampling repeats the terminal sample after the source duration.
        /// </summary>
        [Fact]
        public void ResampleAudioSamples_WhenUpsampling_UsesLinearInterpolation() {
            EditorAudioSampleProcessor processor = new EditorAudioSampleProcessor();

            short[] samples = processor.ResampleAudioSamples([1000, 2000], 1, 2, 4);

            Assert.Equal([1000, 1500, 2000, 2000], samples);
        }

        /// <summary>
        /// Ensures an unsupported encoding family fails instead of silently changing its payload family.
        /// </summary>
        [Fact]
        public void EncodeProcessedAudioPayload_WhenEncodingFamilyIsUnknown_Throws() {
            EditorAudioSampleProcessor processor = new EditorAudioSampleProcessor();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => processor.EncodeProcessedAudioPayload([1], "unknown"));

            Assert.Contains("unknown", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}