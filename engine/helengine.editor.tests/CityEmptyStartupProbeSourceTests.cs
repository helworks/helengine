namespace helengine.editor.tests;

/// <summary>
/// Verifies the temporary city PS2 empty-startup probe command remains available while bottom-up leak isolation is active.
/// </summary>
public sealed class CityEmptyStartupProbeSourceTests {
    /// <summary>
    /// Ensures the city project exposes one dedicated editor command that regenerates the PS2 startup scene as an empty camera-only probe.
    /// </summary>
    [Fact]
    public void City_empty_startup_probe_source_exposes_editor_command_and_factory() {
        string factorySourcePath = @"C:\dev\helprojs\city\assets\codebase\menu.tools\Ps2EmptyStartupProbeSceneFactory.cs";
        string commandSourcePath = @"C:\dev\helprojs\city\assets\codebase\menu.tools\RegeneratePs2EmptyStartupProbeCommand.cs";

        Assert.True(File.Exists(factorySourcePath), $"Expected probe factory source file '{factorySourcePath}' to exist.");
        Assert.True(File.Exists(commandSourcePath), $"Expected probe command source file '{commandSourcePath}' to exist.");

        string factorySource = File.ReadAllText(factorySourcePath);
        string commandSource = File.ReadAllText(commandSourcePath);

        Assert.Contains("DemoDiscMainMenuSceneFactory.SceneId", factorySource, StringComparison.Ordinal);
        Assert.Contains("new CameraComponent", factorySource, StringComparison.Ordinal);
        Assert.Contains("menu.regenerate-ps2-empty-startup-probe", commandSource, StringComparison.Ordinal);
    }
}
