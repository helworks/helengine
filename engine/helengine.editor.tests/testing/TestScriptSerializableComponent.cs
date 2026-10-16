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

    /// <summary>
    /// Custom component fixture proving that a user-authored member may retain the legacy built-in field's name.
    /// </summary>
    public sealed class TestCustomRenderOrder2DSerializableComponent : Component {
        /// <summary>
        /// Gets or sets a user-defined value whose name happens to match the removed built-in drawable setting.
        /// </summary>
        public byte RenderOrder2D { get; set; }
    }
}
