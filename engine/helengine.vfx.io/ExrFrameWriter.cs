using ImageMagick;

namespace helengine.vfx.io {
    /// <summary>
    /// Writes a single FloatImageAsset frame to an EXR file using Magick.NET's HDRI (float) pipeline.
    /// </summary>
    public static class ExrFrameWriter {
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
