namespace helengine.editor {
    /// <summary>
    /// Manages asset importer registration and sidecar import settings.
    /// </summary>
    public class AssetImportManager {
        /// <summary>
        /// File extension for import settings sidecar files.
        /// </summary>
        internal const string SettingsExtension = ".hasset";

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
        /// Registered text importers keyed by identifier.
        /// </summary>
        readonly Dictionary<string, ITextImporter> textImportersById;

        /// <summary>
        /// Default text importer identifiers keyed by extension.
        /// </summary>
        readonly Dictionary<string, string> defaultTextImportersByExtension;

        /// <summary>
        /// Registered model importers keyed by identifier.
        /// </summary>
        readonly Dictionary<string, IModelImporter> ModelImportersById;

        /// <summary>
        /// Default model importer identifiers keyed by extension.
        /// </summary>
        readonly Dictionary<string, string> DefaultModelImportersByExtension;

        /// <summary>
        /// File hasher used to generate content checksums.
        /// </summary>
        readonly AssetFileHasher fileHasher;
        /// <summary>
        /// Content manager used to load source files, cached assets, and import settings.
        /// </summary>
        readonly ContentManager AssetContentManager;

        /// <summary>
        /// Initializes a new asset import manager for a project.
        /// </summary>
        /// <param name="projectRootPath">Absolute path to the project root.</param>
        /// <param name="contentManager">Core-owned content manager used to load project assets and settings.</param>
        public AssetImportManager(string projectRootPath, ContentManager contentManager) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }
            if (contentManager == null) {
                throw new ArgumentNullException(nameof(contentManager));
            }

            this.projectRootPath = Path.GetFullPath(projectRootPath);
            assetsRootPath = Path.Combine(this.projectRootPath, "assets");
            importRootPath = Path.Combine(this.projectRootPath, ImportFolderName);
            importRootPrefix = importRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            textureImportersById = new Dictionary<string, ITextureImporter>(StringComparer.OrdinalIgnoreCase);
            defaultTextureImportersByExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            textImportersById = new Dictionary<string, ITextImporter>(StringComparer.OrdinalIgnoreCase);
            defaultTextImportersByExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ModelImportersById = new Dictionary<string, IModelImporter>(StringComparer.OrdinalIgnoreCase);
            DefaultModelImportersByExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            fileHasher = new AssetFileHasher();
            AssetContentManager = contentManager;
            EditorContentManagerConfiguration.ConfigureProjectContentManager(AssetContentManager);

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

            if (textImportersById.ContainsKey(registration.ImporterId)) {
                throw new InvalidOperationException($"Importer id '{registration.ImporterId}' is already registered for text assets.");
            }

            if (ModelImportersById.ContainsKey(registration.ImporterId)) {
                throw new InvalidOperationException($"Importer id '{registration.ImporterId}' is already registered for model assets.");
            }

            textureImportersById.Add(registration.ImporterId, registration.Importer);
            AssetContentManager.RegisterProcessor(
                registration.ImporterId,
                new TextureImporterContentProcessor(registration.Importer),
                registration.Extensions);
            IReadOnlyList<string> extensions = registration.Extensions;
            for (int i = 0; i < extensions.Count; i++) {
                string extension = NormalizeExtension(extensions[i]);
                if (defaultTextImportersByExtension.ContainsKey(extension)) {
                    throw new InvalidOperationException($"Extension '{extension}' is already mapped to a text importer.");
                }

                if (DefaultModelImportersByExtension.ContainsKey(extension)) {
                    throw new InvalidOperationException($"Extension '{extension}' is already mapped to a model importer.");
                }

                if (!defaultTextureImportersByExtension.ContainsKey(extension)) {
                    defaultTextureImportersByExtension[extension] = registration.ImporterId;
                }
            }
        }

        /// <summary>
        /// Registers a text importer and records its supported extensions.
        /// </summary>
        /// <param name="registration">Importer registration data.</param>
        public void RegisterTextImporter(TextImporterRegistration registration) {
            if (registration == null) {
                throw new ArgumentNullException(nameof(registration));
            }

            if (textImportersById.ContainsKey(registration.ImporterId)) {
                throw new InvalidOperationException($"Text importer '{registration.ImporterId}' is already registered.");
            }

            if (textureImportersById.ContainsKey(registration.ImporterId)) {
                throw new InvalidOperationException($"Importer id '{registration.ImporterId}' is already registered for texture assets.");
            }

            if (ModelImportersById.ContainsKey(registration.ImporterId)) {
                throw new InvalidOperationException($"Importer id '{registration.ImporterId}' is already registered for model assets.");
            }

            textImportersById.Add(registration.ImporterId, registration.Importer);
            AssetContentManager.RegisterProcessor(
                registration.ImporterId,
                new TextImporterContentProcessor(registration.Importer),
                registration.Extensions);
            IReadOnlyList<string> extensions = registration.Extensions;
            for (int i = 0; i < extensions.Count; i++) {
                string extension = NormalizeExtension(extensions[i]);
                if (defaultTextureImportersByExtension.ContainsKey(extension)) {
                    throw new InvalidOperationException($"Extension '{extension}' is already mapped to a texture importer.");
                }

                if (DefaultModelImportersByExtension.ContainsKey(extension)) {
                    throw new InvalidOperationException($"Extension '{extension}' is already mapped to a model importer.");
                }

                if (!defaultTextImportersByExtension.ContainsKey(extension)) {
                    defaultTextImportersByExtension[extension] = registration.ImporterId;
                }
            }
        }

        /// <summary>
        /// Registers a model importer and records its supported extensions.
        /// </summary>
        /// <param name="registration">Importer registration data.</param>
        public void RegisterModelImporter(ModelImporterRegistration registration) {
            if (registration == null) {
                throw new ArgumentNullException(nameof(registration));
            }

            if (ModelImportersById.ContainsKey(registration.ImporterId)) {
                throw new InvalidOperationException($"Model importer '{registration.ImporterId}' is already registered.");
            }

            if (textureImportersById.ContainsKey(registration.ImporterId)) {
                throw new InvalidOperationException($"Importer id '{registration.ImporterId}' is already registered for texture assets.");
            }

            if (textImportersById.ContainsKey(registration.ImporterId)) {
                throw new InvalidOperationException($"Importer id '{registration.ImporterId}' is already registered for text assets.");
            }

            ModelImportersById.Add(registration.ImporterId, registration.Importer);
            AssetContentManager.RegisterProcessor(
                registration.ImporterId,
                new ModelImporterContentProcessor(registration.Importer),
                registration.Extensions);
            IReadOnlyList<string> extensions = registration.Extensions;
            for (int i = 0; i < extensions.Count; i++) {
                string extension = NormalizeExtension(extensions[i]);
                if (defaultTextureImportersByExtension.ContainsKey(extension)) {
                    throw new InvalidOperationException($"Extension '{extension}' is already mapped to a texture importer.");
                }

                if (defaultTextImportersByExtension.ContainsKey(extension)) {
                    throw new InvalidOperationException($"Extension '{extension}' is already mapped to a text importer.");
                }

                if (!DefaultModelImportersByExtension.ContainsKey(extension)) {
                    DefaultModelImportersByExtension[extension] = registration.ImporterId;
                }
            }
        }

        /// <summary>
        /// Gets the identifiers of all registered texture importers.
        /// </summary>
        /// <returns>Ordered list of importer identifiers.</returns>
        public IReadOnlyList<string> GetTextureImporterIds() {
            List<string> ids = new List<string>(textureImportersById.Count);
            foreach (string importerId in textureImportersById.Keys) {
                ids.Add(importerId);
            }

            ids.Sort(StringComparer.OrdinalIgnoreCase);
            return ids;
        }

        /// <summary>
        /// Gets the identifiers of all registered text importers.
        /// </summary>
        /// <returns>Ordered list of importer identifiers.</returns>
        public IReadOnlyList<string> GetTextImporterIds() {
            List<string> ids = new List<string>(textImportersById.Count);
            foreach (string importerId in textImportersById.Keys) {
                ids.Add(importerId);
            }

            ids.Sort(StringComparer.OrdinalIgnoreCase);
            return ids;
        }

        /// <summary>
        /// Gets the identifiers of all registered model importers.
        /// </summary>
        /// <returns>Ordered list of importer identifiers.</returns>
        public IReadOnlyList<string> GetModelImporterIds() {
            List<string> ids = new List<string>(ModelImportersById.Count);
            foreach (string importerId in ModelImportersById.Keys) {
                ids.Add(importerId);
            }

            ids.Sort(StringComparer.OrdinalIgnoreCase);
            return ids;
        }

        /// <summary>
        /// Gets the importer identifiers applicable to a file extension.
        /// </summary>
        /// <param name="extension">File extension to evaluate.</param>
        /// <returns>Ordered list of importer identifiers for the extension.</returns>
        public IReadOnlyList<string> GetImporterIdsForExtension(string extension) {
            if (string.IsNullOrWhiteSpace(extension)) {
                return Array.Empty<string>();
            }

            string normalized = NormalizeExtension(extension);
            if (defaultTextureImportersByExtension.ContainsKey(normalized)) {
                return GetTextureImporterIds();
            }

            if (defaultTextImportersByExtension.ContainsKey(normalized)) {
                return GetTextImporterIds();
            }

            if (DefaultModelImportersByExtension.ContainsKey(normalized)) {
                return GetModelImporterIds();
            }

            return Array.Empty<string>();
        }

        /// <summary>
        /// Checks whether the extension maps to a texture importer.
        /// </summary>
        /// <param name="extension">File extension to evaluate.</param>
        /// <returns>True when the extension maps to a texture importer.</returns>
        public bool IsTextureExtension(string extension) {
            if (string.IsNullOrWhiteSpace(extension)) {
                return false;
            }

            string normalized = NormalizeExtension(extension);
            return defaultTextureImportersByExtension.ContainsKey(normalized);
        }

        /// <summary>
        /// Checks whether the extension maps to a text importer.
        /// </summary>
        /// <param name="extension">File extension to evaluate.</param>
        /// <returns>True when the extension maps to a text importer.</returns>
        public bool IsTextExtension(string extension) {
            if (string.IsNullOrWhiteSpace(extension)) {
                return false;
            }

            string normalized = NormalizeExtension(extension);
            return defaultTextImportersByExtension.ContainsKey(normalized);
        }

        /// <summary>
        /// Checks whether the extension maps to a model importer.
        /// </summary>
        /// <param name="extension">File extension to evaluate.</param>
        /// <returns>True when the extension maps to a model importer.</returns>
        public bool IsModelExtension(string extension) {
            if (string.IsNullOrWhiteSpace(extension)) {
                return false;
            }

            string normalized = NormalizeExtension(extension);
            return DefaultModelImportersByExtension.ContainsKey(normalized);
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
            string normalized = NormalizeExtension(extension);
            if (defaultTextImportersByExtension.ContainsKey(normalized)) {
                throw new InvalidOperationException($"Extension '{normalized}' is already mapped to a text importer.");
            }

            if (DefaultModelImportersByExtension.ContainsKey(normalized)) {
                throw new InvalidOperationException($"Extension '{normalized}' is already mapped to a model importer.");
            }

            defaultTextureImportersByExtension[normalized] = importerId;
        }

        /// <summary>
        /// Sets the default text importer for a specific extension.
        /// </summary>
        /// <param name="extension">File extension to associate with the importer.</param>
        /// <param name="importerId">Identifier of the importer to use.</param>
        public void SetDefaultTextImporter(string extension, string importerId) {
            if (string.IsNullOrWhiteSpace(extension)) {
                throw new ArgumentException("Extension must be provided.", nameof(extension));
            }

            if (string.IsNullOrWhiteSpace(importerId)) {
                throw new ArgumentException("Importer id must be provided.", nameof(importerId));
            }

            EnsureTextImporterExists(importerId);
            string normalized = NormalizeExtension(extension);
            if (defaultTextureImportersByExtension.ContainsKey(normalized)) {
                throw new InvalidOperationException($"Extension '{normalized}' is already mapped to a texture importer.");
            }

            if (DefaultModelImportersByExtension.ContainsKey(normalized)) {
                throw new InvalidOperationException($"Extension '{normalized}' is already mapped to a model importer.");
            }

            defaultTextImportersByExtension[normalized] = importerId;
        }

        /// <summary>
        /// Sets the default model importer for a specific extension.
        /// </summary>
        /// <param name="extension">File extension to associate with the importer.</param>
        /// <param name="importerId">Identifier of the importer to use.</param>
        public void SetDefaultModelImporter(string extension, string importerId) {
            if (string.IsNullOrWhiteSpace(extension)) {
                throw new ArgumentException("Extension must be provided.", nameof(extension));
            }

            if (string.IsNullOrWhiteSpace(importerId)) {
                throw new ArgumentException("Importer id must be provided.", nameof(importerId));
            }

            EnsureModelImporterExists(importerId);
            string normalized = NormalizeExtension(extension);
            if (defaultTextureImportersByExtension.ContainsKey(normalized)) {
                throw new InvalidOperationException($"Extension '{normalized}' is already mapped to a texture importer.");
            }

            if (defaultTextImportersByExtension.ContainsKey(normalized)) {
                throw new InvalidOperationException($"Extension '{normalized}' is already mapped to a text importer.");
            }

            DefaultModelImportersByExtension[normalized] = importerId;
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

            EnsureTextureImporterExists(settings.ImporterId);
            TextureAsset asset = AssetContentManager.Load<TextureAsset>(sourcePath, settings.ImporterId);

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
        /// Imports a text asset from a source file and writes it to disk.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the text source file.</param>
        /// <returns>Imported <see cref="TextAsset"/> instance.</returns>
        public TextAsset ImportText(string sourcePath) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            if (!File.Exists(sourcePath)) {
                throw new FileNotFoundException("Text source file was not found.", sourcePath);
            }

            AssetImportSettings settings = LoadOrCreateImportSettings(sourcePath);
            EnsureImportSettingsValid(settings);

            EnsureTextImporterExists(settings.ImporterId);
            TextAsset asset = AssetContentManager.Load<TextAsset>(sourcePath, settings.ImporterId);

            if (asset == null) {
                throw new InvalidOperationException($"Text importer '{settings.ImporterId}' did not return an asset.");
            }

            asset.Id = settings.AssetId;

            string outputPath = GetTextAssetPath(settings.AssetId);
            EnsureDirectoryForFile(outputPath);
            using (FileStream stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, asset);
            }

            SaveImportSettings(sourcePath, settings);
            return asset;
        }

        /// <summary>
        /// Imports a model asset from a source file and writes it to disk.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the model source file.</param>
        /// <returns>Imported <see cref="ModelAsset"/> instance.</returns>
        public ModelAsset ImportModel(string sourcePath) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            if (!File.Exists(sourcePath)) {
                throw new FileNotFoundException("Model source file was not found.", sourcePath);
            }

            AssetImportSettings settings = LoadOrCreateImportSettings(sourcePath);
            EnsureImportSettingsValid(settings);

            EnsureModelImporterExists(settings.ImporterId);
            ModelAsset asset = AssetContentManager.Load<ModelAsset>(sourcePath, settings.ImporterId);

            if (asset == null) {
                throw new InvalidOperationException($"Model importer '{settings.ImporterId}' did not return an asset.");
            }

            asset.Id = settings.AssetId;

            string outputPath = GetModelAssetPath(settings.AssetId);
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
            bool settingsFileExists = File.Exists(settingsPath);
            AssetImportSettings settings = null;
            bool loadedFromDisk = settingsFileExists && TryLoadImportSettings(settingsPath, out settings);
            if (!loadedFromDisk) {
                settings = CreateDefaultSettings(sourcePath);
            }

            UpdateSettingsChecksum(settings, sourcePath);
            if (settingsFileExists && !loadedFromDisk) {
                SaveImportSettings(sourcePath, settings);
            }

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
                AssetImportSettingsBinarySerializer.Serialize(stream, settings);
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
        /// Imports textures that are missing cache files.
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
                if (File.Exists(outputPath) && TryLoadCachedTextureAsset(outputPath, out _)) {
                    continue;
                }

                ImportTexture(sourcePath);
                importedAssets.Add(outputPath);
            }

            return importedAssets;
        }

        /// <summary>
        /// Imports models that are missing cache files.
        /// </summary>
        /// <returns>Paths to cached assets created during the scan.</returns>
        public List<string> ImportModelsMissingCache() {
            List<string> importedAssets = new List<string>();
            foreach (string sourcePath in EnumerateAssetSourceFiles()) {
                AssetImportSettings settings;
                if (!TryLoadOrCreateImportSettings(sourcePath, out settings)) {
                    continue;
                }

                if (!IsModelImporterRegistered(settings.ImporterId)) {
                    continue;
                }

                string outputPath = GetModelAssetPath(settings.AssetId);
                if (File.Exists(outputPath) && TryLoadCachedModelAsset(outputPath, out _)) {
                    continue;
                }

                ImportModel(sourcePath);
                importedAssets.Add(outputPath);
            }

            return importedAssets;
        }

        /// <summary>
        /// Loads a texture asset for a source file, importing it when needed.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the texture source file.</param>
        /// <param name="asset">Loaded texture asset when available.</param>
        /// <returns>True when the source can be resolved to a texture asset.</returns>
        public bool TryLoadTextureAsset(string sourcePath, out TextureAsset asset) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            if (!File.Exists(sourcePath)) {
                throw new FileNotFoundException("Texture source file was not found.", sourcePath);
            }

            AssetImportSettings settings;
            if (!TryLoadOrCreateImportSettings(sourcePath, out settings)) {
                asset = null;
                return false;
            }

            if (!IsTextureImporterRegistered(settings.ImporterId)) {
                asset = null;
                return false;
            }

            string outputPath = GetTextureAssetPath(settings.AssetId);
            if (!File.Exists(outputPath)) {
                asset = ImportTexture(sourcePath);
                return true;
            }

            if (TryLoadCachedTextureAsset(outputPath, out asset)) {
                return true;
            }

            asset = ImportTexture(sourcePath);
            return true;
        }

        /// <summary>
        /// Loads a text asset for a source file, importing it when needed.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the text source file.</param>
        /// <param name="asset">Loaded text asset when available.</param>
        /// <returns>True when the source can be resolved to a text asset.</returns>
        public bool TryLoadTextAsset(string sourcePath, out TextAsset asset) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            if (!File.Exists(sourcePath)) {
                throw new FileNotFoundException("Text source file was not found.", sourcePath);
            }

            AssetImportSettings settings;
            if (!TryLoadOrCreateImportSettings(sourcePath, out settings)) {
                asset = null;
                return false;
            }

            if (!IsTextImporterRegistered(settings.ImporterId)) {
                asset = null;
                return false;
            }

            string outputPath = GetTextAssetPath(settings.AssetId);
            if (!File.Exists(outputPath)) {
                asset = ImportText(sourcePath);
                return true;
            }

            if (TryLoadCachedTextAsset(outputPath, out asset)) {
                return true;
            }

            asset = ImportText(sourcePath);
            return true;
        }

        /// <summary>
        /// Loads a model asset for a source file, importing it when needed.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the model source file.</param>
        /// <param name="asset">Loaded model asset when available.</param>
        /// <returns>True when the source can be resolved to a model asset.</returns>
        public bool TryLoadModelAsset(string sourcePath, out ModelAsset asset) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            if (!File.Exists(sourcePath)) {
                throw new FileNotFoundException("Model source file was not found.", sourcePath);
            }

            AssetImportSettings settings;
            if (!TryLoadOrCreateImportSettings(sourcePath, out settings)) {
                asset = null;
                return false;
            }

            if (!IsModelImporterRegistered(settings.ImporterId)) {
                asset = null;
                return false;
            }

            string outputPath = GetModelAssetPath(settings.AssetId);
            if (!File.Exists(outputPath)) {
                asset = ImportModel(sourcePath);
                return true;
            }

            if (TryLoadCachedModelAsset(outputPath, out asset)) {
                return true;
            }

            asset = ImportModel(sourcePath);
            return true;
        }

        /// <summary>
        /// Attempts to load a cached texture asset.
        /// </summary>
        /// <param name="outputPath">Absolute path to the cached texture asset.</param>
        /// <param name="asset">Loaded texture asset when the cache file exists and contains the expected payload type.</param>
        /// <returns>True when the cached asset was loaded successfully.</returns>
        bool TryLoadCachedTextureAsset(string outputPath, out TextureAsset asset) {
            if (string.IsNullOrWhiteSpace(outputPath)) {
                throw new ArgumentException("Output path must be provided.", nameof(outputPath));
            }

            asset = null;
            Asset cachedAsset;
            if (!TryLoadCachedAsset(outputPath, "TextureAsset", out cachedAsset)) {
                return false;
            }

            if (cachedAsset is TextureAsset textureAsset) {
                asset = textureAsset;
                return true;
            }

            throw new InvalidOperationException($"Texture cache file '{outputPath}' did not contain a TextureAsset payload.");
        }

        /// <summary>
        /// Attempts to load a cached text asset.
        /// </summary>
        /// <param name="outputPath">Absolute path to the cached text asset.</param>
        /// <param name="asset">Loaded text asset when the cache file exists and contains the expected payload type.</param>
        /// <returns>True when the cached asset was loaded successfully.</returns>
        bool TryLoadCachedTextAsset(string outputPath, out TextAsset asset) {
            if (string.IsNullOrWhiteSpace(outputPath)) {
                throw new ArgumentException("Output path must be provided.", nameof(outputPath));
            }

            asset = null;
            Asset cachedAsset;
            if (!TryLoadCachedAsset(outputPath, "TextAsset", out cachedAsset)) {
                return false;
            }

            if (cachedAsset is TextAsset textAsset) {
                asset = textAsset;
                return true;
            }

            throw new InvalidOperationException($"Text cache file '{outputPath}' did not contain a TextAsset payload.");
        }

        /// <summary>
        /// Attempts to load a cached model asset.
        /// </summary>
        /// <param name="outputPath">Absolute path to the cached model asset.</param>
        /// <param name="asset">Loaded model asset when the cache file exists and contains the expected payload type.</param>
        /// <returns>True when the cached asset was loaded successfully.</returns>
        bool TryLoadCachedModelAsset(string outputPath, out ModelAsset asset) {
            if (string.IsNullOrWhiteSpace(outputPath)) {
                throw new ArgumentException("Output path must be provided.", nameof(outputPath));
            }

            asset = null;
            Asset cachedAsset;
            if (!TryLoadCachedAsset(outputPath, "ModelAsset", out cachedAsset)) {
                return false;
            }

            if (cachedAsset is ModelAsset modelAsset) {
                asset = modelAsset;
                return true;
            }

            throw new InvalidOperationException($"Model cache file '{outputPath}' did not contain a ModelAsset payload.");
        }

        /// <summary>
        /// Attempts to load a cached serialized asset.
        /// </summary>
        /// <param name="outputPath">Absolute path to the cached asset file.</param>
        /// <param name="assetTypeName">Logical asset type name expected inside the cache file.</param>
        /// <param name="asset">Loaded cached asset when the file contains a serialized asset.</param>
        /// <returns>True when the cache file contained a serialized asset.</returns>
        bool TryLoadCachedAsset(string outputPath, string assetTypeName, out Asset asset) {
            if (string.IsNullOrWhiteSpace(outputPath)) {
                throw new ArgumentException("Output path must be provided.", nameof(outputPath));
            } else if (string.IsNullOrWhiteSpace(assetTypeName)) {
                throw new ArgumentException("Asset type name must be provided.", nameof(assetTypeName));
            }

            using (FileStream stream = new FileStream(outputPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                asset = AssetSerializer.Deserialize(stream);
            }

            return true;
        }

        /// <summary>
        /// Attempts to load import settings from a settings file.
        /// </summary>
        /// <param name="settingsPath">Absolute path to the settings file.</param>
        /// <param name="settings">Deserialized import settings when the file exists.</param>
        /// <returns>True when the settings file was loaded successfully.</returns>
        bool TryLoadImportSettings(string settingsPath, out AssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(settingsPath)) {
                throw new ArgumentException("Settings path must be provided.", nameof(settingsPath));
            }

            settings = null;
            if (!File.Exists(settingsPath)) {
                return false;
            }

            using (FileStream stream = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                settings = AssetImportSettingsBinarySerializer.Deserialize(stream);
            }

            return true;
        }

        /// <summary>
        /// Creates new import settings based on the source file extension.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <returns>Newly created settings.</returns>
        AssetImportSettings CreateDefaultSettings(string sourcePath) {
            string extension = Path.GetExtension(sourcePath);
            string importerId = ResolveDefaultImporter(extension);
            return new AssetImportSettings {
                ImporterId = importerId
            };
        }

        /// <summary>
        /// Attempts to load import settings or create defaults when missing.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <param name="settings">Resolved settings when available; null when no default importer exists.</param>
        /// <returns>True when settings could be resolved for the source file.</returns>
        public bool TryLoadOrCreateImportSettings(string sourcePath, out AssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            string settingsPath = GetSettingsPath(sourcePath);
            bool settingsFileExists = File.Exists(settingsPath);
            if (settingsFileExists && TryLoadImportSettings(settingsPath, out settings)) {
                UpdateSettingsChecksum(settings, sourcePath);
                return true;
            }

            if (!TryCreateDefaultSettings(sourcePath, out settings)) {
                return false;
            }

            UpdateSettingsChecksum(settings, sourcePath);
            if (settingsFileExists) {
                SaveImportSettings(sourcePath, settings);
            }

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
            if (!TryResolveDefaultImporter(extension, out importerId)) {
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
        /// Resolves the default importer identifier for an extension.
        /// </summary>
        /// <param name="extension">File extension to match.</param>
        /// <returns>Identifier for the default importer.</returns>
        string ResolveDefaultImporter(string extension) {
            string normalized = NormalizeExtension(extension);
            string textureImporterId;
            string textImporterId;
            string modelImporterId;
            bool hasTexture = defaultTextureImportersByExtension.TryGetValue(normalized, out textureImporterId);
            bool hasText = defaultTextImportersByExtension.TryGetValue(normalized, out textImporterId);
            bool hasModel = DefaultModelImportersByExtension.TryGetValue(normalized, out modelImporterId);

            if ((hasTexture && hasText) || (hasTexture && hasModel) || (hasText && hasModel)) {
                throw new InvalidOperationException($"Multiple importer types are registered for '{normalized}'.");
            }

            if (hasTexture) {
                return textureImporterId;
            }

            if (hasText) {
                return textImporterId;
            }

            if (hasModel) {
                return modelImporterId;
            }

            throw new InvalidOperationException($"No default importer registered for '{normalized}'.");
        }

        /// <summary>
        /// Attempts to resolve a default importer for an extension.
        /// </summary>
        /// <param name="extension">File extension to match.</param>
        /// <param name="importerId">Importer identifier when found.</param>
        /// <returns>True when a default importer is registered for the extension.</returns>
        bool TryResolveDefaultImporter(string extension, out string importerId) {
            if (string.IsNullOrWhiteSpace(extension)) {
                importerId = string.Empty;
                return false;
            }

            string normalized = NormalizeExtension(extension);
            string textureImporterId;
            string textImporterId;
            string modelImporterId;
            bool hasTexture = defaultTextureImportersByExtension.TryGetValue(normalized, out textureImporterId);
            bool hasText = defaultTextImportersByExtension.TryGetValue(normalized, out textImporterId);
            bool hasModel = DefaultModelImportersByExtension.TryGetValue(normalized, out modelImporterId);

            if ((hasTexture && hasText) || (hasTexture && hasModel) || (hasText && hasModel)) {
                throw new InvalidOperationException($"Multiple importer types are registered for '{normalized}'.");
            }

            if (hasTexture) {
                importerId = textureImporterId;
                return true;
            }

            if (hasText) {
                importerId = textImporterId;
                return true;
            }

            if (hasModel) {
                importerId = modelImporterId;
                return true;
            }

            importerId = string.Empty;
            return false;
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
        /// Retrieves a text importer by identifier.
        /// </summary>
        /// <param name="importerId">Identifier of the importer.</param>
        /// <returns>Importer implementation.</returns>
        ITextImporter GetTextImporter(string importerId) {
            ITextImporter importer;
            if (textImportersById.TryGetValue(importerId, out importer)) {
                return importer;
            }

            throw new InvalidOperationException($"Text importer '{importerId}' is not registered.");
        }

        /// <summary>
        /// Ensures a text importer is registered.
        /// </summary>
        /// <param name="importerId">Identifier to verify.</param>
        void EnsureTextImporterExists(string importerId) {
            if (!textImportersById.ContainsKey(importerId)) {
                throw new InvalidOperationException($"Text importer '{importerId}' is not registered.");
            }
        }

        /// <summary>
        /// Checks whether a text importer is registered.
        /// </summary>
        /// <param name="importerId">Identifier to verify.</param>
        /// <returns>True when a matching importer is registered.</returns>
        bool IsTextImporterRegistered(string importerId) {
            if (string.IsNullOrWhiteSpace(importerId)) {
                return false;
            }

            return textImportersById.ContainsKey(importerId);
        }

        /// <summary>
        /// Retrieves a model importer by identifier.
        /// </summary>
        /// <param name="importerId">Identifier of the importer.</param>
        /// <returns>Importer implementation.</returns>
        IModelImporter GetModelImporter(string importerId) {
            IModelImporter importer;
            if (ModelImportersById.TryGetValue(importerId, out importer)) {
                return importer;
            }

            throw new InvalidOperationException($"Model importer '{importerId}' is not registered.");
        }

        /// <summary>
        /// Ensures a model importer is registered.
        /// </summary>
        /// <param name="importerId">Identifier to verify.</param>
        void EnsureModelImporterExists(string importerId) {
            if (!ModelImportersById.ContainsKey(importerId)) {
                throw new InvalidOperationException($"Model importer '{importerId}' is not registered.");
            }
        }

        /// <summary>
        /// Checks whether a model importer is registered.
        /// </summary>
        /// <param name="importerId">Identifier to verify.</param>
        /// <returns>True when a matching importer is registered.</returns>
        bool IsModelImporterRegistered(string importerId) {
            if (string.IsNullOrWhiteSpace(importerId)) {
                return false;
            }

            return ModelImportersById.ContainsKey(importerId);
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

            return Path.Combine(importRootPath, assetId);
        }

        /// <summary>
        /// Builds the output path for an imported text asset.
        /// </summary>
        /// <param name="assetId">Asset identifier used in the file name.</param>
        /// <returns>Absolute path to the serialized asset file.</returns>
        string GetTextAssetPath(string assetId) {
            if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Asset id must be provided.", nameof(assetId));
            }

            return Path.Combine(importRootPath, assetId);
        }

        /// <summary>
        /// Builds the output path for an imported model asset.
        /// </summary>
        /// <param name="assetId">Asset identifier used in the file name.</param>
        /// <returns>Absolute path to the serialized asset file.</returns>
        string GetModelAssetPath(string assetId) {
            if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Asset id must be provided.", nameof(assetId));
            }

            return Path.Combine(importRootPath, assetId);
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
