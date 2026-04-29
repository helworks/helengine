namespace helengine.editor.tests.testing {
    /// <summary>
    /// Test processor that reads UTF-8 text and returns an uppercased text asset.
    /// </summary>
    public class TestUppercaseTextAssetContentProcessor : IContentProcessor<TextAsset> {
        /// <summary>
        /// Gets the output type produced by this processor.
        /// </summary>
        public Type OutputType => typeof(TextAsset);

        /// <summary>
        /// Reads UTF-8 text from the supplied stream and uppercases the result.
        /// </summary>
        /// <param name="stream">Stream containing UTF-8 text data.</param>
        /// <returns>Uppercased text asset.</returns>
        public TextAsset Read(Stream stream) {
            if (stream == null) {
                throw new ArgumentNullException(nameof(stream));
            }

            using StreamReader reader = new StreamReader(stream, System.Text.Encoding.UTF8, true, 1024, true);
            return new TextAsset {
                Text = reader.ReadToEnd().ToUpperInvariant()
            };
        }

        /// <summary>
        /// Reads UTF-8 text from the supplied stream and returns an uppercased text asset boxed as an object.
        /// </summary>
        /// <param name="stream">Stream containing UTF-8 text data.</param>
        /// <returns>Uppercased text asset boxed as an object.</returns>
        object IContentProcessor.ReadObject(Stream stream) {
            return Read(stream);
        }
    }
}
