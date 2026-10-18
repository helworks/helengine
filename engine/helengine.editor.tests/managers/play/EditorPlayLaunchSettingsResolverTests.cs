using helengine.baseplatform.Definitions;
using helengine.baseplatform.Player;
using Xunit;

namespace helengine.editor.tests.managers.play;

/// <summary>
/// Verifies the press-time Play settings become the player's resolved settings once the cook reported the project's
/// graphics options and shadow policy.
/// </summary>
public sealed class EditorPlayLaunchSettingsResolverTests {
    /// <summary>Cook and launch resolve the exact same selected mode before cooked paths exist.</summary>
    [Theory]
    [InlineData("project", "pal-640x512i")]
    [InlineData("ntsc-256x224p", "pal-640x512i")]
    public void PlayAndCook_UseSameVideoSelection(string playMode, string projectMode) {
        EditorPlaySettingsSnapshot snapshot = CreateVideoModeSnapshot(playMode);
        Dictionary<string, string> graphics = new Dictionary<string, string> { ["video-mode"] = projectMode };
        PlatformVideoMode beforeCook = EditorPlayLaunchSettingsResolver.ResolveVideoMode(snapshot, graphics);
        PlayerPlaySettings launched = EditorPlayLaunchSettingsResolver.Resolve(snapshot, CreateCookResult(true, graphics));
        Assert.Same(beforeCook, launched.VideoMode);
        Assert.Equal(playMode == "project" ? projectMode : playMode, beforeCook.Id);
    }

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

    /// <summary>With video modes, project resolves to the project's mode and the frame rate is the mode's player rate.</summary>
    [Fact]
    public void Resolve_VideoModeFollowsProjectAndSetsFrameRate() {
        EditorPlaySettingsSnapshot snapshot = CreateVideoModeSnapshot("project");

        PlayerPlaySettings settings = EditorPlayLaunchSettingsResolver.Resolve(snapshot, CreateCookResult(true, new Dictionary<string, string> { ["video-mode"] = "pal-320x256p" }));

        Assert.Equal("pal-320x256p", settings.VideoMode.Id);
        Assert.Equal(320, settings.VideoMode.Width);
        Assert.Equal(50d, settings.TargetFrameRate);
    }

    /// <summary>An explicit Play mode overrides the project's mode.</summary>
    [Fact]
    public void Resolve_ExplicitVideoModeOverridesProject() {
        EditorPlaySettingsSnapshot snapshot = CreateVideoModeSnapshot("ntsc-256x224p");

        PlayerPlaySettings settings = EditorPlayLaunchSettingsResolver.Resolve(snapshot, CreateCookResult(true, new Dictionary<string, string> { ["video-mode"] = "pal-640x512i" }));

        Assert.Equal("ntsc-256x224p", settings.VideoMode.Id);
        Assert.Equal(60d, settings.TargetFrameRate);
    }

    /// <summary>
    /// An old project file without <c>video-mode</c> (it still holds default-width/default-height) fails Play naming the
    /// field and the platform settings file instead of silently running at 640x448.
    /// </summary>
    [Fact]
    public void Resolve_WhenProjectHasOldSizeFieldsOnly_FailsNamingFieldAndFile() {
        EditorPlaySettingsSnapshot snapshot = CreateVideoModeSnapshot("project");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => EditorPlayLaunchSettingsResolver.Resolve(
            snapshot,
            CreateCookResult(true, new Dictionary<string, string> { ["default-width"] = "640", ["default-height"] = "448" })));

        Assert.Contains("video-mode", exception.Message);
        Assert.Contains("settings/platform.ps2.json", exception.Message);
    }

    /// <summary>
    /// A platform whose capability frame rate is 0 (the rate comes from the video mode) never resolves a paced rate of 0:
    /// <c>frame-rate: platform</c> paces at the resolved mode's player rate, and <c>unlimited</c> stays unpaced with the mode
    /// still resolved.
    /// </summary>
    [Fact]
    public void Resolve_WhenCapabilityRateIsZero_TakesTheRateFromTheResolvedMode() {
        EditorPlaySettingsSnapshot paced = CreateVideoModeSnapshot("pal-640x512i");
        EditorPlaySettingsSnapshot unlimited = CreateVideoModeSnapshot("pal-640x512i", "unlimited");

        PlayerPlaySettings pacedSettings = EditorPlayLaunchSettingsResolver.Resolve(paced, CreateCookResult(true, new Dictionary<string, string>()));
        PlayerPlaySettings unlimitedSettings = EditorPlayLaunchSettingsResolver.Resolve(unlimited, CreateCookResult(true, new Dictionary<string, string>()));

        Assert.Equal(0d, paced.Capability.TargetFrameRate);
        Assert.False(pacedSettings.FrameRateUnlimited);
        Assert.Equal(50d, pacedSettings.TargetFrameRate);
        Assert.True(unlimitedSettings.FrameRateUnlimited);
        Assert.Equal(0d, unlimitedSettings.TargetFrameRate);
        Assert.Equal("pal-640x512i", unlimitedSettings.VideoMode.Id);
    }

    /// <summary>A platform without video modes resolves no mode and paces at the capability's frame rate.</summary>
    [Fact]
    public void Resolve_WithoutVideoModes_HasNoModeAndUsesTheCapabilityRate() {
        EditorPlaySettingsSnapshot snapshot = CreateSnapshot(new Dictionary<string, string>(), new Dictionary<string, string> { ["video-mode"] = "m1" }, false);

        PlayerPlaySettings settings = EditorPlayLaunchSettingsResolver.Resolve(snapshot, CreateCookResult(true, new Dictionary<string, string>()));

        Assert.False(snapshot.HasVideoModes);
        Assert.False(settings.HasVideoMode);
        Assert.Equal(60d, settings.TargetFrameRate);
    }

    /// <summary>
    /// A capability that takes its frame rate from the video mode cannot be snapshotted without video modes, so a frame
    /// rate of 0 never reaches the resolver or the player's pacer.
    /// </summary>
    [Fact]
    public void Snapshot_WhenCapabilityRateIsZeroWithoutVideoModes_Throws() {
        PlatformPlayerCapability capability = new PlatformPlayerCapability(true, "a.B", "ps2-staging", 0d, PlayerInputDevices.Gamepad, "ps2", "1.0.0", []);
        EditorPlaySettingsEvaluation evaluation = EditorPlaySettingsEvaluator.Evaluate(new EditorPlaySettingsDocument(), @"C:\p\user_settings\play.json", "ps2", capability);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => new EditorPlaySettingsSnapshot(evaluation, false, capability, null, 55));

        Assert.Equal("videoModeCapability", exception.ParamName);
        Assert.Contains("ps2", exception.Message);
    }

    /// <summary>Creates a PS2-like snapshot whose Play video mode is the given value.</summary>
    static EditorPlaySettingsSnapshot CreateVideoModeSnapshot(string playValue) {
        return CreateVideoModeSnapshot(playValue, "platform");
    }

    /// <summary>Creates a PS2-like snapshot (capability frame rate 0) whose Play video mode and frame-rate setting are the given values.</summary>
    static EditorPlaySettingsSnapshot CreateVideoModeSnapshot(string playValue, string frameRate) {
        PlatformVideoModeCapability modes = new PlatformVideoModeCapability("video-mode", [
            new PlatformVideoMode("ntsc-256x224p", 256, 224, true, 59.94d, 60d, 4, 3),
            new PlatformVideoMode("pal-320x256p", 320, 256, true, 50d, 50d, 4, 3),
            new PlatformVideoMode("pal-640x512i", 640, 512, false, 50d, 50d, 4, 3)
        ]);
        PlatformPlayerCapability capability = new PlatformPlayerCapability(true, "a.B", "ps2-staging", 0d, PlayerInputDevices.Gamepad, "ps2", "1.0.0",
            [new PlatformSettingDefinition("video-mode", "Video Mode", PlatformSettingKind.Choice, "project", true, ["project", "ntsc-256x224p", "pal-320x256p", "pal-640x512i"])]);
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.CommonValues["frame-rate"] = frameRate;
        document.PlatformValues["ps2"] = new Dictionary<string, string> { ["video-mode"] = playValue };
        EditorPlaySettingsEvaluation evaluation = EditorPlaySettingsEvaluator.Evaluate(document, @"C:\p\user_settings\play.json", "ps2", capability);
        return new EditorPlaySettingsSnapshot(evaluation, false, capability, modes, 55);
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
        return new EditorPlaySettingsSnapshot(evaluation, shift, capability, null, 55);
    }

    /// <summary>Creates a successful cook result with the given shadow policy and graphics options.</summary>
    static EditorPlayCookResult CreateCookResult(bool shadows, Dictionary<string, string> graphics) {
        return new EditorPlayCookResult(true, "Cooked.", @"C:\content", "scene", "scenes/scene.hasset", graphics, shadows);
    }
}
