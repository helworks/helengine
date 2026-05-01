using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.serialization.scene {
    /// <summary>
    /// Verifies runtime scene loading emits a timing log for packaged asset materialization.
    /// </summary>
    public class RuntimeSceneLoadServiceTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the runtime scene-load test harness.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the runtime services required by the scene-load tests.
        /// </summary>
        public RuntimeSceneLoadServiceTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-runtime-scene-load-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });

            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputManager());
        }

        /// <summary>
        /// Deletes temporary test content after each run.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures packaged runtime scene loading writes a start log and a timing log around materialization.
        /// </summary>
        [Fact]
        public void Load_WhenSceneIsMaterialized_WritesStartAndTimingLogs() {
            List<LogEntry> loggedMessages = new List<LogEntry>();
            Action<LogEntry> handleMessageLogged = loggedMessages.Add;

            Logger.MessageLogged += handleMessageLogged;
            try {
                RuntimeSceneAssetReferenceResolver resolver = new RuntimeSceneAssetReferenceResolver(
                    Core.Instance.ContentManager,
                    TempRootPath,
                    ShaderCompileTarget.DirectX11);
                RuntimeSceneLoadService loadService = new RuntimeSceneLoadService(resolver);
                SceneAsset sceneAsset = new SceneAsset {
                    RootEntities = new[] {
                        new SceneEntityAsset {
                            Name = "Root"
                        }
                    }
                };

                IReadOnlyList<Entity> loadedRoots = loadService.Load(sceneAsset);

                Assert.Single(loadedRoots);
                Assert.Contains(loggedMessages, entry => entry.Level == LogLevel.Info && entry.Message == "Loading packaged scene assets.");
                Assert.Contains(loggedMessages, entry => entry.Level == LogLevel.Info && entry.Message.StartsWith("Loaded packaged scene assets in ", StringComparison.Ordinal));
            } finally {
                Logger.MessageLogged -= handleMessageLogged;
            }
        }
    }
}
