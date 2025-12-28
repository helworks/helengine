namespace helengine.editor {
    /// <summary>
    /// Manages asset importer registration and sidecar import settings.
    /// </summary>
    public class AssetImportManager {
        /// <summary>
        /// File extension for import settings sidecar files.
        /// </summary>
        const string SettingsExtension = ".hasset";

        /// <summary>
        /// File extension used for serialized texture assets.
        /// </summary>
        const string TextureAssetExtension = ".texture.asset";

        /// <summary>
        /// Folder name used for imported asset outputs.
        /// </summary>
        const string ImportFolderName = "cache";

        /// <summary>
        /// Root path for the project directory.
        /// </summary>
        readonly string projectRootPath;

        /// <summary>
        /// Root path for the project assets directory.
        /// </summary>
        readonly string assetsRootPath;

        /// <summary>
        /// Root path for imported asset outputs.
        /// </summary>
        readonly string importRootPath;

        /// <summary>
        /// Normalized import root prefix with a trailing directory separator.
        /// </summary>
        readonly string importRootPrefix;

        /// <summary>
        /// Registered texture importers keyed by identifier.
        /// </summary>
        readonly Dictionary<string, ITextureImporter> textureImportersById;

        /// <summary>
        /// Default texture importer identifiers keyed by extension.
        /// </summary>
        readonly Dictionary<string, string> defaultTextureImportersByExtension;

        /// <summary>
        /// File hasher used to generate content checksums.
        /// </summary>
        readonly AssetFileHasher fileHasher;

        /// <summary>
        /// Initializes a new asset import manager for a project.
        /// </summary>
        /// <param name="projectRootPath">Absolute path to the project root.</param>
        public AssetImportManager(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            this.projectRootPath = Path.GetFullPath(projectRootPath);
            assetsRootPath = Path.Combine(this.projectRootPath, "assets");
            importRootPath = Path.Combine(this.projectRootPath, ImportFolderName);
            importRootPrefix = importRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            textureImportersById = new Dictionary<string, ITextureImporter>(StringComparer.OrdinalIgnoreCase);
            defaultTextureImportersByExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            fileHasher = new AssetFileHasher();

            Directory.CreateDirectory(this.projectRootPath);
            Directory.CreateDirectory(assetsRootPath);
            Directory.CreateDirectory(importRootPath);
        }

        /// <summary>
        /// Gets the project assets root path.
        /// </summary>
        public string AssetsRootPath => assetsRootPath;

        /// <summary>
        /// Gets the root path where imported assets are stored.
        /// </summary>
        public string ImportRootPath => importRootPath;

        /// <summary>
        /// Registers a texture importer and records its supported extensions.
        /// </summary>
        /// <param name="registration">Importer registration data.</param>
        public void RegisterTextureImporter(TextureImporterRegistration registration) {
            if (registration == null) {
                throw new ArgumentNullException(nameof(registration));
            }

            if (textureImportersById.ContainsKey(registration.ImporterId)) {
                throw new InvalidOperationException($"Texture importer '{registration.ImporterId}' is already registered.");
            }

            textureImportersById.Add(registration.ImporterId, registration.Importer);
            IReadOnlyList<string> extensions = registration.Extensions;
            for (int i = 0; i < extensions.Count; i++) {
                string extension = NormalizeExtension(extensions[i]);
                if (!defaultTextureImportersByExtension.ContainsKey(extension)) {
                    defaultTextureImportersByExtension[extension] = registration.ImporterId;
                }
            }
        }

        /// <summary>
        /// Sets the default texture importer for a specific extension.
        /// </summary>
        /// <param name="extension">File extension to associate with the importer.</param>
        /// <param name="importerId">Identifier of the importer to use.</param>
        public void SetDefaultTextureImporter(string extension, string importerId) {
            if (string.IsNullOrWhiteSpace(extension)) {
                throw new ArgumentException("Extension must be provided.", nameof(extension));
            }

            if (string.IsNullOrWhiteSpace(importerId)) {
                throw new ArgumentException("Importer id must be provided.", nameof(importerId));
            }

            EnsureTextureImporterExists(importerId);
            defaultTextureImportersByExtension[NormalizeExtension(extension)] = importerId;
        }

        /// <summary>
        /// Imports a texture asset from a source file and writes it to disk.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the texture source file.</param>
        /// <returns>Imported <see cref="TextureAsset"/> instance.</returns>
        public TextureAsset ImportTexture(string sourcePath) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            if (!File.Exists(sourcePath)) {
                throw new FileNotFoundException("Texture source file was not found.", sourcePath);
            }

            AssetImportSettings settings = LoadOrCreateImportSettings(sourcePath);
            EnsureImportSettingsValid(settings);

            ITextureImporter importer = GetTextureImporter(settings.ImporterId);
            TextureAsset asset;
            using (FileStream stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                asset = importer.ImportTexture(stream);
            }

            if (asset == null) {
                throw new InvalidOperationException($"Texture importer '{settings.ImporterId}' did not return an asset.");
            }

            asset.Id = settings.AssetId;

            string outputPath = GetTextureAssetPath(settings.AssetId);
            EnsureDirectoryForFile(outputPath);
            using (FileStream stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, asset);
            }

            SaveImportSettings(sourcePath, settings);
            return asset;
        }

        /// <summary>
        /// Loads import settings for a source file or creates defaults if missing.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <returns>Resolved import settings.</returns>
        public AssetImportSettings LoadOrCreateImportSettings(string sourcePath) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            string settingsPath = GetSettingsPath(sourcePath);
            AssetImportSettings settings = File.Exists(settingsPath)
                ? LoadImportSettings(settingsPath)
                : CreateDefaultSettings(sourcePath);

            UpdateSettingsChecksum(settings, sourcePath);

            return settings;
        }

        /// <summary>
        /// Saves import settings next to the specified source file.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <param name="settings">Settings to serialize.</param>
        public void SaveImportSettings(string sourcePath, AssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            string settingsPath = GetSettingsPath(sourcePath);
            EnsureDirectoryForFile(settingsPath);
            using (FileStream stream = new FileStream(settingsPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                ProtoBuf.Serializer.Serialize(stream, settings);
            }
        }

        /// <summary>
        /// Scans the assets folder and creates missing import settings sidecars.
        /// </summary>
        /// <returns>Paths to settings files created during the scan.</returns>
        public List<string> GenerateMissingImportSettings() {
            List<string> createdSettings = new List<string>();
            foreach (string sourcePath in EnumerateAssetSourceFiles()) {
                string settingsPath = GetSettingsPath(sourcePath);
                if (File.Exists(settingsPath)) {
                    continue;
                }

                AssetImportSettings settings;
                if (!TryCreateDefaultSettings(sourcePath, out settings)) {
                    continue;
                }

                UpdateSettingsChecksum(settings, sourcePath);
                SaveImportSettings(sourcePath, settings);
                createdSettings.Add(settingsPath);
            }

            return createdSettings;
        }

        /// <summary>
        /// Imports textures that do not yet have cached outputs.
        /// </summary>
        /// <returns>Paths to cached assets created during the scan.</returns>
        public List<string> ImportTexturesMissingCache() {
            List<string> importedAssets = new List<string>();
            foreach (string sourcePath in EnumerateAssetSourceFiles()) {
                AssetImportSettings settings;
                if (!TryLoadOrCreateImportSettings(sourcePath, out settings)) {
                    continue;
                }

                if (!IsTextureImporterRegistered(settings.ImporterId)) {
                    continue;
                }

                string outputPath = GetTextureAssetPath(settings.AssetId);
                if (File.Exists(outputPath)) {
                    continue;
                }

                ImportTexture(sourcePath);
                importedAssets.Add(outputPath);
            }

            return importedAssets;
        }

        /// <summary>
        /// Loads import settings from a settings file.
        /// </summary>
        /// <param name="settingsPath">Absolute path to the settings file.</param>
        /// <returns>Deserialized import settings.</returns>
        AssetImportSettings LoadImportSettings(string settingsPath) {
            if (string.IsNullOrWhiteSpace(settingsPath)) {
                throw new ArgumentException("Settings path must be provided.", nameof(settingsPath));
            }

            using (FileStream stream = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                return ProtoBuf.Serializer.Deserialize<AssetImportSettings>(stream);
            }
        }

        /// <summary>
        /// Creates new import settings based on the source file extension.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <returns>Newly created settings.</returns>
        AssetImportSettings CreateDefaultSettings(string sourcePath) {
            string extension = Path.GetExtension(sourcePath);
            string importerId = ResolveDefaultTextureImporter(extension);
            return new AssetImportSettings {
                ImporterId = importerId
            };
        }

        /// <summary>
        /// Attempts to load import settings or create defaults when missing.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <param name="settings">Resolved settings when available.</param>
        /// <returns>True when settings could be resolved for the source file.</returns>
        bool TryLoadOrCreateImportSettings(string sourcePath, out AssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            string settingsPath = GetSettingsPath(sourcePath);
            if (File.Exists(settingsPath)) {
                settings = LoadImportSettings(settingsPath);
                UpdateSettingsChecksum(settings, sourcePath);
                return true;
            }

            if (!TryCreateDefaultSettings(sourcePath, out settings)) {
                return false;
            }

            UpdateSettingsChecksum(settings, sourcePath);
            return true;
        }

        /// <summary>
        /// Attempts to create default import settings for a source file.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <param name="settings">Created settings when defaults are available.</param>
        /// <returns>True when settings were created from a registered default.</returns>
        bool TryCreateDefaultSettings(string sourcePath, out AssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            string extension = Path.GetExtension(sourcePath);
            string importerId;
            if (!TryResolveDefaultTextureImporter(extension, out importerId)) {
                settings = null;
                return false;
            }

            settings = new AssetImportSettings {
                ImporterId = importerId
            };
            return true;
        }

        /// <summary>
        /// Ensures required settings fields are populated.
        /// </summary>
        /// <param name="settings">Settings to validate.</param>
        void EnsureImportSettingsValid(AssetImportSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            if (string.IsNullOrWhiteSpace(settings.ImporterId)) {
                throw new InvalidOperationException("Import settings must specify an importer id.");
            }

            if (string.IsNullOrWhiteSpace(settings.AssetId)) {
                throw new InvalidOperationException("Import settings must specify an asset id.");
            }
        }

        /// <summary>
        /// Resolves the default texture importer identifier for an extension.
        /// </summary>
        /// <param name="extension">File extension to match.</param>
        /// <returns>Identifier for the default importer.</returns>
        string ResolveDefaultTextureImporter(string extension) {
            string normalized = NormalizeExtension(extension);
            string importerId;
            if (defaultTextureImportersByExtension.TryGetValue(normalized, out importerId)) {
                return importerId;
            }

            throw new InvalidOperationException($"No default texture importer registered for '{normalized}'.");
        }

        /// <summary>
        /// Attempts to resolve a default texture importer for an extension.
        /// </summary>
        /// <param name="extension">File extension to match.</param>
        /// <param name="importerId">Importer identifier when found.</param>
        /// <returns>True when a default importer is registered for the extension.</returns>
        bool TryResolveDefaultTextureImporter(string extension, out string importerId) {
            if (string.IsNullOrWhiteSpace(extension)) {
                importerId = string.Empty;
                return false;
            }

            string normalized = NormalizeExtension(extension);
            return defaultTextureImportersByExtension.TryGetValue(normalized, out importerId);
        }

        /// <summary>
        /// Retrieves a texture importer by identifier.
        /// </summary>
        /// <param name="importerId">Identifier of the importer.</param>
        /// <returns>Importer implementation.</returns>
        ITextureImporter GetTextureImporter(string importerId) {
            ITextureImporter importer;
            if (textureImportersById.TryGetValue(importerId, out importer)) {
                return importer;
            }

            throw new InvalidOperationException($"Texture importer '{importerId}' is not registered.");
        }

        /// <summary>
        /// Ensures a texture importer is registered.
        /// </summary>
        /// <param name="importerId">Identifier to verify.</param>
        void EnsureTextureImporterExists(string importerId) {
            if (!textureImportersById.ContainsKey(importerId)) {
                throw new InvalidOperationException($"Texture importer '{importerId}' is not registered.");
            }
        }

        /// <summary>
        /// Checks whether a texture importer is registered.
        /// </summary>
        /// <param name="importerId">Identifier to verify.</param>
        /// <returns>True when a matching importer is registered.</returns>
        bool IsTextureImporterRegistered(string importerId) {
            if (string.IsNullOrWhiteSpace(importerId)) {
                return false;
            }

            return textureImportersById.ContainsKey(importerId);
        }

        /// <summary>
        /// Updates settings to store the current source checksum.
        /// </summary>
        /// <param name="settings">Settings to update.</param>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        void UpdateSettingsChecksum(AssetImportSettings settings, string sourcePath) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            string checksum = fileHasher.ComputeHash(sourcePath);
            settings.SourceChecksum = checksum;
            settings.AssetId = checksum;
        }

        /// <summary>
        /// Enumerates asset source files that should have import settings.
        /// </summary>
        /// <returns>Sequence of asset source file paths.</returns>
        IEnumerable<string> EnumerateAssetSourceFiles() {
            IEnumerable<string> files = Directory.EnumerateFiles(assetsRootPath, "*", SearchOption.AllDirectories);
            foreach (string filePath in files) {
                if (IsSettingsFile(filePath)) {
                    continue;
                }

                if (IsUnderImportRoot(filePath)) {
                    continue;
                }

                yield return filePath;
            }
        }

        /// <summary>
        /// Determines whether a file path points to an import settings sidecar.
        /// </summary>
        /// <param name="filePath">File path to evaluate.</param>
        /// <returns>True when the path is a settings sidecar.</returns>
        bool IsSettingsFile(string filePath) {
            if (string.IsNullOrWhiteSpace(filePath)) {
                return false;
            }

            string extension = Path.GetExtension(filePath);
            return string.Equals(extension, SettingsExtension, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether a path is inside the import output folder.
        /// </summary>
        /// <param name="filePath">Path to evaluate.</param>
        /// <returns>True when the path is under the import output folder.</returns>
        bool IsUnderImportRoot(string filePath) {
            if (string.IsNullOrWhiteSpace(filePath)) {
                return false;
            }

            string fullPath = Path.GetFullPath(filePath);
            if (string.Equals(fullPath, importRootPath, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            return fullPath.StartsWith(importRootPrefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Builds the settings file path for a source file.
        /// </summary>
        /// <param name="sourcePath">Source file path.</param>
        /// <returns>Path to the settings file.</returns>
        string GetSettingsPath(string sourcePath) {
            return sourcePath + SettingsExtension;
        }

        /// <summary>
        /// Builds the output path for an imported texture asset.
        /// </summary>
        /// <param name="assetId">Asset identifier used in the file name.</param>
        /// <returns>Absolute path to the serialized asset file.</returns>
        string GetTextureAssetPath(string assetId) {
            if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Asset id must be provided.", nameof(assetId));
            }

            string fileName = assetId + TextureAssetExtension;
            return Path.Combine(importRootPath, fileName);
        }

        /// <summary>
        /// Ensures the directory for a file path exists.
        /// </summary>
        /// <param name="filePath">Path to the file.</param>
        void EnsureDirectoryForFile(string filePath) {
            if (string.IsNullOrWhiteSpace(filePath)) {
                throw new ArgumentException("File path must be provided.", nameof(filePath));
            }

            string directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(directory)) {
                throw new InvalidOperationException("File directory could not be resolved.");
            }

            Directory.CreateDirectory(directory);
        }

        /// <summary>
        /// Normalizes an extension string for dictionary keys.
        /// </summary>
        /// <param name="extension">Extension string to normalize.</param>
        /// <returns>Normalized extension.</returns>
        string NormalizeExtension(string extension) {
            if (string.IsNullOrWhiteSpace(extension)) {
                throw new ArgumentException("Extension must be provided.", nameof(extension));
            }

            if (!extension.StartsWith(".")) {
                extension = "." + extension;
            }

            return extension.ToLowerInvariant();
        }
    }
}
