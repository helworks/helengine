using ImageMagick;

namespace helengine.vfx.io {
    /// <summary>
    /// Reads a single EXR frame into a FloatImageAsset using Magick.NET's HDRI (float) pipeline.
    /// Quantum values from GetPixels().ToArray() are scaled to [0, Quantum.Max] and must be divided
    /// by Quantum.Max to recover the normalized (and possibly HDR, above-1.0) float value.
    /// </summary>
    public static class ExrFrameReader {
        /// <summary>
        /// Reads one EXR file into an RGBA float image, expanding grayscale and RGB sources to RGBA.
        /// </summary>
        /// <param name="filePath">Path of the EXR file to read.</param>
        /// <returns>The decoded frame, top row first, RGBA interleaved.</returns>
        public static FloatImageAsset ReadFrame(string filePath) {
            return ReadFrame(filePath, out int _);
        }

        /// <summary>
        /// Reads one EXR file into an RGBA float image and reports how many channels the file actually
        /// carried, so callers that require real alpha data (mask frames) can reject files that have none
        /// instead of silently receiving a synthesized opaque alpha.
        /// </summary>
        /// <param name="filePath">Path of the EXR file to read.</param>
        /// <param name="channelCount">Receives the channel count stored in the file, before RGBA expansion.</param>
        /// <returns>The decoded frame, top row first, RGBA interleaved.</returns>
        public static FloatImageAsset ReadFrame(string filePath, out int channelCount) {
            if (string.IsNullOrWhiteSpace(filePath)) {
                throw new ArgumentException("File path must be provided.", nameof(filePath));
            }

            using var image = new MagickImage(filePath);
            int width = (int)image.Width;
            int height = (int)image.Height;

            using var pixelCollection = image.GetPixels();
            float[] quantumScaled = pixelCollection.ToArray();
            channelCount = quantumScaled.Length / (width * height);
            if (channelCount < 1) {
                throw new InvalidOperationException(
                    $"EXR file '{filePath}' reported {channelCount} channel(s) per pixel, which cannot be mapped to RGBA.");
            }

            float[] rgba = new float[width * height * 4];
            for (int i = 0; i < width * height; i++) {
                int sourceOffset = i * channelCount;
                int destOffset = i * 4;
                if (channelCount >= 4) {
                    // RGBA (or RGBA plus extra channels we ignore).
                    rgba[destOffset + 0] = quantumScaled[sourceOffset + 0] / Quantum.Max;
                    rgba[destOffset + 1] = quantumScaled[sourceOffset + 1] / Quantum.Max;
                    rgba[destOffset + 2] = quantumScaled[sourceOffset + 2] / Quantum.Max;
                    rgba[destOffset + 3] = quantumScaled[sourceOffset + 3] / Quantum.Max;
                } else if (channelCount == 3) {
                    // RGB with no alpha stored; treat the frame as fully opaque.
                    rgba[destOffset + 0] = quantumScaled[sourceOffset + 0] / Quantum.Max;
                    rgba[destOffset + 1] = quantumScaled[sourceOffset + 1] / Quantum.Max;
                    rgba[destOffset + 2] = quantumScaled[sourceOffset + 2] / Quantum.Max;
                    rgba[destOffset + 3] = 1f;
                } else if (channelCount == 2) {
                    // Gray plus alpha: the second channel is alpha, not green.
                    float gray = quantumScaled[sourceOffset + 0] / Quantum.Max;
                    rgba[destOffset + 0] = gray;
                    rgba[destOffset + 1] = gray;
                    rgba[destOffset + 2] = gray;
                    rgba[destOffset + 3] = quantumScaled[sourceOffset + 1] / Quantum.Max;
                } else {
                    // Single gray channel replicated across RGB; treat the frame as fully opaque.
                    float gray = quantumScaled[sourceOffset + 0] / Quantum.Max;
                    rgba[destOffset + 0] = gray;
                    rgba[destOffset + 1] = gray;
                    rgba[destOffset + 2] = gray;
                    rgba[destOffset + 3] = 1f;
                }
            }

            return new FloatImageAsset { Id = filePath, Width = (ushort)width, Height = (ushort)height, Pixels = rgba };
        }

        /// <summary>
        /// Reads only the header of an EXR file to discover its pixel dimensions, without decoding pixels.
        /// </summary>
        /// <param name="filePath">Path of the EXR file to probe.</param>
        /// <param name="width">Receives the image width in pixels.</param>
        /// <param name="height">Receives the image height in pixels.</param>
        public static void ReadDimensions(string filePath, out int width, out int height) {
            if (string.IsNullOrWhiteSpace(filePath)) {
                throw new ArgumentException("File path must be provided.", nameof(filePath));
            }

            using var image = new MagickImage(filePath);
            width = (int)image.Width;
            height = (int)image.Height;
        }
    }
}
