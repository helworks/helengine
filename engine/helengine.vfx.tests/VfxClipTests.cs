using helengine.vfx;
using Xunit;

namespace helengine.vfx.tests {
    /// <summary>
    /// Covers VfxClip's source/mask pairing rules, which are the pipeline's first real user-input
    /// boundary: a mismatch here must fail immediately rather than midway through an export.
    /// </summary>
    public class VfxClipTests {
        /// <summary>
        /// Source and mask sequences of different lengths cannot be paired frame for frame.
        /// </summary>
        [Fact]
        public void Constructor_MismatchedFrameCount_Throws() {
            var source = new ImageSequence(new[] { "a.exr", "b.exr" }, 4, 4);
            var mask = new ImageSequence(new[] { "a.exr" }, 4, 4);

            Assert.Throws<InvalidOperationException>(() => new VfxClip(source, mask));
        }

        /// <summary>
        /// A mask at a different resolution than the source would silently rescale during sampling,
        /// so it has to be rejected up front.
        /// </summary>
        [Fact]
        public void Constructor_MismatchedResolution_Throws() {
            var source = new ImageSequence(new[] { "a.exr" }, 4, 4);
            var mask = new ImageSequence(new[] { "a.exr" }, 8, 8);

            Assert.Throws<InvalidOperationException>(() => new VfxClip(source, mask));
        }

        /// <summary>
        /// Confirms a well-formed clip forwards its frame count and resolution from the source sequence.
        /// </summary>
        [Fact]
        public void Constructor_MatchingSequences_ExposesFrameCountAndResolution() {
            var source = new ImageSequence(new[] { "a.exr", "b.exr" }, 4, 4);
            var mask = new ImageSequence(new[] { "a.exr", "b.exr" }, 4, 4);

            var clip = new VfxClip(source, mask);

            Assert.Equal(2, clip.FrameCount);
            Assert.Equal(4, clip.Width);
            Assert.Equal(4, clip.Height);
        }
    }
}
