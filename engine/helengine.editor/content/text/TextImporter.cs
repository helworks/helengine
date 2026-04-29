namespace helengine.editor {
    /// <summary>
    /// Imports plain text files into text assets using UTF-8 decoding.
    /// </summary>
    public class TextImporter : ITextImporter {
        /// <summary>
        /// Imports a text asset from the provided stream.
        /// </summary>
        /// <param name="stream">Stream containing UTF-8 text data.</param>
        /// <returns>Created <see cref="TextAsset"/> instance.</returns>
        public TextAsset ImportText(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, true, 1024, true);
            string text = reader.ReadToEnd();
            return new TextAsset {
                Text = text
            };
        }
    }
}
