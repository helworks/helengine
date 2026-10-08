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
    /// <summary>Independent static transform defaults for this layer.</summary>
    public LayerTransform Transform {get;set;} = new();
    /// <summary>Normalized presentation rectangle in the composition canvas.</summary>
    public LayerViewport Viewport {get;set;} = new();
    /// <summary>Source fitting performed before zoom.</summary>
    public string Fit {get;set;} = "contain";
    /// <summary>Independent typed property tracks evaluated in local layer time.</summary>
    public List<PropertyAnimation> Animations {get;set;} = [];
    /// <summary>Media or text discriminator; groups are added by the transition layer.</summary>
    public string Kind {get;set;} = "media";
    /// <summary>Ordered child identities; children render only inside their single owning group.</summary>
    public List<string> Members {get;set;} = [];
    /// <summary>Optional timed text payload independent of source images.</summary>
    public CompositionText Text {get;set;}
    /// <summary>Optional source coverage mask.</summary>
    public MediaMask Mask {get;set;}
    /// <summary>Color of the fitted viewport's padding before layer opacity is applied.</summary>
    public string PaddingColor {get;set;} = "#00000000";
    /// <summary>Clips transformed content to its reserved presentation region.</summary>
    public bool ClipToViewport {get;set;} = true;
    /// <summary>Lets a video layer outlast its source interval by holding the last source frame, e.g. under a transition overlap without handles.</summary>
    public bool HoldLastFrame {get;set;}
}
