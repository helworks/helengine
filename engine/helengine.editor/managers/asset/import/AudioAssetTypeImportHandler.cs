namespace helengine.editor {
    /// <summary>
    /// Owns the audio side of the editor import pipeline: registered audio importers, the extensions they claim, and the
    /// default audio importer selected per extension.
    /// </summary>
    sealed class AudioAssetTypeImportHandler : EditorAssetTypeImportHandler {
        /// <summary>
        /// Registered audio importers keyed by identifier.
        /// </summary>
        public readonly Dictionary<string, IAudioImporter> ImportersById = new Dictionary<string, IAudioImporter>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Audio importer identifiers keyed by normalized extension, preserving registration order so overlapping importers stay selectable.
        /// </summary>
        public readonly Dictionary<string, List<string>> ImporterIdsByExtension = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates the audio handler for one asset import manager.
        /// </summary>
        /// <param name="owner">Manager that hosts this handler.</param>
        public AudioAssetTypeImportHandler(AssetImportManager owner) : base(owner) {
        }

        /// <summary>
        /// Gets the audio asset family identifier.
        /// </summary>
        public override EditorAssetImportKind Kind => EditorAssetImportKind.Audio;

        /// <summary>
        /// Gets the capitalized family name used when reporting a duplicate audio importer registration.
        /// </summary>
        public override string ImporterKindLabel => "Audio";

        /// <summary>
        /// Gets the lowercase family name used when reporting that an importer id already belongs to audio assets.
        /// </summary>
        public override string AssetKindLabel => "audio";

        /// <summary>
        /// Gets the article-qualified phrase used when reporting that an extension already maps to an audio importer.
        /// </summary>
        public override string ExtensionMappingLabel => "an audio importer";

        /// <summary>
        /// Checks whether one importer identifier is registered as an audio importer.
        /// </summary>
        /// <param name="importerId">Importer identifier to look for.</param>
        /// <returns>True when an audio importer owns the identifier.</returns>
        public override bool ContainsImporterId(string importerId) {
            return ImportersById.ContainsKey(importerId);
        }

        /// <summary>
        /// Gets the identifiers of every registered audio importer.
        /// </summary>
        /// <returns>Case-insensitively sorted audio importer identifiers.</returns>
        public override IReadOnlyList<string> GetImporterIds() {
            List<string> importerIds = new List<string>(ImportersById.Count);
            foreach (string importerId in ImportersById.Keys) {
                importerIds.Add(importerId);
            }

            return SortImporterIds(importerIds);
        }

        /// <summary>
        /// Checks whether one normalized extension is claimed by any registered audio importer.
        /// </summary>
        /// <param name="normalizedExtension">Extension already normalized to a lowercase dotted form.</param>
        /// <returns>True when an audio importer claims the extension.</returns>
        public override bool IsNormalizedExtensionSupported(string normalizedExtension) {
            return ImporterIdsByExtension.ContainsKey(normalizedExtension);
        }

        /// <summary>
        /// Returns the audio importer identifiers registered for one normalized extension.
        /// </summary>
        /// <param name="normalizedExtension">Extension already normalized to a lowercase dotted form.</param>
        /// <returns>Copy of the applicable audio importer identifiers, or null when no audio importer claims the extension.</returns>
        public override IReadOnlyList<string> TryGetImporterIdsForExtension(string normalizedExtension) {
            List<string> importerIds;
            if (!ImporterIdsByExtension.TryGetValue(normalizedExtension, out importerIds)) {
                return null;
            }

            return new List<string>(importerIds);
        }

        /// <summary>
        /// Registers one audio importer and every extension it claims. Audio importers are decoded through the editor audio
        /// pipeline rather than a content processor, so no processor is registered here.
        /// </summary>
        /// <param name="registration">Importer registration data.</param>
        public void Register(AudioImporterRegistration registration) {
            if (registration == null) {
                throw new ArgumentNullException(nameof(registration));
            }

            EnsureImporterIdIsUnregistered(registration.ImporterId);
            EnsureImporterIdIsFree(registration.ImporterId, RegistrationConflictHandlers);

            ImportersById.Add(registration.ImporterId, registration.Importer);
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
        /// Makes one registered audio importer the default for a specific extension.
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
        /// Ensures an audio importer is registered.
        /// </summary>
        /// <param name="importerId">Identifier to verify.</param>
        public void EnsureImporterExists(string importerId) {
            if (!ImportersById.ContainsKey(importerId)) {
                throw new InvalidOperationException($"Audio importer '{importerId}' is not registered.");
            }
        }

        /// <summary>
        /// Checks whether an audio importer is registered.
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
        /// Retrieves an audio importer by identifier.
        /// </summary>
        /// <param name="importerId">Identifier of the importer.</param>
        /// <returns>Importer implementation.</returns>
        public IAudioImporter GetImporter(string importerId) {
            IAudioImporter importer;
            if (ImportersById.TryGetValue(importerId, out importer)) {
                return importer;
            }

            throw new InvalidOperationException($"Audio importer '{importerId}' is not registered.");
        }

        /// <summary>
        /// Records that one audio importer supports one file extension.
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
        /// Ensures the supplied audio importer has been registered for the requested file extension.
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
        /// Loads an audio asset for a source file, importing it when the cache is missing or unreadable.
        /// </summary>
        /// <param name="sourcePath">Absolute path to the audio source file.</param>
        /// <param name="asset">Loaded audio asset when available.</param>
        /// <returns>True when the source can be resolved to an audio asset.</returns>
        public bool TryLoadAsset(string sourcePath, out AudioAsset asset) {
            sourcePath = Owner.NormalizeAndValidateAuthoredSourcePath(sourcePath);

            if (!File.Exists(sourcePath)) {
                throw new FileNotFoundException("Audio source file was not found.", sourcePath);
            }

            AudioAssetImportSettings settings;
            if (!Owner.TryLoadOrCreateAudioImportSettings(sourcePath, out settings)) {
                asset = null;
                return false;
            }

            if (!IsImporterRegistered(settings.Importer.ImporterId)) {
                asset = null;
                return false;
            }

            string outputPath = Owner.GetAudioAssetPath(settings.Importer.AssetId);
            if (!File.Exists(outputPath)) {
                asset = Owner.ImportAudio(sourcePath);
                return true;
            }

            if (TryLoadCachedAsset(outputPath, out asset)) {
                return true;
            }

            asset = Owner.ImportAudio(sourcePath);
            return true;
        }

        /// <summary>
        /// Attempts to load a cached audio asset.
        /// </summary>
        /// <param name="outputPath">Absolute path to the cached audio asset.</param>
        /// <param name="asset">Loaded audio asset when the cache file exists and contains the expected payload type.</param>
        /// <returns>True when the cached asset was loaded successfully.</returns>
        public bool TryLoadCachedAsset(string outputPath, out AudioAsset asset) {
            if (string.IsNullOrWhiteSpace(outputPath)) {
                throw new ArgumentException("Output path must be provided.", nameof(outputPath));
            }

            asset = null;
            Asset cachedAsset;
            if (!Owner.TryLoadCachedAsset(outputPath, "AudioAsset", out cachedAsset)) {
                return false;
            }

            if (cachedAsset is AudioAsset audioAsset) {
                asset = audioAsset;
                return true;
            }

            throw new InvalidOperationException($"Audio cache file '{outputPath}' did not contain an AudioAsset payload.");
        }
    }
}
