namespace helengine.media;
/// <summary>Describes a registered effect version at the contract boundary.</summary>
public sealed class MediaEffectDescriptor {
    /// <summary>Public stable effect identity.</summary>
    public string Id { get; set; } = "";
    /// <summary>Positive supported effect version.</summary>
    public int Version { get; set; }
}
