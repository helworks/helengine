using System.Reflection;
using helengine.baseplatform.Definitions;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.components.ui;

/// <summary>
/// Verifies pointer routing remains stable while the material editor rebuilds rows in response to custom shader changes.
/// </summary>
public sealed class MaterialAssetViewPointerInteractionTests : IDisposable {
    /// <summary>
    /// Temporary root used by the isolated pointer interaction test.
    /// </summary>
    readonly string TempRootPath;

    /// <summary>
    /// Configurable input backend used to drive pointer updates in the test.
    /// </summary>
    readonly TestInputBackend Input;

    /// <summary>
    /// Initializes the core services required by the pointer interaction test.
    /// </summary>
    public MaterialAssetViewPointerInteractionTests() {
        TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-material-asset-pointer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempRootPath);

        Core core = new Core(new CoreInitializationOptions {
            ContentStreamSource = new HostFileSystemContentStreamSource(TempRootPath)
        });
        Input = new TestInputBackend();
        core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), Input, new PlatformInfo("test", "test-version"));
    }

    /// <summary>
    /// <summary>
    /// Deletes the temporary directory used by the current test.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(TempRootPath)) {
            Directory.Delete(TempRootPath, true);
        }
    }

    /// <summary>
    /// Ensures disabling custom shader mode while the pointer is over the checkbox does not leave stale interactables behind.
    /// </summary>
    [Fact]
    public void Show_when_custom_shader_is_disabled_through_pointer_updates_stays_on_life_interactables() {
        CreateUiCamera(1280, 720);

        MaterialAssetView view = new MaterialAssetView(CreateFont(), 1);
        string materialPath = Path.Combine(TempRootPath, "Test.hasset");
        File.WriteAllBytes(materialPath, Array.Empty<byte>());

        view.Show(
            AssetBrowserEntry.CreateFileSystemFile("Test", "Materials/Test.hasset", materialPath, ".hasset", AssetEntryKind.Material),
            new ShaderMaterialAsset {
                Id = "Materials/Test.hasset"
            },
            CreateSettings(useCustomShader: true),
            ["windows"],
            "windows",
            platformId => EditorPlatformBuildSelectionModel.From(CreatePlatformDefinition(platformId)));
        view.UpdateLayout(0, 0, 400);

        Dictionary<string, MaterialAssetPlatformPanel> panels = GetPrivateField<Dictionary<string, MaterialAssetPlatformPanel>>(view, "PlatformPanels");
        MaterialAssetPlatformPanel panel = panels["windows"];
        MaterialAssetFieldEditorRow customShaderRow = Assert.Single(panel.FieldRows, row => row.FieldId == "use-custom-shader");
        CheckBoxComponent checkBox = Assert.IsType<CheckBoxComponent>(customShaderRow.CheckBox);
        InteractableComponent interactable = GetPrivateField<InteractableComponent>(checkBox, "Interactable");
        int2 pointer = new int2((int)interactable.Parent.Position.X + 2, (int)interactable.Parent.Position.Y + 2);

        Input.SetMouseState(CreateMouseState(pointer.X, pointer.Y, ButtonState.Released));
        Input.EarlyUpdate();
        Input.Update();

        Input.SetMouseState(CreateMouseState(pointer.X, pointer.Y, ButtonState.Pressed));
        Input.EarlyUpdate();
        Input.Update();

        Input.SetMouseState(CreateMouseState(pointer.X, pointer.Y, ButtonState.Released));
        Input.EarlyUpdate();
        Input.Update();

        Input.SetMouseState(CreateMouseState(pointer.X, pointer.Y, ButtonState.Released));
        Input.EarlyUpdate();
        Input.Update();

        Assert.DoesNotContain(Core.Instance.ObjectManager.Interactables, candidate => candidate.Parent == null);
    }

    /// <summary>
    /// Ensures the shared color picker accepts pointer input on the hue wheel ring.
    /// </summary>
    [Fact]
    public void Show_when_color_picker_is_opened_allows_pointer_input_on_the_hue_wheel() {
        MaterialAssetView view = new MaterialAssetView(CreateFont(), 1);
        EditorColorPickerOverlayComponent overlay = GetPrivateField<EditorColorPickerOverlayComponent>(view, "ColorPickerOverlay");
        overlay.SetAnchorPosition(80f, 80f, 24);
        overlay.Open(new byte4(0, 0, 255, 255));

        overlay.HueWheelControl.Interactable.OnCursor(new int2(216, 112), new int2(0, 0), PointerInteraction.Press);
        overlay.HueWheelControl.Interactable.OnCursor(new int2(216, 112), new int2(0, 0), PointerInteraction.Release);

        Assert.Equal("#ff0100", overlay.HexTextBoxControl.Text);
    }

    /// <summary>
    /// Creates one UI camera used to route pointer input in the test.
    /// </summary>
    /// <param name="width">Viewport width in pixels.</param>
    /// <param name="height">Viewport height in pixels.</param>
    void CreateUiCamera(int width, int height) {
        EditorEntity cameraEntity = new EditorEntity {
            InternalEntity = true,
            LayerMask = 1
        };

        CameraComponent camera = new CameraComponent {
            LayerMask = 1,
            CameraDrawOrder = 255,
            Viewport = new float4(0f, 0f, width, height)
        };
        cameraEntity.AddComponent(camera);
    }

    /// <summary>
    /// Creates a compact test font that satisfies the editor widgets used by the material view.
    /// </summary>
    /// <returns>Font asset with basic glyph coverage.</returns>
    static FontAsset CreateFont() {
        Dictionary<char, FontChar> characters = new Dictionary<char, FontChar> {
            ['0'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['1'] = new FontChar(new float4(0f, 0f, 4f, 12f), 0f, 4f, 0f, 0f),
            ['2'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['3'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['4'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['5'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['6'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['7'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['8'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['9'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['#'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['a'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['b'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['c'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['d'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['e'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f),
            ['f'] = new FontChar(new float4(0f, 0f, 8f, 12f), 0f, 8f, 0f, 0f)
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
        settings.Processor.Platforms["windows"].FieldValues["use-custom-shader"] = useCustomShader ? "true" : "false";
        settings.Processor.Platforms["windows"].FieldValues["texture-id"] = "textures/diffuse.png";
        settings.Processor.Platforms["windows"].FieldValues["casts-shadow"] = "true";
        settings.Processor.Platforms["windows"].FieldValues["receives-shadow"] = "true";
        settings.Processor.Platforms["windows"].FieldValues["base-color"] = "#ffffff";
        return settings;
    }

    /// <summary>
    /// Creates one minimal platform definition that publishes the material schema under test.
    /// </summary>
    /// <param name="platformId">Platform identifier to publish.</param>
    /// <returns>Platform definition used by the material view test.</returns>
    static PlatformDefinition CreatePlatformDefinition(string platformId) {
        return new PlatformDefinition(
            platformId,
            "Windows",
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
    /// Creates one released or pressed mouse state for the supplied coordinates.
    /// </summary>
    /// <param name="x">Pointer X coordinate.</param>
    /// <param name="y">Pointer Y coordinate.</param>
    /// <param name="buttonState">Left-button state to report.</param>
    /// <returns>Mouse state with the supplied button state.</returns>
    static MouseState CreateMouseState(int x, int y, ButtonState buttonState) {
        return new MouseState(
            x,
            y,
            0,
            buttonState,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released);
    }
}

