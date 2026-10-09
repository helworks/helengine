namespace helengine.timeline {
    /// <summary>
    /// Creates and stores the <see cref="SceneAssetReference"/> values timelines use for audio, animation and nested
    /// timeline assets. Core only exposes its validated construction path through the binary reference layout, so this
    /// class builds references by encoding the fields in that layout and reading them back through
    /// <see cref="SceneAssetReferenceFactory.ReadOptionalReference"/>; every reference therefore passes the same
    /// validation as one loaded from a scene, without core knowing about timelines.
    /// </summary>
    public static class TimelineAssetReferences {
        /// <summary>
        /// Creates a path-only file-system reference, the form authored JSON normally uses.
        /// </summary>
        /// <param name="relativePath">Project-relative asset path.</param>
        /// <returns>A validated reference.</returns>
        public static SceneAssetReference FromPath(string relativePath) {
            return Create(SceneAssetReferenceSourceKind.FileSystem, relativePath, string.Empty, string.Empty, string.Empty);
        }

        /// <summary>
        /// Creates a reference from its five canonical fields.
        /// </summary>
        /// <param name="sourceKind">File-system or generated source.</param>
        /// <param name="relativePath">Project-relative path; required.</param>
        /// <param name="providerId">Generated provider id; required for generated references, empty otherwise.</param>
        /// <param name="assetId">Stable asset id; with a content hash for canonical file-system references, required for
        /// generated references.</param>
        /// <param name="contentHash">sha256 content hash of canonical file-system references; empty otherwise.</param>
        /// <returns>A validated reference.</returns>
        /// <exception cref="ArgumentException">The fields do not form a valid reference.</exception>
        public static SceneAssetReference Create(SceneAssetReferenceSourceKind sourceKind, string relativePath, string providerId, string assetId, string contentHash) {
            using MemoryStream stream = new MemoryStream();
            using (helengine.files.EngineBinaryWriter writer = helengine.files.EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian)) {
                writer.WriteByte(1);
                writer.WriteInt32((int)sourceKind);
                writer.WriteString(relativePath ?? string.Empty);
                writer.WriteString(providerId ?? string.Empty);
                writer.WriteString(assetId ?? string.Empty);
                writer.WriteString(contentHash ?? string.Empty);
            }
            stream.Position = 0;
            using EngineBinaryReader reader = EngineBinaryReader.Create(stream, EngineBinaryEndianness.LittleEndian);
            try {
                return SceneAssetReferenceFactory.ReadOptionalReference(reader);
            } catch (InvalidOperationException exception) {
                throw new ArgumentException(exception.Message, nameof(sourceKind), exception);
            }
        }

        /// <summary>
        /// Writes an optional reference: a presence byte followed by the canonical five-field layout scenes use.
        /// </summary>
        /// <param name="writer">Destination writer.</param>
        /// <param name="reference">Reference to write; null writes only the absent marker.</param>
        public static void WriteOptional(helengine.files.EngineBinaryWriter writer, SceneAssetReference reference) {
            if (reference == null) {
                writer.WriteByte(0);
                return;
            }
            writer.WriteByte(1);
            helengine.files.SceneEntityPayloadFormat.WriteSceneAssetReference(writer, reference);
        }

        /// <summary>
        /// Reads an optional reference written by <see cref="WriteOptional"/>.
        /// </summary>
        /// <param name="reader">Source reader.</param>
        /// <returns>The validated reference, or null when absent.</returns>
        public static SceneAssetReference ReadOptional(EngineBinaryReader reader) {
            return SceneAssetReferenceFactory.ReadOptionalReference(reader);
        }
    }
}
