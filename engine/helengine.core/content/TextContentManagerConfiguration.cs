namespace helengine {
    /// <summary>
    /// Registers text-processing support onto a generic content manager without making plain filesystem content managers depend on text parsing by default.
    /// </summary>
    public static class TextContentManagerConfiguration {
        /// <summary>
        /// Stable processor identifier used for raw UTF-8 text loading.
        /// </summary>
        const string TextContentProcessorId = "core.text-content";

        /// <summary>
        /// Wildcard extension token used to match any file suffix.
        /// </summary>
        const string WildcardExtension = "*";

        /// <summary>
        /// Ensures UTF-8 text loading is registered on the supplied content manager.
        /// </summary>
        /// <param name="contentManager">Content manager that should support raw text loads.</param>
        public static void Configure(ContentManager contentManager) {
            if (contentManager == null) {
                throw new ArgumentNullException(nameof(contentManager));
            }

            if (contentManager.IsProcessorRegistered(TextContentProcessorId)) {
                return;
            }

            contentManager.RegisterProcessor(
                TextContentProcessorId,
                new TextContentProcessor(),
                new[] { WildcardExtension });
        }
    }
}
