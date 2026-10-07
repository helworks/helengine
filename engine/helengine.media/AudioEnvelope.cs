namespace helengine.media;
/// <summary>Sample-evaluated gain envelope in local audio-item time.</summary>
public sealed class AudioEnvelope {
    /// <summary>Linear fade or an explicit equal-power crossfade side.</summary>
    public string Type {get;set;} = "linear";
    /// <summary>Local first envelope instant.</summary>
    public MediaTime Start {get;set;} = MediaTime.Zero;
    /// <summary>Positive envelope duration.</summary>
    public MediaTime Duration {get;set;} = MediaTime.Zero;
    /// <summary>Linear fade's initial gain.</summary>
    public double From {get;set;} = 1;
    /// <summary>Linear fade's final gain.</summary>
    public double To {get;set;} = 1;
    /// <summary>Checks finite values and a valid local interval.</summary>
    public bool IsValid(MediaTime clipDuration) => Start.Denominator>0 && Duration.Denominator>0 && Start>=MediaTime.Zero && Duration>MediaTime.Zero && Start+Duration<=clipDuration && double.IsFinite(From+To) && From>=0 && To>=0 && From<=16 && To<=16 && Type is "linear" or "equal_power_in" or "equal_power_out";
    /// <summary>Evaluates a deterministic per-sample gain with held endpoint values.</summary>
    public double At(MediaTime localTime) {
        if(Duration<=MediaTime.Zero) {throw new InvalidDataException("Audio envelope requires positive duration.");}
        double progress=Math.Clamp((localTime-Start).ToSeconds()/Duration.ToSeconds(),0,1);
        return Type switch {"linear"=>From+(To-From)*progress,"equal_power_in"=>Math.Sin(progress*Math.PI/2),"equal_power_out"=>Math.Cos(progress*Math.PI/2),_=>throw new InvalidDataException("Unknown audio envelope type.")};
    }
}
