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
    /// Verifies the material view keeps shader override fields hidden until the custom-shader toggle is enabled.
    /// </summary>
    [Fact]
    public void Show_when_custom_shader_is_disabled_hides_shader_override_fields_until_the_toggle_is_enabled() {
        MaterialAssetView view = new MaterialAssetView(CreateFont(), 1);
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
            _ => EditorPlatformBuildSelectionModel.From(CreatePlatformDefinition()));

        List<MaterialAssetFieldEditorRow> initialRows = GetPrivateField<List<MaterialAssetFieldEditorRow>>(view, "FieldRows");

        Assert.Equal(["use-custom-shader", "base-color"], initialRows.Select(row => row.FieldId).ToArray());

        MaterialAssetFieldEditorRow customShaderRow = Assert.Single(initialRows, row => row.FieldId == "use-custom-shader");
        CheckBoxComponent checkBox = Assert.IsType<CheckBoxComponent>(customShaderRow.CheckBox);
        InvokePrivate(checkBox, "SetCheckedState", true, true);

        List<MaterialAssetFieldEditorRow> updatedRows = GetPrivateField<List<MaterialAssetFieldEditorRow>>(view, "FieldRows");

        Assert.Equal(
            [
                "use-custom-shader",
                "shader-asset-id",
                "vertex-program",
                "pixel-program",
                "base-color"
            ],
            updatedRows.Select(row => row.FieldId).ToArray());
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
    static AssetImportSettings CreateSettings(bool useCustomShader) {
        AssetImportSettings settings = new AssetImportSettings();
        settings.Processor.Platforms["windows"] = new AssetPlatformProcessorSettings();
        settings.Processor.Platforms["windows"].Material.SchemaId = "standard-shader";
        settings.Processor.Platforms["windows"].Material.FieldValues["use-custom-shader"] = useCustomShader ? "true" : "false";
        settings.Processor.Platforms["windows"].Material.FieldValues["base-color"] = "#ffffff";
        return settings;
    }

    /// <summary>
    /// Creates one minimal platform definition that publishes the material schema under test.
    /// </summary>
    /// <returns>Platform definition used by the material view test.</returns>
    static PlatformDefinition CreatePlatformDefinition() {
        return new PlatformDefinition(
            "windows",
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
