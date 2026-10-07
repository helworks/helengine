namespace helengine.media;
/// <summary>Checks registered layer-effect parameters and role bindings before opening any source.</summary>
public static class EffectInputValidator {
    /// <summary>Collects unsupported effects, untyped parameters and unresolved required input roles.</summary>
    public static IReadOnlyList<CompositionDiagnostic> Validate(List<MediaEffect> effects,MediaCapabilities capabilities,HashSet<string> media,string path) {
        var errors=new List<CompositionDiagnostic>();
        foreach(var effect in effects) {
            var descriptor=effect==null?null:capabilities.Effects.SingleOrDefault(item=>item.Id==effect.Id && item.Version==effect.Version);
            if(descriptor==null || descriptor.Version<=0 || descriptor.Category!="layer") {errors.Add(new(){Code="unknown_effect",Path=path,Message="Effect version or usage is unsupported."});continue;}
            if(effect.Parameters==null || effect.Inputs==null || effect.Inputs.Count>32 || effect.Parameters.Count>64) {errors.Add(new(){Code="invalid_effect",Path=path,Message="Effect input and parameter collections are invalid."});continue;}
            foreach(var parameter in effect.Parameters) {if(!descriptor.Parameters.TryGetValue(parameter.Key,out var shape) || !shape.Accepts(parameter.Value)) {errors.Add(new(){Code="invalid_effect_parameter",Path=path,Message="Effect parameter is absent from its catalog or outside its data range: "+parameter.Key});}}
            foreach(var role in descriptor.InputRoles.Where(role=>role!=descriptor.MainInputRole)) {if(!effect.Inputs.TryGetValue(role,out var id) || !media.Contains(id)) {errors.Add(new(){Code="missing_effect_input",Path=path,Message="Effect requires an owned media binding for role "+role});}}
            foreach(var binding in effect.Inputs) {if(binding.Key==descriptor.MainInputRole || !descriptor.InputRoles.Contains(binding.Key) || !media.Contains(binding.Value)) {errors.Add(new(){Code="invalid_effect_input",Path=path,Message="Effect input role is unknown or unresolved."});}}
        }
        return errors;
    }
}
