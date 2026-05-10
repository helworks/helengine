namespace helengine.editor {
    /// <summary>
    /// Defines the per-instance panel controller contract used by workspace layout management.
    /// </summary>
    public interface IEditorWorkspacePanelController : IDisposable {
        /// <summary>
        /// Gets the dockable entity owned by the panel instance.
        /// </summary>
        DockableEntity Dockable { get; }

        /// <summary>
        /// Captures one serializable panel-specific state payload.
        /// </summary>
        /// <returns>Serializable state payload for the current panel instance.</returns>
        object CaptureState();

        /// <summary>
        /// Restores one previously captured panel-specific state payload.
        /// </summary>
        /// <param name="state">Serialized state payload to reapply.</param>
        void RestoreState(object state);
    }
}
