namespace helengine.editor.tests {
    /// <summary>
    /// Minimal component used to exercise the property controller without a renderer or editor session.
    /// </summary>
    public sealed class TestInspectorComponent : Component {
        /// <summary>
        /// Gets or sets the scalar value edited by the test.
        /// </summary>
        public int Value { get; set; } = 1;
    }
}