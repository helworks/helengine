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

    /// <summary>
    /// Ensures ownership versions are reported only while held and change across acquisitions.
    /// </summary>
    [Fact]
    public void TryGetHeldVersion_ReflectsOwnershipAndChangesPerAcquisition() {
        Assert.False(EditorProjectWriteLock.TryGetHeldVersion(ProjectRootPath, out _));

        long firstVersion;
        using (EditorProjectWriteLock first = EditorProjectWriteLock.Acquire(ProjectRootPath)) {
            Assert.True(EditorProjectWriteLock.TryGetHeldVersion(ProjectRootPath, out firstVersion));
            using (EditorProjectWriteLock reentrant = EditorProjectWriteLock.Acquire(ProjectRootPath)) {
                Assert.True(EditorProjectWriteLock.TryGetHeldVersion(ProjectRootPath, out long reentrantVersion));
                Assert.Equal(firstVersion, reentrantVersion);
            }
            Assert.True(EditorProjectWriteLock.TryGetHeldVersion(ProjectRootPath, out _));
        }

        Assert.False(EditorProjectWriteLock.TryGetHeldVersion(ProjectRootPath, out _));

        using (EditorProjectWriteLock second = EditorProjectWriteLock.Acquire(ProjectRootPath)) {
            Assert.True(EditorProjectWriteLock.TryGetHeldVersion(ProjectRootPath, out long secondVersion));
            Assert.NotEqual(firstVersion, secondVersion);
        }
    }

    /// <summary>
    /// Ensures the pending-transaction check is memoized only for the lifetime of one owning acquisition.
    /// </summary>
    [Fact]
    public void EnsureNoPending_WhenLockIsHeld_MemoizesUntilTheLockIsReleased() {
        string markerPath = EditorAuthoringTransactionPendingMarker.GetPath(ProjectRootPath);

        using (EditorProjectWriteLock held = EditorProjectWriteLock.Acquire(ProjectRootPath)) {
            EditorAuthoringTransactionPendingMarker.EnsureNoPending(ProjectRootPath);
            Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
            File.WriteAllText(markerPath, "{}");
            // Same acquisition: the verdict is memoized, because a marker cannot legitimately appear meanwhile.
            EditorAuthoringTransactionPendingMarker.EnsureNoPending(ProjectRootPath);
        }

        // A new acquisition must look again and see the marker.
        using (EditorProjectWriteLock again = EditorProjectWriteLock.Acquire(ProjectRootPath)) {
            Assert.ThrowsAny<Exception>(() => EditorAuthoringTransactionPendingMarker.EnsureNoPending(ProjectRootPath));
        }

        File.Delete(markerPath);
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
