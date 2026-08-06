using helengine.vfx;
using Xunit;

namespace helengine.vfx.tests {
    /// <summary>
    /// Covers VfxClip's sequence-grouping rules, which are the pipeline's first real user-input
    /// boundary: a mismatch here must fail immediately rather than midway through an export.
    /// </summary>
    public class VfxClipTests {
        /// <summary>
        /// An empty sequence set has nothing to composite and must be rejected.
        /// </summary>
        [Fact]
        public void Constructor_NoSequences_Throws() {
            Assert.Throws<ArgumentException>(() => new VfxClip(new Dictionary<string, ImageSequence>()));
        }

        /// <summary>
        /// Input sequences of different lengths cannot be paired frame for frame.
        /// </summary>
        [Fact]
        public void Constructor_MismatchedFrameCount_Throws() {
            var sequences = new Dictionary<string, ImageSequence> {
                ["Source"] = new ImageSequence(new[] { "a.exr", "b.exr" }, 4, 4),
                ["Mask"] = new ImageSequence(new[] { "a.exr" }, 4, 4)
            };

            Assert.Throws<InvalidOperationException>(() => new VfxClip(sequences));
        }

        /// <summary>
        /// A sequence at a different resolution than the others would silently rescale during
        /// sampling, so it has to be rejected up front.
        /// </summary>
        [Fact]
        public void Constructor_MismatchedResolution_Throws() {
            var sequences = new Dictionary<string, ImageSequence> {
                ["Source"] = new ImageSequence(new[] { "a.exr" }, 4, 4),
                ["Mask"] = new ImageSequence(new[] { "a.exr" }, 8, 8)
            };

            Assert.Throws<InvalidOperationException>(() => new VfxClip(sequences));
        }

        /// <summary>
        /// Confirms a well-formed two-sequence clip forwards its frame count and resolution, and that
        /// each sequence is retrievable by role.
        /// </summary>
        [Fact]
        public void Constructor_MatchingSequences_ExposesFrameCountAndResolution() {
            var source = new ImageSequence(new[] { "a.exr", "b.exr" }, 4, 4);
            var mask = new ImageSequence(new[] { "a.exr", "b.exr" }, 4, 4);
            var clip = new VfxClip(new Dictionary<string, ImageSequence> { ["Source"] = source, ["Mask"] = mask });

            Assert.Equal(2, clip.FrameCount);
            Assert.Equal(4, clip.Width);
            Assert.Equal(4, clip.Height);
            Assert.Same(source, clip.GetSequence("Source"));
            Assert.Same(mask, clip.GetSequence("Mask"));
        }

        /// <summary>
        /// A clip is not limited to two roles; three (or more) matching sequences must group cleanly.
        /// </summary>
        [Fact]
        public void Constructor_ThreeMatchingSequences_ExposesFrameCountAndResolution() {
            var subject = new ImageSequence(new[] { "a.exr" }, 4, 4);
            var renderColor = new ImageSequence(new[] { "b.exr" }, 4, 4);
            var renderDepth = new ImageSequence(new[] { "c.exr" }, 4, 4);
            var clip = new VfxClip(new Dictionary<string, ImageSequence> {
                ["Subject"] = subject,
                ["RenderColor"] = renderColor,
                ["RenderDepth"] = renderDepth
            });

            Assert.Equal(1, clip.FrameCount);
            Assert.Same(renderDepth, clip.GetSequence("RenderDepth"));
        }

        /// <summary>
        /// Looking up a role the clip was never given must fail rather than return null.
        /// </summary>
        [Fact]
        public void GetSequence_UnknownRole_Throws() {
            var source = new ImageSequence(new[] { "a.exr" }, 4, 4);
            var clip = new VfxClip(new Dictionary<string, ImageSequence> { ["Source"] = source });

            Assert.Throws<InvalidOperationException>(() => clip.GetSequence("Mask"));
        }
    }
}
