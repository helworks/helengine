namespace helengine.media;
/// <summary>Timed authored text independent of image-layer transformation.</summary>
public sealed class CompositionTextCue {
    /// <summary>Text displayed while this cue is active.</summary>
    [JsonRequired] public string Text {get;set;} = "";
    /// <summary>Inclusive absolute composition start.</summary>
    [JsonRequired] public MediaTime Start {get;set;} = MediaTime.Zero;
    /// <summary>Exclusive absolute composition end.</summary>
    [JsonRequired] public MediaTime End {get;set;} = MediaTime.Zero;
    /// <summary>Optional real word timings, never inferred by the renderer.</summary>
    public List<CompositionTextWord> Words {get;set;} = [];
}
