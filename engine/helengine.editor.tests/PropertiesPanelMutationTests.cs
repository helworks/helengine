using System.Reflection;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies properties-panel edits emit scene-mutation notifications.
    /// </summary>
    public class PropertiesPanelMutationTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the panel tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by the properties panel.
        /// </summary>
        public PropertiesPanelMutationTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-properties-panel-mutation-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
            EditorSceneMutationService.Reset();
        }

        /// <summary>
        /// Deletes temporary test content and clears shared mutation subscriptions.
        /// </summary>
        public void Dispose() {
            EditorSceneMutationService.Reset();
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures renaming an entity through the properties panel marks the scene dirty.
        /// </summary>
        [Fact]
        public void UpdateTransformEdits_WhenNameChanges_RaisesSceneMutated() {
            bool raised = false;
            Action handleSceneMutated = () => raised = true;
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = new EditorEntity {
                Name = "Original"
            };

            try {
                EditorSceneMutationService.SceneMutated += handleSceneMutated;
                panel.ShowEntityProperties(entity);

                TextBoxComponent nameField = GetPrivateField<TextBoxComponent>(panel, "NameField");
                nameField.Text = "Renamed";
                SetPrivateField(panel, "ApplyTransformRequested", true);
                InvokePrivate(panel, "UpdateTransformEdits");

                Assert.True(raised);
                Assert.Equal("Renamed", entity.Name);
            } finally {
                EditorSceneMutationService.SceneMutated -= handleSceneMutated;
                EditorSceneMutationService.Reset();
            }
        }

        /// <summary>
        /// Ensures the first visible properties section leaves a small gap below the panel header.
        /// </summary>
        [Fact]
        public void ShowEntityProperties_WhenEntityIsSelected_PositionsTheFirstSectionBelowTheTopEdge() {
            PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
            EditorEntity entity = new EditorEntity {
                Name = "Original"
            };

            panel.ShowEntityProperties(entity);

            EditorEntity transformRoot = GetPrivateField<EditorEntity>(panel, "TransformRoot");

            Assert.True(transformRoot.LocalPosition.Y > 0f);
        }

        /// <summary>
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return Assert.IsType<T>(field.GetValue(target));
        }

        /// <summary>
        /// Assigns one non-public instance field.
        /// </summary>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Name of the field to assign.</param>
        /// <param name="value">Value assigned to the field.</param>
        void SetPrivateField(object target, string fieldName, object value) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field.SetValue(target, value);
        }

        /// <summary>
        /// Invokes one non-public instance method.
        /// </summary>
        /// <param name="target">Target object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        void InvokePrivate(object target, string methodName) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, Array.Empty<object>());
        }

        /// <summary>
        /// Creates a small font asset that can satisfy the layout requirements of the properties panel.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['N'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['M'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['R'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
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
