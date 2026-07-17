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
        /// Registered audio importers keyed by identifier.
        /// </summary>
        readonly Dictionary<string, IAudioImporter> audioImportersById;

        /// <summary>
        /// Default text importer identifiers keyed by extension.
        /// </summary>
        readonly Dictionary<string, string> defaultTextImportersByExtension;

        /// <summary>
        /// Default font importer identifiers keyed by extension.
        /// </summary>
        readonly Dictionary<string, string> defaultFontImportersByExtension;

        /// <summary>
        /// Default audio importer identifiers keyed by extension.
        /// </summary>
        readonly Dictionary<string, string> defaultAudioImportersByExtension;

        /// <summary>
        /// Audio importer identifiers keyed by extension.
        /// </summary>
        readonly Dictionary<string, List<string>> audioImporterIdsByExtension;

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
            audioImportersById = new Dictionary<string, IAudioImporter>(StringComparer.OrdinalIgnoreCase);
            defaultTextImportersByExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            defaultFontImportersByExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            defaultAudioImportersByExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            audioImporterIdsByExtension = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
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
        /// Registers an audio importer and records its supported extensions.
        /// </summary>
        /// <param name="registration">Importer registration data.</param>
        public void RegisterAudioImporter(AudioImporterRegistration registration) {
            if (registration == null) {
                throw new ArgumentNullException(nameof(registration));
            }

            if (audioImportersById.ContainsKey(registration.ImporterId)) {
                throw new InvalidOperationException($"Audio importer '{registration.ImporterId}' is already registered.");
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

            if (ModelImportersById.ContainsKey(registration.ImporterId)) {
                throw new InvalidOperationException($"Importer id '{registration.ImporterId}' is already registered for model assets.");
            }

            audioImportersById.Add(registration.ImporterId, registration.Importer);
            string[] extensions = registration.Extensions;
            for (int index = 0; index < extensions.Length; index++) {
                string extension = NormalizeExtension(extensions[index]);
                if (defaultTextureImportersByExtension.ContainsKey(extension)) {
                    throw new InvalidOperationException($"Extension '{extension}' is already mapped to a texture importer.");
                }

                if (defaultTextImportersByExtension.ContainsKey(extension)) {
                    throw new InvalidOperationException($"Extension '{extension}' is already mapped to a text importer.");
                }

                if (defaultFontImportersByExtension.ContainsKey(extension)) {
                    throw new InvalidOperationException($"Extension '{extension}' is already mapped to a font importer.");
                }

                if (DefaultModelImportersByExtension.ContainsKey(extension)) {
                    throw new InvalidOperationException($"Extension '{extension}' is already mapped to a model importer.");
                }

                RegisterAudioImporterExtension(extension, registration.ImporterId);
                if (!defaultAudioImportersByExtension.ContainsKey(extension)) {
                    defaultAudioImportersByExtension[extension] = registration.ImporterId;
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
        /// Gets the identifiers of all registered audio importers.
        /// </summary>
        /// <returns>Ordered list of importer identifiers.</returns>
        public IReadOnlyList<string> GetAudioImporterIds() {
            List<string> ids = new List<string>(audioImportersById.Count);
            foreach (string importerId in audioImportersById.Keys) {
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

            if (audioImporterIdsByExtension.TryGetValue(normalized, out List<string> audioImporterIds)) {
                return new List<string>(audioImporterIds);
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
        /// Checks whether the extension maps to an audio importer.
        /// </summary>
        /// <param name="extension">File extension to evaluate.</param>
        /// <returns>True when the extension maps to an audio importer.</returns>
        public bool IsAudioExtension(string extension) {
            if (string.IsNullOrWhiteSpace(extension)) {
                return false;
            }

            string normalized = NormalizeExtension(extension);
            return audioImporterIdsByExtension.ContainsKey(normalized);
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

            if (defaultAudioImportersByExtension.ContainsKey(normalized)) {
                throw new InvalidOperationException($"Extension '{normalized}' is already mapped to an audio importer.");
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

            if (defaultAudioImportersByExtension.ContainsKey(normalized)) {
                throw new InvalidOperationException($"Extension '{normalized}' is already mapped to an audio importer.");
            }

            defaultTextImportersByExtension[normalized] = importerId;
        }

        /// <summary>
        /// Sets the default audio importer for a specific extension.
        /// </summary>
        /// <param name="extension">File extension to associate with the importer.</param>
        /// <param name="importerId">Identifier of the importer to use.</param>
        public void SetDefaultAudioImporter(string extension, string importerId) {
            if (string.IsNullOrWhiteSpace(extension)) {
                throw new ArgumentException("Extension must be provided.", nameof(extension));
            }

            if (string.IsNullOrWhiteSpace(importerId)) {
                throw new ArgumentException("Importer id must be provided.", nameof(importerId));
            }

            EnsureAudioImporterExists(importerId);
            string normalized = NormalizeExtension(extension);
            if (defaultTextureImportersByExtension.ContainsKey(normalized)) {
                throw new InvalidOperationException($"Extension '{normalized}' is already mapped to a texture importer.");
            }

            if (defaultTextImportersByExtension.ContainsKey(normalized)) {
                throw new InvalidOperationException($"Extension '{normalized}' is already mapped to a text importer.");
            }

            if (defaultFontImportersByExtension.ContainsKey(normalized)) {
                throw new InvalidOperationException($"Extension '{normalized}' is already mapped to a font importer.");
            }

            if (DefaultModelImportersByExtension.ContainsKey(normalized)) {
                throw new InvalidOperationException($"Extension '{normalized}' is already mapped to a model importer.");
            }

            EnsureAudioImporterSupportsExtension(normalized, importerId);
            defaultAudioImportersByExtension[normalized] = importerId;
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

            if (defaultAudioImportersByExtension.ContainsKey(normalized)) {
                throw new InvalidOperationException($"Extension '{normalized}' is already mapped to an audio importer.");
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
            string platformId = ResolveTextureProcessorPlatformId(settings);
            FontAsset asset = BuildImportedFontAsset(sourcePath, settings, platformId, settings.Importer.AssetId);

            string outputPath = GetFontAssetPath(settings.Importer.AssetId);
            EnsureDirectoryForFile(outputPath);
            using (FileStream stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                FontAssetBinarySerializer.Serialize(stream, asset);
            }

            SaveImportSettings(sourcePath, settings);
            return asset;
        }

        /// <summary>
        /// Imports an audio asset from a source file and writes it to disk.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the audio source file.</param>
        /// <returns>Imported <see cref="AudioAsset"/> instance.</returns>
        public AudioAsset ImportAudio(string sourcePath) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            if (!File.Exists(sourcePath)) {
                throw new FileNotFoundException("Audio source file was not found.", sourcePath);
            }

            AudioAssetImportSettings settings = LoadOrCreateAudioImportSettings(sourcePath);
            EnsureAudioImportSettingsValid(settings);

            EnsureAudioImporterExists(settings.Importer.ImporterId);
            IAudioImporter importer = GetAudioImporter(settings.Importer.ImporterId);
            ImportedAudioSource importedAudio;
            using (FileStream stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                importedAudio = importer.ImportAudio(stream);
            }

            if (importedAudio == null) {
                throw new InvalidOperationException($"Audio importer '{settings.Importer.ImporterId}' did not return an asset.");
            }

            AudioAssetProcessorSettings processorSettings = GetCurrentPlatformAudioProcessorSettings(settings);
            AudioAsset asset = BuildImportedAudioAsset(importedAudio, processorSettings, settings.Importer.AssetId);

            string outputPath = GetAudioAssetPath(settings.Importer.AssetId);
            EnsureDirectoryForFile(outputPath);
            using (FileStream stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, asset);
            }

            SaveAudioImportSettings(sourcePath, settings);
            return asset;
        }

        /// <summary>
        /// Builds one font asset for an explicit platform texture-settings context without writing the canonical font cache file.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the font source file.</param>
        /// <param name="platformId">Target platform identifier whose texture settings should be applied.</param>
        /// <returns>Imported <see cref="FontAsset"/> instance for the requested platform.</returns>
        public FontAsset BuildFontAssetForPlatform(string sourcePath, string platformId) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            if (!File.Exists(sourcePath)) {
                throw new FileNotFoundException("Font source file was not found.", sourcePath);
            }

            AssetImportSettings settings = LoadOrCreateImportSettings(sourcePath);
            EnsureImportSettingsValid(settings);
            string fontAssetId = BuildFontAssetId(settings, settings.Importer.SourceChecksum, platformId);
            return BuildImportedFontAsset(sourcePath, settings, platformId, fontAssetId);
        }

        /// <summary>
        /// Imports one font asset and applies the requested platform texture settings before any caller-specific cache write occurs.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the font source file.</param>
        /// <param name="settings">Resolved import settings for the source file.</param>
        /// <param name="platformId">Target platform identifier whose texture settings should drive atlas processing.</param>
        /// <param name="fontAssetId">Processed font asset identifier whose atlas suffix should be published on the source texture.</param>
        /// <returns>Imported <see cref="FontAsset"/> instance with the requested platform texture settings applied.</returns>
        FontAsset BuildImportedFontAsset(string sourcePath, AssetImportSettings settings, string platformId, string fontAssetId) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (string.IsNullOrWhiteSpace(fontAssetId)) {
                throw new ArgumentException("Font asset id must be provided.", nameof(fontAssetId));
            }

            EnsureFontImporterExists(settings.Importer.ImporterId);
            IFontImporter importer = GetFontImporter(settings.Importer.ImporterId);
            FontAssetProcessorSettings fontProcessorSettings = GetFontProcessorSettings(settings, platformId);
            FontAsset asset;
            using (FileStream stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                asset = importer.ImportFont(stream, fontProcessorSettings);
            }

            if (asset == null) {
                throw new InvalidOperationException($"Font importer '{settings.Importer.ImporterId}' did not return an asset.");
            }

            if (asset.SourceTextureAsset == null) {
                throw new InvalidOperationException("Font importers must provide one source atlas texture.");
            }

            TextureAssetProcessorSettings fontAtlasTextureProcessorSettings = GetFontAtlasTextureProcessorSettings(settings, platformId);
            if (fontAtlasTextureProcessorSettings.UsesGenericColorFormat()) {
                TextureAsset processedSourceTextureAsset = TextureAssetProcessor.Apply(asset.SourceTextureAsset, fontAtlasTextureProcessorSettings);
                asset.ApplyProcessedSourceTextureAsset(processedSourceTextureAsset);
            }

            string fontAtlasAssetId = fontAssetId + "#atlas";
            asset.SourceTextureAsset.Id = fontAtlasAssetId;
            asset.SourceTextureAsset.RuntimeAssetId = RuntimeAssetIdGenerator.Generate(fontAtlasAssetId);
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
            settings.FieldValues["emissive-texture-id"] = materialAsset.EmissiveTextureAssetId ?? string.Empty;
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
        /// Imports audio assets that are missing cache files.
        /// </summary>
        /// <returns>Paths to cached assets created during the scan.</returns>
        public List<string> ImportAudiosMissingCache() {
            List<string> importedAssets = new List<string>();
            foreach (string sourcePath in EnumerateAssetSourceFiles()) {
                if (!IsAudioExtension(Path.GetExtension(sourcePath))) {
                    continue;
                }

                AudioAssetImportSettings settings;
                if (!TryLoadOrCreateAudioImportSettings(sourcePath, out settings)) {
                    continue;
                }

                if (!IsAudioImporterRegistered(settings.Importer.ImporterId)) {
                    continue;
                }

                string outputPath = GetAudioAssetPath(settings.Importer.AssetId);
                if (File.Exists(outputPath) && TryLoadCachedAudioAsset(outputPath, out _)) {
                    continue;
                }

                try {
                    ImportAudio(sourcePath);
                    importedAssets.Add(outputPath);
                } catch (Exception ex) {
                    Logger.WriteError($"Audio import failed for '{sourcePath}': {ex.Message}");
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

                if (TryLoadStoredTextureImportAssetId(candidatePath, out string storedAssetId) &&
                    string.Equals(storedAssetId, assetId, StringComparison.OrdinalIgnoreCase)) {
                    sourcePath = candidatePath;
                    return true;
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
        /// Attempts to read the imported texture asset id stored directly on one texture sidecar without normalizing or recomputing it.
        /// </summary>
        /// <param name="sourcePath">Absolute authored source texture path.</param>
        /// <param name="assetId">Stored imported texture asset id when the sidecar could be read.</param>
        /// <returns>True when a non-empty imported texture asset id was read from the sidecar.</returns>
        bool TryLoadStoredTextureImportAssetId(string sourcePath, out string assetId) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            assetId = string.Empty;
            string settingsPath = GetSettingsPath(sourcePath);
            TextureAssetImportSettings settings;
            if (!TryLoadTextureImportSettings(settingsPath, out settings) || settings == null || settings.Importer == null || string.IsNullOrWhiteSpace(settings.Importer.AssetId)) {
                return false;
            }

            assetId = settings.Importer.AssetId;
            return true;
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
                !string.IsNullOrWhiteSpace(settings.Importer.AssetId) &&
                string.Equals(settings.Importer.AssetId, assetId, StringComparison.OrdinalIgnoreCase)) {
                return true;
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
        /// Loads an audio asset for a source file, importing it when needed.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the audio source file.</param>
        /// <param name="asset">Loaded audio asset when available.</param>
        /// <returns>True when the source can be resolved to an audio asset.</returns>
        public bool TryLoadAudioAsset(string sourcePath, out AudioAsset asset) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            if (!File.Exists(sourcePath)) {
                throw new FileNotFoundException("Audio source file was not found.", sourcePath);
            }

            AudioAssetImportSettings settings;
            if (!TryLoadOrCreateAudioImportSettings(sourcePath, out settings)) {
                asset = null;
                return false;
            }

            if (!IsAudioImporterRegistered(settings.Importer.ImporterId)) {
                asset = null;
                return false;
            }

            string outputPath = GetAudioAssetPath(settings.Importer.AssetId);
            if (!File.Exists(outputPath)) {
                asset = ImportAudio(sourcePath);
                return true;
            }

            if (TryLoadCachedAudioAsset(outputPath, out asset)) {
                return true;
            }

            asset = ImportAudio(sourcePath);
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

            string outputPath = null;
            try {
                if (string.Equals(Path.GetExtension(sourcePath), SettingsExtension, StringComparison.OrdinalIgnoreCase)) {
                    return TryLoadSerializedModelAsset(sourcePath, out asset);
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

                outputPath = GetModelAssetPath(settings.Importer.AssetId);
                if (!File.Exists(outputPath)) {
                    asset = ImportModel(sourcePath);
                    return true;
                }

                if (TryLoadCachedModelAsset(outputPath, out asset)) {
                    return true;
                }

                asset = ImportModel(sourcePath);
                return true;
            } catch (Exception exception) {
                throw CreateModelLoadFailureException(sourcePath, outputPath, exception);
            }
        }

        /// <summary>
        /// Describes one asset path with absolute-location provenance and file metadata so higher-level build failures can report which concrete copy was consumed.
        /// </summary>
        /// <param name="path">Asset path to describe.</param>
        /// <returns>Stable human-readable path diagnostics for exception messages.</returns>
        public string DescribeAssetPathForDiagnostics(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Path must be provided.", nameof(path));
            }

            return BuildAssetPathDiagnostics(path);
        }

        /// <summary>
        /// Attempts to load an authored serialized model asset directly from disk.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the serialized model asset file.</param>
        /// <param name="asset">Loaded model asset when the file contains the expected payload type.</param>
        /// <returns>True when the serialized model asset was loaded successfully.</returns>
        bool TryLoadSerializedModelAsset(string sourcePath, out ModelAsset asset) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            asset = null;
            string previousAssetPath = EngineBinaryReadContext.CurrentAssetPath;
            try {
                EngineBinaryReadContext.CurrentAssetPath = sourcePath;
                using FileStream stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                Asset serializedAsset = AssetSerializer.Deserialize(stream);
                if (serializedAsset is ModelAsset modelAsset) {
                    asset = modelAsset;
                    return true;
                }

                throw new InvalidOperationException($"Model asset file '{sourcePath}' did not contain a ModelAsset payload.");
            } finally {
                EngineBinaryReadContext.CurrentAssetPath = previousAssetPath;
            }
        }

        /// <summary>
        /// Creates one model-load failure exception that preserves the original error as an inner exception and annotates the concrete source and cache paths that were consumed.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source model file that was being resolved.</param>
        /// <param name="outputPath">Absolute path to the cached model asset when one had already been resolved.</param>
        /// <param name="innerException">Original exception thrown by the importer, serializer, or cache loader.</param>
        /// <returns>Exception enriched with source provenance and file metadata.</returns>
        InvalidOperationException CreateModelLoadFailureException(string sourcePath, string outputPath, Exception innerException) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            } else if (innerException == null) {
                throw new ArgumentNullException(nameof(innerException));
            }

            string message = "Model asset load failed."
                + " Source=" + BuildAssetPathDiagnostics(sourcePath)
                + (string.IsNullOrWhiteSpace(outputPath) ? string.Empty : " Cache=" + BuildAssetPathDiagnostics(outputPath))
                + " Reason=" + innerException.Message;
            return new InvalidOperationException(message, innerException);
        }

        /// <summary>
        /// Builds one diagnostic string that identifies where a path lives relative to the project roots and records stable file metadata for transient-build investigation.
        /// </summary>
        /// <param name="path">Filesystem path to describe.</param>
        /// <returns>Human-readable path provenance and file metadata.</returns>
        string BuildAssetPathDiagnostics(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Path must be provided.", nameof(path));
            }

            string fullPath = Path.GetFullPath(path);
            string scope = ResolvePathScopeForDiagnostics(fullPath);
            if (!File.Exists(fullPath)) {
                return "{ FullPath=" + fullPath + ", Scope=" + scope + ", Exists=false }";
            }

            FileInfo fileInfo = new FileInfo(fullPath);
            string checksum;
            try {
                checksum = fileHasher.ComputeHash(fullPath);
            } catch (Exception exception) {
                checksum = "unavailable:" + exception.GetType().Name;
            }

            return "{ FullPath=" + fullPath
                + ", Scope=" + scope
                + ", Exists=true"
                + ", Length=" + fileInfo.Length
                + ", LastWriteTimeUtc=" + fileInfo.LastWriteTimeUtc.ToString("O")
                + ", Sha256=" + checksum
                + " }";
        }

        /// <summary>
        /// Resolves the most specific project-root bucket that contains the supplied path so failures can distinguish project-tree reads from cache or external-workspace copies.
        /// </summary>
        /// <param name="fullPath">Absolute filesystem path to classify.</param>
        /// <returns>Short scope label used by exception diagnostics.</returns>
        string ResolvePathScopeForDiagnostics(string fullPath) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Full path must be provided.", nameof(fullPath));
            }

            string normalizedProjectRootPath = EnsureTrailingDirectorySeparator(projectRootPath);
            string normalizedAssetsRootPath = EnsureTrailingDirectorySeparator(assetsRootPath);
            string normalizedImportRootPath = EnsureTrailingDirectorySeparator(importRootPath);
            string normalizedFullPath = Path.GetFullPath(fullPath);
            if (string.Equals(normalizedFullPath, assetsRootPath, StringComparison.OrdinalIgnoreCase) || normalizedFullPath.StartsWith(normalizedAssetsRootPath, StringComparison.OrdinalIgnoreCase)) {
                return "project-assets";
            }

            if (string.Equals(normalizedFullPath, importRootPath, StringComparison.OrdinalIgnoreCase) || normalizedFullPath.StartsWith(normalizedImportRootPath, StringComparison.OrdinalIgnoreCase)) {
                return "project-cache";
            }

            if (string.Equals(normalizedFullPath, projectRootPath, StringComparison.OrdinalIgnoreCase) || normalizedFullPath.StartsWith(normalizedProjectRootPath, StringComparison.OrdinalIgnoreCase)) {
                return "project-root";
            }

            return "external";
        }

        /// <summary>
        /// Ensures one directory path ends with the current platform separator so prefix checks do not confuse sibling directories for ancestors.
        /// </summary>
        /// <param name="path">Directory path to normalize for prefix comparisons.</param>
        /// <returns>Directory path with one trailing separator.</returns>
        string EnsureTrailingDirectorySeparator(string path) {
            if (string.IsNullOrWhiteSpace(path)) {
                throw new ArgumentException("Path must be provided.", nameof(path));
            }

            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
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
        /// Attempts to load a cached audio asset.
        /// </summary>
        /// <param name="outputPath">Absolute path to the cached audio asset.</param>
        /// <param name="asset">Loaded audio asset when the cache file exists and contains the expected payload type.</param>
        /// <returns>True when the cached asset was loaded successfully.</returns>
        bool TryLoadCachedAudioAsset(string outputPath, out AudioAsset asset) {
            if (string.IsNullOrWhiteSpace(outputPath)) {
                throw new ArgumentException("Output path must be provided.", nameof(outputPath));
            }

            asset = null;
            Asset cachedAsset;
            if (!TryLoadCachedAsset(outputPath, "AudioAsset", out cachedAsset)) {
                return false;
            }

            if (cachedAsset is AudioAsset audioAsset) {
                asset = audioAsset;
                return true;
            }

            throw new InvalidOperationException($"Audio cache file '{outputPath}' did not contain an AudioAsset payload.");
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

            string previousAssetPath = EngineBinaryReadContext.CurrentAssetPath;
            try {
                EngineBinaryReadContext.CurrentAssetPath = outputPath;
                using FileStream stream = new FileStream(outputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                asset = RestoreRuntimeTextureForCachedFontAsset(FontAssetBinarySerializer.Deserialize(stream));
                return true;
            } catch {
                asset = null;
                return false;
            } finally {
                EngineBinaryReadContext.CurrentAssetPath = previousAssetPath;
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

            string previousAssetPath = EngineBinaryReadContext.CurrentAssetPath;
            try {
                EngineBinaryReadContext.CurrentAssetPath = outputPath;
                using (FileStream stream = new FileStream(outputPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                    asset = AssetSerializer.Deserialize(stream);
                }
                return true;
            } catch {
                asset = null;
                return false;
            } finally {
                EngineBinaryReadContext.CurrentAssetPath = previousAssetPath;
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
            return TryLoadTextureImportSettings(settingsPath, out settings, out _, out _);
        }

        /// <summary>
        /// Attempts to load typed texture import settings from a settings file, falling back to legacy generic import settings when needed.
        /// </summary>
        /// <param name="settingsPath">Absolute path to the settings file.</param>
        /// <param name="settings">Deserialized settings when the file exists.</param>
        /// <param name="requiresRewrite">True when the settings were loaded through the legacy generic format and should be rewritten as current typed settings.</param>
        /// <param name="preserveLegacyAssetId">True when the legacy sidecar stored one explicit imported texture asset id that must survive the typed rewrite.</param>
        /// <returns>True when the settings file was loaded successfully.</returns>
        bool TryLoadTextureImportSettings(string settingsPath, out TextureAssetImportSettings settings, out bool requiresRewrite, out bool preserveLegacyAssetId) {
            if (string.IsNullOrWhiteSpace(settingsPath)) {
                throw new ArgumentException("Settings path must be provided.", nameof(settingsPath));
            }

            settings = null;
            requiresRewrite = false;
            preserveLegacyAssetId = false;
            if (!File.Exists(settingsPath)) {
                return false;
            }

            try {
                using FileStream stream = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                settings = TextureAssetImportSettingsBinarySerializer.Deserialize(stream);
                return true;
            } catch {
                settings = null;
            }

            if (!TryLoadImportSettings(settingsPath, out AssetImportSettings legacySettings) || legacySettings == null) {
                settings = null;
                return false;
            }

            settings = ConvertLegacyTextureImportSettings(legacySettings);
            requiresRewrite = true;
            preserveLegacyAssetId = !string.IsNullOrWhiteSpace(legacySettings.Importer?.AssetId);
            return true;
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
        /// Attempts to load typed audio import settings from a settings file.
        /// </summary>
        /// <param name="settingsPath">Absolute path to the settings file.</param>
        /// <param name="settings">Deserialized settings when the file exists.</param>
        /// <returns>True when the settings file was loaded successfully.</returns>
        bool TryLoadAudioImportSettings(string settingsPath, out AudioAssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(settingsPath)) {
                throw new ArgumentException("Settings path must be provided.", nameof(settingsPath));
            }

            settings = null;
            if (!File.Exists(settingsPath)) {
                return false;
            }

            try {
                using FileStream stream = new FileStream(settingsPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                settings = AudioAssetImportSettingsBinarySerializer.Deserialize(stream);
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
        /// Creates new typed audio import settings based on the source file extension.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <returns>Newly created settings.</returns>
        AudioAssetImportSettings CreateDefaultAudioImportSettings(string sourcePath) {
            string extension = Path.GetExtension(sourcePath);
            string importerId = ResolveDefaultImporter(extension);
            return new AudioAssetImportSettings {
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
        /// Attempts to create default typed audio import settings for a source file.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <param name="settings">Created settings when defaults are available.</param>
        /// <returns>True when settings were created from a registered default.</returns>
        bool TryCreateDefaultAudioImportSettings(string sourcePath, out AudioAssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            string extension = Path.GetExtension(sourcePath);
            if (!TryResolveDefaultImporter(extension, out string importerId)) {
                settings = null;
                return false;
            }

            settings = new AudioAssetImportSettings {
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
        /// Repairs loaded typed audio import settings when the importer id is missing or no longer valid for the source extension.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <param name="settings">Loaded settings that may need importer normalization.</param>
        /// <returns>True when the importer id was replaced with the registered default importer.</returns>
        bool RepairAudioImporterId(string sourcePath, AudioAssetImportSettings settings) {
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
            bool requiresRewrite = false;
            bool preserveLegacyAssetId = false;
            bool loadedFromDisk = settingsFileExists && TryLoadTextureImportSettings(settingsPath, out settings, out requiresRewrite, out preserveLegacyAssetId);
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

            UpdateTextureImportSettingsChecksum(settings, sourcePath, preserveLegacyAssetId);
            if (settingsFileExists && (!loadedFromDisk || repaired || requiresRewrite)) {
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
            bool requiresRewrite = false;
            bool preserveLegacyAssetId = false;
            if (settingsFileExists && TryLoadTextureImportSettings(settingsPath, out settings, out requiresRewrite, out preserveLegacyAssetId)) {
                bool repaired = RepairTextureImporterId(sourcePath, settings);
                UpdateTextureImportSettingsChecksum(settings, sourcePath, preserveLegacyAssetId);
                if (repaired || requiresRewrite) {
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
        /// Converts one legacy generic import-settings payload into the current typed texture-settings shape.
        /// </summary>
        /// <param name="legacySettings">Legacy generic import settings to convert.</param>
        /// <returns>Typed texture import settings that preserve the legacy importer metadata and per-platform texture processor settings.</returns>
        TextureAssetImportSettings ConvertLegacyTextureImportSettings(AssetImportSettings legacySettings) {
            if (legacySettings == null) {
                throw new ArgumentNullException(nameof(legacySettings));
            } else if (legacySettings.Importer == null) {
                throw new InvalidOperationException("Legacy texture import settings must include importer metadata.");
            }

            TextureAssetImportSettings convertedSettings = new TextureAssetImportSettings();
            convertedSettings.Importer.ImporterId = legacySettings.Importer.ImporterId ?? string.Empty;
            convertedSettings.Importer.SourceChecksum = legacySettings.Importer.SourceChecksum ?? string.Empty;
            convertedSettings.Importer.AssetId = legacySettings.Importer.AssetId ?? string.Empty;

            if (legacySettings.Processor == null || legacySettings.Processor.Platforms == null) {
                return convertedSettings;
            }

            foreach (KeyValuePair<string, AssetPlatformProcessorSettings> entry in legacySettings.Processor.Platforms) {
                if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null) {
                    continue;
                }

                convertedSettings.Processor.Platforms[entry.Key] = CloneTextureProcessorSettings(entry.Value.Texture);
            }

            return convertedSettings;
        }

        /// <summary>
        /// Clones one legacy texture processor settings record into the current typed texture-settings shape.
        /// </summary>
        /// <param name="settings">Legacy settings instance to clone.</param>
        /// <returns>Cloned texture processor settings.</returns>
        TextureAssetProcessorSettings CloneTextureProcessorSettings(TextureAssetProcessorSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            return new TextureAssetProcessorSettings {
                MaxResolution = settings.MaxResolution,
                ColorFormatId = settings.ColorFormatId,
                AlphaPrecision = settings.AlphaPrecision,
                IndexingMethodId = settings.IndexingMethodId ?? string.Empty
            };
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
        /// Loads typed audio import settings for a source file or creates defaults if missing.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <returns>Resolved typed audio import settings.</returns>
        public AudioAssetImportSettings LoadOrCreateAudioImportSettings(string sourcePath) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            string settingsPath = GetSettingsPath(sourcePath);
            bool settingsFileExists = File.Exists(settingsPath);
            AudioAssetImportSettings settings = null;
            bool loadedFromDisk = false;
            try {
                loadedFromDisk = settingsFileExists && TryLoadAudioImportSettings(settingsPath, out settings);
            } catch (Exception ex) {
                throw new InvalidOperationException($"Failed to load audio import settings for source '{sourcePath}'.", ex);
            }
            bool repaired = false;
            if (!loadedFromDisk) {
                try {
                    settings = CreateDefaultAudioImportSettings(sourcePath);
                } catch {
                    settings = new AudioAssetImportSettings();
                }
            } else {
                repaired = RepairAudioImporterId(sourcePath, settings);
            }

            UpdateAudioImportSettingsChecksum(settings, sourcePath);
            if (settingsFileExists && (!loadedFromDisk || repaired)) {
                SaveAudioImportSettings(sourcePath, settings);
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
        /// Saves typed audio import settings next to the specified source file.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <param name="settings">Settings to serialize.</param>
        public void SaveAudioImportSettings(string sourcePath, AudioAssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            } else if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            string settingsPath = GetSettingsPath(sourcePath);
            EnsureDirectoryForFile(settingsPath);
            using FileStream stream = new FileStream(settingsPath, FileMode.Create, FileAccess.Write, FileShare.None);
            AudioAssetImportSettingsBinarySerializer.Serialize(stream, settings);
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
        /// Attempts to load typed audio import settings or create defaults when missing.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <param name="settings">Resolved settings when available.</param>
        /// <returns>True when settings could be resolved for the source file.</returns>
        public bool TryLoadOrCreateAudioImportSettings(string sourcePath, out AudioAssetImportSettings settings) {
            if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            string settingsPath = GetSettingsPath(sourcePath);
            bool settingsFileExists = File.Exists(settingsPath);
            try {
                if (settingsFileExists && TryLoadAudioImportSettings(settingsPath, out settings)) {
                    bool repaired = RepairAudioImporterId(sourcePath, settings);
                    UpdateAudioImportSettingsChecksum(settings, sourcePath);
                    if (repaired) {
                        SaveAudioImportSettings(sourcePath, settings);
                    }
                    return true;
                }
            } catch (Exception ex) {
                throw new InvalidOperationException($"Failed to load audio import settings for source '{sourcePath}'.", ex);
            }

            if (!TryCreateDefaultAudioImportSettings(sourcePath, out settings)) {
                settings = new AudioAssetImportSettings();
            }

            UpdateAudioImportSettingsChecksum(settings, sourcePath);
            if (settingsFileExists) {
                SaveAudioImportSettings(sourcePath, settings);
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
        /// Ensures required typed audio settings fields are populated.
        /// </summary>
        /// <param name="settings">Settings to validate.</param>
        void EnsureAudioImportSettingsValid(AudioAssetImportSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (settings.Importer == null) {
                throw new InvalidOperationException("Audio import settings must include importer settings.");
            } else if (settings.Processor == null || settings.Processor.Platforms == null) {
                throw new InvalidOperationException("Audio import settings must include processor platform settings.");
            } else if (string.IsNullOrWhiteSpace(settings.Importer.ImporterId)) {
                throw new InvalidOperationException("Audio import settings must specify an importer id.");
            } else if (string.IsNullOrWhiteSpace(settings.Importer.AssetId)) {
                throw new InvalidOperationException("Audio import settings must specify an asset id.");
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
            string audioImporterId;
            string modelImporterId;
            bool hasTexture = defaultTextureImportersByExtension.TryGetValue(normalized, out textureImporterId);
            bool hasText = defaultTextImportersByExtension.TryGetValue(normalized, out textImporterId);
            bool hasFont = defaultFontImportersByExtension.TryGetValue(normalized, out fontImporterId);
            bool hasAudio = defaultAudioImportersByExtension.TryGetValue(normalized, out audioImporterId);
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
            if (hasAudio) {
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

            if (hasAudio) {
                return audioImporterId;
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
            string audioImporterId;
            string modelImporterId;
            bool hasTexture = defaultTextureImportersByExtension.TryGetValue(normalized, out textureImporterId);
            bool hasText = defaultTextImportersByExtension.TryGetValue(normalized, out textImporterId);
            bool hasFont = defaultFontImportersByExtension.TryGetValue(normalized, out fontImporterId);
            bool hasAudio = defaultAudioImportersByExtension.TryGetValue(normalized, out audioImporterId);
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
            if (hasAudio) {
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

            if (hasAudio) {
                importerId = audioImporterId;
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
        /// Retrieves an audio importer by identifier.
        /// </summary>
        /// <param name="importerId">Identifier of the importer.</param>
        /// <returns>Importer implementation.</returns>
        IAudioImporter GetAudioImporter(string importerId) {
            if (audioImportersById.TryGetValue(importerId, out IAudioImporter importer)) {
                return importer;
            }

            throw new InvalidOperationException($"Audio importer '{importerId}' is not registered.");
        }

        /// <summary>
        /// Ensures an audio importer is registered.
        /// </summary>
        /// <param name="importerId">Identifier to verify.</param>
        void EnsureAudioImporterExists(string importerId) {
            if (!audioImportersById.ContainsKey(importerId)) {
                throw new InvalidOperationException($"Audio importer '{importerId}' is not registered.");
            }
        }

        /// <summary>
        /// Checks whether an audio importer is registered.
        /// </summary>
        /// <param name="importerId">Identifier to verify.</param>
        /// <returns>True when a matching importer is registered.</returns>
        bool IsAudioImporterRegistered(string importerId) {
            if (string.IsNullOrWhiteSpace(importerId)) {
                return false;
            }

            return audioImportersById.ContainsKey(importerId);
        }

        /// <summary>
        /// Records that one audio importer supports one file extension.
        /// </summary>
        /// <param name="extension">Normalized file extension.</param>
        /// <param name="importerId">Importer identifier that supports the extension.</param>
        void RegisterAudioImporterExtension(string extension, string importerId) {
            if (string.IsNullOrWhiteSpace(extension)) {
                throw new ArgumentException("Extension must be provided.", nameof(extension));
            }
            if (string.IsNullOrWhiteSpace(importerId)) {
                throw new ArgumentException("Importer id must be provided.", nameof(importerId));
            }

            if (!audioImporterIdsByExtension.TryGetValue(extension, out List<string> importerIds)) {
                importerIds = new List<string>();
                audioImporterIdsByExtension.Add(extension, importerIds);
            }

            if (!importerIds.Contains(importerId, StringComparer.OrdinalIgnoreCase)) {
                importerIds.Add(importerId);
            }
        }

        /// <summary>
        /// Ensures the supplied audio importer has been registered for the requested file extension.
        /// </summary>
        /// <param name="extension">Normalized file extension.</param>
        /// <param name="importerId">Importer identifier to validate.</param>
        void EnsureAudioImporterSupportsExtension(string extension, string importerId) {
            if (string.IsNullOrWhiteSpace(extension)) {
                throw new ArgumentException("Extension must be provided.", nameof(extension));
            }
            if (string.IsNullOrWhiteSpace(importerId)) {
                throw new ArgumentException("Importer id must be provided.", nameof(importerId));
            }

            if (!audioImporterIdsByExtension.TryGetValue(extension, out List<string> importerIds)) {
                throw new InvalidOperationException($"No audio importers are registered for '{extension}'.");
            }

            for (int index = 0; index < importerIds.Count; index++) {
                if (string.Equals(importerIds[index], importerId, StringComparison.OrdinalIgnoreCase)) {
                    return;
                }
            }

            throw new InvalidOperationException($"Audio importer '{importerId}' does not support '{extension}'.");
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
            UpdateTextureImportSettingsChecksum(settings, sourcePath, false);
        }

        /// <summary>
        /// Updates typed texture import settings to store the current source checksum while optionally preserving one explicit legacy imported asset id.
        /// </summary>
        /// <param name="settings">Settings to update.</param>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        /// <param name="preserveLegacyAssetId">True when the current importer asset id originated from a legacy sidecar and must not be recomputed.</param>
        void UpdateTextureImportSettingsChecksum(TextureAssetImportSettings settings, string sourcePath, bool preserveLegacyAssetId) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            string checksum = fileHasher.ComputeHash(sourcePath);
            settings.Importer.SourceChecksum = checksum;
            if (!preserveLegacyAssetId || string.IsNullOrWhiteSpace(settings.Importer.AssetId)) {
                settings.Importer.AssetId = BuildTextureAssetId(sourcePath, settings, checksum);
            }
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
        /// Updates typed audio import settings to store the current source checksum.
        /// </summary>
        /// <param name="settings">Settings to update.</param>
        /// <param name="sourcePath">Absolute path to the source file.</param>
        void UpdateAudioImportSettingsChecksum(AudioAssetImportSettings settings, string sourcePath) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (string.IsNullOrWhiteSpace(sourcePath)) {
                throw new ArgumentException("Source path must be provided.", nameof(sourcePath));
            }

            string checksum = fileHasher.ComputeHash(sourcePath);
            settings.Importer.SourceChecksum = checksum;
            settings.Importer.AssetId = BuildAudioAssetId(settings, checksum);
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
                    return BuildFontAssetId(settings, sourceChecksum, texturePlatformId);
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
            string indexingMethodId = processorSettings.UsesIndexedColorFormat()
                ? processorSettings.ResolveIndexingMethod().ToString()
                : string.Empty;
            string identity = string.Concat(
                "texture", "\n",
                sourceChecksum, "\n",
                settings.Importer.ImporterId ?? string.Empty, "\n",
                platformId, "\n",
                processorSettings.MaxResolution.ToString(System.Globalization.CultureInfo.InvariantCulture), "\n",
                processorSettings.ColorFormatId ?? string.Empty, "\n",
                ((int)processorSettings.AlphaPrecision).ToString(System.Globalization.CultureInfo.InvariantCulture), "\n",
                indexingMethodId);
            byte[] identityBytes = System.Text.Encoding.UTF8.GetBytes(identity);
            byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(identityBytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        /// <summary>
        /// Builds the processed font asset identifier for the current import settings and explicit platform texture-settings context.
        /// </summary>
        /// <param name="settings">Resolved import settings for the source file.</param>
        /// <param name="sourceChecksum">Checksum of the source file contents.</param>
        /// <param name="platformId">Platform texture-settings key that should drive identity generation.</param>
        /// <returns>Processed asset identifier for the supplied font-processing context.</returns>
        string BuildFontAssetId(AssetImportSettings settings, string sourceChecksum, string platformId) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (string.IsNullOrWhiteSpace(sourceChecksum)) {
                throw new ArgumentException("Source checksum must be provided.", nameof(sourceChecksum));
            }

            TextureAssetProcessorSettings textureProcessorSettings = GetTextureProcessorSettings(settings, platformId);
            string fontIdentity = string.Concat(
                "font", "\n",
                sourceChecksum, "\n",
                settings.Importer.ImporterId ?? string.Empty, "\n",
                platformId, "\n",
                textureProcessorSettings.MaxResolution.ToString(System.Globalization.CultureInfo.InvariantCulture), "\n",
                textureProcessorSettings.ColorFormatId ?? string.Empty, "\n",
                ((int)textureProcessorSettings.AlphaPrecision).ToString(System.Globalization.CultureInfo.InvariantCulture));
            byte[] fontIdentityBytes = System.Text.Encoding.UTF8.GetBytes(fontIdentity);
            byte[] fontHashBytes = System.Security.Cryptography.SHA256.HashData(fontIdentityBytes);
            return Convert.ToHexString(fontHashBytes).ToLowerInvariant();
        }

        /// <summary>
        /// Builds the processed audio asset identifier for the current typed settings.
        /// </summary>
        /// <param name="settings">Resolved typed audio import settings for the source file.</param>
        /// <param name="sourceChecksum">Checksum of the source file contents.</param>
        /// <returns>Processed asset identifier for the current configuration.</returns>
        string BuildAudioAssetId(AudioAssetImportSettings settings, string sourceChecksum) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (string.IsNullOrWhiteSpace(sourceChecksum)) {
                throw new ArgumentException("Source checksum must be provided.", nameof(sourceChecksum));
            }

            string platformId = ResolveAudioProcessorPlatformId(settings);
            AudioAssetProcessorSettings processorSettings = GetCurrentPlatformAudioProcessorSettings(settings);
            string identity = string.Concat(
                "audio", "\n",
                sourceChecksum, "\n",
                settings.Importer?.ImporterId ?? string.Empty, "\n",
                platformId, "\n",
                processorSettings.EncodingFamilyId ?? string.Empty, "\n",
                ((int)processorSettings.PlaybackMode).ToString(System.Globalization.CultureInfo.InvariantCulture), "\n",
                processorSettings.TargetChannels.ToString(System.Globalization.CultureInfo.InvariantCulture), "\n",
                processorSettings.TargetSampleRate.ToString(System.Globalization.CultureInfo.InvariantCulture), "\n",
                processorSettings.StreamChunkByteSize.ToString(System.Globalization.CultureInfo.InvariantCulture), "\n",
                processorSettings.DefaultLoop ? "1" : "0", "\n",
                processorSettings.DefaultBusId ?? string.Empty);
            byte[] identityBytes = System.Text.Encoding.UTF8.GetBytes(identity);
            byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(identityBytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        /// <summary>
        /// Builds one imported audio asset from decoded audio metadata and processor settings.
        /// </summary>
        /// <param name="importedAudio">Decoded audio metadata returned by the importer.</param>
        /// <param name="processorSettings">Platform processor settings that drive asset generation.</param>
        /// <param name="assetId">Processed asset identifier to publish.</param>
        /// <returns>Imported audio asset ready for serialization.</returns>
        AudioAsset BuildImportedAudioAsset(ImportedAudioSource importedAudio, AudioAssetProcessorSettings processorSettings, string assetId) {
            if (importedAudio == null) {
                throw new ArgumentNullException(nameof(importedAudio));
            } else if (processorSettings == null) {
                throw new ArgumentNullException(nameof(processorSettings));
            } else if (string.IsNullOrWhiteSpace(assetId)) {
                throw new ArgumentException("Audio asset id must be provided.", nameof(assetId));
            }

            if (importedAudio.Channels == 0) {
                throw new InvalidOperationException("Audio importers must provide a non-zero channel count.");
            }
            if (importedAudio.SampleRate <= 0) {
                throw new InvalidOperationException("Audio importers must provide a positive sample rate.");
            }
            if (importedAudio.DurationSeconds < 0f) {
                throw new InvalidOperationException("Audio importers must provide a non-negative duration.");
            }
            if (importedAudio.Pcm16Bytes == null) {
                throw new InvalidOperationException("Audio importers must provide a PCM payload.");
            }
            if (string.IsNullOrWhiteSpace(processorSettings.EncodingFamilyId)) {
                throw new InvalidOperationException("Audio processor settings must provide an encoding family id.");
            }
            if (string.IsNullOrWhiteSpace(processorSettings.DefaultBusId)) {
                throw new InvalidOperationException("Audio processor settings must provide a default bus id.");
            }
            if (processorSettings.PlaybackMode == AudioPlaybackMode.Streamed && processorSettings.StreamChunkByteSize <= 0) {
                throw new InvalidOperationException("Streamed audio assets require a positive stream chunk size.");
            }

            byte[] encodedBytes = BuildProcessedAudioPayload(importedAudio, processorSettings, out ushort targetChannels, out int targetSampleRate, out float targetDurationSeconds);
            return new AudioAsset {
                Id = assetId,
                RuntimeAssetId = RuntimeAssetIdGenerator.Generate(assetId),
                PlaybackMode = processorSettings.PlaybackMode,
                DefaultLoop = processorSettings.DefaultLoop,
                DefaultBusId = processorSettings.DefaultBusId,
                Channels = targetChannels,
                SampleRate = targetSampleRate,
                DurationSeconds = targetDurationSeconds,
                EncodingFamilyId = processorSettings.EncodingFamilyId,
                EncodedBytes = encodedBytes,
                Chunks = BuildAudioChunks(encodedBytes, processorSettings)
            };
        }

        /// <summary>
        /// Builds the processed PCM16 payload published to one imported audio asset after applying requested platform channel and sample-rate conversion.
        /// </summary>
        /// <param name="importedAudio">Decoded audio metadata returned by the importer.</param>
        /// <param name="processorSettings">Platform processor settings that drive asset generation.</param>
        /// <param name="targetChannels">Resolved output channel count.</param>
        /// <param name="targetSampleRate">Resolved output sample rate.</param>
        /// <param name="targetDurationSeconds">Resolved output duration in seconds.</param>
        /// <returns>Processed audio payload bytes ready for serialization.</returns>
        byte[] BuildProcessedAudioPayload(
            ImportedAudioSource importedAudio,
            AudioAssetProcessorSettings processorSettings,
            out ushort targetChannels,
            out int targetSampleRate,
            out float targetDurationSeconds) {
            if (importedAudio == null) {
                throw new ArgumentNullException(nameof(importedAudio));
            } else if (processorSettings == null) {
                throw new ArgumentNullException(nameof(processorSettings));
            }

            targetChannels = processorSettings.TargetChannels != 0 ? processorSettings.TargetChannels : importedAudio.Channels;
            targetSampleRate = processorSettings.TargetSampleRate > 0 ? processorSettings.TargetSampleRate : importedAudio.SampleRate;
            if (targetChannels == 0) {
                throw new InvalidOperationException("Audio processor settings resolved to zero output channels.");
            } else if (targetSampleRate <= 0) {
                throw new InvalidOperationException("Audio processor settings resolved to a non-positive output sample rate.");
            }

            short[] samples = DecodePcm16Samples(importedAudio.Pcm16Bytes, importedAudio.Channels);
            if (samples.Length == 0) {
                targetDurationSeconds = 0f;
                return Array.Empty<byte>();
            }

            short[] channelAdjustedSamples = ConvertAudioChannels(samples, importedAudio.Channels, targetChannels);
            short[] resampledSamples = ResampleAudioSamples(channelAdjustedSamples, targetChannels, importedAudio.SampleRate, targetSampleRate);
            int frameCount = resampledSamples.Length / targetChannels;
            targetDurationSeconds = importedAudio.DurationSeconds > 0f
                ? importedAudio.DurationSeconds
                : frameCount > 0 && targetSampleRate > 0
                    ? (float)(frameCount / (double)targetSampleRate)
                    : 0f;
            return EncodeProcessedAudioPayload(resampledSamples, processorSettings.EncodingFamilyId);
        }

        /// <summary>
        /// Encodes one processed sample buffer into the runtime payload expected by the selected encoding family.
        /// </summary>
        /// <param name="samples">Processed PCM16 sample values.</param>
        /// <param name="encodingFamilyId">Encoding family that should own the serialized payload.</param>
        /// <returns>Encoded payload bytes ready for serialization.</returns>
        byte[] EncodeProcessedAudioPayload(short[] samples, string encodingFamilyId) {
            if (samples == null) {
                throw new ArgumentNullException(nameof(samples));
            }

            if (string.Equals(encodingFamilyId, "adpcm-buffered", StringComparison.OrdinalIgnoreCase)) {
                return EncodeNintendoDsImaAdpcmSamples(samples);
            }

            return EncodePcm16Samples(samples);
        }

        /// <summary>
        /// Decodes one PCM16 byte payload into signed sample values while validating the expected source channel layout.
        /// </summary>
        /// <param name="pcm16Bytes">PCM16 payload bytes emitted by the importer.</param>
        /// <param name="sourceChannels">Expected source channel count.</param>
        /// <returns>Decoded PCM16 sample values.</returns>
        short[] DecodePcm16Samples(byte[] pcm16Bytes, ushort sourceChannels) {
            if (pcm16Bytes == null) {
                throw new ArgumentNullException(nameof(pcm16Bytes));
            } else if (sourceChannels == 0) {
                throw new ArgumentOutOfRangeException(nameof(sourceChannels), "Source channel count must be positive.");
            } else if ((pcm16Bytes.Length % sizeof(short)) != 0) {
                throw new InvalidOperationException("Audio importers must provide a PCM16 payload aligned to 16-bit sample boundaries.");
            }

            int sampleCount = pcm16Bytes.Length / sizeof(short);
            if ((sampleCount % sourceChannels) != 0) {
                throw new InvalidOperationException("Audio importers must provide full PCM16 frames for the declared channel count.");
            }

            short[] samples = new short[sampleCount];
            Buffer.BlockCopy(pcm16Bytes, 0, samples, 0, pcm16Bytes.Length);
            return samples;
        }

        /// <summary>
        /// Converts one PCM16 sample buffer between channel layouts for platform cook output.
        /// </summary>
        /// <param name="sourceSamples">Decoded PCM16 sample values.</param>
        /// <param name="sourceChannels">Source channel count.</param>
        /// <param name="targetChannels">Requested output channel count.</param>
        /// <returns>Channel-adjusted PCM16 sample values.</returns>
        short[] ConvertAudioChannels(short[] sourceSamples, ushort sourceChannels, ushort targetChannels) {
            if (sourceSamples == null) {
                throw new ArgumentNullException(nameof(sourceSamples));
            } else if (sourceChannels == 0) {
                throw new ArgumentOutOfRangeException(nameof(sourceChannels), "Source channel count must be positive.");
            } else if (targetChannels == 0) {
                throw new ArgumentOutOfRangeException(nameof(targetChannels), "Target channel count must be positive.");
            }

            if (sourceChannels == targetChannels) {
                return sourceSamples;
            }

            int sourceFrameCount = sourceSamples.Length / sourceChannels;
            short[] convertedSamples = new short[sourceFrameCount * targetChannels];
            if (targetChannels == 1) {
                for (int frameIndex = 0; frameIndex < sourceFrameCount; frameIndex++) {
                    int sourceFrameOffset = frameIndex * sourceChannels;
                    int summedSample = 0;
                    for (int channelIndex = 0; channelIndex < sourceChannels; channelIndex++) {
                        summedSample += sourceSamples[sourceFrameOffset + channelIndex];
                    }

                    convertedSamples[frameIndex] = ClampToInt16(Math.Round(summedSample / (double)sourceChannels));
                }

                return convertedSamples;
            }

            if (sourceChannels == 1) {
                for (int frameIndex = 0; frameIndex < sourceFrameCount; frameIndex++) {
                    short sample = sourceSamples[frameIndex];
                    int targetFrameOffset = frameIndex * targetChannels;
                    for (int channelIndex = 0; channelIndex < targetChannels; channelIndex++) {
                        convertedSamples[targetFrameOffset + channelIndex] = sample;
                    }
                }

                return convertedSamples;
            }

            throw new InvalidOperationException($"Audio channel conversion from {sourceChannels} to {targetChannels} is not implemented.");
        }

        /// <summary>
        /// Resamples one PCM16 sample buffer to the requested output sample rate using linear interpolation per channel.
        /// </summary>
        /// <param name="sourceSamples">Decoded PCM16 sample values after channel conversion.</param>
        /// <param name="channelCount">Channel count carried by the sample buffer.</param>
        /// <param name="sourceSampleRate">Source sample rate.</param>
        /// <param name="targetSampleRate">Requested output sample rate.</param>
        /// <returns>Resampled PCM16 sample values.</returns>
        short[] ResampleAudioSamples(short[] sourceSamples, ushort channelCount, int sourceSampleRate, int targetSampleRate) {
            if (sourceSamples == null) {
                throw new ArgumentNullException(nameof(sourceSamples));
            } else if (channelCount == 0) {
                throw new ArgumentOutOfRangeException(nameof(channelCount), "Channel count must be positive.");
            } else if (sourceSampleRate <= 0) {
                throw new ArgumentOutOfRangeException(nameof(sourceSampleRate), "Source sample rate must be positive.");
            } else if (targetSampleRate <= 0) {
                throw new ArgumentOutOfRangeException(nameof(targetSampleRate), "Target sample rate must be positive.");
            }

            if (sourceSampleRate == targetSampleRate || sourceSamples.Length == 0) {
                return sourceSamples;
            }

            int sourceFrameCount = sourceSamples.Length / channelCount;
            if (sourceFrameCount == 0) {
                return Array.Empty<short>();
            }

            int targetFrameCount = (int)Math.Round(sourceFrameCount * (double)targetSampleRate / sourceSampleRate);
            if (targetFrameCount <= 0) {
                targetFrameCount = 1;
            }

            short[] resampledSamples = new short[targetFrameCount * channelCount];
            for (int targetFrameIndex = 0; targetFrameIndex < targetFrameCount; targetFrameIndex++) {
                double sourceFramePosition = targetFrameIndex * (double)sourceSampleRate / targetSampleRate;
                int leftFrameIndex = (int)Math.Floor(sourceFramePosition);
                if (leftFrameIndex >= sourceFrameCount) {
                    leftFrameIndex = sourceFrameCount - 1;
                }

                int rightFrameIndex = leftFrameIndex + 1;
                if (rightFrameIndex >= sourceFrameCount) {
                    rightFrameIndex = sourceFrameCount - 1;
                }

                double blend = sourceFramePosition - leftFrameIndex;
                int targetFrameOffset = targetFrameIndex * channelCount;
                int leftFrameOffset = leftFrameIndex * channelCount;
                int rightFrameOffset = rightFrameIndex * channelCount;
                for (int channelIndex = 0; channelIndex < channelCount; channelIndex++) {
                    double leftSample = sourceSamples[leftFrameOffset + channelIndex];
                    double rightSample = sourceSamples[rightFrameOffset + channelIndex];
                    double interpolatedSample = leftSample + ((rightSample - leftSample) * blend);
                    resampledSamples[targetFrameOffset + channelIndex] = ClampToInt16(Math.Round(interpolatedSample));
                }
            }

            return resampledSamples;
        }

        /// <summary>
        /// Encodes one PCM16 sample buffer back into its serialized byte payload form.
        /// </summary>
        /// <param name="samples">PCM16 sample values to encode.</param>
        /// <returns>PCM16 payload bytes.</returns>
        byte[] EncodePcm16Samples(short[] samples) {
            if (samples == null) {
                throw new ArgumentNullException(nameof(samples));
            }

            if (samples.Length == 0) {
                return Array.Empty<byte>();
            }

            byte[] encodedBytes = new byte[samples.Length * sizeof(short)];
            Buffer.BlockCopy(samples, 0, encodedBytes, 0, encodedBytes.Length);
            return encodedBytes;
        }

        /// <summary>
        /// Encodes one mono PCM16 sample buffer into the Nintendo DS IMA ADPCM framing consumed by libnds.
        /// </summary>
        /// <param name="samples">PCM16 sample values to encode.</param>
        /// <returns>IMA ADPCM payload bytes prefixed with the native 4-byte predictor header.</returns>
        byte[] EncodeNintendoDsImaAdpcmSamples(short[] samples) {
            if (samples == null) {
                throw new ArgumentNullException(nameof(samples));
            }

            if (samples.Length == 0) {
                return Array.Empty<byte>();
            }

            int nibbleCount = Math.Max(0, samples.Length - 1);
            byte[] encodedBytes = new byte[4 + ((nibbleCount + 1) / 2)];
            short predictor = samples[0];
            int stepIndex = 0;
            encodedBytes[0] = (byte)(predictor & 0xFF);
            encodedBytes[1] = (byte)((predictor >> 8) & 0xFF);
            encodedBytes[2] = (byte)stepIndex;
            encodedBytes[3] = 0;

            for (int sampleIndex = 1; sampleIndex < samples.Length; sampleIndex++) {
                byte adpcmNibble = EncodeNintendoDsImaAdpcmNibble(samples[sampleIndex], ref predictor, ref stepIndex);
                int payloadByteIndex = 4 + ((sampleIndex - 1) / 2);
                if (((sampleIndex - 1) & 1) == 0) {
                    encodedBytes[payloadByteIndex] = adpcmNibble;
                } else {
                    encodedBytes[payloadByteIndex] |= (byte)(adpcmNibble << 4);
                }
            }

            return encodedBytes;
        }

        /// <summary>
        /// Encodes one PCM16 sample into one Nintendo DS IMA ADPCM nibble while updating predictor state.
        /// </summary>
        /// <param name="sample">PCM16 sample value to encode.</param>
        /// <param name="predictor">Current ADPCM predictor updated in-place.</param>
        /// <param name="stepIndex">Current ADPCM step-table index updated in-place.</param>
        /// <returns>Encoded 4-bit IMA ADPCM nibble.</returns>
        byte EncodeNintendoDsImaAdpcmNibble(short sample, ref short predictor, ref int stepIndex) {
            int step = NintendoDsImaAdpcmStepTable[stepIndex];
            int delta = sample - predictor;
            int nibble = 0;
            if (delta < 0) {
                nibble = 8;
                delta = -delta;
            }

            int diff = step >> 3;
            if (delta >= step) {
                nibble |= 4;
                delta -= step;
                diff += step;
            }

            step >>= 1;
            if (delta >= step) {
                nibble |= 2;
                delta -= step;
                diff += step;
            }

            step >>= 1;
            if (delta >= step) {
                nibble |= 1;
                diff += step;
            }

            int predictorValue = predictor;
            predictorValue += (nibble & 8) != 0 ? -diff : diff;
            predictor = ClampToInt16(predictorValue);

            stepIndex = Math.Clamp(stepIndex + NintendoDsImaAdpcmIndexTable[nibble], 0, NintendoDsImaAdpcmStepTable.Length - 1);
            return (byte)nibble;
        }

        /// <summary>
        /// Clamps one floating-point sample value into the signed 16-bit PCM range.
        /// </summary>
        /// <param name="value">Floating-point sample value.</param>
        /// <returns>Clamped PCM16 sample value.</returns>
        short ClampToInt16(double value) {
            if (value < short.MinValue) {
                return short.MinValue;
            }
            if (value > short.MaxValue) {
                return short.MaxValue;
            }

            return (short)value;
        }

        static readonly int[] NintendoDsImaAdpcmIndexTable = [
            -1, -1, -1, -1,
             2,  4,  6,  8,
            -1, -1, -1, -1,
             2,  4,  6,  8
        ];

        static readonly int[] NintendoDsImaAdpcmStepTable = [
                7,     8,     9,    10,    11,    12,    13,    14,
               16,    17,    19,    21,    23,    25,    28,    31,
               34,    37,    41,    45,    50,    55,    60,    66,
               73,    80,    88,    97,   107,   118,   130,   143,
              157,   173,   190,   209,   230,   253,   279,   307,
              337,   371,   408,   449,   494,   544,   598,   658,
              724,   796,   876,   963,  1060,  1166,  1282,  1411,
             1552,  1707,  1878,  2066,  2272,  2499,  2749,  3024,
             3327,  3660,  4026,  4428,  4871,  5358,  5894,  6484,
             7132,  7845,  8630,  9493, 10442, 11487, 12635, 13899,
            15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794,
            32767
        ];

        /// <summary>
        /// Builds the chunk table published on one imported audio asset.
        /// </summary>
        /// <param name="encodedBytes">Encoded PCM payload bytes.</param>
        /// <param name="processorSettings">Processor settings that drive chunking behavior.</param>
        /// <returns>Chunk table for the imported audio asset.</returns>
        AudioChunkDescriptor[] BuildAudioChunks(byte[] encodedBytes, AudioAssetProcessorSettings processorSettings) {
            if (encodedBytes == null) {
                throw new ArgumentNullException(nameof(encodedBytes));
            } else if (processorSettings == null) {
                throw new ArgumentNullException(nameof(processorSettings));
            }

            if (encodedBytes.Length == 0) {
                return Array.Empty<AudioChunkDescriptor>();
            }

            int chunkSize = processorSettings.PlaybackMode == AudioPlaybackMode.Streamed
                ? processorSettings.StreamChunkByteSize
                : encodedBytes.Length;
            if (chunkSize <= 0) {
                chunkSize = encodedBytes.Length;
            }

            List<AudioChunkDescriptor> chunks = new List<AudioChunkDescriptor>();
            for (int offset = 0; offset < encodedBytes.Length; offset += chunkSize) {
                int byteLength = Math.Min(chunkSize, encodedBytes.Length - offset);
                chunks.Add(new AudioChunkDescriptor {
                    ByteOffset = offset,
                    ByteLength = byteLength
                });
            }

            return chunks.ToArray();
        }

        /// <summary>
        /// Resolves the texture processor settings for one explicit platform id, returning defaults when none were saved yet.
        /// </summary>
        /// <param name="settings">Resolved import settings for the source file.</param>
        /// <param name="platformId">Platform texture-settings key that should drive the returned processor settings.</param>
        /// <returns>Texture processor settings for the requested platform context.</returns>
        TextureAssetProcessorSettings GetTextureProcessorSettings(AssetImportSettings settings, string platformId) {
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

            AssetPlatformProcessorSettings platformSettings;
            if (!settings.Processor.Platforms.TryGetValue(normalizedPlatformId, out platformSettings) || platformSettings == null || platformSettings.Texture == null) {
                return CreateDefaultTextureProcessorSettings(normalizedPlatformId);
            }

            return platformSettings.Texture;
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
        /// Resolves the processor-settings platform key that should drive audio processing for the current manager state.
        /// </summary>
        /// <param name="settings">Resolved typed audio settings for the source file.</param>
        /// <returns>Platform identifier used for audio processor settings, or an empty string when no platform context exists.</returns>
        string ResolveAudioProcessorPlatformId(AudioAssetImportSettings settings) {
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
        /// Resolves the audio processor settings for the active processing platform, returning defaults when none were saved yet.
        /// </summary>
        /// <param name="settings">Resolved typed audio settings for the source file.</param>
        /// <returns>Audio processor settings for the current platform context.</returns>
        AudioAssetProcessorSettings GetCurrentPlatformAudioProcessorSettings(AudioAssetImportSettings settings) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            string platformId = ResolveAudioProcessorPlatformId(settings);
            if (string.IsNullOrWhiteSpace(platformId)) {
                return CreateDefaultAudioProcessorSettings(platformId);
            }

            if (settings.Processor == null || settings.Processor.Platforms == null) {
                return CreateDefaultAudioProcessorSettings(platformId);
            }

            if (!settings.Processor.Platforms.TryGetValue(platformId, out AudioAssetProcessorSettings platformSettings) || platformSettings == null) {
                return CreateDefaultAudioProcessorSettings(platformId);
            }

            return platformSettings;
        }

        /// <summary>
        /// Resolves the audio processor settings for one explicit platform id, returning defaults when none were saved yet.
        /// </summary>
        /// <param name="settings">Resolved typed audio settings for the source file.</param>
        /// <param name="platformId">Platform audio-settings key that should drive the returned processor settings.</param>
        /// <returns>Audio processor settings for the requested platform context.</returns>
        AudioAssetProcessorSettings GetAudioProcessorSettings(AudioAssetImportSettings settings, string platformId) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            string normalizedPlatformId = platformId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedPlatformId)) {
                return CreateDefaultAudioProcessorSettings(normalizedPlatformId);
            }

            if (settings.Processor == null || settings.Processor.Platforms == null) {
                return CreateDefaultAudioProcessorSettings(normalizedPlatformId);
            }

            if (!settings.Processor.Platforms.TryGetValue(normalizedPlatformId, out AudioAssetProcessorSettings platformSettings) || platformSettings == null) {
                return CreateDefaultAudioProcessorSettings(normalizedPlatformId);
            }

            return platformSettings;
        }

        /// <summary>
        /// Resolves the font processor settings for the requested platform, returning defaults when none were saved yet.
        /// </summary>
        /// <param name="settings">Resolved import settings for the source file.</param>
        /// <param name="platformId">Target platform identifier whose font settings should be applied.</param>
        /// <returns>Font processor settings for the requested platform context.</returns>
        FontAssetProcessorSettings GetFontProcessorSettings(AssetImportSettings settings, string platformId) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            } else if (string.IsNullOrWhiteSpace(platformId)) {
                return CreateDefaultFontProcessorSettings();
            } else if (settings.Processor == null || settings.Processor.Platforms == null) {
                return CreateDefaultFontProcessorSettings();
            }

            if (!settings.Processor.Platforms.TryGetValue(platformId, out AssetPlatformProcessorSettings platformSettings) || platformSettings == null) {
                return CreateDefaultFontProcessorSettings();
            }

            return platformSettings.Font;
        }

        /// <summary>
        /// Resolves the generated font-atlas texture settings for the requested platform, returning platform-aware defaults when none were saved yet.
        /// </summary>
        /// <param name="settings">Resolved import settings for the source file.</param>
        /// <param name="platformId">Target platform identifier whose font-atlas texture settings should be applied.</param>
        /// <returns>Texture processor settings for the generated font atlas.</returns>
        TextureAssetProcessorSettings GetFontAtlasTextureProcessorSettings(AssetImportSettings settings, string platformId) {
            if (settings == null) {
                throw new ArgumentNullException(nameof(settings));
            }

            string normalizedPlatformId = platformId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedPlatformId)) {
                return CreateDefaultFontAtlasTextureProcessorSettings(normalizedPlatformId);
            }

            if (settings.Processor == null || settings.Processor.Platforms == null) {
                return CreateDefaultFontAtlasTextureProcessorSettings(normalizedPlatformId);
            }

            if (!settings.Processor.Platforms.TryGetValue(normalizedPlatformId, out AssetPlatformProcessorSettings platformSettings)
                || platformSettings == null
                || platformSettings.Sections == null
                || !platformSettings.Sections.TryGetValue(FontAtlasTextureAssetPlatformSettingsSectionDefinition.SectionIdValue, out AssetPlatformSettingsSection section)
                || section == null
                || section.Settings == null) {
                return CreateDefaultFontAtlasTextureProcessorSettings(normalizedPlatformId);
            }

            if (section.Settings is not TextureAssetProcessorSettings textureSettings) {
                throw new InvalidOperationException(
                    $"Platform font-atlas texture settings for '{normalizedPlatformId}' must use one {nameof(TextureAssetProcessorSettings)} payload.");
            }

            return textureSettings;
        }

        /// <summary>
        /// Creates the default texture processor settings used when a source asset has not authored an explicit override for the active platform yet.
        /// </summary>
        /// <param name="platformId">Active processing platform identifier.</param>
        /// <returns>Default texture processor settings for the requested platform.</returns>
        TextureAssetProcessorSettings CreateDefaultTextureProcessorSettings(string platformId) {
            return new TextureAssetProcessorSettings {
                MaxResolution = 0,
                ColorFormatId = TextureAssetColorFormat.Rgba32.ToString(),
                AlphaPrecision = TextureAssetAlphaPrecision.A8
            };
        }

        /// <summary>
        /// Creates the default generated font-atlas texture settings used when a source font has not authored an explicit override for the active platform yet.
        /// </summary>
        /// <param name="platformId">Active processing platform identifier.</param>
        /// <returns>Default font-atlas texture processor settings for the requested platform.</returns>
        TextureAssetProcessorSettings CreateDefaultFontAtlasTextureProcessorSettings(string platformId) {
            if (string.Equals(platformId, "ds", StringComparison.OrdinalIgnoreCase)) {
                return new TextureAssetProcessorSettings {
                    MaxResolution = 0,
                    ColorFormatId = TextureAssetColorFormat.Indexed4.ToString(),
                    AlphaPrecision = TextureAssetAlphaPrecision.Binary
                };
            }

            return CreateDefaultTextureProcessorSettings(platformId);
        }

        /// <summary>
        /// Creates the default font processor settings used when a source asset has not authored an explicit override yet.
        /// </summary>
        /// <returns>Default font processor settings.</returns>
        FontAssetProcessorSettings CreateDefaultFontProcessorSettings() {
            return new FontAssetProcessorSettings {
                PixelSize = FontAssetProcessorSettings.DefaultPixelSize
            };
        }

        /// <summary>
        /// Creates the default audio processor settings used when a source asset has not authored an explicit override yet.
        /// </summary>
        /// <returns>Default audio processor settings.</returns>
        AudioAssetProcessorSettings CreateDefaultAudioProcessorSettings(string platformId) {
            AudioAssetProcessorSettings settings = new AudioAssetProcessorSettings();
            if (string.Equals(platformId, "ds", StringComparison.OrdinalIgnoreCase)) {
                settings.EncodingFamilyId = "adpcm-buffered";
                settings.PlaybackMode = AudioPlaybackMode.Predecoded;
                settings.TargetChannels = 1;
                // DS audio is cooked to a smaller mono payload so buffered playback fits the runtime memory budget.
                settings.TargetSampleRate = 11025;
            } else if (string.Equals(platformId, "ps2", StringComparison.OrdinalIgnoreCase)) {
                settings.TargetChannels = 1;
                // PS2 playback still deserializes the cooked PCM payload into EE memory, so long music tracks need a more aggressive mono low-rate cook to fit.
                settings.TargetSampleRate = 4000;
                settings.StreamChunkByteSize = 4096;
            } else if (string.Equals(platformId, "psp", StringComparison.OrdinalIgnoreCase)
                || string.Equals(platformId, "gamecube", StringComparison.OrdinalIgnoreCase)
                || string.Equals(platformId, "wii", StringComparison.OrdinalIgnoreCase)) {
                settings.TargetChannels = 1;
                // Constrained platforms need a smaller cook footprint for long looping music tracks than the generic preset.
                settings.TargetSampleRate = 11025;
                settings.StreamChunkByteSize = 4096;
            }

            return settings;
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
        /// Builds the output path for an imported audio asset.
        /// </summary>
        /// <param name="assetId">Asset identifier used in the file name.</param>
        /// <returns>Absolute path to the serialized asset file.</returns>
        string GetAudioAssetPath(string assetId) {
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
