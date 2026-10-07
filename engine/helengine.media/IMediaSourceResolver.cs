namespace helengine.media;
/// <summary>Creates source cursors after verifying identity and filesystem ownership.</summary>
public interface IMediaSourceResolver : IDisposable {
    /// <summary>Opens an independent owned cursor matching the pinned source reference.</summary>
    IMediaSource Open(MediaReference reference);
}
