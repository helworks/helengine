namespace helengine.editor {
    /// <summary>
    /// Canonical native output prepared without publishing it to the assets tree.
    /// </summary>
    internal sealed class EditorPreparedAssetWrite {
        public string RelativePath { get; init; }

        public string FullPath { get; init; }

        public byte[] SerializedBytes { get; init; }

        public string ContentHash { get; init; }

        public string AssetId { get; init; }

        public bool PriorExists { get; init; }

        public string PriorContentHash { get; init; }

        public string PriorSerializedHash { get; init; }

        public bool PreservedExistingIdentity { get; init; }

        public bool IsUnchanged { get; init; }
    }
}
