namespace helengine.editor.tests.testing {
    /// <summary>
    /// Provides a component whose nested value intentionally exercises the unsupported append-marker combination.
    /// </summary>
    public sealed class TestNestedAppendSerializableComponent : Component {
        /// <summary>
        /// Gets or sets the nested value that contains an append-marked member.
        /// </summary>
        public TestNestedAppendSerializableValue Value { get; set; }
    }
}
