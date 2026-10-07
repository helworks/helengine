namespace helengine.media;
/// <summary>Versioned effect capabilities supplied to compilation and validation.</summary>
public sealed class MediaCapabilities {
    /// <summary>Supported effect versions, including composition-owned base operations.</summary>
    public List<MediaEffectDescriptor> Effects { get; set; } = [];
    /// <summary>Checks an exact effect version rather than silently substituting a newer one.</summary>
    public bool Supports(string id,int version) => version>0 && Effects.Any(e=>e.Id==id && e.Version==version);
    /// <summary>Declares the initial composition operations; GPU passes implement these later.</summary>
    public static MediaCapabilities Basic() => new() {Effects=[new(){Id="transform.2d",Version=1},new(){Id="crossfade",Version=1},new(){Id="mask.alpha",Version=1}]};
}
