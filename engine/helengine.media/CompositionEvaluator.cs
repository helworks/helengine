namespace helengine.media;
/// <summary>Selects active layers and evaluates local animations without maintaining playback state.</summary>
public static class CompositionEvaluator {
    /// <summary>Returns stable back-to-front active layers at an exact composition instant.</summary>
    public static IReadOnlyList<LayerEvaluation> Evaluate(CompositionDocument document,MediaTime time) {
        ArgumentNullException.ThrowIfNull(document);time.Validate();
        if(time<MediaTime.Zero) {throw new ArgumentOutOfRangeException(nameof(time));}
        var result=new List<LayerEvaluation>();
        foreach(var layer in document.Layers.OrderBy(layer=>layer.Order)) {
            if(!MediaTime.InInterval(time,layer.Start,layer.End)) {continue;}
            var local=time-layer.Start;var transform=layer.Transform.Copy();
            foreach(var animation in layer.Animations) {transform.Set(animation.Property,animation.Evaluate(local));}
            var source=layer.SourceIn+local;
            if(layer.HoldLastFrame && document.FrameRate.Numerator>0) {var last=layer.SourceOut-new MediaTime(document.FrameRate.Denominator,document.FrameRate.Numerator);if(last<layer.SourceIn) {last=layer.SourceIn;}if(source>last) {source=last;}}
            result.Add(new(layer,local,source,transform));
        }
        return result;
    }
}
