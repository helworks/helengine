using Xunit;
using System.Text.Json;

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
    public void BeginAndComplete_PublishesAndRetiresOneDurableOperationDocument() {
        string source = Path.Combine(ProjectRootPath, "assets", "source.hasset");
        string destination = Path.Combine(ProjectRootPath, "assets", "destination.hasset");
        using (EditorAuthoringMutationJournal journal = EditorAuthoringMutationJournal.Begin(ProjectRootPath, "replace", source, destination)) {
            string[] documents = Directory.GetFiles(Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations"), "*.json");
            Assert.Single(documents);
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(documents[0]));
            Assert.Equal("replace", document.RootElement.GetProperty("Kind").GetString());
            Assert.Contains("missing", document.RootElement.GetProperty("ExpectedSourceIdentity").GetString(), StringComparison.OrdinalIgnoreCase);
            journal.Complete();
        }

        Assert.Empty(Directory.GetFiles(Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations"), "*.json"));
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
        Directory.CreateDirectory(journalDirectory);
        File.WriteAllText(Path.Combine(journalDirectory, "bad.json"), "{\"Version\":99}");

        Assert.Throws<InvalidDataException>(() => EditorAuthoringMutationJournal.Recover(ProjectRootPath));
        Assert.True(File.Exists(Path.Combine(journalDirectory, "bad.json")));
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
    public void Recover_WhenCompletedDocumentRetirementWasInterrupted_RemovesOnlyTheValidatedDocument() {
        string journalDirectory = Path.Combine(ProjectRootPath, "cache", "editor", "authoring-mutations");
        Directory.CreateDirectory(journalDirectory);
        string operationId = Guid.NewGuid().ToString("N");
        string journalPath = Path.Combine(journalDirectory, operationId + ".json");
        File.WriteAllText(journalPath, $"{{\"Version\":1,\"OperationId\":\"{operationId}\",\"Kind\":\"replace\",\"SourceRelativePath\":\"assets/source.hasset\",\"DestinationRelativePath\":\"assets/destination.hasset\",\"ExpectedSourceIdentity\":\"missing\",\"ExpectedDestinationIdentity\":\"missing\",\"Phase\":\"Completed\",\"TransientEntries\":[]}}");

        EditorAuthoringMutationJournal.Recover(ProjectRootPath);

        Assert.False(File.Exists(journalPath));
        Assert.Empty(Directory.GetFiles(ProjectRootPath, "outside-*", SearchOption.AllDirectories));
    }
}
