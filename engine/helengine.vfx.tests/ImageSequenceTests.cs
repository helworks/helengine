using helengine.vfx;
using Xunit;

namespace helengine.vfx.tests {
    /// <summary>
    /// Covers ImageSequence's construction-time validation and the frame/resolution values it exposes.
    /// </summary>
    public class ImageSequenceTests {
        /// <summary>
        /// A sequence with no frames is meaningless and must be rejected instead of producing an
        /// empty clip that fails later during export.
        /// </summary>
        [Fact]
        public void Constructor_EmptyFramePaths_Throws() {
            Assert.Throws<ArgumentException>(() => new ImageSequence(new string[0], 4, 4));
        }

        /// <summary>
        /// Confirms a valid sequence reports the frame count derived from its paths and the resolution it was given.
        /// </summary>
        [Fact]
        public void Constructor_ValidInput_SetsFrameCount() {
            var sequence = new ImageSequence(new[] { "a.exr", "b.exr" }, 4, 4);

            Assert.Equal(2, sequence.FrameCount);
            Assert.Equal(4, sequence.Width);
            Assert.Equal(4, sequence.Height);
        }
    }
}
