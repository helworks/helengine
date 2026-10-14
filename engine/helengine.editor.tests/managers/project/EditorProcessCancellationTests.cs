using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Xunit;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies the child-process cancellation a Play preparation hands to its script build: cancelling kills the attached
/// process and every process it started, raising no first-chance exception on any thread, and a process attached after
/// cancellation is killed at once and refused. The class runs alone so its process-wide first-chance count only sees
/// its own work.
/// </summary>
[Collection(EditorProcessCancellationTestCollection.Name)]
public sealed class EditorProcessCancellationTests {
    /// <summary>
    /// Longest time a test waits for a process to start or exit.
    /// </summary>
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Ensures cancelling kills a real process tree (a command shell and the ping it started after joining) and raises
    /// no first-chance exception on any thread.
    /// </summary>
    [Fact]
    public void Cancel_KillsTheWholeProcessTree_WithoutFirstChanceExceptions() {
        EditorProcessCancellation cancellation = new EditorProcessCancellation();
        using Process shell = Process.Start(new ProcessStartInfo("cmd.exe", "/q /k") {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true
        });
        Assert.True(cancellation.TryAttach(shell));
        HashSet<int> pingsBefore = GetPingProcessIds();
        shell.StandardInput.WriteLine("ping -n 60 127.0.0.1");
        shell.StandardInput.Flush();
        using Process child = WaitForNewPing(pingsBefore);
        int firstChanceCount = 0;
        EventHandler<FirstChanceExceptionEventArgs> handler = (sender, args) => Interlocked.Increment(ref firstChanceCount);

        AppDomain.CurrentDomain.FirstChanceException += handler;
        bool shellExited;
        bool childExited;
        try {
            cancellation.Cancel();
            shellExited = shell.WaitForExit((int)Timeout.TotalMilliseconds);
            childExited = child.WaitForExit((int)Timeout.TotalMilliseconds);
            cancellation.Detach();
        } finally {
            AppDomain.CurrentDomain.FirstChanceException -= handler;
        }

        Assert.True(shellExited);
        Assert.True(childExited);
        Assert.True(cancellation.IsCancellationRequested);
        Assert.Equal(0, Volatile.Read(ref firstChanceCount));
    }

    /// <summary>
    /// Ensures a process started after cancellation is refused and killed.
    /// </summary>
    [Fact]
    public void TryAttach_AfterCancel_KillsAndRefusesTheProcess() {
        EditorProcessCancellation cancellation = new EditorProcessCancellation();
        cancellation.Cancel();
        using Process process = Process.Start(new ProcessStartInfo("ping.exe", "-n 60 127.0.0.1") {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true
        });

        Assert.False(cancellation.TryAttach(process));
        Assert.True(process.WaitForExit((int)Timeout.TotalMilliseconds));
    }

    /// <summary>
    /// Ensures a build that finishes normally and is detached leaves nothing to cancel: cancelling afterwards is quiet.
    /// </summary>
    [Fact]
    public void Cancel_AfterTheProcessExitedAndDetached_IsQuiet() {
        EditorProcessCancellation cancellation = new EditorProcessCancellation();
        using Process process = Process.Start(new ProcessStartInfo("cmd.exe", "/c exit 0") { UseShellExecute = false, CreateNoWindow = true });
        cancellation.TryAttach(process);
        Assert.True(process.WaitForExit((int)Timeout.TotalMilliseconds));
        cancellation.Detach();

        cancellation.Cancel();

        Assert.True(cancellation.IsCancellationRequested);
    }

    /// <summary>
    /// Reads the ids of the ping processes running now.
    /// </summary>
    /// <returns>Ids of every running ping process.</returns>
    static HashSet<int> GetPingProcessIds() {
        HashSet<int> ids = new HashSet<int>();
        Process[] pings = Process.GetProcessesByName("PING");
        for (int index = 0; index < pings.Length; index++) {
            ids.Add(pings[index].Id);
            pings[index].Dispose();
        }

        return ids;
    }

    /// <summary>
    /// Waits until a ping process that was not running before appears.
    /// </summary>
    /// <param name="pingsBefore">Ids of the ping processes running before the shell started its ping.</param>
    /// <returns>The new ping process.</returns>
    static Process WaitForNewPing(HashSet<int> pingsBefore) {
        DateTime deadline = DateTime.UtcNow + Timeout;
        while (DateTime.UtcNow < deadline) {
            Process[] pings = Process.GetProcessesByName("PING");
            Process found = null;
            for (int index = 0; index < pings.Length; index++) {
                if (found == null && !pingsBefore.Contains(pings[index].Id)) {
                    found = pings[index];
                } else {
                    pings[index].Dispose();
                }
            }

            if (found != null) {
                return found;
            }

            Thread.Sleep(20);
        }

        throw new TimeoutException("The shell did not start its ping process.");
    }
}
