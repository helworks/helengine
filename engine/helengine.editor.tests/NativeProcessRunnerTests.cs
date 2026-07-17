using System.Diagnostics;
using helengine.baseplatform.Builders;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the shared native process runner streams and retains child-process diagnostics.
/// </summary>
public sealed class NativeProcessRunnerTests {
    /// <summary>
    /// Ensures a completed process exposes both redirected streams to the console and the returned result.
    /// </summary>
    [Fact]
    public void Run_WhenProcessWritesBothStreams_ForwardsAndRetainsEachStream() {
        if (!OperatingSystem.IsWindows()) {
            return;
        }

        ProcessStartInfo startInfo = new() {
            FileName = "cmd.exe",
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("echo native-runner-output && echo native-runner-error 1>&2");

        TextWriter originalOutput = Console.Out;
        TextWriter originalError = Console.Error;
        using StringWriter output = new();
        using StringWriter error = new();

        try {
            Console.SetOut(output);
            Console.SetError(error);

            NativeProcessRunResult result = new NativeProcessRunner().Run(startInfo, CancellationToken.None);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("native-runner-output", result.StandardOutput, StringComparison.Ordinal);
            Assert.Contains("native-runner-error", result.StandardError, StringComparison.Ordinal);
            Assert.Contains("native-runner-output", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("native-runner-error", error.ToString(), StringComparison.Ordinal);
        } finally {
            Console.SetOut(originalOutput);
            Console.SetError(originalError);
        }
    }
}
