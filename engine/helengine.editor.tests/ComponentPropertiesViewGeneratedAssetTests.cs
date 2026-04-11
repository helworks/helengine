using System.Collections.Generic;
using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies generated model picks in component property rows.
    /// </summary>
    public class ComponentPropertiesViewGeneratedAssetTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the test content manager.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes an isolated content root and the core services required by component property rows.
        /// </summary>
        public ComponentPropertiesViewGeneratedAssetTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-generated-model-picker-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
            EditorSceneMutationService.Reset();
        }

        /// <summary>
        /// Clears generated provider registrations and temporary test content.
        /// </summary>
        public void Dispose() {
            EditorSceneMutationService.Reset();
            GeneratedAssetProviderRegistry.ResetForTests();
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures generated model picks assign the provider runtime model and keep the selected display label.
        /// </summary>
        [Fact]
        public void HandleModelPicked_WhenEntryIsGenerated_AssignsTheProviderRuntimeModelDisplayLabelAndGeneratedReference() {
            TestRuntimeModel runtimeModel = new TestRuntimeModel();
            GeneratedAssetProviderRegistry.Register(new TestGeneratedAssetProvider(
                "engine",
                new[] {
                    AssetBrowserEntry.CreateGeneratedAsset("Cube", "Engine/Models/Cube", AssetEntryKind.Model, "engine", EngineGeneratedModelCache.CubeAssetId)
                },
                runtimeModel));

            MeshComponent meshComponent = new MeshComponent();
            EditorEntity entity = CreateEntityWithComponent(meshComponent);
            ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));
            view.ShowComponents(entity);

            ComponentPropertyRow modelRow = FindModelRow(view);
            MethodInfo handleModelPicked = typeof(ComponentPropertiesView).GetMethod("HandleModelPicked", BindingFlags.Instance | BindingFlags.NonPublic);
            handleModelPicked.Invoke(view, new object[] {
                modelRow,
                AssetBrowserEntry.CreateGeneratedAsset("Cube", "Engine/Models/Cube", AssetEntryKind.Model, "engine", EngineGeneratedModelCache.CubeAssetId)
            });

            Assert.Same(runtimeModel, meshComponent.Model);
            Assert.Equal("Cube", modelRow.ValueText.Text);
            EntitySaveComponent saveComponent = GetSaveComponent(entity);
            Assert.True(saveComponent.TryGetComponentState(meshComponent, out EntityComponentSaveState saveState));
            Assert.True(saveState.TryGetAssetReference("Model", out SceneAssetReference reference));
            Assert.Equal(SceneAssetReferenceSourceKind.Generated, reference.SourceKind);
            Assert.Equal("engine", reference.ProviderId);
            Assert.Equal(EngineGeneratedModelCache.CubeAssetId, reference.AssetId);
        }

        /// <summary>
        /// Ensures generated model picks mark the current scene as mutated.
        /// </summary>
        [Fact]
        public void HandleModelPicked_WhenEntryIsGenerated_RaisesSceneMutated() {
            bool raised = false;
            Action handleSceneMutated = () => raised = true;
            TestRuntimeModel runtimeModel = new TestRuntimeModel();
            GeneratedAssetProviderRegistry.Register(new TestGeneratedAssetProvider(
                "engine",
                new[] {
                    AssetBrowserEntry.CreateGeneratedAsset("Cube", "Engine/Models/Cube", AssetEntryKind.Model, "engine", EngineGeneratedModelCache.CubeAssetId)
                },
                runtimeModel));

            MeshComponent meshComponent = new MeshComponent();
            EditorEntity entity = CreateEntityWithComponent(meshComponent);
            ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));
            view.ShowComponents(entity);

            ComponentPropertyRow modelRow = FindModelRow(view);
            MethodInfo handleModelPicked = typeof(ComponentPropertiesView).GetMethod("HandleModelPicked", BindingFlags.Instance | BindingFlags.NonPublic);

            try {
                EditorSceneMutationService.SceneMutated += handleSceneMutated;

                handleModelPicked.Invoke(view, new object[] {
                    modelRow,
                    AssetBrowserEntry.CreateGeneratedAsset("Cube", "Engine/Models/Cube", AssetEntryKind.Model, "engine", EngineGeneratedModelCache.CubeAssetId)
                });

                Assert.True(raised);
            } finally {
                EditorSceneMutationService.SceneMutated -= handleSceneMutated;
                EditorSceneMutationService.Reset();
            }
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
        /// Retrieves the hidden save component attached to one editor entity.
        /// </summary>
        /// <param name="entity">Entity whose save component should be read.</param>
        /// <returns>Attached hidden save component.</returns>
        EntitySaveComponent GetSaveComponent(EditorEntity entity) {
            return Assert.IsType<EntitySaveComponent>(Assert.Single(entity.Components, value => value is EntitySaveComponent));
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
                ['C'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['b'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
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
