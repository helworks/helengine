namespace helengine.editor.windows {
    /// <summary>
    /// Exposes whether a Windows host should be treated as the foreground input owner.
    /// </summary>
    public interface IWindowForegroundState {
        /// <summary>
        /// Gets a value indicating whether foreground-only window affordances should be active.
        /// </summary>
        bool IsWindowForegroundActive { get; }
    }
}
