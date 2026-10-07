namespace helengine.media;
/// <summary>Validates executable transform and animation properties before rendering.</summary>
public static class AnimationValidator {
    /// <summary>Checks finite property ranges, ordered keyframes and exact named curves.</summary>
    public static IReadOnlyList<CompositionDiagnostic> Validate(VisualLayer layer,string path) {
        var errors=new List<CompositionDiagnostic>();
        if(layer.Transform==null || !layer.Transform.IsValid() || layer.Viewport==null || !layer.Viewport.IsValid() || layer.Fit is not ("contain" or "cover")) {errors.Add(new(){Code="invalid_transform",Path=path,Message="Layer presentation or transform is invalid."});}
        if(layer.Animations==null || layer.Animations.Count>32) {errors.Add(new(){Code="invalid_animation",Path=path,Message="Layer animation collection is absent or exceeds limits."});return errors;}
        var properties=new HashSet<string>(StringComparer.Ordinal);
        foreach(var animation in layer.Animations) {
            bool valid=animation!=null && properties.Add(animation.Property ?? "") && animation.Keyframes!=null && animation.Keyframes.Count>0 && animation.Keyframes.Count<=8192;
            if(valid) {
                MediaTime previous=MediaTime.Zero;bool first=true;
                foreach(var keyframe in animation.Keyframes) {
                    if(keyframe==null || keyframe.Time.Denominator<=0 || keyframe.Time<MediaTime.Zero || (!first && keyframe.Time<=previous) || keyframe.Time>layer.End-layer.Start || !LayerTransform.IsValid(animation.Property,keyframe.Value) || !MediaCurve.Supports(keyframe.Curve)) {valid=false;break;}
                    previous=keyframe.Time;first=false;
                }
            }
            if(!valid) {errors.Add(new(){Code="invalid_animation",Path=path+".animations",Message="Animation needs an allowed property, finite values and strictly ordered local keyframes."});}
        }
        return errors;
    }
}
