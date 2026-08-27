using Xunit;

namespace helengine.editor.tests.managers.asset;

/// <summary>
/// Verifies ordered, path-specific project publication records.
/// </summary>
public sealed class EditorProjectWriteGenerationTests : IDisposable {
    readonly string ProjectRootPath;

    public EditorProjectWriteGenerationTests() {
        ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-write-generation-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
    }

    public void Dispose() {
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
    }

    [Fact]
    public void PublishChange_PreservesOrderedNormalizedPaths() {
        long first = EditorProjectWriteGeneration.PublishChange(ProjectRootPath, "Models\\First.hasset");
        long second = EditorProjectWriteGeneration.PublishChange(ProjectRootPath, "Models/Second.hasset");

        IReadOnlyList<EditorProjectWriteChange> changes = EditorProjectWriteGeneration.ReadAfter(ProjectRootPath, 0);

        Assert.Equal(first + 1, second);
        Assert.Collection(
            changes,
            change => {
                Assert.Equal(first, change.Generation);
                Assert.Equal("Models/First.hasset", change.RelativePath);
            },
            change => {
                Assert.Equal(second, change.Generation);
                Assert.Equal("Models/Second.hasset", change.RelativePath);
            });
    }

    [Fact]
    public void PublishChange_WhenPathIsRepeated_KeepsOneLatestPathRecord() {
        long first = EditorProjectWriteGeneration.PublishChange(ProjectRootPath, "Models/Repeated.hasset");
        long second = EditorProjectWriteGeneration.PublishChange(ProjectRootPath, "Models\\Repeated.hasset");
        string markerPath = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-write.generation");

        IReadOnlyList<EditorProjectWriteChange> changes = EditorProjectWriteGeneration.ReadAfter(ProjectRootPath, 0);

        Assert.Equal(first + 1, second);
        Assert.Single(changes);
        Assert.Equal(second, changes[0].Generation);
        Assert.Equal("Models/Repeated.hasset", changes[0].RelativePath);
        Assert.True(new FileInfo(markerPath).Length < 2048);
    }

    [Fact]
    public void Read_WhenSnapshotIsMalformed_RejectsItExplicitly() {
        string markerPath = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-write.generation");
        Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
        File.WriteAllText(markerPath, "{not-json");

        Assert.Throws<InvalidDataException>(() => EditorProjectWriteGeneration.Read(ProjectRootPath));
    }

    [Fact]
    public void Read_WhenSnapshotHasTornRecord_RejectsItExplicitly() {
        string markerPath = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-write.generation");
        Directory.CreateDirectory(Path.GetDirectoryName(markerPath));
        File.WriteAllText(markerPath, "{\"version\":1,\"currentGeneration\":2,\"changes\":[{\"generation\":2}]}");

        Assert.Throws<InvalidDataException>(() => EditorProjectWriteGeneration.ReadAfter(ProjectRootPath, 0));
    }

    [Fact]
    public async Task PublishChange_WhenConcurrentOwnersPublish_ProducesOneOrderedSnapshot() {
        Task<long> first = Task.Run(() => EditorProjectWriteGeneration.PublishChange(ProjectRootPath, "Models/ConcurrentA.hasset"));
        Task<long> second = Task.Run(() => EditorProjectWriteGeneration.PublishChange(ProjectRootPath, "Models/ConcurrentB.hasset"));

        long[] generations = await Task.WhenAll(first, second);
        IReadOnlyList<EditorProjectWriteChange> changes = EditorProjectWriteGeneration.ReadAfter(ProjectRootPath, 0);

        Assert.Equal(new[] { 1L, 2L }, generations.OrderBy(value => value));
        Assert.Equal(2, changes.Count);
        Assert.Equal(new[] { 1L, 2L }, changes.Select(change => change.Generation));
    }

    [Fact]
    public void ProjectWriteLock_WhenOneBoundaryReentersSameProject_ReusesTheHeldHandle() {
        using EditorProjectWriteLock outer = EditorProjectWriteLock.Acquire(ProjectRootPath, TimeSpan.FromSeconds(1));
        using EditorProjectWriteLock inner = EditorProjectWriteLock.Acquire(ProjectRootPath, TimeSpan.FromMilliseconds(50));

        Assert.NotNull(inner);
    }

    [Fact]
    public void ProjectWriteLock_WhenEquivalentRootSpellingsAreUsed_ReusesTheHeldHandle() {
        string equivalentRoot = Path.Combine(ProjectRootPath, "assets", "..", ".");
        using EditorProjectWriteLock outer = EditorProjectWriteLock.Acquire(ProjectRootPath, TimeSpan.FromSeconds(1));
        using EditorProjectWriteLock inner = EditorProjectWriteLock.Acquire(equivalentRoot, TimeSpan.FromMilliseconds(50));

        Assert.NotNull(inner);
    }

    [Fact]
    public void ProjectWriteLock_WhenRootIsReachedThroughDirectoryLink_ReusesTheHeldHandle() {
        string linkRoot = Path.Combine(Path.GetTempPath(), "helengine-write-generation-tests", Guid.NewGuid().ToString("N"));
        try {
            try {
                Directory.CreateSymbolicLink(linkRoot, ProjectRootPath);
            } catch (Exception exception) when (exception is UnauthorizedAccessException || exception is IOException || exception is PlatformNotSupportedException) {
                return;
            }

            using EditorProjectWriteLock outer = EditorProjectWriteLock.Acquire(ProjectRootPath, TimeSpan.FromSeconds(1));
            using EditorProjectWriteLock inner = EditorProjectWriteLock.Acquire(linkRoot, TimeSpan.FromMilliseconds(50));

            Assert.NotNull(inner);
        } finally {
            if (Directory.Exists(linkRoot)) {
                Directory.Delete(linkRoot);
            }
        }
    }

    [Fact]
    public void ProjectWriteLock_WhenProjectsDiffer_DoesNotShareTheHeldHandle() {
        string secondProjectRoot = Path.Combine(Path.GetTempPath(), "helengine-write-generation-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(secondProjectRoot, "assets"));
        try {
            using EditorProjectWriteLock first = EditorProjectWriteLock.Acquire(ProjectRootPath, TimeSpan.FromSeconds(1));
            using EditorProjectWriteLock second = EditorProjectWriteLock.Acquire(secondProjectRoot, TimeSpan.FromMilliseconds(50));

            Assert.NotSame(GetLockStream(first), GetLockStream(second));
        } finally {
            if (Directory.Exists(secondProjectRoot)) {
                Directory.Delete(secondProjectRoot, true);
            }
        }
    }

    static object GetLockStream(EditorProjectWriteLock projectWriteLock) {
        return typeof(EditorProjectWriteLock)
            .GetField("LockStream", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .GetValue(projectWriteLock);
    }
}
