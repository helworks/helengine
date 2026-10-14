using helengine.baseplatform.Definitions;
using helengine.baseplatform.Player;
using Xunit;

namespace helengine.editor.tests.managers.play;

/// <summary>
/// Verifies the press-time Play settings become the player's resolved settings once the cook reported the project's
/// graphics options and shadow policy.
/// </summary>
public sealed class EditorPlayLaunchSettingsResolverTests {
    /// <summary>
    /// Common settings map onto the typed player settings, with the capability's frame rate for <c>platform</c>; a concrete
    /// platform value passes through unchanged. Tests that do not exercise <c>project</c> pin <c>video-mode</c> to
    /// <c>m1</c>, because the declared default <c>project</c> would otherwise need a project graphics option.
    /// </summary>
    [Fact]
    public void Resolve_MapsCommonSettings() {
        EditorPlaySettingsSnapshot snapshot = CreateSnapshot(new Dictionary<string, string> { ["window-scale"] = "3x", ["window-monitor"] = "primary" }, new Dictionary<string, string> { ["video-mode"] = "m1" }, false);

        PlayerPlaySettings settings = EditorPlayLaunchSettingsResolver.Resolve(snapshot, CreateCookResult(true, new Dictionary<string, string>()));

        Assert.Equal(PlayerWindowScale.Three, settings.WindowScale);
        Assert.Equal(PlayerWindowMonitor.Primary, settings.WindowMonitor);
        Assert.Equal(55, settings.EditorProcessId);
        Assert.False(settings.FrameRateUnlimited);
        Assert.Equal(60d, settings.TargetFrameRate);
        Assert.True(settings.ShadowsEnabled);
        Assert.False(settings.WaitForDebugger);
        Assert.Equal("m1", settings.PlatformValues["video-mode"]);
    }

    /// <summary><c>frame-rate: unlimited</c> turns pacing off.</summary>
    [Fact]
    public void Resolve_UnlimitedFrameRate() {
        EditorPlaySettingsSnapshot snapshot = CreateSnapshot(new Dictionary<string, string> { ["frame-rate"] = "unlimited" }, new Dictionary<string, string> { ["video-mode"] = "m1" }, false);

        PlayerPlaySettings settings = EditorPlayLaunchSettingsResolver.Resolve(snapshot, CreateCookResult(true, new Dictionary<string, string>()));

        Assert.True(settings.FrameRateUnlimited);
        Assert.Equal(0d, settings.TargetFrameRate);
    }

    /// <summary>The Play shadows setting can only turn shadows off; the project's policy still applies.</summary>
    [Theory]
    [InlineData("true", true, true)]
    [InlineData("false", true, false)]
    [InlineData("true", false, false)]
    public void Resolve_ShadowsAreSettingAndProjectPolicy(string playShadows, bool projectShadows, bool expected) {
        EditorPlaySettingsSnapshot snapshot = CreateSnapshot(new Dictionary<string, string> { ["shadows"] = playShadows }, new Dictionary<string, string> { ["video-mode"] = "m1" }, false);

        Assert.Equal(expected, EditorPlayLaunchSettingsResolver.Resolve(snapshot, CreateCookResult(projectShadows, new Dictionary<string, string>())).ShadowsEnabled);
    }

    /// <summary>Shift+click forces the debugger wait even when the setting is off.</summary>
    [Fact]
    public void Resolve_ShiftClickForcesDebuggerWait() {
        EditorPlaySettingsSnapshot snapshot = CreateSnapshot(new Dictionary<string, string>(), new Dictionary<string, string> { ["video-mode"] = "m1" }, true);

        Assert.True(EditorPlayLaunchSettingsResolver.Resolve(snapshot, CreateCookResult(true, new Dictionary<string, string>())).WaitForDebugger);
    }

    /// <summary>A platform value of <c>project</c> takes the project graphics option with the same id.</summary>
    [Fact]
    public void Resolve_ProjectValueTakesGraphicsOption() {
        EditorPlaySettingsSnapshot snapshot = CreateSnapshot(new Dictionary<string, string>(), new Dictionary<string, string> { ["video-mode"] = "project" }, false);

        PlayerPlaySettings settings = EditorPlayLaunchSettingsResolver.Resolve(snapshot, CreateCookResult(true, new Dictionary<string, string> { ["video-mode"] = "m2" }));

        Assert.Equal("m2", settings.PlatformValues["video-mode"]);
    }

    /// <summary>A <c>project</c> value whose graphics option is missing fails, naming the id and the platform settings file.</summary>
    [Fact]
    public void Resolve_WhenProjectOptionMissing_ThrowsNamingIdAndFile() {
        EditorPlaySettingsSnapshot snapshot = CreateSnapshot(new Dictionary<string, string>(), new Dictionary<string, string> { ["video-mode"] = "project" }, false);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => EditorPlayLaunchSettingsResolver.Resolve(snapshot, CreateCookResult(true, new Dictionary<string, string>())));

        Assert.Contains("video-mode", exception.Message);
        Assert.Contains("settings/platform.tst.json", exception.Message);
    }

    /// <summary>A project graphics value the Play setting does not allow fails, naming the value.</summary>
    [Fact]
    public void Resolve_WhenProjectOptionNotAllowed_ThrowsNamingValue() {
        EditorPlaySettingsSnapshot snapshot = CreateSnapshot(new Dictionary<string, string>(), new Dictionary<string, string> { ["video-mode"] = "project" }, false);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => EditorPlayLaunchSettingsResolver.Resolve(snapshot, CreateCookResult(true, new Dictionary<string, string> { ["video-mode"] = "m9" })));

        Assert.Contains("m9", exception.Message);
        Assert.Contains("not one of project, m1, m2", exception.Message);
    }

    /// <summary>
    /// With no stored platform value, the declared default <c>project</c> applies and resolves from the cook's graphics
    /// options: a Play with no <c>play.json</c> follows the project.
    /// </summary>
    [Fact]
    public void Resolve_DefaultProjectFollowsGraphicsOption() {
        EditorPlaySettingsSnapshot snapshot = CreateSnapshot(new Dictionary<string, string>(), new Dictionary<string, string>(), false);

        PlayerPlaySettings settings = EditorPlayLaunchSettingsResolver.Resolve(snapshot, CreateCookResult(true, new Dictionary<string, string> { ["video-mode"] = "m2" }));

        Assert.Equal("m2", settings.PlatformValues["video-mode"]);
    }

    /// <summary>A project graphics option that is itself <c>project</c> fails with wording that says it is not a concrete value.</summary>
    [Fact]
    public void Resolve_WhenProjectOptionIsProjectSentinel_ThrowsNamingSentinel() {
        EditorPlaySettingsSnapshot snapshot = CreateSnapshot(new Dictionary<string, string>(), new Dictionary<string, string> { ["video-mode"] = "project" }, false);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => EditorPlayLaunchSettingsResolver.Resolve(snapshot, CreateCookResult(true, new Dictionary<string, string> { ["video-mode"] = "project" })));

        Assert.Contains("settings/platform.tst.json", exception.Message);
        Assert.Contains("not a concrete value", exception.Message);
    }

    /// <summary>A failed cook is rejected up front, naming the cook's own failure instead of a missing project value.</summary>
    [Fact]
    public void Resolve_WhenCookFailed_ThrowsNamingCookFailure() {
        EditorPlaySettingsSnapshot snapshot = CreateSnapshot(new Dictionary<string, string>(), new Dictionary<string, string> { ["video-mode"] = "m1" }, false);

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => EditorPlayLaunchSettingsResolver.Resolve(snapshot, EditorPlayCookResult.Failure("Cook broke.")));

        Assert.Equal("cookResult", exception.ParamName);
        Assert.Contains("Cook broke.", exception.Message);
    }

    /// <summary>Creates a snapshot for a test platform whose Play setting may follow the project.</summary>
    static EditorPlaySettingsSnapshot CreateSnapshot(Dictionary<string, string> common, Dictionary<string, string> platform, bool shift) {
        PlatformPlayerCapability capability = new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.Gamepad, "tst", "1.0.0",
            [new PlatformSettingDefinition("video-mode", "Video Mode", PlatformSettingKind.Choice, "project", true, ["project", "m1", "m2"])]);
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        foreach (KeyValuePair<string, string> entry in common) {
            document.CommonValues[entry.Key] = entry.Value;
        }
        document.PlatformValues["tst"] = platform;
        EditorPlaySettingsEvaluation evaluation = EditorPlaySettingsEvaluator.Evaluate(document, @"C:\p\user_settings\play.json", "tst", capability);
        return new EditorPlaySettingsSnapshot(evaluation, shift, capability, 55);
    }

    /// <summary>Creates a successful cook result with the given shadow policy and graphics options.</summary>
    static EditorPlayCookResult CreateCookResult(bool shadows, Dictionary<string, string> graphics) {
        return new EditorPlayCookResult(true, "Cooked.", @"C:\content", "scene", "scenes/scene.hasset", graphics, shadows);
    }
}
