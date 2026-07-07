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
                ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath)
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
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
        /// Ensures the dialog opens on the Build tab while keeping the other profile tabs hidden.
        /// </summary>
        [Fact]
        public void Show_WhenOpened_ActivatesBuildTabAndLoadsTheCurrentPlatformRows() {
            ProfilesDialog dialog = new ProfilesDialog(CreateFont());
            EditorProfileSettingsDocument document = CreateProfileDocument();

            dialog.Show(document, new List<string> { "windows", "ps2" }, "windows", CreateSelectionModel());

            int selectedTabIndex = GetPrivateField<int>(dialog, "SelectedTabIndex");
            EditorEntity buildContentHost = GetPrivateField<EditorEntity>(dialog, "BuildContentHost");
            EditorEntity graphicsContentHost = GetPrivateField<EditorEntity>(dialog, "GraphicsContentHost");
            EditorEntity codegenContentHost = GetPrivateField<EditorEntity>(dialog, "CodegenContentHost");
            EditorPlatformSettingsSection buildSection = GetPrivateField<EditorPlatformSettingsSection>(dialog, "BuildSettingsSection");
            EditorPlatformSettingsSection graphicsSection = GetPrivateField<EditorPlatformSettingsSection>(dialog, "GraphicsSettingsSection");
            EditorPlatformSettingsSection codegenSection = GetPrivateField<EditorPlatformSettingsSection>(dialog, "CodegenSettingsSection");

            Assert.Equal(0, selectedTabIndex);
            Assert.True(buildContentHost.Enabled);
            Assert.False(graphicsContentHost.Enabled);
            Assert.False(codegenContentHost.Enabled);
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
        /// Ensures the selector, tab chrome, and active content hosts are positioned immediately during Show.
        /// </summary>
        [Fact]
        public void Show_WhenOpened_PositionsSelectorTabsAndActiveContentImmediately() {
            ProfilesDialog dialog = new ProfilesDialog(CreateFont());
            EditorProfileSettingsDocument document = CreateProfileDocument();

            dialog.Show(document, new List<string> { "windows", "ps2" }, "windows", CreateSelectionModel());

            EditorEntity platformComboBoxHost = GetPrivateField<EditorEntity>(dialog, "PlatformComboBoxHost");
            EditorEntity buildTabButtonHost = GetPrivateField<EditorEntity>(dialog, "BuildTabButtonHost");
            EditorEntity buildContentHost = GetPrivateField<EditorEntity>(dialog, "BuildContentHost");

            Assert.NotEqual(float3.Zero, platformComboBoxHost.LocalPosition);
            Assert.NotEqual(float3.Zero, buildTabButtonHost.LocalPosition);
            Assert.NotEqual(float3.Zero, buildContentHost.LocalPosition);
        }

        /// <summary>
        /// Ensures switching profile tabs keeps draft edits local until Save is pressed.
        /// </summary>
        [Fact]
        public void Show_WhenTabsChange_KeepsDraftEditsOutOfTheSourceDocumentUntilSave() {
            ProfilesDialog dialog = new ProfilesDialog(CreateFont());
            EditorProfileSettingsDocument document = CreateProfileDocument();

            dialog.Show(document, new List<string> { "windows", "ps2" }, "windows", CreateSelectionModel());

            EditorPlatformSettingsSection buildSection = GetPrivateField<EditorPlatformSettingsSection>(dialog, "BuildSettingsSection");
            EditorPlatformSettingsSection graphicsSection = GetPrivateField<EditorPlatformSettingsSection>(dialog, "GraphicsSettingsSection");
            buildSection.Items[0].TextBox.Text = "75";
            buildSection.Items[1].CheckBox.IsChecked = true;

            InvokePrivate(dialog, "HandleGraphicsTabClicked");

            Assert.Equal(1, GetPrivateField<int>(dialog, "SelectedTabIndex"));
            Assert.Equal("50", document.Platforms[0].Build.SelectedOptionValues["texture-scale-percent"]);
            Assert.Equal("false", document.Platforms[0].Build.SelectedOptionValues["shader-variant-pruning"]);

            graphicsSection.Items[0].TextBox.Text = "1600";
            graphicsSection.Items[1].TextBox.Text = "900";

            InvokePrivate(dialog, "HandleBuildTabClicked");

            Assert.Equal("75", buildSection.Items[0].TextBox.Text);
            Assert.True(buildSection.Items[1].CheckBox.IsChecked);
            Assert.Equal("1920", document.Platforms[0].Graphics.SelectedOptionValues["default-width"]);
            Assert.Equal("1080", document.Platforms[0].Graphics.SelectedOptionValues["default-height"]);
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

            Assert.Equal(RenderOrder2D.ModalBackground, background.RenderOrder2D);
            Assert.Equal(RenderOrder2D.ModalOverlayBackground, listBackground.RenderOrder2D);
        }

        /// <summary>
        /// Ensures scaled metrics resize the platform selector, settings sections, and footer buttons.
        /// </summary>
        [Fact]
        public void Show_WithScaledMetrics_UsesScaledSelectorAndFooterLayout() {
            EditorUiMetrics metrics = new EditorUiMetrics(1.5d);
            ProfilesDialog dialog = new ProfilesDialog(CreateFont(), metrics);
            EditorProfileSettingsDocument document = CreateProfileDocument();

            dialog.Show(document, new List<string> { "windows", "ps2" }, "windows", CreateSelectionModel());
            dialog.UpdateLayout(1280, 720);

            EditorEntity buildTabButtonHost = GetPrivateField<EditorEntity>(dialog, "BuildTabButtonHost");
            EditorEntity buildContentHost = GetPrivateField<EditorEntity>(dialog, "BuildContentHost");
            EditorEntity platformComboBoxHost = GetPrivateField<EditorEntity>(dialog, "PlatformComboBoxHost");
            ComboBoxComponent platformComboBox = GetPrivateField<ComboBoxComponent>(dialog, "PlatformComboBox");
            EditorPlatformSettingsSection buildSettingsSection = GetPrivateField<EditorPlatformSettingsSection>(dialog, "BuildSettingsSection");
            ButtonComponent buildTabButton = GetPrivateField<ButtonComponent>(dialog, "BuildTabButton");
            ButtonComponent saveButton = GetPrivateField<ButtonComponent>(dialog, "SaveButton");
            ButtonComponent cancelButton = GetPrivateField<ButtonComponent>(dialog, "CancelButton");

            Assert.Equal(metrics.ScalePixels(ProfilesDialog.PanelPadding + ProfilesDialog.LabelColumnWidth + 12), (int)Math.Round(platformComboBoxHost.LocalPosition.X));
            Assert.Equal(metrics.ScalePixels(ProfilesDialog.HeaderHeight + ProfilesDialog.PanelPadding), (int)Math.Round(platformComboBoxHost.LocalPosition.Y));
            Assert.Equal(new int2(metrics.ScalePixels(ProfilesDialog.PlatformComboBoxWidth), metrics.ScalePixels(ProfilesDialog.FieldRowHeight)), platformComboBox.Size);
            Assert.Equal(metrics.ScalePixels(ProfilesDialog.PanelPadding), (int)Math.Round(buildTabButtonHost.LocalPosition.X));
            Assert.Equal(metrics.ScalePixels(ProfilesDialog.HeaderHeight + ProfilesDialog.PanelPadding + ProfilesDialog.FieldRowHeight + ProfilesDialog.SectionSpacing), (int)Math.Round(buildTabButtonHost.LocalPosition.Y));
            Assert.Equal(new int2(metrics.ScalePixels(ProfilesDialog.TabButtonWidth), metrics.ScalePixels(ProfilesDialog.TabButtonHeight)), buildTabButton.Size);
            Assert.Equal(metrics.ScalePixels(ProfilesDialog.PanelPadding), (int)Math.Round(buildContentHost.LocalPosition.X));
            Assert.Equal(metrics.ScalePixels(ProfilesDialog.HeaderHeight + ProfilesDialog.PanelPadding + ProfilesDialog.FieldRowHeight + ProfilesDialog.SectionSpacing + ProfilesDialog.TabButtonHeight + ProfilesDialog.TabContentSpacing), (int)Math.Round(buildContentHost.LocalPosition.Y));
            Assert.Equal(0, (int)Math.Round(buildSettingsSection.Root.LocalPosition.Y));
            Assert.Equal(new int2(metrics.ScalePixels(88), metrics.ScalePixels(22)), saveButton.Size);
            Assert.Equal(new int2(metrics.ScalePixels(88), metrics.ScalePixels(22)), cancelButton.Size);
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
            Assert.NotSame(document, selection.ProfileSettingsDocument);
            Assert.Equal("50", document.Platforms[0].Build.SelectedOptionValues["texture-scale-percent"]);
            Assert.Equal("false", document.Platforms[0].Build.SelectedOptionValues["shader-variant-pruning"]);
            Assert.Equal("1920", document.Platforms[0].Graphics.SelectedOptionValues["default-width"]);
            Assert.Equal("1080", document.Platforms[0].Graphics.SelectedOptionValues["default-height"]);
            Assert.Equal("false", document.Platforms[0].Graphics.SelectedOptionValues["vsync-enabled"]);
            Assert.Equal("true", document.Platforms[0].Graphics.SelectedOptionValues["fullscreen-enabled"]);
            Assert.Equal("true", document.Platforms[0].Codegen.SelectedOptionValues["write-conversion-report"]);
            Assert.Equal("false", document.Platforms[0].Codegen.SelectedOptionValues["include-project-defined-preprocessor-symbols"]);
            Assert.Equal("true", document.Platforms[0].Codegen.SelectedOptionValues["load-native-runtime-metadata"]);
            Assert.Equal("75", selection.ProfileSettingsDocument.Platforms[0].Build.SelectedOptionValues["texture-scale-percent"]);
            Assert.Equal("false", selection.ProfileSettingsDocument.Platforms[0].Build.SelectedOptionValues["shader-variant-pruning"]);
            Assert.Equal("1600", selection.ProfileSettingsDocument.Platforms[0].Graphics.SelectedOptionValues["default-width"]);
            Assert.Equal("900", selection.ProfileSettingsDocument.Platforms[0].Graphics.SelectedOptionValues["default-height"]);
            Assert.Equal("false", selection.ProfileSettingsDocument.Platforms[0].Graphics.SelectedOptionValues["vsync-enabled"]);
            Assert.Equal("true", selection.ProfileSettingsDocument.Platforms[0].Graphics.SelectedOptionValues["fullscreen-enabled"]);
            Assert.Equal("false", selection.ProfileSettingsDocument.Platforms[0].Codegen.SelectedOptionValues["write-conversion-report"]);
            Assert.Equal("true", selection.ProfileSettingsDocument.Platforms[0].Codegen.SelectedOptionValues["include-project-defined-preprocessor-symbols"]);
            Assert.Equal("false", selection.ProfileSettingsDocument.Platforms[0].Codegen.SelectedOptionValues["load-native-runtime-metadata"]);
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
                    new PlatformComponentSupportRule(
                        "helengine.FPSComponent",
                        PlatformComponentSupportKind.PassThrough,
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


