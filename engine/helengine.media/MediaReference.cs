namespace helengine.media;
/// <summary>Pins a local media source to its identity, bytes and dimensions.</summary>
public sealed class MediaReference {
    /// <summary>Source identity used by timeline items.</summary>
    [JsonRequired]
    public string Id { get; set; } = "";
    /// <summary>Image, video or audio source discriminator.</summary>
    [JsonRequired]
    public string Kind { get; set; } = "";
    /// <summary>Relative path under the authorized media root.</summary>
    [JsonRequired]
    public string Path { get; set; } = "";
    /// <summary>Hash verified before opening the actual source.</summary>
    [JsonRequired]
    public string Sha256 { get; set; } = "";
    /// <summary>Declared image or video width.</summary>
    [JsonRequired]
    public int Width { get; set; }
    /// <summary>Declared image or video height.</summary>
    [JsonRequired]
    public int Height { get; set; }
    /// <summary>Source duration; images use zero.</summary>
    [JsonRequired]
    public MediaTime Duration { get; set; } = MediaTime.Zero;
}
