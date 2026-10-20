using System.Diagnostics;
using System.Text.Json;
using helengine.recording.windows;
namespace helengine.tools.windowrecorder.tests;
/// <summary>Checks automation output, return codes and process packaging without capturing user windows.</summary>
public sealed class RecorderApplicationTests {
    /// <summary>The real process rejects a missing parent before even validating/opening its recording source.</summary>
    [Fact] public async Task MissingOutputDirectoryFailsDuringPreflight() {
        const string parent = "C:\\temp\\helengine-window-recording\\missing-output-parent"; Assert.False(Directory.Exists(parent));
        var start = new ProcessStartInfo("C:\\dev\\helworks\\builds\\helengine-capture\\windows\\recorder\\helengine.window-recorder.exe") { WorkingDirectory = "C:\\temp\\helengine-window-recording", UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (string argument in new[] { "record", "--hwnd", "1", "--output", Path.Combine(parent, "clip.mp4"), "--duration", "300" }) { start.ArgumentList.Add(argument); }
        using var process = Process.Start(start); var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(3, process.ExitCode); using var json = JsonDocument.Parse(await stdout); Assert.False(json.RootElement.GetProperty("hasOutput").GetBoolean());
        Assert.Contains("directory", json.RootElement.GetProperty("errorMessage").GetString(), StringComparison.OrdinalIgnoreCase); Assert.Contains("directory", await stderr, StringComparison.OrdinalIgnoreCase);
    }
    /// <summary>Metadata listing never opens a recording source or encoder.</summary>
    [Fact] public async Task ListsMetadataWithoutOpeningCapture() {
        var app = new RecorderApplication((_, _) => throw new InvalidOperationException("capture must not open"), () => [new() { Hwnd = "0x123", ProcessId = 7, ProcessName = "fixture", Title = "janela ç" }]);
        using var stdout = new StringWriter(); using var stderr = new StringWriter();
        Assert.Equal(0, await app.RunAsync(["windows"], stdout, stderr, default));
        using var json = JsonDocument.Parse(stdout.ToString()); Assert.Equal("0x123", json.RootElement[0].GetProperty("hwnd").GetString()); Assert.Empty(stderr.ToString());
    }
    /// <summary>Failure with completed prefix is still exit3 and remains one machine-readable JSON document.</summary>
    [Theory][InlineData(true, WindowRecordingStopReason.DurationReached, 0)][InlineData(true, WindowRecordingStopReason.Resized, 3)][InlineData(false, WindowRecordingStopReason.EncoderFailed, 3)]
    public async Task MapsResultsToExitCodes(bool hasOutput, WindowRecordingStopReason reason, int expected) {
        var app = new RecorderApplication((_, _) => Task.FromResult(new WindowRecordingResult { HasOutput = hasOutput, StopReason = reason, OutputPath = hasOutput ? "clip.mp4" : null, ErrorMessage = expected == 3 ? "operation failed" : null }), () => []);
        using var stdout = new StringWriter(); using var stderr = new StringWriter();
        Assert.Equal(expected, await app.RunAsync(["record", "--hwnd", "1", "--output", "x.mp4"], stdout, stderr, default));
        using var json = JsonDocument.Parse(stdout.ToString()); Assert.Equal(hasOutput, json.RootElement.GetProperty("hasOutput").GetBoolean());
        Assert.Equal(expected == 0, json.RootElement.GetProperty("succeeded").GetBoolean());
        if (expected == 3) { Assert.Contains("operation failed", stderr.ToString()); }
    }
    /// <summary>Bad input returns exit2 with structured output, without touching capture.</summary>
    [Fact] public async Task WritesOnlyOneJsonDocumentToStdout() {
        var app = new RecorderApplication((_, _) => throw new InvalidOperationException(), () => []);
        using var stdout = new StringWriter(); using var stderr = new StringWriter(); Assert.Equal(2, await app.RunAsync(["record"], stdout, stderr, default));
        using var json = JsonDocument.Parse(stdout.ToString()); Assert.False(json.RootElement.GetProperty("hasOutput").GetBoolean()); Assert.NotEmpty(stderr.ToString());
    }
    /// <summary>Cancellation is forwarded as graceful stop, and the host waits for finalization to finish.</summary>
    [Fact] public async Task CtrlCRequestsGracefulCompletion() {
        using var stop = new CancellationTokenSource(); var completion = new TaskCompletionSource<WindowRecordingResult>(); CancellationToken forwarded = default;
        var app = new RecorderApplication((_, token) => { forwarded = token; return completion.Task; }, () => []);
        using var stdout = new StringWriter(); using var stderr = new StringWriter(); var running = app.RunAsync(["record", "--hwnd", "1", "--output", "x.mp4"], stdout, stderr, stop.Token);
        stop.Cancel(); Assert.True(forwarded.IsCancellationRequested); Assert.False(running.IsCompleted);
        completion.SetResult(new() { HasOutput = true, StopReason = WindowRecordingStopReason.StopRequested }); Assert.Equal(0, await running);
    }
    /// <summary>The packaged process resolves managed/native dependencies without using the repository as cwd.</summary>
    [Fact] public async Task PackagedCliWorksOutsideRepository() {
        const string executable = "C:\\dev\\helworks\\builds\\helengine-capture\\windows\\recorder\\helengine.window-recorder.exe";
        var start = new ProcessStartInfo(executable) { WorkingDirectory = "C:\\temp\\helengine-window-recording", UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true }; start.ArgumentList.Add("windows");
        using var process = Process.Start(start); var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(); Assert.Equal(0, process.ExitCode); using var json = JsonDocument.Parse(await stdout); Assert.Equal(JsonValueKind.Array, json.RootElement.ValueKind); Assert.Empty(await stderr);
    }
    /// <summary>A missing native capture dependency produces structured failure rather than an empty success.</summary>
    [Fact] public async Task MissingNativeDependencyProducesExit3() {
        const string package = "C:\\dev\\helworks\\builds\\helengine-capture\\windows\\recorder";
        const string incomplete = "C:\\temp\\helengine-window-recording\\missing-dependency";
        Directory.CreateDirectory(incomplete);
        foreach (string file in Directory.EnumerateFiles(package)) {
            if (Path.GetFileName(file) != "helengine.capture.windows.native.dll") { File.Copy(file, Path.Combine(incomplete, Path.GetFileName(file)), true); }
        }
        var start = new ProcessStartInfo(Path.Combine(incomplete, "helengine.window-recorder.exe")) { WorkingDirectory = incomplete, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (string argument in new[] { "record", "--hwnd", "1", "--output", "C:\\temp\\helengine-window-recording\\missing-dependency-output.mp4", "--duration", "1" }) { start.ArgumentList.Add(argument); }
        using var process = Process.Start(start); var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(); Assert.Equal(3, process.ExitCode);
        using var json = JsonDocument.Parse(await stdout); Assert.False(json.RootElement.GetProperty("hasOutput").GetBoolean());
        Assert.Contains("helengine.capture.windows.native", json.RootElement.GetProperty("errorMessage").GetString()); Assert.NotEmpty(await stderr);
    }
}
