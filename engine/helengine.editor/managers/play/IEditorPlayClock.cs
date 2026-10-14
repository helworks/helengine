namespace helengine.editor {
    /// <summary>
    /// Monotonic clock the Play session reads to time its stages (save, cook, script build, launch) and the wait for the
    /// player's first frame. Supplied through an interface so the startup timing summary can be verified with fixed
    /// readings instead of real elapsed time.
    /// </summary>
    public interface IEditorPlayClock {
        /// <summary>
        /// Gets the current reading of the monotonic clock. Only differences between two readings are meaningful.
        /// </summary>
        TimeSpan Now { get; }
    }
}
