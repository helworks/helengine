using System.Reflection;

namespace helengine.editor {
    /// <summary>
    /// Texture importer that loads raster image formats through Magick.NET.
    /// </summary>
    public sealed class MagickTextureImporter : ITextureImporter {
        /// <summary>
        /// Simple assembly names probed when resolving the Magick.NET runtime.
        /// </summary>
        static readonly string[] MagickAssemblyNames = new[] {
            "Magick.NET-Q8-AnyCPU",
            "Magick.NET-Q8-x64",
            "Magick.NET-Q8-x86"
        };

        /// <summary>
        /// Fully qualified type name for the Magick image wrapper.
        /// </summary>
        const string MagickImageTypeName = "ImageMagick.MagickImage";

        /// <summary>
        /// Imports one texture asset from the supplied source stream.
        /// </summary>
        /// <param name="stream">Stream containing source image bytes.</param>
        /// <returns>Imported texture asset in RGBA byte order.</returns>
        public TextureAsset ImportTexture(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            object image = CreateMagickImage(stream);
            IDisposable disposableImage = image as IDisposable;
            try {
                return ConvertImage(image);
            } finally {
                disposableImage?.Dispose();
            }
        }

        /// <summary>
        /// Creates a Magick image wrapper for the supplied stream.
        /// </summary>
        /// <param name="stream">Stream containing source image bytes.</param>
        /// <returns>Magick image object.</returns>
        object CreateMagickImage(Stream stream) {
            Type magickImageType = ResolveMagickImageType();
            object resolvedStreamConstructor = magickImageType.GetConstructor(new[] { typeof(Stream) });
            if (resolvedStreamConstructor is ConstructorInfo streamConstructor) {
                object image = streamConstructor.Invoke(new object[] { stream });
                if (image == null) {
                    throw new InvalidOperationException("Magick.NET returned a null image instance.");
                }

                return image;
            }

            object resolvedByteArrayConstructor = magickImageType.GetConstructor(new[] { typeof(byte[]) });
            if (resolvedByteArrayConstructor is not ConstructorInfo byteArrayConstructor) {
                throw new InvalidOperationException("Magick.NET does not expose a supported stream or byte-array image constructor.");
            }

            byte[] sourceBytes = ReadAllBytes(stream);
            object byteArrayImage = byteArrayConstructor.Invoke(new object[] { sourceBytes });
            if (byteArrayImage == null) {
                throw new InvalidOperationException("Magick.NET returned a null image instance.");
            }

            return byteArrayImage;
        }

        /// <summary>
        /// Resolves the Magick image type from the lazily loaded Magick.NET assemblies.
        /// </summary>
        /// <returns>Resolved Magick image type.</returns>
        Type ResolveMagickImageType() {
            for (int index = 0; index < MagickAssemblyNames.Length; index++) {
                try {
                    Assembly assembly = Assembly.Load(MagickAssemblyNames[index]);
                    object resolvedMagickImageType = assembly.GetType(MagickImageTypeName, false);
                    if (resolvedMagickImageType is Type magickImageType) {
                        return magickImageType;
                    }
                } catch (FileNotFoundException) {
                } catch (FileLoadException) {
                }
            }

            throw new InvalidOperationException("The Magick.NET runtime assembly could not be resolved.");
        }

        /// <summary>
        /// Converts a Magick image object into an engine texture asset.
        /// </summary>
        /// <param name="image">Magick image object.</param>
        /// <returns>Texture asset in RGBA byte order.</returns>
        TextureAsset ConvertImage(object image) {
            int width = Convert.ToInt32(GetRequiredPropertyValue(image, "Width"));
            int height = Convert.ToInt32(GetRequiredPropertyValue(image, "Height"));
            ValidateDimensions(width, height);

            object pixels = InvokeRequiredMethod(image, "GetPixels", Type.EmptyTypes, Array.Empty<object>());
            IDisposable disposablePixels = pixels as IDisposable;
            try {
                object colorBytes = InvokeRequiredMethod(pixels, "ToByteArray", new[] { typeof(string) }, new object[] { "RGBA" });
                if (colorBytes is byte[] colors) {
                    return new TextureAsset {
                        Width = (ushort)width,
                        Height = (ushort)height,
                        Colors = colors
                    };
                }

                throw new InvalidOperationException("Magick.NET did not return byte[] pixel data for RGBA export.");
            } finally {
                disposablePixels?.Dispose();
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
        /// Invokes one required reflected method and validates that a result was returned.
        /// </summary>
        /// <param name="instance">Object exposing the method.</param>
        /// <param name="methodName">Method name to invoke.</param>
        /// <param name="parameterTypes">Exact parameter types used to resolve the method.</param>
        /// <param name="arguments">Method arguments.</param>
        /// <returns>Method return value.</returns>
        object InvokeRequiredMethod(object instance, string methodName, Type[] parameterTypes, object[] arguments) {
            if (instance == null) {
                throw new ArgumentNullException(nameof(instance));
            }

            if (string.IsNullOrWhiteSpace(methodName)) {
                throw new ArgumentException("Method name must be provided.", nameof(methodName));
            }

            if (parameterTypes == null) {
                throw new ArgumentNullException(nameof(parameterTypes));
            }

            if (arguments == null) {
                throw new ArgumentNullException(nameof(arguments));
            }

            object resolvedMethod = instance.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance, null, parameterTypes, null);
            if (resolvedMethod is not MethodInfo method) {
                throw new InvalidOperationException($"Required method '{methodName}' was not found on '{instance.GetType().FullName}'.");
            }

            object result = method.Invoke(instance, arguments);
            if (result == null) {
                throw new InvalidOperationException($"Required method '{methodName}' returned null on '{instance.GetType().FullName}'.");
            }

            return result;
        }

        /// <summary>
        /// Reads the remaining bytes from the supplied stream.
        /// </summary>
        /// <param name="stream">Stream containing source image bytes.</param>
        /// <returns>Byte array containing the full stream payload.</returns>
        byte[] ReadAllBytes(Stream stream) {
            using MemoryStream buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
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
    }
}
