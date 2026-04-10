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
        }

        /// <summary>
        /// Clears generated provider registrations and temporary test content.
        /// </summary>
        public void Dispose() {
            GeneratedAssetProviderRegistry.ResetForTests();
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures generated model picks assign the provider runtime model and keep the selected display label.
        /// </summary>
        [Fact]
        public void HandleModelPicked_WhenEntryIsGenerated_AssignsTheProviderRuntimeModelAndDisplayLabel() {
            TestRuntimeModel runtimeModel = new TestRuntimeModel();
            GeneratedAssetProviderRegistry.Register(new TestGeneratedAssetProvider(
                "engine",
                new[] {
                    AssetBrowserEntry.CreateGeneratedAsset("Cube", "Engine/Models/Cube", AssetEntryKind.Model, "engine", EngineGeneratedModelCache.CubeAssetId)
                },
                runtimeModel));

            MeshComponent meshComponent = new MeshComponent();
            ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));
            view.ShowComponents(CreateEntityWithComponent(meshComponent));

            ComponentPropertyRow modelRow = FindModelRow(view);
            MethodInfo handleModelPicked = typeof(ComponentPropertiesView).GetMethod("HandleModelPicked", BindingFlags.Instance | BindingFlags.NonPublic);
            handleModelPicked.Invoke(view, new object[] {
                modelRow,
                AssetBrowserEntry.CreateGeneratedAsset("Cube", "Engine/Models/Cube", AssetEntryKind.Model, "engine", EngineGeneratedModelCache.CubeAssetId)
            });

            Assert.Same(runtimeModel, meshComponent.Model);
            Assert.Equal("Cube", modelRow.ValueText.Text);
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
