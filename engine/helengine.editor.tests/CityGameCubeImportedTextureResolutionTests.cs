using helengine.editor.tests.testing;

namespace helengine.editor.tests;

/// <summary>
/// Verifies GameCube imported texture asset ids used by the shared city authored content still resolve back to their authored source files.
/// </summary>
public sealed class CityGameCubeImportedTextureResolutionTests {
    /// <summary>
    /// Ensures the Tilt Trial player sphere imported texture id can still resolve back to its authored source bitmap under the GameCube packaging context.
    /// </summary>
    [Fact]
    public void Tilt_trial_player_sphere_imported_texture_id_resolves_to_authored_source_for_gamecube() {
        using CityTextureFixtureProject fixtureProject = CityTextureFixtureProject.Create();
        string sourcePath = fixtureProject.WriteTextureSource("Textures/rendering/tilt_trial/PlayerSphereWalnut.bmp");
        TextureAssetImportSettings settings = fixtureProject.Manager.LoadOrCreateTextureImportSettings(sourcePath);

        Assert.False(string.IsNullOrWhiteSpace(settings.Importer.AssetId));

        bool resolved = fixtureProject.Manager.TryResolveImportedTextureSourcePath(settings.Importer.AssetId, out string resolvedSourcePath);

        Assert.True(resolved);
        Assert.True(
            string.Equals(Path.GetFullPath(sourcePath), resolvedSourcePath, StringComparison.OrdinalIgnoreCase),
            $"Expected resolved Tilt Trial texture source path '{Path.GetFullPath(sourcePath)}' but found '{resolvedSourcePath}'.");
    }

    /// <summary>
    /// Ensures the textured cube-grid imported texture id can still resolve back to its authored source bitmap under the GameCube packaging context.
    /// </summary>
    [Fact]
    public void Textured_cube_grid_imported_texture_id_resolves_to_authored_source_for_gamecube() {
        using CityTextureFixtureProject fixtureProject = CityTextureFixtureProject.Create();
        string sourcePath = fixtureProject.WriteTextureSource("textures/rendering/textured_cube_grid/Cube00.bmp");
        TextureAssetImportSettings settings = fixtureProject.Manager.LoadOrCreateTextureImportSettings(sourcePath);

        bool resolved = fixtureProject.Manager.TryResolveImportedTextureSourcePath(settings.Importer.AssetId, out string resolvedSourcePath);

        Assert.True(resolved);
        Assert.True(
            string.Equals(Path.GetFullPath(sourcePath), resolvedSourcePath, StringComparison.OrdinalIgnoreCase),
            $"Expected resolved textured cube-grid source path '{Path.GetFullPath(sourcePath)}' but found '{resolvedSourcePath}'.");
    }
}
