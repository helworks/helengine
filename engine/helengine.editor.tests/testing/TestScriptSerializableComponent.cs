namespace helengine.editor.tests.testing {
    /// <summary>
    /// Deterministic scripted component used to verify the automatic reflected editor persistence fallback.
    /// </summary>
    public sealed class TestScriptSerializableComponent : Component {
        /// <summary>
        /// Gets or sets the display name stored by the scripted component.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the scripted component should appear visible.
        /// </summary>
        public bool Visible { get; set; }

        /// <summary>
        /// Gets or sets the deterministic sort order persisted for the component.
        /// </summary>
        public int SortOrder { get; set; }
    }
}
