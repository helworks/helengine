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
    public void AcquireForMutation_PinsProjectAndAssetsChainOnSupportedOperatingSystems() {
        using EditorAuthoringMutationScope scope = EditorAuthoringMutationScope.AcquireForMutation(
            ProjectRootPath,
            Path.Combine(ProjectRootPath, "assets"));
    }

    [DirectoryLinkFact]
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
        using EditorAuthoringMutationScope scope = EditorAuthoringMutationScope.AcquireForMutation(
            ProjectRootPath,
            Path.Combine(ProjectRootPath, "assets"));

        if (OperatingSystem.IsWindows()) {
            Assert.ThrowsAny<Exception>(() => Directory.Move(
                Path.Combine(ProjectRootPath, "assets"),
                Path.Combine(ProjectRootPath, "assets-swapped")));
        } else {
            Directory.Move(
                Path.Combine(ProjectRootPath, "assets"),
                Path.Combine(ProjectRootPath, "assets-swapped"));
            Directory.Move(
                Path.Combine(ProjectRootPath, "assets-swapped"),
                Path.Combine(ProjectRootPath, "assets"));
        }
    }

    [Fact]
    public void OpenVerifiedFileForMutation_CreatesAndWritesThroughPinnedLeafHandle() {
        string filePath = Path.Combine(ProjectRootPath, "assets", "leaf.bin");
        using (EditorAuthoringMutationScope scope = EditorAuthoringMutationScope.AcquireForMutation(
            ProjectRootPath,
            Path.GetDirectoryName(filePath)))
        using (EditorAuthoringVerifiedFile file = scope.OpenVerifiedFile(
            filePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None)) {
            file.Stream.Write(new byte[] { 7, 8, 9 });
            file.Stream.Flush(true);
        }

        Assert.Equal(new byte[] { 7, 8, 9 }, File.ReadAllBytes(filePath));
    }

    [DirectoryLinkFact]
    public void OpenVerifiedFileForRead_RejectsExistingReparseLeaf() {
        string filePath = Path.Combine(ProjectRootPath, "assets", "linked.bin");
        string outsidePath = Path.Combine(Path.GetTempPath(), "helengine-mutation-leaf-outside-" + Guid.NewGuid().ToString("N"));
        File.WriteAllBytes(outsidePath, new byte[] { 1 });
        try {
            Directory.CreateSymbolicLink(filePath, outsidePath);
            using EditorAuthoringMutationScope scope = EditorAuthoringMutationScope.AcquireForMutation(
                ProjectRootPath,
                Path.Combine(ProjectRootPath, "assets"));
            Assert.Throws<InvalidDataException>(() => scope.OpenVerifiedFile(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read));
            Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(outsidePath));
        } finally {
            if (File.Exists(filePath)) {
                File.Delete(filePath);
            }
            if (File.Exists(outsidePath)) {
                File.Delete(outsidePath);
            }
        }
    }

    [DirectoryLinkFact]
    public void ReplaceLeaf_RejectsReparseDestinationWithoutTouchingLinkTarget() {
        string sourcePath = Path.Combine(ProjectRootPath, "assets", "source.tmp");
        string destinationPath = Path.Combine(ProjectRootPath, "assets", "destination.bin");
        string outsidePath = Path.Combine(Path.GetTempPath(), "helengine-mutation-destination-outside-" + Guid.NewGuid().ToString("N"));
        File.WriteAllBytes(sourcePath, new byte[] { 2, 3, 5 });
        File.WriteAllBytes(outsidePath, new byte[] { 7, 11 });
        try {
            File.CreateSymbolicLink(destinationPath, outsidePath);
            using EditorAuthoringMutationScope scope = EditorAuthoringMutationScope.AcquireForMutation(
                ProjectRootPath,
                Path.Combine(ProjectRootPath, "assets"));

            Assert.Throws<InvalidDataException>(() => scope.ReplaceLeaf(sourcePath, destinationPath, true));
            Assert.Equal(new byte[] { 7, 11 }, File.ReadAllBytes(outsidePath));
            Assert.True(File.Exists(sourcePath));
        } finally {
            if (File.Exists(destinationPath)) {
                File.Delete(destinationPath);
            }
            if (File.Exists(sourcePath)) {
                File.Delete(sourcePath);
            }
            if (File.Exists(outsidePath)) {
                File.Delete(outsidePath);
            }
        }
    }

    [DirectoryLinkFact]
    public void DeleteLeaf_RejectsReparseLeafWithoutTouchingLinkTarget() {
        string linkedPath = Path.Combine(ProjectRootPath, "assets", "delete.bin");
        string outsidePath = Path.Combine(Path.GetTempPath(), "helengine-mutation-delete-outside-" + Guid.NewGuid().ToString("N"));
        File.WriteAllBytes(outsidePath, new byte[] { 13, 17 });
        try {
            File.CreateSymbolicLink(linkedPath, outsidePath);
            using EditorAuthoringMutationScope scope = EditorAuthoringMutationScope.AcquireForMutation(
                ProjectRootPath,
                Path.Combine(ProjectRootPath, "assets"));

            Assert.Throws<InvalidDataException>(() => scope.DeleteLeaf(linkedPath));
            Assert.Equal(new byte[] { 13, 17 }, File.ReadAllBytes(outsidePath));
        } finally {
            if (File.Exists(linkedPath)) {
                File.Delete(linkedPath);
            }
            if (File.Exists(outsidePath)) {
                File.Delete(outsidePath);
            }
        }
    }
}
