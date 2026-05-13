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
        platformSettings.Material.FieldValues["texture-id"] = "textures/test";
        platformSettings.Material.FieldValues["casts-shadow"] = "true";
        platformSettings.Material.FieldValues["receives-shadow"] = "true";
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
        Assert.Equal("textures/test", deserialized.Processor.Platforms["windows"].Material.FieldValues["texture-id"]);
        Assert.Equal("true", deserialized.Processor.Platforms["windows"].Material.FieldValues["casts-shadow"]);
        Assert.Equal("true", deserialized.Processor.Platforms["windows"].Material.FieldValues["receives-shadow"]);
    }

    /// <summary>
    /// Verifies material settings are created per platform and seeded from the authored shader-backed material fields.
    /// </summary>
    [Fact]
    public void MaterialAssetSettingsService_loads_or_creates_platform_material_settings_from_schema_metadata() {
        MaterialAsset materialAsset = new MaterialAsset {
            Id = "Materials/Test.helmat",
            ShaderAssetId = "shaders/test",
            VertexProgram = "Test.vs",
            PixelProgram = "Test.ps",
            DiffuseTextureAssetId = "textures/test",
            CastsShadows = false,
            ReceivesShadows = true,
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
            ]);

        MaterialAssetSettingsService service = new MaterialAssetSettingsService();
        MaterialAssetImportSettings settings = service.LoadOrCreate(
            materialAssetPath,
            materialAsset,
            ["windows"],
            platformId => EditorPlatformBuildSelectionModel.From(definition));

        Assert.True(File.Exists(materialAssetPath));
        Assert.Equal("standard-shader", settings.Processor.Platforms["windows"].SchemaId);
        Assert.Equal("false", settings.Processor.Platforms["windows"].FieldValues["use-custom-shader"]);
        Assert.Equal(string.Empty, settings.Processor.Platforms["windows"].FieldValues["shader-asset-id"]);
        Assert.Equal(string.Empty, settings.Processor.Platforms["windows"].FieldValues["texture-id"]);
        Assert.Equal("true", settings.Processor.Platforms["windows"].FieldValues["casts-shadow"]);
        Assert.Equal("true", settings.Processor.Platforms["windows"].FieldValues["receives-shadow"]);
        Assert.Equal(string.Empty, settings.Processor.Platforms["windows"].FieldValues["vertex-program"]);
        Assert.Equal(string.Empty, settings.Processor.Platforms["windows"].FieldValues["pixel-program"]);
        Assert.Equal("#ffffff", settings.Processor.Platforms["windows"].FieldValues["base-color"]);
    }

    /// <summary>
    /// Verifies base material authoring now lives directly in the material `.hasset` file and platform overrides live in `*.platform.hasset`.
    /// </summary>
    [Fact]
    public void MaterialAssetSettingsService_loads_platform_settings_from_base_hasset_and_platform_override() {
        string materialAssetPath = Path.Combine(TempRootPath, "Test.hasset");
        MaterialAssetCommonSettingsDocument commonDocument = new MaterialAssetCommonSettingsDocument();
        commonDocument.Importer.ImporterId = "helengine.material";
        commonDocument.Importer.AssetId = "Materials/Test.hasset";
        commonDocument.Processor.SchemaId = "standard-shader";
        commonDocument.Processor.FieldValues["use-custom-shader"] = "false";
        commonDocument.Processor.FieldValues["texture-id"] = "Textures/Common.png";
        commonDocument.Processor.FieldValues["casts-shadow"] = "true";
        commonDocument.Processor.FieldValues["receives-shadow"] = "true";
        commonDocument.Processor.FieldValues["base-color"] = "#ffffff";
        using (FileStream stream = File.Create(materialAssetPath)) {
            MaterialAssetCommonSettingsDocumentBinarySerializer.Serialize(stream, commonDocument);
        }

        MaterialAssetPlatformOverrideDocument overrideDocument = new MaterialAssetPlatformOverrideDocument();
        overrideDocument.PlatformId = "windows";
        overrideDocument.Processor.FieldValues["texture-id"] = "Textures/Windows.png";
        overrideDocument.Processor.FieldValues["base-color"] = "#336699";
        using (FileStream stream = File.Create(materialAssetPath + ".windows.hasset")) {
            MaterialAssetPlatformOverrideDocumentBinarySerializer.Serialize(stream, overrideDocument);
        }

        MaterialAssetSettingsService service = new MaterialAssetSettingsService();

        bool loaded = service.TryLoadPlatformSettings(materialAssetPath, "windows", out MaterialAssetProcessorSettings platformSettings);

        Assert.True(loaded);
        Assert.NotNull(platformSettings);
        Assert.Equal("standard-shader", platformSettings.SchemaId);
        Assert.Equal("Textures/Windows.png", platformSettings.FieldValues["texture-id"]);
        Assert.Equal("#336699", platformSettings.FieldValues["base-color"]);
        Assert.Equal("true", platformSettings.FieldValues["casts-shadow"]);
        Assert.False(File.Exists(materialAssetPath + ".hasset"));
    }

    /// <summary>
    /// Verifies standard shader mirrored material fields mirror authored texture and shadow values back into the raw material payload.
    /// </summary>
    [Fact]
    public void ApplyPlatformMaterialFields_when_standard_shader_fields_are_present_mirrors_texture_and_shadow_values() {
        MaterialAsset materialAsset = new MaterialAsset {
            ShaderAssetId = "shaders/test",
            VertexProgram = "Test.vs",
            PixelProgram = "Test.ps",
            DiffuseTextureAssetId = string.Empty,
            CastsShadows = true,
            ReceivesShadows = true,
            Variant = "Mesh"
        };

        MaterialAssetImportSettings settings = new MaterialAssetImportSettings();
        settings.Processor.Platforms["windows"] = new MaterialAssetProcessorSettings();
        settings.Processor.Platforms["windows"].SchemaId = "standard-shader";
        settings.Processor.Platforms["windows"].FieldValues["use-custom-shader"] = "false";
        settings.Processor.Platforms["windows"].FieldValues["texture-id"] = "Textures/Brick.png";
        settings.Processor.Platforms["windows"].FieldValues["casts-shadow"] = "false";
        settings.Processor.Platforms["windows"].FieldValues["receives-shadow"] = "false";

        MaterialAssetSettingsService service = new MaterialAssetSettingsService();

        bool changed = service.ApplyPlatformMaterialFields(materialAsset, settings, "windows");

        Assert.True(changed);
        Assert.Equal("ForwardStandardShader", materialAsset.ShaderAssetId);
        Assert.Equal("ForwardStandardShader.vs", materialAsset.VertexProgram);
        Assert.Equal("ForwardStandardShader.ps", materialAsset.PixelProgram);
        Assert.Equal("Textures/Brick.png", materialAsset.DiffuseTextureAssetId);
        Assert.False(materialAsset.CastsShadows);
        Assert.False(materialAsset.ReceivesShadows);
        Assert.Equal("default", materialAsset.Variant);
    }

    /// <summary>
    /// Verifies non-shader platform settings clear shader mirrored material fields on the raw material payload.
    /// </summary>
    [Fact]
    public void ApplyPlatformMaterialFields_when_shader_fields_are_missing_clears_shader_values() {
        MaterialAsset materialAsset = new MaterialAsset {
            ShaderAssetId = "shaders/test",
            VertexProgram = "Test.vs",
            PixelProgram = "Test.ps",
            Variant = "Mesh"
        };

        MaterialAssetImportSettings settings = new MaterialAssetImportSettings();
        settings.Processor.Platforms["ps2"] = new MaterialAssetProcessorSettings();
        settings.Processor.Platforms["ps2"].SchemaId = "fixed-textured";
        settings.Processor.Platforms["ps2"].FieldValues["texture-id"] = "Textures/Brick.png";

        MaterialAssetSettingsService service = new MaterialAssetSettingsService();

        bool changed = service.ApplyPlatformMaterialFields(materialAsset, settings, "ps2");

        Assert.True(changed);
        Assert.Equal(string.Empty, materialAsset.ShaderAssetId);
        Assert.Equal(string.Empty, materialAsset.VertexProgram);
        Assert.Equal(string.Empty, materialAsset.PixelProgram);
        Assert.Equal("Mesh", materialAsset.Variant);
    }
}

