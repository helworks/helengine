using System.Reflection;

namespace helengine.editor.windows.tests.content.textures {
    /// <summary>
    /// Encodes tiny real image fixtures through Magick.NET using reflection so tests stay decoupled from compile-time Magick types.
    /// </summary>
    public static class MagickFixtureEncoder {
        /// <summary>
        /// Candidate assembly names used to resolve the Magick.NET runtime.
        /// </summary>
        static readonly string[] MagickAssemblyNames = new[] {
            "Magick.NET-Q8-AnyCPU",
            "Magick.NET-Q8-x64",
            "Magick.NET-Q8-x86",
            "Magick.NET.Core"
        };

        /// <summary>
        /// Fully qualified Magick image type name.
        /// </summary>
        const string MagickImageTypeName = "ImageMagick.MagickImage";

        /// <summary>
        /// Encodes one source image payload into the requested Magick file format.
        /// </summary>
        /// <param name="sourceImageBytes">Encoded source image bytes.</param>
        /// <param name="formatName">Target Magick format name.</param>
        /// <returns>Encoded image bytes.</returns>
        public static byte[] EncodeToFormat(byte[] sourceImageBytes, string formatName) {
            if (sourceImageBytes == null) {
                throw new ArgumentNullException(nameof(sourceImageBytes));
            } else if (sourceImageBytes.Length == 0) {
                throw new ArgumentException("Source image bytes must not be empty.", nameof(sourceImageBytes));
            } else if (string.IsNullOrWhiteSpace(formatName)) {
                throw new ArgumentException("Format name must be provided.", nameof(formatName));
            }

            Type magickImageType = ResolveRequiredType(MagickImageTypeName);
            object image = CreateMagickImage(magickImageType, sourceImageBytes);
            IDisposable disposableImage = image as IDisposable;
            try {
                SetFormat(image, formatName);
                return WriteImageToBytes(image);
            } finally {
                disposableImage?.Dispose();
            }
        }

        /// <summary>
        /// Resolves one required Magick type from the runtime assemblies.
        /// </summary>
        /// <param name="fullTypeName">Fully qualified type name to resolve.</param>
        /// <returns>Resolved runtime type.</returns>
        static Type ResolveRequiredType(string fullTypeName) {
            if (string.IsNullOrWhiteSpace(fullTypeName)) {
                throw new ArgumentException("Type name must be provided.", nameof(fullTypeName));
            }

            for (int index = 0; index < MagickAssemblyNames.Length; index++) {
                try {
                    Assembly assembly = Assembly.Load(MagickAssemblyNames[index]);
                    object resolvedType = assembly.GetType(fullTypeName, false);
                    if (resolvedType is Type runtimeType) {
                        return runtimeType;
                    }
                } catch (FileNotFoundException) {
                } catch (FileLoadException) {
                }
            }

            throw new InvalidOperationException($"Magick.NET type '{fullTypeName}' could not be resolved.");
        }

        /// <summary>
        /// Creates one Magick image from encoded source image bytes.
        /// </summary>
        /// <param name="magickImageType">Runtime Magick image type.</param>
        /// <param name="sourceImageBytes">Encoded source image bytes.</param>
        /// <returns>Created Magick image instance.</returns>
        static object CreateMagickImage(Type magickImageType, byte[] sourceImageBytes) {
            if (magickImageType == null) {
                throw new ArgumentNullException(nameof(magickImageType));
            } else if (sourceImageBytes == null) {
                throw new ArgumentNullException(nameof(sourceImageBytes));
            }

            object image = Activator.CreateInstance(magickImageType, new object[] { sourceImageBytes });
            if (image == null) {
                throw new InvalidOperationException("Magick.NET returned a null image instance while encoding a fixture.");
            }

            return image;
        }

        /// <summary>
        /// Sets the requested output format on one Magick image.
        /// </summary>
        /// <param name="image">Magick image instance.</param>
        /// <param name="formatName">Target Magick format name.</param>
        static void SetFormat(object image, string formatName) {
            if (image == null) {
                throw new ArgumentNullException(nameof(image));
            } else if (string.IsNullOrWhiteSpace(formatName)) {
                throw new ArgumentException("Format name must be provided.", nameof(formatName));
            }

            object resolvedProperty = image.GetType().GetProperty("Format", BindingFlags.Public | BindingFlags.Instance);
            if (resolvedProperty is not PropertyInfo formatProperty) {
                throw new InvalidOperationException("Magick.NET does not expose a writable Format property.");
            }

            object formatValue = Enum.Parse(formatProperty.PropertyType, formatName, true);
            formatProperty.SetValue(image, formatValue);
        }

        /// <summary>
        /// Serializes one Magick image instance into a byte array through the stream write path.
        /// </summary>
        /// <param name="image">Magick image instance.</param>
        /// <returns>Encoded image bytes.</returns>
        static byte[] WriteImageToBytes(object image) {
            if (image == null) {
                throw new ArgumentNullException(nameof(image));
            }

            object resolvedWriteMethod = image.GetType().GetMethod("Write", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Stream) }, null);
            if (resolvedWriteMethod is not MethodInfo writeMethod) {
                throw new InvalidOperationException("Magick.NET does not expose a Write(Stream) method.");
            }

            using MemoryStream stream = new MemoryStream();
            writeMethod.Invoke(image, new object[] { stream });
            return stream.ToArray();
        }
    }
}
