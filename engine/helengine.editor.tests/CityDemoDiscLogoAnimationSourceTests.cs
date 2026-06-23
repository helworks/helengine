namespace helengine.editor.tests;

/// <summary>
/// Verifies the city demo-disc logo animation is authored directly on the engine animation player.
/// </summary>
public sealed class CityDemoDiscLogoAnimationSourceTests {
    /// <summary>
    /// Ensures the menu scene authors the demo-disc logo idle clip directly onto one looping engine animation player.
    /// </summary>
    [Fact]
    public void City_demo_disc_logo_idle_animation_source_uses_animation_player_component() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\menu.tools\DemoDiscMainMenuSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("AnimationPlayerComponent animationPlayerComponent = new AnimationPlayerComponent {", source, StringComparison.Ordinal);
        Assert.Contains("Clip = LoadRequiredAnimationClipAsset(DemoDiscLogoIdleAnimationRelativePath),", source, StringComparison.Ordinal);
        Assert.Contains("PlayAutomatically = true,", source, StringComparison.Ordinal);
        Assert.Contains("ShouldLoop = true", source, StringComparison.Ordinal);
        Assert.Contains("ApplyAnimationClipReference(entity, animationPlayerComponent, DemoDiscLogoIdleAnimationRelativePath);", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DemoDiscLogoIdleAnimationComponent", source, StringComparison.Ordinal);
    }
}
