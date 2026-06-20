namespace helengine.editor.tests.testing {
    /// <summary>
    /// Scripted component that exposes one string-keyed dictionary for automatic persistence coverage.
    /// </summary>
    public sealed class TestDictionaryScriptComponent : Component {
        /// <summary>
        /// Initializes the dictionary-backed scripted component with one deterministic empty dictionary.
        /// </summary>
        public TestDictionaryScriptComponent() {
            Labels = new Dictionary<string, string>();
        }

        /// <summary>
        /// Gets or sets the authored label dictionary used by automatic persistence tests.
        /// </summary>
        public Dictionary<string, string> Labels { get; set; }
    }
}
