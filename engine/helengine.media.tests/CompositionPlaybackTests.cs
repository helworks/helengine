namespace helengine.media.tests;
/// <summary>Checks audio-clock playback, seek generations and shared offline rendering.</summary>
public sealed class CompositionPlaybackTests {
    /// <summary>Old callbacks cannot refill the new seek generation.</summary>
    [Fact] public void PauseSeekResumeDoesNotPlayOldBuffer() {
        using var resolver=new CountingAudioResolver();var output=new FakeCompositionAudioOutput();var compositor=new RecordingCompositionCompositor();using var session=new CompositionPlaybackSession(Document(),compositor,new(resolver),output);
        session.Play();var old=output.Reader;long generation=output.Generation;output.Consume(4800);session.Pause();Assert.Equal(new MediaTime(1,10),session.Snapshot().Time);
        session.Seek(new(1,1));session.Play();Assert.True(old(0,128,generation).IsSilence);Assert.Equal(new MediaTime(1,1),session.Snapshot().Time);Assert.Equal(48000,output.Reader(0,1,output.Generation).FirstSample);
    }
    /// <summary>Skipping pictures does not advance or discard PCM.</summary>
    [Fact] public void DroppedPictureDoesNotDropAudio() {
        using var resolver=new CountingAudioResolver();var output=new FakeCompositionAudioOutput();using var session=new CompositionPlaybackSession(Document(),new RecordingCompositionCompositor(),new(resolver),output);
        session.Play();output.Reader(0,4800,output.Generation);output.Consume(4800);Assert.Equal(new MediaTime(1,10),session.Snapshot().Time);output.Consume(4800);Assert.Equal(new MediaTime(1,5),session.Snapshot().Time);
    }
    /// <summary>The preview asks the same compositor for the exact audio-clock instant.</summary>
    [Fact] public void PreviewMatchesOfflineAtSameTime() {
        using var resolver=new CountingAudioResolver();var output=new FakeCompositionAudioOutput();var compositor=new RecordingCompositionCompositor();using var session=new CompositionPlaybackSession(Document(),compositor,new(resolver),output);
        session.Seek(new(3,2));using var frame=session.ReadPreviewFrame(new(32,32));Assert.Equal(new MediaTime(3,2),compositor.LastTime);Assert.Equal(compositor.LastTime,frame.Timestamp);
    }
    /// <summary>Session cancellation reaches a blocked output before releasing owned media.</summary>
    [Fact] public async Task DisposeDuringBlockedWriteReleasesSources() {
        using var resolver=new CountingAudioResolver();var output=new FakeCompositionAudioOutput();var compositor=new RecordingCompositionCompositor();var session=new CompositionPlaybackSession(Document(),compositor,new(resolver),output);session.Play();output.Reader(0,128,output.Generation);
        var handle=output.Token.WaitHandle;var blocked=Task.Run(()=>handle.WaitOne());session.Dispose();Assert.True(await blocked.WaitAsync(TimeSpan.FromSeconds(2)));Assert.True(output.Disposed);Assert.True(compositor.Disposed);
    }
    /// <summary>The stopped preview holds the last in-range frame rather than clearing the scene at its exclusive end.</summary>
    [Fact] public void EndOfTimelineHoldsLastPicture() {using var resolver=new CountingAudioResolver();var compositor=new RecordingCompositionCompositor();using var session=new CompositionPlaybackSession(Document(),compositor,new(resolver),new FakeCompositionAudioOutput());session.Seek(new(3,1));using var frame=session.ReadPreviewFrame(new(32,32));Assert.Equal(new MediaTime(71,24),frame.Timestamp);Assert.Equal(new MediaTime(3,1),session.Snapshot().Time);}
    /// <summary>Creates a resolved three-second voice timeline.</summary>
    static CompositionDocument Document()=>new(){Id="playback",Revision=1,Width=32,Height=32,FrameRate=new(24,1),Duration=new(3,1),Media=[new(){Id="tone",Kind="audio",Path="tone.wav",Sha256=new string('a',64),Duration=new(3,1)}],AudioClips=[new(){Id="voice",MediaId="tone",Start=MediaTime.Zero,End=new(3,1),SourceOut=new(3,1)}]};
}
