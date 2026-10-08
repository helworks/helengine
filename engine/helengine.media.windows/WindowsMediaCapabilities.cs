using System.Text.Json;
namespace helengine.media.windows;
/// <summary>Publishes the base media operations plus every effect of a <see cref="VfxEffectCatalog"/>.</summary>
public static class WindowsMediaCapabilities {
    /// <summary>Builds the executable catalog for the engine's built-in effects only.</summary>
    public static MediaCapabilities Describe() => Describe(VfxEffectCatalog.CreateBuiltIn());
    /// <summary>Builds a typed executable catalog from each effect asset's own input and parameter declarations.</summary>
    /// <param name="effects">Effects compositions may reference.</param>
    public static MediaCapabilities Describe(VfxEffectCatalog effects) {
        var catalog=MediaCapabilities.Basic();
        foreach(var entry in effects.All) {
            var effect=entry.Effect;
            var descriptor=new MediaEffectDescriptor {Id=effect.EffectId,Version=effect.EffectVersion,Category="layer",MainInputRole=effect.Inputs[0].Name,InputRoles=effect.Inputs.Select(input=>input.Name).ToList(),AlphaRequiredInputRoles=effect.Inputs.Where(input=>input.RequiresAlpha).Select(input=>input.Name).ToList()};
            foreach(var parameter in effect.Parameters) {descriptor.Parameters.Add(parameter.Name,Parameter(parameter));}
            catalog.Effects.Add(descriptor);
        }
        return catalog;
    }
    /// <summary>Maps one declared parameter to its JSON data shape, range and default.</summary>
    static MediaParameterDescriptor Parameter(EffectParameterAsset parameter) {
        var result=new MediaParameterDescriptor {Description=parameter.Description,Minimum=parameter.Minimum,Maximum=parameter.Maximum};
        float4 value=parameter.DefaultValue;
        if(parameter.Type==EffectParameterType.Enum) {result.Type="enum";result.AllowedValues=parameter.AllowedValues.ToList();result.DefaultValue=JsonSerializer.SerializeToElement(parameter.AllowedValues[(int)value.X]);}
        else if(parameter.Type==EffectParameterType.Bool) {result.Type="boolean";result.DefaultValue=JsonSerializer.SerializeToElement(value.X!=0);}
        else if(parameter.Type==EffectParameterType.Color) {result.Type="color";result.Minimum=0;result.Maximum=1;result.DefaultValue=JsonSerializer.SerializeToElement(value.W==1?new double[]{value.X,value.Y,value.Z}:new double[]{value.X,value.Y,value.Z,value.W});}
        else if(parameter.Type==EffectParameterType.Float2) {result.Type="float2";result.DefaultValue=JsonSerializer.SerializeToElement(new double[]{value.X,value.Y});}
        else if(parameter.Type==EffectParameterType.Float4) {result.Type="float4";result.DefaultValue=JsonSerializer.SerializeToElement(new double[]{value.X,value.Y,value.Z,value.W});}
        else {result.Type=parameter.Type==EffectParameterType.Integer?"integer":"number";result.DefaultValue=JsonSerializer.SerializeToElement((double)value.X);}
        return result;
    }
}
