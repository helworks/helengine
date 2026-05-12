using System.Reflection;
using helengine.baseplatform.Definitions;
using helengine.editor;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.components.ui;

/// <summary>
/// Verifies conditional rendering behavior for the schema-driven material authoring view.
/// </summary>
public sealed class MaterialAssetViewTests : IDisposable {
    /// <summary>
    /// Temporary content root used to initialize the editor runtime services for the view test.
    /// </summary>
    readonly string TempRootPath;

    /// <summary>
    /// Initializes the core services required by the material view.
    /// </summary>
    public MaterialAssetViewTests() {
        TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-material-asset-view-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempRootPath);

        Core core = new Core(new CoreInitializationOptions {
            ContentRootPath = TempRootPath
        });
        core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
    }

    /// <summary>
    /// Deletes the temporary directory used by the current test.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(TempRootPath)) {
            Directory.Delete(TempRootPath, true);
        }
    }

    /// <summary>
    /// Verifies that the material view renders one panel per platform and keeps schema controls inside each panel.
    /// </summary>
    [Fact]
    public void Show_when_multiple_platforms_are_available_renders_separate_panels_for_each_platform() {
        MaterialAssetView view = new MaterialAssetView(CreateFont(), 1);
        string materialPath = Path.Combine(TempRootPath, "Test.helmat");
        File.WriteAllBytes(materialPath, Array.Empty<byte>());

        view.Show(
            AssetBrowserEntry.CreateFileSystemFile("Test", "Materials/Test.helmat", materialPath, ".helmat", AssetEntryKind.Material),
            new MaterialAsset {
                Id = "Materials/Test.helmat"
            },
            CreateSettings(useCustomShader: false),
            ["windows", "linux"],
            "windows",
            platformId => EditorPlatformBuildSelectionModel.From(CreatePlatformDefinition(platformId)));

        PlatformTabStripView tabStrip = GetPrivateField<PlatformTabStripView>(view, "PlatformTabStrip");
        Dictionary<string, MaterialAssetPlatformPanel> panels = GetPrivateField<Dictionary<string, MaterialAssetPlatformPanel>>(view, "PlatformPanels");
        MaterialAssetPlatformPanel windowsPanel = panels["windows"];
        MaterialAssetPlatformPanel linuxPanel = panels["linux"];

        Assert.Equal(2, tabStrip.TabCount);
        Assert.Equal("windows", tabStrip.SelectedPlatformId);
        Assert.Throws<InvalidOperationException>(() => GetPrivateField<object>(view, "PlatformComboBox"));
        Assert.Equal(["Standard Shader"], windowsPanel.SchemaComboBoxControl.Items);
        Assert.Equal(["Standard Shader"], linuxPanel.SchemaComboBoxControl.Items);
        Assert.Equal("Standard Shader", windowsPanel.SchemaComboBoxControl.SelectedItem);
        Assert.Equal("Standard Shader", linuxPanel.SchemaComboBoxControl.SelectedItem);
        Assert.Equal(["use-custom-shader", "texture-id", "casts-shadow", "receives-shadow", "base-color"], windowsPanel.FieldRows.Select(row => row.FieldId).ToArray());
        Assert.Equal(["use-custom-shader", "texture-id", "casts-shadow", "receives-shadow", "base-color"], linuxPanel.FieldRows.Select(row => row.FieldId).ToArray());

        MaterialAssetFieldEditorRow customShaderRow = Assert.Single(windowsPanel.FieldRows, row => row.FieldId == "use-custom-shader");
        Assert.IsType<CheckBoxComponent>(customShaderRow.CheckBox);

        MaterialAssetFieldEditorRow textureRow = Assert.Single(windowsPanel.FieldRows, row => row.FieldId == "texture-id");
        Assert.NotNull(textureRow.Button);

        string requestedExtensionFilter = string.Empty;
        Action<AssetPickerRequest> pickerHandler = request => requestedExtensionFilter = request.ExtensionFilter;
        EditorAssetPickerService.PickRequested += pickerHandler;

        try {
            textureRow.Button.ActivateFromKey(Keys.Enter);
        } finally {
            EditorAssetPickerService.PickRequested -= pickerHandler;
        }

        Assert.Equal(string.Join(";", TextureImportFormatCatalog.AllTextureExtensions), requestedExtensionFilter);

        InvokePrivate(tabStrip, "HandleTabFocusChanged", "linux", true);

        Assert.Equal("linux", tabStrip.SelectedPlatformId);
        Assert.True(linuxPanel.Root.Enabled);
        Assert.False(windowsPanel.Root.Enabled);
        Assert.Equal(["use-custom-shader", "texture-id", "casts-shadow", "receives-shadow", "base-color"], windowsPanel.FieldRows.Select(row => row.FieldId).ToArray());
        Assert.Equal(["use-custom-shader", "texture-id", "casts-shadow", "receives-shadow", "base-color"], linuxPanel.FieldRows.Select(row => row.FieldId).ToArray());
    }

    /// <summary>
    /// Verifies that the schema selector row uses a wide split so the combo box does not overlap the label text.
    /// </summary>
    [Fact]
    public void UpdateLayout_when_schema_row_is_laid_out_uses_a_forty_sixty_split() {
        MaterialAssetPlatformPanel panel = new MaterialAssetPlatformPanel("windows", CreateFont(), 1, RenderOrder2D.PanelForeground);

        panel.UpdateLayout(0, 0, 200);

        EditorEntity schemaLabelHost = GetPrivateField<EditorEntity>(panel, "SchemaLabelHost");
        TextComponent schemaLabelText = GetPrivateField<TextComponent>(panel, "SchemaLabelText");
        EditorEntity schemaComboHost = GetPrivateField<EditorEntity>(panel, "SchemaComboHost");

        Assert.Equal(0f, schemaLabelHost.Position.X);
        Assert.Equal(80, schemaLabelText.Size.X);
        Assert.Equal(88f, schemaComboHost.Position.X);
        Assert.Equal(112, panel.SchemaComboBoxControl.Size.X);
    }

    /// <summary>
    /// Verifies that the shared color picker is hosted outside the scrollable material editor tree and rendered on the shared overlay layer.
    /// </summary>
    [Fact]
    public void Show_when_color_picker_is_requested_hosts_the_overlay_under_the_modal_root() {
        EditorEntity modalHost = new EditorEntity {
            LayerMask = 1
        };
        MaterialAssetView view = new MaterialAssetView(CreateFont(), 1, modalHost);
        string materialPath = Path.Combine(TempRootPath, "Test.helmat");
        File.WriteAllBytes(materialPath, Array.Empty<byte>());

        view.Show(
            AssetBrowserEntry.CreateFileSystemFile("Test", "Materials/Test.helmat", materialPath, ".helmat", AssetEntryKind.Material),
            new MaterialAsset {
                Id = "Materials/Test.helmat"
            },
            CreateSettings(useCustomShader: false),
            ["windows"],
            "windows",
            platformId => EditorPlatformBuildSelectionModel.From(CreatePlatformDefinition(platformId)));

        Dictionary<string, MaterialAssetPlatformPanel> panels = GetPrivateField<Dictionary<string, MaterialAssetPlatformPanel>>(view, "PlatformPanels");
        MaterialAssetPlatformPanel windowsPanel = panels["windows"];
        MaterialAssetFieldEditorRow colorRow = Assert.Single(windowsPanel.FieldRows, row => row.FieldId == "base-color");
        InteractableComponent interactable = GetPrivateField<InteractableComponent>(colorRow.ColorControl.SwatchButtonControl, "interactableComponent");
        interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Hover);
        interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Press);
        interactable.OnCursor(new int2(4, 4), new int2(0, 0), PointerInteraction.Release);

        EditorColorPickerOverlayComponent overlay = Assert.Single(modalHost.Children.OfType<EditorColorPickerOverlayComponent>());

        Assert.True(overlay.IsOpen);
        Assert.Same(modalHost, overlay.Parent);
        Assert.IsAssignableFrom<EditorDialogBase>(overlay);
        Assert.Equal(EditorLayerMasks.EditorModalUi, overlay.LayerMask);
        Assert.NotNull(overlay.HueWheelControl);
        Assert.NotNull(overlay.SaturationValueTriangleControl);
        Assert.NotNull(overlay.AlphaSliderControl);
        Assert.NotNull(overlay.HexTextBoxControl);
    }

    /// <summary>
    /// Verifies that disabling custom shader mode does not leave disposed row interactables in the pointer hit list.
    /// </summary>
    [Fact]
    public void Show_when_custom_shader_is_disabled_does_not_leave_stale_interactables() {
        MaterialAssetView view = new MaterialAssetView(CreateFont(), 1);
        string materialPath = Path.Combine(TempRootPath, "Test.helmat");
        File.WriteAllBytes(materialPath, Array.Empty<byte>());

        view.Show(
            AssetBrowserEntry.CreateFileSystemFile("Test", "Materials/Test.helmat", materialPath, ".helmat", AssetEntryKind.Material),
            new MaterialAsset {
                Id = "Materials/Test.helmat"
            },
            CreateSettings(useCustomShader: true),
            ["windows"],
            "windows",
            platformId => EditorPlatformBuildSelectionModel.From(CreatePlatformDefinition(platformId)));

        Dictionary<string, MaterialAssetPlatformPanel> panels = GetPrivateField<Dictionary<string, MaterialAssetPlatformPanel>>(view, "PlatformPanels");
        MaterialAssetPlatformPanel windowsPanel = panels["windows"];
        MaterialAssetFieldEditorRow customShaderRow = Assert.Single(windowsPanel.FieldRows, row => row.FieldId == "use-custom-shader");
        CheckBoxComponent checkBox = Assert.IsType<CheckBoxComponent>(customShaderRow.CheckBox);
        InteractableComponent interactable = GetPrivateField<InteractableComponent>(checkBox, "Interactable");

        InvokePrivate(checkBox, "SetCheckedState", false, true);

        Assert.DoesNotContain(Core.Instance.ObjectManager.Interactables, candidate => candidate.Parent == null);
        Assert.DoesNotContain(Core.Instance.ObjectManager.Interactables, candidate => ReferenceEquals(candidate, interactable) && candidate.Parent == null);
    }

    /// <summary>
    /// Creates a compact test font that satisfies the editor widgets used by the material view.
    /// </summary>
    /// <returns>Font asset with basic glyph coverage.</returns>
    static FontAsset CreateFont() {
        Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
            ['A'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
            ['C'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['D'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['E'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['F'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['H'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
            ['I'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
            ['L'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['M'] = new FontChar(new float4(0f, 0f, 11f, 12f), 0f, 11f, 0f, 0f),
            ['P'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['S'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['T'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['V'] = new FontChar(new float4(0f, 0f, 9f, 12f), 0f, 9f, 0f, 0f),
            ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['b'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['c'] = new FontChar(new float4(0f, 0f, 7f, 12f), 0f, 7f, 0f, 0f),
            ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
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
            ['w'] = new FontChar(new float4(0f, 0f, 10f, 12f), 0f, 10f, 0f, 0f)
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

    /// <summary>
    /// Creates one material settings payload that matches the standard-shader schema used by the Windows builder.
    /// </summary>
    /// <param name="useCustomShader">True when the custom-shader toggle should be enabled.</param>
    /// <returns>Material import settings for the active platform.</returns>
    static MaterialAssetImportSettings CreateSettings(bool useCustomShader) {
        MaterialAssetImportSettings settings = new MaterialAssetImportSettings();
        settings.Processor.Platforms["windows"] = new MaterialAssetProcessorSettings {
            SchemaId = "standard-shader"
        };
        settings.Processor.Platforms["linux"] = new MaterialAssetProcessorSettings {
            SchemaId = "standard-shader"
        };
        settings.Processor.Platforms["windows"].FieldValues["use-custom-shader"] = useCustomShader ? "true" : "false";
        settings.Processor.Platforms["windows"].FieldValues["texture-id"] = "textures/diffuse.png";
        settings.Processor.Platforms["windows"].FieldValues["casts-shadow"] = "true";
        settings.Processor.Platforms["windows"].FieldValues["receives-shadow"] = "true";
        settings.Processor.Platforms["windows"].FieldValues["base-color"] = "#ffffff";
        settings.Processor.Platforms["linux"].FieldValues["use-custom-shader"] = "false";
        settings.Processor.Platforms["linux"].FieldValues["texture-id"] = "textures/diffuse.png";
        settings.Processor.Platforms["linux"].FieldValues["casts-shadow"] = "true";
        settings.Processor.Platforms["linux"].FieldValues["receives-shadow"] = "true";
        settings.Processor.Platforms["linux"].FieldValues["base-color"] = "#ffffff";
        return settings;
    }

    /// <summary>
    /// Creates one minimal platform definition that publishes the material schema under test.
    /// </summary>
    /// <returns>Platform definition used by the material view test.</returns>
    static PlatformDefinition CreatePlatformDefinition(string platformId) {
        return new PlatformDefinition(
            platformId,
            string.Equals(platformId, "linux", StringComparison.OrdinalIgnoreCase) ? "Linux" : "Windows",
            [],
            [
                new PlatformGraphicsProfileDefinition(
                    "directx11",
                    "DirectX 11",
                    "Windows renderer",
                    [])
            ],
            [],
            [
                new PlatformMaterialSchemaDefinition(
                    "standard-shader",
                    "Standard Shader",
                    ["directx11"],
                    [
                        new PlatformMaterialFieldDefinition(
                            "use-custom-shader",
                            "Use Custom Shader",
                            PlatformMaterialFieldKind.Boolean,
                            "false",
                            true,
                            []),
                        new PlatformMaterialFieldDefinition(
                            "shader-asset-id",
                            "Shader Asset",
                            PlatformMaterialFieldKind.AssetReference,
                            string.Empty,
                            true,
                            []),
                        new PlatformMaterialFieldDefinition(
                            "texture-id",
                            "Texture",
                            PlatformMaterialFieldKind.AssetReference,
                            string.Empty,
                            true,
                            []),
                        new PlatformMaterialFieldDefinition(
                            "casts-shadow",
                            "Casts Shadow",
                            PlatformMaterialFieldKind.Boolean,
                            "true",
                            true,
                            []),
                        new PlatformMaterialFieldDefinition(
                            "receives-shadow",
                            "Receives Shadow",
                            PlatformMaterialFieldKind.Boolean,
                            "true",
                            true,
                            []),
                        new PlatformMaterialFieldDefinition(
                            "vertex-program",
                            "Vertex Program",
                            PlatformMaterialFieldKind.Text,
                            string.Empty,
                            true,
                            []),
                        new PlatformMaterialFieldDefinition(
                            "pixel-program",
                            "Pixel Program",
                            PlatformMaterialFieldKind.Text,
                            string.Empty,
                            true,
                            []),
                        new PlatformMaterialFieldDefinition(
                            "base-color",
                            "Base Color",
                            PlatformMaterialFieldKind.Color,
                            "#ffffff",
                            false,
                            [])
                    ])
            ],
            [],
            [],
            [],
            []);

    }

    /// <summary>
    /// Reads one private field from an object instance.
    /// </summary>
    /// <typeparam name="T">Expected field type.</typeparam>
    /// <param name="target">Object instance whose private field should be read.</param>
    /// <param name="fieldName">Private field name.</param>
    /// <returns>Resolved field value.</returns>
    static T GetPrivateField<T>(object target, string fieldName) {
        if (target == null) {
            throw new ArgumentNullException(nameof(target));
        } else if (string.IsNullOrWhiteSpace(fieldName)) {
            throw new ArgumentException("Field name must be provided.", nameof(fieldName));
        }

        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Field '{fieldName}' was not found on '{target.GetType().FullName}'.");
        return (T)field.GetValue(target);
    }

    /// <summary>
    /// Invokes one private method on an object instance.
    /// </summary>
    /// <param name="target">Object instance whose private method should be invoked.</param>
    /// <param name="methodName">Private method name.</param>
    /// <param name="arguments">Method arguments.</param>
    static void InvokePrivate(object target, string methodName, params object[] arguments) {
        if (target == null) {
            throw new ArgumentNullException(nameof(target));
        } else if (string.IsNullOrWhiteSpace(methodName)) {
            throw new ArgumentException("Method name must be provided.", nameof(methodName));
        }

        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Method '{methodName}' was not found on '{target.GetType().FullName}'.");
        method.Invoke(target, arguments);
    }
}
