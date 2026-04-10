using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the hidden editor save component attached to user-authored editor entities.
    /// </summary>
    public class EntitySaveComponentTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the properties view content manager.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by the component properties view.
        /// </summary>
        public EntitySaveComponentTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-entity-save-component-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

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
        /// Ensures editor entities receive one hidden save component automatically.
        /// </summary>
        [Fact]
        public void EditorEntity_WhenConstructed_AttachesEntitySaveComponent() {
            EditorEntity entity = new EditorEntity();

            Assert.Contains(entity.Components, component => component is EntitySaveComponent);
        }

        /// <summary>
        /// Ensures hidden editor-only components do not surface in the properties panel UI.
        /// </summary>
        [Fact]
        public void ShowComponents_WhenEntityContainsHiddenSaveComponent_DoesNotShowItInThePropertiesView() {
            EditorEntity entity = new EditorEntity();
            entity.AddComponent(new MeshComponent());
            ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));

            view.ShowComponents(entity);

            List<ComponentPropertyRow> rows = GetActiveRows(view);
            Assert.DoesNotContain(rows, row => string.Equals(row.Label.Text, nameof(EntitySaveComponent), StringComparison.Ordinal));
        }

        /// <summary>
        /// Reads the active component rows from the properties view.
        /// </summary>
        /// <param name="view">View whose active rows should be inspected.</param>
        /// <returns>Active component property rows.</returns>
        List<ComponentPropertyRow> GetActiveRows(ComponentPropertiesView view) {
            FieldInfo field = typeof(ComponentPropertiesView).GetField("ActiveRows", BindingFlags.Instance | BindingFlags.NonPublic);
            return Assert.IsType<List<ComponentPropertyRow>>(field.GetValue(view));
        }

        /// <summary>
        /// Creates a small font asset that can satisfy the layout requirements of property rows.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['E'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['v'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['C'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['M'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
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
