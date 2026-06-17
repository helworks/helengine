using System.Diagnostics;
using System.Reflection;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies the headless script build tool drains redirected process output without deadlocking on warning-heavy child processes.
/// </summary>
public sealed class EditorDotNetScriptBuildToolTests {
    /// <summary>
    /// Ensures the asynchronous output capture path drains large interleaved standard-output and standard-error streams and returns the captured text.
    /// </summary>
    [Fact]
    public void CaptureProcessOutput_drains_large_interleaved_stdout_and_stderr_without_deadlocking() {
        EditorDotNetScriptBuildTool buildTool = new EditorDotNetScriptBuildTool();
        MethodInfo captureMethod = typeof(EditorDotNetScriptBuildTool).GetMethod(
            "CaptureProcessOutput",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(captureMethod);

        using Process process = StartLargeInterleavedOutputProcess();
        Task<(string Stdout, string Stderr)> captureTask = Task.Run(() => InvokeCaptureProcessOutput(captureMethod, buildTool, process));
        bool completed = captureTask.Wait(TimeSpan.FromSeconds(20));

        if (!completed && !process.HasExited) {
            process.Kill(true);
        }

        Assert.True(completed, "Timed out while draining redirected process output.");

        (string stdout, string stderr) = captureTask.Result;
        Assert.Contains("OUT-1", stdout, StringComparison.Ordinal);
        Assert.Contains("OUT-12000", stdout, StringComparison.Ordinal);
        Assert.Contains("ERR-1", stderr, StringComparison.Ordinal);
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

    /// <summary>
    /// Invokes the private output-capture routine through reflection and returns the captured redirected text.
    /// </summary>
    /// <param name="captureMethod">Private capture routine discovered on the build tool.</param>
    /// <param name="buildTool">Build tool instance that owns the capture routine.</param>
    /// <param name="process">Child process whose redirected streams should be drained.</param>
    /// <returns>Captured standard-output and standard-error text.</returns>
    static (string Stdout, string Stderr) InvokeCaptureProcessOutput(MethodInfo captureMethod, EditorDotNetScriptBuildTool buildTool, Process process) {
        if (captureMethod == null) {
            throw new ArgumentNullException(nameof(captureMethod));
        }
        if (buildTool == null) {
            throw new ArgumentNullException(nameof(buildTool));
        }
        if (process == null) {
            throw new ArgumentNullException(nameof(process));
        }

        object[] arguments = [process, string.Empty, string.Empty];
        captureMethod.Invoke(buildTool, arguments);
        return ((string)arguments[1], (string)arguments[2]);
    }
}
