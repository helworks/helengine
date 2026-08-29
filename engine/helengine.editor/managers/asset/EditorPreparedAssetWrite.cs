namespace helengine.editor {
    /// <summary>
    /// Canonical native output prepared without publishing it to the assets tree.
    /// </summary>
    internal sealed class EditorPreparedAssetWrite {
        public string RelativePath { get; init; }

        /// <summary>
        /// Assets-relative path used by the project change log, when this is
        /// an externally indexed generated source.
        /// </summary>
        public string ChangeLogRelativePath { get; init; }

        public string FullPath { get; init; }

        public byte[] SerializedBytes { get; init; }

        public string ContentHash { get; init; }

        public string SerializedHash { get; init; }

        public string AssetId { get; init; }

        public string AssetKind { get; init; }

        public EditorAuthoringTransactionPayloadKind PayloadKind { get; init; }

        /// <summary>
        /// Selects the project root instead of the assets root for generated
        /// source, import-settings, and cache files.
        /// </summary>
        public bool UsesProjectRoot { get; init; }

        public bool PriorExists { get; init; }

        /// <summary>
        /// Indicates whether an externally indexed source already had its
        /// identity sidecar before this generated write was staged.
        /// </summary>
        public bool PriorIdentityMetadataExists { get; init; }

        public string PriorContentHash { get; init; }

        public string PriorSerializedHash { get; init; }

        public bool PreservedExistingIdentity { get; init; }

        public bool IsUnchanged { get; init; }

        /// <summary>
        /// Gets whether this prepared payload uses the editor material-settings
        /// container rather than the ordinary AssetSerializer container.
        /// </summary>
        public bool IsMaterialSettingsPayload { get; init; }

        /// <summary>
        /// Gets whether publication should update the session identity index
        /// for this material-settings path. Platform override documents do not
        /// own an identity entry.
        /// </summary>
        public bool UpdatesIdentityIndex { get; init; }
    }
}
