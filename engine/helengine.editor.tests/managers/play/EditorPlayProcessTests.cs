using System.Diagnostics;
using Xunit;

namespace helengine.editor.tests.managers.play;

/// <summary>
/// Verifies the real player process wrapper against a short-lived console process: console lines and the exit are
/// delivered only when the frame thread drains the action queue, and the exit arrives after every console line.
/// </summary>
public sealed class EditorPlayProcessTests {
    /// <summary>
    /// Maximum time a test waits for the console process to finish.
    /// </summary>
    static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Ensures output, error and exit events are raised on the draining thread, in order, with the real exit code.
    /// </summary>
    [Fact]
    public void Start_DeliversConsoleLinesThenExitOnlyWhenQueueIsDrained() {
        EditorMainThreadActionQueue queue = new EditorMainThreadActionQueue();
        EditorPlayProcessLauncher launcher = new EditorPlayProcessLauncher(queue);
        List<string> outputLines = new List<string>();
        List<string> errorLines = new List<string>();
        List<int> eventThreadIds = new List<int>();
        int exitedCount = 0;
        int outputLineCountAtExit = -1;
        int errorLineCountAtExit = -1;

        IEditorPlayProcess process = launcher.Start(CreateCommandRequest("echo hello& echo oops 1>&2& exit /b 3"));
        process.OutputLine += line => {
            outputLines.Add(line);
            eventThreadIds.Add(Environment.CurrentManagedThreadId);
        };
        process.ErrorLine += line => {
            errorLines.Add(line.Trim());
            eventThreadIds.Add(Environment.CurrentManagedThreadId);
        };
        process.Exited += () => {
            exitedCount++;
            outputLineCountAtExit = outputLines.Count;
            errorLineCountAtExit = errorLines.Count;
            eventThreadIds.Add(Environment.CurrentManagedThreadId);
        };

        WaitForRealExit(process.ProcessId);
        Thread.Sleep(200);
        Assert.Empty(outputLines);
        Assert.Empty(errorLines);
        Assert.Equal(0, exitedCount);
        Assert.False(process.HasExited);

        Stopwatch stopwatch = Stopwatch.StartNew();
        while (exitedCount == 0 && stopwatch.Elapsed < ProcessTimeout) {
            queue.ProcessPending();
            Thread.Sleep(10);
        }

        Assert.Equal(1, exitedCount);
        Assert.True(process.HasExited);
        Assert.Equal(3, process.ExitCode);
        Assert.True(process.ProcessId > 0);
        Assert.Equal(new[] { "hello" }, outputLines);
        Assert.Equal(new[] { "oops" }, errorLines);
        Assert.Equal(1, outputLineCountAtExit);
        Assert.Equal(1, errorLineCountAtExit);
        Assert.All(eventThreadIds, threadId => Assert.Equal(Environment.CurrentManagedThreadId, threadId));
    }

    /// <summary>
    /// Ensures Kill terminates a running process and is a no-op once the process is gone.
    /// </summary>
    [Fact]
    public void Kill_TerminatesRunningProcessAndIgnoresRepeatCalls() {
        EditorMainThreadActionQueue queue = new EditorMainThreadActionQueue();
        EditorPlayProcessLauncher launcher = new EditorPlayProcessLauncher(queue);
        IEditorPlayProcess process = launcher.Start(CreateCommandRequest("ping -n 30 127.0.0.1 >nul"));

        process.Kill();
        WaitForRealExit(process.ProcessId);
        process.Kill();

        Stopwatch stopwatch = Stopwatch.StartNew();
        while (!process.HasExited && stopwatch.Elapsed < ProcessTimeout) {
            queue.ProcessPending();
            Thread.Sleep(10);
        }

        Assert.True(process.HasExited);
    }

    /// <summary>
    /// Ensures a missing player executable fails before any process is started.
    /// </summary>
    [Fact]
    public void Start_WhenExecutableIsMissing_ThrowsFileNotFound() {
        EditorPlayProcessLauncher launcher = new EditorPlayProcessLauncher(new EditorMainThreadActionQueue());
        string missingPath = Path.Combine(@"C:\dev\helworks\builds\helengine\play-tests", Guid.NewGuid().ToString("N"), "helengine.player.exe");

        Assert.Throws<FileNotFoundException>(() => launcher.Start(new EditorPlayLaunchRequest(missingPath, new List<string>())));
    }

    /// <summary>
    /// Creates a launch request that runs one command line through the Windows command interpreter.
    /// </summary>
    /// <param name="commandLine">Command interpreter line to run.</param>
    /// <returns>Launch request for the command interpreter.</returns>
    static EditorPlayLaunchRequest CreateCommandRequest(string commandLine) {
        string commandInterpreterPath = Environment.GetEnvironmentVariable("ComSpec");
        Assert.False(string.IsNullOrWhiteSpace(commandInterpreterPath));
        return new EditorPlayLaunchRequest(commandInterpreterPath, new List<string> { "/c", commandLine });
    }

    /// <summary>
    /// Blocks until the operating-system process has exited, independent of the wrapper's frame-thread view.
    /// </summary>
    /// <param name="processId">Operating-system process identifier.</param>
    static void WaitForRealExit(int processId) {
        Process observer;
        try {
            observer = Process.GetProcessById(processId);
        } catch (ArgumentException) {
            return;
        }

        using (observer) {
            Assert.True(observer.WaitForExit((int)ProcessTimeout.TotalMilliseconds));
        }
    }
}
