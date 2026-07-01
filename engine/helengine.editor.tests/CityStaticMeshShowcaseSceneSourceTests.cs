namespace helengine.editor.tests;

/// <summary>
/// Verifies the generated city static-mesh showcase source scenes remain readable by the shared asset serializer.
/// </summary>
public sealed class CityStaticMeshShowcaseSceneSourceTests {
    /// <summary>
    /// Absolute source scene path for the generated desktop static-mesh showcase scene.
    /// </summary>
    const string CityStaticMeshShowcaseScenePath = @"C:\dev\helprojs\city\assets\scenes\physics\test_scene_static_mesh_showcase.helen";

    /// <summary>
    /// Absolute source scene path for the generated Nintendo DS companion static-mesh showcase scene.
    /// </summary>
    const string CityStaticMeshShowcaseNintendoDsScenePath = @"C:\dev\helprojs\city\assets\scenes\physics\test_scene_static_mesh_showcase_ds.helen";

    /// <summary>
    /// Ensures the generated desktop static-mesh showcase scene deserializes into a scene asset.
    /// </summary>
    [Fact]
    public void City_static_mesh_showcase_scene_source_deserializes() {
        using FileStream stream = File.OpenRead(CityStaticMeshShowcaseScenePath);

        SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));

        Assert.Equal("scenes/physics/test_scene_static_mesh_showcase.helen", sceneAsset.Id);
    }

    /// <summary>
    /// Ensures the generated Nintendo DS companion static-mesh showcase scene deserializes into a scene asset.
    /// </summary>
    [Fact]
    public void City_static_mesh_showcase_ds_scene_source_deserializes() {
        using FileStream stream = File.OpenRead(CityStaticMeshShowcaseNintendoDsScenePath);

        SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));

        Assert.Equal("scenes/physics/test_scene_static_mesh_showcase_ds.helen", sceneAsset.Id);
    }
}
