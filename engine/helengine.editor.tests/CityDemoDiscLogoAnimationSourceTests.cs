using System.Text.RegularExpressions;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the city demo-disc main menu scene still authors the demo-disc logo idle animation reference.
/// </summary>
public sealed class CityDemoDiscLogoAnimationSourceTests {
    /// <summary>
    /// Ensures the menu scene authors the demo-disc logo idle clip onto the Nintendo DS logo animation player.
    /// </summary>
    [Fact]
    public void City_demo_disc_logo_idle_animation_source_authors_logo_animation_for_nintendo_ds() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\menu.tools\DemoDiscMainMenuSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("void CreateNintendoDsTopScreenLogoEntity(Entity topScreenRootEntity, MenuOverlayImageDefinition overlayImage)", source, StringComparison.Ordinal);
        Assert.Contains("AnimationPlayerComponent animationPlayerComponent = new AnimationPlayerComponent", source, StringComparison.Ordinal);
        Assert.Contains("Clip = LoadRequiredAnimationClipAsset(DemoDiscLogoIdleAnimationRelativePath),", source, StringComparison.Ordinal);
        Assert.Contains("ApplyAnimationClipReference(entity, animationPlayerComponent, DemoDiscLogoIdleAnimationRelativePath);", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the desktop main-menu logo uses the authored idle animation instead of the fallback rotate script.
    /// </summary>
    [Fact]
    public void City_demo_disc_logo_idle_animation_source_authors_logo_animation_for_desktop_menu_overlay() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\menu.tools\DemoDiscMainMenuSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Match overlayMethodMatch = Regex.Match(
            source,
            "void CreateOverlayImageEntity\\(Entity generatedRootEntity, MenuOverlayImageDefinition overlayImage\\) \\{(?<body>[\\s\\S]*?)\\n        \\}",
            RegexOptions.CultureInvariant);
        Assert.True(overlayMethodMatch.Success);

        string overlayMethodBody = overlayMethodMatch.Groups["body"].Value;
        Assert.Contains("AnimationPlayerComponent animationPlayerComponent = new AnimationPlayerComponent", overlayMethodBody, StringComparison.Ordinal);
        Assert.Contains("Clip = LoadRequiredAnimationClipAsset(DemoDiscLogoIdleAnimationRelativePath),", overlayMethodBody, StringComparison.Ordinal);
        Assert.Contains("ApplyAnimationClipReference(entity, animationPlayerComponent, DemoDiscLogoIdleAnimationRelativePath);", overlayMethodBody, StringComparison.Ordinal);
        Assert.DoesNotContain("entity.AddComponent(new RotateZComponent());", overlayMethodBody, StringComparison.Ordinal);
    }
}
