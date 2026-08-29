namespace helengine.editor {
    /// <summary>
    /// Prepared common and platform material-settings outputs for one atomic authoring write.
    /// </summary>
    internal sealed class EditorPreparedMaterialWrite {
        public EditorPreparedAssetWrite Common { get; init; }

        public IReadOnlyList<EditorPreparedAssetWrite> Overrides { get; init; }
    }
}
