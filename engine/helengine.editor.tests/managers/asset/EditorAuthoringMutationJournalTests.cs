using Xunit;
using System.Text.Json;
using System.Reflection;

namespace helengine.editor.tests.managers.asset;

/// <summary>
/// Verifies that inode-bound namespace work has a project-scoped durable boundary.
/// </summary>
public sealed class EditorAuthoringMutationJournalTests : IDisposable {
    readonly string ProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-mutation-journal-" + Guid.NewGuid().ToString("N"));

    public EditorAuthoringMutationJournalTests() {
        Directory.CreateDirectory(Path.Combine(ProjectRootPath, "assets"));
    }

    public void Dispose() {
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
    }

    [Fact]
    public void MutationScope_ExposesDedicatedFixedNamePrimitives() {
        BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        Assert.NotNull(typeof(EditorAuthoringMutationScope).GetMethod("FixedRenameNoReplace", flags));
        Assert.NotNull(typeof(EditorAuthoringMutationScope).GetMethod("FixedRenameExchange", flags));
        Assert.NotNull(typeof(EditorAuthoringMutationScope).GetMethod("FixedDeleteVerifiedLeaf", flags));
        Assert.NotNull(typeof(EditorAuthoringMutationScope).GetMethod("FixedDeleteVerifiedDirectoryTree", flags));
        Assert.NotNull(typeof(EditorAuthoringMutationScope).GetMethod("FixedWrite", flags));
    }

    [Fact]
    public void MutationJournalSource_UsesFixedPrimitivesForItsOwnLifecycle() {
        string sourcePath = FindSourceFile("EditorAuthoringMutationJournal.cs");
        string source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("WithoutJournal(", source, StringComparison.Ordinal);
        Assert.Contains("Fixed", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FixedNamePrimitives_UseNoReplaceExchangeAndVerifiedDelete() {
        string source = Path.Combine(ProjectRootPath, "assets", "fixed-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "fixed-destination.hasset");
        string replacement = Path.Combine(ProjectRootPath, "assets", "fixed-replacement.hasset");

        EditorAuthoringMutationScope.FixedCreateExclusive(ProjectRootPath, source, new byte[] { 1, 2, 3 });
        EditorAuthoringMutationScope.FixedRenameNoReplace(ProjectRootPath, source, destination);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(destination));

        EditorAuthoringMutationScope.FixedCreateExclusive(ProjectRootPath, replacement, new byte[] { 4, 5, 6 });
        EditorAuthoringMutationScope.FixedRenameExchange(ProjectRootPath, replacement, destination);
        Assert.Equal(new byte[] { 4, 5, 6 }, File.ReadAllBytes(destination));
        EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(ProjectRootPath, replacement);
        Assert.False(File.Exists(replacement));
    }

    [Fact]
    public void Recover_WhenPayloadWriteIsInterrupted_DiscardsRecognizedPartialPayload() {
        string source = Path.Combine(ProjectRootPath, "assets", "source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "destination.hasset");
        string operationDirectory;
        using (EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "copy", source, destination)) {
            string stagedNextPath = journal.CreateStagedPayloadNextPath();
            operationDirectory = journal.OperationDirectoryPath;
            EditorAuthoringMutationScope.FixedCreateExclusive(ProjectRootPath, stagedNextPath, new byte[] { 8, 9 });
        }

        EditorAuthoringMutationJournal.Recover(ProjectRootPath);

        Assert.False(Directory.Exists(operationDirectory));
        Assert.False(File.Exists(destination));
    }

    [Fact]
    public void FixedRenameNoReplace_WhenDestinationExists_PreservesBothEntries() {
        string source = Path.Combine(ProjectRootPath, "assets", "existing-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "existing-destination.hasset");
        File.WriteAllBytes(source, new byte[] { 1 });
        File.WriteAllBytes(destination, new byte[] { 2 });

        Assert.Throws<IOException>(() => EditorAuthoringMutationScope.FixedRenameNoReplace(ProjectRootPath, source, destination));
        Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(source));
        Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(destination));
    }

    static string FindSourceFile(string fileName) {
        DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory != null) {
            string candidate = Path.Combine(directory.FullName, "helengine.editor", "managers", "asset", fileName);
            if (File.Exists(candidate)) {
                return candidate;
            }
            candidate = Path.Combine(directory.FullName, "engine", "helengine.editor", "managers", "asset", fileName);
            if (File.Exists(candidate)) {
                return candidate;
            }
            directory = directory.Parent;
        }
        throw new FileNotFoundException(fileName);
    }

    [Fact]
    public void BeginAndComplete_PublishesAndRetiresOneDurableOperationDocument() {
        string source = Path.Combine(ProjectRootPath, "assets", "source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "destination.hasset");
        using (EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "replace", source, destination)) {
            string operationDirectory = Assert.Single(Directory.GetDirectories(Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations")));
            string documentPath = Path.Combine(operationDirectory, "document.json");
            Assert.True(File.Exists(documentPath));
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(documentPath));
            Assert.Equal("replace", document.RootElement.GetProperty("Kind").GetString());
            Assert.Contains("missing", document.RootElement.GetProperty("ExpectedSourceIdentity").GetString(), StringComparison.OrdinalIgnoreCase);
            journal.Complete();
        }

        Assert.Empty(Directory.GetDirectories(Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations")));
    }

    [Fact]
    public void Begin_UsesContainedOperationDirectoryWithFixedDocumentArtifact() {
        string source = Path.Combine(ProjectRootPath, "assets", "source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "destination.hasset");
        using EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "copy", source, destination);

        string root = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations");
        string[] operationDirectories = Directory.GetDirectories(root);
        Assert.Single(operationDirectories);
        Assert.True(File.Exists(Path.Combine(operationDirectories[0], "document.json")));
        Assert.Empty(Directory.GetFiles(root, "*.json"));
        Assert.Equal(
            new[] { "document.json" },
            Directory.GetFileSystemEntries(operationDirectories[0])
                .Select(Path.GetFileName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());
    }

    [Fact]
    public void CopyLeaf_PublishesTheStagedBytesToTheRequestedDestination() {
        string source = Path.Combine(ProjectRootPath, "assets", "source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "destination.hasset");
        File.WriteAllBytes(source, new byte[] { 1, 2, 3, 4 });

        EditorAuthoringMutationScope.CopyLeaf(ProjectRootPath, source, destination);

        Assert.True(File.Exists(destination));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(destination));
    }

    [Fact]
    public void DeleteLeaf_UsesOperationOwnedDeletingEntryBeforeRetiringJournal() {
        string source = Path.Combine(ProjectRootPath, "assets", "source.hasset");
        File.WriteAllBytes(source, new byte[] { 9, 8, 7 });

        EditorAuthoringMutationScope.DeleteLeaf(ProjectRootPath, source);

        Assert.False(File.Exists(source));
        Assert.Empty(Directory.GetFiles(Path.Combine(ProjectRootPath, "assets"), ".deleting-*"));
        Assert.Empty(Directory.GetDirectories(Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations")));
    }

    [Fact]
    public void DeleteDirectoryTree_UsesOneTopLevelDeletingEntry() {
        string source = Path.Combine(ProjectRootPath, "cache", "temporary-tree");
        Directory.CreateDirectory(source);
        File.WriteAllBytes(Path.Combine(source, "payload.bin"), new byte[] { 1, 3, 5 });

        EditorAuthoringMutationScope.DeleteDirectoryTree(ProjectRootPath, source, ProjectRootPath);

        Assert.False(Directory.Exists(source));
        Assert.Empty(Directory.GetDirectories(Path.Combine(ProjectRootPath, "cache"), ".deleting-*"));
        Assert.Empty(Directory.GetDirectories(Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations")));
    }

    [Fact]
    public void Recover_WhenCopyPayloadWasStagedButNotPublished_PublishesVerifiedPayload() {
        string source = Path.Combine(ProjectRootPath, "assets", "source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "destination.hasset");
        File.WriteAllBytes(source, new byte[] { 2, 4, 6 });
        string stagedPath;
        using (EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "copy", source, destination)) {
            stagedPath = journal.CreateStagedPayloadPath(Path.GetFileName(destination));
            File.WriteAllBytes(stagedPath, new byte[] { 2, 4, 6 });
            journal.RecordStagedPayload(stagedPath, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(new byte[] { 2, 4, 6 })).ToLowerInvariant());
        }

        EditorAuthoringMutationJournal.Recover(ProjectRootPath);

        Assert.Equal(new byte[] { 2, 4, 6 }, File.ReadAllBytes(destination));
        Assert.Empty(Directory.GetDirectories(Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations")));
    }

    [Fact]
    public void Recover_WhenOperationIsUnresolved_FailsClosedWithoutTouchingAssets() {
        string source = Path.Combine(ProjectRootPath, "assets", "source.hasset");
        using (EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "replace", source, source)) {
            journal.MarkPhase("Published");
            Assert.Throws<InvalidOperationException>(() => EditorAuthoringMutationJournal.Recover(ProjectRootPath));
        }

        Assert.False(File.Exists(source));
    }

    [Fact]
    public void Recover_WhenJournalVersionIsMalformed_FailsClosed() {
        string journalDirectory = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations");
        string operationDirectory = Path.Combine(journalDirectory, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(operationDirectory);
        File.WriteAllText(Path.Combine(operationDirectory, "document.json"), "{\"Version\":99}");

        Assert.Throws<InvalidDataException>(() => EditorAuthoringMutationJournal.Recover(ProjectRootPath));
        Assert.True(File.Exists(Path.Combine(operationDirectory, "document.json")));
    }

    [Fact]
    public void Recover_WhenCreatingOperationDirectoryIsVisible_RemovesOnlyItsContainedTree() {
        string journalDirectory = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations");
        string creatingDirectory = Path.Combine(journalDirectory, ".creating-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(creatingDirectory, "staged"));
        File.WriteAllBytes(Path.Combine(creatingDirectory, "staged", "payload.bin"), new byte[] { 1, 2, 3 });

        EditorAuthoringMutationJournal.Recover(ProjectRootPath);

        Assert.False(Directory.Exists(creatingDirectory));
        Assert.Empty(Directory.GetFiles(ProjectRootPath, "outside-*", SearchOption.AllDirectories));
    }

    [Fact]
    public void Recover_WhenDeletingOperationDirectoryIsVisible_ResumesContainedRetirement() {
        string journalDirectory = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations");
        string deletingDirectory = Path.Combine(journalDirectory, ".deleting-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(deletingDirectory, "staged"));
        File.WriteAllBytes(Path.Combine(deletingDirectory, "staged", "payload.bin"), new byte[] { 4, 5, 6 });

        EditorAuthoringMutationJournal.Recover(ProjectRootPath);

        Assert.False(Directory.Exists(deletingDirectory));
    }

    [Fact]
    public void Recover_WhenNextDocumentHasHigherSequence_PromotesItBeforeRecovery() {
        string journalDirectory = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations");
        string operationId = Guid.NewGuid().ToString("N");
        string operationDirectory = Path.Combine(journalDirectory, operationId);
        Directory.CreateDirectory(operationDirectory);
        string document = "{\"Version\":1,\"OperationId\":\"" + operationId + "\",\"Kind\":\"replace\",\"SourceRelativePath\":\"assets/source.hasset\",\"DestinationRelativePath\":\"assets/destination.hasset\",\"ExpectedSourceIdentity\":\"missing\",\"ExpectedDestinationIdentity\":\"missing\",\"Phase\":\"Completed\",\"Sequence\":1,\"TransientEntries\":[]}";
        string nextDocument = document.Replace("\"Sequence\":1", "\"Sequence\":2", StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(operationDirectory, "document.json"), document);
        File.WriteAllText(Path.Combine(operationDirectory, "document.next"), nextDocument);

        EditorAuthoringMutationJournal.Recover(ProjectRootPath);

        Assert.False(Directory.Exists(operationDirectory));
    }

    [Fact]
    public void Begin_WhenDestinationEscapesProject_RejectsBeforeJournalCreation() {
        string outsidePath = Path.Combine(Path.GetTempPath(), "outside-authoring-journal.hasset");

        Assert.Throws<InvalidDataException>(() => EditorAuthoringMutationJournal.Begin(
            ProjectRootPath,
            "replace",
            Path.Combine(ProjectRootPath, "assets", "source.hasset"),
            outsidePath));
        Assert.False(Directory.Exists(Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations")));
    }

    [Fact]
    public void Begin_WhenMutationKindIsNotWhitelisted_RejectsBeforeJournalCreation() {
        Assert.Throws<ArgumentException>(() => EditorAuthoringMutationJournal.Begin(
            ProjectRootPath,
            "unrecognized-operation",
            Path.Combine(ProjectRootPath, "assets", "source.hasset"),
            Path.Combine(ProjectRootPath, "assets", "destination.hasset")));

        Assert.False(Directory.Exists(Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations")));
    }

    [Fact]
    public void Recover_WhenDocumentOperationIdDoesNotMatchDirectory_RejectsWithoutRetiringIt() {
        string journalDirectory = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations");
        string operationId = Guid.NewGuid().ToString("N");
        string otherOperationId = Guid.NewGuid().ToString("N");
        string operationDirectory = Path.Combine(journalDirectory, operationId);
        Directory.CreateDirectory(operationDirectory);
        File.WriteAllText(
            Path.Combine(operationDirectory, "document.json"),
            $"{{\"Version\":1,\"Sequence\":1,\"OperationId\":\"{otherOperationId}\",\"Kind\":\"replace\",\"SourceRelativePath\":\"assets/source.hasset\",\"DestinationRelativePath\":\"assets/destination.hasset\",\"ExpectedSourceIdentity\":\"missing\",\"ExpectedDestinationIdentity\":\"missing\",\"Phase\":\"Completed\",\"TransientEntries\":[]}}");

        Assert.Throws<InvalidDataException>(() => EditorAuthoringMutationJournal.Recover(ProjectRootPath));
        Assert.True(Directory.Exists(operationDirectory));
    }

    [Fact]
    public void Recover_WhenCompletedDocumentRetirementWasInterrupted_RemovesOnlyTheValidatedDocument() {
        string journalDirectory = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations");
        string operationId = Guid.NewGuid().ToString("N");
        string operationDirectory = Path.Combine(journalDirectory, operationId);
        Directory.CreateDirectory(operationDirectory);
        string journalPath = Path.Combine(operationDirectory, "document.json");
        File.WriteAllText(journalPath, $"{{\"Version\":1,\"Sequence\":1,\"OperationId\":\"{operationId}\",\"Kind\":\"replace\",\"SourceRelativePath\":\"assets/source.hasset\",\"DestinationRelativePath\":\"assets/destination.hasset\",\"ExpectedSourceIdentity\":\"missing\",\"ExpectedDestinationIdentity\":\"missing\",\"Phase\":\"Completed\",\"TransientEntries\":[]}}");

        EditorAuthoringMutationJournal.Recover(ProjectRootPath);

        Assert.False(File.Exists(journalPath));
        Assert.Empty(Directory.GetFiles(ProjectRootPath, "outside-*", SearchOption.AllDirectories));
    }
}
