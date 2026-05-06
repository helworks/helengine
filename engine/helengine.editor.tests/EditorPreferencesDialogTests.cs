using System.Reflection;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the focused editor preferences modal used to control editor-global theme and UI scale settings.
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
        /// Ensures the dialog loads the current theme and override selections while keeping the theme selector enabled.
        /// </summary>
        [Fact]
        public void Show_WhenOverrideModeSelected_LoadsThemeAndEnablesOverridePercentSelector() {
            EditorPreferencesDialog dialog = new EditorPreferencesDialog(CreateFont(), new EditorUiMetrics(1d));

            dialog.Show(new EditorPreferencesSettings(
                new EditorUiScaleSettings(EditorUiScaleMode.Override, 150),
                "dark"));

            ComboBoxComponent themeComboBox = GetPrivateField<ComboBoxComponent>(dialog, "ThemeComboBox");
            ComboBoxComponent scaleModeComboBox = GetPrivateField<ComboBoxComponent>(dialog, "ScaleModeComboBox");
            ComboBoxComponent scalePercentComboBox = GetPrivateField<ComboBoxComponent>(dialog, "ScalePercentComboBox");
            EditorEntity themeComboBoxHost = GetPrivateField<EditorEntity>(dialog, "ThemeComboBoxHost");
            EditorEntity scalePercentComboBoxHost = GetPrivateField<EditorEntity>(dialog, "ScalePercentComboBoxHost");

            Assert.True(themeComboBoxHost.Enabled);
            Assert.Equal("Dark", themeComboBox.SelectedItem);
            Assert.Equal("Override", scaleModeComboBox.SelectedItem);
            Assert.True(scalePercentComboBoxHost.Enabled);
            Assert.Equal("150%", scalePercentComboBox.SelectedItem);
        }

        /// <summary>
        /// Ensures the preferences dialog routes all combo-box drop-down content through the modal overlay render band.
        /// </summary>
        [Fact]
        public void Constructor_WhenComboBoxesAreCreated_UsesModalOverlayRenderOrdersForDropdowns() {
            EditorPreferencesDialog dialog = new EditorPreferencesDialog(CreateFont(), new EditorUiMetrics(1d));

            ComboBoxComponent themeComboBox = GetPrivateField<ComboBoxComponent>(dialog, "ThemeComboBox");
            ComboBoxComponent scaleModeComboBox = GetPrivateField<ComboBoxComponent>(dialog, "ScaleModeComboBox");
            ComboBoxComponent scalePercentComboBox = GetPrivateField<ComboBoxComponent>(dialog, "ScalePercentComboBox");

            Assert.Equal(RenderOrder2D.ModalOverlayBackground, GetPrivateField<byte>(themeComboBox, "listBackgroundOrder"));
            Assert.Equal(RenderOrder2D.ModalOverlayForeground, GetPrivateField<byte>(themeComboBox, "listTextOrder"));
            Assert.Equal(RenderOrder2D.ModalOverlayBackground, GetPrivateField<byte>(scaleModeComboBox, "listBackgroundOrder"));
            Assert.Equal(RenderOrder2D.ModalOverlayForeground, GetPrivateField<byte>(scaleModeComboBox, "listTextOrder"));
            Assert.Equal(RenderOrder2D.ModalOverlayBackground, GetPrivateField<byte>(scalePercentComboBox, "listBackgroundOrder"));
            Assert.Equal(RenderOrder2D.ModalOverlayForeground, GetPrivateField<byte>(scalePercentComboBox, "listTextOrder"));
        }

        /// <summary>
        /// Ensures the enlarged preferences dialog positions the new theme controls immediately during Show.
        /// </summary>
        [Fact]
        public void Show_WhenOpened_PositionsThemeControlsImmediatelyAndUsesLargeMinimumSize() {
            EditorPreferencesDialog dialog = new EditorPreferencesDialog(CreateFont(), new EditorUiMetrics(1d));

            dialog.Show(new EditorPreferencesSettings(
                new EditorUiScaleSettings(EditorUiScaleMode.Override, 150),
                EditorThemeCatalog.DefaultThemeId));

            EditorEntity themeComboBoxHost = GetPrivateField<EditorEntity>(dialog, "ThemeComboBoxHost");
            EditorEntity scaleModeComboBoxHost = GetPrivateField<EditorEntity>(dialog, "ScaleModeComboBoxHost");
            EditorEntity applyButtonHost = GetPrivateField<EditorEntity>(dialog, "ApplyButtonHost");

            Assert.Equal(new int2(EditorPreferencesDialog.PanelWidth, EditorPreferencesDialog.PanelHeight), GetDialogMinimumSize(dialog));
            Assert.NotEqual(float3.Zero, themeComboBoxHost.LocalPosition);
            Assert.NotEqual(float3.Zero, scaleModeComboBoxHost.LocalPosition);
            Assert.NotEqual(float3.Zero, applyButtonHost.LocalPosition);
        }

        /// <summary>
        /// Ensures clicking Apply returns one combined editor preferences document with the selected theme and UI scale.
        /// </summary>
        [Fact]
        public void HandleApplyClicked_WhenInvoked_ReturnsCombinedEditorPreferences() {
            EditorPreferencesDialog dialog = new EditorPreferencesDialog(CreateFont(), new EditorUiMetrics(1d));
            EditorPreferencesSettings confirmedSettings = null;
            dialog.ConfirmRequested += settings => confirmedSettings = settings;

            dialog.Show(new EditorPreferencesSettings(
                new EditorUiScaleSettings(EditorUiScaleMode.Auto, 100),
                EditorThemeCatalog.DefaultThemeId));

            ComboBoxComponent themeComboBox = GetPrivateField<ComboBoxComponent>(dialog, "ThemeComboBox");
            ComboBoxComponent scaleModeComboBox = GetPrivateField<ComboBoxComponent>(dialog, "ScaleModeComboBox");
            ComboBoxComponent scalePercentComboBox = GetPrivateField<ComboBoxComponent>(dialog, "ScalePercentComboBox");
            themeComboBox.SetItems(new[] { "Neon 90s", "Dark", "Light" }, 2);
            scaleModeComboBox.SetItems(new[] { "Auto", "Override" }, 1);
            scalePercentComboBox.SetItems(new[] { "75%", "100%", "125%", "150%", "175%", "200%" }, 4);

            InvokePrivate(dialog, "HandleApplyClicked");

            Assert.NotNull(confirmedSettings);
            Assert.Equal("light", confirmedSettings.ThemeId);
            Assert.Equal(EditorUiScaleMode.Override, confirmedSettings.UiScale.Mode);
            Assert.Equal(175, confirmedSettings.UiScale.OverridePercent);
        }

        /// <summary>
        /// Invokes one non-public instance method with the provided arguments.
        /// </summary>
        /// <param name="target">Object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        /// <param name="arguments">Arguments passed to the invoked method.</param>
        void InvokePrivate(object target, string methodName, params object[] arguments) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, arguments);
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
        /// Reads the protected dialog minimum-size property from one dialog instance.
        /// </summary>
        /// <param name="dialog">Dialog whose minimum size should be inspected.</param>
        /// <returns>Current scaled dialog minimum size.</returns>
        int2 GetDialogMinimumSize(EditorDialogBase dialog) {
            PropertyInfo property = typeof(EditorDialogBase).GetProperty("DialogMinimumSize", BindingFlags.Instance | BindingFlags.NonPublic);
            return Assert.IsType<int2>(property.GetValue(dialog));
        }

        /// <summary>
        /// Creates a font asset that can satisfy dialog layout requirements and theme display names.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['A'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['D'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['L'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['M'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['N'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['O'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['T'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['f'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['k'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['v'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['%'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['0'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['1'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['2'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['5'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['7'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['9'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
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
