using helengine.painting;

namespace helengine {
    /// <summary>Owns one mutable GPU surface while snapshots independently retain completed immutable content.</summary>
    public interface IPaintSurface : IDisposable {
        /// <summary>Gets whether the mutable GPU target is active; a suspended session must explicitly reconfigure and replay before drawing.</summary>
        bool IsActive { get; }
        /// <summary>Gets copied CPU stamp/uniform/clip payload bytes waiting for submission, independent of target pixels.</summary>
        long PendingUploadBytes { get; }
        /// <summary>Gets copied stamp/uniform/clip payload bytes retained by pending native GPU submissions, excluding target pixels.</summary>
        long InFlightUploadBytes { get; }
        /// <summary>Gets the borrowed raster provider used by the existing PaintSession; its borrow ends when this surface is disposed.</summary>
        [NativeBorrowedReturn]
        IPaintSurfaceBackend Backend { get; }
        /// <summary>Submits queued paint on the host's ordered queue and borrows its mutable premultiplied preview, or returns null on saturation. Never use this transient texture as a committed snapshot.</summary>
        /// <remarks>The borrow ends on suspension, reconfiguration or disposal; queued native frames retain their resource until safe retirement.</remarks>
        [NativeBorrowedReturn]
        RuntimeTexture GetPreviewTexture();
        /// <summary>Queues an exact-revision owned snapshot, or returns null under GPU saturation so the caller can explicitly retry.</summary>
        [NativeOwnedReturn]
        IPaintSurfaceSnapshot AcquireSnapshot(long revision, bool mipmapped);
    }
}
