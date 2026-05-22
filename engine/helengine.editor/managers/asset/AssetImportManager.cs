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
        /// Texture importer identifiers keyed by extension.
        /// </summary>
        readonly Dictionary<string, List<string>> textureImporterIdsByExtension;

        /// <summary>
        /// Registered text importers keyed by identifier.
        /// </summary>
        readonly Dictionary<string, ITextImporter> textImportersById;

        /// <summary>
        /// Registered font importers keyed by identifier.
        /// </summary>
        readonly Dictionary<string, IFontImporter> fontImportersById;

        /// <summary>
        /// Default text importer identifiers keyed by extension.
        /// </summary>
        readonly Dictionary<string, string> defaultTextImportersByExtension;

        /// <summary>
        /// Default font importer identifiers keyed by extension.
        /// </summary>
        readonly Dictionary<string, string> defaultFontImportersByExtension;

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
        /// Applies processor settings to imported model assets before they are cached.
        /// </summary>
        readonly ModelAssetProcessor ModelAssetProcessor;

        /// <summary>
        /// Applies processor settings to imported texture assets before they are cached.
        /// </summary>
        readonly TextureAssetProcessor TextureAssetProcessor;

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
            textureImporterIdsByExtension = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            textImportersById = new Dictionary<string, ITextImporter>(StringComparer.OrdinalIgnoreCase);
            fontImportersById = new Dictionary<string, IFontImporter>(StringComparer.OrdinalIgnoreCase);
            defaultTextImportersByExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            defaultFontImportersByExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            ModelImportersById = new Dictionary<string, IModelImporter>(StringComparer.OrdinalIgnoreCase);
            DefaultModelImportersByExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            fileHasher = new AssetFileHasher();
            AssetContentManager = contentManager;
            ModelAssetProcessor = new ModelAssetProcessor();
            TextureAssetProcessor = new TextureAssetProcessor();
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
        /// Gets the project content manager used to load source assets and importer outputs.
        /// </summary>
        public ContentManager ContentManager => AssetContentManager;

        /// <summary>
        /// Gets or sets the active project platform whose processor settings should drive model cache generation.
        /// </summary>
        public string CurrentPlatformId { get; set; }

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

            if (fontImportersById.ContainsKey(registration.ImporterId)) {
                throw new InvalidOperationException($"Importer id '{registration.ImporterId}' is already registered for font assets.");
            }

            if (ModelImportersById.ContainsKey(registration.ImporterId)) {
                throw new InvalidOperationException($"Importer id '{registration.ImporterId}' is already registered for model assets.");
            }

            textureImportersById.Add(registration.ImporterId, registration.Importer);
            AssetContentManager.RegisterProcessor(
                registration.ImporterId,
                new TextureImporterContentProcessor(registration.Importer),
                Array.Empty<string>());
            string[] extensions = registration.Extensions;
            for (int i = 0; i < extensions.Length; i++) {
                string extension = NormalizeExtension(extensions[i]);
                if (defaultTextImportersByExtension.ContainsKey(extension)) {
                    throw new InvalidOperationException($"Extension '{extension}' is already mapped to a text importer.");
                }

                if (defaultFontImportersByExtension.ContainsKey(extension)) {
                    throw new InvalidOperationException($"Extension '{extension}' is already mapped to a font importer.");
                }

                if (DefaultModelImportersByExtension.ContainsKey(extension)) {
                    throw new InvalidOperationException($"Extension '{extension}' is already mapped to a model importer.");
                }

                RegisterTextureImporterExtension(extension, registration.ImporterId);
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

            if (fontImportersById.ContainsKey(registration.ImporterId)) {
                throw new InvalidOperationException($"Importer id '{registration.ImporterId}' is already registered for font assets.");
            }

            if (ModelImportersById.ContainsKey(registration.ImporterId)) {
                throw new InvalidOperationException($"Importer id '{registration.ImporterId}' is already registered for model assets.");
            }

            textImportersById.Add(registration.ImporterId, registration.Importer);
            AssetContentManager.RegisterProcessor(
                registration.ImporterId,
                new TextImporterContentProcessor(registration.Importer),
                registration.Extensions);
            string[] extensions = registration.Extensions;
            for (int i = 0; i < extensions.Length; i++) {
                string extension = NormalizeExtension(extensions[i]);
                if (defaultTextureImportersByExtension.ContainsKey(extension)) {
                    throw new InvalidOperationException($"Extension '{extension}' is already mapped to a texture importer.");
                }

                if (defaultFontImportersByExtension.ContainsKey(extension)) {
                    throw new InvalidOperationException($"Extension '{extension}' is already mapped to a font importer.");
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
        /// Registers a font importer and records its supported extensions.
        /// </summary>
        /// <param name="registration">Importer registration data.</param>
        public void RegisterFontImporter(FontImporterRegistration registration) {
            if (registration == null) {
                throw new ArgumentNullException(nameof(registration));
            }

            if (fontImportersById.ContainsKey(registration.ImporterId)) {
                throw new InvalidOperationException($"Font importer '{registration.ImporterId}' is already registered.");
            }

            if (textureImportersById.ContainsKey(registration.ImporterId)) {
                throw new InvalidOperationException($"Importer id '{registration.ImporterId}' is already registered for texture assets.");
            }

            if (textImportersById.ContainsKey(registration.ImporterId)) {
                throw new InvalidOperationException($"Importer id '{registration.ImporterId}' is already registered for text assets.");
            }

            if (ModelImportersById.ContainsKey(registration.ImporterId)) {
                throw new InvalidOperationException($"Importer id '{registration.ImporterId}' is already registered for model assets.");
            }

            fontImportersById.Add(registration.ImporterId, registration.Importer);
            AssetContentManager.RegisterProcessor(
                registration.ImporterId,
                new FontImporterContentProcessor(registration.Importer),
                registration.Extensions);
            string[] extensions = registration.Extensions;
            for (int i = 0; i < extensions.Length; i++) {
                string extension = NormalizeExtension(extensions[i]);
                if (defaultTextureImportersByExtension.ContainsKey(extension)) {
                    throw new InvalidOperationException($"Extension '{extension}' is already mapped to a texture importer.");
                }

                if (defaultTextImportersByExtension.ContainsKey(extension)) {
                    throw new InvalidOperationException($"Extension '{extension}' is already mapped to a text importer.");
                }

                if (DefaultModelImportersByExtension.ContainsKey(extension)) {
                    throw new InvalidOperationException($"Extension '{extension}' is already mapped to a model importer.");
                }

                if (!defaultFontImportersByExtension.ContainsKey(extension)) {
                    defaultFontImportersByExtension[extension] = registration.ImporterId;
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

            if (fontImportersById.ContainsKey(registration.ImporterId)) {
                throw new InvalidOperationException($"Importer id '{registration.ImporterId}' is already registered for font assets.");
            }

            ModelImportersById.Add(registration.ImporterId, registration.Importer);
            AssetContentManager.RegisterProcessor(
                registration.ImporterId,
                new ModelImporterContentProcessor(registration.Importer),
                registration.Extensions);
            string[] extensions = registration.Extensions;
            for (int i = 0; i < extensions.Length; i++) {
                string extension = NormalizeExtension(extensions[i]);
                if (defaultTextureImportersByExtension.ContainsKey(extension)) {
                    throw new InvalidOperationException($"Extension '{extension}' is already mapped to a texture importer.");
                }

                if (defaultTextImportersByExtension.ContainsKey(extension)) {
                    throw new InvalidOperationException($"Extension '{extension}' is already mapped to a text importer.");
                }

                if (defaultFontImportersByExtension.ContainsKey(extension)) {
                    throw new InvalidOperationException($"Extension '{extension}' is already mapped to a font importer.");
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
        /// Gets the identifiers of all registered font importers.
        /// </summary>
        /// <returns>Ordered list of importer identifiers.</returns>
        public IReadOnlyList<string> GetFontImporterIds() {
            List<string> ids = new List<string>(fontImportersById.Count);
            foreach (string importerId in fontImportersById.Keys) {
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
            if (textureImporterIdsByExtension.TryGetValue(normalized, out List<string> textureImporterIds)) {
                return new List<string>(textureImporterIds);
            }

            if (defaultTextImportersByExtension.ContainsKey(normalized)) {
                return GetTextImporterIds();
            }

            if (defaultFontImportersByExtension.ContainsKey(normalized)) {
                return GetFontImporterIds();
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
            return textureImporterIdsByExtension.ContainsKey(normalized);
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
        /// Checks whether the extension maps to a font importer.
        /// </summary>
        /// <param name="extension">File extension to evaluate.</param>
        /// <returns>True when the extension maps to a font importer.</returns>
        public bool IsFontExtension(string extension) {
            if (string.IsNullOrWhiteSpace(extension)) {
                return false;
            }

            string normalized = NormalizeExtension(extension);
            return defaultFontImportersByExtension.ContainsKey(normalized);
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

            if (defaultFontImportersByExtension.ContainsKey(normalized)) {
                throw new InvalidOperationException($"Extension '{normalized}' is already mapped to a font importer.");
            }

            EnsureTextureImporterSupportsExtension(normalized, importerId);
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

            if (defaultFontImportersByExtension.ContainsKey(normalized)) {
                throw new InvalidOperationException($"Extension '{normalized}' is already mapped to a font importer.");
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

            if (defaultFontImportersByExtension.ContainsKey(normalized)) {
                throw new InvalidOperationException($"Extension '{normalized}' is already mapped to a font importer.");
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

            TextureAssetImportSettings settings = LoadOrCreateTextureImportSettings(sourcePath);
            EnsureTextureImportSettingsValid(settings);

            EnsureTextureImporterExists(settings.Importer.ImporterId);
            TextureAsset asset = AssetContentManager.Load<TextureAsset>(sourcePath, settings.Importer.ImporterId);

            if (asset == null) {
                throw new InvalidOperationException($"Texture importer '{settings.Importer.ImporterId}' did not return an asset.");
            }

            TextureAssetProcessorSettings textureProcessorSettings = GetCurrentPlatformTextureProcessorSettings(settings);
            if (textureProcessorSettings.UsesGenericColorFormat()) {
                asset = TextureAssetProcessor.Apply(asset, textureProcessorSettings);
            }
            asset.Id = settings.Importer.AssetId;
            asset.RuntimeAssetId = RuntimeAssetIdGenerator.Generate(settings.Importer.AssetId);

            string outputPath = GetTextureAssetPath(settings.Importer.AssetId);
            EnsureDirectoryForFile(outputPath);
            using (FileStream stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, asset);
            }

            SaveTextureImportSettings(sourcePath, settings);
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

            EnsureTextImporterExists(settings.Importer.ImporterId);
            TextAsset asset = AssetContentManager.Load<TextAsset>(sourcePath, settings.Importer.ImporterId);

            if (asset == null) {
                throw new InvalidOperationException($"Text importer '{settings.Importer.ImporterId}' did not return an asset.");
            }

            asset.Id = settings.Importer.AssetId;
            asset.RuntimeAssetId = RuntimeAssetIdGenerator.Generate(settings.Importer.AssetId);

            string outputPath = GetTextAssetPath(settings.Importer.AssetId);
            EnsureDirectoryForFile(outputPath);
            using (FileStream stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, asset);
            }

            SaveImportSettings(sourcePath, settings);
            return asset;
        }

        /// <summary>
        /// Imports a font asset from a source file and writes it to disk.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the font source file.</param>
        /// <returns>Imported <see cref="FontAsset"/> instance.</returns>
        public FontAsset ImportFont(string sourcePath) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            if (!File.Exists(sourcePath)) {
                throw new FileNotFoundException("Font source file was not found.", sourcePath);
            }

            AssetImportSettings settings = LoadOrCreateImportSettings(sourcePath);
            EnsureImportSettingsValid(settings);

            EnsureFontImporterExists(settings.Importer.ImporterId);
            FontAsset asset = AssetContentManager.Load<FontAsset>(sourcePath, settings.Importer.ImporterId);

            if (asset == null) {
                throw new InvalidOperationException($"Font importer '{settings.Importer.ImporterId}' did not return an asset.");
            }

            if (asset.SourceTextureAsset == null) {
                throw new InvalidOperationException("Font importers must provide one source atlas texture.");
            }

            TextureAssetProcessorSettings textureProcessorSettings = GetCurrentPlatformTextureProcessorSettings(settings);
            if (textureProcessorSettings.UsesGenericColorFormat()) {
                TextureAsset processedSourceTextureAsset = TextureAssetProcessor.Apply(asset.SourceTextureAsset, textureProcessorSettings);
                asset.ApplyProcessedSourceTextureAsset(processedSourceTextureAsset);
            }
            string fontAtlasAssetId = settings.Importer.AssetId + "#atlas";
            asset.SourceTextureAsset.Id = fontAtlasAssetId;
            asset.SourceTextureAsset.RuntimeAssetId = RuntimeAssetIdGenerator.Generate(fontAtlasAssetId);

            string outputPath = GetFontAssetPath(settings.Importer.AssetId);
            EnsureDirectoryForFile(outputPath);
            using (FileStream stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                FontAssetBinarySerializer.Serialize(stream, asset);
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

            ModelAssetImportSettings settings = LoadOrCreateModelImportSettings(sourcePath);
            EnsureModelImportSettingsValid(settings);

            EnsureModelImporterExists(settings.Importer.ImporterId);
            ImportedModelAssetSet importedModel = AssetContentManager.Load<ImportedModelAssetSet>(sourcePath, settings.Importer.ImporterId);
            if (importedModel == null || importedModel.ModelAsset == null) {
                throw new InvalidOperationException($"Model importer '{settings.Importer.ImporterId}' did not return an asset.");
            }

            ModelAsset asset = importedModel.ModelAsset;
            ModelAssetProcessorSettings processorSettings = GetCurrentPlatformModelProcessorSettings(settings);
            ModelAssetProcessor.Apply(asset, processorSettings);
            asset.Id = settings.Importer.AssetId;
            asset.RuntimeAssetId = RuntimeAssetIdGenerator.Generate(settings.Importer.AssetId);
            WriteGeneratedModelMaterials(sourcePath, importedModel.GeneratedMaterials);

            string outputPath = GetModelAssetPath(settings.Importer.AssetId);
            EnsureDirectoryForFile(outputPath);
            using (FileStream stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, asset);
            }

            SaveModelImportSettings(sourcePath, settings);
            return asset;
        }

        /// <summary>
        /// Writes generated model material assets next to the source model using their importer-provided relative paths.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source model file.</param>
        /// <param name="generatedMaterials">Generated material assets to serialize.</param>
        void WriteGeneratedModelMaterials(string sourcePath, ImportedModelMaterialAsset[] generatedMaterials) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            } else if (generatedMaterials == null) {
                throw new ArgumentNullException(nameof(generatedMaterials));
            }

            string sourceDirectoryPath = Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrWhiteSpace(sourceDirectoryPath)) {
                throw new InvalidOperationException("Source model directory could not be resolved.");
            }

            for (int materialIndex = 0; materialIndex < generatedMaterials.Length; materialIndex++) {
                ImportedModelMaterialAsset generatedMaterial = generatedMaterials[materialIndex];
                if (generatedMaterial == null) {
                    throw new InvalidOperationException("Generated model material collections cannot contain null entries.");
                }

                string materialPath = Path.Combine(sourceDirectoryPath, generatedMaterial.RelativeMaterialPath);
                EnsureDirectoryForFile(materialPath);
                MaterialAssetSettingsService settingsService = new MaterialAssetSettingsService();
                MaterialAssetImportSettings settings = CreateGeneratedMaterialSettings(generatedMaterial);
                settingsService.Save(materialPath, settings);
            }
        }

        /// <summary>
        /// Creates authored material settings for one importer-generated companion material using the project's supported platform list.
        /// </summary>
        /// <param name="generatedMaterial">Generated companion material returned by the model importer.</param>
        /// <returns>Material settings that mirror the generated material across every supported platform.</returns>
        MaterialAssetImportSettings CreateGeneratedMaterialSettings(ImportedModelMaterialAsset generatedMaterial) {
            if (generatedMaterial == null) {
                throw new ArgumentNullException(nameof(generatedMaterial));
            } else if (generatedMaterial.MaterialAsset == null) {
                throw new InvalidOperationException("Generated model materials must include a material asset payload.");
            }

            ShaderMaterialAsset materialAsset = generatedMaterial.MaterialAsset;
            MaterialAssetImportSettings settings = new MaterialAssetImportSettings();
            settings.Importer.ImporterId = "helengine.material";
            settings.Importer.SourceChecksum = string.Empty;
            settings.Importer.AssetId = generatedMaterial.RelativeMaterialPath ?? string.Empty;

            IReadOnlyList<string> supportedPlatforms = new EditorProjectPlatformsService(projectRootPath).Load().SupportedPlatforms;
            for (int platformIndex = 0; platformIndex < supportedPlatforms.Count; platformIndex++) {
                string platformId = supportedPlatforms[platformIndex];
                settings.Processor.Platforms[platformId] = CreateGeneratedMaterialPlatformSettings(materialAsset);
            }

            return settings;
        }

        /// <summary>
        /// Creates one effective platform material settings payload that mirrors the generated material fields returned by the model importer.
        /// </summary>
        /// <param name="materialAsset">Generated material asset returned by the model importer.</param>
        /// <returns>Platform settings payload that preserves shader and diffuse-texture fields.</returns>
        MaterialAssetProcessorSettings CreateGeneratedMaterialPlatformSettings(ShaderMaterialAsset materialAsset) {
            if (materialAsset == null) {
                throw new ArgumentNullException(nameof(materialAsset));
            }

            MaterialAssetProcessorSettings settings = new MaterialAssetProcessorSettings();
            settings.SchemaId = "standard-shader";
            settings.FieldValues["use-custom-shader"] = string.Equals(materialAsset.ShaderAssetId, "engine:material:standard", StringComparison.OrdinalIgnoreCase)
                ? "false"
                : "true";
            settings.FieldValues["texture-id"] = materialAsset.DiffuseTextureAssetId ?? string.Empty;
            settings.FieldValues["casts-shadow"] = "true";
            settings.FieldValues["receives-shadow"] = "true";
            settings.FieldValues["base-color"] = "#FFFFFFFF";

            if (string.Equals(settings.FieldValues["use-custom-shader"], "true", StringComparison.Ordinal)) {
                settings.FieldValues["shader-asset-id"] = materialAsset.ShaderAssetId ?? string.Empty;
                settings.FieldValues["vertex-program"] = materialAsset.VertexProgram ?? string.Empty;
                settings.FieldValues["pixel-program"] = materialAsset.PixelProgram ?? string.Empty;
            }

            return settings;
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
            bool repaired = false;
            if (!loadedFromDisk) {
                settings = CreateDefaultSettings(sourcePath);
            } else {
                repaired = RepairLoadedImportSettings(sourcePath, settings);
            }

            UpdateSettingsChecksum(settings, sourcePath);
            if (settingsFileExists && (!loadedFromDisk || repaired)) {
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
                if (!IsTextureExtension(Path.GetExtension(sourcePath))) {
                    continue;
                }

                TextureAssetImportSettings settings;
                if (!TryLoadOrCreateTextureImportSettings(sourcePath, out settings)) {
                    continue;
                }

                if (!IsTextureImporterRegistered(settings.Importer.ImporterId)) {
                    continue;
                }

                string outputPath = GetTextureAssetPath(settings.Importer.AssetId);
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
                if (!IsModelExtension(Path.GetExtension(sourcePath))) {
                    continue;
                }

                ModelAssetImportSettings settings;
                if (!TryLoadOrCreateModelImportSettings(sourcePath, out settings)) {
                    continue;
                }

                if (!IsModelImporterRegistered(settings.Importer.ImporterId)) {
                    continue;
                }

                string outputPath = GetModelAssetPath(settings.Importer.AssetId);
                if (File.Exists(outputPath) && TryLoadCachedModelAsset(outputPath, out _)) {
                    continue;
                }

                try {
                    ImportModel(sourcePath);
                    importedAssets.Add(outputPath);
                } catch (Exception ex) {
                    Logger.WriteError($"Model import failed for '{sourcePath}': {ex.Message}");
                }
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

            TextureAssetImportSettings settings;
            if (!TryLoadOrCreateTextureImportSettings(sourcePath, out settings)) {
                asset = null;
                return false;
            }

            if (!IsTextureImporterRegistered(settings.Importer.ImporterId)) {
                asset = null;
                return false;
            }

            string outputPath = GetTextureAssetPath(settings.Importer.AssetId);
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
        /// Loads one imported texture asset from the cached asset id, recreating missing cache files before retrying.
        /// </summary>
        /// <param name="assetId">Imported texture asset identifier stored in serialized material data.</param>
        /// <param name="asset">Loaded imported texture asset when the cache can be rebuilt.</param>
        /// <returns>True when the imported texture cache could be loaded or recreated.</returns>
        public bool TryLoadImportedTextureAsset(string assetId, out TextureAsset asset) {
            if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Imported texture asset id must be provided.", nameof(assetId));
            }

            asset = null;
            string outputPath = GetTextureAssetPath(assetId);
            if (TryLoadCachedTextureAsset(outputPath, out asset)) {
                return true;
            }

            ImportTexturesMissingCache();
            if (TryLoadCachedTextureAsset(outputPath, out asset)) {
                return true;
            }

            string sourcePath;
            if (!TryResolveImportedTextureSourcePath(assetId, out sourcePath)) {
                asset = null;
                return false;
            }

            if (TryLoadTextureAsset(sourcePath, out asset)) {
                return true;
            }

            if (TryLoadCachedTextureAsset(outputPath, out asset)) {
                return true;
            }

            asset = null;
            return false;
        }

        /// <summary>
        /// Resolves one imported texture asset id to a source texture file inside the project assets tree.
        /// </summary>
        /// <param name="assetId">Imported texture asset identifier stored in serialized material data.</param>
        /// <param name="sourcePath">Resolved source texture file path when the asset can be found on disk.</param>
        /// <returns>True when the asset id maps to one source texture file.</returns>
        /// <summary>
        /// Resolves one imported texture asset id back to the authored source texture file inside the project assets tree.
        /// </summary>
        /// <param name="assetId">Imported texture asset identifier stored in serialized material data.</param>
        /// <param name="sourcePath">Resolved authored source texture file path when the asset can be found on disk.</param>
        /// <returns>True when the asset id maps to one authored source texture file.</returns>
        public bool TryResolveImportedTextureSourcePath(string assetId, out string sourcePath) {
            if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Imported texture asset id must be provided.", nameof(assetId));
            }

            string directPath = Path.GetFullPath(Path.Combine(assetsRootPath, assetId));
            if (File.Exists(directPath)) {
                sourcePath = directPath;
                return true;
            }

            string fileName = Path.GetFileName(assetId);
            if (string.IsNullOrWhiteSpace(fileName)) {
                return TryResolveImportedTextureSourcePathByComputedAssetId(assetId, out sourcePath);
            }

            foreach (string candidatePath in EnumerateAssetSourceFiles()) {
                if (string.Equals(Path.GetFileName(candidatePath), fileName, StringComparison.OrdinalIgnoreCase)) {
                    sourcePath = candidatePath;
                    return true;
                }
            }

            return TryResolveImportedTextureSourcePathByComputedAssetId(assetId, out sourcePath);
        }

        /// <summary>
        /// Resolves one imported texture asset id by recomputing asset ids for every authored texture source file.
        /// </summary>
        /// <param name="assetId">Imported texture asset identifier stored in serialized material data.</param>
        /// <param name="sourcePath">Resolved authored source texture file path when the asset can be found on disk.</param>
        /// <returns>True when the asset id maps to one authored source texture file.</returns>
        bool TryResolveImportedTextureSourcePathByComputedAssetId(string assetId, out string sourcePath) {
            if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Imported texture asset id must be provided.", nameof(assetId));
            }

            foreach (string candidatePath in EnumerateAssetSourceFiles()) {
                if (!IsTextureExtension(Path.GetExtension(candidatePath))) {
                    continue;
                }

                TextureAssetImportSettings settings;
                if (!TryLoadOrCreateTextureImportSettings(candidatePath, out settings) || settings == null) {
                    continue;
                }

                if (!MatchesComputedTextureAssetId(candidatePath, settings, assetId)) {
                    continue;
                }

                sourcePath = candidatePath;
                return true;
            }

            sourcePath = string.Empty;
            return false;
        }

        /// <summary>
        /// Returns whether one authored texture source can produce the supplied imported texture asset id under any relevant platform texture settings.
        /// </summary>
        /// <param name="sourcePath">Absolute source texture path being evaluated.</param>
        /// <param name="settings">Resolved texture import settings for the source file.</param>
        /// <param name="assetId">Imported texture asset identifier being matched.</param>
        /// <returns>True when the source/settings combination can produce the supplied imported texture asset id.</returns>
        bool MatchesComputedTextureAssetId(string sourcePath, TextureAssetImportSettings settings, string assetId) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Imported texture asset id must be provided.", nameof(assetId));
            }

            string sourceChecksum = settings.Importer?.SourceChecksum;
            if (string.IsNullOrWhiteSpace(sourceChecksum)) {
                sourceChecksum = fileHasher.ComputeHash(sourcePath);
            }

            if (settings.Importer != null &&
                !string.IsNullOrWhiteSpace(settings.Importer.ImporterId) &&
                string.Equals(BuildImporterQualifiedAssetId(sourceChecksum, settings.Importer.ImporterId), assetId, StringComparison.OrdinalIgnoreCase)) {
                return true;
            }

            List<string> candidatePlatformIds = BuildTextureAssetIdCandidatePlatformIds(settings);
            for (int index = 0; index < candidatePlatformIds.Count; index++) {
                string candidateAssetId = BuildTextureAssetId(sourcePath, settings, sourceChecksum, candidatePlatformIds[index]);
                if (string.Equals(candidateAssetId, assetId, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Builds the platform ids whose texture processor settings should be considered when matching one imported texture asset id back to its authored source file.
        /// </summary>
        /// <param name="settings">Resolved texture import settings for the candidate source file.</param>
        /// <returns>Ordered platform ids that can produce texture cache identifiers for the candidate source.</returns>
        List<string> BuildTextureAssetIdCandidatePlatformIds(TextureAssetImportSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            List<string> platformIds = new List<string>();
            AddTextureAssetIdCandidatePlatformId(platformIds, string.Empty);
            AddTextureAssetIdCandidatePlatformId(platformIds, CurrentPlatformId);

            if (settings.Processor != null && settings.Processor.Platforms != null) {
                foreach (string platformId in settings.Processor.Platforms.Keys) {
                    AddTextureAssetIdCandidatePlatformId(platformIds, platformId);
                }
            }

            return platformIds;
        }

        /// <summary>
        /// Adds one candidate platform id to the imported-texture source-resolution probe set when it has not been recorded already.
        /// </summary>
        /// <param name="platformIds">Mutable candidate platform id collection.</param>
        /// <param name="platformId">Platform id to record.</param>
        void AddTextureAssetIdCandidatePlatformId(List<string> platformIds, string platformId) {
            if (platformIds == null) {
                throw new ArgumentNullException(nameof(platformIds));
            }

            string normalizedPlatformId = platformId ?? string.Empty;
            for (int index = 0; index < platformIds.Count; index++) {
                if (string.Equals(platformIds[index], normalizedPlatformId, StringComparison.OrdinalIgnoreCase)) {
                    return;
                }
            }

            platformIds.Add(normalizedPlatformId);
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

            ModelAssetImportSettings settings;
            if (!TryLoadOrCreateModelImportSettings(sourcePath, out settings)) {
                asset = null;
                return false;
            }

            if (!IsTextImporterRegistered(settings.Importer.ImporterId)) {
                asset = null;
                return false;
            }

            string outputPath = GetTextAssetPath(settings.Importer.AssetId);
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
        /// Loads a font asset for a source file, importing it when needed.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the font source file.</param>
        /// <param name="asset">Loaded font asset when available.</param>
        /// <returns>True when the source can be resolved to a font asset.</returns>
        public bool TryLoadFontAsset(string sourcePath, out FontAsset asset) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            if (!File.Exists(sourcePath)) {
                throw new FileNotFoundException("Font source file was not found.", sourcePath);
            }

            AssetImportSettings settings;
            if (!TryLoadOrCreateImportSettings(sourcePath, out settings)) {
                asset = null;
                return false;
            }

            if (!IsFontImporterRegistered(settings.Importer.ImporterId)) {
                asset = null;
                return false;
            }

            string outputPath = GetFontAssetPath(settings.Importer.AssetId);
            if (!File.Exists(outputPath)) {
                asset = ImportFont(sourcePath);
                return true;
            }

            if (TryLoadCachedFontAsset(outputPath, out asset)) {
                return true;
            }

            asset = ImportFont(sourcePath);
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

            ModelAssetImportSettings settings;
            if (!TryLoadOrCreateModelImportSettings(sourcePath, out settings)) {
                asset = null;
                return false;
            }

            if (!IsModelImporterRegistered(settings.Importer.ImporterId)) {
                asset = null;
                return false;
            }

            string outputPath = GetModelAssetPath(settings.Importer.AssetId);
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
        /// Attempts to load a cached font asset.
        /// </summary>
        /// <param name="outputPath">Absolute path to the cached font asset.</param>
        /// <param name="asset">Loaded font asset when the cache file exists and contains the expected payload type.</param>
        /// <returns>True when the cached asset was loaded successfully.</returns>
        bool TryLoadCachedFontAsset(string outputPath, out FontAsset asset) {
            if (string.IsNullOrWhiteSpace(outputPath)) {
                throw new ArgumentException("Output path must be provided.", nameof(outputPath));
            }

            asset = null;
            if (!File.Exists(outputPath)) {
                return false;
            }

            try {
                using FileStream stream = new FileStream(outputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                asset = RestoreRuntimeTextureForCachedFontAsset(FontAssetBinarySerializer.Deserialize(stream));
                return true;
            } catch {
                asset = null;
                return false;
            }
        }

        /// <summary>
        /// Rebuilds the runtime atlas texture required by editor rendering when a cached font asset was deserialized without one.
        /// </summary>
        /// <param name="asset">Cached font asset that may need its runtime atlas restored.</param>
        /// <returns>The original asset when it already owns a runtime texture; otherwise a replacement asset with a rebuilt runtime atlas.</returns>
        FontAsset RestoreRuntimeTextureForCachedFontAsset(FontAsset asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            if (asset.Texture != null || asset.SourceTextureAsset == null) {
                return asset;
            }

            if (Core.Instance == null || Core.Instance.RenderManager2D == null) {
                throw new InvalidOperationException("Cached font assets require an initialized 2D renderer before their runtime atlas can be restored.");
            }

            RuntimeTexture runtimeTexture = Core.Instance.RenderManager2D.BuildTextureFromRaw(asset.SourceTextureAsset);
            FontAsset restoredAsset = new FontAsset(
                asset.FontInfo,
                runtimeTexture,
                asset.Characters,
                asset.LineHeight,
                asset.AtlasWidth,
                asset.AtlasHeight) {
                SourceTextureAsset = asset.SourceTextureAsset,
                CookedAtlasTextureRelativePath = asset.CookedAtlasTextureRelativePath
            };
            return restoredAsset;
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
            if (IsStaleEditorAssetCache(outputPath)) {
                DeleteCacheFile(outputPath);
                return false;
            }

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
        /// Determines whether a cached asset file uses an older editor asset payload version.
        /// </summary>
        /// <param name="outputPath">Absolute path to the cached asset file.</param>
        /// <returns>True when the cache file should be regenerated using the current serializer version.</returns>
        bool IsStaleEditorAssetCache(string outputPath) {
            using (FileStream stream = new FileStream(outputPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
                return header.FormatId == EditorAssetBinarySerializer.FormatId &&
                    header.Version != EditorAssetBinarySerializer.CurrentVersion;
            }
        }

        /// <summary>
        /// Deletes a cached asset file so it can be regenerated from source content.
        /// </summary>
        /// <param name="outputPath">Absolute path to the cached asset file.</param>
        void DeleteCacheFile(string outputPath) {
            File.Delete(outputPath);
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

            asset = null;
            if (!File.Exists(outputPath)) {
                return false;
            }

            try {
                using (FileStream stream = new FileStream(outputPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                    asset = AssetSerializer.Deserialize(stream);
                }
                return true;
            } catch {
                asset = null;
                return false;
            }
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

            try {
                using (FileStream stream = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                    settings = AssetImportSettingsBinarySerializer.Deserialize(stream);
                }
                return true;
            } catch {
                settings = null;
                return false;
            }
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
                Importer = new AssetImporterSettings {
                    ImporterId = importerId
                }
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
            try {
                if (settingsFileExists && TryLoadImportSettings(settingsPath, out settings)) {
                    bool repaired = RepairLoadedImportSettings(sourcePath, settings);
                    UpdateSettingsChecksum(settings, sourcePath);
                    if (repaired) {
                        SaveImportSettings(sourcePath, settings);
                    }
                    return true;
                }
            } catch (Exception ex) {
                throw new InvalidOperationException($"Failed to load generic import settings for source '{sourcePath}'.", ex);
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
        /// Repairs loaded import settings when the importer id is missing or no longer valid for the source extension.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <param name="settings">Loaded settings that may need importer normalization.</param>
        /// <returns>True when the importer id was replaced with the registered default importer.</returns>
        bool RepairLoadedImportSettings(string sourcePath, AssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            string extension = Path.GetExtension(sourcePath);
            IReadOnlyList<string> importerIds = GetImporterIdsForExtension(extension);
            if (importerIds.Count == 0) {
                return false;
            }

            string currentImporterId = string.Empty;
            if (settings.Importer != null) {
                currentImporterId = settings.Importer.ImporterId;
            }

            if (!string.IsNullOrWhiteSpace(currentImporterId)) {
                for (int index = 0; index < importerIds.Count; index++) {
                    if (string.Equals(importerIds[index], currentImporterId, StringComparison.OrdinalIgnoreCase)) {
                        return false;
                    }
                }
            }

            string defaultImporterId = ResolveDefaultImporter(extension);
            if (settings.Importer == null) {
                settings.Importer = new AssetImporterSettings();
            }

            settings.Importer.ImporterId = defaultImporterId;
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
                Importer = new AssetImporterSettings {
                    ImporterId = importerId
                }
            };
            return true;
        }

        /// <summary>
        /// Attempts to load typed texture import settings from a settings file.
        /// </summary>
        /// <param name="settingsPath">Absolute path to the settings file.</param>
        /// <param name="settings">Deserialized settings when the file exists.</param>
        /// <returns>True when the settings file was loaded successfully.</returns>
        bool TryLoadTextureImportSettings(string settingsPath, out TextureAssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(settingsPath)) {
                throw new ArgumentException("Settings path must be provided.", nameof(settingsPath));
            }

            settings = null;
            if (!File.Exists(settingsPath)) {
                return false;
            }

            try {
                using FileStream stream = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                settings = TextureAssetImportSettingsBinarySerializer.Deserialize(stream);
                return true;
            } catch {
                settings = null;
                return false;
            }
        }

        /// <summary>
        /// Attempts to load typed model import settings from a settings file.
        /// </summary>
        /// <param name="settingsPath">Absolute path to the settings file.</param>
        /// <param name="settings">Deserialized settings when the file exists.</param>
        /// <returns>True when the settings file was loaded successfully.</returns>
        bool TryLoadModelImportSettings(string settingsPath, out ModelAssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(settingsPath)) {
                throw new ArgumentException("Settings path must be provided.", nameof(settingsPath));
            }

            settings = null;
            if (!File.Exists(settingsPath)) {
                return false;
            }

            try {
                using FileStream stream = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                settings = ModelAssetImportSettingsBinarySerializer.Deserialize(stream);
                return true;
            } catch {
                settings = null;
                return false;
            }
        }

        /// <summary>
        /// Creates new typed texture import settings based on the source file extension.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <returns>Newly created settings.</returns>
        TextureAssetImportSettings CreateDefaultTextureImportSettings(string sourcePath) {
            string extension = Path.GetExtension(sourcePath);
            string importerId = ResolveDefaultImporter(extension);
            return new TextureAssetImportSettings {
                Importer = new AssetImporterSettings {
                    ImporterId = importerId
                }
            };
        }

        /// <summary>
        /// Creates new typed model import settings based on the source file extension.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <returns>Newly created settings.</returns>
        ModelAssetImportSettings CreateDefaultModelImportSettings(string sourcePath) {
            string extension = Path.GetExtension(sourcePath);
            string importerId = ResolveDefaultImporter(extension);
            return new ModelAssetImportSettings {
                Importer = new AssetImporterSettings {
                    ImporterId = importerId
                }
            };
        }

        /// <summary>
        /// Attempts to create default typed texture import settings for a source file.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <param name="settings">Created settings when defaults are available.</param>
        /// <returns>True when settings were created from a registered default.</returns>
        bool TryCreateDefaultTextureImportSettings(string sourcePath, out TextureAssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            string extension = Path.GetExtension(sourcePath);
            string importerId;
            if (!TryResolveDefaultImporter(extension, out importerId)) {
                settings = null;
                return false;
            }

            settings = new TextureAssetImportSettings {
                Importer = new AssetImporterSettings {
                    ImporterId = importerId
                }
            };
            return true;
        }

        /// <summary>
        /// Attempts to create default typed model import settings for a source file.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <param name="settings">Created settings when defaults are available.</param>
        /// <returns>True when settings were created from a registered default.</returns>
        bool TryCreateDefaultModelImportSettings(string sourcePath, out ModelAssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            string extension = Path.GetExtension(sourcePath);
            string importerId;
            if (!TryResolveDefaultImporter(extension, out importerId)) {
                settings = null;
                return false;
            }

            settings = new ModelAssetImportSettings {
                Importer = new AssetImporterSettings {
                    ImporterId = importerId
                }
            };
            return true;
        }

        /// <summary>
        /// Repairs loaded typed texture import settings when the importer id is missing or no longer valid for the source extension.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <param name="settings">Loaded settings that may need importer normalization.</param>
        /// <returns>True when the importer id was replaced with the registered default importer.</returns>
        bool RepairTextureImporterId(string sourcePath, TextureAssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            return RepairImporterSettings(sourcePath, settings.Importer);
        }

        /// <summary>
        /// Repairs loaded typed model import settings when the importer id is missing or no longer valid for the source extension.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <param name="settings">Loaded settings that may need importer normalization.</param>
        /// <returns>True when the importer id was replaced with the registered default importer.</returns>
        bool RepairModelImporterId(string sourcePath, ModelAssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            return RepairImporterSettings(sourcePath, settings.Importer);
        }

        /// <summary>
        /// Repairs importer metadata when the importer id is missing or no longer valid for the source extension.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <param name="settings">Importer metadata to repair.</param>
        /// <returns>True when the importer id was replaced with the registered default importer.</returns>
        bool RepairImporterSettings(string sourcePath, AssetImporterSettings settings) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            string extension = Path.GetExtension(sourcePath);
            IReadOnlyList<string> importerIds = GetImporterIdsForExtension(extension);
            if (importerIds.Count == 0) {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(settings.ImporterId)) {
                for (int index = 0; index < importerIds.Count; index++) {
                    if (string.Equals(importerIds[index], settings.ImporterId, StringComparison.OrdinalIgnoreCase)) {
                        return false;
                    }
                }
            }

            settings.ImporterId = ResolveDefaultImporter(extension);
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

            if (settings.Importer == null) {
                throw new InvalidOperationException("Import settings must include importer settings.");
            }

            if (settings.Processor == null) {
                throw new InvalidOperationException("Import settings must include processor settings.");
            }

            if (settings.Processor.Platforms == null) {
                throw new InvalidOperationException("Import settings must include processor platform settings.");
            }

            if (string.IsNullOrWhiteSpace(settings.Importer.ImporterId)) {
                throw new InvalidOperationException("Import settings must specify an importer id.");
            }

            if (string.IsNullOrWhiteSpace(settings.Importer.AssetId)) {
                throw new InvalidOperationException("Import settings must specify an asset id.");
            }
        }

        /// <summary>
        /// Loads typed texture import settings for a source file or creates defaults if missing.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <returns>Resolved typed texture import settings.</returns>
        public TextureAssetImportSettings LoadOrCreateTextureImportSettings(string sourcePath) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            string settingsPath = GetSettingsPath(sourcePath);
            bool settingsFileExists = File.Exists(settingsPath);
            TextureAssetImportSettings settings = null;
            bool loadedFromDisk = settingsFileExists && TryLoadTextureImportSettings(settingsPath, out settings);
            bool repaired = false;
            if (!loadedFromDisk) {
                try {
                    settings = CreateDefaultTextureImportSettings(sourcePath);
                } catch {
                    settings = new TextureAssetImportSettings();
                }
            } else {
                repaired = RepairTextureImporterId(sourcePath, settings);
            }

            UpdateTextureImportSettingsChecksum(settings, sourcePath);
            if (settingsFileExists && (!loadedFromDisk || repaired)) {
                SaveTextureImportSettings(sourcePath, settings);
            }

            return settings;
        }

        /// <summary>
        /// Saves typed texture import settings next to the specified source file.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <param name="settings">Settings to serialize.</param>
        public void SaveTextureImportSettings(string sourcePath, TextureAssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            string settingsPath = GetSettingsPath(sourcePath);
            EnsureDirectoryForFile(settingsPath);
            using FileStream stream = new FileStream(settingsPath, FileMode.Create, FileAccess.Write, FileShare.None);
            TextureAssetImportSettingsBinarySerializer.Serialize(stream, settings);
        }

        /// <summary>
        /// Attempts to load typed texture import settings or create defaults when missing.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <param name="settings">Resolved settings when available.</param>
        /// <returns>True when settings could be resolved for the source file.</returns>
        public bool TryLoadOrCreateTextureImportSettings(string sourcePath, out TextureAssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            string settingsPath = GetSettingsPath(sourcePath);
            bool settingsFileExists = File.Exists(settingsPath);
            if (settingsFileExists && TryLoadTextureImportSettings(settingsPath, out settings)) {
                bool repaired = RepairTextureImporterId(sourcePath, settings);
                UpdateTextureImportSettingsChecksum(settings, sourcePath);
                if (repaired) {
                    SaveTextureImportSettings(sourcePath, settings);
                }
                return true;
            }

            if (!TryCreateDefaultTextureImportSettings(sourcePath, out settings)) {
                settings = new TextureAssetImportSettings();
            }

            UpdateTextureImportSettingsChecksum(settings, sourcePath);
            if (settingsFileExists) {
                SaveTextureImportSettings(sourcePath, settings);
            }

            return true;
        }

        /// <summary>
        /// Loads typed model import settings for a source file or creates defaults if missing.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <returns>Resolved typed model import settings.</returns>
        public ModelAssetImportSettings LoadOrCreateModelImportSettings(string sourcePath) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            string settingsPath = GetSettingsPath(sourcePath);
            bool settingsFileExists = File.Exists(settingsPath);
            ModelAssetImportSettings settings = null;
            bool loadedFromDisk = false;
            try {
                loadedFromDisk = settingsFileExists && TryLoadModelImportSettings(settingsPath, out settings);
            } catch (Exception ex) {
                throw new InvalidOperationException($"Failed to load model import settings for source '{sourcePath}'.", ex);
            }
            bool repaired = false;
            if (!loadedFromDisk) {
                try {
                    settings = CreateDefaultModelImportSettings(sourcePath);
                } catch {
                    settings = new ModelAssetImportSettings();
                }
            } else {
                repaired = RepairModelImporterId(sourcePath, settings);
            }

            UpdateModelImportSettingsChecksum(settings, sourcePath);
            if (settingsFileExists && (!loadedFromDisk || repaired)) {
                SaveModelImportSettings(sourcePath, settings);
            }

            return settings;
        }

        /// <summary>
        /// Saves typed model import settings next to the specified source file.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <param name="settings">Settings to serialize.</param>
        public void SaveModelImportSettings(string sourcePath, ModelAssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            string settingsPath = GetSettingsPath(sourcePath);
            EnsureDirectoryForFile(settingsPath);
            using FileStream stream = new FileStream(settingsPath, FileMode.Create, FileAccess.Write, FileShare.None);
            ModelAssetImportSettingsBinarySerializer.Serialize(stream, settings);
        }

        /// <summary>
        /// Attempts to load typed model import settings or create defaults when missing.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <param name="settings">Resolved settings when available.</param>
        /// <returns>True when settings could be resolved for the source file.</returns>
        public bool TryLoadOrCreateModelImportSettings(string sourcePath, out ModelAssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            string settingsPath = GetSettingsPath(sourcePath);
            bool settingsFileExists = File.Exists(settingsPath);
            try {
                if (settingsFileExists && TryLoadModelImportSettings(settingsPath, out settings)) {
                    bool repaired = RepairModelImporterId(sourcePath, settings);
                    UpdateModelImportSettingsChecksum(settings, sourcePath);
                    if (repaired) {
                        SaveModelImportSettings(sourcePath, settings);
                    }
                    return true;
                }
            } catch (Exception ex) {
                throw new InvalidOperationException($"Failed to load model import settings for source '{sourcePath}'.", ex);
            }

            if (!TryCreateDefaultModelImportSettings(sourcePath, out settings)) {
                settings = new ModelAssetImportSettings();
            }

            UpdateModelImportSettingsChecksum(settings, sourcePath);
            if (settingsFileExists) {
                SaveModelImportSettings(sourcePath, settings);
            }

            return true;
        }

        /// <summary>
        /// Ensures required typed texture settings fields are populated.
        /// </summary>
        /// <param name="settings">Settings to validate.</param>
        void EnsureTextureImportSettingsValid(TextureAssetImportSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (settings.Importer == null) {
                throw new InvalidOperationException("Texture import settings must include importer settings.");
            } else if (settings.Processor == null || settings.Processor.Platforms == null) {
                throw new InvalidOperationException("Texture import settings must include processor platform settings.");
            } else if (string.IsNullOrWhiteSpace(settings.Importer.ImporterId)) {
                throw new InvalidOperationException("Texture import settings must specify an importer id.");
            } else if (string.IsNullOrWhiteSpace(settings.Importer.AssetId)) {
                throw new InvalidOperationException("Texture import settings must specify an asset id.");
            }
        }

        /// <summary>
        /// Ensures required typed model settings fields are populated.
        /// </summary>
        /// <param name="settings">Settings to validate.</param>
        void EnsureModelImportSettingsValid(ModelAssetImportSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (settings.Importer == null) {
                throw new InvalidOperationException("Model import settings must include importer settings.");
            } else if (settings.Processor == null || settings.Processor.Platforms == null) {
                throw new InvalidOperationException("Model import settings must include processor platform settings.");
            } else if (string.IsNullOrWhiteSpace(settings.Importer.ImporterId)) {
                throw new InvalidOperationException("Model import settings must specify an importer id.");
            } else if (string.IsNullOrWhiteSpace(settings.Importer.AssetId)) {
                throw new InvalidOperationException("Model import settings must specify an asset id.");
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
            string fontImporterId;
            string modelImporterId;
            bool hasTexture = defaultTextureImportersByExtension.TryGetValue(normalized, out textureImporterId);
            bool hasText = defaultTextImportersByExtension.TryGetValue(normalized, out textImporterId);
            bool hasFont = defaultFontImportersByExtension.TryGetValue(normalized, out fontImporterId);
            bool hasModel = DefaultModelImportersByExtension.TryGetValue(normalized, out modelImporterId);

            int typeCount = 0;
            if (hasTexture) {
                typeCount++;
            }
            if (hasText) {
                typeCount++;
            }
            if (hasFont) {
                typeCount++;
            }
            if (hasModel) {
                typeCount++;
            }

            if (typeCount > 1) {
                throw new InvalidOperationException($"Multiple importer types are registered for '{normalized}'.");
            }

            if (hasTexture) {
                return textureImporterId;
            }

            if (hasText) {
                return textImporterId;
            }

            if (hasFont) {
                return fontImporterId;
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
            string fontImporterId;
            string modelImporterId;
            bool hasTexture = defaultTextureImportersByExtension.TryGetValue(normalized, out textureImporterId);
            bool hasText = defaultTextImportersByExtension.TryGetValue(normalized, out textImporterId);
            bool hasFont = defaultFontImportersByExtension.TryGetValue(normalized, out fontImporterId);
            bool hasModel = DefaultModelImportersByExtension.TryGetValue(normalized, out modelImporterId);

            int typeCount = 0;
            if (hasTexture) {
                typeCount++;
            }
            if (hasText) {
                typeCount++;
            }
            if (hasFont) {
                typeCount++;
            }
            if (hasModel) {
                typeCount++;
            }

            if (typeCount > 1) {
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

            if (hasFont) {
                importerId = fontImporterId;
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
        /// Records that one texture importer supports one file extension.
        /// </summary>
        /// <param name="extension">Normalized file extension.</param>
        /// <param name="importerId">Importer identifier that supports the extension.</param>
        void RegisterTextureImporterExtension(string extension, string importerId) {
            if (string.IsNullOrWhiteSpace(extension)) {
                throw new ArgumentException("Extension must be provided.", nameof(extension));
            }

            if (string.IsNullOrWhiteSpace(importerId)) {
                throw new ArgumentException("Importer id must be provided.", nameof(importerId));
            }

            List<string> importerIds;
            if (!textureImporterIdsByExtension.TryGetValue(extension, out importerIds)) {
                importerIds = new List<string>();
                textureImporterIdsByExtension.Add(extension, importerIds);
            }

            if (!importerIds.Contains(importerId, StringComparer.OrdinalIgnoreCase)) {
                importerIds.Add(importerId);
            }
        }

        /// <summary>
        /// Ensures the supplied texture importer has been registered for the requested file extension.
        /// </summary>
        /// <param name="extension">Normalized file extension.</param>
        /// <param name="importerId">Importer identifier to validate.</param>
        void EnsureTextureImporterSupportsExtension(string extension, string importerId) {
            if (string.IsNullOrWhiteSpace(extension)) {
                throw new ArgumentException("Extension must be provided.", nameof(extension));
            }

            if (string.IsNullOrWhiteSpace(importerId)) {
                throw new ArgumentException("Importer id must be provided.", nameof(importerId));
            }

            List<string> importerIds;
            if (!textureImporterIdsByExtension.TryGetValue(extension, out importerIds)) {
                throw new InvalidOperationException($"No texture importers are registered for '{extension}'.");
            }

            for (int index = 0; index < importerIds.Count; index++) {
                if (string.Equals(importerIds[index], importerId, StringComparison.OrdinalIgnoreCase)) {
                    return;
                }
            }

            throw new InvalidOperationException($"Texture importer '{importerId}' does not support '{extension}'.");
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
        /// Retrieves a font importer by identifier.
        /// </summary>
        /// <param name="importerId">Identifier of the importer.</param>
        /// <returns>Importer implementation.</returns>
        IFontImporter GetFontImporter(string importerId) {
            IFontImporter importer;
            if (fontImportersById.TryGetValue(importerId, out importer)) {
                return importer;
            }

            throw new InvalidOperationException($"Font importer '{importerId}' is not registered.");
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
        /// Ensures a font importer is registered.
        /// </summary>
        /// <param name="importerId">Identifier to verify.</param>
        void EnsureFontImporterExists(string importerId) {
            if (!fontImportersById.ContainsKey(importerId)) {
                throw new InvalidOperationException($"Font importer '{importerId}' is not registered.");
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
        /// Checks whether a font importer is registered.
        /// </summary>
        /// <param name="importerId">Identifier to verify.</param>
        /// <returns>True when a matching importer is registered.</returns>
        bool IsFontImporterRegistered(string importerId) {
            if (string.IsNullOrWhiteSpace(importerId)) {
                return false;
            }

            return fontImportersById.ContainsKey(importerId);
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
            settings.Importer.SourceChecksum = checksum;
            settings.Importer.AssetId = BuildAssetId(sourcePath, settings, checksum);
        }

        /// <summary>
        /// Updates typed texture import settings to store the current source checksum.
        /// </summary>
        /// <param name="settings">Settings to update.</param>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        void UpdateTextureImportSettingsChecksum(TextureAssetImportSettings settings, string sourcePath) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            string checksum = fileHasher.ComputeHash(sourcePath);
            settings.Importer.SourceChecksum = checksum;
            settings.Importer.AssetId = BuildTextureAssetId(sourcePath, settings, checksum);
        }

        /// <summary>
        /// Updates typed model import settings to store the current source checksum.
        /// </summary>
        /// <param name="settings">Settings to update.</param>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        void UpdateModelImportSettingsChecksum(ModelAssetImportSettings settings, string sourcePath) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            string checksum = fileHasher.ComputeHash(sourcePath);
            settings.Importer.SourceChecksum = checksum;
            settings.Importer.AssetId = BuildModelAssetId(settings, checksum);
        }

        /// <summary>
        /// Builds the processed asset identifier that should be used for the current source file and settings.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <param name="settings">Resolved import settings for the source file.</param>
        /// <param name="sourceChecksum">Checksum of the source file contents.</param>
        /// <returns>Processed asset identifier for the current configuration.</returns>
        string BuildAssetId(string sourcePath, AssetImportSettings settings, string sourceChecksum) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }
            if (string.IsNullOrWhiteSpace(sourceChecksum)) {
                throw new ArgumentException("Source checksum must be provided.", nameof(sourceChecksum));
            }

            if (!IsModelSourceForAssetId(sourcePath, settings)) {
                if (IsTextureImporterRegistered(settings.Importer.ImporterId)) {
                    string texturePlatformId = ResolveTextureProcessorPlatformId(settings);
                    TextureAssetProcessorSettings textureProcessorSettings = GetCurrentPlatformTextureProcessorSettings(settings);
                    string textureIdentity = string.Concat(
                        "texture", "\n",
                        sourceChecksum, "\n",
                        settings.Importer.ImporterId ?? string.Empty, "\n",
                        texturePlatformId, "\n",
                        textureProcessorSettings.MaxResolution.ToString(System.Globalization.CultureInfo.InvariantCulture), "\n",
                        textureProcessorSettings.ColorFormatId ?? string.Empty, "\n",
                        ((int)textureProcessorSettings.AlphaPrecision).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    byte[] textureIdentityBytes = System.Text.Encoding.UTF8.GetBytes(textureIdentity);
                    byte[] textureHashBytes = System.Security.Cryptography.SHA256.HashData(textureIdentityBytes);
                    return Convert.ToHexString(textureHashBytes).ToLowerInvariant();
                }

                if (IsFontImporterRegistered(settings.Importer.ImporterId)) {
                    string texturePlatformId = ResolveTextureProcessorPlatformId(settings);
                    TextureAssetProcessorSettings textureProcessorSettings = GetCurrentPlatformTextureProcessorSettings(settings);
                    string fontIdentity = string.Concat(
                        "font", "\n",
                        sourceChecksum, "\n",
                        settings.Importer.ImporterId ?? string.Empty, "\n",
                        texturePlatformId, "\n",
                        textureProcessorSettings.MaxResolution.ToString(System.Globalization.CultureInfo.InvariantCulture), "\n",
                        textureProcessorSettings.ColorFormatId ?? string.Empty, "\n",
                        ((int)textureProcessorSettings.AlphaPrecision).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    byte[] fontIdentityBytes = System.Text.Encoding.UTF8.GetBytes(fontIdentity);
                    byte[] fontHashBytes = System.Security.Cryptography.SHA256.HashData(fontIdentityBytes);
                    return Convert.ToHexString(fontHashBytes).ToLowerInvariant();
                }

                if (ShouldUseTextureImporterQualifiedAssetId(sourcePath, settings)) {
                    return BuildImporterQualifiedAssetId(sourceChecksum, settings.Importer.ImporterId);
                }

                return sourceChecksum;
            }

            string platformId = ResolveModelProcessorPlatformId(settings);
            ModelAssetProcessorSettings processorSettings = GetCurrentPlatformModelProcessorSettings(settings);
            string flipWindingFlag = processorSettings.FlipWinding ? "1" : "0";
            string identity = string.Concat(
                "model", "\n",
                sourceChecksum, "\n",
                settings.Importer.ImporterId ?? string.Empty, "\n",
                platformId, "\n",
                flipWindingFlag);
            byte[] identityBytes = System.Text.Encoding.UTF8.GetBytes(identity);
            byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(identityBytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        /// <summary>
        /// Builds the processed texture asset identifier for the current source file and typed settings.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <param name="settings">Resolved typed texture import settings for the source file.</param>
        /// <param name="sourceChecksum">Checksum of the source file contents.</param>
        /// <returns>Processed asset identifier for the current configuration.</returns>
        string BuildTextureAssetId(string sourcePath, TextureAssetImportSettings settings, string sourceChecksum) {
            return BuildTextureAssetId(sourcePath, settings, sourceChecksum, ResolveTextureProcessorPlatformId(settings));
        }

        /// <summary>
        /// Builds the processed texture asset identifier for one explicit platform texture-settings context.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <param name="settings">Resolved typed texture import settings for the source file.</param>
        /// <param name="sourceChecksum">Checksum of the source file contents.</param>
        /// <param name="platformId">Platform texture-settings key that should drive identity generation.</param>
        /// <returns>Processed asset identifier for the supplied platform texture-settings context.</returns>
        string BuildTextureAssetId(string sourcePath, TextureAssetImportSettings settings, string sourceChecksum, string platformId) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (string.IsNullOrWhiteSpace(sourceChecksum)) {
                throw new ArgumentException("Source checksum must be provided.", nameof(sourceChecksum));
            }

            TextureAssetProcessorSettings processorSettings = GetTextureProcessorSettings(settings, platformId);
            string identity = string.Concat(
                "texture", "\n",
                sourceChecksum, "\n",
                settings.Importer.ImporterId ?? string.Empty, "\n",
                platformId, "\n",
                processorSettings.MaxResolution.ToString(System.Globalization.CultureInfo.InvariantCulture), "\n",
                processorSettings.ColorFormatId ?? string.Empty, "\n",
                ((int)processorSettings.AlphaPrecision).ToString(System.Globalization.CultureInfo.InvariantCulture));
            byte[] identityBytes = System.Text.Encoding.UTF8.GetBytes(identity);
            byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(identityBytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        /// <summary>
        /// Resolves the texture processor settings for one explicit platform id, returning defaults when none were saved yet.
        /// </summary>
        /// <param name="settings">Resolved typed texture settings for the source file.</param>
        /// <param name="platformId">Platform texture-settings key that should drive the returned processor settings.</param>
        /// <returns>Texture processor settings for the requested platform context.</returns>
        TextureAssetProcessorSettings GetTextureProcessorSettings(TextureAssetImportSettings settings, string platformId) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            string normalizedPlatformId = platformId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedPlatformId)) {
                return CreateDefaultTextureProcessorSettings(normalizedPlatformId);
            }

            if (settings.Processor == null || settings.Processor.Platforms == null) {
                return CreateDefaultTextureProcessorSettings(normalizedPlatformId);
            }

            TextureAssetProcessorSettings platformSettings;
            if (!settings.Processor.Platforms.TryGetValue(normalizedPlatformId, out platformSettings) || platformSettings == null) {
                return CreateDefaultTextureProcessorSettings(normalizedPlatformId);
            }

            return platformSettings;
        }

        /// <summary>
        /// Builds the processed model asset identifier for the current typed settings.
        /// </summary>
        /// <param name="settings">Resolved typed model import settings for the source file.</param>
        /// <param name="sourceChecksum">Checksum of the source file contents.</param>
        /// <returns>Processed asset identifier for the current configuration.</returns>
        string BuildModelAssetId(ModelAssetImportSettings settings, string sourceChecksum) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (string.IsNullOrWhiteSpace(sourceChecksum)) {
                throw new ArgumentException("Source checksum must be provided.", nameof(sourceChecksum));
            }

            ModelAssetProcessorSettings processorSettings = GetCurrentPlatformModelProcessorSettings(settings);
            string platformId = ResolveModelProcessorPlatformId(settings);
            string identity = string.Concat(
                "model", "\n",
                sourceChecksum, "\n",
                settings.Importer.ImporterId ?? string.Empty, "\n",
                platformId, "\n",
                (processorSettings.FlipWinding ? "1" : "0"));
            byte[] identityBytes = System.Text.Encoding.UTF8.GetBytes(identity);
            byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(identityBytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        /// <summary>
        /// Determines whether texture cache identity must include the selected importer id for one overlapping texture format.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <param name="settings">Resolved import settings for the source file.</param>
        /// <returns>True when the texture cache identity must include the importer id.</returns>
        bool ShouldUseTextureImporterQualifiedAssetId(string sourcePath, AssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (settings.Importer == null) {
                throw new InvalidOperationException("Import settings must include importer settings.");
            } else if (string.IsNullOrWhiteSpace(settings.Importer.ImporterId)) {
                throw new InvalidOperationException("Import settings must specify an importer id.");
            }

            if (!IsTextureImporterRegistered(settings.Importer.ImporterId)) {
                return false;
            }

            return ShouldUseTextureImporterQualifiedAssetId(sourcePath, settings.Importer.ImporterId);
        }

        /// <summary>
        /// Determines whether texture cache identity must include the selected importer id for one overlapping texture format.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <param name="importerId">Identifier of the importer selected for the source file.</param>
        /// <returns>True when the texture cache identity must include the importer id.</returns>
        bool ShouldUseTextureImporterQualifiedAssetId(string sourcePath, string importerId) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            } else if (string.IsNullOrWhiteSpace(importerId)) {
                throw new ArgumentException("Importer id must be provided.", nameof(importerId));
            }

            string normalizedExtension = NormalizeExtension(Path.GetExtension(sourcePath));
            List<string> importerIds;
            if (!textureImporterIdsByExtension.TryGetValue(normalizedExtension, out importerIds)) {
                return false;
            }

            return importerIds.Count > 1;
        }

        /// <summary>
        /// Builds one importer-qualified asset identifier for overlapping non-model importers.
        /// </summary>
        /// <param name="sourceChecksum">Checksum of the source file contents.</param>
        /// <param name="importerId">Identifier of the importer selected for the source file.</param>
        /// <returns>Processed asset identifier for the importer-qualified configuration.</returns>
        string BuildImporterQualifiedAssetId(string sourceChecksum, string importerId) {
            if (string.IsNullOrWhiteSpace(sourceChecksum)) {
                throw new ArgumentException("Source checksum must be provided.", nameof(sourceChecksum));
            } else if (string.IsNullOrWhiteSpace(importerId)) {
                throw new ArgumentException("Importer id must be provided.", nameof(importerId));
            }

            string identity = string.Concat(
                "importer", "\n",
                sourceChecksum, "\n",
                importerId);
            byte[] identityBytes = System.Text.Encoding.UTF8.GetBytes(identity);
            byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(identityBytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        /// <summary>
        /// Determines whether one source file should use the model-specific processed asset identity rules.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <param name="settings">Resolved import settings for the source file.</param>
        /// <returns>True when the source resolves through the model import pipeline.</returns>
        bool IsModelSourceForAssetId(string sourcePath, AssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            if (IsModelImporterRegistered(settings.Importer.ImporterId)) {
                return true;
            }

            string extension = Path.GetExtension(sourcePath);
            return IsModelExtension(extension);
        }

        /// <summary>
        /// Resolves the processor-settings platform key that should drive model processing for the current manager state.
        /// </summary>
        /// <param name="settings">Resolved import settings for the source file.</param>
        /// <returns>Platform identifier used for model processor settings, or an empty string when no platform context exists.</returns>
        string ResolveModelProcessorPlatformId(AssetImportSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            if (!string.IsNullOrWhiteSpace(CurrentPlatformId)) {
                return CurrentPlatformId;
            }

            if (settings.Processor == null || settings.Processor.Platforms == null || settings.Processor.Platforms.Count == 0) {
                return string.Empty;
            }

            List<string> platformIds = new List<string>(settings.Processor.Platforms.Keys);
            platformIds.Sort(StringComparer.OrdinalIgnoreCase);
            return platformIds[0];
        }

        /// <summary>
        /// Resolves the processor-settings platform key that should drive texture processing for the current manager state.
        /// </summary>
        /// <param name="settings">Resolved import settings for the source file.</param>
        /// <returns>Platform identifier used for texture processor settings, or an empty string when no platform context exists.</returns>
        string ResolveTextureProcessorPlatformId(AssetImportSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            if (!string.IsNullOrWhiteSpace(CurrentPlatformId)) {
                return CurrentPlatformId;
            }

            if (settings.Processor == null || settings.Processor.Platforms == null || settings.Processor.Platforms.Count == 0) {
                return string.Empty;
            }

            List<string> platformIds = new List<string>(settings.Processor.Platforms.Keys);
            platformIds.Sort(StringComparer.OrdinalIgnoreCase);
            return platformIds[0];
        }

        /// <summary>
        /// Resolves the processor-settings platform key that should drive texture processing for the current manager state.
        /// </summary>
        /// <param name="settings">Resolved typed texture settings for the source file.</param>
        /// <returns>Platform identifier used for texture processor settings, or an empty string when no platform context exists.</returns>
        string ResolveTextureProcessorPlatformId(TextureAssetImportSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            if (!string.IsNullOrWhiteSpace(CurrentPlatformId)) {
                return CurrentPlatformId;
            }

            if (settings.Processor == null || settings.Processor.Platforms == null || settings.Processor.Platforms.Count == 0) {
                return string.Empty;
            }

            List<string> platformIds = new List<string>(settings.Processor.Platforms.Keys);
            platformIds.Sort(StringComparer.OrdinalIgnoreCase);
            return platformIds[0];
        }

        /// <summary>
        /// Resolves the processor-settings platform key that should drive model processing for the current manager state.
        /// </summary>
        /// <param name="settings">Resolved typed model settings for the source file.</param>
        /// <returns>Platform identifier used for model processor settings, or an empty string when no platform context exists.</returns>
        string ResolveModelProcessorPlatformId(ModelAssetImportSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            if (!string.IsNullOrWhiteSpace(CurrentPlatformId)) {
                return CurrentPlatformId;
            }

            if (settings.Processor == null || settings.Processor.Platforms == null || settings.Processor.Platforms.Count == 0) {
                return string.Empty;
            }

            List<string> platformIds = new List<string>(settings.Processor.Platforms.Keys);
            platformIds.Sort(StringComparer.OrdinalIgnoreCase);
            return platformIds[0];
        }

        /// <summary>
        /// Resolves the model processor settings for the active processing platform, returning defaults when none were saved yet.
        /// </summary>
        /// <param name="settings">Resolved import settings for the source file.</param>
        /// <returns>Model processor settings for the current platform context.</returns>
        ModelAssetProcessorSettings GetCurrentPlatformModelProcessorSettings(AssetImportSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            string platformId = ResolveModelProcessorPlatformId(settings);
            if (string.IsNullOrWhiteSpace(platformId)) {
                return new ModelAssetProcessorSettings();
            }

            if (settings.Processor == null || settings.Processor.Platforms == null) {
                return new ModelAssetProcessorSettings();
            }

            AssetPlatformProcessorSettings platformSettings;
            if (!settings.Processor.Platforms.TryGetValue(platformId, out platformSettings) || platformSettings == null || platformSettings.Model == null) {
                return new ModelAssetProcessorSettings();
            }

            return platformSettings.Model;
        }

        /// <summary>
        /// Resolves the model processor settings for the active processing platform, returning defaults when none were saved yet.
        /// </summary>
        /// <param name="settings">Resolved typed model settings for the source file.</param>
        /// <returns>Model processor settings for the current platform context.</returns>
        ModelAssetProcessorSettings GetCurrentPlatformModelProcessorSettings(ModelAssetImportSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            string platformId = ResolveModelProcessorPlatformId(settings);
            if (string.IsNullOrWhiteSpace(platformId)) {
                return new ModelAssetProcessorSettings();
            }

            if (settings.Processor == null || settings.Processor.Platforms == null) {
                return new ModelAssetProcessorSettings();
            }

            ModelAssetProcessorSettings platformSettings;
            if (!settings.Processor.Platforms.TryGetValue(platformId, out platformSettings) || platformSettings == null) {
                return new ModelAssetProcessorSettings();
            }

            return platformSettings;
        }

        /// <summary>
        /// Resolves the texture processor settings for the active processing platform, returning defaults when none were saved yet.
        /// </summary>
        /// <param name="settings">Resolved import settings for the source file.</param>
        /// <returns>Texture processor settings for the current platform context.</returns>
        TextureAssetProcessorSettings GetCurrentPlatformTextureProcessorSettings(AssetImportSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            string platformId = ResolveTextureProcessorPlatformId(settings);
            if (string.IsNullOrWhiteSpace(platformId)) {
                return CreateDefaultTextureProcessorSettings(platformId);
            }

            if (settings.Processor == null || settings.Processor.Platforms == null) {
                return CreateDefaultTextureProcessorSettings(platformId);
            }

            AssetPlatformProcessorSettings platformSettings;
            if (!settings.Processor.Platforms.TryGetValue(platformId, out platformSettings) || platformSettings == null || platformSettings.Texture == null) {
                return CreateDefaultTextureProcessorSettings(platformId);
            }

            return platformSettings.Texture;
        }

        /// <summary>
        /// Resolves the texture processor settings for the active processing platform, returning defaults when none were saved yet.
        /// </summary>
        /// <param name="settings">Resolved typed texture settings for the source file.</param>
        /// <returns>Texture processor settings for the current platform context.</returns>
        TextureAssetProcessorSettings GetCurrentPlatformTextureProcessorSettings(TextureAssetImportSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            string platformId = ResolveTextureProcessorPlatformId(settings);
            if (string.IsNullOrWhiteSpace(platformId)) {
                return CreateDefaultTextureProcessorSettings(platformId);
            }

            if (settings.Processor == null || settings.Processor.Platforms == null) {
                return CreateDefaultTextureProcessorSettings(platformId);
            }

            TextureAssetProcessorSettings platformSettings;
            if (!settings.Processor.Platforms.TryGetValue(platformId, out platformSettings) || platformSettings == null) {
                return CreateDefaultTextureProcessorSettings(platformId);
            }

            return platformSettings;
        }

        /// <summary>
        /// Creates the default texture processor settings used when a source asset has not authored an explicit override for the active platform yet.
        /// </summary>
        /// <param name="platformId">Active processing platform identifier.</param>
        /// <returns>Default texture processor settings for the requested platform.</returns>
        TextureAssetProcessorSettings CreateDefaultTextureProcessorSettings(string platformId) {
            if (string.Equals(platformId, "ds", StringComparison.OrdinalIgnoreCase)) {
                return new TextureAssetProcessorSettings {
                    MaxResolution = 128,
                    ColorFormatId = TextureAssetColorFormat.Rgba4444.ToString(),
                    AlphaPrecision = TextureAssetAlphaPrecision.A4
                };
            }

            return new TextureAssetProcessorSettings {
                MaxResolution = 0,
                ColorFormatId = TextureAssetColorFormat.Rgba32.ToString(),
                AlphaPrecision = TextureAssetAlphaPrecision.A8
            };
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
        /// Builds the output path for an imported font asset.
        /// </summary>
        /// <param name="assetId">Asset identifier used in the file name.</param>
        /// <returns>Absolute path to the serialized asset file.</returns>
        string GetFontAssetPath(string assetId) {
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
