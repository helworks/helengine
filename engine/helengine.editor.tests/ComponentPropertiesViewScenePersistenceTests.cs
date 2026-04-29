using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies filesystem-backed asset picks store stable scene persistence metadata.
    /// </summary>
    public class ComponentPropertiesViewScenePersistenceTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the test content manager.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes an isolated content root and the core services required by component property rows.
        /// </summary>
        public ComponentPropertiesViewScenePersistenceTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-scene-persistence-picker-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempRootPath, "assets", "Models"));

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Deletes temporary test content after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures filesystem-backed model picks store the stable relative path used for future scene saves.
        /// </summary>
        [Fact]
        public void HandleModelPicked_WhenEntryIsFileSystem_StoresAFileSystemSceneAssetReference() {
            string modelPath = Path.Combine(TempRootPath, "assets", "Models", "Ship.obj");
            Directory.CreateDirectory(Path.GetDirectoryName(modelPath));
            File.WriteAllText(modelPath, "raw obj source");

            ContentManager contentManager = new ContentManager(TempRootPath);
            EditorContentManagerConfiguration.ConfigureSharedAssetContentManager(contentManager);
            AssetImportManager assetImportManager = CreateAssetImportManager();
            MeshComponent meshComponent = new MeshComponent();
            EditorEntity entity = CreateEntityWithComponent(meshComponent);
            ComponentPropertiesView view = new ComponentPropertiesView(
                CreateFont(),
                contentManager,
                new EditorFileSystemModelResolver(assetImportManager));
            view.ShowComponents(entity);

            ComponentPropertyRow modelRow = FindModelRow(view);
            MethodInfo handleModelPicked = typeof(ComponentPropertiesView).GetMethod("HandleModelPicked", BindingFlags.Instance | BindingFlags.NonPublic);
            AssetBrowserEntry entry = AssetBrowserEntry.CreateFileSystemFile(
                "Ship.obj",
                "Models/Ship.obj",
                modelPath,
                ".obj",
                AssetEntryKind.Model);

            handleModelPicked.Invoke(view, new object[] { modelRow, entry });

            Assert.NotNull(meshComponent.Model);
            EntitySaveComponent saveComponent = GetSaveComponent(entity);
            Assert.True(saveComponent.TryGetComponentState(meshComponent, out EntityComponentSaveState saveState));
            Assert.True(saveState.TryGetAssetReference("Model", out SceneAssetReference reference));
            Assert.Equal(SceneAssetReferenceSourceKind.FileSystem, reference.SourceKind);
            Assert.Equal("Models/Ship.obj", reference.RelativePath);
        }

        /// <summary>
        /// Creates one asset import manager that resolves raw model source files for the current test project.
        /// </summary>
        /// <returns>Configured asset import manager for `.obj` source files.</returns>
        AssetImportManager CreateAssetImportManager() {
            ContentManager contentManager = new ContentManager(Path.Combine(TempRootPath, "assets"));
            AssetImportManager assetImportManager = new AssetImportManager(TempRootPath, contentManager);
            assetImportManager.RegisterModelImporter(new ModelImporterRegistration("test-model", new TestModelImporter(), new[] { ".obj" }));
            return assetImportManager;
        }

        /// <summary>
        /// Creates one entity and attaches the supplied component so the properties view can inspect it.
        /// </summary>
        /// <param name="component">Component to add to the entity.</param>
        /// <returns>Entity containing the supplied component.</returns>
        EditorEntity CreateEntityWithComponent(Component component) {
            EditorEntity entity = new EditorEntity();
            entity.AddComponent(component);
            return entity;
        }

        /// <summary>
        /// Finds the active model row produced for the mesh component.
        /// </summary>
        /// <param name="view">Properties view whose active rows should be inspected.</param>
        /// <returns>The single model row displayed by the view.</returns>
        ComponentPropertyRow FindModelRow(ComponentPropertiesView view) {
            FieldInfo activeRowsField = typeof(ComponentPropertiesView).GetField("ActiveRows", BindingFlags.Instance | BindingFlags.NonPublic);
            List<ComponentPropertyRow> rows = Assert.IsType<List<ComponentPropertyRow>>(activeRowsField.GetValue(view));
            return Assert.Single(rows, row => row.Kind == ComponentPropertyRowKind.Model);
        }

        /// <summary>
        /// Retrieves the hidden save component attached to one editor entity.
        /// </summary>
        /// <param name="entity">Entity whose save component should be read.</param>
        /// <returns>Attached hidden save component.</returns>
        EntitySaveComponent GetSaveComponent(EditorEntity entity) {
            return Assert.IsType<EntitySaveComponent>(Assert.Single(entity.Components, component => component is EntitySaveComponent));
        }

        /// <summary>
        /// Creates a small font asset that can satisfy the layout requirements of property rows.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['M'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
            };

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                16f,
                64,
                64);
        }
    }
}
