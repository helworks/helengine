namespace helengine.media;
/// <summary>Immutable selection of a layer and its independently evaluated frame properties.</summary>
public sealed class LayerEvaluation {
    /// <summary>Stores the authored layer reference, exact timing and a separate evaluated transform.</summary>
    public LayerEvaluation(VisualLayer layer,MediaTime localTime,MediaTime sourceTime,LayerTransform transform) {Layer=layer;LocalTime=localTime;SourceTime=sourceTime;Transform=transform;}
    /// <summary>Authored layer selected by the timeline.</summary>
    public VisualLayer Layer {get;}
    /// <summary>Exact time since this layer's start.</summary>
    public MediaTime LocalTime {get;}
    /// <summary>Exact requested time within the resolved source.</summary>
    public MediaTime SourceTime {get;}
    /// <summary>Frame-local transform snapshot, independent of sibling layers.</summary>
    public LayerTransform Transform {get;}
}
