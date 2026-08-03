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
                    pixels[(i * 4) + 0] = 0.25f;
                    pixels[(i * 4) + 1] = 0.5f;
                    pixels[(i * 4) + 2] = 0.75f;
                    pixels[(i * 4) + 3] = 2.0f; // above 1.0 to confirm HDR values are not clamped
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
