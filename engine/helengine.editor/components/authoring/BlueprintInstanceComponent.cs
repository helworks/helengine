namespace helengine.editor {
    /// <summary>
    /// Marks one scene-owned entity as a blueprint instance root and stores the referenced blueprint asset path.
    /// </summary>
    public sealed class BlueprintInstanceComponent : Component {
        /// <summary>
        /// Gets or sets the project-relative blueprint asset path referenced by this instance root.
        /// </summary>
        public string BlueprintAssetPath { get; set; } = string.Empty;
    }
}
