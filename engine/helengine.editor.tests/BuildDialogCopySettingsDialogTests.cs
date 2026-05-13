using System.Reflection;
using helengine;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the compact chooser modal used by the build dialog to pick a source platform.
    /// </summary>
    public sealed class BuildDialogCopySettingsDialogTests : IDisposable {
        /// <summary>
        /// Temporary content root used to isolate the core instance for each test.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by the chooser-modal tests.
        /// </summary>
        public BuildDialogCopySettingsDialogTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-build-dialog-copy-settings-dialog-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(null, new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        }

        /// <summary>
        /// Releases shared editor state and temporary directories after each test.
        /// </summary>
        public void Dispose() {
            EditorInputCaptureService.Reset();

            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the chooser modal selects the first available source platform and raises the confirm request.
        /// </summary>
        [Fact]
        public void Show_WhenSourcePlatformsProvided_UsesFirstSelectionAndConfirmsIt() {
            BuildDialogCopySettingsDialog dialog = new BuildDialogCopySettingsDialog(CreateFont());
            bool confirmed = false;
            string confirmedSourcePlatformId = string.Empty;
            dialog.ConfirmRequested += sourcePlatformId => {
                confirmed = true;
                confirmedSourcePlatformId = sourcePlatformId;
            };

            dialog.Show([
                "windows",
                "linux"
            ]);

            ComboBoxComponent sourceComboBox = GetPrivateField<ComboBoxComponent>(dialog, "SourceComboBox");

            Assert.True(sourceComboBox.HasSelection);
            Assert.Equal("windows", sourceComboBox.SelectedItem);

            InvokePrivate(dialog, "HandleCopyButtonClicked");

            Assert.True(confirmed);
            Assert.Equal("windows", confirmedSourcePlatformId);
        }

        /// <summary>
        /// Ensures scaled metrics resize the chooser content and footer buttons.
        /// </summary>
        [Fact]
        public void Show_WithScaledMetrics_UsesScaledContentAndFooterButtons() {
            EditorUiMetrics metrics = new EditorUiMetrics(1.5d);
            BuildDialogCopySettingsDialog dialog = new BuildDialogCopySettingsDialog(CreateFont(), metrics);

            dialog.Show([
                "windows",
                "linux"
            ]);
            dialog.UpdateLayout(1280, 720);

            EditorEntity sourceLabelHost = GetPrivateField<EditorEntity>(dialog, "SourceLabelHost");
            ComboBoxComponent sourceComboBox = GetPrivateField<ComboBoxComponent>(dialog, "SourceComboBox");
            ButtonComponent copyButton = GetPrivateField<ButtonComponent>(dialog, "CopyButton");
            ButtonComponent cancelButton = GetPrivateField<ButtonComponent>(dialog, "CancelButton");

            Assert.Equal(metrics.ScalePixels(BuildDialogCopySettingsDialog.PanelPadding), (int)Math.Round(sourceLabelHost.LocalPosition.X));
            Assert.Equal(metrics.ScalePixels(BuildDialogCopySettingsDialog.PanelPadding + BuildDialogCopySettingsDialog.HeaderHeight + BuildDialogCopySettingsDialog.SectionSpacing), (int)Math.Round(sourceLabelHost.LocalPosition.Y));
            Assert.Equal(new int2(metrics.ScalePixels(BuildDialogCopySettingsDialog.PanelWidth - (BuildDialogCopySettingsDialog.PanelPadding * 2)), metrics.ScalePixels(BuildDialogCopySettingsDialog.SourceComboHeight)), sourceComboBox.Size);
            Assert.Equal(metrics.ScalePixels(BuildDialogCopySettingsDialog.FooterHeight), copyButton.Size.Y);
            Assert.Equal(metrics.ScalePixels(BuildDialogCopySettingsDialog.FooterHeight), cancelButton.Size.Y);
        }

        /// <summary>
        /// Ensures the chooser positions its visible content immediately during Show without waiting for UpdateLayout.
        /// </summary>
        [Fact]
        public void Show_WhenOpened_PositionsContentImmediately() {
            BuildDialogCopySettingsDialog dialog = new BuildDialogCopySettingsDialog(CreateFont());

            dialog.Show([
                "windows",
                "linux"
            ]);

            EditorEntity sourceLabelHost = GetPrivateField<EditorEntity>(dialog, "SourceLabelHost");
            EditorEntity sourceComboHost = GetPrivateField<EditorEntity>(dialog, "SourceComboHost");
            EditorEntity copyButtonHost = GetPrivateField<EditorEntity>(dialog, "CopyButtonHost");
            EditorEntity cancelButtonHost = GetPrivateField<EditorEntity>(dialog, "CancelButtonHost");

            Assert.NotEqual(float3.Zero, sourceLabelHost.LocalPosition);
            Assert.NotEqual(float3.Zero, sourceComboHost.LocalPosition);
            Assert.NotEqual(float3.Zero, copyButtonHost.LocalPosition);
            Assert.NotEqual(float3.Zero, cancelButtonHost.LocalPosition);
        }

        /// <summary>
        /// Creates one deterministic font asset for modal layout and control tests.
        /// </summary>
        /// <returns>Font asset with stable glyph metrics.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>();
            string glyphs = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789/.:\\-_ []()";

            for (int index = 0; index < glyphs.Length; index++) {
                characters[glyphs[index]] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f);
            }

            return new FontAsset(
                new FontInfo("Test", 16, 4f),
                new TestRuntimeTexture {
                    Width = 128,
                    Height = 128
                },
                characters,
                16f,
                128,
                128);
        }

        /// <summary>
        /// Reads one non-public instance field from the supplied object.
        /// </summary>
        /// <typeparam name="T">Expected field type.</typeparam>
        /// <param name="target">Object that owns the field.</param>
        /// <param name="fieldName">Exact private field name.</param>
        /// <returns>Field value cast to the requested type.</returns>
        T GetPrivateField<T>(object target, string fieldName) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) {
                throw new InvalidOperationException("Missing private field '" + fieldName + "'.");
            }

            object value = field.GetValue(target);
            return Assert.IsType<T>(value);
        }

        /// <summary>
        /// Invokes one non-public instance method on the supplied target object.
        /// </summary>
        /// <param name="target">Object that owns the method.</param>
        /// <param name="methodName">Exact private method name.</param>
        void InvokePrivate(object target, string methodName) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) {
                throw new InvalidOperationException("Missing private method '" + methodName + "'.");
            }

            method.Invoke(target, []);
        }
    }
}
