using helengine;
using helengine.vfx;
using helengine.vfx.directx11;
using helengine.vfx.effects;
using helengine.vfx.io;
using Xunit;

namespace helengine.vfx.cli.tests {
    public class EndToEndExportTests {
        [Fact]
        public void Run_RainbowExpand_WritesExpectedFrameCountAndResolution() {
            string root = Path.Combine(Path.GetTempPath(), "helengine-vfx-e2e-" + Guid.NewGuid().ToString("N"));
            string sourceFolder = Path.Combine(root, "source");
            string maskFolder = Path.Combine(root, "mask");
            string outputFolder = Path.Combine(root, "output");
            Directory.CreateDirectory(sourceFolder);
            Directory.CreateDirectory(maskFolder);

            const int width = 8;
            const int height = 8;
            const int frameCount = 3;

            try {
                for (int i = 0; i < frameCount; i++) {
                    WriteSolidFrame(Path.Combine(sourceFolder, $"frame.{i:D4}.exr"), width, height, 0.2f, 0.4f, 0.6f, 1f);
                    WriteSolidFrame(Path.Combine(maskFolder, $"frame.{i:D4}.exr"), width, height, 1f, 1f, 1f, 1f);
                }

                ImageSequence source = ExrSequenceReader.ReadSequence(sourceFolder);
                ImageSequence mask = ExrSequenceReader.ReadSequence(maskFolder);
                VfxClip clip = new VfxClip(source, mask);
                IVfxEffect effect = new RainbowExpandEffect();

                using (DirectX11VfxDevice device = new DirectX11VfxDevice())
                using (DirectX11VfxEffectRunner runner = new DirectX11VfxEffectRunner(device, effect)) {
                    runner.Run(clip, effect, new Dictionary<string, string>(), outputFolder);
                }

                string[] outputFiles = Directory.GetFiles(outputFolder, "*.exr");
                Assert.Equal(frameCount, outputFiles.Length);

                foreach (string outputFile in outputFiles) {
                    FloatImageAsset frame = ExrFrameReader.ReadFrame(outputFile);
                    Assert.Equal(width, frame.Width);
                    Assert.Equal(height, frame.Height);
                    Assert.Contains(frame.Pixels, value => value != 0f);
                    frame.Dispose();
                }
            } finally {
                Directory.Delete(root, recursive: true);
            }
        }

        static void WriteSolidFrame(string path, int width, int height, float r, float g, float b, float a) {
            float[] pixels = new float[width * height * 4];
            for (int i = 0; i < width * height; i++) {
                pixels[(i * 4) + 0] = r;
                pixels[(i * 4) + 1] = g;
                pixels[(i * 4) + 2] = b;
                pixels[(i * 4) + 3] = a;
            }
            FloatImageAsset frame = new FloatImageAsset { Width = (ushort)width, Height = (ushort)height, Pixels = pixels };
            ExrFrameWriter.WriteFrame(frame, path);
            frame.Dispose();
        }
    }
}
