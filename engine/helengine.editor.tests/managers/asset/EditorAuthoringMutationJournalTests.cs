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
        Assert.DoesNotContain("standalone-", source, StringComparison.Ordinal);
        Assert.Contains("Fixed", source, StringComparison.Ordinal);
        Assert.Contains("DestinationOld", source, StringComparison.Ordinal);
        Assert.Contains("DocumentOld", source, StringComparison.Ordinal);
        string scopeSource = File.ReadAllText(FindSourceFile("EditorAuthoringMutationScope.cs"));
        Assert.DoesNotContain("RenameLinuxExchange", scopeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("EnterEphemeral", scopeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WithoutJournal", scopeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ReplaceLinuxLeaf_ReceivesTheActualSourcePathForSourceParentOwnership() {
        BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
        MethodInfo method = typeof(EditorAuthoringMutationScope).GetMethod("ReplaceLinuxLeaf", flags);
        Assert.NotNull(method);
        Assert.Contains(method.GetParameters(), parameter => parameter.Name == "sourcePath");

        string source = File.ReadAllText(FindSourceFile("EditorAuthoringMutationScope.cs"));
        Assert.Contains("string sourcePath", source, StringComparison.Ordinal);
        Assert.Contains("sourcePath,\n                recoveryIntent: \"RollbackPublication\"", source, StringComparison.Ordinal);
    }

    [LinuxFact]
    public void ReplaceLeaf_LinuxBindsTheSourceQuarantineToTheSourcePath() {
        string source = Path.Combine(ProjectRootPath, "assets", "linux-replace-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "linux-replace-destination.hasset");
        File.WriteAllBytes(source, new byte[] { 1, 2, 3 });
        File.WriteAllBytes(destination, new byte[] { 4, 5, 6 });

        using (EditorAuthoringMutationScope scope = EditorAuthoringMutationScope.AcquireForMutation(
                   ProjectRootPath,
                   Path.Combine(ProjectRootPath, "assets"))) {
            scope.ReplaceLeaf(source, destination, replaceExisting: true);
        }

        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(destination));
        Assert.False(File.Exists(source));
    }

    [Fact]
    public void ReserveTransient_RestoreOriginalRejectsAnIntendedDestination() {
        string source = Path.Combine(ProjectRootPath, "assets", "restore-intent-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "restore-intent-destination.hasset");
        byte[] bytes = new byte[] { 1, 4, 9 };
        File.WriteAllBytes(source, bytes);
        string identity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, source);
        string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();

        using EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "replace", source, destination);
        Assert.Throws<ArgumentException>(() => EditorAuthoringMutationJournal.ReserveTransient(
            source,
            Path.GetDirectoryName(source),
            destination,
            identity,
            hash,
            "File",
            "RestoreOriginal"));
    }

    [Fact]
    public void ReserveTransient_RollbackPublicationPersistsItsIntendedDestination() {
        string source = Path.Combine(ProjectRootPath, "assets", "rollback-intent-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "rollback-intent-destination.hasset");
        byte[] bytes = new byte[] { 2, 3, 5 };
        File.WriteAllBytes(source, bytes);
        string identity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, source);
        string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();

        using EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "move", source, destination);
        string quarantine = EditorAuthoringMutationJournal.ReserveTransient(
            source,
            Path.GetDirectoryName(source),
            destination,
            identity,
            hash,
            "File",
            "RollbackPublication");
        string documentPath = Path.Combine(journal.OperationDirectoryPath, "document.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(documentPath));
        JsonElement transient = Assert.Single(document.RootElement.GetProperty("TransientEntries").EnumerateArray());
        Assert.Equal("RollbackPublication", transient.GetProperty("RecoveryIntent").GetString());
        Assert.Equal("assets/rollback-intent-destination.hasset", transient.GetProperty("IntendedDestinationRelativePath").GetString());
        Assert.Equal("Prepared", document.RootElement.GetProperty("ResumePhase").GetString());
    }

    [Fact]
    public void ReserveTransient_PersistsThePreQuarantineResumePhase() {
        string source = Path.Combine(ProjectRootPath, "assets", "resume-phase-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "resume-phase-destination.hasset");
        byte[] bytes = new byte[] { 6, 7, 8 };
        File.WriteAllBytes(source, bytes);
        string identity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, source);
        string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();

        using EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "move", source, destination);
        EditorAuthoringMutationJournal.ReserveTransient(
            source,
            Path.GetDirectoryName(source),
            destination,
            identity,
            hash,
            "File",
            "RollbackPublication");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(journal.OperationDirectoryPath, "document.json")));
        Assert.Equal("Prepared", document.RootElement.GetProperty("ResumePhase").GetString());
        Assert.Equal("Quarantining", document.RootElement.GetProperty("Phase").GetString());
    }

    [Fact]
    public void ReserveTransient_SecondEntryPreservesTheOriginalResumePhase() {
        string first = Path.Combine(ProjectRootPath, "assets", "resume-first.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "resume-destination.hasset");
        byte[] firstBytes = new byte[] { 1, 1, 2 };
        byte[] destinationBytes = new byte[] { 3, 5, 8 };
        File.WriteAllBytes(first, firstBytes);
        File.WriteAllBytes(destination, destinationBytes);
        string firstHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(firstBytes)).ToLowerInvariant();
        string destinationHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(destinationBytes)).ToLowerInvariant();

        using EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "replace", first, destination);
        EditorAuthoringMutationJournal.ReserveTransient(
            destination,
            Path.GetDirectoryName(destination),
            null,
            EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, destination),
            destinationHash,
            "File",
            "RestoreOriginal");
        EditorAuthoringMutationJournal.ReserveTransient(
            first,
            Path.GetDirectoryName(first),
            destination,
            EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, first),
            firstHash,
            "File",
            "RollbackPublication");

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(journal.OperationDirectoryPath, "document.json")));
        Assert.Equal("Prepared", document.RootElement.GetProperty("ResumePhase").GetString());
        Assert.Equal(2, document.RootElement.GetProperty("TransientEntries").GetArrayLength());
    }

    [Fact]
    public void ReserveTransient_RejectsTransientGraphsForNonPublicationOperations() {
        string source = Path.Combine(ProjectRootPath, "assets", "copy-transient-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "copy-transient-destination.hasset");
        byte[] bytes = new byte[] { 7, 7, 7 };
        File.WriteAllBytes(source, bytes);
        using EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "copy", source, destination);

        Assert.Throws<ArgumentException>(() => EditorAuthoringMutationJournal.ReserveTransient(
            source,
            Path.GetDirectoryName(source),
            null,
            EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, source),
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(),
            "File",
            "RestoreOriginal"));
    }

    [Fact]
    public void MoveDirectoryJournal_StoresDirectoryProofWithoutHashingTheDirectory() {
        string source = Path.Combine(ProjectRootPath, "assets", "move-directory-source");
        string destination = Path.Combine(ProjectRootPath, "assets", "move-directory-destination");
        Directory.CreateDirectory(source);
        File.WriteAllBytes(Path.Combine(source, "payload.bin"), new byte[] { 1, 3, 5 });

        string documentPath;
        using (EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "move-directory", source, destination)) {
            string sourceIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, source);
            EditorAuthoringMutationJournal.ReserveTransient(
                source,
                Path.GetDirectoryName(source),
                destination,
                sourceIdentity,
                null,
                "Directory",
                "RollbackPublication");
            documentPath = Path.Combine(journal.OperationDirectoryPath, "document.json");
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(documentPath));
        Assert.Equal("directory", document.RootElement.GetProperty("ExpectedSourceHash").GetString());
        JsonElement transient = Assert.Single(document.RootElement.GetProperty("TransientEntries").EnumerateArray());
        Assert.Equal("Directory", transient.GetProperty("EntryKind").GetString());
        Assert.Equal(JsonValueKind.Null, transient.GetProperty("ExpectedHash").ValueKind);
    }

    [Fact]
    public void MoveDirectoryJournal_RejectsAFileTransientForDirectorySource() {
        string source = Path.Combine(ProjectRootPath, "assets", "move-directory-file-proof-source");
        string destination = Path.Combine(ProjectRootPath, "assets", "move-directory-file-proof-destination");
        Directory.CreateDirectory(source);

        string identity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, source);
        using EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "move-directory", source, destination);
        EditorAuthoringMutationJournal.ReserveTransient(
            source,
            Path.GetDirectoryName(source),
            destination,
            identity,
            new string('a', 64),
            "File",
            "RollbackPublication");

        Assert.Throws<InvalidDataException>(() => EditorAuthoringMutationJournal.Recover(ProjectRootPath));
        Assert.True(Directory.Exists(source));
    }

    [Fact]
    public void PreparedMoveDirectoryRecoveryRetiresOnlyWhenBothDirectoryProofsMatch() {
        string source = Path.Combine(ProjectRootPath, "assets", "prepared-move-directory-source");
        string destination = Path.Combine(ProjectRootPath, "assets", "prepared-move-directory-destination");
        Directory.CreateDirectory(source);
        File.WriteAllBytes(Path.Combine(source, "payload.bin"), new byte[] { 8, 6, 4 });
        string operationDirectory;
        using (EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "move-directory", source, destination)) {
            operationDirectory = journal.OperationDirectoryPath;
        }

        EditorAuthoringMutationJournal.Recover(ProjectRootPath);
        EditorAuthoringMutationJournal.Recover(ProjectRootPath);

        Assert.True(Directory.Exists(source));
        Assert.False(Directory.Exists(destination));
        Assert.False(Directory.Exists(operationDirectory));
    }

    [Fact]
    public void PreparedMoveRecoveryRejectsChangedSourceBeforeRetirement() {
        string source = Path.Combine(ProjectRootPath, "assets", "prepared-move-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "prepared-move-destination.hasset");
        File.WriteAllBytes(source, new byte[] { 1, 2, 3 });
        using (EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "move", source, destination)) { }

        File.WriteAllBytes(source, new byte[] { 4, 5, 6 });

        Assert.Throws<InvalidOperationException>(() => EditorAuthoringMutationJournal.Recover(ProjectRootPath));
        Assert.Equal(new byte[] { 4, 5, 6 }, File.ReadAllBytes(source));
    }

    [Fact]
    public void PreparedMoveRecoveryRecognizesAnExactPublishedDestination() {
        string source = Path.Combine(ProjectRootPath, "assets", "prepared-published-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "prepared-published-destination.hasset");
        byte[] bytes = new byte[] { 7, 8, 9 };
        File.WriteAllBytes(source, bytes);
        string operationDirectory;
        using (EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "move", source, destination)) {
            operationDirectory = journal.OperationDirectoryPath;
        }

        File.Move(source, destination);
        EditorAuthoringMutationJournal.Recover(ProjectRootPath);
        EditorAuthoringMutationJournal.Recover(ProjectRootPath);

        Assert.Equal(bytes, File.ReadAllBytes(destination));
        Assert.False(File.Exists(source));
        Assert.False(Directory.Exists(operationDirectory));
    }

    [Fact]
    public void PreparedReplaceRecoveryRejectsChangedDestinationBeforeRetirement() {
        string source = Path.Combine(ProjectRootPath, "assets", "prepared-replace-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "prepared-replace-destination.hasset");
        File.WriteAllBytes(source, new byte[] { 1, 2, 3 });
        File.WriteAllBytes(destination, new byte[] { 4, 5, 6 });
        using (EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "replace", source, destination)) { }

        File.WriteAllBytes(destination, new byte[] { 7, 8, 9 });

        Assert.Throws<InvalidOperationException>(() => EditorAuthoringMutationJournal.Recover(ProjectRootPath));
        Assert.Equal(new byte[] { 7, 8, 9 }, File.ReadAllBytes(destination));
    }

    [Fact]
    public void ValidateDocument_RejectsAnEmptyQuarantiningGraphWithResumeState() {
        string source = Path.Combine(ProjectRootPath, "assets", "empty-quarantine-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "empty-quarantine-destination.hasset");
        File.WriteAllBytes(source, new byte[] { 1, 2, 3 });
        string documentPath;
        using (EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "move", source, destination)) {
            documentPath = Path.Combine(journal.OperationDirectoryPath, "document.json");
        }

        string text = File.ReadAllText(documentPath)
            .Replace("\"Phase\":\"Prepared\"", "\"Phase\":\"Quarantining\"", StringComparison.Ordinal)
            .Replace("\"ResumePhase\":null", "\"ResumePhase\":\"Prepared\"", StringComparison.Ordinal);
        File.WriteAllText(documentPath, text);

        Assert.Throws<InvalidDataException>(() => EditorAuthoringMutationJournal.Recover(ProjectRootPath));
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void ValidateDocument_RejectsAResumeStateWithoutTransientEntries() {
        string source = Path.Combine(ProjectRootPath, "assets", "resume-without-entry-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "resume-without-entry-destination.hasset");
        File.WriteAllBytes(source, new byte[] { 1, 2, 3 });
        string documentPath;
        using (EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "move", source, destination)) {
            documentPath = Path.Combine(journal.OperationDirectoryPath, "document.json");
        }

        string text = File.ReadAllText(documentPath)
            .Replace("\"ResumePhase\":null", "\"ResumePhase\":\"Prepared\"", StringComparison.Ordinal);
        File.WriteAllText(documentPath, text);

        Assert.Throws<InvalidDataException>(() => EditorAuthoringMutationJournal.Recover(ProjectRootPath));
        Assert.True(File.Exists(source));
    }

    [Fact]
    public void SetTransientCleanupPending_RejectsQuarantiningState() {
        string source = Path.Combine(ProjectRootPath, "assets", "cleanup-pending-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "cleanup-pending-destination.hasset");
        File.WriteAllBytes(source, new byte[] { 1, 2, 3 });
        File.WriteAllBytes(destination, new byte[] { 4, 5, 6 });
        string destinationIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, destination);
        string destinationHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(new byte[] { 4, 5, 6 })).ToLowerInvariant();

        using EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "replace", source, destination);
        string quarantine = EditorAuthoringMutationJournal.ReserveTransient(
            destination,
            Path.GetDirectoryName(destination),
            null,
            destinationIdentity,
            destinationHash,
            "File",
            "RestoreOriginal");

        Assert.Throws<InvalidDataException>(() => EditorAuthoringMutationJournal.MarkTransientCleanupPending(
            Path.Combine(Path.GetDirectoryName(destination), quarantine)));
    }

    [Fact]
    public void ReserveTransientName_WhenDocumentWriteIsActive_RejectsReentrantMutation() {
        string source = Path.Combine(ProjectRootPath, "assets", "reentrant-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "reentrant-destination.hasset");
        using EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "replace", source, destination);
        using IDisposable documentWrite = EditorAuthoringMutationJournal.EnterDocumentWriteScope();

        Assert.Throws<InvalidOperationException>(() => EditorAuthoringMutationJournal.ReserveTransientName("payload"));
        Assert.Throws<InvalidOperationException>(() => journal.MarkPhase("Quarantining"));
        Assert.Throws<InvalidOperationException>(() => EditorAuthoringMutationJournal.SetCurrentExpectedIdentities("missing"));
        Assert.Throws<InvalidOperationException>(() => journal.Complete());
        Assert.Throws<InvalidOperationException>(() => EditorAuthoringMutationJournal.Begin(ProjectRootPath, "replace", source, destination));
    }

    [Fact]
    public void ReserveTransientName_PersistsOriginalQuarantineKindIntentAndReservedLifecycle() {
        string source = Path.Combine(ProjectRootPath, "assets", "structured-transient-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "structured-transient-destination.hasset");
        File.WriteAllBytes(source, new byte[] { 2, 4, 6 });
        File.WriteAllBytes(destination, new byte[] { 7, 8, 9 });
        string destinationIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, destination);
        string destinationHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(new byte[] { 7, 8, 9 })).ToLowerInvariant();
        string journalPath;
        using (EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "replace", source, destination)) {
            EditorAuthoringMutationJournal.ReserveTransientName(
                Path.GetFileName(destination),
                Path.GetDirectoryName(destination),
                destinationIdentity,
                destinationHash,
                "RestoreOriginal");
            journalPath = Path.Combine(journal.OperationDirectoryPath, "document.json");
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(journalPath));
        JsonElement transient = Assert.Single(document.RootElement.GetProperty("TransientEntries").EnumerateArray());
        Assert.Equal("assets/structured-transient-destination.hasset", transient.GetProperty("OriginalRelativePath").GetString());
        Assert.StartsWith("assets/.authoring-mutation-", transient.GetProperty("QuarantineRelativePath").GetString(), StringComparison.Ordinal);
        Assert.Equal("File", transient.GetProperty("EntryKind").GetString());
        Assert.Equal("RestoreOriginal", transient.GetProperty("RecoveryIntent").GetString());
        Assert.Equal("Reserved", transient.GetProperty("Lifecycle").GetString());
        Assert.Equal(destinationIdentity, transient.GetProperty("ExpectedIdentity").GetString());
        Assert.Equal(destinationHash, transient.GetProperty("ExpectedHash").GetString());
    }

    [Fact]
    public void Recover_RestoresAnOccupiedTransientToItsRecordedOriginalPath() {
        string source = Path.Combine(ProjectRootPath, "assets", "recover-transient-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "recover-transient-destination.hasset");
        byte[] bytes = new byte[] { 8, 5, 3, 5, 8 };
        File.WriteAllBytes(source, bytes);
        string identity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, source);
        string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        string quarantine;
        using (EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "move", source, destination)) {
            quarantine = EditorAuthoringMutationJournal.ReserveTransient(
                source,
                Path.GetDirectoryName(source),
                destination,
                identity,
                hash,
                "File",
                "RollbackPublication");
            string quarantinePath = Path.Combine(Path.GetDirectoryName(source), quarantine);
            File.Move(source, quarantinePath);
            EditorAuthoringMutationJournal.RecordTransientOccupied(quarantinePath);
        }

        EditorAuthoringMutationJournal.Recover(ProjectRootPath);

        Assert.Equal(bytes, File.ReadAllBytes(source));
        Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(source), quarantine)));
        Assert.Empty(Directory.GetDirectories(Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations")));
    }

    [Fact]
    public void Recover_PublishedTransientRecognizesExactIntendedDestinationAfterQuarantineDisappears() {
        string source = Path.Combine(ProjectRootPath, "assets", "recover-publish-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "recover-publish-destination.hasset");
        byte[] bytes = new byte[] { 13, 21, 34 };
        File.WriteAllBytes(source, bytes);
        string identity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, source);
        string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        string quarantine;
        using (EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "move", source, destination)) {
            quarantine = EditorAuthoringMutationJournal.ReserveTransient(
                source,
                Path.GetDirectoryName(source),
                destination,
                identity,
                hash,
                "File",
                "RollbackPublication");
            string quarantinePath = Path.Combine(Path.GetDirectoryName(source), quarantine);
            File.Move(source, quarantinePath);
            EditorAuthoringMutationJournal.RecordTransientOccupied(quarantinePath);
            File.Move(quarantinePath, destination);
            // The source inode reached its intended destination and the
            // lifecycle/outer phase were durably advanced before the process
            // stopped. Recovery must finish committed cleanup, not roll the
            // publication back to the original source path.
            EditorAuthoringMutationJournal.MarkTransientPublished(Path.Combine(Path.GetDirectoryName(source), quarantine));
            journal.MarkPhase("Published");
        }

        EditorAuthoringMutationJournal.Recover(ProjectRootPath);

        Assert.Equal(bytes, File.ReadAllBytes(destination));
        Assert.False(File.Exists(source));
        Assert.Empty(Directory.GetDirectories(Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations")));
    }

    [Fact]
    public void Recover_CommittedReplacementDoesNotCompareFormerDestinationAgainstItsOldProof() {
        string source = Path.Combine(ProjectRootPath, "assets", "committed-replacement-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "committed-replacement-destination.hasset");
        byte[] sourceBytes = new byte[] { 1, 4, 9 };
        byte[] destinationBytes = new byte[] { 2, 7, 1 };
        File.WriteAllBytes(source, sourceBytes);
        File.WriteAllBytes(destination, destinationBytes);
        string sourceIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, source);
        string destinationIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, destination);
        string sourceHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(sourceBytes)).ToLowerInvariant();
        string destinationHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(destinationBytes)).ToLowerInvariant();
        string operationDirectory;

        using (EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "replace", source, destination)) {
            string destinationQuarantine = EditorAuthoringMutationJournal.ReserveTransient(
                destination,
                Path.GetDirectoryName(destination),
                null,
                destinationIdentity,
                destinationHash,
                "File",
                "RestoreOriginal");
            string destinationQuarantinePath = Path.Combine(Path.GetDirectoryName(destination), destinationQuarantine);
            File.Move(destination, destinationQuarantinePath);
            EditorAuthoringMutationJournal.RecordTransientOccupied(destinationQuarantinePath);

            string sourceQuarantine = EditorAuthoringMutationJournal.ReserveTransient(
                source,
                Path.GetDirectoryName(source),
                destination,
                sourceIdentity,
                sourceHash,
                "File",
                "RollbackPublication");
            string sourceQuarantinePath = Path.Combine(Path.GetDirectoryName(source), sourceQuarantine);
            File.Move(source, sourceQuarantinePath);
            EditorAuthoringMutationJournal.RecordTransientOccupied(sourceQuarantinePath);
            File.Move(sourceQuarantinePath, destination);
            EditorAuthoringMutationJournal.MarkTransientPublished(sourceQuarantinePath);
            journal.MarkPhase("Published");
            operationDirectory = journal.OperationDirectoryPath;
        }

        EditorAuthoringMutationJournal.Recover(ProjectRootPath);

        Assert.Equal(sourceBytes, File.ReadAllBytes(destination));
        Assert.False(File.Exists(source));
        Assert.False(Directory.Exists(operationDirectory));
    }

    [Fact]
    public void Recover_CommittedReplacementResumesAfterSourceRecordRemovalAndCleanupPending() {
        string source = Path.Combine(ProjectRootPath, "assets", "committed-cut-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "committed-cut-destination.hasset");
        byte[] sourceBytes = new byte[] { 6, 2, 4 };
        byte[] destinationBytes = new byte[] { 8, 1, 0 };
        File.WriteAllBytes(source, sourceBytes);
        File.WriteAllBytes(destination, destinationBytes);
        string sourceIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, source);
        string destinationIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, destination);
        string sourceHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(sourceBytes)).ToLowerInvariant();
        string destinationHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(destinationBytes)).ToLowerInvariant();
        string destinationQuarantinePath;
        using (EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "replace", source, destination)) {
            string destinationQuarantine = EditorAuthoringMutationJournal.ReserveTransient(
                destination,
                Path.GetDirectoryName(destination),
                null,
                destinationIdentity,
                destinationHash,
                "File",
                "RestoreOriginal");
            destinationQuarantinePath = Path.Combine(Path.GetDirectoryName(destination), destinationQuarantine);
            File.Move(destination, destinationQuarantinePath);
            EditorAuthoringMutationJournal.RecordTransientOccupied(destinationQuarantinePath);
            string sourceQuarantine = EditorAuthoringMutationJournal.ReserveTransient(
                source,
                Path.GetDirectoryName(source),
                destination,
                sourceIdentity,
                sourceHash,
                "File",
                "RollbackPublication");
            string sourceQuarantinePath = Path.Combine(Path.GetDirectoryName(source), sourceQuarantine);
            File.Move(source, sourceQuarantinePath);
            EditorAuthoringMutationJournal.RecordTransientOccupied(sourceQuarantinePath);
            File.Move(sourceQuarantinePath, destination);
            EditorAuthoringMutationJournal.MarkTransientPublished(sourceQuarantinePath);
            journal.MarkPhase("Published");
            EditorAuthoringMutationJournal.CompleteTransient(sourceQuarantinePath);
            EditorAuthoringMutationJournal.MarkTransientCleanupPending(destinationQuarantinePath);
        }

        EditorAuthoringMutationJournal.Recover(ProjectRootPath);
        EditorAuthoringMutationJournal.Recover(ProjectRootPath);

        Assert.Equal(sourceBytes, File.ReadAllBytes(destination));
        Assert.False(File.Exists(source));
        Assert.False(File.Exists(destinationQuarantinePath));
    }

    [Fact]
    public void Recover_CommittedReplacementRetriesEveryCleanupSecondCrashCut() {
        string[] cutPoints = new[] {
            "Recovery.AfterCleanupPendingPersist",
            "FixedDelete.AfterSyscallBeforeFsync",
            "Recovery.AfterCleanupParentFlush"
        };

        for (int cutIndex = 0; cutIndex < cutPoints.Length; cutIndex++) {
            string source = Path.Combine(ProjectRootPath, "assets", $"cleanup-cut-source-{cutIndex}.hasset");
            string destination = Path.Combine(ProjectRootPath, "assets", $"cleanup-cut-destination-{cutIndex}.hasset");
            byte[] sourceBytes = new byte[] { 6, 2, 4, (byte)cutIndex };
            byte[] destinationBytes = new byte[] { 8, 1, 0, (byte)cutIndex };
            File.WriteAllBytes(source, sourceBytes);
            File.WriteAllBytes(destination, destinationBytes);
            string sourceIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, source);
            string destinationIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, destination);
            string sourceHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(sourceBytes)).ToLowerInvariant();
            string destinationHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(destinationBytes)).ToLowerInvariant();
            string operationDirectory;
            using (EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "replace", source, destination)) {
                string destinationQuarantine = EditorAuthoringMutationJournal.ReserveTransient(
                    destination,
                    Path.GetDirectoryName(destination),
                    null,
                    destinationIdentity,
                    destinationHash,
                    "File",
                    "RestoreOriginal");
                string destinationQuarantinePath = Path.Combine(Path.GetDirectoryName(destination), destinationQuarantine);
                File.Move(destination, destinationQuarantinePath);
                EditorAuthoringMutationJournal.RecordTransientOccupied(destinationQuarantinePath);

                string sourceQuarantine = EditorAuthoringMutationJournal.ReserveTransient(
                    source,
                    Path.GetDirectoryName(source),
                    destination,
                    sourceIdentity,
                    sourceHash,
                    "File",
                    "RollbackPublication");
                string sourceQuarantinePath = Path.Combine(Path.GetDirectoryName(source), sourceQuarantine);
                File.Move(source, sourceQuarantinePath);
                EditorAuthoringMutationJournal.RecordTransientOccupied(sourceQuarantinePath);
                File.Move(sourceQuarantinePath, destination);
                EditorAuthoringMutationJournal.MarkTransientPublished(sourceQuarantinePath);
                journal.MarkPhase("Published");
                operationDirectory = journal.OperationDirectoryPath;
            }

            bool interrupted = false;
            try {
                string cutPoint = cutPoints[cutIndex];
                EditorAuthoringMutationScope.MutationHookForTests = point => {
                    if (!interrupted && point == cutPoint) {
                        interrupted = true;
                        throw new IOException($"injected cleanup cut: {cutPoint}");
                    }
                };
                Assert.Throws<IOException>(() => EditorAuthoringMutationJournal.Recover(ProjectRootPath));
            } finally {
                EditorAuthoringMutationScope.MutationHookForTests = null;
            }

            Assert.True(interrupted);
            Assert.True(Directory.Exists(operationDirectory));
            EditorAuthoringMutationJournal.Recover(ProjectRootPath);
            Assert.Equal(sourceBytes, File.ReadAllBytes(destination));
            Assert.False(File.Exists(source));
            string destinationDirectory = Path.GetDirectoryName(destination);
            Assert.DoesNotContain(
                Directory.EnumerateFileSystemEntries(destinationDirectory),
                entry => Path.GetFileName(entry).StartsWith(".authoring-mutation-", StringComparison.Ordinal));
            Assert.Empty(Directory.GetDirectories(Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations")));
        }
    }

    [Fact]
    public void Recover_PreparedDeleteRejectsChangedSourceContentBeforeRetirement() {
        string source = Path.Combine(ProjectRootPath, "assets", "delete-hash-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "delete-hash-destination.hasset");
        File.WriteAllBytes(source, new byte[] { 1, 2, 3 });
        using (EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "delete", source, destination)) { }

        File.WriteAllBytes(source, new byte[] { 4, 5, 6 });

        Assert.Throws<InvalidOperationException>(() => EditorAuthoringMutationJournal.Recover(ProjectRootPath));
        Assert.Equal(new byte[] { 4, 5, 6 }, File.ReadAllBytes(source));
    }

    [Fact]
    public void Recover_PublishedMoveRejectsChangedIntendedContentBeforeRetirement() {
        string source = Path.Combine(ProjectRootPath, "assets", "published-hash-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "published-hash-destination.hasset");
        byte[] bytes = new byte[] { 9, 8, 7 };
        File.WriteAllBytes(source, bytes);
        using (EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "move", source, destination)) {
            string quarantine = EditorAuthoringMutationJournal.ReserveTransient(
                source,
                Path.GetDirectoryName(source),
                destination,
                EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, source),
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(),
                "File",
                "RollbackPublication");
            string quarantinePath = Path.Combine(Path.GetDirectoryName(source), quarantine);
            File.Move(source, quarantinePath);
            EditorAuthoringMutationJournal.RecordTransientOccupied(quarantinePath);
            File.Move(quarantinePath, destination);
            EditorAuthoringMutationJournal.MarkTransientPublished(quarantinePath);
            journal.MarkPhase("Published");
        }

        File.WriteAllBytes(destination, new byte[] { 1, 1, 1 });

        Assert.Throws<InvalidOperationException>(() => EditorAuthoringMutationJournal.Recover(ProjectRootPath));
        Assert.Equal(new byte[] { 1, 1, 1 }, File.ReadAllBytes(destination));
    }

    [Fact]
    public void Recover_ReplacementTransientGraph_RollsBackSourceThenFormerDestination() {
        string source = Path.Combine(ProjectRootPath, "assets", "graph-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "graph-destination.hasset");
        byte[] sourceBytes = new byte[] { 1, 3, 5, 7 };
        byte[] destinationBytes = new byte[] { 2, 4, 6, 8 };
        File.WriteAllBytes(source, sourceBytes);
        File.WriteAllBytes(destination, destinationBytes);
        string sourceIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, source);
        string destinationIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, destination);
        string sourceHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(sourceBytes)).ToLowerInvariant();
        string destinationHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(destinationBytes)).ToLowerInvariant();

        using (EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "replace", source, destination)) {
            string destinationQuarantine = EditorAuthoringMutationJournal.ReserveTransient(
                destination,
                Path.GetDirectoryName(destination),
                intendedDestinationPath: null,
                destinationIdentity,
                destinationHash,
                "File",
                "RestoreOriginal");
            string destinationQuarantinePath = Path.Combine(Path.GetDirectoryName(destination), destinationQuarantine);
            File.Move(destination, destinationQuarantinePath);
            EditorAuthoringMutationJournal.RecordTransientOccupied(destinationQuarantinePath);

            string sourceQuarantine = EditorAuthoringMutationJournal.ReserveTransient(
                source,
                Path.GetDirectoryName(source),
                destination,
                sourceIdentity,
                sourceHash,
                "File",
                "RollbackPublication");
            string sourceQuarantinePath = Path.Combine(Path.GetDirectoryName(source), sourceQuarantine);
            File.Move(source, sourceQuarantinePath);
            EditorAuthoringMutationJournal.RecordTransientOccupied(sourceQuarantinePath);
        }

        EditorAuthoringMutationJournal.Recover(ProjectRootPath);

        Assert.Equal(sourceBytes, File.ReadAllBytes(source));
        Assert.Equal(destinationBytes, File.ReadAllBytes(destination));
        Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations")));
    }

    [Fact]
    public void Recover_ReplacementTransientGraph_AllowsIntendedDestinationToReferenceAnotherOriginal() {
        string source = Path.Combine(ProjectRootPath, "assets", "graph-cross-source.hasset");
        string destinationDirectory = Path.Combine(ProjectRootPath, "assets", "graph-cross-destination");
        string destination = Path.Combine(destinationDirectory, "item.hasset");
        Directory.CreateDirectory(destinationDirectory);
        byte[] sourceBytes = new byte[] { 9, 7, 5 };
        byte[] destinationBytes = new byte[] { 8, 6, 4 };
        File.WriteAllBytes(source, sourceBytes);
        File.WriteAllBytes(destination, destinationBytes);
        string sourceIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, source);
        string destinationIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, destination);
        string sourceHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(sourceBytes)).ToLowerInvariant();
        string destinationHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(destinationBytes)).ToLowerInvariant();

        using (EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "replace", source, destination)) {
            string destinationQuarantine = EditorAuthoringMutationJournal.ReserveTransient(
                destination,
                Path.GetDirectoryName(destination),
                intendedDestinationPath: null,
                destinationIdentity,
                destinationHash,
                "File",
                "RestoreOriginal");
            string destinationQuarantinePath = Path.Combine(Path.GetDirectoryName(destination), destinationQuarantine);
            File.Move(destination, destinationQuarantinePath);
            EditorAuthoringMutationJournal.RecordTransientOccupied(destinationQuarantinePath);

            string sourceQuarantine = EditorAuthoringMutationJournal.ReserveTransient(
                source,
                Path.GetDirectoryName(source),
                destination,
                sourceIdentity,
                sourceHash,
                "File",
                "RollbackPublication");
            string sourceQuarantinePath = Path.Combine(Path.GetDirectoryName(source), sourceQuarantine);
            File.Move(source, sourceQuarantinePath);
            EditorAuthoringMutationJournal.RecordTransientOccupied(sourceQuarantinePath);
        }

        EditorAuthoringMutationJournal.Recover(ProjectRootPath);

        Assert.Equal(sourceBytes, File.ReadAllBytes(source));
        Assert.Equal(destinationBytes, File.ReadAllBytes(destination));
        Assert.Empty(Directory.GetFileSystemEntries(Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations")));
    }

    [Fact]
    public void Recover_ReplacementTransientGraph_RejectsCyclesBeforeTouchingEntries() {
        string first = Path.Combine(ProjectRootPath, "assets", "graph-cycle-first.hasset");
        string second = Path.Combine(ProjectRootPath, "assets", "graph-cycle-second.hasset");
        byte[] firstBytes = new byte[] { 1, 2, 3 };
        byte[] secondBytes = new byte[] { 4, 5, 6 };
        File.WriteAllBytes(first, firstBytes);
        File.WriteAllBytes(second, secondBytes);
        string firstIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, first);
        string secondIdentity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, second);
        string firstHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(firstBytes)).ToLowerInvariant();
        string secondHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(secondBytes)).ToLowerInvariant();

        using (EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "move", first, second)) {
            EditorAuthoringMutationJournal.ReserveTransient(
                first,
                Path.GetDirectoryName(first),
                second,
                firstIdentity,
                firstHash,
                "File",
                "RollbackPublication");
            EditorAuthoringMutationJournal.ReserveTransient(
                second,
                Path.GetDirectoryName(second),
                first,
                secondIdentity,
                secondHash,
                "File",
                "RollbackPublication");
        }

        Assert.Throws<InvalidDataException>(() => EditorAuthoringMutationJournal.Recover(ProjectRootPath));

        Assert.Equal(firstBytes, File.ReadAllBytes(first));
        Assert.Equal(secondBytes, File.ReadAllBytes(second));
    }

    [Fact]
    public void Recover_TransientWithMalformedFileHash_FailsClosedBeforeMutation() {
        string source = Path.Combine(ProjectRootPath, "assets", "invalid-transient-hash.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "invalid-transient-hash-destination.hasset");
        byte[] bytes = new byte[] { 10, 20, 30 };
        File.WriteAllBytes(source, bytes);
        File.WriteAllBytes(destination, new byte[] { 40, 50, 60 });
        string identity = EditorAuthoringMutationScope.CaptureVerifiedIdentity(ProjectRootPath, destination);
        using (EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "replace", source, destination)) {
            EditorAuthoringMutationJournal.ReserveTransient(
                destination,
                Path.GetDirectoryName(destination),
                intendedDestinationPath: null,
                identity,
                "not-a-sha256",
                "File",
                "RestoreOriginal");
        }

        Assert.Throws<InvalidDataException>(() => EditorAuthoringMutationJournal.Recover(ProjectRootPath));

        Assert.Equal(bytes, File.ReadAllBytes(source));
    }

    [Fact]
    public void DeleteLeaf_InterruptedAfterOwnedRename_LeavesNoTransientGraphForRecovery() {
        string source = Path.Combine(ProjectRootPath, "assets", "delete-owned-cut.hasset");
        File.WriteAllBytes(source, new byte[] { 4, 3, 2, 1 });
        bool interrupted = false;
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (!interrupted && point.StartsWith(
                        "FixedRename.AfterSyscallBeforeFsync:delete-owned-cut.hasset->.deleting-",
                        StringComparison.Ordinal)) {
                    interrupted = true;
                    throw new IOException("injected fixed deleting-entry durability cut");
                }
            };
            Assert.Throws<IOException>(() => EditorAuthoringMutationScope.DeleteLeaf(ProjectRootPath, source));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        Assert.True(interrupted);
        string journalRoot = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations");
        string[] operationEntries = Directory.Exists(journalRoot)
            ? Directory.GetFileSystemEntries(journalRoot)
            : Array.Empty<string>();
        Assert.True(operationEntries.Length > 0, $"No journal entry remained at '{journalRoot}'.");
        string operationDirectory = Assert.Single(operationEntries, path => Guid.TryParseExact(Path.GetFileName(path), "N", out _));
        using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(operationDirectory, "document.json")))) {
            Assert.Empty(document.RootElement.GetProperty("TransientEntries").EnumerateArray());
            Assert.Equal("Prepared", document.RootElement.GetProperty("Phase").GetString());
        }

        EditorAuthoringMutationJournal.Recover(ProjectRootPath);

        Assert.False(File.Exists(source));
        Assert.Empty(Directory.GetFileSystemEntries(journalRoot));
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

    [LinuxFact]
    public void LinuxDocumentLifecycle_MultiplePersistsRecoverAfterScopeRelease() {
        string source = Path.Combine(ProjectRootPath, "assets", "linux-document-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "linux-document-destination.hasset");
        string operationDirectory;
        using (EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "replace", source, destination)) {
            operationDirectory = journal.OperationDirectoryPath;
            journal.MarkPhase("Quarantining");
            journal.MarkPhase("Prepared");
            bool retirementInterrupted = false;
            try {
                EditorAuthoringMutationScope.MutationHookForTests = point => {
                    if (!retirementInterrupted && point.Contains("->.deleting-", StringComparison.Ordinal)) {
                        retirementInterrupted = true;
                        throw new IOException("injected retirement cut");
                    }
                };
                journal.Complete();
            } finally {
                EditorAuthoringMutationScope.MutationHookForTests = null;
            }
        }

        Assert.True(Directory.Exists(operationDirectory));
        EditorAuthoringMutationJournal.Recover(ProjectRootPath);
        EditorAuthoringMutationJournal.Recover(ProjectRootPath);

        Assert.False(Directory.Exists(operationDirectory));
    }

    [LinuxFact]
    public void LinuxQuarantine_PersistsRelativePathAndIdentityBeforeNamespaceMove() {
        string source = Path.Combine(ProjectRootPath, "assets", "linux-owned-quarantine.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "linux-owned-quarantine-destination.hasset");
        File.WriteAllBytes(source, new byte[] { 3, 1, 4 });
        string operationDirectory = null;
        bool interrupted = false;
        try {
            EditorAuthoringMutationScope.MutationHookForTests = point => {
                if (!interrupted && point == "FixedRename.AfterQuarantineProof") {
                    interrupted = true;
                    throw new IOException("injected quarantine cut");
                }
            };
            Assert.Throws<IOException>(() => EditorAuthoringMutationScope.MoveLeaf(ProjectRootPath, source, destination));
        } finally {
            EditorAuthoringMutationScope.MutationHookForTests = null;
        }

        string journalRoot = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations");
        string[] operations = Directory.Exists(journalRoot) ? Directory.GetDirectories(journalRoot) : Array.Empty<string>();
        operationDirectory = Assert.Single(operations, path => Guid.TryParseExact(Path.GetFileName(path), "N", out _));
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(operationDirectory, "document.json")));
        JsonElement transient = Assert.Single(document.RootElement.GetProperty("TransientEntries").EnumerateArray());
        Assert.Contains("assets/", transient.GetProperty("OriginalRelativePath").GetString(), StringComparison.Ordinal);
        Assert.Contains("assets/", transient.GetProperty("QuarantineRelativePath").GetString(), StringComparison.Ordinal);
        Assert.Equal("File", transient.GetProperty("EntryKind").GetString());
        Assert.Equal("RollbackPublication", transient.GetProperty("RecoveryIntent").GetString());
        Assert.Equal("Occupied", transient.GetProperty("Lifecycle").GetString());
        Assert.StartsWith("dev:", transient.GetProperty("ExpectedIdentity").GetString(), StringComparison.Ordinal);
        Assert.Equal(64, transient.GetProperty("ExpectedHash").GetString()?.Length);

        EditorAuthoringMutationJournal.Recover(ProjectRootPath);
        Assert.True(File.Exists(source));
        Assert.False(File.Exists(destination));
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
    public void Recover_WhenBareStagedPayloadWasDeletedAndDestinationIsStillMissing_RetiresIdempotently() {
        string source = Path.Combine(ProjectRootPath, "assets", "bare-staged-deleted-source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "bare-staged-deleted-destination.hasset");
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

        File.Delete(Path.Combine(operationDirectory, "staged", "payload"));

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
        string moved = Path.Combine(ProjectRootPath, "cache", "proof-directory-replacement");
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
