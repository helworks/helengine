namespace helengine.editor.tests.testing {
    /// <summary>
    /// Provides a deterministic clipboard implementation for textbox shortcut tests.
    /// </summary>
    internal sealed class TestTextClipboardService : ITextClipboardService {
        /// <summary>
        /// Gets or sets the current clipboard text payload.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Gets whether the test clipboard currently contains any text payload.
        /// </summary>
        public bool HasText() {
            return !string.IsNullOrEmpty(Text);
        }

        /// <summary>
        /// Reads the current clipboard text payload.
        /// </summary>
        /// <returns>Clipboard text payload.</returns>
        public string ReadText() {
            return Text;
        }

        /// <summary>
        /// Replaces the clipboard payload with the supplied text.
        /// </summary>
        /// <param name="text">Text payload to store.</param>
        public void WriteText(string text) {
            Text = text ?? string.Empty;
        }
    }
}
