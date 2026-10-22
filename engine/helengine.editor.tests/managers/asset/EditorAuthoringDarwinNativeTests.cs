using helengine.editor.tests.testing;

namespace helengine.editor.tests.managers.asset;

/// <summary>Exercises actual Darwin creation permissions, locking and journaled descriptor-relative mutations.</summary>
public sealed class EditorAuthoringDarwinNativeTests : IDisposable {
    /// <summary>Fixed native fixture owned exclusively by this serial test class, using the resolved macOS temporary volume path.</summary>
    readonly string ProjectRootPath = "/private/tmp/helengine/macos/authoring-native-fixture";

    /// <summary>Creates an isolated real directory chain without exporting the source checkout.</summary>
    public EditorAuthoringDarwinNativeTests() {
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
    }

    /// <summary>Removes only this test class's fixed temporary fixture after the native handles have been disposed.</summary>
    public void Dispose() {
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
    }

    /// <summary>Darwin variadic operands must reach libc intact so created files can be reopened and independently locked.</summary>
    [MacOsFact]
    public void CreateAndReopenPreservePermissionsAndLocks() {
        string filePath = Path.Combine(ProjectRootPath, "assets", "ação.bin");
        using EditorAuthoringMutationScope scope = EditorAuthoringMutationScope.AcquireForMutation(ProjectRootPath, Path.GetDirectoryName(filePath));
        using (EditorAuthoringVerifiedFile created = scope.OpenVerifiedFile(filePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.ReadWrite)) {
            created.Stream.WriteByte(17);
            created.Stream.Flush(true);
        }
        UnixFileMode required = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        Assert.Equal(required, File.GetUnixFileMode(filePath) & required);
        using EditorAuthoringVerifiedFile first = scope.OpenVerifiedFile(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        using EditorAuthoringVerifiedFile second = scope.OpenVerifiedFile(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        Assert.Equal(17, first.Stream.ReadByte());
        Assert.True(scope.TryAcquireExclusiveFileLock(first));
        Assert.False(scope.TryAcquireExclusiveFileLock(second));
        first.Dispose();
        Assert.True(scope.TryAcquireExclusiveFileLock(second));
    }

    /// <summary>Journal completion must enumerate dot entries safely, retain atomic replacement and reject symbolic-link leaves.</summary>
    [MacOsFact]
    public void JournaledReplacementAndRecursiveCleanupRetainNoFollow() {
        string filePath = Path.Combine(ProjectRootPath, "assets", "value.bin");
        EditorAuthoringMutationScope.WriteAllBytesAtomically(ProjectRootPath, filePath, [1, 2]);
        EditorAuthoringMutationScope.WriteAllBytesAtomically(ProjectRootPath, filePath, [3, 4]);
        Assert.Equal(new byte[] { 3, 4 }, EditorAuthoringMutationScope.ReadAllBytes(ProjectRootPath, filePath));
        string tree = Path.Combine(ProjectRootPath, "assets", "tree");
        Directory.CreateDirectory(Path.Combine(tree, "nested"));
        File.WriteAllText(Path.Combine(tree, "nested", "leaf.txt"), "data");
        EditorAuthoringMutationScope.FixedDeleteVerifiedDirectoryTree(ProjectRootPath, tree, ProjectRootPath);
        Assert.False(Directory.Exists(tree));
        string link = Path.Combine(ProjectRootPath, "assets", "link.bin");
        File.CreateSymbolicLink(link, filePath);
        Assert.Throws<InvalidDataException>(() => EditorAuthoringMutationScope.ReadAllBytes(ProjectRootPath, link));
        Assert.Equal(new byte[] { 3, 4 }, File.ReadAllBytes(filePath));
    }
}
