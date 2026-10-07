using System.Globalization;
using System.Text.Json;
using helengine.vfx;
using helengine.vfx.effects;
namespace helengine.media.windows;
/// <summary>Publishes the base media operations and existing registered GPU shader effects.</summary>
public static class WindowsMediaCapabilities {
    /// <summary>Effect implementations whose package-owned shaders can be used by compositions.</summary>
    public static IReadOnlyList<IVfxEffect> Effects {get;} = [new RainbowExpandEffect(),new RainbowAuraEffect(),new DepthCompositeEffect()];
    /// <summary>Builds a typed executable catalog from the effects' own parameter declarations.</summary>
    public static MediaCapabilities Describe() {
        var catalog=MediaCapabilities.Basic();
        foreach(var effect in Effects) {
            var descriptor=new MediaEffectDescriptor {Id=effect.Id,Version=1,Category="layer",MainInputRole=effect.InputRoles[0],InputRoles=effect.InputRoles.ToList(),AlphaRequiredInputRoles=effect.AlphaRequiredInputRoles.ToList()};
            foreach(var parameter in effect.Parameters) {descriptor.Parameters.Add(parameter.Name,Parameter(parameter));}
            catalog.Effects.Add(descriptor);
        }
        return catalog;
    }
    /// <summary>Maps declared parameters to finite data shapes without exposing CLI syntax.</summary>
    static MediaParameterDescriptor Parameter(VfxEffectParameterDescriptor parameter) {
        var result=new MediaParameterDescriptor {Description=parameter.Description};
        if(parameter.Type==VfxParameterType.Color) {result.Type="color";result.DefaultValue=JsonSerializer.SerializeToElement(parameter.DefaultValueText.Split(',').Select(value=>double.Parse(value,CultureInfo.InvariantCulture)).ToArray());}
        else if(parameter.Type==VfxParameterType.Int && parameter.Name=="Easing") {result.Type="enum";result.AllowedValues=Enum.GetNames<VfxEasingKind>().ToList();result.DefaultValue=JsonSerializer.SerializeToElement(parameter.DefaultValueText);}
        else {result.Type=parameter.Type==VfxParameterType.Int?"integer":"number";result.DefaultValue=JsonSerializer.SerializeToElement(double.Parse(parameter.DefaultValueText,CultureInfo.InvariantCulture));if(parameter.Name.Contains("Scale",StringComparison.Ordinal)) {result.Minimum=.01;result.Maximum=16;}}
        return result;
    }
}
