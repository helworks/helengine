using System.Diagnostics;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies the script build's output capture drains redirected process output without deadlocking on warning-heavy child
/// processes and without losing anything the process wrote.
/// </summary>
public sealed class EditorDotNetScriptBuildToolTests {
    /// <summary>
    /// Number of short-lived children <see cref="Capture_keeps_output_written_just_before_the_process_exits"/> runs; each
    /// one catches a capture that skips the drain after exit most of the time, so together they catch it every run.
    /// </summary>
    const int FinalWriteAttempts = 40;

    /// <summary>
    /// Ensures the output capture drains large interleaved standard-output and standard-error streams, keeping both pipes
    /// moving so the child never blocks on a full pipe, and returns every line written to each stream.
    /// </summary>
    [Fact]
    public void Capture_drains_large_interleaved_stdout_and_stderr_without_deadlocking() {
        using Process process = StartLargeInterleavedOutputProcess();
        string stdout = string.Empty;
        string stderr = string.Empty;
        Task captureTask = Task.Run(() => EditorBuildProcessOutputCapture.Capture(process, out stdout, out stderr));
        bool completed = captureTask.Wait(TimeSpan.FromSeconds(20));

        if (!completed && !process.HasExited) {
            process.Kill(true);
        }

        Assert.True(completed, "Timed out while draining redirected process output.");
        Assert.Contains("OUT-1" + Environment.NewLine, stdout, StringComparison.Ordinal);
        Assert.Contains("OUT-12000", stdout, StringComparison.Ordinal);
        Assert.Contains("ERR-1" + Environment.NewLine, stderr, StringComparison.Ordinal);
        Assert.Contains("ERR-12000", stderr, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures output written right before the process exits is captured. Each child starts, writes one short marker to
    /// each stream and exits at once, so the marker usually lands while the capture is waiting for the exit; only the
    /// drain after the exit picks it up. Repeating the child makes a capture that skips that drain fail every run instead
    /// of only when a write happens to land just before a check.
    /// </summary>
    [Fact]
    public void Capture_keeps_output_written_just_before_the_process_exits() {
        for (int attempt = 0; attempt < FinalWriteAttempts; attempt++) {
            using Process process = StartFinalWriteProcess(attempt);
            string stdout = string.Empty;
            string stderr = string.Empty;
            Task captureTask = Task.Run(() => EditorBuildProcessOutputCapture.Capture(process, out stdout, out stderr));
            bool completed = captureTask.Wait(TimeSpan.FromSeconds(20));

            Assert.True(completed, $"Timed out while capturing attempt {attempt}.");
            Assert.Contains("FINAL-OUT-" + attempt, stdout, StringComparison.Ordinal);
            Assert.Contains("FINAL-ERR-" + attempt, stderr, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Starts one <c>cmd.exe</c> that writes a short marker to standard output and standard error and exits immediately.
    /// <c>cmd.exe</c> exits within a millisecond or so of its last write, far less than the capture's idle wait.
    /// </summary>
    /// <param name="attempt">Attempt number embedded in the markers.</param>
    /// <returns>Running process configured for redirected output capture.</returns>
    static Process StartFinalWriteProcess(int attempt) {
        ProcessStartInfo startInfo = new ProcessStartInfo {
            FileName = "cmd.exe",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("/d");
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("echo FINAL-OUT-" + attempt + "&echo FINAL-ERR-" + attempt + " 1>&2");

        Process process = Process.Start(startInfo);
        return process ?? throw new InvalidOperationException("Failed to start the final-write child process.");
    }

    /// <summary>
    /// Starts one child PowerShell process that writes a large amount of text to both redirected output streams.
    /// </summary>
    /// <returns>Running process configured for redirected output capture.</returns>
    static Process StartLargeInterleavedOutputProcess() {
        ProcessStartInfo startInfo = new ProcessStartInfo {
            FileName = "powershell",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add("$stdout = [Console]::Out; $stderr = [Console]::Error; foreach ($index in 1..12000) { $stdout.WriteLine(\"OUT-$index\"); $stderr.WriteLine(\"ERR-$index\") }");

        Process process = Process.Start(startInfo);
        return process ?? throw new InvalidOperationException("Failed to start the output-flood child process.");
    }
}
