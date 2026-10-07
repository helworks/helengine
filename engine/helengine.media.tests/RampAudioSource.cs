namespace helengine.media.tests;
/// <summary>Returns each sample frame's index independently in all requested output channels.</summary>
public sealed class RampAudioSource : IMediaSource {
    /// <summary>An audio-only source cannot produce a visual frame.</summary>
    public MediaVideoFrame ReadVideoFrame(MediaTime time)=>throw new InvalidOperationException("Audio-only ramp.");
    /// <summary>Produces a bounded synthetic PCM ramp without hidden cursor state.</summary>
    public AudioBlock ReadAudio(long first,int count,AudioFormat format) {var samples=new float[count*format.Channels];for(int index=0;index<count;index++)for(int channel=0;channel<format.Channels;channel++)samples[index*format.Channels+channel]=first+index;return new(first,samples,format);}
    /// <summary>No native resources exist.</summary>
    public void Dispose() { }
}
