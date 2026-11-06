namespace helengine.editor {
    /// <summary>
    /// Stores source importer settings that are shared across every target platform.
    /// </summary>
    public class AssetImporterSettings {
        /// <summary>
        /// Initializes importer settings with an empty generic data block.
        /// </summary>
        public AssetImporterSettings() {
            SidecarData = new SidecarData();
        }

        /// <summary>
        /// Gets or sets the generic keyed data persisted at the end of source import-settings sidecars (texture, model, audio, font
        /// and text). Owners add data as new keys through <see cref="SidecarDataEntryRegistry"/> instead of bumping the sidecar
        /// version; keys without a registered owner are kept and written back unchanged. Material settings documents do not persist it.
        /// </summary>
        public SidecarData SidecarData { get; set; }

        /// <summary>
        /// Gets or sets the importer identifier used to read the source asset. Source import-settings sidecars
        /// (texture, model, audio, font and text) persist it together with <see cref="SidecarData"/>.
        /// </summary>
        public string ImporterId { get; set; }

        /// <summary>
        /// Gets or sets the checksum of the current source bytes. This property is in-memory only: the import manager
        /// recomputes it from the source every time the settings are loaded, and it feeds the processed-asset cache key.
        /// The checksum a scene-mode <c>.generated</c> folder was generated from is a different value: it is persisted in
        /// the <see cref="GeneratedSceneImportStamp"/> entry of <see cref="SidecarData"/> and compared with this one to
        /// detect a stale folder. Material settings documents carry this property as an empty value.
        /// </summary>
        public string SourceChecksum { get; set; }

        /// <summary>
        /// Gets or sets the processed asset identifier: the key of the processed import cache entry, and the id given
        /// to the processed asset. For source import settings it is derived from the source checksum, the importer,
        /// the platform and environment being processed and their processor settings, so it is computed in memory
        /// on every load and never persisted; it is not the asset's identity (that is the <c>.hmeta</c> asset id).
        /// Material settings documents persist the processed material id here, because a material has no source
        /// bytes to derive it from.
        /// </summary>
        public string ProcessedAssetId { get; set; }
    }
}
