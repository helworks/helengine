using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies metadata-driven reflected inspector behavior.
    /// </summary>
    public class ComponentPropertiesViewDynamicInspectorTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the test content manager.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes core services required by component property rows.
        /// </summary>
        public ComponentPropertiesViewDynamicInspectorTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-dynamic-inspector-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
            EditorSceneMutationService.Reset();
        }

        /// <summary>
        /// Cleans temporary test content.
        /// </summary>
        public void Dispose() {
            EditorSceneMutationService.Reset();
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures metadata hides unsupported Camera runtime properties from the default inspector.
        /// </summary>
        [Fact]
        public void ShowComponents_WhenInspectingCamera_HidesRuntimeAndUnsupportedProperties() {
            CameraComponent camera = new CameraComponent();
            EditorEntity entity = new EditorEntity();
            entity.AddComponent(camera);

            ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));
            view.ShowComponents(entity);

            List<ComponentPropertyRow> rows = GetActiveRows(view);

            Assert.Contains(rows, row => string.Equals(row.Label.Text, "Draw Order", StringComparison.Ordinal));
            Assert.Contains(rows, row => string.Equals(row.Label.Text, "Layer Mask", StringComparison.Ordinal));
            Assert.Contains(rows, row => string.Equals(row.Label.Text, "Near Plane Distance", StringComparison.Ordinal));
            Assert.Contains(rows, row => string.Equals(row.Label.Text, "Far Plane Distance", StringComparison.Ordinal));

            Assert.DoesNotContain(rows, row => string.Equals(row.Label.Text, "ClearSettings", StringComparison.Ordinal));
            Assert.DoesNotContain(rows, row => string.Equals(row.Label.Text, "RenderSettings", StringComparison.Ordinal));
            Assert.DoesNotContain(rows, row => string.Equals(row.Label.Text, "RenderQueue2D", StringComparison.Ordinal));
            Assert.DoesNotContain(rows, row => string.Equals(row.Label.Text, "RenderQueue3D", StringComparison.Ordinal));
            Assert.DoesNotContain(rows, row => string.Equals(row.Label.Text, "RenderTarget", StringComparison.Ordinal));
        }

        /// <summary>
        /// Ensures metadata ordering controls the rendered Camera row order.
        /// </summary>
        [Fact]
        public void ShowComponents_WhenInspectingCamera_UsesMetadataOrderForRows() {
            CameraComponent camera = new CameraComponent();
            EditorEntity entity = new EditorEntity();
            entity.AddComponent(camera);

            ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));
            view.ShowComponents(entity);

            List<ComponentPropertyRow> rows = GetActiveRows(view);
            Assert.Collection(
                rows,
                row => Assert.Equal("Draw Order", row.Label.Text),
                row => Assert.Equal("Layer Mask", row.Label.Text),
                row => Assert.Equal("Near Plane Distance", row.Label.Text),
                row => Assert.Equal("Far Plane Distance", row.Label.Text));
        }

        /// <summary>
        /// Ensures unsupported complex properties are excluded instead of falling back to noisy read-only rows.
        /// </summary>
        [Fact]
        public void ShowComponents_WhenInspectingCamera_DoesNotCreateReadOnlyFallbackRowsForUnsupportedProperties() {
            CameraComponent camera = new CameraComponent();
            EditorEntity entity = new EditorEntity();
            entity.AddComponent(camera);

            ComponentPropertiesView view = new ComponentPropertiesView(CreateFont(), new ContentManager(TempRootPath));
            view.ShowComponents(entity);

            List<ComponentPropertyRow> rows = GetActiveRows(view);
            Assert.DoesNotContain(rows, row => row.Kind == ComponentPropertyRowKind.ReadOnly);
        }

        /// <summary>
        /// Reads the active rows from the reflected properties view.
        /// </summary>
        /// <param name="view">Properties view under test.</param>
        /// <returns>List of active rows.</returns>
        List<ComponentPropertyRow> GetActiveRows(ComponentPropertiesView view) {
            FieldInfo activeRowsField = typeof(ComponentPropertiesView).GetField("ActiveRows", BindingFlags.Instance | BindingFlags.NonPublic);
            return Assert.IsType<List<ComponentPropertyRow>>(activeRowsField.GetValue(view));
        }

        /// <summary>
        /// Creates a small font asset that can satisfy layout requirements for property rows.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['A'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['D'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['F'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['L'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['M'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['N'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['O'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['k'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['w'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                [' '] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f)
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
