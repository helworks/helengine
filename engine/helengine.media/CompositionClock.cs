namespace helengine.media;
/// <summary>Maps output frame and sample indices directly to exact timeline instants.</summary>
public sealed class CompositionClock {
    /// <summary>Validates the output rate, sample rate and positive duration once.</summary>
    public CompositionClock(MediaTime frameRate,int sampleRate,MediaTime duration) {
        if (frameRate<=MediaTime.Zero || frameRate>new MediaTime(240,1)) { throw new ArgumentOutOfRangeException(nameof(frameRate)); }
        if (sampleRate<8000 || sampleRate>192000) { throw new ArgumentOutOfRangeException(nameof(sampleRate)); }
        if (duration<=MediaTime.Zero) { throw new ArgumentOutOfRangeException(nameof(duration)); }
        FrameRate=frameRate; SampleRate=sampleRate; Duration=duration;
    }
    /// <summary>Exact output frames per second.</summary>
    public MediaTime FrameRate { get; }
    /// <summary>Output sample frames per second.</summary>
    public int SampleRate { get; }
    /// <summary>Declared timeline duration, independent of rendering speed.</summary>
    public MediaTime Duration { get; }
    /// <summary>Number of frames whose start instant precedes the declared end.</summary>
    public long FrameCount => (Duration*FrameRate).Ceiling();
    /// <summary>Number of sample frames whose start instant precedes the declared end.</summary>
    public long SampleCount => (Duration*new MediaTime(SampleRate,1)).Ceiling();
    /// <summary>Returns any nonnegative frame's absolute timeline instant without accumulation.</summary>
    public MediaTime FrameTime(long frameIndex) {
        if (frameIndex<0) { throw new ArgumentOutOfRangeException(nameof(frameIndex)); }
        return new MediaTime(frameIndex,1)/FrameRate;
    }
    /// <summary>Returns any nonnegative sample frame's exact absolute timeline instant.</summary>
    public MediaTime SampleTime(long sampleIndex) {
        if (sampleIndex<0) { throw new ArgumentOutOfRangeException(nameof(sampleIndex)); }
        return new(sampleIndex,SampleRate);
    }
}
