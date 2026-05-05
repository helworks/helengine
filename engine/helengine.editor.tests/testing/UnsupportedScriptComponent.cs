namespace helengine.editor.tests.testing {
    /// <summary>
    /// Scripted component with an unsupported reflected member shape used to verify clear fallback failures.
    /// </summary>
    public sealed class UnsupportedScriptComponent : Component {
        /// <summary>
        /// Gets or sets one entity reference that automatic reflection persistence does not support.
        /// </summary>
        public Entity LinkedEntity { get; set; }
    }
}
