namespace helengine.editor {
    /// <summary>
    /// Classifies authored project files and applies the editor's shared sidecar visibility rules.
    /// </summary>
    public sealed class EditorAssetPathClassifier {
        /// <summary>
        /// Extension used for asset import settings sidecar files.
        /// </summary>
        const string ImportSettingsExtension = ".hasset";

        /// <summary>
        /// Extension used for authored identity metadata sidecars.
        /// </summary>
        const string IdentityMetadataExtension = ".hmeta";

        /// <summary>
        /// Classifies one file path into the shared editor asset category.
        /// </summary>
        /// <param name="fullPath">Absolute or project-relative file path.</param>
        /// <returns>Shared asset category.</returns>
        public AssetEntryKind Classify(string fullPath) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                return AssetEntryKind.Unknown;
            }

            string extension = Path.GetExtension(fullPath);
            if (string.IsNullOrEmpty(extension)) {
                return AssetEntryKind.Unknown;
            }

            if (string.Equals(extension, ImportSettingsExtension, StringComparison.OrdinalIgnoreCase)) {
                AssetEntryKind hassetKind;
                return TryClassifyHassetFile(fullPath, out hassetKind) ? hassetKind : AssetEntryKind.File;
            }

            if (new HashSet<string>(TextureImportFormatCatalog.AllTextureExtensions, StringComparer.OrdinalIgnoreCase).Contains(extension)) {
                return AssetEntryKind.Image;
            }
            if (new HashSet<string>(AssimpModelFormatCatalog.AllModelExtensions, StringComparer.OrdinalIgnoreCase).Contains(extension)) {
                return AssetEntryKind.Model;
            }
            if (string.Equals(extension, EditorFileTemplateRegistry.MaterialExtension, StringComparison.OrdinalIgnoreCase)) {
                return AssetEntryKind.Material;
            }
            if (string.Equals(extension, SceneAsset.FileExtension, StringComparison.OrdinalIgnoreCase)) {
                return AssetEntryKind.Scene;
            }
            if (string.Equals(extension, BlueprintAsset.FileExtension, StringComparison.OrdinalIgnoreCase)) {
                return AssetEntryKind.Blueprint;
            }
            if (new HashSet<string>(new[] { ".wav", ".mp3", ".ogg", ".flac", ".aac" }, StringComparer.OrdinalIgnoreCase).Contains(extension)) {
                return AssetEntryKind.Audio;
            }
            if (new HashSet<string>(new[] { ".cs", ".js", ".lua", ".py" }, StringComparer.OrdinalIgnoreCase).Contains(extension)) {
                return AssetEntryKind.Script;
            }
            if (new HashSet<string>(new[] { ".json", ".xml", ".yaml", ".yml" }, StringComparer.OrdinalIgnoreCase).Contains(extension)) {
                return AssetEntryKind.Config;
            }
            if (new HashSet<string>(new[] { ".ttf", ".otf" }, StringComparer.OrdinalIgnoreCase).Contains(extension)) {
                return AssetEntryKind.Font;
            }

            return AssetEntryKind.File;
        }

        /// <summary>
        /// Determines whether one file should be hidden from the asset browser and identity index.
        /// </summary>
        /// <param name="fullPath">Absolute or project-relative file path.</param>
        /// <returns>True when the file is an editor-only sidecar.</returns>
        public bool ShouldHide(string fullPath) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                return true;
            }

            string extension = Path.GetExtension(fullPath);
            if (string.Equals(extension, IdentityMetadataExtension, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
            if (!string.Equals(extension, ImportSettingsExtension, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            AssetEntryKind hassetKind;
            if (!TryClassifyHassetFile(fullPath, out hassetKind)) {
                return true;
            }

            return hassetKind != AssetEntryKind.Material;
        }

        /// <summary>
        /// Determines whether one path is an authored source eligible for identity indexing.
        /// </summary>
        /// <param name="fullPath">Absolute or project-relative file path.</param>
        /// <returns>True when the path is not an editor-only sidecar.</returns>
        public bool IsAuthoredAsset(string fullPath) {
            return !ShouldHide(fullPath) && File.Exists(fullPath);
        }

        /// <summary>
        /// Determines whether one authored file stores identity metadata inside its engine-native payload.
        /// </summary>
        /// <param name="fullPath">Absolute or project-relative authored file path.</param>
        /// <returns>True for native scene, blueprint, and material containers.</returns>
        public bool UsesEmbeddedIdentity(string fullPath) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                return false;
            }

            string extension = Path.GetExtension(fullPath);
            if (string.Equals(extension, SceneAsset.FileExtension, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(extension, BlueprintAsset.FileExtension, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }
            if (!string.Equals(extension, ImportSettingsExtension, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath)) {
                return false;
            }

            try {
                using FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
                return header.FormatId == global::helengine.files.EditorAssetBinarySerializer.FormatId &&
                    ((header.RecordKind == (ushort)EditorBinaryRecordKind.Asset && header.ValueKind == (ushort)EditorAssetBinaryValueKind.MaterialAsset) ||
                     (header.RecordKind == (ushort)EditorBinaryRecordKind.AssetImportSettings && header.ValueKind == (ushort)AssetImportSettingsBinaryValueKind.MaterialAssetCommonSettingsDocument));
            } catch {
                return false;
            }
        }

        /// <summary>
        /// Classifies one `.hasset` file by its HELE header.
        /// </summary>
        /// <param name="filePath">Absolute `.hasset` path.</param>
        /// <param name="entryKind">Classified entry kind.</param>
        /// <returns>True when the header was read successfully.</returns>
        bool TryClassifyHassetFile(string filePath, out AssetEntryKind entryKind) {
            if (string.IsNullOrWhiteSpace(filePath)) {
                entryKind = AssetEntryKind.Unknown;
                return false;
            }

            try {
                using FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
                if (header.FormatId != global::helengine.files.EditorAssetBinarySerializer.FormatId) {
                    entryKind = AssetEntryKind.File;
                    return true;
                }
                if (header.RecordKind == (ushort)EditorBinaryRecordKind.Asset && header.ValueKind == (ushort)EditorAssetBinaryValueKind.MaterialAsset) {
                    entryKind = AssetEntryKind.Material;
                    return true;
                }
                if (header.RecordKind == (ushort)EditorBinaryRecordKind.AssetImportSettings && header.ValueKind == (ushort)AssetImportSettingsBinaryValueKind.MaterialAssetCommonSettingsDocument) {
                    entryKind = AssetEntryKind.Material;
                    return true;
                }

                entryKind = AssetEntryKind.File;
                return true;
            } catch {
                entryKind = AssetEntryKind.Unknown;
                return false;
            }
        }
    }
}
