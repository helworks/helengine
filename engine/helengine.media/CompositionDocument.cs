namespace helengine.media;
/// <summary>Versioned executable media timeline with resolved assets and ordered visual and audio items.</summary>
public sealed class CompositionDocument {
    /// <summary>Identifies the exact supported composition protocol.</summary>
    [JsonRequired]
    public string Schema { get; set; } = "helengine.media.composition.v1";
    /// <summary>Stable composition identity.</summary>
    [JsonRequired]
    public string Id { get; set; } = "";
    /// <summary>Positive revision pinned to the render job.</summary>
    [JsonRequired]
    public int Revision { get; set; }
    /// <summary>Output pixel width.</summary>
    [JsonRequired]
    public int Width { get; set; }
    /// <summary>Output pixel height.</summary>
    [JsonRequired]
    public int Height { get; set; }
    /// <summary>Exact output frames per second.</summary>
    [JsonRequired]
    public MediaTime FrameRate { get; set; } = MediaTime.Zero;
    /// <summary>Explicit timeline end.</summary>
    [JsonRequired]
    public MediaTime Duration { get; set; } = MediaTime.Zero;
    /// <summary>Output audio configuration including an explicit silent mode.</summary>
    [JsonRequired]
    public CompositionAudioSettings Audio { get; set; } = new();
    /// <summary>Immutable source identities resolved by the host.</summary>
    [JsonRequired]
    public List<MediaReference> Media { get; set; } = [];
    /// <summary>Visual items ordered by layer order and declaration position.</summary>
    [JsonRequired]
    public List<VisualLayer> Layers { get; set; } = [];
    /// <summary>Scheduled audio clips mixed by the engine.</summary>
    [JsonRequired]
    public List<AudioClip> AudioClips { get; set; } = [];
    /// <summary>Explicit visual overlaps between layer outputs.</summary>
    [JsonRequired]
    public List<CompositionTransition> Transitions { get; set; } = [];
    /// <summary>Explicit canvas clear color, supporting transparent MOV profiles.</summary>
    public string BackgroundColor {get;set;} = "#171A21FF";
}
