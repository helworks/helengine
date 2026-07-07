namespace helengine.editor.tests;

/// <summary>
/// Verifies the shared Nintendo DS companion-scene scaffold owns the canonical handheld bottom-screen controls contract.
/// </summary>
public sealed class CityNintendoDsBottomScreenControlsSourceTests {
    /// <summary>
    /// Ensures the shared DS scaffold authors a full-width light button, swatch, back button, and one-times FPS scale.
    /// </summary>
    [Fact]
    public void City_ds_scaffold_source_authors_canonical_bottom_screen_controls() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\rendering.tools\NintendoDsRenderingSceneScaffoldFactory.cs";
        Assert.True(File.Exists(sourcePath), $"Expected source file '{sourcePath}' to exist.");

        string source = File.ReadAllText(sourcePath);

        Assert.Contains("FontScale = 1f", source, StringComparison.Ordinal);
        Assert.Contains("CreateBottomScreenLightButton(", source, StringComparison.Ordinal);
        Assert.Contains("CreateBottomScreenBackButton(", source, StringComparison.Ordinal);
        Assert.Contains("DemoDiscBottomScreenLightButton", source, StringComparison.Ordinal);
        Assert.Contains("DemoDiscBottomScreenLightButtonLabel", source, StringComparison.Ordinal);
        Assert.Contains("DemoDiscBottomScreenLightSwatch", source, StringComparison.Ordinal);
        Assert.Contains("const int NintendoDsBackButtonTop = ScreenHeight - NintendoDsBackButtonHeight - 6;", source, StringComparison.Ordinal);
        Assert.Contains("const int NintendoDsBackButtonLabelTop = 6;", source, StringComparison.Ordinal);
        Assert.Contains("const int NintendoDsLightButtonLabelTop = 6;", source, StringComparison.Ordinal);
        Assert.Contains("const int NintendoDsLightSwatchTop = 4;", source, StringComparison.Ordinal);
        Assert.Contains("const byte NintendoDsLightSwatchRenderOrder = 209;", source, StringComparison.Ordinal);
        Assert.Contains("RenderOrder2D = NintendoDsLightSwatchRenderOrder", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateDefaultBottomOverlay(", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the shared DS scaffold bottom-screen camera clears with the exact demo-disc lilac background color.
    /// </summary>
    [Fact]
    public void City_ds_scaffold_source_uses_demo_disc_lilac_bottom_screen_clear() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\rendering.tools\NintendoDsRenderingSceneScaffoldFactory.cs";
        Assert.True(File.Exists(sourcePath), $"Expected source file '{sourcePath}' to exist.");

        string source = File.ReadAllText(sourcePath);

        Assert.Contains("new float4(30f / 255f, 17f / 255f, 41f / 255f, 1f)", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the handheld light controller responds to both bottom-screen pointer presses and handheld R input.
    /// </summary>
    [Fact]
    public void City_handheld_light_controller_source_responds_to_pointer_and_r_input() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\rendering\NintendoDsLightToggleOverlayComponent.cs";
        Assert.True(File.Exists(sourcePath), $"Expected source file '{sourcePath}' to exist.");

        string source = File.ReadAllText(sourcePath);

        Assert.Contains("BoundInteractable.CursorEvent += HandleCursorEvent;", source, StringComparison.Ordinal);
        Assert.Contains("PointerInteraction.Press", source, StringComparison.Ordinal);
        Assert.Contains("PointerInteraction.Release", source, StringComparison.Ordinal);
        Assert.Contains("InputGamepadButton.RightShoulder", source, StringComparison.Ordinal);
        Assert.Contains("AdvanceLightState();", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the handheld light controller uses the same fixed cycle ordering as the desktop light toggle.
    /// </summary>
    [Fact]
    public void City_handheld_light_controller_source_uses_demo_disc_cycle_order() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\rendering\NintendoDsLightToggleOverlayComponent.cs";
        Assert.True(File.Exists(sourcePath), $"Expected source file '{sourcePath}' to exist.");

        string source = File.ReadAllText(sourcePath);

        Assert.Contains("WhiteLightColor", source, StringComparison.Ordinal);
        Assert.Contains("YellowLightColor", source, StringComparison.Ordinal);
        Assert.Contains("RedLightColor", source, StringComparison.Ordinal);
        Assert.Contains("BlueLightColor", source, StringComparison.Ordinal);
        Assert.Contains("GreenLightColor", source, StringComparison.Ordinal);
        Assert.Contains("OffSwatchColor", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures physics companion-scene generation no longer keeps a separate temporary bottom-overlay branch.
    /// </summary>
    [Fact]
    public void City_physics_ds_generator_source_uses_canonical_scaffold_bottom_controls() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsNintendoDsSceneGenerator.cs";
        Assert.True(File.Exists(sourcePath), $"Expected source file '{sourcePath}' to exist.");

        string source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("useDefaultBottomOverlay", source, StringComparison.Ordinal);
        Assert.Contains("WriteNintendoDsCompanionScene(", source, StringComparison.Ordinal);
        Assert.Contains("Array.Empty<Entity>()", source, StringComparison.Ordinal);
    }
}
