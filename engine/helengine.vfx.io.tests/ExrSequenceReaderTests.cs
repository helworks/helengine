using helengine.vfx;
using helengine.vfx.io;
using Xunit;

namespace helengine.vfx.io.tests {
    /// <summary>
    /// Covers EXR sequence discovery: the missing-folder failure the CLI reports to users, and the
    /// numeric frame ordering that plain alphabetical sorting would get wrong.
    /// </summary>
    public class ExrSequenceReaderTests {
        /// <summary>
        /// A folder that does not exist must fail with DirectoryNotFoundException so the CLI can print
        /// a clean message instead of a stack trace.
        /// </summary>
        [Fact]
        public void ReadSequence_MissingFolder_Throws() {
            string missingFolder = Path.Combine(Path.GetTempPath(), "helengine-vfx-io-tests-missing-" + Guid.NewGuid().ToString("N"));

            Assert.Throws<DirectoryNotFoundException>(() => ExrSequenceReader.ReadSequence(missingFolder));
        }

        /// <summary>
        /// frame.0010 must sort after frame.0002, which alphabetical ordering would get right only by
        /// accident of zero padding; the reader sorts on the parsed frame index instead.
        /// </summary>
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

        /// <summary>
        /// Writes a small blank EXR frame used only to populate a fixture folder.
        /// </summary>
        /// <param name="folder">Folder to write the frame into.</param>
        /// <param name="fileName">File name to write.</param>
        static void WriteFrame(string folder, string fileName) {
            var asset = new FloatImageAsset { Width = 2, Height = 2, Pixels = new float[2 * 2 * 4] };
            ExrFrameWriter.WriteFrame(asset, Path.Combine(folder, fileName));
            asset.Dispose();
        }
    }
}
