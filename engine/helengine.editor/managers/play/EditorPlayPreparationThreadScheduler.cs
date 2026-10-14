namespace helengine.editor {
    /// <summary>
    /// Production Play preparation scheduler: runs each preparation on its own dedicated background thread. A dedicated
    /// thread is used instead of the thread pool because a cook can run for seconds and blocks on file I/O and child
    /// processes. The thread is a background thread, so closing the editor while a Play is preparing never keeps the
    /// process alive; the preparation is then abandoned and its result is never delivered.
    /// </summary>
    public sealed class EditorPlayPreparationThreadScheduler : IEditorPlayPreparationScheduler {
        /// <summary>
        /// Name given to every preparation thread, so it is recognisable in a debugger's thread list.
        /// </summary>
        const string ThreadName = "helengine Play preparation";

        /// <summary>
        /// Starts the work on a new background thread and returns immediately.
        /// </summary>
        /// <param name="work">Preparation work to run.</param>
        /// <exception cref="ArgumentNullException">Thrown when the work is null.</exception>
        public void Start(Action work) {
            if (work == null) {
                throw new ArgumentNullException(nameof(work));
            }

            Thread thread = new Thread(new ThreadStart(work)) {
                IsBackground = true,
                Name = ThreadName
            };
            thread.Start();
        }
    }
}
