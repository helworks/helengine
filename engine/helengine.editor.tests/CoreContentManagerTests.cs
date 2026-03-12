using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies that the core owns and reuses content managers by root path.
    /// </summary>
    public class CoreContentManagerTests : IDisposable {
        /// <summary>
        /// Temporary root used for the core's default content manager.
        /// </summary>
        readonly string DefaultContentRootPath;

        /// <summary>
        /// Temporary root used for a secondary content manager lookup.
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
        /// Ensures the core returns the same content manager instance for repeated requests to the same root.
        /// </summary>
        [Fact]
        public void GetContentManager_WithSameRoot_ReturnsCachedInstance() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = DefaultContentRootPath
            });

            ContentManager first = core.GetContentManager(DefaultContentRootPath);
            ContentManager second = core.GetContentManager(DefaultContentRootPath);

            Assert.Same(first, second);
            Assert.Same(first, core.ContentManager);
        }

        /// <summary>
        /// Ensures different content roots receive different cached manager instances.
        /// </summary>
        [Fact]
        public void GetContentManager_WithDifferentRoots_ReturnsDifferentInstances() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = DefaultContentRootPath
            });

            ContentManager defaultManager = core.GetContentManager(DefaultContentRootPath);
            ContentManager projectManager = core.GetContentManager(ProjectContentRootPath);

            Assert.NotSame(defaultManager, projectManager);
        }

        /// <summary>
        /// Ensures editor content-manager configuration can be applied repeatedly to the same cached manager.
        /// </summary>
        [Fact]
        public void ConfigureProjectContentManager_WhenCalledTwice_RemainsReusable() {
            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = DefaultContentRootPath
            });

            ContentManager contentManager = core.GetContentManager(ProjectContentRootPath);

            EditorContentManagerConfiguration.ConfigureProjectContentManager(contentManager);
            EditorContentManagerConfiguration.ConfigureProjectContentManager(contentManager);

            Assert.True(contentManager.IsProcessorRegistered(EditorContentProcessorIds.AssetImportSettings));
            Assert.True(contentManager.IsProcessorRegistered(EditorContentProcessorIds.TextureAsset));
        }
    }
}
