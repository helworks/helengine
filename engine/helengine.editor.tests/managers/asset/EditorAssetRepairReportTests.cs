using Xunit;

namespace helengine.editor.tests.managers.asset;

/// <summary>
/// Verifies immutable deterministic repair evidence and candidate ordering.
/// </summary>
public sealed class EditorAssetRepairReportTests {
    /// <summary>
    /// Ensures candidate evidence orders stronger boolean evidence first and then ordinal path.
    /// </summary>
    [Fact]
    public void CandidateScore_OrdersBooleanEvidenceThenOrdinalPath() {
        EditorAssetResolutionCandidateScore currentId = new EditorAssetResolutionCandidateScore(
            isCurrentId: true,
            matchesSavedPath: false,
            matchesSavedHash: false,
            isRecordedOwner: false,
            relativePath: "z/path.fbx");
        EditorAssetResolutionCandidateScore formerId = new EditorAssetResolutionCandidateScore(
            isCurrentId: false,
            matchesSavedPath: true,
            matchesSavedHash: true,
            isRecordedOwner: true,
            relativePath: "a/path.fbx");
        EditorAssetResolutionCandidateScore ordinalFirst = new EditorAssetResolutionCandidateScore(
            isCurrentId: false,
            matchesSavedPath: false,
            matchesSavedHash: false,
            isRecordedOwner: false,
            relativePath: "A/path.fbx");
        EditorAssetResolutionCandidateScore ordinalSecond = new EditorAssetResolutionCandidateScore(
            isCurrentId: false,
            matchesSavedPath: false,
            matchesSavedHash: false,
            isRecordedOwner: false,
            relativePath: "b/path.fbx");

        Assert.True(currentId.CompareTo(formerId) < 0);
        Assert.True(ordinalFirst.CompareTo(ordinalSecond) < 0);
        Assert.IsAssignableFrom<IComparable>(currentId);
    }

    /// <summary>
    /// Ensures reports expose immutable records and deterministic concise summaries without duplicate records.
    /// </summary>
    [Fact]
    public void Report_AppendsImmutableRecordsAndSummarizesDeterministically() {
        EditorAssetRepairReport report = new EditorAssetRepairReport();
        EditorAssetRepairRecord record = new EditorAssetRepairRecord(
            EditorAssetRepairKind.PathHealing,
            "Models/Moved.fbx",
            "00112233445566778899aabbccddeeff",
            "ffeeddccbbaa99887766554433221100",
            AssetReferenceResolutionTier.Path,
            "exact normalized saved path",
            "scene.json",
            "Saved reference path was repaired.");

        report.Append(record);
        report.Append(record);

        Assert.Single(report.Records);
        Assert.Equal(record, report.Records[0]);
        Assert.Equal(report.Records, report.Snapshot);
        Assert.Contains("PathHealing", report.CreateSummary(), StringComparison.Ordinal);
        Assert.Contains("1", report.CreateSummary(), StringComparison.Ordinal);
        Assert.Throws<NotSupportedException>(() => ((IList<EditorAssetRepairRecord>)report.Records)[0] = record);
    }

    /// <summary>
    /// Ensures CLI completion includes the same concise report summary when repairs occurred.
    /// </summary>
    [Fact]
    public void CliCompletion_AppendsRepairSummaryOnlyWhenReportIsNonEmpty() {
        EditorAssetRepairReport emptyReport = new EditorAssetRepairReport();
        Assert.Equal("completed", EditorCliCommandRunner.AppendRepairSummary("completed", emptyReport));

        EditorAssetRepairReport report = new EditorAssetRepairReport();
        report.Append(new EditorAssetRepairRecord(
            EditorAssetRepairKind.HashHealing,
            "Models/Hash.fbx",
            "",
            "",
            AssetReferenceResolutionTier.ContentHash,
            "saved-hash=true",
            "",
            "hash healed"));

        Assert.Contains(report.CreateSummary(), EditorCliCommandRunner.AppendRepairSummary("completed", report), StringComparison.Ordinal);
    }
}
