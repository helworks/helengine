using Xunit;

namespace helengine.editor.tests.managers.asset {
    /// <summary>
    /// Verifies that serialized blueprint assets are registered correctly in content and asset-browser flows.
    /// </summary>
    public class BlueprintAssetBrowserIntegrationTests : IDisposable {
        /// <summary>
        /// Temporary project root used for filesystem-backed blueprint browser tests.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Initializes an isolated project root containing an assets folder.
        /// </summary>
        public BlueprintAssetBrowserIntegrationTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-blueprint-browser-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Blueprints"));
            EditorProjectPaths.Initialize(ProjectRootPath);
        }

        /// <summary>
        /// Deletes temporary filesystem state after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the editor content manager can deserialize blueprint assets by their registered extension.
        /// </summary>
        [Fact]
        public void ConfigureSharedAssetContentManager_WhenBlueprintFileExists_LoadsBlueprintAssetByExtension() {
            string blueprintPath = Path.Combine(ProjectRootPath, "assets", "Blueprints", "Sample" + BlueprintAsset.FileExtension);
            using (FileStream stream = new FileStream(blueprintPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, new BlueprintAsset {
                    Id = "Blueprints/Sample" + BlueprintAsset.FileExtension,
                    RootEntity = new SceneEntityAsset {
                        Id = 1u,
                        Name = "Root",
                        Components = Array.Empty<SceneComponentAssetRecord>(),
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                });
            }

            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(ProjectRootPath));
            EditorContentManagerConfiguration.ConfigureSharedAssetContentManager(contentManager);

            BlueprintAsset loadedAsset = contentManager.Load<BlueprintAsset>(blueprintPath);

            Assert.Equal("Blueprints/Sample" + BlueprintAsset.FileExtension, loadedAsset.Id);
            Assert.Equal("Root", loadedAsset.RootEntity.Name);
        }

        /// <summary>
        /// Ensures blueprint files are classified as blueprint entries in the asset browser.
        /// </summary>
        [Fact]
        public void LoadEntries_WhenBlueprintFileExists_ClassifiesEntryAsBlueprint() {
            string blueprintPath = Path.Combine(ProjectRootPath, "assets", "Blueprints", "Sample" + BlueprintAsset.FileExtension);
            using (FileStream stream = new FileStream(blueprintPath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                AssetSerializer.Serialize(stream, new BlueprintAsset {
                    Id = "Blueprints/Sample" + BlueprintAsset.FileExtension,
                    RootEntity = new SceneEntityAsset {
                        Id = 1u,
                        Name = "Root",
                        Components = Array.Empty<SceneComponentAssetRecord>(),
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                });
            }

            EditorAssetManager manager = new EditorAssetManager(ProjectRootPath);
            List<AssetBrowserEntry> entries = new List<AssetBrowserEntry>();
            Assert.True(manager.TryNavigateTo("Blueprints"));

            manager.LoadEntries(entries);

            AssetBrowserEntry entry = Assert.Single(entries);
            Assert.Equal(AssetEntryKind.Blueprint, entry.EntryKind);
        }

        /// <summary>
        /// Ensures the blueprint file template creates a valid serialized blank blueprint asset.
        /// </summary>
        [Fact]
        public void CreateFile_WhenBlueprintTemplateIsUsed_WritesSerializedBlankBlueprintAsset() {
            Assert.True(EditorFileTemplateRegistry.TryGetTemplate(EditorFileTemplateKind.Blueprint, out EditorFileTemplate template));

            string targetDirectory = Path.Combine(ProjectRootPath, "assets", "Blueprints");
            EditorFileTemplateService.CreateFile(template, targetDirectory);

            string createdPath = Assert.Single(Directory.GetFiles(targetDirectory, "*" + BlueprintAsset.FileExtension));
            using FileStream stream = File.OpenRead(createdPath);
            BlueprintAsset asset = Assert.IsType<BlueprintAsset>(AssetSerializer.Deserialize(stream));

            Assert.NotNull(asset.RootEntity);
            Assert.Equal("Root", asset.RootEntity.Name);
        }
    }
}
