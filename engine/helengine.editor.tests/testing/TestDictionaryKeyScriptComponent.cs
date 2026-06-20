namespace helengine.editor.tests.testing {
    /// <summary>
    /// Scripted component that exposes supported integer and enum keyed dictionaries for automatic persistence coverage.
    /// </summary>
    public sealed class TestDictionaryKeyScriptComponent : Component {
        /// <summary>
        /// Initializes the supported dictionary test component with empty dictionaries.
        /// </summary>
        public TestDictionaryKeyScriptComponent() {
            IntegerLabels = new Dictionary<int, string>();
            ModeLabels = new Dictionary<TestDictionaryMode, string>();
        }

        /// <summary>
        /// Gets or sets the integer-keyed dictionary used by automatic persistence tests.
        /// </summary>
        public Dictionary<int, string> IntegerLabels { get; set; }

        /// <summary>
        /// Gets or sets the enum-keyed dictionary used by automatic persistence tests.
        /// </summary>
        public Dictionary<TestDictionaryMode, string> ModeLabels { get; set; }
    }
}
