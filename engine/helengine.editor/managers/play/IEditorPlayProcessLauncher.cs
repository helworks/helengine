namespace helengine.editor {
    /// <summary>
    /// Starts the separate desktop player process for an editor Play session.
    /// </summary>
    public interface IEditorPlayProcessLauncher {
        /// <summary>
        /// Starts the player described by the request.
        /// </summary>
        /// <param name="request">Executable path and ordered command-line arguments.</param>
        /// <returns>Handle to the started player process.</returns>
        IEditorPlayProcess Start(EditorPlayLaunchRequest request);
    }
}
