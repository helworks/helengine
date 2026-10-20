using System.Diagnostics;
using System.Text.Json;
namespace helengine.recording.windows.tests;
/// <summary>Captures exclusively synthetic windows owned by these tests; never the desktop or user applications.</summary>
[Trait("Category", "Integration")]
public sealed class WindowRecordingIntegrationTests {
    /// <summary>Measures sustained 1080p60 and verifies real container timestamps and decoded source content.</summary>
    [Fact] public async Task RecordsOwn1080pWindowForTenSeconds() {
        using var window = new RecordingTestWindow(1920, 1080); string path = FreshPath("integration-1080p.mp4");
        var result = await new WindowRecorder().RecordAsync(new() { WindowHandle = window.Handle, OutputPath = path, Duration = TimeSpan.FromSeconds(10) });
        await File.WriteAllTextAsync("C:\\temp\\helengine-window-recording\\1080p-result.json", JsonSerializer.Serialize(result));
        Assert.True(result.Succeeded, JsonSerializer.Serialize(result)); Assert.Equal(600, result.EncodedFrames); Assert.Equal(TimeSpan.FromSeconds(10), result.EncodedDuration);
        Assert.InRange(result.RecordingDuration.TotalSeconds, 10, 10.25); Assert.InRange(result.MaxLag.TotalMilliseconds, 0, 250);
        Assert.Equal(1920, result.SourceWidth); Assert.Equal(1080, result.SourceHeight); Assert.NotNull(result.CaptureStatistics); Assert.True(result.CaptureStatistics.DeliveredFrames > 1);
        await RecordedVideoAssertions.AssertStreamAsync(path, 1920, 1080, 600); await RecordedVideoAssertions.AssertColorsAndMotionAsync(path, 1920, 1080, 599);
    }
    /// <summary>Odd source dimensions stay unscaled and receive only black even-dimension padding.</summary>
    [Fact] public async Task RecordsOddSizedOwnWindowWithBlackPadding() {
        using var window = new RecordingTestWindow(641, 479); string path = FreshPath("integration-odd.mp4");
        var result = await new WindowRecorder().RecordAsync(new() { WindowHandle = window.Handle, OutputPath = path, Duration = TimeSpan.FromSeconds(1) });
        Assert.True(result.Succeeded, JsonSerializer.Serialize(result)); Assert.Equal(641, result.SourceWidth); Assert.Equal(479, result.SourceHeight);
        await RecordedVideoAssertions.AssertStreamAsync(path, 642, 480, 60); byte[] pixels = await RecordedVideoProbe.DecodeAsync(path, 0, "yuv420p");
        Assert.InRange((int)pixels[642 * 480 - 1], 10, 22);
    }
    /// <summary>Real source closure and host cancellation both drain a valid nonempty prefix.</summary>
    [Theory][InlineData(true)][InlineData(false)] public async Task FinalizesOnOwnWindowClosureAndGracefulStop(bool close) {
        using var window = new RecordingTestWindow(); using var stop = new CancellationTokenSource(); string path = FreshPath(close ? "integration-close.mp4" : "integration-stop.mp4");
        var recording = new WindowRecorder().RecordAsync(new() { WindowHandle = window.Handle, OutputPath = path }, stop.Token);
        await Task.Delay(2000); if (close) { window.Dispose(); } else { stop.Cancel(); }
        var result = await recording.WaitAsync(TimeSpan.FromSeconds(20)); Assert.True(result.Succeeded, JsonSerializer.Serialize(result)); Assert.True(result.EncodedFrames > 0);
        Assert.Equal(close ? WindowRecordingStopReason.WindowClosed : WindowRecordingStopReason.StopRequested, result.StopReason); await RecordedVideoAssertions.AssertStreamAsync(path, 640, 480, result.EncodedFrames);
    }
    /// <summary>Resize is reported as failure even though a finalized prefix can still be saved.</summary>
    [Fact] public async Task ResizeReturnsFailureWithValidPrefix() {
        using var window = new RecordingTestWindow(); string path = FreshPath("integration-resize.mp4");
        var recording = new WindowRecorder().RecordAsync(new() { WindowHandle = window.Handle, OutputPath = path, Duration = TimeSpan.FromSeconds(10) });
        await Task.Delay(2000); window.Resize(642, 480); var result = await recording.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.False(result.Succeeded); Assert.True(result.HasOutput, JsonSerializer.Serialize(result)); Assert.Equal(WindowRecordingStopReason.Resized, result.StopReason); Assert.InRange(result.EncodedFrames, 1, 599);
        await RecordedVideoAssertions.AssertStreamAsync(path, 640, 480, result.EncodedFrames);
    }
    /// <summary>The packaged CLI records the exact fixture HWND and emits a complete JSON report.</summary>
    [Fact] public async Task CliRecordsOnlyExplicitTestHwnd() {
        using var window = new RecordingTestWindow(); string path = FreshPath("integration-cli ação.mp4");
        var start = new ProcessStartInfo("C:\\dev\\helworks\\builds\\helengine-capture\\windows\\recorder\\helengine.window-recorder.exe") { WorkingDirectory = "C:\\temp\\helengine-window-recording", UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (string argument in new[] { "record", "--hwnd", "0x" + window.Handle.ToInt64().ToString("X", System.Globalization.CultureInfo.InvariantCulture), "--output", path, "--duration", "1", "--fps", "60", "--bitrate", "40M", "--max-bitrate", "60M" }) { start.ArgumentList.Add(argument); }
        using var process = Process.Start(start); var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        string report = await stdout; string diagnostic = await stderr; Assert.True(process.ExitCode == 0, report + diagnostic);
        using var json = JsonDocument.Parse(report); Assert.True(json.RootElement.GetProperty("hasOutput").GetBoolean()); Assert.Equal(60, json.RootElement.GetProperty("encodedFrames").GetInt64()); Assert.Equal(path, json.RootElement.GetProperty("outputPath").GetString());
        await RecordedVideoAssertions.AssertStreamAsync(path, 640, 480, 60);
    }
    /// <summary>Reuses fixed fixture paths by deleting only prior outputs belonging to this test.</summary>
    static string FreshPath(string name) {
        string path = Path.Combine("C:\\temp\\helengine-window-recording", name); if (File.Exists(path)) { File.Delete(path); } return path;
    }
}
