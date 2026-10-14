namespace helengine.media.tests;
/// <summary>Provides a 440 Hz half-scale sine computed from the absolute sample index, so any read offset is exact.</summary>
public sealed class SineAudioResolver : IMediaSourceResolver {
    /// <summary>Tone frequency in hertz.</summary>
    public const double Frequency=440;
    /// <summary>Creates an independent deterministic sine cursor.</summary>
    public IMediaSource Open(MediaReference reference)=>new SineAudioSource();
    /// <summary>No resolver-owned native handles exist.</summary>
    public void Dispose() { }
    /// <summary>Sine cursor without hidden state.</summary>
    sealed class SineAudioSource : IMediaSource {
        /// <summary>An audio-only source cannot produce a visual frame.</summary>
        public MediaVideoFrame ReadVideoFrame(MediaTime time)=>throw new InvalidOperationException("Audio-only sine.");
        /// <summary>Produces the requested sine samples in every channel.</summary>
        public AudioBlock ReadAudio(long first,int count,AudioFormat format) {var samples=new float[count*format.Channels];for(int index=0;index<count;index++) {float value=(float)(0.5*Math.Sin(2*Math.PI*Frequency*(first+index)/format.SampleRate));for(int channel=0;channel<format.Channels;channel++) {samples[index*format.Channels+channel]=value;}}return new(first,samples,format);}
        /// <summary>No native resources exist.</summary>
        public void Dispose() { }
    }
}
