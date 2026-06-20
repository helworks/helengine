namespace helengine.editor.tests.testing {
    /// <summary>
    /// Scripted component with one unsupported dictionary key type used to verify clear reflected persistence failures.
    /// </summary>
    public sealed class TestUnsupportedDictionaryKeyScriptComponent : Component {
        /// <summary>
        /// Initializes the unsupported dictionary-key test component with one empty dictionary.
        /// </summary>
        public TestUnsupportedDictionaryKeyScriptComponent() {
            InvalidKeys = new Dictionary<float2, string>();
        }

        /// <summary>
        /// Gets or sets the unsupported dictionary used by automatic persistence tests.
        /// </summary>
        public Dictionary<float2, string> InvalidKeys { get; set; }
    }
}
