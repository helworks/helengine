namespace helengine.media;
/// <summary>Declares a timed visual transition between independently evaluated layers.</summary>
public sealed class CompositionTransition {
    /// <summary>Stable transition identity.</summary>
    [JsonRequired]
    public string Id { get; set; } = "";
    /// <summary>Outgoing layer or group identity.</summary>
    [JsonRequired]
    public string FromLayer { get; set; } = "";
    /// <summary>Incoming layer or group identity.</summary>
    [JsonRequired]
    public string ToLayer { get; set; } = "";
    /// <summary>Inclusive first transition instant.</summary>
    [JsonRequired]
    public MediaTime Start { get; set; } = MediaTime.Zero;
    /// <summary>Strictly positive transition interval.</summary>
    [JsonRequired]
    public MediaTime Duration { get; set; } = MediaTime.Zero;
    /// <summary>Registered transition implementation.</summary>
    [JsonRequired]
    public string EffectId { get; set; } = "crossfade";
    /// <summary>Typed parameters pinned to the transition implementation.</summary>
    public Dictionary<string,JsonElement> Parameters {get;set;} = new(StringComparer.Ordinal);
    /// <summary>Pinned transition implementation version.</summary>
    [JsonRequired]
    public int EffectVersion { get; set; } = 1;
}
