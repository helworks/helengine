using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the focused editor preferences modal used to control global UI scale settings.
    /// </summary>
    public class EditorPreferencesDialogTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the preferences dialog tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by the preferences dialog.
        /// </summary>
        public EditorPreferencesDialogTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-preferences-dialog-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Deletes temporary test state after each test.
        /// </summary>
        public void Dispose() {
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures selecting override mode keeps the explicit percent selector enabled and synchronized with the current settings.
        /// </summary>
        [Fact]
        public void Show_WhenOverrideModeSelected_EnablesOverridePercentSelector() {
            EditorPreferencesDialog dialog = new EditorPreferencesDialog(CreateFont(), new EditorUiMetrics(1d));

            dialog.Show(new EditorUiScaleSettings(EditorUiScaleMode.Override, 150));

            ComboBoxComponent scaleModeComboBox = GetPrivateField<ComboBoxComponent>(dialog, "ScaleModeComboBox");
            ComboBoxComponent scalePercentComboBox = GetPrivateField<ComboBoxComponent>(dialog, "ScalePercentComboBox");
            EditorEntity scalePercentComboBoxHost = GetPrivateField<EditorEntity>(dialog, "ScalePercentComboBoxHost");

            Assert.Equal("Override", scaleModeComboBox.SelectedItem);
            Assert.True(scalePercentComboBoxHost.Enabled);
            Assert.Equal("150%", scalePercentComboBox.SelectedItem);
        }

        /// <summary>
        /// Ensures the preferences dialog routes combo-box drop-down content through the modal overlay render band.
        /// </summary>
        [Fact]
        public void Constructor_WhenComboBoxesAreCreated_UsesModalOverlayRenderOrdersForDropdowns() {
            EditorPreferencesDialog dialog = new EditorPreferencesDialog(CreateFont(), new EditorUiMetrics(1d));

            ComboBoxComponent scaleModeComboBox = GetPrivateField<ComboBoxComponent>(dialog, "ScaleModeComboBox");
            ComboBoxComponent scalePercentComboBox = GetPrivateField<ComboBoxComponent>(dialog, "ScalePercentComboBox");

            Assert.Equal(RenderOrder2D.ModalOverlayBackground, GetPrivateField<byte>(scaleModeComboBox, "listBackgroundOrder"));
            Assert.Equal(RenderOrder2D.ModalOverlayForeground, GetPrivateField<byte>(scaleModeComboBox, "listTextOrder"));
            Assert.Equal(RenderOrder2D.ModalOverlayBackground, GetPrivateField<byte>(scalePercentComboBox, "listBackgroundOrder"));
            Assert.Equal(RenderOrder2D.ModalOverlayForeground, GetPrivateField<byte>(scalePercentComboBox, "listTextOrder"));
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
        /// Creates a small font asset that can satisfy dialog layout requirements.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['A'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['O'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['f'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['v'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['%'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['1'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['5'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
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
