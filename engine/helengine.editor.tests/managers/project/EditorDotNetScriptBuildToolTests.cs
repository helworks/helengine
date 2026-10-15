using System.Diagnostics;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies the script build's output capture drains redirected process output without deadlocking on warning-heavy child
/// processes and without losing anything the process wrote.
/// </summary>
public sealed class EditorDotNetScriptBuildToolTests {
    /// <summary>
    /// Ensures the output capture drains large interleaved standard-output and standard-error streams, keeping both pipes
    /// moving so the child never blocks on a full pipe, and returns every line written to each stream.
    /// </summary>
    [Fact]
    public void CaptureProcessOutput_drains_large_interleaved_stdout_and_stderr_without_deadlocking() {
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
