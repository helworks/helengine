using System.Drawing;
using System.Drawing.Imaging;

namespace helengine.render.validation {
    /// <summary>
    /// Captures window client pixels and samples validation colors from images.
    /// </summary>
    public static class RenderImageCapture {
        /// <summary>
        /// Captures the current client area of a window to a PNG image.
        /// </summary>
        /// <param name="window">Window whose client area should be captured.</param>
        /// <param name="outputPath">Target PNG file path.</param>
        public static void CaptureClientArea(Form window, string outputPath) {
            if (window == null) {
                throw new ArgumentNullException(nameof(window));
            }

            if (string.IsNullOrWhiteSpace(outputPath)) {
                throw new ArgumentException("Output path must be provided.", nameof(outputPath));
            }

            if (window.ClientSize.Width <= 0 || window.ClientSize.Height <= 0) {
                throw new InvalidOperationException("Window client size must be greater than zero for capture.");
            }

            string directory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(directory)) {
                throw new InvalidOperationException("Capture output path must include a directory.");
            }

            Directory.CreateDirectory(directory);

            using (var bitmap = new Bitmap(window.ClientSize.Width, window.ClientSize.Height, PixelFormat.Format32bppArgb))
            using (Graphics graphics = Graphics.FromImage(bitmap)) {
                Point topLeft = window.PointToScreen(Point.Empty);
                graphics.CopyFromScreen(topLeft, Point.Empty, window.ClientSize, CopyPixelOperation.SourceCopy);
                bitmap.Save(outputPath, ImageFormat.Png);
            }
        }

        /// <summary>
        /// Reads the center pixel from a PNG image.
        /// </summary>
        /// <param name="path">Image path to read.</param>
        /// <returns>Center pixel color.</returns>
        public static Color ReadCenterPixel(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Image path must be provided.", nameof(path));
            }

            if (!File.Exists(path)) {
                throw new FileNotFoundException("Image file was not found.", path);
            }

            using (var bitmap = new Bitmap(path)) {
                if (bitmap.Width <= 0 || bitmap.Height <= 0) {
                    throw new InvalidOperationException("Image dimensions must be greater than zero.");
                }

                int centerX = bitmap.Width / 2;
                int centerY = bitmap.Height / 2;
                return bitmap.GetPixel(centerX, centerY);
            }
        }

        /// <summary>
        /// Reads a pixel at the requested coordinates from a PNG image.
        /// </summary>
        /// <param name="path">Image path to read.</param>
        /// <param name="x">Zero-based pixel x coordinate.</param>
        /// <param name="y">Zero-based pixel y coordinate.</param>
        /// <returns>Sampled pixel color.</returns>
        public static Color ReadPixel(string path, int x, int y) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Image path must be provided.", nameof(path));
            }

            if (!File.Exists(path)) {
                throw new FileNotFoundException("Image file was not found.", path);
            }

            using (var bitmap = new Bitmap(path)) {
                if (bitmap.Width <= 0 || bitmap.Height <= 0) {
                    throw new InvalidOperationException("Image dimensions must be greater than zero.");
                }

                if (x < 0 || x >= bitmap.Width) {
                    throw new ArgumentOutOfRangeException(nameof(x), "Pixel x coordinate must be within image bounds.");
                }

                if (y < 0 || y >= bitmap.Height) {
                    throw new ArgumentOutOfRangeException(nameof(y), "Pixel y coordinate must be within image bounds.");
                }

                return bitmap.GetPixel(x, y);
            }
        }

        /// <summary>
        /// Formats a color as an `rgba(r,g,b,a)` string.
        /// </summary>
        /// <param name="color">Color value to format.</param>
        /// <returns>Formatted color string.</returns>
        public static string FormatColor(Color color) {
            return $"rgba({color.R},{color.G},{color.B},{color.A})";
        }
    }
}
