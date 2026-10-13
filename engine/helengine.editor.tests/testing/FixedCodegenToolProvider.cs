namespace helengine.editor.tests.testing {
    /// <summary>
    /// Codegen tool provider that returns a fixed, non-existent executable path. Tests that must never launch codegen
    /// use it so any accidental codegen invocation fails loudly instead of touching the real submodule.
    /// </summary>
    public sealed class FixedCodegenToolProvider : IEngineCodegenToolProvider {
        /// <summary>
        /// Returns the fixed placeholder codegen executable path.
        /// </summary>
        /// <returns>The placeholder path <c>provider-codegen.exe</c>.</returns>
        public string Resolve() {
            return "provider-codegen.exe";
        }
    }
}
