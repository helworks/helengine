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
    public void PublishChange_AppendsOrderedNormalizedPaths() {
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
}
