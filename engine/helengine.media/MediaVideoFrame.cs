namespace helengine.media;
/// <summary>Owns one independently releasable visual surface and its exact source timing.</summary>
public sealed class MediaVideoFrame : IDisposable {
    /// <summary>Attaches an owned surface to its presentation interval.</summary>
    public MediaVideoFrame(MediaTime timestamp,MediaTime duration,IVideoSurface surface,int rotationDegrees=0) {
        timestamp.Validate();duration.Validate();Surface=surface ?? throw new ArgumentNullException(nameof(surface));Timestamp=timestamp;Duration=duration;
        if(rotationDegrees is not (0 or 90 or 180 or 270)) {throw new ArgumentOutOfRangeException(nameof(rotationDegrees));}RotationDegrees=rotationDegrees;
    }
    /// <summary>Inclusive source presentation timestamp.</summary>
    public MediaTime Timestamp {get;}
    /// <summary>Source frame interval; still images use zero.</summary>
    public MediaTime Duration {get;}
    /// <summary>Frame-owned surface; callers must release the frame.</summary>
    public IVideoSurface Surface {get;}
    /// <summary>Releases this frame's surface independently of other source cursors.</summary>
    public void Dispose() => Surface.Dispose();
    /// <summary>Source display-matrix angle, applied counterclockwise before viewport fitting.</summary>
    public int RotationDegrees {get;}
    /// <summary>Normalized display width after applying source orientation.</summary>
    public int DisplayWidth => RotationDegrees is 90 or 270 ? Surface.Height : Surface.Width;
    /// <summary>Normalized display height after applying source orientation.</summary>
    public int DisplayHeight => RotationDegrees is 90 or 270 ? Surface.Width : Surface.Height;
}
