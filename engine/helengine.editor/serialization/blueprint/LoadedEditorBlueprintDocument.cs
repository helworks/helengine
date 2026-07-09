namespace helengine.editor {
    /// <summary>
    /// Represents one loaded editor blueprint document, including the live editable root entity.
    /// </summary>
    public class LoadedEditorBlueprintDocument {
        /// <summary>
        /// Gets or sets the live root entity materialized from the blueprint file.
        /// </summary>
        public EditorEntity RootEntity { get; set; }
    }
}
