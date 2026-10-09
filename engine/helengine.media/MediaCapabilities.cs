namespace helengine.media;
/// <summary>Versioned effect capabilities supplied to compilation and validation.</summary>
public sealed class MediaCapabilities {
    /// <summary>Supported effect versions, including composition-owned base operations.</summary>
    public List<MediaEffectDescriptor> Effects { get; set; } = [];
    /// <summary>Checks an exact effect version rather than silently substituting a newer one.</summary>
    public bool Supports(string id,int version) => version>0 && Effects.Any(e=>e.Id==id && e.Version==version);
    /// <summary>Declares the initial composition operations; GPU passes implement these later.</summary>
    public static MediaCapabilities Basic() => new() {Effects=[new(){Id="transform.2d",Version=1},new(){Id="crossfade",Version=1,Category="transition"},new(){Id="mask.alpha",Version=1,Category="mask"}]};
    /// <summary>Versioned machine-readable capability document identity.</summary>
    public string Schema {get;set;} = "helengine.media.capabilities.v1";
    /// <summary>Executable document schema supported by this catalog.</summary>
    public string CompositionSchema {get;set;} = "helengine.media.composition.v1";
    /// <summary>Exact named curves, preserving the editorial smoothstep behavior.</summary>
    public List<string> Curves {get;set;} = ["linear.v1","smoothstep.v1","ease_out_cubic.v1","ease_in_quad.v1","ease_out_back.v1"];
    /// <summary>Finite animation ranges used by the planner and editor.</summary>
    public Dictionary<string,MediaParameterDescriptor> LayerProperties {get;set;} = new(StringComparer.Ordinal) {
        ["position_x"]=new(){Minimum=-4,Maximum=4},["position_y"]=new(){Minimum=-4,Maximum=4},
        ["scale_x"]=new(){Minimum=.01,Maximum=16,DefaultValue=JsonSerializer.SerializeToElement(1)},["scale_y"]=new(){Minimum=.01,Maximum=16,DefaultValue=JsonSerializer.SerializeToElement(1)},
        ["rotation_deg"]=new(){Minimum=-36000,Maximum=36000},["opacity"]=new(){Minimum=0,Maximum=1,DefaultValue=JsonSerializer.SerializeToElement(1)},
        ["zoom"]=new(){Minimum=1,Maximum=2.5,DefaultValue=JsonSerializer.SerializeToElement(1)},["focus_x"]=new(){Minimum=0,Maximum=1,DefaultValue=JsonSerializer.SerializeToElement(.5)},["focus_y"]=new(){Minimum=0,Maximum=1,DefaultValue=JsonSerializer.SerializeToElement(.5)}
    };
    /// <summary>Kinetic typography templates edits may expand into text layers, with their slots, typed parameters and layouts.</summary>
    public List<MediaGraphicTemplateDescriptor> GraphicTemplates {get;set;} = [];
    /// <summary>Scene arrangements edits may declare, with the region names and roles their layers and overlays can claim.</summary>
    public List<MediaArrangementDescriptor> Arrangements {get;set;} = [];
    /// <summary>Publishes the current exact catalog without paths, commands or shader code.</summary>
    public JsonElement Describe() => JsonSerializer.SerializeToElement(this,new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.SnakeCaseLower});
}
