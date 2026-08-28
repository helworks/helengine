using Xunit;
using System.Text.Json;
using System.Reflection;

namespace helengine.editor.tests.managers.asset;

/// <summary>
/// Verifies that inode-bound namespace work has a project-scoped durable boundary.
/// </summary>
sealed class LinuxFactAttribute : FactAttribute {
    public LinuxFactAttribute() {
        if (!OperatingSystem.IsLinux()) {
            Skip = "Linux descriptor-relative mutation tests require a Linux test host.";
        }
    }
}

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
            typeof(EditorAuthoringMutationScope).GetMethod("FixedDeleteVerifiedDirectoryTree", flags)!.GetParameters(),
            parameter => parameter.Name == "expectedIdentity");
        Assert.NotNull(typeof(EditorAuthoringMutationScope).GetProperty("MutationHookForTests", flags));
        BindingFlags journalFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        Assert.NotNull(typeof(EditorAuthoringMutationJournal).GetMethod("CreatePublishingPayloadPath", journalFlags));
        Assert.NotNull(typeof(EditorAuthoringMutationJournal).GetMethod("CreateDestinationOldPath", journalFlags));
    }

    [Fact]
    public void MutationJournalSource_UsesFixedPrimitivesForItsOwnLifecycle() {
        string sourcePath = FindSourceFile("EditorAuthoringMutationJournal.cs");
        string source = File.ReadAllText(sourcePath);

        Assert.DoesNotContain("WithoutJournal(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RecoverStagedDocument", source, StringComparison.Ordinal);
        Assert.DoesNotContain("FixedRenameExchange", source, StringComparison.Ordinal);
        Assert.Contains("Fixed", source, StringComparison.Ordinal);
        Assert.Contains("DestinationOld", source, StringComparison.Ordinal);
        Assert.Contains("DocumentOld", source, StringComparison.Ordinal);
        string scopeSource = File.ReadAllText(FindSourceFile("EditorAuthoringMutationScope.cs"));
        Assert.DoesNotContain("RenameLinuxExchange", scopeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void FixedNamePrimitives_UseNoReplaceAndVerifiedDelete() {
        string source = Path.Combine(ProjectRootPath, "assets", "fixed-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "fixed-destination.hasset");
        string replacement = Path.Combine(ProjectRootPath, "assets", "fixed-replacement.hasset");

        EditorAuthoringMutationScope.FixedCreateExclusive(ProjectRootPath, source, new byte[] { 1, 2, 3 });
        EditorAuthoringMutationScope.FixedRenameNoReplace(ProjectRootPath, source, destination);
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(destination));

        EditorAuthoringMutationScope.FixedCreateExclusive(ProjectRootPath, replacement, new byte[] { 4, 5, 6 });
        EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(ProjectRootPath, destination);
        EditorAuthoringMutationScope.FixedRenameNoReplace(ProjectRootPath, replacement, destination);
        Assert.Equal(new byte[] { 4, 5, 6 }, File.ReadAllBytes(destination));
        EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(ProjectRootPath, destination);
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
    public void FixedRenameNoReplace_WhenSourceContentChangesAfterProof_PreservesChangedSource() {
        string source = Path.Combine(ProjectRootPath, "assets", "proof-content-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "proof-content-destination.hasset");
        byte[] original = new byte[] { 1, 1, 1 };
        byte[] changed = new byte[] { 9, 9, 9 };
        File.WriteAllBytes(source, original);
        string expectedIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, source);
        string expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(original)).ToLowerInvariant();
        bool changedAfterProof = false;
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (!changedAfterProof && point == "FixedRename.BeforeSyscall") {
                    changedAfterProof = true;
                    File.WriteAllBytes(source, changed);
                }
            };

            Assert.Throws<InvalidDataException>(() => EditorAuthoringMutationScope.FixedRenameNoReplace(
                ProjectRootPath, source, destination, expectedIdentity, "missing", expectedHash));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        Assert.False(File.Exists(destination));
        Assert.Equal(changed, File.ReadAllBytes(source));
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
    public void FixedRenameNoReplace_WhenExpectedDestinationContentChangesAfterProof_PreservesChangedDestination() {
        string source = Path.Combine(ProjectRootPath, "assets", "proof-destination-content-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "proof-destination-content.hasset");
        byte[] original = new byte[] { 2, 2, 2 };
        byte[] changed = new byte[] { 8, 8, 8 };
        File.WriteAllBytes(source, new byte[] { 4, 4, 4 });
        File.WriteAllBytes(destination, original);
        string expectedSourceIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, destination);
        string expectedSourceHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(original)).ToLowerInvariant();
        bool changedAfterProof = false;
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (!changedAfterProof && point == "FixedRename.BeforeSyscall") {
                    changedAfterProof = true;
                    File.WriteAllBytes(destination, changed);
                }
            };

            Assert.Throws<InvalidDataException>(() => EditorAuthoringMutationScope.FixedRenameNoReplace(
                ProjectRootPath,
                destination,
                Path.Combine(ProjectRootPath, "cache", "destination-content-old"),
                expectedSourceIdentity,
                "missing",
                expectedSourceHash));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        Assert.False(File.Exists(Path.Combine(ProjectRootPath, "cache", "destination-content-old")));
        Assert.Equal(changed, File.ReadAllBytes(destination));
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
    public void WriteAllBytesAtomically_WhenReplacingExistingDestination_UsesFixedFormerDestinationAndPayloadNames() {
        string destination = Path.Combine(ProjectRootPath, "assets", "write-existing-destination.hasset");
        File.WriteAllBytes(destination, new byte[] { 1, 2, 3 });

        EditorAuthoringMutationScope.WriteAllBytesAtomically(ProjectRootPath, destination, new byte[] { 8, 7, 6 });

        Assert.Equal(new byte[] { 8, 7, 6 }, File.ReadAllBytes(destination));
        string[] operationRoots = Directory.Exists(Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations"))
            ? Directory.GetFileSystemEntries(Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations"))
            : Array.Empty<string>();
        Assert.Empty(operationRoots);
    }

    [Fact]
    public void Recover_WhenReplacementStopsAfterFormerDestinationMove_PublishesPayloadAndRetiresBothProofs() {
        string destination = Path.Combine(ProjectRootPath, "assets", "recover-after-former-destination.hasset");
        File.WriteAllBytes(destination, new byte[] { 2, 2, 2 });
        bool injected = false;
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (!injected && point == "FixedRename.BeforeSyscall:payload.publishing->recover-after-former-destination.hasset") {
                    injected = true;
                    throw new IOException("injected publication interruption");
                }
            };
            Assert.Throws<IOException>(() => EditorAuthoringMutationScope.WriteAllBytesAtomically(
                ProjectRootPath,
                destination,
                new byte[] { 9, 9, 9 }));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        Assert.True(injected);
        Assert.False(File.Exists(destination));
        Assert.NotEmpty(Directory.GetDirectories(Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations")));

        EditorAuthoringMutationJournal.Recover(ProjectRootPath);

        Assert.Equal(new byte[] { 9, 9, 9 }, File.ReadAllBytes(destination));
        Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations")));
    }

    [Fact]
    public void Recover_WhenReplacementStopsAfterPayloadPublish_RetiresPublishedPayloadWithoutRestoringOldBytes() {
        string destination = Path.Combine(ProjectRootPath, "assets", "recover-after-payload-publish.hasset");
        File.WriteAllBytes(destination, new byte[] { 4, 4, 4 });
        bool injected = false;
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (!injected && point == "FixedRename.AfterSyscallBeforeFsync:payload.publishing->recover-after-payload-publish.hasset") {
                    injected = true;
                    throw new IOException("injected fsync interruption");
                }
            };
            Assert.Throws<IOException>(() => EditorAuthoringMutationScope.WriteAllBytesAtomically(
                ProjectRootPath,
                destination,
                new byte[] { 7, 7, 7 }));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        Assert.True(injected);
        EditorAuthoringMutationJournal.Recover(ProjectRootPath);

        Assert.Equal(new byte[] { 7, 7, 7 }, File.ReadAllBytes(destination));
        Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations")));
    }

    [Fact]
    public void Recover_WhenNewDestinationStopsBeforeFinalPublish_CompletesFromPublishingPayload() {
        string destination = Path.Combine(ProjectRootPath, "assets", "recover-new-destination.hasset");
        bool injected = false;
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (!injected && point == "FixedRename.BeforeSyscall:payload.publishing->recover-new-destination.hasset") {
                    injected = true;
                    throw new IOException("injected new-destination interruption");
                }
            };
            Assert.Throws<IOException>(() => EditorAuthoringMutationScope.WriteAllBytesAtomically(
                ProjectRootPath,
                destination,
                new byte[] { 5, 5, 5 }));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        Assert.True(injected);
        Assert.False(File.Exists(destination));
        EditorAuthoringMutationJournal.Recover(ProjectRootPath);

        Assert.Equal(new byte[] { 5, 5, 5 }, File.ReadAllBytes(destination));
        Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations")));
    }

    [Fact]
    public void Recover_WhenPublishingReservationPrecedesPayloadRename_UsesStagedPayloadProof() {
        string source = Path.Combine(ProjectRootPath, "assets", "reservation-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "reservation-destination.hasset");
        string operationDirectory;
        using (EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "replace", source, destination)) {
            string stagedNextPath = journal.CreateStagedPayloadNextPath();
            EditorAuthoringMutationScope.FixedCreateExclusive(ProjectRootPath, stagedNextPath, new byte[] { 4, 4, 4 });
            string stagedPath = Path.Combine(journal.OperationDirectoryPath, "staged", "payload");
            EditorAuthoringMutationScope.FixedRenameNoReplace(ProjectRootPath, stagedNextPath, stagedPath);
            journal.RecordStagedPayload(stagedPath, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(new byte[] { 4, 4, 4 })).ToLowerInvariant());
            journal.ValidateStagedPayload();
            journal.CreatePublishingPayloadPath();
            operationDirectory = journal.OperationDirectoryPath;
        }

        EditorAuthoringMutationJournal.Recover(ProjectRootPath);

        Assert.Equal(new byte[] { 4, 4, 4 }, File.ReadAllBytes(destination));
        Assert.False(Directory.Exists(operationDirectory));
    }

    [Fact]
    public void Recover_WhenCopyIsBareStaged_VerifiesUnchangedDestinationThenRetiresIdempotently() {
        string source = Path.Combine(ProjectRootPath, "assets", "bare-staged-copy-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "bare-staged-copy-destination.hasset");
        File.WriteAllBytes(source, new byte[] { 1, 2, 3 });
        string operationDirectory;
        using (EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "copy", source, destination)) {
            string stagedPath = journal.CreateStagedPayloadPath("payload");
            EditorAuthoringMutationScope.FixedCreateExclusive(ProjectRootPath, stagedPath, new byte[] { 4, 5, 6 });
            journal.RecordStagedPayload(
                stagedPath,
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(new byte[] { 4, 5, 6 })).ToLowerInvariant());
            operationDirectory = journal.OperationDirectoryPath;
        }

        EditorAuthoringMutationJournal.Recover(ProjectRootPath);
        EditorAuthoringMutationJournal.Recover(ProjectRootPath);

        Assert.False(Directory.Exists(operationDirectory));
        Assert.False(File.Exists(destination));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(source));
    }

    [Fact]
    public void Recover_WhenWriteIsBareStagedAndDestinationDiverged_FailsClosedWithoutMutation() {
        string destination = Path.Combine(ProjectRootPath, "assets", "bare-staged-write-destination.hasset");
        File.WriteAllBytes(destination, new byte[] { 1, 2, 3 });
        string operationDirectory;
        using (EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "replace", destination, destination)) {
            string stagedPath = journal.CreateStagedPayloadPath("payload");
            EditorAuthoringMutationScope.FixedCreateExclusive(ProjectRootPath, stagedPath, new byte[] { 4, 5, 6 });
            journal.RecordStagedPayload(
                stagedPath,
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(new byte[] { 4, 5, 6 })).ToLowerInvariant());
            operationDirectory = journal.OperationDirectoryPath;
        }

        File.WriteAllBytes(destination, new byte[] { 7, 8, 9 });
        Assert.Throws<InvalidOperationException>(() => EditorAuthoringMutationJournal.Recover(ProjectRootPath));

        Assert.True(Directory.Exists(operationDirectory));
        Assert.Equal(new byte[] { 7, 8, 9 }, File.ReadAllBytes(destination));
    }

    [Fact]
    public void Recover_WhenExistingDestinationWasObservedBeforeFormerPathReservation_ContinuesSafely() {
        string destination = Path.Combine(ProjectRootPath, "assets", "reservation-existing-destination.hasset");
        File.WriteAllBytes(destination, new byte[] { 1, 1, 1 });
        bool interrupted = false;
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (!interrupted && point == "FixedRename.AfterSyscallBeforeFsync:payload->payload.publishing") {
                    interrupted = true;
                    throw new IOException("injected after publishing reservation");
                }
            };
            Assert.Throws<IOException>(() => EditorAuthoringMutationScope.WriteAllBytesAtomically(
                ProjectRootPath,
                destination,
                new byte[] { 8, 8, 8 }));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        Assert.True(interrupted);
        EditorAuthoringMutationJournal.Recover(ProjectRootPath);

        Assert.Equal(new byte[] { 8, 8, 8 }, File.ReadAllBytes(destination));
    }

    [Fact]
    public void WriteAllBytesAtomically_PersistsFormerDestinationProofBeforeMovingExistingDestination() {
        string destination = Path.Combine(ProjectRootPath, "assets", "former-proof-order.hasset");
        File.WriteAllBytes(destination, new byte[] { 1, 2, 3 });
        bool proofWasPersisted = false;
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (point == "FixedRename.BeforeSyscall:former-proof-order.hasset->destination.old") {
                    string journalRoot = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations");
                    string operationDirectory = Assert.Single(Directory.GetDirectories(journalRoot));
                    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(operationDirectory, "document.json")));
                    Assert.Equal("destination.old", document.RootElement.GetProperty("DestinationOldRelativePath").GetString());
                    Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("DestinationOldIdentity").GetString()));
                    Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("DestinationOldHash").GetString()));
                    proofWasPersisted = true;
                    throw new IOException("injected before former destination move");
                }
            };

            Assert.Throws<IOException>(() => EditorAuthoringMutationScope.WriteAllBytesAtomically(
                ProjectRootPath,
                destination,
                new byte[] { 8, 9, 10 }));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        Assert.True(proofWasPersisted);
        EditorAuthoringMutationJournal.Recover(ProjectRootPath);
        Assert.Equal(new byte[] { 8, 9, 10 }, File.ReadAllBytes(destination));
    }

    [Fact]
    public void CopyLeaf_WhenPublishFsyncFails_RetainsRecoverableJournal() {
        string source = Path.Combine(ProjectRootPath, "assets", "copy-fsync-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "copy-fsync-destination.hasset");
        File.WriteAllBytes(source, new byte[] { 6, 6, 6 });
        bool interrupted = false;
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (!interrupted && point == "FixedRename.AfterSyscallBeforeFsync:payload.publishing->copy-fsync-destination.hasset") {
                    interrupted = true;
                    throw new IOException("injected publication durability failure");
                }
            };
            Assert.Throws<IOException>(() => EditorAuthoringMutationScope.CopyLeaf(ProjectRootPath, source, destination));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        Assert.True(interrupted);
        Assert.True(File.Exists(destination));
        Assert.NotEmpty(Directory.GetDirectories(Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations")));

        EditorAuthoringMutationJournal.Recover(ProjectRootPath);

        Assert.Equal(new byte[] { 6, 6, 6 }, File.ReadAllBytes(destination));
        Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations")));
    }

    [Fact]
    public void Recover_WhenPublishedCopyParentFlushFails_RetainsJournalUntilSecondRecovery() {
        string source = Path.Combine(ProjectRootPath, "assets", "copy-recovery-flush-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "copy-recovery-flush-destination.hasset");
        File.WriteAllBytes(source, new byte[] { 6, 7, 8 });
        bool interruptedPublish = false;
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (!interruptedPublish && point == "FixedRename.AfterSyscallBeforeFsync:payload.publishing->copy-recovery-flush-destination.hasset") {
                    interruptedPublish = true;
                    throw new IOException("injected publication durability failure");
                }
            };
            Assert.Throws<IOException>(() => EditorAuthoringMutationScope.CopyLeaf(ProjectRootPath, source, destination));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        bool interruptedRecovery = false;
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (!interruptedRecovery && point == "Recovery.BeforePublishedDestinationFlush") {
                    interruptedRecovery = true;
                    throw new IOException("injected recovery parent flush failure");
                }
            };
            Assert.Throws<IOException>(() => EditorAuthoringMutationJournal.Recover(ProjectRootPath));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        Assert.True(interruptedPublish);
        Assert.True(interruptedRecovery);
        Assert.True(File.Exists(destination));
        Assert.NotEmpty(Directory.GetDirectories(Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations")));

        EditorAuthoringMutationJournal.Recover(ProjectRootPath);

        Assert.Equal(new byte[] { 6, 7, 8 }, File.ReadAllBytes(destination));
        Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations")));
    }

    [Fact]
    public void Recover_WhenFirstRecoveryStopsBeforeFinalPublish_CanResumeFromFixedProofs() {
        string destination = Path.Combine(ProjectRootPath, "assets", "recover-second-cut.hasset");
        File.WriteAllBytes(destination, new byte[] { 1, 1, 1 });
        bool interruptedWrite = false;
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (!interruptedWrite && point == "FixedRename.AfterSyscallBeforeFsync:payload->payload.publishing") {
                    interruptedWrite = true;
                    throw new IOException("injected pre-former-destination recovery cut");
                }
            };
            Assert.Throws<IOException>(() => EditorAuthoringMutationScope.WriteAllBytesAtomically(
                ProjectRootPath,
                destination,
                new byte[] { 7, 7, 7 }));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        bool interruptedRecovery = false;
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (!interruptedRecovery && point == "FixedRename.BeforeSyscall:payload.publishing->recover-second-cut.hasset") {
                    interruptedRecovery = true;
                    throw new IOException("injected recovery cut");
                }
            };
            Assert.Throws<IOException>(() => EditorAuthoringMutationJournal.Recover(ProjectRootPath));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        Assert.True(interruptedRecovery);
        EditorAuthoringMutationJournal.Recover(ProjectRootPath);

        Assert.Equal(new byte[] { 7, 7, 7 }, File.ReadAllBytes(destination));
        Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations")));
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
    public void FixedDeleteDirectoryTree_WhenTopEntryChangesAfterProof_PreservesReplacement() {
        string source = Path.Combine(ProjectRootPath, "cache", "proof-directory");
        string moved = Path.Combine(ProjectRootPath, "cache", "proof-directory-attacker");
        Directory.CreateDirectory(source);
        string expectedIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, source);
        bool swapped = false;
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (!swapped && point == "FixedDeleteDirectory.BeforeSyscall") {
                    swapped = true;
                    Directory.Move(source, moved);
                    Directory.CreateDirectory(source);
                }
            };

            Assert.Throws<InvalidDataException>(() => EditorAuthoringMutationScope.FixedDeleteVerifiedDirectoryTree(
                ProjectRootPath,
                source,
                ProjectRootPath,
                expectedIdentity));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        Assert.True(swapped);
        Assert.True(Directory.Exists(source));
        Assert.True(Directory.Exists(moved));
    }

    [LinuxFact]
    public void LinuxFixedRename_WhenQuarantinedSourceChangesAfterProof_PreservesBothInodes() {
        string source = Path.Combine(ProjectRootPath, "assets", "linux-rename-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "linux-rename-destination.hasset");
        string attacker = Path.Combine(ProjectRootPath, "assets", "linux-rename-attacker.hasset");
        File.WriteAllBytes(source, new byte[] { 1, 2, 3 });
        string expectedIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, source);
        string expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(new byte[] { 1, 2, 3 })).ToLowerInvariant();
        bool swapped = false;
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (!swapped && point == "FixedRename.AfterQuarantineProof") {
                    string quarantine = Directory.GetFiles(Path.GetDirectoryName(source), ".authoring-mutation-standalone-*", SearchOption.TopDirectoryOnly).Single();
                    File.Move(quarantine, attacker);
                    File.WriteAllBytes(quarantine, new byte[] { 9, 9, 9 });
                    swapped = true;
                }
            };

            Assert.Throws<InvalidDataException>(() => EditorAuthoringMutationScope.FixedRenameNoReplace(
                ProjectRootPath, source, destination, expectedIdentity, "missing", expectedHash));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        Assert.True(swapped);
        Assert.False(File.Exists(destination));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(attacker));
        Assert.Equal(new byte[] { 9, 9, 9 }, File.ReadAllBytes(source));
    }

    [LinuxFact]
    public void LinuxFixedDeleteLeaf_WhenQuarantinedEntryChangesAfterProof_PreservesBothInodes() {
        string source = Path.Combine(ProjectRootPath, "assets", "linux-delete-leaf.hasset");
        string attacker = Path.Combine(ProjectRootPath, "assets", "linux-delete-leaf-attacker.hasset");
        File.WriteAllBytes(source, new byte[] { 3, 2, 1 });
        string expectedIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, source);
        string expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(new byte[] { 3, 2, 1 })).ToLowerInvariant();
        bool swapped = false;
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (!swapped && point == "FixedDelete.AfterQuarantineProof") {
                    string quarantine = Directory.GetFiles(Path.GetDirectoryName(source), ".authoring-mutation-standalone-*", SearchOption.TopDirectoryOnly).Single();
                    File.Move(quarantine, attacker);
                    File.WriteAllBytes(quarantine, new byte[] { 8, 8, 8 });
                    swapped = true;
                }
            };

            Assert.Throws<InvalidDataException>(() => EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(
                ProjectRootPath, source, expectedIdentity, expectedHash));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        Assert.True(swapped);
        Assert.Equal(new byte[] { 3, 2, 1 }, File.ReadAllBytes(attacker));
        Assert.Equal(new byte[] { 8, 8, 8 }, File.ReadAllBytes(source));
    }

    [LinuxFact]
    public void LinuxFixedDeleteDirectory_WhenTopQuarantineChangesAfterProof_PreservesBothTrees() {
        string source = Path.Combine(ProjectRootPath, "cache", "linux-delete-directory");
        string attacker = Path.Combine(ProjectRootPath, "cache", "linux-delete-directory-attacker");
        Directory.CreateDirectory(source);
        File.WriteAllBytes(Path.Combine(source, "payload.bin"), new byte[] { 4, 5, 6 });
        string expectedIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, source);
        bool swapped = false;
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (!swapped && point == "FixedDeleteDirectory.AfterQuarantineProof") {
                    string quarantine = Directory.GetDirectories(Path.Combine(ProjectRootPath, "cache"), ".authoring-mutation-standalone-*", SearchOption.TopDirectoryOnly).Single();
                    Directory.Move(quarantine, attacker);
                    Directory.CreateDirectory(quarantine);
                    File.WriteAllBytes(Path.Combine(quarantine, "replacement.bin"), new byte[] { 7, 7, 7 });
                    swapped = true;
                }
            };

            Assert.Throws<InvalidDataException>(() => EditorAuthoringMutationScope.FixedDeleteVerifiedDirectoryTree(
                ProjectRootPath, source, ProjectRootPath, expectedIdentity));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        Assert.True(swapped);
        Assert.Equal(new byte[] { 4, 5, 6 }, File.ReadAllBytes(Path.Combine(attacker, "payload.bin")));
        Assert.Equal(new byte[] { 7, 7, 7 }, File.ReadAllBytes(Path.Combine(source, "replacement.bin")));
    }

    [LinuxFact]
    public void LinuxFixedDeleteDirectory_WhenNestedQuarantineChangesAfterProof_PreservesNestedTree() {
        string source = Path.Combine(ProjectRootPath, "cache", "linux-delete-nested-directory");
        Directory.CreateDirectory(Path.Combine(source, "child"));
        File.WriteAllBytes(Path.Combine(source, "child", "payload.bin"), new byte[] { 4, 5, 6 });
        string expectedIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, source);
        string topQuarantine = null;
        bool swapped = false;
        int proofCount = 0;
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (point != "FixedDeleteDirectory.AfterQuarantineProof") {
                    return;
                }
                proofCount++;
                string candidate = Directory.GetDirectories(Path.Combine(ProjectRootPath, "cache"), ".authoring-mutation-standalone-*", SearchOption.TopDirectoryOnly).Single();
                if (proofCount == 1) {
                    topQuarantine = candidate;
                    return;
                }
                string nestedQuarantine = Directory.GetDirectories(topQuarantine, ".authoring-mutation-standalone-*", SearchOption.AllDirectories).Single();
                string attacker = Path.Combine(Path.GetDirectoryName(nestedQuarantine), "nested-attacker");
                Directory.Move(nestedQuarantine, attacker);
                Directory.CreateDirectory(nestedQuarantine);
                File.WriteAllBytes(Path.Combine(nestedQuarantine, "replacement.bin"), new byte[] { 8, 8, 8 });
                swapped = true;
            };

            Assert.Throws<InvalidDataException>(() => EditorAuthoringMutationScope.FixedDeleteVerifiedDirectoryTree(
                ProjectRootPath, source, ProjectRootPath, expectedIdentity));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        Assert.True(swapped);
        Assert.Equal(new byte[] { 4, 5, 6 }, File.ReadAllBytes(Path.Combine(source, "child", "payload.bin")));
        Assert.Equal(new byte[] { 8, 8, 8 }, File.ReadAllBytes(Path.Combine(source, "child", "replacement.bin")));
    }

    [LinuxFact]
    public void LinuxFixedRename_WhenFinalProofChanges_PreservesSubstitutedSource() {
        string source = Path.Combine(ProjectRootPath, "assets", "linux-final-rename-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "linux-final-rename-destination.hasset");
        string attacker = Path.Combine(ProjectRootPath, "assets", "linux-final-rename-attacker.hasset");
        File.WriteAllBytes(source, new byte[] { 1, 2, 3 });
        string expectedIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, source);
        string expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(new byte[] { 1, 2, 3 })).ToLowerInvariant();
        bool swapped = false;
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (point != "FixedRename.BeforeFinalSyscall" || swapped) {
                    return;
                }
                string quarantine = Directory.GetFiles(Path.GetDirectoryName(source), ".authoring-mutation-standalone-*", SearchOption.TopDirectoryOnly).Single();
                File.Move(quarantine, attacker);
                File.WriteAllBytes(quarantine, new byte[] { 9, 9, 9 });
                swapped = true;
            };

            Assert.Throws<InvalidDataException>(() => EditorAuthoringMutationScope.FixedRenameNoReplace(
                ProjectRootPath, source, destination, expectedIdentity, "missing", expectedHash));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        Assert.True(swapped);
        Assert.False(File.Exists(destination));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(attacker));
    }

    [LinuxFact]
    public void LinuxFixedDeleteLeaf_WhenFinalProofChanges_PreservesSubstitutedLeaf() {
        string source = Path.Combine(ProjectRootPath, "assets", "linux-final-delete-leaf.hasset");
        string attacker = Path.Combine(ProjectRootPath, "assets", "linux-final-delete-attacker.hasset");
        File.WriteAllBytes(source, new byte[] { 3, 2, 1 });
        string expectedIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, source);
        string expectedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(new byte[] { 3, 2, 1 })).ToLowerInvariant();
        bool swapped = false;
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (point != "FixedDelete.BeforeFinalSyscall" || swapped) {
                    return;
                }
                string quarantine = Directory.GetFiles(Path.GetDirectoryName(source), ".authoring-mutation-standalone-*", SearchOption.TopDirectoryOnly).Single();
                File.Move(quarantine, attacker);
                File.WriteAllBytes(quarantine, new byte[] { 8, 8, 8 });
                swapped = true;
            };

            Assert.Throws<InvalidDataException>(() => EditorAuthoringMutationScope.FixedDeleteVerifiedLeaf(
                ProjectRootPath, source, expectedIdentity, expectedHash));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        Assert.True(swapped);
        Assert.Equal(new byte[] { 3, 2, 1 }, File.ReadAllBytes(attacker));
    }

    [LinuxFact]
    public void LinuxFixedDeleteDirectory_WhenFinalProofChanges_PreservesSubstitutedTopTree() {
        string source = Path.Combine(ProjectRootPath, "cache", "linux-final-delete-directory");
        string attacker = Path.Combine(ProjectRootPath, "cache", "linux-final-delete-directory-attacker");
        Directory.CreateDirectory(source);
        File.WriteAllBytes(Path.Combine(source, "payload.bin"), new byte[] { 4, 5, 6 });
        string expectedIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, source);
        string quarantine = null;
        bool swapped = false;
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (point != "FixedDeleteDirectory.BeforeFinalSyscall" || swapped) {
                    return;
                }
                quarantine = Directory.GetDirectories(Path.Combine(ProjectRootPath, "cache"), ".authoring-mutation-standalone-*", SearchOption.TopDirectoryOnly).Single();
                Directory.Move(quarantine, attacker);
                Directory.CreateDirectory(quarantine);
                File.WriteAllBytes(Path.Combine(quarantine, "replacement.bin"), new byte[] { 7, 7, 7 });
                swapped = true;
            };

            Assert.Throws<InvalidDataException>(() => EditorAuthoringMutationScope.FixedDeleteVerifiedDirectoryTree(
                ProjectRootPath, source, ProjectRootPath, expectedIdentity));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        Assert.True(swapped);
        Assert.True(Directory.Exists(attacker));
        Assert.True(Directory.Exists(quarantine));
        Assert.Equal(new byte[] { 7, 7, 7 }, File.ReadAllBytes(Path.Combine(quarantine, "replacement.bin")));
    }

    [LinuxFact]
    public void LinuxFixedDeleteDirectory_WhenNestedFinalProofChanges_PreservesSubstitutedNestedTree() {
        string source = Path.Combine(ProjectRootPath, "cache", "linux-final-delete-nested");
        Directory.CreateDirectory(Path.Combine(source, "child"));
        File.WriteAllBytes(Path.Combine(source, "child", "payload.bin"), new byte[] { 4, 5, 6 });
        string expectedIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, source);
        string topQuarantine = null;
        string nestedQuarantine = null;
        bool swapped = false;
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (point != "FixedDeleteDirectory.BeforeFinalSyscall" || swapped) {
                    return;
                }
                topQuarantine = Directory.GetDirectories(Path.Combine(ProjectRootPath, "cache"), ".authoring-mutation-standalone-*", SearchOption.TopDirectoryOnly).Single();
                nestedQuarantine = Directory.GetDirectories(topQuarantine, ".authoring-mutation-standalone-*", SearchOption.AllDirectories).Single();
                string attacker = Path.Combine(Path.GetDirectoryName(nestedQuarantine), "nested-final-attacker");
                Directory.Move(nestedQuarantine, attacker);
                Directory.CreateDirectory(nestedQuarantine);
                File.WriteAllBytes(Path.Combine(nestedQuarantine, "replacement.bin"), new byte[] { 8, 8, 8 });
                swapped = true;
            };

            Assert.Throws<InvalidDataException>(() => EditorAuthoringMutationScope.FixedDeleteVerifiedDirectoryTree(
                ProjectRootPath, source, ProjectRootPath, expectedIdentity));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        Assert.True(swapped);
        Assert.True(Directory.Exists(source));
        Assert.True(File.Exists(Path.Combine(source, "child", "nested-final-attacker", "payload.bin")));
        Assert.Equal(new byte[] { 8, 8, 8 }, File.ReadAllBytes(Path.Combine(source, "child", Path.GetFileName(nestedQuarantine), "replacement.bin")));
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
            journal.CreatePublishingPayloadPath();
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
        string currentDocumentPath = Path.Combine(operationDirectory, "document.json");
        File.WriteAllText(currentDocumentPath, document);
        string currentDocumentIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, currentDocumentPath);
        string currentDocumentHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(currentDocumentPath))).ToLowerInvariant();
        string nextDocument = document.Replace(
            "\"Sequence\":1",
            "\"Sequence\":2,\"DocumentOldRelativePath\":\"document.old\",\"DocumentOldHash\":\"" + currentDocumentHash + "\",\"DocumentOldIdentity\":\"" + currentDocumentIdentity + "\"",
            StringComparison.Ordinal);
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
