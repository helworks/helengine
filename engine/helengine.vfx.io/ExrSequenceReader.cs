using System.Globalization;
using System.Text.RegularExpressions;
using helengine.vfx;

namespace helengine.vfx.io {
    /// <summary>
    /// Discovers EXR frame files in a folder and builds an ImageSequence, sorted by the numeric
    /// frame index embedded in each filename (e.g. frame.0007.exr).
    /// </summary>
    public static class ExrSequenceReader {
        /// <summary>
        /// Matches the last run of digits in a filename, which is treated as the frame index.
        /// </summary>
        static readonly Regex FrameNumberPattern = new Regex(@"(\d+)(?!.*\d)", RegexOptions.Compiled);

        /// <summary>
        /// Builds a frame-ordered sequence from every .exr file in a folder, using the first frame's
        /// header to establish the sequence resolution.
        /// </summary>
        /// <param name="folderPath">Folder containing the .exr frames.</param>
        /// <returns>The discovered sequence, ordered by numeric frame index.</returns>
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

            ExrFrameReader.ReadDimensions(sorted[0], out int width, out int height);

            return new ImageSequence(sorted, width, height);
        }

        /// <summary>
        /// Extracts the trailing numeric frame index from a frame file path so frames sort numerically
        /// rather than alphabetically (frame.0002 before frame.0010).
        /// </summary>
        /// <param name="path">Frame file path to inspect.</param>
        /// <returns>The parsed frame index.</returns>
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
