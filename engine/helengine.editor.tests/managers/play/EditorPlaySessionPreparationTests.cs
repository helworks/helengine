using System.Runtime.ExceptionServices;
using helengine.baseplatform.Definitions;
using Xunit;

namespace helengine.editor.tests.managers.play;

/// <summary>
/// Verifies that Play prepares in the background: the cook and the script build run off the calling (editor frame)
/// thread, Play returns while they run, a second Play is ignored, Stop cancels the preparation so the player never
/// launches, failures are reported, and neither the success nor the cancel path raises a first-chance exception.
/// </summary>
public sealed class EditorPlaySessionPreparationTests {
    /// <summary>
    /// Player executable path handed to the service under test.
    /// </summary>
    const string PlayerExecutablePath = @"C:\player\helengine.player.exe";

    /// <summary>
    /// Saved scene path handed to every Play.
    /// </summary>
    const string ScenePath = @"C:\project\assets\scenes\cube_test.hscene";

    /// <summary>
    /// Longest time a test waits for the preparation thread; generous because test classes run in parallel.
    /// </summary>
    static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Main-thread queue the service reports its finished preparation through; the test drains it as the editor frame does.
    /// </summary>
    readonly EditorMainThreadActionQueue Queue = new EditorMainThreadActionQueue();

    /// <summary>
    /// Ensures Play returns while the cook is still running on another thread, presents the preparing state, and
    /// launches only once the cook finished and the queue was drained on the calling thread.
    /// </summary>
    [Fact]
    public void Play_RunsTheCookOffTheCallingThread_AndReturnsWhileItRuns() {
        using BlockingPlayCookRunner cookRunner = new BlockingPlayCookRunner(CreateSucceedingCookResult());
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = CreateThreadedService(cookRunner, launcher);
        EditorBuildExecutionResult launchResult = null;

        EditorBuildExecutionResult startResult = StartPlay(service, result => launchResult = result);

        Assert.True(startResult.Succeeded, startResult.Message);
        Assert.True(cookRunner.WaitUntilStarted(WaitTimeout));
        Assert.NotEqual(Environment.CurrentManagedThreadId, cookRunner.CookThreadId);
        Assert.Equal(EditorPlaySessionState.Preparing, service.State);
        Assert.True(service.IsPreparationActive);
        Assert.Empty(launcher.Requests);

        cookRunner.Release();
        DrainUntil(() => launchResult != null);

        Assert.True(launchResult.Succeeded, launchResult.Message);
        Assert.Equal("Player started (pid 1000).", launchResult.Message);
        Assert.Single(launcher.Requests);
        Assert.Equal(EditorPlaySessionState.Running, service.State);
        Assert.False(service.IsPreparationActive);
    }

    /// <summary>
    /// Ensures a Play pressed while another is preparing is ignored: it neither saves, cooks nor launches again, and the
    /// first preparation still launches exactly one player.
    /// </summary>
    [Fact]
    public void Play_WhilePreparing_IsIgnored() {
        FakePlayCookRunner cookRunner = new FakePlayCookRunner(CreateSucceedingCookResult());
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        InlinePlayPreparationScheduler scheduler = new InlinePlayPreparationScheduler();
        EditorPlaySessionService service = new EditorPlaySessionService(cookRunner, CreateSucceedingScriptBuilder(), launcher, PlayerExecutablePath, Queue, scheduler, TestPlaySettings.CreateWriter());
        int saveCount = 0;
        int completionCount = 0;
        StartPlay(service, () => { saveCount++; return true; }, result => completionCount++);

        EditorBuildExecutionResult secondResult = StartPlay(service, () => { saveCount++; return true; }, result => completionCount++);
        Queue.ProcessPending();

        Assert.False(secondResult.Succeeded);
        Assert.Equal(EditorPlaySessionService.AlreadyPreparingMessage, secondResult.Message);
        Assert.Equal(1, saveCount);
        Assert.Equal(1, scheduler.StartCount);
        Assert.Single(cookRunner.CookedPlatformIds);
        Assert.Single(launcher.Requests);
        Assert.Equal(1, completionCount);
        Assert.Equal(EditorPlaySessionState.Running, service.State);
    }

    /// <summary>
    /// Ensures Stop during preparation ends the session stopped at once, skips the script build, never launches the
    /// player and never reports a launch, even after the cook finishes. A Play pressed while the stopped cook is still
    /// finishing is refused with the reason, and Play works again once the stopped preparation has completed.
    /// </summary>
    [Fact]
    public void Stop_WhilePreparing_NeverLaunchesThePlayer() {
        using BlockingPlayCookRunner cookRunner = new BlockingPlayCookRunner(CreateSucceedingCookResult());
        FakePlayScriptBuilder scriptBuilder = CreateSucceedingScriptBuilder();
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = new EditorPlaySessionService(cookRunner, scriptBuilder, launcher, PlayerExecutablePath, Queue, new EditorPlayPreparationThreadScheduler(), TestPlaySettings.CreateWriter());
        int completionCount = 0;
        StartPlay(service, result => completionCount++);
        Assert.True(cookRunner.WaitUntilStarted(WaitTimeout));

        service.Stop();
        EditorBuildExecutionResult playWhileStopping = StartPlay(service, result => completionCount++);
        cookRunner.Release();
        DrainUntil(() => !service.IsPreparationActive);

        Assert.Equal(EditorPlaySessionState.Stopped, service.State);
        Assert.False(playWhileStopping.Succeeded);
        Assert.Equal(EditorPlaySessionService.PreviousPreparationStoppingMessage, playWhileStopping.Message);
        Assert.Empty(scriptBuilder.BuiltPlatformIds);
        Assert.Empty(launcher.Requests);
        Assert.Equal(0, completionCount);

        EditorBuildExecutionResult launchResult = null;
        Assert.True(StartPlay(service, result => launchResult = result).Succeeded);
        DrainUntil(() => launchResult != null);
        Assert.True(launchResult.Succeeded, launchResult.Message);
        Assert.Single(launcher.Requests);
    }

    /// <summary>
    /// Ensures closing the editor while the cook runs on its real background thread stops Play, waits a bounded time
    /// for the preparation thread (reporting when it is still busy), and never launches the player or runs the script
    /// build, with no first-chance exception on either thread. The completion is never drained, as after a close.
    /// </summary>
    [Fact]
    public void Shutdown_WhileTheCookRunsOnItsThread_WaitsBoundedAndNeverLaunches() {
        using BlockingPlayCookRunner cookRunner = new BlockingPlayCookRunner(CreateSucceedingCookResult());
        FakePlayScriptBuilder scriptBuilder = CreateSucceedingScriptBuilder();
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = new EditorPlaySessionService(cookRunner, scriptBuilder, launcher, PlayerExecutablePath, Queue, new EditorPlayPreparationThreadScheduler(), TestPlaySettings.CreateWriter());
        int completionCount = 0;
        List<int> exceptionThreadIds = new List<int>();
        EventHandler<FirstChanceExceptionEventArgs> handler = (sender, args) => {
            lock (exceptionThreadIds) {
                exceptionThreadIds.Add(Environment.CurrentManagedThreadId);
            }
        };
        bool finishedWhileCookBlocked;
        bool finishedAfterRelease;

        AppDomain.CurrentDomain.FirstChanceException += handler;
        try {
            StartPlay(service, result => completionCount++);
            Assert.True(cookRunner.WaitUntilStarted(WaitTimeout));
            finishedWhileCookBlocked = service.Shutdown(TimeSpan.FromMilliseconds(50));
            cookRunner.Release();
            finishedAfterRelease = service.Shutdown(WaitTimeout);
        } finally {
            AppDomain.CurrentDomain.FirstChanceException -= handler;
        }

        Assert.False(finishedWhileCookBlocked);
        Assert.True(finishedAfterRelease);
        Assert.Equal(EditorPlaySessionState.Stopped, service.State);
        Assert.Empty(scriptBuilder.BuiltPlatformIds);
        Assert.Empty(launcher.Requests);
        Assert.Equal(0, completionCount);
        Assert.Equal(0, CountOnThreads(exceptionThreadIds, Environment.CurrentManagedThreadId, cookRunner.CookThreadId));
    }

    /// <summary>
    /// Ensures the script build receives the preparation's process cancellation and Stop cancels it, which is what
    /// kills a running <c>dotnet build</c> process tree.
    /// </summary>
    [Fact]
    public void Stop_CancelsTheScriptBuildProcessCancellation() {
        FakePlayScriptBuilder scriptBuilder = CreateSucceedingScriptBuilder();
        EditorPlaySessionService service = new EditorPlaySessionService(
            new FakePlayCookRunner(CreateSucceedingCookResult()),
            scriptBuilder,
            new FakePlayProcessLauncher(),
            PlayerExecutablePath,
            Queue,
            new InlinePlayPreparationScheduler(),
            TestPlaySettings.CreateWriter());
        StartPlay(service, result => { });
        Assert.NotNull(scriptBuilder.LastCancellation);
        Assert.False(scriptBuilder.LastCancellation.IsCancellationRequested);

        service.Stop();

        Assert.True(scriptBuilder.LastCancellation.IsCancellationRequested);
    }

    /// <summary>
    /// Ensures a preparation whose completion is never drained never launches the player once stopped: nothing
    /// launches outside the frame thread's drain.
    /// </summary>
    [Fact]
    public void StopBeforeTheCompletionIsDrained_NeverLaunchesThePlayer() {
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = new EditorPlaySessionService(
            new FakePlayCookRunner(CreateSucceedingCookResult()),
            CreateSucceedingScriptBuilder(),
            launcher,
            PlayerExecutablePath,
            Queue,
            new InlinePlayPreparationScheduler(),
            TestPlaySettings.CreateWriter());
        StartPlay(service, result => { });

        service.Stop();

        Assert.Empty(launcher.Requests);
        Assert.Equal(EditorPlaySessionState.Stopped, service.State);
        Queue.ProcessPending();
        Assert.Empty(launcher.Requests);
        Assert.Equal(EditorPlaySessionState.Stopped, service.State);
    }

    /// <summary>
    /// Ensures an exception thrown by the cook on the preparation thread is reported through the completion as a Play
    /// failure carrying the exception, and the session ends stopped without launching.
    /// </summary>
    [Fact]
    public void Preparation_WhenTheCookThrows_ReportsTheErrorAndEndsStopped() {
        InvalidOperationException failure = new InvalidOperationException("Cook blew up.");
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = new EditorPlaySessionService(
            new ThrowingPlayCookRunner(failure),
            CreateSucceedingScriptBuilder(),
            launcher,
            PlayerExecutablePath,
            Queue,
            new InlinePlayPreparationScheduler(),
            TestPlaySettings.CreateWriter());
        EditorBuildExecutionResult launchResult = null;

        Assert.True(StartPlay(service, result => launchResult = result).Succeeded);
        Queue.ProcessPending();

        Assert.NotNull(launchResult);
        Assert.False(launchResult.Succeeded);
        Assert.StartsWith("Play failed: System.InvalidOperationException: Cook blew up.", launchResult.Message, StringComparison.Ordinal);
        Assert.Empty(launcher.Requests);
        Assert.Equal(EditorPlaySessionState.Stopped, service.State);
        Assert.False(service.IsPreparationActive);
    }

    /// <summary>
    /// Ensures a Play that prepares on its own thread and launches raises no first-chance exception on the calling
    /// thread or the preparation thread.
    /// </summary>
    [Fact]
    public void SuccessfulPreparation_RaisesNoFirstChanceExceptions() {
        using BlockingPlayCookRunner cookRunner = new BlockingPlayCookRunner(CreateSucceedingCookResult());
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = CreateThreadedService(cookRunner, launcher);
        EditorBuildExecutionResult launchResult = null;
        List<int> exceptionThreadIds = new List<int>();
        EventHandler<FirstChanceExceptionEventArgs> handler = (sender, args) => {
            lock (exceptionThreadIds) {
                exceptionThreadIds.Add(Environment.CurrentManagedThreadId);
            }
        };

        AppDomain.CurrentDomain.FirstChanceException += handler;
        try {
            StartPlay(service, result => launchResult = result);
            Assert.True(cookRunner.WaitUntilStarted(WaitTimeout));
            cookRunner.Release();
            DrainUntil(() => launchResult != null);
        } finally {
            AppDomain.CurrentDomain.FirstChanceException -= handler;
        }

        Assert.True(launchResult.Succeeded, launchResult.Message);
        Assert.Equal(0, CountOnThreads(exceptionThreadIds, Environment.CurrentManagedThreadId, cookRunner.CookThreadId));
    }

    /// <summary>
    /// Ensures stopping a preparation raises no first-chance exception on the calling thread or the preparation thread:
    /// cancellation is a polled flag, never an <see cref="OperationCanceledException"/>.
    /// </summary>
    [Fact]
    public void CancelledPreparation_RaisesNoFirstChanceExceptions() {
        using BlockingPlayCookRunner cookRunner = new BlockingPlayCookRunner(CreateSucceedingCookResult());
        FakePlayProcessLauncher launcher = new FakePlayProcessLauncher();
        EditorPlaySessionService service = CreateThreadedService(cookRunner, launcher);
        List<int> exceptionThreadIds = new List<int>();
        EventHandler<FirstChanceExceptionEventArgs> handler = (sender, args) => {
            lock (exceptionThreadIds) {
                exceptionThreadIds.Add(Environment.CurrentManagedThreadId);
            }
        };

        AppDomain.CurrentDomain.FirstChanceException += handler;
        try {
            StartPlay(service, result => { });
            Assert.True(cookRunner.WaitUntilStarted(WaitTimeout));
            service.Stop();
            cookRunner.Release();
            DrainUntil(() => !service.IsPreparationActive);
        } finally {
            AppDomain.CurrentDomain.FirstChanceException -= handler;
        }

        Assert.Empty(launcher.Requests);
        Assert.Equal(0, CountOnThreads(exceptionThreadIds, Environment.CurrentManagedThreadId, cookRunner.CookThreadId));
    }

    /// <summary>
    /// Creates a service that prepares on real background threads, as the editor does.
    /// </summary>
    /// <param name="cookRunner">Cook step the preparation runs.</param>
    /// <param name="launcher">Process launcher fake.</param>
    /// <returns>The service under test.</returns>
    EditorPlaySessionService CreateThreadedService(IEditorPlayCookRunner cookRunner, FakePlayProcessLauncher launcher) {
        return new EditorPlaySessionService(cookRunner, CreateSucceedingScriptBuilder(), launcher, PlayerExecutablePath, Queue, new EditorPlayPreparationThreadScheduler(), TestPlaySettings.CreateWriter());
    }

    /// <summary>
    /// Presses Play for the PS2 platform with a saved scene whose save succeeds.
    /// </summary>
    /// <param name="service">Service under test.</param>
    /// <param name="completed">Receives the launch outcome.</param>
    /// <returns>The immediate Play result.</returns>
    static EditorBuildExecutionResult StartPlay(EditorPlaySessionService service, Action<EditorBuildExecutionResult> completed) {
        return StartPlay(service, () => true, completed);
    }

    /// <summary>
    /// Presses Play for the PS2 platform with a saved scene.
    /// </summary>
    /// <param name="service">Service under test.</param>
    /// <param name="saveScene">Scene save callback.</param>
    /// <param name="completed">Receives the launch outcome.</param>
    /// <returns>The immediate Play result.</returns>
    static EditorBuildExecutionResult StartPlay(EditorPlaySessionService service, Func<bool> saveScene, Action<EditorBuildExecutionResult> completed) {
        return service.Play(
            "ps2", "PlayStation 2", ScenePath, saveScene,
            @"C:\platforms\ps2\helengine.ps2.player.dll", "helengine.ps2.player.Ps2PlayerAdapter",
            PlayerInputDevices.Gamepad, "ps2", "1.0.0", TestPlaySettings.CreateDefaults("ps2", 60d, false), completed);
    }

    /// <summary>
    /// Drains the main-thread queue on the calling thread, as editor frames do, until the condition holds.
    /// </summary>
    /// <param name="condition">Condition that ends the wait.</param>
    void DrainUntil(Func<bool> condition) {
        DateTime deadline = DateTime.UtcNow + WaitTimeout;
        while (true) {
            Queue.ProcessPending();
            if (condition()) {
                return;
            }

            Assert.True(DateTime.UtcNow < deadline, "The Play preparation did not complete in time.");
            Thread.Sleep(5);
        }
    }

    /// <summary>
    /// Counts recorded first-chance exceptions raised on either of two threads.
    /// </summary>
    /// <param name="exceptionThreadIds">Thread ids recorded for every first-chance exception in the process.</param>
    /// <param name="firstThreadId">Calling thread id.</param>
    /// <param name="secondThreadId">Preparation thread id.</param>
    /// <returns>Number of first-chance exceptions raised on those threads.</returns>
    static int CountOnThreads(List<int> exceptionThreadIds, int firstThreadId, int secondThreadId) {
        lock (exceptionThreadIds) {
            return exceptionThreadIds.Count(threadId => threadId == firstThreadId || threadId == secondThreadId);
        }
    }

    /// <summary>
    /// Creates a successful cook outcome with fixed content and startup scene values.
    /// </summary>
    /// <returns>Successful cook outcome.</returns>
    static EditorPlayCookResult CreateSucceedingCookResult() {
        return new EditorPlayCookResult(true, "Cooked.", @"C:\project\cache\play\ps2-staging", "scene-cube-test", "scenes/cube_test.hscene", new Dictionary<string, string>());
    }

    /// <summary>
    /// Creates a script builder fake that reports a successful build.
    /// </summary>
    /// <returns>Succeeding script builder fake.</returns>
    static FakePlayScriptBuilder CreateSucceedingScriptBuilder() {
        return new FakePlayScriptBuilder(EditorBuildExecutionResult.Success("Scripts built."), @"C:\project\cache\play\modules");
    }
}
