namespace helengine.editor {
    /// <summary>Stores one user-authored complete runtime preset without inheritance or parent references.</summary>
    public sealed class EditorRuntimePresetDocument {
        /// <summary>Gets or sets the stable preset identifier exposed to gameplay code.</summary>
        public string Id { get; set; } = string.Empty;
        /// <summary>Gets or sets the user's freely chosen preset name.</summary>
        public string Name { get; set; } = string.Empty;
        /// <summary>Gets or sets every explicitly authored value, encoded as invariant editor text.</summary>
        public Dictionary<string, string> Values { get; set; } = [];
    }
}
