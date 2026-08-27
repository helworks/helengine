namespace helengine.editor {
    /// <summary>
    /// Coordinates the disposable lifetime of one project authoring session.
    /// </summary>
    sealed class EditorAuthoringSessionLifetime : IEditorAuthoringSessionLifetime {
        /// <summary>
        /// Disposable service owned by this session lifetime.
        /// </summary>
        readonly IDisposable OwnedService;
        /// <summary>
        /// Tracks whether the coordinator has already released its owned state.
        /// </summary>
        bool IsDisposed;

        /// <summary>
        /// Initializes a lifetime coordinator over one session-owned disposable service.
        /// </summary>
        /// <param name="ownedService">Disposable service owned by the session.</param>
        public EditorAuthoringSessionLifetime(IDisposable ownedService) {
            OwnedService = ownedService ?? throw new ArgumentNullException(nameof(ownedService));
        }

        /// <summary>
        /// Releases the current session lifetime exactly once.
        /// </summary>
        public void Dispose() {
            if (IsDisposed) {
                return;
            }

            IsDisposed = true;
            OwnedService.Dispose();
        }
    }
}
