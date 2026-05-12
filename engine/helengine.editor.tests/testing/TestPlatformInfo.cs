namespace helengine.editor.tests.testing {
    /// <summary>
    /// Provides stable platform metadata used by tests that initialize the core runtime.
    /// </summary>
    public static class TestPlatformInfo {
        /// <summary>
        /// Gets the shared platform metadata instance used by test cores and editor sessions.
        /// </summary>
        public static PlatformInfo Shared { get; } = new PlatformInfo("test", "test-version");
    }
}
