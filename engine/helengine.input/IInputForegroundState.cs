namespace helengine {
    /// <summary>
    /// Optional input-backend capability that reports whether its host window currently owns foreground focus.
    /// </summary>
    public interface IInputForegroundState {
        /// <summary>
        /// Gets whether the host window is active and can continue an in-progress pointer interaction.
        /// </summary>
        bool IsForegroundActive { get; }
    }
}
