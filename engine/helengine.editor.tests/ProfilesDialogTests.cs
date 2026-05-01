using System.Reflection;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the profiles dialog loads and saves platform-scoped build and graphics settings.
    /// </summary>
    public sealed class ProfilesDialogTests : IDisposable {
        /// <summary>
        /// Temporary content root used by the dialog tests.
        /// </summary>
        readonly string TempRootPath;

        /// <summary>
        /// Initializes the core services required by the dialog tests.
        /// </summary>
        public ProfilesDialogTests() {
            TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-profiles-dialog-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempRootPath);
            EditorInputCaptureService.Reset();

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
        }

        /// <summary>
        /// Deletes temporary test state after each test.
        /// </summary>
        public void Dispose() {
            EditorInputCaptureService.Reset();
            if (Directory.Exists(TempRootPath)) {
                Directory.Delete(TempRootPath, true);
            }
        }

        /// <summary>
        /// Ensures the dialog loads the active platform profile into the visible fields.
        /// </summary>
        [Fact]
        public void Show_WhenActivePlatformIsProvided_LoadsThatPlatformProfileValues() {
            ProfilesDialog dialog = new ProfilesDialog(CreateFont());
            EditorProfileSettingsDocument document = CreateProfileDocument();

            dialog.Show(document, new List<string> { "windows", "ps2" }, "windows");

            TextBoxComponent textureScaleTextBox = GetPrivateField<TextBoxComponent>(dialog, "TextureScaleTextBox");
            CheckBoxComponent shaderPruningCheckBox = GetPrivateField<CheckBoxComponent>(dialog, "ShaderPruningCheckBox");
            TextBoxComponent widthTextBox = GetPrivateField<TextBoxComponent>(dialog, "WidthTextBox");
            TextBoxComponent heightTextBox = GetPrivateField<TextBoxComponent>(dialog, "HeightTextBox");
            CheckBoxComponent vSyncCheckBox = GetPrivateField<CheckBoxComponent>(dialog, "VSyncCheckBox");
            CheckBoxComponent fullscreenCheckBox = GetPrivateField<CheckBoxComponent>(dialog, "FullscreenCheckBox");

            Assert.Equal("50", textureScaleTextBox.Text);
            Assert.False(shaderPruningCheckBox.IsChecked);
            Assert.Equal("1920", widthTextBox.Text);
            Assert.Equal("1080", heightTextBox.Text);
            Assert.False(vSyncCheckBox.IsChecked);
            Assert.True(fullscreenCheckBox.IsChecked);
        }

        /// <summary>
        /// Ensures switching the combo box loads the selected platform values.
        /// </summary>
        [Fact]
        public void Show_WhenPlatformSelectionChanges_LoadsTheSelectedPlatformValues() {
            ProfilesDialog dialog = new ProfilesDialog(CreateFont());
            EditorProfileSettingsDocument document = CreateProfileDocument();

            dialog.Show(document, new List<string> { "windows", "ps2" }, "windows");

            ComboBoxComponent platformComboBox = GetPrivateField<ComboBoxComponent>(dialog, "PlatformComboBox");
            platformComboBox.SelectedIndex = 1;

            TextBoxComponent textureScaleTextBox = GetPrivateField<TextBoxComponent>(dialog, "TextureScaleTextBox");
            TextBoxComponent widthTextBox = GetPrivateField<TextBoxComponent>(dialog, "WidthTextBox");
            TextBoxComponent heightTextBox = GetPrivateField<TextBoxComponent>(dialog, "HeightTextBox");

            Assert.Equal("75", textureScaleTextBox.Text);
            Assert.Equal("1280", widthTextBox.Text);
            Assert.Equal("720", heightTextBox.Text);
            Assert.Equal("ps2", platformComboBox.SelectedItem);
        }

        /// <summary>
        /// Ensures confirming the dialog raises the current platform and edited document.
        /// </summary>
        [Fact]
        public void HandleSaveClicked_RaisesConfirmedSelectionWithCurrentPlatformAndDocument() {
            ProfilesDialog dialog = new ProfilesDialog(CreateFont());
            EditorProfileSettingsDocument document = CreateProfileDocument();

            dialog.Show(document, new List<string> { "windows", "ps2" }, "windows");

            TextBoxComponent textureScaleTextBox = GetPrivateField<TextBoxComponent>(dialog, "TextureScaleTextBox");
            TextBoxComponent widthTextBox = GetPrivateField<TextBoxComponent>(dialog, "WidthTextBox");
            TextBoxComponent heightTextBox = GetPrivateField<TextBoxComponent>(dialog, "HeightTextBox");
            CheckBoxComponent shaderPruningCheckBox = GetPrivateField<CheckBoxComponent>(dialog, "ShaderPruningCheckBox");
            CheckBoxComponent vSyncCheckBox = GetPrivateField<CheckBoxComponent>(dialog, "VSyncCheckBox");
            CheckBoxComponent fullscreenCheckBox = GetPrivateField<CheckBoxComponent>(dialog, "FullscreenCheckBox");

            textureScaleTextBox.Text = "75";
            widthTextBox.Text = "1600";
            heightTextBox.Text = "900";
            shaderPruningCheckBox.IsChecked = false;
            vSyncCheckBox.IsChecked = false;
            fullscreenCheckBox.IsChecked = true;

            ProfilesDialogSelection selection = null;
            dialog.ConfirmRequested += value => selection = value;
            InvokePrivate(dialog, "HandleSaveClicked");

            Assert.NotNull(selection);
            Assert.Equal("windows", selection.ActivePlatformId);
            Assert.Same(document, selection.ProfileSettingsDocument);
            Assert.Equal(75, document.Platforms[0].Build.TextureScalePercent);
            Assert.False(document.Platforms[0].Build.ShaderVariantPruningEnabled);
            Assert.Equal(1600, document.Platforms[0].Graphics.DefaultWidth);
            Assert.Equal(900, document.Platforms[0].Graphics.DefaultHeight);
            Assert.False(document.Platforms[0].Graphics.VSyncEnabled);
            Assert.True(document.Platforms[0].Graphics.FullscreenEnabled);
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
        /// Invokes one non-public instance method.
        /// </summary>
        /// <param name="target">Target object that owns the method.</param>
        /// <param name="methodName">Name of the method to invoke.</param>
        void InvokePrivate(object target, string methodName) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, Array.Empty<object>());
        }

        /// <summary>
        /// Creates a profile document with distinct values for Windows and PS2.
        /// </summary>
        /// <returns>Profile document used by the tests.</returns>
        EditorProfileSettingsDocument CreateProfileDocument() {
            return new EditorProfileSettingsDocument {
                Platforms = new List<EditorPlatformProfileSettingsDocument> {
                    new EditorPlatformProfileSettingsDocument {
                        PlatformId = "windows",
                        Build = new EditorBuildProfileSettingsDocument {
                            TextureScalePercent = 50,
                            ShaderVariantPruningEnabled = false
                        },
                        Graphics = new EditorGraphicsProfileSettingsDocument {
                            DefaultWidth = 1920,
                            DefaultHeight = 1080,
                            VSyncEnabled = false,
                            FullscreenEnabled = true
                        }
                    },
                    new EditorPlatformProfileSettingsDocument {
                        PlatformId = "ps2",
                        Build = new EditorBuildProfileSettingsDocument {
                            TextureScalePercent = 75,
                            ShaderVariantPruningEnabled = true
                        },
                        Graphics = new EditorGraphicsProfileSettingsDocument {
                            DefaultWidth = 1280,
                            DefaultHeight = 720,
                            VSyncEnabled = true,
                            FullscreenEnabled = false
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Creates a small font asset that can satisfy dialog layout.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
                ['0'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['1'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['2'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['5'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['7'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['8'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['9'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['B'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['C'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['D'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['F'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['G'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['H'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['P'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['V'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['T'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
                ['c'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['b'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['f'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['g'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['h'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['i'] = new FontChar(new float4(0f, 0f, 3f, 12f), 0f, 3f, 0f, 0f),
                ['l'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
                ['m'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['n'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['o'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['p'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['r'] = new FontChar(new float4(0f, 0f, 6f, 12f), 0f, 6f, 0f, 0f),
                ['s'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
                ['t'] = new FontChar(new float4(0f, 0f, 5f, 12f), 0f, 5f, 0f, 0f),
                ['u'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['v'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['w'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                ['x'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['y'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
                ['%'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f),
                [' '] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f)
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
