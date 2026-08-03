using helengine.vfx;
using Xunit;

namespace helengine.vfx.tests {
    public class VfxClipTests {
        [Fact]
        public void Constructor_MismatchedFrameCount_Throws() {
            var source = new ImageSequence(new[] { "a.exr", "b.exr" }, 4, 4);
            var mask = new ImageSequence(new[] { "a.exr" }, 4, 4);

            Assert.Throws<InvalidOperationException>(() => new VfxClip(source, mask));
        }

        [Fact]
        public void Constructor_MismatchedResolution_Throws() {
            var source = new ImageSequence(new[] { "a.exr" }, 4, 4);
            var mask = new ImageSequence(new[] { "a.exr" }, 8, 8);

            Assert.Throws<InvalidOperationException>(() => new VfxClip(source, mask));
        }

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
