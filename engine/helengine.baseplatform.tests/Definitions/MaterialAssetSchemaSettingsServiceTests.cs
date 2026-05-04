using helengine.baseplatform.Definitions;
using helengine.editor;
using Xunit;

namespace helengine.baseplatform.tests.Definitions;

/// <summary>
/// Verifies schema selection and field seeding for per-platform material settings.
/// </summary>
public sealed class MaterialAssetSchemaSettingsServiceTests {
    /// <summary>
    /// Verifies a blank schema selection falls back to the first published schema and seeds its default values.
    /// </summary>
    [Fact]
    public void EnsureSelectedSchema_when_schema_id_is_blank_selects_first_schema_and_seeds_defaults() {
        MaterialAssetProcessorSettings settings = new MaterialAssetProcessorSettings();
        settings.FieldValues["obsolete"] = "remove-me";

        MaterialAssetSchemaSettingsService service = new MaterialAssetSchemaSettingsService();

        PlatformMaterialSchemaDefinition selectedSchema = service.EnsureSelectedSchema(settings, CreateMaterialSchemas());

        Assert.Equal("shader-textured", selectedSchema.SchemaId);
        Assert.Equal("shader-textured", settings.SchemaId);
        Assert.Equal("default", settings.FieldValues["variant"]);
        Assert.Equal("Textures/Diffuse.png", settings.FieldValues["texture-id"]);
        Assert.False(settings.FieldValues.ContainsKey("obsolete"));
    }

    /// <summary>
    /// Verifies switching schemas preserves overlapping fields while pruning fields no longer defined by the selected schema.
    /// </summary>
    [Fact]
    public void SelectSchema_when_switching_schemas_preserves_overlapping_values_and_drops_removed_fields() {
        MaterialAssetProcessorSettings settings = new MaterialAssetProcessorSettings();
        settings.SchemaId = "shader-textured";
        settings.FieldValues["texture-id"] = "Textures/Brick.png";
        settings.FieldValues["variant"] = "skinned";
        settings.FieldValues["obsolete"] = "remove-me";

        MaterialAssetSchemaSettingsService service = new MaterialAssetSchemaSettingsService();

        PlatformMaterialSchemaDefinition selectedSchema = service.SelectSchema(settings, CreateMaterialSchemas(), "fixed-textured");

        Assert.Equal("fixed-textured", selectedSchema.SchemaId);
        Assert.Equal("fixed-textured", settings.SchemaId);
        Assert.Equal("Textures/Brick.png", settings.FieldValues["texture-id"]);
        Assert.Equal("true", settings.FieldValues["lighting-enabled"]);
        Assert.False(settings.FieldValues.ContainsKey("variant"));
        Assert.False(settings.FieldValues.ContainsKey("obsolete"));
    }

    /// <summary>
    /// Creates the material schemas used by the current test.
    /// </summary>
    /// <returns>Two compatible schemas with one overlapping field.</returns>
    static PlatformMaterialSchemaDefinition[] CreateMaterialSchemas() {
        return [
            new PlatformMaterialSchemaDefinition(
                "shader-textured",
                "Shader Textured",
                ["directx11"],
                [
                    new PlatformMaterialFieldDefinition(
                        "texture-id",
                        "Texture",
                        PlatformMaterialFieldKind.AssetReference,
                        "Textures/Diffuse.png",
                        true,
                        []),
                    new PlatformMaterialFieldDefinition(
                        "variant",
                        "Variant",
                        PlatformMaterialFieldKind.Choice,
                        "default",
                        true,
                        ["default", "skinned"])
                ]),
            new PlatformMaterialSchemaDefinition(
                "fixed-textured",
                "Fixed Textured",
                ["ps2"],
                [
                    new PlatformMaterialFieldDefinition(
                        "texture-id",
                        "Texture",
                        PlatformMaterialFieldKind.AssetReference,
                        "Textures/Fallback.png",
                        true,
                        []),
                    new PlatformMaterialFieldDefinition(
                        "lighting-enabled",
                        "Lighting",
                        PlatformMaterialFieldKind.Boolean,
                        "true",
                        false,
                        [])
                ])
        ];
    }
}
