namespace helengine.media;
/// <summary>Schedules an independently composed visual source on the timeline.</summary>
public sealed class VisualLayer {
    /// <summary>Layer identity, distinct from its source identity.</summary>
    [JsonRequired]
    public string Id { get; set; } = "";
    /// <summary>Resolved source identity.</summary>
    [JsonRequired]
    public string MediaId { get; set; } = "";
    /// <summary>Back-to-front ordering index.</summary>
    [JsonRequired]
    public int Order { get; set; }
    /// <summary>Inclusive first timeline instant.</summary>
    [JsonRequired]
    public MediaTime Start { get; set; } = MediaTime.Zero;
    /// <summary>Exclusive last timeline instant.</summary>
    [JsonRequired]
    public MediaTime End { get; set; } = MediaTime.Zero;
    /// <summary>Inclusive source start for a video.</summary>
    [JsonRequired]
    public MediaTime SourceIn { get; set; } = MediaTime.Zero;
    /// <summary>Exclusive source end for a video.</summary>
    [JsonRequired]
    public MediaTime SourceOut { get; set; } = MediaTime.Zero;
    /// <summary>Ordered typed effects pinned to implementation versions.</summary>
    [JsonRequired]
    public List<MediaEffect> Effects { get; set; } = [];
}
