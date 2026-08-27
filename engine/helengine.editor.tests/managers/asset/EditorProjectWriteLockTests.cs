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
