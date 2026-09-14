namespace helengine.editor {
    /// <summary>
    /// Owns the model side of the editor import pipeline: registered model importers and the default model importer selected per extension.
    /// </summary>
    sealed class ModelAssetTypeImportHandler : EditorAssetTypeImportHandler {
        /// <summary>
        /// Registered model importers keyed by identifier.
        /// </summary>
        public readonly Dictionary<string, IModelImporter> ImportersById = new Dictionary<string, IModelImporter>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates the model handler for one asset import manager.
        /// </summary>
        /// <param name="owner">Manager that hosts this handler.</param>
        public ModelAssetTypeImportHandler(AssetImportManager owner) : base(owner) {
        }

        /// <summary>
        /// Gets the model asset family identifier.
        /// </summary>
        public override EditorAssetImportKind Kind => EditorAssetImportKind.Model;

        /// <summary>
        /// Gets the capitalized family name used when reporting a duplicate model importer registration.
        /// </summary>
        public override string ImporterKindLabel => "Model";

        /// <summary>
        /// Gets the lowercase family name used when reporting that an importer id already belongs to model assets.
        /// </summary>
        public override string AssetKindLabel => "model";

        /// <summary>
        /// Gets the article-qualified phrase used when reporting that an extension already maps to a model importer.
        /// </summary>
        public override string ExtensionMappingLabel => "a model importer";

        /// <summary>
        /// Checks whether one importer identifier is registered as a model importer.
        /// </summary>
        /// <param name="importerId">Importer identifier to look for.</param>
        /// <returns>True when a model importer owns the identifier.</returns>
        public override bool ContainsImporterId(string importerId) {
            return ImportersById.ContainsKey(importerId);
        }

        /// <summary>
        /// Gets the identifiers of every registered model importer.
        /// </summary>
        /// <returns>Case-insensitively sorted model importer identifiers.</returns>
        public override IReadOnlyList<string> GetImporterIds() {
            List<string> importerIds = new List<string>(ImportersById.Count);
            foreach (string importerId in ImportersById.Keys) {
                importerIds.Add(importerId);
            }

            return SortImporterIds(importerIds);
        }

        /// <summary>
        /// Registers one model importer, its content processor and every extension it claims.
        /// </summary>
        /// <param name="registration">Importer registration data.</param>
        public void Register(ModelImporterRegistration registration) {
            if (registration == null) {
                throw new ArgumentNullException(nameof(registration));
            }

            EnsureImporterIdIsUnregistered(registration.ImporterId);
            EnsureImporterIdIsFree(registration.ImporterId, RegistrationConflictHandlers);

            ImportersById.Add(registration.ImporterId, registration.Importer);
            Owner.AssetContentManager.RegisterProcessor(
                registration.ImporterId,
                new ModelImporterContentProcessor(registration.Importer),
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
        /// Makes one registered model importer the default for a specific extension.
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
        /// Ensures a model importer is registered.
        /// </summary>
        /// <param name="importerId">Identifier to verify.</param>
        public void EnsureImporterExists(string importerId) {
            if (!ImportersById.ContainsKey(importerId)) {
                throw new InvalidOperationException($"Model importer '{importerId}' is not registered.");
            }
        }

        /// <summary>
        /// Checks whether a model importer is registered.
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
        /// Retrieves a model importer by identifier.
        /// </summary>
        /// <param name="importerId">Identifier of the importer.</param>
        /// <returns>Importer implementation.</returns>
        public IModelImporter GetImporter(string importerId) {
            IModelImporter importer;
            if (ImportersById.TryGetValue(importerId, out importer)) {
                return importer;
            }

            throw new InvalidOperationException($"Model importer '{importerId}' is not registered.");
        }

        /// <summary>
        /// Loads a model asset for a source file, importing it when the cache is missing or unreadable and reporting
        /// any failure with the source and cache provenance callers need.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the model source file.</param>
        /// <param name="asset">Loaded model asset when available.</param>
        /// <returns>True when the source can be resolved to a model asset.</returns>
        public bool TryLoadAsset(string sourcePath, out ModelAsset asset) {
            sourcePath = Owner.NormalizeAndValidateAuthoredSourcePath(sourcePath);

            if (!File.Exists(sourcePath)) {
                throw new FileNotFoundException("Model source file was not found.", sourcePath);
            }

            string outputPath = null;
            try {
                if (string.Equals(Path.GetExtension(sourcePath), AssetImportManager.SettingsExtension, StringComparison.OrdinalIgnoreCase)) {
                    return Owner.TryLoadSerializedModelAsset(sourcePath, out asset);
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

                outputPath = Owner.GetModelAssetPath(settings.Importer.AssetId);
                if (!File.Exists(outputPath)) {
                    asset = Owner.ImportModel(sourcePath);
                    return true;
                }

                if (TryLoadCachedAsset(outputPath, out asset)) {
                    return true;
                }

                asset = Owner.ImportModel(sourcePath);
                return true;
            } catch (Exception exception) {
                throw Owner.CreateModelLoadFailureException(sourcePath, outputPath, exception);
            }
        }

        /// <summary>
        /// Attempts to load a cached model asset, discarding cache files written by an older editor asset payload version.
        /// </summary>
        /// <param name="outputPath">Absolute path to the cached model asset.</param>
        /// <param name="asset">Loaded model asset when the cache file exists and contains the expected payload type.</param>
        /// <returns>True when the cached asset was loaded successfully.</returns>
        public bool TryLoadCachedAsset(string outputPath, out ModelAsset asset) {
            if (string.IsNullOrWhiteSpace(outputPath)) {
                throw new ArgumentException("Output path must be provided.", nameof(outputPath));
            }

            asset = null;
            if (Owner.IsStaleEditorAssetCache(outputPath)) {
                Owner.DeleteCacheFile(outputPath);
                return false;
            }

            Asset cachedAsset;
            if (!Owner.TryLoadCachedAsset(outputPath, "ModelAsset", out cachedAsset)) {
                return false;
            }

            if (cachedAsset is ModelAsset modelAsset) {
                asset = modelAsset;
                return true;
            }

            throw new InvalidOperationException($"Model cache file '{outputPath}' did not contain a ModelAsset payload.");
        }
    }
}
