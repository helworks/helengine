namespace helengine {
    /// <summary>
    /// Reads UTF-8 text files into <see cref="TextContent"/> instances.
    /// </summary>
    public class TextContentProcessor : IContentProcessor<TextContent> {
        /// <summary>
        /// Gets the output type produced by this processor.
        /// </summary>
        public Type OutputType => typeof(TextContent);

        /// <summary>
        /// Reads UTF-8 text from the supplied stream.
        /// </summary>
        /// <param name="stream">Stream containing UTF-8 text data.</param>
        /// <returns>Decoded text content.</returns>
        public TextContent Read(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            using StreamReader reader = new StreamReader(stream, System.Text.Encoding.UTF8, true, 1024, true);
            return new TextContent {
                Text = reader.ReadToEnd()
            };
        }

        /// <summary>
        /// Reads UTF-8 text from the supplied stream and returns it boxed as an object.
        /// </summary>
        /// <param name="stream">Stream containing UTF-8 text data.</param>
        /// <returns>Decoded text content boxed as an object.</returns>
        object IContentProcessor.ReadObject(Stream stream) {
            return Read(stream);
        }
    }
}
