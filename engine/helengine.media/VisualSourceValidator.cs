namespace helengine.media;
/// <summary>Checks executable visual kinds and timed auxiliary input handles without touching files or devices.</summary>
public static class VisualSourceValidator {
    /// <summary>Rejects audio-only picture/mask/effect inputs and implicit timed-input freezing.</summary>
    public static IReadOnlyList<CompositionDiagnostic> Validate(CompositionDocument document) {
        var errors=new List<CompositionDiagnostic>();var media=document.Media.Where(item=>item!=null && item.Id!=null).GroupBy(item=>item.Id).ToDictionary(group=>group.Key,group=>group.First());
        foreach(var layer in document.Layers.Where(item=>item!=null)) {
            if(layer.Kind=="media" && layer.MediaId!=null && media.TryGetValue(layer.MediaId,out var picture) && picture.Kind is not("image" or "video")) {errors.Add(new(){Code="invalid_visual_source",Path=layer.Id,Message="Picture requires an image or video source."});}
            var inputs=new List<string>();if(layer.Mask?.MediaId!=null) {inputs.Add(layer.Mask.MediaId);}if(layer.Effects!=null) {foreach(var effect in layer.Effects.Where(item=>item?.Inputs!=null)) {inputs.AddRange(effect.Inputs.Values);}}
            foreach(string id in inputs.Where(id=>id!=null).Distinct()) {if(!media.TryGetValue(id,out var input)) {continue;}if(input.Kind is not("image" or "video")) {errors.Add(new(){Code="invalid_visual_source",Path=layer.Id,Message="Mask and visual effect inputs require image or video sources."});continue;}if(input.Kind=="video" && input.Duration.Denominator>0 && layer.SourceIn.Denominator>0 && layer.Start.Denominator>0 && layer.End.Denominator>0 && (layer.SourceIn<MediaTime.Zero || layer.SourceIn+layer.End-layer.Start>input.Duration)) {errors.Add(new(){Code="visual_input_handles",Path=layer.Id,Message="Timed visual input must cover the complete requested source interval."});}}
        }return errors;
    }
}
