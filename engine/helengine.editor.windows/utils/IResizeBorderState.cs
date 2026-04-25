namespace helengine.editor.windows {
    /// <summary>
    /// Exposes whether a borderless host should allow resize-border hit testing and cursors.
    /// </summary>
    public interface IResizeBorderState {
        /// <summary>
        /// Gets a value indicating whether resize-border behavior should remain enabled for the current window state.
        /// </summary>
        bool IsResizeBorderEnabled { get; }
    }
}
