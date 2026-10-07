namespace helengine.media.tests;
/// <summary>Checks stream ownership, bounded queues and atomic cancellation behavior.</summary>
public sealed class CompositionExportTests {
    /// <summary>The encoder receives exactly forty-eight composed frames and ninety-six thousand mixed sample frames.</summary>
    [Fact] public async Task EncoderReceivesComposedFramesAndMixedPcmOnly() {
        using var resolver=new CountingAudioResolver();using var mixer=new CompositionAudioMixer(resolver);using var compositor=new RecordingCompositionCompositor();using var encoder=new FakeCompositionEncoder();var exporter=new CompositionExporter(compositor,mixer,encoder,MediaCapabilities.Basic());string output=Output();try {var report=await exporter.ExportAsync(Document(),OutputProfile.Resolve("mp4-h264-aac.v1",32,32),output,CancellationToken.None);Assert.Equal(48,report.FrameCount);Assert.Equal(96000,report.SampleCount);Assert.Equal(48,encoder.Frames.Count);Assert.Equal(96000,encoder.Samples);Assert.True(File.Exists(output));Assert.True(File.Exists(output+".report.json"));}finally {Delete(output);}
    }
    /// <summary>Cancellation cannot overwrite an existing successful export with a partial file.</summary>
    [Fact] public async Task CancelDoesNotPublish() {
        using var resolver=new CountingAudioResolver();using var mixer=new CompositionAudioMixer(resolver);using var compositor=new RecordingCompositionCompositor();using var encoder=new FakeCompositionEncoder{DelayMilliseconds=30};var exporter=new CompositionExporter(compositor,mixer,encoder,MediaCapabilities.Basic());string output=Output();await File.WriteAllBytesAsync(output,[9]);using var cancellation=new CancellationTokenSource(50);try {await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>exporter.ExportAsync(Document(),OutputProfile.Resolve("mp4-h264-aac.v1",32,32),output,cancellation.Token));Assert.Equal(new byte[]{9},await File.ReadAllBytesAsync(output));Assert.False(File.Exists(output+".report.json"));}finally {Delete(output);}
    }
    /// <summary>A slow encoder holds at most one composed video frame in flight.</summary>
    [Fact] public async Task SlowEncoderIsBounded() {
        using var resolver=new CountingAudioResolver();using var mixer=new CompositionAudioMixer(resolver);using var compositor=new RecordingCompositionCompositor();using var encoder=new FakeCompositionEncoder{DelayMilliseconds=1};var exporter=new CompositionExporter(compositor,mixer,encoder,MediaCapabilities.Basic());string output=Output();try {await exporter.ExportAsync(Document(),OutputProfile.Resolve("mp4-h264-aac.v1",32,32),output,CancellationToken.None);Assert.Equal(1,encoder.MaximumWrites);}finally {Delete(output);}
    }
    /// <summary>Profile metadata explicitly declares engine-side alpha handling.</summary>
    [Fact] public void Mp4FlattensAlphaAndMovRetainsIt() {Assert.False(OutputProfile.Resolve("mp4-h264-aac.v1",32,32).PreserveAlpha);Assert.True(OutputProfile.Resolve("mov-prores4444-pcm.v1",32,32).PreserveAlpha);}
    /// <summary>Creates a resolved two-second stereo composition.</summary>
    static CompositionDocument Document()=>new(){Id="export",Revision=1,Width=32,Height=32,FrameRate=new(24,1),Duration=new(2,1),Audio=new(){Enabled=true},Media=[new(){Id="tone",Kind="audio",Path="tone.wav",Sha256=new string('a',64),Duration=new(2,1)}],AudioClips=[new(){Id="voice",MediaId="tone",Start=MediaTime.Zero,End=new(2,1),SourceOut=new(2,1)}]};
    /// <summary>Builds a unique test-owned output in the visible build tree.</summary>
    static string Output()=>"C:/dev/helworks/builds/helengine/media-composition/fixtures/export-"+Guid.NewGuid().ToString("N")+".mp4";
    /// <summary>Deletes only this test's exact output and report.</summary>
    static void Delete(string output) {File.Delete(output);File.Delete(output+".report.json");}
}
