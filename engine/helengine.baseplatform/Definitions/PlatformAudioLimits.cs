namespace helengine.baseplatform.Definitions;

/// <summary>Optional maximum audio limits published by a platform cook capability.</summary>
public sealed class PlatformAudioLimits {
    /// <summary>Initializes positive sample-rate and channel limits.</summary>
    public PlatformAudioLimits(int maximumSampleRate, int maximumChannels) {
        if (maximumSampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(maximumSampleRate));
        if (maximumChannels <= 0) throw new ArgumentOutOfRangeException(nameof(maximumChannels));
        MaximumSampleRate = maximumSampleRate;
        MaximumChannels = maximumChannels;
    }
    /// <summary>Gets the maximum accepted sample rate.</summary>
    public int MaximumSampleRate { get; }
    /// <summary>Gets the maximum accepted channel count.</summary>
    public int MaximumChannels { get; }
}