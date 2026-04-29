using System.Reflection;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the unsaved-changes confirmation dialog.
    /// </summary>
    public class UnsavedChangesDialogTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the dialog tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by the dialog.
        /// </summary>
        public UnsavedChangesDialogTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-unsaved-changes-dialog-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Deletes temporary project state after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the unsaved-changes dialog raises the Save action.
        /// </summary>
        [Fact]
        public void HandleSaveClicked_RaisesSaveRequested() {
            UnsavedChangesDialog dialog = new UnsavedChangesDialog(CreateFont());
            bool raised = false;
            dialog.SaveRequested += () => raised = true;
            dialog.Show();
            dialog.UpdateLayout(1280, 720);

            InvokePrivate(dialog, "HandleSaveClicked");

            Assert.True(raised);
        }

        /// <summary>
        /// Ensures the unsaved-changes dialog raises the Don't Save action.
        /// </summary>
        [Fact]
        public void HandleDontSaveClicked_RaisesDontSaveRequested() {
            UnsavedChangesDialog dialog = new UnsavedChangesDialog(CreateFont());
            bool raised = false;
            dialog.DontSaveRequested += () => raised = true;
            dialog.Show();
            dialog.UpdateLayout(1280, 720);

            InvokePrivate(dialog, "HandleDontSaveClicked");

            Assert.True(raised);
        }

        /// <summary>
        /// Ensures the unsaved-changes dialog occupies the modal band above overlay menus.
        /// </summary>
        [Fact]
        public void Constructor_UsesModalBand() {
            UnsavedChangesDialog dialog = new UnsavedChangesDialog(CreateFont());
            FieldInfo panelBackgroundField = typeof(UnsavedChangesDialog).GetField("PanelBackground", BindingFlags.Instance | BindingFlags.NonPublic);
            RoundedRectComponent panelBackground = Assert.IsType<RoundedRectComponent>(panelBackgroundField.GetValue(dialog));

            Assert.Equal(RenderOrder2D.ModalBackground, panelBackground.RenderOrder2D);
        }

        /// <summary>
        /// Ensures the dialog header uses a dedicated title-bar color instead of the panel fill color.
        /// </summary>
        [Fact]
        public void Constructor_UsesDistinctHeaderColor() {
            UnsavedChangesDialog dialog = new UnsavedChangesDialog(CreateFont());
            RoundedRectComponent panelBackground = GetPrivateField<RoundedRectComponent>(dialog, "PanelBackground");
            SpriteComponent headerBackground = GetPrivateField<SpriteComponent>(dialog, "HeaderBackground");

            Assert.Equal(ThemeManager.Colors.AccentSecondary, headerBackground.Color);
            Assert.NotEqual(panelBackground.FillColor, headerBackground.Color);
        }

        /// <summary>
        /// Ensures dragging the title bar moves the dialog panel.
        /// </summary>
        [Fact]
        public void HandleHeaderCursor_WhenDragged_MovesPanelPosition() {
            UnsavedChangesDialog dialog = new UnsavedChangesDialog(CreateFont());
            dialog.Show();
            dialog.UpdateLayout(1280, 720);

            EditorEntity panelRoot = GetPrivateField<EditorEntity>(dialog, "PanelRoot");
            float3 initialPosition = panelRoot.Position;

            InvokePrivate(dialog, "HandleHeaderCursor", new int2(16, 16), new int2(0, 0), PointerInteraction.Press);
            InvokePrivate(dialog, "HandleHeaderCursor", new int2(36, 28), new int2(20, 12), PointerInteraction.Hover);
            InvokePrivate(dialog, "HandleHeaderCursor", new int2(36, 28), new int2(0, 0), PointerInteraction.Release);

            Assert.Equal(initialPosition.X + 20, panelRoot.Position.X);
            Assert.Equal(initialPosition.Y + 12, panelRoot.Position.Y);
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
        /// Invokes one non-public instance method with explicit arguments.
        /// </summary>
        /// <param name="target">Target object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        /// <param name="arguments">Arguments forwarded to the method.</param>
        void InvokePrivate(object target, string methodName, params object[] arguments) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, arguments);
        }

        /// <summary>
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Field name to read.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return Assert.IsType<T>(field.GetValue(target));
        }

        /// <summary>
        /// Creates a small font asset that can satisfy the layout requirements of the dialog.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['D'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['v'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['\''] = new FontChar(new float4(0f, 0f, 2f, 12f), 0f, 2f, 0f, 0f),
                [' '] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['w'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['?'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f)
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
