namespace helengine.media;
/// <summary>Evaluates one typed property independently of query order or previous frames.</summary>
public sealed class PropertyAnimation {
    /// <summary>Allowed catalog property path on the layer transform.</summary>
    [JsonRequired] public string Property {get;set;} = "";
    /// <summary>Strictly ordered local instants and outgoing interpolation curves.</summary>
    [JsonRequired] public List<AnimationKeyframe> Keyframes {get;set;} = [];
    /// <summary>Returns the interpolated value, holding endpoint values outside the keyframe span.</summary>
    public double Evaluate(MediaTime localTime) {
        if(Keyframes==null || Keyframes.Count==0) {throw new InvalidDataException("Animation needs keyframes.");}
        if(localTime<=Keyframes[0].Time) {return Keyframes[0].Value;}
        for(int index=1;index<Keyframes.Count;index++) {
            var to=Keyframes[index];var from=Keyframes[index-1];
            if(to.Time<=from.Time) {throw new InvalidDataException("Animation keyframes must be strictly ordered.");}
            if(localTime<=to.Time) {double progress=(localTime-from.Time).ToSeconds()/(to.Time-from.Time).ToSeconds();return from.Value+(to.Value-from.Value)*MediaCurve.Evaluate(from.Curve,progress);}
        }
        return Keyframes[^1].Value;
    }
}
