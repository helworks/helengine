namespace helengine.editor {
    /// <summary>
    /// Identifies the baked menu scene layout variant that should be generated for one target scene id.
    /// </summary>
    public enum DemoMenuSceneBuildVariant {
        /// <summary>
        /// Generates the existing desktop-oriented single-screen menu scene.
        /// </summary>
        Desktop = 0,

        /// <summary>
        /// Generates the Nintendo DS dual-screen menu scene with separate top and bottom cameras.
        /// </summary>
        NintendoDs = 1
    }
}
