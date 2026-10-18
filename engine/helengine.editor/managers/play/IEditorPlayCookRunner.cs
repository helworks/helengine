namespace helengine.editor {
    /// <summary>
    /// Runs the assets-only cook that stages content for one editor Play session.
    /// </summary>
    public interface IEditorPlayCookRunner {
        /// <summary>
        /// Cooks the project's content for the given platform and reports where the player should read it from.
        /// </summary>
        /// <param name="platformId">Identifier of the platform whose content processors drive the cook.</param>
        /// <param name="playSettings">Press-time Play selection, or null for a project-only cook.</param>
        /// <returns>Cook outcome with the staged content root and startup scene on success.</returns>
        EditorPlayCookResult Cook(string platformId, EditorPlaySettingsSnapshot playSettings = null);
    }
}
