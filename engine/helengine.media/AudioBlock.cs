namespace helengine.media;
/// <summary>Owned interleaved PCM block indexed against a source or timeline origin.</summary>
public sealed class AudioBlock {
    /// <summary>Constructs a finite aligned block with explicit silence provenance.</summary>
    public AudioBlock(long firstSample,float[] samples,AudioFormat format,bool isSilence=false) {
        ArgumentNullException.ThrowIfNull(samples);ArgumentNullException.ThrowIfNull(format);
        if(firstSample<0 || samples.Length%format.Channels!=0) {throw new ArgumentOutOfRangeException(nameof(firstSample));}
        FirstSample=firstSample;Samples=samples;Format=format;IsSilence=isSilence;
    }
    /// <summary>Inclusive first sample-frame index in the source or timeline.</summary>
    public long FirstSample {get;}
    /// <summary>Owned interleaved PCM float samples.</summary>
    public ReadOnlyMemory<float> Samples {get;}
    /// <summary>Output PCM rate and channels.</summary>
    public AudioFormat Format {get;}
    /// <summary>Number of sample frames, rather than number of channel values.</summary>
    public int SampleCount => Samples.Length/Format.Channels;
    /// <summary>Whether absence of sound is explicit rather than a decode failure.</summary>
    public bool IsSilence {get;}
}
