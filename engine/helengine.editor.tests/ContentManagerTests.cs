using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies type-based content loading, raw loading, and processor resolution.
    /// </summary>
    public class ContentManagerTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the test content manager.
        /// </summary>
        readonly string ContentRootPath;

        /// <summary>
        /// Initializes a new test fixture with an isolated content directory.
        /// </summary>
        public ContentManagerTests() {
            ContentRootPath = Path.Combine(Path.GetTempPath(), "helengine-content-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ContentRootPath);
        }

        /// <summary>
        /// Removes the temporary content directory after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ContentRootPath)) {
                Directory.Delete(ContentRootPath, true);
            }
        }

        /// <summary>
        /// Ensures raw byte content can be loaded through the built-in wildcard processor.
        /// </summary>
        [Fact]
        public void Load_RawByteContent_ReturnsRawBytes() {
            WriteBytesFile("raw.bin", new byte[] { 1, 2, 3, 4, 5 });
            ContentManager contentManager = CreateContentManager();

            RawByteContent loadedContent = contentManager.Load<RawByteContent>("raw.bin");

            Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, loadedContent.Bytes);
        }

        /// <summary>
        /// Ensures text content can be loaded through the built-in wildcard processor.
        /// </summary>
        [Fact]
        public void Load_TextContent_ReturnsRawUtf8Text() {
            WriteTextFile("notes.txt", "hello content");
            ContentManager contentManager = CreateContentManager();

            TextContent content = contentManager.Load<TextContent>("notes.txt");

            Assert.Equal("hello content", content.Text);
        }

        /// <summary>
        /// Ensures the content manager resolves the default processor by type and extension.
        /// </summary>
        [Fact]
        public void Load_TextAsset_UsesRegisteredProcessorByTypeAndExtension() {
            WriteTextFile("dialogue.txt", "line one");
            ContentManager contentManager = CreateContentManager();
            contentManager.RegisterProcessor("text", new TextImporterContentProcessor(new TextImporter()), new[] { ".txt" });

            TextAsset asset = contentManager.Load<TextAsset>("dialogue.txt");

            Assert.Equal("line one", asset.Text);
        }

        /// <summary>
        /// Ensures an explicit processor id overrides extension-based resolution.
        /// </summary>
        [Fact]
        public void Load_WithExplicitProcessorId_UsesSpecifiedProcessor() {
            WriteTextFile("message.txt", "mixed Case");
            ContentManager contentManager = CreateContentManager();
            contentManager.RegisterProcessor("text", new TextImporterContentProcessor(new TextImporter()), new[] { ".txt" });
            contentManager.RegisterProcessor("upper", new TestUppercaseTextAssetContentProcessor());

            TextAsset asset = contentManager.Load<TextAsset>("message.txt", "upper");

            Assert.Equal("MIXED CASE", asset.Text);
        }

        /// <summary>
        /// Ensures explicit-only processors can be registered without any default extension mapping.
        /// </summary>
        [Fact]
        public void RegisterProcessor_WithoutExtensions_AllowsExplicitOnlyLoads() {
            WriteTextFile("explicit.dat", "processor only");
            ContentManager contentManager = CreateContentManager();
            contentManager.RegisterProcessor("upper", new TestUppercaseTextAssetContentProcessor());

            TextAsset asset = contentManager.Load<TextAsset>("explicit.dat", "upper");

            Assert.Equal("PROCESSOR ONLY", asset.Text);
        }

        /// <summary>
        /// Ensures the default processor resolver can match compound file suffixes.
        /// </summary>
        [Fact]
        public void Load_WithCompoundExtension_UsesLongestRegisteredSuffix() {
            string path = Path.Combine(ContentRootPath, "effect.dx11.shader.asset");
            ShaderAsset shaderAsset = new ShaderAsset {
                Id = "shader/test",
                TargetName = "dx11",
                Programs = Array.Empty<ShaderProgramAsset>(),
                Binaries = Array.Empty<ShaderBinaryAsset>()
            };
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, shaderAsset);
            }

            ContentManager contentManager = CreateContentManager();
            contentManager.RegisterProcessor("shader", new AssetContentProcessor<ShaderAsset>(), new[] { ".shader.asset" });
            contentManager.RegisterProcessor("asset", new AssetContentProcessor<ShaderAsset>(), new[] { ".asset" });

            ShaderAsset loadedShader = contentManager.Load<ShaderAsset>("effect.dx11.shader.asset");

            Assert.Equal("shader/test", loadedShader.Id);
        }

        /// <summary>
        /// Ensures callers can provide a processor instance directly for one-off loads.
        /// </summary>
        [Fact]
        public void Load_WithProcessorInstance_UsesProvidedProcessor() {
            WriteTextFile("instance.txt", "mixed Case");
            ContentManager contentManager = CreateContentManager();

            TextAsset asset = contentManager.Load("instance.txt", new TestUppercaseTextAssetContentProcessor());

            Assert.Equal("MIXED CASE", asset.Text);
        }

        /// <summary>
        /// Ensures absolute file paths are loaded directly even when they are outside the configured content root.
        /// </summary>
        [Fact]
        public void Load_WithAbsolutePath_LoadsFileOutsideRoot() {
            string externalRootPath = Path.Combine(Path.GetTempPath(), "helengine-content-tests-external", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(externalRootPath);

            try {
                string externalFilePath = Path.Combine(externalRootPath, "absolute.txt");
                File.WriteAllText(externalFilePath, "absolute content", System.Text.Encoding.UTF8);
                ContentManager contentManager = CreateContentManager();

                TextContent content = contentManager.Load<TextContent>(externalFilePath);

                Assert.Equal("absolute content", content.Text);
            } finally {
                if (Directory.Exists(externalRootPath)) {
                    Directory.Delete(externalRootPath, true);
                }
            }
        }

        /// <summary>
        /// Ensures a missing default processor fails with a clear error.
        /// </summary>
        [Fact]
        public void Load_WhenProcessorIsMissing_Throws() {
            WriteTextFile("data.txt", "missing");
            ContentManager contentManager = CreateContentManager();

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => contentManager.Load<TextAsset>("data.txt"));

            Assert.Contains("No content processors are registered", exception.Message);
        }

        /// <summary>
        /// Ensures duplicate default mappings for the same type and extension are rejected.
        /// </summary>
        [Fact]
        public void RegisterProcessor_WhenTypeExtensionAlreadyMapped_Throws() {
            ContentManager contentManager = CreateContentManager();
            contentManager.RegisterProcessor("text", new TextImporterContentProcessor(new TextImporter()), new[] { ".txt" });

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => contentManager.RegisterProcessor("upper", new TestUppercaseTextAssetContentProcessor(), new[] { ".TXT" }));

            Assert.Contains(".txt", exception.Message);
        }

        /// <summary>
        /// Ensures an explicit processor id cannot be used to request the wrong output type.
        /// </summary>
        [Fact]
        public void Load_WhenExplicitProcessorProducesDifferentType_Throws() {
            WriteTextFile("typed.txt", "wrong type");
            ContentManager contentManager = CreateContentManager();
            contentManager.RegisterProcessor("text", new TextImporterContentProcessor(new TextImporter()), new[] { ".txt" });

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => contentManager.Load<TextureAsset>("typed.txt", "text"));

            Assert.Contains("not 'TextureAsset'", exception.Message);
        }

        /// <summary>
        /// Creates a content manager rooted at the test directory.
        /// </summary>
        /// <returns>Content manager rooted at the isolated test directory.</returns>
        ContentManager CreateContentManager() {
            return new ContentManager(ContentRootPath);
        }

        /// <summary>
        /// Writes a UTF-8 text file into the test content directory.
        /// </summary>
        /// <param name="relativePath">Relative path under the test content root.</param>
        /// <param name="text">Text content to write.</param>
        void WriteTextFile(string relativePath, string text) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }
            if (text == null) {
                throw new ArgumentNullException(nameof(text));
            }

            string fullPath = Path.Combine(ContentRootPath, relativePath);
            string directoryPath = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directoryPath)) {
                Directory.CreateDirectory(directoryPath);
            }

            File.WriteAllText(fullPath, text, System.Text.Encoding.UTF8);
        }

        /// <summary>
        /// Writes a binary file into the test content directory.
        /// </summary>
        /// <param name="relativePath">Relative path under the test content root.</param>
        /// <param name="bytes">Bytes to write to disk.</param>
        void WriteBytesFile(string relativePath, byte[] bytes) {
            if (string.IsNullOrWhiteSpace(relativePath)) {
                throw new ArgumentException("Relative path must be provided.", nameof(relativePath));
            }
            if (bytes == null) {
                throw new ArgumentNullException(nameof(bytes));
            }

            string fullPath = Path.Combine(ContentRootPath, relativePath);
            string directoryPath = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directoryPath)) {
                Directory.CreateDirectory(directoryPath);
            }

            File.WriteAllBytes(fullPath, bytes);
        }
    }
}
