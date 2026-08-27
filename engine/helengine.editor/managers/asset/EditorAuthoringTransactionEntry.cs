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

        public string StagedContentHash { get; set; }

        public string BackupRelativePath { get; set; }

        public EditorAuthoringTransactionState State { get; set; }

        public bool Changed { get; set; }
    }

    /// <summary>
    /// Durable state of an authoring transaction journal.
    /// </summary>
    public enum EditorAuthoringTransactionState {
        Staging,
        Committing,
        Committed
    }
}
