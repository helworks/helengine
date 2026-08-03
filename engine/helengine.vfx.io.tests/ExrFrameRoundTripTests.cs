using helengine;
using helengine.vfx.io;
using Xunit;

namespace helengine.vfx.io.tests {
    public class ExrFrameRoundTripTests {
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
    }
}
