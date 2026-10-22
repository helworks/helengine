using helengine.baseplatform.Definitions;
using helengine.baseplatform.Profiles;

namespace helengine.editor.tests {
    /// <summary>Exercises invocation scene subsets through the real catalog, persisted configuration and build queue.</summary>
    public sealed class EditorCliSceneSelectionTests : IDisposable {
        /// <summary>Fixed disposable fixture root beneath the user-required temporary directory.</summary>
        readonly string ProjectRootPath = Path.Combine(Path.GetTempPath(), "cli-scene-selection");

        /// <summary>Creates authored scenes independently from the production selection code.</summary>
        public EditorCliSceneSelectionTests() {
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Scenes"));
            foreach (string sceneId in new[] { "A", "B", "DemoDiscMainMenu", "DemoDiscMainMenuHandheld" }) {
                using FileStream stream = File.Create(Path.Combine(ProjectRootPath, "assets", "Scenes", sceneId + ".helen"));
                AssetSerializer.Serialize(stream, new SceneAsset { Id = sceneId, AuthoringAssetId = Guid.NewGuid().ToString("N") });
            }
        }

        /// <summary>Removes this fixture's temporary scenes, sidecars and settings after each regression.</summary>
        public void Dispose() {
            Directory.Delete(ProjectRootPath, true);
        }

        /// <summary>An ordered override replaces stale persisted ordering without changing the source document or either settings file.</summary>
        [Fact]
        public void RequestedScenesReplacePersistedSelectionForThisInvocationOnly() {
            EditorProjectSceneCatalogService catalog = new EditorProjectSceneCatalogService(ProjectRootPath);
            EditorBuildPlatformConfigDocument source = new EditorBuildPlatformConfigDocument {
                PlatformId = "windows", SelectedSceneIds = ["A", "B"],
                SceneOrders = [new EditorBuildSceneOrderDocument { SceneId = "A", OrderNumber = 1 }, new EditorBuildSceneOrderDocument { SceneId = "B", OrderNumber = 2 }]
            };
            EditorBuildConfigService service = new EditorBuildConfigService(ProjectRootPath);
            service.Save(new EditorBuildConfigDocument { Platforms = [source] });
            string sharedPath = Path.Combine(ProjectRootPath, "settings", "build_config.json");
            string localPath = Path.Combine(ProjectRootPath, "user_settings", "build_config.json");
            string sharedBefore = File.ReadAllText(sharedPath);
            string localBefore = File.ReadAllText(localPath);
            EditorBuildPlatformConfigDocument invocation = EditorBuildConfigService.CopyPlatformConfig(source);

            ApplyRequestedScenes(invocation, catalog, ["B", "A", "B"]);
            Assert.Equal(new[] { "B", "A" }, invocation.SelectedSceneIds);
            Assert.Equal(new[] { "B", "A" }, catalog.ResolveSceneIds(invocation.SelectedSceneReferences));
            Assert.Equal(new[] { 1, 2 }, invocation.SceneOrders.Select(order => order.OrderNumber));
            Assert.Equal(new[] { "B", "A" }, invocation.SceneOrders.Select(order => catalog.ResolveSceneId(order.SceneReference)));
            EditorBuildQueueItemDocument queue = EditorBuildQueueItemDocument.Create(catalog, invocation, CreateSelectionModel("windows"), Path.Combine(ProjectRootPath, "output"));

            Assert.Equal(new[] { "B", "A" }, queue.SelectedSceneIds);
            Assert.Equal(new[] { "A", "B" }, source.SelectedSceneIds);
            Assert.Equal(new[] { "A", "B" }, source.SceneOrders.Select(order => order.SceneId));
            Assert.Equal(sharedBefore, File.ReadAllText(sharedPath));
            Assert.Equal(localBefore, File.ReadAllText(localPath));
            Assert.Equal(new[] { "A", "B" }, service.TryLoadExisting().Platforms.Single().SelectedSceneIds);
        }

        /// <summary>Later caller mutations cannot change the subset captured by a build request.</summary>
        [Fact]
        public void BuildOptionsCaptureIndependentSceneSelection() {
            List<string> sceneIds = ["A"];
            EditorCliBuildOptions options = new EditorCliBuildOptions(ProjectRootPath, "windows", "release", "output", false, "release", false, new Dictionary<string, string>(), sceneIds);
            sceneIds[0] = "B";
            EditorProjectSceneCatalogService catalog = new EditorProjectSceneCatalogService(ProjectRootPath);
            EditorBuildPlatformConfigDocument config = new EditorBuildPlatformConfigDocument { PlatformId = "windows" };

            ApplyRequestedScenes(config, catalog, options.SelectedSceneIds);
            EditorBuildQueueItemDocument queue = EditorBuildQueueItemDocument.Create(catalog, config, CreateSelectionModel("windows"), Path.Combine(ProjectRootPath, "output"));

            Assert.Equal(new[] { "A" }, queue.SelectedSceneIds);
        }

        /// <summary>Existing callers that omit a scene override retain their persisted subset and order.</summary>
        [Fact]
        public void OmittedScenesKeepPersistedSubset() {
            EditorCliBuildOptions options = new EditorCliBuildOptions(ProjectRootPath, "windows", "release", "output", false);
            EditorBuildPlatformConfigDocument config = new EditorBuildPlatformConfigDocument { PlatformId = "windows", SelectedSceneIds = ["B"] };
            EditorProjectSceneCatalogService catalog = new EditorProjectSceneCatalogService(ProjectRootPath);

            ApplyRequestedScenes(config, catalog, options.SelectedSceneIds);
            EditorBuildQueueItemDocument queue = EditorBuildQueueItemDocument.Create(catalog, config, CreateSelectionModel("windows"), Path.Combine(ProjectRootPath, "output"));

            Assert.Equal(new[] { "B" }, queue.SelectedSceneIds);
        }

        /// <summary>Unknown and incorrectly cased ids fail before queue normalization can silently restore all scenes.</summary>
        /// <param name="unknownSceneId">Invalid authored id supplied by the invocation.</param>
        [Theory]
        [InlineData("Missing")]
        [InlineData("demodiscmainmenu")]
        public void UnknownSceneFailsWithoutChangingSelection(string unknownSceneId) {
            EditorBuildPlatformConfigDocument config = new EditorBuildPlatformConfigDocument { SelectedSceneIds = ["A"] };

            ArgumentException error = Assert.Throws<ArgumentException>(() => ApplyRequestedScenes(config, new EditorProjectSceneCatalogService(ProjectRootPath), ["B", unknownSceneId]));

            Assert.Contains(unknownSceneId, error.Message);
            Assert.Equal(new[] { "A" }, config.SelectedSceneIds);
        }

        /// <summary>The normal queue retains platform menu variants and generated startup scenes for a logical menu subset.</summary>
        /// <param name="platformId">Platform passed through the shared queue pipeline.</param>
        /// <param name="expectedScenes">Hand-checked packaged scene order.</param>
        [Theory]
        [InlineData("windows", new string[] { "DemoDiscMainMenu" })]
        [InlineData("ps2", new string[] { "DemoDiscMainMenu" })]
        [InlineData("ds", new string[] { "GeneratedBootScene", "DemoDiscMainMenuHandheld" })]
        [InlineData("3ds", new string[] { "GeneratedBootScene", "DemoDiscMainMenuHandheld" })]
        public void MainMenuSubsetUsesExistingPlatformRules(string platformId, string[] expectedScenes) {
            EditorProjectSceneCatalogService catalog = new EditorProjectSceneCatalogService(ProjectRootPath);
            EditorBuildPlatformConfigDocument config = new EditorBuildPlatformConfigDocument { PlatformId = platformId, SelectedSceneIds = ["A"] };
            ApplyRequestedScenes(config, catalog, ["DemoDiscMainMenu"]);

            EditorBuildQueueItemDocument queue = EditorBuildQueueItemDocument.Create(catalog, config, CreateSelectionModel(platformId), Path.Combine(ProjectRootPath, "output"));

            Assert.Equal(expectedScenes, queue.SelectedSceneIds);
        }

        /// <summary>Applies the same invocation overlay used immediately before CLI queue creation.</summary>
        /// <param name="config">Invocation-only configuration receiving the subset.</param>
        /// <param name="catalog">Real authored scene catalog.</param>
        /// <param name="sceneIds">Explicit scene order.</param>
        static void ApplyRequestedScenes(EditorBuildPlatformConfigDocument config, EditorProjectSceneCatalogService catalog, IReadOnlyList<string> sceneIds) {
            EditorCliBuildPlatformConfigOverlayService.ApplyRequestedScenes(config, catalog, sceneIds);
        }

        /// <summary>Provides the real queue's minimal valid profile metadata without a native build dependency.</summary>
        /// <param name="platformId">Platform whose normal scene policy is exercised.</param>
        /// <returns>Release profile with valid graphics and codegen selections.</returns>
        static EditorPlatformBuildSelectionModel CreateSelectionModel(string platformId) {
            return EditorPlatformBuildSelectionModel.From(new PlatformDefinition(platformId, platformId, [
                new PlatformBuildProfileDefinition("release", "Release", "Release", "graphics", "default", [])
            ], [new PlatformGraphicsProfileDefinition("graphics", "Graphics", "Graphics", [])], [], [], [], [
                new PlatformCodegenProfileDefinition("default", "Default", "Default", PlatformCodegenLanguage.Cpp, PlatformSerializationEndianness.LittleEndian, [])
            ], [], []));
        }
    }
}
