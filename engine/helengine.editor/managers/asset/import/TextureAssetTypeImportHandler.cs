namespace helengine.editor {
    /// <summary>
    /// Owns the texture side of the editor import pipeline: registered texture importers, the extensions they claim, and the
    /// default texture importer selected per extension.
    /// </summary>
    sealed class TextureAssetTypeImportHandler : EditorAssetTypeImportHandler {
        /// <summary>
        /// Registered texture importers keyed by identifier.
        /// </summary>
        public readonly Dictionary<string, ITextureImporter> ImportersById = new Dictionary<string, ITextureImporter>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Texture importer identifiers keyed by normalized extension, preserving registration order so overlapping importers stay selectable.
        /// </summary>
        public readonly Dictionary<string, List<string>> ImporterIdsByExtension = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates the texture handler for one asset import manager.
        /// </summary>
        /// <param name="owner">Manager that hosts this handler.</param>
        public TextureAssetTypeImportHandler(AssetImportManager owner) : base(owner) {
        }

        /// <summary>
        /// Gets the texture asset family identifier.
        /// </summary>
        public override EditorAssetImportKind Kind => EditorAssetImportKind.Texture;

        /// <summary>
        /// Gets the capitalized family name used when reporting a duplicate texture importer registration.
        /// </summary>
        public override string ImporterKindLabel => "Texture";

        /// <summary>
        /// Gets the lowercase family name used when reporting that an importer id already belongs to texture assets.
        /// </summary>
        public override string AssetKindLabel => "texture";

        /// <summary>
        /// Gets the article-qualified phrase used when reporting that an extension already maps to a texture importer.
        /// </summary>
        public override string ExtensionMappingLabel => "a texture importer";

        /// <summary>
        /// Checks whether one importer identifier is registered as a texture importer.
        /// </summary>
        /// <param name="importerId">Importer identifier to look for.</param>
        /// <returns>True when a texture importer owns the identifier.</returns>
        public override bool ContainsImporterId(string importerId) {
            return ImportersById.ContainsKey(importerId);
        }

        /// <summary>
        /// Gets the identifiers of every registered texture importer.
        /// </summary>
        /// <returns>Case-insensitively sorted texture importer identifiers.</returns>
        public override IReadOnlyList<string> GetImporterIds() {
            List<string> importerIds = new List<string>(ImportersById.Count);
            foreach (string importerId in ImportersById.Keys) {
                importerIds.Add(importerId);
            }

            return SortImporterIds(importerIds);
        }

        /// <summary>
        /// Checks whether one normalized extension is claimed by any registered texture importer.
        /// </summary>
        /// <param name="normalizedExtension">Extension already normalized to a lowercase dotted form.</param>
        /// <returns>True when a texture importer claims the extension.</returns>
        public override bool IsNormalizedExtensionSupported(string normalizedExtension) {
            return ImporterIdsByExtension.ContainsKey(normalizedExtension);
        }

        /// <summary>
        /// Returns the texture importer identifiers registered for one normalized extension.
        /// </summary>
        /// <param name="normalizedExtension">Extension already normalized to a lowercase dotted form.</param>
        /// <returns>Copy of the applicable texture importer identifiers, or null when no texture importer claims the extension.</returns>
        public override IReadOnlyList<string> TryGetImporterIdsForExtension(string normalizedExtension) {
            List<string> importerIds;
            if (!ImporterIdsByExtension.TryGetValue(normalizedExtension, out importerIds)) {
                return null;
            }

            return new List<string>(importerIds);
        }

        /// <summary>
        /// Checks whether more than one registered texture importer claims one normalized extension, which forces cache
        /// identities for that extension to be qualified by importer id.
        /// </summary>
        /// <param name="normalizedExtension">Extension already normalized to a lowercase dotted form.</param>
        /// <returns>True when at least two texture importers claim the extension.</returns>
        public bool HasOverlappingImportersForExtension(string normalizedExtension) {
            List<string> importerIds;
            if (!ImporterIdsByExtension.TryGetValue(normalizedExtension, out importerIds)) {
                return false;
            }

            return importerIds.Count > 1;
        }

        /// <summary>
        /// Registers one texture importer, its content processor and every extension it claims.
        /// </summary>
        /// <param name="registration">Importer registration data.</param>
        public void Register(TextureImporterRegistration registration) {
            if (registration == null) {
                throw new ArgumentNullException(nameof(registration));
            }

            EnsureImporterIdIsUnregistered(registration.ImporterId);
            EnsureImporterIdIsFree(registration.ImporterId, RegistrationConflictHandlers);

            ImportersById.Add(registration.ImporterId, registration.Importer);
            Owner.AssetContentManager.RegisterProcessor(
                registration.ImporterId,
                new TextureImporterContentProcessor(registration.Importer),
                Array.Empty<string>());
            string[] extensions = registration.Extensions;
            for (int index = 0; index < extensions.Length; index++) {
                string extension = Owner.NormalizeExtension(extensions[index]);
                EnsureExtensionIsFree(extension, RegistrationConflictHandlers);
                RegisterImporterExtension(extension, registration.ImporterId);
                if (!DefaultImporterIdsByExtension.ContainsKey(extension)) {
                    DefaultImporterIdsByExtension[extension] = registration.ImporterId;
                }
            }
        }

        /// <summary>
        /// Makes one registered texture importer the default for a specific extension.
        /// </summary>
        /// <param name="extension">File extension to associate with the importer.</param>
        /// <param name="importerId">Identifier of the importer to use.</param>
        public void SetDefaultImporter(string extension, string importerId) {
            if (string.IsNullOrWhiteSpace(extension)) {
                throw new ArgumentException("Extension must be provided.", nameof(extension));
            }

            if (string.IsNullOrWhiteSpace(importerId)) {
                throw new ArgumentException("Importer id must be provided.", nameof(importerId));
            }

            EnsureImporterExists(importerId);
            string normalized = Owner.NormalizeExtension(extension);
            EnsureExtensionIsFree(normalized, DefaultSelectionConflictHandlers);
            EnsureImporterSupportsExtension(normalized, importerId);
            DefaultImporterIdsByExtension[normalized] = importerId;
        }

        /// <summary>
        /// Ensures a texture importer is registered.
        /// </summary>
        /// <param name="importerId">Identifier to verify.</param>
        public void EnsureImporterExists(string importerId) {
            if (!ImportersById.ContainsKey(importerId)) {
                throw new InvalidOperationException($"Texture importer '{importerId}' is not registered.");
            }
        }

        /// <summary>
        /// Checks whether a texture importer is registered.
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
        /// Retrieves a texture importer by identifier.
        /// </summary>
        /// <param name="importerId">Identifier of the importer.</param>
        /// <returns>Importer implementation.</returns>
        public ITextureImporter GetImporter(string importerId) {
            ITextureImporter importer;
            if (ImportersById.TryGetValue(importerId, out importer)) {
                return importer;
            }

            throw new InvalidOperationException($"Texture importer '{importerId}' is not registered.");
        }

        /// <summary>
        /// Records that one texture importer supports one file extension.
        /// </summary>
        /// <param name="extension">Normalized file extension.</param>
        /// <param name="importerId">Importer identifier that supports the extension.</param>
        public void RegisterImporterExtension(string extension, string importerId) {
            if (string.IsNullOrWhiteSpace(extension)) {
                throw new ArgumentException("Extension must be provided.", nameof(extension));
            }

            if (string.IsNullOrWhiteSpace(importerId)) {
                throw new ArgumentException("Importer id must be provided.", nameof(importerId));
            }

            List<string> importerIds;
            if (!ImporterIdsByExtension.TryGetValue(extension, out importerIds)) {
                importerIds = new List<string>();
                ImporterIdsByExtension.Add(extension, importerIds);
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
        public void EnsureImporterSupportsExtension(string extension, string importerId) {
            if (string.IsNullOrWhiteSpace(extension)) {
                throw new ArgumentException("Extension must be provided.", nameof(extension));
            }

            if (string.IsNullOrWhiteSpace(importerId)) {
                throw new ArgumentException("Importer id must be provided.", nameof(importerId));
            }

            List<string> importerIds;
            if (!ImporterIdsByExtension.TryGetValue(extension, out importerIds)) {
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
        /// Loads a texture asset for a source file, importing it when the cache is missing or unreadable.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the texture source file.</param>
        /// <param name="asset">Loaded texture asset when available.</param>
        /// <returns>True when the source can be resolved to a texture asset.</returns>
        public bool TryLoadAsset(string sourcePath, out TextureAsset asset) {
            sourcePath = Owner.NormalizeAndValidateAuthoredSourcePath(sourcePath);

            if (!File.Exists(sourcePath)) {
                throw new FileNotFoundException("Texture source file was not found.", sourcePath);
            }

            TextureAssetImportSettings settings;
            if (!Owner.TryLoadOrCreateTextureImportSettings(sourcePath, out settings)) {
                asset = null;
                return false;
            }

            if (!IsImporterRegistered(settings.Importer.ImporterId)) {
                asset = null;
                return false;
            }

            string outputPath = Owner.GetTextureAssetPath(settings.Importer.AssetId);
            if (!File.Exists(outputPath)) {
                asset = Owner.ImportTexture(sourcePath);
                return true;
            }

            if (TryLoadCachedAsset(outputPath, out asset)) {
                return true;
            }

            asset = Owner.ImportTexture(sourcePath);
            return true;
        }

        /// <summary>
        /// Attempts to load a cached texture asset.
        /// </summary>
        /// <param name="outputPath">Absolute path to the cached texture asset.</param>
        /// <param name="asset">Loaded texture asset when the cache file exists and contains the expected payload type.</param>
        /// <returns>True when the cached asset was loaded successfully.</returns>
        public bool TryLoadCachedAsset(string outputPath, out TextureAsset asset) {
            if (string.IsNullOrWhiteSpace(outputPath)) {
                throw new ArgumentException("Output path must be provided.", nameof(outputPath));
            }

            asset = null;
            Asset cachedAsset;
            if (!Owner.TryLoadCachedAsset(outputPath, "TextureAsset", out cachedAsset)) {
                return false;
            }

            if (cachedAsset is TextureAsset textureAsset) {
                asset = textureAsset;
                return true;
            }

            throw new InvalidOperationException($"Texture cache file '{outputPath}' did not contain a TextureAsset payload.");
        }
    }
}
