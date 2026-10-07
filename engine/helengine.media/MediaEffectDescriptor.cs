namespace helengine.media;
/// <summary>Describes a registered effect version at the contract boundary.</summary>
public sealed class MediaEffectDescriptor {
    /// <summary>Public stable effect identity.</summary>
    public string Id { get; set; } = "";
    /// <summary>Positive supported effect version.</summary>
    public int Version { get; set; }
    /// <summary>Layer, mask or transition usage category.</summary>
    public string Category {get;set;} = "layer";
    /// <summary>Input roles required by this effect; Source is the current layer frame.</summary>
    public List<string> InputRoles {get;set;} = [];
    /// <summary>Roles that require real stored source alpha, not synthesized JPEG opacity.</summary>
    public List<string> AlphaRequiredInputRoles {get;set;} = [];
    /// <summary>Named data-only parameter descriptors.</summary>
    public Dictionary<string,MediaParameterDescriptor> Parameters {get;set;} = new(StringComparer.Ordinal);
    /// <summary>Role receiving the current layer source rather than an additional binding.</summary>
    public string MainInputRole {get;set;} = "Source";
}
