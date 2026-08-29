namespace helengine.editor {
    /// <summary>
    /// One exact destination and its staged publication data.
    /// </summary>
    internal sealed class EditorAuthoringTransactionEntry {
        public string DestinationRelativePath { get; set; }

        /// <summary>Assets-relative change-log path for an indexed generated source.</summary>
        public string ChangeLogRelativePath { get; set; }

        public bool UsesProjectRoot { get; set; }

        public string StagedRelativePath { get; set; }

        public bool PriorExists { get; set; }

        /// <summary>Persists the prior external-source identity-sidecar state for rollback.</summary>
        public bool PriorIdentityMetadataExists { get; set; }

        public string PriorContentHash { get; set; }

        public string PriorSerializedHash { get; set; }

        public string StagedSerializedHash { get; set; }

        public string BackupContentHash { get; set; }

        public string BackupSerializedHash { get; set; }

        public string ExpectedAssetId { get; set; }

        public string ExpectedAssetKind { get; set; }

        /// <summary>
        /// Identifies the durable payload format. Recovery must validate
        /// identity-bearing native assets differently from material settings
        /// and identity-less generated files.
        /// </summary>
        public EditorAuthoringTransactionPayloadKind PayloadKind { get; set; }

        public string StagedContentHash { get; set; }

        public string BackupRelativePath { get; set; }

        public EditorAuthoringTransactionState State { get; set; }

        public bool Changed { get; set; }

        public EditorAuthoringTransactionEntryProgress Progress { get; set; }

        /// <summary>
        /// Gets whether this entry contains a material-settings payload rather
        /// than an ordinary AssetSerializer payload.
        /// </summary>
        public bool IsMaterialSettingsPayload { get; set; }

        public bool UpdatesIdentityIndex { get; set; }
    }

    /// <summary>
    /// Durable payload formats understood by an authoring transaction.
    /// </summary>
    public enum EditorAuthoringTransactionPayloadKind {
        NativeAsset,
        MaterialCommonSettings,
        MaterialPlatformOverride,
        GeneratedFile
    }

    /// <summary>
    /// Durable state of an authoring transaction journal.
    /// </summary>
    public enum EditorAuthoringTransactionState {
        Staging,
        Committing,
        Committed,
        Aborting,
        RolledBack
    }

    /// <summary>
    /// In-memory terminal outcome of one authoring transaction instance.
    /// </summary>
    public enum EditorAuthoringTransactionOutcome {
        Active,
        Committed,
        Disposed,
        RolledBack,
        Failed
    }

    /// <summary>
    /// Durable replacement progress for one transaction entry.
    /// </summary>
    public enum EditorAuthoringTransactionEntryProgress {
        Staged,
        Applying,
        Applied,
        Skipped
    }
}
