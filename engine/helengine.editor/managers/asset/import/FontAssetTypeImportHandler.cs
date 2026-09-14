namespace helengine.editor {
    /// <summary>
    /// Owns the font side of the editor import pipeline: registered font importers and the default font importer selected per extension.
    /// </summary>
    sealed class FontAssetTypeImportHandler : EditorAssetTypeImportHandler {
        /// <summary>
        /// Registered font importers keyed by identifier.
        /// </summary>
        public readonly Dictionary<string, IFontImporter> ImportersById = new Dictionary<string, IFontImporter>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates the font handler for one asset import manager.
        /// </summary>
        /// <param name="owner">Manager that hosts this handler.</param>
        public FontAssetTypeImportHandler(AssetImportManager owner) : base(owner) {
        }

        /// <summary>
        /// Gets the font asset family identifier.
        /// </summary>
        public override EditorAssetImportKind Kind => EditorAssetImportKind.Font;

        /// <summary>
        /// Gets the capitalized family name used when reporting a duplicate font importer registration.
        /// </summary>
        public override string ImporterKindLabel => "Font";

        /// <summary>
        /// Gets the lowercase family name used when reporting that an importer id already belongs to font assets.
        /// </summary>
        public override string AssetKindLabel => "font";

        /// <summary>
        /// Gets the article-qualified phrase used when reporting that an extension already maps to a font importer.
        /// </summary>
        public override string ExtensionMappingLabel => "a font importer";

        /// <summary>
        /// Checks whether one importer identifier is registered as a font importer.
        /// </summary>
        /// <param name="importerId">Importer identifier to look for.</param>
        /// <returns>True when a font importer owns the identifier.</returns>
        public override bool ContainsImporterId(string importerId) {
            return ImportersById.ContainsKey(importerId);
        }

        /// <summary>
        /// Gets the identifiers of every registered font importer.
        /// </summary>
        /// <returns>Case-insensitively sorted font importer identifiers.</returns>
        public override IReadOnlyList<string> GetImporterIds() {
            List<string> importerIds = new List<string>(ImportersById.Count);
            foreach (string importerId in ImportersById.Keys) {
                importerIds.Add(importerId);
            }

            return SortImporterIds(importerIds);
        }

        /// <summary>
        /// Registers one font importer, its content processor and every extension it claims.
        /// </summary>
        /// <param name="registration">Importer registration data.</param>
        public void Register(FontImporterRegistration registration) {
            if (registration == null) {
                throw new ArgumentNullException(nameof(registration));
            }

            EnsureImporterIdIsUnregistered(registration.ImporterId);
            EnsureImporterIdIsFree(registration.ImporterId, RegistrationConflictHandlers);

            ImportersById.Add(registration.ImporterId, registration.Importer);
            Owner.AssetContentManager.RegisterProcessor(
                registration.ImporterId,
                new FontImporterContentProcessor(registration.Importer),
                registration.Extensions);
            string[] extensions = registration.Extensions;
            for (int index = 0; index < extensions.Length; index++) {
                string extension = Owner.NormalizeExtension(extensions[index]);
                EnsureExtensionIsFree(extension, RegistrationConflictHandlers);
                if (!DefaultImporterIdsByExtension.ContainsKey(extension)) {
                    DefaultImporterIdsByExtension[extension] = registration.ImporterId;
                }
            }
        }

        /// <summary>
        /// Ensures a font importer is registered.
        /// </summary>
        /// <param name="importerId">Identifier to verify.</param>
        public void EnsureImporterExists(string importerId) {
            if (!ImportersById.ContainsKey(importerId)) {
                throw new InvalidOperationException($"Font importer '{importerId}' is not registered.");
            }
        }

        /// <summary>
        /// Checks whether a font importer is registered.
        /// </summary>
        /// <param name="importerId">Identifier to verify.</param>
        /// <returns>True when a matching importer is registered.</returns>
        public bool IsImporterRegistered(string importerId) {
            if (string.IsNullOrWhiteSpace(importerId)) {
                return false;
            }

            return ImportersById.ContainsKey(importerId);
        }

        /// <summary>
        /// Retrieves a font importer by identifier.
        /// </summary>
        /// <param name="importerId">Identifier of the importer.</param>
        /// <returns>Importer implementation.</returns>
        public IFontImporter GetImporter(string importerId) {
            IFontImporter importer;
            if (ImportersById.TryGetValue(importerId, out importer)) {
                return importer;
            }

            throw new InvalidOperationException($"Font importer '{importerId}' is not registered.");
        }

        /// <summary>
        /// Loads a font asset for a source file, importing it when the cache is missing or unreadable.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the font source file.</param>
        /// <param name="asset">Loaded font asset when available.</param>
        /// <returns>True when the source can be resolved to a font asset.</returns>
        public bool TryLoadAsset(string sourcePath, out FontAsset asset) {
            sourcePath = Owner.NormalizeAndValidateAuthoredSourcePath(sourcePath);

            if (!File.Exists(sourcePath)) {
                throw new FileNotFoundException("Font source file was not found.", sourcePath);
            }

            AssetImportSettings settings;
            if (!Owner.TryLoadOrCreateImportSettings(sourcePath, out settings)) {
                asset = null;
                return false;
            }

            if (!IsImporterRegistered(settings.Importer.ImporterId)) {
                asset = null;
                return false;
            }

            string outputPath = Owner.GetFontAssetPath(settings.Importer.AssetId);
            if (!File.Exists(outputPath)) {
                asset = Owner.ImportFont(sourcePath);
                return true;
            }

            if (TryLoadCachedAsset(outputPath, out asset)) {
                return true;
            }

            asset = Owner.ImportFont(sourcePath);
            return true;
        }

        /// <summary>
        /// Attempts to load a cached font asset, restoring the runtime atlas texture editor rendering needs.
        /// </summary>
        /// <param name="outputPath">Absolute path to the cached font asset.</param>
        /// <param name="asset">Loaded font asset when the cache file exists and deserializes.</param>
        /// <returns>True when the cached asset was loaded successfully.</returns>
        public bool TryLoadCachedAsset(string outputPath, out FontAsset asset) {
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
                using MemoryStream stream = Owner.OpenVerifiedRead(outputPath);
                asset = RestoreRuntimeTextureForCachedAsset(FontAssetBinarySerializer.Deserialize(stream));
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
        FontAsset RestoreRuntimeTextureForCachedAsset(FontAsset asset) {
            if (asset == null) {
                throw new ArgumentNullException(nameof(asset));
            }

            if (asset.Texture != null || asset.SourceTextureAsset == null) {
                return asset;
            }

            if (Owner.RenderManager2D == null) {
                throw new InvalidOperationException("Cached font assets require session-owned 2D renderer resources before their runtime atlas can be restored.");
            }

            RuntimeTexture runtimeTexture = Owner.RenderManager2D.BuildTextureFromRaw(asset.SourceTextureAsset);
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
    }
}
