using helengine.ui.format;

namespace helengine.editor {
    /// <summary>
    /// Validates standalone UI documents before copying their original bytes into packaged runtime content.
    /// </summary>
    public sealed class UiBinaryAssetCooker : IPackagedAssetCooker {
        /// <summary>
        /// Gets the standalone UI document extension handled by the shared portable codec.
        /// </summary>
        public string Extension => ".heui";

        /// <summary>
        /// Reads only bounded input, validates the complete document and packages the identical binary payload.
        /// </summary>
        /// <param name="reference">Authored scene reference to the standalone UI document.</param>
        /// <param name="context">Current packaging paths, tracked writers and reference factory.</param>
        /// <returns>Scene reference to the validated binary beneath the cooked content root.</returns>
        public SceneAssetReference Cook(SceneAssetReference reference, PackagedAssetCookContext context) {
            if (reference == null) {
                throw new ArgumentNullException(nameof(reference));
            } else if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            byte[] bytes;
            using (FileStream stream = File.OpenRead(context.ProjectAssetFullPath)) {
                long length = stream.Length;
                if (length < 20 || length > UiFormatLimits.Bytes) {
                    throw new FormatException($"UI asset '{reference.RelativePath}' has an invalid document length ({length} bytes).");
                }

                bytes = new byte[(int)length];
                stream.ReadExactly(bytes);
                if (stream.ReadByte() != -1) {
                    throw new FormatException($"UI asset '{reference.RelativePath}' changed length while being read.");
                }
            }

            using (UiDocument document = UiBinaryCodec.Read(bytes)) {
                // Validation owns a disposable parsed model; the package preserves the authored binary exactly.
            }

            string packagedPath = "cooked/" + CanonicalPackagedAssetPath.Normalize(reference.RelativePath);
            context.WriteBytes(packagedPath, bytes);
            return context.CreatePackagedReference(packagedPath);
        }
    }
}
