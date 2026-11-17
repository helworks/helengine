using helengine.painting;

namespace helengine {
    /// <summary>Owns an immutable GPU revision independently of later surface edits, suspension or disposal.</summary>
    public interface IPaintSurfaceSnapshot : IDisposable {
        /// <summary>Creates an owned lease of this same immutable GPU resource without copying pixels; the last lease retires its texture.</summary>
        [NativeOwnedReturn]
        IPaintSurfaceSnapshot Retain();
        /// <summary>Gets the exact committed session revision requested when this snapshot was acquired.</summary>
        long Revision { get; }
        /// <summary>Polls the native copy without blocking, reading pixels or presenting an unfinished texture.</summary>
        PaintSubmissionStatus Status { get; }
        /// <summary>Gets the borrowed ready premultiplied RuntimeTexture; callers must retain this snapshot until they finish using its texture.</summary>
        [NativeBorrowedReturn]
        RuntimeTexture Texture { get; }
        /// <summary>Gets packed RGBA8 level-zero allocation bytes, excluding mip levels and driver overhead.</summary>
        long BaseBytes { get; }
        /// <summary>Gets packed bytes allocated for mip levels beyond level zero, excluding driver overhead.</summary>
        long MipmapBytes { get; }
    }
}
