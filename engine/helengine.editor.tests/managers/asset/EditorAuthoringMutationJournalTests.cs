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

        Assert.Contains(
            typeof(EditorAuthoringMutationScope).GetMethod("FixedRenameNoReplace", flags)!.GetParameters(),
            parameter => parameter.Name == "expectedSourceIdentity");
        Assert.Contains(
            typeof(EditorAuthoringMutationScope).GetMethod("FixedRenameNoReplace", flags)!.GetParameters(),
            parameter => parameter.Name == "expectedDestinationIdentity");
        Assert.Contains(
            typeof(EditorAuthoringMutationScope).GetMethod("FixedRenameExchange", flags)!.GetParameters(),
            parameter => parameter.Name == "expectedDestinationIdentity");
        Assert.Contains(
            typeof(EditorAuthoringMutationScope).GetMethod("FixedDeleteVerifiedDirectoryTree", flags)!.GetParameters(),
            parameter => parameter.Name == "expectedIdentity");
        Assert.NotNull(typeof(EditorAuthoringMutationScope).GetProperty("MutationHookForTests", flags));
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

    [Fact]
    public void FixedRenameNoReplace_WhenSourceIdentityChangesAfterProof_PreservesReplacement() {
        string source = Path.Combine(ProjectRootPath, "assets", "proof-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "proof-destination.hasset");
        File.WriteAllBytes(source, new byte[] { 1 });
        string expectedIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, source);
        bool changed = false;
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (!changed && point == "FixedRename.BeforeSyscall") {
                    changed = true;
                    File.Delete(source);
                    File.WriteAllBytes(source, new byte[] { 7 });
                }
            };

            Assert.Throws<InvalidDataException>(() => EditorAuthoringMutationScope.FixedRenameNoReplace(
                ProjectRootPath, source, destination, expectedIdentity));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        Assert.False(File.Exists(destination));
        Assert.Equal(new byte[] { 7 }, File.ReadAllBytes(source));
    }

    [Fact]
    public void FixedRenameNoReplace_WhenDestinationAppearsAfterProof_PreservesBothEntries() {
        string source = Path.Combine(ProjectRootPath, "assets", "proof-destination-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "proof-destination-race.hasset");
        File.WriteAllBytes(source, new byte[] { 2 });
        string expectedIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, source);
        bool appeared = false;
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (!appeared && point == "FixedRename.BeforeSyscall") {
                    appeared = true;
                    File.WriteAllBytes(destination, new byte[] { 8 });
                }
            };

            Assert.Throws<IOException>(() => EditorAuthoringMutationScope.FixedRenameNoReplace(
                ProjectRootPath, source, destination, expectedIdentity));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        Assert.Equal(new byte[] { 2 }, File.ReadAllBytes(source));
        Assert.Equal(new byte[] { 8 }, File.ReadAllBytes(destination));
    }

    [Fact]
    public void FixedDeleteVerifiedLeaf_WhenIdentityChangesAfterProof_PreservesReplacement() {
        string path = Path.Combine(ProjectRootPath, "assets", "proof-delete.hasset");
        File.WriteAllBytes(path, new byte[] { 3 });
        string expectedIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, path);
        bool changed = false;
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (!changed && point == "FixedDelete.BeforeSyscall") {
                    changed = true;
                    File.Delete(path);
                    File.WriteAllBytes(path, new byte[] { 6 });
                }
            };

            Assert.Throws<InvalidDataException>(() => EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(
                ProjectRootPath, path, expectedIdentity));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        Assert.Equal(new byte[] { 6 }, File.ReadAllBytes(path));
    }

    [Fact]
    public void FixedRenameExchange_WhenDestinationIdentityChangesAfterProof_PreservesReplacement() {
        string source = Path.Combine(ProjectRootPath, "assets", "exchange-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "exchange-destination.hasset");
        File.WriteAllBytes(source, new byte[] { 4 });
        File.WriteAllBytes(destination, new byte[] { 5 });
        string sourceIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, source);
        string destinationIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, destination);
        bool changed = false;
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (!changed && point == "FixedRename.BeforeSyscall") {
                    changed = true;
                    File.Delete(destination);
                    File.WriteAllBytes(destination, new byte[] { 10 });
                }
            };

            Assert.Throws<InvalidDataException>(() => EditorAuthoringMutationScope.FixedRenameExchange(
                ProjectRootPath,
                source,
                destination,
                sourceIdentity,
                destinationIdentity));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        Assert.Equal(new byte[] { 4 }, File.ReadAllBytes(source));
        Assert.Equal(new byte[] { 10 }, File.ReadAllBytes(destination));
    }

    [Fact]
    public void FixedRename_WhenFailureOccursAfterNamespaceSyscall_LeavesPublishedIdentityObservable() {
        string source = Path.Combine(ProjectRootPath, "assets", "post-syscall-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "post-syscall-destination.hasset");
        File.WriteAllBytes(source, new byte[] { 11 });
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (point == "FixedRename.AfterSyscallBeforeFsync") {
                    throw new IOException("injected durability failure");
                }
            };

            Assert.Throws<IOException>(() => EditorAuthoringMutationScope.FixedRenameNoReplace(
                ProjectRootPath,
                source,
                destination,
                EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, source)));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        Assert.False(File.Exists(source));
        Assert.Equal(new byte[] { 11 }, File.ReadAllBytes(destination));
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
            Assert.Equal("missing", document.RootElement.GetProperty("ExpectedDestinationHash").GetString());
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
    public void CopyLeaf_WhenDestinationAlreadyExists_RejectsWithoutReplacingEitherEntry() {
        string source = Path.Combine(ProjectRootPath, "assets", "copy-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "copy-destination.hasset");
        File.WriteAllBytes(source, new byte[] { 1, 2, 3 });
        File.WriteAllBytes(destination, new byte[] { 9, 8, 7 });

        Assert.Throws<IOException>(() => EditorAuthoringMutationScope.CopyLeaf(ProjectRootPath, source, destination));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(source));
        Assert.Equal(new byte[] { 9, 8, 7 }, File.ReadAllBytes(destination));
    }

    [Fact]
    public void CopyLeaf_WhenDestinationAppearsDuringStaging_PreservesTheConcurrentDestination() {
        string source = Path.Combine(ProjectRootPath, "assets", "copy-race-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "copy-race-destination.hasset");
        File.WriteAllBytes(source, new byte[] { 1, 4, 9 });
        bool appeared = false;
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (!appeared && point == "FixedRename.BeforeSyscall") {
                    appeared = true;
                    File.WriteAllBytes(destination, new byte[] { 6, 4, 2 });
                }
            };

            Assert.Throws<InvalidDataException>(() => EditorAuthoringMutationScope.CopyLeaf(ProjectRootPath, source, destination));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        Assert.Equal(new byte[] { 1, 4, 9 }, File.ReadAllBytes(source));
        Assert.Equal(new byte[] { 6, 4, 2 }, File.ReadAllBytes(destination));
    }

    [Fact]
    public void WriteAllBytesAtomically_WhenDestinationChangesDuringStaging_PreservesTheConcurrentDestination() {
        string destination = Path.Combine(ProjectRootPath, "assets", "write-race-destination.hasset");
        File.WriteAllBytes(destination, new byte[] { 3, 3, 3 });
        bool changed = false;
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (!changed && point == "FixedRename.BeforeSyscall") {
                    changed = true;
                    File.WriteAllBytes(destination, new byte[] { 8, 8, 8 });
                }
            };

            Assert.Throws<InvalidDataException>(() => EditorAuthoringMutationScope.WriteAllBytesAtomically(
                ProjectRootPath,
                destination,
                new byte[] { 1, 1, 1 }));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        Assert.Equal(new byte[] { 8, 8, 8 }, File.ReadAllBytes(destination));
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
