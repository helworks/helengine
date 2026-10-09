namespace helengine.media.windows;
/// <summary>Publishes the base media operations plus every effect of a <see cref="VfxEffectCatalog"/>, the built-in scene arrangements and overlay timeline support.</summary>
public static class WindowsMediaCapabilities {
    /// <summary>Builds the executable catalog for the engine's built-in effects only.</summary>
    public static MediaCapabilities Describe() => Describe(VfxEffectCatalog.CreateBuiltIn());
    /// <summary>Builds the executable catalog for the given effects plus the given graphic templates.</summary>
    /// <param name="effects">Effects compositions may reference.</param>
    /// <param name="graphics">Graphic templates edits may expand.</param>
    public static MediaCapabilities Describe(VfxEffectCatalog effects,helengine.video.GraphicTemplateCatalog graphics) {
        var catalog=Describe(effects);
        (graphics ?? throw new ArgumentNullException(nameof(graphics))).Publish(catalog);
        return catalog;
    }
    /// <summary>Builds a typed executable catalog from each effect asset's own input and parameter declarations, plus the built-in scene arrangements and overlay timeline support.</summary>
    /// <param name="effects">Effects compositions may reference.</param>
    public static MediaCapabilities Describe(VfxEffectCatalog effects) {
        var catalog=MediaCapabilities.Basic();
        foreach(var entry in effects.All) {
            var effect=entry.Effect;
            var descriptor=new MediaEffectDescriptor {Id=effect.EffectId,Version=effect.EffectVersion,Category=effect.Category==EffectCategory.Transition?"transition":"layer",MainInputRole=effect.Inputs[0].Name,InputRoles=effect.Inputs.Select(input=>input.Name).ToList(),AlphaRequiredInputRoles=effect.Inputs.Where(input=>input.RequiresAlpha).Select(input=>input.Name).ToList()};
            foreach(var parameter in effect.Parameters) {descriptor.Parameters.Add(parameter.Name,helengine.video.VideoParameterDescriptors.Describe(parameter));}
            catalog.Effects.Add(descriptor);
        }
        helengine.video.VideoArrangementPresets.Publish(catalog);
        helengine.video.VideoTimelineCapabilities.Publish(catalog);
        return catalog;
    }
}
