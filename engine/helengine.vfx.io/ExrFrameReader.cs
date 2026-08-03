using ImageMagick;

namespace helengine.vfx.io {
    /// <summary>
    /// Reads a single EXR frame into a FloatImageAsset using Magick.NET's HDRI (float) pipeline.
    /// Quantum values from GetPixels().ToArray() are scaled to [0, Quantum.Max] and must be divided
    /// by Quantum.Max to recover the normalized (and possibly HDR, above-1.0) float value.
    /// </summary>
    public static class ExrFrameReader {
        public static FloatImageAsset ReadFrame(string filePath) {
            if (string.IsNullOrWhiteSpace(filePath)) {
                throw new ArgumentException("File path must be provided.", nameof(filePath));
            }

            using var image = new MagickImage(filePath);
            int width = (int)image.Width;
            int height = (int)image.Height;

            using var pixelCollection = image.GetPixels();
            float[] quantumScaled = pixelCollection.ToArray();
            int channelCount = quantumScaled.Length / (width * height);

            float[] rgba = new float[width * height * 4];
            for (int i = 0; i < width * height; i++) {
                int sourceOffset = i * channelCount;
                int destOffset = i * 4;
                rgba[destOffset + 0] = quantumScaled[sourceOffset + 0] / Quantum.Max;
                rgba[destOffset + 1] = (channelCount > 1 ? quantumScaled[sourceOffset + 1] : quantumScaled[sourceOffset + 0]) / Quantum.Max;
                rgba[destOffset + 2] = (channelCount > 2 ? quantumScaled[sourceOffset + 2] : quantumScaled[sourceOffset + 0]) / Quantum.Max;
                rgba[destOffset + 3] = channelCount > 3 ? quantumScaled[sourceOffset + 3] / Quantum.Max : 1f;
            }

            return new FloatImageAsset { Id = filePath, Width = (ushort)width, Height = (ushort)height, Pixels = rgba };
        }

        public static (int Width, int Height) ReadDimensions(string filePath) {
            using var image = new MagickImage(filePath);
            return ((int)image.Width, (int)image.Height);
        }
    }
}
