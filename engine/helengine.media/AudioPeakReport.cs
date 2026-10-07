namespace helengine.media;
/// <summary>Reports output amplitude without secretly applying a limiter or normalizer.</summary>
public sealed class AudioPeakReport {
    /// <summary>Stores the maximum absolute mixed channel sample.</summary>
    public AudioPeakReport(double maximum) {Maximum=maximum;}
    /// <summary>Peak absolute value before codec conversion.</summary>
    public double Maximum {get;}
    /// <summary>Whether output can clip in a fixed-range codec or playback device.</summary>
    public bool ExceedsFullScale => Maximum>1;
}
