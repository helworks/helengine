namespace helengine.media;
/// <summary>Caption cues and a portable Helengine caption-style snapshot.</summary>
public sealed class CompositionText {
    /// <summary>Absolute timed cues used by both preview and export.</summary>
    [JsonRequired] public List<CompositionTextCue> Cues {get;set;} = [];
    /// <summary>Caption preset data; file paths are resolved by the trusted host.</summary>
    public JsonElement Style {get;set;} = JsonSerializer.SerializeToElement(new Dictionary<string,object>());
}
