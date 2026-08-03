using helengine.vfx;
using Xunit;

namespace helengine.vfx.tests {
    public class ImageSequenceTests {
        [Fact]
        public void Constructor_EmptyFramePaths_Throws() {
            Assert.Throws<ArgumentException>(() => new ImageSequence(new string[0], 4, 4));
        }

        [Fact]
        public void Constructor_ValidInput_SetsFrameCount() {
            var sequence = new ImageSequence(new[] { "a.exr", "b.exr" }, 4, 4);

            Assert.Equal(2, sequence.FrameCount);
            Assert.Equal(4, sequence.Width);
            Assert.Equal(4, sequence.Height);
        }
    }
}
