namespace helengine.editor.app {
    /// <summary>
    /// Bridges textbox clipboard commands to the native Windows clipboard used by the editor host.
    /// </summary>
    internal sealed class SystemTextClipboardService : ITextClipboardService {
        /// <summary>
        /// Gets whether the Windows clipboard currently contains text.
        /// </summary>
        /// <returns>True when text can be pasted from the clipboard.</returns>
        public bool HasText() {
            return Clipboard.ContainsText();
        }

        /// <summary>
        /// Reads the current Windows clipboard text payload.
        /// </summary>
        /// <returns>Clipboard text payload.</returns>
        public string ReadText() {
            return Clipboard.GetText();
        }

        /// <summary>
        /// Replaces the current Windows clipboard text payload.
        /// </summary>
        /// <param name="text">Text payload to store.</param>
        public void WriteText(string text) {
            Clipboard.SetText(text ?? string.Empty);
        }
    }
}
