using helengine.baseplatform.Definitions;
using helengine.editor;
using Xunit;

namespace helengine.baseplatform.tests.Definitions;

/// <summary>
/// Verifies the editor selection model resolves builder-published material schema metadata.
/// </summary>
public sealed class EditorPlatformBuildSelectionModelTests {
    /// <summary>
    /// Verifies the selection model returns the material schemas compatible with the requested graphics profile.
    /// </summary>
    [Fact]
    public void ResolveMaterialSchemas_returns_schemas_for_the_requested_graphics_profile() {
        PlatformDefinition definition = new(
            "windows",
            "Windows",
            [],
            [
                new PlatformGraphicsProfileDefinition(
                    "directx11",
                    "DirectX 11",
                    "Windows renderer",
                    []),
                new PlatformGraphicsProfileDefinition(
                    "vulkan",
                    "Vulkan",
                    "Alternate Windows renderer",
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
                            "variant",
                            "Variant",
                            PlatformMaterialFieldKind.Choice,
                            "default",
                            true,
                            ["default", "skinned"])
                    ]),
                new PlatformMaterialSchemaDefinition(
                    "portable-fallback",
                    "Portable Fallback",
                    [],
                    [
                        new PlatformMaterialFieldDefinition(
                            "tint",
                            "Tint",
                            PlatformMaterialFieldKind.Color,
                            "#ffffff",
                            false,
                            [])
                    ])
            ]);

        EditorPlatformBuildSelectionModel selectionModel = EditorPlatformBuildSelectionModel.From(definition);

        PlatformMaterialSchemaDefinition[] schemas = selectionModel.ResolveMaterialSchemas("directx11");

        Assert.Equal(2, schemas.Length);
        Assert.Equal("standard-shader", schemas[0].SchemaId);
        Assert.Equal("portable-fallback", schemas[1].SchemaId);
    }
}
