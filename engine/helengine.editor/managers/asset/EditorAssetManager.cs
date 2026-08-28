namespace helengine.editor {
    /// <summary>
    /// Provides asset browsing data and extension classification for the editor UI.
    /// </summary>
    public class EditorAssetManager : IDisposable {
        /// <summary>
        /// Name of the assets folder at the project root.
        /// </summary>
        const string AssetsFolderName = "assets";
        /// <summary>
        /// Extension used for asset import settings sidecar files.
        /// </summary>
        const string ImportSettingsExtension = ".hasset";

        /// <summary>
        /// Shared file classifier used by the asset browser and identity services.
        /// </summary>
        EditorAssetPathClassifier pathClassifier;

        /// <summary>
        /// Sidecar service used to provide stable authored identities.
        /// </summary>
        AssetIdentityMetadataService identityMetadataService;

        /// <summary>
        /// Project-scoped content hash cache.
        /// </summary>
        EditorAssetHashCache identityHashCache;

        /// <summary>
        /// Optional session-owned identity index borrowed by this browser manager.
        /// </summary>
        EditorAssetIdentityIndex identityIndex;

        /// <summary>
        /// Optional session resolver providing the publication boundary for borrowed reads.
        /// </summary>
        EditorAssetReferenceResolver publicationResolver;

        /// <summary>
        /// Indicates whether this manager created its cache and therefore releases it.
        /// </summary>
        bool ownsIdentityHashCache;

        /// <summary>
        /// Tracks repeated manager disposal calls.
        /// </summary>
        bool isDisposed;

        /// <summary>
        /// Extensions treated as image assets.
        /// </summary>
        readonly HashSet<string> imageExtensions = new HashSet<string>(TextureImportFormatCatalog.AllTextureExtensions, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Extensions treated as 3D model assets.
        /// </summary>
        readonly HashSet<string> modelExtensions = new HashSet<string>(AssimpModelFormatCatalog.AllModelExtensions, StringComparer.OrdinalIgnoreCase);
        /// <summary>
        /// Extensions treated as material assets.
        /// </summary>
        readonly HashSet<string> materialExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            EditorFileTemplateRegistry.MaterialExtension
        };
        /// <summary>
        /// Extensions treated as serialized scene assets.
        /// </summary>
        readonly HashSet<string> sceneExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            SceneAsset.FileExtension
        };
        /// <summary>
        /// Extensions treated as serialized blueprint assets.
        /// </summary>
        readonly HashSet<string> blueprintExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            BlueprintAsset.FileExtension
        };

        /// <summary>
        /// Extensions treated as audio assets.
        /// </summary>
        readonly HashSet<string> audioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            ".wav", ".mp3", ".ogg", ".flac", ".aac"
        };

        /// <summary>
        /// Extensions treated as script assets.
        /// </summary>
        readonly HashSet<string> scriptExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            ".cs", ".js", ".lua", ".py"
        };

        /// <summary>
        /// Extensions treated as configuration assets.
        /// </summary>
        readonly HashSet<string> configExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            ".json", ".xml", ".yaml", ".yml"
        };

        /// <summary>
        /// Extensions treated as source font assets.
        /// </summary>
        readonly HashSet<string> fontExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            ".ttf", ".otf"
        };

        /// <summary>
        /// Absolute path to the assets root on disk.
        /// </summary>
        string assetsRootPath;

        /// <summary>
        /// Absolute project root used for verified browser reads and directory
        /// creation.
        /// </summary>
        string projectRootPath;

        /// <summary>
        /// Current directory path relative to the assets root.
        /// </summary>
        string currentRelativePath;

        /// <summary>
        /// Initializes a new asset manager for the provided project path.
        /// </summary>
        /// <param name="projectPath">Path to the project root.</param>
        public EditorAssetManager(string projectPath) {
            Initialize(projectPath, null, null);
        }

        /// <summary>
        /// Initializes a browser manager using the resolver graph owned by an editor session.
        /// </summary>
        /// <param name="projectPath">Path to the project root.</param>
        /// <param name="referenceResolver">Session-owned resolver whose cache and index are borrowed.</param>
        internal EditorAssetManager(string projectPath, EditorAssetReferenceResolver referenceResolver) {
            if (referenceResolver == null) {
                throw new ArgumentNullException(nameof(referenceResolver));
            }

            publicationResolver = referenceResolver;
            Initialize(projectPath, referenceResolver.HashCacheValue, referenceResolver.IdentityIndexValue);
        }

        /// <summary>
        /// Initializes a manager over an explicitly owned cache for disposal-boundary tests.
        /// </summary>
        /// <param name="projectPath">Path to the project root.</param>
        /// <param name="hashCache">Cache owned by this manager.</param>
        /// <param name="ownsHashCache">Whether this manager releases the supplied cache.</param>
        internal EditorAssetManager(string projectPath, EditorAssetHashCache hashCache, bool ownsHashCache) {
            if (hashCache == null) {
                throw new ArgumentNullException(nameof(hashCache));
            }

            Initialize(projectPath, hashCache, null);
            ownsIdentityHashCache = ownsHashCache;
        }

        /// <summary>
        /// Initializes one manager over explicit shared identity services.
        /// </summary>
        /// <param name="projectPath">Path to the project root.</param>
        /// <param name="hashCache">Borrowed shared hash cache, or null to create one.</param>
        /// <param name="identityIndex">Borrowed shared identity index, or null for metadata-only browsing.</param>
        void Initialize(string projectPath, EditorAssetHashCache hashCache, EditorAssetIdentityIndex identityIndexValue) {
            assetsRootPath = ResolveAssetsRoot(projectPath);
            projectRootPath = Path.GetDirectoryName(assetsRootPath) ?? Directory.GetCurrentDirectory();
            currentRelativePath = string.Empty;
            pathClassifier = new EditorAssetPathClassifier();
            identityMetadataService = new AssetIdentityMetadataService();
            identityHashCache = hashCache ?? new EditorAssetHashCache(projectRootPath);
            ownsIdentityHashCache = hashCache == null;
            identityIndex = identityIndexValue;
        }

        /// <summary>
        /// Gets the absolute path to the assets root.
        /// </summary>
        public string AssetsRootPath => assetsRootPath;

        /// <summary>
        /// Gets the current directory path relative to the assets root.
        /// </summary>
        public string CurrentRelativePath => currentRelativePath;
        /// <summary>
        /// Gets the absolute path for the current folder.
        /// </summary>
        public string CurrentFullPath => GetCurrentFullPath();

        /// <summary>
        /// Builds the display path used by the asset browser UI.
        /// </summary>
        /// <returns>Display-ready path label for the current location.</returns>
        public string GetDisplayPath() {
            if (string.IsNullOrEmpty(currentRelativePath)) {
                return AssetsFolderName;
            }

            return $"{AssetsFolderName}/{currentRelativePath}";
        }

        /// <summary>
        /// Populates the provided list with entries for the current folder.
        /// </summary>
        /// <param name="entries">List to populate with asset entries.</param>
        /// <exception cref="ArgumentNullException">Thrown when the entries list is null.</exception>
        public void LoadEntries(List<AssetBrowserEntry> entries) {
            if (entries == null) {
                throw new ArgumentNullException(nameof(entries));
            }

            if (publicationResolver != null) {
                publicationResolver.ExecuteSynchronizedRead(() => {
                    LoadEntriesCore(entries);
                    return true;
                });
                return;
            }

            LoadEntriesCore(entries);
        }

        /// <summary>
        /// Populates browser entries after the caller has selected the appropriate read boundary.
        /// </summary>
        /// <param name="entries">List to populate with asset entries.</param>
        void LoadEntriesCore(List<AssetBrowserEntry> entries) {
            if (entries == null) {
                throw new ArgumentNullException(nameof(entries));
            }

            EnsureAssetsRootExists();
            entries.Clear();

            string currentPath = GetCurrentFullPath();
            if (!Directory.Exists(currentPath)) {
                currentRelativePath = string.Empty;
                currentPath = assetsRootPath;
            }

            try {
                var directories = Directory.GetDirectories(currentPath);
                for (int i = 0; i < directories.Length; i++) {
                    string dirPath = directories[i];
                    EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(dirPath, assetsRootPath);
                    string name = Path.GetFileName(dirPath);
                    if (string.IsNullOrWhiteSpace(name)) {
                        continue;
                    }

                    string relativePath = CombineRelativePath(currentRelativePath, name);
                    entries.Add(AssetBrowserEntry.CreateFileSystemDirectory(name, relativePath, dirPath));
                }

                var files = Directory.GetFiles(currentPath);
                for (int i = 0; i < files.Length; i++) {
                    string filePath = files[i];
                    EditorAuthoringTransactionRecoveryService.ValidateNoReparsePath(filePath, assetsRootPath);
                    string name = Path.GetFileName(filePath);
                    if (string.IsNullOrWhiteSpace(name)) {
                        continue;
                    }

                    string relativePath = CombineRelativePath(currentRelativePath, name);
                    string extension = Path.GetExtension(filePath);
                    if (pathClassifier.ShouldHide(filePath)) {
                        continue;
                    }
                    try {
                        AssetEntryKind entryKind = pathClassifier.Classify(filePath);
                        string assetId = LoadAssetId(filePath, relativePath);
                        string contentHash = identityHashCache.GetContentHash(filePath);
                        entries.Add(AssetBrowserEntry.CreateFileSystemFile(name, relativePath, filePath, extension, entryKind, assetId, contentHash));
                    } catch (Exception ex) {
                        Logger.WriteError($"Asset browser skipped '{relativePath}': {ex.Message}");
                    }
                }
            } catch (Exception ex) {
                Logger.WriteError($"Asset browser refresh failed: {ex.Message}");
            }

            entries.Sort(CompareEntries);
        }

        /// <summary>
        /// Flushes the project-scoped identity hash cache at the manager lifetime boundary.
        /// </summary>
        public void Dispose() {
            if (isDisposed) {
                return;
            }

            if (ownsIdentityHashCache) {
                identityHashCache.Dispose();
            }
            isDisposed = true;
        }

        /// <summary>
        /// Loads the indexed identity when one is available, without creating new metadata.
        /// </summary>
        /// <param name="filePath">Absolute authored path.</param>
        /// <param name="relativePath">Normalized assets-relative path.</param>
        /// <returns>Current asset id.</returns>
        string LoadAssetId(string filePath, string relativePath) {
            if (identityIndex != null) {
                EditorAssetIdentityEntry entry = identityIndex.FindByPath(relativePath);
                if (entry != null) {
                    return entry.AssetId;
                }

                if (pathClassifier.IsAuthoredAsset(filePath)) {
                    entry = identityIndex.RegisterOrUpdate(filePath);
                    return entry.AssetId;
                }
            }

            return LoadIdentityMetadata(filePath).AssetId;
        }

        /// <summary>
        /// Loads one current identity, creating metadata only when an external sidecar is absent.
        /// </summary>
        /// <param name="filePath">Absolute authored source path.</param>
        /// <returns>Validated current identity metadata.</returns>
        AssetIdentityMetadataDocument LoadIdentityMetadata(string filePath) {
            if (pathClassifier.UsesEmbeddedIdentity(filePath)) {
                return identityMetadataService.Load(filePath);
            }
            return identityMetadataService.LoadOrCreate(filePath, string.Empty);
        }

        /// <summary>
        /// Updates the current relative path when navigating into a child folder.
        /// </summary>
        /// <param name="relativePath">Relative path to navigate into.</param>
        /// <returns>True when the navigation target exists.</returns>
        public bool TryNavigateTo(string relativePath) {
            string normalized = NormalizeRelativePath(relativePath);
            string targetPath = string.IsNullOrEmpty(normalized)
                ? assetsRootPath
                : Path.Combine(assetsRootPath, normalized.Replace('/', Path.DirectorySeparatorChar));

            if (!Directory.Exists(targetPath)) {
                return false;
            }

            currentRelativePath = normalized;
            return true;
        }

        /// <summary>
        /// Updates the current relative path when navigating to the parent folder.
        /// </summary>
        /// <returns>True when the current path changed.</returns>
        public bool TryNavigateUp() {
            if (string.IsNullOrEmpty(currentRelativePath)) {
                return false;
            }

            string normalized = currentRelativePath.Replace('/', Path.DirectorySeparatorChar);
            string? parent = Path.GetDirectoryName(normalized);
            currentRelativePath = NormalizeRelativePath(parent ?? string.Empty);
            return true;
        }

        /// <summary>
        /// Classifies an entry so the UI can select the correct icon styling.
        /// </summary>
        /// <param name="entry">Entry to classify.</param>
        /// <returns>Category describing the entry.</returns>
        public AssetEntryKind GetEntryKind(AssetBrowserEntry entry) {
            if (entry.IsDirectory) {
                return AssetEntryKind.Directory;
            }

            return pathClassifier.Classify(entry.FullPath);
        }

        /// <summary>
        /// Ensures the assets root directory exists on disk.
        /// </summary>
        void EnsureAssetsRootExists() {
            if (!Directory.Exists(assetsRootPath)) {
                EditorAuthoringMutationScope.EnsureDirectory(projectRootPath, assetsRootPath);
            }
        }

        /// <summary>
        /// Gets the absolute path for the current relative folder.
        /// </summary>
        /// <returns>Absolute directory path for the current view.</returns>
        string GetCurrentFullPath() {
            if (string.IsNullOrEmpty(currentRelativePath)) {
                return assetsRootPath;
            }

            string relativePath = currentRelativePath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(assetsRootPath, relativePath);
        }

        /// <summary>
        /// Resolves and ensures the assets root folder for a project.
        /// </summary>
        /// <param name="projectPath">Path to the project root.</param>
        /// <returns>Absolute assets folder path.</returns>
        string ResolveAssetsRoot(string projectPath) {
            string rootPath = projectPath;
            if (string.IsNullOrWhiteSpace(rootPath)) {
                rootPath = Directory.GetCurrentDirectory();
            } else {
                try {
                    rootPath = Path.GetFullPath(rootPath);
                } catch {
                    rootPath = Directory.GetCurrentDirectory();
                }
            }

            if (File.Exists(rootPath)) {
                rootPath = Path.GetDirectoryName(rootPath) ?? Directory.GetCurrentDirectory();
            }

            if (!Directory.Exists(rootPath)) {
                rootPath = Directory.GetCurrentDirectory();
            }

            string assetsPath = Path.Combine(rootPath, AssetsFolderName);
            if (!Directory.Exists(assetsPath)) {
                EditorAuthoringMutationScope.EnsureDirectory(rootPath, assetsPath);
            }

            return assetsPath;
        }

        /// <summary>
        /// Normalizes a relative path to use forward slashes without leading or trailing separators.
        /// </summary>
        /// <param name="relativePath">Path string to normalize.</param>
        /// <returns>Normalized relative path.</returns>
        string NormalizeRelativePath(string relativePath) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                return string.Empty;
            }

            return relativePath.Replace('\\', '/').Trim('/');
        }

        /// <summary>
        /// Combines two path segments into a normalized relative path.
        /// </summary>
        /// <param name="left">Base relative path.</param>
        /// <param name="right">Child path segment.</param>
        /// <returns>Normalized combined relative path.</returns>
        string CombineRelativePath(string left, string right) {
            if (string.IsNullOrWhiteSpace(left)) {
                return NormalizeRelativePath(right);
            }

            if (string.IsNullOrWhiteSpace(right)) {
                return NormalizeRelativePath(left);
            }

            return NormalizeRelativePath($"{left}/{right}");
        }

        /// <summary>
        /// Classifies one file extension into the browser icon category used by the UI.
        /// </summary>
        /// <param name="extension">File extension including the dot.</param>
        /// <returns>Visual category used by the browser row.</returns>
        AssetEntryKind ClassifyEntryKind(string filePath, string extension) {
            if (string.IsNullOrEmpty(extension)) {
                return AssetEntryKind.Unknown;
            }

            if (string.Equals(extension, ImportSettingsExtension, StringComparison.OrdinalIgnoreCase)) {
                AssetEntryKind hassetKind;
                if (TryClassifyHassetFile(filePath, out hassetKind)) {
                    return hassetKind;
                }

                return AssetEntryKind.File;
            }

            if (imageExtensions.Contains(extension)) {
                return AssetEntryKind.Image;
            }

            if (modelExtensions.Contains(extension)) {
                return AssetEntryKind.Model;
            }

            if (materialExtensions.Contains(extension)) {
                return AssetEntryKind.Material;
            }

            if (sceneExtensions.Contains(extension)) {
                return AssetEntryKind.Scene;
            }

            if (blueprintExtensions.Contains(extension)) {
                return AssetEntryKind.Blueprint;
            }

            if (audioExtensions.Contains(extension)) {
                return AssetEntryKind.Audio;
            }

            if (scriptExtensions.Contains(extension)) {
                return AssetEntryKind.Script;
            }

            if (configExtensions.Contains(extension)) {
                return AssetEntryKind.Config;
            }

            if (fontExtensions.Contains(extension)) {
                return AssetEntryKind.Font;
            }

            return AssetEntryKind.File;
        }

        /// <summary>
        /// Returns true when a file should be omitted from the asset browser.
        /// </summary>
        /// <param name="filePath">Absolute path to the candidate file.</param>
        /// <param name="extension">File extension including the dot.</param>
        /// <returns>True when the file should be hidden from the browser.</returns>
        bool ShouldHideFile(string filePath, string extension) {
            if (!string.Equals(extension, ImportSettingsExtension, StringComparison.OrdinalIgnoreCase)) {
                return false;
            }

            AssetEntryKind hassetKind;
            if (!TryClassifyHassetFile(filePath, out hassetKind)) {
                return true;
            }

            return hassetKind != AssetEntryKind.Material;
        }

        /// <summary>
        /// Classifies one `.hasset` file by peeking its HELE header so authored material assets can coexist with importer settings sidecars.
        /// </summary>
        /// <param name="filePath">Absolute path to the `.hasset` file.</param>
        /// <param name="entryKind">Resolved asset-browser entry kind when classification succeeds.</param>
        /// <returns>True when the file header could be classified successfully.</returns>
        bool TryClassifyHassetFile(string filePath, out AssetEntryKind entryKind) {
            if (string.IsNullOrWhiteSpace(filePath)) {
                entryKind = AssetEntryKind.Unknown;
                return false;
            }

            try {
                using MemoryStream stream = new MemoryStream(
                    EditorAuthoringMutationScope.ReadAllBytes(projectRootPath, filePath),
                    writable: false);
                EngineBinaryHeader header = EngineBinaryHeaderSerializer.Read(stream);
                if (header.FormatId != EditorAssetBinarySerializer.FormatId) {
                    entryKind = AssetEntryKind.File;
                    return true;
                }

                if (header.RecordKind == (ushort)EditorBinaryRecordKind.Asset) {
                    entryKind = ClassifyNativeAssetValueKind(header.ValueKind);
                    return true;
                }

                if (header.RecordKind == (ushort)EditorBinaryRecordKind.AssetImportSettings &&
                    header.ValueKind == (ushort)AssetImportSettingsBinaryValueKind.MaterialAssetCommonSettingsDocument) {
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

        /// <summary>
        /// Maps a native asset value kind to the asset-browser category.
        /// </summary>
        /// <param name="valueKind">Native asset value kind.</param>
        /// <returns>Asset-browser category.</returns>
        static AssetEntryKind ClassifyNativeAssetValueKind(ushort valueKind) {
            switch ((EditorAssetBinaryValueKind)valueKind) {
                case EditorAssetBinaryValueKind.TextureAsset:
                    return AssetEntryKind.Image;
                case EditorAssetBinaryValueKind.ModelAsset:
                    return AssetEntryKind.Model;
                case EditorAssetBinaryValueKind.MaterialAsset:
                    return AssetEntryKind.Material;
                case EditorAssetBinaryValueKind.SceneAsset:
                    return AssetEntryKind.Scene;
                case EditorAssetBinaryValueKind.BlueprintAsset:
                    return AssetEntryKind.Blueprint;
                case EditorAssetBinaryValueKind.AudioAsset:
                    return AssetEntryKind.Audio;
                default:
                    return AssetEntryKind.File;
            }
        }

        /// <summary>
        /// Compares entries so directories sort before files, then by name.
        /// </summary>
        /// <param name="left">Left entry to compare.</param>
        /// <param name="right">Right entry to compare.</param>
        /// <returns>Sort order value.</returns>
        int CompareEntries(AssetBrowserEntry left, AssetBrowserEntry right) {
            if (left.IsDirectory != right.IsDirectory) {
                return left.IsDirectory ? -1 : 1;
            }

            return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
