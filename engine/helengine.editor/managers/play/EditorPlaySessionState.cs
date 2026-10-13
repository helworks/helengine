namespace helengine.editor {
    /// <summary>
    /// Lifecycle phase of the editor Play session, driving the Play/Stop button presentation.
    /// </summary>
    public enum EditorPlaySessionState {
        /// <summary>
        /// No player process is running and no Play request is in progress.
        /// </summary>
        Stopped = 0,

        /// <summary>
        /// A Play request is saving the scene, cooking content or building scripts before launch.
        /// </summary>
        Preparing = 1,

        /// <summary>
        /// The separate player process has been launched and has not yet exited or been stopped.
        /// </summary>
        Running = 2
    }
}
