using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies keyboard-focus behavior for scene-hierarchy rows.
    /// </summary>
    public class SceneHierarchyPanelKeyboardFocusTests : IDisposable {
        /// <summary>
        /// Clears shared editor selection and keyboard-focus state after each test.
        /// </summary>
        public void Dispose() {
            EditorKeyboardFocusService.Reset();
            EditorSelectionService.ClearSelection();
        }

        /// <summary>
        /// Ensures keyboard activation on one hierarchy row selects the represented entity.
        /// </summary>
        [Fact]
        public void SceneHierarchyPanel_WhenRowIsActivated_SelectsTheRepresentedEntity() {
            InitializeCore();
            SceneHierarchyPanel panel = new SceneHierarchyPanel(CreateFont());
            EditorEntity cube = new EditorEntity {
                Name = "Cube"
            };

            panel.RefreshHierarchy();

            List<SceneHierarchyRow> rows = GetPrivateField<List<SceneHierarchyRow>>(panel, "rows");

            rows[0].FocusTarget.ActivateFromKey(Keys.Enter);

            Assert.Same(cube, EditorSelectionService.SelectedEntity);
        }

        /// <summary>
        /// Ensures row relayout reuses pooled focus targets and updates traversal order instead of recreating targets.
        /// </summary>
        [Fact]
        public void SceneHierarchyPanel_WhenRowsAreRelaidOut_ReusesExistingFocusTargetsAndUpdatesTabIndex() {
            InitializeCore();
            SceneHierarchyPanel panel = new SceneHierarchyPanel(CreateFont());
            EditorEntity first = new EditorEntity {
                Name = "First"
            };

            panel.RefreshHierarchy();

            List<SceneHierarchyRow> rows = GetPrivateField<List<SceneHierarchyRow>>(panel, "rows");
            EditorFocusTarget firstTarget = rows[0].FocusTarget;

            EditorEntity second = new EditorEntity {
                Name = "Second"
            };

            panel.RefreshHierarchy();

            rows = GetPrivateField<List<SceneHierarchyRow>>(panel, "rows");

            Assert.Same(firstTarget, rows[0].FocusTarget);
            Assert.Same(first, rows[0].NodeEntity);
            Assert.Same(second, rows[1].NodeEntity);
            Assert.Equal(0, rows[0].FocusTarget.TabIndex);
            Assert.Equal(1, rows[1].FocusTarget.TabIndex);
        }

        /// <summary>
        /// Ensures changing the editor selection updates the matching hierarchy row state and highlight color.
        /// </summary>
        [Fact]
        public void SceneHierarchyPanel_WhenSelectionChanges_MarksTheMatchingRowAsSelected() {
            InitializeCore();
            SceneHierarchyPanel panel = new SceneHierarchyPanel(CreateFont());
            EditorEntity first = new EditorEntity {
                Name = "First"
            };
            EditorEntity second = new EditorEntity {
                Name = "Second"
            };

            panel.RefreshHierarchy();

            EditorSelectionService.SetSelectedEntity(second);

            List<SceneHierarchyRow> rows = GetPrivateField<List<SceneHierarchyRow>>(panel, "rows");

            Assert.Same(first, rows[0].NodeEntity);
            Assert.Same(second, rows[1].NodeEntity);
            Assert.False(rows[0].IsSelected);
            Assert.True(rows[1].IsSelected);
            Assert.Equal(ThemeManager.Colors.AccentSecondary, rows[1].Background.Color);
        }

        /// <summary>
        /// Initializes the core services required by hierarchy keyboard-focus tests.
        /// </summary>
        void InitializeCore() {
            Core core = new Core();
            core.Initialize(null, new TestRenderManager2D(), null);
            EditorKeyboardFocusService.Reset();
            EditorSelectionService.ClearSelection();
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
            if (field == null) {
                throw new InvalidOperationException("Expected private field was not found.");
            }

            object value = field.GetValue(target);
            return Assert.IsType<T>(value);
        }

        /// <summary>
        /// Creates a deterministic font asset for hierarchy row layout.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['C'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['F'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['b'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
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
