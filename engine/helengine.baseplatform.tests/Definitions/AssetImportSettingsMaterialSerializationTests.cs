using helengine.baseplatform.Definitions;
using helengine;
using helengine.editor;
using Xunit;

namespace helengine.baseplatform.tests.Definitions;

/// <summary>
/// Verifies material-side platform processor settings serialize and seed correctly.
/// </summary>
public sealed class AssetImportSettingsMaterialSerializationTests : IDisposable {
    /// <summary>
    /// Temporary directory used for file-backed material-settings tests.
    /// </summary>
    readonly string TempRootPath;

    /// <summary>
    /// Initializes an isolated temporary directory for the current test.
    /// </summary>
    public AssetImportSettingsMaterialSerializationTests() {
        TempRootPath = Path.Combine(Path.GetTempPath(), "helengine-material-settings-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TempRootPath);
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
    /// Verifies asset import settings round-trip material processor settings alongside model settings.
    /// </summary>
    [Fact]
    public void AssetImportSettingsBinarySerializer_round_trips_material_processor_settings() {
        AssetImportSettings settings = new AssetImportSettings();
        settings.Importer.ImporterId = "helengine.material";
        settings.Importer.SourceChecksum = "checksum";
        settings.Importer.AssetId = "Materials/Test.helmat";

        AssetPlatformProcessorSettings platformSettings = new AssetPlatformProcessorSettings();
        platformSettings.Model.FlipWinding = true;
        platformSettings.Material.SchemaId = "standard-shader";
        platformSettings.Material.FieldValues["use-custom-shader"] = "false";
        platformSettings.Material.FieldValues["shader-asset-id"] = "shaders/test";
        settings.Processor.Platforms["windows"] = platformSettings;

        using MemoryStream stream = new MemoryStream();
        AssetImportSettingsBinarySerializer.Serialize(stream, settings);
        stream.Position = 0;

        AssetImportSettings deserialized = AssetImportSettingsBinarySerializer.Deserialize(stream);

        Assert.Equal("helengine.material", deserialized.Importer.ImporterId);
        Assert.True(deserialized.Processor.Platforms["windows"].Model.FlipWinding);
        Assert.Equal("standard-shader", deserialized.Processor.Platforms["windows"].Material.SchemaId);
        Assert.Equal("false", deserialized.Processor.Platforms["windows"].Material.FieldValues["use-custom-shader"]);
        Assert.Equal("shaders/test", deserialized.Processor.Platforms["windows"].Material.FieldValues["shader-asset-id"]);
    }

    /// <summary>
    /// Verifies material settings are created per platform and seeded from legacy shader-backed material fields.
    /// </summary>
    [Fact]
    public void MaterialAssetSettingsService_loads_or_creates_platform_material_settings_from_schema_metadata() {
        MaterialAsset materialAsset = new MaterialAsset {
            Id = "Materials/Test.helmat",
            ShaderAssetId = "shaders/test",
            VertexProgram = "Test.vs",
            PixelProgram = "Test.ps",
            Variant = "Mesh"
        };
        string materialAssetPath = Path.Combine(TempRootPath, "Test.helmat");
        File.WriteAllBytes(materialAssetPath, Array.Empty<byte>());

        PlatformDefinition definition = new(
            "windows",
            "Windows",
            [
                new PlatformBuildProfileDefinition(
                    "debug",
                    "Debug",
                    "Debug build",
                    "directx11",
                    [])
            ],
            [
                new PlatformGraphicsProfileDefinition(
                    "directx11",
                    "DirectX 11",
                    "Default graphics profile",
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
            ]);

        MaterialAssetSettingsService service = new MaterialAssetSettingsService();
        AssetImportSettings settings = service.LoadOrCreate(
            materialAssetPath,
            materialAsset,
            ["windows"],
            platformId => EditorPlatformBuildSelectionModel.From(definition));

        Assert.True(File.Exists(materialAssetPath + ".hasset"));
        Assert.Equal("standard-shader", settings.Processor.Platforms["windows"].Material.SchemaId);
        Assert.Equal("false", settings.Processor.Platforms["windows"].Material.FieldValues["use-custom-shader"]);
        Assert.Equal("shaders/test", settings.Processor.Platforms["windows"].Material.FieldValues["shader-asset-id"]);
        Assert.Equal("Test.vs", settings.Processor.Platforms["windows"].Material.FieldValues["vertex-program"]);
        Assert.Equal("Test.ps", settings.Processor.Platforms["windows"].Material.FieldValues["pixel-program"]);
        Assert.Equal("#ffffff", settings.Processor.Platforms["windows"].Material.FieldValues["base-color"]);
    }

    /// <summary>
    /// Verifies non-shader platform settings clear shader compatibility fields on the raw material payload.
    /// </summary>
    [Fact]
    public void ApplyPlatformCompatibilityFields_when_shader_fields_are_missing_clears_legacy_shader_values() {
        MaterialAsset materialAsset = new MaterialAsset {
            ShaderAssetId = "shaders/test",
            VertexProgram = "Test.vs",
            PixelProgram = "Test.ps",
            Variant = "Mesh"
        };

        AssetImportSettings settings = new AssetImportSettings();
        settings.Processor.Platforms["ps2"] = new AssetPlatformProcessorSettings();
        settings.Processor.Platforms["ps2"].Material.SchemaId = "fixed-textured";
        settings.Processor.Platforms["ps2"].Material.FieldValues["texture-id"] = "Textures/Brick.png";

        MaterialAssetSettingsService service = new MaterialAssetSettingsService();

        bool changed = service.ApplyPlatformCompatibilityFields(materialAsset, settings, "ps2");

        Assert.True(changed);
        Assert.Equal(string.Empty, materialAsset.ShaderAssetId);
        Assert.Equal(string.Empty, materialAsset.VertexProgram);
        Assert.Equal(string.Empty, materialAsset.PixelProgram);
        Assert.Equal("Mesh", materialAsset.Variant);
    }
}
