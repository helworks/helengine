namespace helengine.editor {
    /// <summary>
    /// Owns the text side of the editor import pipeline: registered text importers and the default text importer selected per extension.
    /// </summary>
    sealed class TextAssetTypeImportHandler : EditorAssetTypeImportHandler {
        /// <summary>
        /// Registered text importers keyed by identifier.
        /// </summary>
        public readonly Dictionary<string, ITextImporter> ImportersById = new Dictionary<string, ITextImporter>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates the text handler for one asset import manager.
        /// </summary>
        /// <param name="owner">Manager that hosts this handler.</param>
        public TextAssetTypeImportHandler(AssetImportManager owner) : base(owner) {
        }

        /// <summary>
        /// Gets the text asset family identifier.
        /// </summary>
        public override EditorAssetImportKind Kind => EditorAssetImportKind.Text;

        /// <summary>
        /// Gets the capitalized family name used when reporting a duplicate text importer registration.
        /// </summary>
        public override string ImporterKindLabel => "Text";

        /// <summary>
        /// Gets the lowercase family name used when reporting that an importer id already belongs to text assets.
        /// </summary>
        public override string AssetKindLabel => "text";

        /// <summary>
        /// Gets the article-qualified phrase used when reporting that an extension already maps to a text importer.
        /// </summary>
        public override string ExtensionMappingLabel => "a text importer";

        /// <summary>
        /// Checks whether one importer identifier is registered as a text importer.
        /// </summary>
        /// <param name="importerId">Importer identifier to look for.</param>
        /// <returns>True when a text importer owns the identifier.</returns>
        public override bool ContainsImporterId(string importerId) {
            return ImportersById.ContainsKey(importerId);
        }

        /// <summary>
        /// Gets the identifiers of every registered text importer.
        /// </summary>
        /// <returns>Case-insensitively sorted text importer identifiers.</returns>
        public override IReadOnlyList<string> GetImporterIds() {
            List<string> importerIds = new List<string>(ImportersById.Count);
            foreach (string importerId in ImportersById.Keys) {
                importerIds.Add(importerId);
            }

            return SortImporterIds(importerIds);
        }

        /// <summary>
        /// Registers one text importer, its content processor and every extension it claims.
        /// </summary>
        /// <param name="registration">Importer registration data.</param>
        public void Register(TextImporterRegistration registration) {
            if (registration == null) {
                throw new ArgumentNullException(nameof(registration));
            }

            EnsureImporterIdIsUnregistered(registration.ImporterId);
            EnsureImporterIdIsFree(registration.ImporterId, RegistrationConflictHandlers);

            ImportersById.Add(registration.ImporterId, registration.Importer);
            Owner.AssetContentManager.RegisterProcessor(
                registration.ImporterId,
                new TextImporterContentProcessor(registration.Importer),
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
        /// Makes one registered text importer the default for a specific extension.
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
            DefaultImporterIdsByExtension[normalized] = importerId;
        }

        /// <summary>
        /// Ensures a text importer is registered.
        /// </summary>
        /// <param name="importerId">Identifier to verify.</param>
        public void EnsureImporterExists(string importerId) {
            if (!ImportersById.ContainsKey(importerId)) {
                throw new InvalidOperationException($"Text importer '{importerId}' is not registered.");
            }
        }

        /// <summary>
        /// Checks whether a text importer is registered.
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
        /// Retrieves a text importer by identifier.
        /// </summary>
        /// <param name="importerId">Identifier of the importer.</param>
        /// <returns>Importer implementation.</returns>
        public ITextImporter GetImporter(string importerId) {
            ITextImporter importer;
            if (ImportersById.TryGetValue(importerId, out importer)) {
                return importer;
            }

            throw new InvalidOperationException($"Text importer '{importerId}' is not registered.");
        }

        /// <summary>
        /// Loads a text asset for a source file, importing it when the cache is missing or unreadable.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the text source file.</param>
        /// <param name="asset">Loaded text asset when available.</param>
        /// <returns>True when the source can be resolved to a text asset.</returns>
        public bool TryLoadAsset(string sourcePath, out TextAsset asset) {
            sourcePath = Owner.NormalizeAndValidateAuthoredSourcePath(sourcePath);

            if (!File.Exists(sourcePath)) {
                throw new FileNotFoundException("Text source file was not found.", sourcePath);
            }

            ModelAssetImportSettings settings;
            if (!Owner.TryLoadOrCreateModelImportSettings(sourcePath, out settings)) {
                asset = null;
                return false;
            }

            if (!IsImporterRegistered(settings.Importer.ImporterId)) {
                asset = null;
                return false;
            }

            string outputPath = Owner.GetTextAssetPath(settings.Importer.AssetId);
            if (!File.Exists(outputPath)) {
                asset = Owner.ImportText(sourcePath);
                return true;
            }

            if (TryLoadCachedAsset(outputPath, out asset)) {
                return true;
            }

            asset = Owner.ImportText(sourcePath);
            return true;
        }

        /// <summary>
        /// Attempts to load a cached text asset.
        /// </summary>
        /// <param name="outputPath">Absolute path to the cached text asset.</param>
        /// <param name="asset">Loaded text asset when the cache file exists and contains the expected payload type.</param>
        /// <returns>True when the cached asset was loaded successfully.</returns>
        public bool TryLoadCachedAsset(string outputPath, out TextAsset asset) {
            if (string.IsNullOrWhiteSpace(outputPath)) {
                throw new ArgumentException("Output path must be provided.", nameof(outputPath));
            }

            asset = null;
            Asset cachedAsset;
            if (!Owner.TryLoadCachedAsset(outputPath, "TextAsset", out cachedAsset)) {
                return false;
            }

            if (cachedAsset is TextAsset textAsset) {
                asset = textAsset;
                return true;
            }

            throw new InvalidOperationException($"Text cache file '{outputPath}' did not contain a TextAsset payload.");
        }
    }
}
