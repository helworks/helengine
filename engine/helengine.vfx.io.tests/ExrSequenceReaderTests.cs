using helengine.vfx;
using helengine.vfx.io;
using Xunit;

namespace helengine.vfx.io.tests {
    public class ExrSequenceReaderTests {
        [Fact]
        public void ReadSequence_MissingFolder_Throws() {
            string missingFolder = Path.Combine(Path.GetTempPath(), "helengine-vfx-io-tests-missing-" + Guid.NewGuid().ToString("N"));

            Assert.Throws<DirectoryNotFoundException>(() => ExrSequenceReader.ReadSequence(missingFolder));
        }

        [Fact]
        public void ReadSequence_SortsFramesNumerically_NotAlphabetically() {
            string folder = Path.Combine(Path.GetTempPath(), "helengine-vfx-io-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try {
                WriteFrame(folder, "frame.0010.exr");
                WriteFrame(folder, "frame.0002.exr");
                WriteFrame(folder, "frame.0001.exr");

                ImageSequence sequence = ExrSequenceReader.ReadSequence(folder);

                Assert.Equal(3, sequence.FrameCount);
                Assert.EndsWith("frame.0001.exr", sequence.FramePaths[0]);
                Assert.EndsWith("frame.0002.exr", sequence.FramePaths[1]);
                Assert.EndsWith("frame.0010.exr", sequence.FramePaths[2]);
                Assert.Equal(2, sequence.Width);
                Assert.Equal(2, sequence.Height);
            } finally {
                Directory.Delete(folder, recursive: true);
            }
        }

        static void WriteFrame(string folder, string fileName) {
            var asset = new FloatImageAsset { Width = 2, Height = 2, Pixels = new float[2 * 2 * 4] };
            ExrFrameWriter.WriteFrame(asset, Path.Combine(folder, fileName));
            asset.Dispose();
        }
    }
}
