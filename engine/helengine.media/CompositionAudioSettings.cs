namespace helengine.media;
/// <summary>Defines whether audio is produced and the common PCM layout.</summary>
public sealed class CompositionAudioSettings {
    /// <summary>Whether to produce an audio stream.</summary>
    [JsonRequired]
    public bool Enabled { get; set; } = true;
    /// <summary>Common PCM sample frames per second.</summary>
    [JsonRequired]
    public int SampleRate { get; set; } = 48000;
    /// <summary>Output channel count; initial renderer supports stereo.</summary>
    [JsonRequired]
    public int Channels { get; set; } = 2;
}
