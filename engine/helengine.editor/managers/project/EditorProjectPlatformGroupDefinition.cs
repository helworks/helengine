namespace helengine.editor {
    /// <summary>
    /// One platform group: its member platforms and its nested child groups.
    /// </summary>
    public sealed class EditorProjectPlatformGroupDefinition {
        /// <summary>Gets or sets the stable group id, unique across the tree and distinct from every platform id.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Gets or sets the label shown in the editor; falls back to <see cref="Id"/> when blank.</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Gets or sets the platforms that belong directly to this group.</summary>
        public List<string> PlatformIds { get; set; } = [];

        /// <summary>Gets or sets the nested groups.</summary>
        public List<EditorProjectPlatformGroupDefinition> Children { get; set; } = [];
    }
}
