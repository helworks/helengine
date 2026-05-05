using System.Reflection;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the editor save-file dialog used for scene saving.
    /// </summary>
    public class SaveFileDialogTests : IDisposable {
        /// <summary>
        /// Temporary project root used by the save dialog.
        /// </summary>
        readonly string ProjectRootPath;

        /// <summary>
        /// Initializes an isolated project root and the core services required by the dialog.
        /// </summary>
        public SaveFileDialogTests() {
            ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-save-file-dialog-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets", "Scenes"));
            EditorInputCaptureService.Reset();

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = ProjectRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Deletes temporary test state after each test.
        /// </summary>
        public void Dispose() {
            EditorInputCaptureService.Reset();
            if (Directory.Exists(ProjectRootPath)) {
                Directory.Delete(ProjectRootPath, true);
            }
        }

        /// <summary>
        /// Ensures valid file names raise a resolved scene save path.
        /// </summary>
        [Fact]
        public void HandleSaveClicked_WhenNameIsValid_RaisesResolvedScenePath() {
            SaveFileDialog dialog = new SaveFileDialog(CreateFont(), ProjectRootPath);
            string raisedPath = string.Empty;
            dialog.SaveRequested += path => raisedPath = path;
            dialog.Show("Scenes", "Prototype");
            dialog.UpdateLayout(1280, 720);

            SetFileName(dialog, "Prototype");
            InvokePrivate(dialog, "HandleSaveClicked");

            Assert.EndsWith(Path.Combine("assets", "Scenes", "Prototype.helen"), raisedPath, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Ensures invalid file names stay visible as dialog validation errors.
        /// </summary>
        [Fact]
        public void HandleSaveClicked_WhenNameIsInvalid_ShowsValidationError() {
            SaveFileDialog dialog = new SaveFileDialog(CreateFont(), ProjectRootPath);
            dialog.Show("Scenes", "Prototype");
            dialog.UpdateLayout(1280, 720);

            SetFileName(dialog, "bad:name");
            InvokePrivate(dialog, "HandleSaveClicked");

            TextComponent statusText = GetPrivateField<TextComponent>(dialog, "StatusText");
            Assert.False(string.IsNullOrWhiteSpace(statusText.Text));
        }

        /// <summary>
        /// Ensures the save-scene dialog uses the modal foreground band for its labels.
        /// </summary>
        [Fact]
        public void Constructor_UsesModalForegroundForDialogLabels() {
            SaveFileDialog dialog = new SaveFileDialog(CreateFont(), ProjectRootPath);

            TextComponent headerText = GetPrivateField<TextComponent>(dialog, "HeaderText");

            Assert.Equal(RenderOrder2D.ModalForeground, headerText.RenderOrder2D);
        }

        /// <summary>
        /// Ensures the backdrop strip ends at the window-button cluster instead of leaving a gap by the minimize button.
        /// </summary>
        [Fact]
        public void UpdateLayout_PositionsBackdropTopFlushToWindowControlCluster() {
            SaveFileDialog dialog = new SaveFileDialog(CreateFont(), ProjectRootPath);
            dialog.Show("Scenes", "Prototype");
            dialog.UpdateLayout(1280, 720);

            SpriteComponent backdropTopSurface = GetPrivateField<SpriteComponent>(dialog, "BackdropTopSurface");

            Assert.Equal(1280 - (EditorDialogBase.CloseButtonWidth * 3), backdropTopSurface.Size.X);
            Assert.Equal(EditorTitleBar.HeightPixels, backdropTopSurface.Size.Y);
        }

        /// <summary>
        /// Ensures the save dialog scales its file-name field, footer buttons, and backdrop height with shared metrics.
        /// </summary>
        [Fact]
        public void UpdateLayout_WithScaledMetrics_UsesScaledFieldAndFooterButtons() {
            EditorUiMetrics metrics = new EditorUiMetrics(1.5d);
            SaveFileDialog dialog = new SaveFileDialog(CreateFont(), metrics, ProjectRootPath);
            dialog.Show("Scenes", "Prototype");
            dialog.UpdateLayout(1280, 720);

            TextBoxComponent fileNameField = GetPrivateField<TextBoxComponent>(dialog, "FileNameField");
            ButtonComponent cancelButton = GetPrivateField<ButtonComponent>(dialog, "CancelButton");
            ButtonComponent saveButton = GetPrivateField<ButtonComponent>(dialog, "SaveButton");
            SpriteComponent backdropTopSurface = GetPrivateField<SpriteComponent>(dialog, "BackdropTopSurface");

            Assert.Equal(new int2(1184, 33), fileNameField.Size);
            Assert.Equal(new int2(132, 33), cancelButton.Size);
            Assert.Equal(new int2(132, 33), saveButton.Size);
            Assert.Equal(54, backdropTopSurface.Size.Y);
        }

        /// <summary>
        /// Assigns the save-file name field through the private dialog field.
        /// </summary>
        /// <param name="dialog">Dialog whose file name should be updated.</param>
        /// <param name="fileName">File name to apply.</param>
        void SetFileName(SaveFileDialog dialog, string fileName) {
            TextBoxComponent field = GetPrivateField<TextBoxComponent>(dialog, "FileNameField");
            field.Text = fileName;
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
        /// Creates a small font asset that can satisfy the layout requirements of the save dialog.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['v'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['F'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['D'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['b'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                [':'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f)
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
