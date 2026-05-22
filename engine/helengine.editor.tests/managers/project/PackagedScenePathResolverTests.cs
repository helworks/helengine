namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies packaged runtime scene paths preserve authored scene file names.
/// </summary>
public sealed class PackagedScenePathResolverTests {
    /// <summary>
    /// Verifies the startup scene keeps its authored file name instead of being rewritten to the former main-scene alias.
    /// </summary>
    [Fact]
    public void BuildRelativePath_for_startup_scene_preserves_the_authored_file_name() {
        string relativePath = PackagedScenePathResolver.BuildRelativePath("Scenes/MainMenuScene.helen", 0);

        Assert.Equal("cooked/scenes/MainMenuScene.hasset", relativePath);
    }

    /// <summary>
    /// Verifies nested scene paths stay rooted beneath <c>cooked/scenes</c> without duplicating the authored <c>scenes/</c> segment.
    /// </summary>
    [Fact]
    public void BuildRelativePath_for_nested_scene_trims_the_authored_scenes_root() {
        string relativePath = PackagedScenePathResolver.BuildRelativePath("scenes/rendering/point-shadow.helen", 0);

        Assert.Equal("cooked/scenes/rendering/point-shadow.hasset", relativePath);
    }
}
