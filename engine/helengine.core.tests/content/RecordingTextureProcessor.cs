namespace helengine.core.tests.content {
    /// <summary>
    /// Test texture processor that ignores the stream payload, returns a fresh 1x1 texture and remembers it so tests can verify which processor served a load.
    /// </summary>
    public sealed class RecordingTextureProcessor : IContentProcessor<TextureAsset> {
        /// <summary>
        /// Gets the texture returned by the most recent <see cref="Read"/> call; null before any read.
        /// </summary>
        public TextureAsset LastResult { get; private set; }

        /// <summary>
        /// Gets the output type produced by this processor, always <see cref="TextureAsset"/>.
        /// </summary>
        public Type OutputType => typeof(TextureAsset);

        /// <summary>
        /// Creates a new 1x1 texture, stores it in <see cref="LastResult"/> and returns it.
        /// </summary>
        /// <param name="stream">Content stream supplied by the content manager; its bytes are not inspected.</param>
        /// <returns>The newly created texture.</returns>
        public TextureAsset Read(Stream stream) {
            LastResult = new TextureAsset { Width = 1, Height = 1, Colors = new byte[4] };
            return LastResult;
        }

        /// <summary>
        /// Untyped entry point that forwards to <see cref="Read"/>.
        /// </summary>
        /// <param name="stream">Content stream supplied by the content manager.</param>
        /// <returns>The newly created texture.</returns>
        public object ReadObject(Stream stream) {
            return Read(stream);
        }
    }
}
