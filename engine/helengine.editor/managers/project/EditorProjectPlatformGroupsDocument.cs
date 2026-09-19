namespace helengine.editor {
    /// <summary>
    /// Stores the platform group tree and the project's default override level order, persisted in <c>settings/platform-groups.json</c>.
    /// </summary>
    public sealed class EditorProjectPlatformGroupsDocument {
        /// <summary>Gets or sets the root groups.</summary>
        public List<EditorProjectPlatformGroupDefinition> Groups { get; set; } = [];

        /// <summary>Gets or sets the level order new entities take beneath Common.</summary>
        public List<SceneOverrideScopeStepKind> DefaultLevelOrder { get; set; } = [];
    }
}
