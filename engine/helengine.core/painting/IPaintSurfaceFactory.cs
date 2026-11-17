using helengine.painting;

namespace helengine {
    /// <summary>Host-owned factory connecting portable painting sessions to the active renderer and its device.</summary>
    public interface IPaintSurfaceFactory {
        /// <summary>Gets packed bytes of retired painting textures still retained by queued normal renderer frames, excluding driver overhead.</summary>
        long FrameRetainedSnapshotBytes { get; }
        /// <summary>Gets packed level-zero bytes of all live immutable resources, counting retained lease aliases once.</summary>
        long SnapshotBaseBytes { get; }
        /// <summary>Gets packed additional mip bytes of all live immutable resources, counting retained lease aliases once.</summary>
        long SnapshotMipmapBytes { get; }
        /// <summary>Creates an owned surface; canvas-clipped normalized triangles are copied and an empty array masks every pixel.</summary>
        [NativeOwnedReturn]
        IPaintSurface Create([NativeNoEscape] PaintCanvasDescriptor canvas, [NativeNoEscape] float[] clipTriangles);
        /// <summary>Creates a renderer-registered two-sided paper material with explicit base and premultiplied-overlay bindings.</summary>
        [NativeOwnedReturn]
        RuntimeMaterial CreateMaterial();
        /// <summary>Creates a canonical lowercase stroke UUID from the host OS identity generator, independent of runtime Guid emulation.</summary>
        string CreateStrokeId();
        /// <summary>Binds copied paper appearance and borrowed base/overlay textures to one material instance from this factory.</summary>
        void BindMaterial([NativeNoEscape] RuntimeMaterial material, [NativeRetainsBorrow] RuntimeTexture baseImage, [NativeRetainsBorrow] RuntimeTexture overlay, byte4 baseColor, int printedFrontSign, float overlayUvScaleX, float overlayUvScaleY);
    }
}
