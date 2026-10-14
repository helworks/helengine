using System.Globalization;
using Xunit;

namespace helengine.editor.tests.managers.play;

/// <summary>
/// Verifies the formatting of the Play startup summary line and of the player's first-frame marker.
/// </summary>
public sealed class EditorPlayStartupTimingTests {
    /// <summary>
    /// Ensures the first-frame summary lists every stage and the total in seconds with one decimal.
    /// </summary>
    [Fact]
    public void FormatFirstFrameSummary_WritesEveryStageWithOneDecimal() {
        EditorPlayStartupTiming timing = new EditorPlayStartupTiming(
            TimeSpan.FromSeconds(100),
            TimeSpan.FromMilliseconds(120),
            TimeSpan.FromMilliseconds(8420),
            TimeSpan.FromMilliseconds(1250),
            false,
            TimeSpan.FromMilliseconds(310));

        string summary = timing.FormatFirstFrameSummary(TimeSpan.FromMilliseconds(2690), TimeSpan.FromMilliseconds(12800));

        Assert.Equal("[play] save 0.1s · cook 8.4s · scripts 1.3s · launch 0.3s · first frame 2.7s · total 12.8s", summary);
    }

    /// <summary>
    /// Ensures a skipped script build is tagged in the summary.
    /// </summary>
    [Fact]
    public void FormatFirstFrameSummary_WhenScriptBuildSkipped_TagsScriptStage() {
        EditorPlayStartupTiming timing = new EditorPlayStartupTiming(
            TimeSpan.Zero, TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(8.4), TimeSpan.FromSeconds(0.01), true, TimeSpan.FromSeconds(0.3));

        string summary = timing.FormatFirstFrameSummary(TimeSpan.FromSeconds(2.7), TimeSpan.FromSeconds(11.5));

        Assert.Equal("[play] save 0.1s · cook 8.4s · scripts 0.0s (skipped) · launch 0.3s · first frame 2.7s · total 11.5s", summary);
    }

    /// <summary>
    /// Ensures the summary uses a period as the decimal separator even when the current culture uses a comma.
    /// </summary>
    [Fact]
    public void FormatFirstFrameSummary_UsesInvariantCulture() {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
        try {
            EditorPlayStartupTiming timing = new EditorPlayStartupTiming(
                TimeSpan.Zero, TimeSpan.FromSeconds(1.5), TimeSpan.Zero, TimeSpan.Zero, false, TimeSpan.Zero);

            string summary = timing.FormatFirstFrameSummary(TimeSpan.FromSeconds(2.5), TimeSpan.FromSeconds(4));

            Assert.Equal("[play] save 1.5s · cook 0.0s · scripts 0.0s · launch 0.0s · first frame 2.5s · total 4.0s", summary);
        } finally {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    /// <summary>
    /// Ensures the no-frame summary lists the measured stages followed by the player's exit code.
    /// </summary>
    [Fact]
    public void FormatNoFrameSummary_EndsWithExitCode() {
        EditorPlayStartupTiming timing = new EditorPlayStartupTiming(
            TimeSpan.Zero, TimeSpan.FromSeconds(0.2), TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(4.44), false, TimeSpan.FromSeconds(0.25));

        string summary = timing.FormatNoFrameSummary(-532462766);

        Assert.Equal("[play] save 0.2s · cook 3.0s · scripts 4.4s · launch 0.3s · no frame (exit code -532462766)", summary);
    }

    /// <summary>
    /// Ensures a stopped player's summary ends in "no frame (stopped)".
    /// </summary>
    [Fact]
    public void FormatStoppedSummary_EndsWithStopped() {
        EditorPlayStartupTiming timing = new EditorPlayStartupTiming(
            TimeSpan.Zero, TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1), true, TimeSpan.FromSeconds(0.2));

        Assert.Equal("[play] save 0.1s · cook 2.0s · scripts 1.0s (skipped) · launch 0.2s · no frame (stopped)", timing.FormatStoppedSummary());
    }

    /// <summary>
    /// Ensures the first-frame marker round-trips through its format and parse, and is not confused with log text.
    /// </summary>
    [Fact]
    public void FirstFrameMarker_FormatsAndParsesMilliseconds() {
        string line = EditorPlayFirstFrameMarker.Format(2734);

        Assert.Equal("##helengine.play first-frame-ms=2734", line);
        Assert.True(EditorPlayFirstFrameMarker.TryParseMilliseconds(line, out long milliseconds));
        Assert.Equal(2734L, milliseconds);
        Assert.True(EditorPlayFirstFrameMarker.TryParseMilliseconds(line + " \r", out long tolerated));
        Assert.Equal(2734L, tolerated);
        Assert.False(EditorPlayFirstFrameMarker.IsMarker("first-frame 2734"));
        Assert.False(EditorPlayFirstFrameMarker.IsMarker("[player] ##helengine.play first-frame-ms=2734"));
    }

    /// <summary>
    /// Ensures malformed markers are rejected without throwing.
    /// </summary>
    /// <param name="line">Malformed marker line.</param>
    [Theory]
    [InlineData("##helengine.play first-frame-ms=")]
    [InlineData("##helengine.play first-frame-ms=abc")]
    [InlineData("##helengine.play first-frame-ms=-1")]
    [InlineData("##helengine.play first-frame-ms=1.5")]
    [InlineData("##helengine.play first-frame-ms=99999999999999999999")]
    [InlineData("##helengine.play first-frame-ms=12 ms")]
    public void FirstFrameMarker_WhenMalformed_TryParseFails(string line) {
        Assert.True(EditorPlayFirstFrameMarker.IsMarker(line));
        Assert.False(EditorPlayFirstFrameMarker.TryParseMilliseconds(line, out _));
    }
}
