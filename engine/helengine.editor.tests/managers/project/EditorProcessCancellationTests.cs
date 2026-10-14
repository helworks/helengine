using System.Diagnostics;
using Xunit;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies the child-process cancellation a Play preparation hands to its script build: cancelling kills the attached
/// running process, and a process attached after cancellation is killed at once and refused.
/// </summary>
public sealed class EditorProcessCancellationTests {
    /// <summary>
    /// Longest time a test waits for a killed process to exit.
    /// </summary>
    static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Ensures cancelling kills the attached process tree.
    /// </summary>
    [Fact]
    public void Cancel_KillsTheAttachedProcess() {
        EditorProcessCancellation cancellation = new EditorProcessCancellation();
        using Process process = StartLongRunningProcess();

        Assert.True(cancellation.TryAttach(process));
        cancellation.Cancel();

        Assert.True(process.WaitForExit((int)ExitTimeout.TotalMilliseconds));
        Assert.True(cancellation.IsCancellationRequested);
        cancellation.Detach();
    }

    /// <summary>
    /// Ensures a process started after cancellation is refused and killed.
    /// </summary>
    [Fact]
    public void TryAttach_AfterCancel_KillsAndRefusesTheProcess() {
        EditorProcessCancellation cancellation = new EditorProcessCancellation();
        cancellation.Cancel();
        using Process process = StartLongRunningProcess();

        Assert.False(cancellation.TryAttach(process));
        Assert.True(process.WaitForExit((int)ExitTimeout.TotalMilliseconds));
    }

    /// <summary>
    /// Ensures cancelling after the attached process already exited does nothing and raises no exception.
    /// </summary>
    [Fact]
    public void Cancel_AfterTheProcessExited_IsQuiet() {
        EditorProcessCancellation cancellation = new EditorProcessCancellation();
        using Process process = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit 0") { UseShellExecute = false, CreateNoWindow = true });
        Assert.True(cancellation.TryAttach(process));
        Assert.True(process.WaitForExit((int)ExitTimeout.TotalMilliseconds));

        cancellation.Cancel();

        Assert.True(cancellation.IsCancellationRequested);
    }

    /// <summary>
    /// Starts a process that runs for about half a minute unless it is killed.
    /// </summary>
    /// <returns>Started process.</returns>
    static Process StartLongRunningProcess() {
        return Process.Start(new ProcessStartInfo("ping.exe", "-n 30 127.0.0.1") {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true
        });
    }
}
