using System.Reflection;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Profiles;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the profiles dialog loads and saves builder-defined build and graphics settings.
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
        /// Ensures the dialog loads the active platform profile into the visible settings rows.
        /// </summary>
        [Fact]
        public void Show_WhenActivePlatformIsProvided_LoadsThatPlatformProfileValues() {
            ProfilesDialog dialog = new ProfilesDialog(CreateFont());
            EditorProfileSettingsDocument document = CreateProfileDocument();

            dialog.Show(document, new List<string> { "windows", "ps2" }, "windows", CreateSelectionModel());

            EditorPlatformSettingsSection buildSection = GetPrivateField<EditorPlatformSettingsSection>(dialog, "BuildSettingsSection");
            EditorPlatformSettingsSection graphicsSection = GetPrivateField<EditorPlatformSettingsSection>(dialog, "GraphicsSettingsSection");
            EditorPlatformSettingsSection codegenSection = GetPrivateField<EditorPlatformSettingsSection>(dialog, "CodegenSettingsSection");

            Assert.Equal(2, buildSection.Items.Count);
            Assert.Equal(4, graphicsSection.Items.Count);
            Assert.Equal(3, codegenSection.Items.Count);
            Assert.Equal("Texture scale %", buildSection.Items[0].LabelText.Text);
            Assert.Equal("50", buildSection.Items[0].TextBox.Text);
            Assert.False(buildSection.Items[1].CheckBox.IsChecked);
            Assert.Equal("Default width", graphicsSection.Items[0].LabelText.Text);
            Assert.Equal("1920", graphicsSection.Items[0].TextBox.Text);
            Assert.Equal("1080", graphicsSection.Items[1].TextBox.Text);
            Assert.False(graphicsSection.Items[2].CheckBox.IsChecked);
            Assert.True(graphicsSection.Items[3].CheckBox.IsChecked);
            Assert.Equal("Write Conversion Report", codegenSection.Items[0].LabelText.Text);
            Assert.True(codegenSection.Items[0].CheckBox.IsChecked);
            Assert.False(codegenSection.Items[1].CheckBox.IsChecked);
            Assert.True(codegenSection.Items[2].CheckBox.IsChecked);
        }

        /// <summary>
        /// Ensures switching the combo box loads the selected platform values.
        /// </summary>
        [Fact]
        public void Show_WhenPlatformSelectionChanges_LoadsTheSelectedPlatformValues() {
            ProfilesDialog dialog = new ProfilesDialog(CreateFont());
            EditorProfileSettingsDocument document = CreateProfileDocument();

            dialog.Show(document, new List<string> { "windows", "ps2" }, "windows", CreateSelectionModel());

            ComboBoxComponent platformComboBox = GetPrivateField<ComboBoxComponent>(dialog, "PlatformComboBox");
            platformComboBox.SelectedIndex = 1;

            EditorPlatformSettingsSection buildSection = GetPrivateField<EditorPlatformSettingsSection>(dialog, "BuildSettingsSection");
            EditorPlatformSettingsSection graphicsSection = GetPrivateField<EditorPlatformSettingsSection>(dialog, "GraphicsSettingsSection");
            EditorPlatformSettingsSection codegenSection = GetPrivateField<EditorPlatformSettingsSection>(dialog, "CodegenSettingsSection");

            Assert.Equal("75", buildSection.Items[0].TextBox.Text);
            Assert.True(buildSection.Items[1].CheckBox.IsChecked);
            Assert.Equal("1280", graphicsSection.Items[0].TextBox.Text);
            Assert.Equal("720", graphicsSection.Items[1].TextBox.Text);
            Assert.True(graphicsSection.Items[2].CheckBox.IsChecked);
            Assert.False(graphicsSection.Items[3].CheckBox.IsChecked);
            Assert.True(codegenSection.Items[0].CheckBox.IsChecked);
            Assert.True(codegenSection.Items[1].CheckBox.IsChecked);
            Assert.False(codegenSection.Items[2].CheckBox.IsChecked);
            Assert.Equal("ps2", platformComboBox.SelectedItem);
        }

        /// <summary>
        /// Ensures the platform selector uses the same modal render-order pattern as other dropdown controls.
        /// </summary>
        [Fact]
        public void Show_WhenPlatformSelectorIsCreated_UsesModalRenderOrdersForTheComboBox() {
            ProfilesDialog dialog = new ProfilesDialog(CreateFont());
            EditorProfileSettingsDocument document = CreateProfileDocument();

            dialog.Show(document, new List<string> { "windows", "ps2" }, "windows", CreateSelectionModel());

            ComboBoxComponent platformComboBox = GetPrivateField<ComboBoxComponent>(dialog, "PlatformComboBox");
            RoundedRectComponent background = GetPrivateField<RoundedRectComponent>(platformComboBox, "background");
            RoundedRectComponent listBackground = GetPrivateField<RoundedRectComponent>(platformComboBox, "listBackground");

            Assert.Equal(RenderOrder2D.PanelSurface, background.RenderOrder2D);
            Assert.Equal(RenderOrder2D.ModalBackground, listBackground.RenderOrder2D);
        }

        /// <summary>
        /// Ensures confirming the dialog raises the current platform and edited document.
        /// </summary>
        [Fact]
        public void HandleSaveClicked_RaisesConfirmedSelectionWithCurrentPlatformAndDocument() {
            ProfilesDialog dialog = new ProfilesDialog(CreateFont());
            EditorProfileSettingsDocument document = CreateProfileDocument();

            dialog.Show(document, new List<string> { "windows", "ps2" }, "windows", CreateSelectionModel());

            EditorPlatformSettingsSection buildSection = GetPrivateField<EditorPlatformSettingsSection>(dialog, "BuildSettingsSection");
            EditorPlatformSettingsSection graphicsSection = GetPrivateField<EditorPlatformSettingsSection>(dialog, "GraphicsSettingsSection");
            EditorPlatformSettingsSection codegenSection = GetPrivateField<EditorPlatformSettingsSection>(dialog, "CodegenSettingsSection");

            buildSection.Items[0].TextBox.Text = "75";
            buildSection.Items[1].CheckBox.IsChecked = false;
            graphicsSection.Items[0].TextBox.Text = "1600";
            graphicsSection.Items[1].TextBox.Text = "900";
            graphicsSection.Items[2].CheckBox.IsChecked = false;
            graphicsSection.Items[3].CheckBox.IsChecked = true;
            codegenSection.Items[0].CheckBox.IsChecked = false;
            codegenSection.Items[1].CheckBox.IsChecked = true;
            codegenSection.Items[2].CheckBox.IsChecked = false;

            ProfilesDialogSelection selection = null;
            dialog.ConfirmRequested += value => selection = value;
            InvokePrivate(dialog, "HandleSaveClicked");

            Assert.NotNull(selection);
            Assert.Equal("windows", selection.ActivePlatformId);
            Assert.Same(document, selection.ProfileSettingsDocument);
            Assert.Equal("75", document.Platforms[0].Build.SelectedOptionValues["texture-scale-percent"]);
            Assert.Equal("False", document.Platforms[0].Build.SelectedOptionValues["shader-variant-pruning"]);
            Assert.Equal("1600", document.Platforms[0].Graphics.SelectedOptionValues["default-width"]);
            Assert.Equal("900", document.Platforms[0].Graphics.SelectedOptionValues["default-height"]);
            Assert.Equal("False", document.Platforms[0].Graphics.SelectedOptionValues["vsync-enabled"]);
            Assert.Equal("True", document.Platforms[0].Graphics.SelectedOptionValues["fullscreen-enabled"]);
            Assert.Equal("False", document.Platforms[0].Codegen.SelectedOptionValues["write-conversion-report"]);
            Assert.Equal("True", document.Platforms[0].Codegen.SelectedOptionValues["include-project-defined-preprocessor-symbols"]);
            Assert.Equal("False", document.Platforms[0].Codegen.SelectedOptionValues["load-native-runtime-metadata"]);
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
                            SelectedBuildProfileId = "debug",
                            SelectedOptionValues = new Dictionary<string, string> {
                                ["texture-scale-percent"] = "50",
                                ["shader-variant-pruning"] = "false"
                            }
                        },
                        Graphics = new EditorGraphicsProfileSettingsDocument {
                            SelectedGraphicsProfileId = "directx11",
                            SelectedOptionValues = new Dictionary<string, string> {
                                ["default-width"] = "1920",
                                ["default-height"] = "1080",
                                ["vsync-enabled"] = "false",
                                ["fullscreen-enabled"] = "true"
                            }
                        },
                        Codegen = new EditorCodegenProfileSettingsDocument {
                            SelectedCodegenProfileId = "default",
                            SelectedOptionValues = new Dictionary<string, string> {
                                ["write-conversion-report"] = "true",
                                ["include-project-defined-preprocessor-symbols"] = "false",
                                ["load-native-runtime-metadata"] = "true"
                            }
                        }
                    },
                    new EditorPlatformProfileSettingsDocument {
                        PlatformId = "ps2",
                        Build = new EditorBuildProfileSettingsDocument {
                            SelectedBuildProfileId = "debug",
                            SelectedOptionValues = new Dictionary<string, string> {
                                ["texture-scale-percent"] = "75",
                                ["shader-variant-pruning"] = "true"
                            }
                        },
                        Graphics = new EditorGraphicsProfileSettingsDocument {
                            SelectedGraphicsProfileId = "directx11",
                            SelectedOptionValues = new Dictionary<string, string> {
                                ["default-width"] = "1280",
                                ["default-height"] = "720",
                                ["vsync-enabled"] = "true",
                                ["fullscreen-enabled"] = "false"
                            }
                        },
                        Codegen = new EditorCodegenProfileSettingsDocument {
                            SelectedCodegenProfileId = "default",
                            SelectedOptionValues = new Dictionary<string, string> {
                                ["write-conversion-report"] = "true",
                                ["include-project-defined-preprocessor-symbols"] = "true",
                                ["load-native-runtime-metadata"] = "false"
                            }
                        }
                    }
                }
            };
        }

        /// <summary>
        /// Creates one builder metadata model with builder-defined build and graphics settings.
        /// </summary>
        /// <returns>Selection model used by the tests.</returns>
        static EditorPlatformBuildSelectionModel CreateSelectionModel() {
            PlatformDefinition definition = new PlatformDefinition(
                "windows",
                "Windows DirectX",
                [
                    new PlatformBuildProfileDefinition(
                        "debug",
                        "Debug",
                        "Debug player build",
                        "directx11",
                        [
                            new PlatformSettingDefinition(
                                "texture-scale-percent",
                                "Texture scale %",
                                PlatformSettingKind.Text,
                                "50",
                                true,
                                []),
                            new PlatformSettingDefinition(
                                "shader-variant-pruning",
                                "Shader variant pruning",
                                PlatformSettingKind.Boolean,
                                "false",
                                false,
                                [])
                        ])
                ],
                [
                    new PlatformGraphicsProfileDefinition(
                        "directx11",
                        "DirectX 11",
                        "Default Windows renderer",
                        [
                            new PlatformSettingDefinition(
                                "default-width",
                                "Default width",
                                PlatformSettingKind.Text,
                                "1920",
                                true,
                                []),
                            new PlatformSettingDefinition(
                                "default-height",
                                "Default height",
                                PlatformSettingKind.Text,
                                "1080",
                                true,
                                []),
                            new PlatformSettingDefinition(
                                "vsync-enabled",
                                "VSync",
                                PlatformSettingKind.Boolean,
                                "false",
                                false,
                                []),
                            new PlatformSettingDefinition(
                                "fullscreen-enabled",
                                "Fullscreen",
                                PlatformSettingKind.Boolean,
                                "true",
                                false,
                                [])
                        ])
                ],
                [
                    new PlatformAssetRequirementDefinition(
                        "texture",
                        "Texture",
                        true,
                        ["png", "tga"])
                ],
                [
                    new PlatformComponentCompatibilityDefinition(
                        "helengine.FPSComponent",
                        PlatformComponentCompatibilityKind.PassThrough,
                        "FPS overlay is canonical on this platform.",
                        string.Empty)
                ],
                [
                    new PlatformCodegenProfileDefinition(
                        "default",
                        "Default",
                        "Default codegen profile",
                        PlatformCodegenLanguage.Cpp,
                        PlatformSerializationEndianness.LittleEndian,
                        [
                            new PlatformSettingDefinition(
                                "write-conversion-report",
                                "Write Conversion Report",
                                PlatformSettingKind.Boolean,
                                "true",
                                true,
                                []),
                            new PlatformSettingDefinition(
                                "include-project-defined-preprocessor-symbols",
                                "Include Project Symbols",
                                PlatformSettingKind.Boolean,
                                "false",
                                true,
                                []),
                            new PlatformSettingDefinition(
                                "load-native-runtime-metadata",
                                "Load Native Runtime Metadata",
                                PlatformSettingKind.Boolean,
                                "true",
                                true,
                                [])
                        ])
                ]);

            return EditorPlatformBuildSelectionModel.From(definition);
        }

        /// <summary>
        /// Creates a small font asset that can satisfy dialog layout.
        /// </summary>
        /// <returns>Font asset with basic glyph metrics for the current test.</returns>
        FontAsset CreateFont() {
            Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>();
            string glyphs = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789% ";
            for (int i = 0; i < glyphs.Length; i++) {
                char glyph = glyphs[i];
                if (characters.ContainsKey(glyph)) {
                    continue;
                }

                double advance = glyph == 'M' || glyph == 'W' || glyph == 'm' || glyph == 'w' ? 11d :
                    glyph == 'I' || glyph == 'i' || glyph == 'l' ? 4d : 8d;
                if (glyph == ' ') {
                    advance = 4d;
                }

                characters[glyph] = new FontChar(new float4(0f, 0f, (float)advance, 12f), 0f, (float)advance, 0f, 0f);
            }

            return new FontAsset(
                new FontInfo("Test", 12, 4f),
                new TestRuntimeTexture {
                    Width = 64,
                    Height = 64
                },
                characters,
                12f,
                64,
                64);
        }
    }
}
