namespace helengine.editor {
    /// <summary>
    /// Stores source importer settings that are shared across every target platform.
    /// </summary>
    public class AssetImporterSettings {
        /// <summary>
        /// Gets or sets the importer identifier used to read the source asset. This is the only importer field that
        /// source import-settings sidecars (texture, model, audio, font and text) persist.
        /// </summary>
        public string ImporterId { get; set; }

        /// <summary>
        /// Gets or sets the checksum of the current source bytes. Source import-settings sidecars never persist it: the
        /// import manager recomputes it from the source every time the settings are loaded, and it only feeds the
        /// processed-asset cache key. Material settings documents carry it as an empty value.
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
