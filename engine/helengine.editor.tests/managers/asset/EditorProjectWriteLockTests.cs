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

    /// <summary>
    /// Ensures two threads of one process contending for the lock exclude each other and wait on the in-process gate
    /// without a single first-chance exception on either thread.
    /// </summary>
    [Fact]
    public void Acquire_WhenTwoThreadsOfOneProcessContend_ExcludesWithoutFirstChanceExceptions() {
        using (EditorProjectWriteLock warmUp = EditorProjectWriteLock.Acquire(ProjectRootPath)) {
        }

        const int Iterations = 25;
        int inside = 0;
        int maximumInside = 0;
        int[] threadIds = new int[2];
        Exception[] failures = new Exception[2];
        List<int> exceptionThreadIds = new List<int>();
        EventHandler<System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs> handler = (sender, args) => {
            lock (exceptionThreadIds) {
                exceptionThreadIds.Add(Environment.CurrentManagedThreadId);
            }
        };
        Thread[] threads = new Thread[2];
        for (int threadIndex = 0; threadIndex < threads.Length; threadIndex++) {
            int slot = threadIndex;
            threads[slot] = new Thread(() => {
                threadIds[slot] = Environment.CurrentManagedThreadId;
                try {
                    for (int iteration = 0; iteration < Iterations; iteration++) {
                        using EditorProjectWriteLock held = EditorProjectWriteLock.Acquire(ProjectRootPath);
                        int now = Interlocked.Increment(ref inside);
                        int seen;
                        do {
                            seen = Volatile.Read(ref maximumInside);
                        } while (now > seen && Interlocked.CompareExchange(ref maximumInside, now, seen) != seen);
                        Thread.Sleep(2);
                        Interlocked.Decrement(ref inside);
                    }
                } catch (Exception exception) {
                    failures[slot] = exception;
                }
            });
        }

        AppDomain.CurrentDomain.FirstChanceException += handler;
        try {
            for (int index = 0; index < threads.Length; index++) {
                threads[index].Start();
            }
            for (int index = 0; index < threads.Length; index++) {
                threads[index].Join();
            }
        } finally {
            AppDomain.CurrentDomain.FirstChanceException -= handler;
        }

        Assert.Null(failures[0]);
        Assert.Null(failures[1]);
        Assert.Equal(1, maximumInside);
        lock (exceptionThreadIds) {
            Assert.DoesNotContain(exceptionThreadIds, threadId => threadId == threadIds[0] || threadId == threadIds[1]);
        }
    }

    /// <summary>
    /// Ensures the cross-process exclusion is unchanged: while another holder (here a separate handle opened like another
    /// process would) keeps the lock file, acquiring times out with an I/O error, and the in-process gate is given back,
    /// so the lock is available again once the file is free.
    /// </summary>
    [Fact]
    public void Acquire_WhenAnotherHolderKeepsTheLockFile_TimesOutAndReleasesTheGate() {
        using (EditorProjectWriteLock warmUp = EditorProjectWriteLock.Acquire(ProjectRootPath)) {
        }

        string lockPath = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-write.lock");
        using (FileStream otherHolder = new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
            Assert.ThrowsAny<IOException>(() => EditorProjectWriteLock.Acquire(ProjectRootPath, TimeSpan.FromMilliseconds(200)));
        }

        Task<bool> otherThread = Task.Run(() => {
            using EditorProjectWriteLock acquired = EditorProjectWriteLock.Acquire(ProjectRootPath, TimeSpan.FromSeconds(5));
            return acquired != null;
        });
        Assert.True(otherThread.Wait(TimeSpan.FromSeconds(10)));
        Assert.True(otherThread.Result);
    }

    /// <summary>
    /// Ensures a free lock file is always tried at least once, even when the deadline has already passed by the time the
    /// in-process gate was taken.
    /// </summary>
    [Fact]
    public void Acquire_WhenTheDeadlineIsUsedUpBeforeTheFileAttempt_StillTriesTheFreeFileOnce() {
        using EditorProjectWriteLock acquired = EditorProjectWriteLock.Acquire(ProjectRootPath, TimeSpan.FromTicks(1));

        Assert.NotNull(acquired);
    }

    /// <summary>
    /// Ensures a release that fails after the lock file was closed still gives the in-process gate back, so another
    /// thread of the editor can take the free lock at once instead of waiting out its deadline.
    /// </summary>
    [Fact]
    public void Dispose_WhenTheScopeReleaseFailsAfterTheFileClosed_StillReleasesTheGate() {
        int testThreadId = Environment.CurrentManagedThreadId;
        bool armed = true;
        Action<string> previousHook = EditorAuthoringMutationScope.MutationHookForTests;
        EditorAuthoringMutationScope.MutationHookForTests = point => {
            previousHook?.Invoke(point);
            if (armed && point == EditorProjectWriteLock.ScopeReleaseHookPoint && Environment.CurrentManagedThreadId == testThreadId) {
                throw new IOException("Simulated scope release failure.");
            }
        };
        try {
            EditorProjectWriteLock held = EditorProjectWriteLock.Acquire(ProjectRootPath);
            Assert.Throws<AggregateException>(() => held.Dispose());

            Task<bool> otherThread = Task.Run(() => {
                using EditorProjectWriteLock acquired = EditorProjectWriteLock.Acquire(ProjectRootPath, TimeSpan.FromSeconds(5));
                return acquired != null;
            });
            Assert.True(otherThread.Wait(TimeSpan.FromSeconds(10)));
            Assert.True(otherThread.Result);

            armed = false;
            held.Dispose();
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = previousHook;
        }
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
