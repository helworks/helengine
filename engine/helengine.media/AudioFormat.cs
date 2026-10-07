namespace helengine.media;
/// <summary>Immutable common PCM format used by source conversion and timeline mixing.</summary>
public sealed class AudioFormat {
    /// <summary>Validates a supported rate and the initial stereo layout.</summary>
    public AudioFormat(int sampleRate,int channels) {
        if(sampleRate<8000 || sampleRate>192000 || channels!=2) {throw new ArgumentOutOfRangeException(nameof(sampleRate),"PCM requires a supported rate and stereo channels.");}
        SampleRate=sampleRate;Channels=channels;
    }
    /// <summary>Sample frames per second.</summary>
    public int SampleRate {get;}
    /// <summary>Interleaved channels per sample frame.</summary>
    public int Channels {get;}
}
