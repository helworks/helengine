namespace helengine.editor {
    /// <summary>
    /// Outcome of one editor Play cook: whether the assets-only build succeeded, and where the desktop player
    /// should find the staged content and the startup scene.
    /// </summary>
    public sealed class EditorPlayCookResult {
        /// <summary>
        /// Initializes one Play cook outcome.
        /// </summary>
        /// <param name="succeeded">Whether the assets-only build completed successfully.</param>
        /// <param name="message">Human-readable success or failure message.</param>
        /// <param name="contentRootPath">Absolute root of the staged cooked content the player reads; empty on failure.</param>
        /// <param name="startupSceneId">Identifier of the scene the player should load first; empty on failure.</param>
        /// <param name="startupSceneCookedRelativePath">Forward-slash path of the startup scene relative to the cooked content layout; empty on failure.</param>
        public EditorPlayCookResult(
            bool succeeded,
            string message,
            string contentRootPath,
            string startupSceneId,
            string startupSceneCookedRelativePath) {
            Succeeded = succeeded;
            Message = message ?? throw new ArgumentNullException(nameof(message));
            ContentRootPath = contentRootPath ?? throw new ArgumentNullException(nameof(contentRootPath));
            StartupSceneId = startupSceneId ?? throw new ArgumentNullException(nameof(startupSceneId));
            StartupSceneCookedRelativePath = startupSceneCookedRelativePath ?? throw new ArgumentNullException(nameof(startupSceneCookedRelativePath));
        }

        /// <summary>
        /// Gets whether the assets-only build completed successfully.
        /// </summary>
        public bool Succeeded { get; }

        /// <summary>
        /// Gets the human-readable success or failure message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the absolute root of the staged cooked content the player reads; empty on failure.
        /// </summary>
        public string ContentRootPath { get; }

        /// <summary>
        /// Gets the identifier of the scene the player should load first; empty on failure.
        /// </summary>
        public string StartupSceneId { get; }

        /// <summary>
        /// Gets the forward-slash path of the startup scene relative to the cooked content layout; empty on failure.
        /// </summary>
        public string StartupSceneCookedRelativePath { get; }

        /// <summary>
        /// Creates a failed Play cook outcome with empty content and scene paths.
        /// </summary>
        /// <param name="message">Human-readable failure message.</param>
        /// <returns>Failed Play cook outcome.</returns>
        public static EditorPlayCookResult Failure(string message) {
            return new EditorPlayCookResult(false, message, string.Empty, string.Empty, string.Empty);
        }
    }
}
