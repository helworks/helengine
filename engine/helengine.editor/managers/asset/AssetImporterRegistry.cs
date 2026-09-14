namespace helengine.editor {
    /// <summary>
    /// Owns the bootstrap boundary for typed asset importer registrations.
    /// </summary>
    public sealed class AssetImporterRegistry {
        /// <summary>
        /// Manager that owns the existing importer maps and content processors during this migration boundary.
        /// </summary>
        readonly AssetImportManager AssetImportManager;

        /// <summary>
        /// Indicates whether host registration has been sealed for the current session.
        /// </summary>
        public bool IsFrozen { get; private set; }

        /// <summary>
        /// Creates a registry adapter for one asset import manager.
        /// </summary>
        /// <param name="assetImportManager">Manager receiving the typed registration state.</param>
        public AssetImporterRegistry(AssetImportManager assetImportManager) {
            AssetImportManager = assetImportManager ?? throw new ArgumentNullException(nameof(assetImportManager));
        }

        /// <summary>
        /// Seals host registration so importer selection cannot change during an active session.
        /// </summary>
        public void Freeze() {
            IsFrozen = true;
        }

        /// <summary>
        /// Registers a texture importer before the registry is frozen.
        /// </summary>
        /// <param name="registration">Texture importer registration.</param>
        public void RegisterTextureImporter(TextureImporterRegistration registration) {
            EnsureMutable();
            AssetImportManager.RegisterTextureImporter(registration);
        }

        /// <summary>
        /// Registers a text importer before the registry is frozen.
        /// </summary>
        /// <param name="registration">Text importer registration.</param>
        public void RegisterTextImporter(TextImporterRegistration registration) {
            EnsureMutable();
            AssetImportManager.RegisterTextImporter(registration);
        }

        /// <summary>
        /// Registers a font importer before the registry is frozen.
        /// </summary>
        /// <param name="registration">Font importer registration.</param>
        public void RegisterFontImporter(FontImporterRegistration registration) {
            EnsureMutable();
            AssetImportManager.RegisterFontImporter(registration);
        }

        /// <summary>
        /// Registers an audio importer before the registry is frozen.
        /// </summary>
        /// <param name="registration">Audio importer registration.</param>
        public void RegisterAudioImporter(AudioImporterRegistration registration) {
            EnsureMutable();
            AssetImportManager.RegisterAudioImporter(registration);
        }

        /// <summary>
        /// Registers a model importer before the registry is frozen.
        /// </summary>
        /// <param name="registration">Model importer registration.</param>
        public void RegisterModelImporter(ModelImporterRegistration registration) {
            EnsureMutable();
            AssetImportManager.RegisterModelImporter(registration);
        }

        /// <summary>
        /// Gets the registered texture importer identifiers.
        /// </summary>
        /// <returns>Sorted texture importer identifiers.</returns>
        public IReadOnlyList<string> GetTextureImporterIds() {
            return AssetImportManager.GetTextureImporterIds();
        }

        /// <summary>
        /// Gets the registered text importer identifiers.
        /// </summary>
        /// <returns>Sorted text importer identifiers.</returns>
        public IReadOnlyList<string> GetTextImporterIds() {
            return AssetImportManager.GetTextImporterIds();
        }

        /// <summary>
        /// Gets the registered font importer identifiers.
        /// </summary>
        /// <returns>Sorted font importer identifiers.</returns>
        public IReadOnlyList<string> GetFontImporterIds() {
            return AssetImportManager.GetFontImporterIds();
        }

        /// <summary>
        /// Gets the registered audio importer identifiers.
        /// </summary>
        /// <returns>Sorted audio importer identifiers.</returns>
        public IReadOnlyList<string> GetAudioImporterIds() {
            return AssetImportManager.GetAudioImporterIds();
        }

        /// <summary>
        /// Gets the registered model importer identifiers.
        /// </summary>
        /// <returns>Sorted model importer identifiers.</returns>
        public IReadOnlyList<string> GetModelImporterIds() {
            return AssetImportManager.GetModelImporterIds();
        }

        /// <summary>
        /// Gets importer identifiers applicable to an extension using existing precedence rules.
        /// </summary>
        /// <param name="extension">Source extension.</param>
        /// <returns>Applicable importer identifiers.</returns>
        public IReadOnlyList<string> GetImporterIdsForExtension(string extension) {
            return AssetImportManager.GetImporterIdsForExtension(extension);
        }

        /// <summary>
        /// Selects the default texture importer for an extension.
        /// </summary>
        /// <param name="extension">Source extension.</param>
        /// <param name="importerId">Importer identifier.</param>
        public void SetDefaultTextureImporter(string extension, string importerId) {
            EnsureMutable();
            AssetImportManager.SetDefaultTextureImporter(extension, importerId);
        }

        /// <summary>
        /// Selects the default text importer for an extension.
        /// </summary>
        /// <param name="extension">Source extension.</param>
        /// <param name="importerId">Importer identifier.</param>
        public void SetDefaultTextImporter(string extension, string importerId) {
            EnsureMutable();
            AssetImportManager.SetDefaultTextImporter(extension, importerId);
        }

        /// <summary>
        /// Selects the default font importer for an extension.
        /// </summary>
        /// <param name="extension">Source extension.</param>
        /// <param name="importerId">Importer identifier.</param>
        public void SetDefaultFontImporter(string extension, string importerId) {
            EnsureMutable();
            AssetImportManager.SetDefaultFontImporter(extension, importerId);
        }

        /// <summary>
        /// Selects the default audio importer for an extension.
        /// </summary>
        /// <param name="extension">Source extension.</param>
        /// <param name="importerId">Importer identifier.</param>
        public void SetDefaultAudioImporter(string extension, string importerId) {
            EnsureMutable();
            AssetImportManager.SetDefaultAudioImporter(extension, importerId);
        }

        /// <summary>
        /// Selects the default model importer for an extension.
        /// </summary>
        /// <param name="extension">Source extension.</param>
        /// <param name="importerId">Importer identifier.</param>
        public void SetDefaultModelImporter(string extension, string importerId) {
            EnsureMutable();
            AssetImportManager.SetDefaultModelImporter(extension, importerId);
        }

        /// <summary>
        /// Rejects mutation after the host bootstrap boundary has closed.
        /// </summary>
        void EnsureMutable() {
            if (IsFrozen) {
                throw new InvalidOperationException("Asset importer registration is frozen.");
            }
        }
    }
}