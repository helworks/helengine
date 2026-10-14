namespace helengine.baseplatform.Player {
    /// <summary>
    /// Converts <see cref="PlayerWindowMonitor"/> to and from the <c>window-monitor</c> Play setting text.
    /// </summary>
    public static class PlayerWindowMonitorText {
        /// <summary>
        /// Parses one validated <c>window-monitor</c> value.
        /// </summary>
        /// <param name="text">Setting text (<c>editor</c> or <c>primary</c>).</param>
        /// <returns>The monitor choice.</returns>
        /// <exception cref="ArgumentException">Thrown when the text is not a monitor choice.</exception>
        public static PlayerWindowMonitor Parse(string text) {
            if (string.Equals(text, "editor", StringComparison.Ordinal)) {
                return PlayerWindowMonitor.Editor;
            } else if (string.Equals(text, "primary", StringComparison.Ordinal)) {
                return PlayerWindowMonitor.Primary;
            }

            throw new ArgumentException($"'{text}' is not a window monitor; expected editor or primary.", nameof(text));
        }

        /// <summary>
        /// Formats one monitor choice as its setting text.
        /// </summary>
        /// <param name="monitor">Monitor choice.</param>
        /// <returns>The setting text.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown for an undefined enum value.</exception>
        public static string Format(PlayerWindowMonitor monitor) {
            switch (monitor) {
                case PlayerWindowMonitor.Editor: return "editor";
                case PlayerWindowMonitor.Primary: return "primary";
                default: throw new ArgumentOutOfRangeException(nameof(monitor), monitor, "Unknown window monitor.");
            }
        }
    }
}
