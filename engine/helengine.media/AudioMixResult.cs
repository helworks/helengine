namespace helengine.media;
/// <summary>Complete mixed PCM interval and its amplitude diagnostics.</summary>
public sealed class AudioMixResult {
    /// <summary>Attaches diagnostics to an independently owned mixed output block.</summary>
    public AudioMixResult(AudioBlock block,AudioPeakReport peaks) {Block=block;Peaks=peaks;}
    /// <summary>Mixed stereo samples indexed to the composition clock.</summary>
    public AudioBlock Block {get;}
    /// <summary>Unmodified mixed peak information.</summary>
    public AudioPeakReport Peaks {get;}
}
