using Xunit;

namespace helengine.editor.tests.managers.asset;

/// <summary>
/// Verifies that a valid concurrent authoring operation can wait beyond the short retry window.
/// </summary>
public sealed class EditorProjectWriteLockTests : IDisposable {
    readonly string ProjectRootPath;

    public EditorProjectWriteLockTests() {
        ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-write-lock-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
    }

    public void Dispose() {
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
    }

    /// <summary>
    /// Ensures acquiring the lock on an ordinary directory tree raises no first-chance exceptions while canonicalizing ancestors up to the drive root.
    /// </summary>
    [Fact]
    public void Acquire_WhenProjectHasNoLinkedAncestors_DoesNotRaiseDirectoryExceptions() {
        int directoryNotFoundCount = 0;
        string firstStackTrace = string.Empty;
        EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> handler = (sender, args) => {
            if (args.Exception is DirectoryNotFoundException) {
                directoryNotFoundCount++;
                if (string.IsNullOrEmpty(firstStackTrace)) {
                    firstStackTrace = args.Exception.Message + Environment.NewLine + Environment.StackTrace;
                }
            }
        };

        AppDomain.CurrentDomain.FirstChanceException += handler;
        try {
            using EditorProjectWriteLock acquired = EditorProjectWriteLock.Acquire(ProjectRootPath);
            Assert.NotNull(acquired);
        } finally {
            AppDomain.CurrentDomain.FirstChanceException -= handler;
        }

        Assert.True(directoryNotFoundCount == 0, $"Saw {directoryNotFoundCount} DirectoryNotFoundException(s). First:{Environment.NewLine}{firstStackTrace}");
    }

    [Fact]
    public async Task Acquire_WhenLockIsHeldForSeveralSeconds_WaitsAndSucceeds() {
        using EditorProjectWriteLock heldLock = EditorProjectWriteLock.Acquire(ProjectRootPath);
        Task<EditorProjectWriteLock> waitingAcquire = Task.Run(() => EditorProjectWriteLock.Acquire(ProjectRootPath));

        await Task.Delay(TimeSpan.FromMilliseconds(2300));
        heldLock.Dispose();

        using EditorProjectWriteLock acquired = await waitingAcquire.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.NotNull(acquired);
    }
}
