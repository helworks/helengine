namespace helengine.media;
/// <summary>Schedules a source audio interval separately from visual layers.</summary>
public sealed class AudioClip {
    /// <summary>Audio item identity.</summary>
    [JsonRequired]
    public string Id { get; set; } = "";
    /// <summary>Source providing PCM or embedded audio.</summary>
    [JsonRequired]
    public string MediaId { get; set; } = "";
    /// <summary>Inclusive first timeline instant.</summary>
    [JsonRequired]
    public MediaTime Start { get; set; } = MediaTime.Zero;
    /// <summary>Exclusive last timeline instant.</summary>
    [JsonRequired]
    public MediaTime End { get; set; } = MediaTime.Zero;
    /// <summary>Inclusive source start.</summary>
    [JsonRequired]
    public MediaTime SourceIn { get; set; } = MediaTime.Zero;
    /// <summary>Exclusive source end.</summary>
    [JsonRequired]
    public MediaTime SourceOut { get; set; } = MediaTime.Zero;
    /// <summary>Linear gain applied by the timeline mixer.</summary>
    [JsonRequired]
    public double Gain { get; set; } = 1;
    /// <summary>Whether this item intentionally produces silence.</summary>
    [JsonRequired]
    public bool Muted { get; set; }
    /// <summary>Gain envelopes evaluated per sample in local clip time.</summary>
    public List<AudioEnvelope> Envelopes {get;set;} = [];
}
