using helengine.editor.tests.testing;

namespace helengine.editor.tests;

/// <summary>
/// Verifies GameCube imported texture asset ids used by the shared city authored content still resolve back to their authored source files.
/// </summary>
public sealed class CityGameCubeImportedTextureResolutionTests {
    /// <summary>
    /// Absolute city project root used by the shared asset import manager.
    /// </summary>
    const string CityProjectRootPath = @"C:\dev\helprojs\city";

    /// <summary>
    /// Imported texture asset id currently referenced by the first textured cube-grid material.
    /// </summary>
    const string TexturedCubeGridTextureAssetId = "ff8a0f1fafe1f1c4989f73f39db8b800512e09e26439b011cb7afb0fed44dd5a";

    /// <summary>
    /// Absolute authored source texture path that should back the Tilt Trial player sphere material.
    /// </summary>
    const string TiltTrialPlayerSphereTextureSourcePath = @"C:\dev\helprojs\city\assets\Textures\rendering\tilt_trial\PlayerSphereWalnut.bmp";

    /// <summary>
    /// Absolute authored source texture path that should back the first textured cube-grid material.
    /// </summary>
    const string TexturedCubeGridTextureSourcePath = @"C:\dev\helprojs\city\assets\textures\rendering\textured_cube_grid\Cube00.bmp";

    /// <summary>
    /// Ensures the Tilt Trial player sphere imported texture id can still resolve back to its authored source bitmap under the GameCube packaging context.
    /// </summary>
    [Fact]
    public void Tilt_trial_player_sphere_imported_texture_id_resolves_to_authored_source_for_gamecube() {
        AssetImportManager manager = CreateManager();
        TextureAssetImportSettings settings = manager.LoadOrCreateTextureImportSettings(TiltTrialPlayerSphereTextureSourcePath);

        Assert.False(string.IsNullOrWhiteSpace(settings.Importer.AssetId));

        bool resolved = manager.TryResolveImportedTextureSourcePath(settings.Importer.AssetId, out string sourcePath);

        Assert.True(resolved);
        Assert.True(
            string.Equals(Path.GetFullPath(TiltTrialPlayerSphereTextureSourcePath), sourcePath, StringComparison.OrdinalIgnoreCase),
            $"Expected resolved Tilt Trial texture source path '{Path.GetFullPath(TiltTrialPlayerSphereTextureSourcePath)}' but found '{sourcePath}'.");
    }

    /// <summary>
    /// Ensures the textured cube-grid imported texture id can still resolve back to its authored source bitmap under the GameCube packaging context.
    /// </summary>
    [Fact]
    public void Textured_cube_grid_imported_texture_id_resolves_to_authored_source_for_gamecube() {
        AssetImportManager manager = CreateManager();

        bool resolved = manager.TryResolveImportedTextureSourcePath(TexturedCubeGridTextureAssetId, out string sourcePath);

        Assert.True(resolved);
        Assert.True(
            string.Equals(Path.GetFullPath(TexturedCubeGridTextureSourcePath), sourcePath, StringComparison.OrdinalIgnoreCase),
            $"Expected resolved textured cube-grid source path '{Path.GetFullPath(TexturedCubeGridTextureSourcePath)}' but found '{sourcePath}'.");
    }

    /// <summary>
    /// Creates one GameCube-scoped asset import manager rooted to the real city project.
    /// </summary>
    /// <returns>Configured asset import manager.</returns>
    static AssetImportManager CreateManager() {
        ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(CityProjectRootPath));
        AssetImportManager manager = new AssetImportManager(CityProjectRootPath, contentManager);
        manager.RegisterTextureImporter(new TextureImporterRegistration("gdi", new TestTextureImporter(), new[] { ".bmp" }));
        manager.CurrentPlatformId = "gamecube";
        return manager;
    }
}
