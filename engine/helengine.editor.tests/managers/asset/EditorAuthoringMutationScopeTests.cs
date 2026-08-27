using Xunit;

namespace helengine.editor.tests.managers.asset;

/// <summary>
/// Verifies the Windows handle-pinned mutation boundary used by authoring.
/// </summary>
public sealed class EditorAuthoringMutationScopeTests : IDisposable {
    readonly string ProjectRootPath;

    public EditorAuthoringMutationScopeTests() {
        ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-mutation-scope-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
    }

    public void Dispose() {
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
    }

    [Fact]
    public void AcquireForMutation_WhenNonWindows_FailsClosed() {
        if (OperatingSystem.IsWindows()) {
            return;
        }

        Assert.Throws<PlatformNotSupportedException>(() =>
            EditorAuthoringMutationScope.AcquireForMutation(ProjectRootPath, Path.Combine(ProjectRootPath, "assets")));
    }

    [Fact(Skip = "Requires Windows directory-link privilege to exercise reparse rejection.")]
    public void AcquireForMutation_WhenProjectRootIsLinked_RejectsBeforeMutation() {
        string outsideRoot = Path.Combine(Path.GetTempPath(), "helengine-mutation-outside-" + Guid.NewGuid().ToString("N"));
        string linkedRoot = Path.Combine(Path.GetTempPath(), "helengine-mutation-linked-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outsideRoot);
        try {
            Directory.CreateSymbolicLink(linkedRoot, outsideRoot);

            Assert.ThrowsAny<Exception>(() => EditorAuthoringMutationScope.AcquireForMutation(
                linkedRoot,
                Path.Combine(linkedRoot, "assets")));
            Assert.Empty(Directory.EnumerateFileSystemEntries(outsideRoot));
        } finally {
            if (Directory.Exists(linkedRoot)) {
                Directory.Delete(linkedRoot);
            }
            if (Directory.Exists(outsideRoot)) {
                Directory.Delete(outsideRoot, true);
            }
        }
    }

    [Fact]
    public void AcquireForMutation_HoldsParentIdentityAcrossScope() {
        if (!OperatingSystem.IsWindows()) {
            return;
        }

        using EditorAuthoringMutationScope scope = EditorAuthoringMutationScope.AcquireForMutation(
            ProjectRootPath,
            Path.Combine(ProjectRootPath, "assets"));

        Assert.ThrowsAny<Exception>(() => Directory.Move(
            Path.Combine(ProjectRootPath, "assets"),
            Path.Combine(ProjectRootPath, "assets-swapped")));
    }
}
