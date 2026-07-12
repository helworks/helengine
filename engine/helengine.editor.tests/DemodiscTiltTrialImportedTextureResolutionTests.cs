using helengine.editor.tests.testing;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the imported texture asset ids referenced by the authored Demodisc Tilt Trial materials still resolve back to their source textures in editor workflows.
/// </summary>
public sealed class DemodiscTiltTrialImportedTextureResolutionTests {
    /// <summary>
    /// Absolute Demodisc project root used by the shared asset import manager.
    /// </summary>
    const string DemodiscProjectRootPath = @"C:\dev\helprojs\demodisc";

    /// <summary>
    /// Imported texture asset id currently referenced by the Tilt Trial course material.
    /// </summary>
    const string TiltTrialCourseTextureAssetId = "97e4cc7fa14f7c67f75322e3c2d14048712ea0f1cff0bac9f223c98f7d1766dc";

    /// <summary>
    /// Imported texture asset id currently referenced by the Tilt Trial player sphere diffuse material field.
    /// </summary>
    const string TiltTrialPlayerSphereDiffuseTextureAssetId = "40fc7cf2f49de336277c54f73927d9f4e6eaedcf0e96dbf787019729ae633553";

    /// <summary>
    /// Imported texture asset id currently referenced by the Tilt Trial player sphere roughness material field.
    /// </summary>
    const string TiltTrialPlayerSphereRoughnessTextureAssetId = "b812df31a9b0b41bf9b0e447318a4ec4643c2f5ba6531c2b714fd994c5224c00";

    /// <summary>
    /// Absolute authored source texture path that should back the Tilt Trial course material.
    /// </summary>
    const string TiltTrialCourseTextureSourcePath = @"C:\dev\helprojs\demodisc\assets\textures\rendering\tilt_trial\CourseLilacGrid.bmp";

    /// <summary>
    /// Absolute authored source texture path that should back the Tilt Trial player sphere diffuse material field.
    /// </summary>
    const string TiltTrialPlayerSphereDiffuseTextureSourcePath = @"C:\dev\helprojs\demodisc\assets\textures\rendering\tilt_trial\PlayerSphereMarble.jpg";

    /// <summary>
    /// Absolute authored source texture path that should back the Tilt Trial player sphere roughness material field.
    /// </summary>
    const string TiltTrialPlayerSphereRoughnessTextureSourcePath = @"C:\dev\helprojs\demodisc\assets\textures\rendering\tilt_trial\PlayerSphereMarbleRoughness.jpg";

    /// <summary>
    /// Ensures the Tilt Trial course imported texture id still resolves back to the authored course bitmap.
    /// </summary>
    [Fact]
    public void Tilt_trial_course_imported_texture_id_resolves_to_authored_source() {
        AssertImportedTextureResolvesToSource(TiltTrialCourseTextureAssetId, TiltTrialCourseTextureSourcePath);
    }

    /// <summary>
    /// Ensures the Tilt Trial player sphere diffuse imported texture id still resolves back to the authored marble albedo texture.
    /// </summary>
    [Fact]
    public void Tilt_trial_player_sphere_diffuse_imported_texture_id_resolves_to_authored_source() {
        AssertImportedTextureResolvesToSource(TiltTrialPlayerSphereDiffuseTextureAssetId, TiltTrialPlayerSphereDiffuseTextureSourcePath);
    }

    /// <summary>
    /// Ensures the Tilt Trial player sphere roughness imported texture id still resolves back to the authored roughness texture.
    /// </summary>
    [Fact]
    public void Tilt_trial_player_sphere_roughness_imported_texture_id_resolves_to_authored_source() {
        AssertImportedTextureResolvesToSource(TiltTrialPlayerSphereRoughnessTextureAssetId, TiltTrialPlayerSphereRoughnessTextureSourcePath);
    }

    /// <summary>
    /// Resolves one imported texture asset id back to its authored source file and verifies the expected source path is returned.
    /// </summary>
    /// <param name="assetId">Imported texture asset id stored by one authored material.</param>
    /// <param name="expectedSourcePath">Absolute authored source texture path expected for the asset id.</param>
    static void AssertImportedTextureResolvesToSource(string assetId, string expectedSourcePath) {
        if (string.IsNullOrWhiteSpace(assetId)) {
            throw new ArgumentException("Imported texture asset id must be provided.", nameof(assetId));
        }
        if (string.IsNullOrWhiteSpace(expectedSourcePath)) {
            throw new ArgumentException("Expected source path must be provided.", nameof(expectedSourcePath));
        }

        AssetImportManager manager = CreateManager();
        bool resolved = manager.TryResolveImportedTextureSourcePath(assetId, out string sourcePath);

        Assert.True(resolved);
        Assert.True(
            string.Equals(Path.GetFullPath(expectedSourcePath), sourcePath, StringComparison.OrdinalIgnoreCase),
            $"Expected resolved source path '{Path.GetFullPath(expectedSourcePath)}' but found '{sourcePath}'.");
    }

    /// <summary>
    /// Creates one Windows-scoped asset import manager rooted to the real Demodisc project and configured for the authored Tilt Trial texture formats.
    /// </summary>
    /// <returns>Configured asset import manager.</returns>
    static AssetImportManager CreateManager() {
        ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(DemodiscProjectRootPath));
        AssetImportManager manager = new AssetImportManager(DemodiscProjectRootPath, contentManager);
        manager.RegisterTextureImporter(new TextureImporterRegistration("gdi", new TestTextureImporter(), new[] { ".bmp", ".jpg" }));
        manager.CurrentPlatformId = "windows";
        return manager;
    }
}
