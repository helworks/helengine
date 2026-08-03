using System.Globalization;
using System.Text.RegularExpressions;
using helengine.vfx;

namespace helengine.vfx.io {
    /// <summary>
    /// Discovers EXR frame files in a folder and builds an ImageSequence, sorted by the numeric
    /// frame index embedded in each filename (e.g. frame.0007.exr).
    /// </summary>
    public static class ExrSequenceReader {
        static readonly Regex FrameNumberPattern = new Regex(@"(\d+)(?!.*\d)", RegexOptions.Compiled);

        public static ImageSequence ReadSequence(string folderPath) {
            if (string.IsNullOrWhiteSpace(folderPath)) {
                throw new ArgumentException("Folder path must be provided.", nameof(folderPath));
            }
            if (!Directory.Exists(folderPath)) {
                throw new DirectoryNotFoundException($"Image sequence folder '{folderPath}' does not exist.");
            }

            string[] files = Directory.GetFiles(folderPath, "*.exr");
            if (files.Length == 0) {
                throw new InvalidOperationException($"Image sequence folder '{folderPath}' contains no .exr files.");
            }

            string[] sorted = files
                .OrderBy(path => ExtractFrameNumber(path))
                .ToArray();

            (int width, int height) = ExrFrameReader.ReadDimensions(sorted[0]);

            return new ImageSequence(sorted, width, height);
        }

        static int ExtractFrameNumber(string path) {
            string fileName = Path.GetFileNameWithoutExtension(path);
            Match match = FrameNumberPattern.Match(fileName);
            if (!match.Success) {
                throw new InvalidOperationException($"File '{path}' does not contain a numeric frame index in its name.");
            }
            return int.Parse(match.Value, CultureInfo.InvariantCulture);
        }
    }
}
