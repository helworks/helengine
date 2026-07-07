using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies that the core owns and reuses content managers by injected content stream source.
    /// </summary>
    public class CoreContentManagerTests : IDisposable {
        /// <summary>
        /// Temporary root used for the core's default content manager.
        /// </summary>
        readonly string DefaultContentRootPath;

        /// <summary>
        /// Temporary root used for a secondary content-manager source lookup.
        /// </summary>
        readonly string ProjectContentRootPath;

        /// <summary>
        /// Initializes isolated content roots for each test.
        /// </summary>
        public CoreContentManagerTests() {
            string testRootPath = Path.Combine(Path.GetTempPath(), "helengine-core-content-tests", Guid.NewGuid().ToString("N"));
            DefaultContentRootPath = Path.Combine(testRootPath, "default");
            ProjectContentRootPath = Path.Combine(testRootPath, "project");
            Directory.CreateDirectory(DefaultContentRootPath);
            Directory.CreateDirectory(ProjectContentRootPath);
        }

        /// <summary>
        /// Removes the temporary content roots after each test.
        /// </summary>
        public void Dispose() {
            string testRootPath = Path.GetDirectoryName(DefaultContentRootPath);
            if (!string.IsNullOrWhiteSpace(testRootPath) && Directory.Exists(testRootPath)) {
                Directory.Delete(testRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the core returns the same content manager instance for repeated requests to the same source.
        /// </summary>
        [Fact]
        public void GetContentManager_WithSameSource_ReturnsCachedInstance() {
            HostFileSystemContentStreamSource defaultSource = new HostFileSystemContentStreamSource(DefaultContentRootPath);
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = defaultSource
            });

            ContentManager first = core.GetContentManager(defaultSource);
            ContentManager second = core.GetContentManager(defaultSource);

            Assert.Same(first, second);
            Assert.Same(first, core.ContentManager);
        }

        /// <summary>
        /// Ensures different content sources receive different cached manager instances.
        /// </summary>
        [Fact]
        public void GetContentManager_WithDifferentSources_ReturnsDifferentInstances() {
            HostFileSystemContentStreamSource defaultSource = new HostFileSystemContentStreamSource(DefaultContentRootPath);
            HostFileSystemContentStreamSource projectSource = new HostFileSystemContentStreamSource(ProjectContentRootPath);
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = defaultSource
            });

            ContentManager defaultManager = core.GetContentManager(defaultSource);
            ContentManager projectManager = core.GetContentManager(projectSource);

            Assert.NotSame(defaultManager, projectManager);
        }

        /// <summary>
        /// Ensures editor content-manager configuration can be applied repeatedly to the same cached manager.
        /// </summary>
        [Fact]
        public void ConfigureProjectContentManager_WhenCalledTwice_RemainsReusable() {
            HostFileSystemContentStreamSource defaultSource = new HostFileSystemContentStreamSource(DefaultContentRootPath);
            HostFileSystemContentStreamSource projectSource = new HostFileSystemContentStreamSource(ProjectContentRootPath);
            Core core = new Core(new CoreInitializationOptions {
                ContentStreamSource = defaultSource
            });

            ContentManager contentManager = core.GetContentManager(projectSource);

            EditorContentManagerConfiguration.ConfigureSharedAssetContentManager(contentManager);
            EditorContentManagerConfiguration.ConfigureProjectContentManager(contentManager);
            EditorContentManagerConfiguration.ConfigureProjectContentManager(contentManager);

            Assert.True(contentManager.IsProcessorRegistered(EditorContentProcessorIds.AssetImportSettings));
            Assert.True(contentManager.IsProcessorRegistered(EditorContentProcessorIds.TextureAsset));
        }

        /// <summary>
        /// Ensures core initialization rejects missing content stream sources.
        /// </summary>
        [Fact]
        public void Initialize_WhenContentStreamSourceIsMissing_Throws() {
            CoreInitializationOptions options = new CoreInitializationOptions {
                ContentStreamSource = null
            };

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => new Core(options));

            Assert.Contains("ContentStreamSource", exception.Message);
        }

    }
}
