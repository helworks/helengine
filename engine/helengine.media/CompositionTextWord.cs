namespace helengine.media;
/// <summary>Preserves real word timing for highlighted caption styles.</summary>
public sealed class CompositionTextWord {
    /// <summary>Authored word including punctuation.</summary>
    [JsonRequired] public string Text {get;set;} = "";
    /// <summary>Composition-relative first word instant.</summary>
    [JsonRequired] public MediaTime Start {get;set;} = MediaTime.Zero;
    /// <summary>Composition-relative exclusive last word instant.</summary>
    [JsonRequired] public MediaTime End {get;set;} = MediaTime.Zero;
}
