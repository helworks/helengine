namespace helengine.media;
/// <summary>Selects an engine-owned effect and supplies data-only parameters.</summary>
public sealed class MediaEffect {
    /// <summary>Registered effect identity.</summary>
    [JsonRequired]
    public string Id { get; set; } = "";
    /// <summary>Pinned positive implementation version.</summary>
    [JsonRequired]
    public int Version { get; set; }
    /// <summary>Named typed parameters validated against the catalog.</summary>
    [JsonRequired]
    public Dictionary<string,JsonElement> Parameters { get; set; } = new(StringComparer.Ordinal);
    /// <summary>Additional source references bound to registered roles without exposing shader registers.</summary>
    public Dictionary<string,string> Inputs {get;set;} = new(StringComparer.Ordinal);
}
