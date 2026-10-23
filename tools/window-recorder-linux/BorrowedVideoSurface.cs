using helengine.media;

namespace helengine.tools.windowrecorderlinux;

/// <summary>Retimes a retained capture for a repeated output slot without transferring or duplicating its pixel allocation.</summary>
sealed class BorrowedVideoSurface : IVideoSurface {
    /// <summary>Capture-owned immutable pixels, retained until the sequential encoder write has returned.</summary>
    readonly IVideoSurface Source;
    /// <summary>Borrows the caller's retained frame for one awaited encode operation.</summary>
    public BorrowedVideoSurface(IVideoSurface source) { Source = source ?? throw new ArgumentNullException(nameof(source)); }
    /// <summary>Pixel width inherited from the capture.</summary>
    public int Width => Source.Width;
    /// <summary>Pixel height inherited from the capture.</summary>
    public int Height => Source.Height;
    /// <summary>Reads the same immutable CPU RGBA pixels for an explicitly repeated output slot.</summary>
    public ReadOnlyMemory<byte> ReadRgba() => Source.ReadRgba();
    /// <summary>Leaves release to the owning capture frame after its last sequential write.</summary>
    public void Dispose() { }
}
