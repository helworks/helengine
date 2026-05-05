using System.Reflection;

namespace helengine.editor {
    /// <summary>
    /// Texture importer that loads TGA and DDS images through the Pfim decoder library.
    /// </summary>
    public sealed class PfimTextureImporter : ITextureImporter {
        /// <summary>
        /// Simple assembly name used to resolve the Pfim package at runtime.
        /// </summary>
        const string PfimAssemblyName = "Pfim";

        /// <summary>
        /// Primary decoder type name used by recent Pfim builds.
        /// </summary>
        const string PfimageTypeName = "Pfim.Pfimage";

        /// <summary>
        /// Fallback decoder type name used by older Pfim samples.
        /// </summary>
        const string PfimTypeName = "Pfim.Pfim";

        /// <summary>
        /// Imports one texture asset from the supplied source stream.
        /// </summary>
        /// <param name="stream">Stream containing source image bytes.</param>
        /// <returns>Imported texture asset in RGBA byte order.</returns>
        public TextureAsset ImportTexture(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            IDisposable decodedImage = OpenDecodedImage(stream);
            try {
                return ConvertDecodedImage(decodedImage);
            } finally {
                decodedImage.Dispose();
            }
        }

        /// <summary>
        /// Opens a decoded Pfim image from the supplied stream.
        /// </summary>
        /// <param name="stream">Stream containing source image bytes.</param>
        /// <returns>Disposable decoded image wrapper.</returns>
        IDisposable OpenDecodedImage(Stream stream) {
            Assembly assembly = Assembly.Load(PfimAssemblyName);
            object resolvedDecoderType = assembly.GetType(PfimageTypeName, false) ?? assembly.GetType(PfimTypeName, false);
            if (resolvedDecoderType is not Type decoderType) {
                throw new InvalidOperationException("The Pfim decoder type could not be resolved.");
            }

            object resolvedFromStreamMethod = decoderType.GetMethod(
                "FromStream",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(Stream) },
                null);
            if (resolvedFromStreamMethod is not MethodInfo fromStreamMethod) {
                throw new InvalidOperationException("The Pfim decoder does not expose a supported FromStream overload.");
            }

            object decodedImage = fromStreamMethod.Invoke(null, new object[] { stream });
            if (decodedImage == null) {
                throw new InvalidOperationException("The Pfim decoder returned null.");
            }

            InvokeOptionalMethod(decodedImage, "Decompress");
            if (decodedImage is IDisposable disposableImage) {
                return disposableImage;
            }

            throw new InvalidOperationException("The Pfim decoder did not return a disposable decoded image.");
        }

        /// <summary>
        /// Converts one decoded Pfim image into an engine texture asset.
        /// </summary>
        /// <param name="decodedImage">Decoded Pfim image instance.</param>
        /// <returns>Texture asset in RGBA byte order.</returns>
        TextureAsset ConvertDecodedImage(object decodedImage) {
            int width = Convert.ToInt32(GetRequiredPropertyValue(decodedImage, "Width"));
            int height = Convert.ToInt32(GetRequiredPropertyValue(decodedImage, "Height"));
            int stride = Convert.ToInt32(GetRequiredPropertyValue(decodedImage, "Stride"));
            byte[] sourceBytes = (byte[])GetRequiredPropertyValue(decodedImage, "Data");
            string formatName = Convert.ToString(GetRequiredPropertyValue(decodedImage, "Format"));
            if (string.IsNullOrWhiteSpace(formatName)) {
                throw new InvalidOperationException("The Pfim decoder returned an empty format name.");
            }
            ValidateDimensions(width, height);

            if (string.Equals(formatName, "Rgba32", StringComparison.OrdinalIgnoreCase)) {
                return CreateTextureAsset(width, height, CopyRgba32(sourceBytes, width, height, stride));
            } else if (string.Equals(formatName, "Bgra32", StringComparison.OrdinalIgnoreCase)) {
                return CreateTextureAsset(width, height, ConvertBgra32ToRgba(sourceBytes, width, height, stride));
            } else if (string.Equals(formatName, "Rgb24", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(formatName, "Bgr24", StringComparison.OrdinalIgnoreCase)) {
                return CreateTextureAsset(width, height, ConvertBgr24ToRgba(sourceBytes, width, height, stride));
            } else if (formatName.IndexOf("5g6b5", StringComparison.OrdinalIgnoreCase) >= 0) {
                return CreateTextureAsset(width, height, ConvertRgb565ToRgba(sourceBytes, width, height, stride));
            } else if (formatName.IndexOf("5g5b5", StringComparison.OrdinalIgnoreCase) >= 0) {
                return CreateTextureAsset(width, height, ConvertRgb555ToRgba(sourceBytes, width, height, stride));
            } else if (string.Equals(formatName, "Gray8", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(formatName, "Grey8", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(formatName, "R8", StringComparison.OrdinalIgnoreCase)) {
                return CreateTextureAsset(width, height, ConvertGray8ToRgba(sourceBytes, width, height, stride));
            }

            throw new NotSupportedException($"Pfim image format '{formatName}' is not supported by the engine texture importer.");
        }

        /// <summary>
        /// Invokes one optional method on the supplied instance when that method exists.
        /// </summary>
        /// <param name="instance">Object exposing the optional method.</param>
        /// <param name="methodName">Method name to invoke.</param>
        void InvokeOptionalMethod(object instance, string methodName) {
            if (instance == null) {
                throw new ArgumentNullException(nameof(instance));
            }

            if (string.IsNullOrWhiteSpace(methodName)) {
                throw new ArgumentException("Method name must be provided.", nameof(methodName));
            }

            object resolvedMethod = instance.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (resolvedMethod is MethodInfo method) {
                method.Invoke(instance, Array.Empty<object>());
            }
        }

        /// <summary>
        /// Retrieves one required reflected property value.
        /// </summary>
        /// <param name="instance">Object exposing the property.</param>
        /// <param name="propertyName">Property name to resolve.</param>
        /// <returns>Resolved property value.</returns>
        object GetRequiredPropertyValue(object instance, string propertyName) {
            if (instance == null) {
                throw new ArgumentNullException(nameof(instance));
            }

            if (string.IsNullOrWhiteSpace(propertyName)) {
                throw new ArgumentException("Property name must be provided.", nameof(propertyName));
            }

            object resolvedProperty = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (resolvedProperty is not PropertyInfo property) {
                throw new InvalidOperationException($"Required property '{propertyName}' was not found on '{instance.GetType().FullName}'.");
            }

            object value = property.GetValue(instance);
            if (value == null) {
                throw new InvalidOperationException($"Required property '{propertyName}' resolved to null on '{instance.GetType().FullName}'.");
            }

            return value;
        }

        /// <summary>
        /// Validates decoded image dimensions against engine texture limits.
        /// </summary>
        /// <param name="width">Decoded image width.</param>
        /// <param name="height">Decoded image height.</param>
        void ValidateDimensions(int width, int height) {
            if (width <= 0 || height <= 0) {
                throw new InvalidOperationException("Texture dimensions must be positive.");
            }

            if (width > ushort.MaxValue || height > ushort.MaxValue) {
                throw new InvalidOperationException("Texture dimensions exceed supported limits.");
            }
        }

        /// <summary>
        /// Creates a texture asset wrapper around RGBA bytes.
        /// </summary>
        /// <param name="width">Texture width in pixels.</param>
        /// <param name="height">Texture height in pixels.</param>
        /// <param name="colors">Texture bytes in RGBA order.</param>
        /// <returns>Texture asset instance.</returns>
        TextureAsset CreateTextureAsset(int width, int height, byte[] colors) {
            if (colors == null) {
                throw new ArgumentNullException(nameof(colors));
            }

            return new TextureAsset {
                Width = (ushort)width,
                Height = (ushort)height,
                Colors = colors
            };
        }

        /// <summary>
        /// Copies 32-bit RGBA rows into tightly packed RGBA bytes.
        /// </summary>
        /// <param name="sourceBytes">Source image bytes.</param>
        /// <param name="width">Image width in pixels.</param>
        /// <param name="height">Image height in pixels.</param>
        /// <param name="stride">Source row stride in bytes.</param>
        /// <returns>RGBA bytes.</returns>
        byte[] CopyRgba32(byte[] sourceBytes, int width, int height, int stride) {
            byte[] colors = new byte[width * height * 4];
            for (int y = 0; y < height; y++) {
                int sourceRowOffset = y * stride;
                int destinationRowOffset = y * width * 4;
                Buffer.BlockCopy(sourceBytes, sourceRowOffset, colors, destinationRowOffset, width * 4);
            }

            return colors;
        }

        /// <summary>
        /// Converts 32-bit BGRA rows into tightly packed RGBA bytes.
        /// </summary>
        /// <param name="sourceBytes">Source image bytes.</param>
        /// <param name="width">Image width in pixels.</param>
        /// <param name="height">Image height in pixels.</param>
        /// <param name="stride">Source row stride in bytes.</param>
        /// <returns>RGBA bytes.</returns>
        byte[] ConvertBgra32ToRgba(byte[] sourceBytes, int width, int height, int stride) {
            byte[] colors = new byte[width * height * 4];
            for (int y = 0; y < height; y++) {
                int sourceRowOffset = y * stride;
                int destinationRowOffset = y * width * 4;
                for (int x = 0; x < width; x++) {
                    int sourceIndex = sourceRowOffset + (x * 4);
                    int destinationIndex = destinationRowOffset + (x * 4);
                    colors[destinationIndex] = sourceBytes[sourceIndex + 2];
                    colors[destinationIndex + 1] = sourceBytes[sourceIndex + 1];
                    colors[destinationIndex + 2] = sourceBytes[sourceIndex];
                    colors[destinationIndex + 3] = sourceBytes[sourceIndex + 3];
                }
            }

            return colors;
        }

        /// <summary>
        /// Converts 24-bit BGR rows into tightly packed RGBA bytes.
        /// </summary>
        /// <param name="sourceBytes">Source image bytes.</param>
        /// <param name="width">Image width in pixels.</param>
        /// <param name="height">Image height in pixels.</param>
        /// <param name="stride">Source row stride in bytes.</param>
        /// <returns>RGBA bytes.</returns>
        byte[] ConvertBgr24ToRgba(byte[] sourceBytes, int width, int height, int stride) {
            byte[] colors = new byte[width * height * 4];
            for (int y = 0; y < height; y++) {
                int sourceRowOffset = y * stride;
                int destinationRowOffset = y * width * 4;
                for (int x = 0; x < width; x++) {
                    int sourceIndex = sourceRowOffset + (x * 3);
                    int destinationIndex = destinationRowOffset + (x * 4);
                    colors[destinationIndex] = sourceBytes[sourceIndex + 2];
                    colors[destinationIndex + 1] = sourceBytes[sourceIndex + 1];
                    colors[destinationIndex + 2] = sourceBytes[sourceIndex];
                    colors[destinationIndex + 3] = 255;
                }
            }

            return colors;
        }

        /// <summary>
        /// Converts 16-bit RGB565 rows into tightly packed RGBA bytes.
        /// </summary>
        /// <param name="sourceBytes">Source image bytes.</param>
        /// <param name="width">Image width in pixels.</param>
        /// <param name="height">Image height in pixels.</param>
        /// <param name="stride">Source row stride in bytes.</param>
        /// <returns>RGBA bytes.</returns>
        byte[] ConvertRgb565ToRgba(byte[] sourceBytes, int width, int height, int stride) {
            byte[] colors = new byte[width * height * 4];
            for (int y = 0; y < height; y++) {
                int sourceRowOffset = y * stride;
                int destinationRowOffset = y * width * 4;
                for (int x = 0; x < width; x++) {
                    int sourceIndex = sourceRowOffset + (x * 2);
                    ushort packed = (ushort)(sourceBytes[sourceIndex] | (sourceBytes[sourceIndex + 1] << 8));
                    byte red = (byte)(((packed >> 11) & 0x1F) * 255 / 31);
                    byte green = (byte)(((packed >> 5) & 0x3F) * 255 / 63);
                    byte blue = (byte)((packed & 0x1F) * 255 / 31);
                    int destinationIndex = destinationRowOffset + (x * 4);
                    colors[destinationIndex] = red;
                    colors[destinationIndex + 1] = green;
                    colors[destinationIndex + 2] = blue;
                    colors[destinationIndex + 3] = 255;
                }
            }

            return colors;
        }

        /// <summary>
        /// Converts 16-bit RGB555 rows into tightly packed RGBA bytes.
        /// </summary>
        /// <param name="sourceBytes">Source image bytes.</param>
        /// <param name="width">Image width in pixels.</param>
        /// <param name="height">Image height in pixels.</param>
        /// <param name="stride">Source row stride in bytes.</param>
        /// <returns>RGBA bytes.</returns>
        byte[] ConvertRgb555ToRgba(byte[] sourceBytes, int width, int height, int stride) {
            byte[] colors = new byte[width * height * 4];
            for (int y = 0; y < height; y++) {
                int sourceRowOffset = y * stride;
                int destinationRowOffset = y * width * 4;
                for (int x = 0; x < width; x++) {
                    int sourceIndex = sourceRowOffset + (x * 2);
                    ushort packed = (ushort)(sourceBytes[sourceIndex] | (sourceBytes[sourceIndex + 1] << 8));
                    byte red = (byte)(((packed >> 10) & 0x1F) * 255 / 31);
                    byte green = (byte)(((packed >> 5) & 0x1F) * 255 / 31);
                    byte blue = (byte)((packed & 0x1F) * 255 / 31);
                    int destinationIndex = destinationRowOffset + (x * 4);
                    colors[destinationIndex] = red;
                    colors[destinationIndex + 1] = green;
                    colors[destinationIndex + 2] = blue;
                    colors[destinationIndex + 3] = 255;
                }
            }

            return colors;
        }

        /// <summary>
        /// Converts 8-bit grayscale rows into tightly packed RGBA bytes.
        /// </summary>
        /// <param name="sourceBytes">Source image bytes.</param>
        /// <param name="width">Image width in pixels.</param>
        /// <param name="height">Image height in pixels.</param>
        /// <param name="stride">Source row stride in bytes.</param>
        /// <returns>RGBA bytes.</returns>
        byte[] ConvertGray8ToRgba(byte[] sourceBytes, int width, int height, int stride) {
            byte[] colors = new byte[width * height * 4];
            for (int y = 0; y < height; y++) {
                int sourceRowOffset = y * stride;
                int destinationRowOffset = y * width * 4;
                for (int x = 0; x < width; x++) {
                    byte value = sourceBytes[sourceRowOffset + x];
                    int destinationIndex = destinationRowOffset + (x * 4);
                    colors[destinationIndex] = value;
                    colors[destinationIndex + 1] = value;
                    colors[destinationIndex + 2] = value;
                    colors[destinationIndex + 3] = 255;
                }
            }

            return colors;
        }
    }
}
