namespace helengine.editor {
    /// <summary>
    /// Owns everything one asset family needs from the editor import pipeline: its registered importers, the extensions those
    /// importers claim, the default importer chosen per extension, and the conflict messages other families raise when they
    /// collide with it. <see cref="AssetImportManager"/> keeps one handler per <see cref="EditorAssetImportKind"/> and
    /// dispatches to them instead of hand-duplicating a method family per asset type.
    /// </summary>
    abstract class EditorAssetTypeImportHandler {
        /// <summary>
        /// Manager that owns the shared import plumbing (path resolution, sidecar settings, cache reads) this handler calls back into.
        /// </summary>
        protected readonly AssetImportManager Owner;

        /// <summary>
        /// Default importer identifiers keyed by normalized extension for this asset family.
        /// </summary>
        public readonly Dictionary<string, string> DefaultImporterIdsByExtension = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates one asset-type handler bound to the manager that owns the shared import plumbing.
        /// </summary>
        /// <param name="owner">Manager that hosts this handler.</param>
        protected EditorAssetTypeImportHandler(AssetImportManager owner) {
            if (owner == null) {
                throw new ArgumentNullException(nameof(owner));
            }

            Owner = owner;
        }

        /// <summary>
        /// Gets the foreign handlers consulted, in the exact reporting order, when this family registers a new importer.
        /// </summary>
        public EditorAssetTypeImportHandler[] RegistrationConflictHandlers { get; private set; }

        /// <summary>
        /// Gets the foreign handlers consulted, in the exact reporting order, when this family is asked to make one importer the default for an extension.
        /// Families that expose no default-importer selection bind an empty order.
        /// </summary>
        public EditorAssetTypeImportHandler[] DefaultSelectionConflictHandlers { get; private set; }

        /// <summary>
        /// Gets the asset family this handler owns.
        /// </summary>
        public abstract EditorAssetImportKind Kind { get; }

        /// <summary>
        /// Gets the capitalized family name used when reporting a duplicate importer registration, for example "Texture".
        /// </summary>
        public abstract string ImporterKindLabel { get; }

        /// <summary>
        /// Gets the lowercase family name used when reporting that an importer id already belongs to this family, for example "texture".
        /// </summary>
        public abstract string AssetKindLabel { get; }

        /// <summary>
        /// Gets the article-qualified phrase used when reporting that an extension already maps to this family, for example "a texture importer".
        /// </summary>
        public abstract string ExtensionMappingLabel { get; }

        /// <summary>
        /// Records which foreign families this handler must not collide with, and in which order their collisions are reported.
        /// The two orders differ per asset family and are preserved exactly as the manager reported them before handlers existed.
        /// </summary>
        /// <param name="registrationConflictHandlers">Handlers consulted while registering an importer.</param>
        /// <param name="defaultSelectionConflictHandlers">Handlers consulted while selecting a default importer for an extension.</param>
        public void BindConflictOrder(
            EditorAssetTypeImportHandler[] registrationConflictHandlers,
            EditorAssetTypeImportHandler[] defaultSelectionConflictHandlers) {
            if (registrationConflictHandlers == null) {
                throw new ArgumentNullException(nameof(registrationConflictHandlers));
            }

            if (defaultSelectionConflictHandlers == null) {
                throw new ArgumentNullException(nameof(defaultSelectionConflictHandlers));
            }

            RegistrationConflictHandlers = registrationConflictHandlers;
            DefaultSelectionConflictHandlers = defaultSelectionConflictHandlers;
        }

        /// <summary>
        /// Checks whether one importer identifier is registered for this asset family.
        /// </summary>
        /// <param name="importerId">Importer identifier to look for.</param>
        /// <returns>True when this family already owns the identifier.</returns>
        public abstract bool ContainsImporterId(string importerId);

        /// <summary>
        /// Gets the identifiers of every importer registered for this asset family.
        /// </summary>
        /// <returns>Case-insensitively sorted importer identifiers.</returns>
        public abstract IReadOnlyList<string> GetImporterIds();

        /// <summary>
        /// Checks whether one normalized extension is claimed by this asset family.
        /// </summary>
        /// <param name="normalizedExtension">Extension already normalized to a lowercase dotted form.</param>
        /// <returns>True when the extension belongs to this family.</returns>
        public virtual bool IsNormalizedExtensionSupported(string normalizedExtension) {
            return DefaultImporterIdsByExtension.ContainsKey(normalizedExtension);
        }

        /// <summary>
        /// Returns the importer identifiers this family offers for one normalized extension.
        /// </summary>
        /// <param name="normalizedExtension">Extension already normalized to a lowercase dotted form.</param>
        /// <returns>Applicable importer identifiers, or null when this family does not claim the extension.</returns>
        public virtual IReadOnlyList<string> TryGetImporterIdsForExtension(string normalizedExtension) {
            if (!DefaultImporterIdsByExtension.ContainsKey(normalizedExtension)) {
                return null;
            }

            return GetImporterIds();
        }

        /// <summary>
        /// Rejects an importer identifier that is already registered inside this same asset family.
        /// </summary>
        /// <param name="importerId">Importer identifier being registered.</param>
        public void EnsureImporterIdIsUnregistered(string importerId) {
            if (ContainsImporterId(importerId)) {
                throw new InvalidOperationException($"{ImporterKindLabel} importer '{importerId}' is already registered.");
            }
        }

        /// <summary>
        /// Rejects an importer identifier that another asset family already claimed.
        /// </summary>
        /// <param name="importerId">Importer identifier being registered by a different asset family.</param>
        public void EnsureImporterIdIsNotClaimed(string importerId) {
            if (ContainsImporterId(importerId)) {
                throw new InvalidOperationException($"Importer id '{importerId}' is already registered for {AssetKindLabel} assets.");
            }
        }

        /// <summary>
        /// Rejects an extension that another asset family already mapped to a default importer.
        /// </summary>
        /// <param name="normalizedExtension">Extension already normalized to a lowercase dotted form.</param>
        public void EnsureExtensionIsNotClaimed(string normalizedExtension) {
            if (DefaultImporterIdsByExtension.ContainsKey(normalizedExtension)) {
                throw new InvalidOperationException($"Extension '{normalizedExtension}' is already mapped to {ExtensionMappingLabel}.");
            }
        }

        /// <summary>
        /// Rejects an extension claimed by any of the supplied foreign asset families, in the order they are listed.
        /// </summary>
        /// <param name="normalizedExtension">Extension already normalized to a lowercase dotted form.</param>
        /// <param name="foreignHandlers">Handlers to consult, in the exact order their conflicts should be reported.</param>
        public static void EnsureExtensionIsFree(string normalizedExtension, EditorAssetTypeImportHandler[] foreignHandlers) {
            for (int index = 0; index < foreignHandlers.Length; index++) {
                foreignHandlers[index].EnsureExtensionIsNotClaimed(normalizedExtension);
            }
        }

        /// <summary>
        /// Rejects an importer identifier claimed by any of the supplied foreign asset families, in the order they are listed.
        /// </summary>
        /// <param name="importerId">Importer identifier being registered.</param>
        /// <param name="foreignHandlers">Handlers to consult, in the exact order their conflicts should be reported.</param>
        public static void EnsureImporterIdIsFree(string importerId, EditorAssetTypeImportHandler[] foreignHandlers) {
            for (int index = 0; index < foreignHandlers.Length; index++) {
                foreignHandlers[index].EnsureImporterIdIsNotClaimed(importerId);
            }
        }

        /// <summary>
        /// Sorts one importer identifier list the way every public importer-id accessor reports it.
        /// </summary>
        /// <param name="importerIds">Identifier list to sort in place.</param>
        /// <returns>The same list, sorted case-insensitively.</returns>
        protected static IReadOnlyList<string> SortImporterIds(List<string> importerIds) {
            importerIds.Sort(StringComparer.OrdinalIgnoreCase);
            return importerIds;
        }
    }
}
