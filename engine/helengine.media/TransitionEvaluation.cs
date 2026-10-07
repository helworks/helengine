namespace helengine.media;
/// <summary>Pure normalized transition evaluation independent of audio and playback state.</summary>
public sealed class TransitionEvaluation {
    /// <summary>Pinned visual transition being evaluated.</summary>
    public CompositionTransition Transition {get;}
    /// <summary>Clamped normalized overlap position.</summary>
    public double Progress {get;}
    /// <summary>Retains the transition and its normalized progress.</summary>
    TransitionEvaluation(CompositionTransition transition,double progress) {Transition=transition;Progress=progress;}
    /// <summary>Evaluates exact rational time before converting the bounded ratio to floating point.</summary>
    public static TransitionEvaluation At(CompositionTransition transition,MediaTime time) {
        ArgumentNullException.ThrowIfNull(transition);time.Validate();transition.Start.Validate();transition.Duration.Validate();
        if(transition.Duration<=MediaTime.Zero) {throw new InvalidDataException("Transition duration must be positive.");}
        var progress=(time-transition.Start)/transition.Duration;return new(transition,Math.Clamp(progress.ToSeconds(),0,1));
    }
}
