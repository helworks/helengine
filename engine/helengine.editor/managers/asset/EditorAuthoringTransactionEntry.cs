namespace helengine.editor {
    /// <summary>
    /// One exact destination and its staged publication data.
    /// </summary>
    internal sealed class EditorAuthoringTransactionEntry {
        public string DestinationRelativePath { get; set; }

        public string StagedRelativePath { get; set; }

        public bool PriorExists { get; set; }

        public string PriorContentHash { get; set; }

        public string PriorSerializedHash { get; set; }

        public string StagedSerializedHash { get; set; }

        public string BackupContentHash { get; set; }

        public string BackupSerializedHash { get; set; }

        public string ExpectedAssetId { get; set; }

        public string ExpectedAssetKind { get; set; }

        public string StagedContentHash { get; set; }

        public string BackupRelativePath { get; set; }

        public EditorAuthoringTransactionState State { get; set; }

        public bool Changed { get; set; }

        public EditorAuthoringTransactionEntryProgress Progress { get; set; }
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
    /// Durable replacement progress for one transaction entry.
    /// </summary>
    public enum EditorAuthoringTransactionEntryProgress {
        Staged,
        Applying,
        Applied,
        Skipped
    }
}
