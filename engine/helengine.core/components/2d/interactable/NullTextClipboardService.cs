namespace helengine {
    /// <summary>
    /// Provides a no-op clipboard implementation for hosts that do not expose clipboard integration.
    /// </summary>
    public sealed class NullTextClipboardService : ITextClipboardService {
        /// <summary>
        /// Gets whether this no-op clipboard has any readable text.
        /// </summary>
        /// <returns>Always false.</returns>
        public bool HasText() {
            return false;
        }

        /// <summary>
        /// Reads the clipboard text payload.
        /// </summary>
        /// <returns>Always an empty string.</returns>
        public string ReadText() {
            return string.Empty;
        }

        /// <summary>
        /// Ignores clipboard writes for hosts without clipboard integration.
        /// </summary>
        /// <param name="text">Text payload requested by the caller.</param>
        public void WriteText(string text) {
        }
    }
}
