namespace helengine.recording.windows.tests;
/// <summary>Validates the real publication algorithm with exclusive-create and race-aware storage.</summary>
public sealed class RecordingOutputPublisherTests {
    /// <summary>A destination that appears after validation remains byte-for-byte untouched.</summary>
    [Theory][InlineData(false)][InlineData(true)] public void ExistingOrRacingDestinationIsUntouched(bool crossVolume) {
        var files = new RecordingTestFileSystem { CrossVolume = crossVolume }; files.Files["work"] = [1, 2, 3];
        files.BeforePublish = () => files.Files["output"] = [9, 8, 7];
        Assert.Throws<IOException>(() => new RecordingOutputPublisher(files).Publish("work", "output"));
        Assert.Equal(new byte[] { 9, 8, 7 }, files.Files["output"]); Assert.True(files.Exists("work"));
    }
    /// <summary>Cross-volume completion copies bytes and removes the work file only after success.</summary>
    [Fact] public void CopiesAcrossVolumesUsingCreateNew() {
        var files = new RecordingTestFileSystem { CrossVolume = true }; files.Files["work"] = [1, 2, 3, 4];
        new RecordingOutputPublisher(files).Publish("work", "output"); Assert.False(files.Exists("work")); Assert.Equal(new byte[] { 1, 2, 3, 4 }, files.Files["output"]);
    }
    /// <summary>Partial copy cleanup removes its own target while preserving work and unrelated files.</summary>
    [Fact] public void CopyFailureDeletesOnlyOwnedPartial() {
        var files = new RecordingTestFileSystem { CrossVolume = true, FailCopy = true }; files.Files["work"] = [1, 2, 3, 4]; files.Files["other"] = [9];
        Assert.Throws<IOException>(() => new RecordingOutputPublisher(files).Publish("work", "output"));
        Assert.False(files.Exists("output")); Assert.True(files.Exists("work")); Assert.Equal(new byte[] { 9 }, files.Files["other"]);
    }
}
