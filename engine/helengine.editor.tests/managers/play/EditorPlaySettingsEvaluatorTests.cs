using helengine.baseplatform.Definitions;
using Xunit;

namespace helengine.editor.tests.managers.play;

/// <summary>
/// Verifies stored Play settings are checked against the current declarations and frozen into a press-time snapshot.
/// </summary>
public sealed class EditorPlaySettingsEvaluatorTests {
    /// <summary>File path reported in messages.</summary>
    const string FilePath = @"C:\project\user_settings\play.json";

    /// <summary>An empty document yields every declared default and no issues.</summary>
    [Fact]
    public void Evaluate_WhenNothingStored_UsesDeclaredDefaults() {
        EditorPlaySettingsEvaluation evaluation = EditorPlaySettingsEvaluator.Evaluate(new EditorPlaySettingsDocument(), FilePath, "ds", CreateCapability());

        Assert.True(evaluation.IsValid);
        Assert.Equal("fit", evaluation.CommonValues["window-scale"]);
        Assert.Equal("editor", evaluation.CommonValues["window-monitor"]);
        Assert.Equal("false", evaluation.CommonValues["wait-for-debugger"]);
        Assert.Equal("true", evaluation.CommonValues["shadows"]);
        Assert.Equal("platform", evaluation.CommonValues["frame-rate"]);
        Assert.Equal("stacked", evaluation.PlatformValues["screen-layout"]);
    }

    /// <summary>A stored value outside a choice is an issue (not an exception) and the failure names file, platform and id.</summary>
    [Fact]
    public void Evaluate_WhenStoredChoiceNotAllowed_ReportsIssueNamingFilePlatformAndId() {
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.CommonValues["window-scale"] = "5x";

        EditorPlaySettingsEvaluation evaluation = EditorPlaySettingsEvaluator.Evaluate(document, FilePath, "ds", CreateCapability());

        Assert.False(evaluation.IsValid);
        EditorPlaySettingIssue issue = Assert.Single(evaluation.Issues);
        Assert.Equal(EditorPlaySettingScope.Common, issue.Scope);
        Assert.Equal("window-scale", issue.SettingId);
        Assert.Equal("5x", issue.StoredValue);
        Assert.Equal("5x", evaluation.CommonValues["window-scale"]);
        string failure = evaluation.FormatFailure();
        Assert.Contains(FilePath, failure);
        Assert.Contains("'ds'", failure);
        Assert.Contains("window-scale", failure);
    }

    /// <summary>A stored boolean that is not true/false is an issue.</summary>
    [Fact]
    public void Evaluate_WhenStoredBooleanInvalid_ReportsIssue() {
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.CommonValues["shadows"] = "True";

        Assert.False(EditorPlaySettingsEvaluator.Evaluate(document, FilePath, "ds", CreateCapability()).IsValid);
    }

    /// <summary>A stored platform id the platform does not declare throws, naming file, platform and id.</summary>
    [Fact]
    public void Evaluate_WhenPlatformIdUnknown_Throws() {
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.PlatformValues["ds"] = new Dictionary<string, string> { ["screen-mode"] = "x" };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => EditorPlaySettingsEvaluator.Evaluate(document, FilePath, "ds", CreateCapability()));

        Assert.Contains(FilePath, exception.Message);
        Assert.Contains("'ds'", exception.Message);
        Assert.Contains("screen-mode", exception.Message);
    }

    /// <summary>Another platform's section is neither validated nor reported.</summary>
    [Fact]
    public void Evaluate_IgnoresOtherPlatformSections() {
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.PlatformValues["ps2"] = new Dictionary<string, string> { ["video-mode"] = "not-a-mode" };

        Assert.True(EditorPlaySettingsEvaluator.Evaluate(document, FilePath, "ds", CreateCapability()).IsValid);
    }

    /// <summary>The snapshot copies the values, so later edits to the document or evaluation never reach it.</summary>
    [Fact]
    public void Snapshot_IsIndependentOfLaterEdits() {
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.CommonValues["window-scale"] = "2x";
        EditorPlaySettingsEvaluation evaluation = EditorPlaySettingsEvaluator.Evaluate(document, FilePath, "ds", CreateCapability());
        EditorPlaySettingsSnapshot snapshot = new EditorPlaySettingsSnapshot(evaluation, false, CreateCapability(), 77);

        document.CommonValues["window-scale"] = "4x";

        Assert.Equal("2x", snapshot.CommonValues["window-scale"]);
        Assert.Equal("ds", snapshot.PlatformId);
        Assert.Equal(77, snapshot.EditorProcessId);
    }

    /// <summary>A snapshot refuses an invalid evaluation, because Play must not start from invalid settings.</summary>
    [Fact]
    public void Snapshot_RejectsInvalidEvaluation() {
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.CommonValues["window-scale"] = "5x";
        EditorPlaySettingsEvaluation evaluation = EditorPlaySettingsEvaluator.Evaluate(document, FilePath, "ds", CreateCapability());

        Assert.Throws<ArgumentException>(() => new EditorPlaySettingsSnapshot(evaluation, false, CreateCapability(), 77));
    }

    /// <summary>The debugger wait is the stored setting or the Shift+click request.</summary>
    [Theory]
    [InlineData("false", false, false)]
    [InlineData("true", false, true)]
    [InlineData("false", true, true)]
    public void Snapshot_EffectiveWaitForDebugger(string stored, bool shiftRequested, bool expected) {
        EditorPlaySettingsDocument document = new EditorPlaySettingsDocument();
        document.CommonValues["wait-for-debugger"] = stored;
        EditorPlaySettingsEvaluation evaluation = EditorPlaySettingsEvaluator.Evaluate(document, FilePath, "ds", CreateCapability());

        Assert.Equal(expected, new EditorPlaySettingsSnapshot(evaluation, shiftRequested, CreateCapability(), 77).EffectiveWaitForDebugger);
    }

    /// <summary>Creates a DS-like capability with one choice Play setting.</summary>
    static PlatformPlayerCapability CreateCapability() {
        return new PlatformPlayerCapability(true, "a.B", "staging", 60d, PlayerInputDevices.Gamepad, "ds", "1.0.0",
            [new PlatformSettingDefinition("screen-layout", "Screen Layout", PlatformSettingKind.Choice, "stacked", true, ["stacked"])]);
    }
}
