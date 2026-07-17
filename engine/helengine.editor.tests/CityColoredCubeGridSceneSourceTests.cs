namespace helengine.editor.tests;

/// <summary>
/// Verifies the generated colored cube-grid scene source keeps the intended GameCube material authoring contract.
/// </summary>
public sealed class CityColoredCubeGridSceneSourceTests {
    /// <summary>
    /// Ensures the colored cube-grid authors explicit GameCube material settings instead of relying on white fallback defaults.
    /// </summary>
    [Fact]
    public void City_colored_cube_grid_source_authors_gamecube_material_settings() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\rendering.tools\ColoredCubeGridSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("const string GameCubeMaterialSchemaId = \"standard-shader\";", source, StringComparison.Ordinal);
        Assert.Contains("GeneratedMaterialPlatformDefinition gameCubeSettings = definition.GetOrCreatePlatform(\"gamecube\");", source, StringComparison.Ordinal);
        Assert.Contains("gameCubeSettings.SchemaId = GameCubeMaterialSchemaId;", source, StringComparison.Ordinal);
        Assert.Contains("gameCubeSettings.SetFieldValue(BaseColorFieldId, CubeMaterialColors[cubeIndex]);", source, StringComparison.Ordinal);
        Assert.Contains("gameCubeSettings.SetFieldValue(DoubleSidedFieldId, \"false\");", source, StringComparison.Ordinal);
        Assert.Contains("gameCubeSettings.SetFieldValue(VertexColorModeFieldId, \"ignore\");", source, StringComparison.Ordinal);
        Assert.Contains("gameCubeSettings.SetFieldValue(LightingModeFieldId, \"lit\");", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the Nintendo DS colored cube-grid scene uses the same shared bottom overlay as the other rendering scenes.
    /// </summary>
    [Fact]
    public void City_colored_cube_grid_source_uses_shared_ds_bottom_overlay() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\rendering.tools\ColoredCubeGridSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("Entity[] nintendoDsRootEntities = new Entity[cubeEntities.Length + 3];", source, StringComparison.Ordinal);
        Assert.Contains("nintendoDsRootEntities[0] = cameraEntity;", source, StringComparison.Ordinal);
        Assert.Contains("nintendoDsRootEntities[1] = CreateNintendoDsPerfUiEntity();", source, StringComparison.Ordinal);
        Assert.Contains("nintendoDsRootEntities[2] = directionalLightEntity;", source, StringComparison.Ordinal);
        Assert.Contains("Array.Copy(cubeEntities, 0, nintendoDsRootEntities, 3, cubeEntities.Length);", source, StringComparison.Ordinal);
        Assert.Contains("RootEntities = nintendoDsRootEntities,", source, StringComparison.Ordinal);
        Assert.Contains("UseDefaultBottomOverlay = true,", source, StringComparison.Ordinal);
        Assert.Contains("BottomScreenRootEntities = Array.Empty<Entity>()", source, StringComparison.Ordinal);
        Assert.Contains("rootEntities[2] = CreateUiEntity();", source, StringComparison.Ordinal);
        Assert.Contains("Entity CreateNintendoDsPerfUiEntity()", source, StringComparison.Ordinal);
    }
}
