namespace helengine.media;
/// <summary>Immutable playback state associated with one seek generation.</summary>
public sealed class PlaybackSnapshot {
    /// <summary>Exact current composition instant.</summary>
    public MediaTime Time {get;}
    /// <summary>Whether the stream is currently advancing.</summary>
    public bool Playing {get;}
    /// <summary>Identity used to discard stale callbacks or pictures.</summary>
    public long Generation {get;}
    /// <summary>Constructs an immutable state value.</summary>
    public PlaybackSnapshot(MediaTime time,bool playing,long generation) {Time=time;Playing=playing;Generation=generation;}
}
