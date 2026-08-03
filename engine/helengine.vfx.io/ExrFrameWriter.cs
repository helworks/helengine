using ImageMagick;

namespace helengine.vfx.io {
    /// <summary>
    /// Writes a single FloatImageAsset frame to an EXR file using Magick.NET's HDRI (float) pipeline.
    /// </summary>
    public static class ExrFrameWriter {
        /// <summary>
        /// Writes one RGBA float frame out as an EXR file, creating the destination directory when needed.
        /// Pixel data is passed straight through as 32-bit floats so HDR values above 1.0 survive.
        /// </summary>
        /// <param name="frame">Frame to write; pixels are RGBA interleaved with the top row first.</param>
        /// <param name="filePath">Destination EXR file path.</param>
        public static void WriteFrame(FloatImageAsset frame, string filePath) {
            if (frame == null) {
                throw new ArgumentNullException(nameof(frame));
            }
            if (string.IsNullOrWhiteSpace(filePath)) {
                throw new ArgumentException("File path must be provided.", nameof(filePath));
            }

            byte[] rgbaBytes = new byte[frame.Pixels.Length * sizeof(float)];
            Buffer.BlockCopy(frame.Pixels, 0, rgbaBytes, 0, rgbaBytes.Length);

            var settings = new PixelReadSettings(frame.Width, frame.Height, StorageType.Float, PixelMapping.RGBA);
            using var image = new MagickImage(rgbaBytes, settings);
            image.Format = MagickFormat.Exr;

            string directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
            if (!string.IsNullOrWhiteSpace(directory)) {
                Directory.CreateDirectory(directory);
            }

            image.Write(filePath);
        }
    }
}
