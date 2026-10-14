namespace helengine.editor {
    /// <summary>
    /// Starts the background half of editor Play (the cook and the script build) away from the editor frame thread, so
    /// the editor keeps rendering and responding while a Play is prepared. Supplied through an interface so tests can run
    /// the preparation inline or on a thread they observe.
    /// </summary>
    public interface IEditorPlayPreparationScheduler {
        /// <summary>
        /// Starts the work and returns without waiting for it. The work must report its own outcome; it is never
        /// awaited by the caller.
        /// </summary>
        /// <param name="work">Preparation work to run.</param>
        void Start(Action work);
    }
}
