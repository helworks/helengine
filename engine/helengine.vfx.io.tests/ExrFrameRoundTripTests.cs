using helengine;
using helengine.vfx.io;
using ImageMagick;
using Xunit;

namespace helengine.vfx.io.tests {
    /// <summary>
    /// Exercises the Magick.NET Q16-HDRI EXR path that the design flagged as an open risk: values above
    /// 1.0 must survive a write/read cycle, and the reader's channel expansion must map channels to the
    /// right RGBA slots.
    /// </summary>
    public class ExrFrameRoundTripTests {
        /// <summary>
        /// Writes distinct per-pixel HDR values and reads them back, catching both value corruption and
        /// row/column transposition.
        /// </summary>
        [Fact]
        public void WriteFrame_ThenReadFrame_RoundTripsWithinTolerance() {
            string path = Path.Combine(Path.GetTempPath(), "helengine-vfx-io-tests-" + Guid.NewGuid().ToString("N") + ".exr");
            try {
                float[] pixels = new float[2 * 2 * 4];
                for (int i = 0; i < 2 * 2; i++) {
                    // Distinct per-pixel values to catch row/column transposition bugs
                    // Pixel 0: (0.1, 0.2, 0.3, 0.5)
                    // Pixel 1: (0.2, 0.4, 0.6, 1.0)
                    // Pixel 2: (0.3, 0.6, 0.9, 1.5) - 1.5 is HDR above 1.0
                    // Pixel 3: (0.4, 0.8, 1.2, 2.0) - 1.2 and 2.0 are HDR above 1.0
                    pixels[(i * 4) + 0] = 0.1f * (i + 1);
                    pixels[(i * 4) + 1] = 0.2f * (i + 1);
                    pixels[(i * 4) + 2] = 0.3f * (i + 1);
                    pixels[(i * 4) + 3] = 0.5f * (i + 1); // Multiple pixels have values above 1.0
                }
                var original = new FloatImageAsset { Width = 2, Height = 2, Pixels = pixels };

                ExrFrameWriter.WriteFrame(original, path);
                FloatImageAsset roundTripped = ExrFrameReader.ReadFrame(path);

                Assert.Equal(2, roundTripped.Width);
                Assert.Equal(2, roundTripped.Height);
                for (int i = 0; i < pixels.Length; i++) {
                    Assert.Equal(pixels[i], roundTripped.Pixels[i], 2);
                }

                original.Dispose();
                roundTripped.Dispose();
            } finally {
                if (File.Exists(path)) {
                    File.Delete(path);
                }
            }
        }

        /// <summary>
        /// A frame written as RGBA must report four channels, which is how the runner tells a real
        /// matte apart from an alpha-less image whose alpha was synthesized during read.
        /// </summary>
        [Fact]
        public void ReadFrame_RgbaSource_ReportsFourChannels() {
            string path = Path.Combine(Path.GetTempPath(), "helengine-vfx-io-tests-" + Guid.NewGuid().ToString("N") + ".exr");
            try {
                var original = new FloatImageAsset { Width = 2, Height = 2, Pixels = new float[2 * 2 * 4] };
                ExrFrameWriter.WriteFrame(original, path);
                original.Dispose();

                FloatImageAsset roundTripped = ExrFrameReader.ReadFrame(path, out int channelCount);
                roundTripped.Dispose();

                Assert.Equal(4, channelCount);
            } finally {
                if (File.Exists(path)) {
                    File.Delete(path);
                }
            }
        }

        /// <summary>
        /// An EXR with no alpha channel must be reported as such, and must be expanded with a
        /// synthesized opaque alpha rather than silently borrowing a color channel. The runner relies
        /// on this channel count to reject alpha-less mask sequences.
        /// </summary>
        [Fact]
        public void ReadFrame_RgbSourceWithoutAlpha_ReportsFewerChannelsAndSynthesizesOpaqueAlpha() {
            string path = Path.Combine(Path.GetTempPath(), "helengine-vfx-io-tests-" + Guid.NewGuid().ToString("N") + ".exr");
            try {
                float[] rgb = { 0.25f, 0.5f, 0.75f, 0.25f, 0.5f, 0.75f, 0.25f, 0.5f, 0.75f, 0.25f, 0.5f, 0.75f };
                byte[] rgbBytes = new byte[rgb.Length * sizeof(float)];
                Buffer.BlockCopy(rgb, 0, rgbBytes, 0, rgbBytes.Length);

                var settings = new PixelReadSettings(2, 2, StorageType.Float, PixelMapping.RGB);
                using (var image = new MagickImage(rgbBytes, settings)) {
                    image.Format = MagickFormat.Exr;
                    image.Alpha(AlphaOption.Off);
                    image.Write(path);
                }

                FloatImageAsset frame = ExrFrameReader.ReadFrame(path, out int channelCount);
                try {
                    Assert.True(channelCount < 4, $"Expected fewer than 4 channels for an alpha-less EXR, got {channelCount}.");
                    Assert.Equal(0.25f, frame.Pixels[0], 2);
                    Assert.Equal(0.5f, frame.Pixels[1], 2);
                    Assert.Equal(0.75f, frame.Pixels[2], 2);
                    Assert.Equal(1f, frame.Pixels[3]);
                } finally {
                    frame.Dispose();
                }
            } finally {
                if (File.Exists(path)) {
                    File.Delete(path);
                }
            }
        }
    }
}
