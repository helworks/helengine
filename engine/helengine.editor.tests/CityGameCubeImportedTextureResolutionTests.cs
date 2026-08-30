using helengine.editor.tests.testing;

namespace helengine.editor.tests;

/// <summary>
/// Verifies GameCube imported texture asset ids used by the shared city authored content still resolve back to their authored source files.
/// </summary>
public sealed class CityGameCubeImportedTextureResolutionTests {
    const string TiltTrialTextureAssetId = "00112233445566778899aabbccddeeff";
    const string TexturedCubeGridTextureAssetId = "ffeeddccbbaa99887766554433221100";

    /// <summary>
    /// Ensures the Tilt Trial player sphere imported texture id can still resolve back to its authored source bitmap under the GameCube packaging context.
    /// </summary>
    [Fact]
    public void Tilt_trial_persisted_texture_identity_resolves_to_authored_source_for_gamecube() {
        using CityTextureFixtureProject fixtureProject = CityTextureFixtureProject.Create();
        string sourcePath = fixtureProject.WritePersistedTextureReference(
            "Textures/rendering/tilt_trial/PlayerSphereWalnut.bmp",
            TiltTrialTextureAssetId);
        AssetImportManager resolver = fixtureProject.CreateFreshManager();

        bool resolved = resolver.TryResolveImportedTextureSourcePath(TiltTrialTextureAssetId, out string resolvedSourcePath);

        Assert.True(resolved);
        Assert.Equal(Path.GetFullPath(sourcePath), resolvedSourcePath, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ensures the textured cube-grid imported texture id can still resolve back to its authored source bitmap under the GameCube packaging context.
    /// </summary>
    [Fact]
    public void Textured_cube_grid_persisted_texture_identity_resolves_to_authored_source_for_gamecube() {
        using CityTextureFixtureProject fixtureProject = CityTextureFixtureProject.Create();
        string sourcePath = fixtureProject.WritePersistedTextureReference(
            "textures/rendering/textured_cube_grid/Cube00.bmp",
            TexturedCubeGridTextureAssetId);
        AssetImportManager resolver = fixtureProject.CreateFreshManager();

        bool resolved = resolver.TryResolveImportedTextureSourcePath(TexturedCubeGridTextureAssetId, out string resolvedSourcePath);

        Assert.True(resolved);
        Assert.Equal(Path.GetFullPath(sourcePath), resolvedSourcePath, StringComparer.OrdinalIgnoreCase);
    }
}
