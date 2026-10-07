namespace helengine.media;
/// <summary>Declares a normalized source mask applied before alpha-over composition.</summary>
public sealed class MediaMask {
    /// <summary>Media providing a static or timed mask texture.</summary>
    [JsonRequired] public string MediaId {get;set;} = "";
    /// <summary>Raw alpha or encoded grayscale luminance channel used as coverage.</summary>
    public string Channel {get;set;} = "alpha";
    /// <summary>Whether zero and full mask coverage should be exchanged.</summary>
    public bool Inverted {get;set;}
}
