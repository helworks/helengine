namespace helengine {
    /// <summary>
    /// Abstracts clipboard text exchange for textbox shortcut commands.
    /// </summary>
    public interface ITextClipboardService {
        /// <summary>
        /// Gets whether the clipboard currently contains text that can be pasted.
        /// </summary>
        /// <returns>True when text can be read from the clipboard.</returns>
        bool HasText();

        /// <summary>
        /// Reads the current clipboard text payload.
        /// </summary>
        /// <returns>Clipboard text payload.</returns>
        string ReadText();

        /// <summary>
        /// Replaces the clipboard text payload.
        /// </summary>
        /// <param name="text">Text payload to store.</param>
        void WriteText(string text);
    }
}
