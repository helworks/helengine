namespace helengine.core.tests.content {
    /// <summary>
    /// Verifies that explicit content aliases remain neutral and one-hop.
    /// </summary>
    public sealed class AliasedContentStreamSourceTests {
        /// <summary>
        /// Ensures a logical path is mapped exactly once before the underlying source reads it.
        /// </summary>
        [Fact]
        public void OpenRead_UsesExplicitAliasOnce() {
            RecordingContentStreamSource source = new RecordingContentStreamSource();
            AliasedContentStreamSource aliases = new AliasedContentStreamSource(
                source,
                new Dictionary<string, string>(StringComparer.Ordinal) {
                    ["logical.bin"] = "stored.bin",
                    ["stored.bin"] = "second.bin"
                });

            using Stream result = aliases.OpenRead("logical.bin");

            Assert.NotNull(result);
            Assert.Equal("stored.bin", source.LastPath);
        }

        /// <summary>
        /// Ensures an unmapped path is passed through unchanged.
        /// </summary>
        [Fact]
        public void OpenRead_PassesThroughUnmappedPath() {
            RecordingContentStreamSource source = new RecordingContentStreamSource();
            AliasedContentStreamSource aliases = new AliasedContentStreamSource(source, new Dictionary<string, string>());

            using Stream result = aliases.OpenRead("asset.bin");

            Assert.NotNull(result);
            Assert.Equal("asset.bin", source.LastPath);
        }

        /// <summary>
        /// Ensures constructor and read arguments reject missing values.
        /// </summary>
        [Fact]
        public void ConstructorAndOpenRead_RejectMissingValues() {
            RecordingContentStreamSource source = new RecordingContentStreamSource();

            Assert.Throws<ArgumentNullException>(() => new AliasedContentStreamSource(null, new Dictionary<string, string>()));
            Assert.Throws<ArgumentNullException>(() => new AliasedContentStreamSource(source, null));
            Assert.Throws<ArgumentException>(() => new AliasedContentStreamSource(source, new Dictionary<string, string> { [""] = "stored" }));
            Assert.Throws<ArgumentException>(() => new AliasedContentStreamSource(source, new Dictionary<string, string> { ["logical"] = "" }));

            AliasedContentStreamSource aliases = new AliasedContentStreamSource(source, new Dictionary<string, string>());
            Assert.Throws<ArgumentException>(() => aliases.OpenRead(""));
        }

        /// <summary>
        /// Records the final path sent to a delegated content source.
        /// </summary>
        sealed class RecordingContentStreamSource : IContentStreamSource {
            /// <summary>
            /// Gets the most recently requested path.
            /// </summary>
            public string LastPath { get; private set; }

            /// <summary>
            /// Records one read and returns an empty stream.
            /// </summary>
            /// <param name="assetPath">Path requested by the alias source.</param>
            /// <returns>Empty readable stream.</returns>
            public Stream OpenRead(string assetPath) {
                LastPath = assetPath;
                return new MemoryStream();
            }
        }
    }
}
